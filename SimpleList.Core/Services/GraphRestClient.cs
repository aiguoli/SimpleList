using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleList.Core.Services;

internal sealed class GraphRestClient
{
    private const int MaxRetries = 3;
    private static readonly Uri GraphBaseUri = new("https://graph.microsoft.com/v1.0/");
    private static readonly HttpClient GraphHttpClient = new(new HttpClientHandler { AllowAutoRedirect = true });
    private static readonly HttpClient NoRedirectHttpClient = new(new HttpClientHandler { AllowAutoRedirect = false });
    private static readonly HttpClient UploadHttpClient = new(new HttpClientHandler { AllowAutoRedirect = false });
    private readonly Func<CancellationToken, Task<string>> _getAccessToken;
    private readonly HttpClient _graphHttpClient;
    private readonly HttpClient _noRedirectHttpClient;
    private readonly HttpClient _uploadHttpClient;

    public GraphRestClient(Func<CancellationToken, Task<string>> getAccessToken,
        HttpClient graphHttpClient = null, HttpClient noRedirectHttpClient = null, HttpClient uploadHttpClient = null)
    {
        _getAccessToken = getAccessToken ?? throw new ArgumentNullException(nameof(getAccessToken));
        _graphHttpClient = graphHttpClient ?? GraphHttpClient;
        _noRedirectHttpClient = noRedirectHttpClient ?? NoRedirectHttpClient;
        _uploadHttpClient = uploadHttpClient ?? UploadHttpClient;
    }

    public async Task<T> GetAsync<T>(string url, JsonTypeInfo<T> jsonTypeInfo, CancellationToken ct)
    {
        using HttpResponseMessage response = await SendGraphAsync(
            () => new HttpRequestMessage(HttpMethod.Get, ResolveGraphUri(url)),
            HttpCompletionOption.ResponseHeadersRead,
            ct).ConfigureAwait(false);
        return await ReadJsonAsync(response, jsonTypeInfo, ct).ConfigureAwait(false);
    }

    public Task<TResponse> PostAsync<TRequest, TResponse>(string url, TRequest body,
        JsonTypeInfo<TRequest> requestTypeInfo, JsonTypeInfo<TResponse> responseTypeInfo, CancellationToken ct)
        => SendJsonAsync(HttpMethod.Post, url, body, requestTypeInfo, responseTypeInfo, ct);

    public Task<TResponse> PatchAsync<TRequest, TResponse>(string url, TRequest body,
        JsonTypeInfo<TRequest> requestTypeInfo, JsonTypeInfo<TResponse> responseTypeInfo, CancellationToken ct)
        => SendJsonAsync(HttpMethod.Patch, url, body, requestTypeInfo, responseTypeInfo, ct);

    public async Task PostAsync<TRequest>(string url, TRequest body, JsonTypeInfo<TRequest> requestTypeInfo, CancellationToken ct)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(body, requestTypeInfo);
        using HttpResponseMessage response = await SendGraphAsync(
            () => CreateJsonRequest(HttpMethod.Post, ResolveGraphUri(url), json),
            HttpCompletionOption.ResponseHeadersRead,
            ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string url, CancellationToken ct)
    {
        using HttpResponseMessage response = await SendGraphAsync(
            () => new HttpRequestMessage(HttpMethod.Delete, ResolveGraphUri(url)),
            HttpCompletionOption.ResponseHeadersRead,
            ct).ConfigureAwait(false);
    }

    public async Task<Stream> GetStreamAsync(string url, string format, CancellationToken ct)
    {
        HttpResponseMessage response = await SendGraphAsync(
            () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, ResolveGraphUri(url));
                if (!string.IsNullOrWhiteSpace(format)) request.Headers.TryAddWithoutValidation("Format", format);
                return request;
            },
            HttpCompletionOption.ResponseHeadersRead,
            ct).ConfigureAwait(false);

        try
        {
            Stream stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            return new ResponseOwnedStream(stream, response);
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    public async Task<string> GetRedirectLocationAsync(string url, CancellationToken ct)
    {
        using HttpResponseMessage response = await SendGraphAsync(
            () => new HttpRequestMessage(HttpMethod.Get, ResolveGraphUri(url)),
            HttpCompletionOption.ResponseHeadersRead,
            ct,
            _noRedirectHttpClient,
            allowRedirectResponse: true).ConfigureAwait(false);

        if ((int)response.StatusCode is >= 300 and < 400 && response.Headers.Location != null)
            return response.Headers.Location.ToString();

        throw await CreateExceptionAsync(response, ct).ConfigureAwait(false);
    }

    public async Task<GraphDriveItem> PutContentAsync(string url, Stream content, CancellationToken ct)
    {
        using HttpResponseMessage response = await SendGraphAsync(
            () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Put, ResolveGraphUri(url));
                request.Content = new StreamContent(content, 81920);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                return request;
            },
            HttpCompletionOption.ResponseHeadersRead,
            ct,
            retryableRequest: false).ConfigureAwait(false);
        return await ReadJsonAsync(response, GraphJsonContext.Default.GraphDriveItem, ct).ConfigureAwait(false);
    }

    public async Task<GraphDriveItem> UploadLargeFileAsync(string uploadUrl, Stream content, long totalLength,
        IProgress<long> progress, bool queryResumeOffset, CancellationToken ct)
    {
        ValidateUploadUri(uploadUrl);
        if (!content.CanSeek) throw new ArgumentException("Large upload content must be seekable", nameof(content));

        long offset = queryResumeOffset ? await GetUploadOffsetAsync(uploadUrl, ct).ConfigureAwait(false) : 0;
        if (offset < 0 || offset > totalLength)
            throw new InvalidDataException($"Invalid upload offset {offset} for a {totalLength}-byte stream");

        const int chunkSize = 320 * 1024;
        byte[] buffer = new byte[chunkSize];
        int stalledResponses = 0;
        while (offset < totalLength)
        {
            ct.ThrowIfCancellationRequested();
            content.Seek(offset, SeekOrigin.Begin);
            int requested = (int)Math.Min(chunkSize, totalLength - offset);
            int read = await ReadExactlyUpToAsync(content, buffer, requested, ct).ConfigureAwait(false);
            if (read != requested)
                throw new EndOfStreamException($"Upload stream ended at {offset + read} of {totalLength} bytes");

            HttpResponseMessage response = await SendUploadChunkAsync(uploadUrl, buffer, read, offset, totalLength, ct).ConfigureAwait(false);
            using (response)
            {
                if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created)
                {
                    GraphDriveItem completed = await ReadJsonAsync(response, GraphJsonContext.Default.GraphDriveItem, ct).ConfigureAwait(false);
                    progress?.Report(totalLength);
                    return completed;
                }
                if (response.StatusCode == HttpStatusCode.Accepted)
                {
                    GraphUploadSession session = await ReadJsonAsync(response, GraphJsonContext.Default.GraphUploadSession, ct).ConfigureAwait(false);
                    long nextOffset = ParseNextExpectedOffset(session?.NextExpectedRanges, offset + read);
                    stalledResponses = nextOffset <= offset ? stalledResponses + 1 : 0;
                    if (stalledResponses > MaxRetries)
                        throw new InvalidDataException("Upload session repeatedly returned a non-advancing range");
                    offset = nextOffset;
                    progress?.Report(offset);
                    continue;
                }
                if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
                {
                    long nextOffset = await GetUploadOffsetAsync(uploadUrl, ct).ConfigureAwait(false);
                    stalledResponses = nextOffset == offset ? stalledResponses + 1 : 0;
                    if (stalledResponses > MaxRetries)
                        throw new InvalidDataException("Upload session could not recover from a range conflict");
                    offset = nextOffset;
                    progress?.Report(offset);
                    continue;
                }
                throw await CreateExceptionAsync(response, ct).ConfigureAwait(false);
            }
        }
        throw new InvalidOperationException("Upload session completed without a drive item response");
    }

    private async Task<TResponse> SendJsonAsync<TRequest, TResponse>(HttpMethod method, string url, TRequest body,
        JsonTypeInfo<TRequest> requestTypeInfo, JsonTypeInfo<TResponse> responseTypeInfo, CancellationToken ct)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(body, requestTypeInfo);
        using HttpResponseMessage response = await SendGraphAsync(
            () => CreateJsonRequest(method, ResolveGraphUri(url), json),
            HttpCompletionOption.ResponseHeadersRead,
            ct).ConfigureAwait(false);
        return await ReadJsonAsync(response, responseTypeInfo, ct).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendGraphAsync(Func<HttpRequestMessage> requestFactory,
        HttpCompletionOption completionOption, CancellationToken ct, HttpClient httpClient = null,
        bool allowRedirectResponse = false, bool retryableRequest = true)
    {
        httpClient ??= _graphHttpClient;
        int attempt = 0;
        while (true)
        {
            using HttpRequestMessage request = requestFactory();
            ValidateGraphUri(request.RequestUri);
            string token = await _getAccessToken(ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(token))
                throw new InvalidOperationException("Microsoft Graph access token is unavailable");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            HttpResponseMessage response;
            try
            {
                response = await httpClient.SendAsync(request, completionOption, ct).ConfigureAwait(false);
            }
            catch (HttpRequestException) when (retryableRequest && attempt < MaxRetries)
            {
                await DelayBeforeRetryAsync(null, attempt++, ct).ConfigureAwait(false);
                continue;
            }

            if (response.IsSuccessStatusCode || (allowRedirectResponse && (int)response.StatusCode is >= 300 and < 400))
                return response;

            if (retryableRequest && IsTransient(response.StatusCode) && attempt < MaxRetries)
            {
                await DelayBeforeRetryAsync(response, attempt++, ct).ConfigureAwait(false);
                response.Dispose();
                continue;
            }

            GraphHttpException exception = await CreateExceptionAsync(response, ct).ConfigureAwait(false);
            response.Dispose();
            throw exception;
        }
    }

    private async Task<HttpResponseMessage> SendUploadChunkAsync(string uploadUrl, byte[] buffer, int count,
        long offset, long totalLength, CancellationToken ct)
    {
        int attempt = 0;
        while (true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, uploadUrl);
            request.Content = new ByteArrayContent(buffer, 0, count);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            request.Content.Headers.ContentRange = new ContentRangeHeaderValue(offset, offset + count - 1, totalLength);
            HttpResponseMessage response;
            try
            {
                response = await _uploadHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            }
            catch (HttpRequestException) when (attempt < MaxRetries)
            {
                await DelayBeforeRetryAsync(null, attempt++, ct).ConfigureAwait(false);
                continue;
            }
            if (!IsTransient(response.StatusCode) || attempt >= MaxRetries) return response;
            await DelayBeforeRetryAsync(response, attempt++, ct).ConfigureAwait(false);
            response.Dispose();
        }
    }

    private async Task<long> GetUploadOffsetAsync(string uploadUrl, CancellationToken ct)
    {
        ValidateUploadUri(uploadUrl);
        int attempt = 0;
        while (true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uploadUrl);
            HttpResponseMessage response;
            try
            {
                response = await _uploadHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            }
            catch (HttpRequestException) when (attempt < MaxRetries)
            {
                await DelayBeforeRetryAsync(null, attempt++, ct).ConfigureAwait(false);
                continue;
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                {
                    GraphUploadSession session = await ReadJsonAsync(response, GraphJsonContext.Default.GraphUploadSession, ct).ConfigureAwait(false);
                    return ParseNextExpectedOffset(session?.NextExpectedRanges, 0);
                }
                if (IsTransient(response.StatusCode) && attempt < MaxRetries)
                {
                    await DelayBeforeRetryAsync(response, attempt++, ct).ConfigureAwait(false);
                    continue;
                }
                throw await CreateExceptionAsync(response, ct).ConfigureAwait(false);
            }
        }
    }

    private static long ParseNextExpectedOffset(System.Collections.Generic.IReadOnlyList<string> ranges, long fallback)
    {
        string first = ranges?.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(first)) return fallback;
        int separator = first.IndexOf('-');
        string value = separator >= 0 ? first[..separator] : first;
        return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long offset) ? offset : fallback;
    }

    private static async Task<int> ReadExactlyUpToAsync(Stream stream, byte[] buffer, int count, CancellationToken ct)
    {
        int total = 0;
        while (total < count)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(total, count - total), ct).ConfigureAwait(false);
            if (read == 0) break;
            total += read;
        }
        return total;
    }

    private static HttpRequestMessage CreateJsonRequest(HttpMethod method, Uri uri, byte[] json)
    {
        var request = new HttpRequestMessage(method, uri) { Content = new ByteArrayContent(json) };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return request;
    }

    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response, JsonTypeInfo<T> jsonTypeInfo, CancellationToken ct)
    {
        await using Stream stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        T result = await JsonSerializer.DeserializeAsync(stream, jsonTypeInfo, ct).ConfigureAwait(false);
        return result ?? throw new InvalidDataException("Microsoft Graph returned an empty JSON response");
    }

    private static async Task<GraphHttpException> CreateExceptionAsync(HttpResponseMessage response, CancellationToken ct)
    {
        string body = response.Content == null ? null : await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        string code = null;
        string message = null;
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(body);
                if (document.RootElement.TryGetProperty("error", out JsonElement error))
                {
                    if (error.TryGetProperty("code", out JsonElement codeElement)) code = codeElement.GetString();
                    if (error.TryGetProperty("message", out JsonElement messageElement)) message = messageElement.GetString();
                }
            }
            catch (JsonException) { }
        }
        message ??= response.ReasonPhrase ?? "Microsoft Graph request failed";
        return new GraphHttpException(response.StatusCode, code, message);
    }

    private static bool IsTransient(HttpStatusCode statusCode)
        => statusCode == HttpStatusCode.RequestTimeout || (int)statusCode == 429 ||
           statusCode == HttpStatusCode.InternalServerError || statusCode == HttpStatusCode.BadGateway ||
           statusCode == HttpStatusCode.ServiceUnavailable || statusCode == HttpStatusCode.GatewayTimeout;

    private static async Task DelayBeforeRetryAsync(HttpResponseMessage response, int attempt, CancellationToken ct)
    {
        TimeSpan delay = response?.Headers.RetryAfter?.Delta ??
            (response?.Headers.RetryAfter?.Date is DateTimeOffset retryAt
                ? retryAt - DateTimeOffset.UtcNow
                : TimeSpan.FromSeconds(Math.Pow(2, attempt)));
        if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
        if (delay > TimeSpan.FromSeconds(60)) delay = TimeSpan.FromSeconds(60);
        await Task.Delay(delay, ct).ConfigureAwait(false);
    }

    private static Uri ResolveGraphUri(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) throw new ArgumentException("Graph URL cannot be empty", nameof(url));
        return Uri.TryCreate(url, UriKind.Absolute, out Uri absolute) ? absolute : new Uri(GraphBaseUri, url.TrimStart('/'));
    }

    private static void ValidateGraphUri(Uri uri)
    {
        if (uri == null || uri.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(uri.Host, "graph.microsoft.com", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Refusing to send a Graph access token to an untrusted host");
    }

    private static void ValidateUploadUri(string uploadUrl)
    {
        if (!Uri.TryCreate(uploadUrl, UriKind.Absolute, out Uri uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("Upload session URL must be an absolute HTTPS URL");
    }

    private sealed class ResponseOwnedStream : Stream
    {
        private readonly Stream _inner;
        private readonly HttpResponseMessage _response;
        public ResponseOwnedStream(Stream inner, HttpResponseMessage response) { _inner = inner; _response = response; }
        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => _inner.Position = value; }
        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
        public override void SetLength(long value) => _inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
        public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _inner.ReadAsync(buffer, cancellationToken);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => _inner.WriteAsync(buffer, cancellationToken);
        protected override void Dispose(bool disposing)
        {
            if (disposing) { _inner.Dispose(); _response.Dispose(); }
            base.Dispose(disposing);
        }
        public override async ValueTask DisposeAsync()
        {
            await _inner.DisposeAsync().ConfigureAwait(false);
            _response.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}

internal sealed class GraphHttpException : HttpRequestException
{
    public GraphHttpException(HttpStatusCode statusCode, string errorCode, string message)
        : base(message, null, statusCode)
    {
        ResponseStatusCode = statusCode;
        ErrorCode = errorCode;
    }
    public HttpStatusCode ResponseStatusCode { get; }
    public string ErrorCode { get; }
}
