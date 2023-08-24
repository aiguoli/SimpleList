using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using SimpleList.Services;
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

namespace SimpleList.ViewModels
{
    public class ConvertFileFormatViewModel : ObservableObject
    {
        public ConvertFileFormatViewModel(FileViewModel file)
        {
            _file = file;
            ConvertFileCommand = new AsyncRelayCommand(ConvertFileFormat);
        }

        public async Task ConvertFileFormat()
        {
            Window _downloadPathSelectWindow = new();
            IntPtr hwnd = WindowNative.GetWindowHandle(_downloadPathSelectWindow);
            FileSavePicker savePicker = new()
            {
                SuggestedStartLocation = PickerLocationId.Downloads
            };
            savePicker.FileTypeChoices.Add("PDF Document", new List<string>() { ".pdf" });
            savePicker.SuggestedFileName = Path.GetFileNameWithoutExtension(_file.Name);
            InitializeWithWindow.Initialize(savePicker, hwnd);
            StorageFile file = await savePicker.PickSaveFileAsync();
            SavedFilePath = file?.Path;

            string fileExtension = Path.GetExtension(_file.Name).ToLower();
            
            if (allowedExtensions.Contains(fileExtension))
            {
                await drive.ConvertFileFormat(_file.Id, file);
            }
        }

        private readonly FileViewModel _file;
        private readonly OneDrive drive = Ioc.Default.GetService<OneDrive>();
        private static readonly string[] allowedExtensions = { ".csv", ".doc", ".docx", ".odp", ".ods", ".odt", ".pot", ".potm", ".potx", ".pps", ".ppsx", ".ppsxm", ".ppt", ".pptm", ".pptx", ".rtf", ".xls", ".xlsx" };
        private string _selectedFormat = "pdf";
        private string _savedFilePath;

        public IEnumerable<string> TargetFormats => new List<string> { "pdf" };
        public string SelectedFormat { get => _selectedFormat; set => SetProperty(ref _selectedFormat, value); }
        public string SavedFilePath { get => _savedFilePath; set => SetProperty(ref _savedFilePath, value); }
        public string FormattedExtensions => string.Join(", ", allowedExtensions.Select(ext => ext.TrimStart('.')));
        public AsyncRelayCommand ConvertFileCommand { get; }
    }
}
