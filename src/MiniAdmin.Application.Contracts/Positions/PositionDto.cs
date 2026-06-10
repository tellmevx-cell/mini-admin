namespace MiniAdmin.Application.Contracts.Positions;

public sealed record PositionDto(
    string Id,
    string Code,
    string Name,
    int Order,
    string? Remark,
    bool IsEnabled);
