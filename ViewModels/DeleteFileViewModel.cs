using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using SimpleList.Services;
using System.Threading.Tasks;

namespace SimpleList.ViewModels
{
    public class DeleteFileViewModel : ObservableObject
    {
        public DeleteFileViewModel(FileViewModel file) 
        {
            File = file;
            DeleteFileCommand = new AsyncRelayCommand(DeleteFile);
        }

        public async Task DeleteFile()
        {
            OneDrive drive = Ioc.Default.GetService<OneDrive>();
            if (PermanentDelete)
            {
                await drive.PermanentDeleteItem(File.Id);
            } else
            {
                await drive.DeleteItem(File.Id);
            }
            await File.Cloud.Refresh();
        }

        private bool _permanentDelete;

        public FileViewModel File;
        public AsyncRelayCommand DeleteFileCommand { get; }
        public bool PermanentDelete
        {
            get => _permanentDelete;
            set => SetProperty(ref _permanentDelete, value);
        }
    }
}
