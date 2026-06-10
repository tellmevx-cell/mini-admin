namespace MiniAdmin.Application.Contracts.Parameters;

public sealed record SaveSystemParameterRequest(
    string Key,
    string Name,
    string Value,
    string Group,
    string? Remark,
    int Order,
    bool IsEnabled);
