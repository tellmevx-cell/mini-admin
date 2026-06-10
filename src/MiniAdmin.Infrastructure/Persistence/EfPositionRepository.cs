using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.MultiTenancy;
using MiniAdmin.Application.Contracts.Positions;
using MiniAdmin.Domain.Entities;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class EfPositionRepository(
    MiniAdminDbContext dbContext,
    ICurrentTenant currentTenant) : IPositionRepository
{
    public async Task<PageResult<PositionDto>> GetListAsync(
        PositionListQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var positionsQuery = ApplyTenantScope(dbContext.Positions.AsNoTracking());

        if (!string.IsNullOrWhiteSpace(query.Code))
        {
            positionsQuery = positionsQuery.Where(x => x.Code.Contains(query.Code));
        }

        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            positionsQuery = positionsQuery.Where(x => x.Name.Contains(query.Name));
        }

        var total = await positionsQuery.CountAsync(cancellationToken);
        var items = await positionsQuery
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => ToDto(x))
            .ToArrayAsync(cancellationToken);

        return new PageResult<PositionDto>(items, total);
    }

    public async Task<IReadOnlyList<PositionDto>> GetExportListAsync(
        PositionListQuery query,
        int limit = 10000,
        CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(limit, 1, 10000);
        var positionsQuery = ApplyTenantScope(dbContext.Positions.AsNoTracking());

        if (!string.IsNullOrWhiteSpace(query.Code))
        {
            positionsQuery = positionsQuery.Where(x => x.Code.Contains(query.Code));
        }

        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            positionsQuery = positionsQuery.Where(x => x.Name.Contains(query.Name));
        }

        return await positionsQuery
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Code)
            .Take(take)
            .Select(x => ToDto(x))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<PositionImportResultDto> ValidateImportAsync(
        IReadOnlyList<PositionImportRowDto> rows,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateImportRowsAsync(rows, cancellationToken);
        return new PositionImportResultDto(validation.ValidRows.Count, validation.Errors);
    }

    public async Task<PositionImportResultDto> ImportAsync(
        IReadOnlyList<PositionImportRowDto> rows,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateImportRowsAsync(rows, cancellationToken);
        if (validation.Errors.Count > 0)
        {
            return new PositionImportResultDto(0, validation.Errors);
        }

        var positions = validation.ValidRows
            .Select(row => new Position
            {
                Id = Guid.NewGuid(),
                TenantId = currentTenant.TenantId,
                Code = row.Code.Trim(),
                Name = row.Name.Trim(),
                Order = row.Order,
                Remark = NormalizeOptional(row.Remark),
                IsEnabled = row.IsEnabled
            })
            .ToArray();

        if (positions.Length > 0)
        {
            dbContext.Positions.AddRange(positions);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new PositionImportResultDto(positions.Length, []);
    }

    public async Task<PositionDto> CreateAsync(
        SavePositionRequest request,
        CancellationToken cancellationToken = default)
    {
        var position = new Position
        {
            Id = Guid.NewGuid(),
            TenantId = currentTenant.TenantId
        };

        ApplyRequest(position, request);
        dbContext.Positions.Add(position);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(position);
    }

    public async Task<PositionDto?> UpdateAsync(
        Guid id,
        SavePositionRequest request,
        CancellationToken cancellationToken = default)
    {
        var position = await ApplyTenantScope(dbContext.Positions)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (position is null)
        {
            return null;
        }

        ApplyRequest(position, request);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(position);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var isBoundToUser = await dbContext.Users
            .AnyAsync(x => x.PositionId == id &&
                           (currentTenant.IsPlatform || x.TenantId == currentTenant.TenantId),
                cancellationToken);
        if (isBoundToUser)
        {
            return false;
        }

        var position = await ApplyTenantScope(dbContext.Positions)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (position is null)
        {
            return false;
        }

        dbContext.Positions.Remove(position);
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task<PositionImportValidationResult> ValidateImportRowsAsync(
        IReadOnlyList<PositionImportRowDto> rows,
        CancellationToken cancellationToken)
    {
        var errors = new List<PositionImportErrorDto>();
        if (rows.Count == 0)
        {
            return new PositionImportValidationResult([], errors);
        }

        var codes = rows
            .Select(x => x.Code)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var existingCodes = (await ApplyTenantScope(dbContext.Positions.AsNoTracking())
            .Select(x => x.Code)
            .ToArrayAsync(cancellationToken))
            .Where(x => codes.Contains(x, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        var existingCodeSet = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);
        var validRows = new List<PositionImportRowDto>();

        foreach (var row in rows)
        {
            if (existingCodeSet.Contains(row.Code))
            {
                errors.Add(new PositionImportErrorDto(row.RowNumber, row.Code, "岗位编码已存在."));
                continue;
            }

            validRows.Add(row);
        }

        return new PositionImportValidationResult(validRows, errors);
    }

    private static void ApplyRequest(Position position, SavePositionRequest request)
    {
        position.Code = request.Code.Trim();
        position.Name = request.Name.Trim();
        position.Order = request.Order;
        position.Remark = NormalizeOptional(request.Remark);
        position.IsEnabled = request.IsEnabled;
    }

    private IQueryable<Position> ApplyTenantScope(IQueryable<Position> positionsQuery)
    {
        return currentTenant.IsTenant
            ? positionsQuery.Where(x => x.TenantId == currentTenant.TenantId)
            : positionsQuery.Where(x => x.TenantId == null);
    }

    private static PositionDto ToDto(Position position)
    {
        return new PositionDto(
            position.Id.ToString(),
            position.Code,
            position.Name,
            position.Order,
            position.Remark,
            position.IsEnabled);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private sealed record PositionImportValidationResult(
        IReadOnlyList<PositionImportRowDto> ValidRows,
        IReadOnlyList<PositionImportErrorDto> Errors);
}
