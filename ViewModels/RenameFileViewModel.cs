using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;

namespace SimpleList.ViewModels
{
    public partial class RenameFileViewModel : ObservableObject
    {
        public RenameFileViewModel(CloudViewModel cloud, FileViewModel file)
        {
            Cloud = cloud;
            _file = file;
            _fileName = file.Name;
        }

        [RelayCommand]
        private async Task RenameFile()
        {
            await Cloud.Drive.RenameFile(_file.Id, FileName);
            await Cloud.Refresh();
        }

        [ObservableProperty] private string _fileName;
        private readonly FileViewModel _file;
        public CloudViewModel Cloud { get; }
    }
}
