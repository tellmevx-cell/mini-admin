using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.AuditLogs;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.DataScopes;
using MiniAdmin.Domain.Entities;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class EfAuditLogRepository(
    MiniAdminDbContext dbContext,
    IDataScopeProvider dataScopeProvider) : IAuditLogRepository
{
    public async Task<PageResult<AuditLogDto>> GetListAsync(
        AuditLogListQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var logsQuery = await ApplyDataScopeAsync(
            dbContext.AuditLogs.AsNoTracking(),
            query.CurrentUserName,
            cancellationToken);
        logsQuery = ApplyFilters(logsQuery, query);

        var total = await logsQuery.CountAsync(cancellationToken);
        var auditLogs = await logsQuery
            .Include(x => x.EntityChanges)
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);
        var items = auditLogs.Select(ToDto).ToArray();

        return new PageResult<AuditLogDto>(items, total);
    }

    public async Task<IReadOnlyList<AuditLogDto>> GetExportListAsync(
        AuditLogListQuery query,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var exportLimit = Math.Clamp(limit, 1, 5000);
        var logsQuery = await ApplyDataScopeAsync(
            dbContext.AuditLogs.AsNoTracking(),
            query.CurrentUserName,
            cancellationToken);

        var auditLogs = await ApplyFilters(logsQuery, query)
            .Include(x => x.EntityChanges)
            .OrderByDescending(x => x.CreatedAt)
            .Take(exportLimit)
            .ToArrayAsync(cancellationToken);

        return auditLogs.Select(ToDto).ToArray();
    }

    public async Task CreateAsync(
        SaveAuditLogRequest request,
        CancellationToken cancellationToken = default)
    {
        var auditLogId = Guid.NewGuid();
        var entityChanges = request.EntityChanges ?? Array.Empty<CapturedAuditEntityChange>();

        dbContext.ChangeTracker.Clear();
        dbContext.AuditLogs.Add(new AuditLog
        {
            Id = auditLogId,
            UserId = NormalizeOptional(request.UserId),
            UserName = NormalizeOptional(request.UserName),
            Method = request.Method,
            Path = request.Path,
            QueryString = NormalizeOptional(request.QueryString),
            Module = request.Module,
            Action = request.Action,
            ResourceId = NormalizeOptional(request.ResourceId),
            StatusCode = request.StatusCode,
            IsSuccess = request.IsSuccess,
            ElapsedMilliseconds = request.ElapsedMilliseconds,
            IpAddress = NormalizeOptional(request.IpAddress),
            UserAgent = NormalizeOptional(request.UserAgent),
            RequestBody = request.RequestBody,
            ErrorMessage = NormalizeOptional(request.ErrorMessage),
            CreatedAt = request.CreatedAt,
            EntityChanges = entityChanges.Select(change => new AuditEntityChange
            {
                Id = Guid.NewGuid(),
                AuditLogId = auditLogId,
                EntityName = change.EntityName,
                EntityId = change.EntityId,
                OperationType = change.OperationType,
                BeforeJson = NormalizeOptional(change.BeforeJson),
                AfterJson = NormalizeOptional(change.AfterJson),
                DiffJson = change.DiffJson,
                CreatedAt = change.CreatedAt
            }).ToArray()
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static AuditLogDto ToDto(AuditLog auditLog)
    {
        return new AuditLogDto(
            auditLog.Id.ToString(),
            auditLog.UserId,
            auditLog.UserName,
            auditLog.Method,
            auditLog.Path,
            auditLog.QueryString,
            auditLog.Module,
            auditLog.Action,
            auditLog.ResourceId,
            auditLog.StatusCode,
            auditLog.IsSuccess,
            auditLog.ElapsedMilliseconds,
            auditLog.IpAddress,
            auditLog.UserAgent,
            auditLog.RequestBody,
            auditLog.ErrorMessage,
            auditLog.CreatedAt,
            auditLog.EntityChanges
                .OrderBy(x => x.CreatedAt)
                .Select(ToEntityChangeDto)
                .ToArray());
    }

    private static AuditEntityChangeDto ToEntityChangeDto(AuditEntityChange change)
    {
        return new AuditEntityChangeDto(
            change.Id.ToString(),
            change.AuditLogId.ToString(),
            change.EntityName,
            change.EntityId,
            change.OperationType,
            change.BeforeJson,
            change.AfterJson,
            change.DiffJson,
            change.CreatedAt);
    }

    private static IQueryable<AuditLog> ApplyFilters(
        IQueryable<AuditLog> logsQuery,
        AuditLogListQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.UserName))
        {
            logsQuery = logsQuery.Where(x => x.UserName != null && x.UserName.Contains(query.UserName));
        }

        if (!string.IsNullOrWhiteSpace(query.Method))
        {
            logsQuery = logsQuery.Where(x => x.Method == query.Method);
        }

        if (!string.IsNullOrWhiteSpace(query.Path))
        {
            logsQuery = logsQuery.Where(x => x.Path == query.Path);
        }

        if (!string.IsNullOrWhiteSpace(query.Module))
        {
            logsQuery = logsQuery.Where(x => x.Module == query.Module);
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            logsQuery = logsQuery.Where(x => x.Action == query.Action);
        }

        if (query.IsSuccess.HasValue)
        {
            logsQuery = logsQuery.Where(x => x.IsSuccess == query.IsSuccess);
        }

        if (query.StartCreatedAt.HasValue)
        {
            logsQuery = logsQuery.Where(x => x.CreatedAt >= query.StartCreatedAt);
        }

        if (query.EndCreatedAt.HasValue)
        {
            logsQuery = logsQuery.Where(x => x.CreatedAt <= query.EndCreatedAt);
        }

        return logsQuery;
    }

    private async Task<IQueryable<AuditLog>> ApplyDataScopeAsync(
        IQueryable<AuditLog> logsQuery,
        string? currentUserName,
        CancellationToken cancellationToken)
    {
        var dataScope = await dataScopeProvider.GetAsync(currentUserName, cancellationToken);
        if (dataScope.IsUnrestricted)
        {
            return logsQuery;
        }

        if (dataScope.IsDenied || string.IsNullOrWhiteSpace(dataScope.UserName))
        {
            return logsQuery.Where(x => false);
        }

        if (dataScope.Level is DataScopeLevel.DepartmentAndChildren
            or DataScopeLevel.Department
            or DataScopeLevel.CustomDepartments
            or DataScopeLevel.Mixed)
        {
            return logsQuery.Where(log =>
                log.UserName != null &&
                dbContext.Users.Any(user =>
                    user.UserName == log.UserName &&
                    user.DepartmentId.HasValue &&
                    dataScope.DepartmentIds.Contains(user.DepartmentId.Value)));
        }

        return logsQuery.Where(x => x.UserName == dataScope.UserName);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
