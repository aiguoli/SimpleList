using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;

namespace SimpleList.ViewModels
{
    public partial class DeleteFileViewModel : ObservableObject
    {
        public DeleteFileViewModel(FileViewModel file) 
        {
            _drive = file.Drive;
            _file = file;
        }

        [RelayCommand]
        public async Task DeleteFile()
        {
            if (PermanentDelete)
            {
                await _drive.Provider.PermanentDeleteItem(File.Id);
            } else
            {
                await _drive.Provider.DeleteItem(File.Id);
            }
            await File.Drive.Refresh();
        }

        private readonly DriveViewModel _drive;
        [ObservableProperty] private bool _permanentDelete;
        [ObservableProperty] private FileViewModel _file;
    }
}
