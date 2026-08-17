using System.IO;
using System.Runtime.InteropServices;
using Windows.Storage;
using WinUI.Framework.Services;

namespace AkariTool.Services;

/// <summary>
/// App-local <see cref="IFileService"/> that replaces the framework's default
/// (<c>WinUI.Framework.Services.FileService</c>) — registered AFTER
/// <c>AddWinUIFrameworkCore()</c> so MS.DI's last-registration-wins picks this one, with
/// ZERO call-site changes in BackupViewModel / WimUtilService / AdvancedToolsPage.
///
/// WHY (Phase 4 diagnosis): the framework uses WinRT <c>Windows.Storage.Pickers</c>
/// (FileOpenPicker/FileSavePicker/FolderPicker), which throw <c>COMException 0x80004005</c>
/// under process elevation — the picker broker refuses a High-integrity, unpackaged caller,
/// and <c>InitializeWithWindow</c> does not fix the integrity mismatch. The app ships
/// <c>requireAdministrator</c>, so every file dialog was broken in normal launch mode.
///
/// FIX: drive the classic Win32 COM common dialogs (<c>IFileOpenDialog</c>/
/// <c>IFileSaveDialog</c> via <c>CoCreateInstance</c>) instead. These work at ANY integrity
/// level, elevated or not. Results are converted path → <see cref="StorageFile"/>/
/// <see cref="StorageFolder"/> via the path-based (broker-free) WinRT statics, which are
/// also elevation-safe. Callers only read <c>.Path</c>, so the surface is preserved.
///
/// Touches no framework file. No tweak logic — <c>[WARMUP]</c> unaffected. No Defender code.
/// </summary>
public sealed class AkariFileService : IFileService
{
    public nint WindowHandle { get; set; }

    // ── IFileService ────────────────────────────────────────────────────────

    public async Task<StorageFile?> PickSingleFileAsync(IReadOnlyList<string>? fileTypeFilter = null)
    {
        var paths = ShowOpenDialog(fileTypeFilter, pickFolders: false, multiSelect: false);
        var path = paths?.FirstOrDefault();
        return path is null ? null : await StorageFile.GetFileFromPathAsync(path);
    }

    public async Task<IReadOnlyList<StorageFile>> PickMultipleFilesAsync(IReadOnlyList<string>? fileTypeFilter = null)
    {
        var paths = ShowOpenDialog(fileTypeFilter, pickFolders: false, multiSelect: true);
        if (paths is null || paths.Count == 0) return [];
        var files = new List<StorageFile>(paths.Count);
        foreach (var p in paths) files.Add(await StorageFile.GetFileFromPathAsync(p));
        return files;
    }

    public async Task<StorageFolder?> PickFolderAsync()
    {
        var paths = ShowOpenDialog(fileTypeFilter: null, pickFolders: true, multiSelect: false);
        var path = paths?.FirstOrDefault();
        return path is null ? null : await StorageFolder.GetFolderFromPathAsync(path);
    }

    public async Task<StorageFile?> SaveFileAsync(string suggestedFileName, IReadOnlyList<string>? fileTypeFilter = null)
    {
        var path = ShowSaveDialog(suggestedFileName, fileTypeFilter);
        if (path is null) return null;

        // WinRT FileSavePicker returns a StorageFile that already exists (name reserved).
        // Match that: open-or-create at the chosen path (no truncation — callers overwrite
        // via .Path). Path-based WinRT statics don't use the picker broker.
        var dir = Path.GetDirectoryName(path);
        var name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(name)) return null;
        var folder = await StorageFolder.GetFolderFromPathAsync(dir);
        return await folder.CreateFileAsync(name, CreationCollisionOption.OpenIfExists);
    }

    public Task<string> ReadTextAsync(StorageFile file) => FileIO.ReadTextAsync(file).AsTask();

    public Task WriteTextAsync(StorageFile file, string content) => FileIO.WriteTextAsync(file, content).AsTask();

    // ── Win32 COM common dialogs ──────────────────────────────────────────────

    /// <summary>Returns picked path(s), or null when the user cancels.</summary>
    private List<string>? ShowOpenDialog(IReadOnlyList<string>? fileTypeFilter, bool pickFolders, bool multiSelect)
    {
        var dialog = (IFileOpenDialog)new FileOpenDialogRcw();
        try
        {
            dialog.GetOptions(out var opts);
            opts |= FOS_FORCEFILESYSTEM | FOS_FILEMUSTEXIST | FOS_PATHMUSTEXIST;
            if (pickFolders) opts |= FOS_PICKFOLDERS;
            if (multiSelect) opts |= FOS_ALLOWMULTISELECT;
            dialog.SetOptions(opts);

            if (!pickFolders) ApplyFilters(dialog, fileTypeFilter);

            int hr = dialog.Show(WindowHandle);
            if (hr == ERROR_CANCELLED) return null;
            if (hr != 0) Marshal.ThrowExceptionForHR(hr);

            var results = new List<string>();
            if (multiSelect)
            {
                dialog.GetResults(out var array);
                try
                {
                    array.GetCount(out uint count);
                    for (uint i = 0; i < count; i++)
                    {
                        array.GetItemAt(i, out var item);
                        results.Add(PathOf(item));
                    }
                }
                finally { Marshal.ReleaseComObject(array); }
            }
            else
            {
                dialog.GetResult(out var item);
                results.Add(PathOf(item));
            }
            return results;
        }
        finally { Marshal.ReleaseComObject(dialog); }
    }

    /// <summary>Returns the chosen save path, or null when the user cancels.</summary>
    private string? ShowSaveDialog(string suggestedFileName, IReadOnlyList<string>? fileTypeFilter)
    {
        var dialog = (IFileSaveDialog)new FileSaveDialogRcw();
        try
        {
            dialog.GetOptions(out var opts);
            dialog.SetOptions(opts | FOS_FORCEFILESYSTEM | FOS_OVERWRITEPROMPT | FOS_PATHMUSTEXIST);

            ApplyFilters(dialog, fileTypeFilter);
            if (!string.IsNullOrEmpty(suggestedFileName))
            {
                dialog.SetFileName(suggestedFileName);
                var ext = Path.GetExtension(suggestedFileName).TrimStart('.');
                if (!string.IsNullOrEmpty(ext)) dialog.SetDefaultExtension(ext);
            }

            int hr = dialog.Show(WindowHandle);
            if (hr == ERROR_CANCELLED) return null;
            if (hr != 0) Marshal.ThrowExceptionForHR(hr);

            dialog.GetResult(out var item);
            return PathOf(item);
        }
        finally { Marshal.ReleaseComObject(dialog); }
    }

    /// <summary>Maps the interface's extension list onto a single COMDLG filter (+ all-files).</summary>
    private static void ApplyFilters(IFileDialog dialog, IReadOnlyList<string>? fileTypeFilter)
    {
        var exts = fileTypeFilter?.Where(e => !string.IsNullOrWhiteSpace(e) && e != "*").ToList();
        if (exts is null || exts.Count == 0)
        {
            var all = new[] { new COMDLG_FILTERSPEC { pszName = "All files", pszSpec = "*.*" } };
            dialog.SetFileTypes((uint)all.Length, all);
            return;
        }

        // ".json" → "*.json"; multiple → "*.json;*.xml"
        var spec = string.Join(";", exts.Select(e => "*" + (e.StartsWith('.') ? e : "." + e)));
        var name = "Files (" + spec + ")";
        var specs = new[]
        {
            new COMDLG_FILTERSPEC { pszName = name, pszSpec = spec },
            new COMDLG_FILTERSPEC { pszName = "All files", pszSpec = "*.*" },
        };
        dialog.SetFileTypes((uint)specs.Length, specs);
    }

    private static string PathOf(IShellItem item)
    {
        try
        {
            item.GetDisplayName(SIGDN_FILESYSPATH, out var ptr);
            try { return Marshal.PtrToStringUni(ptr) ?? string.Empty; }
            finally { Marshal.FreeCoTaskMem(ptr); }
        }
        finally { Marshal.ReleaseComObject(item); }
    }

    // ── Interop declarations (canonical vtable order) ─────────────────────────

    private const int ERROR_CANCELLED = unchecked((int)0x800704C7);
    private const uint SIGDN_FILESYSPATH = 0x80058000;

    private const uint FOS_OVERWRITEPROMPT   = 0x00000002;
    private const uint FOS_PICKFOLDERS       = 0x00000020;
    private const uint FOS_FORCEFILESYSTEM   = 0x00000040;
    private const uint FOS_ALLOWMULTISELECT  = 0x00000200;
    private const uint FOS_PATHMUSTEXIST     = 0x00000800;
    private const uint FOS_FILEMUSTEXIST     = 0x00001000;

    [ComImport, Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
    private class FileOpenDialogRcw { }

    [ComImport, Guid("C0B4E2F3-BA21-4773-8DBA-335EC946EB8B")]
    private class FileSaveDialogRcw { }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct COMDLG_FILTERSPEC
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string pszName;
        [MarshalAs(UnmanagedType.LPWStr)] public string pszSpec;
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("b4db1657-70d7-485e-8e3e-6fcb5a5c1802")]
    private interface IModalWindow
    {
        [PreserveSig] int Show(nint parent);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("42f85136-db7e-439c-85f1-e4075d135fc8")]
    private interface IFileDialog : IModalWindow
    {
        // IModalWindow
        [PreserveSig] new int Show(nint parent);
        // IFileDialog
        void SetFileTypes(uint cFileTypes, [In, MarshalAs(UnmanagedType.LPArray)] COMDLG_FILTERSPEC[] rgFilterSpec);
        void SetFileTypeIndex(uint iFileType);
        void GetFileTypeIndex(out uint piFileType);
        void Advise(nint pfde, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOptions(uint fos);
        void GetOptions(out uint pfos);
        void SetDefaultFolder(IShellItem psi);
        void SetFolder(IShellItem psi);
        void GetFolder(out IShellItem ppsi);
        void GetCurrentSelection(out IShellItem ppsi);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        void GetResult(out IShellItem ppsi);
        void AddPlace(IShellItem psi, int fdap);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        void Close([MarshalAs(UnmanagedType.Error)] int hr);
        void SetClientGuid(ref Guid guid);
        void ClearClientData();
        void SetFilter(nint pFilter);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("d57c7288-d4ad-4768-be02-9d969532d960")]
    private interface IFileOpenDialog : IFileDialog
    {
        // IModalWindow
        [PreserveSig] new int Show(nint parent);
        // IFileDialog
        new void SetFileTypes(uint cFileTypes, [In, MarshalAs(UnmanagedType.LPArray)] COMDLG_FILTERSPEC[] rgFilterSpec);
        new void SetFileTypeIndex(uint iFileType);
        new void GetFileTypeIndex(out uint piFileType);
        new void Advise(nint pfde, out uint pdwCookie);
        new void Unadvise(uint dwCookie);
        new void SetOptions(uint fos);
        new void GetOptions(out uint pfos);
        new void SetDefaultFolder(IShellItem psi);
        new void SetFolder(IShellItem psi);
        new void GetFolder(out IShellItem ppsi);
        new void GetCurrentSelection(out IShellItem ppsi);
        new void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        new void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
        new void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        new void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
        new void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        new void GetResult(out IShellItem ppsi);
        new void AddPlace(IShellItem psi, int fdap);
        new void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        new void Close([MarshalAs(UnmanagedType.Error)] int hr);
        new void SetClientGuid(ref Guid guid);
        new void ClearClientData();
        new void SetFilter(nint pFilter);
        // IFileOpenDialog
        void GetResults(out IShellItemArray ppenum);
        void GetSelectedItems(out IShellItemArray ppsai);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("84bccd23-5fde-4cdb-aea4-af64b83d78ab")]
    private interface IFileSaveDialog : IFileDialog
    {
        // IModalWindow
        [PreserveSig] new int Show(nint parent);
        // IFileDialog
        new void SetFileTypes(uint cFileTypes, [In, MarshalAs(UnmanagedType.LPArray)] COMDLG_FILTERSPEC[] rgFilterSpec);
        new void SetFileTypeIndex(uint iFileType);
        new void GetFileTypeIndex(out uint piFileType);
        new void Advise(nint pfde, out uint pdwCookie);
        new void Unadvise(uint dwCookie);
        new void SetOptions(uint fos);
        new void GetOptions(out uint pfos);
        new void SetDefaultFolder(IShellItem psi);
        new void SetFolder(IShellItem psi);
        new void GetFolder(out IShellItem ppsi);
        new void GetCurrentSelection(out IShellItem ppsi);
        new void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        new void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
        new void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        new void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
        new void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        new void GetResult(out IShellItem ppsi);
        new void AddPlace(IShellItem psi, int fdap);
        new void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        new void Close([MarshalAs(UnmanagedType.Error)] int hr);
        new void SetClientGuid(ref Guid guid);
        new void ClearClientData();
        new void SetFilter(nint pFilter);
        // IFileSaveDialog
        void SetSaveAsItem(IShellItem psi);
        void SetProperties(nint pStore);
        void SetCollectedProperties(nint pList, int fAppendDefault);
        void GetProperties(out nint ppStore);
        void ApplyProperties(IShellItem psi, nint pStore, nint hwnd, nint pSink);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
    private interface IShellItem
    {
        void BindToHandler(nint pbc, ref Guid bhid, ref Guid riid, out nint ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(uint sigdnName, out nint ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("b63ea76d-1f85-456f-a19c-48159efa858b")]
    private interface IShellItemArray
    {
        void BindToHandler(nint pbc, ref Guid bhid, ref Guid riid, out nint ppvOut);
        void GetPropertyStore(int flags, ref Guid riid, out nint ppv);
        void GetPropertyDescriptionList(nint keyType, ref Guid riid, out nint ppv);
        void GetAttributes(int attribFlags, uint sfgaoMask, out uint psfgaoAttribs);
        void GetCount(out uint pdwNumItems);
        void GetItemAt(uint dwIndex, out IShellItem ppsi);
        void EnumItems(out nint ppenumShellItems);
    }
}
