using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.MultiTenancy;
using MiniAdmin.Application.Contracts.UserNotifications;
using MiniAdmin.Application.Contracts.Workflows;
using MiniAdmin.Domain.Entities;
using System.Globalization;
using System.Text.Json;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class EfWorkflowRepository(
    MiniAdminDbContext dbContext,
    ICurrentTenant currentTenant,
    INotificationTemplateRenderer notificationTemplateRenderer,
    INotificationDeliveryService notificationDeliveryService) : IWorkflowRepository
{
    public async Task<PageResult<WorkflowDefinitionDto>> GetDefinitionsAsync(
        WorkflowDefinitionListQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var definitionsQuery = ApplyTenantScope(dbContext.WorkflowDefinitions.AsNoTracking())
            .Include(x => x.Nodes)
            .ThenInclude(x => x.ApproverUser)
            .Include(x => x.Nodes)
            .ThenInclude(x => x.ApproverRole)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            definitionsQuery = definitionsQuery.Where(x =>
                x.Code.Contains(keyword) || x.Name.Contains(keyword));
        }

        if (query.IsEnabled.HasValue)
        {
            definitionsQuery = definitionsQuery.Where(x => x.IsEnabled == query.IsEnabled.Value);
        }

        var total = await definitionsQuery.CountAsync(cancellationToken);
        var definitions = await definitionsQuery
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        return new PageResult<WorkflowDefinitionDto>(
            definitions.Select(ToDefinitionDto).ToArray(),
            total);
    }

    public async Task<IReadOnlyList<WorkflowDefinitionOptionDto>> GetDefinitionOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        return await ApplyTenantScope(dbContext.WorkflowDefinitions.AsNoTracking())
            .Where(x =>
                x.IsEnabled &&
                x.PublishStatus == WorkflowDefinition.PublishedStatus &&
                x.Nodes.Any(node => node.IsEnabled))
            .OrderBy(x => x.Name)
            .ThenByDescending(x => x.Version)
            .Select(x => new WorkflowDefinitionOptionDto(
                x.Id.ToString(),
                x.Code,
                x.Name,
                x.FormName,
                x.Version,
                x.FormSchemaJson))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<PageResult<WorkflowBusinessBindingDto>> GetBusinessBindingsAsync(
        WorkflowBusinessBindingListQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var bindingsQuery = ApplyTenantScope(dbContext.WorkflowBusinessBindings.AsNoTracking())
            .Include(x => x.Definition)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            bindingsQuery = bindingsQuery.Where(x =>
                x.BusinessType.Contains(keyword) ||
                x.BusinessName.Contains(keyword) ||
                x.Definition.Code.Contains(keyword) ||
                x.Definition.Name.Contains(keyword));
        }

        if (query.IsEnabled.HasValue)
        {
            bindingsQuery = bindingsQuery.Where(x => x.IsEnabled == query.IsEnabled.Value);
        }

        var total = await bindingsQuery.CountAsync(cancellationToken);
        var bindings = await bindingsQuery
            .OrderByDescending(x => x.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        return new PageResult<WorkflowBusinessBindingDto>(
            bindings.Select(ToBusinessBindingDto).ToArray(),
            total);
    }

    public async Task<WorkflowBusinessDefinitionDto?> ResolveBusinessDefinitionAsync(
        string businessType,
        CancellationToken cancellationToken = default)
    {
        var normalizedBusinessType = NormalizeBusinessType(businessType);
        var binding = await ApplyTenantScope(dbContext.WorkflowBusinessBindings.AsNoTracking())
            .Include(x => x.Definition)
            .Where(x =>
                x.BusinessType == normalizedBusinessType &&
                x.IsEnabled &&
                x.Definition.IsEnabled &&
                x.Definition.PublishStatus == WorkflowDefinition.PublishedStatus)
            .SingleOrDefaultAsync(cancellationToken);

        return binding is null ? null : ToBusinessDefinitionDto(binding);
    }

    public async Task<WorkflowDefinitionDto> CreateDefinitionAsync(
        SaveWorkflowDefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureCodeCanCreateAsync(request.Code, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var definition = new WorkflowDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = currentTenant.TenantId,
            Version = 1,
            PublishStatus = WorkflowDefinition.DraftStatus,
            CreatedAt = now,
            UpdatedAt = now
        };
        ApplyDefinitionRequest(definition, request);

        dbContext.WorkflowDefinitions.Add(definition);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetRequiredDefinitionDtoAsync(definition.Id, cancellationToken);
    }

    public async Task<WorkflowDefinitionDto?> UpdateDefinitionAsync(
        Guid id,
        SaveWorkflowDefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        var definition = await ApplyTenantScope(dbContext.WorkflowDefinitions)
            .Include(x => x.Nodes)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (definition is null)
        {
            return null;
        }

        if (definition.PublishStatus != WorkflowDefinition.DraftStatus)
        {
            throw new WorkflowOperationException("已发布或归档的流程定义不可直接修改，请创建新版本草稿后再调整。");
        }

        await EnsureCodeVersionUniqueAsync(id, request.Code, definition.Version, cancellationToken);
        dbContext.WorkflowNodes.RemoveRange(definition.Nodes);
        ApplyDefinitionRequest(definition, request);
        definition.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetRequiredDefinitionDtoAsync(id, cancellationToken);
    }

    public async Task<WorkflowDefinitionDto?> PublishDefinitionAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var definition = await ApplyTenantScope(dbContext.WorkflowDefinitions)
            .Include(x => x.Nodes)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (definition is null)
        {
            return null;
        }

        if (!definition.Nodes.Any(x => x.IsEnabled))
        {
            throw new WorkflowOperationException("流程定义没有可用审批节点，不能发布.");
        }

        if (definition.PublishStatus == WorkflowDefinition.ArchivedStatus)
        {
            throw new WorkflowOperationException("已归档的流程定义不能重新发布，请创建新版本。");
        }

        var now = DateTimeOffset.UtcNow;
        var publishedDefinitions = await ApplyTenantScope(dbContext.WorkflowDefinitions)
            .Where(x =>
                x.Id != definition.Id &&
                x.Code == definition.Code &&
                x.PublishStatus == WorkflowDefinition.PublishedStatus)
            .ToArrayAsync(cancellationToken);
        foreach (var publishedDefinition in publishedDefinitions)
        {
            publishedDefinition.PublishStatus = WorkflowDefinition.ArchivedStatus;
            publishedDefinition.UpdatedAt = now;
        }

        definition.PublishStatus = WorkflowDefinition.PublishedStatus;
        definition.IsEnabled = true;
        definition.PublishedAt = now;
        definition.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetRequiredDefinitionDtoAsync(id, cancellationToken);
    }

    public async Task<WorkflowDefinitionDto?> CreateNewVersionAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var source = await ApplyTenantScope(dbContext.WorkflowDefinitions.AsNoTracking())
            .Include(x => x.Nodes)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (source is null)
        {
            return null;
        }

        var existingVersions = await ApplyTenantScope(dbContext.WorkflowDefinitions.AsNoTracking())
            .Where(x => x.Code == source.Code)
            .Select(x => x.Version)
            .ToArrayAsync(cancellationToken);
        var nextVersion = (existingVersions.Length == 0 ? 0 : existingVersions.Max()) + 1;

        var now = DateTimeOffset.UtcNow;
        var definition = new WorkflowDefinition
        {
            Id = Guid.NewGuid(),
            TenantId = currentTenant.TenantId,
            Code = source.Code,
            Name = source.Name,
            FormName = source.FormName,
            Description = source.Description,
            DesignerJson = source.DesignerJson,
            FormSchemaJson = source.FormSchemaJson,
            IsEnabled = source.IsEnabled,
            Version = nextVersion,
            PublishStatus = WorkflowDefinition.DraftStatus,
            CreatedAt = now,
            UpdatedAt = now,
            Nodes = source.Nodes
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Name)
                .Select(node => new WorkflowNode
                {
                    Id = Guid.NewGuid(),
                    Name = node.Name,
                    DesignerNodeId = node.DesignerNodeId,
                    NodeType = NormalizeNodeType(node.NodeType),
                    ApprovalMode = NormalizeApprovalMode(node.ApprovalMode),
                    SlaMinutes = node.SlaMinutes,
                    ApproverType = node.ApproverType,
                    ApproverUserId = node.ApproverUserId,
                    ApproverRoleId = node.ApproverRoleId,
                    Order = node.Order,
                    IsEnabled = node.IsEnabled
                })
                .ToList()
        };

        dbContext.WorkflowDefinitions.Add(definition);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetRequiredDefinitionDtoAsync(definition.Id, cancellationToken);
    }

    public async Task<bool> DeleteDefinitionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var hasInstances = await HasInstancesAsync(id, cancellationToken);
        if (hasInstances)
        {
            throw new WorkflowOperationException("已有流程实例，不能删除流程定义.");
        }

        var definition = await ApplyTenantScope(dbContext.WorkflowDefinitions)
            .Include(x => x.Nodes)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (definition is null)
        {
            return false;
        }

        dbContext.WorkflowNodes.RemoveRange(definition.Nodes);
        dbContext.WorkflowDefinitions.Remove(definition);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<WorkflowBusinessBindingDto> CreateBusinessBindingAsync(
        SaveWorkflowBusinessBindingRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedBusinessType = NormalizeBusinessType(request.BusinessType);
        await EnsureBusinessTypeUniqueAsync(null, normalizedBusinessType, cancellationToken);
        await EnsureDefinitionCanBindAsync(request.DefinitionId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var binding = new WorkflowBusinessBinding
        {
            Id = Guid.NewGuid(),
            TenantId = currentTenant.TenantId,
            CreatedAt = now,
            UpdatedAt = now
        };
        ApplyBusinessBindingRequest(binding, request, normalizedBusinessType);

        dbContext.WorkflowBusinessBindings.Add(binding);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetRequiredBusinessBindingDtoAsync(binding.Id, cancellationToken);
    }

    public async Task<WorkflowBusinessBindingDto?> UpdateBusinessBindingAsync(
        Guid id,
        SaveWorkflowBusinessBindingRequest request,
        CancellationToken cancellationToken = default)
    {
        var binding = await ApplyTenantScope(dbContext.WorkflowBusinessBindings)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (binding is null)
        {
            return null;
        }

        var normalizedBusinessType = NormalizeBusinessType(request.BusinessType);
        await EnsureBusinessTypeUniqueAsync(id, normalizedBusinessType, cancellationToken);
        await EnsureDefinitionCanBindAsync(request.DefinitionId, cancellationToken);
        ApplyBusinessBindingRequest(binding, request, normalizedBusinessType);
        binding.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetRequiredBusinessBindingDtoAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteBusinessBindingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var binding = await ApplyTenantScope(dbContext.WorkflowBusinessBindings)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (binding is null)
        {
            return false;
        }

        dbContext.WorkflowBusinessBindings.Remove(binding);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<PageResult<WorkflowInstanceDto>> GetInstancesAsync(
        WorkflowInstanceListQuery query,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var instancesQuery = ApplyInstanceListFilters(BuildInstanceQuery(), query);

        if (query.Scope.Equals("startedByMe", StringComparison.OrdinalIgnoreCase))
        {
            instancesQuery = instancesQuery.Where(x => x.InitiatorUserId == user.UserId);
        }
        else if (!user.CanManageAllWorkflowInstances)
        {
            instancesQuery = ApplyParticipantVisibility(instancesQuery, user);
        }

        var total = await instancesQuery.CountAsync(cancellationToken);
        var instances = await instancesQuery
            .OrderByDescending(x => x.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        return new PageResult<WorkflowInstanceDto>(
            instances.Select(ToInstanceDto).ToArray(),
            total);
    }

    public async Task<PageResult<WorkflowInstanceDto>> GetCcInstancesAsync(
        WorkflowInstanceListQuery query,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var instancesQuery = ApplyInstanceListFilters(BuildInstanceQuery(), query)
            .Where(x => x.ActionLogs.Any(log =>
                log.Action == "Cc" &&
                log.OperatorUserId == user.UserId));

        var total = await instancesQuery.CountAsync(cancellationToken);
        var instances = await instancesQuery
            .OrderByDescending(x => x.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        return new PageResult<WorkflowInstanceDto>(
            instances.Select(ToInstanceDto).ToArray(),
            total);
    }

    public async Task<PageResult<WorkflowCcRecordDto>> GetCcRecordsAsync(
        WorkflowCcListQuery query,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var recordsQuery = ApplyCcRecordListFilters(
                ApplyTenantScope(dbContext.WorkflowCcRecords.AsNoTracking())
                    .Include(x => x.Instance),
                query)
            .Where(x => x.RecipientUserId == user.UserId);

        var total = await recordsQuery.CountAsync(cancellationToken);
        var records = await recordsQuery
            .OrderBy(x => x.ReadAt != null)
            .ThenByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        return new PageResult<WorkflowCcRecordDto>(
            records.Select(ToCcRecordDto).ToArray(),
            total);
    }

    public async Task<WorkflowCcRecordDto?> MarkCcRecordAsReadAsync(
        Guid ccRecordId,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default)
    {
        var record = await ApplyTenantScope(dbContext.WorkflowCcRecords)
            .Include(x => x.Instance)
            .SingleOrDefaultAsync(
                x => x.Id == ccRecordId && x.RecipientUserId == user.UserId,
                cancellationToken);
        if (record is null)
        {
            return null;
        }

        record.ReadAt ??= DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToCcRecordDto(record);
    }

    public async Task<WorkflowInstanceDto?> GetInstanceAsync(
        Guid id,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default)
    {
        var instance = await BuildInstanceQuery()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return instance is null || !CanAccessInstance(instance, user)
            ? null
            : ToInstanceDto(instance);
    }

    public async Task<IReadOnlyList<WorkflowTaskDto>> GetTodoTasksAsync(
        WorkflowUserContext user,
        CancellationToken cancellationToken = default)
    {
        var tasks = await ApplyTenantScope(dbContext.WorkflowTasks.AsNoTracking())
            .Include(x => x.Instance)
            .Where(x => x.ApproverUserId == user.UserId && x.Status == "Pending")
            .OrderBy(x => x.CreatedAt)
            .ToArrayAsync(cancellationToken);

        return tasks.Select(ToTaskDto).ToArray();
    }

    public async Task<IReadOnlyList<WorkflowTaskDto>> GetDoneTasksAsync(
        WorkflowUserContext user,
        CancellationToken cancellationToken = default)
    {
        var tasks = await ApplyTenantScope(dbContext.WorkflowTasks.AsNoTracking())
            .Include(x => x.Instance)
            .Where(x => x.ApproverUserId == user.UserId && x.Status != "Pending")
            .OrderByDescending(x => x.CompletedAt ?? x.CreatedAt)
            .Take(100)
            .ToArrayAsync(cancellationToken);

        return tasks.Select(ToTaskDto).ToArray();
    }

    public async Task<WorkflowInstanceDto> StartInstanceAsync(
        StartWorkflowInstanceRequest request,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default)
    {
        var definition = await ApplyTenantScope(dbContext.WorkflowDefinitions.AsNoTracking())
            .Include(x => x.Nodes)
            .SingleOrDefaultAsync(
                x =>
                    x.Id == request.DefinitionId &&
                    x.IsEnabled &&
                    x.PublishStatus == WorkflowDefinition.PublishedStatus,
                cancellationToken);
        if (definition is null)
        {
            throw new WorkflowOperationException("流程定义不存在、未启用或未发布.");
        }

        var formDataJson = NormalizeFormData(request.FormDataJson);
        ValidateFormDataAgainstSchema(definition.FormSchemaJson, formDataJson);
        var now = DateTimeOffset.UtcNow;
        var instance = new WorkflowInstance
        {
            Id = Guid.NewGuid(),
            TenantId = currentTenant.TenantId,
            DefinitionId = definition.Id,
            DefinitionCode = definition.Code,
            DefinitionName = definition.Name,
            DefinitionVersion = definition.Version,
            DefinitionSnapshotJson = BuildDefinitionSnapshotJson(definition),
            Title = request.Title.Trim(),
            BusinessKey = NormalizeOptional(request.BusinessKey),
            FormDataJson = formDataJson,
            Status = "Pending",
            InitiatorUserId = user.UserId,
            InitiatorUserName = user.UserName,
            StartedAt = now
        };
        dbContext.WorkflowInstances.Add(instance);
        dbContext.WorkflowActionLogs.Add(CreateActionLog(instance, null, "Start", user, "发起审批"));
        await AddAttachmentLinksAsync(instance, request.AttachmentFileIds, user, null, cancellationToken);

        var firstNode = await ResolveNextRuntimeNodeAsync(
            definition,
            "start",
            null,
            formDataJson,
            instance,
            user,
            cancellationToken);
        if (firstNode is null)
        {
            throw new WorkflowOperationException("流程定义没有可用审批节点.");
        }

        instance.CurrentNodeId = firstNode.Id;
        instance.CurrentNodeName = firstNode.Name;

        await CreatePendingTasksAsync(instance, firstNode, user, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (await GetInstanceAsync(instance.Id, user, cancellationToken))!;
    }

    public async Task<WorkflowInstanceDto?> AddAttachmentAsync(
        Guid instanceId,
        WorkflowAttachmentRequest request,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default)
    {
        var instance = await ApplyTenantScope(dbContext.WorkflowInstances)
            .Include(x => x.Attachments)
            .Include(x => x.Tasks)
            .Include(x => x.ActionLogs)
            .SingleOrDefaultAsync(x => x.Id == instanceId, cancellationToken);
        if (instance is null)
        {
            return null;
        }

        EnsureCanAccessInstance(instance, user);
        await AddAttachmentLinkAsync(instance, request.FileId, user, request.Remark, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetInstanceAsync(instanceId, user, cancellationToken);
    }

    public async Task<WorkflowCommentDto?> AddCommentAsync(
        Guid instanceId,
        WorkflowCommentRequest request,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default)
    {
        var instance = await ApplyTenantScope(dbContext.WorkflowInstances)
            .Include(x => x.Tasks)
            .Include(x => x.ActionLogs)
            .SingleOrDefaultAsync(x => x.Id == instanceId, cancellationToken);
        if (instance is null)
        {
            return null;
        }

        EnsureCanAccessInstance(instance, user);
        var content = NormalizeCommentContent(request.Content);
        var comment = new WorkflowComment
        {
            Id = Guid.NewGuid(),
            InstanceId = instance.Id,
            Content = content,
            AuthorUserId = user.UserId,
            AuthorUserName = user.UserName,
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.WorkflowComments.Add(comment);
        dbContext.WorkflowActionLogs.Add(CreateActionLog(instance, null, "Comment", user, content));

        await QueueWorkflowCommentNotificationsAsync(instance, comment, user, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToCommentDto(comment);
    }

    public async Task<WorkflowAttachmentDownloadDto?> GetAttachmentDownloadAsync(
        Guid instanceId,
        Guid attachmentId,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default)
    {
        var instance = await BuildInstanceQuery()
            .SingleOrDefaultAsync(x => x.Id == instanceId, cancellationToken);
        if (instance is null || !CanAccessInstance(instance, user))
        {
            return null;
        }

        var attachment = instance.Attachments.SingleOrDefault(x => x.Id == attachmentId);
        return attachment is null
            ? null
            : new WorkflowAttachmentDownloadDto(
                attachment.Id.ToString(),
                attachment.InstanceId.ToString(),
                attachment.FileId.ToString(),
                attachment.File.OriginalName,
                attachment.File.ContentType);
    }

    public Task<WorkflowInstanceDto?> ApproveAsync(
        Guid instanceId,
        WorkflowActionRequest request,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default)
    {
        return CompleteTaskAsync(instanceId, request, user, true, cancellationToken);
    }

    public Task<WorkflowInstanceDto?> RejectAsync(
        Guid instanceId,
        WorkflowActionRequest request,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default)
    {
        return CompleteTaskAsync(instanceId, request, user, false, cancellationToken);
    }

    public async Task<WorkflowTaskDto?> TransferTaskAsync(
        Guid taskId,
        WorkflowTransferTaskRequest request,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default)
    {
        if (request.TargetUserId == user.UserId)
        {
            throw new WorkflowOperationException("不能把待办转办给自己.");
        }

        var targetUser = await ApplyTenantScope(dbContext.Users.AsNoTracking())
            .SingleOrDefaultAsync(x => x.Id == request.TargetUserId && x.IsEnabled, cancellationToken);
        if (targetUser is null)
        {
            throw new WorkflowOperationException("转办接收人不存在、已禁用或不属于当前流程范围.");
        }

        var task = await ApplyTenantScope(dbContext.WorkflowTasks)
            .Include(x => x.Instance)
            .SingleOrDefaultAsync(x => x.Id == taskId, cancellationToken);
        if (task is null)
        {
            return null;
        }

        if (task.Status != "Pending")
        {
            throw new WorkflowOperationException("只有待办中的任务可以转办.");
        }

        if (task.ApproverUserId != user.UserId)
        {
            throw new WorkflowOperationException("只能转办自己的待办任务.");
        }

        var comment = NormalizeOptional(request.Comment);
        var transferLog = CreateActionLog(
            task.Instance,
            task,
            "Transfer",
            user,
            string.IsNullOrWhiteSpace(comment)
                ? $"转办给 {targetUser.UserName}"
                : $"转办给 {targetUser.UserName}: {comment}");
        dbContext.WorkflowActionLogs.Add(transferLog);

        task.ApproverUserId = targetUser.Id;
        task.ApproverUserName = targetUser.UserName;
        task.Comment = comment;
        await QueueWorkflowTransferNotificationAsync(task.Instance, task, targetUser, user, transferLog, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToTaskDto(task);
    }

    public async Task<WorkflowTaskDto?> RemindTaskAsync(
        Guid taskId,
        WorkflowRemindTaskRequest request,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default)
    {
        var task = await ApplyTenantScope(dbContext.WorkflowTasks)
            .Include(x => x.Instance)
            .SingleOrDefaultAsync(x => x.Id == taskId, cancellationToken);
        if (task is null)
        {
            return null;
        }

        if (task.Status != "Pending")
        {
            throw new WorkflowOperationException("只有待办中的任务可以催办.");
        }

        if (task.Instance.InitiatorUserId != user.UserId && task.ApproverUserId != user.UserId)
        {
            throw new WorkflowOperationException("只能催办自己发起或自己待处理的审批任务.");
        }

        var remindLog = CreateActionLog(
            task.Instance,
            task,
            "Remind",
            user,
            NormalizeOptional(request.Comment) ?? $"由 {user.UserName} 催办");
        dbContext.WorkflowActionLogs.Add(remindLog);
        await QueueWorkflowRemindNotificationAsync(task.Instance, task, user, remindLog, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToTaskDto(task);
    }

    public async Task<WorkflowSlaScanResultDto> ScanOverdueTasksAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var overdueTasks = await ApplyTenantScope(dbContext.WorkflowTasks)
            .Include(x => x.Instance)
            .Where(x => x.Status == "Pending" && x.DueAt.HasValue && x.DueAt.Value <= now)
            .OrderBy(x => x.DueAt)
            .Take(100)
            .ToArrayAsync(cancellationToken);
        var remindedDetails = new List<WorkflowSlaScanDetailDto>();
        var systemUser = new WorkflowUserContext(Guid.Empty, "system");

        foreach (var task in overdueTasks)
        {
            if (task.LastAutoRemindedAt.HasValue)
            {
                continue;
            }

            var dueAt = task.DueAt!.Value;
            var overdueLog = new WorkflowActionLog
            {
                Id = Guid.NewGuid(),
                InstanceId = task.InstanceId,
                NodeId = task.NodeId,
                NodeName = task.NodeName,
                Action = "Overdue",
                OperatorUserId = systemUser.UserId,
                OperatorUserName = systemUser.UserName,
                Comment = $"任务已超时，截止时间：{dueAt:yyyy-MM-dd HH:mm}",
                CreatedAt = now
            };
            dbContext.WorkflowActionLogs.Add(overdueLog);
            task.LastAutoRemindedAt = now;
            await QueueWorkflowOverdueNotificationAsync(task.Instance, task, systemUser, overdueLog, cancellationToken);
            remindedDetails.Add(new WorkflowSlaScanDetailDto(
                task.Id.ToString(),
                task.InstanceId.ToString(),
                task.Instance.Title,
                task.NodeName,
                task.ApproverUserId.ToString(),
                task.ApproverUserName,
                dueAt));
        }

        if (remindedDetails.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new WorkflowSlaScanResultDto(
            overdueTasks.Length,
            remindedDetails.Count,
            remindedDetails);
    }

    public async Task<WorkflowInstanceDto?> WithdrawAsync(
        Guid instanceId,
        WorkflowActionRequest request,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default)
    {
        var instance = await ApplyTenantScope(dbContext.WorkflowInstances)
            .Include(x => x.Tasks)
            .SingleOrDefaultAsync(x => x.Id == instanceId, cancellationToken);
        if (instance is null)
        {
            return null;
        }

        if (instance.InitiatorUserId != user.UserId)
        {
            throw new WorkflowOperationException("只有发起人可以撤回流程.");
        }

        if (instance.Status != "Pending")
        {
            throw new WorkflowOperationException("只有审批中的流程可以撤回.");
        }

        var now = DateTimeOffset.UtcNow;
        instance.Status = "Withdrawn";
        instance.CompletedAt = now;
        instance.CurrentNodeId = null;
        instance.CurrentNodeName = null;
        foreach (var task in instance.Tasks.Where(x => x.Status == "Pending"))
        {
            task.Status = "Closed";
            task.Comment = "流程已撤回";
            task.CompletedAt = now;
        }

        var withdrawLog = CreateActionLog(instance, null, "Withdraw", user, request.Comment);
        dbContext.WorkflowActionLogs.Add(withdrawLog);
        await QueueWorkflowResultNotificationAsync(
            instance,
            instance.InitiatorUserId,
            $"审批撤回：{instance.Title}",
            $"流程“{instance.Title}”已由 {user.UserName} 撤回。",
            "WorkflowWithdraw",
            withdrawLog.Id.ToString(),
            withdrawLog,
            user,
            null,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await GetInstanceAsync(instanceId, user, cancellationToken);
    }

    private async Task<WorkflowInstanceDto?> CompleteTaskAsync(
        Guid instanceId,
        WorkflowActionRequest request,
        WorkflowUserContext user,
        bool approved,
        CancellationToken cancellationToken)
    {
        var instance = await ApplyTenantScope(dbContext.WorkflowInstances)
            .Include(x => x.Tasks)
            .ThenInclude(x => x.Node)
            .Include(x => x.Definition)
            .ThenInclude(x => x.Nodes)
            .SingleOrDefaultAsync(x => x.Id == instanceId, cancellationToken);
        if (instance is null)
        {
            return null;
        }

        if (instance.Status != "Pending" || !instance.CurrentNodeId.HasValue)
        {
            throw new WorkflowOperationException("流程当前不可审批.");
        }

        var task = instance.Tasks.SingleOrDefault(x =>
            x.NodeId == instance.CurrentNodeId.Value &&
            x.ApproverUserId == user.UserId &&
            x.Status == "Pending");
        if (task is null)
        {
            throw new WorkflowOperationException("当前用户没有该流程的待办任务.");
        }

        var now = DateTimeOffset.UtcNow;
        task.Status = approved ? "Approved" : "Rejected";
        task.Comment = NormalizeOptional(request.Comment);
        task.CompletedAt = now;

        var actionLog = CreateActionLog(
            instance,
            task,
            approved ? "Approve" : "Reject",
            user,
            request.Comment);
        dbContext.WorkflowActionLogs.Add(actionLog);

        if (!approved)
        {
            ClosePendingSiblingTasks(instance, task, now, "同节点其他审批人已驳回");
            instance.Status = "Rejected";
            instance.CompletedAt = now;
            instance.CurrentNodeId = null;
            instance.CurrentNodeName = null;
            await QueueWorkflowResultNotificationAsync(
                instance,
                instance.InitiatorUserId,
                $"审批驳回：{instance.Title}",
                $"流程“{instance.Title}”已被 {user.UserName} 驳回，节点“{task.NodeName}”。",
                "WorkflowReject",
                actionLog.Id.ToString(),
                actionLog,
                user,
                task,
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return await GetInstanceAsync(instanceId, user, cancellationToken);
        }

        if (IsAnyApprovalMode(task.Node))
        {
            ClosePendingSiblingTasks(instance, task, now, "同节点其他审批人已通过");
        }
        else if (HasPendingSiblingTasks(instance, task))
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return await GetInstanceAsync(instanceId, user, cancellationToken);
        }

        await QueueWorkflowResultNotificationAsync(
            instance,
            instance.InitiatorUserId,
            $"审批通过：{instance.Title}",
            $"流程“{instance.Title}”已由 {user.UserName} 审批通过，节点“{task.NodeName}”。",
            "WorkflowApprove",
            actionLog.Id.ToString(),
            actionLog,
            user,
            task,
            cancellationToken);

        var nextNode = await ResolveNextRuntimeNodeAsync(
            instance.Definition,
            task.Node.DesignerNodeId,
            task.Node,
            instance.FormDataJson,
            instance,
            user,
            cancellationToken);
        if (nextNode is null)
        {
            instance.Status = "Approved";
            instance.CompletedAt = now;
            instance.CurrentNodeId = null;
            instance.CurrentNodeName = null;
        }
        else
        {
            instance.CurrentNodeId = nextNode.Id;
            instance.CurrentNodeName = nextNode.Name;
            await CreatePendingTasksAsync(instance, nextNode, user, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetInstanceAsync(instanceId, user, cancellationToken);
    }

    private static bool IsAnyApprovalMode(WorkflowNode node)
    {
        return !node.ApprovalMode.Equals("All", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasPendingSiblingTasks(WorkflowInstance instance, WorkflowTask task)
    {
        return instance.Tasks.Any(x =>
            x.Id != task.Id &&
            x.NodeId == task.NodeId &&
            x.Status == "Pending");
    }

    private static void ClosePendingSiblingTasks(
        WorkflowInstance instance,
        WorkflowTask task,
        DateTimeOffset completedAt,
        string comment)
    {
        foreach (var siblingTask in instance.Tasks.Where(x =>
            x.Id != task.Id &&
            x.NodeId == task.NodeId &&
            x.Status == "Pending"))
        {
            siblingTask.Status = "Closed";
            siblingTask.Comment = comment;
            siblingTask.CompletedAt = completedAt;
        }
    }

    private async Task CreatePendingTasksAsync(
        WorkflowInstance instance,
        WorkflowNode node,
        WorkflowUserContext operatorUser,
        CancellationToken cancellationToken)
    {
        var approvers = await ResolveApproversAsync(node, cancellationToken);
        if (approvers.Count == 0)
        {
            var reason = await BuildUnavailableApproverReasonAsync(node, operatorUser, cancellationToken);
            throw new WorkflowOperationException(reason);
        }

        foreach (var approver in approvers)
        {
            var createdAt = DateTimeOffset.UtcNow;
            var task = new WorkflowTask
            {
                Id = Guid.NewGuid(),
                InstanceId = instance.Id,
                NodeId = node.Id,
                NodeName = node.Name,
                ApproverUserId = approver.Id,
                ApproverUserName = approver.UserName,
                Status = "Pending",
                CreatedAt = createdAt,
                DueAt = node.SlaMinutes is > 0
                    ? createdAt.AddMinutes(node.SlaMinutes.Value)
                    : null
            };
            dbContext.WorkflowTasks.Add(task);
            await QueueWorkflowTaskNotificationAsync(instance, node, task, approver.Id, cancellationToken);
        }
    }

    private async Task AddAttachmentLinksAsync(
        WorkflowInstance instance,
        IReadOnlyList<Guid>? fileIds,
        WorkflowUserContext user,
        string? remark,
        CancellationToken cancellationToken)
    {
        if (fileIds is null || fileIds.Count == 0)
        {
            return;
        }

        foreach (var fileId in fileIds.Where(x => x != Guid.Empty).Distinct())
        {
            await AddAttachmentLinkAsync(instance, fileId, user, remark, cancellationToken);
        }
    }

    private async Task AddAttachmentLinkAsync(
        WorkflowInstance instance,
        Guid fileId,
        WorkflowUserContext user,
        string? remark,
        CancellationToken cancellationToken)
    {
        if (instance.Attachments.Any(x => x.FileId == fileId) ||
            dbContext.WorkflowAttachments.Local.Any(x => x.InstanceId == instance.Id && x.FileId == fileId))
        {
            return;
        }

        var exists = await dbContext.WorkflowAttachments
            .AsNoTracking()
            .AnyAsync(x => x.InstanceId == instance.Id && x.FileId == fileId, cancellationToken);
        if (exists)
        {
            return;
        }

        var file = await dbContext.ManagedFiles
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == fileId && x.Status == "Normal", cancellationToken);
        if (file is null)
        {
            throw new WorkflowOperationException("附件文件不存在或不可用.");
        }

        var normalizedRemark = NormalizeOptional(remark);
        dbContext.WorkflowAttachments.Add(new WorkflowAttachment
        {
            Id = Guid.NewGuid(),
            InstanceId = instance.Id,
            FileId = file.Id,
            Remark = normalizedRemark,
            UploaderUserId = user.UserId,
            UploaderUserName = user.UserName,
            CreatedAt = DateTimeOffset.UtcNow
        });
        dbContext.WorkflowActionLogs.Add(CreateActionLog(
            instance,
            null,
            "Attach",
            user,
            string.IsNullOrWhiteSpace(normalizedRemark)
                ? $"添加附件：{file.OriginalName}"
                : $"添加附件：{file.OriginalName}，备注：{normalizedRemark}"));
    }

    private async Task<List<User>> ResolveApproversAsync(
        WorkflowNode node,
        CancellationToken cancellationToken)
    {
        if (node.ApproverType.Equals("User", StringComparison.OrdinalIgnoreCase))
        {
            return await ApplyTenantScope(dbContext.Users.AsNoTracking())
                .Where(x => x.Id == node.ApproverUserId && x.IsEnabled)
                .ToListAsync(cancellationToken);
        }

        if (node.ApproverType.Equals("Role", StringComparison.OrdinalIgnoreCase))
        {
            return await ApplyTenantScope(dbContext.UserRoles.AsNoTracking())
                .Where(x => x.RoleId == node.ApproverRoleId && x.User.IsEnabled && x.Role.IsEnabled)
                .Select(x => x.User)
                .Distinct()
                .OrderBy(x => x.UserName)
                .ToListAsync(cancellationToken);
        }

        return [];
    }

    private async Task<string> BuildUnavailableApproverReasonAsync(
        WorkflowNode node,
        WorkflowUserContext operatorUser,
        CancellationToken cancellationToken)
    {
        var scopeName = GetWorkflowScopeName();
        var operatorHint = $"当前登录用户 {operatorUser.UserName} 不能代替节点审批人审批；如需 Admin 审批，请把该节点审批人改为 Admin 后保存并重新发起流程。";

        if (node.ApproverType.Equals("User", StringComparison.OrdinalIgnoreCase))
        {
            if (!node.ApproverUserId.HasValue)
            {
                return $"节点“{node.Name}”没有配置审批人。{operatorHint}";
            }

            var user = await dbContext.Users.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == node.ApproverUserId.Value, cancellationToken);
            if (user is null)
            {
                return $"节点“{node.Name}”配置的审批用户不存在。{operatorHint}";
            }

            var userName = FormatUser(user);
            if (!user.IsEnabled)
            {
                return $"节点“{node.Name}”配置的审批用户 {userName} 已被禁用，无法生成待办任务。{operatorHint}";
            }

            if (!IsInCurrentTenantScope(user.TenantId))
            {
                return $"节点“{node.Name}”配置的审批用户 {userName} 不属于{scopeName}，无法生成待办任务。{operatorHint}";
            }

            return $"节点“{node.Name}”配置的审批用户 {userName} 当前不可用，无法生成待办任务。{operatorHint}";
        }

        if (node.ApproverType.Equals("Role", StringComparison.OrdinalIgnoreCase))
        {
            if (!node.ApproverRoleId.HasValue)
            {
                return $"节点“{node.Name}”没有配置审批角色。{operatorHint}";
            }

            var role = await dbContext.Roles.AsNoTracking()
                .Include(x => x.UserRoles)
                .ThenInclude(x => x.User)
                .SingleOrDefaultAsync(x => x.Id == node.ApproverRoleId.Value, cancellationToken);
            if (role is null)
            {
                return $"节点“{node.Name}”配置的审批角色不存在。{operatorHint}";
            }

            var roleName = FormatRole(role);
            if (!role.IsEnabled)
            {
                return $"节点“{node.Name}”配置的审批角色 {roleName} 已被禁用，无法生成待办任务。{operatorHint}";
            }

            if (!IsInCurrentTenantScope(role.TenantId))
            {
                return $"节点“{node.Name}”配置的审批角色 {roleName} 不属于{scopeName}，无法生成待办任务。{operatorHint}";
            }

            var hasEnabledUserInScope = role.UserRoles.Any(userRole =>
                userRole.User.IsEnabled &&
                IsInCurrentTenantScope(userRole.User.TenantId));
            if (!hasEnabledUserInScope)
            {
                return $"节点“{node.Name}”配置的审批角色 {roleName} 在{scopeName}内没有启用用户，无法生成待办任务。{operatorHint}";
            }

            return $"节点“{node.Name}”配置的审批角色 {roleName} 当前不可用，无法生成待办任务。{operatorHint}";
        }

        return $"节点“{node.Name}”的审批类型“{node.ApproverType}”暂不支持，无法生成待办任务。{operatorHint}";
    }

    private bool IsInCurrentTenantScope(Guid? tenantId)
    {
        return currentTenant.IsTenant
            ? tenantId == currentTenant.TenantId
            : tenantId is null;
    }

    private string GetWorkflowScopeName()
    {
        return currentTenant.IsTenant ? "当前租户流程" : "平台流程";
    }

    private IQueryable<WorkflowInstance> BuildInstanceQuery()
    {
        return ApplyTenantScope(dbContext.WorkflowInstances.AsNoTracking())
            .Include(x => x.Tasks.OrderBy(task => task.CreatedAt))
            .Include(x => x.ActionLogs.OrderBy(log => log.CreatedAt))
            .Include(x => x.CcRecords.OrderBy(record => record.CreatedAt))
            .Include(x => x.Attachments.OrderBy(attachment => attachment.CreatedAt))
            .ThenInclude(x => x.File)
            .Include(x => x.Comments.OrderBy(comment => comment.CreatedAt));
    }

    private static IQueryable<WorkflowInstance> ApplyInstanceListFilters(
        IQueryable<WorkflowInstance> query,
        WorkflowInstanceListQuery listQuery)
    {
        if (!string.IsNullOrWhiteSpace(listQuery.Keyword))
        {
            var keyword = listQuery.Keyword.Trim();
            query = query.Where(x =>
                x.Title.Contains(keyword) ||
                x.DefinitionName.Contains(keyword) ||
                (x.BusinessKey != null && x.BusinessKey.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(listQuery.Status))
        {
            query = query.Where(x => x.Status == listQuery.Status);
        }

        return query;
    }

    private static IQueryable<WorkflowCcRecord> ApplyCcRecordListFilters(
        IQueryable<WorkflowCcRecord> query,
        WorkflowCcListQuery listQuery)
    {
        if (!string.IsNullOrWhiteSpace(listQuery.Keyword))
        {
            var keyword = listQuery.Keyword.Trim();
            query = query.Where(x =>
                x.Instance.Title.Contains(keyword) ||
                x.Instance.DefinitionName.Contains(keyword) ||
                (x.Instance.BusinessKey != null && x.Instance.BusinessKey.Contains(keyword)) ||
                (x.NodeName != null && x.NodeName.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(listQuery.InstanceStatus))
        {
            query = query.Where(x => x.Instance.Status == listQuery.InstanceStatus);
        }

        if (listQuery.ReadStatus?.Equals("read", StringComparison.OrdinalIgnoreCase) == true)
        {
            query = query.Where(x => x.ReadAt != null);
        }
        else if (listQuery.ReadStatus?.Equals("unread", StringComparison.OrdinalIgnoreCase) == true)
        {
            query = query.Where(x => x.ReadAt == null);
        }

        return query;
    }

    private IQueryable<WorkflowDefinition> ApplyTenantScope(IQueryable<WorkflowDefinition> query)
    {
        return currentTenant.IsTenant
            ? query.Where(x => x.TenantId == currentTenant.TenantId)
            : query.Where(x => x.TenantId == null);
    }

    private IQueryable<WorkflowInstance> ApplyTenantScope(IQueryable<WorkflowInstance> query)
    {
        return currentTenant.IsTenant
            ? query.Where(x => x.TenantId == currentTenant.TenantId)
            : query.Where(x => x.TenantId == null);
    }

    private static IQueryable<WorkflowInstance> ApplyParticipantVisibility(
        IQueryable<WorkflowInstance> query,
        WorkflowUserContext user)
    {
        return query.Where(x =>
            x.InitiatorUserId == user.UserId ||
            x.Tasks.Any(task => task.ApproverUserId == user.UserId) ||
            x.CcRecords.Any(record => record.RecipientUserId == user.UserId) ||
            x.ActionLogs.Any(log =>
                log.Action == "Cc" &&
                log.OperatorUserId == user.UserId));
    }

    private IQueryable<WorkflowTask> ApplyTenantScope(IQueryable<WorkflowTask> query)
    {
        return currentTenant.IsTenant
            ? query.Where(x => x.Instance.TenantId == currentTenant.TenantId)
            : query.Where(x => x.Instance.TenantId == null);
    }

    private IQueryable<WorkflowCcRecord> ApplyTenantScope(IQueryable<WorkflowCcRecord> query)
    {
        return currentTenant.IsTenant
            ? query.Where(x => x.Instance.TenantId == currentTenant.TenantId)
            : query.Where(x => x.Instance.TenantId == null);
    }

    private IQueryable<WorkflowBusinessBinding> ApplyTenantScope(IQueryable<WorkflowBusinessBinding> query)
    {
        return currentTenant.IsTenant
            ? query.Where(x => x.TenantId == currentTenant.TenantId)
            : query.Where(x => x.TenantId == null);
    }

    private IQueryable<User> ApplyTenantScope(IQueryable<User> query)
    {
        return currentTenant.IsTenant
            ? query.Where(x => x.TenantId == currentTenant.TenantId)
            : query.Where(x => x.TenantId == null);
    }

    private IQueryable<UserRole> ApplyTenantScope(IQueryable<UserRole> query)
    {
        return currentTenant.IsTenant
            ? query.Where(x => x.User.TenantId == currentTenant.TenantId && x.Role.TenantId == currentTenant.TenantId)
            : query.Where(x => x.User.TenantId == null && x.Role.TenantId == null);
    }

    private async Task EnsureCodeCanCreateAsync(
        string code,
        CancellationToken cancellationToken)
    {
        var normalizedCode = code.Trim();
        var exists = await ApplyTenantScope(dbContext.WorkflowDefinitions.AsNoTracking())
            .AnyAsync(x => x.Code == normalizedCode, cancellationToken);
        if (exists)
        {
            throw new WorkflowOperationException("流程编码已存在，请从已有流程创建新版本。");
        }
    }

    private async Task EnsureCodeVersionUniqueAsync(
        Guid? id,
        string code,
        int version,
        CancellationToken cancellationToken)
    {
        var normalizedCode = code.Trim();
        var exists = await ApplyTenantScope(dbContext.WorkflowDefinitions.AsNoTracking())
            .AnyAsync(
                x =>
                    x.Code == normalizedCode &&
                    x.Version == version &&
                    (!id.HasValue || x.Id != id.Value),
                cancellationToken);
        if (exists)
        {
            throw new WorkflowOperationException("流程编码和版本已存在.");
        }
    }

    private async Task<bool> HasInstancesAsync(Guid definitionId, CancellationToken cancellationToken)
    {
        return await ApplyTenantScope(dbContext.WorkflowInstances.AsNoTracking())
            .AnyAsync(x => x.DefinitionId == definitionId, cancellationToken);
    }

    private async Task EnsureBusinessTypeUniqueAsync(
        Guid? id,
        string businessType,
        CancellationToken cancellationToken)
    {
        var exists = await ApplyTenantScope(dbContext.WorkflowBusinessBindings.AsNoTracking())
            .AnyAsync(
                x =>
                    x.BusinessType == businessType &&
                    (!id.HasValue || x.Id != id.Value),
                cancellationToken);
        if (exists)
        {
            throw new WorkflowOperationException("业务类型已绑定流程，请编辑已有绑定。");
        }
    }

    private async Task EnsureDefinitionCanBindAsync(
        Guid definitionId,
        CancellationToken cancellationToken)
    {
        var definition = await ApplyTenantScope(dbContext.WorkflowDefinitions.AsNoTracking())
            .SingleOrDefaultAsync(x => x.Id == definitionId, cancellationToken);
        if (definition is null)
        {
            throw new WorkflowOperationException("流程定义不存在.");
        }

        if (!definition.IsEnabled ||
            definition.PublishStatus != WorkflowDefinition.PublishedStatus)
        {
            throw new WorkflowOperationException("只能绑定已发布且启用的流程定义。");
        }
    }

    private async Task<WorkflowDefinitionDto> GetRequiredDefinitionDtoAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var definition = await ApplyTenantScope(dbContext.WorkflowDefinitions.AsNoTracking())
            .Include(x => x.Nodes)
            .ThenInclude(x => x.ApproverUser)
            .Include(x => x.Nodes)
            .ThenInclude(x => x.ApproverRole)
            .SingleAsync(x => x.Id == id, cancellationToken);

        return ToDefinitionDto(definition);
    }

    private async Task<WorkflowBusinessBindingDto> GetRequiredBusinessBindingDtoAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var binding = await ApplyTenantScope(dbContext.WorkflowBusinessBindings.AsNoTracking())
            .Include(x => x.Definition)
            .SingleAsync(x => x.Id == id, cancellationToken);

        return ToBusinessBindingDto(binding);
    }

    private static void ApplyDefinitionRequest(
        WorkflowDefinition definition,
        SaveWorkflowDefinitionRequest request)
    {
        var normalizedNodes = request.Nodes
            .Select(node => new NormalizedWorkflowNodeInput(
                node,
                NormalizeDesignerNodeId(node.DesignerNodeId)))
            .ToArray();

        definition.Code = request.Code.Trim();
        definition.Name = request.Name.Trim();
        definition.FormName = NormalizeOptional(request.FormName);
        definition.Description = NormalizeOptional(request.Description);
        definition.DesignerJson = NormalizeDesignerJson(request.DesignerJson, normalizedNodes);
        definition.FormSchemaJson = NormalizeFormSchemaJson(request.FormSchemaJson);
        definition.IsEnabled = request.IsEnabled;
        definition.Nodes = normalizedNodes
            .OrderBy(x => x.Request.Order)
            .ThenBy(x => x.Request.Name)
            .Select(node => new WorkflowNode
            {
                Id = Guid.NewGuid(),
                DefinitionId = definition.Id,
                Name = node.Request.Name.Trim(),
                DesignerNodeId = node.DesignerNodeId,
                NodeType = NormalizeNodeType(node.Request.NodeType),
                ApprovalMode = NormalizeApprovalMode(node.Request.ApprovalMode),
                SlaMinutes = node.Request.SlaMinutes is > 0 ? node.Request.SlaMinutes : null,
                ApproverType = node.Request.ApproverType.Trim(),
                ApproverUserId = node.Request.ApproverType.Equals("User", StringComparison.OrdinalIgnoreCase)
                    ? node.Request.ApproverUserId
                    : null,
                ApproverRoleId = node.Request.ApproverType.Equals("Role", StringComparison.OrdinalIgnoreCase)
                    ? node.Request.ApproverRoleId
                    : null,
                Order = node.Request.Order,
                IsEnabled = node.Request.IsEnabled
            })
            .ToList();
    }

    private static string BuildDefinitionSnapshotJson(WorkflowDefinition definition)
    {
        var snapshot = new WorkflowDefinitionSnapshot(
            definition.Id.ToString(),
            definition.Code,
            definition.Name,
            definition.FormName,
            definition.Version,
            definition.PublishStatus,
            definition.PublishedAt,
            definition.DesignerJson,
            definition.FormSchemaJson,
            definition.Nodes
                .Where(x => x.IsEnabled)
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Name)
                .Select(node => new WorkflowDefinitionSnapshotNode(
                    node.Id.ToString(),
                    node.Name,
                    node.DesignerNodeId,
                    NormalizeNodeType(node.NodeType),
                    NormalizeApprovalMode(node.ApprovalMode),
                    node.ApproverType,
                    node.ApproverUserId?.ToString(),
                    node.ApproverRoleId?.ToString(),
                    node.Order,
                    node.IsEnabled,
                    node.SlaMinutes))
                .ToArray());

        return JsonSerializer.Serialize(snapshot, JsonOptions);
    }

    private static void ApplyBusinessBindingRequest(
        WorkflowBusinessBinding binding,
        SaveWorkflowBusinessBindingRequest request,
        string normalizedBusinessType)
    {
        binding.BusinessType = normalizedBusinessType;
        binding.BusinessName = request.BusinessName.Trim();
        binding.DefinitionId = request.DefinitionId;
        binding.IsEnabled = request.IsEnabled;
        binding.Remark = NormalizeOptional(request.Remark);
    }

    private static WorkflowActionLog CreateActionLog(
        WorkflowInstance instance,
        WorkflowTask? task,
        string action,
        WorkflowUserContext user,
        string? comment)
    {
        return new WorkflowActionLog
        {
            Id = Guid.NewGuid(),
            InstanceId = instance.Id,
            NodeId = task?.NodeId,
            NodeName = task?.NodeName,
            Action = action,
            OperatorUserId = user.UserId,
            OperatorUserName = user.UserName,
            Comment = NormalizeOptional(comment),
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private static void EnsureCanAccessInstance(
        WorkflowInstance instance,
        WorkflowUserContext user)
    {
        if (!CanAccessInstance(instance, user))
        {
            throw new WorkflowOperationException("没有权限访问该流程.");
        }
    }

    private static bool CanAccessInstance(
        WorkflowInstance instance,
        WorkflowUserContext user)
    {
        if (user.CanManageAllWorkflowInstances)
        {
            return true;
        }

        return instance.InitiatorUserId == user.UserId ||
            instance.Tasks.Any(task => task.ApproverUserId == user.UserId) ||
            instance.CcRecords.Any(record => record.RecipientUserId == user.UserId) ||
            instance.ActionLogs.Any(log =>
                log.Action.Equals("Cc", StringComparison.OrdinalIgnoreCase) &&
                log.OperatorUserId == user.UserId);
    }

    private static string FormatUser(User user)
    {
        var displayName = string.IsNullOrWhiteSpace(user.RealName)
            ? user.UserName
            : user.RealName;
        return $"{displayName}({user.UserName})";
    }

    private static string FormatRole(Role role)
    {
        return $"{role.Name}({role.Code})";
    }

    private static WorkflowDefinitionDto ToDefinitionDto(WorkflowDefinition definition)
    {
        return new WorkflowDefinitionDto(
            definition.Id.ToString(),
            definition.Code,
            definition.Name,
            definition.FormName,
            definition.Description,
            definition.DesignerJson,
            definition.IsEnabled,
            definition.Version,
            definition.PublishStatus,
            definition.PublishedAt,
            definition.CreatedAt,
            definition.UpdatedAt,
            definition.Nodes
                .OrderBy(x => x.Order)
                .ThenBy(x => x.Name)
                .Select(ToNodeDto)
                .ToArray(),
            definition.FormSchemaJson);
    }

    private static WorkflowNodeDto ToNodeDto(WorkflowNode node)
    {
        return new WorkflowNodeDto(
            node.Id.ToString(),
            node.Name,
            node.DesignerNodeId,
            NormalizeNodeType(node.NodeType),
            NormalizeApprovalMode(node.ApprovalMode),
            node.ApproverType,
            node.ApproverUserId?.ToString(),
            node.ApproverUser?.UserName,
            node.ApproverRoleId?.ToString(),
            node.ApproverRole?.Name,
            node.Order,
            node.IsEnabled,
            node.SlaMinutes);
    }

    private static WorkflowBusinessBindingDto ToBusinessBindingDto(WorkflowBusinessBinding binding)
    {
        return new WorkflowBusinessBindingDto(
            binding.Id.ToString(),
            binding.BusinessType,
            binding.BusinessName,
            binding.DefinitionId.ToString(),
            binding.Definition.Code,
            binding.Definition.Name,
            binding.Definition.Version,
            binding.Definition.PublishStatus,
            binding.IsEnabled,
            binding.Remark,
            binding.CreatedAt,
            binding.UpdatedAt);
    }

    private static WorkflowBusinessDefinitionDto ToBusinessDefinitionDto(WorkflowBusinessBinding binding)
    {
        return new WorkflowBusinessDefinitionDto(
            binding.Id.ToString(),
            binding.BusinessType,
            binding.BusinessName,
            binding.DefinitionId.ToString(),
            binding.Definition.Code,
            binding.Definition.Name,
            binding.Definition.FormName,
            binding.Definition.Version);
    }

    private static WorkflowInstanceDto ToInstanceDto(WorkflowInstance instance)
    {
        return new WorkflowInstanceDto(
            instance.Id.ToString(),
            instance.DefinitionId.ToString(),
            instance.DefinitionCode,
            instance.DefinitionName,
            instance.DefinitionVersion,
            instance.DefinitionSnapshotJson,
            instance.Title,
            instance.BusinessKey,
            instance.FormDataJson,
            instance.Status,
            instance.CurrentNodeId?.ToString(),
            instance.CurrentNodeName,
            instance.InitiatorUserId.ToString(),
            instance.InitiatorUserName,
            instance.StartedAt,
            instance.CompletedAt,
            instance.Tasks
                .OrderBy(x => x.CreatedAt)
                .Select(ToTaskDto)
                .ToArray(),
            instance.ActionLogs
                .OrderBy(x => x.CreatedAt)
                .Select(ToActionLogDto)
                .ToArray(),
            instance.Attachments
                .OrderBy(x => x.CreatedAt)
                .Select(ToAttachmentDto)
                .ToArray(),
            instance.Comments
                .OrderBy(x => x.CreatedAt)
                .Select(ToCommentDto)
                .ToArray(),
            instance.CcRecords
                .OrderBy(x => x.CreatedAt)
                .Select(ToCcRecordDto)
                .ToArray());
    }

    private static WorkflowTaskDto ToTaskDto(WorkflowTask task)
    {
        return new WorkflowTaskDto(
            task.Id.ToString(),
            task.InstanceId.ToString(),
            task.Instance.Title,
            task.Instance.DefinitionName,
            task.NodeId.ToString(),
            task.NodeName,
            task.ApproverUserId.ToString(),
            task.ApproverUserName,
            task.Status,
            task.Comment,
            task.CreatedAt,
            task.CompletedAt,
            task.DueAt,
            task.LastAutoRemindedAt,
            IsTaskOverdue(task, DateTimeOffset.UtcNow));
    }

    private static WorkflowCcRecordDto ToCcRecordDto(WorkflowCcRecord record)
    {
        var isRead = record.ReadAt.HasValue;
        return new WorkflowCcRecordDto(
            record.Id.ToString(),
            record.InstanceId.ToString(),
            record.Instance.Title,
            record.Instance.DefinitionName,
            record.Instance.BusinessKey,
            record.Instance.Status,
            record.Instance.CurrentNodeName,
            record.NodeId?.ToString() ?? string.Empty,
            record.NodeName ?? string.Empty,
            record.RecipientUserId.ToString(),
            record.RecipientUserName,
            record.SenderUserId?.ToString(),
            record.SenderUserName,
            record.Instance.InitiatorUserId.ToString(),
            record.Instance.InitiatorUserName,
            record.Instance.StartedAt,
            record.CreatedAt,
            record.ReadAt,
            isRead,
            isRead ? "Read" : "Unread");
    }

    private static WorkflowActionLogDto ToActionLogDto(WorkflowActionLog log)
    {
        return new WorkflowActionLogDto(
            log.Id.ToString(),
            log.Action,
            log.NodeId?.ToString(),
            log.NodeName,
            log.OperatorUserId.ToString(),
            log.OperatorUserName,
            log.Comment,
            log.CreatedAt);
    }

    private static WorkflowAttachmentDto ToAttachmentDto(WorkflowAttachment attachment)
    {
        return new WorkflowAttachmentDto(
            attachment.Id.ToString(),
            attachment.InstanceId.ToString(),
            attachment.FileId.ToString(),
            attachment.File.OriginalName,
            attachment.File.ContentType,
            attachment.File.Size,
            attachment.File.StorageProvider,
            attachment.File.StoragePath,
            attachment.Remark,
            attachment.UploaderUserId.ToString(),
            attachment.UploaderUserName,
            attachment.CreatedAt);
    }

    private static WorkflowCommentDto ToCommentDto(WorkflowComment comment)
    {
        return new WorkflowCommentDto(
            comment.Id.ToString(),
            comment.InstanceId.ToString(),
            comment.Content,
            comment.AuthorUserId.ToString(),
            comment.AuthorUserName,
            comment.CreatedAt);
    }

    private static bool IsTaskOverdue(WorkflowTask task, DateTimeOffset now)
    {
        return task.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase) &&
            task.DueAt.HasValue &&
            task.DueAt.Value <= now;
    }

    private static string NormalizeFormData(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "{}" : value.Trim();
    }

    private static string NormalizeFormSchemaJson(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "[]" : value.Trim();
    }

    private static void ValidateFormDataAgainstSchema(string? formSchemaJson, string formDataJson)
    {
        var fields = ParseFormSchema(formSchemaJson);
        if (fields.Count == 0)
        {
            return;
        }

        using var document = ParseFormDataJson(formDataJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new WorkflowOperationException("表单数据必须是 JSON 对象.");
        }

        foreach (var field in fields)
        {
            var fieldCode = field.Field?.Trim();
            if (string.IsNullOrWhiteSpace(fieldCode))
            {
                continue;
            }

            var fieldLabel = string.IsNullOrWhiteSpace(field.Label) ? fieldCode : field.Label.Trim();
            var hasValue = document.RootElement.TryGetProperty(fieldCode, out var value);
            if (field.Required && (!hasValue || IsEmptyJsonValue(value)))
            {
                throw new WorkflowOperationException($"表单字段「{fieldLabel}」为必填项.");
            }

            if (!hasValue || IsEmptyJsonValue(value))
            {
                continue;
            }

            var component = NormalizeFormComponent(field.Component);
            if (component == "number" && !IsNumberJsonValue(value))
            {
                throw new WorkflowOperationException($"表单字段「{fieldLabel}」必须填写数字.");
            }

            if (component == "select" && !IsSelectValueAllowed(value, field.Options))
            {
                throw new WorkflowOperationException($"表单字段「{fieldLabel}」的值不在可选项中.");
            }
        }
    }

    private static JsonDocument ParseFormDataJson(string formDataJson)
    {
        try
        {
            return JsonDocument.Parse(NormalizeFormData(formDataJson));
        }
        catch (JsonException)
        {
            throw new WorkflowOperationException("表单数据 JSON 格式不正确.");
        }
    }

    private static IReadOnlyList<WorkflowFormSchemaField> ParseFormSchema(string? formSchemaJson)
    {
        if (string.IsNullOrWhiteSpace(formSchemaJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<WorkflowFormSchemaField>>(
                formSchemaJson,
                JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            throw new WorkflowOperationException("流程表单配置 JSON 格式不正确.");
        }
    }

    private static bool IsEmptyJsonValue(JsonElement value)
    {
        return value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
               value.ValueKind == JsonValueKind.String &&
               string.IsNullOrWhiteSpace(value.GetString());
    }

    private static bool IsNumberJsonValue(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.Number ||
               value.ValueKind == JsonValueKind.String &&
               decimal.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out _);
    }

    private static bool IsSelectValueAllowed(
        JsonElement value,
        IReadOnlyList<WorkflowFormSchemaOption>? options)
    {
        if (options is null || options.Count == 0)
        {
            return true;
        }

        var actualValue = JsonElementToString(value);
        return options
            .Where(option => !string.IsNullOrWhiteSpace(option.Value))
            .Any(option => string.Equals(option.Value!.Trim(), actualValue, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeFormComponent(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "text" : value.Trim().ToLowerInvariant();
    }

    private static string NormalizeDesignerJson(
        string? value,
        IReadOnlyList<NormalizedWorkflowNodeInput> nodes)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }

        var startNodeId = "start";
        var endNodeId = "end";
        var approveNodes = nodes
            .Where(x => x.Request.IsEnabled)
            .OrderBy(x => x.Request.Order)
            .Select((node, index) => new WorkflowDesignerNode(
                node.DesignerNodeId,
                "approve",
                node.Request.Name,
                260 + index * 220,
                140))
            .ToArray();
        var designerNodes = new List<WorkflowDesignerNode>
        {
            new(startNodeId, "start", "开始", 60, 140)
        };
        designerNodes.AddRange(approveNodes);
        designerNodes.Add(new WorkflowDesignerNode(endNodeId, "end", "结束", 300 + approveNodes.Length * 220, 140));

        var edges = new List<WorkflowDesignerEdge>();
        var previousNodeId = startNodeId;
        foreach (var node in approveNodes)
        {
            edges.Add(new WorkflowDesignerEdge($"edge-{previousNodeId}-{node.Id}", previousNodeId, node.Id));
            previousNodeId = node.Id;
        }

        edges.Add(new WorkflowDesignerEdge($"edge-{previousNodeId}-{endNodeId}", previousNodeId, endNodeId));

        return JsonSerializer.Serialize(new WorkflowDesignerGraph(designerNodes, edges), JsonOptions);
    }

    private static string NormalizeDesignerNodeId(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? $"approve-{Guid.NewGuid():N}" : value.Trim();
    }

    private static string NormalizeNodeType(string? value)
    {
        return string.Equals(value, "cc", StringComparison.OrdinalIgnoreCase) ? "cc" : "approve";
    }

    private static string NormalizeApprovalMode(string? value)
    {
        return string.Equals(value, "All", StringComparison.OrdinalIgnoreCase) ? "All" : "Any";
    }

    private static string NormalizeBusinessType(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeCommentContent(string value)
    {
        var content = value.Trim();
        return content.Length <= 2000 ? content : content[..2000];
    }

    private async Task QueueWorkflowTaskNotificationAsync(
        WorkflowInstance instance,
        WorkflowNode node,
        WorkflowTask task,
        Guid approverUserId,
        CancellationToken cancellationToken)
    {
        var variables = CreateWorkflowNotificationVariables(
            instance,
            node,
            task,
            null,
            null,
            null);
        await QueueNotificationAsync(
            approverUserId,
            $"待处理审批：{instance.Title}",
            $"你有一条待处理审批，流程“{instance.Title}”当前节点“{node.Name}”，请及时处理。",
            "Workflow",
            "Info",
            CreateWorkflowCenterLink(instance),
            "WorkflowTask",
            task.Id.ToString(),
            variables,
            cancellationToken);
    }

    private async Task QueueWorkflowTransferNotificationAsync(
        WorkflowInstance instance,
        WorkflowTask task,
        User targetUser,
        WorkflowUserContext operatorUser,
        WorkflowActionLog transferLog,
        CancellationToken cancellationToken)
    {
        var variables = CreateWorkflowNotificationVariables(
            instance,
            null,
            task,
            operatorUser,
            targetUser,
            task.Comment);
        await QueueNotificationAsync(
            targetUser.Id,
            $"审批转办：{instance.Title}",
            $"{operatorUser.UserName} 已将流程“{instance.Title}”节点“{task.NodeName}”转办给你，请及时处理。",
            "Workflow",
            "Info",
            CreateWorkflowCenterLink(instance, task),
            "WorkflowTransfer",
            transferLog.Id.ToString(),
            variables,
            cancellationToken);
    }

    private async Task QueueWorkflowRemindNotificationAsync(
        WorkflowInstance instance,
        WorkflowTask task,
        WorkflowUserContext operatorUser,
        WorkflowActionLog remindLog,
        CancellationToken cancellationToken)
    {
        var variables = CreateWorkflowNotificationVariables(
            instance,
            null,
            task,
            operatorUser,
            null,
            remindLog.Comment);
        await QueueNotificationAsync(
            task.ApproverUserId,
            $"审批催办：{instance.Title}",
            $"{operatorUser.UserName} 催办流程“{instance.Title}”节点“{task.NodeName}”，请及时处理。",
            "Workflow",
            "Warning",
            CreateWorkflowCenterLink(instance, task),
            "WorkflowRemind",
            remindLog.Id.ToString(),
            variables,
            cancellationToken);
    }

    private async Task QueueWorkflowOverdueNotificationAsync(
        WorkflowInstance instance,
        WorkflowTask task,
        WorkflowUserContext operatorUser,
        WorkflowActionLog overdueLog,
        CancellationToken cancellationToken)
    {
        var variables = CreateWorkflowNotificationVariables(
            instance,
            null,
            task,
            operatorUser,
            null,
            overdueLog.Comment);
        await QueueNotificationAsync(
            task.ApproverUserId,
            $"审批超时：{instance.Title}",
            $"流程“{instance.Title}”节点“{task.NodeName}”已超时，请尽快处理。",
            "Workflow",
            "Warning",
            CreateWorkflowCenterLink(instance, task),
            "WorkflowOverdue",
            task.Id.ToString(),
            variables,
            cancellationToken);
    }

    private async Task QueueWorkflowResultNotificationAsync(
        WorkflowInstance instance,
        Guid userId,
        string title,
        string message,
        string sourceType,
        string sourceId,
        WorkflowActionLog actionLog,
        WorkflowUserContext operatorUser,
        WorkflowTask? task,
        CancellationToken cancellationToken)
    {
        var variables = CreateWorkflowNotificationVariables(
            instance,
            null,
            task,
            operatorUser,
            null,
            actionLog.Comment);
        await QueueNotificationAsync(
            userId,
            title,
            message,
            "Workflow",
            "Info",
            CreateWorkflowCenterLink(instance, task),
            sourceType,
            sourceId,
            variables,
            cancellationToken);
    }

    private async Task QueueWorkflowCommentNotificationsAsync(
        WorkflowInstance instance,
        WorkflowComment comment,
        WorkflowUserContext author,
        CancellationToken cancellationToken)
    {
        var variables = CreateWorkflowNotificationVariables(
            instance,
            null,
            null,
            author,
            null,
            comment.Content);
        variables["commentId"] = comment.Id.ToString();

        foreach (var recipientUserId in GetWorkflowParticipantUserIds(instance).Where(x => x != author.UserId))
        {
            await QueueNotificationAsync(
                recipientUserId,
                $"流程评论：{instance.Title}",
                $"{author.UserName} 评论了流程“{instance.Title}”：{comment.Content}",
                "Workflow",
                "Info",
                CreateWorkflowCenterLink(instance),
                "WorkflowComment",
                comment.Id.ToString(),
                variables,
                cancellationToken);
        }
    }

    private static IReadOnlyCollection<Guid> GetWorkflowParticipantUserIds(WorkflowInstance instance)
    {
        var userIds = new HashSet<Guid> { instance.InitiatorUserId };

        foreach (var task in instance.Tasks)
        {
            userIds.Add(task.ApproverUserId);
        }

        foreach (var ccLog in instance.ActionLogs.Where(x => x.Action.Equals("Cc", StringComparison.OrdinalIgnoreCase)))
        {
            userIds.Add(ccLog.OperatorUserId);
        }

        userIds.Remove(Guid.Empty);
        return userIds;
    }

    private async Task QueueNotificationAsync(
        Guid userId,
        string title,
        string message,
        string category,
        string level,
        string? link,
        string sourceType,
        string sourceId,
        IReadOnlyDictionary<string, string>? variables,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return;
        }

        var policy = await GetNotificationPolicyAsync(sourceType, cancellationToken);
        var subscription = await GetNotificationSubscriptionAsync(userId, sourceType, cancellationToken);
        var shouldCreateInApp = ApplySubscriptionPreference(
            ShouldCreateInAppNotification(policy),
            subscription,
            static item => item.EnableInApp);
        var shouldCreateEmail = ApplySubscriptionPreference(
            ShouldCreateEmailDelivery(policy),
            subscription,
            static item => item.EnableEmail);
        var shouldCreateWebhook = ApplySubscriptionPreference(
            ShouldCreateWebhookDelivery(policy),
            subscription,
            static item => item.EnableWebhook);
        if (!shouldCreateInApp && !shouldCreateEmail && !shouldCreateWebhook)
        {
            return;
        }

        var rendered = await RenderNotificationTemplateAsync(
            sourceType,
            title,
            message,
            link,
            category,
            level,
            sourceId,
            variables,
            cancellationToken);

        var renderedLink = NormalizeWorkflowNotificationLink(rendered.Link, sourceType, variables);

        if (shouldCreateInApp && !await HasExistingInAppNotificationAsync(
                userId,
                sourceType,
                sourceId,
                cancellationToken))
        {
            dbContext.UserNotifications.Add(new UserNotification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = rendered.Title,
                Message = rendered.Message,
                Category = category,
                Level = level,
                Link = renderedLink,
                SourceType = sourceType,
                SourceId = sourceId,
                IsRead = false,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        if (shouldCreateEmail)
        {
            await notificationDeliveryService.CreateWorkflowEmailDeliveryAsync(
                userId,
                sourceType,
                sourceId,
                rendered.Title,
                rendered.Message,
                DateTimeOffset.UtcNow,
                cancellationToken);
        }

        if (shouldCreateWebhook)
        {
            await notificationDeliveryService.CreateWorkflowWebhookDeliveryAsync(
                userId,
                sourceType,
                sourceId,
                rendered.Title,
                rendered.Message,
                DateTimeOffset.UtcNow,
                cancellationToken);
        }
    }

    private async Task<NotificationPolicy?> GetNotificationPolicyAsync(
        string sourceType,
        CancellationToken cancellationToken)
    {
        return await dbContext.NotificationPolicies
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.EventCode == sourceType, cancellationToken);
    }

    private async Task<NotificationSubscription?> GetNotificationSubscriptionAsync(
        Guid userId,
        string sourceType,
        CancellationToken cancellationToken)
    {
        return await dbContext.NotificationSubscriptions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.UserId == userId && x.EventCode == sourceType,
                cancellationToken);
    }

    private static bool ShouldCreateInAppNotification(NotificationPolicy? policy)
    {
        return policy is null || (policy.IsEnabled && policy.EnableInApp);
    }

    private static bool ShouldCreateEmailDelivery(NotificationPolicy? policy)
    {
        return policy is not null && policy.IsEnabled && policy.EnableEmail;
    }

    private static bool ShouldCreateWebhookDelivery(NotificationPolicy? policy)
    {
        return policy is not null && policy.IsEnabled && policy.EnableWebhook;
    }

    private static bool ApplySubscriptionPreference(
        bool policyEnabled,
        NotificationSubscription? subscription,
        Func<NotificationSubscription, bool> isChannelEnabled)
    {
        return policyEnabled && (subscription is null || (subscription.IsEnabled && isChannelEnabled(subscription)));
    }

    private async Task<bool> HasExistingInAppNotificationAsync(
        Guid userId,
        string sourceType,
        string sourceId,
        CancellationToken cancellationToken)
    {
        if (dbContext.UserNotifications.Local.Any(x =>
                x.UserId == userId &&
                x.SourceType == sourceType &&
                x.SourceId == sourceId))
        {
            return true;
        }

        return await dbContext.UserNotifications
            .AsNoTracking()
            .AnyAsync(x =>
                x.UserId == userId &&
                x.SourceType == sourceType &&
                x.SourceId == sourceId,
                cancellationToken);
    }

    private async Task<NotificationTemplateRenderResult> RenderNotificationTemplateAsync(
        string sourceType,
        string fallbackTitle,
        string fallbackMessage,
        string? fallbackLink,
        string category,
        string level,
        string sourceId,
        IReadOnlyDictionary<string, string>? variables,
        CancellationToken cancellationToken)
    {
        var renderVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["category"] = category,
            ["level"] = level,
            ["sourceId"] = sourceId,
            ["sourceType"] = sourceType
        };
        if (variables is not null)
        {
            foreach (var variable in variables)
            {
                renderVariables[variable.Key] = variable.Value;
            }
        }

        return await notificationTemplateRenderer.RenderAsync(
            sourceType,
            fallbackTitle,
            fallbackMessage,
            fallbackLink,
            renderVariables,
            cancellationToken);
    }

    private static Dictionary<string, string> CreateWorkflowNotificationVariables(
        WorkflowInstance instance,
        WorkflowNode? node,
        WorkflowTask? task,
        WorkflowUserContext? operatorUser,
        User? targetUser,
        string? comment)
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["businessKey"] = instance.BusinessKey ?? string.Empty,
            ["currentNodeId"] = instance.CurrentNodeId?.ToString() ?? string.Empty,
            ["currentNodeName"] = instance.CurrentNodeName ?? string.Empty,
            ["definitionId"] = instance.DefinitionId.ToString(),
            ["definitionName"] = instance.DefinitionName,
            ["formDataJson"] = instance.FormDataJson,
            ["initiatorUserId"] = instance.InitiatorUserId.ToString(),
            ["initiatorUserName"] = instance.InitiatorUserName,
            ["instanceId"] = instance.Id.ToString(),
            ["instanceTitle"] = instance.Title,
            ["status"] = instance.Status,
            ["title"] = instance.Title,
            ["workflowTaskQuery"] = string.Empty
        };

        if (node is not null)
        {
            variables["nodeId"] = node.Id.ToString();
            variables["nodeName"] = node.Name;
        }

        if (task is not null)
        {
            variables["approverUserId"] = task.ApproverUserId.ToString();
            variables["approverUserName"] = task.ApproverUserName;
            variables["nodeId"] = task.NodeId.ToString();
            variables["nodeName"] = task.NodeName;
            variables["taskId"] = task.Id.ToString();
            variables["taskStatus"] = task.Status;
            variables["dueAt"] = task.DueAt?.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? string.Empty;
            variables["workflowTaskQuery"] = $"&workflowTaskId={task.Id}";
        }

        if (operatorUser is not null)
        {
            variables["operatorUserId"] = operatorUser.UserId.ToString();
            variables["operatorUserName"] = operatorUser.UserName;
        }

        if (targetUser is not null)
        {
            variables["targetUserId"] = targetUser.Id.ToString();
            variables["targetUserName"] = targetUser.UserName;
            variables["targetRealName"] = targetUser.RealName;
        }

        variables["comment"] = comment ?? string.Empty;
        return variables;
    }

    private static string CreateWorkflowCenterLink(WorkflowInstance instance, WorkflowTask? task = null)
    {
        var instanceId = Uri.EscapeDataString(instance.Id.ToString());
        var link = $"/workflow/center?workflowInstanceId={instanceId}";
        return task is null
            ? link
            : $"{link}&workflowTaskId={Uri.EscapeDataString(task.Id.ToString())}";
    }

    private static string? NormalizeWorkflowNotificationLink(
        string? link,
        string sourceType,
        IReadOnlyDictionary<string, string>? variables)
    {
        if (string.IsNullOrWhiteSpace(link) ||
            !sourceType.StartsWith("Workflow", StringComparison.OrdinalIgnoreCase) ||
            variables is null ||
            !variables.TryGetValue("instanceId", out var instanceId) ||
            string.IsNullOrWhiteSpace(instanceId))
        {
            return link;
        }

        if (!IsWorkflowCenterLink(link))
        {
            return link;
        }

        variables.TryGetValue("taskId", out var taskId);
        var includeTask = ShouldIncludeTaskInWorkflowLink(sourceType) &&
            !string.IsNullOrWhiteSpace(taskId);
        string? ccRecordId = null;
        var includeCcRecord = sourceType.Equals("WorkflowCc", StringComparison.OrdinalIgnoreCase) &&
            TryGetWorkflowCcRecordId(variables, out ccRecordId);
        var escapedInstanceId = Uri.EscapeDataString(instanceId);
        var detailLink = $"/workflow/center?workflowInstanceId={escapedInstanceId}";
        var normalizedLink = string.Equals(link, "/workflow/center", StringComparison.OrdinalIgnoreCase)
            ? detailLink
            : EnsureQueryParameter(link, "workflowInstanceId", instanceId);
        if (includeTask)
        {
            return EnsureQueryParameter(normalizedLink, "workflowTaskId", taskId!);
        }

        return includeCcRecord
            ? EnsureQueryParameter(normalizedLink, "workflowCcId", ccRecordId!)
            : normalizedLink;
    }

    private static bool ShouldIncludeTaskInWorkflowLink(string sourceType)
    {
        return sourceType.Equals("WorkflowTask", StringComparison.OrdinalIgnoreCase) ||
            sourceType.Equals("WorkflowTransfer", StringComparison.OrdinalIgnoreCase) ||
            sourceType.Equals("WorkflowRemind", StringComparison.OrdinalIgnoreCase) ||
            sourceType.Equals("WorkflowOverdue", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetWorkflowCcRecordId(
        IReadOnlyDictionary<string, string> variables,
        out string? ccRecordId)
    {
        if (variables.TryGetValue("ccRecordId", out ccRecordId) &&
            !string.IsNullOrWhiteSpace(ccRecordId))
        {
            return true;
        }

        if (variables.TryGetValue("sourceId", out ccRecordId) &&
            !string.IsNullOrWhiteSpace(ccRecordId))
        {
            return true;
        }

        ccRecordId = null;
        return false;
    }

    private static bool IsWorkflowCenterLink(string link)
    {
        var hashIndex = link.IndexOf('#', StringComparison.Ordinal);
        var linkWithoutHash = hashIndex >= 0 ? link[..hashIndex] : link;
        var queryIndex = linkWithoutHash.IndexOf('?', StringComparison.Ordinal);
        var path = queryIndex >= 0 ? linkWithoutHash[..queryIndex] : linkWithoutHash;
        return path.Equals("/workflow/center", StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureQueryParameter(string link, string name, string value)
    {
        if (ContainsQueryParameter(link, name))
        {
            return link;
        }

        var hashIndex = link.IndexOf('#', StringComparison.Ordinal);
        var linkWithoutHash = hashIndex >= 0 ? link[..hashIndex] : link;
        var hash = hashIndex >= 0 ? link[hashIndex..] : string.Empty;
        var separator = linkWithoutHash.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{linkWithoutHash}{separator}{name}={Uri.EscapeDataString(value)}{hash}";
    }

    private static bool ContainsQueryParameter(string link, string name)
    {
        var queryIndex = link.IndexOf('?', StringComparison.Ordinal);
        if (queryIndex < 0)
        {
            return false;
        }

        var hashIndex = link.IndexOf('#', queryIndex);
        var query = hashIndex >= 0
            ? link[(queryIndex + 1)..hashIndex]
            : link[(queryIndex + 1)..];
        return query
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Any(part =>
            {
                var equalIndex = part.IndexOf('=', StringComparison.Ordinal);
                var parameterName = equalIndex >= 0 ? part[..equalIndex] : part;
                return parameterName.Equals(name, StringComparison.OrdinalIgnoreCase);
            });
    }

    private async Task<WorkflowNode?> ResolveNextRuntimeNodeAsync(
        WorkflowDefinition definition,
        string sourceDesignerNodeId,
        WorkflowNode? currentNode,
        string formDataJson,
        WorkflowInstance instance,
        WorkflowUserContext operatorUser,
        CancellationToken cancellationToken)
    {
        var graph = ParseDesignerGraph(definition.DesignerJson);
        if (graph is not null &&
            graph.Nodes.Any(x => x.Id == sourceDesignerNodeId) &&
            graph.Edges.Any(x => x.Source == sourceDesignerNodeId))
        {
            var nextNode = await ResolveNextRuntimeNodeAsync(
                definition,
                graph,
                sourceDesignerNodeId,
                formDataJson,
                instance,
                operatorUser,
                [],
                cancellationToken);
            return nextNode;
        }

        var nodes = definition.Nodes
            .Where(x => x.IsEnabled && (currentNode is null || x.Order > currentNode.Order))
            .OrderBy(x => x.Order)
            .ThenBy(x => x.Name)
            .ToArray();

        foreach (var node in nodes)
        {
            if (IsCcNode(node))
            {
                await CreateCcLogsAsync(instance, node, operatorUser, cancellationToken);
                continue;
            }

            return node;
        }

        return null;
    }

    private async Task<WorkflowNode?> ResolveNextRuntimeNodeAsync(
        WorkflowDefinition definition,
        WorkflowDesignerGraph graph,
        string sourceDesignerNodeId,
        string formDataJson,
        WorkflowInstance instance,
        WorkflowUserContext operatorUser,
        HashSet<string> visitedNodeIds,
        CancellationToken cancellationToken)
    {
        if (!visitedNodeIds.Add(sourceDesignerNodeId))
        {
            return null;
        }

        var edge = SelectNextEdge(graph, sourceDesignerNodeId, formDataJson);
        if (edge is null)
        {
            return null;
        }

        var targetDesignerNode = graph.Nodes.FirstOrDefault(x => x.Id == edge.Target);
        if (targetDesignerNode is null || targetDesignerNode.Type == "end")
        {
            return null;
        }

        var executableNode = FindExecutableNode(definition, edge.Target);
        if (executableNode is not null)
        {
            if (IsCcNode(executableNode))
            {
                await CreateCcLogsAsync(instance, executableNode, operatorUser, cancellationToken);
                return await ResolveNextRuntimeNodeAsync(
                    definition,
                    graph,
                    targetDesignerNode.Id,
                    formDataJson,
                    instance,
                    operatorUser,
                    visitedNodeIds,
                    cancellationToken);
            }

            return executableNode;
        }

        return await ResolveNextRuntimeNodeAsync(
            definition,
            graph,
            targetDesignerNode.Id,
            formDataJson,
            instance,
            operatorUser,
            visitedNodeIds,
            cancellationToken);
    }

    private async Task CreateCcLogsAsync(
        WorkflowInstance instance,
        WorkflowNode node,
        WorkflowUserContext operatorUser,
        CancellationToken cancellationToken)
    {
        var recipients = await ResolveApproversAsync(node, cancellationToken);
        if (recipients.Count == 0)
        {
            var reason = await BuildUnavailableApproverReasonAsync(node, operatorUser, cancellationToken);
            throw new WorkflowOperationException(reason);
        }

        foreach (var recipient in recipients)
        {
            var now = DateTimeOffset.UtcNow;
            var ccLog = new WorkflowActionLog
            {
                Id = Guid.NewGuid(),
                InstanceId = instance.Id,
                NodeId = node.Id,
                NodeName = node.Name,
                Action = "Cc",
                OperatorUserId = recipient.Id,
                OperatorUserName = recipient.UserName,
                Comment = $"由 {operatorUser.UserName} 抄送",
                CreatedAt = now
            };
            dbContext.WorkflowActionLogs.Add(ccLog);
            var ccRecord = new WorkflowCcRecord
            {
                Id = Guid.NewGuid(),
                InstanceId = instance.Id,
                NodeId = node.Id,
                NodeName = node.Name,
                RecipientUserId = recipient.Id,
                RecipientUserName = recipient.UserName,
                SenderUserId = operatorUser.UserId,
                SenderUserName = operatorUser.UserName,
                CreatedAt = now
            };
            dbContext.WorkflowCcRecords.Add(ccRecord);
            var variables = CreateWorkflowNotificationVariables(
                instance,
                node,
                null,
                operatorUser,
                null,
                ccLog.Comment);
            variables["ccRecordId"] = ccRecord.Id.ToString();
            await QueueNotificationAsync(
                recipient.Id,
                $"流程抄送：{instance.Title}",
                $"流程“{instance.Title}”已抄送给你，节点“{node.Name}”。",
                "Workflow",
                "Info",
                $"{CreateWorkflowCenterLink(instance)}&workflowCcId={Uri.EscapeDataString(ccRecord.Id.ToString())}",
                "WorkflowCc",
                ccRecord.Id.ToString(),
                variables,
                cancellationToken);
        }
    }

    private static bool IsCcNode(WorkflowNode node)
    {
        return node.NodeType.Equals("cc", StringComparison.OrdinalIgnoreCase);
    }

    private static WorkflowDesignerNode? ResolveNextDesignerNode(
        WorkflowDesignerGraph graph,
        string sourceDesignerNodeId,
        string formDataJson,
        HashSet<string> visitedNodeIds)
    {
        if (!visitedNodeIds.Add(sourceDesignerNodeId))
        {
            return null;
        }

        var edge = SelectNextEdge(graph, sourceDesignerNodeId, formDataJson);
        if (edge is null)
        {
            return null;
        }

        var targetDesignerNode = graph.Nodes.FirstOrDefault(x => x.Id == edge.Target);
        if (targetDesignerNode is null || targetDesignerNode.Type == "end")
        {
            return targetDesignerNode;
        }

        return ResolveNextDesignerNode(
            graph,
            targetDesignerNode.Id,
            formDataJson,
            visitedNodeIds);
    }

    private static WorkflowDesignerEdge? SelectNextEdge(
        WorkflowDesignerGraph graph,
        string sourceDesignerNodeId,
        string formDataJson)
    {
        var sourceNode = graph.Nodes.FirstOrDefault(x => x.Id == sourceDesignerNodeId);
        var outgoingEdges = graph.Edges
            .Where(x => x.Source == sourceDesignerNodeId)
            .OrderBy(x => x.Sort)
            .ThenBy(x => x.Id)
            .ToArray();
        if (outgoingEdges.Length == 0)
        {
            return null;
        }

        if (!string.Equals(sourceNode?.Type, "condition", StringComparison.OrdinalIgnoreCase))
        {
            return outgoingEdges[0];
        }

        var matchedEdge = outgoingEdges.FirstOrDefault(edge =>
            !edge.IsDefault && HasCondition(edge) && EvaluateCondition(edge, formDataJson));
        if (matchedEdge is not null)
        {
            return matchedEdge;
        }

        return outgoingEdges.FirstOrDefault(edge => edge.IsDefault) ??
               outgoingEdges.FirstOrDefault(edge => !HasCondition(edge)) ??
               outgoingEdges[0];
    }

    private static bool HasCondition(WorkflowDesignerEdge edge)
    {
        return !string.IsNullOrWhiteSpace(edge.ConditionField) ||
               !string.IsNullOrWhiteSpace(edge.ConditionOperator) ||
               !string.IsNullOrWhiteSpace(edge.ConditionValue);
    }

    private static bool EvaluateCondition(WorkflowDesignerEdge edge, string formDataJson)
    {
        var conditionOperator = string.IsNullOrWhiteSpace(edge.ConditionOperator)
            ? "Equals"
            : edge.ConditionOperator.Trim();

        if (conditionOperator.Equals("Always", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(edge.ConditionField))
        {
            return false;
        }

        using var document = JsonDocument.Parse(NormalizeFormData(formDataJson));
        if (!TryGetJsonValue(document.RootElement, edge.ConditionField, out var value))
        {
            return conditionOperator.Equals("Empty", StringComparison.OrdinalIgnoreCase);
        }

        var actualText = JsonElementToString(value);
        var expectedText = edge.ConditionValue ?? string.Empty;

        return conditionOperator switch
        {
            "Contains" => actualText.Contains(expectedText, StringComparison.OrdinalIgnoreCase),
            "Empty" => string.IsNullOrWhiteSpace(actualText),
            "Equals" => CompareTexts(actualText, expectedText) == 0,
            "GreaterThan" => CompareNumbersOrTexts(actualText, expectedText) > 0,
            "GreaterThanOrEqual" => CompareNumbersOrTexts(actualText, expectedText) >= 0,
            "LessThan" => CompareNumbersOrTexts(actualText, expectedText) < 0,
            "LessThanOrEqual" => CompareNumbersOrTexts(actualText, expectedText) <= 0,
            "NotEmpty" => !string.IsNullOrWhiteSpace(actualText),
            "NotEquals" => CompareTexts(actualText, expectedText) != 0,
            _ => false
        };
    }

    private static bool TryGetJsonValue(
        JsonElement root,
        string path,
        out JsonElement value)
    {
        value = root;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (value.ValueKind != JsonValueKind.Object ||
                !value.TryGetProperty(segment, out value))
            {
                return false;
            }
        }

        return true;
    }

    private static string JsonElementToString(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.False => "false",
            JsonValueKind.Null => string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.True => "true",
            _ => value.GetRawText()
        };
    }

    private static int CompareNumbersOrTexts(string actual, string expected)
    {
        return decimal.TryParse(actual, NumberStyles.Any, CultureInfo.InvariantCulture, out var actualNumber) &&
               decimal.TryParse(expected, NumberStyles.Any, CultureInfo.InvariantCulture, out var expectedNumber)
            ? actualNumber.CompareTo(expectedNumber)
            : CompareTexts(actual, expected);
    }

    private static int CompareTexts(string actual, string expected)
    {
        return string.Compare(actual, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static WorkflowNode? FindExecutableNode(
        WorkflowDefinition definition,
        string? designerNodeId)
    {
        return string.IsNullOrWhiteSpace(designerNodeId)
            ? null
            : definition.Nodes
                .Where(x => x.IsEnabled)
                .FirstOrDefault(x => x.DesignerNodeId == designerNodeId);
    }

    private static WorkflowDesignerGraph? ParseDesignerGraph(string? designerJson)
    {
        if (string.IsNullOrWhiteSpace(designerJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<WorkflowDesignerGraph>(designerJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record NormalizedWorkflowNodeInput(
        SaveWorkflowNodeRequest Request,
        string DesignerNodeId);

    private sealed record WorkflowDefinitionSnapshot(
        string Id,
        string Code,
        string Name,
        string? FormName,
        int Version,
        string PublishStatus,
        DateTimeOffset? PublishedAt,
        string DesignerJson,
        string FormSchemaJson,
        IReadOnlyList<WorkflowDefinitionSnapshotNode> Nodes);

    private sealed record WorkflowDefinitionSnapshotNode(
        string Id,
        string Name,
        string DesignerNodeId,
        string NodeType,
        string ApprovalMode,
        string ApproverType,
        string? ApproverUserId,
        string? ApproverRoleId,
        int Order,
        bool IsEnabled,
        int? SlaMinutes);

    private sealed record WorkflowDesignerGraph(
        IReadOnlyList<WorkflowDesignerNode> Nodes,
        IReadOnlyList<WorkflowDesignerEdge> Edges);

    private sealed record WorkflowDesignerNode(
        string Id,
        string Type,
        string Label,
        double X,
        double Y);

    private sealed record WorkflowDesignerEdge(
        string Id,
        string Source,
        string Target,
        string? Label = null,
        string? ConditionField = null,
        string? ConditionOperator = null,
        string? ConditionValue = null,
        bool IsDefault = false,
        int Sort = 0);

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
