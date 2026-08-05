using Windows.Storage;
using Windows.Storage.Pickers;

namespace AkariTool.Tabs
{
    /// <summary>
    /// File/folder pickers for WinUI 3.
    ///
    /// MIGRATION: replaces the WPF <c>Microsoft.Win32.OpenFileDialog</c> /
    /// <c>SaveFileDialog</c> / <c>OpenFolderDialog</c>. Two differences the call
    /// sites must honour:
    ///   1. WinUI pickers are **async** (the WPF dialogs were synchronous), so
    ///      every call site is awaited.
    ///   2. In an **unpackaged** app a picker has no window to parent to and
    ///      throws unless it is initialised with the main window's HWND — hence
    ///      the InitializeWithWindow call in every helper below.
    ///
    /// Each helper returns the chosen path, or null when the user cancelled
    /// (matching the old <c>ShowDialog() != true</c> guard).
    /// </summary>
    internal static class FilePickers
    {
        private static void Attach(object picker) =>
            WinRT.Interop.InitializeWithWindow.Initialize(picker, MainWindow.WindowHandle);

        /// <summary>Pick a single existing file. <paramref name="extensions"/> e.g. ".iso".</summary>
        public static async Task<string?> OpenFileAsync(params string[] extensions)
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder };
            if (extensions.Length == 0) picker.FileTypeFilter.Add("*");
            else foreach (var ext in extensions) picker.FileTypeFilter.Add(ext);

            Attach(picker);
            StorageFile? file = await picker.PickSingleFileAsync();
            return file?.Path;
        }

        /// <summary>Pick an existing folder.</summary>
        public static async Task<string?> OpenFolderAsync()
        {
            var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder };
            picker.FileTypeFilter.Add("*");   // required, or PickSingleFolderAsync throws

            Attach(picker);
            StorageFolder? folder = await picker.PickSingleFolderAsync();
            return folder?.Path;
        }

        /// <summary>
        /// Pick a save location. <paramref name="typeLabel"/>/<paramref name="extension"/>
        /// mirror the old Filter string, e.g. ("ISO files", ".iso").
        /// </summary>
        public static async Task<string?> SaveFileAsync(string typeLabel, string extension, string suggestedName)
        {
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.ComputerFolder,
                SuggestedFileName = suggestedName,
            };
            picker.FileTypeChoices.Add(typeLabel, new List<string> { extension });

            Attach(picker);
            StorageFile? file = await picker.PickSaveFileAsync();
            return file?.Path;
        }
    }
}
