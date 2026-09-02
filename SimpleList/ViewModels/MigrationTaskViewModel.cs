using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using SimpleList.Core.Models;
using SimpleList.Core.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WinUICommunity;

namespace SimpleList.ViewModels;

public partial class MigrationTaskViewModel : ObservableObject
{
    private const int MaxConcurrentFilesPerFolder = 3;

    public MigrationTaskViewModel(
        FileViewModel sourceItem,
        DriveViewModel sourceDrive,
        DriveViewModel targetDrive,
        string targetParentId,
        string targetPathText,
        SemaphoreSlim rootSemaphore,
        TaskManagerViewModel manager)
    {
        SourceItem = sourceItem;
        SourceDrive = sourceDrive;
        TargetDrive = targetDrive;
        TargetParentId = targetParentId;
        TargetPathText = targetPathText;
        _rootSemaphore = rootSemaphore;
        _manager = manager;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
    }

    public FileViewModel SourceItem { get; }
    public DriveViewModel SourceDrive { get; }
    public DriveViewModel TargetDrive { get; }
    public string TargetParentId { get; }
    public string TargetPathText { get; }
    public string Name => SourceItem.Name;
    public string SourceDriveName => SourceDrive.DisplayName;
    public string TargetDriveName => TargetDrive.DisplayName;
    public string TargetDisplay => string.IsNullOrWhiteSpace(TargetPathText)
        ? TargetDrive.DisplayName
        : $"{TargetDrive.DisplayName} / {TargetPathText}";

    [ObservableProperty]
    public partial MigrationStatus Status { get; set; } = MigrationStatus.Pending;

    [ObservableProperty]
    public partial int ProgressValue { get; set; }

    [ObservableProperty]
    public partial long MigratedBytes { get; set; }

    [ObservableProperty]
    public partial long TotalBytes { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; }

    public bool Completed => Status == MigrationStatus.Completed;
    public bool IsRunning => Status == MigrationStatus.Running;
    public bool CanCancel => Status == MigrationStatus.Pending || Status == MigrationStatus.Running;
    public string StatusText => Status switch
    {
        MigrationStatus.Pending => Helpers.ResourceHelper.GetLocalized("TaskManagerPage_MigrationStatus_Pending"),
        MigrationStatus.Running => Helpers.ResourceHelper.GetLocalized("TaskManagerPage_MigrationStatus_Running"),
        MigrationStatus.Completed => Helpers.ResourceHelper.GetLocalized("TaskManagerPage_MigrationStatus_Completed"),
        MigrationStatus.Failed => Helpers.ResourceHelper.GetLocalized("TaskManagerPage_MigrationStatus_Failed"),
        MigrationStatus.Canceled => Helpers.ResourceHelper.GetLocalized("TaskManagerPage_MigrationStatus_Canceled"),
        _ => Status.ToString(),
    };

    partial void OnStatusChanged(MigrationStatus value)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(Completed));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(CanCancel));
        CancelTaskCommand.NotifyCanExecuteChanged();
    }

    partial void OnProgressValueChanged(int value) => OnPropertyChanged(nameof(ProgressPercentText));
    public string ProgressPercentText => $"{ProgressValue}%";

    public Task StartAsync()
    {
        if (_task != null)
        {
            return _task;
        }

        _cts = new CancellationTokenSource();
        _task = Task.Run(RunAsync);
        return _task;
    }

    private async Task RunAsync()
    {
        bool acquiredRootSlot = false;
        try
        {
            await _rootSemaphore.WaitAsync(_cts.Token);
            acquiredRootSlot = true;
            SetStatus(MigrationStatus.Running);
            TotalBytes = await CalculateTotalBytesAsync(SourceItem.Id, SourceItem.IsFolder, _cts.Token);
            if (TotalBytes == 0)
            {
                ProgressValue = 100;
            }

            if (SourceItem.IsFolder)
            {
                var createFolderResult = await TargetDrive.Provider.CreateFolderAsync(TargetParentId, SourceItem.Name, "rename", _cts.Token);
                if (!createFolderResult.IsSuccess)
                {
                    throw new InvalidOperationException(createFolderResult.ErrorMessage);
                }

                using SemaphoreSlim fileSemaphore = new(MaxConcurrentFilesPerFolder, MaxConcurrentFilesPerFolder);
                await MigrateFolderChildrenAsync(SourceItem.Id, createFolderResult.Data.Id, fileSemaphore, _cts.Token);
            }
            else
            {
                await MigrateFileAsync(SourceItem.Id, SourceItem.Name, TargetParentId, SourceItem.Size, _cts.Token);
            }

            SetProgress(TotalBytes, TotalBytes == 0 ? 100 : 100);
            SetStatus(MigrationStatus.Completed);
            NotifySuccess();
        }
        catch (OperationCanceledException)
        {
            SetStatus(MigrationStatus.Canceled);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            SetStatus(MigrationStatus.Failed);
            NotifyError(ex.Message);
        }
        finally
        {
            if (acquiredRootSlot)
            {
                _rootSemaphore.Release();
            }
        }
    }

    private async Task MigrateFolderChildrenAsync(string sourceFolderId, string targetFolderId, SemaphoreSlim fileSemaphore, CancellationToken ct)
    {
        StorageResult<PageResult<FileItem>> childrenResult = await SourceDrive.Provider.ListAllChildrenAsync(sourceFolderId, ct);
        if (!childrenResult.IsSuccess)
        {
            throw new InvalidOperationException(childrenResult.ErrorMessage);
        }

        List<Task> fileTasks = [];
        foreach (FileItem child in childrenResult.Data.Items)
        {
            ct.ThrowIfCancellationRequested();
            if (child.IsFolder)
            {
                var createFolderResult = await TargetDrive.Provider.CreateFolderAsync(targetFolderId, child.Name, "rename", ct);
                if (!createFolderResult.IsSuccess)
                {
                    throw new InvalidOperationException(createFolderResult.ErrorMessage);
                }
                await MigrateFolderChildrenAsync(child.Id, createFolderResult.Data.Id, fileSemaphore, ct);
            }
            else
            {
                fileTasks.Add(Task.Run(async () =>
                {
                    await fileSemaphore.WaitAsync(ct);
                    try
                    {
                        await MigrateFileAsync(child.Id, child.Name, targetFolderId, child.Size, ct);
                    }
                    finally
                    {
                        fileSemaphore.Release();
                    }
                }, ct));
            }
        }

        await Task.WhenAll(fileTasks);
    }

    private async Task MigrateFileAsync(string sourceItemId, string fileName, string targetParentId, long? fileSize, CancellationToken ct)
    {
        StorageResult<Stream> contentResult = await SourceDrive.Provider.GetItemContentAsync(sourceItemId, ct);
        if (!contentResult.IsSuccess)
        {
            throw new InvalidOperationException(contentResult.ErrorMessage);
        }

        await using Stream content = contentResult.Data;
        long previousBytes = 0;
        IProgress<long> progress = new Progress<long>(bytes =>
        {
            long delta = bytes - previousBytes;
            if (delta < 0) delta = 0;
            previousBytes = bytes;
            AddMigratedBytes(delta);
        });

        StorageResult<FileItem> uploadResult = await TargetDrive.Provider.UploadFileContentAsync(content, fileName, targetParentId, fileSize, progress, ct);
        if (!uploadResult.IsSuccess)
        {
            throw new InvalidOperationException(uploadResult.ErrorMessage);
        }

        long remaining = (fileSize ?? 0) - previousBytes;
        if (remaining > 0)
        {
            AddMigratedBytes(remaining);
        }
    }

    private async Task<long> CalculateTotalBytesAsync(string itemId, bool isFolder, CancellationToken ct)
    {
        if (!isFolder)
        {
            return SourceItem.Size ?? 0;
        }

        long total = 0;
        StorageResult<PageResult<FileItem>> childrenResult = await SourceDrive.Provider.ListAllChildrenAsync(itemId, ct);
        if (!childrenResult.IsSuccess)
        {
            throw new InvalidOperationException(childrenResult.ErrorMessage);
        }

        foreach (FileItem child in childrenResult.Data.Items)
        {
            ct.ThrowIfCancellationRequested();
            total += child.IsFolder
                ? await CalculateFolderBytesAsync(child.Id, ct)
                : child.Size ?? 0;
        }
        return total;
    }

    private async Task<long> CalculateFolderBytesAsync(string folderId, CancellationToken ct)
    {
        long total = 0;
        StorageResult<PageResult<FileItem>> childrenResult = await SourceDrive.Provider.ListAllChildrenAsync(folderId, ct);
        if (!childrenResult.IsSuccess)
        {
            throw new InvalidOperationException(childrenResult.ErrorMessage);
        }

        foreach (FileItem child in childrenResult.Data.Items)
        {
            ct.ThrowIfCancellationRequested();
            total += child.IsFolder
                ? await CalculateFolderBytesAsync(child.Id, ct)
                : child.Size ?? 0;
        }
        return total;
    }

    private void AddMigratedBytes(long delta)
    {
        if (delta <= 0) return;
        long migrated = Interlocked.Add(ref _migratedBytesBacking, delta);
        int percent = TotalBytes > 0 ? (int)(Math.Min(migrated, TotalBytes) * 100.0 / TotalBytes) : 0;
        SetProgress(migrated, percent);
    }

    private void SetProgress(long migratedBytes, int progressValue)
    {
        _dispatcher.TryEnqueue(() =>
        {
            MigratedBytes = migratedBytes;
            ProgressValue = progressValue;
        });
    }

    private void SetStatus(MigrationStatus status)
    {
        _dispatcher.TryEnqueue(() => Status = status);
    }

    private void NotifySuccess()
    {
        _dispatcher.TryEnqueue(() =>
        {
            Growl.Success(new GrowlInfo
            {
                Title = Helpers.ResourceHelper.GetLocalized("TaskManagerPage_MigrationCompleted"),
                Message = Name,
                StaysOpen = false,
                Token = "DriveGrowl"
            });
        });
    }

    private void NotifyError(string message)
    {
        _dispatcher.TryEnqueue(() =>
        {
            Growl.Error(new GrowlInfo
            {
                Title = Helpers.ResourceHelper.GetLocalized("TaskManagerPage_MigrationFailed"),
                Message = message,
                StaysOpen = false,
                Token = "DriveGrowl"
            });
        });
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void CancelTask()
    {
        _cts?.Cancel();
    }

    [RelayCommand]
    private void RemoveTask()
    {
        _manager.RemoveSelectedMigrationTask(this);
    }

    private readonly SemaphoreSlim _rootSemaphore;
    private readonly TaskManagerViewModel _manager;
    private readonly DispatcherQueue _dispatcher;
    private CancellationTokenSource _cts;
    private Task _task;
    private long _migratedBytesBacking;
}
