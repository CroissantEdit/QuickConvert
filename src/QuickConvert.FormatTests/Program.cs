using QuickConvert.Conversion;

var expectedTargets = new[]
{
    "jxl", "tif", "ico", "apng",
    "aiff", "ac3", "wma",
    "avi", "wmv", "m4v", "ts", "mpg", "3gp", "flv",
};
foreach (var target in expectedTargets)
    if (!FormatCatalog.IsSupportedTarget(target))
        throw new InvalidOperationException($"Missing target: {target}");

var aacArgs = FormatCatalog.BuildArgs("wav", "aac", Quality.Balanced);
if (!aacArgs.SequenceEqual(new[] { "-c:a", "aac", "-b:a", "192k", "-f", "adts" }))
    throw new InvalidOperationException("AAC arguments must preserve the FFmpeg option boundary.");

if (FormatCatalog.NormalizeExt(".JFIF") != "jpg" ||
    FormatCatalog.NormalizeExt(".tiff") != "tif" ||
    FormatCatalog.NormalizeExt(".mpeg") != "mpg" ||
    FormatCatalog.NormalizeExt(".wave") != "wav")
    throw new InvalidOperationException("Common input aliases must normalize to supported formats.");

if (!FormatCatalog.IsSupportedInput(".heic") || !FormatCatalog.IsSupportedInput(".psd") || !FormatCatalog.IsSupportedInput(".m2ts"))
    throw new InvalidOperationException("Expected common FFmpeg-backed input formats are missing.");

if (!FormatCatalog.CanConvert("heic", "jxl"))
    throw new InvalidOperationException("HEIC should be convertible to JPEG XL.");
if (!FormatCatalog.CanConvert("mp4", "mp3"))
    throw new InvalidOperationException("Video should support audio extraction.");
if (FormatCatalog.CanConvert("png", "mp3"))
    throw new InvalidOperationException("Still images must not offer audio outputs.");
if (!FormatCatalog.CanConvert("apng", "mp4"))
    throw new InvalidOperationException("Animated PNG should support video output.");


var avifArgs = FormatCatalog.BuildArgs("png", "avif", Quality.Balanced);
if (avifArgs.Contains("yuv420p"))
    throw new InvalidOperationException("Generic AVIF arguments must not force away alpha; Converter handles AVIF alpha as a separate stream.");

var animatedStillArgs = FormatCatalog.BuildArgs("gif", "jpg", Quality.Balanced);
if (!animatedStillArgs.SequenceEqual(new[] { "-c:v", "mjpeg", "-q:v", "4", "-frames:v", "1" }))
    throw new InvalidOperationException("Animated image to still output should use the first frame.");

var common = FormatCatalog.GetCompatibleTargets(new[] { "png", "jpg" });
if (common.Any(option => option.Ext is "mp3" or "mp4") || !common.Any(option => option.Ext == "webp"))
    throw new InvalidOperationException("Mixed image selections should only expose common image outputs.");

Console.WriteLine("[PASS] Expanded conversion catalog and compatibility filtering");
