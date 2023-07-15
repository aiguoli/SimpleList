using Microsoft.UI.Xaml.Controls;
using SimpleList.Services;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace SimpleList.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class HomePage : Page
    {
        public HomePage()
        {
            InitializeComponent();
            // GetTotalUsed();
        }

        private async void GetTotalUsed()
        {
            long totalUsed = 0;
            var files = await (new OneDrive().GetFiles());
            foreach (var file in files)
            {
                totalUsed += file.Size ?? 0;
            }
        }

        private static string ReadableFileSize(long size)
        {
            if (size == 0) return "0";
            if (size < 1024)
            {
                return size.ToString("F0") + " bytes";
            }

            if (size >> 10 < 1024)
            {
                return (size / 1024F).ToString("F1") + " KB";
            }

            if (size >> 20 < 1024)
            {
                return ((size >> 10) / 1024F).ToString("F1") + " MB";
            }

            if (size >> 30 < 1024)
            {
                return ((size >> 20) / 1024F).ToString("F1") + " GB";
            }

            if (size >> 40 < 1024)
            {
                return ((size >> 30) / 1024F).ToString("F1") + " TB";
            }

            if (size >> 50 < 1024)
            {
                return ((size >> 40) / 1024F).ToString("F1") + " PB";
            }
            return ((size >> 50) / 1024F).ToString("F1") + " EB";
        }
    }
}
