using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SimpleList.Helpers;
using SimpleList.Models;
using SimpleList.Services;
using SimpleList.ViewModels.Tools;
using SimpleList.Views.Tools;
using System;
using System.Threading.Tasks;
using WinUICommunity;

namespace SimpleList.Pages.Tools;

public sealed partial class ShareCommunity : Page
{
    private bool _initialized;

    public ShareCommunity()
    {
        InitializeComponent();
        DataContext = App.GetService<ShareCommunityViewModel>();
    }

    private ShareCommunityViewModel ViewModel => (ShareCommunityViewModel)DataContext;

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
        {
            return;
        }
        _initialized = true;
        await ViewModel.Refresh();
        UpdateAccountButton();

        if (!ViewModel.Api.IsAuthenticated && !ViewModel.Api.HasSeenAuthPrompt)
        {
            ViewModel.Api.HasSeenAuthPrompt = true;
            await ShowAuthChoiceAsync();
        }
    }

    private async void ShowLinkDetailsDialogAsync(object sender, RoutedEventArgs e)
    {
        LinkDetails dialog = new()
        {
            XamlRoot = XamlRoot,
            DataContext = new LinkDetailsViewModel((sender as Button)?.DataContext as ShareCommunityLink)
        };
        await dialog.ShowAsync();
    }

    private async void ShowCreateLinkDialogAsync(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.Api.IsAuthenticated && !await ShowAuthChoiceAsync())
        {
            return;
        }
        CreateLink dialog = new()
        {
            XamlRoot = XamlRoot,
            DataContext = new CreateLinkViewModel(ViewModel)
        };
        await dialog.ShowAsync();
    }

    private async void AccountButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.Api.IsAuthenticated)
        {
            await ShowAuthChoiceAsync();
            return;
        }

        try
        {
            await ViewModel.Logout();
            UpdateAccountButton();
        }
        catch (Exception ex)
        {
            Growl.Error(ex.Message);
        }
    }

    private async Task<bool> ShowAuthChoiceAsync()
    {
        ContentDialog prompt = new()
        {
            XamlRoot = XamlRoot,
            Title = Localized("ShareCommunityAuth_Title", "Join the share community"),
            Content = new TextBlock
            {
                Text = Localized("ShareCommunityAuth_Description", "Sign in or create an account to publish and collect links. You can continue browsing as a guest."),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 420,
            },
            PrimaryButtonText = Localized("ShareCommunityAuth_Login", "Sign in"),
            SecondaryButtonText = Localized("ShareCommunityAuth_Register", "Register"),
            CloseButtonText = Localized("ShareCommunityAuth_Guest", "Browse as guest"),
            DefaultButton = ContentDialogButton.Primary,
        };
        ContentDialogResult choice = await prompt.ShowAsync();
        bool authenticated = choice switch
        {
            ContentDialogResult.Primary => await ShowCredentialsAsync(register: false),
            ContentDialogResult.Secondary => await ShowCredentialsAsync(register: true),
            _ => false,
        };
        UpdateAccountButton();
        return authenticated;
    }

    private async Task<bool> ShowCredentialsAsync(bool register)
    {
        InfoBar errorBar = new()
        {
            IsOpen = false,
            IsClosable = false,
            Severity = InfoBarSeverity.Error,
        };
        TextBox email = new() { Header = Localized("ShareCommunityAuth_Email", "Email") };
        TextBox username = new()
        {
            Header = Localized("ShareCommunityAuth_Username", "Username"),
            Visibility = register ? Visibility.Visible : Visibility.Collapsed,
        };
        PasswordBox password = new() { Header = Localized("ShareCommunityAuth_Password", "Password") };
        StackPanel fields = new() { Spacing = 12, MinWidth = 360 };
        fields.Children.Add(errorBar);
        fields.Children.Add(email);
        fields.Children.Add(username);
        fields.Children.Add(password);

        ContentDialog dialog = new()
        {
            XamlRoot = XamlRoot,
            Title = register
                ? Localized("ShareCommunityAuth_Register", "Register")
                : Localized("ShareCommunityAuth_Login", "Sign in"),
            Content = fields,
            PrimaryButtonText = register
                ? Localized("ShareCommunityAuth_Register", "Register")
                : Localized("ShareCommunityAuth_Login", "Sign in"),
            CloseButtonText = Localized("Cancel", "Cancel"),
            DefaultButton = ContentDialogButton.Primary,
        };
        bool authenticated = false;
        dialog.PrimaryButtonClick += async (_, args) =>
        {
            ContentDialogButtonClickDeferral deferral = args.GetDeferral();
            args.Cancel = true;
            dialog.IsPrimaryButtonEnabled = false;
            errorBar.IsOpen = false;
            try
            {
                if (string.IsNullOrWhiteSpace(email.Text) || string.IsNullOrWhiteSpace(password.Password))
                {
                    throw new InvalidOperationException(Localized("ShareCommunityAuth_Required", "Email and password are required."));
                }
                if (register && string.IsNullOrWhiteSpace(username.Text))
                {
                    throw new InvalidOperationException(Localized("ShareCommunityAuth_UsernameRequired", "Username is required."));
                }

                if (register)
                {
                    await ViewModel.Api.RegisterAsync(email.Text, username.Text, password.Password);
                }
                else
                {
                    await ViewModel.Api.LoginAsync(email.Text, password.Password);
                }
                authenticated = true;
                args.Cancel = false;
                ViewModel.NotifySessionChanged();
            }
            catch (Exception ex)
            {
                errorBar.Message = ex.Message;
                errorBar.IsOpen = true;
                App.LogError(register ? "Share community registration failed" : "Share community login failed", this, ex);
            }
            finally
            {
                dialog.IsPrimaryButtonEnabled = true;
                deferral.Complete();
            }
        };

        await dialog.ShowAsync();
        return authenticated;
    }

    private void UpdateAccountButton()
    {
        AccountButton.Label = ViewModel.Api.IsAuthenticated
            ? $"{ViewModel.Api.CurrentUser?.Username} · {Localized("ShareCommunityAuth_Logout", "Sign out")}"
            : Localized("ShareCommunityAuth_Login", "Sign in");
    }

    private static string Localized(string key, string fallback)
    {
        string value = SimpleList.Helpers.ResourceHelper.GetLocalized(key);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
