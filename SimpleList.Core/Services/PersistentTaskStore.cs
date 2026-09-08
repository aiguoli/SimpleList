using SimpleList.Core.Models.DTO;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleList.Core.Services;

public sealed class PersistentTaskStore
{
    public PersistentTaskStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = filePath;
    }

    public async Task<PersistentTaskListDTO> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return new PersistentTaskListDTO();
        }

        try
        {
            await using FileStream stream = new(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            PersistentTaskListDTO result = await JsonSerializer.DeserializeAsync(
                stream,
                PersistentTaskJsonContext.Default.PersistentTaskListDTO,
                cancellationToken);
            return result?.Version == 1 ? result : new PersistentTaskListDTO();
        }
        catch (JsonException)
        {
            return new PersistentTaskListDTO();
        }
        catch (IOException)
        {
            return new PersistentTaskListDTO();
        }
    }

    public async Task SaveAsync(PersistentTaskListDTO tasks, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        string directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (FileStream stream = new(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    tasks,
                    PersistentTaskJsonContext.Default.PersistentTaskListDTO,
                    cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, _filePath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private readonly string _filePath;
}
