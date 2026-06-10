namespace MiniAdmin.Infrastructure.Storage;

public sealed class FileStorageOptions
{
    public string Provider { get; set; } = "Local";

    public LocalStorageOptions Local { get; set; } = new();

    public MinioStorageOptions Minio { get; set; } = new();
}

public sealed class LocalStorageOptions
{
    public string RootPath { get; set; } = "storage/uploads";
}

public sealed class MinioStorageOptions
{
    public string Endpoint { get; set; } = string.Empty;

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public string Bucket { get; set; } = "mini-admin";

    public string Region { get; set; } = "us-east-1";

    public bool UseSsl { get; set; }
}
