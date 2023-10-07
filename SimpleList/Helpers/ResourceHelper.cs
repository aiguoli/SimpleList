using Microsoft.Windows.ApplicationModel.Resources;

namespace SimpleList.Helpers
{
    public static class ResourceHelper
    {
        private static readonly ResourceLoader _resourceLoader = new();

        public static string GetLocalized(this string resourceKey) => _resourceLoader.GetString(resourceKey);
    }
}
