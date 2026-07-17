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
using MiniAdmin.Application.Contracts.Events;
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

public static class OperationsEndpointExtensions
{
    public static IEndpointRouteBuilder MapOperationsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/system/online-user/list", async (
            ClaimsPrincipal user,
            int? page,
            int? pageSize,
            string? userName,
            IOnlineUserAppService onlineUserAppService,
            CancellationToken cancellationToken) =>
        {
            var query = new OnlineUserListQuery(
                Page: page ?? 1,
                PageSize: pageSize ?? 20,
                UserName: userName,
                CurrentUserName: GetRequiredUserName(user));
            var users = await onlineUserAppService.GetOnlineUsersAsync(query, cancellationToken);

            return Results.Ok(ApiResponse<object>.Ok(users));
        })
        .RequirePermission("system:online-user:query");

        app.MapPost("/system/online-user/{userId:guid}/force-logout", async (
            ClaimsPrincipal user,
            Guid userId,
            IOnlineUserAppService onlineUserAppService,
            ISecurityCenterAppService securityCenterAppService,
            CancellationToken cancellationToken) =>
        {
            var forced = await onlineUserAppService.ForceLogoutAsync(
                userId,
                GetRequiredUserName(user),
                cancellationToken);
            if (forced)
            {
                await securityCenterAppService.RecordEventAsync(
                    new SaveSecurityEventRequest(
                        "ForceLogout",
                        "Warning",
                        "强制下线",
                        "管理员强制用户下线.",
                        UserId: userId,
                        RelatedEntityType: "User",
                        RelatedEntityId: userId.ToString()),
                    cancellationToken);
            }

            return forced
                ? Results.Ok(ApiResponse<bool>.Ok(true))
                : Results.NotFound(ApiResponse<bool>.Fail("User not found."));
        })
        .RequirePermission("system:online-user:force-logout");

        app.MapPost("/system/online-user/session/{sessionId:guid}/force-logout", async (
            ClaimsPrincipal user,
            Guid sessionId,
            IOnlineUserAppService onlineUserAppService,
            ISecurityCenterAppService securityCenterAppService,
            CancellationToken cancellationToken) =>
        {
            var forced = await onlineUserAppService.ForceLogoutSessionAsync(
                sessionId,
                GetRequiredUserName(user),
                cancellationToken);
            if (forced)
            {
                await securityCenterAppService.RecordEventAsync(
                    new SaveSecurityEventRequest(
                        "ForceLogout",
                        "Warning",
                        "强制单端下线",
                        "管理员强制用户会话下线.",
                        RelatedEntityType: "OnlineSession",
                        RelatedEntityId: sessionId.ToString()),
                    cancellationToken);
            }

            return forced
                ? Results.Ok(ApiResponse<bool>.Ok(true))
                : Results.NotFound(ApiResponse<bool>.Fail("Session not found."));
        })
        .RequirePermission("system:online-user:force-logout");

        app.MapGet("/system/scheduled-job/list", async (
            int? page,
            int? pageSize,
            string? jobKey,
            string? name,
            bool? isEnabled,
            IScheduledJobAppService scheduledJobAppService,
            CancellationToken cancellationToken) =>
        {
            var query = new ScheduledJobListQuery(
                Page: page ?? 1,
                PageSize: pageSize ?? 20,
                JobKey: jobKey,
                Name: name,
                IsEnabled: isEnabled);
            var jobs = await scheduledJobAppService.GetListAsync(query, cancellationToken);

            return Results.Ok(ApiResponse<object>.Ok(jobs));
        })
        .RequirePermission("system:scheduled-job:query");

        app.MapPut("/system/scheduled-job/{id:guid}", async (
            Guid id,
            SaveScheduledJobRequest request,
            IScheduledJobAppService scheduledJobAppService,
            CancellationToken cancellationToken) =>
        {
            var job = await scheduledJobAppService.UpdateAsync(id, request, cancellationToken);

            return job is null
                ? Results.NotFound(ApiResponse<ScheduledJobDto?>.Fail("Scheduled job not found."))
                : Results.Ok(ApiResponse<ScheduledJobDto>.Ok(job));
        })
        .RequirePermission("system:scheduled-job:update");

        app.MapPost("/system/scheduled-job/{id:guid}/run", async (
            Guid id,
            IScheduledJobAppService scheduledJobAppService,
            CancellationToken cancellationToken) =>
        {
            var result = await scheduledJobAppService.RunOnceAsync(id, cancellationToken);

            return result is null
                ? Results.NotFound(ApiResponse<ScheduledJobRunResultDto?>.Fail("Scheduled job not found."))
                : Results.Ok(ApiResponse<ScheduledJobRunResultDto>.Ok(result));
        })
        .RequirePermission("system:scheduled-job:run");

        app.MapGet("/system/scheduled-job/{id:guid}/logs", async (
            Guid id,
            int? page,
            int? pageSize,
            IScheduledJobAppService scheduledJobAppService,
            CancellationToken cancellationToken) =>
        {
            var query = new ScheduledJobLogListQuery(
                Page: page ?? 1,
                PageSize: pageSize ?? 20);
            var logs = await scheduledJobAppService.GetLogsAsync(id, query, cancellationToken);

            return Results.Ok(ApiResponse<object>.Ok(logs));
        })
        .RequirePermission("system:scheduled-job:query");

        app.MapGet("/system/scheduled-job/logs/{logId:guid}/details", async (
            Guid logId,
            int? page,
            int? pageSize,
            IScheduledJobAppService scheduledJobAppService,
            CancellationToken cancellationToken) =>
        {
            var query = new ScheduledJobLogDetailListQuery(
                Page: page ?? 1,
                PageSize: pageSize ?? 20);
            var details = await scheduledJobAppService.GetLogDetailsAsync(logId, query, cancellationToken);

            return Results.Ok(ApiResponse<object>.Ok(details));
        })
        .RequirePermission("system:scheduled-job:query");

        app.MapGet("/system/outbox-message/list", async (
            int? page,
            int? pageSize,
            string? status,
            string? eventType,
            IOutboxAppService outboxAppService,
            CancellationToken cancellationToken) =>
        {
            var query = new OutboxMessageListQuery(
                Page: page ?? 1,
                PageSize: pageSize ?? 20,
                Status: status,
                EventType: eventType);
            var messages = await outboxAppService.GetListAsync(query, cancellationToken);
            return Results.Ok(ApiResponse<object>.Ok(messages));
        })
        .RequirePermission("system:scheduled-job:query");

        app.MapPost("/system/outbox-message/{id:guid}/retry", async (
            Guid id,
            IOutboxAppService outboxAppService,
            CancellationToken cancellationToken) =>
        {
            var retried = await outboxAppService.RetryAsync(id, cancellationToken);
            return retried
                ? Results.Ok(ApiResponse<bool>.Ok(true))
                : Results.BadRequest(ApiResponse<bool>.Fail(
                    "Only retry or dead-letter outbox messages can be resubmitted."));
        })
        .RequirePermission("system:scheduled-job:run");

        app.MapGet("/system/monitor/overview", async (
            ISystemMonitorAppService systemMonitorAppService,
            CancellationToken cancellationToken) =>
        {
            var overview = await systemMonitorAppService.GetOverviewAsync(cancellationToken);

            return Results.Ok(ApiResponse<SystemMonitorOverviewDto>.Ok(overview));
        })
        .RequirePermission("system:monitor:query");

        app.MapGet("/system/security-center/overview", async (
            ISecurityCenterAppService securityCenterAppService,
            CancellationToken cancellationToken) =>
        {
            var overview = await securityCenterAppService.GetOverviewAsync(cancellationToken);

            return Results.Ok(ApiResponse<SecurityCenterOverviewDto>.Ok(overview));
        })
        .RequirePermission("system:security-center:query");

        app.MapGet("/system/security-event/list", async (
            ClaimsPrincipal user,
            int? page,
            int? pageSize,
            string? eventType,
            string? level,
            string? userName,
            ISecurityCenterAppService securityCenterAppService,
            CancellationToken cancellationToken) =>
        {
            var query = new SecurityEventListQuery(
                Page: page ?? 1,
                PageSize: pageSize ?? 20,
                EventType: eventType,
                Level: level,
                UserName: userName,
                CurrentUserName: GetRequiredUserName(user));
            var events = await securityCenterAppService.GetEventsAsync(query, cancellationToken);

            return Results.Ok(ApiResponse<object>.Ok(events));
        })
        .RequirePermission("system:security-event:query");

        app.MapGet("/system/security-policy", async (
            ISecurityPolicyAppService securityPolicyAppService,
            CancellationToken cancellationToken) =>
        {
            var policy = await securityPolicyAppService.GetPolicyAsync(cancellationToken);

            return Results.Ok(ApiResponse<SecurityPolicyDto>.Ok(policy));
        })
        .RequirePermission("system:security-policy:query");

        app.MapPut("/system/security-policy", async (
            UpdateSecurityPolicyRequest request,
            ISecurityPolicyAppService securityPolicyAppService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var policy = await securityPolicyAppService.UpdatePolicyAsync(request, cancellationToken);

                return Results.Ok(ApiResponse<SecurityPolicyDto>.Ok(policy));
            }
            catch (ArgumentOutOfRangeException exception)
            {
                return Results.BadRequest(ApiResponse<SecurityPolicyDto?>.Fail(exception.Message));
            }
        })
        .RequirePermission("system:security-policy:update");

        app.MapGet("/system/alert/list", async (
            int? page,
            int? pageSize,
            string? type,
            string? level,
            string? status,
            IAlertAppService alertAppService,
            CancellationToken cancellationToken) =>
        {
            var query = new AlertListQuery(
                Page: page ?? 1,
                PageSize: pageSize ?? 20,
                Type: type,
                Level: level,
                Status: status);
            var alerts = await alertAppService.GetListAsync(query, cancellationToken);

            return Results.Ok(ApiResponse<object>.Ok(alerts));
        })
        .RequirePermission("system:alert:query");

        app.MapGet("/system/alert-rule/list", async (
            int? page,
            int? pageSize,
            string? keyword,
            string? level,
            bool? enabled,
            IAlertRuleAppService alertRuleAppService,
            CancellationToken cancellationToken) =>
        {
            var query = new AlertRuleListQuery(
                Page: page ?? 1,
                PageSize: pageSize ?? 20,
                Keyword: keyword,
                Level: level,
                Enabled: enabled);
            var rules = await alertRuleAppService.GetListAsync(query, cancellationToken);

            return Results.Ok(ApiResponse<object>.Ok(rules));
        })
        .RequirePermission("system:alert-rule:query");

        app.MapPut("/system/alert-rule/{id:guid}", async (
            Guid id,
            UpdateAlertRuleRequest request,
            IAlertRuleAppService alertRuleAppService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var rule = await alertRuleAppService.UpdateAsync(id, request, cancellationToken);

                return rule is null
                    ? Results.NotFound(ApiResponse<AlertRuleDto?>.Fail("Alert rule not found."))
                    : Results.Ok(ApiResponse<AlertRuleDto>.Ok(rule));
            }
            catch (ArgumentOutOfRangeException exception)
            {
                return Results.BadRequest(ApiResponse<AlertRuleDto?>.Fail(exception.Message));
            }
        })
        .RequirePermission("system:alert-rule:update");

        app.MapPost("/system/alert/{id:guid}/acknowledge", async (
            Guid id,
            AcknowledgeAlertRequest request,
            IAlertAppService alertAppService,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var userName = httpContext.User.Identity?.Name ?? "system";
            var alert = await alertAppService.AcknowledgeAsync(id, userName, request, cancellationToken);

            return alert is null
                ? Results.NotFound(ApiResponse<AlertDto?>.Fail("Alert not found."))
                : Results.Ok(ApiResponse<AlertDto>.Ok(alert));
        })
        .RequirePermission("system:alert:acknowledge");

        app.MapGet("/system/permission-diagnostics/user/{userName}", async (
            string userName,
            IPermissionDiagnosticsAppService permissionDiagnosticsAppService,
            CancellationToken cancellationToken) =>
        {
            var diagnostics = await permissionDiagnosticsAppService.GetByUserNameAsync(
                userName,
                cancellationToken);

            return diagnostics is null
                ? Results.NotFound(ApiResponse<PermissionDiagnosticsDto?>.Fail("User not found."))
                : Results.Ok(ApiResponse<PermissionDiagnosticsDto>.Ok(diagnostics));
        })
        .RequirePermission("system:permission-diagnostics:query");

        app.MapPost("/system/permission-diagnostics/user/{userName}/refresh-cache", async (
            string userName,
            IPermissionDiagnosticsAppService permissionDiagnosticsAppService,
            CancellationToken cancellationToken) =>
        {
            var refreshed = await permissionDiagnosticsAppService.RefreshUserCacheAsync(
                userName,
                cancellationToken);

            return refreshed
                ? Results.Ok(ApiResponse<bool>.Ok(true))
                : Results.NotFound(ApiResponse<bool>.Fail("User not found."));
        })
        .RequirePermission("system:permission-diagnostics:refresh-cache");

        return app;
    }
}
