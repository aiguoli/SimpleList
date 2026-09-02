using SimpleList.Core.Models;
using System;
using Xunit;

namespace SimpleList.Tests;

public class StorageResultTests
{
    [Fact]
    public void Success_PopulatesIsSuccessAndData()
    {
        var result = StorageResult<string>.Success("hello");
        Assert.True(result.IsSuccess);
        Assert.Equal("hello", result.Data);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(StorageErrorType.None, result.ErrorType);
        Assert.Null(result.Exception);
    }

    [Fact]
    public void Success_WithNullData_IsTolerated()
    {
        var result = StorageResult<string>.Success(null);
        Assert.True(result.IsSuccess);
        Assert.Null(result.Data);
    }

    [Fact]
    public void Failure_PopulatesAllErrorFields()
    {
        var ex = new InvalidOperationException("boom");
        var result = StorageResult<int>.Failure("nope", StorageErrorType.Forbidden, ex);
        Assert.False(result.IsSuccess);
        Assert.Equal(0, result.Data);
        Assert.Equal("nope", result.ErrorMessage);
        Assert.Equal(StorageErrorType.Forbidden, result.ErrorType);
        Assert.Same(ex, result.Exception);
    }

    [Fact]
    public void Failure_WithoutException_IsAllowed()
    {
        var result = StorageResult<int>.Failure("nope", StorageErrorType.NotFound);
        Assert.False(result.IsSuccess);
        Assert.Null(result.Exception);
        Assert.Equal(StorageErrorType.NotFound, result.ErrorType);
    }
}
