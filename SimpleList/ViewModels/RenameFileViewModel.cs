using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;

namespace SimpleList.ViewModels
{
    public partial class RenameFileViewModel : ObservableObject
    {
        public RenameFileViewModel(DriveViewModel drive, FileViewModel file)
        {
            Drive = drive;
            _file = file;
            _fileName = file.Name;
        }

        [RelayCommand]
        private async Task RenameFile()
        {
            await Drive.Provider.RenameFile(_file.Id, FileName);
            await Drive.Refresh();
        }

        [ObservableProperty] private string _fileName;
        private readonly FileViewModel _file;
        public DriveViewModel Drive { get; }
    }
}
