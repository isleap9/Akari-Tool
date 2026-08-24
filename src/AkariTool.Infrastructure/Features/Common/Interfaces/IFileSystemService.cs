using System.Threading.Tasks;

namespace AkariTool.Infrastructure.Features.Common.Interfaces;

public interface IFileSystemService
{
    string CombinePath(string path1, string path2);
    void CreateDirectory(string path);
    string GetTempPath();
    Task WriteAllTextAsync(string path, string content);
    bool FileExists(string path);
    void DeleteFile(string path);
}
