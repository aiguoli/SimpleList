using SimpleList.Core.Contracts;
using SimpleList.Core.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace SimpleList.Core.Services;

public class PikPakStorageProvider : StorageProviderBase, IStorageProvider
{
    private const string ClientId = "YUMx5nI8ZU8Ap8pm";
    private const string ClientSecret = "dbw2OtmVEeuUvIptb1Coyg";
    private const string ClientVersion = "2.0.0";
    private const string PackageName = "mypikpak.com";
    private const string DriveApiBaseUrl = "https://api-drive.mypikpak.com";
    private const string UserApiBaseUrl = "https://user.mypikpak.net";
    private const string RootId = "Root";
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/117.0.0.0 Safari/537.36";
    private const int CaptchaVerificationMaxAttempts = 30;
    private const int CaptchaVerificationRetryDelayMs = 2000;

    private static readonly string[] Algorithms =
    [
        "C9qPpZLN8ucRTaTiUMWYS9cQvWOE",
        "+r6CQVxjzJV6LCV",
        "F",
        "pFJRC",
        "9WXYIDGrwTCz2OiVlgZa90qpECPD6olt",
        "/750aCr4lm/Sly/c",
        "RB+DT/gZCrbV",
        "",
        "CyLsf7hdkIRxRm215hl",
        "7xHvLi2tOYP0Y92b",
        "ZGTXXxu8E/MIWaEDB+Sm/",
        "1UI3",
        "E7fP5Pfijd+7K+t6Tg/NhuLq0eEUVChpJSkrKxpO",
        "ihtqpG6FMt65+Xk+tWUH2",
        "NhXXU9rg4XXdzo7u5o",
    ];

    private readonly IPikPakCredentialStore _credentialStore;
    private readonly HttpClient _httpClient;
    private readonly bool _rememberPassword;
    private readonly Func<string, CancellationToken, Task> _captchaChallengeHandler;
    private string _password;
    private string _refreshToken;
    private string _accessToken;
    private string _captchaToken;
    private string _deviceId;
    private string _userId;

    public PikPakStorageProvider(
        string driveId,
        string username,
        string password = null,
        IPikPakCredentialStore credentialStore = null,
        IStringLocalizer localizer = null,
        HttpClient httpClient = null,
        bool rememberPassword = true,
        Func<string, CancellationToken, Task> captchaChallengeHandler = null)
        : base(localizer)
    {
        DriveId = string.IsNullOrWhiteSpace(driveId) ? RootId : driveId;
        AccountId = username;
        _password = password;
        _credentialStore = credentialStore;
        _httpClient = httpClient ?? new HttpClient();
        _rememberPassword = rememberPassword;
        _captchaChallengeHandler = captchaChallengeHandler;
    }

    public ProviderType ProviderType => ProviderType.PikPak;
    public string AccountId { get; private set; }
    public string DriveId { get; private set; }
    public bool IsAuthenticated { get; private set; }
    public bool SupportsTrash => true;
    public ShareCapabilities ShareCapabilities => global::SimpleList.Core.Models.ShareCapabilities.Unsupported;

    public async Task<StorageResult<bool>> LoginAsync(CancellationToken ct = default)
    {
        try
        {
            await LoginInternalAsync(ct).ConfigureAwait(false);
            return StorageResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            return StorageResult<bool>.Failure($"{L("LoginFail", "Login failed")}: {ex.Message}", StorageErrorType.Authentication, ex);
        }
    }

    public Task<StorageResult<string>> GetDisplayNameAsync(CancellationToken ct = default)
    {
        return ExecuteAsync(() => Task.FromResult(AccountId), () => ValidateNotEmpty(AccountId, nameof(AccountId)));
    }

    public Task<StorageResult<StorageQuota>> GetQuotaAsync(CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            PikPakAboutResponse about = await RequestAsync<PikPakAboutResponse>(
                HttpMethod.Get,
                $"{DriveApiBaseUrl}/drive/v1/about",
                ct: ct).ConfigureAwait(false);

            long? total = TryParseLong(about?.Quota?.Limit);
            long? used = TryParseLong(about?.Quota?.Usage);
            long? deleted = TryParseLong(about?.Quota?.UsageInTrash);
            return new StorageQuota
            {
                Total = total,
                Used = used,
                Deleted = deleted,
                Remaining = total.HasValue && used.HasValue ? Math.Max(0, total.Value - used.Value) : null,
            };
        });
    }

    public Task<StorageResult<PageResult<FileItem>>> ListChildrenAsync(string parentId, string pageToken = null, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            Dictionary<string, string> query = new()
            {
                ["parent_id"] = NormalizeId(parentId),
                ["thumbnail_size"] = "SIZE_LARGE",
                ["with_audit"] = "true",
                ["limit"] = "100",
                ["filters"] = "{\"phase\":{\"eq\":\"PHASE_TYPE_COMPLETE\"},\"trashed\":{\"eq\":false}}",
                ["page_token"] = pageToken ?? string.Empty,
            };

            PikPakFilesResponse response = await RequestAsync<PikPakFilesResponse>(
                HttpMethod.Get,
                $"{DriveApiBaseUrl}/drive/v1/files",
                query,
                ct: ct).ConfigureAwait(false);

            return PikPakMappers.ToPageResult(response);
        }, () => ValidateNotEmpty(parentId, nameof(parentId)));
    }

    public Task<StorageResult<FileItem>> GetItemAsync(string itemId, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            if (NormalizeId(itemId) == string.Empty)
            {
                return new FileItem
                {
                    Id = RootId,
                    Name = "Root",
                    IsFolder = true,
                    Provider = ProviderType.PikPak,
                };
            }

            PikPakFile file = await RequestAsync<PikPakFile>(
                HttpMethod.Get,
                $"{DriveApiBaseUrl}/drive/v1/files/{Uri.EscapeDataString(itemId)}",
                new Dictionary<string, string>
                {
                    ["_magic"] = "2021",
                    ["usage"] = "FETCH",
                    ["thumbnail_size"] = "SIZE_LARGE",
                },
                ct: ct).ConfigureAwait(false);
            return PikPakMappers.ToFileItem(file);
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public Task<StorageResult<Stream>> GetItemContentAsync(string itemId, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            string url = await GetDownloadUrlInternalAsync(itemId, ct).ConfigureAwait(false);
            HttpResponseMessage response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            try
            {
                await EnsureHttpSuccessAsync(response).ConfigureAwait(false);
                Stream stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                return (Stream)new HttpResponseOwnedStream(stream, response);
            }
            catch
            {
                response.Dispose();
                throw;
            }
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public Task<StorageResult<bool>> DownloadFileAsync(string itemId, Stream destination, IProgress<long> progress = null, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            ValidateNotNull(destination, nameof(destination));
            string url = await GetDownloadUrlInternalAsync(itemId, ct).ConfigureAwait(false);
            using HttpResponseMessage response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            await EnsureHttpSuccessAsync(response).ConfigureAwait(false);
            await using Stream source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await CopyToAsync(source, destination, progress, ct).ConfigureAwait(false);
            return true;
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public Task<StorageResult<ThumbnailSet>> GetThumbnailsAsync(string itemId, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            FileItem item = await GetItemInternalAsync(itemId, ct).ConfigureAwait(false);
            if (item.ProviderTokens != null
                && item.ProviderTokens.TryGetValue("thumbnailLink", out string thumbnail)
                && !string.IsNullOrWhiteSpace(thumbnail))
            {
                return new ThumbnailSet
                {
                    SmallUrl = thumbnail,
                    MediumUrl = thumbnail,
                    LargeUrl = thumbnail,
                };
            }

            throw new FileNotFoundException(L("ThumbnailUnavailable", "Thumbnail is not available"), itemId);
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public Task<StorageResult<string>> GetDownloadUrlAsync(string itemId, CancellationToken ct = default)
    {
        return ExecuteAsync(() => GetDownloadUrlInternalAsync(itemId, ct), () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public Task<StorageResult<FileItem>> CreateFolderAsync(string parentId, string name, string conflictBehavior = "rename", CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            PikPakFile file = await RequestAsync<PikPakFile>(
                HttpMethod.Post,
                $"{DriveApiBaseUrl}/drive/v1/files",
                body: new PikPakCreateFolderRequest
                {
                    Kind = "drive#folder",
                    ParentId = NormalizeId(parentId),
                    Name = name,
                },
                ct: ct).ConfigureAwait(false);
            return PikPakMappers.ToFileItem(file);
        }, () =>
        {
            ValidateNotEmpty(parentId, nameof(parentId));
            ValidateFileName(name, nameof(name));
        });
    }

    public Task<StorageResult<FileItem>> RenameAsync(string itemId, string newName, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            PikPakFile file = await RequestAsync<PikPakFile>(
                HttpMethod.Patch,
                $"{DriveApiBaseUrl}/drive/v1/files/{Uri.EscapeDataString(itemId)}",
                body: new PikPakRenameRequest
                {
                    Name = newName,
                },
                ct: ct).ConfigureAwait(false);
            return PikPakMappers.ToFileItem(file);
        }, () =>
        {
            ValidateNotEmpty(itemId, nameof(itemId));
            ValidateFileName(newName, nameof(newName));
        });
    }

    public Task<StorageResult<bool>> DeleteAsync(string itemId, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            await RequestAsync<JsonElement>(
                HttpMethod.Post,
                $"{DriveApiBaseUrl}/drive/v1/files:batchTrash",
                body: new PikPakIdsRequest
                {
                    Ids = [itemId],
                },
                ct: ct).ConfigureAwait(false);
            return true;
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public Task<StorageResult<bool>> PermanentDeleteAsync(string itemId, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            await RequestAsync<JsonElement>(
                HttpMethod.Post,
                $"{DriveApiBaseUrl}/drive/v1/files:batchDelete",
                body: new PikPakIdsRequest
                {
                    Ids = [itemId],
                },
                ct: ct).ConfigureAwait(false);
            return true;
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public Task<StorageResult<FileItem>> RestoreAsync(string itemId, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            await RequestAsync<JsonElement>(
                HttpMethod.Post,
                $"{DriveApiBaseUrl}/drive/v1/files:batchUntrash",
                body: new PikPakIdsRequest
                {
                    Ids = [itemId],
                },
                ct: ct).ConfigureAwait(false);

            return await GetItemInternalAsync(itemId, ct).ConfigureAwait(false);
        }, () => ValidateNotEmpty(itemId, nameof(itemId)));
    }

    public Task<StorageResult<PageResult<FileItem>>> ListTrashAsync(string pageToken = null, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            Dictionary<string, string> query = new()
            {
                ["thumbnail_size"] = "SIZE_LARGE",
                ["with_audit"] = "true",
                ["limit"] = "100",
                ["filters"] = "{\"phase\":{\"eq\":\"PHASE_TYPE_COMPLETE\"},\"trashed\":{\"eq\":true}}",
                ["page_token"] = pageToken ?? string.Empty,
            };

            PikPakFilesResponse response = await RequestAsync<PikPakFilesResponse>(
                HttpMethod.Get,
                $"{DriveApiBaseUrl}/drive/v1/files",
                query,
                ct: ct).ConfigureAwait(false);

            return PikPakMappers.ToPageResult(response);
        });
    }

    public Task<StorageResult<bool>> EmptyTrashAsync(CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            string pageToken = null;
            do
            {
                StorageResult<PageResult<FileItem>> result = await ListTrashAsync(pageToken, ct).ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    throw result.Exception ?? new InvalidOperationException(result.ErrorMessage);
                }

                PageResult<FileItem> page = result.Data;
                List<string> ids = page?.Items?.Select(item => item.Id).Where(id => !string.IsNullOrWhiteSpace(id)).ToList() ?? [];
                if (ids.Count > 0)
                {
                    await RequestAsync<JsonElement>(
                        HttpMethod.Post,
                        $"{DriveApiBaseUrl}/drive/v1/files:batchDelete",
                        body: new PikPakIdsRequest
                        {
                            Ids = ids,
                        },
                        ct: ct).ConfigureAwait(false);
                }

                pageToken = page?.NextPageToken;
            }
            while (!string.IsNullOrWhiteSpace(pageToken));

            return true;
        });
    }

    public Task<StorageResult<FileItem>> UploadFileAsync(StorageFile file, string parentId, IProgress<long> progress = null, string resumeToken = null, Action<string> resumeTokenCallback = null, CancellationToken ct = default)
    {
        return Task.FromResult(StorageResult<FileItem>.Failure(L("PikPak_UploadUnsupported", "PikPak upload requires Aliyun OSS signing and is not supported yet"), StorageErrorType.InvalidRequest));
    }

    public Task<StorageResult<FileItem>> UploadFileContentAsync(Stream content, string fileName, string parentId, long? size = null, IProgress<long> progress = null, CancellationToken ct = default)
    {
        return Task.FromResult(StorageResult<FileItem>.Failure(L("PikPak_UploadUnsupported", "PikPak upload requires Aliyun OSS signing and is not supported yet"), StorageErrorType.InvalidRequest));
    }

    public Task<StorageResult<FileItem>> UploadFolderAsync(StorageFolder folder, string parentId, IProgress<long> overallProgress = null, IProgress<FolderUploadProgressInfo> detailProgress = null, CancellationToken ct = default)
    {
        return Task.FromResult(StorageResult<FileItem>.Failure(L("PikPak_UploadUnsupported", "PikPak upload requires Aliyun OSS signing and is not supported yet"), StorageErrorType.InvalidRequest));
    }

    public Task<StorageResult<ShareLink>> CreateLinkAsync(string itemId, DateTimeOffset? expiration = null, string password = null, string type = "view", CancellationToken ct = default)
    {
        return Task.FromResult(StorageResult<ShareLink>.Failure(L("PikPak_ShareUnsupported", "PikPak share links are not supported yet"), StorageErrorType.InvalidRequest));
    }

    public Task<StorageResult<ShareLink>> GetShareLinkAsync(string itemId, CancellationToken ct = default)
    {
        return Task.FromResult(StorageResult<ShareLink>.Failure(L("PikPak_ShareUnsupported", "PikPak share links are not supported yet"), StorageErrorType.InvalidRequest));
    }

    public Task<StorageResult<bool>> RevokeShareLinkAsync(string itemId, CancellationToken ct = default)
    {
        return Task.FromResult(StorageResult<bool>.Failure(L("PikPak_ShareUnsupported", "PikPak share links are not supported yet"), StorageErrorType.InvalidRequest));
    }

    public Task<StorageResult<bool>> ConvertToPdfAsync(string itemId, StorageFile destination, CancellationToken ct = default)
    {
        return Task.FromResult(StorageResult<bool>.Failure(L("PikPak_PdfConversionUnsupported", "PikPak does not support PDF conversion"), StorageErrorType.InvalidRequest));
    }

    public Task<StorageResult<PageResult<FileItem>>> SearchAsync(string query, string scopeParentId = null, CancellationToken ct = default)
    {
        return ExecuteAsync(async () =>
        {
            List<FileItem> items = [];
            await CollectChildrenAsync(string.IsNullOrWhiteSpace(scopeParentId) ? RootId : scopeParentId, items, ct).ConfigureAwait(false);
            return new PageResult<FileItem>
            {
                Items = items
                    .Where(item => item.Name?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                    .ToList(),
            };
        }, () => ValidateNotEmpty(query, nameof(query)));
    }

    protected override Task EnsureAuthenticatedAsync()
    {
        return IsAuthenticated ? Task.CompletedTask : LoginInternalAsync(CancellationToken.None);
    }

    protected override StorageResult<T> HandleException<T>(Exception exception)
    {
        return exception switch
        {
            PikPakApiException ex when ex.ErrorCode is 4121 or 4122 or 16 => StorageResult<T>.Failure(L("PikPak_AuthenticationExpired", "PikPak authentication expired"), StorageErrorType.Authentication, exception),
            PikPakApiException ex when ex.ErrorCode is 9 or 4002 => StorageResult<T>.Failure(L("PikPak_CaptchaExpired", "PikPak captcha token expired"), StorageErrorType.Authentication, exception),
            PikPakApiException ex => StorageResult<T>.Failure(ex.Message, StorageErrorType.InvalidRequest, exception),
            HttpRequestException httpEx when httpEx.StatusCode == HttpStatusCode.Unauthorized => StorageResult<T>.Failure(L("Http_Unauthorized", "Unauthorized"), StorageErrorType.Authentication, exception),
            HttpRequestException httpEx when httpEx.StatusCode == HttpStatusCode.Forbidden => StorageResult<T>.Failure(L("Http_Forbidden", "Forbidden"), StorageErrorType.Forbidden, exception),
            HttpRequestException httpEx when httpEx.StatusCode == HttpStatusCode.NotFound => StorageResult<T>.Failure(L("Http_NotFound", "Not Found"), StorageErrorType.NotFound, exception),
            HttpRequestException httpEx => StorageResult<T>.Failure(LF("PikPak_ApiErrorFormat", "PikPak API error: {0}", httpEx.Message), StorageErrorType.Network, exception),
            ValidationException validationEx => StorageResult<T>.Failure(validationEx.Message, StorageErrorType.InvalidRequest, exception),
            _ => base.HandleException<T>(exception),
        };
    }

    private async Task LoginInternalAsync(CancellationToken ct)
    {
        LoadCachedCredentials();
        ValidateNotEmpty(AccountId, nameof(AccountId));
        ValidateNotEmpty(_password, "password");
        _deviceId ??= Md5(AccountId + _password);

        if (!string.IsNullOrWhiteSpace(_refreshToken))
        {
            try
            {
                await RefreshTokenAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                await SignInAsync(ct).ConfigureAwait(false);
            }
        }
        else
        {
            await SignInAsync(ct).ConfigureAwait(false);
        }

        await RefreshCaptchaTokenAtLoginAsync(GetAction(HttpMethod.Get, $"{DriveApiBaseUrl}/drive/v1/files"), ct).ConfigureAwait(false);
        IsAuthenticated = true;
        DriveId = RootId;
        SaveCachedCredentials();
    }

    private async Task SignInAsync(CancellationToken ct)
    {
        string url = $"{UserApiBaseUrl}/v1/auth/signin";
        if (string.IsNullOrWhiteSpace(_captchaToken))
        {
            await RefreshCaptchaTokenInLoginAsync(GetAction(HttpMethod.Post, url), ct).ConfigureAwait(false);
        }

        try
        {
            PikPakAuthResponse auth = await SendJsonAsync<PikPakAuthResponse>(
                HttpMethod.Post,
                AddQuery(url, new Dictionary<string, string> { ["client_id"] = ClientId }),
                new PikPakSignInRequest
                {
                    CaptchaToken = _captchaToken,
                    ClientId = ClientId,
                    ClientSecret = ClientSecret,
                    Username = AccountId,
                    Password = _password,
                },
            ct,
            includeAuthHeaders: false).ConfigureAwait(false);

            ApplyAuthResponse(auth);
        }
        catch (PikPakApiException ex) when (ex.ErrorCode is 9 or 4002)
        {
            _captchaToken = null;
            await RefreshCaptchaTokenInLoginAsync(GetAction(HttpMethod.Post, url), ct).ConfigureAwait(false);
            PikPakAuthResponse auth = await SendJsonAsync<PikPakAuthResponse>(
                HttpMethod.Post,
                AddQuery(url, new Dictionary<string, string> { ["client_id"] = ClientId }),
                new PikPakSignInRequest
                {
                    CaptchaToken = _captchaToken,
                    ClientId = ClientId,
                    ClientSecret = ClientSecret,
                    Username = AccountId,
                    Password = _password,
                },
                ct,
                includeAuthHeaders: false).ConfigureAwait(false);

            ApplyAuthResponse(auth);
        }
    }

    private void ApplyAuthResponse(PikPakAuthResponse auth)
    {
        _refreshToken = auth.RefreshToken;
        _accessToken = auth.AccessToken;
        _userId = auth.Subject;
    }

    private async Task RefreshTokenAsync(CancellationToken ct)
    {
            PikPakAuthResponse auth = await SendJsonAsync<PikPakAuthResponse>(
                HttpMethod.Post,
                AddQuery($"{UserApiBaseUrl}/v1/auth/token", new Dictionary<string, string> { ["client_id"] = ClientId }),
            new PikPakRefreshTokenRequest
            {
                ClientId = ClientId,
                ClientSecret = ClientSecret,
                GrantType = "refresh_token",
                RefreshToken = _refreshToken,
            },
            ct,
            includeAuthHeaders: false).ConfigureAwait(false);

        _refreshToken = auth.RefreshToken;
        _accessToken = auth.AccessToken;
        _userId = auth.Subject;
    }

    private Task RefreshCaptchaTokenAtLoginAsync(string action, CancellationToken ct)
    {
        Dictionary<string, string> meta = new()
        {
            ["client_version"] = ClientVersion,
            ["package_name"] = PackageName,
            ["user_id"] = _userId ?? string.Empty,
        };
        (string timestamp, string sign) = GetCaptchaSign();
        meta["timestamp"] = timestamp;
        meta["captcha_sign"] = sign;
        return RefreshCaptchaTokenAsync(action, meta, ct);
    }

    private Task RefreshCaptchaTokenInLoginAsync(string action, CancellationToken ct)
    {
        Dictionary<string, string> meta = [];
        if (Regex.IsMatch(AccountId, @"\w+([-+.]\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*"))
        {
            meta["email"] = AccountId;
        }
        else if (AccountId.Length is >= 11 and <= 18)
        {
            meta["phone_number"] = AccountId;
        }
        else
        {
            meta["username"] = AccountId;
        }

        return RefreshCaptchaTokenAsync(action, meta, ct);
    }

    private async Task RefreshCaptchaTokenAsync(string action, Dictionary<string, string> meta, CancellationToken ct)
    {
        bool challengeOpened = false;
        for (int attempt = 0; attempt <= CaptchaVerificationMaxAttempts; attempt++)
        {
            PikPakCaptchaResponse response = await SendJsonAsync<PikPakCaptchaResponse>(
                HttpMethod.Post,
                AddQuery($"{UserApiBaseUrl}/v1/shield/captcha/init", new Dictionary<string, string> { ["client_id"] = ClientId }),
                new PikPakCaptchaInitRequest
                {
                    Action = action,
                    CaptchaToken = _captchaToken,
                    ClientId = ClientId,
                    DeviceId = _deviceId,
                    Meta = meta,
                    RedirectUri = "xlaccsdk01://xbase.cloud/callback?state=harbor",
                },
                ct,
                includeAuthHeaders: true,
                allowCaptchaRefresh: false).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(response.CaptchaToken))
            {
                _captchaToken = response.CaptchaToken;
            }

            if (string.IsNullOrWhiteSpace(response.Url))
            {
                return;
            }

            if (_captchaChallengeHandler == null)
            {
                throw new PikPakApiException(9, $"PikPak verification required: {response.Url}", response.Url);
            }

            if (!challengeOpened)
            {
                challengeOpened = true;
                await _captchaChallengeHandler(response.Url, ct).ConfigureAwait(false);
            }
            else if (attempt == CaptchaVerificationMaxAttempts)
            {
                throw new PikPakApiException(9, "PikPak captcha verification was not completed. Please finish verification in the browser and try again.", response.Url);
            }

            await Task.Delay(CaptchaVerificationRetryDelayMs, ct).ConfigureAwait(false);
        }
    }

    private async Task<FileItem> GetItemInternalAsync(string itemId, CancellationToken ct)
    {
        StorageResult<FileItem> result = await GetItemAsync(itemId, ct).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            throw result.Exception ?? new InvalidOperationException(result.ErrorMessage);
        }

        return result.Data;
    }

    private async Task<string> GetDownloadUrlInternalAsync(string itemId, CancellationToken ct)
    {
        PikPakFile file = await RequestAsync<PikPakFile>(
            HttpMethod.Get,
            $"{DriveApiBaseUrl}/drive/v1/files/{Uri.EscapeDataString(itemId)}",
            new Dictionary<string, string>
            {
                ["_magic"] = "2021",
                ["usage"] = "FETCH",
                ["thumbnail_size"] = "SIZE_LARGE",
            },
            ct: ct).ConfigureAwait(false);

        string url = file.WebContentLink;
        if (string.IsNullOrWhiteSpace(url) && file.Medias?.Count > 0)
        {
            url = file.Medias[0].Link?.Url;
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException(L("PikPak_DownloadUrlUnavailable", "Download URL is not available for this PikPak item."));
        }

        return url;
    }

    private async Task CollectChildrenAsync(string parentId, List<FileItem> output, CancellationToken ct)
    {
        string pageToken = null;
        do
        {
            PageResult<FileItem> page = (await ListChildrenAsync(parentId, pageToken, ct).ConfigureAwait(false)).Data;
            if (page?.Items == null)
            {
                return;
            }

            output.AddRange(page.Items);
            foreach (FileItem folder in page.Items.Where(item => item.IsFolder))
            {
                await CollectChildrenAsync(folder.Id, output, ct).ConfigureAwait(false);
            }

            pageToken = page.NextPageToken;
        }
        while (!string.IsNullOrWhiteSpace(pageToken));
    }

    private async Task<T> RequestAsync<T>(HttpMethod method, string url, Dictionary<string, string> query = null, object body = null, CancellationToken ct = default)
    {
        return await SendWithRetryAsync<T>(method, AddQuery(url, query), body, ct, allowCaptchaRefresh: true).ConfigureAwait(false);
    }

    private async Task<T> SendWithRetryAsync<T>(HttpMethod method, string url, object body, CancellationToken ct, bool allowCaptchaRefresh)
    {
        try
        {
            return await SendJsonAsync<T>(method, url, body, ct, includeAuthHeaders: true, allowCaptchaRefresh).ConfigureAwait(false);
        }
        catch (PikPakApiException ex) when (ex.ErrorCode is 4122 or 4121 or 16)
        {
            await RefreshTokenAsync(ct).ConfigureAwait(false);
            return await SendJsonAsync<T>(method, url, body, ct, includeAuthHeaders: true, allowCaptchaRefresh).ConfigureAwait(false);
        }
        catch (PikPakApiException ex) when (allowCaptchaRefresh && ex.ErrorCode is 9 or 4002)
        {
            await RefreshCaptchaTokenAtLoginAsync(GetAction(method, url), ct).ConfigureAwait(false);
            return await SendJsonAsync<T>(method, url, body, ct, includeAuthHeaders: true, allowCaptchaRefresh: false).ConfigureAwait(false);
        }
    }

    private async Task<T> SendJsonAsync<T>(HttpMethod method, string url, object body, CancellationToken ct, bool includeAuthHeaders, bool allowCaptchaRefresh = true)
    {
        using HttpRequestMessage request = new(method, url);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        request.Headers.TryAddWithoutValidation("X-Device-ID", _deviceId ?? string.Empty);
        request.Headers.TryAddWithoutValidation("X-Captcha-Token", _captchaToken ?? string.Empty);
        if (includeAuthHeaders && !string.IsNullOrWhiteSpace(_accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        }

        if (body != null)
        {
            request.Content = new StringContent(SerializeBody(body), Encoding.UTF8, "application/json");
        }

        using HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        string text = response.Content == null ? string.Empty : await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            PikPakErrorResponse error = Deserialize<PikPakErrorResponse>(text);
            if (error != null && error.IsError)
            {
                throw new PikPakApiException(error.ErrorCode, error.ToString());
            }

            throw new HttpRequestException(string.IsNullOrWhiteSpace(text) ? response.ReasonPhrase : text, null, response.StatusCode);
        }

        PikPakErrorResponse apiError = Deserialize<PikPakErrorResponse>(text);
        if (apiError != null && apiError.IsError)
        {
            throw new PikPakApiException(apiError.ErrorCode, apiError.ToString());
        }

        return Deserialize<T>(text);
    }

    private void LoadCachedCredentials()
    {
        PikPakCredentials credentials = _credentialStore?.Get(DriveId, AccountId) ?? _credentialStore?.Get(RootId, AccountId);
        if (credentials == null)
        {
            return;
        }

        AccountId = string.IsNullOrWhiteSpace(AccountId) ? credentials.Username : AccountId;
        _password ??= credentials.Password;
        _refreshToken ??= credentials.RefreshToken;
        _captchaToken ??= credentials.CaptchaToken;
        _deviceId ??= credentials.DeviceId;
    }

    private void SaveCachedCredentials()
    {
        if (!_rememberPassword)
        {
            return;
        }

        try
        {
            _credentialStore?.Save(new PikPakCredentials
            {
                ServerUrl = RootId,
                Username = AccountId,
                Password = _password,
                RefreshToken = _refreshToken,
                CaptchaToken = _captchaToken,
                DeviceId = _deviceId,
            });
        }
        catch
        {
            // Login should still succeed if the OS credential vault is unavailable.
        }
    }

    private static string NormalizeId(string itemId)
    {
        return string.IsNullOrWhiteSpace(itemId) || itemId == RootId ? string.Empty : itemId;
    }

    private static string AddQuery(string url, Dictionary<string, string> query)
    {
        if (query == null || query.Count == 0)
        {
            return url;
        }

        string separator = url.Contains('?') ? "&" : "?";
        return url + separator + string.Join("&", query.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value ?? string.Empty)}"));
    }

    private static string GetAction(HttpMethod method, string url)
    {
        Uri uri = new(url);
        return $"{method.Method}:{uri.AbsolutePath}";
    }

    private (string Timestamp, string Sign) GetCaptchaSign()
    {
        string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        string value = $"{ClientId}{ClientVersion}{PackageName}{_deviceId}{timestamp}";
        foreach (string algorithm in Algorithms)
        {
            value = Md5(value + algorithm);
        }

        return (timestamp, "1." + value);
    }

    private static T Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        if (typeof(T) == typeof(PikPakFilesResponse))
        {
            return (T)(object)JsonSerializer.Deserialize(json, PikPakSourceGenerationContext.Default.PikPakFilesResponse);
        }

        if (typeof(T) == typeof(PikPakFile))
        {
            return (T)(object)JsonSerializer.Deserialize(json, PikPakSourceGenerationContext.Default.PikPakFile);
        }

        if (typeof(T) == typeof(PikPakAuthResponse))
        {
            return (T)(object)JsonSerializer.Deserialize(json, PikPakSourceGenerationContext.Default.PikPakAuthResponse);
        }

        if (typeof(T) == typeof(PikPakCaptchaResponse))
        {
            return (T)(object)JsonSerializer.Deserialize(json, PikPakSourceGenerationContext.Default.PikPakCaptchaResponse);
        }

        if (typeof(T) == typeof(PikPakAboutResponse))
        {
            return (T)(object)JsonSerializer.Deserialize(json, PikPakSourceGenerationContext.Default.PikPakAboutResponse);
        }

        if (typeof(T) == typeof(PikPakErrorResponse))
        {
            return (T)(object)JsonSerializer.Deserialize(json, PikPakSourceGenerationContext.Default.PikPakErrorResponse);
        }

        if (typeof(T) == typeof(JsonElement))
        {
            return (T)(object)(string.IsNullOrWhiteSpace(json) ? JsonDocument.Parse("{}").RootElement.Clone() : JsonDocument.Parse(json).RootElement.Clone());
        }

        throw new NotSupportedException($"Unsupported PikPak JSON type: {typeof(T).FullName}");
    }

    private static string SerializeBody(object body)
    {
        return body switch
        {
            PikPakSignInRequest value => JsonSerializer.Serialize(value, PikPakSourceGenerationContext.Default.PikPakSignInRequest),
            PikPakRefreshTokenRequest value => JsonSerializer.Serialize(value, PikPakSourceGenerationContext.Default.PikPakRefreshTokenRequest),
            PikPakCaptchaInitRequest value => JsonSerializer.Serialize(value, PikPakSourceGenerationContext.Default.PikPakCaptchaInitRequest),
            PikPakCreateFolderRequest value => JsonSerializer.Serialize(value, PikPakSourceGenerationContext.Default.PikPakCreateFolderRequest),
            PikPakRenameRequest value => JsonSerializer.Serialize(value, PikPakSourceGenerationContext.Default.PikPakRenameRequest),
            PikPakIdsRequest value => JsonSerializer.Serialize(value, PikPakSourceGenerationContext.Default.PikPakIdsRequest),
            _ => throw new NotSupportedException($"Unsupported PikPak request body: {body.GetType().FullName}"),
        };
    }

    private static long? TryParseLong(string value)
    {
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) ? parsed : null;
    }

    private static async Task EnsureHttpSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string body = response.Content == null ? null : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        throw new HttpRequestException(string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase : body, null, response.StatusCode);
    }

    private static async Task CopyToAsync(Stream source, Stream destination, IProgress<long> progress, CancellationToken ct)
    {
        byte[] buffer = new byte[81920];
        long totalBytes = 0;
        int bytesRead;
        while ((bytesRead = await source.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
        {
            await destination.WriteAsync(buffer, 0, bytesRead, ct).ConfigureAwait(false);
            totalBytes += bytesRead;
            progress?.Report(totalBytes);
        }
    }

    private static string Md5(string value)
    {
        byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

}

[JsonSerializable(typeof(PikPakFilesResponse))]
[JsonSerializable(typeof(PikPakFile))]
[JsonSerializable(typeof(PikPakAuthResponse))]
[JsonSerializable(typeof(PikPakCaptchaResponse))]
[JsonSerializable(typeof(PikPakAboutResponse))]
[JsonSerializable(typeof(PikPakErrorResponse))]
[JsonSerializable(typeof(PikPakSignInRequest))]
[JsonSerializable(typeof(PikPakRefreshTokenRequest))]
[JsonSerializable(typeof(PikPakCaptchaInitRequest))]
[JsonSerializable(typeof(PikPakCreateFolderRequest))]
[JsonSerializable(typeof(PikPakRenameRequest))]
[JsonSerializable(typeof(PikPakIdsRequest))]
public partial class PikPakSourceGenerationContext : JsonSerializerContext
{
}

public class PikPakFilesResponse
{
    [JsonPropertyName("files")]
    public List<PikPakFile> Files { get; set; } = [];

    [JsonPropertyName("next_page_token")]
    public string NextPageToken { get; set; }
}

public class PikPakFile
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("kind")]
    public string Kind { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("parent_id")]
    public string ParentId { get; set; }

    [JsonPropertyName("created_time")]
    public DateTimeOffset? CreatedTime { get; set; }

    [JsonPropertyName("modified_time")]
    public DateTimeOffset? ModifiedTime { get; set; }

    [JsonPropertyName("hash")]
    public string Hash { get; set; }

    [JsonPropertyName("size")]
    public string Size { get; set; }

    [JsonPropertyName("mime_type")]
    public string MimeType { get; set; }

    [JsonPropertyName("thumbnail_link")]
    public string ThumbnailLink { get; set; }

    [JsonPropertyName("web_content_link")]
    public string WebContentLink { get; set; }

    [JsonPropertyName("medias")]
    public List<PikPakMedia> Medias { get; set; } = [];
}

public class PikPakSignInRequest
{
    [JsonPropertyName("captcha_token")]
    public string CaptchaToken { get; set; }

    [JsonPropertyName("client_id")]
    public string ClientId { get; set; }

    [JsonPropertyName("client_secret")]
    public string ClientSecret { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; }

    [JsonPropertyName("password")]
    public string Password { get; set; }
}

public class PikPakRefreshTokenRequest
{
    [JsonPropertyName("client_id")]
    public string ClientId { get; set; }

    [JsonPropertyName("client_secret")]
    public string ClientSecret { get; set; }

    [JsonPropertyName("grant_type")]
    public string GrantType { get; set; }

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; }
}

public class PikPakCaptchaInitRequest
{
    [JsonPropertyName("action")]
    public string Action { get; set; }

    [JsonPropertyName("captcha_token")]
    public string CaptchaToken { get; set; }

    [JsonPropertyName("client_id")]
    public string ClientId { get; set; }

    [JsonPropertyName("device_id")]
    public string DeviceId { get; set; }

    [JsonPropertyName("meta")]
    public Dictionary<string, string> Meta { get; set; }

    [JsonPropertyName("redirect_uri")]
    public string RedirectUri { get; set; }
}

public class PikPakCreateFolderRequest
{
    [JsonPropertyName("kind")]
    public string Kind { get; set; }

    [JsonPropertyName("parent_id")]
    public string ParentId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }
}

public class PikPakRenameRequest
{
    [JsonPropertyName("name")]
    public string Name { get; set; }
}

public class PikPakIdsRequest
{
    [JsonPropertyName("ids")]
    public List<string> Ids { get; set; }
}

public class PikPakMedia
{
    [JsonPropertyName("link")]
    public PikPakMediaLink Link { get; set; }
}

public class PikPakMediaLink
{
    [JsonPropertyName("url")]
    public string Url { get; set; }
}

public class PikPakAuthResponse
{
    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; }

    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; }

    [JsonPropertyName("sub")]
    public string Subject { get; set; }
}

public class PikPakCaptchaResponse
{
    [JsonPropertyName("captcha_token")]
    public string CaptchaToken { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; }
}

public class PikPakAboutResponse
{
    [JsonPropertyName("quota")]
    public PikPakQuota Quota { get; set; }
}

public class PikPakQuota
{
    [JsonPropertyName("limit")]
    public string Limit { get; set; }

    [JsonPropertyName("usage")]
    public string Usage { get; set; }

    [JsonPropertyName("usage_in_trash")]
    public string UsageInTrash { get; set; }
}

public class PikPakErrorResponse
{
    [JsonPropertyName("error_code")]
    public long ErrorCode { get; set; }

    [JsonPropertyName("error")]
    public string Error { get; set; }

    [JsonPropertyName("error_description")]
    public string ErrorDescription { get; set; }

    [JsonIgnore]
    public bool IsError => ErrorCode != 0 || !string.IsNullOrWhiteSpace(Error) || !string.IsNullOrWhiteSpace(ErrorDescription);

    public override string ToString()
    {
        return $"ErrorCode: {ErrorCode}, Error: {Error}, ErrorDescription: {ErrorDescription}";
    }
}

public class PikPakApiException : Exception
{
    public PikPakApiException(long errorCode, string message, string verificationUrl = null)
        : base(message)
    {
        ErrorCode = errorCode;
        VerificationUrl = verificationUrl;
    }

    public long ErrorCode { get; }
    public string VerificationUrl { get; }
}
