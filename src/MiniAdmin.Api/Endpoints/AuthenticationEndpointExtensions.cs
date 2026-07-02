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

public static class AuthenticationEndpointExtensions
{
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/auth/captcha", async (
            ILoginSecurityService loginSecurityService,
            CancellationToken cancellationToken) =>
        {
            var captcha = await loginSecurityService.CreateCaptchaAsync(cancellationToken);
            return Results.Ok(ApiResponse<CaptchaDto>.Ok(captcha));
        });

        app.MapPost("/auth/login", async (
            LoginRequest request,
            IAuthAppService authAppService,
            IOnlineUserAppService onlineUserAppService,
            ISecurityCenterAppService securityCenterAppService,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var clientIp = GetClientIpAddress(httpContext);
            LoginResult? result;
            try
            {
                result = await authAppService.LoginAsync(request with
                {
                    ClientIp = clientIp
                }, cancellationToken);
            }
            catch (LoginFailureException exception)
            {
                await onlineUserAppService.RecordLoginAsync(
                    new SaveLoginLogRequest(
                        request.Username,
                        false,
                        exception.Message,
                        clientIp,
                        httpContext.Request.Headers.UserAgent.ToString()),
                    cancellationToken);
                await securityCenterAppService.RecordEventAsync(
                    new SaveSecurityEventRequest(
                        exception.LockRemainingSeconds.HasValue ? "AccountLocked" : "LoginFailed",
                        "Warning",
                        exception.LockRemainingSeconds.HasValue ? "账号登录锁定" : "登录失败",
                        exception.Message,
                        UserName: request.Username,
                        IpAddress: clientIp,
                        UserAgent: httpContext.Request.Headers.UserAgent.ToString()),
                    cancellationToken);

                return Results.Json(
                    ApiResponse<LoginFailureResult>.Fail(
                        exception.Message,
                        new LoginFailureResult(exception.CaptchaRequired, exception.LockRemainingSeconds)),
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            await onlineUserAppService.RecordLoginAsync(
                new SaveLoginLogRequest(
                    request.Username,
                    result is not null,
                        result is null ? "登录失败" : "登录成功",
                        clientIp,
                        httpContext.Request.Headers.UserAgent.ToString(),
                        result is null ? null : Guid.Parse(result.SessionId)),
                cancellationToken);

            return result is null
                ? Results.Unauthorized()
                : Results.Ok(ApiResponse<LoginResult>.Ok(result));
        })
        .RequireRateLimiting(MiniAdminRateLimitPolicyNames.Login);

        app.MapPost("/auth/logout", async (
            ClaimsPrincipal principal,
            IOnlineUserAppService onlineUserAppService,
            CancellationToken cancellationToken) =>
        {
            await onlineUserAppService.SignOutAsync(GetRequiredSessionId(principal), cancellationToken);

            return Results.Ok(ApiResponse<bool>.Ok(true));
        })
        .RequireAuthorization();

        app.MapGet("/user/info", async (
            ClaimsPrincipal principal,
            IUserAppService userAppService,
            CancellationToken cancellationToken) =>
        {
            var userName = GetRequiredUserName(principal);
            var user = await userAppService.GetCurrentUserAsync(userName, cancellationToken);

            return Results.Ok(ApiResponse<CurrentUserDto>.Ok(user));
        })
        .RequireAuthorization();

        app.MapPost("/user/change-password", async (
            ClaimsPrincipal principal,
            ChangeCurrentUserPasswordRequest request,
            IUserAppService userAppService,
            CancellationToken cancellationToken) =>
        {
            var result = await userAppService.ChangePasswordAsync(
                GetRequiredUserName(principal),
                request,
                cancellationToken);

            return ToPasswordOperationHttpResult(result);
        })
        .RequireAuthorization();

        app.MapGet("/auth/codes", async (
            ClaimsPrincipal principal,
            IAuthAppService authAppService,
            CancellationToken cancellationToken) =>
        {
            var userName = GetRequiredUserName(principal);
            var codes = await authAppService.GetAccessCodesAsync(userName, cancellationToken);

            return Results.Ok(ApiResponse<IReadOnlyList<string>>.Ok(codes));
        })
        .RequireAuthorization();

        app.MapGet("/menu/all", async (
            ClaimsPrincipal principal,
            IMenuAppService menuAppService,
            CancellationToken cancellationToken) =>
        {
            var userName = GetRequiredUserName(principal);
            var menus = await menuAppService.GetAllMenusAsync(userName, cancellationToken);

            return Results.Ok(ApiResponse<IReadOnlyList<VbenMenuDto>>.Ok(menus));
        })
        .RequireAuthorization();

        return app;
    }
}
