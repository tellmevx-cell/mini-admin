using System.Text;
using System.Text.Json;
using System.Security.Claims;
using MiniAdmin.Api.CodeGenerators;
using MiniAdmin.Api.Composition;
using MiniAdmin.Api.Endpoints;
using MiniAdmin.Api.Hubs;
using MiniAdmin.Api.Health;
using MiniAdmin.Api.OpenPlatform;
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
using MiniAdmin.Application.Contracts.OpenPlatform;
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
using MiniAdmin.Application.Platform;
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
using MiniAdmin.Platform.AspNetCore.DynamicApi;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json.Serialization;
using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using static MiniAdmin.Api.Endpoints.EndpointHelpers;


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddLocalization();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { new CultureInfo("zh-CN"), new CultureInfo("en-US") };
    options.DefaultRequestCulture = new RequestCulture("zh-CN");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.ApplyCurrentCultureToResponseHeaders = true;
});
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});
builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(60);
});

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is missing.");
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience is missing.");
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey is missing.");
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey));
var openPlatformIssuer = builder.Configuration["OpenPlatform:Issuer"] ?? "http://localhost:5021/";
var openPlatformSigningMaterial = builder.Configuration["OpenPlatform:SigningKey"];
if (string.IsNullOrWhiteSpace(openPlatformSigningMaterial))
{
    openPlatformSigningMaterial = jwtSigningKey;
}
var openPlatformSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(openPlatformSigningMaterial));

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = MiniAdminAuthenticationSchemes.Smart;
        options.DefaultChallengeScheme = MiniAdminAuthenticationSchemes.Smart;
    })
    .AddPolicyScheme(
        MiniAdminAuthenticationSchemes.Smart,
        MiniAdminAuthenticationSchemes.Smart,
        options =>
        {
            options.ForwardDefaultSelector = context =>
                context.Request.Headers.ContainsKey("X-MA-AppKey")
                    ? MiniAdminAuthenticationSchemes.AppKey
                    : JwtBearerDefaults.AuthenticationScheme;
        })
    .AddScheme<AuthenticationSchemeOptions, OpenApiAuthenticationHandler>(
        MiniAdminAuthenticationSchemes.AppKey,
        _ => { })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = [jwtIssuer, openPlatformIssuer],
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = [signingKey, openPlatformSigningKey],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrWhiteSpace(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var principalType = context.Principal?.FindFirstValue(OpenPlatformClaimTypes.PrincipalType);
                var isOpenPlatformApplication = string.Equals(
                    principalType,
                    OpenPlatformClaimTypes.Application,
                    StringComparison.Ordinal);
                var isOpenPlatformUser = string.Equals(
                    principalType,
                    OpenPlatformClaimTypes.User,
                    StringComparison.Ordinal);
                if (isOpenPlatformApplication && string.IsNullOrWhiteSpace(
                        context.Principal?.FindFirstValue(OpenPlatformClaimTypes.ClientId)))
                {
                    context.Fail("Open platform client identity is missing.");
                    return;
                }

                var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var sessionIdValue = context.Principal?.FindFirstValue("session_id");
                var securityStamp = context.Principal?.FindFirstValue("security_stamp");
                Guid userId = Guid.Empty;
                if (!isOpenPlatformApplication &&
                    (!Guid.TryParse(userIdValue, out userId) || string.IsNullOrWhiteSpace(securityStamp)))
                {
                    context.Fail("Token user security information is missing.");
                    return;
                }

                if (!isOpenPlatformApplication)
                {
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

                if (isOpenPlatformApplication || isOpenPlatformUser)
                {
                    return;
                }

                if (!Guid.TryParse(sessionIdValue, out var sessionId))
                {
                    context.Fail("Token security session is missing.");
                    return;
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
builder.Services.AddMiniAdminOpenPlatform(
    builder.Configuration,
    builder.Environment);
builder.Services.AddAuthorization();
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumReceiveMessageSize = 32 * 1024;
});
builder.Services.AddMiniAdminRateLimiting(builder.Configuration);
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

builder.Services.AddMiniAdminApplicationServices(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy("Process is running."),
        tags: ["live"])
    .AddCheck<DatabaseReadinessHealthCheck>("database", tags: ["ready"])
    .AddCheck<PrimaryCacheReadinessHealthCheck>("primary-cache", tags: ["ready"]);
builder.Services.AddMiniAdminDynamicApis(
    typeof(PlatformMetadataAppService).Assembly,
    typeof(SystemMonitorAppService).Assembly);

var app = builder.Build();

ProductionConfigurationValidator.Validate(app.Configuration, app.Environment, app.Logger);

var liveHealthOptions = new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live"),
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
};
var readyHealthOptions = new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteAsync
};
app.MapHealthChecks("/health/live", liveHealthOptions).DisableRateLimiting();
app.MapHealthChecks("/health/ready", readyHealthOptions).DisableRateLimiting();
app.MapHealthChecks("/health", readyHealthOptions).DisableRateLimiting();

if (app.Configuration.GetValue("Database:InitializeOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    var initializationLock = scope.ServiceProvider.GetRequiredService<IDatabaseInitializationLock>();
    var databaseInitializer = scope.ServiceProvider.GetRequiredService<IMiniAdminDatabaseInitializer>();
    await using var databaseInitializationLease = await initializationLock.AcquireAsync();
    await databaseInitializer.InitializeAsync();
}

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("OpenApi:Enabled"))
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseRequestLocalization();
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
app.UseRateLimiter();
app.UseAuthorization();
app.UseMiddleware<AuditLogMiddleware>();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapHub<ChatHub>("/hubs/chat");
app.MapOpenPlatformEndpoints();

app.MapCoreEndpoints();
app.MapGeneratedTransportEndpoints();
app.MapAuthenticationEndpoints();
app.MapPlatformEndpoints();
app.MapNotificationEndpoints();
app.MapUserManagementEndpoints();
app.MapRoleEndpoints();
app.MapSystemManagementEndpoints();
app.MapWorkflowEndpoints();
app.MapFileAndAuditEndpoints();
app.MapOperationsEndpoints();
app.MapProjectRuntimeEndpoints();
app.MapCodeGeneratorEndpoints();

app.Run();

public partial class Program;
