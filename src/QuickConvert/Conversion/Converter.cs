using System.Diagnostics;
using System.IO;
using System.Linq;

namespace QuickConvert.Conversion;

public static class Converter
{
    public static string FindFfmpeg()
    {
        var local = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
        return File.Exists(local) ? local : "ffmpeg";
    }

    public static string FindAvifEnc()
    {
        var configured = Environment.GetEnvironmentVariable("QUICKCONVERT_AVIFENC");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            return configured;

        var local = Path.Combine(AppContext.BaseDirectory, "avifenc.exe");
        return File.Exists(local) ? local : "avifenc";
    }

    public static IReadOnlyList<ConversionResult> ConvertBatch(
        IReadOnlyList<string> files,
        string target,
        Quality quality,
        string? outputDirectory,
        Action<int, int, string>? progress)
    {
        var results = new List<ConversionResult>(files.Count);
        for (var i = 0; i < files.Count; i++)
        {
            progress?.Invoke(i, files.Count, Path.GetFileName(files[i]));
            results.Add(ConvertOne(files[i], target, quality, outputDirectory));
        }
        return results;
    }

    public static ConversionResult ConvertOne(string source, string target, Quality quality, string? outputDirectory)
    {
        var srcExt = FormatCatalog.NormalizeExt(Path.GetExtension(source));
        target = FormatCatalog.NormalizeExt(target);

        if (!File.Exists(source))
            return new ConversionResult(source, null, false, "File not found.");

        if (srcExt == target)
            return new ConversionResult(source, null, false, $"File is already a .{target}.");

        if (!FormatCatalog.IsSupportedTarget(target))
            return new ConversionResult(source, null, false, $"Unknown target format '.{target}'.");

        if (!FormatCatalog.IsSupportedInput(srcExt))
            return new ConversionResult(source, null, false, $"Unsupported source format '.{srcExt}'.");

        if (!FormatCatalog.CanConvert(srcExt, target))
            return new ConversionResult(source, null, false, $"Can't convert .{srcExt} to .{target}.");

        var output = GetOutputPath(source, target, outputDirectory);

        string? error;
        var ok = target == "gif" && FormatCatalog.IsVideoSource(srcExt)
            ? RunVideoToGif(source, output, out error)
            : target == "gif" && FormatCatalog.IsImageSource(srcExt)
                ? RunImageToGif(source, output, out error)
                : target == "avif" && FormatCatalog.IsImageSource(srcExt)
                    ? RunImageToAvif(source, output, quality, out error)
                    : target == "jpg" && FormatCatalog.IsImageSource(srcExt)
                        ? RunImageToJpeg(source, output, quality, out error)
                        : target == "bmp" && FormatCatalog.IsImageSource(srcExt)
                            ? RunImageToBmp(source, output, out error)
                            : RunFfmpeg(BuildCommand(source, output, FormatCatalog.BuildArgs(srcExt, target, quality)), out error);

        if (ok && WaitForOutput(output))
            return new ConversionResult(source, output, true, null);

        return new ConversionResult(source, null, false, error ?? "Converter finished without creating an output file.");
    }



    private static bool RunImageToGif(string input, string output, out string? error)
    {
        // GIF only has 1-bit transparency. Build a palette that reserves one transparent entry
        // instead of silently turning transparent PNG pixels opaque/black.
        var args = new[]
        {
            "-hide_banner", "-loglevel", "error", "-y",
            "-i", input,
            "-filter_complex", "[0:v]split[a][b];[a]palettegen=reserve_transparent=1:transparency_color=ffffff[p];[b][p]paletteuse=alpha_threshold=128",
            "-loop", "0",
            output,
        };
        return RunFfmpeg(args, out error);
    }

    private static bool RunImageToBmp(string input, string output, out string? error)
    {
        // Windows BMP alpha support is inconsistent. Flatten to white so transparent source
        // pixels never become the surprising black background produced by a plain BMP encode.
        var args = new[]
        {
            "-hide_banner", "-loglevel", "error", "-y",
            "-i", input,
            "-f", "lavfi", "-i", "color=c=white:s=2x2:r=1",
            "-filter_complex", "[1:v][0:v]scale2ref=w=rw:h=rh[bg][fg];[bg][fg]overlay=shortest=1:format=auto,format=bgr24[out]",
            "-map", "[out]",
            "-c:v", "bmp",
            "-frames:v", "1",
            output,
        };
        return RunFfmpeg(args, out error);
    }

    private static bool RunImageToJpeg(string input, string output, Quality quality, out string? error)
    {
        // JPEG has no alpha channel. Composite transparent/semi-transparent pixels onto
        // white instead of letting the encoder expose the hidden RGB values as black halos.
        // scale2ref grows the tiny generated white frame to exactly match the source size.
        var q = quality == Quality.Best ? "2" : quality == Quality.Balanced ? "4" : "7";
        var args = new[]
        {
            "-hide_banner", "-loglevel", "error", "-y",
            "-i", input,
            "-f", "lavfi", "-i", "color=c=white:s=2x2:r=1",
            "-filter_complex", "[1:v][0:v]scale2ref=w=rw:h=rh[bg][fg];[bg][fg]overlay=shortest=1:format=auto,format=yuvj444p[out]",
            "-map", "[out]",
            "-c:v", "mjpeg",
            "-q:v", q,
            "-frames:v", "1",
            output,
        };
        return RunFfmpeg(args, out error);
    }

    private static bool RunImageToAvif(string input, string output, Quality quality, out string? error)
    {
        // FFmpeg's libaom AVIF-alpha path is broken in some current Windows builds: the
        // monochrome alpha stream can fail inside libaom even though the same command works
        // on other platforms. Use libavif's official avifenc for still AVIF instead. It reads
        // PNG alpha directly and owns the AVIF alpha-item details, so transparent pixels are
        // preserved without relying on FFmpeg's two-stream AVIF encoder path.
        string? preparedPng = null;
        try
        {
            var sourceExt = FormatCatalog.NormalizeExt(Path.GetExtension(input));
            var encoderInput = input;

            // avifenc natively accepts PNG/JPEG. For the other image formats QuickConvert
            // supports, normalize the first frame through lossless PNG so alpha is retained.
            if (sourceExt is not "png" and not "jpg")
            {
                preparedPng = Path.Combine(Path.GetTempPath(), $"qc_avif_{Guid.NewGuid():N}.png");
                var prepareArgs = new[]
                {
                    "-hide_banner", "-loglevel", "error", "-y",
                    "-i", input,
                    "-frames:v", "1",
                    "-c:v", "png",
                    preparedPng,
                };
                if (!RunFfmpeg(prepareArgs, out var prepareError))
                {
                    error = $"Could not prepare image for AVIF: {prepareError}";
                    return false;
                }
                encoderInput = preparedPng;
            }

            var q = quality == Quality.Best ? "90" : quality == Quality.Balanced ? "75" : "55";
            var args = new[] { "-q", q, encoderInput, output };
            return RunExternalTool(FindAvifEnc(), args, "AVIF encoder", out error);
        }
        finally
        {
            if (preparedPng is not null)
            {
                try { if (File.Exists(preparedPng)) File.Delete(preparedPng); } catch { }
            }
        }
    }

    private static string GetOutputPath(string source, string target, string? outputDirectory)
    {
        var dir = outputDirectory ?? Path.GetDirectoryName(source) ?? Directory.GetCurrentDirectory();
        var baseName = Path.GetFileNameWithoutExtension(source);
        var candidate = Path.Combine(dir, baseName + "." + target);
        var i = 1;
        while (File.Exists(candidate))
            candidate = Path.Combine(dir, $"{baseName} ({i++}).{target}");
        return candidate;
    }

    private static bool WaitForOutput(string output)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            if (File.Exists(output)) return true;
            Thread.Sleep(50);
        }
        return false;
    }

    private static string[] BuildCommand(string input, string output, string[] args)
    {
        var full = new List<string>
        {
            "-hide_banner", "-loglevel", "error", "-y",
            "-i", input
        };
        full.AddRange(args);
        full.Add(output);
        return full.ToArray();
    }

    private static bool RunVideoToGif(string input, string output, out string? error)
    {
        var palette = Path.Combine(Path.GetTempPath(), $"qc_pal_{Guid.NewGuid():N}.png");

        var pass1 = new List<string>
        {
            "-hide_banner", "-loglevel", "error", "-y",
            "-i", input,
            "-vf", "fps=15,scale=640:-1:flags=lanczos,palettegen=max_colors=256",
            palette
        };
        var pass2 = new List<string>
        {
            "-hide_banner", "-loglevel", "error", "-y",
            "-i", input, "-i", palette,
            "-lavfi", "fps=15,scale=640:-1:flags=lanczos[x];[x][1:v]paletteuse",
            "-loop", "0",
            output
        };

        var ok = RunFfmpeg(pass1.ToArray(), out error);
        if (!ok) return false;
        ok = RunFfmpeg(pass2.ToArray(), out error);
        try { if (File.Exists(palette)) File.Delete(palette); } catch { }
        return ok;
    }

    private static bool RunFfmpeg(string[] args, out string? error)
        => RunExternalTool(FindFfmpeg(), args, "FFmpeg", out error);

    private static bool RunExternalTool(string executable, string[] args, string toolName, out string? error)
    {
        error = null;
        try
        {
            var psi = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var a in args) psi.ArgumentList.Add(a);

            using var process = Process.Start(psi);
            if (process is null)
            {
                error = $"{toolName} is not available.";
                return false;
            }

            var outTask = process.StandardOutput.ReadToEndAsync();
            var errTask = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            _ = outTask.Result;
            var stderr = errTask.Result;

            if (process.ExitCode == 0) return true;

            var lines = stderr.Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .TakeLast(3);
            error = string.Join(" — ", lines);
            if (string.IsNullOrEmpty(error)) error = $"{toolName} exited with code {process.ExitCode}.";
            if (error.Length > 400) error = error[..400];
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
