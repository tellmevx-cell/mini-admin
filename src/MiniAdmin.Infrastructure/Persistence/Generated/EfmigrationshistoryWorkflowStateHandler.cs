using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.MultiTenancy;
using MiniAdmin.Application.Contracts.Workflows;
using MiniAdmin.Domain.Entities;

namespace MiniAdmin.Infrastructure.Persistence.Generated;

public sealed class EfmigrationshistoryWorkflowStateHandler(
    MiniAdminDbContext dbContext,
    ICurrentTenant currentTenant) : IWorkflowBusinessStateHandler
{
    public async Task HandleAsync(
        WorkflowInstanceDto instance,
        CancellationToken cancellationToken = default)
    {
        if (!Efmigrationshistory.TryParseBusinessKey(instance.BusinessKey, out var id))
        {
            return;
        }

        var entity = await ApplyTenantFilter(dbContext.Set<Efmigrationshistory>())
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return;
        }

        entity.WorkflowInstanceId = instance.Id;
        entity.ApprovalStatus = instance.Status switch
        {
            "Pending" => "Pending",
            "Approved" => "Approved",
            "Rejected" => "Rejected",
            "Withdrawn" => "Withdrawn",
            _ => entity.ApprovalStatus
        };

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<Efmigrationshistory> ApplyTenantFilter(IQueryable<Efmigrationshistory> source)
    {
        return currentTenant.IsTenant
            ? source.Where(x => x.TenantId == currentTenant.TenantId)
            : source.Where(x => x.TenantId == null);
    }
}