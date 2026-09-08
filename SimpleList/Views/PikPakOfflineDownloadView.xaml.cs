using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SimpleList.Core.Contracts;
using SimpleList.Core.Models;
using SimpleList.Helpers;
using System;

namespace SimpleList.Views;

public sealed partial class PikPakOfflineDownloadView : ContentDialog
{
    private readonly IOfflineDownloadProvider _provider;
    private readonly string _parentId;

    public PikPakOfflineDownloadView(IOfflineDownloadProvider provider, string parentId)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _parentId = parentId;
        InitializeComponent();
    }

    public bool Succeeded { get; private set; }

    private void SourceUrl_TextChanged(object sender, TextChangedEventArgs e)
    {
        IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(SourceUrl.Text);
        ErrorMessage.Visibility = Visibility.Collapsed;
    }

    private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ContentDialogButtonClickDeferral deferral = args.GetDeferral();
        args.Cancel = true;
        IsPrimaryButtonEnabled = false;
        IsSecondaryButtonEnabled = false;
        SubmitProgress.Visibility = Visibility.Visible;
        ErrorMessage.Visibility = Visibility.Collapsed;

        try
        {
            StorageResult<OfflineDownloadTask> result = await _provider.CreateOfflineDownloadAsync(
                SourceUrl.Text,
                _parentId,
                FileName.Text);

            if (!result.IsSuccess)
            {
                ErrorMessage.Text = result.ErrorMessage;
                ErrorMessage.Visibility = Visibility.Visible;
                return;
            }

            Succeeded = true;
            args.Cancel = false;
            OperationLogger.Record(
                "OperationLog_Category_Task",
                "OperationLog_Action_OfflineDownloadCreated",
                result.Data.Id,
                OperationLogLevel.Success);
        }
        catch (Exception ex)
        {
            ErrorMessage.Text = ex.Message;
            ErrorMessage.Visibility = Visibility.Visible;
        }
        finally
        {
            SubmitProgress.Visibility = Visibility.Collapsed;
            IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(SourceUrl.Text);
            IsSecondaryButtonEnabled = true;
            deferral.Complete();
        }
    }
}
