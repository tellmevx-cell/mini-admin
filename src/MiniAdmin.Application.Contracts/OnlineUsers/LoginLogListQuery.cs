namespace MiniAdmin.Application.Contracts.OnlineUsers;

public sealed record LoginLogListQuery(
    int Page = 1,
    int PageSize = 20,
    string? UserName = null,
    bool? IsSuccess = null,
    DateTimeOffset? StartCreatedAt = null,
    DateTimeOffset? EndCreatedAt = null,
    string? CurrentUserName = null);
