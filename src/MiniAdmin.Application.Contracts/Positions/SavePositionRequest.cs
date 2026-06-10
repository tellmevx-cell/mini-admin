namespace MiniAdmin.Application.Contracts.Positions;

public sealed record SavePositionRequest(
    string Code,
    string Name,
    int Order,
    string? Remark,
    bool IsEnabled);
