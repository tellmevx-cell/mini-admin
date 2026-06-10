using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.MultiTenancy;
using MiniAdmin.Application.Contracts.Workflows;
using MiniAdmin.Domain.Entities;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class SampleOrderWorkflowStateHandler(
    MiniAdminDbContext dbContext,
    ICurrentTenant currentTenant) : IWorkflowBusinessStateHandler
{
    public async Task HandleAsync(
        WorkflowInstanceDto instance,
        CancellationToken cancellationToken = default)
    {
        if (!SampleOrder.TryParseBusinessKey(instance.BusinessKey, out var orderId))
        {
            return;
        }

        var order = await ApplyTenantFilter(dbContext.Set<SampleOrder>())
            .SingleOrDefaultAsync(x => x.Id == orderId, cancellationToken);
        if (order is null)
        {
            return;
        }

        order.WorkflowInstanceId = Guid.Parse(instance.Id);
        order.Status = instance.Status switch
        {
            "Pending" => SampleOrder.PendingApprovalStatus,
            "Approved" => SampleOrder.ApprovedStatus,
            "Rejected" => SampleOrder.RejectedStatus,
            "Withdrawn" => SampleOrder.WithdrawnStatus,
            _ => order.Status
        };

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<SampleOrder> ApplyTenantFilter(IQueryable<SampleOrder> source)
    {
        return currentTenant.IsTenant
            ? source.Where(x => x.TenantId == currentTenant.TenantId)
            : source.Where(x => x.TenantId == null);
    }
}
