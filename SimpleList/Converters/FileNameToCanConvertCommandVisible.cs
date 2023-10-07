using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;
using System.Linq;

namespace SimpleList.Converters
{
    public class FileNameToCanConvertCommandVisible : IValueConverter
    {
        public static readonly FileNameToCanConvertCommandVisible Instance = new();
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            string[] allowedExtensions = { ".csv", ".doc", ".docx", ".odp", ".ods", ".odt", ".pot", ".potm", ".potx", ".pps", ".ppsx", ".ppsxm", ".ppt", ".pptm", ".pptx", ".rtf", ".xls", ".xlsx" };
            if (value is string fileName)
            {
                string fileExtension = System.IO.Path.GetExtension(fileName).ToLower();
                if (allowedExtensions.Contains(fileExtension))
                {
                    return Visibility.Visible;
                }
            }
            return Visibility.Collapsed;
        }


        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
