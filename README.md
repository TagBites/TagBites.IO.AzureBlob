# TagBites.IO.AzureBlob

[![Nuget](https://img.shields.io/nuget/v/TagBites.IO.AzureBlob.svg)](https://www.nuget.org/packages/TagBites.IO.AzureBlob/)
![.NET Standard 2.1](https://img.shields.io/badge/.NET%20Standard-2.1-512BD4)
[![License](https://img.shields.io/github/license/TagBites/TagBites.IO.AzureBlob)](https://github.com/TagBites/TagBites.IO.AzureBlob/blob/master/LICENSE.md)

Azure Blob Storage file system support for [TagBites.IO](https://github.com/TagBites/TagBites.IO), built on `Azure.Storage.Blobs`. Browse, read and write an Azure Storage container through the same `FileSystem` API used for local disk and other storages.

## Install

```
dotnet add package TagBites.IO.AzureBlob
```

Targets `netstandard2.1`. Depends on `Azure.Storage.Blobs`.

## Usage

```csharp
using TagBites.IO.AzureBlob;

var fs = AzureBlobFileSystem.Create(connectionString, containerName);

var file = fs.GetFile("/reports/summary.txt");
file.WriteAllText("Hello world!");

var content = file.ReadAllText();
```

Blob storage has no real directory concept, so directories are represented as blob name prefixes.

## Capabilities

- Asynchronous operations. Synchronous calls run on top of them.
- Metadata: none.
- Object storage: a directory exists only as a prefix of a blob name (`FileSystemFlags.IsDirectoryAsPrefix`), so an empty directory cannot be represented.

## Links

- [Changelog](https://github.com/TagBites/TagBites.IO.AzureBlob/blob/master/CHANGELOG.md)
- [Security policy](https://github.com/TagBites/TagBites.IO.AzureBlob/blob/master/SECURITY.md)
- [License (MIT)](https://github.com/TagBites/TagBites.IO.AzureBlob/blob/master/LICENSE.md)
