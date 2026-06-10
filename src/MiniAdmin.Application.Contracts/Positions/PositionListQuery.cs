namespace MiniAdmin.Application.Contracts.Positions;

public sealed record PositionListQuery(
    int Page = 1,
    int PageSize = 20,
    string? Code = null,
    string? Name = null);
