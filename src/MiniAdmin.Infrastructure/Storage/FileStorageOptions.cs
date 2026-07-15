namespace MiniAdmin.Infrastructure.Storage;

public sealed class FileStorageOptions
{
    public string Provider { get; set; } = "Local";

    public LocalStorageOptions Local { get; set; } = new();

    public MinioStorageOptions Minio { get; set; } = new();

    public S3StorageOptions S3 { get; set; } = new();

    public OssStorageOptions Oss { get; set; } = new();

    public CosStorageOptions Cos { get; set; } = new();
}

public sealed class LocalStorageOptions
{
    public string RootPath { get; set; } = "storage/uploads";
}

public class S3CompatibleStorageOptions
{
    public string Endpoint { get; set; } = string.Empty;

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public string SessionToken { get; set; } = string.Empty;

    public string Bucket { get; set; } = "mini-admin";

    public string Region { get; set; } = "us-east-1";

    public bool UseSsl { get; set; }

    public bool ForcePathStyle { get; set; }

    public string ResolveEndpoint(string providerName)
    {
        if (!string.IsNullOrWhiteSpace(Endpoint))
        {
            return NormalizeEndpoint(Endpoint, UseSsl);
        }

        return providerName.ToLowerInvariant() switch
        {
            "s3" => string.Empty,
            "oss" when !string.IsNullOrWhiteSpace(Region) => $"https://oss-{Region}.aliyuncs.com",
            "cos" when !string.IsNullOrWhiteSpace(Region) => $"https://cos.{Region}.myqcloud.com",
            _ => string.Empty
        };
    }

    private static string NormalizeEndpoint(string endpoint, bool useSsl)
    {
        var value = endpoint.Trim().TrimEnd('/');
        return value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"{(useSsl ? "https" : "http")}://{value}";
    }
}

public sealed class MinioStorageOptions : S3CompatibleStorageOptions
{
    public MinioStorageOptions()
    {
        ForcePathStyle = true;
    }
}

public sealed class S3StorageOptions : S3CompatibleStorageOptions;

public sealed class OssStorageOptions : S3CompatibleStorageOptions;

public sealed class CosStorageOptions : S3CompatibleStorageOptions;
