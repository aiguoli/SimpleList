using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace SimpleList.ViewModels
{
    public partial class ShareFileViewModel : ObservableObject
    {
        public ShareFileViewModel(FileViewModel file)
        {
            _file = file;
        }

        [RelayCommand]
        private async Task<string> ShareFile()
        {
            PreventClose = true;
            ShareLink = await _file.Drive.Provider.CreateLink(_file.Id, ExpirationDateTime, Password, Type == 0 ? "view" : "edit");
            Finished = true;
            return ShareLink;
        }

        [RelayCommand]
        private async Task CopyToClipboard()
        {
            DataPackage package = new();
            package.SetText(ShareLink);
            Clipboard.SetContent(package);
        }

        private readonly FileViewModel _file;
        [ObservableProperty] private string _password;
        [ObservableProperty] private DateTimeOffset _expirationDateTime;
        [ObservableProperty] private int _type = 0;
        [ObservableProperty] private bool _finished = false;
        [ObservableProperty] private string _shareLink;

        public static DateTime Today => DateTime.Today;
        public bool PreventClose { get; set; } = true;
    }
}
