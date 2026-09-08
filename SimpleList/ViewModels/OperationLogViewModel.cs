using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using SimpleList.Core.Models;
using SimpleList.Core.Services;
using SimpleList.Helpers;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace SimpleList.ViewModels;

public partial class OperationLogViewModel : ObservableObject
{
    private readonly OperationLogService _logService;
    private readonly DispatcherQueue _dispatcher;
    private bool _isListening;

    public OperationLogViewModel(OperationLogService logService)
    {
        _logService = logService;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        Reload();
    }

    public ObservableCollection<OperationLogItemViewModel> Entries { get; } = [];

    [ObservableProperty]
    public partial Visibility EmptyStateVisibility { get; set; } = Visibility.Visible;

    public string LogDirectory => _logService.DirectoryPath;

    public void StartListening()
    {
        if (_isListening)
        {
            return;
        }

        _logService.EntryAdded += LogService_EntryAdded;
        _logService.EntriesCleared += LogService_EntriesCleared;
        _isListening = true;
        Reload();
    }

    public void StopListening()
    {
        if (!_isListening)
        {
            return;
        }

        _logService.EntryAdded -= LogService_EntryAdded;
        _logService.EntriesCleared -= LogService_EntriesCleared;
        _isListening = false;
    }

    public void Clear()
    {
        _logService.Clear();
    }

    private void Reload()
    {
        Entries.Clear();
        foreach (OperationLogEntry entry in _logService.GetRecentEntries().OrderByDescending(entry => entry.Timestamp))
        {
            Entries.Add(new OperationLogItemViewModel(entry));
        }
        UpdateEmptyState();
    }

    private void LogService_EntryAdded(object sender, OperationLogEntry entry)
    {
        RunOnUiThread(() =>
        {
            if (Entries.Any(item => item.Entry.Id == entry.Id))
            {
                return;
            }

            Entries.Insert(0, new OperationLogItemViewModel(entry));
            while (Entries.Count > 500)
            {
                Entries.RemoveAt(Entries.Count - 1);
            }
            UpdateEmptyState();
        });
    }

    private void LogService_EntriesCleared(object sender, EventArgs args)
    {
        RunOnUiThread(() =>
        {
            Entries.Clear();
            UpdateEmptyState();
        });
    }

    private void RunOnUiThread(Action action)
    {
        if (_dispatcher == null || _dispatcher.HasThreadAccess)
        {
            action();
            return;
        }

        _dispatcher.TryEnqueue(() => action());
    }

    private void UpdateEmptyState()
    {
        EmptyStateVisibility = Entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}

public sealed class OperationLogItemViewModel
{
    public OperationLogItemViewModel(OperationLogEntry entry)
    {
        Entry = entry;
    }

    public OperationLogEntry Entry { get; }
    public string TimestampText => Entry.TimestampText;
    public string Category => Entry.Category;
    public string Action => Entry.Action;
    public string Details => Entry.Details;
    public Visibility DetailsVisibility => string.IsNullOrWhiteSpace(Details) ? Visibility.Collapsed : Visibility.Visible;
    public string LevelText => ResourceHelper.GetLocalized($"OperationLog_Level_{Entry.Level}");
    public string LevelGlyph => Entry.Level switch
    {
        OperationLogLevel.Success => "\uE73E",
        OperationLogLevel.Warning => "\uE7BA",
        OperationLogLevel.Error => "\uEA39",
        _ => "\uE946",
    };
}
