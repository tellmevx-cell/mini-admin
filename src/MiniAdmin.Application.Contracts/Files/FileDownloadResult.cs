namespace MiniAdmin.Application.Contracts.Files;

public sealed record FileDownloadResult(
    string OriginalName,
    string ContentType,
    Stream Content);
