using System.Security.Cryptography;
using System.Text;

namespace MiniAdmin.Platform.Navigation;

public static class PageIdentity
{
    public static Guid ForPage(PageDefinition page)
    {
        return page.Id ?? CreateDeterministicGuid($"page:{page.Key}");
    }

    public static Guid ForPermission(PermissionDefinition permission)
    {
        return permission.Id ?? CreateDeterministicGuid($"permission:{permission.Code}");
    }

    public static Guid CreateDeterministicGuid(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"mini-admin:{value.Trim().ToLowerInvariant()}"));
        Span<byte> bytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }
}
