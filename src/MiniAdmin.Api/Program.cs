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

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is missing.");
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience is missing.");
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey is missing.");
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var sessionIdValue = context.Principal?.FindFirstValue("session_id");
                var securityStamp = context.Principal?.FindFirstValue("security_stamp");
                if (!Guid.TryParse(userIdValue, out var userId) ||
                    !Guid.TryParse(sessionIdValue, out var sessionId) ||
                    string.IsNullOrWhiteSpace(securityStamp))
                {
                    context.Fail("Token security session is missing.");
                    return;
                }

                var userAuthorizationCache =
                    context.HttpContext.RequestServices.GetRequiredService<IUserAuthorizationCache>();
                var dbContext = context.HttpContext.RequestServices.GetRequiredService<MiniAdminDbContext>();
                var currentSecurityStamp = await userAuthorizationCache.GetSecurityStampAsync(
                    userId,
                    async cancellationToken => await dbContext.Users
                        .AsNoTracking()
                        .Where(user => user.Id == userId && user.IsEnabled)
                        .Select(user => user.SecurityStamp)
                        .SingleOrDefaultAsync(cancellationToken),
                    context.HttpContext.RequestAborted);

                if (!string.Equals(currentSecurityStamp, securityStamp, StringComparison.Ordinal))
                {
                    context.Fail("Token security stamp is invalid.");
                    return;
                }

                var tenantIdValue = context.Principal?.FindFirstValue("tenant_id");
                if (!string.IsNullOrWhiteSpace(tenantIdValue))
                {
                    if (!Guid.TryParse(tenantIdValue, out var tenantId))
                    {
                        context.Fail("Token tenant is invalid.");
                        return;
                    }

                    var tenantRepository =
                        context.HttpContext.RequestServices.GetRequiredService<ITenantRepository>();
                    var tenant = await tenantRepository.FindByIdAsync(tenantId, context.HttpContext.RequestAborted);
                    if (tenant is null ||
                        tenant.Status != TenantStatus.Active ||
                        (tenant.ExpireAt.HasValue && tenant.ExpireAt.Value <= DateTimeOffset.UtcNow))
                    {
                        context.Fail("Token tenant is disabled or expired.");
                        return;
                    }
                }

                var onlineUserAppService =
                    context.HttpContext.RequestServices.GetRequiredService<IOnlineUserAppService>();
                var sessionActive = await onlineUserAppService.TouchAsync(
                    sessionId,
                    userId,
                    context.Principal?.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
                    GetClientIpAddress(context.HttpContext),
                    context.HttpContext.Request.Headers.UserAgent.ToString(),
                    context.HttpContext.RequestAborted);
                if (!sessionActive)
                {
                    context.Fail("Token session is offline.");
                }
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
{
    options.AddPolicy("VbenDev", policy =>
    {
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetIsOriginAllowed(origin =>
                origin.StartsWith("http://localhost:", StringComparison.OrdinalIgnoreCase) ||
                origin.StartsWith("http://127.0.0.1:", StringComparison.OrdinalIgnoreCase));
    });
});

builder.Services.AddMiniAdminPersistence(builder.Configuration);
builder.Services.AddScoped<IAppBrandingAppService, AppBrandingAppService>();
builder.Services.AddScoped<IAuditLogAppService, AuditLogAppService>();
builder.Services.AddScoped<IAlertAppService, AlertAppService>();
builder.Services.AddScoped<IAlertRuleAppService, AlertRuleAppService>();
builder.Services.AddScoped<IUserNotificationAppService, UserNotificationAppService>();
builder.Services.AddScoped<INotificationTemplateAppService, NotificationTemplateAppService>();
builder.Services.AddScoped<INotificationPolicyAppService, NotificationPolicyAppService>();
builder.Services.AddScoped<INotificationSubscriptionAppService, NotificationSubscriptionAppService>();
builder.Services.AddScoped<INotificationTemplateRenderer, NotificationTemplateRenderer>();
builder.Services.AddScoped<IFileAppService, FileAppService>();
builder.Services.AddScoped<IAuthAppService, AuthAppService>();
builder.Services.AddScoped<IUserAppService, UserAppService>();
builder.Services.AddScoped<IRoleAppService, RoleAppService>();
builder.Services.AddScoped<IMenuAppService, MenuAppService>();
builder.Services.AddScoped<IDepartmentAppService, DepartmentAppService>();
builder.Services.AddScoped<IDictionaryAppService, DictionaryAppService>();
builder.Services.AddScoped<ISystemParameterAppService, SystemParameterAppService>();
builder.Services.AddScoped<IPositionAppService, PositionAppService>();
builder.Services.AddScoped<INoticeAppService, NoticeAppService>();
builder.Services.AddScoped<IOnlineUserAppService, OnlineUserAppService>();
builder.Services.AddScoped<IPermissionDiagnosticsAppService, PermissionDiagnosticsAppService>();
builder.Services.AddScoped<IScheduledJobAppService, ScheduledJobAppService>();
builder.Services.AddScoped<ISecurityCenterAppService, SecurityCenterAppService>();
builder.Services.AddScoped<ISecurityPolicyAppService, SecurityPolicyAppService>();
builder.Services.AddScoped<ISystemMonitorAppService, SystemMonitorAppService>();
builder.Services.AddSingleton<IProjectRuntimeAppService, ProjectRuntimeAppService>();
builder.Services.AddScoped<ITenantPackageAppService, TenantPackageAppService>();
builder.Services.AddScoped<ITenantAppService, TenantAppService>();
builder.Services.AddScoped<IWorkflowAppService, WorkflowAppService>();
builder.Services.AddScoped<CodeGeneratorTemplateRenderer>();
builder.Services.AddScoped<ICodeGeneratorAppService, CodeGeneratorAppService>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddGeneratedCrudServices();

var app = builder.Build();

if (app.Configuration.GetValue("Database:InitializeOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    var databaseInitializer = scope.ServiceProvider.GetRequiredService<IMiniAdminDatabaseInitializer>();
    await databaseInitializer.InitializeAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("VbenDev");
app.UseAuthentication();
app.Use(async (context, next) =>
{
    var currentTenant = context.RequestServices.GetRequiredService<CurrentTenant>();
    var tenantIdValue = context.User.FindFirstValue("tenant_id");
    var tenantId = Guid.TryParse(tenantIdValue, out var parsedTenantId)
        ? parsedTenantId
        : (Guid?)null;
    currentTenant.Change(tenantId, context.User.FindFirstValue("tenant_code"));
    await next();
});
app.UseAuthorization();
app.UseMiddleware<AuditLogMiddleware>();

app.MapGet("/health", () => Results.Ok(new
{
    Application = "MiniAdmin.Api",
    Status = "Healthy",
    Timestamp = DateTimeOffset.UtcNow
}))
.WithName("HealthCheck");

app.MapGet("/public/app-branding", async (
    IAppBrandingAppService appBrandingAppService,
    CancellationToken cancellationToken) =>
{
    var branding = await appBrandingAppService.GetAsync(cancellationToken);
    return Results.Ok(ApiResponse<AppBrandingDto>.Ok(branding));
});

app.MapGeneratedCrudEndpoints();

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
});

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
    var result = await userAppService.PreviewImportAsync(
        stream,
        GetRequiredUserName(principal),
        cancellationToken);

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
    var result = await userAppService.ImportAsync(
        stream,
        GetRequiredUserName(principal),
        cancellationToken);

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

app.MapGet("/system/menu/tree", async (
    IMenuAppService menuAppService,
    CancellationToken cancellationToken) =>
{
    var menus = await menuAppService.GetManagementTreeAsync(cancellationToken);

    return Results.Ok(ApiResponse<IReadOnlyList<MenuTreeNodeDto>>.Ok(menus));
})
.RequireAnyPermission("system:menu:query", "system:role:assign");

app.MapGet("/system/menu/list", async (
    IMenuAppService menuAppService,
    CancellationToken cancellationToken) =>
{
    var menus = await menuAppService.GetManagementListAsync(cancellationToken);

    return Results.Ok(ApiResponse<IReadOnlyList<MenuManagementItemDto>>.Ok(menus));
})
.RequirePermission("system:menu:query");

app.MapPost("/system/menu", async (
    SaveMenuRequest request,
    IMenuAppService menuAppService,
    CancellationToken cancellationToken) =>
{
    var menu = await menuAppService.CreateAsync(request, cancellationToken);

    return Results.Ok(ApiResponse<MenuManagementItemDto>.Ok(menu));
})
.RequirePermission("system:menu:create");

app.MapPut("/system/menu/{id:guid}", async (
    Guid id,
    SaveMenuRequest request,
    IMenuAppService menuAppService,
    CancellationToken cancellationToken) =>
{
    var menu = await menuAppService.UpdateAsync(id, request, cancellationToken);

    return menu is null
        ? Results.NotFound(ApiResponse<MenuManagementItemDto?>.Fail("Menu not found."))
        : Results.Ok(ApiResponse<MenuManagementItemDto>.Ok(menu));
})
.RequirePermission("system:menu:update");

app.MapDelete("/system/menu/{id:guid}", async (
    Guid id,
    IMenuAppService menuAppService,
    CancellationToken cancellationToken) =>
{
    var deleted = await menuAppService.DeleteAsync(id, cancellationToken);

    return Results.Ok(ApiResponse<bool>.Ok(deleted));
})
.RequirePermission("system:menu:delete");

app.MapGet("/system/department/list", async (
    IDepartmentAppService departmentAppService,
    CancellationToken cancellationToken) =>
{
    var departments = await departmentAppService.GetListAsync(cancellationToken);

    return Results.Ok(ApiResponse<IReadOnlyList<DepartmentItemDto>>.Ok(departments));
})
.RequirePermission("system:department:query");

app.MapPost("/system/department", async (
    SaveDepartmentRequest request,
    IDepartmentAppService departmentAppService,
    CancellationToken cancellationToken) =>
{
    var department = await departmentAppService.CreateAsync(request, cancellationToken);

    return Results.Ok(ApiResponse<DepartmentItemDto>.Ok(department));
})
.RequirePermission("system:department:create");

app.MapPut("/system/department/{id:guid}", async (
    Guid id,
    SaveDepartmentRequest request,
    IDepartmentAppService departmentAppService,
    CancellationToken cancellationToken) =>
{
    var department = await departmentAppService.UpdateAsync(id, request, cancellationToken);

    return department is null
        ? Results.NotFound(ApiResponse<DepartmentItemDto?>.Fail("Department not found."))
        : Results.Ok(ApiResponse<DepartmentItemDto>.Ok(department));
})
.RequirePermission("system:department:update");

app.MapDelete("/system/department/{id:guid}", async (
    Guid id,
    IDepartmentAppService departmentAppService,
    CancellationToken cancellationToken) =>
{
    var deleted = await departmentAppService.DeleteAsync(id, cancellationToken);

    return Results.Ok(ApiResponse<bool>.Ok(deleted));
})
.RequirePermission("system:department:delete");

app.MapGet("/system/position/list", async (
    int? page,
    int? pageSize,
    string? code,
    string? name,
    IPositionAppService positionAppService,
    CancellationToken cancellationToken) =>
{
    var query = new PositionListQuery(
        Page: page ?? 1,
        PageSize: pageSize ?? 20,
        Code: code,
        Name: name);
    var positions = await positionAppService.GetListAsync(query, cancellationToken);

    return Results.Ok(ApiResponse<object>.Ok(positions));
})
.RequirePermission("system:position:query");

app.MapGet("/system/position/export", async (
    int? page,
    int? pageSize,
    string? code,
    string? name,
    IPositionAppService positionAppService,
    CancellationToken cancellationToken) =>
{
    var query = new PositionListQuery(
        Page: page ?? 1,
        PageSize: pageSize ?? 20,
        Code: code,
        Name: name);
    var file = await positionAppService.ExportAsync(query, cancellationToken);

    return Results.File(file.Content, file.ContentType, file.FileName);
})
.RequirePermission("system:position:export");

app.MapGet("/system/position/import-template", async (
    IPositionAppService positionAppService,
    CancellationToken cancellationToken) =>
{
    var file = await positionAppService.GetImportTemplateAsync(cancellationToken);

    return Results.File(file.Content, file.ContentType, file.FileName);
})
.RequirePermission("system:position:import");

app.MapPost("/system/position/import/preview", async (
    IFormFile file,
    IPositionAppService positionAppService,
    CancellationToken cancellationToken) =>
{
    if (file.Length == 0)
    {
        return Results.BadRequest(ApiResponse<PositionImportResultDto?>.Fail("导入文件不能为空."));
    }

    await using var stream = file.OpenReadStream();
    var result = await positionAppService.PreviewImportAsync(stream, cancellationToken);

    return Results.Ok(ApiResponse<PositionImportResultDto>.Ok(result));
})
.DisableAntiforgery()
.RequirePermission("system:position:import");

app.MapPost("/system/position/import/error-report", async (
    IFormFile file,
    IPositionAppService positionAppService,
    CancellationToken cancellationToken) =>
{
    if (file.Length == 0)
    {
        return Results.BadRequest(ApiResponse<PositionImportResultDto?>.Fail("导入文件不能为空."));
    }

    await using var stream = file.OpenReadStream();
    var report = await positionAppService.ExportImportErrorsAsync(stream, cancellationToken);

    return Results.File(report.Content, report.ContentType, report.FileName);
})
.DisableAntiforgery()
.RequirePermission("system:position:import");

app.MapPost("/system/position/import", async (
    IFormFile file,
    IPositionAppService positionAppService,
    CancellationToken cancellationToken) =>
{
    if (file.Length == 0)
    {
        return Results.BadRequest(ApiResponse<PositionImportResultDto?>.Fail("导入文件不能为空."));
    }

    await using var stream = file.OpenReadStream();
    var result = await positionAppService.ImportAsync(stream, cancellationToken);

    return Results.Ok(ApiResponse<PositionImportResultDto>.Ok(result));
})
.DisableAntiforgery()
.RequirePermission("system:position:import");

app.MapPost("/system/position", async (
    SavePositionRequest request,
    IPositionAppService positionAppService,
    CancellationToken cancellationToken) =>
{
    var position = await positionAppService.CreateAsync(request, cancellationToken);

    return Results.Ok(ApiResponse<PositionDto>.Ok(position));
})
.RequirePermission("system:position:create");

app.MapPut("/system/position/{id:guid}", async (
    Guid id,
    SavePositionRequest request,
    IPositionAppService positionAppService,
    CancellationToken cancellationToken) =>
{
    var position = await positionAppService.UpdateAsync(id, request, cancellationToken);

    return position is null
        ? Results.NotFound(ApiResponse<PositionDto?>.Fail("Position not found."))
        : Results.Ok(ApiResponse<PositionDto>.Ok(position));
})
.RequirePermission("system:position:update");

app.MapDelete("/system/position/{id:guid}", async (
    Guid id,
    IPositionAppService positionAppService,
    CancellationToken cancellationToken) =>
{
    var deleted = await positionAppService.DeleteAsync(id, cancellationToken);

    return Results.Ok(ApiResponse<bool>.Ok(deleted));
})
.RequirePermission("system:position:delete");

app.MapGet("/system/dictionary/list", async (
    IDictionaryAppService dictionaryAppService,
    CancellationToken cancellationToken) =>
{
    var dictionaries = await dictionaryAppService.GetListAsync(cancellationToken);

    return Results.Ok(ApiResponse<IReadOnlyList<DictionaryTypeDto>>.Ok(dictionaries));
})
.RequirePermission("system:dictionary:query");

app.MapPost("/system/dictionary/type", async (
    SaveDictionaryTypeRequest request,
    IDictionaryAppService dictionaryAppService,
    CancellationToken cancellationToken) =>
{
    var dictionaryType = await dictionaryAppService.CreateTypeAsync(request, cancellationToken);

    return Results.Ok(ApiResponse<DictionaryTypeDto>.Ok(dictionaryType));
})
.RequirePermission("system:dictionary:create");

app.MapPut("/system/dictionary/type/{id:guid}", async (
    Guid id,
    SaveDictionaryTypeRequest request,
    IDictionaryAppService dictionaryAppService,
    CancellationToken cancellationToken) =>
{
    var dictionaryType = await dictionaryAppService.UpdateTypeAsync(id, request, cancellationToken);

    return dictionaryType is null
        ? Results.NotFound(ApiResponse<DictionaryTypeDto?>.Fail("Dictionary type not found."))
        : Results.Ok(ApiResponse<DictionaryTypeDto>.Ok(dictionaryType));
})
.RequirePermission("system:dictionary:update");

app.MapDelete("/system/dictionary/type/{id:guid}", async (
    Guid id,
    IDictionaryAppService dictionaryAppService,
    CancellationToken cancellationToken) =>
{
    var deleted = await dictionaryAppService.DeleteTypeAsync(id, cancellationToken);

    return Results.Ok(ApiResponse<bool>.Ok(deleted));
})
.RequirePermission("system:dictionary:delete");

app.MapPost("/system/dictionary/item", async (
    SaveDictionaryItemRequest request,
    IDictionaryAppService dictionaryAppService,
    CancellationToken cancellationToken) =>
{
    var dictionaryItem = await dictionaryAppService.CreateItemAsync(request, cancellationToken);

    return Results.Ok(ApiResponse<DictionaryItemDto>.Ok(dictionaryItem));
})
.RequirePermission("system:dictionary:create");

app.MapPut("/system/dictionary/item/{id:guid}", async (
    Guid id,
    SaveDictionaryItemRequest request,
    IDictionaryAppService dictionaryAppService,
    CancellationToken cancellationToken) =>
{
    var dictionaryItem = await dictionaryAppService.UpdateItemAsync(id, request, cancellationToken);

    return dictionaryItem is null
        ? Results.NotFound(ApiResponse<DictionaryItemDto?>.Fail("Dictionary item not found."))
        : Results.Ok(ApiResponse<DictionaryItemDto>.Ok(dictionaryItem));
})
.RequirePermission("system:dictionary:update");

app.MapDelete("/system/dictionary/item/{id:guid}", async (
    Guid id,
    IDictionaryAppService dictionaryAppService,
    CancellationToken cancellationToken) =>
{
    var deleted = await dictionaryAppService.DeleteItemAsync(id, cancellationToken);

    return Results.Ok(ApiResponse<bool>.Ok(deleted));
})
.RequirePermission("system:dictionary:delete");

app.MapGet("/system/parameter/list", async (
    int? page,
    int? pageSize,
    string? key,
    string? name,
    string? group,
    ISystemParameterAppService systemParameterAppService,
    CancellationToken cancellationToken) =>
{
    var query = new SystemParameterListQuery(
        Page: page ?? 1,
        PageSize: pageSize ?? 20,
        Key: key,
        Name: name,
        Group: group);
    var parameters = await systemParameterAppService.GetListAsync(query, cancellationToken);

    return Results.Ok(ApiResponse<object>.Ok(parameters));
})
.RequirePermission("system:parameter:query");

app.MapPost("/system/parameter", async (
    SaveSystemParameterRequest request,
    ISystemParameterAppService systemParameterAppService,
    CancellationToken cancellationToken) =>
{
    var parameter = await systemParameterAppService.CreateAsync(request, cancellationToken);

    return Results.Ok(ApiResponse<SystemParameterDto>.Ok(parameter));
})
.RequirePermission("system:parameter:create");

app.MapPut("/system/parameter/{id:guid}", async (
    Guid id,
    SaveSystemParameterRequest request,
    ISystemParameterAppService systemParameterAppService,
    CancellationToken cancellationToken) =>
{
    var parameter = await systemParameterAppService.UpdateAsync(id, request, cancellationToken);

    return parameter is null
        ? Results.NotFound(ApiResponse<SystemParameterDto?>.Fail("System parameter not found."))
        : Results.Ok(ApiResponse<SystemParameterDto>.Ok(parameter));
})
.RequirePermission("system:parameter:update");

app.MapDelete("/system/parameter/{id:guid}", async (
    Guid id,
    ISystemParameterAppService systemParameterAppService,
    CancellationToken cancellationToken) =>
{
    var deleted = await systemParameterAppService.DeleteAsync(id, cancellationToken);

    return Results.Ok(ApiResponse<bool>.Ok(deleted));
})
.RequirePermission("system:parameter:delete");

app.MapGet("/system/notice/list", async (
    int? page,
    int? pageSize,
    string? title,
    string? type,
    bool? isPublished,
    INoticeAppService noticeAppService,
    CancellationToken cancellationToken) =>
{
    var query = new NoticeListQuery(
        Page: page ?? 1,
        PageSize: pageSize ?? 20,
        Title: title,
        Type: type,
        IsPublished: isPublished);
    var notices = await noticeAppService.GetListAsync(query, cancellationToken);

    return Results.Ok(ApiResponse<object>.Ok(notices));
})
.RequirePermission("system:notice:query");

app.MapPost("/system/notice", async (
    SaveNoticeRequest request,
    INoticeAppService noticeAppService,
    CancellationToken cancellationToken) =>
{
    var notice = await noticeAppService.CreateAsync(request, cancellationToken);

    return Results.Ok(ApiResponse<NoticeDto>.Ok(notice));
})
.RequirePermission("system:notice:create");

app.MapPut("/system/notice/{id:guid}", async (
    Guid id,
    SaveNoticeRequest request,
    INoticeAppService noticeAppService,
    CancellationToken cancellationToken) =>
{
    var notice = await noticeAppService.UpdateAsync(id, request, cancellationToken);

    return notice is null
        ? Results.NotFound(ApiResponse<NoticeDto?>.Fail("Notice not found."))
        : Results.Ok(ApiResponse<NoticeDto>.Ok(notice));
})
.RequirePermission("system:notice:update");

app.MapDelete("/system/notice/{id:guid}", async (
    Guid id,
    INoticeAppService noticeAppService,
    CancellationToken cancellationToken) =>
{
    var deleted = await noticeAppService.DeleteAsync(id, cancellationToken);

    return Results.Ok(ApiResponse<bool>.Ok(deleted));
})
.RequirePermission("system:notice:delete");

app.MapGet("/workflow/definition/list", async (
    int? page,
    int? pageSize,
    string? keyword,
    bool? isEnabled,
    IWorkflowAppService workflowAppService,
    CancellationToken cancellationToken) =>
{
    var definitions = await workflowAppService.GetDefinitionsAsync(
        new WorkflowDefinitionListQuery(
            Page: page ?? 1,
            PageSize: pageSize ?? 20,
            Keyword: keyword,
            IsEnabled: isEnabled),
        cancellationToken);

    return Results.Ok(ApiResponse<object>.Ok(definitions));
})
.RequirePermission("workflow:center:query");

app.MapGet("/workflow/definition/options", async (
    IWorkflowAppService workflowAppService,
    CancellationToken cancellationToken) =>
{
    var definitions = await workflowAppService.GetDefinitionOptionsAsync(cancellationToken);

    return Results.Ok(ApiResponse<IReadOnlyList<WorkflowDefinitionOptionDto>>.Ok(definitions));
})
.RequirePermission("workflow:center:query");

app.MapGet("/workflow/business-binding/list", async (
    int? page,
    int? pageSize,
    string? keyword,
    bool? isEnabled,
    IWorkflowAppService workflowAppService,
    CancellationToken cancellationToken) =>
{
    var bindings = await workflowAppService.GetBusinessBindingsAsync(
        new WorkflowBusinessBindingListQuery(
            Page: page ?? 1,
            PageSize: pageSize ?? 20,
            Keyword: keyword,
            IsEnabled: isEnabled),
        cancellationToken);

    return Results.Ok(ApiResponse<object>.Ok(bindings));
})
.RequirePermission("workflow:center:query");

app.MapGet("/workflow/business-binding/resolve/{businessType}", async (
    string businessType,
    IWorkflowAppService workflowAppService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var definition = await workflowAppService.ResolveBusinessDefinitionAsync(
            businessType,
            cancellationToken);

        return Results.Ok(ApiResponse<WorkflowBusinessDefinitionDto?>.Ok(definition));
    }
    catch (WorkflowOperationException exception)
    {
        return Results.BadRequest(ApiResponse<WorkflowBusinessDefinitionDto?>.Fail(exception.Message));
    }
})
.RequirePermission("workflow:center:query");

app.MapPost("/workflow/business-binding", async (
    SaveWorkflowBusinessBindingRequest request,
    IWorkflowAppService workflowAppService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var binding = await workflowAppService.CreateBusinessBindingAsync(request, cancellationToken);
        return Results.Ok(ApiResponse<WorkflowBusinessBindingDto>.Ok(binding));
    }
    catch (WorkflowOperationException exception)
    {
        return Results.BadRequest(ApiResponse<WorkflowBusinessBindingDto?>.Fail(exception.Message));
    }
})
.RequirePermission("workflow:definition:manage");

app.MapPut("/workflow/business-binding/{id:guid}", async (
    Guid id,
    SaveWorkflowBusinessBindingRequest request,
    IWorkflowAppService workflowAppService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var binding = await workflowAppService.UpdateBusinessBindingAsync(id, request, cancellationToken);
        return binding is null
            ? Results.NotFound(ApiResponse<WorkflowBusinessBindingDto?>.Fail("Workflow business binding not found."))
            : Results.Ok(ApiResponse<WorkflowBusinessBindingDto>.Ok(binding));
    }
    catch (WorkflowOperationException exception)
    {
        return Results.BadRequest(ApiResponse<WorkflowBusinessBindingDto?>.Fail(exception.Message));
    }
})
.RequirePermission("workflow:definition:manage");

app.MapDelete("/workflow/business-binding/{id:guid}", async (
    Guid id,
    IWorkflowAppService workflowAppService,
    CancellationToken cancellationToken) =>
{
    var deleted = await workflowAppService.DeleteBusinessBindingAsync(id, cancellationToken);

    return Results.Ok(ApiResponse<bool>.Ok(deleted));
})
.RequirePermission("workflow:definition:manage");

app.MapGet("/workflow/approver/users", async (
    MiniAdminDbContext dbContext,
    ICurrentTenant currentTenant,
    CancellationToken cancellationToken) =>
{
    var usersQuery = dbContext.Users.AsNoTracking()
        .Where(x => x.IsEnabled);
    usersQuery = currentTenant.IsTenant
        ? usersQuery.Where(x => x.TenantId == currentTenant.TenantId)
        : usersQuery.Where(x => x.TenantId == null);

    var users = await usersQuery
        .OrderBy(x => x.UserName)
        .Select(x => new WorkflowApproverUserOptionDto(
            x.Id.ToString(),
            x.UserName,
            x.RealName))
        .ToArrayAsync(cancellationToken);

    return Results.Ok(ApiResponse<IReadOnlyList<WorkflowApproverUserOptionDto>>.Ok(users));
})
.RequirePermission("workflow:definition:manage");

app.MapGet("/workflow/approver/roles", async (
    MiniAdminDbContext dbContext,
    ICurrentTenant currentTenant,
    CancellationToken cancellationToken) =>
{
    var rolesQuery = dbContext.Roles.AsNoTracking()
        .Where(x => x.IsEnabled);
    rolesQuery = currentTenant.IsTenant
        ? rolesQuery.Where(x => x.TenantId == currentTenant.TenantId)
        : rolesQuery.Where(x => x.TenantId == null);

    var roleRows = await rolesQuery
        .Include(x => x.UserRoles)
        .ThenInclude(x => x.User)
        .OrderBy(x => x.Code)
        .ToArrayAsync(cancellationToken);

    var roles = roleRows
        .Select(x => new WorkflowApproverRoleOptionDto(
            x.Id.ToString(),
            x.Code,
            x.Name,
            x.UserRoles.Count(userRole =>
                userRole.User.IsEnabled &&
                (currentTenant.IsTenant
                    ? userRole.User.TenantId == currentTenant.TenantId
                    : userRole.User.TenantId == null))))
        .Where(x => x.EnabledUserCount > 0)
        .ToArray();

    return Results.Ok(ApiResponse<IReadOnlyList<WorkflowApproverRoleOptionDto>>.Ok(roles));
})
.RequirePermission("workflow:definition:manage");

app.MapPost("/workflow/definition", async (
    SaveWorkflowDefinitionRequest request,
    IWorkflowAppService workflowAppService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var definition = await workflowAppService.CreateDefinitionAsync(request, cancellationToken);
        return Results.Ok(ApiResponse<WorkflowDefinitionDto>.Ok(definition));
    }
    catch (WorkflowOperationException exception)
    {
        return Results.BadRequest(ApiResponse<WorkflowDefinitionDto?>.Fail(exception.Message));
    }
})
.RequirePermission("workflow:definition:manage");

app.MapPut("/workflow/definition/{id:guid}", async (
    Guid id,
    SaveWorkflowDefinitionRequest request,
    IWorkflowAppService workflowAppService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var definition = await workflowAppService.UpdateDefinitionAsync(id, request, cancellationToken);
        return definition is null
            ? Results.NotFound(ApiResponse<WorkflowDefinitionDto?>.Fail("Workflow definition not found."))
            : Results.Ok(ApiResponse<WorkflowDefinitionDto>.Ok(definition));
    }
    catch (WorkflowOperationException exception)
    {
        return Results.BadRequest(ApiResponse<WorkflowDefinitionDto?>.Fail(exception.Message));
    }
})
.RequirePermission("workflow:definition:manage");

app.MapPost("/workflow/definition/{id:guid}/publish", async (
    Guid id,
    IWorkflowAppService workflowAppService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var definition = await workflowAppService.PublishDefinitionAsync(id, cancellationToken);
        return definition is null
            ? Results.NotFound(ApiResponse<WorkflowDefinitionDto?>.Fail("Workflow definition not found."))
            : Results.Ok(ApiResponse<WorkflowDefinitionDto>.Ok(definition));
    }
    catch (WorkflowOperationException exception)
    {
        return Results.BadRequest(ApiResponse<WorkflowDefinitionDto?>.Fail(exception.Message));
    }
})
.RequirePermission("workflow:definition:manage");

app.MapPost("/workflow/definition/{id:guid}/new-version", async (
    Guid id,
    IWorkflowAppService workflowAppService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var definition = await workflowAppService.CreateNewVersionAsync(id, cancellationToken);
        return definition is null
            ? Results.NotFound(ApiResponse<WorkflowDefinitionDto?>.Fail("Workflow definition not found."))
            : Results.Ok(ApiResponse<WorkflowDefinitionDto>.Ok(definition));
    }
    catch (WorkflowOperationException exception)
    {
        return Results.BadRequest(ApiResponse<WorkflowDefinitionDto?>.Fail(exception.Message));
    }
})
.RequirePermission("workflow:definition:manage");

app.MapDelete("/workflow/definition/{id:guid}", async (
    Guid id,
    IWorkflowAppService workflowAppService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var deleted = await workflowAppService.DeleteDefinitionAsync(id, cancellationToken);
        return Results.Ok(ApiResponse<bool>.Ok(deleted));
    }
    catch (WorkflowOperationException exception)
    {
        return Results.BadRequest(ApiResponse<bool>.Fail(exception.Message));
    }
})
.RequirePermission("workflow:definition:manage");

app.MapGet("/workflow/instance/list", async (
    ClaimsPrincipal principal,
    int? page,
    int? pageSize,
    string? keyword,
    string? status,
    string? scope,
    IWorkflowAppService workflowAppService,
    CancellationToken cancellationToken) =>
{
    var instances = await workflowAppService.GetInstancesAsync(
        new WorkflowInstanceListQuery(
            Page: page ?? 1,
            PageSize: pageSize ?? 20,
            Keyword: keyword,
            Status: status,
            Scope: string.IsNullOrWhiteSpace(scope) ? "all" : scope),
        GetWorkflowUserContext(principal),
        cancellationToken);

    return Results.Ok(ApiResponse<object>.Ok(instances));
})
.RequirePermission("workflow:center:query");

app.MapGet("/workflow/instance/started-by-me", async (
    ClaimsPrincipal principal,
    int? page,
    int? pageSize,
    string? keyword,
    string? status,
    IWorkflowAppService workflowAppService,
    CancellationToken cancellationToken) =>
{
    var instances = await workflowAppService.GetInstancesAsync(
        new WorkflowInstanceListQuery(
            Page: page ?? 1,
            PageSize: pageSize ?? 20,
            Keyword: keyword,
            Status: status,
            Scope: "startedByMe"),
        GetWorkflowUserContext(principal),
        cancellationToken);

    return Results.Ok(ApiResponse<object>.Ok(instances));
})
.RequirePermission("workflow:center:query");

app.MapGet("/workflow/instance/cc", async (
    ClaimsPrincipal principal,
    int? page,
    int? pageSize,
    string? keyword,
    string? status,
    string? readStatus,
    IWorkflowAppService workflowAppService,
    CancellationToken cancellationToken) =>
{
    var records = await workflowAppService.GetCcRecordsAsync(
        new WorkflowCcListQuery(
            Page: page ?? 1,
            PageSize: pageSize ?? 20,
            Keyword: keyword,
            InstanceStatus: status,
            ReadStatus: readStatus),
        GetWorkflowUserContext(principal),
        cancellationToken);

    return Results.Ok(ApiResponse<object>.Ok(records));
})
.RequirePermission("workflow:center:query");

app.MapPost("/workflow/cc/{id:guid}/read", async (
    ClaimsPrincipal principal,
    Guid id,
    IWorkflowAppService workflowAppService,
    CancellationToken cancellationToken) =>
{
    var record = await workflowAppService.MarkCcRecordAsReadAsync(
        id,
        GetWorkflowUserContext(principal),
        cancellationToken);

    return record is null
        ? Results.NotFound(ApiResponse<WorkflowCcRecordDto?>.Fail("Workflow cc record not found."))
        : Results.Ok(ApiResponse<WorkflowCcRecordDto>.Ok(record));
})
.RequirePermission("workflow:center:query");

app.MapGet("/workflow/instance/{id:guid}", async (
    ClaimsPrincipal principal,
    Guid id,
    IWorkflowAppService workflowAppService,
    CancellationToken cancellationToken) =>
{
    var instance = await workflowAppService.GetInstanceAsync(
        id,
        GetWorkflowUserContext(principal),
        cancellationToken);

    return instance is null
        ? Results.NotFound(ApiResponse<WorkflowInstanceDto?>.Fail("Workflow instance not found."))
        : Results.Ok(ApiResponse<WorkflowInstanceDto>.Ok(instance));
})
.RequirePermission("workflow:center:query");

app.MapGet("/workflow/task/todo", async (
    ClaimsPrincipal principal,
    IWorkflowAppService workflowAppService,
    CancellationToken cancellationToken) =>
{
    var tasks = await workflowAppService.GetTodoTasksAsync(
        GetWorkflowUserContext(principal),
        cancellationToken);

    return Results.Ok(ApiResponse<IReadOnlyList<WorkflowTaskDto>>.Ok(tasks));
})
.RequirePermission("workflow:center:query");

app.MapGet("/workflow/task/done", async (
    ClaimsPrincipal principal,
    IWorkflowAppService workflowAppService,
    CancellationToken cancellationToken) =>
{
    var tasks = await workflowAppService.GetDoneTasksAsync(
        GetWorkflowUserContext(principal),
        cancellationToken);

    return Results.Ok(ApiResponse<IReadOnlyList<WorkflowTaskDto>>.Ok(tasks));
})
.RequirePermission("workflow:center:query");

app.MapPost("/workflow/instance/start", async (
    ClaimsPrincipal principal,
    StartWorkflowInstanceRequest request,
    IWorkflowAppService workflowAppService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var instance = await workflowAppService.StartInstanceAsync(
            request,
            GetWorkflowUserContext(principal),
            cancellationToken);
        return Results.Ok(ApiResponse<WorkflowInstanceDto>.Ok(instance));
    }
    catch (WorkflowOperationException exception)
    {
        return Results.BadRequest(ApiResponse<WorkflowInstanceDto?>.Fail(exception.Message));
    }
})
.RequirePermission("workflow:instance:start");

app.MapPost("/workflow/instance/{id:guid}/attachments", async (
    ClaimsPrincipal principal,
    Guid id,
    WorkflowAttachmentRequest request,
    IWorkflowAppService workflowAppService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var instance = await workflowAppService.AddAttachmentAsync(
            id,
            request,
            GetWorkflowUserContext(principal),
            cancellationToken);
        return instance is null
            ? Results.NotFound(ApiResponse<WorkflowInstanceDto?>.Fail("Workflow instance not found."))
            : Results.Ok(ApiResponse<WorkflowInstanceDto>.Ok(instance));
    }
    catch (WorkflowOperationException exception)
    {
        return Results.BadRequest(ApiResponse<WorkflowInstanceDto?>.Fail(exception.Message));
    }
})
.RequireAnyPermission("workflow:center:query", "workflow:instance:start", "workflow:task:approve");

app.MapPost("/workflow/instance/{id:guid}/comments", async (
    ClaimsPrincipal principal,
    Guid id,
    WorkflowCommentRequest request,
    IWorkflowAppService workflowAppService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var comment = await workflowAppService.AddCommentAsync(
            id,
            request,
            GetWorkflowUserContext(principal),
            cancellationToken);
        return comment is null
            ? Results.NotFound(ApiResponse<WorkflowCommentDto?>.Fail("Workflow instance not found."))
            : Results.Ok(ApiResponse<WorkflowCommentDto>.Ok(comment));
    }
    catch (WorkflowOperationException exception)
    {
        return Results.BadRequest(ApiResponse<WorkflowCommentDto?>.Fail(exception.Message));
    }
})
.RequireAnyPermission("workflow:center:query", "workflow:instance:start", "workflow:task:approve");

app.MapGet("/workflow/instance/{id:guid}/attachments/{attachmentId:guid}/download", async (
    ClaimsPrincipal principal,
    Guid id,
    Guid attachmentId,
    IWorkflowAppService workflowAppService,
    IFileAppService fileAppService,
    CancellationToken cancellationToken) =>
{
    WorkflowAttachmentDownloadDto? attachment;
    try
    {
        attachment = await workflowAppService.GetAttachmentDownloadAsync(
            id,
            attachmentId,
            GetWorkflowUserContext(principal),
            cancellationToken);
    }
    catch (WorkflowOperationException exception)
    {
        return Results.BadRequest(ApiResponse<WorkflowAttachmentDownloadDto?>.Fail(exception.Message));
    }

    if (attachment is null)
    {
        return Results.NotFound(ApiResponse<WorkflowAttachmentDownloadDto?>.Fail("Workflow attachment not found."));
    }

    FileDownloadResult? file;
    try
    {
        file = await fileAppService.DownloadAsync(Guid.Parse(attachment.FileId), cancellationToken);
    }
    catch (FileUnavailableException exception)
    {
        return Results.Conflict(ApiResponse<FileDto?>.Fail(exception.Message));
    }

    return file is null
        ? Results.NotFound(ApiResponse<FileDto?>.Fail("File not found."))
        : Results.File(file.Content, file.ContentType, file.OriginalName);
})
.RequirePermission("workflow:center:query");

app.MapPost("/workflow/instance/{id:guid}/approve", async (
    ClaimsPrincipal principal,
    Guid id,
    WorkflowActionRequest request,
    IWorkflowAppService workflowAppService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var instance = await workflowAppService.ApproveAsync(
            id,
            request,
            GetWorkflowUserContext(principal),
            cancellationToken);
        return instance is null
            ? Results.NotFound(ApiResponse<WorkflowInstanceDto?>.Fail("Workflow instance not found."))
            : Results.Ok(ApiResponse<WorkflowInstanceDto>.Ok(instance));
    }
    catch (WorkflowOperationException exception)
    {
        return Results.BadRequest(ApiResponse<WorkflowInstanceDto?>.Fail(exception.Message));
    }
})
.RequirePermission("workflow:task:approve");

app.MapPost("/workflow/instance/{id:guid}/reject", async (
    ClaimsPrincipal principal,
    Guid id,
    WorkflowActionRequest request,
    IWorkflowAppService workflowAppService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var instance = await workflowAppService.RejectAsync(
            id,
            request,
            GetWorkflowUserContext(principal),
            cancellationToken);
        return instance is null
            ? Results.NotFound(ApiResponse<WorkflowInstanceDto?>.Fail("Workflow instance not found."))
            : Results.Ok(ApiResponse<WorkflowInstanceDto>.Ok(instance));
    }
    catch (WorkflowOperationException exception)
    {
        return Results.BadRequest(ApiResponse<WorkflowInstanceDto?>.Fail(exception.Message));
    }
})
.RequirePermission("workflow:task:approve");

app.MapPost("/workflow/task/{id:guid}/transfer", async (
    ClaimsPrincipal principal,
    Guid id,
    WorkflowTransferTaskRequest request,
    IWorkflowAppService workflowAppService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var task = await workflowAppService.TransferTaskAsync(
            id,
            request,
            GetWorkflowUserContext(principal),
            cancellationToken);
        return task is null
            ? Results.NotFound(ApiResponse<WorkflowTaskDto?>.Fail("Workflow task not found."))
            : Results.Ok(ApiResponse<WorkflowTaskDto>.Ok(task));
    }
    catch (WorkflowOperationException exception)
    {
        return Results.BadRequest(ApiResponse<WorkflowTaskDto?>.Fail(exception.Message));
    }
})
.RequirePermission("workflow:task:approve");

app.MapPost("/workflow/task/{id:guid}/remind", async (
    ClaimsPrincipal principal,
    Guid id,
    WorkflowRemindTaskRequest request,
    IWorkflowAppService workflowAppService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var task = await workflowAppService.RemindTaskAsync(
            id,
            request,
            GetWorkflowUserContext(principal),
            cancellationToken);
        return task is null
            ? Results.NotFound(ApiResponse<WorkflowTaskDto?>.Fail("Workflow task not found."))
            : Results.Ok(ApiResponse<WorkflowTaskDto>.Ok(task));
    }
    catch (WorkflowOperationException exception)
    {
        return Results.BadRequest(ApiResponse<WorkflowTaskDto?>.Fail(exception.Message));
    }
})
.RequireAnyPermission("workflow:instance:start", "workflow:task:approve");

app.MapPost("/workflow/instance/{id:guid}/withdraw", async (
    ClaimsPrincipal principal,
    Guid id,
    WorkflowActionRequest request,
    IWorkflowAppService workflowAppService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var instance = await workflowAppService.WithdrawAsync(
            id,
            request,
            GetWorkflowUserContext(principal),
            cancellationToken);
        return instance is null
            ? Results.NotFound(ApiResponse<WorkflowInstanceDto?>.Fail("Workflow instance not found."))
            : Results.Ok(ApiResponse<WorkflowInstanceDto>.Ok(instance));
    }
    catch (WorkflowOperationException exception)
    {
        return Results.BadRequest(ApiResponse<WorkflowInstanceDto?>.Fail(exception.Message));
    }
})
.RequirePermission("workflow:instance:start");

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
    var uploaded = await fileAppService.UploadAsync(
        stream,
        file.FileName,
        file.ContentType,
        file.Length,
        cancellationToken);

    return Results.Ok(ApiResponse<FileDto>.Ok(uploaded));
})
.DisableAntiforgery()
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

app.Run();

static async Task<CodeGeneratorRollbackRequest> ReadRollbackRequestAsync(
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

static async Task<CodeGeneratorArtifactCleanupRequest> ReadArtifactCleanupRequestAsync(
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

static string GetRequiredUserName(ClaimsPrincipal principal)
{
    return principal.Identity?.Name
        ?? principal.FindFirstValue(ClaimTypes.Name)
        ?? throw new InvalidOperationException("Authenticated user name is missing.");
}

static Guid GetRequiredUserId(ClaimsPrincipal principal)
{
    var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
    return Guid.TryParse(userId, out var value)
        ? value
        : throw new InvalidOperationException("Authenticated user id is missing.");
}

static Guid GetRequiredSessionId(ClaimsPrincipal principal)
{
    var sessionId = principal.FindFirstValue("session_id");
    return Guid.TryParse(sessionId, out var value)
        ? value
        : throw new InvalidOperationException("Authenticated session id is missing.");
}

static WorkflowUserContext GetWorkflowUserContext(ClaimsPrincipal principal)
{
    return new WorkflowUserContext(
        GetRequiredUserId(principal),
        GetRequiredUserName(principal),
        HasPermission(principal, "workflow:definition:manage"));
}

static bool HasPermission(ClaimsPrincipal principal, string permissionCode)
{
    return principal.Claims.Any(claim =>
        claim.Type == "permission" &&
        claim.Value.Equals(permissionCode, StringComparison.OrdinalIgnoreCase));
}

static string? GetClientIpAddress(HttpContext httpContext)
{
    return httpContext.Connection.RemoteIpAddress?.ToString();
}

static IResult ToPasswordOperationHttpResult(PasswordOperationResult result)
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

static byte[] BuildAuditLogCsv(IReadOnlyList<AuditLogDto> logs)
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

static string EscapeCsvCell(string? value)
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

public partial class Program;
