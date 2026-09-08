using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SimpleList.Helpers;
using SimpleList.ViewModels;
using System;

namespace SimpleList.Pages;

public sealed partial class OperationLogPage : Page
{
    public OperationLogViewModel ViewModel { get; }

    public OperationLogPage()
    {
        InitializeComponent();
        ViewModel = new OperationLogViewModel(App.GetService<Core.Services.OperationLogService>());
        DataContext = ViewModel;
        Loaded += (_, _) => ViewModel.StartListening();
        Unloaded += (_, _) => ViewModel.StopListening();
    }

    private async void ClearLogs_Click(object sender, RoutedEventArgs e)
    {
        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = ResourceHelper.GetLocalized("OperationLogPage_ClearDialog_Title"),
            Content = ResourceHelper.GetLocalized("OperationLogPage_ClearDialog_Content"),
            PrimaryButtonText = ResourceHelper.GetLocalized("OperationLogPage_ClearDialog_Confirm"),
            CloseButtonText = ResourceHelper.GetLocalized("OperationLogPage_ClearDialog_Cancel"),
            DefaultButton = ContentDialogButton.Close,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            ViewModel.Clear();
        }
    }
}
