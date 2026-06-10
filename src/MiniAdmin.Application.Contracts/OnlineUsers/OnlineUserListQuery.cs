namespace MiniAdmin.Application.Contracts.OnlineUsers;

public sealed record OnlineUserListQuery(
    int Page = 1,
    int PageSize = 20,
    string? UserName = null,
    string? CurrentUserName = null);
