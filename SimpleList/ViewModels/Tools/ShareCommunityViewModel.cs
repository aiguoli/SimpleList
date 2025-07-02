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
using WinUICommunity;

namespace SimpleList.ViewModels.Tools
{
    public partial class ShareCommunityViewModel : ObservableObject
    {
        [RelayCommand]
        public async Task Refresh()
        {
            try
            {
                string response = await _client.GetStringAsync(_apiUrl + "/api/links");
                LinksResponse linksResponse = JsonSerializer.Deserialize(response, LinksResponseJsonContext.Default.LinksResponse);
                Links = linksResponse.data;
            }
            catch (Exception e)
            {
                Growl.Error(e.Message);
                App.LogError("Error fetching links", this, e);
            }
        }

        [JsonSerializable(typeof(LinksResponse), GenerationMode = JsonSourceGenerationMode.Metadata)]
        internal partial class LinksResponseJsonContext : JsonSerializerContext { }

        [ObservableProperty] private IEnumerable<ShareCommunityLink> _links;
        private readonly HttpClient _client = new();
        private readonly string _apiUrl = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build().GetSection("Tools:ShareCommunity:Url").Value;
    }
}
