using SimpleList.Models;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Windows.Storage;

namespace SimpleList.Services
{
    public class Utils
    {
        public static async Task<ulong> GetFolderSize(StorageFolder folder)
        {
            ulong res = 0;
            foreach (StorageFile file in await folder.GetFilesAsync())
            {
                Windows.Storage.FileProperties.BasicProperties properties = await file.GetBasicPropertiesAsync();
                res += properties.Size;
            }

            foreach (StorageFolder subFolder in await folder.GetFoldersAsync())
            {
                res += await GetFolderSize(subFolder);
            }
            return res;
        }

        public static string ReadableFileSize(long? bytes)
        {
            if (bytes is long size)
            {
                if (size == 0) return "0";
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
            return "0";
        }

        public static FileType GetFileType(string ext)
        {
            return ext.ToLower() switch
            {
                ".txt" => FileType.Text,
                ".md" => FileType.Markdown,
                string img when ImageType.Contains(img) => FileType.Image,// https://learn.microsoft.com/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.controls.image?view=windows-app-sdk-1.4#image-file-formats
                string media when MediaType.Contains(media) => FileType.Media,
                _ => FileType.Unknown,
            };
        }

        public static string GetVersion()
        {
            Version version = Assembly.GetEntryAssembly().GetName().Version;
            return string.Format("{0}.{1}.{2}", version.Major, version.Minor, version.Build);
        }

        public static bool IsValidUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out Uri uriResult)
                && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        public static readonly string[] ImageType = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tiff", ".ico", ".svg" };
        public static readonly string[] MediaType = { "mp3", ".mp4", ".wma", ".3gp", ".aac", ".flac", ".wax", ".wav", ".wmx", ".wpl", ".avi" };
    }
}
