using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using SimpleList.Services;
using System;
using System.Threading.Tasks;

namespace SimpleList.ViewModels
{
    public class ShareFileViewModel : ObservableObject
    {
        public ShareFileViewModel(FileViewModel file)
        {
            _file = file;
            ShareFileCommand = new AsyncRelayCommand(ShareFile);
        }

        private async Task<string> ShareFile()
        {
            PreventClose = true;
            ShareLink = await _drive.CreateLink(_file.Id, _expirationDateTime, _password, _type == 0 ? "view" : "edit");
            Finished = true;
            return ShareLink;
        }

        private readonly OneDrive _drive = Ioc.Default.GetService<OneDrive>();
        private readonly FileViewModel _file;
        public AsyncRelayCommand ShareFileCommand { get; }
        private string _password;
        private DateTimeOffset _expirationDateTime;
        private int _type = 0;
        private bool _finished = false;
        private string _shareLink;

        public static DateTime Today => DateTime.Today;
        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }
        public DateTimeOffset ExpirationDateTime
        {
            get => _expirationDateTime;
            set => SetProperty(ref _expirationDateTime, value);
        }
        public int Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }
        public bool Finished
        {
            get => _finished;
            set => SetProperty(ref _finished, value);
        }
        public bool PreventClose { get; set; } = true;
        public string ShareLink
        {
            get => _shareLink;
            set => SetProperty(ref _shareLink, value);
        }
    }
}
