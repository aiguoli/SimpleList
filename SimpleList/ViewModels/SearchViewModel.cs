using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;

namespace SimpleList.ViewModels
{
    public partial class SearchViewModel : ObservableObject
    {
        public SearchViewModel(DriveViewModel drive)
        {
            _drive = drive;
        }

        [RelayCommand]
        private async Task Search()
        {
            if (string.IsNullOrEmpty(FileName))
                return;
            if (Mode == SearchMode.Local)
            {
                _drive.FilterByName(FileName);
            } else
            {
                await _drive.SearchFile(FileName);
            }
        }

        public enum SearchMode
        {
            Local,
            Global,
        }

        private readonly DriveViewModel _drive;
        [ObservableProperty] private string _fileName;
        [ObservableProperty] private SearchMode _mode;
    }
}
