using Microsoft.UI.Xaml.Controls;
using SimpleList.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SimpleList.Pages
{
    public partial class ToolPage : Page, INotifyPropertyChanged
    {
        public ToolPage()
        {
            InitializeComponent();
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(storage, value)) return false;

            storage = value;
            NotifyPropertyChanged(propertyName);
            return true;
        }

        protected void NotifyPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private IEnumerable<ToolItem> _items = [
            new() {
                Name = "Share Community",
                Description = "Share and browse OneDrive files",
                ImagePath = "/Assets/link-share.png",
                FileName = "ShareCommunity"
            },
            new() {
                Name = "External Downloader",
                Description = "Downlaod files with external downloader.",
                ImagePath = "/Assets/external-downloader.png",
                FileName = "ExternalDownloader"
            }
        ];
        public IEnumerable<ToolItem> Items
        {
            get => _items;
            set => SetProperty(ref _items, value);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnToolItemClick(object sender, ItemClickEventArgs e)
        {
            ToolItem item = (ToolItem)e.ClickedItem;
            Type pageType = Type.GetType("SimpleList.Pages.Tools." + item.FileName);
            (App.StartupWindow as MainWindow).Navigate(pageType);
        }
    }
}
