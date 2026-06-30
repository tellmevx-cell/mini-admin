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

public static class ProjectRuntimeEndpointExtensions
{
    public static IEndpointRouteBuilder MapProjectRuntimeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/system/project-runtime/overview", async (
            IProjectRuntimeAppService projectRuntimeAppService,
            CancellationToken cancellationToken) =>
        {
            var overview = await projectRuntimeAppService.GetOverviewAsync(cancellationToken);

            return Results.Ok(ApiResponse<ProjectRuntimeOverviewDto>.Ok(overview));
        })
        .RequirePermission("system:project-runtime:query");

        app.MapPost("/system/project-runtime/projects", async (
            SaveProjectRuntimeProjectRequest request,
            IProjectRuntimeAppService projectRuntimeAppService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var project = await projectRuntimeAppService.CreateProjectAsync(request, cancellationToken);
                return Results.Ok(ApiResponse<ProjectRuntimeProjectDto>.Ok(project));
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(ApiResponse<ProjectRuntimeProjectDto?>.Fail(exception.Message));
            }
        })
        .RequirePermission("system:project-runtime:manage");

        app.MapPut("/system/project-runtime/projects/{projectId:guid}", async (
            Guid projectId,
            SaveProjectRuntimeProjectRequest request,
            IProjectRuntimeAppService projectRuntimeAppService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var project = await projectRuntimeAppService.UpdateProjectAsync(projectId, request, cancellationToken);
                return project is null
                    ? Results.NotFound(ApiResponse<ProjectRuntimeProjectDto?>.Fail("项目不存在."))
                    : Results.Ok(ApiResponse<ProjectRuntimeProjectDto>.Ok(project));
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(ApiResponse<ProjectRuntimeProjectDto?>.Fail(exception.Message));
            }
        })
        .RequirePermission("system:project-runtime:manage");

        app.MapDelete("/system/project-runtime/projects/{projectId:guid}", async (
            Guid projectId,
            IProjectRuntimeAppService projectRuntimeAppService,
            CancellationToken cancellationToken) =>
        {
            var deleted = await projectRuntimeAppService.DeleteProjectAsync(projectId, cancellationToken);
            return Results.Ok(ApiResponse<bool>.Ok(deleted));
        })
        .RequirePermission("system:project-runtime:manage");

        app.MapPost("/system/project-runtime/workspaces/{workspaceId:guid}/start", async (
            Guid workspaceId,
            IProjectRuntimeAppService projectRuntimeAppService,
            CancellationToken cancellationToken) =>
        {
            var results = await projectRuntimeAppService.StartWorkspaceAsync(workspaceId, cancellationToken);
            return Results.Ok(ApiResponse<IReadOnlyList<ProjectRuntimeActionResultDto>>.Ok(results));
        })
        .RequirePermission("system:project-runtime:manage");

        app.MapPost("/system/project-runtime/workspaces/{workspaceId:guid}/stop", async (
            Guid workspaceId,
            IProjectRuntimeAppService projectRuntimeAppService,
            CancellationToken cancellationToken) =>
        {
            var results = await projectRuntimeAppService.StopWorkspaceAsync(workspaceId, cancellationToken);
            return Results.Ok(ApiResponse<IReadOnlyList<ProjectRuntimeActionResultDto>>.Ok(results));
        })
        .RequirePermission("system:project-runtime:manage");

        app.MapPost("/system/project-runtime/services/{serviceId:guid}/start", async (
            Guid serviceId,
            IProjectRuntimeAppService projectRuntimeAppService,
            CancellationToken cancellationToken) =>
        {
            var result = await projectRuntimeAppService.StartServiceAsync(serviceId, cancellationToken);
            return Results.Ok(ApiResponse<ProjectRuntimeActionResultDto>.Ok(result));
        })
        .RequirePermission("system:project-runtime:manage");

        app.MapPost("/system/project-runtime/services/{serviceId:guid}/stop", async (
            Guid serviceId,
            IProjectRuntimeAppService projectRuntimeAppService,
            CancellationToken cancellationToken) =>
        {
            var result = await projectRuntimeAppService.StopServiceAsync(serviceId, cancellationToken);
            return Results.Ok(ApiResponse<ProjectRuntimeActionResultDto>.Ok(result));
        })
        .RequirePermission("system:project-runtime:manage");

        app.MapPost("/system/project-runtime/services/{serviceId:guid}/restart", async (
            Guid serviceId,
            IProjectRuntimeAppService projectRuntimeAppService,
            CancellationToken cancellationToken) =>
        {
            var result = await projectRuntimeAppService.RestartServiceAsync(serviceId, cancellationToken);
            return Results.Ok(ApiResponse<ProjectRuntimeActionResultDto>.Ok(result));
        })
        .RequirePermission("system:project-runtime:manage");

        app.MapPost("/system/project-runtime/services/{serviceId:guid}/build", async (
            Guid serviceId,
            IProjectRuntimeAppService projectRuntimeAppService,
            CancellationToken cancellationToken) =>
        {
            var result = await projectRuntimeAppService.BuildServiceAsync(serviceId, cancellationToken);
            return Results.Ok(ApiResponse<ProjectRuntimeActionResultDto>.Ok(result));
        })
        .RequirePermission("system:project-runtime:manage");

        app.MapGet("/system/project-runtime/services/{serviceId:guid}/logs", async (
            Guid serviceId,
            int? lines,
            IProjectRuntimeAppService projectRuntimeAppService,
            CancellationToken cancellationToken) =>
        {
            var logs = await projectRuntimeAppService.GetServiceLogsAsync(serviceId, lines ?? 200, cancellationToken);
            return Results.Ok(ApiResponse<ProjectRuntimeLogDto>.Ok(logs));
        })
        .RequirePermission("system:project-runtime:log");

        app.MapGet("/system/project-runtime/services/{serviceId:guid}/build-logs", async (
            Guid serviceId,
            int? lines,
            IProjectRuntimeAppService projectRuntimeAppService,
            CancellationToken cancellationToken) =>
        {
            var logs = await projectRuntimeAppService.GetServiceBuildLogsAsync(serviceId, lines ?? 200, cancellationToken);
            return Results.Ok(ApiResponse<ProjectRuntimeLogDto>.Ok(logs));
        })
        .RequirePermission("system:project-runtime:log");

        app.MapGet("/system/project-runtime/services/{serviceId:guid}/build-history", async (
            Guid serviceId,
            int? take,
            IProjectRuntimeAppService projectRuntimeAppService,
            CancellationToken cancellationToken) =>
        {
            var history = await projectRuntimeAppService.GetServiceBuildHistoryAsync(serviceId, take ?? 20, cancellationToken);
            return Results.Ok(ApiResponse<IReadOnlyList<ProjectRuntimeBuildHistoryDto>>.Ok(history));
        })
        .RequirePermission("system:project-runtime:log");

        app.MapGet("/system/project-runtime/services/{serviceId:guid}/artifact", async (
            Guid serviceId,
            IProjectRuntimeAppService projectRuntimeAppService,
            CancellationToken cancellationToken) =>
        {
            var artifact = await projectRuntimeAppService.GetServiceArtifactAsync(serviceId, cancellationToken);
            return Results.Ok(ApiResponse<ProjectRuntimeArtifactDto>.Ok(artifact));
        })
        .RequirePermission("system:project-runtime:log");

        app.MapPost("/system/project-runtime/services/{serviceId:guid}/artifact/open", async (
            Guid serviceId,
            IProjectRuntimeAppService projectRuntimeAppService,
            CancellationToken cancellationToken) =>
        {
            var result = await projectRuntimeAppService.OpenServiceArtifactAsync(serviceId, cancellationToken);
            return Results.Ok(ApiResponse<ProjectRuntimeActionResultDto>.Ok(result));
        })
        .RequirePermission("system:project-runtime:manage");

        return app;
    }
}
