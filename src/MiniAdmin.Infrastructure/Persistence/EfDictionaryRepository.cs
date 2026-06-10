using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.Dictionaries;
using MiniAdmin.Domain.Entities;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class EfDictionaryRepository(MiniAdminDbContext dbContext) : IDictionaryRepository
{
    public async Task<IReadOnlyList<DictionaryTypeDto>> GetListAsync(
        CancellationToken cancellationToken = default)
    {
        var types = await dbContext.DictionaryTypes
            .AsNoTracking()
            .Include(x => x.Items)
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Code)
            .ToArrayAsync(cancellationToken);

        return types.Select(ToTypeDto).ToArray();
    }

    public async Task<DictionaryTypeDto> CreateTypeAsync(
        SaveDictionaryTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        var type = new DictionaryType
        {
            Id = Guid.NewGuid(),
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            Order = request.Order,
            IsEnabled = request.IsEnabled
        };

        dbContext.DictionaryTypes.Add(type);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToTypeDto(type);
    }

    public async Task<DictionaryTypeDto?> UpdateTypeAsync(
        Guid id,
        SaveDictionaryTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        var type = await dbContext.DictionaryTypes
            .Include(x => x.Items)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (type is null)
        {
            return null;
        }

        type.Code = request.Code.Trim();
        type.Name = request.Name.Trim();
        type.Order = request.Order;
        type.IsEnabled = request.IsEnabled;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToTypeDto(type);
    }

    public async Task<bool> DeleteTypeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var hasItems = await dbContext.DictionaryItems.AnyAsync(x => x.TypeId == id, cancellationToken);
        if (hasItems)
        {
            return false;
        }

        var type = await dbContext.DictionaryTypes.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (type is null)
        {
            return false;
        }

        dbContext.DictionaryTypes.Remove(type);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<DictionaryItemDto> CreateItemAsync(
        SaveDictionaryItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var item = new DictionaryItem
        {
            Id = Guid.NewGuid(),
            TypeId = request.TypeId,
            Label = request.Label.Trim(),
            Value = request.Value.Trim(),
            Color = NormalizeOptional(request.Color),
            Order = request.Order,
            IsEnabled = request.IsEnabled
        };

        dbContext.DictionaryItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToItemDto(item);
    }

    public async Task<DictionaryItemDto?> UpdateItemAsync(
        Guid id,
        SaveDictionaryItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var item = await dbContext.DictionaryItems.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return null;
        }

        item.TypeId = request.TypeId;
        item.Label = request.Label.Trim();
        item.Value = request.Value.Trim();
        item.Color = NormalizeOptional(request.Color);
        item.Order = request.Order;
        item.IsEnabled = request.IsEnabled;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToItemDto(item);
    }

    public async Task<bool> DeleteItemAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await dbContext.DictionaryItems.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return false;
        }

        dbContext.DictionaryItems.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static DictionaryTypeDto ToTypeDto(DictionaryType type)
    {
        var items = type.Items
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Value)
            .Select(ToItemDto)
            .ToArray();

        return new DictionaryTypeDto(
            type.Id.ToString(),
            type.Code,
            type.Name,
            type.Order,
            type.IsEnabled,
            items);
    }

    private static DictionaryItemDto ToItemDto(DictionaryItem item)
    {
        return new DictionaryItemDto(
            item.Id.ToString(),
            item.TypeId.ToString(),
            item.Label,
            item.Value,
            item.Color,
            item.Order,
            item.IsEnabled);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
