using Xunit;

namespace TagBites.IO.AzureBlob.Tests;

public class AzureBlobFileSystemTests
{
    // The well-known Azure Storage Emulator / Azurite development connection string, documented by
    // Microsoft (see "UseDevelopmentStorage" docs) - not a real credential. No network call is made
    // by the client constructor, so it is safe to use for construction-only tests.
    private const string ConnectionString =
        "DefaultEndpointsProtocol=https;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;EndpointSuffix=core.windows.net";

    [Fact]
    public void Create_ValidArguments_KindIsAzureBlob()
    {
        var fileSystem = AzureBlobFileSystem.Create(ConnectionString, "my-container");

        Assert.Equal("azureblob", fileSystem.Kind);
    }

    [Fact]
    public void Create_ValidArguments_NameIsContainerName()
    {
        var fileSystem = AzureBlobFileSystem.Create(ConnectionString, "my-container");

        Assert.Equal("my-container", fileSystem.Name);
    }

    [Fact]
    public void Create_NullContainerName_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => AzureBlobFileSystem.Create(ConnectionString, null!));
    }
}
