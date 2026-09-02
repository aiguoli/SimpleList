using NSubstitute;
using SimpleList.Core.Contracts;
using SimpleList.Core.Models;
using SimpleList.Core.Services;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace SimpleList.Tests;

public class StorageProviderPagingExtensionsTests
{
    [Fact]
    public async Task ListAllChildrenAsync_CombinesEveryPage()
    {
        IStorageProvider provider = Substitute.For<IStorageProvider>();
        provider.ListChildrenAsync("root", null, Arg.Any<CancellationToken>())
            .Returns(StorageResult<PageResult<FileItem>>.Success(new PageResult<FileItem>
            {
                Items = [new FileItem { Id = "first" }],
                NextPageToken = "page-2",
            }));
        provider.ListChildrenAsync("root", "page-2", Arg.Any<CancellationToken>())
            .Returns(StorageResult<PageResult<FileItem>>.Success(new PageResult<FileItem>
            {
                Items = [new FileItem { Id = "second" }],
            }));

        StorageResult<PageResult<FileItem>> result = await provider.ListAllChildrenAsync("root");

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Collection(
            result.Data.Items,
            item => Assert.Equal("first", item.Id),
            item => Assert.Equal("second", item.Id));
        Assert.Null(result.Data.NextPageToken);
        await provider.Received(1).ListChildrenAsync("root", "page-2", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListAllChildrenAsync_RejectsRepeatedPageToken()
    {
        IStorageProvider provider = Substitute.For<IStorageProvider>();
        provider.ListChildrenAsync("root", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(StorageResult<PageResult<FileItem>>.Success(new PageResult<FileItem>
            {
                Items = [],
                NextPageToken = "same-token",
            }));

        StorageResult<PageResult<FileItem>> result = await provider.ListAllChildrenAsync("root");

        Assert.False(result.IsSuccess);
        Assert.Contains("repeated page token", result.ErrorMessage);
    }

    [Fact]
    public async Task ListAllTrashAsync_PropagatesProviderFailure()
    {
        IStorageProvider provider = Substitute.For<IStorageProvider>();
        provider.ListTrashAsync(null, Arg.Any<CancellationToken>())
            .Returns(StorageResult<PageResult<FileItem>>.Failure(
                "offline",
                StorageErrorType.Network));

        StorageResult<PageResult<FileItem>> result = await provider.ListAllTrashAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal("offline", result.ErrorMessage);
        Assert.Equal(StorageErrorType.Network, result.ErrorType);
    }
}
