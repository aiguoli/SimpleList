using System;
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

        public static string ReadableFileSize(long size)
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

    }
}
