using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace SimpleList.Converters
{
    public partial class StringToBoolConverter : IValueConverter
    {
        public static readonly StringToBoolConverter Instance = new();
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string str)
            {
                return !string.IsNullOrEmpty(str);
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
