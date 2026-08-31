#include <windows.h>
#include <shobjidl_core.h>
#include <shellapi.h>
#include <shlwapi.h>
#include <wrl/client.h>

#include <algorithm>
#include <array>
#include <atomic>
#include <memory>
#include <new>
#include <string>
#include <vector>

#include "ShellLogic.h"

using Microsoft::WRL::ComPtr;

namespace {

constexpr GUID CommandClsid{0x4fee7c82, 0x2a07, 0x4f48, {0xba, 0x44, 0x7f, 0x4b, 0x29, 0x4c, 0xa7, 0x9c}};
constexpr GUID RootCanonical{0x4fee7c82, 0x2a07, 0x4f48, {0xba, 0x44, 0x7f, 0x4b, 0x29, 0x4c, 0xa7, 0x9c}};
constexpr GUID OptionsCanonical{0x742d29e4, 0x17c5, 0x4c53, {0x95, 0x87, 0x27, 0xea, 0xe2, 0xfc, 0xb7, 0x66}};

struct QuickTarget
{
    const wchar_t* extension;
    const wchar_t* title;
    GUID canonical;
};

constexpr std::array QuickTargets{
    QuickTarget{L"jpg",  L"Convert to JPG",     GUID{0x24cc1ae8, 0x9b9b, 0x4fbb, {0x89, 0x0d, 0xfd, 0x88, 0x3f, 0x3a, 0x81, 0x90}}},
    QuickTarget{L"png",  L"Convert to PNG",     GUID{0x406b68f7, 0xc8aa, 0x4a4d, {0x86, 0x42, 0x00, 0xe8, 0x83, 0xde, 0x5c, 0xbb}}},
    QuickTarget{L"webp", L"Convert to WEBP",    GUID{0xd1bbc150, 0x91a4, 0x4bd1, {0x8a, 0x15, 0x4e, 0x96, 0x87, 0xc7, 0x16, 0x9a}}},
    QuickTarget{L"avif", L"Convert to AVIF",    GUID{0xbbbc2558, 0x110a, 0x405f, {0xb3, 0x94, 0x0f, 0xbb, 0x4e, 0x27, 0x5e, 0xe0}}},
    QuickTarget{L"jxl",  L"Convert to JPEG XL", GUID{0xf151cd2e, 0x388e, 0x4fd0, {0x98, 0x54, 0xed, 0xe9, 0xe1, 0x5e, 0xe5, 0x7b}}},
    QuickTarget{L"gif",  L"Convert to GIF",     GUID{0x29f54640, 0xc17f, 0x41b7, {0xb6, 0x04, 0xe1, 0xc2, 0xee, 0xad, 0x10, 0x2d}}},
    QuickTarget{L"tif",  L"Convert to TIFF",    GUID{0xd28829fe, 0x9610, 0x4f87, {0x8f, 0x74, 0xbe, 0xe0, 0xc2, 0xd8, 0xac, 0xc9}}},
    QuickTarget{L"ico",  L"Convert to ICO",     GUID{0xd1f08db0, 0x1330, 0x4af5, {0x89, 0xf8, 0x1e, 0x18, 0xae, 0x94, 0x4e, 0x25}}},

    QuickTarget{L"mp3",  L"Convert to MP3",     GUID{0x9a5a63d4, 0x9cb8, 0x4ede, {0x8b, 0xd9, 0xa4, 0xdc, 0xc4, 0xbb, 0x4c, 0xc4}}},
    QuickTarget{L"m4a",  L"Convert to M4A",     GUID{0x6037f984, 0xda12, 0x4e88, {0x89, 0x23, 0x0c, 0x67, 0x0a, 0x5e, 0xe0, 0x89}}},
    QuickTarget{L"flac", L"Convert to FLAC",    GUID{0x4822432c, 0x9597, 0x4456, {0x97, 0xc4, 0xfe, 0xbf, 0xbb, 0x20, 0x82, 0xbb}}},
    QuickTarget{L"wav",  L"Convert to WAV",     GUID{0x22e83253, 0x2716, 0x4acf, {0xba, 0x85, 0x31, 0x31, 0x5d, 0xff, 0xcc, 0x9d}}},
    QuickTarget{L"opus", L"Convert to OPUS",    GUID{0x5fd86059, 0x8326, 0x4bee, {0x83, 0x54, 0x07, 0x7e, 0x56, 0x0c, 0x2a, 0x93}}},
    QuickTarget{L"ogg",  L"Convert to OGG",     GUID{0xcd60f659, 0x83c2, 0x47de, {0xb5, 0xad, 0xe5, 0xfc, 0x44, 0x6d, 0x81, 0x7f}}},

    QuickTarget{L"mp4",  L"Convert to MP4",     GUID{0xbad4218d, 0x0910, 0x41a2, {0x9e, 0xe6, 0x8f, 0xc0, 0x1f, 0x88, 0xdf, 0xc0}}},
    QuickTarget{L"mkv",  L"Convert to MKV",     GUID{0x50481185, 0x2713, 0x4b16, {0xb1, 0xc1, 0xba, 0x98, 0x18, 0xad, 0xe5, 0xbd}}},
    QuickTarget{L"webm", L"Convert to WEBM",    GUID{0x623b7b3f, 0xf84d, 0x464d, {0xa0, 0x01, 0x08, 0xa2, 0x91, 0x51, 0x71, 0x37}}},
    QuickTarget{L"mov",  L"Convert to MOV",     GUID{0x563f36c4, 0x3d0b, 0x4ad5, {0x90, 0x18, 0x52, 0xfb, 0xea, 0x62, 0x0f, 0xae}}},
    QuickTarget{L"avi",  L"Convert to AVI",     GUID{0x528695ec, 0x6e50, 0x4f02, {0x92, 0xa0, 0xd4, 0x0f, 0xa9, 0x33, 0xe8, 0x8d}}},
};

HMODULE moduleHandle{};
std::atomic_ulong liveObjects{};
std::atomic_ulong serverLocks{};

template <typename Function>
HRESULT Safe(Function&& function) noexcept
{
    try {
        return function();
    }
    catch (const std::bad_alloc&) {
        return E_OUTOFMEMORY;
    }
    catch (...) {
        return E_FAIL;
    }
}

class RefCounted
{
public:
    RefCounted() noexcept { ++liveObjects; }
    virtual ~RefCounted() { --liveObjects; }

    ULONG AddRefImpl() noexcept { return InterlockedIncrement(&references_); }
    ULONG ReleaseImpl() noexcept
    {
        const auto remaining = InterlockedDecrement(&references_);
        if (!remaining)
            delete this;
        return remaining;
    }

private:
    volatile LONG references_{1};
};

HRESULT SelectedSupportedPaths(IShellItemArray* items, std::vector<std::wstring>& paths)
{
    if (!items)
        return E_INVALIDARG;

    DWORD count{};
    auto hr = items->GetCount(&count);
    if (FAILED(hr))
        return hr;
    if (count == 0)
        return S_FALSE;

    paths.clear();
    paths.reserve(count);
    for (DWORD index = 0; index < count; ++index) {
        ComPtr<IShellItem> item;
        hr = items->GetItemAt(index, &item);
        if (FAILED(hr))
            return hr;

        PWSTR rawPath{};
        hr = item->GetDisplayName(SIGDN_FILESYSPATH, &rawPath);
        if (FAILED(hr))
            return S_FALSE;
        const std::unique_ptr<wchar_t, decltype(&CoTaskMemFree)> ownedPath(rawPath, CoTaskMemFree);
        std::wstring path(ownedPath.get());
        if (!qc::IsExistingSupportedFile(path))
            return S_FALSE;
        paths.push_back(std::move(path));
    }
    return S_OK;
}

bool SelectionCanConvertTo(const std::vector<std::wstring>& paths, std::wstring_view target)
{
    return !paths.empty() && std::all_of(paths.begin(), paths.end(), [&](const std::wstring& path) {
        return qc::CanConvertTo(path, target);
    });
}

bool SelectionCanQuickConvertTo(const std::vector<std::wstring>& paths, std::wstring_view target)
{
    return !paths.empty() && std::all_of(paths.begin(), paths.end(), [&](const std::wstring& path) {
        return qc::CanQuickConvertTo(path, target);
    });
}

HRESULT ModulePath(std::wstring& path)
{
    path.assign(32768, L'\0');
    const auto length = GetModuleFileNameW(moduleHandle, path.data(), static_cast<DWORD>(path.size()));
    if (!length)
        return HRESULT_FROM_WIN32(GetLastError());
    if (length == path.size())
        return HRESULT_FROM_WIN32(ERROR_INSUFFICIENT_BUFFER);
    path.resize(length);
    return S_OK;
}

enum class CommandKind
{
    Root,
    Target,
    OpenOptions,
};

class CommandEnumerator;

class Command final : public IExplorerCommand, public RefCounted
{
public:
    explicit Command(CommandKind kind, size_t targetIndex = 0) noexcept : kind_(kind), targetIndex_(targetIndex) {}

    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID iid, void** result) noexcept override
    {
        if (!result)
            return E_POINTER;
        *result = nullptr;
        if (IsEqualIID(iid, IID_IUnknown) || IsEqualIID(iid, __uuidof(IExplorerCommand))) {
            *result = static_cast<IExplorerCommand*>(this);
            AddRef();
            return S_OK;
        }
        return E_NOINTERFACE;
    }

    ULONG STDMETHODCALLTYPE AddRef() noexcept override { return AddRefImpl(); }
    ULONG STDMETHODCALLTYPE Release() noexcept override { return ReleaseImpl(); }

    HRESULT STDMETHODCALLTYPE GetTitle(IShellItemArray*, PWSTR* title) noexcept override
    {
        if (!title)
            return E_POINTER;
        *title = nullptr;

        const wchar_t* text = L"QuickConvert";
        if (kind_ == CommandKind::Target && targetIndex_ < QuickTargets.size())
            text = QuickTargets[targetIndex_].title;
        else if (kind_ == CommandKind::OpenOptions)
            text = L"Convert...";
        return SHStrDupW(text, title);
    }

    HRESULT STDMETHODCALLTYPE GetIcon(IShellItemArray*, PWSTR* icon) noexcept override
    {
        if (!icon)
            return E_POINTER;
        *icon = nullptr;
        if (kind_ != CommandKind::Root)
            return E_NOTIMPL;

        return Safe([&]() -> HRESULT {
            std::wstring modulePath;
            auto hr = ModulePath(modulePath);
            if (FAILED(hr))
                return hr;
            const auto resource = qc::IconResourcePath(modulePath);
            return SHStrDupW(resource.c_str(), icon);
        });
    }

    HRESULT STDMETHODCALLTYPE GetToolTip(IShellItemArray*, PWSTR* tooltip) noexcept override
    {
        if (!tooltip)
            return E_POINTER;
        *tooltip = nullptr;
        const wchar_t* text = kind_ == CommandKind::Root
            ? L"Convert selected media files locally"
            : kind_ == CommandKind::OpenOptions
                ? L"Choose format, quality, and output folder"
                : L"Quick convert beside the original file";
        return SHStrDupW(text, tooltip);
    }

    HRESULT STDMETHODCALLTYPE GetCanonicalName(GUID* canonicalName) noexcept override
    {
        if (!canonicalName)
            return E_POINTER;
        if (kind_ == CommandKind::Root)
            *canonicalName = RootCanonical;
        else if (kind_ == CommandKind::OpenOptions)
            *canonicalName = OptionsCanonical;
        else if (targetIndex_ < QuickTargets.size())
            *canonicalName = QuickTargets[targetIndex_].canonical;
        else
            return E_INVALIDARG;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE GetState(IShellItemArray* items, BOOL, EXPCMDSTATE* state) noexcept override
    {
        if (!state)
            return E_POINTER;
        *state = ECS_HIDDEN;

        return Safe([&]() -> HRESULT {
            std::vector<std::wstring> paths;
            if (SelectedSupportedPaths(items, paths) != S_OK)
                return S_OK;

            if (kind_ == CommandKind::Root || kind_ == CommandKind::OpenOptions) {
                *state = ECS_ENABLED;
                return S_OK;
            }

            if (targetIndex_ < QuickTargets.size() && SelectionCanQuickConvertTo(paths, QuickTargets[targetIndex_].extension))
                *state = ECS_ENABLED;
            return S_OK;
        });
    }

    HRESULT STDMETHODCALLTYPE Invoke(IShellItemArray* items, IBindCtx*) noexcept override
    {
        if (kind_ == CommandKind::Root)
            return E_NOTIMPL;

        return Safe([&]() -> HRESULT {
            std::vector<std::wstring> selectedPaths;
            auto hr = SelectedSupportedPaths(items, selectedPaths);
            if (FAILED(hr) || hr == S_FALSE)
                return FAILED(hr) ? hr : E_INVALIDARG;

            std::wstring modulePath;
            hr = ModulePath(modulePath);
            if (FAILED(hr))
                return hr;
            const auto worker = qc::WorkerPath(modulePath);
            if (GetFileAttributesW(worker.c_str()) == INVALID_FILE_ATTRIBUTES)
                return HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND);

            std::wstring arguments;
            if (kind_ == CommandKind::OpenOptions) {
                arguments = qc::BuildOpenArguments(std::span<const std::wstring>(selectedPaths));
            } else {
                if (targetIndex_ >= QuickTargets.size() || !SelectionCanQuickConvertTo(selectedPaths, QuickTargets[targetIndex_].extension))
                    return E_INVALIDARG;
                arguments = qc::BuildWorkerArguments(QuickTargets[targetIndex_].extension, std::span<const std::wstring>(selectedPaths));
            }
            if (arguments.empty())
                return E_INVALIDARG;

            SHELLEXECUTEINFOW info{sizeof(info)};
            info.fMask = SEE_MASK_FLAG_NO_UI;
            info.lpFile = worker.c_str();
            info.lpParameters = arguments.c_str();
            info.nShow = SW_SHOWNORMAL;
            return ShellExecuteExW(&info) ? S_OK : HRESULT_FROM_WIN32(GetLastError());
        });
    }

    HRESULT STDMETHODCALLTYPE GetFlags(EXPCMDFLAGS* flags) noexcept override
    {
        if (!flags)
            return E_POINTER;
        *flags = kind_ == CommandKind::Root ? ECF_HASSUBCOMMANDS : ECF_DEFAULT;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE EnumSubCommands(IEnumExplorerCommand** commands) noexcept override;

private:
    CommandKind kind_;
    size_t targetIndex_{};
};

class CommandEnumerator final : public IEnumExplorerCommand, public RefCounted
{
public:
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID iid, void** result) noexcept override
    {
        if (!result)
            return E_POINTER;
        *result = nullptr;
        if (IsEqualIID(iid, IID_IUnknown) || IsEqualIID(iid, __uuidof(IEnumExplorerCommand))) {
            *result = static_cast<IEnumExplorerCommand*>(this);
            AddRef();
            return S_OK;
        }
        return E_NOINTERFACE;
    }

    ULONG STDMETHODCALLTYPE AddRef() noexcept override { return AddRefImpl(); }
    ULONG STDMETHODCALLTYPE Release() noexcept override { return ReleaseImpl(); }

    HRESULT STDMETHODCALLTYPE Next(ULONG count, IExplorerCommand** commands, ULONG* fetched) noexcept override
    {
        if (!commands || (count != 1 && !fetched))
            return E_POINTER;
        if (fetched)
            *fetched = 0;

        return Safe([&]() -> HRESULT {
            constexpr size_t commandCount = QuickTargets.size() + 1;
            std::vector<ComPtr<IExplorerCommand>> pending;
            pending.reserve(count);
            auto nextIndex = index_;

            while (pending.size() < count && nextIndex < commandCount) {
                ComPtr<IExplorerCommand> command;
                if (nextIndex < QuickTargets.size())
                    command.Attach(new Command(CommandKind::Target, nextIndex));
                else
                    command.Attach(new Command(CommandKind::OpenOptions));
                pending.push_back(std::move(command));
                ++nextIndex;
            }

            for (size_t i = 0; i < pending.size(); ++i)
                commands[i] = pending[i].Detach();
            index_ = nextIndex;
            if (fetched)
                *fetched = static_cast<ULONG>(pending.size());
            return pending.size() == count ? S_OK : S_FALSE;
        });
    }

    HRESULT STDMETHODCALLTYPE Skip(ULONG count) noexcept override
    {
        constexpr size_t commandCount = QuickTargets.size() + 1;
        const auto remaining = commandCount - index_;
        const auto skipped = (std::min)(static_cast<size_t>(count), remaining);
        index_ += skipped;
        return skipped == count ? S_OK : S_FALSE;
    }

    HRESULT STDMETHODCALLTYPE Reset() noexcept override
    {
        index_ = 0;
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE Clone(IEnumExplorerCommand** enumerator) noexcept override
    {
        if (!enumerator)
            return E_POINTER;
        *enumerator = nullptr;
        return Safe([&]() -> HRESULT {
            auto clone = new CommandEnumerator;
            clone->index_ = index_;
            *enumerator = clone;
            return S_OK;
        });
    }

private:
    size_t index_{};
};

HRESULT Command::EnumSubCommands(IEnumExplorerCommand** commands) noexcept
{
    if (!commands)
        return E_POINTER;
    *commands = nullptr;
    if (kind_ != CommandKind::Root)
        return E_NOTIMPL;
    return Safe([&]() -> HRESULT {
        *commands = new CommandEnumerator;
        return S_OK;
    });
}

class ClassFactory final : public IClassFactory, public RefCounted
{
public:
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID iid, void** result) noexcept override
    {
        if (!result)
            return E_POINTER;
        *result = nullptr;
        if (IsEqualIID(iid, IID_IUnknown) || IsEqualIID(iid, IID_IClassFactory)) {
            *result = static_cast<IClassFactory*>(this);
            AddRef();
            return S_OK;
        }
        return E_NOINTERFACE;
    }

    ULONG STDMETHODCALLTYPE AddRef() noexcept override { return AddRefImpl(); }
    ULONG STDMETHODCALLTYPE Release() noexcept override { return ReleaseImpl(); }

    HRESULT STDMETHODCALLTYPE CreateInstance(IUnknown* outer, REFIID iid, void** result) noexcept override
    {
        if (outer)
            return CLASS_E_NOAGGREGATION;
        if (!result)
            return E_POINTER;
        *result = nullptr;
        return Safe([&]() -> HRESULT {
            auto command = new Command(CommandKind::Root);
            const auto hr = command->QueryInterface(iid, result);
            command->Release();
            return hr;
        });
    }

    HRESULT STDMETHODCALLTYPE LockServer(BOOL lock) noexcept override
    {
        if (lock)
            ++serverLocks;
        else
            --serverLocks;
        return S_OK;
    }
};

} // namespace

BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, void*)
{
    if (reason == DLL_PROCESS_ATTACH) {
        moduleHandle = instance;
        DisableThreadLibraryCalls(instance);
    }
    return TRUE;
}

STDAPI DllGetClassObject(REFCLSID clsid, REFIID iid, void** result)
{
    if (!IsEqualCLSID(clsid, CommandClsid))
        return CLASS_E_CLASSNOTAVAILABLE;
    if (!result)
        return E_POINTER;
    *result = nullptr;
    return Safe([&]() -> HRESULT {
        auto factory = new ClassFactory;
        const auto hr = factory->QueryInterface(iid, result);
        factory->Release();
        return hr;
    });
}

STDAPI DllCanUnloadNow()
{
    return liveObjects == 0 && serverLocks == 0 ? S_OK : S_FALSE;
}
