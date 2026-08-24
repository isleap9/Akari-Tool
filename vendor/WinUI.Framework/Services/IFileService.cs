using Windows.Storage;

namespace WinUI.Framework.Services;

/// <summary>
/// Wraps the Windows file pickers (open / save / folder) and file I/O.
/// The system pickers are Win32 dialogs, so they must be initialized with the
/// app's window handle (HWND) before being shown.
/// </summary>
public interface IFileService
{
    /// <summary>The app window handle used to initialize the pickers. Set once at startup.</summary>
    nint WindowHandle { get; set; }

    /// <summary>Opens the file picker for a single file. Pass null for "all files".</summary>
    Task<StorageFile?> PickSingleFileAsync(IReadOnlyList<string>? fileTypeFilter = null);

    /// <summary>Opens the file picker for multiple files.</summary>
    Task<IReadOnlyList<StorageFile>> PickMultipleFilesAsync(IReadOnlyList<string>? fileTypeFilter = null);

    /// <summary>Opens a folder picker.</summary>
    Task<StorageFolder?> PickFolderAsync();

    /// <summary>Opens the save-as picker for a single file.</summary>
    Task<StorageFile?> SaveFileAsync(string suggestedFileName, IReadOnlyList<string>? fileTypeFilter = null);

    /// <summary>Reads a text file.</summary>
    Task<string> ReadTextAsync(StorageFile file);

    /// <summary>Writes text to a file.</summary>
    Task WriteTextAsync(StorageFile file, string content);
}
