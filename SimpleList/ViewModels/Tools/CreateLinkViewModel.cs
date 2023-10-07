using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using SimpleList.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SimpleList.ViewModels.Tools
{
    public partial class CreateLinkViewModel : ObservableObject
    {
        public CreateLinkViewModel(ShareCommunityViewModel community)
        {
            _community = community;
        }

        [RelayCommand]
        public async Task CreateLink()
        {
            if (string.IsNullOrEmpty(Title) || string.IsNullOrEmpty(Content) || string.IsNullOrEmpty(Category))
            {
                return;
            }
            IConfigurationRoot configuration = (IConfigurationRoot)App.Current.Resources["Configuration"];
            string apiUrl = configuration.GetSection("Tools:ShareCommunity:Url").Value;
            string response = await _client.PostAsync(apiUrl + "/api/links", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "title", Title },
                { "content", Content },
                { "password", Password },
                { "expiration_date", Expiration.ToString("yyyy-MM-dd") },
                { "category", Category }
            })).Result.Content.ReadAsStringAsync();
            CreateLinkResponse createLinkResponse = JsonSerializer.Deserialize(response, CreateLinkResponseJsonContext.Default.CreateLinkResponse);
            if (createLinkResponse.code == 200)
            {
                await _community.Refresh();
            }
        }


        [JsonSerializable(typeof(CreateLinkResponse), GenerationMode = JsonSourceGenerationMode.Metadata)]
        internal partial class CreateLinkResponseJsonContext : JsonSerializerContext { }

        private ShareCommunityViewModel _community;
        private readonly HttpClient _client = new();
        [ObservableProperty] private string _title;
        [ObservableProperty] private string _content;
        [ObservableProperty] private string _password;
        [ObservableProperty] private DateTimeOffset _expiration;
        [ObservableProperty] private string _category = "OneDrive";

        public List<string> Categories { get; } = new() { "OneDrive" };
        public static DateTimeOffset Today { get; } = DateTimeOffset.Now;
    }
}
