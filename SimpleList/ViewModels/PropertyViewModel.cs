using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace SimpleList.ViewModels
{
    public class PropertyViewModel : ObservableObject
    {
        public PropertyViewModel(FileViewModel file)
        {
            _file = file;
        }

        private FileViewModel _file;
        public string Name => _file.Name;
        public long? Size => _file.Size;
        public DateTimeOffset? Updated => _file.Updated;
        public bool IsFolder => _file.IsFolder;
        public int? ChildrenCount => _file.ChildrenCount;
    }
}
