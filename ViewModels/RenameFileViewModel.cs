using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SimpleList.ViewModels
{
    public class RenameFileViewModel : ObservableObject
    {
        public RenameFileViewModel(CloudViewModel cloud, FileViewModel file)
        {
            Cloud = cloud;
            _file = file;
            _fileName = file.Name;
            RenameFileCommand = new RelayCommand(RenameFile);
        }

        private async void RenameFile()
        {
            await Cloud.Drive.RenameFile(_file.Id, _fileName);
            Cloud.Refresh();
        }

        private string _fileName;
        private readonly FileViewModel _file;
        public CloudViewModel Cloud { get; }
        public string FileName
        {
            get => _fileName;
            set => SetProperty(ref _fileName, value);
        }
        public RelayCommand RenameFileCommand { get; }
    }
}
