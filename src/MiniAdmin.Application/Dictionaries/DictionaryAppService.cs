using MiniAdmin.Application.Contracts.Dictionaries;

namespace MiniAdmin.Application.Dictionaries;

public sealed class DictionaryAppService(IDictionaryRepository dictionaryRepository) : IDictionaryAppService
{
    public Task<IReadOnlyList<DictionaryTypeDto>> GetListAsync(CancellationToken cancellationToken = default)
    {
        return dictionaryRepository.GetListAsync(cancellationToken);
    }

    public Task<DictionaryTypeDto> CreateTypeAsync(
        SaveDictionaryTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        return dictionaryRepository.CreateTypeAsync(request, cancellationToken);
    }

    public Task<DictionaryTypeDto?> UpdateTypeAsync(
        Guid id,
        SaveDictionaryTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        return dictionaryRepository.UpdateTypeAsync(id, request, cancellationToken);
    }

    public Task<bool> DeleteTypeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dictionaryRepository.DeleteTypeAsync(id, cancellationToken);
    }

    public Task<DictionaryItemDto> CreateItemAsync(
        SaveDictionaryItemRequest request,
        CancellationToken cancellationToken = default)
    {
        return dictionaryRepository.CreateItemAsync(request, cancellationToken);
    }

    public Task<DictionaryItemDto?> UpdateItemAsync(
        Guid id,
        SaveDictionaryItemRequest request,
        CancellationToken cancellationToken = default)
    {
        return dictionaryRepository.UpdateItemAsync(id, request, cancellationToken);
    }

    public Task<bool> DeleteItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dictionaryRepository.DeleteItemAsync(id, cancellationToken);
    }
}
