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
    }
}
