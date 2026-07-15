using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.Parameters;
using MiniAdmin.Domain.Entities;
using MiniAdmin.Platform.Caching;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class EfSystemParameterRepository(
    MiniAdminDbContext dbContext,
    IPlatformCache platformCache) : ISystemParameterRepository
{
    public async Task<PageResult<SystemParameterDto>> GetListAsync(
        SystemParameterListQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var parametersQuery = dbContext.SystemParameters.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Key))
        {
            parametersQuery = parametersQuery.Where(x => x.Key.Contains(query.Key));
        }

        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            parametersQuery = parametersQuery.Where(x => x.Name.Contains(query.Name));
        }

        if (!string.IsNullOrWhiteSpace(query.Group))
        {
            parametersQuery = parametersQuery.Where(x => x.Group.Contains(query.Group));
        }

        var total = await parametersQuery.CountAsync(cancellationToken);
        var items = await parametersQuery
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Key)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => ToDto(x))
            .ToArrayAsync(cancellationToken);

        return new PageResult<SystemParameterDto>(items, total);
    }

    public async Task<string?> GetValueByKeyAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        var normalizedKey = key.Trim();
        return await platformCache.GetOrCreateAsync(
            "system-parameters",
            normalizedKey,
            tenantId: null,
            [ParameterTag(normalizedKey)],
            async token => await dbContext.SystemParameters
                .AsNoTracking()
                .Where(parameter => parameter.Key == normalizedKey && parameter.IsEnabled)
                .Select(parameter => parameter.Value)
                .SingleOrDefaultAsync(token),
            cancellationToken: cancellationToken);
    }

    public async Task<SystemParameterDto> UpsertValueByKeyAsync(
        string key,
        string name,
        string value,
        string group,
        string? remark,
        int order,
        bool isEnabled,
        CancellationToken cancellationToken = default)
    {
        var normalizedKey = key.Trim();
        var parameter = await dbContext.SystemParameters
            .SingleOrDefaultAsync(x => x.Key == normalizedKey, cancellationToken);
        if (parameter is null)
        {
            parameter = new SystemParameter
            {
                Id = Guid.NewGuid(),
                Key = normalizedKey
            };
            dbContext.SystemParameters.Add(parameter);
        }

        parameter.Name = name.Trim();
        parameter.Value = value.Trim();
        parameter.Group = group.Trim();
        parameter.Remark = NormalizeOptional(remark);
        parameter.Order = order;
        parameter.IsEnabled = isEnabled;

        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateParameterAsync(normalizedKey, cancellationToken);

        return ToDto(parameter);
    }

    public async Task<SystemParameterDto> CreateAsync(
        SaveSystemParameterRequest request,
        CancellationToken cancellationToken = default)
    {
        var parameter = new SystemParameter
        {
            Id = Guid.NewGuid()
        };

        ApplyRequest(parameter, request);
        dbContext.SystemParameters.Add(parameter);
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateParameterAsync(parameter.Key, cancellationToken);

        return ToDto(parameter);
    }

    public async Task<SystemParameterDto?> UpdateAsync(
        Guid id,
        SaveSystemParameterRequest request,
        CancellationToken cancellationToken = default)
    {
        var parameter = await dbContext.SystemParameters.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (parameter is null)
        {
            return null;
        }

        var previousKey = parameter.Key;
        ApplyRequest(parameter, request);
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateParameterAsync(previousKey, cancellationToken);
        if (!previousKey.Equals(parameter.Key, StringComparison.OrdinalIgnoreCase))
        {
            await InvalidateParameterAsync(parameter.Key, cancellationToken);
        }

        return ToDto(parameter);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var parameter = await dbContext.SystemParameters.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (parameter is null)
        {
            return false;
        }

        var parameterKey = parameter.Key;
        dbContext.SystemParameters.Remove(parameter);
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidateParameterAsync(parameterKey, cancellationToken);

        return true;
    }

    private static void ApplyRequest(SystemParameter parameter, SaveSystemParameterRequest request)
    {
        parameter.Key = request.Key.Trim();
        parameter.Name = request.Name.Trim();
        parameter.Value = request.Value.Trim();
        parameter.Group = request.Group.Trim();
        parameter.Remark = NormalizeOptional(request.Remark);
        parameter.Order = request.Order;
        parameter.IsEnabled = request.IsEnabled;
    }

    private static SystemParameterDto ToDto(SystemParameter parameter)
    {
        return new SystemParameterDto(
            parameter.Id.ToString(),
            parameter.Key,
            parameter.Name,
            parameter.Value,
            parameter.Group,
            parameter.Remark,
            parameter.Order,
            parameter.IsEnabled);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private Task InvalidateParameterAsync(string key, CancellationToken cancellationToken)
    {
        return platformCache.InvalidateTagsAsync(
            tenantId: null,
            [ParameterTag(key)],
            cancellationToken);
    }

    private static string ParameterTag(string key) =>
        $"system-parameter:{key.Trim().ToLowerInvariant()}";
}
