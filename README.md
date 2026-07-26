# TagBites.IO.AzureBlob

Azure Blob Storage file system support for [TagBites.IO](https://github.com/TagBites/TagBites.IO), built on `Azure.Storage.Blobs`. Browse, read and write an Azure Storage container through the same `FileSystem` API used for local disk and other storages.

## Install

```
dotnet add package TagBites.IO.AzureBlob
```

## Usage

```csharp
using TagBites.IO.AzureBlob;

var fs = AzureBlobFileSystem.Create(connectionString, containerName);

var file = fs.GetFile("/reports/summary.txt");
file.WriteAllText("Hello world!");

var content = file.ReadAllText();
```

Blob storage has no real directory concept, so directories are represented as blob name prefixes.

## License

See [https://www.tagbites.com/io](https://www.tagbites.com/io) for licensing terms.
