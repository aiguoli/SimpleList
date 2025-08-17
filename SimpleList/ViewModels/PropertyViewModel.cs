using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Linq;

namespace SimpleList.ViewModels;

public class PropertyViewModel : ObservableObject
{
    public PropertyViewModel(FileViewModel[] files)
    {
        _files = files;
    }

    private readonly FileViewModel[] _files;
    public string Name => _files.Length == 1 ? _files[0].Name : $"{_files.Length} files";
    public long? Size => _files.Length == 1 ? _files[0].Size : _files.Sum(f => f.Size);
    public DateTimeOffset? Updated => _files.Length == 1 ? _files[0].Updated : null;
    public bool IsFolder => _files.Length == 1 && _files[0].IsFolder;
    public int? ChildrenCount => _files.Length == 1 ? _files[0].ChildrenCount : null;
}
