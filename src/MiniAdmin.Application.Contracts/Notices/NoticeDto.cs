namespace MiniAdmin.Application.Contracts.Notices;

public sealed record NoticeDto(
    string Id,
    string Title,
    string Type,
    string Content,
    bool IsPublished,
    DateTimeOffset? PublishedAt,
    DateTimeOffset CreatedAt);
