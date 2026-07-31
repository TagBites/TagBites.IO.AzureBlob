using System.Net;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using TagBites.IO.Operations;

namespace TagBites.IO.AzureBlob;

internal class AzureBlobFileSystemOperations : IFileSystemAsyncWriteOperations, IFileSystemMetadataSupport
{
    // Azure Blob Storage silently drops a trailing "/" from a blob name on upload, so a blob keyed by the directory's own prefix would be indistinguishable from a real file - a dedicated marker name inside the prefix survives as-is.
    private const string DirectoryMarkerName = ".tagbites-directory";

    private readonly BlobContainerClient _containerClient;

    public char DirectorySeparator => '/';
    public string DirectorySeparatorString => "/";

    public string Kind => "azureblob";
    public string Name => _containerClient.Name;

    bool IFileSystemMetadataSupport.SupportsIsHiddenMetadata => false;
    bool IFileSystemMetadataSupport.SupportsIsReadOnlyMetadata => false;
    bool IFileSystemMetadataSupport.SupportsLastWriteTimeMetadata => false;

    public AzureBlobFileSystemOperations(string connectionString, string containerName)
    {
        if (containerName == null)
            throw new ArgumentNullException(nameof(containerName));

        _containerClient = new BlobContainerClient(connectionString, containerName);
    }

    public async Task<IFileSystemStructureLinkInfo?> GetLinkInfoAsync(string fullName)
    {
        try
        {
            return await GetLinkInfoCoreAsync(fullName);
        }
        catch (RequestFailedException e) when (e.Status == (int)HttpStatusCode.NotFound)
        {
            if (Path.HasExtension(fullName))
                return null;

            var directoryFullName = GetCorrectDirectoryFullName(fullName);
            try
            {
                var markerClient = _containerClient.GetBlobClient(directoryFullName + DirectoryMarkerName);
                var response = await markerClient.GetPropertiesAsync();
                return new DirectoryInfo(fullName, response.Value);
            }
            catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
            {
                // Intermediate ancestors never get their own marker blob, only the leaf does - treat the directory as existing if anything shares its prefix.
                await foreach (var _ in _containerClient.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix: directoryFullName, cancellationToken: default))
                    return new DirectoryInfo(fullName);

                return null;
            }
        }
    }
    private async Task<IFileSystemStructureLinkInfo> GetLinkInfoCoreAsync(string fullName)
    {
        var blobClient = _containerClient.GetBlobClient(fullName);
        var response = await blobClient.GetPropertiesAsync();
        return new FileInfo(fullName, response.Value);
    }

    public async Task ReadFileAsync(FileLink file, Stream stream)
    {
        var blobClient = _containerClient.GetBlobClient(file.FullName);
        await blobClient.DownloadToAsync(stream);
    }
    public async Task<IFileLinkInfo> WriteFileAsync(FileLink file, Stream stream, bool overwrite)
    {
        var blobClient = _containerClient.GetBlobClient(file.FullName);

        try
        {
            await blobClient.UploadAsync(stream, overwrite);
        }
        catch (RequestFailedException e) when (!overwrite && e.Status == (int)HttpStatusCode.Conflict)
        {
            throw new IOException($"Unable to create a new file. File already exists: {file.FullName}", e);
        }

        var response = await blobClient.GetPropertiesAsync();
        return new FileInfo(file.FullName, response.Value);
    }
    public async Task<IFileLinkInfo> MoveFileAsync(FileLink source, FileLink destination, bool overwrite)
    {
        var sourceClient = _containerClient.GetBlobClient(source.FullName);
        var destinationClient = _containerClient.GetBlobClient(destination.FullName);

        if (!overwrite && await destinationClient.ExistsAsync())
            throw new IOException($"Unable to move a new file. File already exists: {destination.FullName}");

        var operation = await destinationClient.StartCopyFromUriAsync(sourceClient.Uri);
        await operation.WaitForCompletionAsync();
        await sourceClient.DeleteIfExistsAsync();

        var response = await destinationClient.GetPropertiesAsync();
        return new FileInfo(destination.FullName, response.Value);
    }
    public async Task DeleteFileAsync(FileLink file) => await _containerClient.GetBlobClient(file.FullName).DeleteIfExistsAsync();

    public async Task<IFileSystemStructureLinkInfo> CreateDirectoryAsync(DirectoryLink directory)
    {
        var directoryFullName = GetCorrectDirectoryFullName(directory.FullName);
        var blobClient = _containerClient.GetBlobClient(directoryFullName + DirectoryMarkerName);

        using (var emptyStream = new MemoryStream(Array.Empty<byte>()))
            await blobClient.UploadAsync(emptyStream, overwrite: true);

        var response = await blobClient.GetPropertiesAsync();
        return new DirectoryInfo(directory.FullName, response.Value);
    }
    public async Task<IFileSystemStructureLinkInfo> MoveDirectoryAsync(DirectoryLink source, DirectoryLink destination)
    {
        var sourceFullName = GetCorrectDirectoryFullName(source.FullName);
        var destinationFullName = GetCorrectDirectoryFullName(destination.FullName);

        // Azure Blob Storage has no atomic rename - a directory is a key prefix, so every blob under it must be copied to the new prefix and removed.
        await foreach (var blobItem in _containerClient.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix: sourceFullName, cancellationToken: default))
        {
            var destinationKey = destinationFullName + blobItem.Name.Substring(sourceFullName.Length);
            var sourceClient = _containerClient.GetBlobClient(blobItem.Name);
            var destinationClient = _containerClient.GetBlobClient(destinationKey);

            var operation = await destinationClient.StartCopyFromUriAsync(sourceClient.Uri);
            await operation.WaitForCompletionAsync();
            await sourceClient.DeleteIfExistsAsync();
        }

        // The copy loop already carried over the source's own marker blob if it had one; otherwise GetLinkInfoAsync falls back to a prefix check.
        return await GetLinkInfoAsync(destination.FullName)
            ?? throw new IOException($"Failed to move directory: {source.FullName}");
    }
    public async Task DeleteDirectoryAsync(DirectoryLink directory, bool recursive)
    {
        var directoryFullName = GetCorrectDirectoryFullName(directory.FullName);
        var markerName = directoryFullName + DirectoryMarkerName;

        if (recursive)
        {
            await foreach (var blobItem in _containerClient.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix: directoryFullName, cancellationToken: default))
                await _containerClient.GetBlobClient(blobItem.Name).DeleteIfExistsAsync();
        }
        else
        {
            var hasOtherContent = false;
            await foreach (var blobItem in _containerClient.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix: directoryFullName, cancellationToken: default))
            {
                if (blobItem.Name != markerName)
                {
                    hasOtherContent = true;
                    break;
                }
            }

            if (hasOtherContent)
                throw new IOException($"Folder is not empty: {directory.FullName}");

            await _containerClient.GetBlobClient(markerName).DeleteIfExistsAsync();
        }
    }

    public async Task<IList<IFileSystemStructureLinkInfo>> GetLinksAsync(DirectoryLink directory, FileSystem.ListingOptions options)
    {
        var directoryFullName = GetCorrectDirectoryFullName(directory.FullName);
        options.RecursiveHandled = true;

        var result = new List<IFileSystemStructureLinkInfo>();

        if (options.Recursive)
        {
            await foreach (var blobItem in _containerClient.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix: directoryFullName, cancellationToken: default))
            {
                if (blobItem.Name == directoryFullName + DirectoryMarkerName)
                    continue; // this directory's own marker, not a child

                // A recursive listing includes nested subdirectories' marker blobs too, so this branch needs its own SearchForFiles/SearchForDirectories check per item.
                if (blobItem.Name.EndsWith(DirectoryMarkerName, StringComparison.Ordinal))
                {
                    if (options.SearchForDirectories)
                    {
                        var childDirectoryFullName = blobItem.Name.Substring(0, blobItem.Name.Length - DirectoryMarkerName.Length);
                        result.Add(new DirectoryInfo(childDirectoryFullName, blobItem));
                    }
                }
                else if (options.SearchForFiles)
                    result.Add(new FileInfo(blobItem));
            }
        }
        else
        {
            await foreach (var item in _containerClient.GetBlobsByHierarchyAsync(BlobTraits.None, BlobStates.None, DirectorySeparatorString, directoryFullName, default))
            {
                if (item.IsPrefix)
                {
                    if (options.SearchForDirectories)
                    {
                        var info = await GetLinkInfoAsync(item.Prefix);
                        if (info != null)
                            result.Add(info);
                    }
                }
                else if (item.IsBlob && item.Blob.Name != directoryFullName + DirectoryMarkerName)
                {
                    if (options.SearchForFiles)
                        result.Add(new FileInfo(item.Blob));
                }
            }
        }

        return result;
    }

    public async Task<IFileSystemStructureLinkInfo> UpdateMetadataAsync(FileSystemStructureLink link, IFileSystemLinkMetadata metadata)
    {
        // The link was just updated, so it necessarily still exists.
        return (await GetLinkInfoAsync(link.FullName))!;
    }

    private string GetCorrectDirectoryFullName(string directoryFullName) => directoryFullName.TrimEnd(DirectorySeparator) + DirectorySeparator;

    private class FileInfo : IFileLinkInfo
    {
        public string FullName { get; }
        public bool Exists => true;
        public bool? IsDirectory => false;
        public DateTime? CreationTime { get; }
        public DateTime? LastWriteTime { get; }
        public bool IsHidden => false;
        public bool IsReadOnly => false;

        public string ContentPath => FullName;
        public FileHash Hash { get; }
        public long Length { get; }

        public FileInfo(string fullName, BlobProperties properties)
        {
            FullName = fullName;
            CreationTime = properties.LastModified.LocalDateTime;
            LastWriteTime = properties.LastModified.LocalDateTime;
            Length = properties.ContentLength;
            Hash = properties.ContentHash is { Length: > 0 }
                ? new FileHash(FileHashAlgorithm.Md5, BitConverter.ToString(properties.ContentHash))
                : FileHash.Empty;
        }
        public FileInfo(BlobItem item)
        {
            FullName = item.Name;
            CreationTime = item.Properties.LastModified?.LocalDateTime;
            LastWriteTime = item.Properties.LastModified?.LocalDateTime;
            Length = item.Properties.ContentLength ?? 0;
            Hash = item.Properties.ContentHash is { Length: > 0 }
                ? new FileHash(FileHashAlgorithm.Md5, BitConverter.ToString(item.Properties.ContentHash))
                : FileHash.Empty;
        }
    }
    private class DirectoryInfo : IFileSystemStructureLinkInfo
    {
        public string FullName { get; }
        public bool Exists => true;
        public bool? IsDirectory => true;
        public DateTime? CreationTime { get; }
        public DateTime? LastWriteTime { get; }
        public bool IsHidden => false;
        public bool IsReadOnly => false;

        public DirectoryInfo(string fullName, BlobProperties properties)
        {
            FullName = fullName;
            CreationTime = properties.LastModified.LocalDateTime;
            LastWriteTime = properties.LastModified.LocalDateTime;
        }
        // fullName is the directory's own path, not the marker blob's.
        public DirectoryInfo(string fullName, BlobItem item)
        {
            FullName = fullName;
            CreationTime = item.Properties.LastModified?.LocalDateTime;
            LastWriteTime = item.Properties.LastModified?.LocalDateTime;
        }
        // A directory implied purely by a key prefix has no marker blob, so no timestamp to report.
        public DirectoryInfo(string fullName)
        {
            FullName = fullName;
        }
    }
}
