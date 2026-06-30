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

public static class NotificationEndpointExtensions
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/notification/my", async (
            ClaimsPrincipal principal,
            int? take,
            int? page,
            int? pageSize,
            bool? isRead,
            string? category,
            string? sourceType,
            IUserNotificationAppService userNotificationAppService,
            CancellationToken cancellationToken) =>
        {
            var result = await userNotificationAppService.GetListAsync(
                GetRequiredUserId(principal),
                new UserNotificationListQuery(
                    take,
                    page ?? 1,
                    pageSize ?? 20,
                    isRead,
                    category,
                    sourceType),
                cancellationToken);

            return Results.Ok(ApiResponse<UserNotificationListResult>.Ok(result));
        })
        .RequireAuthorization();

        app.MapGet("/notification/channels/overview", async (
            ClaimsPrincipal principal,
            INotificationDeliveryService notificationDeliveryService,
            CancellationToken cancellationToken) =>
        {
            var result = await notificationDeliveryService.GetChannelOverviewAsync(
                GetRequiredUserId(principal),
                cancellationToken);

            return Results.Ok(ApiResponse<NotificationChannelOverviewDto>.Ok(result));
        })
        .RequireAuthorization();

        app.MapGet("/notification/deliveries", async (
            int? page,
            int? pageSize,
            string? channel,
            string? status,
            string? sourceType,
            INotificationDeliveryService notificationDeliveryService,
            CancellationToken cancellationToken) =>
        {
            var result = await notificationDeliveryService.GetListAsync(
                new NotificationDeliveryListQuery(
                    Page: page ?? 1,
                    PageSize: pageSize ?? 20,
                    Channel: channel,
                    Status: status,
                    SourceType: sourceType),
                cancellationToken);

            return Results.Ok(ApiResponse<PageResult<NotificationDeliveryDto>>.Ok(result));
        })
        .RequirePermission("system:notification:query");

        app.MapPost("/notification/deliveries/{id:guid}/retry", async (
            Guid id,
            INotificationDeliveryService notificationDeliveryService,
            CancellationToken cancellationToken) =>
        {
            var result = await notificationDeliveryService.RetryAsync(id, cancellationToken);

            return result is null
                ? Results.NotFound(ApiResponse<string>.Fail("投递记录不存在"))
                : Results.Ok(ApiResponse<NotificationDeliveryDto>.Ok(result));
        })
        .RequirePermission("system:notification:retry");

        app.MapGet("/notification/templates", async (
            int? page,
            int? pageSize,
            string? keyword,
            string? category,
            string? code,
            bool? isEnabled,
            INotificationTemplateAppService notificationTemplateAppService,
            CancellationToken cancellationToken) =>
        {
            var result = await notificationTemplateAppService.GetListAsync(
                new NotificationTemplateListQuery(
                    Page: page ?? 1,
                    PageSize: pageSize ?? 20,
                    Keyword: keyword,
                    Category: category,
                    Code: code,
                    IsEnabled: isEnabled),
                cancellationToken);

            return Results.Ok(ApiResponse<PageResult<NotificationTemplateDto>>.Ok(result));
        })
        .RequirePermission("system:notification:query");

        app.MapPost("/notification/templates/preview", async (
            PreviewNotificationTemplateRequest request,
            INotificationTemplateAppService notificationTemplateAppService,
            CancellationToken cancellationToken) =>
        {
            var result = await notificationTemplateAppService.PreviewAsync(request, cancellationToken);

            return Results.Ok(ApiResponse<NotificationTemplatePreviewDto>.Ok(result));
        })
        .RequirePermission("system:notification:query");

        app.MapPost("/notification/templates/{id:guid}", async (
            Guid id,
            SaveNotificationTemplateRequest request,
            INotificationTemplateAppService notificationTemplateAppService,
            CancellationToken cancellationToken) =>
        {
            var result = await notificationTemplateAppService.UpdateAsync(id, request, cancellationToken);

            return result is null
                ? Results.NotFound(ApiResponse<NotificationTemplateDto>.Fail("Notification template not found."))
                : Results.Ok(ApiResponse<NotificationTemplateDto>.Ok(result));
        })
        .RequirePermission("system:notification:template:update");

        app.MapGet("/notification/policies", async (
            int? page,
            int? pageSize,
            string? keyword,
            string? category,
            string? eventCode,
            bool? isEnabled,
            INotificationPolicyAppService notificationPolicyAppService,
            CancellationToken cancellationToken) =>
        {
            var result = await notificationPolicyAppService.GetListAsync(
                new NotificationPolicyListQuery(
                    Page: page ?? 1,
                    PageSize: pageSize ?? 20,
                    Keyword: keyword,
                    Category: category,
                    EventCode: eventCode,
                    IsEnabled: isEnabled),
                cancellationToken);

            return Results.Ok(ApiResponse<PageResult<NotificationPolicyDto>>.Ok(result));
        })
        .RequirePermission("system:notification:query");

        app.MapPost("/notification/policies/{id:guid}", async (
            Guid id,
            SaveNotificationPolicyRequest request,
            INotificationPolicyAppService notificationPolicyAppService,
            CancellationToken cancellationToken) =>
        {
            var result = await notificationPolicyAppService.UpdateAsync(id, request, cancellationToken);

            return result is null
                ? Results.NotFound(ApiResponse<NotificationPolicyDto>.Fail("Notification policy not found."))
                : Results.Ok(ApiResponse<NotificationPolicyDto>.Ok(result));
        })
        .RequirePermission("system:notification:policy:update");

        app.MapGet("/notification/subscriptions/my", async (
            ClaimsPrincipal principal,
            string? keyword,
            string? category,
            INotificationSubscriptionAppService notificationSubscriptionAppService,
            CancellationToken cancellationToken) =>
        {
            var result = await notificationSubscriptionAppService.GetMyAsync(
                GetRequiredUserId(principal),
                new NotificationSubscriptionListQuery(keyword, category),
                cancellationToken);

            return Results.Ok(ApiResponse<NotificationSubscriptionListResult>.Ok(result));
        })
        .RequireAuthorization();

        app.MapPost("/notification/subscriptions/my/{eventCode}", async (
            string eventCode,
            SaveNotificationSubscriptionRequest request,
            ClaimsPrincipal principal,
            INotificationSubscriptionAppService notificationSubscriptionAppService,
            CancellationToken cancellationToken) =>
        {
            var result = await notificationSubscriptionAppService.SaveMyAsync(
                GetRequiredUserId(principal),
                eventCode,
                request,
                cancellationToken);

            return result is null
                ? Results.NotFound(ApiResponse<NotificationSubscriptionDto>.Fail("Notification event not found."))
                : Results.Ok(ApiResponse<NotificationSubscriptionDto>.Ok(result));
        })
        .RequireAuthorization();

        app.MapDelete("/notification/subscriptions/my", async (
            ClaimsPrincipal principal,
            INotificationSubscriptionAppService notificationSubscriptionAppService,
            CancellationToken cancellationToken) =>
        {
            var resetCount = await notificationSubscriptionAppService.ResetAllMyAsync(
                GetRequiredUserId(principal),
                cancellationToken);

            return Results.Ok(ApiResponse<int>.Ok(resetCount));
        })
        .RequireAuthorization();

        app.MapDelete("/notification/subscriptions/my/{eventCode}", async (
            string eventCode,
            ClaimsPrincipal principal,
            INotificationSubscriptionAppService notificationSubscriptionAppService,
            CancellationToken cancellationToken) =>
        {
            var success = await notificationSubscriptionAppService.ResetMyAsync(
                GetRequiredUserId(principal),
                eventCode,
                cancellationToken);

            return Results.Ok(ApiResponse<bool>.Ok(success));
        })
        .RequireAuthorization();

        app.MapPost("/notification/{id:guid}/read", async (
            Guid id,
            ClaimsPrincipal principal,
            IUserNotificationAppService userNotificationAppService,
            CancellationToken cancellationToken) =>
        {
            var success = await userNotificationAppService.MarkReadAsync(
                GetRequiredUserId(principal),
                id,
                cancellationToken);

            return success
                ? Results.Ok(ApiResponse<bool>.Ok(true))
                : Results.NotFound(ApiResponse<bool>.Fail("Notification not found."));
        })
        .RequireAuthorization();

        app.MapPost("/notification/read-all", async (
            ClaimsPrincipal principal,
            IUserNotificationAppService userNotificationAppService,
            CancellationToken cancellationToken) =>
        {
            var count = await userNotificationAppService.MarkAllReadAsync(
                GetRequiredUserId(principal),
                cancellationToken);

            return Results.Ok(ApiResponse<int>.Ok(count));
        })
        .RequireAuthorization();

        app.MapDelete("/notification/{id:guid}", async (
            Guid id,
            ClaimsPrincipal principal,
            IUserNotificationAppService userNotificationAppService,
            CancellationToken cancellationToken) =>
        {
            var success = await userNotificationAppService.DeleteAsync(
                GetRequiredUserId(principal),
                id,
                cancellationToken);

            return success
                ? Results.Ok(ApiResponse<bool>.Ok(true))
                : Results.NotFound(ApiResponse<bool>.Fail("Notification not found."));
        })
        .RequireAuthorization();

        app.MapDelete("/notification/all", async (
            ClaimsPrincipal principal,
            IUserNotificationAppService userNotificationAppService,
            CancellationToken cancellationToken) =>
        {
            var count = await userNotificationAppService.DeleteAllAsync(
                GetRequiredUserId(principal),
                cancellationToken);

            return Results.Ok(ApiResponse<int>.Ok(count));
        })
        .RequireAuthorization();

        return app;
    }
}
