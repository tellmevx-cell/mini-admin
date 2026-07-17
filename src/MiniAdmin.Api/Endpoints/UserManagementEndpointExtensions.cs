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
using MiniAdmin.Application.Contracts.TenantResourceQuotas;
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

public static class UserManagementEndpointExtensions
{
    public static IEndpointRouteBuilder MapUserManagementEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/system/user/list", async (
            ClaimsPrincipal principal,
            int? page,
            int? pageSize,
            string? userName,
            Guid? departmentId,
            Guid? positionId,
            IUserAppService userAppService,
            CancellationToken cancellationToken) =>
        {
            var query = new UserListQuery(
                Page: page ?? 1,
                PageSize: pageSize ?? 20,
                UserName: userName,
                DepartmentId: departmentId,
                PositionId: positionId,
                CurrentUserName: GetRequiredUserName(principal));
            var users = await userAppService.GetListAsync(query, cancellationToken);

            return Results.Ok(ApiResponse<object>.Ok(users));
        })
        .RequirePermission("system:user:query");

        app.MapGet("/system/user/export", async (
            ClaimsPrincipal principal,
            int? page,
            int? pageSize,
            string? userName,
            Guid? departmentId,
            Guid? positionId,
            IUserAppService userAppService,
            CancellationToken cancellationToken) =>
        {
            var query = new UserListQuery(
                Page: page ?? 1,
                PageSize: pageSize ?? 20,
                UserName: userName,
                DepartmentId: departmentId,
                PositionId: positionId,
                CurrentUserName: GetRequiredUserName(principal));
            var file = await userAppService.ExportAsync(query, cancellationToken);

            return Results.File(file.Content, file.ContentType, file.FileName);
        })
        .RequirePermission("system:user:export");

        app.MapGet("/system/user/import-template", async (
            IUserAppService userAppService,
            CancellationToken cancellationToken) =>
        {
            var file = await userAppService.GetImportTemplateAsync(cancellationToken);

            return Results.File(file.Content, file.ContentType, file.FileName);
        })
        .RequirePermission("system:user:import");

        app.MapPost("/system/user/import/preview", async (
            ClaimsPrincipal principal,
            IFormFile file,
            IUserAppService userAppService,
            CancellationToken cancellationToken) =>
        {
            if (file.Length == 0)
            {
                return Results.BadRequest(ApiResponse<UserImportResultDto?>.Fail("导入文件不能为空."));
            }

            await using var stream = file.OpenReadStream();
            UserImportResultDto result;
            try
            {
                result = await userAppService.PreviewImportAsync(
                    stream,
                    GetRequiredUserName(principal),
                    cancellationToken);
            }
            catch (TenantResourceQuotaExceededException exception)
            {
                return Results.Conflict(ApiResponse<UserImportResultDto?>.Fail(exception.Message));
            }

            return Results.Ok(ApiResponse<UserImportResultDto>.Ok(result));
        })
        .DisableAntiforgery()
        .RequirePermission("system:user:import");

        app.MapPost("/system/user/import/error-report", async (
            ClaimsPrincipal principal,
            IFormFile file,
            IUserAppService userAppService,
            CancellationToken cancellationToken) =>
        {
            if (file.Length == 0)
            {
                return Results.BadRequest(ApiResponse<UserImportResultDto?>.Fail("导入文件不能为空."));
            }

            await using var stream = file.OpenReadStream();
            var report = await userAppService.ExportImportErrorsAsync(
                stream,
                GetRequiredUserName(principal),
                cancellationToken);

            return Results.File(report.Content, report.ContentType, report.FileName);
        })
        .DisableAntiforgery()
        .RequirePermission("system:user:import");

        app.MapPost("/system/user/import", async (
            ClaimsPrincipal principal,
            IFormFile file,
            IUserAppService userAppService,
            CancellationToken cancellationToken) =>
        {
            if (file.Length == 0)
            {
                return Results.BadRequest(ApiResponse<UserImportResultDto?>.Fail("导入文件不能为空."));
            }

            await using var stream = file.OpenReadStream();
            UserImportResultDto result;
            try
            {
                result = await userAppService.ImportAsync(
                    stream,
                    GetRequiredUserName(principal),
                    cancellationToken);
            }
            catch (TenantResourceQuotaExceededException exception)
            {
                return Results.Conflict(ApiResponse<UserImportResultDto?>.Fail(exception.Message));
            }

            return Results.Ok(ApiResponse<UserImportResultDto>.Ok(result));
        })
        .DisableAntiforgery()
        .RequirePermission("system:user:import");

        app.MapPost("/system/user", async (
            CreateUserRequest request,
            IUserAppService userAppService,
            CancellationToken cancellationToken) =>
        {
            UserListItemDto user;
            try
            {
                user = await userAppService.CreateAsync(request, cancellationToken);
            }
            catch (UserOperationException exception)
            {
                return Results.BadRequest(ApiResponse<UserListItemDto?>.Fail(exception.Message));
            }
            catch (TenantResourceQuotaExceededException exception)
            {
                return Results.Conflict(ApiResponse<UserListItemDto?>.Fail(exception.Message));
            }

            return Results.Ok(ApiResponse<UserListItemDto>.Ok(user));
        })
        .RequirePermission("system:user:create");

        app.MapPut("/system/user/{id:guid}", async (
            ClaimsPrincipal principal,
            Guid id,
            UpdateUserRequest request,
            IUserAppService userAppService,
            CancellationToken cancellationToken) =>
        {
            UserListItemDto? user;
            try
            {
                user = await userAppService.UpdateAsync(
                    id,
                    GetRequiredUserName(principal),
                    request,
                    cancellationToken);
            }
            catch (UserOperationException exception)
            {
                return Results.BadRequest(ApiResponse<UserListItemDto?>.Fail(exception.Message));
            }

            return user is null
                ? Results.NotFound(ApiResponse<UserListItemDto?>.Fail("User not found."))
                : Results.Ok(ApiResponse<UserListItemDto>.Ok(user));
        })
        .RequirePermission("system:user:update");

        app.MapPost("/system/user/{userName}/unlock-login", async (
            string userName,
            ILoginSecurityService loginSecurityService,
            CancellationToken cancellationToken) =>
        {
            await loginSecurityService.UnlockUserAsync(userName, cancellationToken);

            return Results.Ok(ApiResponse<bool>.Ok(true));
        })
        .RequirePermission("system:user:unlock");

        app.MapPost("/system/user/{id:guid}/reset-password", async (
            Guid id,
            ResetUserPasswordRequest request,
            IUserAppService userAppService,
            CancellationToken cancellationToken) =>
        {
            var result = await userAppService.ResetPasswordAsync(id, request, cancellationToken);

            return ToPasswordOperationHttpResult(result);
        })
        .RequirePermission("system:user:reset-password");

        app.MapDelete("/system/user/{id:guid}", async (
            ClaimsPrincipal principal,
            Guid id,
            IUserAppService userAppService,
            CancellationToken cancellationToken) =>
        {
            var deleted = await userAppService.DeleteAsync(
                id,
                GetRequiredUserName(principal),
                cancellationToken);

            return deleted switch
            {
                DeleteUserResult.Deleted => Results.Ok(ApiResponse<bool>.Ok(true)),
                DeleteUserResult.NotFound => Results.NotFound(ApiResponse<bool>.Fail("User not found.")),
                DeleteUserResult.BuiltInAdmin => Results.BadRequest(ApiResponse<bool>.Fail("内置管理员不能删除.")),
                DeleteUserResult.CurrentUser => Results.BadRequest(ApiResponse<bool>.Fail("不能删除当前登录账户.")),
                DeleteUserResult.LastAdministrator => Results.BadRequest(ApiResponse<bool>.Fail("至少保留一个可用管理员.")),
                _ => Results.Json(
                    ApiResponse<bool>.Fail("没有权限删除其他部门账户."),
                    statusCode: StatusCodes.Status403Forbidden)
            };
        })
        .RequirePermission("system:user:delete");

        return app;
    }
}
