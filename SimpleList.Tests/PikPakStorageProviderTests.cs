using SimpleList.Core.Contracts;
using SimpleList.Core.Models;
using SimpleList.Core.Services;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SimpleList.Tests;

public class PikPakStorageProviderTests
{
    [Fact]
    public async Task LoginAndList_UsesPikPakApiHeadersAndMapsResponse()
    {
        Queue<Func<HttpRequestMessage, HttpResponseMessage>> responses = new([
            request =>
            {
                Assert.Contains("/shield/captcha/init", request.RequestUri.ToString());
                return Json("""{"captcha_token":"captcha-1"}""");
            },
            request =>
            {
                Assert.Contains("/v1/auth/signin", request.RequestUri.ToString());
                return Json("""{"refresh_token":"refresh-1","access_token":"access-1","sub":"user-1"}""");
            },
            request =>
            {
                Assert.Contains("/shield/captcha/init", request.RequestUri.ToString());
                Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
                return Json("""{"captcha_token":"captcha-2"}""");
            },
            request =>
            {
                Assert.Contains("/drive/v1/files", request.RequestUri.ToString());
                Assert.Equal("api-drive.mypikpak.com", request.RequestUri.Host);
                Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
                Assert.Equal("access-1", request.Headers.Authorization?.Parameter);
                Assert.True(request.Headers.Contains("X-Device-ID"));
                Assert.True(request.Headers.Contains("X-Captcha-Token"));
                return Json("""
                    {
                      "files": [
                        { "id": "file-1", "kind": "drive#file", "name": "readme.txt", "size": "5" }
                      ],
                      "next_page_token": ""
                    }
                    """);
            },
        ]);

        using HttpClient httpClient = new(new StubHandler(request => responses.Dequeue()(request)));
        var store = new MemoryCredentialStore();
        var provider = new PikPakStorageProvider("Root", "me@example.com", "secret", store, httpClient: httpClient);

        StorageResult<bool> login = await provider.LoginAsync();
        StorageResult<PageResult<FileItem>> list = await provider.ListChildrenAsync("Root");

        Assert.True(login.IsSuccess);
        Assert.True(list.IsSuccess);
        FileItem item = Assert.Single(list.Data.Items);
        Assert.Equal("readme.txt", item.Name);
        Assert.Equal(5, item.Size);
        Assert.NotNull(store.Saved);
        Assert.Equal("me@example.com", store.Saved.Username);
        Assert.Equal("secret", store.Saved.Password);
        Assert.Equal("refresh-1", store.Saved.RefreshToken);
        Assert.Equal("captcha-2", store.Saved.CaptchaToken);
    }

    [Fact]
    public async Task Login_DoesNotSaveCredentials_WhenRememberPasswordIsDisabled()
    {
        Queue<Func<HttpRequestMessage, HttpResponseMessage>> responses = new([
            request =>
            {
                Assert.Contains("/shield/captcha/init", request.RequestUri.ToString());
                return Json("""{"captcha_token":"captcha-1"}""");
            },
            request =>
            {
                Assert.Contains("/v1/auth/signin", request.RequestUri.ToString());
                return Json("""{"refresh_token":"refresh-1","access_token":"access-1","sub":"user-1"}""");
            },
            request =>
            {
                Assert.Contains("/shield/captcha/init", request.RequestUri.ToString());
                return Json("""{"captcha_token":"captcha-2"}""");
            },
        ]);

        using HttpClient httpClient = new(new StubHandler(request => responses.Dequeue()(request)));
        var store = new MemoryCredentialStore();
        var provider = new PikPakStorageProvider("Root", "me@example.com", "secret", store, httpClient: httpClient, rememberPassword: false);

        StorageResult<bool> login = await provider.LoginAsync();

        Assert.True(login.IsSuccess);
        Assert.Null(store.Saved);
    }

    [Fact]
    public async Task Login_OpensCaptchaChallengeAndRetriesUntilTokenIsAvailable()
    {
        Queue<Func<HttpRequestMessage, HttpResponseMessage>> responses = new([
            request =>
            {
                Assert.Contains("/shield/captcha/init", request.RequestUri.ToString());
                return Json("""{"captcha_token":"challenge-1","url":"https://verify.example/captcha"}""");
            },
            request =>
            {
                Assert.Contains("/shield/captcha/init", request.RequestUri.ToString());
                string body = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                Assert.Contains("\"captcha_token\":\"challenge-1\"", body);
                return Json("""{"captcha_token":"captcha-1"}""");
            },
            request =>
            {
                Assert.Contains("/v1/auth/signin", request.RequestUri.ToString());
                return Json("""{"refresh_token":"refresh-1","access_token":"access-1","sub":"user-1"}""");
            },
            request =>
            {
                Assert.Contains("/shield/captcha/init", request.RequestUri.ToString());
                return Json("""{"captcha_token":"captcha-2"}""");
            },
        ]);

        string openedUrl = null;
        using HttpClient httpClient = new(new StubHandler(request => responses.Dequeue()(request)));
        var store = new MemoryCredentialStore();
        var provider = new PikPakStorageProvider(
            "Root",
            "me@example.com",
            "secret",
            store,
            httpClient: httpClient,
            captchaChallengeHandler: (url, _) =>
            {
                openedUrl = url;
                return Task.CompletedTask;
            });

        StorageResult<bool> login = await provider.LoginAsync();

        Assert.True(login.IsSuccess);
        Assert.Equal("https://verify.example/captcha", openedUrl);
        Assert.Equal("captcha-2", store.Saved.CaptchaToken);
    }

    [Fact]
    public async Task Login_RefreshesCaptchaAndRetries_WhenCachedCaptchaIsInvalid()
    {
        Queue<Func<HttpRequestMessage, HttpResponseMessage>> responses = new([
            request =>
            {
                Assert.Contains("/v1/auth/signin", request.RequestUri.ToString());
                Assert.True(request.Headers.TryGetValues("X-Captcha-Token", out IEnumerable<string> values));
                Assert.Contains("stale-captcha", values);
                return Json("""{"error_code":4002,"error":"captcha_invalid","error_description":"captcha invalid"}""");
            },
            request =>
            {
                Assert.Contains("/shield/captcha/init", request.RequestUri.ToString());
                return Json("""{"captcha_token":"captcha-1"}""");
            },
            request =>
            {
                Assert.Contains("/v1/auth/signin", request.RequestUri.ToString());
                Assert.True(request.Headers.TryGetValues("X-Captcha-Token", out IEnumerable<string> values));
                Assert.Contains("captcha-1", values);
                return Json("""{"refresh_token":"refresh-1","access_token":"access-1","sub":"user-1"}""");
            },
            request =>
            {
                Assert.Contains("/shield/captcha/init", request.RequestUri.ToString());
                return Json("""{"captcha_token":"captcha-2"}""");
            },
        ]);

        using HttpClient httpClient = new(new StubHandler(request => responses.Dequeue()(request)));
        var store = new MemoryCredentialStore
        {
            Cached = new PikPakCredentials
            {
                ServerUrl = "Root",
                Username = "me@example.com",
                Password = "secret",
                CaptchaToken = "stale-captcha",
            },
        };
        var provider = new PikPakStorageProvider("Root", "me@example.com", credentialStore: store, httpClient: httpClient);

        StorageResult<bool> login = await provider.LoginAsync();

        Assert.True(login.IsSuccess);
        Assert.Equal("refresh-1", store.Saved.RefreshToken);
        Assert.Equal("captcha-2", store.Saved.CaptchaToken);
    }

    [Fact]
    public async Task Upload_ReturnsInvalidRequestUntilOssUploadIsImplemented()
    {
        var provider = new PikPakStorageProvider("Root", "me@example.com", "secret");

        StorageResult<FileItem> result = await provider.UploadFileContentAsync(new System.IO.MemoryStream(), "a.txt", "Root");

        Assert.False(result.IsSuccess);
        Assert.Equal(StorageErrorType.InvalidRequest, result.ErrorType);
    }

    private static HttpResponseMessage Json(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json),
        };
    }

    private class MemoryCredentialStore : IPikPakCredentialStore
    {
        public PikPakCredentials Cached { get; set; }
        public PikPakCredentials Saved { get; private set; }

        public PikPakCredentials Get(string serverUrl, string username)
        {
            return Cached;
        }

        public void Save(PikPakCredentials credentials)
        {
            Saved = credentials;
        }
    }

    private class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }
}
