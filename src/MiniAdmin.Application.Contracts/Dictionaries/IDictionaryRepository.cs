namespace MiniAdmin.Application.Contracts.Dictionaries;

public interface IDictionaryRepository
{
    Task<IReadOnlyList<DictionaryTypeDto>> GetListAsync(CancellationToken cancellationToken = default);

    Task<DictionaryTypeDto> CreateTypeAsync(
        SaveDictionaryTypeRequest request,
        CancellationToken cancellationToken = default);

    Task<DictionaryTypeDto?> UpdateTypeAsync(
        Guid id,
        SaveDictionaryTypeRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteTypeAsync(Guid id, CancellationToken cancellationToken = default);

    Task<DictionaryItemDto> CreateItemAsync(
        SaveDictionaryItemRequest request,
        CancellationToken cancellationToken = default);

    Task<DictionaryItemDto?> UpdateItemAsync(
        Guid id,
        SaveDictionaryItemRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteItemAsync(Guid id, CancellationToken cancellationToken = default);
}
