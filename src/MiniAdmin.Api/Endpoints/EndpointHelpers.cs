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

internal static class EndpointHelpers
{
    internal static async Task<CodeGeneratorRollbackRequest> ReadRollbackRequestAsync(
        HttpRequest httpRequest,
        CancellationToken cancellationToken)
    {
        if (httpRequest.ContentLength.GetValueOrDefault() <= 0)
        {
            return new CodeGeneratorRollbackRequest();
        }

        return await JsonSerializer.DeserializeAsync<CodeGeneratorRollbackRequest>(
            httpRequest.Body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            cancellationToken) ?? new CodeGeneratorRollbackRequest();
    }

    internal static async Task<CodeGeneratorArtifactCleanupRequest> ReadArtifactCleanupRequestAsync(
        HttpRequest httpRequest,
        CancellationToken cancellationToken)
    {
        if (httpRequest.ContentLength.GetValueOrDefault() <= 0)
        {
            return new CodeGeneratorArtifactCleanupRequest();
        }

        return await JsonSerializer.DeserializeAsync<CodeGeneratorArtifactCleanupRequest>(
            httpRequest.Body,
            new JsonSerializerOptions(JsonSerializerDefaults.Web),
            cancellationToken) ?? new CodeGeneratorArtifactCleanupRequest();
    }

    internal static string GetRequiredUserName(ClaimsPrincipal principal)
    {
        return principal.Identity?.Name
            ?? principal.FindFirstValue(ClaimTypes.Name)
            ?? throw new InvalidOperationException("Authenticated user name is missing.");
    }

    internal static Guid GetRequiredUserId(ClaimsPrincipal principal)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userId, out var value)
            ? value
            : throw new InvalidOperationException("Authenticated user id is missing.");
    }

    internal static Guid GetRequiredSessionId(ClaimsPrincipal principal)
    {
        var sessionId = principal.FindFirstValue("session_id");
        return Guid.TryParse(sessionId, out var value)
            ? value
            : throw new InvalidOperationException("Authenticated session id is missing.");
    }

    internal static WorkflowUserContext GetWorkflowUserContext(ClaimsPrincipal principal)
    {
        return new WorkflowUserContext(
            GetRequiredUserId(principal),
            GetRequiredUserName(principal),
            HasPermission(principal, "workflow:definition:manage"));
    }

    internal static bool HasPermission(ClaimsPrincipal principal, string permissionCode)
    {
        return principal.Claims.Any(claim =>
            claim.Type == "permission" &&
            claim.Value.Equals(permissionCode, StringComparison.OrdinalIgnoreCase));
    }

    internal static string? GetClientIpAddress(HttpContext httpContext)
    {
        return httpContext.Connection.RemoteIpAddress?.ToString();
    }

    internal static IResult ToPasswordOperationHttpResult(PasswordOperationResult result)
    {
        return result.Status switch
        {
            PasswordOperationStatus.Succeeded => Results.Ok(ApiResponse<bool>.Ok(true)),
            PasswordOperationStatus.UserNotFound => Results.NotFound(ApiResponse<bool>.Fail(result.Message)),
            PasswordOperationStatus.OldPasswordIncorrect => Results.BadRequest(ApiResponse<bool>.Fail(result.Message)),
            PasswordOperationStatus.PasswordMismatch => Results.BadRequest(ApiResponse<bool>.Fail(result.Message)),
            PasswordOperationStatus.PasswordPolicyViolation => Results.BadRequest(ApiResponse<bool>.Fail(result.Message)),
            _ => Results.BadRequest(ApiResponse<bool>.Fail(result.Message))
        };
    }

    internal static byte[] BuildAuditLogCsv(IReadOnlyList<AuditLogDto> logs)
    {
        var builder = new StringBuilder();
        builder.AppendLine("CreatedAt,UserName,Method,Path,Module,Action,StatusCode,IsSuccess,IpAddress,ElapsedMilliseconds,RequestBody,ErrorMessage");

        foreach (var log in logs)
        {
            builder.AppendLine(string.Join(',', new[]
            {
                EscapeCsvCell(log.CreatedAt.ToString("O")),
                EscapeCsvCell(log.UserName),
                EscapeCsvCell(log.Method),
                EscapeCsvCell(log.Path),
                EscapeCsvCell(log.Module),
                EscapeCsvCell(log.Action),
                EscapeCsvCell(log.StatusCode.ToString()),
                EscapeCsvCell(log.IsSuccess.ToString()),
                EscapeCsvCell(log.IpAddress),
                EscapeCsvCell(log.ElapsedMilliseconds.ToString()),
                EscapeCsvCell(log.RequestBody),
                EscapeCsvCell(log.ErrorMessage)
            }));
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
    }

    internal static string EscapeCsvCell(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var normalized = value.Replace("\r", " ").Replace("\n", " ");
        if (normalized.Length > 0 && normalized[0] is '=' or '+' or '-' or '@')
        {
            normalized = $"'{normalized}";
        }

        return normalized.Contains(',') || normalized.Contains('"') || normalized.Contains(' ')
            ? $"\"{normalized.Replace("\"", "\"\"")}\""
            : normalized;
    }
}
