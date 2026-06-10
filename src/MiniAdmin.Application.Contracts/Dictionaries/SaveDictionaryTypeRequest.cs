namespace MiniAdmin.Application.Contracts.Dictionaries;

public sealed record SaveDictionaryTypeRequest(
    string Code,
    string Name,
    int Order,
    bool IsEnabled);
