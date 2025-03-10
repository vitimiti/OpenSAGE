namespace OpenSage.IO;

public sealed class CompositeFileSystem : FileSystem
{
    private readonly FileSystem[] _fileSystems;

    public CompositeFileSystem(params FileSystem[] fileSystems)
    {
        _fileSystems = fileSystems;

        foreach (var fileSystem in _fileSystems)
        {
            AddDisposable(fileSystem);
        }
    }

    public override FileSystemEntry? GetFile(string filePath)
    {
        return _fileSystems.Select(fileSystem => fileSystem.GetFile(filePath)).OfType<FileSystemEntry>().FirstOrDefault();
    }

    public override IEnumerable<FileSystemEntry> GetFilesInDirectory(
        string directoryPath,
        string searchPattern = "*",
        SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        var paths = new HashSet<string>();

        foreach (var fileSystem in _fileSystems)
        {
            foreach (var fileSystemEntry in fileSystem.GetFilesInDirectory(directoryPath, searchPattern, searchOption))
            {
                if (!paths.Add(fileSystemEntry.FilePath))
                {
                    continue;
                }

                yield return fileSystemEntry;
            }
        }
    }
}
