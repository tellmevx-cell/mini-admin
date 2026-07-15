using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace MiniAdmin.Api.OpenPlatform;

public static class OpenApiSignature
{
    public static string BuildCanonicalRequest(
        string method,
        string path,
        IEnumerable<KeyValuePair<string, string>> query,
        string bodySha256,
        string timestamp,
        string nonce)
    {
        var canonicalQuery = string.Join(
            "&",
            query.Select(pair => new KeyValuePair<string, string>(
                    Escape(pair.Key),
                    Escape(pair.Value)))
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ThenBy(pair => pair.Value, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key}={pair.Value}"));
        return string.Join(
            '\n',
            method.ToUpperInvariant(),
            string.IsNullOrEmpty(path) ? "/" : path,
            canonicalQuery,
            bodySha256.ToLowerInvariant(),
            timestamp,
            nonce);
    }

    public static string Compute(string appSecret, string canonicalRequest)
    {
        var key = Encoding.UTF8.GetBytes(appSecret);
        var payload = Encoding.UTF8.GetBytes(canonicalRequest);
        return Convert.ToHexString(HMACSHA256.HashData(key, payload)).ToLowerInvariant();
    }

    public static IEnumerable<KeyValuePair<string, string>> FlattenQuery(IQueryCollection query)
    {
        return query.SelectMany(pair => pair.Value.Select(value =>
            new KeyValuePair<string, string>(pair.Key, value ?? string.Empty)));
    }

    private static string Escape(string value)
    {
        return Uri.EscapeDataString(value).Replace("%7E", "~", StringComparison.OrdinalIgnoreCase);
    }
}
