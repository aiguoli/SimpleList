using SimpleList.Core.Models;
using SimpleList.Core.Services;
using System;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace SimpleList.Tests;

public class StorageProviderBaseTests
{
    private class TestProvider : StorageProviderBase
    {
        public bool AuthCalled { get; private set; }
        public bool AuthFails { get; set; }

        protected override Task EnsureAuthenticatedAsync()
        {
            AuthCalled = true;
            if (AuthFails) throw new UnauthorizedAccessException("denied");
            return Task.CompletedTask;
        }

        public Task<StorageResult<T>> CallExecuteAsync<T>(Func<Task<T>> op, Action validate = null) => ExecuteAsync(op, validate);
    }

    [Fact]
    public async Task ExecuteAsync_Success_ReturnsSuccessResult()
    {
        var provider = new TestProvider();
        var result = await provider.CallExecuteAsync(() => Task.FromResult(42));
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Data);
        Assert.True(provider.AuthCalled);
    }

    [Fact]
    public async Task ExecuteAsync_AuthFailure_MapsToForbidden()
    {
        var provider = new TestProvider { AuthFails = true };
        var result = await provider.CallExecuteAsync(() => Task.FromResult(1));
        Assert.False(result.IsSuccess);
        Assert.Equal(StorageErrorType.Forbidden, result.ErrorType);
    }

    [Fact]
    public async Task ExecuteAsync_TimeoutException_MapsToNetwork()
    {
        var provider = new TestProvider();
        var result = await provider.CallExecuteAsync<int>(() => throw new TimeoutException("slow"));
        Assert.False(result.IsSuccess);
        Assert.Equal(StorageErrorType.Network, result.ErrorType);
    }

    [Fact]
    public async Task ExecuteAsync_ArgumentException_MapsToInvalidRequest()
    {
        var provider = new TestProvider();
        var result = await provider.CallExecuteAsync<int>(() => throw new ArgumentException("bad"));
        Assert.False(result.IsSuccess);
        Assert.Equal(StorageErrorType.InvalidRequest, result.ErrorType);
    }

    [Fact]
    public async Task ExecuteAsync_WebException_MapsToNetwork()
    {
        var provider = new TestProvider();
        var result = await provider.CallExecuteAsync<int>(() => throw new WebException("no net"));
        Assert.False(result.IsSuccess);
        Assert.Equal(StorageErrorType.Network, result.ErrorType);
    }

    [Fact]
    public async Task ExecuteAsync_OperationCanceledException_PropagatesUp()
    {
        var provider = new TestProvider();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            provider.CallExecuteAsync<int>(() => throw new OperationCanceledException()));
    }

    [Fact]
    public async Task ExecuteAsync_UnknownException_MapsToUnknown()
    {
        var provider = new TestProvider();
        var result = await provider.CallExecuteAsync<int>(() => throw new InvalidOperationException("?"));
        Assert.False(result.IsSuccess);
        Assert.Equal(StorageErrorType.Unknown, result.ErrorType);
    }

    [Fact]
    public async Task ExecuteAsync_ValidationFails_ReturnsInvalidRequest()
    {
        var provider = new TestProvider();
        var result = await provider.CallExecuteAsync(
            () => Task.FromResult(1),
            () => throw new System.ComponentModel.DataAnnotations.ValidationException("empty"));
        Assert.False(result.IsSuccess);
        Assert.Equal(StorageErrorType.InvalidRequest, result.ErrorType);
        Assert.False(provider.AuthCalled);
    }
}
