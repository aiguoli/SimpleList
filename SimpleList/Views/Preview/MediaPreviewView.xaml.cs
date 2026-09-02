using Microsoft.UI.Xaml;
using SimpleList.Helpers;
using SimpleList.ViewModels;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Windows.Graphics;

namespace SimpleList.Views.Preview
{
    public sealed partial class MediaPreviewView : Window
    {
        private static readonly HashSet<MediaPreviewView> OpenWindows = [];
        private readonly CancellationTokenSource _mediaLoadCancellation = new();
        private bool _isCloseRequested;
        private bool _isCleanedUp;

        public MediaPreviewView()
        {
            InitializeComponent();
            Title = ResourceHelper.GetLocalized("MediaPreviewView_Title");
            AppWindow.Resize(new SizeInt32(960, 600));
            AppWindow.Closing += (_, _) => Cleanup();
        }

        public object DataContext
        {
            get => RootGrid.DataContext;
            set => RootGrid.DataContext = value;
        }

        public void Show()
        {
            OpenWindows.Add(this);
            Activate();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isCloseRequested)
            {
                return;
            }

            _isCloseRequested = true;
            Cleanup();

            // Closing a WinUI window synchronously from a routed Click handler can fail
            // with E_ABORT because the button is still dispatching the event.
            DispatcherQueue.TryEnqueue(CloseAfterClick);
        }

        private void CloseAfterClick()
        {
            try
            {
                Close();
            }
            catch (COMException ex) when (ex.HResult == unchecked((int)0x80004004))
            {
                // E_ABORT means another close operation has already started.
            }
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            Cleanup();
            OpenWindows.Remove(this);
            _mediaLoadCancellation.Dispose();
        }

        private void Cleanup()
        {
            if (_isCleanedUp)
            {
                return;
            }

            _isCleanedUp = true;
            _mediaLoadCancellation.Cancel();
            var mediaPlayer = Player.MediaPlayer;
            mediaPlayer?.Pause();
            Player.Source = null;
            mediaPlayer?.Dispose();
            if (DataContext is PreviewViewModel vm)
            {
                vm.CleanupMediaPreview();
            }
        }

        private async void LoadDownloadUrlAsync(object sender, RoutedEventArgs e)
        {
            if (DataContext is PreviewViewModel vm)
            {
                await vm.LoadMediaSource(_mediaLoadCancellation.Token);
            }
        }
    }
}
