using System;
using System.Threading.Tasks;
using AkariTool.Infrastructure.Features.Common.Interfaces;

namespace AkariTool.Infrastructure.Features.Common.Services;

/// <summary>Stub — declarative apply path not yet implemented (Track A Phase 2 follow-up).</summary>
public sealed class FileSystemService : IFileSystemService
{
    public string CombinePath(string path1, string path2) => throw new NotImplementedException();
    public void CreateDirectory(string path) => throw new NotImplementedException();
    public string GetTempPath() => throw new NotImplementedException();
    public Task WriteAllTextAsync(string path, string content) => throw new NotImplementedException();
    public bool FileExists(string path) => throw new NotImplementedException();
    public void DeleteFile(string path) => throw new NotImplementedException();
}
