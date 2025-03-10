using OpenSage.FileFormats.Big;

namespace OpenSage.IO;

public sealed class BigFileSystem : FileSystem
{
    private readonly BigDirectory _rootDirectory;

    public BigFileSystem(string rootDirectory)
    {
        _rootDirectory = new BigDirectory();

        SkudefReader.Read(rootDirectory, AddBigArchive);
    }

    private void AddBigArchive(string path)
    {
        var bigArchive = AddDisposable(new BigArchive(path));

        foreach (var bigArchiveEntry in bigArchive.Entries)
        {
            var directoryParts = bigArchiveEntry.FullName.Split('\\', '/');

            var bigDirectory = _rootDirectory;
            for (var i = 0; i < directoryParts.Length - 1; i++)
            {
                bigDirectory = bigDirectory.GetOrCreateDirectory(directoryParts[i]);
            }

            var fileName = directoryParts[^1];

            bigDirectory.Files.TryAdd(fileName, bigArchiveEntry);
        }
    }

    public override IEnumerable<FileSystemEntry> GetFilesInDirectory(
        string directoryPath,
        string searchPattern = "*",
        SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        var search = new SearchPattern(searchPattern);

        var bigDirectory = _rootDirectory;

        if (directoryPath == "")
        {
            return GetFilesInDirectory(bigDirectory, search, searchOption);
        }

        var directoryParts = NormalizeFilePath(directoryPath).Split(Path.DirectorySeparatorChar);
        return directoryParts.Any(directoryPart => !bigDirectory.Directories.TryGetValue(directoryPart, out bigDirectory))
            ? []
            : GetFilesInDirectory(bigDirectory, search, searchOption);
    }

    private IEnumerable<FileSystemEntry> GetFilesInDirectory(
        BigDirectory bigDirectory,
        SearchPattern searchPattern,
        SearchOption searchOption)
    {
        foreach (var file in bigDirectory.Files.Values.Where(file => searchPattern.Match(file.FullName)))
        {
            yield return CreateFileSystemEntry(file);
        }

        if (searchOption != SearchOption.AllDirectories)
        {
            yield break;
        }

        {
            foreach (var file in bigDirectory.Directories.Values.SelectMany(directory => GetFilesInDirectory(directory, searchPattern, searchOption)))
            {
                yield return file;
            }
        }
    }

    public override FileSystemEntry? GetFile(string filePath)
    {
        var directoryParts = NormalizeFilePath(filePath).Split(Path.DirectorySeparatorChar);

        var bigDirectory = _rootDirectory;
        for (var i = 0; i < directoryParts.Length - 1; i++)
        {
            if (!bigDirectory.Directories.TryGetValue(directoryParts[i], out bigDirectory))
            {
                return null;
            }
        }

        var fileName = directoryParts[^1];

        return !bigDirectory.Files.TryGetValue(fileName, out var file) ? null : CreateFileSystemEntry(file);
    }

    private FileSystemEntry CreateFileSystemEntry(BigArchiveEntry entry)
    {
        return new FileSystemEntry(
            this,
            NormalizeFilePath(entry.FullName),
            entry.Length,
            entry.Open);
    }

    private sealed class BigDirectory
    {
        public readonly Dictionary<string, BigDirectory> Directories = new(StringComparer.InvariantCultureIgnoreCase);
        public readonly Dictionary<string, BigArchiveEntry> Files = new(StringComparer.InvariantCultureIgnoreCase);

        public BigDirectory GetOrCreateDirectory(string directoryName)
        {
            if (Directories.TryGetValue(directoryName, out var directory))
            {
                return directory;
            }

            directory = new BigDirectory();
            Directories.Add(directoryName, directory);
            return directory;
        }
    }
}
