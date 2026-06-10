using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.SampleOrders;
using MiniAdmin.Application.Contracts.Workflows;
using MiniAdmin.Domain.Entities;
using System.Text.Json;

namespace MiniAdmin.Application.SampleOrders;

public sealed class SampleOrderAppService(
    ISampleOrderRepository sampleOrderRepository,
    IWorkflowAppService workflowAppService) : ISampleOrderAppService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    public Task<PageResult<SampleOrderDto>> GetListAsync(SampleOrderListQuery query, CancellationToken cancellationToken = default)
    {
        return sampleOrderRepository.GetListAsync(query, cancellationToken);
    }

    public Task<SampleOrderDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return sampleOrderRepository.GetAsync(id, cancellationToken);
    }

    public Task<SampleOrderDto> CreateAsync(SaveSampleOrderRequest request, CancellationToken cancellationToken = default)
    {
        return sampleOrderRepository.CreateAsync(request, cancellationToken);
    }

    public Task<SampleOrderDto?> UpdateAsync(Guid id, SaveSampleOrderRequest request, CancellationToken cancellationToken = default)
    {
        return sampleOrderRepository.UpdateAsync(id, request, cancellationToken);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return sampleOrderRepository.DeleteAsync(id, cancellationToken);
    }

    public async Task<SampleOrderDto?> SubmitWorkflowAsync(
        Guid id,
        SubmitSampleOrderWorkflowRequest request,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default)
    {
        if (request.DefinitionId == Guid.Empty)
        {
            throw new InvalidOperationException("请选择流程定义.");
        }

        var order = await sampleOrderRepository.GetAsync(id, cancellationToken);
        if (order is null)
        {
            return null;
        }

        if (order.Status is SampleOrder.PendingApprovalStatus)
        {
            throw new InvalidOperationException("示例订单已在审批中，不能重复提交.");
        }

        if (order.Status is SampleOrder.ApprovedStatus)
        {
            throw new InvalidOperationException("示例订单已审批通过，不能重新提交.");
        }

        var title = $"示例订单审批：{order.StoredName}";
        var formDataJson = JsonSerializer.Serialize(new
        {
            orderId = order.Id,
            orderName = order.OriginalName,
            orderNo = order.StoredName,
            orderType = order.ContentType,
            amount = order.Size,
            source = order.StorageProvider,
            remark = order.StoragePath
        }, JsonOptions);

        await workflowAppService.StartInstanceAsync(
            new StartWorkflowInstanceRequest(
                request.DefinitionId,
                title,
                SampleOrder.CreateBusinessKey(id),
                formDataJson),
            user,
            cancellationToken);

        return await sampleOrderRepository.GetAsync(id, cancellationToken);
    }

    public async Task<SampleOrderDto?> WithdrawWorkflowAsync(
        Guid id,
        WithdrawSampleOrderWorkflowRequest request,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default)
    {
        var order = await sampleOrderRepository.GetAsync(id, cancellationToken);
        if (order is null)
        {
            return null;
        }

        if (order.Status is not SampleOrder.PendingApprovalStatus)
        {
            throw new InvalidOperationException("只有审批中的示例订单可以撤回.");
        }

        if (string.IsNullOrWhiteSpace(order.WorkflowInstanceId) ||
            !Guid.TryParse(order.WorkflowInstanceId, out var workflowInstanceId))
        {
            throw new InvalidOperationException("示例订单没有关联的流程实例，无法撤回.");
        }

        await workflowAppService.WithdrawAsync(
            workflowInstanceId,
            new WorkflowActionRequest(request.Comment),
            user,
            cancellationToken);

        return await sampleOrderRepository.GetAsync(id, cancellationToken);
    }
}
