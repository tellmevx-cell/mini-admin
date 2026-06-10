namespace MiniAdmin.Infrastructure.Storage;

internal static class FileStorageNameHelper
{
    public static (string StoredName, string StoragePath) CreateStorageNames(string originalName)
    {
        var extension = Path.GetExtension(Path.GetFileName(originalName));
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var today = DateTimeOffset.UtcNow;
        var storagePath = string.Join(
            '/',
            today.Year.ToString("0000"),
            today.Month.ToString("00"),
            today.Day.ToString("00"),
            storedName);

        return (storedName, storagePath);
    }
}
