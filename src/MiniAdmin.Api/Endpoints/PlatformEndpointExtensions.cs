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

public static class PlatformEndpointExtensions
{
    public static IEndpointRouteBuilder MapPlatformEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/platform/tenant/list", async (
            int? page,
            int? pageSize,
            string? code,
            string? name,
            string? status,
            ITenantAppService tenantAppService,
            CancellationToken cancellationToken) =>
        {
            var result = await tenantAppService.GetListAsync(
                new TenantListQuery(page ?? 1, pageSize ?? 10, code, name, status),
                cancellationToken);

            return Results.Ok(ApiResponse<PageResult<TenantDto>>.Ok(result));
        })
        .RequirePermission("platform:tenant:query");

        app.MapGet("/platform/tenant/initialization-templates", async (
            ITenantAppService tenantAppService,
            CancellationToken cancellationToken) =>
        {
            var templates = await tenantAppService.GetInitializationTemplatesAsync(cancellationToken);

            return Results.Ok(ApiResponse<IReadOnlyList<TenantInitializationTemplateDto>>.Ok(templates));
        })
        .RequirePermission("platform:tenant:query");

        app.MapGet("/auth/tenant-options", async (
            ITenantAppService tenantAppService,
            CancellationToken cancellationToken) =>
        {
            var tenants = await tenantAppService.GetLoginOptionsAsync(cancellationToken);

            return Results.Ok(ApiResponse<IReadOnlyList<TenantLoginOptionDto>>.Ok(tenants));
        });

        app.MapPost("/platform/tenant", async (
            CreateTenantRequest request,
            ITenantAppService tenantAppService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var tenant = await tenantAppService.CreateAsync(request, cancellationToken);
                return Results.Ok(ApiResponse<TenantDto>.Ok(tenant));
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(ApiResponse<TenantDto?>.Fail(exception.Message));
            }
        })
        .RequirePermission("platform:tenant:create");

        app.MapPut("/platform/tenant/{id:guid}", async (
            Guid id,
            UpdateTenantRequest request,
            ITenantAppService tenantAppService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var tenant = await tenantAppService.UpdateAsync(id, request, cancellationToken);
                return tenant is null
                    ? Results.NotFound(ApiResponse<TenantDto>.Fail("Tenant not found."))
                    : Results.Ok(ApiResponse<TenantDto>.Ok(tenant));
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(ApiResponse<TenantDto?>.Fail(exception.Message));
            }
        })
        .RequirePermission("platform:tenant:update");

        app.MapPost("/platform/tenant/{id:guid}/enable", async (
            Guid id,
            ITenantAppService tenantAppService,
            CancellationToken cancellationToken) =>
        {
            var tenant = await tenantAppService.EnableAsync(id, cancellationToken);
            return tenant is null
                ? Results.NotFound(ApiResponse<TenantDto>.Fail("Tenant not found."))
                : Results.Ok(ApiResponse<TenantDto>.Ok(tenant));
        })
        .RequirePermission("platform:tenant:enable");

        app.MapPost("/platform/tenant/{id:guid}/disable", async (
            Guid id,
            ITenantAppService tenantAppService,
            CancellationToken cancellationToken) =>
        {
            var tenant = await tenantAppService.DisableAsync(id, cancellationToken);
            return tenant is null
                ? Results.NotFound(ApiResponse<TenantDto>.Fail("Tenant not found."))
                : Results.Ok(ApiResponse<TenantDto>.Ok(tenant));
        })
        .RequirePermission("platform:tenant:disable");

        app.MapGet("/platform/tenant-package/list", async (
            int? page,
            int? pageSize,
            string? name,
            bool? isEnabled,
            ITenantPackageAppService tenantPackageAppService,
            CancellationToken cancellationToken) =>
        {
            var result = await tenantPackageAppService.GetListAsync(
                new TenantPackageListQuery(page ?? 1, pageSize ?? 10, name, isEnabled),
                cancellationToken);

            return Results.Ok(ApiResponse<PageResult<TenantPackageDto>>.Ok(result));
        })
        .RequirePermission("platform:tenant:query");

        app.MapGet("/platform/tenant-package/options", async (
            ITenantPackageAppService tenantPackageAppService,
            CancellationToken cancellationToken) =>
        {
            var result = await tenantPackageAppService.GetOptionsAsync(cancellationToken);

            return Results.Ok(ApiResponse<IReadOnlyList<TenantPackageOptionDto>>.Ok(result));
        })
        .RequirePermission("platform:tenant:query");

        app.MapPost("/platform/tenant-package", async (
            SaveTenantPackageRequest request,
            ITenantPackageAppService tenantPackageAppService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await tenantPackageAppService.CreateAsync(request, cancellationToken);
                return Results.Ok(ApiResponse<TenantPackageDto>.Ok(result));
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(ApiResponse<TenantPackageDto?>.Fail(exception.Message));
            }
        })
        .RequirePermission("platform:tenant:create");

        app.MapPut("/platform/tenant-package/{id:guid}", async (
            Guid id,
            SaveTenantPackageRequest request,
            ITenantPackageAppService tenantPackageAppService,
            CancellationToken cancellationToken) =>
        {
            var result = await tenantPackageAppService.UpdateAsync(id, request, cancellationToken);
            return result is null
                ? Results.NotFound(ApiResponse<TenantPackageDto>.Fail("Tenant package not found."))
                : Results.Ok(ApiResponse<TenantPackageDto>.Ok(result));
        })
        .RequirePermission("platform:tenant:update");

        app.MapPost("/platform/tenant-package/{id:guid}/enable", async (
            Guid id,
            ITenantPackageAppService tenantPackageAppService,
            CancellationToken cancellationToken) =>
        {
            var result = await tenantPackageAppService.SetEnabledAsync(id, true, cancellationToken);
            return result is null
                ? Results.NotFound(ApiResponse<TenantPackageDto>.Fail("Tenant package not found."))
                : Results.Ok(ApiResponse<TenantPackageDto>.Ok(result));
        })
        .RequirePermission("platform:tenant:enable");

        app.MapPost("/platform/tenant-package/{id:guid}/disable", async (
            Guid id,
            ITenantPackageAppService tenantPackageAppService,
            CancellationToken cancellationToken) =>
        {
            var result = await tenantPackageAppService.SetEnabledAsync(id, false, cancellationToken);
            return result is null
                ? Results.NotFound(ApiResponse<TenantPackageDto>.Fail("Tenant package not found."))
                : Results.Ok(ApiResponse<TenantPackageDto>.Ok(result));
        })
        .RequirePermission("platform:tenant:disable");

        app.MapGet("/platform/tenant-package/{id:guid}/menus", async (
            Guid id,
            ITenantPackageAppService tenantPackageAppService,
            CancellationToken cancellationToken) =>
        {
            var result = await tenantPackageAppService.GetMenuIdsAsync(id, cancellationToken);

            return Results.Ok(ApiResponse<IReadOnlyList<string>>.Ok(result));
        })
        .RequirePermission("platform:tenant:query");

        app.MapPut("/platform/tenant-package/{id:guid}/menus", async (
            Guid id,
            UpdateTenantPackageMenusRequest request,
            ITenantPackageAppService tenantPackageAppService,
            CancellationToken cancellationToken) =>
        {
            var result = await tenantPackageAppService.UpdateMenuIdsAsync(id, request.MenuIds, cancellationToken);

            return Results.Ok(ApiResponse<IReadOnlyList<string>>.Ok(result));
        })
        .RequirePermission("platform:tenant:update");

        return app;
    }
}
