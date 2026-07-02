using System.Text;
using System.Text.Json;
using System.Security.Claims;
using MiniAdmin.Api.CodeGenerators;
using MiniAdmin.Api.Composition;
using MiniAdmin.Api.Endpoints;
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
app.UseRateLimiter();
app.UseAuthorization();
app.UseMiddleware<AuditLogMiddleware>();

app.MapCoreEndpoints();
app.MapGeneratedCrudEndpoints();
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
