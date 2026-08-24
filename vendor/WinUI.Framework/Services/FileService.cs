using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace WinUI.Framework.Services;

/// <summary>
/// <see cref="IFileService"/> implementation. Every picker is attached to the
/// app window via <see cref="InitializeWithWindow.Initialize"/>, otherwise the
/// system shows an error about a missing owner window.
/// </summary>
public class FileService : IFileService
{
    public nint WindowHandle { get; set; }

    public async Task<StorageFile?> PickSingleFileAsync(IReadOnlyList<string>? fileTypeFilter = null)
    {
        var picker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };

        AddFilter(picker.FileTypeFilter, fileTypeFilter);
        InitializeWithWindow.Initialize(picker, WindowHandle);

        return await picker.PickSingleFileAsync();
    }

    public async Task<IReadOnlyList<StorageFile>> PickMultipleFilesAsync(IReadOnlyList<string>? fileTypeFilter = null)
    {
        var picker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };

        AddFilter(picker.FileTypeFilter, fileTypeFilter);
        InitializeWithWindow.Initialize(picker, WindowHandle);

        return await picker.PickMultipleFilesAsync();
    }

    public async Task<StorageFolder?> PickFolderAsync()
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };

        // A folder picker requires at least one file type filter.
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowHandle);

        return await picker.PickSingleFolderAsync();
    }

    public async Task<StorageFile?> SaveFileAsync(string suggestedFileName, IReadOnlyList<string>? fileTypeFilter = null)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = suggestedFileName,
        };

        if (fileTypeFilter is { Count: > 0 })
        {
            picker.FileTypeChoices.Add("Files", new List<string>(fileTypeFilter));
        }
        else
        {
            picker.FileTypeChoices.Add("All files", new List<string> { "*" });
        }

        InitializeWithWindow.Initialize(picker, WindowHandle);

        return await picker.PickSaveFileAsync();
    }

    public async Task<string> ReadTextAsync(StorageFile file) => await FileIO.ReadTextAsync(file);

    public async Task WriteTextAsync(StorageFile file, string content) => await FileIO.WriteTextAsync(file, content);

    private static void AddFilter(IList<string> filter, IReadOnlyList<string>? extensions)
    {
        if (extensions is { Count: > 0 })
        {
            foreach (var extension in extensions)
            {
                filter.Add(extension);
            }
        }
        else
        {
            // "*" means "any file type".
            filter.Add("*");
        }
    }
}
