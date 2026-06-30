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

public static class RoleEndpointExtensions
{
    public static IEndpointRouteBuilder MapRoleEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/system/role/list", async (
            int? page,
            int? pageSize,
            string? code,
            string? name,
            IRoleAppService roleAppService,
            CancellationToken cancellationToken) =>
        {
            var query = new RoleListQuery(
                Page: page ?? 1,
                PageSize: pageSize ?? 20,
                Code: code,
                Name: name);
            var roles = await roleAppService.GetListAsync(query, cancellationToken);

            return Results.Ok(ApiResponse<object>.Ok(roles));
        })
        .RequirePermission("system:role:query");

        app.MapPost("/system/role", async (
            CreateRoleRequest request,
            IRoleAppService roleAppService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var role = await roleAppService.CreateAsync(request, cancellationToken);

                return Results.Ok(ApiResponse<RoleListItemDto>.Ok(role));
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(ApiResponse<RoleListItemDto?>.Fail(exception.Message));
            }
        })
        .RequirePermission("system:role:create");

        app.MapGet("/system/role/{id:guid}/menus", async (
            Guid id,
            IRoleAppService roleAppService,
            CancellationToken cancellationToken) =>
        {
            var menuIds = await roleAppService.GetMenuIdsAsync(id, cancellationToken);

            return Results.Ok(ApiResponse<IReadOnlyList<string>>.Ok(menuIds));
        })
        .RequirePermission("system:role:assign");

        app.MapPut("/system/role/{id:guid}/menus", async (
            Guid id,
            UpdateRoleMenusRequest request,
            IRoleAppService roleAppService,
            CancellationToken cancellationToken) =>
        {
            var menuIds = await roleAppService.UpdateMenuIdsAsync(id, request, cancellationToken);

            return Results.Ok(ApiResponse<IReadOnlyList<string>>.Ok(menuIds));
        })
        .RequirePermission("system:role:assign");

        app.MapPut("/system/role/{id:guid}", async (
            Guid id,
            UpdateRoleRequest request,
            IRoleAppService roleAppService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var role = await roleAppService.UpdateAsync(id, request, cancellationToken);

                return role is null
                    ? Results.NotFound(ApiResponse<RoleListItemDto?>.Fail("Role not found."))
                    : Results.Ok(ApiResponse<RoleListItemDto>.Ok(role));
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(ApiResponse<RoleListItemDto?>.Fail(exception.Message));
            }
        })
        .RequirePermission("system:role:update");

        app.MapDelete("/system/role/{id:guid}", async (
            Guid id,
            IRoleAppService roleAppService,
            CancellationToken cancellationToken) =>
        {
            var deleted = await roleAppService.DeleteAsync(id, cancellationToken);

            return Results.Ok(ApiResponse<bool>.Ok(deleted));
        })
        .RequirePermission("system:role:delete");

        return app;
    }
}
