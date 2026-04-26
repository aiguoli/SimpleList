using CommunityToolkit.Mvvm.ComponentModel;
using SimpleList.Helpers;

namespace SimpleList.ViewModels;

public class UploadFileProgressViewModel : ObservableObject
{
    private string _filePath;
    private ulong _uploadedBytes;
    private ulong _totalBytes;
    private int _progressValue;
    private bool _completed;

    public string FilePath
    {
        get => _filePath;
        set => SetProperty(ref _filePath, value);
    }

    public ulong UploadedBytes
    {
        get => _uploadedBytes;
        set => SetProperty(ref _uploadedBytes, value);
    }

    public ulong TotalBytes
    {
        get => _totalBytes;
        set => SetProperty(ref _totalBytes, value);
    }

    public int ProgressValue
    {
        get => _progressValue;
        set
        {
            if (SetProperty(ref _progressValue, value))
            {
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public bool Completed
    {
        get => _completed;
        set
        {
            if (SetProperty(ref _completed, value))
            {
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public string StatusText
    {
        get
        {
            if (Completed)
            {
                return ResourceHelper.GetLocalized("TaskManagerPage_UploadDetailStatus_Completed");
            }

            if (ProgressValue > 0)
            {
                return ResourceHelper.GetLocalized("TaskManagerPage_UploadDetailStatus_Uploading");
            }

            return ResourceHelper.GetLocalized("TaskManagerPage_UploadDetailStatus_Pending");
        }
    }
}
