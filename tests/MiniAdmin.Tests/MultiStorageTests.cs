using System.Text;
using Microsoft.Extensions.Options;
using MiniAdmin.Infrastructure.Storage;

namespace MiniAdmin.Tests;

public sealed class MultiStorageTests
{
    [Theory]
    [InlineData("oss", "cn-hangzhou", "https://oss-cn-hangzhou.aliyuncs.com")]
    [InlineData("cos", "ap-guangzhou", "https://cos.ap-guangzhou.myqcloud.com")]
    [InlineData("s3", "us-east-1", "")]
    public void Provider_endpoint_defaults_are_deterministic(
        string provider,
        string region,
        string expected)
    {
        var options = new S3CompatibleStorageOptions { Region = region };

        Assert.Equal(expected, options.ResolveEndpoint(provider));
    }

    [Fact]
    public void Minio_uses_path_style_and_custom_ssl_endpoint_by_default()
    {
        var options = new MinioStorageOptions
        {
            Endpoint = "minio.internal:9000",
            UseSsl = true
        };

        Assert.True(options.ForcePathStyle);
        Assert.Equal("https://minio.internal:9000", options.ResolveEndpoint("minio"));
    }

    [Theory]
    [InlineData("Local", "local")]
    [InlineData("Minio", "minio")]
    [InlineData("S3", "s3")]
    [InlineData("Oss", "oss")]
    [InlineData("Cos", "cos")]
    public void Composite_storage_resolves_supported_provider_names(
        string configured,
        string expected)
    {
        using var service = new CompositeFileStorageService(Options.Create(new FileStorageOptions
        {
            Provider = configured
        }));

        Assert.Equal(expected, service.ProviderName);
    }

    [Fact]
    public async Task Local_storage_behavior_remains_compatible()
    {
        var root = Path.Combine(Path.GetTempPath(), $"miniadmin-storage-{Guid.NewGuid():N}");
        try
        {
            using var service = new CompositeFileStorageService(Options.Create(new FileStorageOptions
            {
                Provider = "Local",
                Local = new LocalStorageOptions { RootPath = root }
            }));
            await using var upload = new MemoryStream(Encoding.UTF8.GetBytes("mini-admin"));

            var saved = await service.SaveAsync(upload, "readme.txt", "text/plain");
            Assert.Equal("local", saved.StorageProvider);
            Assert.True(await service.ExistsAsync(saved.StorageProvider, saved.StoragePath));

            await using (var download = await service.OpenReadAsync(saved.StorageProvider, saved.StoragePath))
            using (var reader = new StreamReader(download, Encoding.UTF8))
            {
                Assert.Equal("mini-admin", await reader.ReadToEndAsync());
            }

            await service.DeleteAsync(saved.StorageProvider, saved.StoragePath);
            Assert.False(await service.ExistsAsync(saved.StorageProvider, saved.StoragePath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
