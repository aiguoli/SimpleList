using SimpleList.Core.Services;
using System;
using Xunit;

namespace SimpleList.Tests;

public class DownloadEtaEstimatorTests
{
    [Fact]
    public void AddSpeedSample_SmoothsSubsequentSamples()
    {
        DownloadEtaEstimator estimator = new(0.25);

        Assert.True(estimator.AddSpeedSample(1_000));
        Assert.True(estimator.AddSpeedSample(3_000));

        Assert.Equal(1_500, estimator.SmoothedBytesPerSecond);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void AddSpeedSample_RejectsInvalidSamples(double sample)
    {
        DownloadEtaEstimator estimator = new();

        Assert.False(estimator.AddSpeedSample(sample));
        Assert.Equal(0, estimator.SmoothedBytesPerSecond);
    }

    [Fact]
    public void EstimateRemaining_UsesSmoothedSpeedAndRoundsUp()
    {
        DownloadEtaEstimator estimator = new();
        estimator.AddSpeedSample(400);

        TimeSpan? remaining = estimator.EstimateRemaining(totalBytes: 1_000, downloadedBytes: 1);

        Assert.Equal(TimeSpan.FromSeconds(3), remaining);
    }

    [Fact]
    public void EstimateRemaining_ReturnsNullWithoutUsableProgress()
    {
        DownloadEtaEstimator estimator = new();

        Assert.Null(estimator.EstimateRemaining(1_000, 500));

        estimator.AddSpeedSample(100);
        Assert.Null(estimator.EstimateRemaining(1_000, 1_000));
        Assert.Null(estimator.EstimateRemaining(1_000, -1));
    }

    [Fact]
    public void Reset_DiscardsPreviousSpeed()
    {
        DownloadEtaEstimator estimator = new();
        estimator.AddSpeedSample(1_000);

        estimator.Reset();

        Assert.Equal(0, estimator.SmoothedBytesPerSecond);
        Assert.Null(estimator.EstimateRemaining(1_000, 500));
    }
}
