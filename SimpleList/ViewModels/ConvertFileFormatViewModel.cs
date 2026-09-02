using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Windows.Storage;
using WinRT.Interop;
using Microsoft.UI.Xaml;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using System.Linq;
using SimpleList.Helpers;

namespace SimpleList.ViewModels
{
    public partial class ConvertFileFormatViewModel : ObservableObject
    {
        public ConvertFileFormatViewModel(FileViewModel file)
        {
            _file = file;
        }

        [RelayCommand]
        public async Task ConvertFileFormat()
        {
            Window _downloadPathSelectWindow = new();
            IntPtr hwnd = WindowNative.GetWindowHandle(_downloadPathSelectWindow);
            FileSavePicker savePicker = new()
            {
                SuggestedStartLocation = PickerLocationId.Downloads
            };
            savePicker.FileTypeChoices.Add(ResourceHelper.GetLocalized("FilePicker_PdfDocument"), [".pdf"]);
            savePicker.SuggestedFileName = Path.GetFileNameWithoutExtension(_file.Name);
            InitializeWithWindow.Initialize(savePicker, hwnd);
            StorageFile file = await savePicker.PickSaveFileAsync();
            SavedFilePath = file?.Path;
            string fileExtension = Path.GetExtension(_file.Name).ToLower();

            if (allowedExtensions.Contains(fileExtension) && file != null)
            {
                await _file.Drive.Provider.ConvertToPdfAsync(_file.Id, file);
            }
        }

        private readonly FileViewModel _file;
        private static readonly string[] allowedExtensions = { ".csv", ".doc", ".docx", ".odp", ".ods", ".odt", ".pot", ".potm", ".potx", ".pps", ".ppsx", ".ppsxm", ".ppt", ".pptm", ".pptx", ".rtf", ".xls", ".xlsx" };
        [ObservableProperty] public partial string SelectedFormat { get; set; } = "pdf";

        [ObservableProperty] public partial string SavedFilePath { get; set; }

        public static IEnumerable<string> TargetFormats => ["pdf"];
        public string FormattedExtensions => string.Join(", ", allowedExtensions.Select(ext => ext.TrimStart('.')));
    }
}
