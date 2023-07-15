using Microsoft.UI.Xaml.Data;
using System;

namespace SimpleList.Converters
{
    public class DateTimeOffsetToStringConverter : IValueConverter
    {
        public static readonly DateTimeOffsetToStringConverter Instance = new();
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is DateTimeOffset dateTimeOffset)
            {
                return dateTimeOffset.ToString("yyyy/MM/dd HH:mm");
            }

            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
