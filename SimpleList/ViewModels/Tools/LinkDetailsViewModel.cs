using CommunityToolkit.Mvvm.ComponentModel;
using SimpleList.Models;

namespace SimpleList.ViewModels.Tools
{
    partial class LinkDetailsViewModel : ObservableObject
    {
        public LinkDetailsViewModel(ShareCommunityLink link)
        {
            Link = link;
        }

        public LinkDetailsViewModel(string linkId)
        {
        }

        [ObservableProperty]
        public partial ShareCommunityLink Link { get; set; }

        public string Title => Link.Title;
        public string Content => Link.Url;
        public string Provider => Link.ProviderType;
        public string Password => Link.Password;
        public string ExpireDate => Link.ExpiresAt?.ToString() ?? string.Empty;
        public string CreatedAt => Link.CreatedAt.ToString();
        public string UpdatedAt => Link.UpdatedAt.ToString();
        public int Views => Link.Views;
    }
}
