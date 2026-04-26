using Microsoft.UI.Xaml.Data;
using System;

namespace SimpleList.Converters
{
    public class FileSizeConverter : IValueConverter
    {
        public static readonly FileSizeConverter Instance = new();
        
        // Optimize: Use static array for units to avoid repeated allocations and ifs
        private static readonly string[] _units = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB" };

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            double dValue;

            // Unify type handling by converting to double
            if (value is long l)
            {
                dValue = l;
            }
            else if (value is ulong ul)
            {
                dValue = ul;
            }
            else if (value is double d)
            {
                dValue = d;
            }
            else
            {
                return "";
            }

            // Original logic returns empty string for 0
            if (dValue == 0) return "";

            int unitIndex = 0;
            while (dValue >= 1024 && unitIndex < _units.Length - 1)
            {
                dValue /= 1024;
                unitIndex++;
            }

            return $"{dValue.ToString(unitIndex == 0 ? "F0" : "F1")} {_units[unitIndex]}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
