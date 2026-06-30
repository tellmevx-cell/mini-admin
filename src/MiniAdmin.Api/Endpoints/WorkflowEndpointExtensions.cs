using System.Text;
using System.Text.Json;
using System.Security.Claims;
using MiniAdmin.Api.CodeGenerators;
using MiniAdmin.Application.AppBranding;
using MiniAdmin.Application.Alerts;
using MiniAdmin.Application.AuditLogs;
using MiniAdmin.Application.Auth;
using MiniAdmin.Application.Contracts.AppBranding;
using MiniAdmin.Application.Contracts.AuditLogs;
using MiniAdmin.Application.Contracts.Alerts;
using MiniAdmin.Application.Contracts.Auth;
using MiniAdmin.Application.Contracts.Caching;
using MiniAdmin.Application.Contracts.CodeGenerators;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.Departments;
using MiniAdmin.Application.Contracts.Dictionaries;
using MiniAdmin.Application.Contracts.Files;
using MiniAdmin.Application.Contracts.Menus;
using MiniAdmin.Application.Contracts.MultiTenancy;
using MiniAdmin.Application.Contracts.Notices;
using MiniAdmin.Application.Contracts.OnlineUsers;
using MiniAdmin.Application.Contracts.Parameters;
using MiniAdmin.Application.Contracts.PermissionDiagnostics;
using MiniAdmin.Application.Contracts.Positions;
using MiniAdmin.Application.Contracts.ProjectRuntimes;
using MiniAdmin.Application.Contracts.Roles;
using MiniAdmin.Application.Contracts.ScheduledJobs;
using MiniAdmin.Application.Contracts.Security;
using MiniAdmin.Application.Contracts.SystemMonitor;
using MiniAdmin.Application.Contracts.TenantPackages;
using MiniAdmin.Application.Contracts.Tenants;
using MiniAdmin.Application.Contracts.UserNotifications;
using MiniAdmin.Application.Contracts.Users;
using MiniAdmin.Application.Contracts.Workflows;
using MiniAdmin.Application.CodeGenerators;
using MiniAdmin.Application.Departments;
using MiniAdmin.Application.Dictionaries;
using MiniAdmin.Application.Files;
using MiniAdmin.Application.Menus;
using MiniAdmin.Application.Notices;
using MiniAdmin.Application.OnlineUsers;
using MiniAdmin.Application.Parameters;
using MiniAdmin.Application.PermissionDiagnostics;
using MiniAdmin.Application.Positions;
using MiniAdmin.Application.Roles;
using MiniAdmin.Application.ScheduledJobs;
using MiniAdmin.Application.Security;
using MiniAdmin.Application.TenantPackages;
using MiniAdmin.Application.Tenants;
using MiniAdmin.Application.Users;
using MiniAdmin.Application.UserNotifications;
using MiniAdmin.Application.Workflows;
using MiniAdmin.Infrastructure.Auth;
using MiniAdmin.Infrastructure.MultiTenancy;
using MiniAdmin.Infrastructure.Persistence;
using MiniAdmin.Infrastructure.ProjectRuntimes;
using MiniAdmin.Infrastructure.SystemMonitor;
using MiniAdmin.Domain.Shared.MultiTenancy;
using MiniAdmin.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json.Serialization;
using static MiniAdmin.Api.Endpoints.EndpointHelpers;

namespace MiniAdmin.Api.Endpoints;

public static class WorkflowEndpointExtensions
{
    public static IEndpointRouteBuilder MapWorkflowEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/workflow/definition/list", async (
            int? page,
            int? pageSize,
            string? keyword,
            bool? isEnabled,
            IWorkflowAppService workflowAppService,
            CancellationToken cancellationToken) =>
        {
            var definitions = await workflowAppService.GetDefinitionsAsync(
                new WorkflowDefinitionListQuery(
                    Page: page ?? 1,
                    PageSize: pageSize ?? 20,
                    Keyword: keyword,
                    IsEnabled: isEnabled),
                cancellationToken);

            return Results.Ok(ApiResponse<object>.Ok(definitions));
        })
        .RequirePermission("workflow:center:query");

        app.MapGet("/workflow/definition/options", async (
            IWorkflowAppService workflowAppService,
            CancellationToken cancellationToken) =>
        {
            var definitions = await workflowAppService.GetDefinitionOptionsAsync(cancellationToken);

            return Results.Ok(ApiResponse<IReadOnlyList<WorkflowDefinitionOptionDto>>.Ok(definitions));
        })
        .RequirePermission("workflow:center:query");

        app.MapGet("/workflow/business-binding/list", async (
            int? page,
            int? pageSize,
            string? keyword,
            bool? isEnabled,
            IWorkflowAppService workflowAppService,
            CancellationToken cancellationToken) =>
        {
            var bindings = await workflowAppService.GetBusinessBindingsAsync(
                new WorkflowBusinessBindingListQuery(
                    Page: page ?? 1,
                    PageSize: pageSize ?? 20,
                    Keyword: keyword,
                    IsEnabled: isEnabled),
                cancellationToken);

            return Results.Ok(ApiResponse<object>.Ok(bindings));
        })
        .RequirePermission("workflow:center:query");

        app.MapGet("/workflow/business-binding/resolve/{businessType}", async (
            string businessType,
            IWorkflowAppService workflowAppService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var definition = await workflowAppService.ResolveBusinessDefinitionAsync(
                    businessType,
                    cancellationToken);

                return Results.Ok(ApiResponse<WorkflowBusinessDefinitionDto?>.Ok(definition));
            }
            catch (WorkflowOperationException exception)
            {
                return Results.BadRequest(ApiResponse<WorkflowBusinessDefinitionDto?>.Fail(exception.Message));
            }
        })
        .RequirePermission("workflow:center:query");

        app.MapPost("/workflow/business-binding", async (
            SaveWorkflowBusinessBindingRequest request,
            IWorkflowAppService workflowAppService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var binding = await workflowAppService.CreateBusinessBindingAsync(request, cancellationToken);
                return Results.Ok(ApiResponse<WorkflowBusinessBindingDto>.Ok(binding));
            }
            catch (WorkflowOperationException exception)
            {
                return Results.BadRequest(ApiResponse<WorkflowBusinessBindingDto?>.Fail(exception.Message));
            }
        })
        .RequirePermission("workflow:definition:manage");

        app.MapPut("/workflow/business-binding/{id:guid}", async (
            Guid id,
            SaveWorkflowBusinessBindingRequest request,
            IWorkflowAppService workflowAppService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var binding = await workflowAppService.UpdateBusinessBindingAsync(id, request, cancellationToken);
                return binding is null
                    ? Results.NotFound(ApiResponse<WorkflowBusinessBindingDto?>.Fail("Workflow business binding not found."))
                    : Results.Ok(ApiResponse<WorkflowBusinessBindingDto>.Ok(binding));
            }
            catch (WorkflowOperationException exception)
            {
                return Results.BadRequest(ApiResponse<WorkflowBusinessBindingDto?>.Fail(exception.Message));
            }
        })
        .RequirePermission("workflow:definition:manage");

        app.MapDelete("/workflow/business-binding/{id:guid}", async (
            Guid id,
            IWorkflowAppService workflowAppService,
            CancellationToken cancellationToken) =>
        {
            var deleted = await workflowAppService.DeleteBusinessBindingAsync(id, cancellationToken);

            return Results.Ok(ApiResponse<bool>.Ok(deleted));
        })
        .RequirePermission("workflow:definition:manage");

        app.MapGet("/workflow/approver/users", async (
            MiniAdminDbContext dbContext,
            ICurrentTenant currentTenant,
            CancellationToken cancellationToken) =>
        {
            var usersQuery = dbContext.Users.AsNoTracking()
                .Where(x => x.IsEnabled);
            usersQuery = currentTenant.IsTenant
                ? usersQuery.Where(x => x.TenantId == currentTenant.TenantId)
                : usersQuery.Where(x => x.TenantId == null);

            var users = await usersQuery
                .OrderBy(x => x.UserName)
                .Select(x => new WorkflowApproverUserOptionDto(
                    x.Id.ToString(),
                    x.UserName,
                    x.RealName))
                .ToArrayAsync(cancellationToken);

            return Results.Ok(ApiResponse<IReadOnlyList<WorkflowApproverUserOptionDto>>.Ok(users));
        })
        .RequirePermission("workflow:definition:manage");

        app.MapGet("/workflow/approver/roles", async (
            MiniAdminDbContext dbContext,
            ICurrentTenant currentTenant,
            CancellationToken cancellationToken) =>
        {
            var rolesQuery = dbContext.Roles.AsNoTracking()
                .Where(x => x.IsEnabled);
            rolesQuery = currentTenant.IsTenant
                ? rolesQuery.Where(x => x.TenantId == currentTenant.TenantId)
                : rolesQuery.Where(x => x.TenantId == null);

            var roleRows = await rolesQuery
                .Include(x => x.UserRoles)
                .ThenInclude(x => x.User)
                .OrderBy(x => x.Code)
                .ToArrayAsync(cancellationToken);

            var roles = roleRows
                .Select(x => new WorkflowApproverRoleOptionDto(
                    x.Id.ToString(),
                    x.Code,
                    x.Name,
                    x.UserRoles.Count(userRole =>
                        userRole.User.IsEnabled &&
                        (currentTenant.IsTenant
                            ? userRole.User.TenantId == currentTenant.TenantId
                            : userRole.User.TenantId == null))))
                .Where(x => x.EnabledUserCount > 0)
                .ToArray();

            return Results.Ok(ApiResponse<IReadOnlyList<WorkflowApproverRoleOptionDto>>.Ok(roles));
        })
        .RequirePermission("workflow:definition:manage");

        app.MapPost("/workflow/definition", async (
            SaveWorkflowDefinitionRequest request,
            IWorkflowAppService workflowAppService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var definition = await workflowAppService.CreateDefinitionAsync(request, cancellationToken);
                return Results.Ok(ApiResponse<WorkflowDefinitionDto>.Ok(definition));
            }
            catch (WorkflowOperationException exception)
            {
                return Results.BadRequest(ApiResponse<WorkflowDefinitionDto?>.Fail(exception.Message));
            }
        })
        .RequirePermission("workflow:definition:manage");

        app.MapPut("/workflow/definition/{id:guid}", async (
            Guid id,
            SaveWorkflowDefinitionRequest request,
            IWorkflowAppService workflowAppService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var definition = await workflowAppService.UpdateDefinitionAsync(id, request, cancellationToken);
                return definition is null
                    ? Results.NotFound(ApiResponse<WorkflowDefinitionDto?>.Fail("Workflow definition not found."))
                    : Results.Ok(ApiResponse<WorkflowDefinitionDto>.Ok(definition));
            }
            catch (WorkflowOperationException exception)
            {
                return Results.BadRequest(ApiResponse<WorkflowDefinitionDto?>.Fail(exception.Message));
            }
        })
        .RequirePermission("workflow:definition:manage");

        app.MapPost("/workflow/definition/{id:guid}/publish", async (
            Guid id,
            IWorkflowAppService workflowAppService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var definition = await workflowAppService.PublishDefinitionAsync(id, cancellationToken);
                return definition is null
                    ? Results.NotFound(ApiResponse<WorkflowDefinitionDto?>.Fail("Workflow definition not found."))
                    : Results.Ok(ApiResponse<WorkflowDefinitionDto>.Ok(definition));
            }
            catch (WorkflowOperationException exception)
            {
                return Results.BadRequest(ApiResponse<WorkflowDefinitionDto?>.Fail(exception.Message));
            }
        })
        .RequirePermission("workflow:definition:manage");

        app.MapPost("/workflow/definition/{id:guid}/new-version", async (
            Guid id,
            IWorkflowAppService workflowAppService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var definition = await workflowAppService.CreateNewVersionAsync(id, cancellationToken);
                return definition is null
                    ? Results.NotFound(ApiResponse<WorkflowDefinitionDto?>.Fail("Workflow definition not found."))
                    : Results.Ok(ApiResponse<WorkflowDefinitionDto>.Ok(definition));
            }
            catch (WorkflowOperationException exception)
            {
                return Results.BadRequest(ApiResponse<WorkflowDefinitionDto?>.Fail(exception.Message));
            }
        })
        .RequirePermission("workflow:definition:manage");

        app.MapDelete("/workflow/definition/{id:guid}", async (
            Guid id,
            IWorkflowAppService workflowAppService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var deleted = await workflowAppService.DeleteDefinitionAsync(id, cancellationToken);
                return Results.Ok(ApiResponse<bool>.Ok(deleted));
            }
            catch (WorkflowOperationException exception)
            {
                return Results.BadRequest(ApiResponse<bool>.Fail(exception.Message));
            }
        })
        .RequirePermission("workflow:definition:manage");

        app.MapGet("/workflow/instance/list", async (
            ClaimsPrincipal principal,
            int? page,
            int? pageSize,
            string? keyword,
            string? status,
            string? scope,
            IWorkflowAppService workflowAppService,
            CancellationToken cancellationToken) =>
        {
            var instances = await workflowAppService.GetInstancesAsync(
                new WorkflowInstanceListQuery(
                    Page: page ?? 1,
                    PageSize: pageSize ?? 20,
                    Keyword: keyword,
                    Status: status,
                    Scope: string.IsNullOrWhiteSpace(scope) ? "all" : scope),
                GetWorkflowUserContext(principal),
                cancellationToken);

            return Results.Ok(ApiResponse<object>.Ok(instances));
        })
        .RequirePermission("workflow:center:query");

        app.MapGet("/workflow/instance/started-by-me", async (
            ClaimsPrincipal principal,
            int? page,
            int? pageSize,
            string? keyword,
            string? status,
            IWorkflowAppService workflowAppService,
            CancellationToken cancellationToken) =>
        {
            var instances = await workflowAppService.GetInstancesAsync(
                new WorkflowInstanceListQuery(
                    Page: page ?? 1,
                    PageSize: pageSize ?? 20,
                    Keyword: keyword,
                    Status: status,
                    Scope: "startedByMe"),
                GetWorkflowUserContext(principal),
                cancellationToken);

            return Results.Ok(ApiResponse<object>.Ok(instances));
        })
        .RequirePermission("workflow:center:query");

        app.MapGet("/workflow/instance/cc", async (
            ClaimsPrincipal principal,
            int? page,
            int? pageSize,
            string? keyword,
            string? status,
            string? readStatus,
            IWorkflowAppService workflowAppService,
            CancellationToken cancellationToken) =>
        {
            var records = await workflowAppService.GetCcRecordsAsync(
                new WorkflowCcListQuery(
                    Page: page ?? 1,
                    PageSize: pageSize ?? 20,
                    Keyword: keyword,
                    InstanceStatus: status,
                    ReadStatus: readStatus),
                GetWorkflowUserContext(principal),
                cancellationToken);

            return Results.Ok(ApiResponse<object>.Ok(records));
        })
        .RequirePermission("workflow:center:query");

        app.MapPost("/workflow/cc/{id:guid}/read", async (
            ClaimsPrincipal principal,
            Guid id,
            IWorkflowAppService workflowAppService,
            CancellationToken cancellationToken) =>
        {
            var record = await workflowAppService.MarkCcRecordAsReadAsync(
                id,
                GetWorkflowUserContext(principal),
                cancellationToken);

            return record is null
                ? Results.NotFound(ApiResponse<WorkflowCcRecordDto?>.Fail("Workflow cc record not found."))
                : Results.Ok(ApiResponse<WorkflowCcRecordDto>.Ok(record));
        })
        .RequirePermission("workflow:center:query");

        app.MapGet("/workflow/instance/{id:guid}", async (
            ClaimsPrincipal principal,
            Guid id,
            IWorkflowAppService workflowAppService,
            CancellationToken cancellationToken) =>
        {
            var instance = await workflowAppService.GetInstanceAsync(
                id,
                GetWorkflowUserContext(principal),
                cancellationToken);

            return instance is null
                ? Results.NotFound(ApiResponse<WorkflowInstanceDto?>.Fail("Workflow instance not found."))
                : Results.Ok(ApiResponse<WorkflowInstanceDto>.Ok(instance));
        })
        .RequirePermission("workflow:center:query");

        app.MapGet("/workflow/task/todo", async (
            ClaimsPrincipal principal,
            IWorkflowAppService workflowAppService,
            CancellationToken cancellationToken) =>
        {
            var tasks = await workflowAppService.GetTodoTasksAsync(
                GetWorkflowUserContext(principal),
                cancellationToken);

            return Results.Ok(ApiResponse<IReadOnlyList<WorkflowTaskDto>>.Ok(tasks));
        })
        .RequirePermission("workflow:center:query");

        app.MapGet("/workflow/task/done", async (
            ClaimsPrincipal principal,
            IWorkflowAppService workflowAppService,
            CancellationToken cancellationToken) =>
        {
            var tasks = await workflowAppService.GetDoneTasksAsync(
                GetWorkflowUserContext(principal),
                cancellationToken);

            return Results.Ok(ApiResponse<IReadOnlyList<WorkflowTaskDto>>.Ok(tasks));
        })
        .RequirePermission("workflow:center:query");

        app.MapPost("/workflow/instance/start", async (
            ClaimsPrincipal principal,
            StartWorkflowInstanceRequest request,
            IWorkflowAppService workflowAppService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var instance = await workflowAppService.StartInstanceAsync(
                    request,
                    GetWorkflowUserContext(principal),
                    cancellationToken);
                return Results.Ok(ApiResponse<WorkflowInstanceDto>.Ok(instance));
            }
            catch (WorkflowOperationException exception)
            {
                return Results.BadRequest(ApiResponse<WorkflowInstanceDto?>.Fail(exception.Message));
            }
        })
        .RequirePermission("workflow:instance:start");

        app.MapPost("/workflow/instance/{id:guid}/attachments", async (
            ClaimsPrincipal principal,
            Guid id,
            WorkflowAttachmentRequest request,
            IWorkflowAppService workflowAppService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var instance = await workflowAppService.AddAttachmentAsync(
                    id,
                    request,
                    GetWorkflowUserContext(principal),
                    cancellationToken);
                return instance is null
                    ? Results.NotFound(ApiResponse<WorkflowInstanceDto?>.Fail("Workflow instance not found."))
                    : Results.Ok(ApiResponse<WorkflowInstanceDto>.Ok(instance));
            }
            catch (WorkflowOperationException exception)
            {
                return Results.BadRequest(ApiResponse<WorkflowInstanceDto?>.Fail(exception.Message));
            }
        })
        .RequireAnyPermission("workflow:center:query", "workflow:instance:start", "workflow:task:approve");

        app.MapPost("/workflow/instance/{id:guid}/comments", async (
            ClaimsPrincipal principal,
            Guid id,
            WorkflowCommentRequest request,
            IWorkflowAppService workflowAppService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var comment = await workflowAppService.AddCommentAsync(
                    id,
                    request,
                    GetWorkflowUserContext(principal),
                    cancellationToken);
                return comment is null
                    ? Results.NotFound(ApiResponse<WorkflowCommentDto?>.Fail("Workflow instance not found."))
                    : Results.Ok(ApiResponse<WorkflowCommentDto>.Ok(comment));
            }
            catch (WorkflowOperationException exception)
            {
                return Results.BadRequest(ApiResponse<WorkflowCommentDto?>.Fail(exception.Message));
            }
        })
        .RequireAnyPermission("workflow:center:query", "workflow:instance:start", "workflow:task:approve");

        app.MapGet("/workflow/instance/{id:guid}/attachments/{attachmentId:guid}/download", async (
            ClaimsPrincipal principal,
            Guid id,
            Guid attachmentId,
            IWorkflowAppService workflowAppService,
            IFileAppService fileAppService,
            CancellationToken cancellationToken) =>
        {
            WorkflowAttachmentDownloadDto? attachment;
            try
            {
                attachment = await workflowAppService.GetAttachmentDownloadAsync(
                    id,
                    attachmentId,
                    GetWorkflowUserContext(principal),
                    cancellationToken);
            }
            catch (WorkflowOperationException exception)
            {
                return Results.BadRequest(ApiResponse<WorkflowAttachmentDownloadDto?>.Fail(exception.Message));
            }

            if (attachment is null)
            {
                return Results.NotFound(ApiResponse<WorkflowAttachmentDownloadDto?>.Fail("Workflow attachment not found."));
            }

            FileDownloadResult? file;
            try
            {
                file = await fileAppService.DownloadAsync(Guid.Parse(attachment.FileId), cancellationToken);
            }
            catch (FileUnavailableException exception)
            {
                return Results.Conflict(ApiResponse<FileDto?>.Fail(exception.Message));
            }

            return file is null
                ? Results.NotFound(ApiResponse<FileDto?>.Fail("File not found."))
                : Results.File(file.Content, file.ContentType, file.OriginalName);
        })
        .RequirePermission("workflow:center:query");

        app.MapPost("/workflow/instance/{id:guid}/approve", async (
            ClaimsPrincipal principal,
            Guid id,
            WorkflowActionRequest request,
            IWorkflowAppService workflowAppService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var instance = await workflowAppService.ApproveAsync(
                    id,
                    request,
                    GetWorkflowUserContext(principal),
                    cancellationToken);
                return instance is null
                    ? Results.NotFound(ApiResponse<WorkflowInstanceDto?>.Fail("Workflow instance not found."))
                    : Results.Ok(ApiResponse<WorkflowInstanceDto>.Ok(instance));
            }
            catch (WorkflowOperationException exception)
            {
                return Results.BadRequest(ApiResponse<WorkflowInstanceDto?>.Fail(exception.Message));
            }
        })
        .RequirePermission("workflow:task:approve");

        app.MapPost("/workflow/instance/{id:guid}/reject", async (
            ClaimsPrincipal principal,
            Guid id,
            WorkflowActionRequest request,
            IWorkflowAppService workflowAppService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var instance = await workflowAppService.RejectAsync(
                    id,
                    request,
                    GetWorkflowUserContext(principal),
                    cancellationToken);
                return instance is null
                    ? Results.NotFound(ApiResponse<WorkflowInstanceDto?>.Fail("Workflow instance not found."))
                    : Results.Ok(ApiResponse<WorkflowInstanceDto>.Ok(instance));
            }
            catch (WorkflowOperationException exception)
            {
                return Results.BadRequest(ApiResponse<WorkflowInstanceDto?>.Fail(exception.Message));
            }
        })
        .RequirePermission("workflow:task:approve");

        app.MapPost("/workflow/task/{id:guid}/transfer", async (
            ClaimsPrincipal principal,
            Guid id,
            WorkflowTransferTaskRequest request,
            IWorkflowAppService workflowAppService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var task = await workflowAppService.TransferTaskAsync(
                    id,
                    request,
                    GetWorkflowUserContext(principal),
                    cancellationToken);
                return task is null
                    ? Results.NotFound(ApiResponse<WorkflowTaskDto?>.Fail("Workflow task not found."))
                    : Results.Ok(ApiResponse<WorkflowTaskDto>.Ok(task));
            }
            catch (WorkflowOperationException exception)
            {
                return Results.BadRequest(ApiResponse<WorkflowTaskDto?>.Fail(exception.Message));
            }
        })
        .RequirePermission("workflow:task:approve");

        app.MapPost("/workflow/task/{id:guid}/remind", async (
            ClaimsPrincipal principal,
            Guid id,
            WorkflowRemindTaskRequest request,
            IWorkflowAppService workflowAppService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var task = await workflowAppService.RemindTaskAsync(
                    id,
                    request,
                    GetWorkflowUserContext(principal),
                    cancellationToken);
                return task is null
                    ? Results.NotFound(ApiResponse<WorkflowTaskDto?>.Fail("Workflow task not found."))
                    : Results.Ok(ApiResponse<WorkflowTaskDto>.Ok(task));
            }
            catch (WorkflowOperationException exception)
            {
                return Results.BadRequest(ApiResponse<WorkflowTaskDto?>.Fail(exception.Message));
            }
        })
        .RequireAnyPermission("workflow:instance:start", "workflow:task:approve");

        app.MapPost("/workflow/instance/{id:guid}/withdraw", async (
            ClaimsPrincipal principal,
            Guid id,
            WorkflowActionRequest request,
            IWorkflowAppService workflowAppService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var instance = await workflowAppService.WithdrawAsync(
                    id,
                    request,
                    GetWorkflowUserContext(principal),
                    cancellationToken);
                return instance is null
                    ? Results.NotFound(ApiResponse<WorkflowInstanceDto?>.Fail("Workflow instance not found."))
                    : Results.Ok(ApiResponse<WorkflowInstanceDto>.Ok(instance));
            }
            catch (WorkflowOperationException exception)
            {
                return Results.BadRequest(ApiResponse<WorkflowInstanceDto?>.Fail(exception.Message));
            }
        })
        .RequirePermission("workflow:instance:start");

        return app;
    }
}
