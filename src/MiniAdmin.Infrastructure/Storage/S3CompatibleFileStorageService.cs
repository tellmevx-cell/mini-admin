using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using MiniAdmin.Application.Contracts.Files;

namespace MiniAdmin.Infrastructure.Storage;

internal sealed class S3CompatibleFileStorageService : IFileStorageProvider, IDisposable
{
    private readonly S3CompatibleStorageOptions options;
    private readonly string providerName;
    private AmazonS3Client? client;

    public S3CompatibleFileStorageService(
        string providerName,
        S3CompatibleStorageOptions options)
    {
        this.providerName = providerName.ToLowerInvariant();
        this.options = options;
    }

    public string ProviderName => providerName;

    public async Task<FileStorageResult> SaveAsync(
        Stream content,
        string originalName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var (storedName, storagePath) = FileStorageNameHelper.CreateStorageNames(originalName);
        var request = new PutObjectRequest
        {
            BucketName = GetBucket(),
            Key = storagePath,
            InputStream = content,
            ContentType = string.IsNullOrWhiteSpace(contentType)
                ? "application/octet-stream"
                : contentType,
            AutoCloseStream = false
        };
        await GetClient().PutObjectAsync(request, cancellationToken);
        return new FileStorageResult(ProviderName, storagePath, storedName);
    }

    public async Task<Stream> OpenReadAsync(
        string storagePath,
        CancellationToken cancellationToken = default)
    {
        using var response = await GetClient().GetObjectAsync(
            GetBucket(),
            NormalizeKey(storagePath),
            cancellationToken);
        var result = new MemoryStream();
        await response.ResponseStream.CopyToAsync(result, cancellationToken);
        result.Position = 0;
        return result;
    }

    public async Task<bool> ExistsAsync(
        string storagePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await GetClient().GetObjectMetadataAsync(
                GetBucket(),
                NormalizeKey(storagePath),
                cancellationToken);
            return true;
        }
        catch (AmazonS3Exception exception) when (
            exception.StatusCode == System.Net.HttpStatusCode.NotFound ||
            string.Equals(exception.ErrorCode, "NoSuchKey", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(exception.ErrorCode, "NotFound", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
    }

    public async Task DeleteAsync(
        string storagePath,
        CancellationToken cancellationToken = default)
    {
        await GetClient().DeleteObjectAsync(
            GetBucket(),
            NormalizeKey(storagePath),
            cancellationToken);
    }

    public void Dispose()
    {
        client?.Dispose();
    }

    private AmazonS3Client GetClient()
    {
        return client ??= CreateClient();
    }

    private AmazonS3Client CreateClient()
    {
        ValidateOptions();
        AWSCredentials credentials = string.IsNullOrWhiteSpace(options.SessionToken)
            ? new BasicAWSCredentials(options.AccessKey, options.SecretKey)
            : new SessionAWSCredentials(options.AccessKey, options.SecretKey, options.SessionToken);
        var endpoint = options.ResolveEndpoint(providerName);
        var configuration = new AmazonS3Config
        {
            ForcePathStyle = options.ForcePathStyle,
            AuthenticationRegion = options.Region
        };
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            configuration.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);
        }
        else
        {
            configuration.ServiceURL = endpoint;
        }

        return new AmazonS3Client(credentials, configuration);
    }

    private void ValidateOptions()
    {
        if (string.IsNullOrWhiteSpace(options.AccessKey) ||
            string.IsNullOrWhiteSpace(options.SecretKey) ||
            string.IsNullOrWhiteSpace(options.Bucket) ||
            string.IsNullOrWhiteSpace(options.Region))
        {
            throw new InvalidOperationException(
                $"{ProviderName.ToUpperInvariant()} storage requires AccessKey, SecretKey, Bucket and Region.");
        }

        if (!providerName.Equals("s3", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(options.ResolveEndpoint(providerName)))
        {
            throw new InvalidOperationException($"{ProviderName.ToUpperInvariant()} storage endpoint is not configured.");
        }
    }

    private string GetBucket()
    {
        ValidateOptions();
        return options.Bucket.Trim();
    }

    private static string NormalizeKey(string storagePath)
    {
        var key = storagePath.Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrWhiteSpace(key) || key.Split('/').Any(segment => segment == ".."))
        {
            throw new InvalidOperationException("Invalid storage path.");
        }

        return key;
    }
}
