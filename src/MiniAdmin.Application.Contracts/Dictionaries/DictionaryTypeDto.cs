namespace MiniAdmin.Application.Contracts.Dictionaries;

public sealed record DictionaryTypeDto(
    string Id,
    string Code,
    string Name,
    int Order,
    bool IsEnabled,
    IReadOnlyList<DictionaryItemDto> Items);
