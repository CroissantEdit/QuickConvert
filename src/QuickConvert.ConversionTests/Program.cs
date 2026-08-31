using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using QuickConvert.Conversion;

var runMatrix = args.Contains("--matrix", StringComparer.OrdinalIgnoreCase);
if (runMatrix)
    RunConversionMatrix();

var root = Path.Combine(Path.GetTempPath(), $"QuickConvert-Transparency-{Guid.NewGuid():N}");
Directory.CreateDirectory(root);
try
{
    var source = Path.Combine(root, "transparent.png");
    using (var bitmap = new Bitmap(64, 48, PixelFormat.Format32bppArgb))
    using (var graphics = Graphics.FromImage(bitmap))
    using (var opaque = new SolidBrush(Color.FromArgb(255, 0, 120, 212)))
    using (var semi = new SolidBrush(Color.FromArgb(128, 255, 64, 32)))
    {
        graphics.Clear(Color.Transparent);
        graphics.FillRectangle(opaque, 20, 8, 28, 28);
        // Keep a semi-transparent sample away from the opaque rectangle so the
        // JPEG matte check can verify actual alpha compositing, not just a white corner.
        graphics.FillRectangle(semi, 4, 18, 10, 10);
        bitmap.Save(source, ImageFormat.Png);
    }

    var jpg = RequireConversion(source, "jpg", root);
    using (var flattened = new Bitmap(jpg))
    {
        var corner = flattened.GetPixel(2, 2);
        if (corner.R < 235 || corner.G < 235 || corner.B < 235)
            throw new InvalidOperationException($"JPG transparency matte should be white; got RGB({corner.R},{corner.G},{corner.B}).");

        var semi = flattened.GetPixel(8, 22);
        if (semi.R < 220 || semi.G < 110 || semi.B < 90)
            throw new InvalidOperationException($"JPG semi-transparent pixels were not composited onto white; got RGB({semi.R},{semi.G},{semi.B}).");
    }

    var bmp = RequireConversion(source, "bmp", root);
    using (var flattenedBmp = new Bitmap(bmp))
    {
        var corner = flattenedBmp.GetPixel(2, 2);
        if (corner.R < 245 || corner.G < 245 || corner.B < 245)
            throw new InvalidOperationException($"BMP transparency matte should be white; got RGB({corner.R},{corner.G},{corner.B}).");
    }

    var gif = RequireConversion(source, "gif", root);
    AssertBinaryAlpha(gif, "GIF");

    foreach (var target in new[] { "webp", "jxl", "tif", "apng" })
    {
        var converted = RequireConversion(source, target, root);
        AssertMeaningfulAlpha(converted, target.ToUpperInvariant());
    }

    // ICO varies in how viewers expose alpha, but QuickConvert must at least produce a
    // readable icon from a transparent PNG without the conversion itself failing.
    var ico = RequireConversion(source, "ico", root);
    AssertFfmpegCanDecode(ico, "ICO");

    var avif = RequireConversion(source, "avif", root);
    var decodedAvif = Path.Combine(root, "decoded-avif.png");
    DecodeAvifWithLibavif(avif, decodedAvif);
    using (var decoded = new Bitmap(decodedAvif))
    {
        var transparent = decoded.GetPixel(2, 2).A;
        var semi = decoded.GetPixel(8, 22).A;
        var opaque = decoded.GetPixel(30, 16).A;
        if (transparent > 12 || semi is < 80 or > 180 || opaque < 243)
            throw new InvalidOperationException($"AVIF alpha mismatch: transparent={transparent}, semi={semi}, opaque={opaque}.");
    }

    Console.WriteLine("[PASS] Image conversions: JPG/BMP matte white; GIF/WebP/JXL/TIFF/APNG/AVIF transparency; ICO decodes");
}
finally
{
    try { Directory.Delete(root, true); } catch { }
}

static string RequireConversion(string source, string target, string outputDirectory)
{
    var result = Converter.ConvertOne(source, target, Quality.Balanced, outputDirectory);
    if (!result.Success || result.Output is null || !File.Exists(result.Output))
        throw new InvalidOperationException($"PNG -> {target.ToUpperInvariant()} failed: {result.Error}");
    return result.Output;
}

static void AssertBinaryAlpha(string input, string label)
{
    var alpha = ReadDecodedAlphaPlane(input);
    if (alpha.Length == 0 || alpha.Min() > 8 || alpha.Max() < 247)
        throw new InvalidOperationException($"{label} output did not preserve binary transparency.");
}

static void AssertMeaningfulAlpha(string input, string label)
{
    var alpha = ReadDecodedAlphaPlane(input);
    if (alpha.Length == 0 || alpha.Min() > 8 || alpha.Max() < 247)
        throw new InvalidOperationException($"{label} output did not preserve transparent/opaque pixels.");
}

static void AssertFfmpegCanDecode(string input, string label)
{
    var start = new ProcessStartInfo(Converter.FindFfmpeg())
    {
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };
    foreach (var argument in new[]
    {
        "-hide_banner", "-loglevel", "error", "-i", input,
        "-frames:v", "1", "-f", "null", "-"
    })
        start.ArgumentList.Add(argument);

    using var process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start FFmpeg to verify {label}.");
    var errors = process.StandardError.ReadToEndAsync();
    process.WaitForExit();
    if (process.ExitCode != 0)
        throw new InvalidOperationException($"{label} output could not be decoded: {errors.Result.Trim()}");
}

static byte[] ReadDecodedAlphaPlane(string input)
{
    var start = new ProcessStartInfo(Converter.FindFfmpeg())
    {
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };
    foreach (var argument in new[]
    {
        "-hide_banner", "-loglevel", "error", "-i", input,
        "-vf", "alphaextract", "-frames:v", "1", "-pix_fmt", "gray", "-f", "rawvideo", "-"
    })
        start.ArgumentList.Add(argument);

    using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start FFmpeg for alpha verification.");
    using var output = new MemoryStream();
    var copy = process.StandardOutput.BaseStream.CopyToAsync(output);
    var errors = process.StandardError.ReadToEndAsync();
    Task.WaitAll(copy, errors);
    process.WaitForExit();
    if (process.ExitCode != 0)
        throw new InvalidOperationException($"Could not decode alpha plane from {Path.GetExtension(input)}: {errors.Result.Trim()}");
    return output.ToArray();
}

static void DecodeAvifWithLibavif(string input, string output)
{
    var configured = Environment.GetEnvironmentVariable("QUICKCONVERT_AVIFDEC");
    var executable = !string.IsNullOrWhiteSpace(configured) && File.Exists(configured)
        ? configured
        : Path.Combine(AppContext.BaseDirectory, "avifdec.exe");
    if (!File.Exists(executable)) executable = "avifdec";

    var start = new ProcessStartInfo(executable)
    {
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };
    start.ArgumentList.Add(input);
    start.ArgumentList.Add(output);

    using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start avifdec for AVIF verification.");
    var stdout = process.StandardOutput.ReadToEndAsync();
    var stderr = process.StandardError.ReadToEndAsync();
    process.WaitForExit();
    _ = stdout.Result;
    if (process.ExitCode != 0 || !File.Exists(output))
        throw new InvalidOperationException($"libavif could not decode generated AVIF: {stderr.Result.Trim()}");
}

static void RunConversionMatrix()
{
    var root = Path.Combine(Path.GetTempPath(), $"QuickConvert-Matrix-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);
    try
    {
        var imageInputs = CreateImageInputs(root);
        var conversions = 0;
        foreach (var (sourceExt, source) in imageInputs)
            conversions += ConvertEveryCompatibleTarget(source, sourceExt, root);

        var audioInputs = CreateAudioInputs(root);
        foreach (var (sourceExt, source) in audioInputs)
            conversions += ConvertEveryCompatibleTarget(source, sourceExt, root);

        var videoInputs = CreateVideoInputs(root);
        foreach (var (sourceExt, source) in videoInputs)
            conversions += ConvertEveryCompatibleTarget(source, sourceExt, root);

        Console.WriteLine($"[PASS] Full conversion matrix: {conversions} compatible conversions decoded successfully");
    }
    finally
    {
        try { Directory.Delete(root, true); } catch { }
    }
}

static int ConvertEveryCompatibleTarget(string source, string sourceExt, string root)
{
    var count = 0;
    foreach (var target in FormatCatalog.GetMenuTargets(sourceExt))
    {
        var outputDirectory = Path.Combine(root, "outputs", sourceExt, target);
        Directory.CreateDirectory(outputDirectory);
        var result = Converter.ConvertOne(source, target, Quality.Small, outputDirectory);
        if (!result.Success || result.Output is null)
            throw new InvalidOperationException($".{sourceExt} -> .{target} failed: {result.Error}");
        AssertFfmpegCanDecode(result.Output, $".{sourceExt} -> .{target}");
        count++;
    }
    return count;
}

static Dictionary<string, string> CreateImageInputs(string root)
{
    var fixtures = Path.Combine(root, "images");
    Directory.CreateDirectory(fixtures);
    var png = Path.Combine(fixtures, "transparent.png");
    using (var bitmap = new Bitmap(64, 48, PixelFormat.Format32bppArgb))
    using (var graphics = Graphics.FromImage(bitmap))
    {
        graphics.Clear(Color.Transparent);
        graphics.FillEllipse(Brushes.CornflowerBlue, 16, 8, 32, 32);
        bitmap.Save(png, ImageFormat.Png);
    }

    var inputs = new Dictionary<string, string> { ["png"] = png };
    foreach (var target in new[] { "jpg", "webp", "avif", "jxl", "bmp", "tif", "ico", "apng" })
        inputs[target] = RequireConversion(png, target, fixtures);

    inputs["gif"] = CreateAnimatedImage(fixtures, "gif", "gif");
    inputs["apng"] = CreateAnimatedImage(fixtures, "apng", "apng");
    foreach (var (extension, codec) in new[]
    {
        ("tga", "targa"), ("pcx", "pcx"), ("ppm", "ppm"), ("pgm", "pgm"),
        ("pbm", "pbm"), ("exr", "exr"), ("jp2", "jpeg2000"), ("j2k", "jpeg2000"), ("qoi", "qoi"),
    })
        inputs[extension] = CreateStillImage(fixtures, extension, codec);

    inputs["dds"] = CreateDds(fixtures);
    inputs["psd"] = CreatePsd(fixtures);
    // FFmpeg probes image content instead of trusting an extension. The bundled tools cannot
    // encode HEIC/HEIF, so an AVIF fixture checks QuickConvert's HEIF-family routing here.
    inputs["heic"] = CopyWithExtension(inputs["avif"], fixtures, "heic");
    inputs["heif"] = CopyWithExtension(inputs["avif"], fixtures, "heif");
    return inputs;
}

static Dictionary<string, string> CreateAudioInputs(string root)
{
    var fixtures = Path.Combine(root, "audio");
    Directory.CreateDirectory(fixtures);
    var wav = Path.Combine(fixtures, "tone.wav");
    RunFfmpeg("-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=44100", "-t", "0.25", "-c:a", "pcm_s16le", wav);

    var inputs = new Dictionary<string, string> { ["wav"] = wav };
    foreach (var target in new[] { "mp3", "m4a", "aac", "flac", "aiff", "ogg", "opus", "wma", "ac3" })
        inputs[target] = RequireConversion(wav, target, fixtures);
    inputs["m4b"] = CopyWithExtension(inputs["m4a"], fixtures, "m4b");
    return inputs;
}

static Dictionary<string, string> CreateVideoInputs(string root)
{
    var fixtures = Path.Combine(root, "video");
    Directory.CreateDirectory(fixtures);
    var mp4 = Path.Combine(fixtures, "clip.mp4");
    RunFfmpeg(
        "-hide_banner", "-loglevel", "error", "-y",
        "-f", "lavfi", "-i", "testsrc2=size=176x144:rate=12",
        "-f", "lavfi", "-i", "sine=frequency=660:sample_rate=44100",
        "-t", "0.5", "-shortest", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-c:a", "aac", mp4);

    var inputs = new Dictionary<string, string> { ["mp4"] = mp4 };
    foreach (var target in new[] { "mkv", "webm", "mov", "avi", "wmv", "m4v", "mpg", "ts", "3gp", "flv" })
        inputs[target] = RequireConversion(mp4, target, fixtures);
    inputs["mts"] = CopyWithExtension(inputs["ts"], fixtures, "mts");
    inputs["m2ts"] = CopyWithExtension(inputs["ts"], fixtures, "m2ts");
    inputs["3g2"] = CopyWithExtension(inputs["3gp"], fixtures, "3g2");
    return inputs;
}

static string CreateAnimatedImage(string directory, string extension, string codec)
{
    var path = Path.Combine(directory, $"animated.{extension}");
    RunFfmpeg("-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", "testsrc2=size=64x48:rate=4", "-t", "0.5", "-c:v", codec, path);
    return path;
}

static string CreateStillImage(string directory, string extension, string codec)
{
    var path = Path.Combine(directory, $"source.{extension}");
    RunFfmpeg("-hide_banner", "-loglevel", "error", "-y", "-f", "lavfi", "-i", "testsrc2=size=64x48:rate=1", "-frames:v", "1", "-c:v", codec, path);
    return path;
}

static string CreateDds(string directory)
{
    var path = Path.Combine(directory, "source.dds");
    using var writer = new BinaryWriter(File.Create(path));
    writer.Write(0x20534444); // DDS 
    writer.Write(124);
    writer.Write(0x0002100F);
    writer.Write(2);
    writer.Write(2);
    writer.Write(8);
    writer.Write(0);
    writer.Write(0);
    for (var i = 0; i < 11; i++) writer.Write(0);
    writer.Write(32);
    writer.Write(0x41);
    writer.Write(0);
    writer.Write(32);
    writer.Write(0x00FF0000);
    writer.Write(0x0000FF00);
    writer.Write(0x000000FF);
    writer.Write(unchecked((int)0xFF000000));
    writer.Write(0x1000);
    writer.Write(0);
    writer.Write(0);
    writer.Write(0);
    writer.Write(0);
    foreach (var pixel in new[] { 0xFFFF0000u, 0xFF00FF00u, 0xFF0000FFu, 0xFFFFFFFFu }) writer.Write(pixel);
    return path;
}

static string CreatePsd(string directory)
{
    var path = Path.Combine(directory, "source.psd");
    using var writer = new BinaryWriter(File.Create(path));
    writer.Write("8BPS"u8.ToArray());
    WriteU16BE(writer, 1);
    writer.Write(new byte[6]);
    WriteU16BE(writer, 3);
    WriteU32BE(writer, 2);
    WriteU32BE(writer, 2);
    WriteU16BE(writer, 8);
    WriteU16BE(writer, 3);
    WriteU32BE(writer, 0);
    WriteU32BE(writer, 0);
    WriteU32BE(writer, 0);
    WriteU16BE(writer, 0);
    writer.Write(new byte[] { 255, 0, 0, 255 });
    writer.Write(new byte[] { 0, 255, 0, 255 });
    writer.Write(new byte[] { 0, 0, 255, 255 });
    return path;
}

static void WriteU16BE(BinaryWriter writer, ushort value) => writer.Write(new[] { (byte)(value >> 8), (byte)value });
static void WriteU32BE(BinaryWriter writer, uint value) => writer.Write(new[] { (byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value });

static string CopyWithExtension(string source, string directory, string extension)
{
    var output = Path.Combine(directory, $"alias.{extension}");
    File.Copy(source, output, true);
    return output;
}

static void RunFfmpeg(params string[] arguments)
{
    var start = new ProcessStartInfo(Converter.FindFfmpeg())
    {
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardError = true,
    };
    foreach (var argument in arguments) start.ArgumentList.Add(argument);
    using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start FFmpeg to create a matrix fixture.");
    var errors = process.StandardError.ReadToEndAsync();
    process.WaitForExit();
    if (process.ExitCode != 0)
        throw new InvalidOperationException($"Could not create a matrix fixture: {errors.Result.Trim()}");
}
