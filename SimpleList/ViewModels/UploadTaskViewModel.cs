using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using SimpleList.Core.Models;
using SimpleList.Core.Models.DTO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace SimpleList.ViewModels;

public partial class UploadTaskViewModel : ObservableObject
{
    public UploadTaskViewModel(DriveViewModel drive, string itemId, IStorageItem item)
    {
        _itemId = itemId;
        _item = item;
        _itemPath = item?.Path;
        _name = item?.Name;
        _isFolder = item is StorageFolder;
        Drive = drive;
    }

    internal UploadTaskViewModel(DriveViewModel drive, PersistentTaskDTO persistedTask, IStorageItem item)
        : this(drive, persistedTask.ItemId, item)
    {
        _itemPath = persistedTask.Path;
        _name = persistedTask.Name;
        _isFolder = persistedTask.IsFolder;
        ResumeToken = persistedTask.ResumeToken;
        ProgressValue = persistedTask.Progress;
        UploadedBytes = (ulong)Math.Max(0, persistedTask.TransferredBytes);
        TotalBytes = (ulong)Math.Max(0, persistedTask.TotalBytes);
        Completed = persistedTask.Completed;
        IsPaused = persistedTask.IsPaused && !persistedTask.Completed;
        IsUploading = false;
    }

    private Task _uploadTask;
    private CancellationTokenSource _cancellationTokenSource;
    private bool _isCanceled;
    private readonly Dictionary<string, UploadFileProgressViewModel> _folderProgressMap = [];


    [ObservableProperty]
    public partial int ProgressValue { get; set; }

    [ObservableProperty]
    public partial ulong UploadedBytes { get; set; } = 0;

    [ObservableProperty]
    public partial ulong TotalBytes { get; set; }

    [ObservableProperty]
    public partial bool Completed { get; set; } = false;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PauseCommand))]
    public partial bool IsUploading { get; set; } = false;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResumeCommand))]
    [NotifyCanExecuteChangedFor(nameof(PauseCommand))]
    public partial bool IsPaused { get; set; } = false;

    public bool IsFolder => _isFolder;
    public ObservableCollection<UploadFileProgressViewModel> FolderUploadItems { get; } = [];
    public string ProgressPercentText => $"{ProgressValue}%";
    public string CompletedText
    {
        get
        {
            var completedLabel = Helpers.ResourceHelper.GetLocalized(
                "TaskManagerPage_UploadCompleted.Text",
                "TaskManager_UploadCompleted.Text");
            if (string.IsNullOrWhiteSpace(completedLabel))
            {
                completedLabel = Helpers.ResourceHelper.GetLocalized("Success");
            }

            var sizeText = Converters.FileSizeConverter.Instance.Convert(TotalBytes, typeof(string), null, string.Empty)?.ToString();
            if (string.IsNullOrWhiteSpace(sizeText))
            {
                sizeText = Helpers.ResourceHelper.GetLocalized("FileSize_ZeroBytes");
            }

            return $"{completedLabel}, {sizeText}";
        }
    }

    partial void OnProgressValueChanged(int value) => OnPropertyChanged(nameof(ProgressPercentText));

    partial void OnCompletedChanged(bool value) => OnPropertyChanged(nameof(CompletedText));

    partial void OnTotalBytesChanged(ulong value) => OnPropertyChanged(nameof(CompletedText));

    [RelayCommand(CanExecute = nameof(CanPause))]
    private void Pause() // Generates PauseCommand
    {
        if (IsUploading && !IsPaused && _cancellationTokenSource != null)
        {
            _cancellationTokenSource.Cancel();
            IsPaused = true;
            IsUploading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanResume))]
    private void Resume()
    {
        if (IsPaused && !IsUploading)
        {
            IsPaused = false;
            IsUploading = true;
            _ = StartUpload();
        }
    }

    [RelayCommand]
    private void CancelTask()
    {
        _isCanceled = true;
        if (_cancellationTokenSource != null && !_cancellationTokenSource.Token.IsCancellationRequested)
        {
            _cancellationTokenSource.Cancel();
        }
        _manager.RemoveSelectedUploadTasks(this);
    }
    
    private bool CanPause => IsUploading && !IsPaused;
    private bool CanResume => IsPaused;
    
    public DriveViewModel Drive;
    public string Name => _name;

    private readonly string _itemId;
    private readonly IStorageItem _item;
    private readonly string _itemPath;
    private readonly string _name;
    private readonly bool _isFolder;
    private readonly TaskManagerViewModel _manager = App.GetService<TaskManagerViewModel>();
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    internal string ResumeToken { get; private set; }
    internal string PersistentItemId => _itemId;
    internal string PersistentItemPath => _itemPath;

    private void UpdateOnUiThread(Action action)
    {
        if (_dispatcher == null || _dispatcher.HasThreadAccess)
        {
            action();
            return;
        }

        if (!_dispatcher.TryEnqueue(() => action()))
        {
            throw new InvalidOperationException("Unable to update the upload task on the UI thread.");
        }
    }

    private void MarkUploadCompleted()
    {
        UpdateOnUiThread(() =>
        {
            if (IsFolder)
            {
                foreach (UploadFileProgressViewModel detailItem in FolderUploadItems)
                {
                    detailItem.UploadedBytes = detailItem.TotalBytes;
                    detailItem.ProgressValue = 100;
                    detailItem.Completed = true;
                }
            }

            UploadedBytes = TotalBytes;
            ProgressValue = 100;
            Completed = true;
            IsUploading = false;
            IsPaused = false;
        });
    }

    private void ShowUploadSuccess()
    {
        UpdateOnUiThread(() =>
        {
            DevWinUI.Growl.Success(new DevWinUI.GrowlInfo
            {
                Title = Helpers.ResourceHelper.GetLocalized("Success"),
                StaysOpen = false,
                Token = "DriveGrowl"
            });
        });
    }

    private void ShowUploadError(string message)
    {
        UpdateOnUiThread(() =>
        {
            DevWinUI.Growl.Error(new DevWinUI.GrowlInfo
            {
                Title = Helpers.ResourceHelper.GetLocalized("Error"),
                Message = message,
                StaysOpen = false,
                Token = "DriveGrowl"
            });
        });
    }

    public async Task StartUpload()
    {
        if (_item == null || _isCanceled || (_uploadTask != null && !_uploadTask.IsCompleted))
            return;

        CancellationTokenSource uploadCancellation = new();
        _cancellationTokenSource = uploadCancellation;
        IsUploading = true;
        IsPaused = false;
        Completed = false;
        FolderUploadItems.Clear();
        _folderProgressMap.Clear();

        DevWinUI.Growl.Info(new DevWinUI.GrowlInfo
        {
            Title = Helpers.ResourceHelper.GetLocalized("TaskManagerPage_Upload"),
            Message = string.Format(Helpers.ResourceHelper.GetLocalized("TaskManagerPage_StartUploadDesc"), _item.Name),
            IsClosable = true,
            ShowDateTime = true,
            Token = "DriveGrowl"
        });

        try
        {
            if (_item is StorageFile fileItem)
            {
                TotalBytes = (await fileItem.GetBasicPropertiesAsync()).Size;
            }
            else if (_item is StorageFolder folderItem)
            {
                TotalBytes = await Services.Utils.GetFolderSize(folderItem);
            }
        }
        catch (Exception ex)
        {
            IsUploading = false;
            ShowUploadError(ex.Message);
            return;
        }

        System.IProgress<long> progress = new System.Progress<long>(value =>
        {
            UpdateOnUiThread(() =>
            {
                if (!uploadCancellation.IsCancellationRequested && !Completed)
                {
                    if (_item is StorageFile)
                    {
                         UploadedBytes = TotalBytes > 0 ? Math.Min((ulong)Math.Max(0, value), TotalBytes) : (ulong)Math.Max(0, value);
                         if (TotalBytes > 0)
                         {
                            ProgressValue = (int)(UploadedBytes * 100 / TotalBytes);
                         }
                    }
                    else
                    {
                        ProgressValue = Math.Clamp((int)value, 0, 100);
                        if (TotalBytes > 0)
                        {
                            UploadedBytes = (ulong)((double)ProgressValue / 100 * TotalBytes);
                        }
                    }
                }
            });
        });

        System.IProgress<FolderUploadProgressInfo> folderDetailProgress = new System.Progress<FolderUploadProgressInfo>(detail =>
        {
            if (detail == null)
            {
                return;
            }

            UpdateOnUiThread(() =>
            {
                if (Completed)
                {
                    return;
                }

                if (!_folderProgressMap.TryGetValue(detail.FilePath, out UploadFileProgressViewModel item))
                {
                    item = new UploadFileProgressViewModel
                    {
                        FilePath = detail.FilePath
                    };
                    _folderProgressMap[detail.FilePath] = item;
                    FolderUploadItems.Add(item);
                }

                item.TotalBytes = detail.TotalBytes;
                item.UploadedBytes = detail.UploadedBytes;
                item.Completed = detail.Completed;

                if (detail.TotalBytes > 0)
                {
                    item.ProgressValue = (int)(detail.UploadedBytes * 100 / detail.TotalBytes);
                }
                else
                {
                    item.ProgressValue = detail.Completed ? 100 : 0;
                }
            });
        });

        _uploadTask = Task.Run(async () =>
        {
            try
            {
                uploadCancellation.Token.ThrowIfCancellationRequested();
                
                if (_item is StorageFile file)
                {
                    StorageResult<FileItem> result = await UploadFileWithResumeFallbackAsync(
                        file,
                        progress,
                        uploadCancellation.Token);
                    
                    uploadCancellation.Token.ThrowIfCancellationRequested();
                    
                    if (result.IsSuccess)
                    {
                        UpdateResumeToken(null);
                        MarkUploadCompleted();
                        ShowUploadSuccess();
                    }
                    else
                    {
                        ShowUploadError(result.ErrorMessage);
                    }
                }
                else if (_item is StorageFolder folder)
                {
                    var result = await Drive.Provider.UploadFolderAsync(folder, _itemId, progress, folderDetailProgress, uploadCancellation.Token);
                    
                    uploadCancellation.Token.ThrowIfCancellationRequested();
                    
                    if (result.IsSuccess)
                    {
                        MarkUploadCompleted();
                        ShowUploadSuccess();
                    }
                    else
                    {
                        ShowUploadError(result.ErrorMessage);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                ShowUploadError(ex.Message);
            }
            finally
            {
                UpdateOnUiThread(() =>
                {
                    if (!uploadCancellation.IsCancellationRequested)
                    {
                        if (ProgressValue >= 100 && !Completed)
                        {
                            MarkUploadCompleted();
                        }
                        else
                        {
                            IsUploading = false;
                        }
                    }
                });
            }
        }, uploadCancellation.Token);
        
        try
        {
            await _uploadTask;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task<StorageResult<FileItem>> UploadFileWithResumeFallbackAsync(
        StorageFile file,
        IProgress<long> progress,
        CancellationToken cancellationToken)
    {
        string resumeToken = ResumeToken;
        StorageResult<FileItem> result = await Drive.Provider.UploadFileAsync(
            file,
            _itemId,
            progress,
            resumeToken,
            UpdateResumeToken,
            cancellationToken);

        if (result.IsSuccess || cancellationToken.IsCancellationRequested || string.IsNullOrWhiteSpace(resumeToken))
        {
            return result;
        }

        // The persisted upload session may have expired or been invalidated. In that
        // case create a fresh session and restart the file, as a safe fallback.
        UpdateResumeToken(null);
        UpdateOnUiThread(() =>
        {
            UploadedBytes = 0;
            ProgressValue = 0;
        });
        return await Drive.Provider.UploadFileAsync(
            file,
            _itemId,
            progress,
            null,
            UpdateResumeToken,
            cancellationToken);
    }

    private void UpdateResumeToken(string value)
    {
        UpdateOnUiThread(() =>
        {
            ResumeToken = value;
            OnPropertyChanged(nameof(ResumeToken));
        });
    }
}
