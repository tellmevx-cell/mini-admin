using MiniAdmin.Api.CodeGenerators;
using MiniAdmin.Application.AppBranding;
using MiniAdmin.Application.Alerts;
using MiniAdmin.Application.AuditLogs;
using MiniAdmin.Application.Auth;
using MiniAdmin.Application.CodeGenerators;
using MiniAdmin.Application.Contracts.AppBranding;
using MiniAdmin.Application.Contracts.Alerts;
using MiniAdmin.Application.Contracts.AuditLogs;
using MiniAdmin.Application.Contracts.Auth;
using MiniAdmin.Application.Contracts.CodeGenerators;
using MiniAdmin.Application.Contracts.Departments;
using MiniAdmin.Application.Contracts.Dictionaries;
using MiniAdmin.Application.Contracts.Files;
using MiniAdmin.Application.Contracts.Menus;
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
using MiniAdmin.Application.UserNotifications;
using MiniAdmin.Application.Users;
using MiniAdmin.Application.Workflows;
using MiniAdmin.Infrastructure.Auth;
using MiniAdmin.Infrastructure.Persistence;
using MiniAdmin.Infrastructure.ProjectRuntimes;
using MiniAdmin.Infrastructure.SystemMonitor;

namespace MiniAdmin.Api.Composition;

public static class MiniAdminApiServiceCollectionExtensions
{
    public static IServiceCollection AddMiniAdminApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMiniAdminPersistence(configuration);
        services.AddScoped<IAppBrandingAppService, AppBrandingAppService>();
        services.AddScoped<IAuditLogAppService, AuditLogAppService>();
        services.AddScoped<IAlertAppService, AlertAppService>();
        services.AddScoped<IAlertRuleAppService, AlertRuleAppService>();
        services.AddScoped<IUserNotificationAppService, UserNotificationAppService>();
        services.AddScoped<INotificationTemplateAppService, NotificationTemplateAppService>();
        services.AddScoped<INotificationPolicyAppService, NotificationPolicyAppService>();
        services.AddScoped<INotificationSubscriptionAppService, NotificationSubscriptionAppService>();
        services.AddScoped<INotificationTemplateRenderer, NotificationTemplateRenderer>();
        services.AddScoped<IFileAppService, FileAppService>();
        services.AddScoped<IAuthAppService, AuthAppService>();
        services.AddScoped<IUserAppService, UserAppService>();
        services.AddScoped<IRoleAppService, RoleAppService>();
        services.AddScoped<IMenuAppService, MenuAppService>();
        services.AddScoped<IDepartmentAppService, DepartmentAppService>();
        services.AddScoped<IDictionaryAppService, DictionaryAppService>();
        services.AddScoped<ISystemParameterAppService, SystemParameterAppService>();
        services.AddScoped<IPositionAppService, PositionAppService>();
        services.AddScoped<INoticeAppService, NoticeAppService>();
        services.AddScoped<IOnlineUserAppService, OnlineUserAppService>();
        services.AddScoped<IPermissionDiagnosticsAppService, PermissionDiagnosticsAppService>();
        services.AddScoped<IScheduledJobAppService, ScheduledJobAppService>();
        services.AddScoped<ISecurityCenterAppService, SecurityCenterAppService>();
        services.AddScoped<ISecurityPolicyAppService, SecurityPolicyAppService>();
        services.AddScoped<ISystemMonitorAppService, SystemMonitorAppService>();
        services.AddSingleton<IProjectRuntimeAppService, ProjectRuntimeAppService>();
        services.AddScoped<ITenantPackageAppService, TenantPackageAppService>();
        services.AddScoped<ITenantAppService, TenantAppService>();
        services.AddScoped<IWorkflowAppService, WorkflowAppService>();
        services.AddScoped<CodeGeneratorTemplateRenderer>();
        services.AddScoped<ICodeGeneratorAppService, CodeGeneratorAppService>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddGeneratedCrudServices();

        return services;
    }
}
