namespace TagBites.IO.AzureBlob;

/// <summary>
/// Exposes static method for creating an Azure Blob Storage file system.
/// </summary>
public static class AzureBlobFileSystem
{
    /// <summary>
    /// Creates an Azure Blob Storage file system.
    /// </summary>
    /// <param name="connectionString">The Azure Storage account connection string.</param>
    /// <param name="containerName">The name of an existing blob container.</param>
    /// <returns>An Azure Blob Storage file system contains the procedures that are used to perform file and directory operations.</returns>
    public static FileSystem Create(string connectionString, string containerName) =>
        new(new AzureBlobFileSystemOperations(connectionString, containerName), FileSystemFlags.IsDirectoryAsPrefix);
}
