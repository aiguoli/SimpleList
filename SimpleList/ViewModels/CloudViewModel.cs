using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;

namespace SimpleList.ViewModels
{
    public partial class CloudViewModel : ObservableObject
    {
        public CloudViewModel()
        {
        }

        public void AddDrive(DriveViewModel drive)
        {
            Drives.Add(drive);
        }

        public DriveViewModel GetDrive(string name)
        {
            return Drives.FirstOrDefault(d => d.DisplayName == name);
        }

        public ObservableCollection<DriveViewModel> Drives { get; set; } = new();
    }
}
