namespace MiniAdmin.Application.Contracts.Dictionaries;

public sealed record DictionaryItemDto(
    string Id,
    string TypeId,
    string Label,
    string Value,
    string? Color,
    int Order,
    bool IsEnabled);
