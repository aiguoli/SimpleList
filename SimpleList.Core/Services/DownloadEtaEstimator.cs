using System;

namespace SimpleList.Core.Services;

public sealed class DownloadEtaEstimator
{
    private const double DefaultSmoothingFactor = 0.25;
    private readonly double _smoothingFactor;
    private double _smoothedBytesPerSecond;

    public DownloadEtaEstimator(double smoothingFactor = DefaultSmoothingFactor)
    {
        if (smoothingFactor <= 0 || smoothingFactor > 1 || !double.IsFinite(smoothingFactor))
        {
            throw new ArgumentOutOfRangeException(nameof(smoothingFactor));
        }

        _smoothingFactor = smoothingFactor;
    }

    public double SmoothedBytesPerSecond => _smoothedBytesPerSecond;

    public bool AddSpeedSample(double bytesPerSecond)
    {
        if (bytesPerSecond <= 0 || !double.IsFinite(bytesPerSecond))
        {
            return false;
        }

        _smoothedBytesPerSecond = _smoothedBytesPerSecond <= 0
            ? bytesPerSecond
            : (_smoothingFactor * bytesPerSecond) + ((1 - _smoothingFactor) * _smoothedBytesPerSecond);
        return true;
    }

    public TimeSpan? EstimateRemaining(long totalBytes, long downloadedBytes)
    {
        if (totalBytes <= downloadedBytes || downloadedBytes < 0 || _smoothedBytesPerSecond <= 0)
        {
            return null;
        }

        double remainingSeconds = (totalBytes - downloadedBytes) / _smoothedBytesPerSecond;
        if (!double.IsFinite(remainingSeconds)
            || remainingSeconds <= 0
            || remainingSeconds > TimeSpan.MaxValue.TotalSeconds)
        {
            return null;
        }

        return TimeSpan.FromSeconds(Math.Ceiling(remainingSeconds));
    }

    public void Reset()
    {
        _smoothedBytesPerSecond = 0;
    }
}
