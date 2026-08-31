#include "../QuickConvert.Shell/ShellLogic.h"

#include <cassert>
#include <filesystem>
#include <fstream>
#include <vector>

int wmain()
{
    assert(qc::NormalizeExtension(LR"(C:\Temp\PHOTO.JPEG)") == L"jpg");
    assert(qc::NormalizeExtension(L".TIFF") == L"tif");
    assert(qc::NormalizeExtension(L"movie.MPEG") == L"mpg");

    assert(qc::IsSupportedMedia(LR"(C:\Temp\photo.png)"));
    assert(qc::IsSupportedMedia(LR"(C:\Temp\photo.heic)"));
    assert(qc::IsSupportedMedia(LR"(C:\Temp\track.flac)"));
    assert(qc::IsSupportedMedia(LR"(C:\Temp\movie.mkv)"));
    assert(!qc::IsSupportedMedia(LR"(C:\Temp\notes.txt)"));

    assert(qc::CanConvertTo(L"photo.png", L"jpg"));
    assert(qc::CanConvertTo(L"photo.heic", L"jxl"));
    assert(!qc::CanConvertTo(L"photo.jpg", L"jpg"));
    assert(!qc::CanConvertTo(L"photo.png", L"mp3"));
    assert(qc::CanConvertTo(L"track.wav", L"opus"));
    assert(qc::CanConvertTo(L"movie.mp4", L"mp3"));
    assert(qc::CanConvertTo(L"movie.mp4", L"gif"));
    assert(qc::CanConvertTo(L"animation.apng", L"mp4"));
    assert(qc::CanQuickConvertTo(L"track.wav", L"mp3"));
    assert(!qc::CanQuickConvertTo(L"movie.mp4", L"mp3"));
    assert(qc::CanQuickConvertTo(L"movie.mp4", L"mkv"));

    const std::vector<std::wstring> paths{
        LR"(C:\A B\one.png)",
        LR"(C:\A B\two.png)",
    };
    assert(qc::BuildWorkerArguments(L"jpg", std::span<const std::wstring>(paths)) ==
           LR"(--convert jpg "C:\A B\one.png" "C:\A B\two.png")");
    assert(qc::BuildOpenArguments(std::span<const std::wstring>(paths)) ==
           LR"(--open "C:\A B\one.png" "C:\A B\two.png")");
    assert(qc::WorkerPath(LR"(C:\QuickConvert\QuickConvert.Shell.dll)") ==
           LR"(C:\QuickConvert\QuickConvert.exe)");
    assert(qc::IconResourcePath(LR"(C:\QuickConvert\QuickConvert.Shell.dll)") ==
           LR"(C:\QuickConvert\App.ico,0)");

    const auto testRoot = std::filesystem::temp_directory_path() /
                          (L"QuickConvert-Shell-" + std::to_wstring(GetCurrentProcessId()));
    std::filesystem::create_directory(testRoot);
    const auto png = testRoot / L"photo.png";
    std::ofstream(png).put('\0');
    const auto folder = testRoot / L"folder.png";
    std::filesystem::create_directory(folder);
    assert(qc::IsExistingSupportedFile(png.c_str()));
    assert(!qc::IsExistingSupportedFile(folder.c_str()));
    std::filesystem::remove_all(testRoot);
}
