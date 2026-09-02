using SimpleList.Core.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SimpleList.Tests;

public class GraphRestClientTests
{
    [Fact]
    public async Task GetAsync_AddsBearerTokenOnlyForGraphHost()
    {
        var snapshots = new List<RequestSnapshot>();
        var responses = new Queue<HttpResponseMessage>([
            JsonResponse(HttpStatusCode.OK, "{\"displayName\":\"Ada\"}"),
        ]);
        using var graphClient = new HttpClient(new RecordingHandler(snapshots, responses));
        var client = new GraphRestClient(_ => Task.FromResult("access-token"), graphHttpClient: graphClient);

        GraphUser user = await client.GetAsync("me", GraphJsonContext.Default.GraphUser, CancellationToken.None);

        Assert.Equal("Ada", user.DisplayName);
        Assert.Equal("Bearer access-token", snapshots[0].Authorization);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetAsync("https://example.com/steal", GraphJsonContext.Default.GraphUser, CancellationToken.None));
        Assert.Single(snapshots);
    }

    [Fact]
    public void SourceGeneratedJson_MapsGraphFacetsAndDownloadUrl()
    {
        const string json = """
            {
              "id":"item-1",
              "name":"photo.jpg",
              "size":42,
              "parentReference":{"id":"parent-1"},
              "folder":{"childCount":3},
              "image":{"width":1920,"height":1080},
              "@microsoft.graph.downloadUrl":"https://download.example/item-1"
            }
            """;

        GraphDriveItem item = JsonSerializer.Deserialize(json, GraphJsonContext.Default.GraphDriveItem);

        Assert.Equal("item-1", item.Id);
        Assert.Equal("parent-1", item.ParentReference.Id);
        Assert.Equal(3, item.Folder.ChildCount);
        Assert.Equal(1920, item.Image.Width);
        Assert.Equal("https://download.example/item-1", item.DownloadUrl);
    }

    [Fact]
    public async Task UploadLargeFileAsync_SendsSequential320KiBChunksWithoutBearerToken()
    {
        const int chunkSize = 320 * 1024;
        byte[] data = new byte[chunkSize + 10];
        var snapshots = new List<RequestSnapshot>();
        var responses = new Queue<HttpResponseMessage>([
            JsonResponse(HttpStatusCode.Accepted, $"{{\"nextExpectedRanges\":[\"{chunkSize}-\"]}}"),
            JsonResponse(HttpStatusCode.Created, "{\"id\":\"uploaded\",\"name\":\"file.bin\"}"),
        ]);
        using var uploadClient = new HttpClient(new RecordingHandler(snapshots, responses));
        var client = new GraphRestClient(
            _ => throw new InvalidOperationException("Upload URLs must not request a Graph token"),
            uploadHttpClient: uploadClient);
        var progress = new RecordingProgress();

        GraphDriveItem result = await client.UploadLargeFileAsync(
            "https://tenant.up.1drv.com/upload/session", new MemoryStream(data), data.Length,
            progress, false, CancellationToken.None);

        Assert.Equal("uploaded", result.Id);
        Assert.Equal(2, snapshots.Count);
        Assert.All(snapshots, item => Assert.Null(item.Authorization));
        Assert.Equal($"bytes 0-{chunkSize - 1}/{data.Length}", snapshots[0].ContentRange);
        Assert.Equal($"bytes {chunkSize}-{data.Length - 1}/{data.Length}", snapshots[1].ContentRange);
        Assert.Equal(chunkSize, snapshots[0].BodyLength);
        Assert.Equal(10, snapshots[1].BodyLength);
        Assert.Equal(data.Length, progress.Value);
    }

    [Fact]
    public async Task UploadLargeFileAsync_ResumeQueriesServerOffsetBeforeSendingContent()
    {
        const int chunkSize = 320 * 1024;
        byte[] data = new byte[chunkSize + 7];
        var snapshots = new List<RequestSnapshot>();
        var responses = new Queue<HttpResponseMessage>([
            JsonResponse(HttpStatusCode.OK, $"{{\"nextExpectedRanges\":[\"{chunkSize}-\"]}}"),
            JsonResponse(HttpStatusCode.Created, "{\"id\":\"resumed\"}"),
        ]);
        using var uploadClient = new HttpClient(new RecordingHandler(snapshots, responses));
        var client = new GraphRestClient(_ => Task.FromResult("unused"), uploadHttpClient: uploadClient);

        GraphDriveItem result = await client.UploadLargeFileAsync(
            "https://tenant.up.1drv.com/upload/session", new MemoryStream(data), data.Length,
            null, true, CancellationToken.None);

        Assert.Equal("resumed", result.Id);
        Assert.Equal(HttpMethod.Get, snapshots[0].Method);
        Assert.Equal(HttpMethod.Put, snapshots[1].Method);
        Assert.Equal($"bytes {chunkSize}-{data.Length - 1}/{data.Length}", snapshots[1].ContentRange);
        Assert.Equal(7, snapshots[1].BodyLength);
        Assert.All(snapshots, item => Assert.Null(item.Authorization));
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json)
        => new(statusCode) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly List<RequestSnapshot> _snapshots;
        private readonly Queue<HttpResponseMessage> _responses;

        public RecordingHandler(List<RequestSnapshot> snapshots, Queue<HttpResponseMessage> responses)
        {
            _snapshots = snapshots;
            _responses = responses;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            byte[] body = request.Content == null
                ? []
                : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            _snapshots.Add(new RequestSnapshot(
                request.Method,
                request.Headers.Authorization?.ToString(),
                request.Content?.Headers.ContentRange?.ToString(),
                body.Length));
            return _responses.Dequeue();
        }
    }

    private sealed record RequestSnapshot(HttpMethod Method, string Authorization, string ContentRange, int BodyLength);

    private sealed class RecordingProgress : IProgress<long>
    {
        public long Value { get; private set; }
        public void Report(long value) => Value = value;
    }
}
