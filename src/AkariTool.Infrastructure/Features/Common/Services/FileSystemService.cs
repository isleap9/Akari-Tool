using System;
using System.IO;
using System.Threading.Tasks;
using AkariTool.Infrastructure.Features.Common.Interfaces;

namespace AkariTool.Infrastructure.Features.Common.Services;

/// <summary>
/// Default implementation that delegates to System.IO static methods
/// (Winhance FileSystemService parity, restricted to Akari's IFileSystemService
/// surface). Previously a Track A Phase 2 throwing stub — which silently broke
/// WindowsUpdatePolicyHandler's DLL-rename probes (detection) and Disabled-mode
/// apply until 4h-era diagnosis.
/// </summary>
public sealed class FileSystemService : IFileSystemService
{
    public string CombinePath(string path1, string path2) => Path.Combine(path1, path2);
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public string GetTempPath() => Path.GetTempPath();
    public Task WriteAllTextAsync(string path, string content) => File.WriteAllTextAsync(path, content);
    public bool FileExists(string path) => File.Exists(path);
    public void DeleteFile(string path) => File.Delete(path);
}
