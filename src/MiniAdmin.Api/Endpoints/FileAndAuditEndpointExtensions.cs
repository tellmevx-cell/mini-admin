using System.Text;
using System.Text.Json;
using System.Security.Claims;
using MiniAdmin.Api.CodeGenerators;
using MiniAdmin.Api.RateLimiting;
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
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json.Serialization;
using static MiniAdmin.Api.Endpoints.EndpointHelpers;

namespace MiniAdmin.Api.Endpoints;

public static class FileAndAuditEndpointExtensions
{
    public static IEndpointRouteBuilder MapFileAndAuditEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/system/file/list", async (
            int? page,
            int? pageSize,
            string? originalName,
            string? storageProvider,
            IFileAppService fileAppService,
            CancellationToken cancellationToken) =>
        {
            var query = new FileListQuery(
                Page: page ?? 1,
                PageSize: pageSize ?? 20,
                OriginalName: originalName,
                StorageProvider: storageProvider);
            var files = await fileAppService.GetListAsync(query, cancellationToken);

            return Results.Ok(ApiResponse<object>.Ok(files));
        })
        .RequirePermission("system:file:query");

        app.MapPost("/system/file/upload", async (
            IFormFile file,
            IFileAppService fileAppService,
            CancellationToken cancellationToken) =>
        {
            if (file.Length == 0)
            {
                return Results.BadRequest(ApiResponse<FileDto?>.Fail("File is empty."));
            }

            await using var stream = file.OpenReadStream();
            FileDto uploaded;
            try
            {
                uploaded = await fileAppService.UploadAsync(
                    stream,
                    file.FileName,
                    file.ContentType,
                    file.Length,
                    cancellationToken);
            }
            catch (TenantResourceQuotaExceededException exception)
            {
                return Results.Conflict(ApiResponse<FileDto?>.Fail(exception.Message));
            }

            return Results.Ok(ApiResponse<FileDto>.Ok(uploaded));
        })
        .DisableAntiforgery()
        .RequireRateLimiting(MiniAdminRateLimitPolicyNames.Upload)
        .RequirePermission("system:file:upload");

        app.MapGet("/system/file/{id:guid}/download", async (
            Guid id,
            IFileAppService fileAppService,
            CancellationToken cancellationToken) =>
        {
            FileDownloadResult? file;
            try
            {
                file = await fileAppService.DownloadAsync(id, cancellationToken);
            }
            catch (FileUnavailableException exception)
            {
                return Results.Conflict(ApiResponse<FileDto?>.Fail(exception.Message));
            }

            if (file is null)
            {
                return Results.NotFound(ApiResponse<FileDto?>.Fail("File not found."));
            }

            return Results.File(file.Content, file.ContentType, file.OriginalName);
        })
        .RequirePermission("system:file:download");

        app.MapPost("/system/file/{id:guid}/mark-invalid", async (
            Guid id,
            IFileAppService fileAppService,
            CancellationToken cancellationToken) =>
        {
            var file = await fileAppService.MarkInvalidAsync(id, cancellationToken);

            return file is null
                ? Results.NotFound(ApiResponse<FileDto?>.Fail("File not found."))
                : Results.Ok(ApiResponse<FileDto>.Ok(file));
        })
        .RequirePermission("system:file:mark-invalid");

        app.MapDelete("/system/file/{id:guid}", async (
            Guid id,
            IFileAppService fileAppService,
            CancellationToken cancellationToken) =>
        {
            var deleted = await fileAppService.DeleteAsync(id, cancellationToken);

            return Results.Ok(ApiResponse<bool>.Ok(deleted));
        })
        .RequirePermission("system:file:delete");

        app.MapGet("/system/audit-log/list", async (
            ClaimsPrincipal principal,
            int? page,
            int? pageSize,
            string? userName,
            string? method,
            string? path,
            string? module,
            string? action,
            bool? isSuccess,
            DateTimeOffset? startCreatedAt,
            DateTimeOffset? endCreatedAt,
            IAuditLogAppService auditLogAppService,
            CancellationToken cancellationToken) =>
        {
            var query = new AuditLogListQuery(
                Page: page ?? 1,
                PageSize: pageSize ?? 20,
                UserName: userName,
                Method: method,
                Path: path,
                Module: module,
                Action: action,
                IsSuccess: isSuccess,
                StartCreatedAt: startCreatedAt,
                EndCreatedAt: endCreatedAt,
                CurrentUserName: GetRequiredUserName(principal));
            var logs = await auditLogAppService.GetListAsync(query, cancellationToken);

            return Results.Ok(ApiResponse<object>.Ok(logs));
        })
        .RequirePermission("system:log:query");

        app.MapGet("/system/audit-log/export", async (
            ClaimsPrincipal principal,
            string? userName,
            string? method,
            string? path,
            string? module,
            string? action,
            bool? isSuccess,
            DateTimeOffset? startCreatedAt,
            DateTimeOffset? endCreatedAt,
            IAuditLogAppService auditLogAppService,
            CancellationToken cancellationToken) =>
        {
            var query = new AuditLogListQuery(
                UserName: userName,
                Method: method,
                Path: path,
                Module: module,
                Action: action,
                IsSuccess: isSuccess,
                StartCreatedAt: startCreatedAt,
                EndCreatedAt: endCreatedAt,
                CurrentUserName: GetRequiredUserName(principal));
            var logs = await auditLogAppService.GetExportListAsync(query, 5000, cancellationToken);
            var fileName = $"audit-logs-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}.csv";

            return Results.File(
                BuildAuditLogCsv(logs),
                "text/csv; charset=utf-8",
                fileName);
        })
        .RequirePermission("system:log:export");

        app.MapGet("/system/login-log/list", async (
            ClaimsPrincipal user,
            int? page,
            int? pageSize,
            string? userName,
            bool? isSuccess,
            DateTimeOffset? startCreatedAt,
            DateTimeOffset? endCreatedAt,
            IOnlineUserAppService onlineUserAppService,
            CancellationToken cancellationToken) =>
        {
            var query = new LoginLogListQuery(
                Page: page ?? 1,
                PageSize: pageSize ?? 20,
                UserName: userName,
                IsSuccess: isSuccess,
                StartCreatedAt: startCreatedAt,
                EndCreatedAt: endCreatedAt,
                CurrentUserName: GetRequiredUserName(user));
            var logs = await onlineUserAppService.GetLoginLogsAsync(query, cancellationToken);

            return Results.Ok(ApiResponse<object>.Ok(logs));
        })
        .RequirePermission("system:login-log:query");

        return app;
    }
}
