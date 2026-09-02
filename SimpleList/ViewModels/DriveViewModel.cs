using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SimpleList.Core.Contracts;
using SimpleList.Core.Models;
using SimpleList.Core.Models.DTO;
using SimpleList.Core.Services;
using SimpleList.Helpers;
using SimpleList.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Windows.Storage;
using WinRT.Interop;
using WinUICommunity;
using ResourceHelper = SimpleList.Helpers.ResourceHelper;

namespace SimpleList.ViewModels;

public partial class DriveViewModel : ObservableObject
{
    private static readonly TimeSpan CapacityRequestTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan DirectoryCacheFreshDuration = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan DirectoryCacheStaleDuration = TimeSpan.FromMinutes(3);

    public DriveViewModel(IStorageProvider provider, string displayName = null)
    {
        DisplayName = displayName ?? provider.DriveId;
        Provider = provider;
        BreadcrumbItems.Add(new BreadcrumbItem { Name = "RootFileName".GetLocalized(), ItemId = "Root" });
        SelectedItems.CollectionChanged += (s, e) => UpdateSelectionStatus();
        Files.CollectionChanged += (s, e) => UpdateSelectionStatus();
        StorageStatus = ResourceHelper.GetLocalized("CloudPage_CapacityPending");
        StorageDetails = ResourceHelper.GetLocalized("CloudPage_CapacityPendingDetail");
        UpdateSelectionStatus();
    }

    private void UpdateSelectionStatus()
    {
        int selectedCount = SelectedItems?.Count ?? 0;
        int totalCount = Files?.Count ?? 0;
        CanBatchRename = CanEditCurrentFolder && selectedCount > 1;
        CanShowBatchRename = CanEditCurrentFolder && selectedCount >= 2;
        CanOperateSelectedTrashItems = IsTrashMode && selectedCount > 0;
        if (selectedCount == 0)
        {
            SelectionStatus = string.Format("SelectionStatus_Total".GetLocalized(), totalCount);
        }
        else
        {
            SelectionStatus = string.Format("SelectionStatus_Selected".GetLocalized(), selectedCount, totalCount);
        }

        _dispatcher.TryEnqueue(() =>
        {
            OnPropertyChanged(nameof(SelectionStatus));
            foreach (FileViewModel file in Files)
            {
                file.NotifySelectionChanged();
            }
        });
    }

    [RelayCommand]
    public async Task GetFiles(string itemId = null)
    {
        if (IsTrashMode)
        {
            await GetTrashFiles();
            return;
        }

        itemId ??= _parentItemId;
        await LoadDirectoryAsync(itemId, forceRefresh: false);
    }

    private async Task GetTrashFiles()
    {
        if (!CanManageTrash)
        {
            return;
        }

        long requestVersion = BeginFilesRequest(out CancellationTokenSource requestCancellation);
        IsLoading = Visibility.Visible;
        try
        {
            StorageResult<PageResult<FileItem>> result = await Provider.ListAllTrashAsync(requestCancellation.Token);
            if (!IsCurrentFilesRequest(requestVersion, requestCancellation))
            {
                return;
            }

            if (result.IsSuccess)
            {
                LoadFiles(result.Data);
            }
            else
            {
                ShowFilesError(result.ErrorMessage);
            }
        }
        catch (OperationCanceledException) when (requestCancellation.IsCancellationRequested)
        {
        }
        finally
        {
            if (IsCurrentFilesRequest(requestVersion, requestCancellation))
            {
                IsLoading = Visibility.Collapsed;
            }

            CompleteFilesRequest(requestCancellation);
        }
    }

    private void LoadFiles(PageResult<FileItem> page)
    {
        if (ArePagesEquivalent(_displayedPage, page))
        {
            return;
        }

        _displayedPage = page;
        Dictionary<string, FileViewModel> existingFiles = new(StringComparer.Ordinal);
        foreach (FileViewModel existingFile in Files)
        {
            if (!string.IsNullOrEmpty(existingFile.Id))
            {
                existingFiles.TryAdd(existingFile.Id, existingFile);
            }
        }

        List<FileViewModel> updatedFiles = [];
        foreach (FileItem file in page?.Items ?? [])
        {
            if (!string.IsNullOrEmpty(file.Id)
                && existingFiles.TryGetValue(file.Id, out FileViewModel existingFile))
            {
                existingFile.Update(file);
                updatedFiles.Add(existingFile);
            }
            else
            {
                updatedFiles.Add(new FileViewModel(this, file));
            }
        }

        SynchronizeFiles(Files, updatedFiles);
        SynchronizeFiles(Images, updatedFiles.Where(file => file.HasImageMetadata).ToList());

        HashSet<FileViewModel> visibleFiles = [.. updatedFiles];
        foreach (FileViewModel selectedItem in SelectedItems.Where(file => !visibleFiles.Contains(file)).ToList())
        {
            SelectedItems.Remove(selectedItem);
        }
    }

    private static void SynchronizeFiles(
        ObservableCollection<FileViewModel> collection,
        IReadOnlyList<FileViewModel> desiredItems)
    {
        for (int index = 0; index < desiredItems.Count; index++)
        {
            FileViewModel desiredItem = desiredItems[index];
            if (index < collection.Count && ReferenceEquals(collection[index], desiredItem))
            {
                continue;
            }

            int existingIndex = collection.IndexOf(desiredItem);
            if (existingIndex >= 0)
            {
                collection.Move(existingIndex, index);
            }
            else
            {
                collection.Insert(index, desiredItem);
            }
        }

        while (collection.Count > desiredItems.Count)
        {
            collection.RemoveAt(collection.Count - 1);
        }
    }

    private async Task LoadDirectoryAsync(string itemId, bool forceRefresh)
    {
        long requestVersion = BeginFilesRequest(out CancellationTokenSource requestCancellation);
        _parentItemId = itemId;

        bool hasCachedSnapshot = _directoryCache.TryGet(itemId, out DirectoryCacheSnapshot cachedSnapshot);
        bool displayedCachedSnapshot = false;

        try
        {
            if (!forceRefresh
                && hasCachedSnapshot
                && cachedSnapshot.Freshness is DirectoryCacheFreshness.Fresh or DirectoryCacheFreshness.Stale)
            {
                LoadFiles(cachedSnapshot.Page);
                displayedCachedSnapshot = true;
                IsLoading = Visibility.Collapsed;

                if (cachedSnapshot.Freshness == DirectoryCacheFreshness.Fresh)
                {
                    return;
                }
            }

            if (!displayedCachedSnapshot)
            {
                IsLoading = Visibility.Visible;
            }

            StorageResult<PageResult<FileItem>> result = await GetOrCreateDirectoryRequest(itemId)
                .WaitAsync(requestCancellation.Token);

            if (result.IsSuccess)
            {
                PageResult<FileItem> page = result.Data ?? new PageResult<FileItem> { Items = [] };
                _directoryCache.Set(itemId, page);

                if (IsCurrentFilesRequest(requestVersion, requestCancellation))
                {
                    LoadFiles(page);
                }
            }
            else if (IsCurrentFilesRequest(requestVersion, requestCancellation))
            {
                if (!displayedCachedSnapshot && hasCachedSnapshot)
                {
                    LoadFiles(cachedSnapshot.Page);
                }

                ShowFilesError(result.ErrorMessage);
            }
        }
        catch (OperationCanceledException) when (requestCancellation.IsCancellationRequested)
        {
        }
        finally
        {
            if (IsCurrentFilesRequest(requestVersion, requestCancellation))
            {
                IsLoading = Visibility.Collapsed;
            }

            CompleteFilesRequest(requestCancellation);
        }
    }

    private long BeginFilesRequest(out CancellationTokenSource requestCancellation)
    {
        requestCancellation = new CancellationTokenSource();
        long requestVersion;
        lock (_filesRequestGate)
        {
            CancellationTokenSource previous = _filesRequestCancellation;
            _filesRequestCancellation = requestCancellation;
            requestVersion = ++_filesRequestVersion;
            previous?.Cancel();
        }

        return requestVersion;
    }

    private bool IsCurrentFilesRequest(long requestVersion, CancellationTokenSource requestCancellation)
    {
        lock (_filesRequestGate)
        {
            return requestVersion == _filesRequestVersion
                && ReferenceEquals(_filesRequestCancellation, requestCancellation);
        }
    }

    private void CompleteFilesRequest(CancellationTokenSource requestCancellation)
    {
        lock (_filesRequestGate)
        {
            if (ReferenceEquals(_filesRequestCancellation, requestCancellation))
            {
                _filesRequestCancellation = null;
            }
        }

        requestCancellation.Dispose();
    }

    private Task<StorageResult<PageResult<FileItem>>> GetOrCreateDirectoryRequest(string itemId)
    {
        lock (_directoryRequestsGate)
        {
            if (_directoryRequests.TryGetValue(itemId, out Task<StorageResult<PageResult<FileItem>>> existing))
            {
                return existing;
            }

            Task<StorageResult<PageResult<FileItem>>> request = Provider.ListAllChildrenAsync(itemId);
            _directoryRequests[itemId] = request;
            _ = request.ContinueWith(
                completed => CompleteDirectoryRequest(itemId, request, completed),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return request;
        }
    }

    private void CompleteDirectoryRequest(
        string itemId,
        Task<StorageResult<PageResult<FileItem>>> request,
        Task<StorageResult<PageResult<FileItem>>> completed)
    {
        if (completed.Status == TaskStatus.RanToCompletion
            && completed.Result.IsSuccess)
        {
            PageResult<FileItem> page = completed.Result.Data ?? new PageResult<FileItem> { Items = [] };
            _directoryCache.Set(itemId, page);
        }

        lock (_directoryRequestsGate)
        {
            if (_directoryRequests.TryGetValue(itemId, out Task<StorageResult<PageResult<FileItem>>> current)
                && ReferenceEquals(current, request))
            {
                _directoryRequests.Remove(itemId);
            }
        }
    }

    private static void ShowFilesError(string message)
    {
        Growl.Error(new GrowlInfo
        {
            Title = ResourceHelper.GetLocalized("Error"),
            StaysOpen = false,
            Message = message,
            Token = "DriveGrowl"
        });
    }

    private static bool ArePagesEquivalent(PageResult<FileItem> first, PageResult<FileItem> second)
    {
        if (ReferenceEquals(first, second))
        {
            return true;
        }

        if (first is null || second is null
            || !string.Equals(first.NextPageToken, second.NextPageToken, StringComparison.Ordinal))
        {
            return false;
        }

        IReadOnlyList<FileItem> firstItems = first.Items ?? [];
        IReadOnlyList<FileItem> secondItems = second.Items ?? [];
        if (firstItems.Count != secondItems.Count)
        {
            return false;
        }

        for (int i = 0; i < firstItems.Count; i++)
        {
            if (!AreFileItemsEquivalent(firstItems[i], secondItems[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreFileItemsEquivalent(FileItem first, FileItem second)
    {
        if (ReferenceEquals(first, second))
        {
            return true;
        }

        return first is not null
            && second is not null
            && string.Equals(first.Id, second.Id, StringComparison.Ordinal)
            && string.Equals(first.Name, second.Name, StringComparison.Ordinal)
            && string.Equals(first.ParentId, second.ParentId, StringComparison.Ordinal)
            && first.Size == second.Size
            && first.Updated == second.Updated
            && first.Created == second.Created
            && first.IsFolder == second.IsFolder
            && first.ChildCount == second.ChildCount
            && string.Equals(first.MimeType, second.MimeType, StringComparison.Ordinal)
            && string.Equals(first.ETag, second.ETag, StringComparison.Ordinal)
            && first.Provider == second.Provider
            && first.IsShared == second.IsShared
            && first.Image?.Width == second.Image?.Width
            && first.Image?.Height == second.Image?.Height
            && AreProviderTokensEquivalent(first.ProviderTokens, second.ProviderTokens);
    }

    private static bool AreProviderTokensEquivalent(
        IReadOnlyDictionary<string, string> first,
        IReadOnlyDictionary<string, string> second)
    {
        if (ReferenceEquals(first, second))
        {
            return true;
        }

        if (first is null || second is null || first.Count != second.Count)
        {
            return false;
        }

        return first.All(pair => second.TryGetValue(pair.Key, out string value)
            && string.Equals(pair.Value, value, StringComparison.Ordinal));
    }


    [RelayCommand]
    public async Task GetCapacity()
    {
        if (IsCapacityLoading)
        {
            return;
        }

        IsCapacityLoading = true;
        StorageStatus = ResourceHelper.GetLocalized("CloudPage_CapacityLoading");
        StorageDetails = ResourceHelper.GetLocalized("CloudPage_CapacityLoadingDetail");
        StorageUsagePercent = 0;

        try
        {
            using CancellationTokenSource cts = new(CapacityRequestTimeout);
            StorageResult<StorageQuota> result = await Provider.GetQuotaAsync(cts.Token);
            if (result.IsSuccess && result.Data != null)
            {
                StorageQuota quota = result.Data;
                long used = Math.Max(0, quota.Used ?? 0);
                long total = Math.Max(0, quota.Total ?? 0);
                long remaining = Math.Max(0, total - used);

                StorageInfo = Services.Utils.ReadableFileSize(used) + " / " + Services.Utils.ReadableFileSize(total);
                StorageStatus = StorageInfo;
                StorageDetails = string.Format(
                    ResourceHelper.GetLocalized("CloudPage_CapacityLoadedDetail"),
                    Services.Utils.ReadableFileSize(remaining));
                StorageUsagePercent = total > 0
                    ? Math.Min(100, Math.Round((double)used / total * 100, 2))
                    : 0;
            }
            else
            {
                StorageInfo = ResourceHelper.GetLocalized("CloudPage_CapacityUnavailable");
                StorageStatus = StorageInfo;
                StorageDetails = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? result.ErrorType.ToString()
                    : result.ErrorMessage;
                StorageUsagePercent = 0;
            }
        }
        catch (OperationCanceledException)
        {
            StorageInfo = ResourceHelper.GetLocalized("CloudPage_CapacityUnavailable");
            StorageStatus = StorageInfo;
            StorageDetails = ResourceHelper.GetLocalized("CloudPage_CapacityTimeout");
            StorageUsagePercent = 0;
        }
        catch (Exception ex)
        {
            StorageInfo = ResourceHelper.GetLocalized("CloudPage_CapacityUnavailable");
            StorageStatus = StorageInfo;
            StorageDetails = ex.Message;
            StorageUsagePercent = 0;
        }
        finally
        {
            IsCapacityLoading = false;
        }
    }

    [RelayCommand]
    public async Task Refresh()
    {
        if (IsTrashMode)
        {
            await GetTrashFiles();
            return;
        }

        _directoryCache.MarkStale(_parentItemId);
        await LoadDirectoryAsync(_parentItemId, forceRefresh: true);
    }

    public void EnterTrashMode()
    {
        if (!CanManageTrash)
        {
            return;
        }

        IsTrashMode = true;
        _parentItemId = "Trash";
        PendingSelectedItemId = null;
        SelectedItems.Clear();
        BreadcrumbItems.Clear();
        BreadcrumbItems.Add(new BreadcrumbItem { Name = "TrashFileName".GetLocalized(), ItemId = "Trash" });
        NotifyModeChanged();
    }

    public void ExitTrashMode()
    {
        IsTrashMode = false;
        _parentItemId = "Root";
        PendingSelectedItemId = null;
        SelectedItems.Clear();
        BreadcrumbItems.Clear();
        BreadcrumbItems.Add(new BreadcrumbItem { Name = "RootFileName".GetLocalized(), ItemId = "Root" });
        NotifyModeChanged();
    }

    [RelayCommand]
    public async Task RestoreSelectedTrashItems()
    {
        if (!IsTrashMode || SelectedItems.Count == 0)
        {
            return;
        }

        StorageResult<FileItem>[] results = await Task.WhenAll(SelectedItems.Select(file => Provider.RestoreAsync(file.Id)));
        if (results.All(result => result.IsSuccess))
        {
            ShowSuccess("Trash_RestoreSuccess");
            await Refresh();
        }
        else
        {
            ShowError("Trash_RestoreFail", results.Where(result => !result.IsSuccess).Select(result => result.ErrorMessage));
        }
    }

    [RelayCommand]
    public async Task PermanentDeleteSelectedTrashItems()
    {
        if (!IsTrashMode || SelectedItems.Count == 0)
        {
            return;
        }

        StorageResult<bool>[] results = await Task.WhenAll(SelectedItems.Select(file => Provider.PermanentDeleteAsync(file.Id)));
        if (results.All(result => result.IsSuccess && result.Data))
        {
            ShowSuccess("Trash_DeleteSuccess");
            await Refresh();
        }
        else
        {
            ShowError("Trash_DeleteFail", results.Where(result => !result.IsSuccess).Select(result => result.ErrorMessage));
        }
    }

    [RelayCommand]
    public async Task EmptyTrash()
    {
        if (!IsTrashMode)
        {
            return;
        }

        StorageResult<bool> result = await Provider.EmptyTrashAsync();
        if (result.IsSuccess && result.Data)
        {
            ShowSuccess("Trash_EmptySuccess");
            await Refresh();
        }
        else
        {
            ShowError("Trash_EmptyFail", [result.ErrorMessage]);
        }
    }

    [RelayCommand]
    public async Task MigrateFiles()
    {
        if (SelectedItems == null || SelectedItems.Count == 0)
        {
            return;
        }

        var dialog = new Views.MigrateFileView
        {
            XamlRoot = App.StartupWindow.Content.XamlRoot,
            DataContext = new MigrateFileViewModel(this, SelectedItems.ToList())
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary && dialog.DataContext is MigrateFileViewModel vm)
        {
            TaskManagerViewModel manager = App.GetService<TaskManagerViewModel>();
            await manager.AddMigrationTasks(vm.SourceItems, this, vm.TargetDrive, vm.TargetParentId, vm.TargetPathText);
        }
    }

    [RelayCommand]
    public async Task OpenFolder(FileViewModel file)
    {
        if (IsTrashMode)
        {
            return;
        }

        BreadcrumbItems.Add(new BreadcrumbItem { Name = file.Name, ItemId = file.Id });
        await GetFiles(file.Id);
    }

    public async Task OpenBookmark(BookmarkDTO bookmark)
    {
        BreadcrumbItems.Clear();
        foreach (BookmarkPathSegmentDTO segment in bookmark.PathSegments ?? [])
        {
            BreadcrumbItems.Add(new BreadcrumbItem { Name = segment.Name, ItemId = segment.ItemId });
        }

        if (BreadcrumbItems.Count == 0)
        {
            BreadcrumbItems.Add(new BreadcrumbItem { Name = "RootFileName".GetLocalized(), ItemId = "Root" });
        }

        if (bookmark.IsFolder)
        {
            PendingSelectedItemId = null;
            await GetFiles(bookmark.Id);
        }
        else
        {
            string parentId = string.IsNullOrEmpty(bookmark.ParentId) ? "Root" : bookmark.ParentId;
            PendingSelectedItemId = bookmark.Id;
            await GetFiles(parentId);
        }
    }

    [RelayCommand]
    public async Task AddBookmark(FileViewModel file)
    {
        BookmarkDTO bookmark = CreateBookmark(file);
        BookmarkStore bookmarkStore = new(Path.Combine(Directory.GetCurrentDirectory(), "cache", "bookmarks.json"));
        bool added = await bookmarkStore.AddAsync(bookmark);

        Growl.Info(new GrowlInfo
        {
            Title = added ? ResourceHelper.GetLocalized("Bookmark_AddedTitle") : ResourceHelper.GetLocalized("Bookmark_ExistsTitle"),
            Message = file.Name,
            StaysOpen = false,
            Token = "DriveGrowl",
            UseBlueColorForInfo = true
        });
    }

    [RelayCommand]
    public async Task SearchFile(string fileName)
    {
        if (IsTrashMode)
        {
            return;
        }

        IsLoading = Visibility.Visible;
        var result = await Provider.SearchAsync(fileName);
        if (result.IsSuccess)
        {
            LoadFiles(result.Data);
        }
        else
        {
            Growl.Error(new GrowlInfo
            {
                Title = ResourceHelper.GetLocalized("Error"),
                Message = result.ErrorMessage,
                StaysOpen = false,
                Token = "DriveGrowl"
            });
        }
        IsLoading = Visibility.Collapsed;
    }

    [RelayCommand]
    private async Task DownloadFiles()
    {
        Window _downloadPathSelectWindow = new();
        IntPtr hwnd = WindowNative.GetWindowHandle(_downloadPathSelectWindow);
        FolderPicker savePicker = new()
        {
            SuggestedStartLocation = PickerLocationId.Downloads,
        };
        InitializeWithWindow.Initialize(savePicker, hwnd);
        StorageFolder folder = await savePicker.PickSingleFolderAsync();
        if (folder != null)
        {
            TaskManagerViewModel manager = App.GetService<TaskManagerViewModel>();
            var tasks = SelectedItems.Select(i => manager.AddDownloadTask(this, i.Id, folder));
            Growl.Info(new GrowlInfo
            {
                Title = ResourceHelper.GetLocalized("TaskManagerPage_StartDownload"),
                Message = string.Format(ResourceHelper.GetLocalized("TaskManagerPage_StartDownloadDesc"), SelectedItems.Count),
                IsClosable = true,
                ShowDateTime = true,
                Token = "DriveGrowl",
                UseBlueColorForInfo = true
            });
            await Task.WhenAll(tasks);
        }
    }

    public async Task<IReadOnlyList<string>> GetSelectedDownloadUrlsAsync()
    {
        if (SelectedItems.Count == 0)
        {
            return [];
        }

        var files = SelectedItems.Where(item => item.IsFile).ToList();
        if (files.Count == 0)
        {
            return [];
        }

        var results = await Task.WhenAll(files.Select(async file => new
        {
            file.Name,
            Result = await file.GetDownloadUrlResultAsync()
        }));

        var urls = results
            .Where(result => result.Result.IsSuccess && !string.IsNullOrWhiteSpace(result.Result.Data))
            .Select(result => result.Result.Data)
            .ToList();

        var failed = results
            .Where(result => !result.Result.IsSuccess || string.IsNullOrWhiteSpace(result.Result.Data))
            .Select(result => string.IsNullOrWhiteSpace(result.Result.ErrorMessage)
                ? result.Name
                : $"{result.Name}: {result.Result.ErrorMessage}");
        if (failed.Any())
        {
            ShowError("ExternalDownloader_NoDownloadUrl", failed);
        }

        return urls;
    }

    public void FilterByName(string name)
    {
        var filesToRemove = Files.Where(file => !file.Name.Contains(name)).ToList();
        foreach (var file in filesToRemove)
        {
            Files.Remove(file);
            Images.Remove(file);
        }
    }

    private BookmarkDTO CreateBookmark(FileViewModel file)
    {
        List<BookmarkPathSegmentDTO> pathSegments = BreadcrumbItems
            .Select(item => new BookmarkPathSegmentDTO { Name = item.Name, ItemId = item.ItemId })
            .ToList();

        if (file.IsFolder)
        {
            pathSegments.Add(new BookmarkPathSegmentDTO { Name = file.Name, ItemId = file.Id });
        }

        return new BookmarkDTO
        {
            Id = file.Id,
            Name = file.Name,
            IsFolder = file.IsFolder,
            ParentId = ParentItemId,
            DriveId = Provider.DriveId,
            ProviderType = Provider.ProviderType,
            AccountId = Provider.AccountId,
            DriveDisplayName = DisplayName,
            PathSegments = pathSegments,
            CreatedAt = DateTimeOffset.Now
        };
    }

    private void NotifyModeChanged()
    {
        OnPropertyChanged(nameof(CanEditCurrentFolder));
        OnPropertyChanged(nameof(CanSearch));
        OnPropertyChanged(nameof(CanManageTrash));
        UpdateSelectionStatus();
    }

    private static void ShowSuccess(string titleKey)
    {
        Growl.Success(new GrowlInfo
        {
            Title = ResourceHelper.GetLocalized(titleKey),
            StaysOpen = false,
            Token = "DriveGrowl"
        });
    }

    private static void ShowError(string titleKey, IEnumerable<string> messages)
    {
        Growl.Error(new GrowlInfo
        {
            Title = ResourceHelper.GetLocalized(titleKey),
            StaysOpen = false,
            Message = string.Join(", ", messages.Where(message => !string.IsNullOrWhiteSpace(message))),
            Token = "DriveGrowl"
        });
    }

    private string _parentItemId = "Root";
    private readonly DirectoryListingCache _directoryCache = new(
        DirectoryCacheFreshDuration,
        DirectoryCacheStaleDuration,
        capacity: 50);
    private readonly object _filesRequestGate = new();
    private CancellationTokenSource _filesRequestCancellation;
    private long _filesRequestVersion;
    private readonly object _directoryRequestsGate = new();
    private readonly Dictionary<string, Task<StorageResult<PageResult<FileItem>>>> _directoryRequests = [];
    private PageResult<FileItem> _displayedPage;
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    [ObservableProperty]
    public partial Visibility IsLoading { get; set; } = Visibility.Collapsed;

    [ObservableProperty]
    public partial string StorageInfo { get; set; }

    [ObservableProperty]
    public partial bool IsCapacityLoading { get; set; }

    [ObservableProperty]
    public partial string StorageStatus { get; set; }

    [ObservableProperty]
    public partial string StorageDetails { get; set; }

    [ObservableProperty]
    public partial double StorageUsagePercent { get; set; }

    [ObservableProperty]
    public partial string SelectionStatus { get; set; }
    [ObservableProperty]
    public partial bool IsTrashMode { get; set; }
    [ObservableProperty]
    public partial bool CanOperateSelectedTrashItems { get; set; }
    public ObservableCollection<FileViewModel> Files { get; } = [];
    public ObservableCollection<FileViewModel> Images { get; } = [];
    public ObservableCollection<BreadcrumbItem> BreadcrumbItems { get; } = [];
    public ObservableCollection<FileViewModel> SelectedItems { get; set; } = [];
    [ObservableProperty]
    public partial bool CanBatchRename { get; set; }
    [ObservableProperty]
    public partial bool CanShowBatchRename { get; set; }

    public string ParentItemId => _parentItemId;
    public string PendingSelectedItemId { get; set; }
    public IStorageProvider Provider { get; }
    public string DisplayName { get; }
    public bool CanManageTrash => Provider.SupportsTrash;
    public bool CanEditCurrentFolder => CanWrite && !IsTrashMode;
    public bool CanSearch => !IsTrashMode;
    public string ProviderDisplayName => Provider.ProviderType switch
    {
        ProviderType.OneDrive => ResourceHelper.GetLocalized("CloudPage_Provider_OneDrive"),
        ProviderType.GoogleDrive => ResourceHelper.GetLocalized("CloudPage_Provider_GoogleDrive"),
        ProviderType.Local => ResourceHelper.GetLocalized("CloudPage_Provider_Local"),
        ProviderType.PikPak => ResourceHelper.GetLocalized("CloudPage_Provider_PikPak"),
        _ => Provider.ProviderType.ToString()
    };
    public ImageSource ProviderLogoSource => new BitmapImage(new Uri($"ms-appx:///Assets/ProviderLogos/{ProviderLogoFileName}"));
    private string ProviderLogoFileName => Provider.ProviderType switch
    {
        ProviderType.OneDrive => "OneDrive.png",
        ProviderType.GoogleDrive => "GoogleDrive.png",
        ProviderType.Local => "Local.png",
        ProviderType.PikPak => "PikPak.png",
        _ => "Local.png"
    };
    public string AccountSummary => Provider.ProviderType == ProviderType.Local
        ? string.Format(
            ResourceHelper.GetLocalized("CloudPage_PathSummary"),
            string.IsNullOrWhiteSpace(Provider.DriveId) ? "-" : Provider.DriveId)
        : string.Format(
            ResourceHelper.GetLocalized("CloudPage_AccountSummary"),
            string.IsNullOrWhiteSpace(Provider.AccountId)
                ? ResourceHelper.GetLocalized("CloudPage_UnknownValue")
                : Provider.AccountId);
    public string IdentifierSummary => Provider.ProviderType == ProviderType.Local
        ? ResourceHelper.GetLocalized("CloudPage_LocalDriveHint")
        : string.Format(
            ResourceHelper.GetLocalized("CloudPage_DriveIdSummary"),
            string.IsNullOrWhiteSpace(Provider.DriveId)
                ? ResourceHelper.GetLocalized("CloudPage_UnknownValue")
                : Provider.DriveId);
    public bool CanWrite => true;
}
