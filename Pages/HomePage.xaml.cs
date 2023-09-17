using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.Graph.Models;
using Microsoft.UI.Xaml.Controls;
using SimpleList.Helpers;
using SimpleList.Services;

namespace SimpleList.Pages
{
    public sealed partial class HomePage : Page
    {
        public HomePage()
        {
            InitializeComponent();
            GetDisplayName();
            GetTotalUsed();
        }

        private async void GetTotalUsed()
        {
            OneDrive drive = Ioc.Default.GetService<OneDrive>();
            if (drive.IsAuthenticated)
            {
                Quota info = await drive.GetStorageInfo();
                StorageText.Text = "HomePage_Capacity".GetLocalized() + Utils.ReadableFileSize(info.Used ?? 0) + "/" + Utils.ReadableFileSize(info.Total ?? 0);
            }
        }

        private async void GetDisplayName()
        {
            OneDrive drive = Ioc.Default.GetService<OneDrive>();
            if (drive.IsAuthenticated)
            {
                GreetingText.Text = "HomePage_Greeting".GetLocalized() + await drive.GetDisplayName();
            }
        }

    }
}
