using System.Collections.Concurrent;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MiniAdmin.Application.Contracts.MultiTenancy;
using MiniAdmin.Application.Contracts.TenantResourceQuotas;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class TenantResourceQuotaService(
    MiniAdminDbContext dbContext,
    ICurrentTenant currentTenant) : ITenantResourceQuotaService
{
    private const long BytesPerMb = 1024L * 1024L;
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> TenantLocks = new();

    public Task<TenantResourceQuotaSnapshot?> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        return currentTenant.TenantId is Guid tenantId
            ? GetSnapshotAsync(tenantId, cancellationToken)
            : Task.FromResult<TenantResourceQuotaSnapshot?>(null);
    }

    public async Task EnsureCanAddUsersAsync(
        int additionalUsers,
        CancellationToken cancellationToken = default)
    {
        if (additionalUsers <= 0 || currentTenant.TenantId is not Guid tenantId)
        {
            return;
        }

        var snapshot = await GetRequiredSnapshotAsync(tenantId, cancellationToken);
        EnsureUserQuota(snapshot, additionalUsers);
    }

    public async Task EnsureCanAddStorageAsync(
        long additionalBytes,
        CancellationToken cancellationToken = default)
    {
        if (additionalBytes <= 0 || currentTenant.TenantId is not Guid tenantId)
        {
            return;
        }

        var snapshot = await GetRequiredSnapshotAsync(tenantId, cancellationToken);
        EnsureStorageQuota(snapshot, additionalBytes);
    }

    public Task<TResult> ExecuteUserWriteAsync<TResult>(
        int additionalUsers,
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken = default)
    {
        return ExecuteWriteAsync(
            Math.Max(additionalUsers, 0),
            action,
            static (snapshot, requested) => EnsureUserQuota(snapshot, checked((int)requested)),
            cancellationToken);
    }

    public Task<TResult> ExecuteStorageWriteAsync<TResult>(
        long additionalBytes,
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken = default)
    {
        return ExecuteWriteAsync(
            Math.Max(additionalBytes, 0),
            action,
            EnsureStorageQuota,
            cancellationToken);
    }

    private async Task<TResult> ExecuteWriteAsync<TResult>(
        long requested,
        Func<CancellationToken, Task<TResult>> action,
        Action<TenantResourceQuotaSnapshot, long> ensureQuota,
        CancellationToken cancellationToken)
    {
        if (requested == 0 || currentTenant.TenantId is not Guid tenantId)
        {
            return await action(cancellationToken);
        }

        var gate = TenantLocks.GetOrAdd(tenantId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            IDbContextTransaction? ownedTransaction = null;
            try
            {
                if (dbContext.Database.IsRelational() && dbContext.Database.CurrentTransaction is null)
                {
                    ownedTransaction = await dbContext.Database.BeginTransactionAsync(
                        IsolationLevel.ReadCommitted,
                        cancellationToken);
                }

                await LockTenantAsync(tenantId, cancellationToken);
                var snapshot = await GetRequiredSnapshotAsync(tenantId, cancellationToken);
                ensureQuota(snapshot, requested);

                var result = await action(cancellationToken);
                if (ownedTransaction is not null)
                {
                    await ownedTransaction.CommitAsync(cancellationToken);
                }

                return result;
            }
            catch
            {
                if (ownedTransaction is not null)
                {
                    await ownedTransaction.RollbackAsync(CancellationToken.None);
                }

                throw;
            }
            finally
            {
                if (ownedTransaction is not null)
                {
                    await ownedTransaction.DisposeAsync();
                }
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task LockTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            if (!await dbContext.Tenants.AnyAsync(x => x.Id == tenantId, cancellationToken))
            {
                throw new InvalidOperationException("当前租户不存在。");
            }

            return;
        }

        var tenant = await dbContext.Tenants
            .FromSqlInterpolated($"SELECT * FROM `mini_tenants` WHERE `Id` = {tenantId} FOR UPDATE")
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (tenant is null)
        {
            throw new InvalidOperationException("当前租户不存在。");
        }
    }

    private async Task<TenantResourceQuotaSnapshot> GetRequiredSnapshotAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        return await GetSnapshotAsync(tenantId, cancellationToken)
            ?? throw new InvalidOperationException("当前租户不存在。");
    }

    private async Task<TenantResourceQuotaSnapshot?> GetSnapshotAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var limits = await dbContext.Tenants
            .AsNoTracking()
            .Where(x => x.Id == tenantId)
            .Select(x => new
            {
                MaxUsers = x.Package == null ? 0 : x.Package.MaxUsers,
                MaxStorageMb = x.Package == null ? 0 : x.Package.MaxStorageMb
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (limits is null)
        {
            return null;
        }

        var usedUsers = await dbContext.Users
            .AsNoTracking()
            .CountAsync(x => x.TenantId == tenantId, cancellationToken);
        var usedStorageBytes = await dbContext.ManagedFiles
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .SumAsync(x => (long?)x.Size, cancellationToken) ?? 0;

        return new TenantResourceQuotaSnapshot(
            tenantId,
            usedUsers,
            Math.Max(limits.MaxUsers, 0),
            usedStorageBytes,
            checked((long)Math.Max(limits.MaxStorageMb, 0) * BytesPerMb));
    }

    private static void EnsureUserQuota(TenantResourceQuotaSnapshot snapshot, int additionalUsers)
    {
        if (snapshot.IsUserUnlimited || snapshot.UsedUsers + (long)additionalUsers <= snapshot.MaxUsers)
        {
            return;
        }

        throw new TenantResourceQuotaExceededException(
            "users",
            snapshot.UsedUsers,
            snapshot.MaxUsers,
            additionalUsers,
            $"租户用户配额不足：当前 {snapshot.UsedUsers}/{snapshot.MaxUsers}，本次需要新增 {additionalUsers} 个账号。");
    }

    private static void EnsureStorageQuota(TenantResourceQuotaSnapshot snapshot, long additionalBytes)
    {
        if (snapshot.IsStorageUnlimited ||
            snapshot.UsedStorageBytes + additionalBytes <= snapshot.MaxStorageBytes)
        {
            return;
        }

        throw new TenantResourceQuotaExceededException(
            "storage",
            snapshot.UsedStorageBytes,
            snapshot.MaxStorageBytes,
            additionalBytes,
            $"租户存储配额不足：当前已用 {FormatBytes(snapshot.UsedStorageBytes)}，上限 {FormatBytes(snapshot.MaxStorageBytes)}，本次文件 {FormatBytes(additionalBytes)}。");
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024L * 1024L * 1024L)
        {
            return $"{bytes / (1024d * 1024d * 1024d):0.##} GB";
        }

        return $"{bytes / (1024d * 1024d):0.##} MB";
    }
}
