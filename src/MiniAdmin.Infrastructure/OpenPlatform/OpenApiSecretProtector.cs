using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace MiniAdmin.Infrastructure.OpenPlatform;

public interface IOpenApiSecretProtector
{
    string Protect(string value);

    string Unprotect(string value);
}

public sealed class OpenApiSecretProtector : IOpenApiSecretProtector
{
    private readonly byte[] _key;

    public OpenApiSecretProtector(IConfiguration configuration)
    {
        var material = configuration["OpenPlatform:CredentialEncryptionKey"];
        if (string.IsNullOrWhiteSpace(material))
        {
            material = configuration["OpenPlatform:EncryptionKey"];
        }

        if (string.IsNullOrWhiteSpace(material))
        {
            material = configuration["OpenPlatform:SigningKey"];
        }

        if (string.IsNullOrWhiteSpace(material))
        {
            material = configuration["Jwt:SigningKey"];
        }

        if (string.IsNullOrWhiteSpace(material))
        {
            throw new InvalidOperationException("未配置 OpenAPI 凭证加密密钥。");
        }

        _key = SHA256.HashData(Encoding.UTF8.GetBytes($"miniadmin:openapi:{material}"));
    }

    public string Protect(string value)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plaintext = Encoding.UTF8.GetBytes(value);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(_key, tag.Length);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);
        var payload = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, payload, nonce.Length + tag.Length, ciphertext.Length);
        return Convert.ToBase64String(payload);
    }

    public string Unprotect(string value)
    {
        var payload = Convert.FromBase64String(value);
        if (payload.Length < 29)
        {
            throw new CryptographicException("OpenAPI 凭证密文无效。");
        }

        var nonce = payload.AsSpan(0, 12);
        var tag = payload.AsSpan(12, 16);
        var ciphertext = payload.AsSpan(28);
        var plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(_key, tag.Length);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return Encoding.UTF8.GetString(plaintext);
    }
}
