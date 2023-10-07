using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using SimpleList.Services;
using System.Threading.Tasks;

namespace SimpleList.ViewModels
{
    public partial class DeleteFileViewModel : ObservableObject
    {
        public DeleteFileViewModel(FileViewModel file) 
        {
            File = file;
        }

        [RelayCommand]
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

        [ObservableProperty] private bool _permanentDelete;

        public FileViewModel File;
    }
}
