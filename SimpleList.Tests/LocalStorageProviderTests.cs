using SimpleList.Core.Models;
using SimpleList.Core.Services;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace SimpleList.Tests;

public class LocalStorageProviderTests
{
    [Fact]
    public async Task UploadFileContentAsync_CopiesStreamToTargetFolder()
    {
        string root = Path.Combine(Path.GetTempPath(), "SimpleListTests", Path.GetRandomFileName());
        Directory.CreateDirectory(root);
        try
        {
            LocalStorageProvider provider = new(root);
            await using MemoryStream content = new(Encoding.UTF8.GetBytes("streamed migration content"));

            var result = await provider.UploadFileContentAsync(content, "migrated.txt", "Root");

            Assert.True(result.IsSuccess, result.ErrorMessage);
            Assert.True(File.Exists(Path.Combine(root, "migrated.txt")));
            Assert.Equal("streamed migration content", await File.ReadAllTextAsync(Path.Combine(root, "migrated.txt")));
            Assert.Equal("migrated.txt", result.Data.Name);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public async Task GetItemAsync_RejectsPathOutsideConfiguredRoot()
    {
        string parent = Path.Combine(Path.GetTempPath(), "SimpleListTests", Path.GetRandomFileName());
        string root = Path.Combine(parent, "root");
        string outsideFile = Path.Combine(parent, "outside.txt");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(outsideFile, "must remain inaccessible");
        try
        {
            LocalStorageProvider provider = new(root);

            var result = await provider.GetItemAsync(outsideFile);

            Assert.False(result.IsSuccess);
            Assert.Equal(StorageErrorType.Forbidden, result.ErrorType);
        }
        finally
        {
            if (Directory.Exists(parent))
            {
                Directory.Delete(parent, true);
            }
        }
    }

    [Fact]
    public async Task DeleteAsync_RejectsParentTraversalOutsideConfiguredRoot()
    {
        string parent = Path.Combine(Path.GetTempPath(), "SimpleListTests", Path.GetRandomFileName());
        string root = Path.Combine(parent, "root");
        string outsideFile = Path.Combine(parent, "outside.txt");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(outsideFile, "must not be deleted");
        try
        {
            LocalStorageProvider provider = new(root);
            string traversalPath = Path.Combine(root, "..", "outside.txt");

            var result = await provider.DeleteAsync(traversalPath);

            Assert.False(result.IsSuccess);
            Assert.True(File.Exists(outsideFile));
        }
        finally
        {
            if (Directory.Exists(parent))
            {
                Directory.Delete(parent, true);
            }
        }
    }
}
