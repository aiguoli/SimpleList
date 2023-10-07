using Microsoft.UI.Xaml.Data;
using System;

namespace SimpleList.Converters
{
    public class FileSizeConverter : IValueConverter
    {
        public static readonly FileSizeConverter Instance = new();
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is long size)
            {
                if (size == 0) return "";
                if (size < 1024)
                {
                    return size.ToString("F0") + " bytes";
                }

                if (size >> 10 < 1024)
                {
                    return (size / 1024F).ToString("F1") + " KB";
                }

                if (size >> 20 < 1024)
                {
                    return ((size >> 10) / 1024F).ToString("F1") + " MB";
                }

                if (size >> 30 < 1024)
                {
                    return ((size >> 20) / 1024F).ToString("F1") + " GB";
                }

                if (size >> 40 < 1024)
                {
                    return ((size >> 30) / 1024F).ToString("F1") + " TB";
                }

                if (size >> 50 < 1024)
                {
                    return ((size >> 40) / 1024F).ToString("F1") + " PB";
                }

                return ((size >> 50) / 1024F).ToString("F1") + " EB";
            }

            return "";
        }


        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
