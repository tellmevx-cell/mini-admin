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

public static class CodeGeneratorEndpointExtensions
{
    public static IEndpointRouteBuilder MapCodeGeneratorEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/system/code-generator/tables", async (
            ICodeGeneratorAppService codeGeneratorAppService,
            CancellationToken cancellationToken) =>
        {
            var tables = await codeGeneratorAppService.GetTablesAsync(cancellationToken);

            return Results.Ok(ApiResponse<IReadOnlyList<CodeGeneratorTableDto>>.Ok(tables));
        })
        .RequirePermission("system:code-generator:query");

        app.MapGet("/system/code-generator/tables/{tableName}", async (
            string tableName,
            ICodeGeneratorAppService codeGeneratorAppService,
            CancellationToken cancellationToken) =>
        {
            var table = await codeGeneratorAppService.GetTableAsync(tableName, cancellationToken);

            return table is null
                ? Results.NotFound(ApiResponse<CodeGeneratorTableDto?>.Fail("Table not found."))
                : Results.Ok(ApiResponse<CodeGeneratorTableDto>.Ok(table));
        })
        .RequirePermission("system:code-generator:query");

        app.MapPost("/system/code-generator/preview", async (
            CodeGeneratorPreviewRequest request,
            ICodeGeneratorAppService codeGeneratorAppService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var preview = await codeGeneratorAppService.PreviewAsync(request, cancellationToken);

                return Results.Ok(ApiResponse<CodeGeneratorPreviewResultDto>.Ok(preview));
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(ApiResponse<CodeGeneratorPreviewResultDto?>.Fail(exception.Message));
            }
        })
        .RequirePermission("system:code-generator:preview");

        app.MapPost("/system/code-generator/generate", async (
            CodeGeneratorGenerateRequest request,
            ClaimsPrincipal user,
            ICodeGeneratorAppService codeGeneratorAppService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var history = await codeGeneratorAppService.GenerateAsync(
                    request,
                    GetRequiredUserId(user),
                    GetRequiredUserName(user),
                    cancellationToken);

                return Results.Ok(ApiResponse<CodeGenerationHistoryDto>.Ok(history));
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(ApiResponse<CodeGenerationHistoryDto?>.Fail(exception.Message));
            }
        })
        .RequirePermission("system:code-generator:generate");

        app.MapGet("/system/code-generator/artifacts", async (
            ICodeGeneratorAppService codeGeneratorAppService,
            CancellationToken cancellationToken) =>
        {
            var result = await codeGeneratorAppService.GetArtifactGovernanceAsync(cancellationToken);
            return Results.Ok(ApiResponse<CodeGeneratorArtifactGovernanceResultDto>.Ok(result));
        })
        .RequirePermission("system:code-generator:query");

        app.MapPost("/system/code-generator/artifacts/{moduleName}/cleanup", async (
            string moduleName,
            HttpRequest httpRequest,
            ClaimsPrincipal user,
            ICodeGeneratorAppService codeGeneratorAppService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var request = await ReadArtifactCleanupRequestAsync(httpRequest, cancellationToken);
                var result = await codeGeneratorAppService.CleanupArtifactAsync(
                    moduleName,
                    request,
                    GetRequiredUserId(user),
                    GetRequiredUserName(user),
                    cancellationToken);
                return Results.Ok(ApiResponse<CodeGeneratorArtifactCleanupResultDto>.Ok(result));
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(ApiResponse<CodeGeneratorArtifactCleanupResultDto?>.Fail(exception.Message));
            }
        })
        .RequireAnyPermission("system:code-generator:rollback", "system:code-generator:generate");

        app.MapPost("/system/code-generator/artifacts/{moduleName}/register-history", async (
            string moduleName,
            ClaimsPrincipal user,
            ICodeGeneratorAppService codeGeneratorAppService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await codeGeneratorAppService.RegisterArtifactHistoryAsync(
                    moduleName,
                    GetRequiredUserId(user),
                    GetRequiredUserName(user),
                    cancellationToken);
                return Results.Ok(ApiResponse<CodeGeneratorArtifactRegisterHistoryResultDto>.Ok(result));
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(ApiResponse<CodeGeneratorArtifactRegisterHistoryResultDto?>.Fail(exception.Message));
            }
        })
        .RequirePermission("system:code-generator:generate");

        app.MapGet("/system/code-generator/history", async (
            int? page,
            int? pageSize,
            string? moduleName,
            string? tableName,
            string? status,
            ICodeGeneratorAppService codeGeneratorAppService,
            CancellationToken cancellationToken) =>
        {
            var histories = await codeGeneratorAppService.GetHistoriesAsync(
                new CodeGeneratorHistoryListQuery(
                    page ?? 1,
                    pageSize ?? 20,
                    moduleName,
                    tableName,
                    status),
                cancellationToken);

            return Results.Ok(ApiResponse<object>.Ok(histories));
        })
        .RequirePermission("system:code-generator:query");

        app.MapGet("/system/code-generator/history/{id:guid}", async (
            Guid id,
            ICodeGeneratorAppService codeGeneratorAppService,
            CancellationToken cancellationToken) =>
        {
            var detail = await codeGeneratorAppService.GetHistoryDetailAsync(id, cancellationToken);
            return detail is null
                ? Results.NotFound(ApiResponse<CodeGenerationHistoryDetailDto?>.Fail("生成记录不存在。"))
                : Results.Ok(ApiResponse<CodeGenerationHistoryDetailDto>.Ok(detail));
        })
        .RequirePermission("system:code-generator:query");

        app.MapPost("/system/code-generator/history/{id:guid}/rollback", async (
            Guid id,
            HttpRequest httpRequest,
            ClaimsPrincipal user,
            ICodeGeneratorAppService codeGeneratorAppService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var rollbackRequest = await ReadRollbackRequestAsync(httpRequest, cancellationToken);
                var result = await codeGeneratorAppService.RollbackAsync(
                    id,
                    rollbackRequest,
                    GetRequiredUserId(user),
                    GetRequiredUserName(user),
                    cancellationToken);

                return Results.Ok(ApiResponse<CodeGeneratorRollbackResultDto>.Ok(result));
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(ApiResponse<CodeGeneratorRollbackResultDto?>.Fail(exception.Message));
            }
        })
        .RequireAnyPermission("system:code-generator:rollback", "system:code-generator:generate");

        return app;
    }
}
