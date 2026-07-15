using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.SampleOrders;
using MiniAdmin.Application.Contracts.Security;
using MiniAdmin.Application.Contracts.Workflows;
using MiniAdmin.Domain.Entities;
using MiniAdmin.Platform.DynamicApi;
using System.Text.Json;

namespace MiniAdmin.Application.SampleOrders;

[DynamicApi("business/sample-order", Name = "SampleOrder", Tag = "Business")]
public sealed class SampleOrderAppService(
    ISampleOrderRepository sampleOrderRepository,
    IWorkflowAppService workflowAppService) : ISampleOrderAppService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    [DynamicGet(
        "list",
        Permission = "business:sample-order:query",
        Resource = "business.sample-order",
        Action = "query",
        OperationId = "SampleOrder_GetList",
        Summary = "查询示例订单")]
    public Task<PageResult<SampleOrderDto>> GetListAsync(SampleOrderListQuery query, CancellationToken cancellationToken = default)
    {
        return sampleOrderRepository.GetListAsync(query, cancellationToken);
    }

    [DynamicGet(
        "{id:guid}",
        Permission = "business:sample-order:query",
        Resource = "business.sample-order",
        Action = "query",
        OperationId = "SampleOrder_Get",
        Summary = "获取示例订单")]
    public Task<SampleOrderDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return sampleOrderRepository.GetAsync(id, cancellationToken);
    }

    [DynamicPost(
        Permission = "business:sample-order:create",
        Resource = "business.sample-order",
        Action = "create",
        OperationId = "SampleOrder_Create",
        Summary = "创建示例订单")]
    public Task<SampleOrderDto> CreateAsync(SaveSampleOrderRequest request, CancellationToken cancellationToken = default)
    {
        return sampleOrderRepository.CreateAsync(request, cancellationToken);
    }

    [DynamicPut(
        "{id:guid}",
        Permission = "business:sample-order:update",
        Resource = "business.sample-order",
        Action = "update",
        OperationId = "SampleOrder_Update",
        Summary = "更新示例订单")]
    public Task<SampleOrderDto?> UpdateAsync(Guid id, SaveSampleOrderRequest request, CancellationToken cancellationToken = default)
    {
        return sampleOrderRepository.UpdateAsync(id, request, cancellationToken);
    }

    [DynamicDelete(
        "{id:guid}",
        Permission = "business:sample-order:delete",
        Resource = "business.sample-order",
        Action = "delete",
        OperationId = "SampleOrder_Delete",
        Summary = "删除示例订单")]
    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return sampleOrderRepository.DeleteAsync(id, cancellationToken);
    }

    [DynamicPost(
        "{id:guid}/submit-workflow",
        Permission = "business:sample-order:submit-workflow",
        Resource = "business.sample-order",
        Action = "submit-workflow",
        OperationId = "SampleOrder_SubmitWorkflow",
        Summary = "提交示例订单审批")]
    public Task<SampleOrderDto?> SubmitCurrentUserWorkflowAsync(
        Guid id,
        SubmitSampleOrderWorkflowRequest request,
        [DynamicApiParameter(DynamicApiParameterSource.Services)] ICurrentUserContext currentUser,
        CancellationToken cancellationToken = default)
    {
        return SubmitWorkflowAsync(
            id,
            request,
            new WorkflowUserContext(currentUser.UserId, currentUser.UserName),
            cancellationToken);
    }

    [DynamicPost(
        "{id:guid}/withdraw-workflow",
        Permission = "business:sample-order:withdraw-workflow",
        Resource = "business.sample-order",
        Action = "withdraw-workflow",
        OperationId = "SampleOrder_WithdrawWorkflow",
        Summary = "撤回示例订单审批")]
    public Task<SampleOrderDto?> WithdrawCurrentUserWorkflowAsync(
        Guid id,
        WithdrawSampleOrderWorkflowRequest request,
        [DynamicApiParameter(DynamicApiParameterSource.Services)] ICurrentUserContext currentUser,
        CancellationToken cancellationToken = default)
    {
        return WithdrawWorkflowAsync(
            id,
            request,
            new WorkflowUserContext(currentUser.UserId, currentUser.UserName),
            cancellationToken);
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
