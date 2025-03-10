using System.Diagnostics.CodeAnalysis;

namespace OpenSage.IO;

public sealed class VirtualFileSystem : FileSystem
{
    private readonly string _virtualDirectory;
    private readonly FileSystem _targetFileSystem;

    public VirtualFileSystem(string virtualDirectory, FileSystem targetFileSystem)
    {
        _virtualDirectory = virtualDirectory;
        _targetFileSystem = targetFileSystem;
    }

    public override FileSystemEntry? GetFile(string filePath)
    {
        return !TryGetRelativePath(filePath, out var relativePath) ? null : _targetFileSystem.GetFile(relativePath);
    }

    public override IEnumerable<FileSystemEntry> GetFilesInDirectory(
        string directoryPath,
        string searchPattern = "*",
        SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        if (!TryGetRelativePath(directoryPath, out var relativePath))
        {
            return [];
        }

        return _targetFileSystem.GetFilesInDirectory(
            relativePath,
            searchPattern,
            searchOption);
    }

    private bool TryGetRelativePath(string path, [NotNullWhen(true)] out string? relativePath)
    {
        if (!path.StartsWith(_virtualDirectory))
        {
            relativePath = null;
            return false;
        }

        relativePath = path[_virtualDirectory.Length..];
        return true;
    }
}
