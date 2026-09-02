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
                Description = "Share and browse multi-cloud links",
                ImagePath = "/Assets/link-share.png",
                FileName = "ShareCommunity"
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
            if (e.ClickedItem is ToolItem item
                && App.StartupWindow is MainWindow window)
            {
                Type pageType = item.FileName switch
                {
                    "ShareCommunity" => typeof(SimpleList.Pages.Tools.ShareCommunity),
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(item.FileName),
                        item.FileName,
                        "Unknown tool page")
                };
                window.Navigate(pageType);
            }
        }
    }
}
