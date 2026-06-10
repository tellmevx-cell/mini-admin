using System.Text.Json;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.Workflows;

namespace MiniAdmin.Application.Workflows;

public sealed class WorkflowAppService(
    IWorkflowRepository workflowRepository,
    IEnumerable<IWorkflowBusinessStateHandler> businessStateHandlers) : IWorkflowAppService
{
    public Task<PageResult<WorkflowDefinitionDto>> GetDefinitionsAsync(
        WorkflowDefinitionListQuery query,
        CancellationToken cancellationToken = default)
    {
        return workflowRepository.GetDefinitionsAsync(query, cancellationToken);
    }

    public Task<IReadOnlyList<WorkflowDefinitionOptionDto>> GetDefinitionOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        return workflowRepository.GetDefinitionOptionsAsync(cancellationToken);
    }

    public Task<PageResult<WorkflowBusinessBindingDto>> GetBusinessBindingsAsync(
        WorkflowBusinessBindingListQuery query,
        CancellationToken cancellationToken = default)
    {
        return workflowRepository.GetBusinessBindingsAsync(query, cancellationToken);
    }

    public Task<WorkflowBusinessDefinitionDto?> ResolveBusinessDefinitionAsync(
        string businessType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(businessType))
        {
            throw new WorkflowOperationException("业务类型不能为空.");
        }

        return workflowRepository.ResolveBusinessDefinitionAsync(businessType, cancellationToken);
    }

    public Task<WorkflowDefinitionDto> CreateDefinitionAsync(
        SaveWorkflowDefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateDefinitionRequest(request);
        return workflowRepository.CreateDefinitionAsync(request, cancellationToken);
    }

    public Task<WorkflowDefinitionDto?> UpdateDefinitionAsync(
        Guid id,
        SaveWorkflowDefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateDefinitionRequest(request);
        return workflowRepository.UpdateDefinitionAsync(id, request, cancellationToken);
    }

    public Task<WorkflowDefinitionDto?> PublishDefinitionAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return workflowRepository.PublishDefinitionAsync(id, cancellationToken);
    }

    public Task<WorkflowDefinitionDto?> CreateNewVersionAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return workflowRepository.CreateNewVersionAsync(id, cancellationToken);
    }

    public Task<bool> DeleteDefinitionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return workflowRepository.DeleteDefinitionAsync(id, cancellationToken);
    }

    public Task<WorkflowBusinessBindingDto> CreateBusinessBindingAsync(
        SaveWorkflowBusinessBindingRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateBusinessBindingRequest(request);
        return workflowRepository.CreateBusinessBindingAsync(request, cancellationToken);
    }

    public Task<WorkflowBusinessBindingDto?> UpdateBusinessBindingAsync(
        Guid id,
        SaveWorkflowBusinessBindingRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateBusinessBindingRequest(request);
        return workflowRepository.UpdateBusinessBindingAsync(id, request, cancellationToken);
    }

    public Task<bool> DeleteBusinessBindingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return workflowRepository.DeleteBusinessBindingAsync(id, cancellationToken);
    }

    public Task<PageResult<WorkflowInstanceDto>> GetInstancesAsync(
        WorkflowInstanceListQuery query,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default)
    {
        return workflowRepository.GetInstancesAsync(query, user, cancellationToken);
    }

    public Task<PageResult<WorkflowInstanceDto>> GetCcInstancesAsync(
        WorkflowInstanceListQuery query,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default)
    {
        return workflowRepository.GetCcInstancesAsync(query, user, cancellationToken);
    }

    public Task<PageResult<WorkflowCcRecordDto>> GetCcRecordsAsync(
        WorkflowCcListQuery query,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default)
    {
        return workflowRepository.GetCcRecordsAsync(query, user, cancellationToken);
    }

    public Task<WorkflowCcRecordDto?> MarkCcRecordAsReadAsync(
        Guid ccRecordId,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default)
    {
        return workflowRepository.MarkCcRecordAsReadAsync(ccRecordId, user, cancellationToken);
    }

    public Task<WorkflowInstanceDto?> GetInstanceAsync(
        Guid id,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default)
    {
        return workflowRepository.GetInstanceAsync(id, user, cancellationToken);
    }

    public Task<IReadOnlyList<WorkflowTaskDto>> GetTodoTasksAsync(
        WorkflowUserContext user,
        CancellationToken cancellationToken = default)
    {
        return workflowRepository.GetTodoTasksAsync(user, cancellationToken);
    }

    public Task<IReadOnlyList<WorkflowTaskDto>> GetDoneTasksAsync(
        WorkflowUserContext user,
        CancellationToken cancellationToken = default)
    {
        return workflowRepository.GetDoneTasksAsync(user, cancellationToken);
    }

    public async Task<WorkflowInstanceDto> StartInstanceAsync(
        StartWorkflowInstanceRequest request,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default)
    {
        if (request.DefinitionId == Guid.Empty)
        {
            throw new WorkflowOperationException("请选择流程定义.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new WorkflowOperationException("请填写审批标题.");
        }

        var instance = await workflowRepository.StartInstanceAsync(request, user, cancellationToken);
        await DispatchBusinessStateAsync(instance, cancellationToken);
        return instance;
    }

    public Task<WorkflowInstanceDto?> AddAttachmentAsync(
        Guid instanceId,
        WorkflowAttachmentRequest request,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default)
    {
        if (request.FileId == Guid.Empty)
        {
            throw new WorkflowOperationException("请选择要上传的附件.");
        }

        return workflowRepository.AddAttachmentAsync(instanceId, request, user, cancellationToken);
    }

    public Task<WorkflowCommentDto?> AddCommentAsync(
        Guid instanceId,
        WorkflowCommentRequest request,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new WorkflowOperationException("评论内容不能为空.");
        }

        return workflowRepository.AddCommentAsync(instanceId, request, user, cancellationToken);
    }

    public Task<WorkflowAttachmentDownloadDto?> GetAttachmentDownloadAsync(
        Guid instanceId,
        Guid attachmentId,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default)
    {
        if (instanceId == Guid.Empty || attachmentId == Guid.Empty)
        {
            throw new WorkflowOperationException("请选择要下载的流程附件.");
        }

        return workflowRepository.GetAttachmentDownloadAsync(
            instanceId,
            attachmentId,
            user,
            cancellationToken);
    }

    public async Task<WorkflowInstanceDto?> ApproveAsync(
        Guid instanceId,
        WorkflowActionRequest request,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default)
    {
        var instance = await workflowRepository.ApproveAsync(instanceId, request, user, cancellationToken);
        await DispatchBusinessStateAsync(instance, cancellationToken);
        return instance;
    }

    public async Task<WorkflowInstanceDto?> RejectAsync(
        Guid instanceId,
        WorkflowActionRequest request,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default)
    {
        var instance = await workflowRepository.RejectAsync(instanceId, request, user, cancellationToken);
        await DispatchBusinessStateAsync(instance, cancellationToken);
        return instance;
    }

    public Task<WorkflowTaskDto?> TransferTaskAsync(
        Guid taskId,
        WorkflowTransferTaskRequest request,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default)
    {
        if (request.TargetUserId == Guid.Empty)
        {
            throw new WorkflowOperationException("请选择转办接收人.");
        }

        return workflowRepository.TransferTaskAsync(taskId, request, user, cancellationToken);
    }

    public Task<WorkflowTaskDto?> RemindTaskAsync(
        Guid taskId,
        WorkflowRemindTaskRequest request,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default)
    {
        return workflowRepository.RemindTaskAsync(taskId, request, user, cancellationToken);
    }

    public Task<WorkflowSlaScanResultDto> ScanOverdueTasksAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        return workflowRepository.ScanOverdueTasksAsync(now, cancellationToken);
    }

    public async Task<WorkflowInstanceDto?> WithdrawAsync(
        Guid instanceId,
        WorkflowActionRequest request,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default)
    {
        var instance = await workflowRepository.WithdrawAsync(instanceId, request, user, cancellationToken);
        await DispatchBusinessStateAsync(instance, cancellationToken);
        return instance;
    }

    private async Task DispatchBusinessStateAsync(
        WorkflowInstanceDto? instance,
        CancellationToken cancellationToken)
    {
        if (instance is null)
        {
            return;
        }

        foreach (var handler in businessStateHandlers)
        {
            await handler.HandleAsync(instance, cancellationToken);
        }
    }

    private static void ValidateDefinitionRequest(SaveWorkflowDefinitionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            throw new WorkflowOperationException("请填写流程编码.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new WorkflowOperationException("请填写流程名称.");
        }

        ValidateFormSchemaJson(request.FormSchemaJson);

        var enabledNodes = request.Nodes.Where(node => node.IsEnabled).ToArray();
        if (enabledNodes.Length == 0)
        {
            throw new WorkflowOperationException("请至少配置一个启用的审批节点.");
        }

        foreach (var node in enabledNodes)
        {
            if (string.IsNullOrWhiteSpace(node.Name))
            {
                throw new WorkflowOperationException("请填写节点名称.");
            }

            if (!node.NodeType.Equals("approve", StringComparison.OrdinalIgnoreCase) &&
                !node.NodeType.Equals("cc", StringComparison.OrdinalIgnoreCase))
            {
                throw new WorkflowOperationException("节点类型仅支持审批或抄送.");
            }

            if (!node.ApprovalMode.Equals("Any", StringComparison.OrdinalIgnoreCase) &&
                !node.ApprovalMode.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                throw new WorkflowOperationException("审批方式仅支持或签或会签.");
            }

            if (node.SlaMinutes is < 0)
            {
                throw new WorkflowOperationException("处理时限不能小于 0.");
            }

            if (node.ApproverType.Equals("User", StringComparison.OrdinalIgnoreCase) &&
                !node.ApproverUserId.HasValue)
            {
                throw new WorkflowOperationException("指定用户节点必须选择处理用户.");
            }

            if (node.ApproverType.Equals("Role", StringComparison.OrdinalIgnoreCase) &&
                !node.ApproverRoleId.HasValue)
            {
                throw new WorkflowOperationException("指定角色节点必须选择处理角色.");
            }

            if (!node.ApproverType.Equals("User", StringComparison.OrdinalIgnoreCase) &&
                !node.ApproverType.Equals("Role", StringComparison.OrdinalIgnoreCase))
            {
                throw new WorkflowOperationException("审批人类型仅支持指定用户或指定角色.");
            }
        }

        ValidateDesignerGraph(request.DesignerJson, enabledNodes);
    }

    private static void ValidateFormSchemaJson(string? formSchemaJson)
    {
        if (string.IsNullOrWhiteSpace(formSchemaJson))
        {
            return;
        }

        List<WorkflowFormSchemaField>? fields;
        try
        {
            fields = JsonSerializer.Deserialize<List<WorkflowFormSchemaField>>(
                formSchemaJson,
                JsonOptions);
        }
        catch (JsonException)
        {
            throw new WorkflowOperationException("流程表单配置 JSON 格式不正确.");
        }

        if (fields is null || fields.Count == 0)
        {
            return;
        }

        var fieldCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            var fieldCode = field.Field?.Trim();
            if (string.IsNullOrWhiteSpace(fieldCode))
            {
                throw new WorkflowOperationException("表单字段编码不能为空.");
            }

            if (!IsValidFormFieldCode(fieldCode))
            {
                throw new WorkflowOperationException($"表单字段编码「{fieldCode}」只能使用英文字母、数字和下划线，且不能以数字开头.");
            }

            if (!fieldCodes.Add(fieldCode))
            {
                throw new WorkflowOperationException($"表单字段编码「{fieldCode}」重复.");
            }

            var fieldLabel = string.IsNullOrWhiteSpace(field.Label) ? fieldCode : field.Label.Trim();
            var component = NormalizeFormComponent(field.Component);
            if (!IsSupportedFormComponent(component))
            {
                throw new WorkflowOperationException($"表单字段「{fieldLabel}」控件类型不支持.");
            }

            if (component == "select")
            {
                ValidateSelectOptions(fieldLabel, field.Options);
            }
        }
    }

    private static void ValidateSelectOptions(
        string fieldLabel,
        IReadOnlyList<WorkflowFormSchemaOption>? options)
    {
        var validOptions = options?
            .Where(option => !string.IsNullOrWhiteSpace(option.Value))
            .ToArray() ?? [];
        if (validOptions.Length == 0)
        {
            throw new WorkflowOperationException($"表单字段「{fieldLabel}」是下拉框时必须配置选项.");
        }

        var optionValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var option in validOptions)
        {
            var optionValue = option.Value!.Trim();
            if (!optionValues.Add(optionValue))
            {
                throw new WorkflowOperationException($"表单字段「{fieldLabel}」存在重复选项值「{optionValue}」.");
            }
        }
    }

    private static bool IsValidFormFieldCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || char.IsDigit(value[0]))
        {
            return false;
        }

        return value.All(ch => char.IsLetterOrDigit(ch) || ch == '_');
    }

    private static bool IsSupportedFormComponent(string component)
    {
        return component is "date" or "number" or "select" or "text" or "textarea";
    }

    private static string NormalizeFormComponent(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "text" : value.Trim().ToLowerInvariant();
    }

    private static void ValidateDesignerGraph(
        string? designerJson,
        IReadOnlyCollection<SaveWorkflowNodeRequest> enabledNodes)
    {
        if (string.IsNullOrWhiteSpace(designerJson))
        {
            return;
        }

        WorkflowDesignerValidationGraph? graph;
        try
        {
            graph = JsonSerializer.Deserialize<WorkflowDesignerValidationGraph>(
                designerJson,
                JsonOptions);
        }
        catch (JsonException)
        {
            throw new WorkflowOperationException("流程画布 JSON 格式不正确.");
        }

        var nodes = graph?.Nodes ?? [];
        var edges = graph?.Edges ?? [];
        if (edges.Count == 0)
        {
            // 兼容旧草稿：没有画布连线时，运行时仍按节点顺序流转。
            return;
        }

        if (nodes.Count == 0)
        {
            throw new WorkflowOperationException("流程画布缺少节点，请先整理画布连线.");
        }

        var nodeIds = nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.Id))
            .Select(node => node.Id!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in edges)
        {
            if (string.IsNullOrWhiteSpace(edge.Source) ||
                string.IsNullOrWhiteSpace(edge.Target))
            {
                throw new WorkflowOperationException("流程画布存在未连接完整的连线，请检查分支起点和终点.");
            }

            if (!nodeIds.Contains(edge.Source) || !nodeIds.Contains(edge.Target))
            {
                throw new WorkflowOperationException("流程画布存在指向不存在节点的连线，请重新整理连线.");
            }
        }

        var startNode = nodes.FirstOrDefault(node =>
            string.Equals(node.Id, "start", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(node.Type, "start", StringComparison.OrdinalIgnoreCase));
        if (startNode is null)
        {
            throw new WorkflowOperationException("流程画布缺少开始节点.");
        }

        if (!edges.Any(edge => string.Equals(edge.Source, startNode.Id, StringComparison.OrdinalIgnoreCase)))
        {
            throw new WorkflowOperationException("开始节点必须至少连接一个出口.");
        }

        foreach (var conditionNode in nodes.Where(node =>
            string.Equals(node.Type, "condition", StringComparison.OrdinalIgnoreCase)))
        {
            var outgoingEdges = edges
                .Where(edge => string.Equals(edge.Source, conditionNode.Id, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var conditionNodeName = GetDesignerNodeName(conditionNode);
            if (outgoingEdges.Length == 0)
            {
                throw new WorkflowOperationException($"条件节点「{conditionNodeName}」必须至少配置一个出口.");
            }

            if (!outgoingEdges.Any(edge => edge.IsDefault))
            {
                throw new WorkflowOperationException($"条件节点「{conditionNodeName}」必须配置默认分支.");
            }

            var invalidConditionEdge = outgoingEdges.FirstOrDefault(IsInvalidConditionEdge);
            if (invalidConditionEdge is not null)
            {
                throw new WorkflowOperationException($"条件分支「{invalidConditionEdge.Id ?? conditionNodeName}」缺少判断规则.");
            }
        }

        var reachableNodeIds = ResolveReachableDesignerNodeIds(startNode.Id!, edges);
        foreach (var node in enabledNodes)
        {
            if (string.IsNullOrWhiteSpace(node.DesignerNodeId))
            {
                throw new WorkflowOperationException($"节点「{node.Name}」没有关联画布节点.");
            }

            if (!nodeIds.Contains(node.DesignerNodeId))
            {
                throw new WorkflowOperationException($"节点「{node.Name}」没有出现在流程画布中.");
            }

            if (!reachableNodeIds.Contains(node.DesignerNodeId))
            {
                throw new WorkflowOperationException($"节点「{node.Name}」在流程画布中不可达，请检查连线.");
            }
        }
    }

    private static bool IsInvalidConditionEdge(WorkflowDesignerValidationEdge edge)
    {
        if (edge.IsDefault)
        {
            return false;
        }

        var conditionOperator = string.IsNullOrWhiteSpace(edge.ConditionOperator)
            ? "Equals"
            : edge.ConditionOperator.Trim();
        if (conditionOperator.Equals("Always", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(edge.ConditionField))
        {
            return true;
        }

        return !conditionOperator.Equals("Empty", StringComparison.OrdinalIgnoreCase) &&
               !conditionOperator.Equals("NotEmpty", StringComparison.OrdinalIgnoreCase) &&
               string.IsNullOrWhiteSpace(edge.ConditionValue);
    }

    private static HashSet<string> ResolveReachableDesignerNodeIds(
        string startNodeId,
        IReadOnlyCollection<WorkflowDesignerValidationEdge> edges)
    {
        var reachableNodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pendingNodeIds = new Queue<string>();
        pendingNodeIds.Enqueue(startNodeId);

        while (pendingNodeIds.TryDequeue(out var nodeId))
        {
            if (!reachableNodeIds.Add(nodeId))
            {
                continue;
            }

            foreach (var edge in edges.Where(edge =>
                string.Equals(edge.Source, nodeId, StringComparison.OrdinalIgnoreCase)))
            {
                if (!string.IsNullOrWhiteSpace(edge.Target) &&
                    !reachableNodeIds.Contains(edge.Target))
                {
                    pendingNodeIds.Enqueue(edge.Target);
                }
            }
        }

        return reachableNodeIds;
    }

    private static string GetDesignerNodeName(WorkflowDesignerValidationNode node)
    {
        return string.IsNullOrWhiteSpace(node.Label)
            ? node.Id ?? "未命名条件节点"
            : node.Label;
    }

    private static void ValidateBusinessBindingRequest(SaveWorkflowBusinessBindingRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.BusinessType))
        {
            throw new WorkflowOperationException("请填写业务类型.");
        }

        if (string.IsNullOrWhiteSpace(request.BusinessName))
        {
            throw new WorkflowOperationException("请填写业务名称.");
        }

        if (request.DefinitionId == Guid.Empty)
        {
            throw new WorkflowOperationException("请选择流程定义.");
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record WorkflowDesignerValidationGraph(
        List<WorkflowDesignerValidationNode>? Nodes = null,
        List<WorkflowDesignerValidationEdge>? Edges = null);

    private sealed record WorkflowDesignerValidationNode(
        string? Id = null,
        string? Type = null,
        string? Label = null);

    private sealed record WorkflowDesignerValidationEdge(
        string? Id = null,
        string? Source = null,
        string? Target = null,
        string? ConditionField = null,
        string? ConditionOperator = null,
        string? ConditionValue = null,
        bool IsDefault = false);

    private sealed record WorkflowFormSchemaField(
        string? Field = null,
        string? Label = null,
        string? Component = null,
        bool Required = false,
        string? DefaultValue = null,
        string? Placeholder = null,
        IReadOnlyList<WorkflowFormSchemaOption>? Options = null);

    private sealed record WorkflowFormSchemaOption(
        string? Label = null,
        string? Value = null);
}
