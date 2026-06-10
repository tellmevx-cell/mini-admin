namespace MiniAdmin.Application.Contracts.Parameters;

public sealed record SystemParameterListQuery(
    int Page = 1,
    int PageSize = 20,
    string? Key = null,
    string? Name = null,
    string? Group = null);
