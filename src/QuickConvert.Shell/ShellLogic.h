#pragma once

#include <windows.h>

#include <algorithm>
#include <array>
#include <cwctype>
#include <span>
#include <string>
#include <string_view>

namespace qc {

enum class MediaKind
{
    Unknown,
    Image,
    Audio,
    Video,
};

template <typename T, size_t N>
inline bool Contains(const std::array<T, N>& values, std::wstring_view value) noexcept
{
    return std::any_of(values.begin(), values.end(), [&](const auto& candidate) {
        return std::wstring_view(candidate) == value;
    });
}

inline std::wstring NormalizeExtension(std::wstring_view value)
{
    const auto slash = value.find_last_of(L"\\/");
    const auto dot = value.find_last_of(L'.');
    if (dot != std::wstring_view::npos && (slash == std::wstring_view::npos || dot > slash))
        value = value.substr(dot + 1);
    else if (!value.empty() && value.front() == L'.')
        value.remove_prefix(1);

    std::wstring ext(value);
    std::transform(ext.begin(), ext.end(), ext.begin(), [](wchar_t ch) { return static_cast<wchar_t>(std::towlower(ch)); });

    if (ext == L"jpeg" || ext == L"jfif" || ext == L"jpe") return L"jpg";
    if (ext == L"tiff") return L"tif";
    if (ext == L"mpeg") return L"mpg";
    if (ext == L"wave") return L"wav";
    if (ext == L"aif") return L"aiff";
    if (ext == L"oga") return L"ogg";
    return ext;
}

inline MediaKind MediaKindForExtension(std::wstring_view value)
{
    const auto ext = NormalizeExtension(value);

    static constexpr std::array imageExtensions{
        L"jpg", L"png", L"webp", L"avif", L"jxl", L"gif", L"bmp", L"tif", L"ico", L"apng",
        L"heic", L"heif", L"tga", L"dds", L"pcx", L"ppm", L"pgm", L"pbm", L"exr", L"psd", L"jp2", L"j2k", L"qoi"
    };
    static constexpr std::array audioExtensions{
        L"mp3", L"m4a", L"m4b", L"aac", L"flac", L"wav", L"aiff", L"ogg", L"opus", L"wma", L"ac3"
    };
    static constexpr std::array videoExtensions{
        L"mp4", L"mkv", L"webm", L"mov", L"avi", L"wmv", L"m4v", L"mpg", L"3gp", L"3g2", L"flv", L"ts", L"mts", L"m2ts"
    };

    if (Contains(imageExtensions, ext)) return MediaKind::Image;
    if (Contains(audioExtensions, ext)) return MediaKind::Audio;
    if (Contains(videoExtensions, ext)) return MediaKind::Video;
    return MediaKind::Unknown;
}

inline bool IsSupportedMedia(std::wstring_view path)
{
    return MediaKindForExtension(path) != MediaKind::Unknown;
}

inline bool IsExistingSupportedFile(std::wstring_view path)
{
    if (!IsSupportedMedia(path))
        return false;

    const std::wstring ownedPath(path);
    const auto attributes = GetFileAttributesW(ownedPath.c_str());
    return attributes != INVALID_FILE_ATTRIBUTES && !(attributes & FILE_ATTRIBUTE_DIRECTORY);
}

inline bool CanConvertTo(std::wstring_view pathOrExtension, std::wstring_view targetExtension)
{
    const auto source = NormalizeExtension(pathOrExtension);
    const auto target = NormalizeExtension(targetExtension);
    if (source.empty() || target.empty() || source == target)
        return false;

    static constexpr std::array imageTargets{L"jpg", L"png", L"webp", L"avif", L"jxl", L"gif", L"tif", L"ico"};
    static constexpr std::array audioTargets{L"mp3", L"m4a", L"flac", L"wav", L"opus", L"ogg"};
    static constexpr std::array videoTargets{L"mp4", L"mkv", L"webm", L"mov", L"avi"};

    switch (MediaKindForExtension(source)) {
    case MediaKind::Image:
        return Contains(imageTargets, target) || ((source == L"gif" || source == L"apng") && Contains(videoTargets, target));
    case MediaKind::Audio:
        return Contains(audioTargets, target);
    case MediaKind::Video:
        return Contains(videoTargets, target) || Contains(audioTargets, target) || target == L"gif";
    default:
        return false;
    }
}

inline bool IsAudioTarget(std::wstring_view targetExtension)
{
    const auto target = NormalizeExtension(targetExtension);
    static constexpr std::array audioTargets{L"mp3", L"m4a", L"flac", L"wav", L"opus", L"ogg"};
    return Contains(audioTargets, target);
}

inline bool CanQuickConvertTo(std::wstring_view pathOrExtension, std::wstring_view targetExtension)
{
    if (!CanConvertTo(pathOrExtension, targetExtension))
        return false;

    // Video-to-audio extraction can involve stream selection and output settings, so keep it
    // in the full Convert... window instead of presenting it as a one-click quick conversion.
    return MediaKindForExtension(pathOrExtension) != MediaKind::Video || !IsAudioTarget(targetExtension);
}

inline bool AppendQuotedPath(std::wstring& arguments, std::wstring_view path)
{
    if (path.empty() || path.find(L'"') != std::wstring_view::npos)
        return false;
    arguments.append(L" \"");
    arguments.append(path);
    arguments.push_back(L'"');
    return true;
}

inline std::wstring BuildWorkerArguments(std::wstring_view target, std::span<const std::wstring> paths)
{
    if (target.empty() || target.find_first_of(L" \t\"") != std::wstring_view::npos || paths.empty())
        return {};

    std::wstring arguments = L"--convert ";
    arguments.append(target);
    for (const auto& path : paths)
        if (!AppendQuotedPath(arguments, path))
            return {};
    return arguments;
}

inline std::wstring BuildOpenArguments(std::span<const std::wstring> paths)
{
    if (paths.empty())
        return {};

    std::wstring arguments = L"--open";
    for (const auto& path : paths)
        if (!AppendQuotedPath(arguments, path))
            return {};
    return arguments;
}

inline std::wstring WorkerPath(std::wstring_view modulePath)
{
    const auto slash = modulePath.find_last_of(L"\\/");
    if (slash == std::wstring_view::npos)
        return L"QuickConvert.exe";
    return std::wstring(modulePath.substr(0, slash + 1)) + L"QuickConvert.exe";
}

inline std::wstring IconResourcePath(std::wstring_view modulePath)
{
    return WorkerPath(modulePath) + L",0";
}

} // namespace qc
