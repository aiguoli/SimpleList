using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Util.Store;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace SimpleList.Core.Services;

public class GoogleTokenDataStore : IDataStore
{
    private readonly string _folderPath;

    public GoogleTokenDataStore(string folderPath)
    {
        _folderPath = folderPath;
        Directory.CreateDirectory(_folderPath);
    }

    private string FilePath(string key) => Path.Combine(_folderPath, SanitizeKey(key) + ".json");

    private static string SanitizeKey(string key)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            key = key.Replace(c, '_');
        }
        return key;
    }

    public Task StoreAsync<T>(string key, T value)
    {
        var serialized = Newtonsoft.Json.JsonConvert.SerializeObject(value);
        File.WriteAllText(FilePath(key), serialized);
        return Task.CompletedTask;
    }

    public Task DeleteAsync<T>(string key)
    {
        var path = FilePath(key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public Task<T> GetAsync<T>(string key)
    {
        var path = FilePath(key);
        if (!File.Exists(path)) return Task.FromResult(default(T));
        var content = File.ReadAllText(path);
        return Task.FromResult(Newtonsoft.Json.JsonConvert.DeserializeObject<T>(content));
    }

    public Task ClearAsync()
    {
        if (Directory.Exists(_folderPath))
        {
            foreach (var file in Directory.GetFiles(_folderPath, "*.json"))
            {
                File.Delete(file);
            }
        }
        return Task.CompletedTask;
    }
}
