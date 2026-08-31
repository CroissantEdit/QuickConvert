using System.Collections.Generic;
using System.Linq;

namespace QuickConvert.Conversion;

public enum Quality
{
    Best,
    Balanced,
    Small
}

public sealed record FormatOption(string Ext, string Category, string DisplayName);

public sealed record ConversionResult(string Source, string? Output, bool Success, string? Error);

public static class FormatCatalog
{
    public static readonly IReadOnlyList<FormatOption> All = new List<FormatOption>
    {
        new("jpg",  "Image", "JPG"),
        new("png",  "Image", "PNG"),
        new("webp", "Image", "WEBP"),
        new("avif", "Image", "AVIF"),
        new("jxl",  "Image", "JPEG XL"),
        new("gif",  "Image", "GIF"),
        new("bmp",  "Image", "BMP"),
        new("tif",  "Image", "TIFF"),
        new("ico",  "Image", "ICO"),
        new("apng", "Image", "APNG"),


        new("mp3",  "Audio", "MP3"),
        new("m4a",  "Audio", "M4A"),
        new("aac",  "Audio", "AAC"),
        new("flac", "Audio", "FLAC"),
        new("wav",  "Audio", "WAV"),
        new("aiff", "Audio", "AIFF"),
        new("ogg",  "Audio", "OGG"),
        new("opus", "Audio", "OPUS"),
        new("wma",  "Audio", "WMA"),
        new("ac3",  "Audio", "AC3"),

        new("mp4",  "Video", "MP4"),
        new("mkv",  "Video", "MKV"),
        new("webm", "Video", "WEBM"),
        new("mov",  "Video", "MOV"),
        new("avi",  "Video", "AVI"),
        new("wmv",  "Video", "WMV"),
        new("m4v",  "Video", "M4V"),
        new("mpg",  "Video", "MPEG"),
        new("ts",   "Video", "MPEG-TS"),
        new("3gp",  "Video", "3GP"),
        new("flv",  "Video", "FLV"),
    };

    public static readonly IReadOnlyDictionary<string, string> Labels =
        All.ToDictionary(o => o.Ext, o => o.DisplayName);

    private static readonly string[] ImageTargetOrder =
        { "jpg", "png", "webp", "avif", "jxl", "gif", "bmp", "tif", "ico", "apng" };

    private static readonly string[] AudioTargetOrder =
        { "mp3", "m4a", "aac", "flac", "wav", "aiff", "ogg", "opus", "wma", "ac3" };

    private static readonly string[] VideoTargetOrder =
        { "mp4", "mkv", "webm", "mov", "avi", "wmv", "m4v", "mpg", "ts", "3gp", "flv" };


    private static readonly HashSet<string> ImageTargets = new(ImageTargetOrder);
    private static readonly HashSet<string> AudioTargets = new(AudioTargetOrder);
    private static readonly HashSet<string> VideoTargets = new(VideoTargetOrder);

    private static readonly HashSet<string> ImageSources = new()
    {
        "jpg", "png", "webp", "avif", "jxl", "gif", "bmp", "tif", "ico", "apng",
        "heic", "heif", "tga", "dds", "pcx", "ppm", "pgm", "pbm", "exr", "psd", "jp2", "j2k", "qoi",
    };

    private static readonly HashSet<string> AudioSources = new()
    {
        "mp3", "m4a", "aac", "flac", "wav", "aiff", "ogg", "opus", "wma", "ac3", "m4b",
    };

    private static readonly HashSet<string> VideoSources = new()
    {
        "mp4", "mkv", "webm", "mov", "avi", "wmv", "m4v", "mpg", "3gp", "flv", "ts", "mts", "m2ts", "3g2",
    };


    public static IReadOnlyList<string> SupportedInputExtensions { get; } =
        new[]
        {
            "png", "jpg", "jpeg", "jfif", "jpe", "webp", "avif", "jxl", "gif", "bmp", "tif", "tiff", "ico", "apng",
            "heic", "heif", "tga", "dds", "pcx", "ppm", "pgm", "pbm", "exr", "psd", "jp2", "j2k", "qoi",
            "mp3", "m4a", "m4b", "aac", "flac", "wav", "wave", "aiff", "aif", "ogg", "oga", "opus", "wma", "ac3",
            "mp4", "mkv", "webm", "mov", "avi", "wmv", "m4v", "mpg", "mpeg", "3gp", "3g2", "flv", "ts", "mts", "m2ts",
        };

    public static string NormalizeExt(string ext) => ext.Trim().TrimStart('.').ToLowerInvariant() switch
    {
        "jpeg" or "jfif" or "jpe" => "jpg",
        "tiff" => "tif",
        "mpeg" => "mpg",
        "wave" => "wav",
        "aif" => "aiff",
        "oga" => "ogg",
        var value => value,
    };

    public static bool IsSupportedInput(string ext)
    {
        var e = NormalizeExt(ext);
        return ImageSources.Contains(e) || AudioSources.Contains(e) || VideoSources.Contains(e);
    }

    public static bool IsSupportedTarget(string target) =>
        All.Any(o => o.Ext == NormalizeExt(target));

    public static bool IsImageSource(string ext) => ImageSources.Contains(NormalizeExt(ext));
    public static bool IsAudioSource(string ext) => AudioSources.Contains(NormalizeExt(ext));
    public static bool IsVideoSource(string ext) => VideoSources.Contains(NormalizeExt(ext));
    public static bool IsImageTarget(string ext) => ImageTargets.Contains(NormalizeExt(ext));
    public static bool IsAudioTarget(string ext) => AudioTargets.Contains(NormalizeExt(ext));
    public static bool IsVideoTarget(string ext) => VideoTargets.Contains(NormalizeExt(ext));

    public static bool CanConvert(string sourceExt, string targetExt)
    {
        var source = NormalizeExt(sourceExt);
        var target = NormalizeExt(targetExt);
        if (source == target || !IsSupportedTarget(target) || !IsSupportedInput(source))
            return false;

        if (ImageSources.Contains(source))
            return ImageTargets.Contains(target) || ((source is "gif" or "apng") && VideoTargets.Contains(target));

        if (AudioSources.Contains(source))
            return AudioTargets.Contains(target);

        return VideoTargets.Contains(target) || AudioTargets.Contains(target) || target == "gif";
    }

    public static string[] GetMenuTargets(string ext)
    {
        var source = NormalizeExt(ext);
        IEnumerable<string> targets = ImageSources.Contains(source)
            ? ImageTargetOrder.Concat((source is "gif" or "apng") ? VideoTargetOrder : Array.Empty<string>())
            : AudioSources.Contains(source)
                ? AudioTargetOrder
                : VideoSources.Contains(source)
                    ? VideoTargetOrder.Concat(AudioTargetOrder).Concat(new[] { "gif" })
                    : Array.Empty<string>();

        return targets.Where(target => CanConvert(source, target)).Distinct().ToArray();
    }

    public static IReadOnlyList<FormatOption> GetCompatibleTargets(IEnumerable<string> sourceExtensions)
    {
        var sources = sourceExtensions.Select(NormalizeExt).Distinct().ToArray();
        if (sources.Length == 0)
            return Array.Empty<FormatOption>();

        return All.Where(option => sources.All(source => CanConvert(source, option.Ext))).ToArray();
    }

    public static string[] BuildArgs(string srcExt, string target, Quality q)
    {
        srcExt = NormalizeExt(srcExt);
        target = NormalizeExt(target);
        var args = new List<string>();

        if (IsAudioTarget(target) && IsVideoSource(srcExt))
            args.Add("-vn");

        args.AddRange(CodecArgs(target, q));

        // A single-file still-image target cannot represent every frame of an animated GIF/APNG.
        // Use the first frame instead of letting FFmpeg fail while trying to write a sequence to one name.
        if (srcExt is "gif" or "apng" && target is not "gif" and not "apng" && IsImageTarget(target))
            args.AddRange(new[] { "-frames:v", "1" });

        // Windows icons are conventionally at most 256x256. Preserve aspect ratio and only scale down.
        if (target == "ico")
            args.AddRange(new[] { "-vf", "scale='min(256,iw)':'min(256,ih)':force_original_aspect_ratio=decrease,format=bgra" });

        switch (target)
        {
            case "aac":
                args.AddRange(new[] { "-f", "adts" });
                break;
            case "m4v":
                args.AddRange(new[] { "-f", "mp4" });
                break;
            case "ts":
                args.AddRange(new[] { "-f", "mpegts" });
                break;
        }

        return args.ToArray();
    }

    private static string[] H264Args(Quality q) => new[]
    {
        "-c:v", "libx264", "-preset", "veryfast", "-crf", q == Quality.Best ? "16" : q == Quality.Balanced ? "23" : "30",
        "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "192k",
    };

    private static string[] CodecArgs(string target, Quality q) => target switch
    {
        "jpg" => new[] { "-c:v", "mjpeg", "-q:v", q == Quality.Best ? "2" : q == Quality.Balanced ? "4" : "7" },
        "png" => new[] { "-c:v", "png", "-compression_level", q == Quality.Best ? "9" : q == Quality.Balanced ? "6" : "1" },
        "webp" => q == Quality.Best
            ? new[] { "-c:v", "libwebp", "-lossless", "1", "-compression_level", "6" }
            : new[] { "-c:v", "libwebp", "-quality", q == Quality.Balanced ? "78" : "55" },
        // Actual still-image AVIF conversion uses Converter.RunImageToAvif so alpha can be
        // represented as AVIF's separate alpha item. Keep this as a safe codec fallback.
        "avif" => new[] { "-c:v", "libaom-av1", "-crf", q == Quality.Best ? "18" : q == Quality.Balanced ? "30" : "45", "-cpu-used", "6", "-still-picture", "1" },
        "jxl" => new[] { "-c:v", "libjxl", "-distance", q == Quality.Best ? "0.3" : q == Quality.Balanced ? "1.0" : "2.5", "-effort", q == Quality.Best ? "7" : q == Quality.Balanced ? "5" : "3" },
        "gif" => new[] { "-c:v", "gif", "-loop", "0" },
        "bmp" => new[] { "-c:v", "bmp" },
        "tif" => new[] { "-c:v", "tiff", "-compression_algo", q == Quality.Best ? "lzw" : "deflate" },
        "ico" => new[] { "-c:v", "bmp" },
        "apng" => new[] { "-c:v", "apng", "-plays", "0" },

        "mp3" => new[] { "-c:a", "libmp3lame", "-b:a", q == Quality.Best ? "320k" : q == Quality.Balanced ? "192k" : "96k" },
        "m4a" => new[] { "-c:a", "aac", "-b:a", q == Quality.Best ? "320k" : q == Quality.Balanced ? "192k" : "128k" },
        "aac" => new[] { "-c:a", "aac", "-b:a", q == Quality.Best ? "320k" : q == Quality.Balanced ? "192k" : "128k" },
        "flac" => new[] { "-c:a", "flac", "-compression_level", q == Quality.Best ? "8" : "5" },
        "wav" => new[] { "-c:a", "pcm_s16le" },
        "aiff" => new[] { "-c:a", "pcm_s16be" },
        "ogg" => new[] { "-c:a", "libvorbis", "-q:a", q == Quality.Best ? "8" : q == Quality.Balanced ? "5" : "2" },
        "opus" => new[] { "-c:a", "libopus", "-b:a", q == Quality.Best ? "256k" : q == Quality.Balanced ? "160k" : "96k" },
        "wma" => new[] { "-c:a", "wmav2", "-b:a", q == Quality.Best ? "192k" : q == Quality.Balanced ? "128k" : "64k" },
        "ac3" => new[] { "-c:a", "ac3", "-b:a", q == Quality.Best ? "640k" : q == Quality.Balanced ? "448k" : "192k" },

        "mp4" => H264Args(q).Concat(new[] { "-movflags", "+faststart" }).ToArray(),
        "mov" => H264Args(q).Concat(new[] { "-movflags", "+faststart" }).ToArray(),
        "m4v" => H264Args(q).Concat(new[] { "-movflags", "+faststart" }).ToArray(),
        "mkv" => H264Args(q),
        "webm" => new[] { "-c:v", "libvpx-vp9", "-crf", q == Quality.Best ? "24" : q == Quality.Balanced ? "32" : "42", "-b:v", "0", "-c:a", "libopus", "-b:a", "160k", "-row-mt", "1" },
        "avi" => new[] { "-c:v", "mpeg4", "-q:v", q == Quality.Best ? "2" : q == Quality.Balanced ? "5" : "9", "-c:a", "libmp3lame", "-b:a", "192k" },
        "wmv" => new[] { "-c:v", "wmv2", "-b:v", q == Quality.Best ? "4000k" : q == Quality.Balanced ? "2200k" : "1000k", "-c:a", "wmav2", "-b:a", "128k" },
        "mpg" => new[] { "-c:v", "mpeg2video", "-q:v", q == Quality.Best ? "2" : q == Quality.Balanced ? "5" : "9", "-c:a", "mp2", "-b:a", "192k" },
        "ts" => H264Args(q),
        // H.263 only accepts a handful of legacy frame sizes, so ordinary images and videos
        // fail when converted to 3GP. H.264/AAC is valid in a 3GP container and handles them.
        "3gp" => H264Args(q),
        "flv" => new[] { "-c:v", "flv", "-b:v", q == Quality.Best ? "1200k" : q == Quality.Balanced ? "700k" : "350k", "-c:a", "aac", "-b:a", "128k" },
        _ => Array.Empty<string>(),
    };
}
