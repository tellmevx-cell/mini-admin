namespace MiniAdmin.Application.Contracts.Notices;

public sealed record SaveNoticeRequest(
    string Title,
    string Type,
    string Content,
    bool IsPublished);
