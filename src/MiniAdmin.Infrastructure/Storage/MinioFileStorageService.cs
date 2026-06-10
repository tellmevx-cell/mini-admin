using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using MiniAdmin.Application.Contracts.Files;

namespace MiniAdmin.Infrastructure.Storage;

internal sealed class MinioFileStorageService(IOptions<FileStorageOptions> options) : IFileStorageProvider
{
    private static readonly HttpClient HttpClient = new();

    public string ProviderName => "minio";

    public async Task<FileStorageResult> SaveAsync(
        Stream content,
        string originalName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var (storedName, storagePath) = FileStorageNameHelper.CreateStorageNames(originalName);
        using var contentStream = new MemoryStream();
        await content.CopyToAsync(contentStream, cancellationToken);
        var bytes = contentStream.ToArray();
        using var request = CreateRequest(HttpMethod.Put, storagePath, bytes);
        request.Content = new ByteArrayContent(bytes);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);

        using var response = await HttpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return new FileStorageResult(ProviderName, storagePath, storedName);
    }

    public async Task<Stream> OpenReadAsync(
        string storagePath,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, storagePath, []);
        var response = await HttpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStreamAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(
        string storagePath,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Head, storagePath, []);
        using var response = await HttpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();

        return true;
    }

    public async Task DeleteAsync(
        string storagePath,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Delete, storagePath, []);
        using var response = await HttpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string storagePath, byte[] payload)
    {
        var minio = options.Value.Minio;
        if (string.IsNullOrWhiteSpace(minio.Endpoint) ||
            string.IsNullOrWhiteSpace(minio.AccessKey) ||
            string.IsNullOrWhiteSpace(minio.SecretKey) ||
            string.IsNullOrWhiteSpace(minio.Bucket))
        {
            throw new InvalidOperationException("MinIO storage is not configured.");
        }

        var endpoint = minio.Endpoint.TrimEnd('/');
        if (!endpoint.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            endpoint = $"{(minio.UseSsl ? "https" : "http")}://{endpoint}";
        }

        var escapedPath = string.Join(
            '/',
            storagePath.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));
        var uri = new Uri($"{endpoint}/{minio.Bucket}/{escapedPath}");
        var now = DateTimeOffset.UtcNow;
        var amzDate = now.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var dateStamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var payloadHash = ToHex(SHA256.HashData(payload));
        var host = uri.Authority;
        var canonicalUri = uri.AbsolutePath;
        var canonicalHeaders =
            $"host:{host}\n" +
            $"x-amz-content-sha256:{payloadHash}\n" +
            $"x-amz-date:{amzDate}\n";
        const string signedHeaders = "host;x-amz-content-sha256;x-amz-date";
        var canonicalRequest = string.Join('\n', new[]
        {
            method.Method,
            canonicalUri,
            string.Empty,
            canonicalHeaders,
            signedHeaders,
            payloadHash
        });
        var credentialScope = $"{dateStamp}/{minio.Region}/s3/aws4_request";
        var stringToSign = string.Join('\n', new[]
        {
            "AWS4-HMAC-SHA256",
            amzDate,
            credentialScope,
            ToHex(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)))
        });
        var signingKey = GetSignatureKey(minio.SecretKey, dateStamp, minio.Region, "s3");
        var signature = ToHex(HmacSha256(signingKey, stringToSign));
        var authorization =
            $"AWS4-HMAC-SHA256 Credential={minio.AccessKey}/{credentialScope}, SignedHeaders={signedHeaders}, Signature={signature}";

        var request = new HttpRequestMessage(method, uri);
        request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        request.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);
        request.Headers.Authorization = AuthenticationHeaderValue.Parse(authorization);

        return request;
    }

    private static byte[] GetSignatureKey(string key, string dateStamp, string regionName, string serviceName)
    {
        var kDate = HmacSha256(Encoding.UTF8.GetBytes($"AWS4{key}"), dateStamp);
        var kRegion = HmacSha256(kDate, regionName);
        var kService = HmacSha256(kRegion, serviceName);

        return HmacSha256(kService, "aws4_request");
    }

    private static byte[] HmacSha256(byte[] key, string data)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    private static string ToHex(byte[] bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
