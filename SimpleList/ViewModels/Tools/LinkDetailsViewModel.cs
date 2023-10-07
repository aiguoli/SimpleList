using CommunityToolkit.Mvvm.ComponentModel;
using SimpleList.Models;

namespace SimpleList.ViewModels.Tools
{
    partial class LinkDetailsViewModel : ObservableObject
    {
        public LinkDetailsViewModel(Link link)
        {
            _link = link;
        }

        public LinkDetailsViewModel(string linkId)
        {
        }

        [ObservableProperty]private Link _link;
        public string Title => Link.title;
        public string Content => Link.content;
        public string Password => Link.password;
        public string ExpireDate => Link.expire_date.ToString();
        public string CreatedAt => Link.CreatedAt.ToString();
        public string UpdatedAt => Link.UpdatedAt.ToString();
        public int Views => Link.views;
    }
}
