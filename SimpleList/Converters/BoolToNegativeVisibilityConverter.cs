using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace SimpleList.Converters
{
    public partial class BoolToNegativeVisibilityConverter : IValueConverter
    {
        public static readonly BoolToNegativeVisibilityConverter Instance = new();
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
