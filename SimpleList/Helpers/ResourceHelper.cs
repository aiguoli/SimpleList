using Microsoft.Windows.ApplicationModel.Resources;
using System;

namespace SimpleList.Helpers
{
    public static class ResourceHelper
    {
        private static readonly ResourceLoader _resourceLoader = new();

        public static string GetLocalized(params string[] resourceKeys)
        {
            if (resourceKeys == null || resourceKeys.Length == 0)
            {
                return string.Empty;
            }

            foreach (var key in resourceKeys)
            {
                var value = GetLocalizedOrEmptyInternal(key);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        public static string GetLocalized(this string resourceKey)
        {
            if (string.IsNullOrWhiteSpace(resourceKey))
            {
                return string.Empty;
            }

            try
            {
                var value = _resourceLoader.GetString(resourceKey);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
            catch (Exception)
            {
            }

            // Backward-compatible fallback for callers that omit the .Text suffix.
            if (!resourceKey.EndsWith(".Text", StringComparison.Ordinal))
            {
                try
                {
                    var fallbackValue = _resourceLoader.GetString($"{resourceKey}.Text");
                    if (!string.IsNullOrWhiteSpace(fallbackValue))
                    {
                        return fallbackValue;
                    }
                }
                catch (Exception)
                {
                }
            }

            return resourceKey;
        }

        private static string GetLocalizedOrEmptyInternal(string resourceKey)
        {
            if (string.IsNullOrWhiteSpace(resourceKey))
            {
                return string.Empty;
            }

            try
            {
                var value = _resourceLoader.GetString(resourceKey);
                if (!string.IsNullOrWhiteSpace(value) && !string.Equals(value, resourceKey, StringComparison.Ordinal))
                {
                    return value;
                }
            }
            catch (Exception)
            {
            }

            if (!resourceKey.EndsWith(".Text", StringComparison.Ordinal))
            {
                try
                {
                    var textKey = $"{resourceKey}.Text";
                    var fallbackValue = _resourceLoader.GetString(textKey);
                    if (!string.IsNullOrWhiteSpace(fallbackValue) && !string.Equals(fallbackValue, textKey, StringComparison.Ordinal))
                    {
                        return fallbackValue;
                    }
                }
                catch (Exception)
                {
                }
            }

            return string.Empty;
        }
    }
}
