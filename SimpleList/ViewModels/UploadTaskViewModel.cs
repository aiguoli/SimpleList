using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Graph.Models;
using Microsoft.UI.Dispatching;
using SimpleList.Models;
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
        Drive = drive;
    }

    private Task _uploadTask;
    private CancellationTokenSource _cancellationTokenSource;
    private readonly Dictionary<string, UploadFileProgressViewModel> _folderProgressMap = [];


    [ObservableProperty] public int progressValue;
    [ObservableProperty] private ulong _uploadedBytes = 0;
    [ObservableProperty] private ulong _totalBytes;
    [ObservableProperty] private bool _completed = false;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PauseCommand))]
    private bool _isUploading = false;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResumeCommand))]
    [NotifyCanExecuteChangedFor(nameof(PauseCommand))]
    private bool _isPaused = false;

    public bool IsFolder => _item is StorageFolder;
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
                sizeText = "0 bytes";
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
        if (_cancellationTokenSource != null && !_cancellationTokenSource.Token.IsCancellationRequested)
        {
            _cancellationTokenSource.Cancel();
        }
        _manager.RemoveSelectedUploadTasks(this);
    }
    
    private bool CanPause => IsUploading && !IsPaused;
    private bool CanResume => IsPaused;
    
    public DriveViewModel Drive;
    public string Name => _item.Name;

    private readonly string _itemId;
    private readonly IStorageItem _item;
    private readonly TaskManagerViewModel _manager = App.GetService<TaskManagerViewModel>();
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private string _uploadUrl = null;

    private void UpdateOnUiThread(Action action)
    {
        if (_dispatcher.HasThreadAccess)
        {
            action();
            return;
        }

        _dispatcher.TryEnqueue(() => action());
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

    public async Task StartUpload()
    {
        if (_uploadTask != null && !_uploadTask.IsCompleted)
            return;
            
        _cancellationTokenSource = new CancellationTokenSource();
        IsUploading = true;
        IsPaused = false;
        Completed = false;
        FolderUploadItems.Clear();
        _folderProgressMap.Clear();

        WinUICommunity.Growl.Info(new WinUICommunity.GrowlInfo
        {
            Title = Helpers.ResourceHelper.GetLocalized("TaskManagerPage_Upload"),
            Message = string.Format(Helpers.ResourceHelper.GetLocalized("TaskManagerPage_StartUploadDesc"), _item.Name),
            IsClosable = true,
            ShowDateTime = true,
            Token = "DriveGrowl"
        });

        if (_item is StorageFile fileItem)
        {
            TotalBytes = (await fileItem.GetBasicPropertiesAsync()).Size;
        }
        else if (_item is StorageFolder folderItem)
        {
            TotalBytes = await Services.Utils.GetFolderSize(folderItem);
        }

        System.IProgress<long> progress = new System.Progress<long>(value =>
        {
            _dispatcher.TryEnqueue(() =>
            {
                if (_cancellationTokenSource?.Token.IsCancellationRequested == false)
                {
                    if (_item is StorageFile)
                    {
                         UploadedBytes = (ulong)value;
                         if (TotalBytes > 0)
                         {
                            ProgressValue = (int)(UploadedBytes * 100 / TotalBytes);
                         }
                    }
                    else
                    {
                        ProgressValue = (int)value;
                        if (TotalBytes > 0)
                        {
                            UploadedBytes = (ulong)((double)value / 100 * TotalBytes);
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

            _dispatcher.TryEnqueue(() =>
            {
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
                _cancellationTokenSource.Token.ThrowIfCancellationRequested();
                
                if (_item is StorageFile file)
                {
                    var result = await Drive.Provider.UploadFileAsync(file, _itemId, progress, _uploadUrl, (url) => _uploadUrl = url, _cancellationTokenSource.Token);
                    
                    _cancellationTokenSource.Token.ThrowIfCancellationRequested();
                    
                    if (result.IsSuccess)
                    {
                        WinUICommunity.Growl.Success(new WinUICommunity.GrowlInfo
                        {
                            Title = Helpers.ResourceHelper.GetLocalized("Success"),
                            StaysOpen = false,
                            Token = "DriveGrowl"
                        });
                        MarkUploadCompleted();
                    }
                    else
                    {
                        WinUICommunity.Growl.Error(new WinUICommunity.GrowlInfo
                        {
                            Title = Helpers.ResourceHelper.GetLocalized("Error"),
                            Message = result.ErrorMessage,
                            StaysOpen = false,
                            Token = "DriveGrowl"
                        });
                    }
                }
                else if (_item is StorageFolder folder)
                {
                    var result = await Drive.Provider.UploadFolderAsync(folder, _itemId, progress, folderDetailProgress, _cancellationTokenSource.Token);
                    
                    _cancellationTokenSource.Token.ThrowIfCancellationRequested();
                    
                    if (result.IsSuccess)
                    {
                        WinUICommunity.Growl.Success(new WinUICommunity.GrowlInfo
                        {
                            Title = Helpers.ResourceHelper.GetLocalized("Success"),
                            StaysOpen = false,
                            Token = "DriveGrowl"
                        });
                        MarkUploadCompleted();
                    }
                    else
                    {
                        WinUICommunity.Growl.Error(new WinUICommunity.GrowlInfo
                        {
                            Title = Helpers.ResourceHelper.GetLocalized("Error"),
                            Message = result.ErrorMessage,
                            StaysOpen = false,
                            Token = "DriveGrowl"
                        });
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
            }
            finally
            {
                if (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    if (ProgressValue >= 100 && !Completed)
                    {
                        MarkUploadCompleted();
                    }
                    else
                    {
                        UpdateOnUiThread(() => IsUploading = false);
                    }
                }
            }
        }, _cancellationTokenSource.Token);
        
        try
        {
            await _uploadTask;
        }
        catch (OperationCanceledException)
        {
        }
    }
}
