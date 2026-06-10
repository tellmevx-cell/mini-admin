namespace MiniAdmin.Application.Contracts.Notices;

public sealed record NoticeListQuery(
    int Page = 1,
    int PageSize = 20,
    string? Title = null,
    string? Type = null,
    bool? IsPublished = null);
