namespace MiniAdmin.Application.Contracts.Dictionaries;

public sealed record SaveDictionaryItemRequest(
    Guid TypeId,
    string Label,
    string Value,
    string? Color,
    int Order,
    bool IsEnabled);
