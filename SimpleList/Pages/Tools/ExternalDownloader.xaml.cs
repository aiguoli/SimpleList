using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SimpleList.ViewModels.Tools;
using System;

namespace SimpleList.Pages.Tools;
public sealed partial class ExternalDownloader : Page
{
  public ExternalDownloader()
  {
    InitializeComponent();
    DataContext = new ExternalDownloaderViewModel();
  }

  private void ParseShareUrl(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
  {
    if (string.IsNullOrEmpty(InputUrlBox.Text)) return;
    if (Uri.TryCreate(InputUrlBox.Text, UriKind.Absolute, out Uri shareUrl))
    {
      if (shareUrl.Host.EndsWith("sharepoint.com"))
      {

      }
    }
  }

    private void ChangeConfigTemplate(object sender, SelectionChangedEventArgs e)
    {
        (DataContext as ExternalDownloaderViewModel).Result = "";
        DownloaderConfig.ContentTemplate = (DataTemplate)Resources[e.AddedItems[0]];
    }
}
