using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MiniAdmin.Application.Contracts.Auth;
using MiniAdmin.Application.Contracts.Caching;
using MiniAdmin.Domain.Entities;
using MiniAdmin.Domain.Shared.MultiTenancy;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class MiniAdminDatabaseInitializer(
    MiniAdminDbContext dbContext,
    IPasswordService passwordService,
    IUserAuthorizationCache userAuthorizationCache,
    IOptions<DatabaseOptions> databaseOptions) : IMiniAdminDatabaseInitializer
{
    private const string InitialCreateMigrationId = "20260528021515_InitialCreate";
    private const string BaselineSeedVersion = "202605280001-baseline-system-data";
    private const string BaselineSeedName = "系统基础菜单权限角色用户字典参数";
    private const string MenuRestructureSeedVersion = "202605280002-menu-monitor-log-restructure";
    private const string MenuRestructureSeedName = "日志管理和系统监控菜单重整";
    private const string ScheduledJobsSeedVersion = "202605280003-scheduled-jobs";
    private const string ScheduledJobsSeedName = "定时任务菜单权限和内置任务";
    private const string StorageConsistencyJobSeedVersion = "202605280004-storage-consistency-job";
    private const string StorageConsistencyJobSeedName = "文件存储一致性检查定时任务";
    private const string FileExceptionHandlingSeedVersion = "202605280005-file-exception-handling";
    private const string FileExceptionHandlingSeedName = "文件异常处理权限";
    private const string SystemMonitorDashboardSeedVersion = "202605280006-system-monitor-dashboard";
    private const string SystemMonitorDashboardSeedName = "系统监控看板菜单权限";
    private const string AlertCenterSeedVersion = "202605280007-alert-center";
    private const string AlertCenterSeedName = "系统告警中心菜单权限和扫描任务";
    private const string NotificationCenterSeedVersion = "202605280008-notification-center";
    private const string NotificationCenterSeedName = "通知中心菜单权限";
    private const string NotificationTemplateSeedVersion = "202606080002-notification-template-center";
    private const string NotificationTemplateSeedName = "消息模板中心默认模板";
    private const string NotificationPolicySeedVersion = "202606090001-notification-policy-center";
    private const string NotificationPolicySeedName = "通知策略默认配置";
    private const string NotificationRetrySeedVersion = "202606090002-notification-delivery-retry";
    private const string NotificationRetrySeedName = "通知投递手工重发权限";
    private const string NotificationDeliveryRetryJobSeedVersion = "202606090003-notification-delivery-retry-job";
    private const string NotificationDeliveryRetryJobSeedName = "通知投递自动重试定时任务";
    private const string AlertRulesSeedVersion = "202605280011-alert-rules";
    private const string AlertRulesSeedName = "告警规则配置菜单权限和默认规则";
    private const string NotificationRoutingSeedVersion = "202605280012-notification-routing";
    private const string NotificationRoutingSeedName = "告警通知默认接收人";
    private const string SecurityCenterSeedVersion = "202605290001-security-center";
    private const string SecurityCenterSeedName = "安全中心菜单权限";
    private const string PlatformTenantManagementSeedVersion = "202605290002-platform-tenant-management";
    private const string PlatformTenantManagementSeedName = "平台租户管理菜单权限";
    private const string TenantPackageManagementSeedVersion = "202605290003-tenant-package-management";
    private const string TenantPackageManagementSeedName = "租户套餐管理菜单权限";
    private const string LegacySystemTenantManagementCleanupSeedVersion = "202605290004-legacy-system-tenant-management-cleanup";
    private const string LegacySystemTenantManagementCleanupSeedName = "清理系统管理旧租户管理入口";
    private const string TenantDashboardAnalyticsAccessSeedVersion = "202605290005-tenant-dashboard-analytics-access";
    private const string TenantDashboardAnalyticsAccessSeedName = "补齐租户默认分析页访问权限";
    private const string CodeGeneratorSeedVersion = "202605290006-code-generator-basic";
    private const string CodeGeneratorSeedName = "代码生成器菜单权限";
    private const string PositionImportExportSeedVersion = "202605300001-position-import-export";
    private const string PositionImportExportSeedName = "岗位导入导出权限";
    private const string ProjectRuntimeSeedVersion = "202605300002-project-runtime-panel";
    private const string ProjectRuntimeSeedName = "项目运行管理菜单权限";
    private const string AppBrandingWatermarkSeedVersion = "202605310001-app-branding-watermark";
    private const string AppBrandingWatermarkSeedName = "应用品牌与全局水印参数";
    private const string WorkflowBasicSeedVersion = "202606010001-workflow-basic";
    private const string WorkflowBasicSeedName = "工作流审批中心菜单权限";
    private const string WorkflowSlaScanSeedVersion = "202606080003-workflow-sla-scan";
    private const string WorkflowSlaScanSeedName = "工作流 SLA 超时扫描任务";
    private const string WorkflowCollaborationSeedVersion = "202606080004-workflow-collaboration";
    private const string WorkflowCollaborationSeedName = "工作流附件评论协作";

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await InitializeSchemaAsync(cancellationToken);
        await CleanupExpiredAuditLogsAsync(cancellationToken);
        await ApplySeedVersionAsync(
            BaselineSeedVersion,
            BaselineSeedName,
            SeedBaselineSystemDataAsync,
            cancellationToken);
        await ApplySeedVersionAsync(
            MenuRestructureSeedVersion,
            MenuRestructureSeedName,
            SeedMenuMonitorLogRestructureAsync,
            cancellationToken);
        await ApplySeedVersionAsync(
            ScheduledJobsSeedVersion,
            ScheduledJobsSeedName,
            SeedScheduledJobsAsync,
            cancellationToken);
        await ApplySeedVersionAsync(
            StorageConsistencyJobSeedVersion,
            StorageConsistencyJobSeedName,
            SeedStorageConsistencyJobAsync,
            cancellationToken);
        await ApplySeedVersionAsync(
            FileExceptionHandlingSeedVersion,
            FileExceptionHandlingSeedName,
            SeedFileExceptionHandlingAsync,
            cancellationToken);
        await ApplySeedVersionAsync(
            SystemMonitorDashboardSeedVersion,
            SystemMonitorDashboardSeedName,
            SeedSystemMonitorDashboardAsync,
            cancellationToken);
        await ApplySeedVersionAsync(
            AlertCenterSeedVersion,
            AlertCenterSeedName,
            SeedAlertCenterAsync,
            cancellationToken);
        await ApplySeedVersionAsync(
            NotificationCenterSeedVersion,
            NotificationCenterSeedName,
            SeedNotificationCenterAsync,
            cancellationToken);
        await ApplySeedVersionAsync(
            NotificationTemplateSeedVersion,
            NotificationTemplateSeedName,
            SeedNotificationTemplatesAsync,
            cancellationToken);
        await ApplySeedVersionAsync(
            NotificationPolicySeedVersion,
            NotificationPolicySeedName,
            SeedNotificationPoliciesAsync,
            cancellationToken);
        await ApplySeedVersionAsync(
            NotificationRetrySeedVersion,
            NotificationRetrySeedName,
            SeedNotificationRetryAsync,
            cancellationToken);
        await ApplySeedVersionAsync(
            NotificationDeliveryRetryJobSeedVersion,
            NotificationDeliveryRetryJobSeedName,
            SeedNotificationDeliveryRetryJobAsync,
            cancellationToken);
        await ApplySeedVersionAsync(
            AlertRulesSeedVersion,
            AlertRulesSeedName,
            SeedAlertRulesAsync,
            cancellationToken);
        await ApplySeedVersionAsync(
            NotificationRoutingSeedVersion,
            NotificationRoutingSeedName,
            SeedNotificationRoutingAsync,
            cancellationToken);
        await ApplySeedVersionAsync(
            SecurityCenterSeedVersion,
            SecurityCenterSeedName,
            SeedSecurityCenterAsync,
            cancellationToken);
        await ApplySeedVersionAsync(
            PlatformTenantManagementSeedVersion,
            PlatformTenantManagementSeedName,
            SeedPlatformTenantManagementAsync,
            cancellationToken);
        await ApplySeedVersionAsync(
            TenantPackageManagementSeedVersion,
            TenantPackageManagementSeedName,
            SeedTenantPackageManagementAsync,
            cancellationToken);
        await ApplySeedVersionAsync(
            LegacySystemTenantManagementCleanupSeedVersion,
            LegacySystemTenantManagementCleanupSeedName,
            SeedLegacySystemTenantManagementCleanupAsync,
            cancellationToken);
        await ApplySeedVersionAsync(
            PositionImportExportSeedVersion,
            PositionImportExportSeedName,
            SeedPositionImportExportAsync,
            cancellationToken);
        await ApplySeedVersionAsync(
            WorkflowBasicSeedVersion,
            WorkflowBasicSeedName,
            SeedWorkflowBasicAsync,
            cancellationToken);
        await ApplySeedVersionAsync(
            WorkflowSlaScanSeedVersion,
            WorkflowSlaScanSeedName,
            SeedWorkflowSlaScanAsync,
            cancellationToken);
        await ApplySeedVersionAsync(
            WorkflowCollaborationSeedVersion,
            WorkflowCollaborationSeedName,
            SeedWorkflowCollaborationAsync,
            cancellationToken);
        await EnsureTenantAdminRoleAsync(cancellationToken);
        await EnsureTenantAdminRoleMenusAsync(cancellationToken);
        await EnsureTenantsAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await ApplySeedVersionAsync(
            TenantDashboardAnalyticsAccessSeedVersion,
            TenantDashboardAnalyticsAccessSeedName,
            SeedTenantDashboardAnalyticsAccessAsync,
            cancellationToken);
        await ApplySeedVersionAsync(
            CodeGeneratorSeedVersion,
            CodeGeneratorSeedName,
            SeedCodeGeneratorAsync,
            cancellationToken);
        await ApplySeedVersionAsync(
            ProjectRuntimeSeedVersion,
            ProjectRuntimeSeedName,
            SeedProjectRuntimeAsync,
            cancellationToken);
        await ApplySeedVersionAsync(
            AppBrandingWatermarkSeedVersion,
            AppBrandingWatermarkSeedName,
            SeedAppBrandingWatermarkAsync,
            cancellationToken);
        await SeedGeneratedCrudModulesAsync(cancellationToken);
        await EnsureUsersAsync(cancellationToken);
        await EnsureAllUsersHaveSecurityStampAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RefreshSeedUserAuthorizationCacheAsync(cancellationToken);
        await RefreshTenantAdminAuthorizationCacheAsync(cancellationToken);
    }

    private async Task InitializeSchemaAsync(CancellationToken cancellationToken)
    {
        if (databaseOptions.Value.SchemaManagement.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (ShouldUseMigrations())
        {
            await AdoptExistingMySqlDatabaseAsync(cancellationToken);
            await dbContext.Database.MigrateAsync(cancellationToken);
            await EnsureTenantTablesAsync(cancellationToken);
            await EnsureTenantInitializationColumnsAsync(cancellationToken);
            await EnsureUserTenantColumnAsync(cancellationToken);
            await EnsureTenantScopedCoreColumnsAsync(cancellationToken);
            await EnsureTenantScopedCoreIndexesAsync(cancellationToken);
            await EnsureRoleCustomDepartmentIdsColumnAsync(cancellationToken);
            await EnsureSecurityEventTableAsync(cancellationToken);
            await EnsureCodeGenerationHistoryTableAsync(cancellationToken);
            await EnsureNotificationTemplateTableAsync(cancellationToken);
            await EnsureNotificationPolicyTableAsync(cancellationToken);
            await EnsureNotificationSubscriptionTableAsync(cancellationToken);
            await EnsureWorkflowTablesAsync(cancellationToken);
            await EnsureGeneratedCustomerTableAsync(cancellationToken);
            await EnsureGeneratedSampleOrderTableAsync(cancellationToken);
            return;
        }

        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await EnsureLegacyMySqlSchemaAsync(cancellationToken);
    }

    private async Task EnsureLegacyMySqlSchemaAsync(CancellationToken cancellationToken)
    {
        await EnsureTenantTablesAsync(cancellationToken);
        await EnsureTenantInitializationColumnsAsync(cancellationToken);
        await EnsureDepartmentTableAsync(cancellationToken);
        await EnsurePositionTableAsync(cancellationToken);
        await EnsureRoleDataScopeColumnAsync(cancellationToken);
        await EnsureRoleCustomDepartmentIdsColumnAsync(cancellationToken);
        await EnsureDictionaryTablesAsync(cancellationToken);
        await EnsureSystemParameterTableAsync(cancellationToken);
        await EnsureNoticeTableAsync(cancellationToken);
        await EnsureAuditLogTableAsync(cancellationToken);
        await EnsureAuditEntityChangeTableAsync(cancellationToken);
        await EnsureLoginLogTableAsync(cancellationToken);
        await EnsureOnlineUserTableAsync(cancellationToken);
        await EnsureFileTableAsync(cancellationToken);
        await EnsureAlertTableAsync(cancellationToken);
        await EnsureAlertRuleTableAsync(cancellationToken);
        await EnsureAlertRuleRecipientTableAsync(cancellationToken);
        await EnsureUserNotificationTableAsync(cancellationToken);
        await EnsureNotificationDeliveryTableAsync(cancellationToken);
        await EnsureNotificationTemplateTableAsync(cancellationToken);
        await EnsureNotificationPolicyTableAsync(cancellationToken);
        await EnsureNotificationSubscriptionTableAsync(cancellationToken);
        await EnsureSecurityEventTableAsync(cancellationToken);
        await EnsureCodeGenerationHistoryTableAsync(cancellationToken);
        await EnsureWorkflowTablesAsync(cancellationToken);
        await EnsureGeneratedCustomerTableAsync(cancellationToken);
        await EnsureGeneratedSampleOrderTableAsync(cancellationToken);
        await EnsureUserDepartmentColumnAsync(cancellationToken);
        await EnsureUserPositionColumnAsync(cancellationToken);
        await EnsureUserSecurityStampColumnAsync(cancellationToken);
        await EnsureUserEmailColumnAsync(cancellationToken);
        await EnsureUserTenantColumnAsync(cancellationToken);
        await EnsureTenantScopedCoreColumnsAsync(cancellationToken);
        await EnsureTenantScopedCoreIndexesAsync(cancellationToken);
        await EnsureAlertRuleEmailEnabledColumnAsync(cancellationToken);
    }

    private async Task SeedBaselineSystemDataAsync(CancellationToken cancellationToken)
    {
        await EnsureAdminRoleAsync(cancellationToken);
        await EnsureMenusAsync(cancellationToken);
        await EnsureDepartmentsAsync(cancellationToken);
        await EnsurePositionsAsync(cancellationToken);
        await EnsureDictionariesAsync(cancellationToken);
        await EnsureSystemParametersAsync(cancellationToken);
        await EnsureNoticesAsync(cancellationToken);
        await EnsureTenantsAsync(cancellationToken);
        await EnsureUsersAsync(cancellationToken);
        await EnsureAllUsersHaveSecurityStampAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        await EnsureAdminUserRoleAsync(cancellationToken);
        await RemoveLegacyAdminRoleFromNonAdminSeedUsersAsync(cancellationToken);

        if (!await HasAdminRoleMenusAsync(cancellationToken))
        {
            foreach (var menuId in GetAdminMenuIds())
            {
                await EnsureRoleMenuAsync(menuId, cancellationToken);
            }
        }
        else
        {
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.UserManagementMenuId,
                MiniAdminSeedIds.UserUnlockPermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.UserManagementMenuId,
                MiniAdminSeedIds.UserResetPasswordPermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.UserManagementMenuId,
                MiniAdminSeedIds.UserImportPermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.UserManagementMenuId,
                MiniAdminSeedIds.UserExportPermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.SystemMenuId,
                MiniAdminSeedIds.TenantPackageMenuId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.RoleManagementMenuId,
                MiniAdminSeedIds.RoleQueryPermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.RoleManagementMenuId,
                MiniAdminSeedIds.RoleCreatePermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.RoleManagementMenuId,
                MiniAdminSeedIds.RoleUpdatePermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.RoleManagementMenuId,
                MiniAdminSeedIds.RoleDeletePermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.RoleManagementMenuId,
                MiniAdminSeedIds.RoleAssignPermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.MenuManagementMenuId,
                MiniAdminSeedIds.MenuQueryPermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.MenuManagementMenuId,
                MiniAdminSeedIds.MenuCreatePermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.MenuManagementMenuId,
                MiniAdminSeedIds.MenuUpdatePermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.MenuManagementMenuId,
                MiniAdminSeedIds.MenuDeletePermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.DepartmentManagementMenuId,
                MiniAdminSeedIds.DepartmentQueryPermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.DepartmentManagementMenuId,
                MiniAdminSeedIds.DepartmentCreatePermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.DepartmentManagementMenuId,
                MiniAdminSeedIds.DepartmentUpdatePermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.DepartmentManagementMenuId,
                MiniAdminSeedIds.DepartmentDeletePermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.PositionManagementMenuId,
                MiniAdminSeedIds.PositionQueryPermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.PositionManagementMenuId,
                MiniAdminSeedIds.PositionCreatePermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.PositionManagementMenuId,
                MiniAdminSeedIds.PositionUpdatePermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.PositionManagementMenuId,
                MiniAdminSeedIds.PositionDeletePermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.PositionManagementMenuId,
                MiniAdminSeedIds.PositionImportPermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.PositionManagementMenuId,
                MiniAdminSeedIds.PositionExportPermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.DictionaryManagementMenuId,
                MiniAdminSeedIds.DictionaryQueryPermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.DictionaryManagementMenuId,
                MiniAdminSeedIds.DictionaryCreatePermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.DictionaryManagementMenuId,
                MiniAdminSeedIds.DictionaryUpdatePermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.DictionaryManagementMenuId,
                MiniAdminSeedIds.DictionaryDeletePermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.ParameterSettingMenuId,
                MiniAdminSeedIds.ParameterQueryPermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.ParameterSettingMenuId,
                MiniAdminSeedIds.ParameterCreatePermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.ParameterSettingMenuId,
                MiniAdminSeedIds.ParameterUpdatePermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.ParameterSettingMenuId,
                MiniAdminSeedIds.ParameterDeletePermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.NoticeAnnouncementMenuId,
                MiniAdminSeedIds.NoticeQueryPermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.NoticeAnnouncementMenuId,
                MiniAdminSeedIds.NoticeCreatePermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.NoticeAnnouncementMenuId,
                MiniAdminSeedIds.NoticeUpdatePermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.NoticeAnnouncementMenuId,
                MiniAdminSeedIds.NoticeDeletePermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.LogManagementMenuId,
                MiniAdminSeedIds.LogQueryPermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.LogManagementMenuId,
                MiniAdminSeedIds.LogExportPermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.SystemMenuId,
                MiniAdminSeedIds.LoginLogMenuId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.SystemMenuId,
                MiniAdminSeedIds.OnlineUserMenuId,
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.LoginLogMenuId,
                MiniAdminSeedIds.LoginLogQueryPermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.OnlineUserMenuId,
                MiniAdminSeedIds.OnlineUserQueryPermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.OnlineUserMenuId,
                MiniAdminSeedIds.OnlineUserForceLogoutPermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.SystemMenuId,
                MiniAdminSeedIds.PermissionDiagnosticsMenuId,
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.PermissionDiagnosticsMenuId,
                MiniAdminSeedIds.PermissionDiagnosticsQueryPermissionId,
                cancellationToken);
            await EnsureAdminPermissionIfParentAssignedAsync(
                MiniAdminSeedIds.PermissionDiagnosticsMenuId,
                MiniAdminSeedIds.PermissionDiagnosticsRefreshCachePermissionId,
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await userAuthorizationCache.RemoveUserAsync(
            MiniAdminSeedIds.AdminUserId,
            "admin",
            cancellationToken);
    }

    private async Task SeedMenuMonitorLogRestructureAsync(CancellationToken cancellationToken)
    {
        await EnsureMenuAsync(new Menu
        {
            Id = MiniAdminSeedIds.LogCenterMenuId,
            Name = "LogManagement",
            Path = "/log",
            Redirect = "/system/log",
            Title = "日志管理",
            Icon = "lucide:file-text",
            Order = 20,
            IsEnabled = true
        }, cancellationToken);

        await EnsureMenuAsync(new Menu
        {
            Id = MiniAdminSeedIds.LogManagementMenuId,
            ParentId = MiniAdminSeedIds.LogCenterMenuId,
            Name = "OperationLog",
            Path = "/system/log",
            Component = "/system/log/index",
            Title = "操作日志",
            Icon = "lucide:clipboard-list",
            Order = 1,
            PermissionCode = "system:log:query",
            IsEnabled = true
        }, cancellationToken);

        await EnsureMenuParentAndOrderAsync(
            MiniAdminSeedIds.LoginLogMenuId,
            MiniAdminSeedIds.LogCenterMenuId,
            2,
            cancellationToken);

        await EnsureMenuAsync(new Menu
        {
            Id = MiniAdminSeedIds.SystemMonitorMenuId,
            Name = "SystemMonitor",
            Path = "/monitor",
            Redirect = "/system/online-user",
            Title = "系统监控",
            Icon = "lucide:activity",
            Order = 30,
            IsEnabled = true
        }, cancellationToken);

        await EnsureMenuParentAndOrderAsync(
            MiniAdminSeedIds.OnlineUserMenuId,
            MiniAdminSeedIds.SystemMonitorMenuId,
            1,
            cancellationToken);
        await EnsureMenuParentAndOrderAsync(
            MiniAdminSeedIds.PermissionDiagnosticsMenuId,
            MiniAdminSeedIds.SystemMonitorMenuId,
            2,
            cancellationToken);

        await EnsureParentRoleMenusAsync(
            MiniAdminSeedIds.LogCenterMenuId,
            [
                MiniAdminSeedIds.LogManagementMenuId,
                MiniAdminSeedIds.LoginLogMenuId,
                MiniAdminSeedIds.LogQueryPermissionId,
                MiniAdminSeedIds.LogExportPermissionId,
                MiniAdminSeedIds.LoginLogQueryPermissionId
            ],
            cancellationToken);
        await EnsureParentRoleMenusAsync(
            MiniAdminSeedIds.SystemMonitorMenuId,
            [
                MiniAdminSeedIds.OnlineUserMenuId,
                MiniAdminSeedIds.OnlineUserQueryPermissionId,
                MiniAdminSeedIds.OnlineUserForceLogoutPermissionId,
                MiniAdminSeedIds.PermissionDiagnosticsMenuId,
                MiniAdminSeedIds.PermissionDiagnosticsQueryPermissionId,
                MiniAdminSeedIds.PermissionDiagnosticsRefreshCachePermissionId
            ],
            cancellationToken);
    }

    private async Task SeedScheduledJobsAsync(CancellationToken cancellationToken)
    {
        await EnsureMenuAsync(new Menu
        {
            Id = MiniAdminSeedIds.ScheduledJobMenuId,
            ParentId = MiniAdminSeedIds.SystemMonitorMenuId,
            Name = "ScheduledJob",
            Path = "/system/scheduled-job",
            Component = "/system/scheduled-job/index",
            Title = "定时任务",
            Icon = "lucide:timer",
            Order = 2,
            PermissionCode = "system:scheduled-job:query",
            IsEnabled = true
        }, cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.ScheduledJobQueryPermissionId,
            MiniAdminSeedIds.ScheduledJobMenuId,
            "ScheduledJobQueryPermission",
            "system:scheduled-job:query",
            "system:scheduled-job:query",
            1,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.ScheduledJobUpdatePermissionId,
            MiniAdminSeedIds.ScheduledJobMenuId,
            "ScheduledJobUpdatePermission",
            "system:scheduled-job:update",
            "system:scheduled-job:update",
            2,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.ScheduledJobRunPermissionId,
            MiniAdminSeedIds.ScheduledJobMenuId,
            "ScheduledJobRunPermission",
            "system:scheduled-job:run",
            "system:scheduled-job:run",
            3,
            cancellationToken);
        await EnsureMenuParentAndOrderAsync(
            MiniAdminSeedIds.PermissionDiagnosticsMenuId,
            MiniAdminSeedIds.SystemMonitorMenuId,
            3,
            cancellationToken);

        await EnsureScheduledJobAsync(new ScheduledJob
        {
            Id = MiniAdminSeedIds.AuditLogCleanupJobId,
            JobKey = "audit-log-cleanup",
            Name = "清理审计日志",
            Description = "清理 90 天前的操作审计日志",
            IntervalSeconds = 24 * 60 * 60,
            IsEnabled = true,
            LastStatus = "Never",
            NextRunAt = DateTimeOffset.UtcNow.AddHours(1)
        }, cancellationToken);

        await EnsureAdminPermissionIfParentAssignedAsync(
            MiniAdminSeedIds.SystemMonitorMenuId,
            MiniAdminSeedIds.ScheduledJobMenuId,
            cancellationToken);
        await EnsureAdminPermissionIfParentAssignedAsync(
            MiniAdminSeedIds.ScheduledJobMenuId,
            MiniAdminSeedIds.ScheduledJobQueryPermissionId,
            cancellationToken);
        await EnsureAdminPermissionIfParentAssignedAsync(
            MiniAdminSeedIds.ScheduledJobMenuId,
            MiniAdminSeedIds.ScheduledJobUpdatePermissionId,
            cancellationToken);
        await EnsureAdminPermissionIfParentAssignedAsync(
            MiniAdminSeedIds.ScheduledJobMenuId,
            MiniAdminSeedIds.ScheduledJobRunPermissionId,
            cancellationToken);
    }

    private async Task SeedStorageConsistencyJobAsync(CancellationToken cancellationToken)
    {
        await EnsureScheduledJobAsync(new ScheduledJob
        {
            Id = MiniAdminSeedIds.StorageConsistencyCheckJobId,
            JobKey = "storage-consistency-check",
            Name = "检查文件存储一致性",
            Description = "检查数据库文件记录对应的本地或 MinIO 文件是否存在",
            IntervalSeconds = 24 * 60 * 60,
            IsEnabled = true,
            LastStatus = "Never",
            NextRunAt = DateTimeOffset.UtcNow.AddHours(2)
        }, cancellationToken);
    }

    private async Task SeedWorkflowSlaScanAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await EnsureNotificationTemplateAsync(new NotificationTemplate
        {
            Id = Guid.Parse("40000000-0000-0000-0000-000000000010"),
            Code = "WorkflowOverdue",
            Name = "工作流-审批超时",
            Category = "Workflow",
            Level = "Warning",
            Channel = "InApp",
            TitleTemplate = "审批超时：{instanceTitle}",
            MessageTemplate = "{nodeName} 已超过处理时限（截止：{dueAt}），请尽快处理。",
            LinkTemplate = "/workflow/center?workflowInstanceId={instanceId}{workflowTaskQuery}",
            IsEnabled = true,
            Remark = "工作流 SLA 自动催办通知。",
            CreatedAt = now,
            UpdatedAt = now
        }, cancellationToken);
        await EnsureScheduledJobAsync(new ScheduledJob
        {
            Id = MiniAdminSeedIds.WorkflowSlaScanJobId,
            JobKey = "workflow-sla-scan",
            Name = "工作流 SLA 超时扫描",
            Description = "扫描超时待办并发送自动催办消息",
            IntervalSeconds = 5 * 60,
            IsEnabled = true,
            LastStatus = "Never",
            NextRunAt = DateTimeOffset.UtcNow.AddMinutes(5)
        }, cancellationToken);
    }

    private async Task SeedWorkflowCollaborationAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await EnsureNotificationTemplateAsync(new NotificationTemplate
        {
            Id = Guid.Parse("40000000-0000-0000-0000-000000000011"),
            Code = "WorkflowComment",
            Name = "工作流-流程评论",
            Category = "Workflow",
            Level = "Info",
            Channel = "InApp",
            TitleTemplate = "流程评论：{instanceTitle}",
            MessageTemplate = "{operatorUserName} 评论了 {definitionName} 流程：{comment}",
            LinkTemplate = "/workflow/center?workflowInstanceId={instanceId}",
            IsEnabled = true,
            Remark = "工作流评论协作通知。",
            CreatedAt = now,
            UpdatedAt = now
        }, cancellationToken);
    }

    private async Task SeedFileExceptionHandlingAsync(CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(
            MiniAdminSeedIds.FileMarkInvalidPermissionId,
            MiniAdminSeedIds.FileManagementMenuId,
            "FileMarkInvalidPermission",
            "system:file:mark-invalid",
            "system:file:mark-invalid",
            5,
            cancellationToken);

        await EnsureAdminPermissionIfParentAssignedAsync(
            MiniAdminSeedIds.FileManagementMenuId,
            MiniAdminSeedIds.FileMarkInvalidPermissionId,
            cancellationToken);
    }

    private async Task SeedSystemMonitorDashboardAsync(CancellationToken cancellationToken)
    {
        await EnsureMenuAsync(new Menu
        {
            Id = MiniAdminSeedIds.SystemMonitorMenuId,
            Name = "SystemMonitor",
            Path = "/monitor",
            Redirect = "/system/monitor",
            Title = "系统监控",
            Icon = "lucide:activity",
            Order = 30,
            IsEnabled = true
        }, cancellationToken);
        await EnsureMenuAsync(new Menu
        {
            Id = MiniAdminSeedIds.SystemMonitorDashboardMenuId,
            ParentId = MiniAdminSeedIds.SystemMonitorMenuId,
            Name = "SystemMonitorDashboard",
            Path = "/system/monitor",
            Component = "/system/monitor/index",
            Title = "系统监控",
            Icon = "lucide:gauge",
            Order = 1,
            PermissionCode = "system:monitor:query",
            IsEnabled = true
        }, cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.SystemMonitorDashboardQueryPermissionId,
            MiniAdminSeedIds.SystemMonitorDashboardMenuId,
            "SystemMonitorDashboardQueryPermission",
            "system:monitor:query",
            "system:monitor:query",
            1,
            cancellationToken);
        await EnsureMenuParentAndOrderAsync(
            MiniAdminSeedIds.OnlineUserMenuId,
            MiniAdminSeedIds.SystemMonitorMenuId,
            2,
            cancellationToken);
        await EnsureMenuParentAndOrderAsync(
            MiniAdminSeedIds.ScheduledJobMenuId,
            MiniAdminSeedIds.SystemMonitorMenuId,
            3,
            cancellationToken);
        await EnsureMenuParentAndOrderAsync(
            MiniAdminSeedIds.PermissionDiagnosticsMenuId,
            MiniAdminSeedIds.SystemMonitorMenuId,
            4,
            cancellationToken);

        await EnsureAdminPermissionIfParentAssignedAsync(
            MiniAdminSeedIds.SystemMonitorMenuId,
            MiniAdminSeedIds.SystemMonitorDashboardMenuId,
            cancellationToken);
        await EnsureAdminPermissionIfParentAssignedAsync(
            MiniAdminSeedIds.SystemMonitorDashboardMenuId,
            MiniAdminSeedIds.SystemMonitorDashboardQueryPermissionId,
            cancellationToken);
    }

    private async Task SeedAlertCenterAsync(CancellationToken cancellationToken)
    {
        await EnsureMenuAsync(new Menu
        {
            Id = MiniAdminSeedIds.AlertCenterMenuId,
            ParentId = MiniAdminSeedIds.SystemMonitorMenuId,
            Name = "AlertCenter",
            Path = "/system/alert",
            Component = "/system/alert/index",
            Title = "告警中心",
            Icon = "lucide:bell-ring",
            Order = 2,
            PermissionCode = "system:alert:query",
            IsEnabled = true
        }, cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.AlertQueryPermissionId,
            MiniAdminSeedIds.AlertCenterMenuId,
            "AlertQueryPermission",
            "system:alert:query",
            "system:alert:query",
            1,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.AlertAcknowledgePermissionId,
            MiniAdminSeedIds.AlertCenterMenuId,
            "AlertAcknowledgePermission",
            "system:alert:acknowledge",
            "system:alert:acknowledge",
            2,
            cancellationToken);
        await EnsureScheduledJobAsync(new ScheduledJob
        {
            Id = MiniAdminSeedIds.AlertScanJobId,
            JobKey = "alert-scan",
            Name = "系统告警扫描",
            Description = "扫描系统监控指标并生成告警记录",
            IntervalSeconds = 60,
            IsEnabled = true,
            LastStatus = "Never",
            NextRunAt = DateTimeOffset.UtcNow.AddMinutes(1)
        }, cancellationToken);
        await EnsureMenuParentAndOrderAsync(
            MiniAdminSeedIds.OnlineUserMenuId,
            MiniAdminSeedIds.SystemMonitorMenuId,
            4,
            cancellationToken);
        await EnsureMenuParentAndOrderAsync(
            MiniAdminSeedIds.ScheduledJobMenuId,
            MiniAdminSeedIds.SystemMonitorMenuId,
            5,
            cancellationToken);
        await EnsureMenuParentAndOrderAsync(
            MiniAdminSeedIds.PermissionDiagnosticsMenuId,
            MiniAdminSeedIds.SystemMonitorMenuId,
            6,
            cancellationToken);

        await EnsureAdminPermissionIfParentAssignedAsync(
            MiniAdminSeedIds.SystemMonitorMenuId,
            MiniAdminSeedIds.AlertCenterMenuId,
            cancellationToken);
        await EnsureAdminPermissionIfParentAssignedAsync(
            MiniAdminSeedIds.AlertCenterMenuId,
            MiniAdminSeedIds.AlertQueryPermissionId,
            cancellationToken);
        await EnsureAdminPermissionIfParentAssignedAsync(
            MiniAdminSeedIds.AlertCenterMenuId,
            MiniAdminSeedIds.AlertAcknowledgePermissionId,
            cancellationToken);
    }

    private async Task SeedNotificationCenterAsync(CancellationToken cancellationToken)
    {
        await EnsureMenuAsync(new Menu
        {
            Id = MiniAdminSeedIds.NotificationCenterMenuId,
            ParentId = MiniAdminSeedIds.SystemMonitorMenuId,
            Name = "NotificationCenter",
            Path = "/system/notification",
            Component = "/system/notification/index",
            Title = "通知中心",
            Icon = "lucide:inbox",
            Order = 3,
            PermissionCode = "system:notification:query",
            IsEnabled = true
        }, cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.NotificationQueryPermissionId,
            MiniAdminSeedIds.NotificationCenterMenuId,
            "NotificationQueryPermission",
            "system:notification:query",
            "system:notification:query",
            1,
            cancellationToken);
        await EnsureMenuParentAndOrderAsync(
            MiniAdminSeedIds.OnlineUserMenuId,
            MiniAdminSeedIds.SystemMonitorMenuId,
            4,
            cancellationToken);
        await EnsureMenuParentAndOrderAsync(
            MiniAdminSeedIds.ScheduledJobMenuId,
            MiniAdminSeedIds.SystemMonitorMenuId,
            5,
            cancellationToken);
        await EnsureMenuParentAndOrderAsync(
            MiniAdminSeedIds.PermissionDiagnosticsMenuId,
            MiniAdminSeedIds.SystemMonitorMenuId,
            6,
            cancellationToken);

        await EnsureAdminPermissionIfParentAssignedAsync(
            MiniAdminSeedIds.SystemMonitorMenuId,
            MiniAdminSeedIds.NotificationCenterMenuId,
            cancellationToken);
        await EnsureAdminPermissionIfParentAssignedAsync(
            MiniAdminSeedIds.NotificationCenterMenuId,
            MiniAdminSeedIds.NotificationQueryPermissionId,
            cancellationToken);
    }

    private async Task SeedNotificationTemplatesAsync(CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(
            MiniAdminSeedIds.NotificationTemplateUpdatePermissionId,
            MiniAdminSeedIds.NotificationCenterMenuId,
            "NotificationTemplateUpdatePermission",
            "system:notification:template:update",
            "system:notification:template:update",
            2,
            cancellationToken);
        await EnsureAdminPermissionIfParentAssignedAsync(
            MiniAdminSeedIds.NotificationCenterMenuId,
            MiniAdminSeedIds.NotificationTemplateUpdatePermissionId,
            cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var templates = new[]
        {
            new NotificationTemplate
            {
                Id = Guid.Parse("40000000-0000-0000-0000-000000000001"),
                Code = "Alert.Warning",
                Name = "系统告警-警告",
                Category = "SystemAlert",
                Level = "Warning",
                Channel = "InApp",
                TitleTemplate = "[{levelText}] {title}",
                MessageTemplate = "{content}",
                LinkTemplate = "/system/alert",
                IsEnabled = true,
                Remark = "用于警告级系统告警站内信。",
                CreatedAt = now,
                UpdatedAt = now
            },
            new NotificationTemplate
            {
                Id = Guid.Parse("40000000-0000-0000-0000-000000000002"),
                Code = "Alert.Critical",
                Name = "系统告警-严重",
                Category = "SystemAlert",
                Level = "Critical",
                Channel = "InApp",
                TitleTemplate = "[{levelText}] {title}",
                MessageTemplate = "{content}",
                LinkTemplate = "/system/alert",
                IsEnabled = true,
                Remark = "用于严重级系统告警站内信。",
                CreatedAt = now,
                UpdatedAt = now
            },
            new NotificationTemplate
            {
                Id = Guid.Parse("40000000-0000-0000-0000-000000000003"),
                Code = "WorkflowTask",
                Name = "工作流-待办任务",
                Category = "Workflow",
                Level = "Info",
                Channel = "InApp",
                TitleTemplate = "新的审批待办：{instanceTitle}",
                MessageTemplate = "你有一个 {definitionName} 流程需要处理，当前节点：{nodeName}。",
                LinkTemplate = "/workflow/center?workflowInstanceId={instanceId}{workflowTaskQuery}",
                IsEnabled = true,
                Remark = "预留给工作流待办通知，后续迁移执行链路。",
                CreatedAt = now,
                UpdatedAt = now
            },
            new NotificationTemplate
            {
                Id = Guid.Parse("40000000-0000-0000-0000-000000000004"),
                Code = "WorkflowApprove",
                Name = "工作流-审批通过",
                Category = "Workflow",
                Level = "Info",
                Channel = "InApp",
                TitleTemplate = "审批通过：{instanceTitle}",
                MessageTemplate = "{operatorUserName} 已通过 {definitionName} 流程。",
                LinkTemplate = "/workflow/center?workflowInstanceId={instanceId}",
                IsEnabled = true,
                Remark = "预留给工作流审批通过通知。",
                CreatedAt = now,
                UpdatedAt = now
            },
            new NotificationTemplate
            {
                Id = Guid.Parse("40000000-0000-0000-0000-000000000005"),
                Code = "WorkflowReject",
                Name = "工作流-审批驳回",
                Category = "Workflow",
                Level = "Warning",
                Channel = "InApp",
                TitleTemplate = "审批驳回：{instanceTitle}",
                MessageTemplate = "{operatorUserName} 驳回了 {definitionName} 流程，原因：{comment}。",
                LinkTemplate = "/workflow/center?workflowInstanceId={instanceId}",
                IsEnabled = true,
                Remark = "预留给工作流审批驳回通知。",
                CreatedAt = now,
                UpdatedAt = now
            },
            new NotificationTemplate
            {
                Id = Guid.Parse("40000000-0000-0000-0000-000000000006"),
                Code = "WorkflowWithdraw",
                Name = "工作流-撤回",
                Category = "Workflow",
                Level = "Info",
                Channel = "InApp",
                TitleTemplate = "流程撤回：{instanceTitle}",
                MessageTemplate = "{operatorUserName} 已撤回 {definitionName} 流程。",
                LinkTemplate = "/workflow/center?workflowInstanceId={instanceId}",
                IsEnabled = true,
                Remark = "预留给工作流撤回通知。",
                CreatedAt = now,
                UpdatedAt = now
            },
            new NotificationTemplate
            {
                Id = Guid.Parse("40000000-0000-0000-0000-000000000007"),
                Code = "WorkflowTransfer",
                Name = "工作流-转办",
                Category = "Workflow",
                Level = "Info",
                Channel = "InApp",
                TitleTemplate = "审批转办：{instanceTitle}",
                MessageTemplate = "{operatorUserName} 已将 {nodeName} 转办给 {targetUserName}。",
                LinkTemplate = "/workflow/center?workflowInstanceId={instanceId}{workflowTaskQuery}",
                IsEnabled = true,
                Remark = "预留给工作流转办通知。",
                CreatedAt = now,
                UpdatedAt = now
            },
            new NotificationTemplate
            {
                Id = Guid.Parse("40000000-0000-0000-0000-000000000008"),
                Code = "WorkflowRemind",
                Name = "工作流-催办",
                Category = "Workflow",
                Level = "Warning",
                Channel = "InApp",
                TitleTemplate = "审批催办：{instanceTitle}",
                MessageTemplate = "{operatorUserName} 正在催办 {nodeName}，请尽快处理。",
                LinkTemplate = "/workflow/center?workflowInstanceId={instanceId}{workflowTaskQuery}",
                IsEnabled = true,
                Remark = "预留给工作流催办通知。",
                CreatedAt = now,
                UpdatedAt = now
            },
            new NotificationTemplate
            {
                Id = Guid.Parse("40000000-0000-0000-0000-000000000010"),
                Code = "WorkflowOverdue",
                Name = "工作流-审批超时",
                Category = "Workflow",
                Level = "Warning",
                Channel = "InApp",
                TitleTemplate = "审批超时：{instanceTitle}",
                MessageTemplate = "{nodeName} 已超过处理时限（截止：{dueAt}），请尽快处理。",
                LinkTemplate = "/workflow/center?workflowInstanceId={instanceId}{workflowTaskQuery}",
                IsEnabled = true,
                Remark = "工作流 SLA 自动催办通知。",
                CreatedAt = now,
                UpdatedAt = now
            },
            new NotificationTemplate
            {
                Id = Guid.Parse("40000000-0000-0000-0000-000000000009"),
                Code = "WorkflowCc",
                Name = "工作流-抄送",
                Category = "Workflow",
                Level = "Info",
                Channel = "InApp",
                TitleTemplate = "流程抄送：{instanceTitle}",
                MessageTemplate = "你被抄送了 {definitionName} 流程，当前节点：{nodeName}。",
                LinkTemplate = "/workflow/center?workflowInstanceId={instanceId}",
                IsEnabled = true,
                Remark = "预留给工作流抄送通知。",
                CreatedAt = now,
                UpdatedAt = now
            },
            new NotificationTemplate
            {
                Id = Guid.Parse("40000000-0000-0000-0000-000000000011"),
                Code = "WorkflowComment",
                Name = "工作流-流程评论",
                Category = "Workflow",
                Level = "Info",
                Channel = "InApp",
                TitleTemplate = "流程评论：{instanceTitle}",
                MessageTemplate = "{operatorUserName} 评论了 {definitionName} 流程：{comment}",
                LinkTemplate = "/workflow/center?workflowInstanceId={instanceId}",
                IsEnabled = true,
                Remark = "工作流评论协作通知。",
                CreatedAt = now,
                UpdatedAt = now
            }
        };

        foreach (var template in templates)
        {
            await EnsureNotificationTemplateAsync(template, cancellationToken);
        }
    }

    private async Task SeedNotificationRetryAsync(CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(
            MiniAdminSeedIds.NotificationRetryPermissionId,
            MiniAdminSeedIds.NotificationCenterMenuId,
            "NotificationRetryPermission",
            "system:notification:retry",
            "system:notification:retry",
            4,
            cancellationToken);
        await EnsureAdminPermissionIfParentAssignedAsync(
            MiniAdminSeedIds.NotificationCenterMenuId,
            MiniAdminSeedIds.NotificationRetryPermissionId,
            cancellationToken);
    }

    private async Task SeedNotificationDeliveryRetryJobAsync(CancellationToken cancellationToken)
    {
        await EnsureScheduledJobAsync(new ScheduledJob
        {
            Id = MiniAdminSeedIds.NotificationDeliveryRetryJobId,
            JobKey = "notification-delivery-retry",
            Name = "通知投递自动重试",
            Description = "自动重试失败或跳过的邮件与 Webhook 投递记录",
            IntervalSeconds = 10 * 60,
            IsEnabled = true,
            LastStatus = "Never",
            NextRunAt = DateTimeOffset.UtcNow.AddMinutes(5)
        }, cancellationToken);
    }

    private async Task SeedNotificationPoliciesAsync(CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(
            MiniAdminSeedIds.NotificationPolicyUpdatePermissionId,
            MiniAdminSeedIds.NotificationCenterMenuId,
            "NotificationPolicyUpdatePermission",
            "system:notification:policy:update",
            "system:notification:policy:update",
            3,
            cancellationToken);
        await EnsureAdminPermissionIfParentAssignedAsync(
            MiniAdminSeedIds.NotificationCenterMenuId,
            MiniAdminSeedIds.NotificationPolicyUpdatePermissionId,
            cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var policies = new[]
        {
            new NotificationPolicy
            {
                Id = Guid.Parse("41000000-0000-0000-0000-000000000001"),
                EventCode = "WorkflowTask",
                EventName = "工作流-待办任务",
                Category = "Workflow",
                RecipientStrategy = "WorkflowDefault",
                EnableInApp = true,
                EnableEmail = false,
                EnableWebhook = false,
                IsEnabled = true,
                Remark = "审批节点生成待办时通知当前处理人。",
                CreatedAt = now,
                UpdatedAt = now
            },
            new NotificationPolicy
            {
                Id = Guid.Parse("41000000-0000-0000-0000-000000000002"),
                EventCode = "WorkflowApprove",
                EventName = "工作流-审批通过",
                Category = "Workflow",
                RecipientStrategy = "WorkflowDefault",
                EnableInApp = true,
                EnableEmail = false,
                EnableWebhook = false,
                IsEnabled = true,
                Remark = "审批通过后通知流程发起人。",
                CreatedAt = now,
                UpdatedAt = now
            },
            new NotificationPolicy
            {
                Id = Guid.Parse("41000000-0000-0000-0000-000000000003"),
                EventCode = "WorkflowReject",
                EventName = "工作流-审批驳回",
                Category = "Workflow",
                RecipientStrategy = "WorkflowDefault",
                EnableInApp = true,
                EnableEmail = false,
                EnableWebhook = false,
                IsEnabled = true,
                Remark = "审批驳回后通知流程发起人。",
                CreatedAt = now,
                UpdatedAt = now
            },
            new NotificationPolicy
            {
                Id = Guid.Parse("41000000-0000-0000-0000-000000000004"),
                EventCode = "WorkflowWithdraw",
                EventName = "工作流-撤回",
                Category = "Workflow",
                RecipientStrategy = "WorkflowDefault",
                EnableInApp = true,
                EnableEmail = false,
                EnableWebhook = false,
                IsEnabled = true,
                Remark = "流程撤回后通知相关处理人。",
                CreatedAt = now,
                UpdatedAt = now
            },
            new NotificationPolicy
            {
                Id = Guid.Parse("41000000-0000-0000-0000-000000000005"),
                EventCode = "WorkflowTransfer",
                EventName = "工作流-转办",
                Category = "Workflow",
                RecipientStrategy = "WorkflowDefault",
                EnableInApp = true,
                EnableEmail = false,
                EnableWebhook = false,
                IsEnabled = true,
                Remark = "审批任务转办后通知新的处理人。",
                CreatedAt = now,
                UpdatedAt = now
            },
            new NotificationPolicy
            {
                Id = Guid.Parse("41000000-0000-0000-0000-000000000006"),
                EventCode = "WorkflowRemind",
                EventName = "工作流-催办",
                Category = "Workflow",
                RecipientStrategy = "WorkflowDefault",
                EnableInApp = true,
                EnableEmail = false,
                EnableWebhook = false,
                IsEnabled = true,
                Remark = "人工催办审批任务时通知当前处理人。",
                CreatedAt = now,
                UpdatedAt = now
            },
            new NotificationPolicy
            {
                Id = Guid.Parse("41000000-0000-0000-0000-000000000007"),
                EventCode = "WorkflowOverdue",
                EventName = "工作流-审批超时",
                Category = "Workflow",
                RecipientStrategy = "WorkflowDefault",
                EnableInApp = true,
                EnableEmail = false,
                EnableWebhook = false,
                IsEnabled = true,
                Remark = "SLA 扫描发现待办超时时通知当前处理人。",
                CreatedAt = now,
                UpdatedAt = now
            },
            new NotificationPolicy
            {
                Id = Guid.Parse("41000000-0000-0000-0000-000000000008"),
                EventCode = "WorkflowCc",
                EventName = "工作流-抄送",
                Category = "Workflow",
                RecipientStrategy = "WorkflowDefault",
                EnableInApp = true,
                EnableEmail = false,
                EnableWebhook = false,
                IsEnabled = true,
                Remark = "流程抄送节点命中时通知抄送人。",
                CreatedAt = now,
                UpdatedAt = now
            },
            new NotificationPolicy
            {
                Id = Guid.Parse("41000000-0000-0000-0000-000000000009"),
                EventCode = "WorkflowComment",
                EventName = "工作流-流程评论",
                Category = "Workflow",
                RecipientStrategy = "WorkflowDefault",
                EnableInApp = true,
                EnableEmail = false,
                EnableWebhook = false,
                IsEnabled = true,
                Remark = "流程评论后通知流程参与人。",
                CreatedAt = now,
                UpdatedAt = now
            }
        };

        foreach (var policy in policies)
        {
            await EnsureNotificationPolicyAsync(policy, cancellationToken);
        }
    }

    private async Task SeedAlertRulesAsync(CancellationToken cancellationToken)
    {
        await EnsureMenuAsync(new Menu
        {
            Id = MiniAdminSeedIds.AlertRuleMenuId,
            ParentId = MiniAdminSeedIds.SystemMonitorMenuId,
            Name = "AlertRule",
            Path = "/system/alert-rule",
            Component = "/system/alert-rule/index",
            Title = "告警规则",
            Icon = "lucide:sliders-horizontal",
            Order = 3,
            PermissionCode = "system:alert-rule:query",
            IsEnabled = true
        }, cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.AlertRuleQueryPermissionId,
            MiniAdminSeedIds.AlertRuleMenuId,
            "AlertRuleQueryPermission",
            "system:alert-rule:query",
            "system:alert-rule:query",
            1,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.AlertRuleUpdatePermissionId,
            MiniAdminSeedIds.AlertRuleMenuId,
            "AlertRuleUpdatePermission",
            "system:alert-rule:update",
            "system:alert-rule:update",
            2,
            cancellationToken);

        await EnsureMenuParentAndOrderAsync(
            MiniAdminSeedIds.NotificationCenterMenuId,
            MiniAdminSeedIds.SystemMonitorMenuId,
            4,
            cancellationToken);
        await EnsureMenuParentAndOrderAsync(
            MiniAdminSeedIds.OnlineUserMenuId,
            MiniAdminSeedIds.SystemMonitorMenuId,
            5,
            cancellationToken);
        await EnsureMenuParentAndOrderAsync(
            MiniAdminSeedIds.ScheduledJobMenuId,
            MiniAdminSeedIds.SystemMonitorMenuId,
            6,
            cancellationToken);
        await EnsureMenuParentAndOrderAsync(
            MiniAdminSeedIds.PermissionDiagnosticsMenuId,
            MiniAdminSeedIds.SystemMonitorMenuId,
            7,
            cancellationToken);

        await EnsureAlertRuleAsync(new AlertRule
        {
            Id = MiniAdminSeedIds.AlertRuleMemoryHighId,
            Code = "MemoryHigh",
            Name = "内存使用率过高",
            Description = "系统物理内存使用率达到阈值时触发",
            Metric = "system.memory.usedPercent",
            Operator = ">=",
            Threshold = 85,
            WindowMinutes = 1,
            Level = "Warning",
            Enabled = true,
            NotifyEnabled = true,
            Sort = 1
        }, cancellationToken);
        await EnsureAlertRuleAsync(new AlertRule
        {
            Id = MiniAdminSeedIds.AlertRuleDependencyUnhealthyId,
            Code = "DependencyUnhealthy",
            Name = "依赖异常",
            Description = "数据库、缓存或文件存储依赖异常时触发",
            Metric = "system.dependency.unhealthyCount",
            Operator = ">=",
            Threshold = 1,
            WindowMinutes = 1,
            Level = "Critical",
            Enabled = true,
            NotifyEnabled = true,
            Sort = 2
        }, cancellationToken);
        await EnsureAlertRuleAsync(new AlertRule
        {
            Id = MiniAdminSeedIds.AlertRuleScheduledJobFailedId,
            Code = "ScheduledJobFailed",
            Name = "定时任务失败",
            Description = "统计窗口内存在失败定时任务时触发",
            Metric = "scheduledJob.failedCount",
            Operator = ">=",
            Threshold = 1,
            WindowMinutes = 1440,
            Level = "Warning",
            Enabled = true,
            NotifyEnabled = true,
            Sort = 3
        }, cancellationToken);
        await EnsureAlertRuleAsync(new AlertRule
        {
            Id = MiniAdminSeedIds.AlertRuleAuditFailureHighId,
            Code = "AuditFailureHigh",
            Name = "操作失败日志过多",
            Description = "统计窗口内存在失败操作日志时触发",
            Metric = "auditLog.failedCount",
            Operator = ">=",
            Threshold = 1,
            WindowMinutes = 1440,
            Level = "Warning",
            Enabled = true,
            NotifyEnabled = true,
            Sort = 4
        }, cancellationToken);
        await EnsureAlertRuleAsync(new AlertRule
        {
            Id = MiniAdminSeedIds.AlertRuleAbnormalFileDetectedId,
            Code = "AbnormalFileDetected",
            Name = "发现异常文件",
            Description = "文件记录存在缺失或异常状态时触发",
            Metric = "managedFile.abnormalCount",
            Operator = ">=",
            Threshold = 1,
            WindowMinutes = 1,
            Level = "Warning",
            Enabled = true,
            NotifyEnabled = true,
            Sort = 5
        }, cancellationToken);

        await EnsureAdminPermissionIfParentAssignedAsync(
            MiniAdminSeedIds.SystemMonitorMenuId,
            MiniAdminSeedIds.SystemMonitorDashboardMenuId,
            cancellationToken);
        await EnsureAdminPermissionIfParentAssignedAsync(
            MiniAdminSeedIds.SystemMonitorMenuId,
            MiniAdminSeedIds.AlertCenterMenuId,
            cancellationToken);
        await EnsureAdminPermissionIfParentAssignedAsync(
            MiniAdminSeedIds.SystemMonitorMenuId,
            MiniAdminSeedIds.NotificationCenterMenuId,
            cancellationToken);
        await EnsureAdminPermissionIfParentAssignedAsync(
            MiniAdminSeedIds.SystemMonitorMenuId,
            MiniAdminSeedIds.AlertRuleMenuId,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await EnsureAdminPermissionIfParentAssignedAsync(
            MiniAdminSeedIds.SystemMonitorDashboardMenuId,
            MiniAdminSeedIds.SystemMonitorDashboardQueryPermissionId,
            cancellationToken);
        await EnsureAdminPermissionIfParentAssignedAsync(
            MiniAdminSeedIds.AlertCenterMenuId,
            MiniAdminSeedIds.AlertQueryPermissionId,
            cancellationToken);
        await EnsureAdminPermissionIfParentAssignedAsync(
            MiniAdminSeedIds.AlertCenterMenuId,
            MiniAdminSeedIds.AlertAcknowledgePermissionId,
            cancellationToken);
        await EnsureAdminPermissionIfParentAssignedAsync(
            MiniAdminSeedIds.NotificationCenterMenuId,
            MiniAdminSeedIds.NotificationQueryPermissionId,
            cancellationToken);
        await EnsureAdminPermissionIfParentAssignedAsync(
            MiniAdminSeedIds.AlertRuleMenuId,
            MiniAdminSeedIds.AlertRuleQueryPermissionId,
            cancellationToken);
        await EnsureAdminPermissionIfParentAssignedAsync(
            MiniAdminSeedIds.AlertRuleMenuId,
            MiniAdminSeedIds.AlertRuleUpdatePermissionId,
            cancellationToken);
    }

    private async Task SeedSecurityCenterAsync(CancellationToken cancellationToken)
    {
        await EnsureMenuAsync(new Menu
        {
            Id = MiniAdminSeedIds.SecurityCenterMenuId,
            ParentId = MiniAdminSeedIds.SystemMonitorMenuId,
            Name = "SecurityCenter",
            Path = "/system/security-center",
            Component = "/system/security-center/index",
            Title = "安全中心",
            Icon = "lucide:shield-check",
            Order = 5,
            PermissionCode = "system:security-center:query",
            IsEnabled = true
        }, cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.SecurityCenterQueryPermissionId,
            MiniAdminSeedIds.SecurityCenterMenuId,
            "SecurityCenterQueryPermission",
            "system:security-center:query",
            "system:security-center:query",
            1,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.SecurityEventQueryPermissionId,
            MiniAdminSeedIds.SecurityCenterMenuId,
            "SecurityEventQueryPermission",
            "system:security-event:query",
            "system:security-event:query",
            2,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.SecurityPolicyQueryPermissionId,
            MiniAdminSeedIds.SecurityCenterMenuId,
            "SecurityPolicyQueryPermission",
            "system:security-policy:query",
            "system:security-policy:query",
            3,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.SecurityPolicyUpdatePermissionId,
            MiniAdminSeedIds.SecurityCenterMenuId,
            "SecurityPolicyUpdatePermission",
            "system:security-policy:update",
            "system:security-policy:update",
            4,
            cancellationToken);

        await EnsureMenuParentAndOrderAsync(
            MiniAdminSeedIds.OnlineUserMenuId,
            MiniAdminSeedIds.SystemMonitorMenuId,
            6,
            cancellationToken);
        await EnsureMenuParentAndOrderAsync(
            MiniAdminSeedIds.ScheduledJobMenuId,
            MiniAdminSeedIds.SystemMonitorMenuId,
            7,
            cancellationToken);
        await EnsureMenuParentAndOrderAsync(
            MiniAdminSeedIds.PermissionDiagnosticsMenuId,
            MiniAdminSeedIds.SystemMonitorMenuId,
            8,
            cancellationToken);

        await EnsureAdminPermissionIfParentAssignedAsync(
            MiniAdminSeedIds.SystemMonitorMenuId,
            MiniAdminSeedIds.SecurityCenterMenuId,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await EnsureAdminPermissionIfParentAssignedAsync(
            MiniAdminSeedIds.SecurityCenterMenuId,
            MiniAdminSeedIds.SecurityCenterQueryPermissionId,
            cancellationToken);
        await EnsureAdminPermissionIfParentAssignedAsync(
            MiniAdminSeedIds.SecurityCenterMenuId,
            MiniAdminSeedIds.SecurityEventQueryPermissionId,
            cancellationToken);
        await EnsureAdminPermissionIfParentAssignedAsync(
            MiniAdminSeedIds.SecurityCenterMenuId,
            MiniAdminSeedIds.SecurityPolicyQueryPermissionId,
            cancellationToken);
        await EnsureAdminPermissionIfParentAssignedAsync(
            MiniAdminSeedIds.SecurityCenterMenuId,
            MiniAdminSeedIds.SecurityPolicyUpdatePermissionId,
            cancellationToken);
    }

    private async Task SeedNotificationRoutingAsync(CancellationToken cancellationToken)
    {
        var alertRuleIds = await dbContext.AlertRules
            .Select(rule => rule.Id)
            .ToArrayAsync(cancellationToken);

        foreach (var alertRuleId in alertRuleIds)
        {
            await EnsureDefaultAlertRuleRecipientAsync(alertRuleId, cancellationToken);
        }
    }

    private async Task SeedPlatformTenantManagementAsync(CancellationToken cancellationToken)
    {
        await EnsureMenuAsync(new Menu
        {
            Id = MiniAdminSeedIds.PlatformManagementMenuId,
            Name = "PlatformManagement",
            Path = "/platform",
            Redirect = "/platform/tenant",
            Title = "平台管理",
            Icon = "lucide:building-2",
            Order = 10,
            IsEnabled = true
        }, cancellationToken);

        await EnsureMenuAsync(new Menu
        {
            Id = MiniAdminSeedIds.PlatformTenantMenuId,
            ParentId = MiniAdminSeedIds.PlatformManagementMenuId,
            Name = "PlatformTenant",
            Path = "/platform/tenant",
            Component = "/platform/tenant/index",
            Title = "租户管理",
            Icon = "lucide:building",
            Order = 1,
            PermissionCode = "platform:tenant:query",
            IsEnabled = true
        }, cancellationToken);

        await EnsurePermissionAsync(
            MiniAdminSeedIds.PlatformTenantQueryPermissionId,
            MiniAdminSeedIds.PlatformTenantMenuId,
            "PlatformTenantQueryPermission",
            "platform:tenant:query",
            "platform:tenant:query",
            1,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.PlatformTenantCreatePermissionId,
            MiniAdminSeedIds.PlatformTenantMenuId,
            "PlatformTenantCreatePermission",
            "platform:tenant:create",
            "platform:tenant:create",
            2,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.PlatformTenantUpdatePermissionId,
            MiniAdminSeedIds.PlatformTenantMenuId,
            "PlatformTenantUpdatePermission",
            "platform:tenant:update",
            "platform:tenant:update",
            3,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.PlatformTenantEnablePermissionId,
            MiniAdminSeedIds.PlatformTenantMenuId,
            "PlatformTenantEnablePermission",
            "platform:tenant:enable",
            "platform:tenant:enable",
            4,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.PlatformTenantDisablePermissionId,
            MiniAdminSeedIds.PlatformTenantMenuId,
            "PlatformTenantDisablePermission",
            "platform:tenant:disable",
            "platform:tenant:disable",
            5,
            cancellationToken);

        await EnsureRoleMenuAsync(MiniAdminSeedIds.PlatformManagementMenuId, cancellationToken);
        await EnsureRoleMenuAsync(MiniAdminSeedIds.PlatformTenantMenuId, cancellationToken);
        await EnsureRoleMenuAsync(MiniAdminSeedIds.PlatformTenantQueryPermissionId, cancellationToken);
        await EnsureRoleMenuAsync(MiniAdminSeedIds.PlatformTenantCreatePermissionId, cancellationToken);
        await EnsureRoleMenuAsync(MiniAdminSeedIds.PlatformTenantUpdatePermissionId, cancellationToken);
        await EnsureRoleMenuAsync(MiniAdminSeedIds.PlatformTenantEnablePermissionId, cancellationToken);
        await EnsureRoleMenuAsync(MiniAdminSeedIds.PlatformTenantDisablePermissionId, cancellationToken);
    }

    private async Task SeedCodeGeneratorAsync(CancellationToken cancellationToken)
    {
        await EnsureMenuAsync(new Menu
        {
            Id = MiniAdminSeedIds.DevelopmentToolsMenuId,
            ParentId = MiniAdminSeedIds.SystemMenuId,
            Name = "DevelopmentTools",
            Path = "/system/development",
            Redirect = "/system/code-generator",
            Title = "开发工具",
            Icon = "lucide:code-2",
            Order = 9,
            IsEnabled = true
        }, cancellationToken);

        await EnsureMenuAsync(new Menu
        {
            Id = MiniAdminSeedIds.CodeGeneratorMenuId,
            ParentId = MiniAdminSeedIds.DevelopmentToolsMenuId,
            Name = "CodeGenerator",
            Path = "/system/code-generator",
            Component = "/system/code-generator/index",
            Title = "代码生成",
            Icon = "lucide:wand-sparkles",
            Order = 1,
            PermissionCode = "system:code-generator:query",
            IsEnabled = true
        }, cancellationToken);

        await EnsurePermissionAsync(
            MiniAdminSeedIds.CodeGeneratorQueryPermissionId,
            MiniAdminSeedIds.CodeGeneratorMenuId,
            "CodeGeneratorQueryPermission",
            "system:code-generator:query",
            "system:code-generator:query",
            1,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.CodeGeneratorPreviewPermissionId,
            MiniAdminSeedIds.CodeGeneratorMenuId,
            "CodeGeneratorPreviewPermission",
            "system:code-generator:preview",
            "system:code-generator:preview",
            2,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.CodeGeneratorGeneratePermissionId,
            MiniAdminSeedIds.CodeGeneratorMenuId,
            "CodeGeneratorGeneratePermission",
            "system:code-generator:generate",
            "system:code-generator:generate",
            3,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.CodeGeneratorRollbackPermissionId,
            MiniAdminSeedIds.CodeGeneratorMenuId,
            "CodeGeneratorRollbackPermission",
            "system:code-generator:rollback",
            "system:code-generator:rollback",
            4,
            cancellationToken);

        await EnsureRoleMenuAsync(MiniAdminSeedIds.SystemMenuId, cancellationToken);
        await EnsureRoleMenuAsync(MiniAdminSeedIds.DevelopmentToolsMenuId, cancellationToken);
        await EnsureRoleMenuAsync(MiniAdminSeedIds.CodeGeneratorMenuId, cancellationToken);
        await EnsureRoleMenuAsync(MiniAdminSeedIds.CodeGeneratorQueryPermissionId, cancellationToken);
        await EnsureRoleMenuAsync(MiniAdminSeedIds.CodeGeneratorPreviewPermissionId, cancellationToken);
        await EnsureRoleMenuAsync(MiniAdminSeedIds.CodeGeneratorGeneratePermissionId, cancellationToken);
        await EnsureRoleMenuAsync(MiniAdminSeedIds.CodeGeneratorRollbackPermissionId, cancellationToken);
    }

    private async Task SeedProjectRuntimeAsync(CancellationToken cancellationToken)
    {
        await EnsureMenuAsync(new Menu
        {
            Id = MiniAdminSeedIds.DevelopmentToolsMenuId,
            ParentId = MiniAdminSeedIds.SystemMenuId,
            Name = "DevelopmentTools",
            Path = "/system/development",
            Redirect = "/system/code-generator",
            Title = "开发工具",
            Icon = "lucide:code-2",
            Order = 9,
            IsEnabled = true
        }, cancellationToken);

        await EnsureMenuAsync(new Menu
        {
            Id = MiniAdminSeedIds.ProjectRuntimeMenuId,
            ParentId = MiniAdminSeedIds.DevelopmentToolsMenuId,
            Name = "ProjectRuntime",
            Path = "/system/project-runtime",
            Component = "/system/project-runtime/index",
            Title = "项目运行管理",
            Icon = "lucide:panel-top-open",
            Order = 2,
            PermissionCode = "system:project-runtime:query",
            IsEnabled = true
        }, cancellationToken);

        await EnsurePermissionAsync(
            MiniAdminSeedIds.ProjectRuntimeQueryPermissionId,
            MiniAdminSeedIds.ProjectRuntimeMenuId,
            "ProjectRuntimeQueryPermission",
            "system:project-runtime:query",
            "system:project-runtime:query",
            1,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.ProjectRuntimeManagePermissionId,
            MiniAdminSeedIds.ProjectRuntimeMenuId,
            "ProjectRuntimeManagePermission",
            "system:project-runtime:manage",
            "system:project-runtime:manage",
            2,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.ProjectRuntimeLogPermissionId,
            MiniAdminSeedIds.ProjectRuntimeMenuId,
            "ProjectRuntimeLogPermission",
            "system:project-runtime:log",
            "system:project-runtime:log",
            3,
            cancellationToken);

        await EnsureRoleMenuAsync(MiniAdminSeedIds.SystemMenuId, cancellationToken);
        await EnsureRoleMenuAsync(MiniAdminSeedIds.DevelopmentToolsMenuId, cancellationToken);
        await EnsureRoleMenuAsync(MiniAdminSeedIds.ProjectRuntimeMenuId, cancellationToken);
        await EnsureRoleMenuAsync(MiniAdminSeedIds.ProjectRuntimeQueryPermissionId, cancellationToken);
        await EnsureRoleMenuAsync(MiniAdminSeedIds.ProjectRuntimeManagePermissionId, cancellationToken);
        await EnsureRoleMenuAsync(MiniAdminSeedIds.ProjectRuntimeLogPermissionId, cancellationToken);
    }

    private async Task SeedWorkflowBasicAsync(CancellationToken cancellationToken)
    {
        await EnsureMenuAsync(new Menu
        {
            Id = MiniAdminSeedIds.WorkflowManagementMenuId,
            Name = "WorkflowManagement",
            Path = "/workflow",
            Redirect = "/workflow/center",
            Title = "工作流",
            Icon = "lucide:workflow",
            Order = 6,
            IsEnabled = true
        }, cancellationToken);

        await EnsureMenuAsync(new Menu
        {
            Id = MiniAdminSeedIds.WorkflowCenterMenuId,
            ParentId = MiniAdminSeedIds.WorkflowManagementMenuId,
            Name = "WorkflowCenter",
            Path = "/workflow/center",
            Component = "/workflow/center/index",
            Title = "审批中心",
            Icon = "lucide:list-checks",
            Order = 1,
            PermissionCode = "workflow:center:query",
            IsEnabled = true
        }, cancellationToken);

        await EnsurePermissionAsync(
            MiniAdminSeedIds.WorkflowCenterQueryPermissionId,
            MiniAdminSeedIds.WorkflowCenterMenuId,
            "WorkflowCenterQueryPermission",
            "workflow:center:query",
            "workflow:center:query",
            1,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.WorkflowDefinitionManagePermissionId,
            MiniAdminSeedIds.WorkflowCenterMenuId,
            "WorkflowDefinitionManagePermission",
            "workflow:definition:manage",
            "workflow:definition:manage",
            2,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.WorkflowInstanceStartPermissionId,
            MiniAdminSeedIds.WorkflowCenterMenuId,
            "WorkflowInstanceStartPermission",
            "workflow:instance:start",
            "workflow:instance:start",
            3,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.WorkflowTaskApprovePermissionId,
            MiniAdminSeedIds.WorkflowCenterMenuId,
            "WorkflowTaskApprovePermission",
            "workflow:task:approve",
            "workflow:task:approve",
            4,
            cancellationToken);

        await EnsureRoleMenuAsync(MiniAdminSeedIds.WorkflowManagementMenuId, cancellationToken);
        await EnsureRoleMenuAsync(MiniAdminSeedIds.WorkflowCenterMenuId, cancellationToken);
        await EnsureRoleMenuAsync(MiniAdminSeedIds.WorkflowCenterQueryPermissionId, cancellationToken);
        await EnsureRoleMenuAsync(MiniAdminSeedIds.WorkflowDefinitionManagePermissionId, cancellationToken);
        await EnsureRoleMenuAsync(MiniAdminSeedIds.WorkflowInstanceStartPermissionId, cancellationToken);
        await EnsureRoleMenuAsync(MiniAdminSeedIds.WorkflowTaskApprovePermissionId, cancellationToken);
    }

    private async Task SeedGeneratedCrudModulesAsync(CancellationToken cancellationToken)
    {
        var seedDefinitions = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(type =>
                !type.IsAbstract &&
                typeof(IGeneratedCrudSeedDefinition).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .Select(type => (IGeneratedCrudSeedDefinition)Activator.CreateInstance(type)!)
            .ToArray();

        foreach (var seedDefinition in seedDefinitions)
        {
            await seedDefinition.SeedAsync(dbContext, cancellationToken);
        }
    }

    private bool ShouldUseMigrations()
    {
        if (!dbContext.Database.IsRelational())
        {
            return false;
        }

        if (databaseOptions.Value.SchemaManagement.Equals("EnsureCreated", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return databaseOptions.Value.SchemaManagement.Equals("Auto", StringComparison.OrdinalIgnoreCase) ||
               databaseOptions.Value.SchemaManagement.Equals("Migrations", StringComparison.OrdinalIgnoreCase);
    }

    private async Task ApplySeedVersionAsync(
        string version,
        string name,
        Func<CancellationToken, Task> seedAction,
        CancellationToken cancellationToken)
    {
        if (await dbContext.DataSeedVersions.AnyAsync(x => x.Version == version, cancellationToken))
        {
            return;
        }

        await seedAction(cancellationToken);
        dbContext.DataSeedVersions.Add(new DataSeedVersion
        {
            Id = Guid.NewGuid(),
            Version = version,
            Name = name,
            AppliedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedTenantPackageManagementAsync(CancellationToken cancellationToken)
    {
        await EnsureSystemChildMenuAsync(
            MiniAdminSeedIds.TenantPackageMenuId,
            "TenantPackage",
            "/system/tenant-package",
            "/system/tenant-package/index",
            "租户套餐",
            "lucide:box",
            1,
            "system:tenant-package:query",
            cancellationToken);

        await EnsureAdminPermissionIfParentAssignedAsync(
            MiniAdminSeedIds.SystemMenuId,
            MiniAdminSeedIds.TenantPackageMenuId,
            cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await userAuthorizationCache.RemoveUserAsync(
            MiniAdminSeedIds.AdminUserId,
            "admin",
            cancellationToken);
    }

    private async Task SeedLegacySystemTenantManagementCleanupAsync(CancellationToken cancellationToken)
    {
        var legacyMenus = await dbContext.Menus
            .Where(menu =>
                menu.Id == MiniAdminSeedIds.TenantManagementMenuId ||
                menu.ParentId == MiniAdminSeedIds.TenantManagementMenuId)
            .ToListAsync(cancellationToken);
        if (legacyMenus.Count == 0)
        {
            return;
        }

        foreach (var menu in legacyMenus)
        {
            menu.IsEnabled = false;
            menu.IsVisible = false;
        }

        var legacyMenuIds = legacyMenus.Select(menu => menu.Id).ToList();
        var legacyRoleMenus = await dbContext.RoleMenus
            .Where(roleMenu => legacyMenuIds.Contains(roleMenu.MenuId))
            .ToListAsync(cancellationToken);
        dbContext.RoleMenus.RemoveRange(legacyRoleMenus);

        await dbContext.SaveChangesAsync(cancellationToken);
        await userAuthorizationCache.RemoveUserAsync(
            MiniAdminSeedIds.AdminUserId,
            "admin",
            cancellationToken);
    }

    private async Task SeedTenantDashboardAnalyticsAccessAsync(CancellationToken cancellationToken)
    {
        var packageChanged = false;
        var packages = await dbContext.TenantPackages.ToListAsync(cancellationToken);
        foreach (var package in packages)
        {
            var menuIds = EfTenantPackageRepository.ParseMenuIds(package.MenuIds);
            if (!menuIds.Contains(MiniAdminSeedIds.DashboardMenuId) ||
                menuIds.Contains(MiniAdminSeedIds.AnalyticsMenuId))
            {
                continue;
            }

            menuIds.Add(MiniAdminSeedIds.AnalyticsMenuId);
            package.MenuIds = JsonSerializer.Serialize(menuIds.Order().ToArray());
            packageChanged = true;
        }

        var dashboardRoleIds = await dbContext.RoleMenus
            .AsNoTracking()
            .Where(x => x.MenuId == MiniAdminSeedIds.DashboardMenuId)
            .Select(x => x.RoleId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var analyticsRoleIds = await dbContext.RoleMenus
            .AsNoTracking()
            .Where(x => x.MenuId == MiniAdminSeedIds.AnalyticsMenuId)
            .Select(x => x.RoleId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var missingAnalyticsRoleIds = dashboardRoleIds
            .Except(analyticsRoleIds)
            .ToArray();
        foreach (var roleId in missingAnalyticsRoleIds)
        {
            dbContext.RoleMenus.Add(new RoleMenu
            {
                RoleId = roleId,
                MenuId = MiniAdminSeedIds.AnalyticsMenuId
            });
        }

        if (packageChanged || missingAnalyticsRoleIds.Length > 0)
        {
            var tenantUsers = await dbContext.Users
                .Where(x => x.TenantId.HasValue)
                .Select(x => new { x.Id, x.UserName })
                .ToArrayAsync(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            foreach (var user in tenantUsers)
            {
                await userAuthorizationCache.RemoveUserAsync(user.Id, user.UserName, cancellationToken);
            }
        }
    }

    private async Task SeedPositionImportExportAsync(CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(
            MiniAdminSeedIds.PositionImportPermissionId,
            MiniAdminSeedIds.PositionManagementMenuId,
            "PositionImportPermission",
            "system:position:import",
            "system:position:import",
            5,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.PositionExportPermissionId,
            MiniAdminSeedIds.PositionManagementMenuId,
            "PositionExportPermission",
            "system:position:export",
            "system:position:export",
            6,
            cancellationToken);
        await EnsureAdminPermissionIfParentAssignedAsync(
            MiniAdminSeedIds.PositionManagementMenuId,
            MiniAdminSeedIds.PositionImportPermissionId,
            cancellationToken);
        await EnsureAdminPermissionIfParentAssignedAsync(
            MiniAdminSeedIds.PositionManagementMenuId,
            MiniAdminSeedIds.PositionExportPermissionId,
            cancellationToken);
    }

    private async Task AdoptExistingMySqlDatabaseAsync(CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider())
        {
            return;
        }

        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var hasApplicationTables = await ExistsAsync(
                connection,
                """
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME IN ('mini_users', 'mini_roles', 'mini_menus')
                """,
                cancellationToken);

            if (!hasApplicationTables)
            {
                return;
            }

            await EnsureMigrationsHistoryTableAsync(connection, cancellationToken);

            var hasInitialCreateHistory = await ExistsAsync(
                connection,
                $"""
                SELECT COUNT(*)
                FROM `__EFMigrationsHistory`
                WHERE `MigrationId` = '{InitialCreateMigrationId}'
                """,
                cancellationToken);

            if (hasInitialCreateHistory)
            {
                return;
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }

        await EnsureLegacyMySqlSchemaAsync(cancellationToken);

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await EnsureMigrationsHistoryTableAsync(connection, cancellationToken);
            await ExecuteNonQueryAsync(
                connection,
                $"""
                INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
                SELECT '{InitialCreateMigrationId}', '9.0.0'
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM `__EFMigrationsHistory`
                    WHERE `MigrationId` = '{InitialCreateMigrationId}'
                )
                """,
                cancellationToken);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task EnsureMigrationsHistoryTableAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await ExecuteNonQueryAsync(
            connection,
            """
            CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
              `MigrationId` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
              `ProductVersion` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              CONSTRAINT `PK___EFMigrationsHistory` PRIMARY KEY (`MigrationId`)
            ) CHARACTER SET=utf8mb4;
            """,
            cancellationToken);
    }

    private async Task EnsureDepartmentTableAsync(CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider())
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `mini_departments` (
              `Id` char(36) COLLATE ascii_general_ci NOT NULL,
              `TenantId` char(36) COLLATE ascii_general_ci NULL,
              `ParentId` char(36) COLLATE ascii_general_ci NULL,
              `Code` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              `Name` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              `Leader` varchar(64) CHARACTER SET utf8mb4 NULL,
              `Phone` varchar(32) CHARACTER SET utf8mb4 NULL,
              `Order` int NOT NULL,
              `IsEnabled` tinyint(1) NOT NULL,
              PRIMARY KEY (`Id`),
              UNIQUE KEY `IX_mini_departments_TenantId_Code` (`TenantId`, `Code`),
              KEY `IX_mini_departments_ParentId` (`ParentId`),
              CONSTRAINT `FK_mini_departments_mini_departments_ParentId`
                FOREIGN KEY (`ParentId`) REFERENCES `mini_departments` (`Id`) ON DELETE RESTRICT
            ) CHARACTER SET=utf8mb4;
            """,
            cancellationToken);
    }

    private async Task EnsureDictionaryTablesAsync(CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider())
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `mini_dictionary_types` (
              `Id` char(36) COLLATE ascii_general_ci NOT NULL,
              `Code` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              `Name` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              `Order` int NOT NULL,
              `IsEnabled` tinyint(1) NOT NULL,
              PRIMARY KEY (`Id`),
              UNIQUE KEY `IX_mini_dictionary_types_Code` (`Code`)
            ) CHARACTER SET=utf8mb4;
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `mini_dictionary_items` (
              `Id` char(36) COLLATE ascii_general_ci NOT NULL,
              `TypeId` char(36) COLLATE ascii_general_ci NOT NULL,
              `Label` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              `Value` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              `Color` varchar(32) CHARACTER SET utf8mb4 NULL,
              `Order` int NOT NULL,
              `IsEnabled` tinyint(1) NOT NULL,
              PRIMARY KEY (`Id`),
              UNIQUE KEY `IX_mini_dictionary_items_TypeId_Value` (`TypeId`, `Value`),
              CONSTRAINT `FK_mini_dictionary_items_mini_dictionary_types_TypeId`
                FOREIGN KEY (`TypeId`) REFERENCES `mini_dictionary_types` (`Id`) ON DELETE RESTRICT
            ) CHARACTER SET=utf8mb4;
            """,
            cancellationToken);
    }

    private async Task EnsurePositionTableAsync(CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider())
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `mini_positions` (
              `Id` char(36) COLLATE ascii_general_ci NOT NULL,
              `TenantId` char(36) COLLATE ascii_general_ci NULL,
              `Code` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              `Name` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              `Order` int NOT NULL,
              `Remark` varchar(512) CHARACTER SET utf8mb4 NULL,
              `IsEnabled` tinyint(1) NOT NULL,
              PRIMARY KEY (`Id`),
              UNIQUE KEY `IX_mini_positions_TenantId_Code` (`TenantId`, `Code`)
            ) CHARACTER SET=utf8mb4;
            """,
            cancellationToken);
    }

    private async Task EnsureRoleDataScopeColumnAsync(CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider())
        {
            return;
        }

        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            if (!await ExistsAsync(
                connection,
                """
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'mini_roles'
                  AND COLUMN_NAME = 'DataScope'
                """,
                cancellationToken))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    "ALTER TABLE `mini_roles` ADD COLUMN `DataScope` varchar(32) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'all'",
                    cancellationToken);
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task EnsureRoleCustomDepartmentIdsColumnAsync(CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider())
        {
            return;
        }

        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            if (!await ExistsAsync(
                connection,
                """
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'mini_roles'
                  AND COLUMN_NAME = 'CustomDepartmentIds'
                """,
                cancellationToken))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    "ALTER TABLE `mini_roles` ADD COLUMN `CustomDepartmentIds` longtext NULL",
                    cancellationToken);
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task EnsureSystemParameterTableAsync(CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider())
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `mini_system_parameters` (
              `Id` char(36) COLLATE ascii_general_ci NOT NULL,
              `Key` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              `Name` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              `Value` varchar(1024) CHARACTER SET utf8mb4 NOT NULL,
              `Group` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              `Remark` varchar(512) CHARACTER SET utf8mb4 NULL,
              `Order` int NOT NULL,
              `IsEnabled` tinyint(1) NOT NULL,
              PRIMARY KEY (`Id`),
              UNIQUE KEY `IX_mini_system_parameters_Key` (`Key`)
            ) CHARACTER SET=utf8mb4;
            """,
            cancellationToken);
    }

    private async Task EnsureNoticeTableAsync(CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider())
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `mini_notices` (
              `Id` char(36) COLLATE ascii_general_ci NOT NULL,
              `Title` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
              `Type` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              `Content` varchar(4000) CHARACTER SET utf8mb4 NOT NULL,
              `IsPublished` tinyint(1) NOT NULL,
              `PublishedAt` datetime(6) NULL,
              `CreatedAt` datetime(6) NOT NULL,
              PRIMARY KEY (`Id`)
            ) CHARACTER SET=utf8mb4;
            """,
            cancellationToken);
    }

    private async Task EnsureAuditLogTableAsync(CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider())
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `mini_audit_logs` (
              `Id` char(36) COLLATE ascii_general_ci NOT NULL,
              `UserId` varchar(64) CHARACTER SET utf8mb4 NULL,
              `UserName` varchar(64) CHARACTER SET utf8mb4 NULL,
              `Method` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
              `Path` varchar(256) CHARACTER SET utf8mb4 NOT NULL,
              `QueryString` varchar(1024) CHARACTER SET utf8mb4 NULL,
              `Module` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              `Action` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              `ResourceId` varchar(64) CHARACTER SET utf8mb4 NULL,
              `StatusCode` int NOT NULL,
              `IsSuccess` tinyint(1) NOT NULL,
              `ElapsedMilliseconds` bigint NOT NULL,
              `IpAddress` varchar(64) CHARACTER SET utf8mb4 NULL,
              `UserAgent` varchar(512) CHARACTER SET utf8mb4 NULL,
              `RequestBody` varchar(4000) CHARACTER SET utf8mb4 NOT NULL,
              `ErrorMessage` varchar(1024) CHARACTER SET utf8mb4 NULL,
              `CreatedAt` datetime(6) NOT NULL,
              PRIMARY KEY (`Id`),
              KEY `IX_mini_audit_logs_CreatedAt` (`CreatedAt`),
              KEY `IX_mini_audit_logs_UserName` (`UserName`)
            ) CHARACTER SET=utf8mb4;
            """,
            cancellationToken);
    }

    private async Task EnsureAuditEntityChangeTableAsync(CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider())
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `mini_audit_entity_changes` (
              `Id` char(36) COLLATE ascii_general_ci NOT NULL,
              `AuditLogId` char(36) COLLATE ascii_general_ci NOT NULL,
              `EntityName` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              `EntityId` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              `OperationType` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
              `BeforeJson` longtext CHARACTER SET utf8mb4 NULL,
              `AfterJson` longtext CHARACTER SET utf8mb4 NULL,
              `DiffJson` longtext CHARACTER SET utf8mb4 NOT NULL,
              `CreatedAt` datetime(6) NOT NULL,
              PRIMARY KEY (`Id`),
              KEY `IX_mini_audit_entity_changes_AuditLogId` (`AuditLogId`),
              KEY `IX_mini_audit_entity_changes_EntityName_EntityId` (`EntityName`, `EntityId`),
              CONSTRAINT `FK_mini_audit_entity_changes_mini_audit_logs_AuditLogId`
                FOREIGN KEY (`AuditLogId`) REFERENCES `mini_audit_logs` (`Id`) ON DELETE CASCADE
            ) CHARACTER SET=utf8mb4;
            """,
            cancellationToken);
    }

    private async Task EnsureLoginLogTableAsync(CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider())
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `mini_login_logs` (
              `Id` char(36) COLLATE ascii_general_ci NOT NULL,
              `UserId` char(36) COLLATE ascii_general_ci NULL,
              `UserName` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              `RealName` varchar(64) CHARACTER SET utf8mb4 NULL,
              `IpAddress` varchar(64) CHARACTER SET utf8mb4 NULL,
              `UserAgent` varchar(512) CHARACTER SET utf8mb4 NULL,
              `IsSuccess` tinyint(1) NOT NULL,
              `Message` varchar(256) CHARACTER SET utf8mb4 NOT NULL,
              `CreatedAt` datetime(6) NOT NULL,
              PRIMARY KEY (`Id`),
              KEY `IX_mini_login_logs_CreatedAt` (`CreatedAt`)
            ) CHARACTER SET=utf8mb4;
            """,
            cancellationToken);
    }

    private async Task EnsureOnlineUserTableAsync(CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider())
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `mini_online_users` (
              `SessionId` char(36) COLLATE ascii_general_ci NOT NULL,
              `UserId` char(36) COLLATE ascii_general_ci NOT NULL,
              `UserName` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              `RealName` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              `IpAddress` varchar(64) CHARACTER SET utf8mb4 NULL,
              `UserAgent` varchar(512) CHARACTER SET utf8mb4 NULL,
              `DeviceName` varchar(64) CHARACTER SET utf8mb4 NULL,
              `BrowserName` varchar(64) CHARACTER SET utf8mb4 NULL,
              `LoginAt` datetime(6) NOT NULL,
              `LastActiveAt` datetime(6) NOT NULL,
              `IsOnline` tinyint(1) NOT NULL,
              PRIMARY KEY (`SessionId`),
              KEY `IX_mini_online_users_UserId` (`UserId`),
              KEY `IX_mini_online_users_UserName` (`UserName`)
            ) CHARACTER SET=utf8mb4;
            """,
            cancellationToken);

        await EnsureOnlineUserSessionSchemaAsync(cancellationToken);
    }

    private async Task EnsureOnlineUserSessionSchemaAsync(CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            if (!await ExistsAsync(
                    connection,
                    """
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'mini_online_users'
                      AND COLUMN_NAME = 'SessionId'
                    """,
                    cancellationToken))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    "ALTER TABLE `mini_online_users` ADD COLUMN `SessionId` char(36) COLLATE ascii_general_ci NULL",
                    cancellationToken);
                await ExecuteNonQueryAsync(
                    connection,
                    "UPDATE `mini_online_users` SET `SessionId` = `UserId` WHERE `SessionId` IS NULL",
                    cancellationToken);
                await ExecuteNonQueryAsync(
                    connection,
                    "ALTER TABLE `mini_online_users` MODIFY COLUMN `SessionId` char(36) COLLATE ascii_general_ci NOT NULL",
                    cancellationToken);
            }

            if (!await ExistsAsync(
                    connection,
                    """
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'mini_online_users'
                      AND COLUMN_NAME = 'DeviceName'
                    """,
                    cancellationToken))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    "ALTER TABLE `mini_online_users` ADD COLUMN `DeviceName` varchar(64) CHARACTER SET utf8mb4 NULL",
                    cancellationToken);
            }

            if (!await ExistsAsync(
                    connection,
                    """
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'mini_online_users'
                      AND COLUMN_NAME = 'BrowserName'
                    """,
                    cancellationToken))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    "ALTER TABLE `mini_online_users` ADD COLUMN `BrowserName` varchar(64) CHARACTER SET utf8mb4 NULL",
                    cancellationToken);
            }

            if (!await ExistsAsync(
                    connection,
                    """
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'mini_online_users'
                      AND CONSTRAINT_NAME = 'PRIMARY'
                      AND COLUMN_NAME = 'SessionId'
                    """,
                    cancellationToken))
            {
                await ExecuteNonQueryAsync(connection, "ALTER TABLE `mini_online_users` DROP PRIMARY KEY", cancellationToken);
                await ExecuteNonQueryAsync(connection, "ALTER TABLE `mini_online_users` ADD PRIMARY KEY (`SessionId`)", cancellationToken);
            }

            if (!await ExistsAsync(
                    connection,
                    """
                    SELECT COUNT(*)
                    FROM INFORMATION_SCHEMA.STATISTICS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'mini_online_users'
                      AND INDEX_NAME = 'IX_mini_online_users_UserId'
                    """,
                    cancellationToken))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    "CREATE INDEX `IX_mini_online_users_UserId` ON `mini_online_users` (`UserId`)",
                    cancellationToken);
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task EnsureSecurityEventTableAsync(CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider())
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `mini_security_events` (
              `Id` char(36) COLLATE ascii_general_ci NOT NULL,
              `EventType` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              `Level` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              `UserId` char(36) COLLATE ascii_general_ci NULL,
              `UserName` varchar(64) CHARACTER SET utf8mb4 NULL,
              `IpAddress` varchar(64) CHARACTER SET utf8mb4 NULL,
              `UserAgent` varchar(512) CHARACTER SET utf8mb4 NULL,
              `Title` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
              `Description` varchar(1024) CHARACTER SET utf8mb4 NOT NULL,
              `RelatedEntityType` varchar(64) CHARACTER SET utf8mb4 NULL,
              `RelatedEntityId` varchar(128) CHARACTER SET utf8mb4 NULL,
              `CreatedAt` datetime(6) NOT NULL,
              PRIMARY KEY (`Id`),
              KEY `IX_mini_security_events_CreatedAt` (`CreatedAt`),
              KEY `IX_mini_security_events_EventType` (`EventType`),
              KEY `IX_mini_security_events_UserName` (`UserName`)
            ) CHARACTER SET=utf8mb4;
            """,
            cancellationToken);
    }

    private async Task EnsureCodeGenerationHistoryTableAsync(CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider())
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `mini_code_generation_histories` (
              `Id` char(36) COLLATE ascii_general_ci NOT NULL,
              `TableName` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              `ModuleName` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              `BusinessName` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              `PermissionPrefix` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              `TenantMode` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              `RequestJson` longtext CHARACTER SET utf8mb4 NOT NULL,
              `FilesJson` longtext CHARACTER SET utf8mb4 NOT NULL,
              `Status` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              `ErrorMessage` varchar(1024) CHARACTER SET utf8mb4 NULL,
              `OperatorUserId` char(36) COLLATE ascii_general_ci NULL,
              `OperatorUserName` varchar(64) CHARACTER SET utf8mb4 NULL,
              `CreatedAt` datetime(6) NOT NULL,
              PRIMARY KEY (`Id`),
              KEY `IX_mini_code_generation_histories_CreatedAt` (`CreatedAt`),
              KEY `IX_mini_code_generation_histories_ModuleName` (`ModuleName`)
            ) CHARACTER SET=utf8mb4;
            """,
            cancellationToken);
    }

    private async Task EnsureWorkflowTablesAsync(CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider())
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `mini_workflow_definitions` (
              `Id` char(36) COLLATE ascii_general_ci NOT NULL,
              `TenantId` char(36) COLLATE ascii_general_ci NULL,
              `Code` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              `Name` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              `FormName` varchar(128) CHARACTER SET utf8mb4 NULL,
              `Description` varchar(512) CHARACTER SET utf8mb4 NULL,
              `DesignerJson` longtext CHARACTER SET utf8mb4 NOT NULL,
              `FormSchemaJson` longtext CHARACTER SET utf8mb4 NOT NULL,
              `IsEnabled` tinyint(1) NOT NULL,
              `Version` int NOT NULL DEFAULT 1,
              `PublishStatus` varchar(32) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Published',
              `PublishedAt` datetime(6) NULL,
              `CreatedAt` datetime(6) NOT NULL,
              `UpdatedAt` datetime(6) NOT NULL,
              PRIMARY KEY (`Id`),
              UNIQUE KEY `IX_mini_workflow_definitions_TenantId_Code_Version` (`TenantId`, `Code`, `Version`),
              KEY `IX_mini_workflow_definitions_TenantId` (`TenantId`)
            ) CHARACTER SET=utf8mb4;
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `mini_workflow_nodes` (
              `Id` char(36) COLLATE ascii_general_ci NOT NULL,
              `DefinitionId` char(36) COLLATE ascii_general_ci NOT NULL,
              `Name` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              `DesignerNodeId` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              `NodeType` varchar(32) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'approve',
              `ApprovalMode` varchar(32) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Any',
              `SlaMinutes` int NULL,
              `ApproverType` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              `ApproverUserId` char(36) COLLATE ascii_general_ci NULL,
              `ApproverRoleId` char(36) COLLATE ascii_general_ci NULL,
              `Order` int NOT NULL,
              `IsEnabled` tinyint(1) NOT NULL,
              PRIMARY KEY (`Id`),
              KEY `IX_mini_workflow_nodes_DefinitionId` (`DefinitionId`)
            ) CHARACTER SET=utf8mb4;
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `mini_workflow_instances` (
              `Id` char(36) COLLATE ascii_general_ci NOT NULL,
              `TenantId` char(36) COLLATE ascii_general_ci NULL,
              `DefinitionId` char(36) COLLATE ascii_general_ci NOT NULL,
              `DefinitionCode` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              `DefinitionName` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              `DefinitionVersion` int NOT NULL DEFAULT 1,
              `DefinitionSnapshotJson` longtext CHARACTER SET utf8mb4 NOT NULL,
              `Title` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
              `BusinessKey` varchar(128) CHARACTER SET utf8mb4 NULL,
              `FormDataJson` longtext CHARACTER SET utf8mb4 NOT NULL,
              `Status` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              `CurrentNodeId` char(36) COLLATE ascii_general_ci NULL,
              `CurrentNodeName` varchar(128) CHARACTER SET utf8mb4 NULL,
              `InitiatorUserId` char(36) COLLATE ascii_general_ci NOT NULL,
              `InitiatorUserName` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              `StartedAt` datetime(6) NOT NULL,
              `CompletedAt` datetime(6) NULL,
              PRIMARY KEY (`Id`),
              KEY `IX_mini_workflow_instances_TenantId` (`TenantId`),
              KEY `IX_mini_workflow_instances_Status` (`Status`),
              KEY `IX_mini_workflow_instances_InitiatorUserId` (`InitiatorUserId`)
            ) CHARACTER SET=utf8mb4;
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `mini_workflow_tasks` (
              `Id` char(36) COLLATE ascii_general_ci NOT NULL,
              `InstanceId` char(36) COLLATE ascii_general_ci NOT NULL,
              `NodeId` char(36) COLLATE ascii_general_ci NOT NULL,
              `NodeName` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              `ApproverUserId` char(36) COLLATE ascii_general_ci NOT NULL,
              `ApproverUserName` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              `Status` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              `Comment` varchar(512) CHARACTER SET utf8mb4 NULL,
              `CreatedAt` datetime(6) NOT NULL,
              `DueAt` datetime(6) NULL,
              `LastAutoRemindedAt` datetime(6) NULL,
              `CompletedAt` datetime(6) NULL,
              PRIMARY KEY (`Id`),
              KEY `IX_mini_workflow_tasks_InstanceId` (`InstanceId`),
              KEY `IX_mini_workflow_tasks_ApproverUserId_Status` (`ApproverUserId`, `Status`)
            ) CHARACTER SET=utf8mb4;
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `mini_workflow_action_logs` (
              `Id` char(36) COLLATE ascii_general_ci NOT NULL,
              `InstanceId` char(36) COLLATE ascii_general_ci NOT NULL,
              `NodeId` char(36) COLLATE ascii_general_ci NULL,
              `NodeName` varchar(128) CHARACTER SET utf8mb4 NULL,
              `Action` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              `OperatorUserId` char(36) COLLATE ascii_general_ci NOT NULL,
              `OperatorUserName` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              `Comment` varchar(512) CHARACTER SET utf8mb4 NULL,
              `CreatedAt` datetime(6) NOT NULL,
              PRIMARY KEY (`Id`),
              KEY `IX_mini_workflow_action_logs_InstanceId` (`InstanceId`)
            ) CHARACTER SET=utf8mb4;
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `mini_workflow_cc_records` (
              `Id` char(36) COLLATE ascii_general_ci NOT NULL,
              `InstanceId` char(36) COLLATE ascii_general_ci NOT NULL,
              `NodeId` char(36) COLLATE ascii_general_ci NULL,
              `NodeName` varchar(128) CHARACTER SET utf8mb4 NULL,
              `RecipientUserId` char(36) COLLATE ascii_general_ci NOT NULL,
              `RecipientUserName` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              `SenderUserId` char(36) COLLATE ascii_general_ci NULL,
              `SenderUserName` varchar(64) CHARACTER SET utf8mb4 NULL,
              `CreatedAt` datetime(6) NOT NULL,
              `ReadAt` datetime(6) NULL,
              PRIMARY KEY (`Id`),
              UNIQUE KEY `IX_mini_workflow_cc_records_Instance_Node_Recipient` (`InstanceId`, `NodeId`, `RecipientUserId`),
              KEY `IX_mini_workflow_cc_records_InstanceId` (`InstanceId`),
              KEY `IX_mini_workflow_cc_records_RecipientUserId_ReadAt` (`RecipientUserId`, `ReadAt`)
            ) CHARACTER SET=utf8mb4;
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT IGNORE INTO `mini_workflow_cc_records` (
              `Id`,
              `InstanceId`,
              `NodeId`,
              `NodeName`,
              `RecipientUserId`,
              `RecipientUserName`,
              `SenderUserId`,
              `SenderUserName`,
              `CreatedAt`,
              `ReadAt`
            )
            SELECT
              l.`Id`,
              l.`InstanceId`,
              l.`NodeId`,
              l.`NodeName`,
              l.`OperatorUserId`,
              l.`OperatorUserName`,
              NULL,
              NULL,
              l.`CreatedAt`,
              NULL
            FROM `mini_workflow_action_logs` l
            WHERE l.`Action` = 'Cc';
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `mini_workflow_business_bindings` (
              `Id` char(36) COLLATE ascii_general_ci NOT NULL,
              `TenantId` char(36) COLLATE ascii_general_ci NULL,
              `BusinessType` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              `BusinessName` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              `DefinitionId` char(36) COLLATE ascii_general_ci NOT NULL,
              `IsEnabled` tinyint(1) NOT NULL,
              `Remark` varchar(512) CHARACTER SET utf8mb4 NULL,
              `CreatedAt` datetime(6) NOT NULL,
              `UpdatedAt` datetime(6) NOT NULL,
              PRIMARY KEY (`Id`),
              UNIQUE KEY `IX_mini_workflow_business_bindings_TenantId_BusinessType` (`TenantId`, `BusinessType`),
              KEY `IX_mini_workflow_business_bindings_DefinitionId` (`DefinitionId`)
            ) CHARACTER SET=utf8mb4;
            """,
            cancellationToken);

        await EnsureWorkflowDesignerColumnsAsync(cancellationToken);
        await EnsureWorkflowDefinitionVersionColumnsAsync(cancellationToken);
        await EnsureWorkflowInstanceVersionColumnsAsync(cancellationToken);
        await EnsureWorkflowCollaborationTablesAsync(cancellationToken);
    }

    private async Task EnsureWorkflowDesignerColumnsAsync(CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            if (!await ExistsAsync(
                connection,
                """
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'mini_workflow_definitions'
                  AND COLUMN_NAME = 'DesignerJson'
                """,
                cancellationToken))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    "ALTER TABLE `mini_workflow_definitions` ADD COLUMN `DesignerJson` longtext CHARACTER SET utf8mb4 NOT NULL",
                    cancellationToken);
            }

            if (!await ExistsAsync(
                connection,
                """
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'mini_workflow_nodes'
                  AND COLUMN_NAME = 'DesignerNodeId'
                """,
                cancellationToken))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    "ALTER TABLE `mini_workflow_nodes` ADD COLUMN `DesignerNodeId` varchar(128) CHARACTER SET utf8mb4 NOT NULL DEFAULT ''",
                    cancellationToken);
            }

            if (!await ExistsAsync(
                connection,
                """
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'mini_workflow_nodes'
                  AND COLUMN_NAME = 'NodeType'
                """,
                cancellationToken))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    "ALTER TABLE `mini_workflow_nodes` ADD COLUMN `NodeType` varchar(32) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'approve'",
                    cancellationToken);
            }

            if (!await ExistsAsync(
                connection,
                """
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'mini_workflow_definitions'
                  AND COLUMN_NAME = 'FormSchemaJson'
                """,
                cancellationToken))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    "ALTER TABLE `mini_workflow_definitions` ADD COLUMN `FormSchemaJson` longtext CHARACTER SET utf8mb4 NULL",
                    cancellationToken);
                await ExecuteNonQueryAsync(
                    connection,
                    "UPDATE `mini_workflow_definitions` SET `FormSchemaJson` = '[]' WHERE `FormSchemaJson` IS NULL OR `FormSchemaJson` = ''",
                    cancellationToken);
                await ExecuteNonQueryAsync(
                    connection,
                    "ALTER TABLE `mini_workflow_definitions` MODIFY COLUMN `FormSchemaJson` longtext CHARACTER SET utf8mb4 NOT NULL",
                    cancellationToken);
            }

            if (!await ExistsAsync(
                connection,
                """
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'mini_workflow_nodes'
                  AND COLUMN_NAME = 'ApprovalMode'
                """,
                cancellationToken))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    "ALTER TABLE `mini_workflow_nodes` ADD COLUMN `ApprovalMode` varchar(32) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Any'",
                    cancellationToken);
            }

            if (!await ExistsAsync(
                connection,
                """
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'mini_workflow_nodes'
                  AND COLUMN_NAME = 'SlaMinutes'
                """,
                cancellationToken))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    "ALTER TABLE `mini_workflow_nodes` ADD COLUMN `SlaMinutes` int NULL",
                    cancellationToken);
            }

            if (!await ExistsAsync(
                connection,
                """
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'mini_workflow_tasks'
                  AND COLUMN_NAME = 'DueAt'
                """,
                cancellationToken))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    "ALTER TABLE `mini_workflow_tasks` ADD COLUMN `DueAt` datetime(6) NULL",
                    cancellationToken);
            }

            if (!await ExistsAsync(
                connection,
                """
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'mini_workflow_tasks'
                  AND COLUMN_NAME = 'LastAutoRemindedAt'
                """,
                cancellationToken))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    "ALTER TABLE `mini_workflow_tasks` ADD COLUMN `LastAutoRemindedAt` datetime(6) NULL",
                    cancellationToken);
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task EnsureWorkflowDefinitionVersionColumnsAsync(CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            if (!await ExistsAsync(
                connection,
                """
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'mini_workflow_definitions'
                  AND COLUMN_NAME = 'Version'
                """,
                cancellationToken))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    "ALTER TABLE `mini_workflow_definitions` ADD COLUMN `Version` int NOT NULL DEFAULT 1",
                    cancellationToken);
            }

            if (!await ExistsAsync(
                connection,
                """
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'mini_workflow_definitions'
                  AND COLUMN_NAME = 'PublishStatus'
                """,
                cancellationToken))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    "ALTER TABLE `mini_workflow_definitions` ADD COLUMN `PublishStatus` varchar(32) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Published'",
                    cancellationToken);
            }

            if (!await ExistsAsync(
                connection,
                """
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'mini_workflow_definitions'
                  AND COLUMN_NAME = 'PublishedAt'
                """,
                cancellationToken))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    "ALTER TABLE `mini_workflow_definitions` ADD COLUMN `PublishedAt` datetime(6) NULL",
                    cancellationToken);
            }

            if (await ExistsAsync(
                connection,
                """
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'mini_workflow_definitions'
                  AND INDEX_NAME = 'IX_mini_workflow_definitions_TenantId_Code'
                """,
                cancellationToken))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    "ALTER TABLE `mini_workflow_definitions` DROP INDEX `IX_mini_workflow_definitions_TenantId_Code`",
                    cancellationToken);
            }

            if (!await ExistsAsync(
                connection,
                """
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'mini_workflow_definitions'
                  AND INDEX_NAME = 'IX_mini_workflow_definitions_TenantId_Code_Version'
                """,
                cancellationToken))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    "CREATE UNIQUE INDEX `IX_mini_workflow_definitions_TenantId_Code_Version` ON `mini_workflow_definitions` (`TenantId`, `Code`, `Version`)",
                    cancellationToken);
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task EnsureWorkflowInstanceVersionColumnsAsync(CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            if (!await ExistsAsync(
                connection,
                """
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'mini_workflow_instances'
                  AND COLUMN_NAME = 'DefinitionCode'
                """,
                cancellationToken))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    "ALTER TABLE `mini_workflow_instances` ADD COLUMN `DefinitionCode` varchar(64) CHARACTER SET utf8mb4 NULL",
                    cancellationToken);
            }

            if (!await ExistsAsync(
                connection,
                """
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'mini_workflow_instances'
                  AND COLUMN_NAME = 'DefinitionVersion'
                """,
                cancellationToken))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    "ALTER TABLE `mini_workflow_instances` ADD COLUMN `DefinitionVersion` int NULL",
                    cancellationToken);
            }

            if (!await ExistsAsync(
                connection,
                """
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'mini_workflow_instances'
                  AND COLUMN_NAME = 'DefinitionSnapshotJson'
                """,
                cancellationToken))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    "ALTER TABLE `mini_workflow_instances` ADD COLUMN `DefinitionSnapshotJson` longtext CHARACTER SET utf8mb4 NULL",
                    cancellationToken);
            }

            await ExecuteNonQueryAsync(
                connection,
                """
                UPDATE `mini_workflow_instances` wi
                LEFT JOIN `mini_workflow_definitions` wd ON wd.`Id` = wi.`DefinitionId`
                SET wi.`DefinitionCode` = COALESCE(NULLIF(wi.`DefinitionCode`, ''), wd.`Code`, ''),
                    wi.`DefinitionVersion` = CASE
                      WHEN wi.`DefinitionVersion` IS NULL OR wi.`DefinitionVersion` <= 0 THEN COALESCE(wd.`Version`, 1)
                      ELSE wi.`DefinitionVersion`
                    END
                """,
                cancellationToken);

            await ExecuteNonQueryAsync(
                connection,
                """
                UPDATE `mini_workflow_instances` wi
                LEFT JOIN `mini_workflow_definitions` wd ON wd.`Id` = wi.`DefinitionId`
                SET wi.`DefinitionSnapshotJson` = JSON_OBJECT(
                    'id', COALESCE(CAST(wd.`Id` AS CHAR), CAST(wi.`DefinitionId` AS CHAR)),
                    'code', COALESCE(wd.`Code`, wi.`DefinitionCode`, ''),
                    'name', COALESCE(wd.`Name`, wi.`DefinitionName`, ''),
                    'formName', wd.`FormName`,
                    'version', COALESCE(wd.`Version`, wi.`DefinitionVersion`, 1),
                    'publishStatus', COALESCE(wd.`PublishStatus`, 'Published'),
                    'publishedAt', wd.`PublishedAt`,
                    'designerJson', COALESCE(wd.`DesignerJson`, '{}'),
                    'formSchemaJson', COALESCE(wd.`FormSchemaJson`, '[]'),
                    'nodes', JSON_ARRAY()
                )
                WHERE wi.`DefinitionSnapshotJson` IS NULL
                   OR wi.`DefinitionSnapshotJson` = ''
                   OR wi.`DefinitionSnapshotJson` = '{}'
                """,
                cancellationToken);

            await ExecuteNonQueryAsync(
                connection,
                "ALTER TABLE `mini_workflow_instances` MODIFY COLUMN `DefinitionCode` varchar(64) CHARACTER SET utf8mb4 NOT NULL",
                cancellationToken);
            await ExecuteNonQueryAsync(
                connection,
                "ALTER TABLE `mini_workflow_instances` MODIFY COLUMN `DefinitionVersion` int NOT NULL DEFAULT 1",
                cancellationToken);
            await ExecuteNonQueryAsync(
                connection,
                "UPDATE `mini_workflow_instances` SET `DefinitionSnapshotJson` = '{}' WHERE `DefinitionSnapshotJson` IS NULL OR `DefinitionSnapshotJson` = ''",
                cancellationToken);
            await ExecuteNonQueryAsync(
                connection,
                "ALTER TABLE `mini_workflow_instances` MODIFY COLUMN `DefinitionSnapshotJson` longtext CHARACTER SET utf8mb4 NOT NULL",
                cancellationToken);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task EnsureWorkflowCollaborationTablesAsync(CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `mini_workflow_attachments` (
              `Id` char(36) COLLATE ascii_general_ci NOT NULL,
              `InstanceId` char(36) COLLATE ascii_general_ci NOT NULL,
              `FileId` char(36) COLLATE ascii_general_ci NOT NULL,
              `Remark` varchar(512) CHARACTER SET utf8mb4 NULL,
              `UploaderUserId` char(36) COLLATE ascii_general_ci NOT NULL,
              `UploaderUserName` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              `CreatedAt` datetime(6) NOT NULL,
              PRIMARY KEY (`Id`),
              UNIQUE KEY `IX_mini_workflow_attachments_InstanceId_FileId` (`InstanceId`, `FileId`),
              KEY `IX_mini_workflow_attachments_InstanceId` (`InstanceId`),
              KEY `IX_mini_workflow_attachments_FileId` (`FileId`)
            ) CHARACTER SET=utf8mb4;
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `mini_workflow_comments` (
              `Id` char(36) COLLATE ascii_general_ci NOT NULL,
              `InstanceId` char(36) COLLATE ascii_general_ci NOT NULL,
              `Content` varchar(2000) CHARACTER SET utf8mb4 NOT NULL,
              `AuthorUserId` char(36) COLLATE ascii_general_ci NOT NULL,
              `AuthorUserName` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              `CreatedAt` datetime(6) NOT NULL,
              PRIMARY KEY (`Id`),
              KEY `IX_mini_workflow_comments_InstanceId` (`InstanceId`)
            ) CHARACTER SET=utf8mb4;
            """,
            cancellationToken);
    }

    private async Task EnsureGeneratedCustomerTableAsync(CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider())
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `mini_customer` (
              `id` char(36) COLLATE ascii_general_ci NOT NULL,
              `TenantId` char(36) COLLATE ascii_general_ci NULL,
              `Title` varchar(256) CHARACTER SET utf8mb4 NOT NULL,
              `Type` varchar(256) CHARACTER SET utf8mb4 NOT NULL,
              `Content` varchar(256) CHARACTER SET utf8mb4 NOT NULL,
              `IsPublished` int NOT NULL,
              `PublishedAt` datetime(6) NULL,
              `created_at` datetime(6) NOT NULL,
              PRIMARY KEY (`id`),
              KEY `IX_mini_customer_TenantId` (`TenantId`)
            ) CHARACTER SET=utf8mb4;
            """,
            cancellationToken);
    }

    private async Task EnsureGeneratedSampleOrderTableAsync(CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider())
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `biz_sample_order` (
              `id` char(36) COLLATE ascii_general_ci NOT NULL,
              `TenantId` char(36) COLLATE ascii_general_ci NULL,
              `WorkflowInstanceId` char(36) COLLATE ascii_general_ci NULL,
              `OriginalName` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              `StoredName` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              `ContentType` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              `Size` bigint NOT NULL,
              `StorageProvider` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              `StoragePath` varchar(512) CHARACTER SET utf8mb4 NOT NULL,
              `Status` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              `created_at` datetime(6) NOT NULL,
              PRIMARY KEY (`id`),
              KEY `IX_biz_sample_order_TenantId` (`TenantId`),
              KEY `IX_biz_sample_order_WorkflowInstanceId` (`WorkflowInstanceId`)
            ) CHARACTER SET=utf8mb4;
            """,
            cancellationToken);

        await EnsureSampleOrderWorkflowColumnsAsync(cancellationToken);
    }

    private async Task EnsureSampleOrderWorkflowColumnsAsync(CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            if (!await ExistsAsync(
                connection,
                """
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'biz_sample_order'
                  AND COLUMN_NAME = 'WorkflowInstanceId'
                """,
                cancellationToken))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    "ALTER TABLE `biz_sample_order` ADD COLUMN `WorkflowInstanceId` char(36) COLLATE ascii_general_ci NULL",
                    cancellationToken);
            }

            if (!await ExistsAsync(
                connection,
                """
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'biz_sample_order'
                  AND INDEX_NAME = 'IX_biz_sample_order_WorkflowInstanceId'
                """,
                cancellationToken))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    "CREATE INDEX `IX_biz_sample_order_WorkflowInstanceId` ON `biz_sample_order` (`WorkflowInstanceId`)",
                    cancellationToken);
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task EnsureFileTableAsync(CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider())
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `mini_files` (
              `Id` char(36) COLLATE ascii_general_ci NOT NULL,
              `OriginalName` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              `StoredName` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
              `ContentType` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              `Size` bigint NOT NULL,
              `StorageProvider` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              `StoragePath` varchar(512) CHARACTER SET utf8mb4 NOT NULL,
              `CreatedAt` datetime(6) NOT NULL,
              PRIMARY KEY (`Id`),
              KEY `IX_mini_files_CreatedAt` (`CreatedAt`)
            ) CHARACTER SET=utf8mb4;
            """,
            cancellationToken);
    }

    private async Task EnsureAlertTableAsync(CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider())
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `mini_alerts` (
              `Id` char(36) COLLATE ascii_general_ci NOT NULL,
              `Type` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              `Level` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              `Title` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
              `Content` varchar(1024) CHARACTER SET utf8mb4 NOT NULL,
              `Source` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              `Status` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              `FirstTriggeredAt` datetime(6) NOT NULL,
              `LastTriggeredAt` datetime(6) NOT NULL,
              `RecoveredAt` datetime(6) NULL,
              `AcknowledgedBy` varchar(64) CHARACTER SET utf8mb4 NULL,
              `AcknowledgedAt` datetime(6) NULL,
              `AcknowledgeRemark` varchar(512) CHARACTER SET utf8mb4 NULL,
              `TriggerCount` int NOT NULL,
              `CreatedAt` datetime(6) NOT NULL,
              `UpdatedAt` datetime(6) NOT NULL,
              PRIMARY KEY (`Id`),
              KEY `IX_mini_alerts_LastTriggeredAt` (`LastTriggeredAt`),
              KEY `IX_mini_alerts_Type_Source_Status` (`Type`, `Source`, `Status`)
            ) CHARACTER SET=utf8mb4;
            """,
            cancellationToken);
    }

    private async Task EnsureAlertRuleTableAsync(CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider())
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `mini_alert_rules` (
              `Id` char(36) COLLATE ascii_general_ci NOT NULL,
              `Code` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              `Name` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              `Description` varchar(512) CHARACTER SET utf8mb4 NOT NULL,
              `Metric` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              `Operator` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
              `Threshold` decimal(18,2) NOT NULL,
              `WindowMinutes` int NOT NULL,
              `Level` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              `Enabled` tinyint(1) NOT NULL,
              `NotifyEnabled` tinyint(1) NOT NULL,
              `Sort` int NOT NULL,
              `Remark` varchar(512) CHARACTER SET utf8mb4 NULL,
              `CreatedAt` datetime(6) NOT NULL,
              `UpdatedAt` datetime(6) NOT NULL,
              PRIMARY KEY (`Id`),
              UNIQUE KEY `IX_mini_alert_rules_Code` (`Code`)
            ) CHARACTER SET=utf8mb4;
            """,
            cancellationToken);
    }

    private async Task EnsureUserNotificationTableAsync(CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider())
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `mini_user_notifications` (
              `Id` char(36) COLLATE ascii_general_ci NOT NULL,
              `UserId` char(36) COLLATE ascii_general_ci NOT NULL,
              `Title` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
              `Message` varchar(1024) CHARACTER SET utf8mb4 NOT NULL,
              `Category` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              `Level` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              `Link` varchar(256) CHARACTER SET utf8mb4 NULL,
              `SourceType` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              `SourceId` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              `IsRead` tinyint(1) NOT NULL,
              `CreatedAt` datetime(6) NOT NULL,
              `ReadAt` datetime(6) NULL,
              PRIMARY KEY (`Id`),
              UNIQUE KEY `IX_mini_user_notifications_User_Source` (`UserId`, `SourceType`, `SourceId`),
              KEY `IX_mini_user_notifications_UserId` (`UserId`),
              KEY `IX_mini_user_notifications_CreatedAt` (`CreatedAt`),
              CONSTRAINT `FK_mini_user_notifications_mini_users_UserId`
                FOREIGN KEY (`UserId`) REFERENCES `mini_users` (`Id`) ON DELETE CASCADE
            ) CHARACTER SET=utf8mb4;
            """,
            cancellationToken);
    }

    private async Task EnsureAlertRuleRecipientTableAsync(CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider())
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `mini_alert_rule_recipients` (
              `Id` char(36) COLLATE ascii_general_ci NOT NULL,
              `AlertRuleId` char(36) COLLATE ascii_general_ci NOT NULL,
              `RecipientType` varchar(16) CHARACTER SET utf8mb4 NOT NULL,
              `RecipientId` char(36) COLLATE ascii_general_ci NOT NULL,
              `CreatedAt` datetime(6) NOT NULL,
              PRIMARY KEY (`Id`),
              UNIQUE KEY `IX_mini_alert_rule_recipients_Rule_Type_Target` (`AlertRuleId`, `RecipientType`, `RecipientId`),
              CONSTRAINT `FK_mini_alert_rule_recipients_mini_alert_rules_AlertRuleId`
                FOREIGN KEY (`AlertRuleId`) REFERENCES `mini_alert_rules` (`Id`) ON DELETE CASCADE
            ) CHARACTER SET=utf8mb4;
            """,
            cancellationToken);
    }

    private async Task EnsureNotificationDeliveryTableAsync(CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider())
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `mini_notification_deliveries` (
              `Id` char(36) COLLATE ascii_general_ci NOT NULL,
              `Channel` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              `UserId` char(36) COLLATE ascii_general_ci NOT NULL,
              `RecipientAddress` varchar(256) CHARACTER SET utf8mb4 NOT NULL,
              `Title` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
              `Content` varchar(2000) CHARACTER SET utf8mb4 NOT NULL,
              `SourceType` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              `SourceId` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              `Status` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              `ErrorMessage` varchar(1024) CHARACTER SET utf8mb4 NULL,
              `RetryCount` int NOT NULL,
              `CreatedAt` datetime(6) NOT NULL,
              `SentAt` datetime(6) NULL,
              PRIMARY KEY (`Id`),
              UNIQUE KEY `IX_mini_notification_deliveries_Channel_Source_User` (`Channel`, `SourceType`, `SourceId`, `UserId`),
              KEY `IX_mini_notification_deliveries_CreatedAt` (`CreatedAt`),
              CONSTRAINT `FK_mini_notification_deliveries_mini_users_UserId`
                FOREIGN KEY (`UserId`) REFERENCES `mini_users` (`Id`) ON DELETE CASCADE
            ) CHARACTER SET=utf8mb4;
            """,
            cancellationToken);
    }

    private async Task EnsureNotificationTemplateTableAsync(CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider())
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `mini_notification_templates` (
              `Id` char(36) COLLATE ascii_general_ci NOT NULL,
              `Code` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              `Name` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              `Category` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              `Level` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              `Channel` varchar(32) CHARACTER SET utf8mb4 NULL,
              `TitleTemplate` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
              `MessageTemplate` varchar(2000) CHARACTER SET utf8mb4 NOT NULL,
              `LinkTemplate` varchar(256) CHARACTER SET utf8mb4 NULL,
              `IsEnabled` tinyint(1) NOT NULL,
              `Remark` varchar(512) CHARACTER SET utf8mb4 NULL,
              `CreatedAt` datetime(6) NOT NULL,
              `UpdatedAt` datetime(6) NOT NULL,
              PRIMARY KEY (`Id`),
              UNIQUE KEY `IX_mini_notification_templates_Code` (`Code`),
              KEY `IX_mini_notification_templates_Category` (`Category`)
            ) CHARACTER SET=utf8mb4;
            """,
            cancellationToken);
    }

    private async Task EnsureNotificationPolicyTableAsync(CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider())
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `mini_notification_policies` (
              `Id` char(36) COLLATE ascii_general_ci NOT NULL,
              `EventCode` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              `EventName` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              `Category` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              `RecipientStrategy` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              `EnableInApp` tinyint(1) NOT NULL,
              `EnableEmail` tinyint(1) NOT NULL,
              `EnableWebhook` tinyint(1) NOT NULL,
              `IsEnabled` tinyint(1) NOT NULL,
              `Remark` varchar(512) CHARACTER SET utf8mb4 NULL,
              `CreatedAt` datetime(6) NOT NULL,
              `UpdatedAt` datetime(6) NOT NULL,
              PRIMARY KEY (`Id`),
              UNIQUE KEY `IX_mini_notification_policies_EventCode` (`EventCode`),
              KEY `IX_mini_notification_policies_Category` (`Category`)
            ) CHARACTER SET=utf8mb4;
            """,
            cancellationToken);
    }

    private async Task EnsureNotificationSubscriptionTableAsync(CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider())
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `mini_notification_subscriptions` (
              `Id` char(36) COLLATE ascii_general_ci NOT NULL,
              `UserId` char(36) COLLATE ascii_general_ci NOT NULL,
              `EventCode` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              `EnableInApp` tinyint(1) NOT NULL,
              `EnableEmail` tinyint(1) NOT NULL,
              `EnableWebhook` tinyint(1) NOT NULL,
              `IsEnabled` tinyint(1) NOT NULL,
              `CreatedAt` datetime(6) NOT NULL,
              `UpdatedAt` datetime(6) NOT NULL,
              PRIMARY KEY (`Id`),
              UNIQUE KEY `IX_mini_notification_subscriptions_UserId_EventCode` (`UserId`, `EventCode`),
              KEY `IX_mini_notification_subscriptions_EventCode` (`EventCode`),
              CONSTRAINT `FK_mini_notification_subscriptions_mini_users_UserId`
                FOREIGN KEY (`UserId`) REFERENCES `mini_users` (`Id`) ON DELETE CASCADE
            ) CHARACTER SET=utf8mb4;
            """,
            cancellationToken);
    }

    private async Task EnsureTenantTablesAsync(CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider())
        {
            return;
        }

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `mini_tenant_packages` (
              `Id` char(36) COLLATE ascii_general_ci NOT NULL,
              `Name` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              `MaxUsers` int NOT NULL,
              `MaxStorageMb` int NOT NULL,
              `MenuIds` longtext CHARACTER SET utf8mb4 NOT NULL,
              `IsEnabled` tinyint(1) NOT NULL,
              `Remark` varchar(512) CHARACTER SET utf8mb4 NULL,
              PRIMARY KEY (`Id`),
              UNIQUE KEY `IX_mini_tenant_packages_Name` (`Name`)
            ) CHARACTER SET=utf8mb4;
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS `mini_tenants` (
              `Id` char(36) COLLATE ascii_general_ci NOT NULL,
              `Name` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
              `Code` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
              `Status` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
              `PackageId` char(36) COLLATE ascii_general_ci NULL,
              `InitializationTemplateCode` varchar(64) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'standard',
              `InitializationStatus` varchar(32) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Success',
              `InitializedAt` datetime(6) NULL,
              `InitializationError` varchar(512) CHARACTER SET utf8mb4 NULL,
              `ContactName` varchar(64) CHARACTER SET utf8mb4 NULL,
              `ContactPhone` varchar(32) CHARACTER SET utf8mb4 NULL,
              `ContactEmail` varchar(256) CHARACTER SET utf8mb4 NULL,
              `ExpireAt` datetime(6) NULL,
              `Remark` varchar(512) CHARACTER SET utf8mb4 NULL,
              `CreatedAt` datetime(6) NOT NULL,
              `UpdatedAt` datetime(6) NOT NULL,
              PRIMARY KEY (`Id`),
              UNIQUE KEY `IX_mini_tenants_Code` (`Code`),
              KEY `IX_mini_tenants_PackageId` (`PackageId`),
              CONSTRAINT `FK_mini_tenants_mini_tenant_packages_PackageId`
                FOREIGN KEY (`PackageId`) REFERENCES `mini_tenant_packages` (`Id`) ON DELETE SET NULL
            ) CHARACTER SET=utf8mb4;
            """,
            cancellationToken);
    }

    private bool IsMySqlProvider()
    {
        return dbContext.Database.ProviderName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) == true;
    }

    private async Task EnsureTenantInitializationColumnsAsync(CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider())
        {
            return;
        }

        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await EnsureColumnAsync(
                connection,
                "mini_tenants",
                "InitializationTemplateCode",
                "ALTER TABLE `mini_tenants` ADD COLUMN `InitializationTemplateCode` varchar(64) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'standard'",
                cancellationToken);
            await EnsureColumnAsync(
                connection,
                "mini_tenants",
                "InitializationStatus",
                "ALTER TABLE `mini_tenants` ADD COLUMN `InitializationStatus` varchar(32) CHARACTER SET utf8mb4 NOT NULL DEFAULT 'Success'",
                cancellationToken);
            await EnsureColumnAsync(
                connection,
                "mini_tenants",
                "InitializedAt",
                "ALTER TABLE `mini_tenants` ADD COLUMN `InitializedAt` datetime(6) NULL",
                cancellationToken);
            await EnsureColumnAsync(
                connection,
                "mini_tenants",
                "InitializationError",
                "ALTER TABLE `mini_tenants` ADD COLUMN `InitializationError` varchar(512) CHARACTER SET utf8mb4 NULL",
                cancellationToken);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task CleanupExpiredAuditLogsAsync(CancellationToken cancellationToken)
    {
        var retentionBoundary = DateTimeOffset.UtcNow.AddDays(-90);
        if (IsMySqlProvider())
        {
            await dbContext.AuditLogs
                .Where(log => log.CreatedAt < retentionBoundary)
                .ExecuteDeleteAsync(cancellationToken);
            return;
        }

        var expiredLogs = await dbContext.AuditLogs
            .Where(log => log.CreatedAt < retentionBoundary)
            .ToArrayAsync(cancellationToken);
        dbContext.AuditLogs.RemoveRange(expiredLogs);
    }

    private async Task EnsureUserDepartmentColumnAsync(CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider())
        {
            return;
        }

        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            if (!await ExistsAsync(
                connection,
                """
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'mini_users'
                  AND COLUMN_NAME = 'DepartmentId'
                """,
                cancellationToken))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    "ALTER TABLE `mini_users` ADD COLUMN `DepartmentId` char(36) COLLATE ascii_general_ci NULL",
                    cancellationToken);
            }

            if (!await ExistsAsync(
                connection,
                """
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'mini_users'
                  AND INDEX_NAME = 'IX_mini_users_DepartmentId'
                """,
                cancellationToken))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    "CREATE INDEX `IX_mini_users_DepartmentId` ON `mini_users` (`DepartmentId`)",
                    cancellationToken);
            }

            if (!await ExistsAsync(
                connection,
                """
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'mini_users'
                  AND CONSTRAINT_NAME = 'FK_mini_users_mini_departments_DepartmentId'
                """,
                cancellationToken))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    """
                    ALTER TABLE `mini_users`
                    ADD CONSTRAINT `FK_mini_users_mini_departments_DepartmentId`
                    FOREIGN KEY (`DepartmentId`) REFERENCES `mini_departments` (`Id`) ON DELETE SET NULL
                    """,
                    cancellationToken);
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task EnsureUserPositionColumnAsync(CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider())
        {
            return;
        }

        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            if (!await ExistsAsync(
                connection,
                """
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'mini_users'
                  AND COLUMN_NAME = 'PositionId'
                """,
                cancellationToken))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    "ALTER TABLE `mini_users` ADD COLUMN `PositionId` char(36) COLLATE ascii_general_ci NULL",
                    cancellationToken);
            }

            if (!await ExistsAsync(
                connection,
                """
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'mini_users'
                  AND INDEX_NAME = 'IX_mini_users_PositionId'
                """,
                cancellationToken))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    "CREATE INDEX `IX_mini_users_PositionId` ON `mini_users` (`PositionId`)",
                    cancellationToken);
            }

            if (!await ExistsAsync(
                connection,
                """
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'mini_users'
                  AND CONSTRAINT_NAME = 'FK_mini_users_mini_positions_PositionId'
                """,
                cancellationToken))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    """
                    ALTER TABLE `mini_users`
                    ADD CONSTRAINT `FK_mini_users_mini_positions_PositionId`
                    FOREIGN KEY (`PositionId`) REFERENCES `mini_positions` (`Id`) ON DELETE SET NULL
                    """,
                    cancellationToken);
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task EnsureUserSecurityStampColumnAsync(CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider())
        {
            return;
        }

        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            if (!await ExistsAsync(
                connection,
                """
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'mini_users'
                  AND COLUMN_NAME = 'SecurityStamp'
                """,
                cancellationToken))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    "ALTER TABLE `mini_users` ADD COLUMN `SecurityStamp` varchar(64) CHARACTER SET utf8mb4 NOT NULL DEFAULT ''",
                    cancellationToken);
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task EnsureUserEmailColumnAsync(CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider())
        {
            return;
        }

        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            if (!await ExistsAsync(
                connection,
                """
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'mini_users'
                  AND COLUMN_NAME = 'Email'
                """,
                cancellationToken))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    "ALTER TABLE `mini_users` ADD COLUMN `Email` varchar(256) CHARACTER SET utf8mb4 NULL",
                    cancellationToken);
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task EnsureUserTenantColumnAsync(CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider())
        {
            return;
        }

        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            if (!await ExistsAsync(
                connection,
                """
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'mini_users'
                  AND COLUMN_NAME = 'TenantId'
                """,
                cancellationToken))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    "ALTER TABLE `mini_users` ADD COLUMN `TenantId` char(36) COLLATE ascii_general_ci NULL",
                    cancellationToken);
            }

            if (!await ExistsAsync(
                connection,
                """
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.STATISTICS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'mini_users'
                  AND INDEX_NAME = 'IX_mini_users_TenantId'
                """,
                cancellationToken))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    "CREATE INDEX `IX_mini_users_TenantId` ON `mini_users` (`TenantId`)",
                    cancellationToken);
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task EnsureTenantScopedCoreColumnsAsync(CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider())
        {
            return;
        }

        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            foreach (var tableName in new[] { "mini_roles", "mini_departments", "mini_positions" })
            {
                await EnsureTenantColumnAsync(connection, tableName, cancellationToken);
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task EnsureTenantScopedCoreIndexesAsync(CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider())
        {
            return;
        }

        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await EnsureTenantScopedCodeIndexAsync(
                connection,
                "mini_roles",
                "IX_mini_roles_Code",
                "IX_mini_roles_TenantId_Code",
                cancellationToken);
            await EnsureTenantScopedCodeIndexAsync(
                connection,
                "mini_departments",
                "IX_mini_departments_Code",
                "IX_mini_departments_TenantId_Code",
                cancellationToken);
            await EnsureTenantScopedCodeIndexAsync(
                connection,
                "mini_positions",
                "IX_mini_positions_Code",
                "IX_mini_positions_TenantId_Code",
                cancellationToken);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task EnsureTenantScopedCodeIndexAsync(
        DbConnection connection,
        string tableName,
        string legacyIndexName,
        string tenantScopedIndexName,
        CancellationToken cancellationToken)
    {
        if (!await ExistsAsync(
            connection,
            $"""
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = '{tableName}'
              AND INDEX_NAME = '{tenantScopedIndexName}'
            """,
            cancellationToken))
        {
            await ExecuteNonQueryAsync(
                connection,
                $"CREATE UNIQUE INDEX `{tenantScopedIndexName}` ON `{tableName}` (`TenantId`, `Code`)",
                cancellationToken);
        }

        if (await ExistsAsync(
            connection,
            $"""
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = '{tableName}'
              AND INDEX_NAME = '{legacyIndexName}'
            """,
            cancellationToken))
        {
            await ExecuteNonQueryAsync(
                connection,
                $"ALTER TABLE `{tableName}` DROP INDEX `{legacyIndexName}`",
                cancellationToken);
        }
    }

    private async Task EnsureTenantColumnAsync(
        DbConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        if (!await ExistsAsync(
            connection,
            $"""
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = '{tableName}'
              AND COLUMN_NAME = 'TenantId'
            """,
            cancellationToken))
        {
            await ExecuteNonQueryAsync(
                connection,
                $"ALTER TABLE `{tableName}` ADD COLUMN `TenantId` char(36) COLLATE ascii_general_ci NULL",
                cancellationToken);
        }

        var indexName = $"IX_{tableName}_TenantId";
        if (!await ExistsAsync(
            connection,
            $"""
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = '{tableName}'
              AND INDEX_NAME = '{indexName}'
            """,
            cancellationToken))
        {
            await ExecuteNonQueryAsync(
                connection,
                $"CREATE INDEX `{indexName}` ON `{tableName}` (`TenantId`)",
                cancellationToken);
        }
    }

    private async Task EnsureAlertRuleEmailEnabledColumnAsync(CancellationToken cancellationToken)
    {
        if (!IsMySqlProvider())
        {
            return;
        }

        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            if (!await ExistsAsync(
                connection,
                """
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME = 'mini_alert_rules'
                  AND COLUMN_NAME = 'EmailEnabled'
                """,
                cancellationToken))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    "ALTER TABLE `mini_alert_rules` ADD COLUMN `EmailEnabled` tinyint(1) NOT NULL DEFAULT 0",
                    cancellationToken);
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<bool> ExistsAsync(
        DbConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        var value = await command.ExecuteScalarAsync(cancellationToken);

        return Convert.ToInt32(value) > 0;
    }

    private static async Task EnsureColumnAsync(
        DbConnection connection,
        string tableName,
        string columnName,
        string addColumnSql,
        CancellationToken cancellationToken)
    {
        if (await ExistsAsync(
            connection,
            $"""
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = '{tableName}'
              AND COLUMN_NAME = '{columnName}'
            """,
            cancellationToken))
        {
            return;
        }

        await ExecuteNonQueryAsync(connection, addColumnSql, cancellationToken);
    }

    private static async Task ExecuteNonQueryAsync(
        DbConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task EnsureAdminRoleAsync(CancellationToken cancellationToken)
    {
        var existingAdminRole = await dbContext.Roles.SingleOrDefaultAsync(x => x.Code == "admin", cancellationToken);
        if (existingAdminRole is not null)
        {
            existingAdminRole.DataScope = "all";
            return;
        }

        dbContext.Roles.Add(new Role
        {
            Id = MiniAdminSeedIds.AdminRoleId,
            Code = "admin",
            Name = "Administrator",
            DataScope = "all",
            IsEnabled = true
        });
    }

    private async Task EnsureTenantAdminRoleAsync(CancellationToken cancellationToken)
    {
        var existingRole = await dbContext.Roles.SingleOrDefaultAsync(
            x => x.Code == "tenant-admin",
            cancellationToken);
        if (existingRole is not null)
        {
            existingRole.Name = "Tenant Administrator";
            existingRole.DataScope = "all";
            existingRole.IsEnabled = true;
            return;
        }

        dbContext.Roles.Add(new Role
        {
            Id = MiniAdminSeedIds.TenantAdminRoleId,
            Code = "tenant-admin",
            Name = "Tenant Administrator",
            DataScope = "all",
            IsEnabled = true
        });
    }

    private async Task EnsureUsersAsync(CancellationToken cancellationToken)
    {
        await EnsureUserAsync(
            MiniAdminSeedIds.AdminUserId,
            "admin",
            "Admin",
            MiniAdminSeedIds.DepartmentHeadquartersId,
            MiniAdminSeedIds.PositionManagerId,
            null,
            true,
            cancellationToken);
        await EnsureUserAsync(
            MiniAdminSeedIds.DemoUserId,
            "demo",
            "Demo User",
            MiniAdminSeedIds.DepartmentHeadquartersId,
            MiniAdminSeedIds.PositionDeveloperId,
            MiniAdminSeedIds.DemoTenantId,
            true,
            cancellationToken);
        await EnsureUserAsync(
            MiniAdminSeedIds.AuditorUserId,
            "auditor",
            "Audit User",
            MiniAdminSeedIds.DepartmentHeadquartersId,
            MiniAdminSeedIds.PositionDeveloperId,
            null,
            false,
            cancellationToken);
    }

    private async Task EnsureTenantsAsync(CancellationToken cancellationToken)
    {
        var defaultPackageMenuIdsJson = JsonSerializer.Serialize(GetTenantAdminMenuIds());
        var existingPackage = await dbContext.TenantPackages.SingleOrDefaultAsync(
            x => x.Id == MiniAdminSeedIds.DefaultTenantPackageId,
            cancellationToken);
        if (existingPackage is null)
        {
            dbContext.TenantPackages.Add(new TenantPackage
            {
                Id = MiniAdminSeedIds.DefaultTenantPackageId,
                Name = "默认套餐",
                MaxUsers = 100,
                MaxStorageMb = 1024,
                MenuIds = defaultPackageMenuIdsJson,
                IsEnabled = true,
                Remark = "SaaS 租户默认套餐"
            });
        }
        else if (EfTenantPackageRepository.ParseMenuIds(existingPackage.MenuIds).Count == 0)
        {
            existingPackage.MenuIds = defaultPackageMenuIdsJson;
        }
        else
        {
            var menuIds = EfTenantPackageRepository.ParseMenuIds(existingPackage.MenuIds);
            var defaultMenuIds = GetTenantAdminMenuIds();
            var missingMenuIds = defaultMenuIds
                .Where(menuId => !menuIds.Contains(menuId))
                .ToArray();
            if (missingMenuIds.Length > 0)
            {
                foreach (var menuId in missingMenuIds)
                {
                    menuIds.Add(menuId);
                }

                existingPackage.MenuIds = JsonSerializer.Serialize(menuIds.Order().ToArray());
            }
        }

        var existingTenant = await dbContext.Tenants.SingleOrDefaultAsync(
            x => x.Id == MiniAdminSeedIds.DemoTenantId,
            cancellationToken);
        if (existingTenant is null)
        {
            dbContext.Tenants.Add(new Tenant
            {
                Id = MiniAdminSeedIds.DemoTenantId,
                Name = "演示租户",
                Code = "demo",
                Status = TenantStatus.Active,
                PackageId = MiniAdminSeedIds.DefaultTenantPackageId,
                ContactName = "Demo",
                Remark = "系统内置演示租户",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            return;
        }

        existingTenant.Code = "demo";
        existingTenant.PackageId ??= MiniAdminSeedIds.DefaultTenantPackageId;
        existingTenant.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private async Task EnsureAllUsersHaveSecurityStampAsync(CancellationToken cancellationToken)
    {
        var usersWithoutSecurityStamp = await dbContext.Users
            .Where(x => x.SecurityStamp == string.Empty)
            .ToArrayAsync(cancellationToken);

        foreach (var user in usersWithoutSecurityStamp)
        {
            user.SecurityStamp = CreateSecurityStamp();
        }
    }

    private async Task EnsureUserAsync(
        Guid userId,
        string userName,
        string realName,
        Guid? departmentId,
        Guid? positionId,
        Guid? tenantId,
        bool isEnabled,
        CancellationToken cancellationToken)
    {
        var existingUser = await dbContext.Users.SingleOrDefaultAsync(
            x => x.UserName == userName,
            cancellationToken);
        if (existingUser is not null)
        {
            existingUser.DepartmentId ??= departmentId;
            existingUser.PositionId ??= positionId;
            existingUser.TenantId = tenantId;
            if (string.IsNullOrWhiteSpace(existingUser.SecurityStamp))
            {
                existingUser.SecurityStamp = CreateSecurityStamp();
            }
            return;
        }

        dbContext.Users.Add(new User
        {
            Id = userId,
            UserName = userName,
            RealName = realName,
            TenantId = tenantId,
            DepartmentId = departmentId,
            PositionId = positionId,
            PasswordHash = passwordService.HashPassword("123456"),
            SecurityStamp = CreateSecurityStamp(),
            IsEnabled = isEnabled
        });
    }

    private static string CreateSecurityStamp()
    {
        return Guid.NewGuid().ToString("N");
    }

    private async Task EnsureMenusAsync(CancellationToken cancellationToken)
    {
        await EnsureMenuAsync(new Menu
        {
            Id = MiniAdminSeedIds.DashboardMenuId,
            Name = "Dashboard",
            Path = "/dashboard",
            Redirect = "/analytics",
            Title = "page.dashboard.title",
            Order = -1,
            IsEnabled = true
        }, cancellationToken);

        await EnsureMenuAsync(new Menu
        {
            Id = MiniAdminSeedIds.AnalyticsMenuId,
            ParentId = MiniAdminSeedIds.DashboardMenuId,
            Name = "Analytics",
            Path = "/analytics",
            Component = "/dashboard/analytics/index",
            Title = "page.dashboard.analytics",
            AffixTab = true,
            PermissionCode = "system:dashboard:analytics",
            IsEnabled = true
        }, cancellationToken);

        await EnsureMenuAsync(new Menu
        {
            Id = MiniAdminSeedIds.WorkspaceMenuId,
            ParentId = MiniAdminSeedIds.DashboardMenuId,
            Name = "Workspace",
            Path = "/workspace",
            Component = "/dashboard/workspace/index",
            Title = "page.dashboard.workspace",
            PermissionCode = "system:dashboard:workspace",
            IsEnabled = true
        }, cancellationToken);

        await EnsureMenuAsync(new Menu
        {
            Id = MiniAdminSeedIds.SystemMenuId,
            Name = "System",
            Path = "/system",
            Redirect = "/system/user",
            Title = "系统管理",
            Icon = "lucide:settings",
            Order = 10,
            IsEnabled = true
        }, cancellationToken);

        await EnsureMenuAsync(new Menu
        {
            Id = MiniAdminSeedIds.UserManagementMenuId,
            ParentId = MiniAdminSeedIds.SystemMenuId,
            Name = "UserManagement",
            Path = "/system/user",
            Component = "/system/user/index",
            Title = "用户管理",
            Icon = "lucide:users",
            Order = 3,
            PermissionCode = "system:user:query",
            IsEnabled = true
        }, cancellationToken);

        await EnsurePermissionAsync(
            MiniAdminSeedIds.UserQueryPermissionId,
            MiniAdminSeedIds.UserManagementMenuId,
            "UserQueryPermission",
            "system:user:query",
            "system:user:query",
            1,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.UserCreatePermissionId,
            MiniAdminSeedIds.UserManagementMenuId,
            "UserCreatePermission",
            "system:user:create",
            "system:user:create",
            2,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.UserUpdatePermissionId,
            MiniAdminSeedIds.UserManagementMenuId,
            "UserUpdatePermission",
            "system:user:update",
            "system:user:update",
            3,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.UserDeletePermissionId,
            MiniAdminSeedIds.UserManagementMenuId,
            "UserDeletePermission",
            "system:user:delete",
            "system:user:delete",
            4,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.UserUnlockPermissionId,
            MiniAdminSeedIds.UserManagementMenuId,
            "UserUnlockPermission",
            "system:user:unlock",
            "system:user:unlock",
            5,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.UserResetPasswordPermissionId,
            MiniAdminSeedIds.UserManagementMenuId,
            "UserResetPasswordPermission",
            "system:user:reset-password",
            "system:user:reset-password",
            6,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.UserImportPermissionId,
            MiniAdminSeedIds.UserManagementMenuId,
            "UserImportPermission",
            "system:user:import",
            "system:user:import",
            7,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.UserExportPermissionId,
            MiniAdminSeedIds.UserManagementMenuId,
            "UserExportPermission",
            "system:user:export",
            "system:user:export",
            8,
            cancellationToken);

        await EnsureSystemChildMenuAsync(
            MiniAdminSeedIds.TenantPackageMenuId,
            "TenantPackage",
            "/system/tenant-package",
            "/system/tenant-package/index",
            "租户套餐",
            "lucide:box",
            1,
            "system:tenant-package:query",
            cancellationToken);

        await EnsureSystemChildMenuAsync(
            MiniAdminSeedIds.TenantManagementMenuId,
            "TenantManagement",
            "/system/tenant",
            "/system/tenant/index",
            "租户管理",
            "lucide:building-2",
            2,
            "system:tenant:query",
            cancellationToken);

        await EnsureSystemChildMenuAsync(
            MiniAdminSeedIds.FileManagementMenuId,
            "FileManagement",
            "/system/file",
            "/system/file/index",
            "文件管理",
            "lucide:file",
            4,
            "system:file:query",
            cancellationToken);

        await EnsurePermissionAsync(
            MiniAdminSeedIds.FileQueryPermissionId,
            MiniAdminSeedIds.FileManagementMenuId,
            "FileQueryPermission",
            "system:file:query",
            "system:file:query",
            1,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.FileUploadPermissionId,
            MiniAdminSeedIds.FileManagementMenuId,
            "FileUploadPermission",
            "system:file:upload",
            "system:file:upload",
            2,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.FileDownloadPermissionId,
            MiniAdminSeedIds.FileManagementMenuId,
            "FileDownloadPermission",
            "system:file:download",
            "system:file:download",
            3,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.FileDeletePermissionId,
            MiniAdminSeedIds.FileManagementMenuId,
            "FileDeletePermission",
            "system:file:delete",
            "system:file:delete",
            4,
            cancellationToken);

        await EnsureSystemChildMenuAsync(
            MiniAdminSeedIds.RoleManagementMenuId,
            "RoleManagement",
            "/system/role",
            "/system/role/index",
            "角色管理",
            "lucide:shield-check",
            5,
            "system:role:query",
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.RoleQueryPermissionId,
            MiniAdminSeedIds.RoleManagementMenuId,
            "RoleQueryPermission",
            "system:role:query",
            "system:role:query",
            1,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.RoleCreatePermissionId,
            MiniAdminSeedIds.RoleManagementMenuId,
            "RoleCreatePermission",
            "system:role:create",
            "system:role:create",
            2,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.RoleUpdatePermissionId,
            MiniAdminSeedIds.RoleManagementMenuId,
            "RoleUpdatePermission",
            "system:role:update",
            "system:role:update",
            3,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.RoleDeletePermissionId,
            MiniAdminSeedIds.RoleManagementMenuId,
            "RoleDeletePermission",
            "system:role:delete",
            "system:role:delete",
            4,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.RoleAssignPermissionId,
            MiniAdminSeedIds.RoleManagementMenuId,
            "RoleAssignPermission",
            "system:role:assign",
            "system:role:assign",
            5,
            cancellationToken);

        await EnsureSystemChildMenuAsync(
            MiniAdminSeedIds.MenuManagementMenuId,
            "MenuManagement",
            "/system/menu",
            "/system/menu/index",
            "菜单管理",
            "lucide:menu",
            6,
            "system:menu:query",
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.MenuQueryPermissionId,
            MiniAdminSeedIds.MenuManagementMenuId,
            "MenuQueryPermission",
            "system:menu:query",
            "system:menu:query",
            1,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.MenuCreatePermissionId,
            MiniAdminSeedIds.MenuManagementMenuId,
            "MenuCreatePermission",
            "system:menu:create",
            "system:menu:create",
            2,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.MenuUpdatePermissionId,
            MiniAdminSeedIds.MenuManagementMenuId,
            "MenuUpdatePermission",
            "system:menu:update",
            "system:menu:update",
            3,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.MenuDeletePermissionId,
            MiniAdminSeedIds.MenuManagementMenuId,
            "MenuDeletePermission",
            "system:menu:delete",
            "system:menu:delete",
            4,
            cancellationToken);

        await EnsureSystemChildMenuAsync(
            MiniAdminSeedIds.DepartmentManagementMenuId,
            "DepartmentManagement",
            "/system/department",
            "/system/department/index",
            "部门管理",
            "lucide:network",
            7,
            "system:department:query",
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.DepartmentQueryPermissionId,
            MiniAdminSeedIds.DepartmentManagementMenuId,
            "DepartmentQueryPermission",
            "system:department:query",
            "system:department:query",
            1,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.DepartmentCreatePermissionId,
            MiniAdminSeedIds.DepartmentManagementMenuId,
            "DepartmentCreatePermission",
            "system:department:create",
            "system:department:create",
            2,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.DepartmentUpdatePermissionId,
            MiniAdminSeedIds.DepartmentManagementMenuId,
            "DepartmentUpdatePermission",
            "system:department:update",
            "system:department:update",
            3,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.DepartmentDeletePermissionId,
            MiniAdminSeedIds.DepartmentManagementMenuId,
            "DepartmentDeletePermission",
            "system:department:delete",
            "system:department:delete",
            4,
            cancellationToken);

        await EnsureSystemChildMenuAsync(
            MiniAdminSeedIds.PositionManagementMenuId,
            "PositionManagement",
            "/system/position",
            "/system/position/index",
            "岗位管理",
            "lucide:user-cog",
            8,
            "system:position:query",
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.PositionQueryPermissionId,
            MiniAdminSeedIds.PositionManagementMenuId,
            "PositionQueryPermission",
            "system:position:query",
            "system:position:query",
            1,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.PositionCreatePermissionId,
            MiniAdminSeedIds.PositionManagementMenuId,
            "PositionCreatePermission",
            "system:position:create",
            "system:position:create",
            2,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.PositionUpdatePermissionId,
            MiniAdminSeedIds.PositionManagementMenuId,
            "PositionUpdatePermission",
            "system:position:update",
            "system:position:update",
            3,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.PositionDeletePermissionId,
            MiniAdminSeedIds.PositionManagementMenuId,
            "PositionDeletePermission",
            "system:position:delete",
            "system:position:delete",
            4,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.PositionImportPermissionId,
            MiniAdminSeedIds.PositionManagementMenuId,
            "PositionImportPermission",
            "system:position:import",
            "system:position:import",
            5,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.PositionExportPermissionId,
            MiniAdminSeedIds.PositionManagementMenuId,
            "PositionExportPermission",
            "system:position:export",
            "system:position:export",
            6,
            cancellationToken);

        await EnsureSystemChildMenuAsync(
            MiniAdminSeedIds.DictionaryManagementMenuId,
            "DictionaryManagement",
            "/system/dictionary",
            "/system/dictionary/index",
            "字典管理",
            "lucide:book-open",
            9,
            "system:dictionary:query",
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.DictionaryQueryPermissionId,
            MiniAdminSeedIds.DictionaryManagementMenuId,
            "DictionaryQueryPermission",
            "system:dictionary:query",
            "system:dictionary:query",
            1,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.DictionaryCreatePermissionId,
            MiniAdminSeedIds.DictionaryManagementMenuId,
            "DictionaryCreatePermission",
            "system:dictionary:create",
            "system:dictionary:create",
            2,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.DictionaryUpdatePermissionId,
            MiniAdminSeedIds.DictionaryManagementMenuId,
            "DictionaryUpdatePermission",
            "system:dictionary:update",
            "system:dictionary:update",
            3,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.DictionaryDeletePermissionId,
            MiniAdminSeedIds.DictionaryManagementMenuId,
            "DictionaryDeletePermission",
            "system:dictionary:delete",
            "system:dictionary:delete",
            4,
            cancellationToken);

        await EnsureSystemChildMenuAsync(
            MiniAdminSeedIds.ParameterSettingMenuId,
            "ParameterSetting",
            "/system/parameter",
            "/system/parameter/index",
            "参数设置",
            "lucide:settings-2",
            10,
            "system:parameter:query",
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.ParameterQueryPermissionId,
            MiniAdminSeedIds.ParameterSettingMenuId,
            "ParameterQueryPermission",
            "system:parameter:query",
            "system:parameter:query",
            1,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.ParameterCreatePermissionId,
            MiniAdminSeedIds.ParameterSettingMenuId,
            "ParameterCreatePermission",
            "system:parameter:create",
            "system:parameter:create",
            2,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.ParameterUpdatePermissionId,
            MiniAdminSeedIds.ParameterSettingMenuId,
            "ParameterUpdatePermission",
            "system:parameter:update",
            "system:parameter:update",
            3,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.ParameterDeletePermissionId,
            MiniAdminSeedIds.ParameterSettingMenuId,
            "ParameterDeletePermission",
            "system:parameter:delete",
            "system:parameter:delete",
            4,
            cancellationToken);

        await EnsureSystemChildMenuAsync(
            MiniAdminSeedIds.NoticeAnnouncementMenuId,
            "NoticeAnnouncement",
            "/system/notice",
            "/system/notice/index",
            "通知公告",
            "lucide:megaphone",
            11,
            "system:notice:query",
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.NoticeQueryPermissionId,
            MiniAdminSeedIds.NoticeAnnouncementMenuId,
            "NoticeQueryPermission",
            "system:notice:query",
            "system:notice:query",
            1,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.NoticeCreatePermissionId,
            MiniAdminSeedIds.NoticeAnnouncementMenuId,
            "NoticeCreatePermission",
            "system:notice:create",
            "system:notice:create",
            2,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.NoticeUpdatePermissionId,
            MiniAdminSeedIds.NoticeAnnouncementMenuId,
            "NoticeUpdatePermission",
            "system:notice:update",
            "system:notice:update",
            3,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.NoticeDeletePermissionId,
            MiniAdminSeedIds.NoticeAnnouncementMenuId,
            "NoticeDeletePermission",
            "system:notice:delete",
            "system:notice:delete",
            4,
            cancellationToken);

        await EnsureSystemChildMenuAsync(
            MiniAdminSeedIds.LogManagementMenuId,
            "LogManagement",
            "/system/log",
            "/system/log/index",
            "日志管理",
            "lucide:file-text",
            12,
            "system:log:query",
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.LogQueryPermissionId,
            MiniAdminSeedIds.LogManagementMenuId,
            "LogQueryPermission",
            "system:log:query",
            "system:log:query",
            1,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.LogExportPermissionId,
            MiniAdminSeedIds.LogManagementMenuId,
            "LogExportPermission",
            "system:log:export",
            "system:log:export",
            2,
            cancellationToken);

        await EnsureSystemChildMenuAsync(
            MiniAdminSeedIds.LoginLogMenuId,
            "LoginLog",
            "/system/login-log",
            "/system/login-log/index",
            "登录日志",
            "lucide:log-in",
            13,
            "system:login-log:query",
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.LoginLogQueryPermissionId,
            MiniAdminSeedIds.LoginLogMenuId,
            "LoginLogQueryPermission",
            "system:login-log:query",
            "system:login-log:query",
            1,
            cancellationToken);

        await EnsureSystemChildMenuAsync(
            MiniAdminSeedIds.OnlineUserMenuId,
            "OnlineUser",
            "/system/online-user",
            "/system/online-user/index",
            "在线用户",
            "lucide:monitor-dot",
            14,
            "system:online-user:query",
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.OnlineUserQueryPermissionId,
            MiniAdminSeedIds.OnlineUserMenuId,
            "OnlineUserQueryPermission",
            "system:online-user:query",
            "system:online-user:query",
            1,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.OnlineUserForceLogoutPermissionId,
            MiniAdminSeedIds.OnlineUserMenuId,
            "OnlineUserForceLogoutPermission",
            "system:online-user:force-logout",
            "system:online-user:force-logout",
            2,
            cancellationToken);

        await EnsureSystemChildMenuAsync(
            MiniAdminSeedIds.PermissionDiagnosticsMenuId,
            "PermissionDiagnostics",
            "/system/permission-diagnostics",
            "/system/permission-diagnostics/index",
            "权限诊断",
            "lucide:shield-question",
            15,
            "system:permission-diagnostics:query",
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.PermissionDiagnosticsQueryPermissionId,
            MiniAdminSeedIds.PermissionDiagnosticsMenuId,
            "PermissionDiagnosticsQueryPermission",
            "system:permission-diagnostics:query",
            "system:permission-diagnostics:query",
            1,
            cancellationToken);
        await EnsurePermissionAsync(
            MiniAdminSeedIds.PermissionDiagnosticsRefreshCachePermissionId,
            MiniAdminSeedIds.PermissionDiagnosticsMenuId,
            "PermissionDiagnosticsRefreshCachePermission",
            "system:permission-diagnostics:refresh-cache",
            "system:permission-diagnostics:refresh-cache",
            2,
            cancellationToken);
    }

    private async Task EnsureDepartmentsAsync(CancellationToken cancellationToken)
    {
        await EnsureDepartmentAsync(new Department
        {
            Id = MiniAdminSeedIds.DepartmentHeadquartersId,
            Code = "hq",
            Name = "总部",
            Leader = "Admin",
            Order = 1,
            IsEnabled = true
        }, cancellationToken);

        await EnsureDepartmentAsync(new Department
        {
            Id = MiniAdminSeedIds.DepartmentResearchId,
            ParentId = MiniAdminSeedIds.DepartmentHeadquartersId,
            Code = "rd",
            Name = "研发部",
            Order = 1,
            IsEnabled = true
        }, cancellationToken);

        await EnsureDepartmentAsync(new Department
        {
            Id = MiniAdminSeedIds.DepartmentOperationsId,
            ParentId = MiniAdminSeedIds.DepartmentHeadquartersId,
            Code = "ops",
            Name = "运营部",
            Order = 2,
            IsEnabled = true
        }, cancellationToken);
    }

    private async Task EnsureDepartmentAsync(Department department, CancellationToken cancellationToken)
    {
        var existingDepartment = await dbContext.Departments.SingleOrDefaultAsync(
            x => x.Id == department.Id,
            cancellationToken);
        if (existingDepartment is not null)
        {
            existingDepartment.ParentId = department.ParentId;
            existingDepartment.Code = department.Code;
            existingDepartment.Name = department.Name;
            existingDepartment.Leader = department.Leader;
            existingDepartment.Phone = department.Phone;
            existingDepartment.Order = department.Order;
            existingDepartment.IsEnabled = department.IsEnabled;
            return;
        }

        dbContext.Departments.Add(department);
    }

    private async Task EnsurePositionsAsync(CancellationToken cancellationToken)
    {
        await EnsurePositionAsync(new Position
        {
            Id = MiniAdminSeedIds.PositionManagerId,
            Code = "manager",
            Name = "管理员",
            Order = 1,
            Remark = "系统默认管理岗位",
            IsEnabled = true
        }, cancellationToken);

        await EnsurePositionAsync(new Position
        {
            Id = MiniAdminSeedIds.PositionDeveloperId,
            Code = "developer",
            Name = "开发工程师",
            Order = 2,
            Remark = "研发岗位示例",
            IsEnabled = true
        }, cancellationToken);
    }

    private async Task EnsurePositionAsync(Position position, CancellationToken cancellationToken)
    {
        var existingPosition = await dbContext.Positions.SingleOrDefaultAsync(
            x => x.Id == position.Id,
            cancellationToken);
        if (existingPosition is not null)
        {
            existingPosition.Code = position.Code;
            existingPosition.Name = position.Name;
            existingPosition.Order = position.Order;
            existingPosition.Remark = position.Remark;
            existingPosition.IsEnabled = position.IsEnabled;
            return;
        }

        dbContext.Positions.Add(position);
    }

    private async Task EnsureDictionariesAsync(CancellationToken cancellationToken)
    {
        await EnsureDictionaryTypeAsync(new DictionaryType
        {
            Id = MiniAdminSeedIds.DictionaryUserStatusId,
            Code = "user_status",
            Name = "用户状态",
            Order = 1,
            IsEnabled = true
        }, cancellationToken);

        await EnsureDictionaryItemAsync(new DictionaryItem
        {
            Id = MiniAdminSeedIds.DictionaryUserStatusEnabledId,
            TypeId = MiniAdminSeedIds.DictionaryUserStatusId,
            Label = "启用",
            Value = "1",
            Color = "green",
            Order = 1,
            IsEnabled = true
        }, cancellationToken);

        await EnsureDictionaryItemAsync(new DictionaryItem
        {
            Id = MiniAdminSeedIds.DictionaryUserStatusDisabledId,
            TypeId = MiniAdminSeedIds.DictionaryUserStatusId,
            Label = "停用",
            Value = "0",
            Color = "default",
            Order = 2,
            IsEnabled = true
        }, cancellationToken);
    }

    private async Task EnsureDictionaryTypeAsync(DictionaryType dictionaryType, CancellationToken cancellationToken)
    {
        var existingType = await dbContext.DictionaryTypes.SingleOrDefaultAsync(
            x => x.Id == dictionaryType.Id,
            cancellationToken);
        if (existingType is not null)
        {
            existingType.Code = dictionaryType.Code;
            existingType.Name = dictionaryType.Name;
            existingType.Order = dictionaryType.Order;
            existingType.IsEnabled = dictionaryType.IsEnabled;
            return;
        }

        dbContext.DictionaryTypes.Add(dictionaryType);
    }

    private async Task EnsureDictionaryItemAsync(DictionaryItem dictionaryItem, CancellationToken cancellationToken)
    {
        var existingItem = await dbContext.DictionaryItems.SingleOrDefaultAsync(
            x => x.Id == dictionaryItem.Id,
            cancellationToken);
        if (existingItem is not null)
        {
            existingItem.TypeId = dictionaryItem.TypeId;
            existingItem.Label = dictionaryItem.Label;
            existingItem.Value = dictionaryItem.Value;
            existingItem.Color = dictionaryItem.Color;
            existingItem.Order = dictionaryItem.Order;
            existingItem.IsEnabled = dictionaryItem.IsEnabled;
            return;
        }

        dbContext.DictionaryItems.Add(dictionaryItem);
    }

    private async Task EnsureSystemParametersAsync(CancellationToken cancellationToken)
    {
        await EnsureSystemParameterAsync(new SystemParameter
        {
            Id = MiniAdminSeedIds.ParameterSiteNameId,
            Key = "site_name",
            Name = "站点名称",
            Value = "MiniAdmin",
            Group = "system",
            Remark = "后台系统显示名称",
            Order = 1,
            IsEnabled = true
        }, cancellationToken);

        await EnsureSystemParameterAsync(new SystemParameter
        {
            Id = MiniAdminSeedIds.ParameterDefaultPasswordId,
            Key = "default_password",
            Name = "默认密码",
            Value = "123456",
            Group = "security",
            Remark = "新增用户默认密码示例",
            Order = 2,
            IsEnabled = true
        }, cancellationToken);

        await EnsureSystemParameterAsync(new SystemParameter
        {
            Id = MiniAdminSeedIds.ParameterPasswordMinLengthId,
            Key = "Security.Password.MinLength",
            Name = "密码最小长度",
            Value = "6",
            Group = "security",
            Remark = "修改密码和重置密码时使用",
            Order = 3,
            IsEnabled = true
        }, cancellationToken);

        await EnsureSystemParameterAsync(new SystemParameter
        {
            Id = MiniAdminSeedIds.ParameterPasswordRequireDigitId,
            Key = "Security.Password.RequireDigit",
            Name = "密码必须包含数字",
            Value = "true",
            Group = "security",
            Remark = "修改密码和重置密码时使用",
            Order = 4,
            IsEnabled = true
        }, cancellationToken);

        await EnsureSystemParameterAsync(new SystemParameter
        {
            Id = MiniAdminSeedIds.ParameterPasswordRequireLetterId,
            Key = "Security.Password.RequireLetter",
            Name = "密码必须包含字母",
            Value = "true",
            Group = "security",
            Remark = "修改密码和重置密码时使用",
            Order = 5,
            IsEnabled = true
        }, cancellationToken);

        await EnsureSystemParameterAsync(new SystemParameter
        {
            Id = MiniAdminSeedIds.ParameterSecurityCaptchaRequiredFailuresId,
            Key = "security.login.captcha_required_failures",
            Name = "验证码触发失败次数",
            Value = "3",
            Group = "security",
            Remark = "同一用户/IP 连续登录失败达到该次数后要求验证码",
            Order = 6,
            IsEnabled = true
        }, cancellationToken, preserveExistingValue: true);

        await EnsureSystemParameterAsync(new SystemParameter
        {
            Id = MiniAdminSeedIds.ParameterSecurityLockoutFailuresId,
            Key = "security.login.lockout_failures",
            Name = "账号锁定失败次数",
            Value = "5",
            Group = "security",
            Remark = "连续登录失败达到该次数后锁定登录",
            Order = 7,
            IsEnabled = true
        }, cancellationToken, preserveExistingValue: true);

        await EnsureSystemParameterAsync(new SystemParameter
        {
            Id = MiniAdminSeedIds.ParameterSecurityLockoutMinutesId,
            Key = "security.login.lockout_minutes",
            Name = "账号锁定分钟数",
            Value = "10",
            Group = "security",
            Remark = "登录锁定持续时间",
            Order = 8,
            IsEnabled = true
        }, cancellationToken, preserveExistingValue: true);

        await EnsureSystemParameterAsync(new SystemParameter
        {
            Id = MiniAdminSeedIds.ParameterSecurityCaptchaExpireSecondsId,
            Key = "security.login.captcha_expire_seconds",
            Name = "验证码有效秒数",
            Value = "120",
            Group = "security",
            Remark = "验证码缓存有效期",
            Order = 9,
            IsEnabled = true
        }, cancellationToken, preserveExistingValue: true);

        await EnsureSystemParameterAsync(new SystemParameter
        {
            Id = MiniAdminSeedIds.ParameterSecurityOnlineActiveTimeoutMinutesId,
            Key = "security.online.active_timeout_minutes",
            Name = "在线活跃分钟数",
            Value = "30",
            Group = "security",
            Remark = "超过该时间无请求则不视为在线",
            Order = 10,
            IsEnabled = true
        }, cancellationToken, preserveExistingValue: true);

        await EnsureSystemParameterAsync(new SystemParameter
        {
            Id = MiniAdminSeedIds.ParameterSecurityOnlineTouchThrottleSecondsId,
            Key = "security.online.touch_throttle_seconds",
            Name = "在线心跳写入间隔秒数",
            Value = "30",
            Group = "security",
            Remark = "降低每次请求刷新在线状态的写库频率",
            Order = 11,
            IsEnabled = true
        }, cancellationToken, preserveExistingValue: true);

        await EnsureSystemParameterAsync(new SystemParameter
        {
            Id = MiniAdminSeedIds.ParameterSecurityStaleUserDaysId,
            Key = "security.account.stale_user_days",
            Name = "长期未登录天数",
            Value = "90",
            Group = "security",
            Remark = "安全中心统计长期未登录用户的阈值",
            Order = 12,
            IsEnabled = true
        }, cancellationToken, preserveExistingValue: true);

        await EnsureAppBrandingWatermarkParametersAsync(cancellationToken);
    }

    private async Task SeedAppBrandingWatermarkAsync(CancellationToken cancellationToken)
    {
        await EnsureAppBrandingWatermarkParametersAsync(cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureAppBrandingWatermarkParametersAsync(CancellationToken cancellationToken)
    {
        await EnsureSystemParameterAsync(new SystemParameter
        {
            Id = MiniAdminSeedIds.ParameterAppBrandNameId,
            Key = "app.brand.name",
            Name = "系统完整名称",
            Value = "MiniAdmin",
            Group = "appearance",
            Remark = "用于浏览器标题和后台布局显示",
            Order = 101,
            IsEnabled = true
        }, cancellationToken, preserveExistingValue: true);

        await EnsureSystemParameterAsync(new SystemParameter
        {
            Id = MiniAdminSeedIds.ParameterAppBrandShortNameId,
            Key = "app.brand.shortName",
            Name = "系统短名称",
            Value = "MiniAdmin",
            Group = "appearance",
            Remark = "用于登录页等空间有限的位置",
            Order = 102,
            IsEnabled = true
        }, cancellationToken, preserveExistingValue: true);

        await EnsureSystemParameterAsync(new SystemParameter
        {
            Id = MiniAdminSeedIds.ParameterAppBrandLoginTitleId,
            Key = "app.brand.loginTitle",
            Name = "登录页标题",
            Value = "MiniAdmin 企业后台",
            Group = "appearance",
            Remark = "登录页主标题",
            Order = 103,
            IsEnabled = true
        }, cancellationToken, preserveExistingValue: true);

        await EnsureSystemParameterAsync(new SystemParameter
        {
            Id = MiniAdminSeedIds.ParameterAppBrandCopyrightId,
            Key = "app.brand.copyright",
            Name = "版权文案",
            Value = string.Empty,
            Group = "appearance",
            Remark = "预留给页脚或版权展示",
            Order = 104,
            IsEnabled = true
        }, cancellationToken, preserveExistingValue: true);

        await EnsureSystemParameterAsync(new SystemParameter
        {
            Id = MiniAdminSeedIds.ParameterAppWatermarkEnabledId,
            Key = "app.watermark.enabled",
            Name = "启用全局水印",
            Value = "false",
            Group = "appearance",
            Remark = "开启后在后台主布局显示全局水印",
            Order = 105,
            IsEnabled = true
        }, cancellationToken, preserveExistingValue: true);

        await EnsureSystemParameterAsync(new SystemParameter
        {
            Id = MiniAdminSeedIds.ParameterAppWatermarkTextId,
            Key = "app.watermark.text",
            Name = "全局水印文字",
            Value = string.Empty,
            Group = "appearance",
            Remark = "为空时默认显示当前登录用户信息",
            Order = 106,
            IsEnabled = true
        }, cancellationToken, preserveExistingValue: true);
    }

    private async Task EnsureSystemParameterAsync(
        SystemParameter parameter,
        CancellationToken cancellationToken,
        bool preserveExistingValue = false)
    {
        var existingParameter = await dbContext.SystemParameters.SingleOrDefaultAsync(
            x => x.Id == parameter.Id || x.Key == parameter.Key,
            cancellationToken);
        if (existingParameter is not null)
        {
            existingParameter.Key = parameter.Key;
            existingParameter.Name = parameter.Name;
            if (!preserveExistingValue)
            {
                existingParameter.Value = parameter.Value;
            }
            existingParameter.Group = parameter.Group;
            existingParameter.Remark = parameter.Remark;
            existingParameter.Order = parameter.Order;
            existingParameter.IsEnabled = parameter.IsEnabled;
            return;
        }

        dbContext.SystemParameters.Add(parameter);
    }

    private async Task EnsureNoticesAsync(CancellationToken cancellationToken)
    {
        await EnsureNoticeAsync(new Notice
        {
            Id = MiniAdminSeedIds.NoticeWelcomeId,
            Title = "欢迎使用 MiniAdmin",
            Type = "notice",
            Content = "MiniAdmin 后台管理系统已经完成基础登录、权限和系统管理模块接入。",
            IsPublished = true,
            PublishedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
    }

    private async Task EnsureNoticeAsync(Notice notice, CancellationToken cancellationToken)
    {
        var existingNotice = await dbContext.Notices.SingleOrDefaultAsync(
            x => x.Id == notice.Id,
            cancellationToken);
        if (existingNotice is not null)
        {
            existingNotice.Title = notice.Title;
            existingNotice.Type = notice.Type;
            existingNotice.Content = notice.Content;
            existingNotice.IsPublished = notice.IsPublished;
            existingNotice.PublishedAt = notice.PublishedAt;
            return;
        }

        dbContext.Notices.Add(notice);
    }

    private Task EnsureSystemChildMenuAsync(
        Guid id,
        string name,
        string path,
        string component,
        string title,
        string icon,
        int order,
        string permissionCode,
        CancellationToken cancellationToken)
    {
        return EnsureMenuAsync(new Menu
        {
            Id = id,
            ParentId = MiniAdminSeedIds.SystemMenuId,
            Name = name,
            Path = path,
            Component = component,
            Title = title,
            Icon = icon,
            Order = order,
            PermissionCode = permissionCode,
            IsEnabled = true
        }, cancellationToken);
    }

    private Task EnsurePermissionAsync(
        Guid id,
        Guid parentId,
        string name,
        string path,
        string title,
        int order,
        CancellationToken cancellationToken)
    {
        return EnsureMenuAsync(new Menu
        {
            Id = id,
            ParentId = parentId,
            Name = name,
            Path = path,
            Title = title,
            Order = order,
            PermissionCode = path,
            IsEnabled = true,
            IsVisible = false
        }, cancellationToken);
    }

    private async Task EnsureMenuAsync(Menu menu, CancellationToken cancellationToken)
    {
        var existingMenu = await dbContext.Menus.SingleOrDefaultAsync(x => x.Id == menu.Id, cancellationToken);
        if (existingMenu is not null)
        {
            existingMenu.ParentId = menu.ParentId;
            existingMenu.Name = menu.Name;
            existingMenu.Path = menu.Path;
            existingMenu.Component = menu.Component;
            existingMenu.Redirect = menu.Redirect;
            existingMenu.Title = menu.Title;
            existingMenu.Icon = menu.Icon;
            existingMenu.Order = menu.Order;
            existingMenu.AffixTab = menu.AffixTab;
            existingMenu.PermissionCode = menu.PermissionCode;
            existingMenu.IsEnabled = menu.IsEnabled;
            existingMenu.IsVisible = menu.IsVisible;
            return;
        }

        dbContext.Menus.Add(menu);
    }

    private async Task EnsureScheduledJobAsync(ScheduledJob job, CancellationToken cancellationToken)
    {
        var existingJob = await dbContext.ScheduledJobs.SingleOrDefaultAsync(
            x => x.JobKey == job.JobKey,
            cancellationToken);
        if (existingJob is not null)
        {
            existingJob.Name = job.Name;
            existingJob.Description = job.Description;
            existingJob.IntervalSeconds = existingJob.IntervalSeconds < 60
                ? job.IntervalSeconds
                : existingJob.IntervalSeconds;
            existingJob.NextRunAt ??= job.NextRunAt;
            existingJob.UpdatedAt = DateTimeOffset.UtcNow;
            return;
        }

        dbContext.ScheduledJobs.Add(job);
    }

    private async Task EnsureNotificationTemplateAsync(
        NotificationTemplate template,
        CancellationToken cancellationToken)
    {
        var existingTemplate = await dbContext.NotificationTemplates.SingleOrDefaultAsync(
            x => x.Code == template.Code,
            cancellationToken);
        if (existingTemplate is not null)
        {
            return;
        }

        dbContext.NotificationTemplates.Add(template);
    }

    private async Task EnsureNotificationPolicyAsync(
        NotificationPolicy policy,
        CancellationToken cancellationToken)
    {
        var existingPolicy = await dbContext.NotificationPolicies.SingleOrDefaultAsync(
            x => x.EventCode == policy.EventCode,
            cancellationToken);
        if (existingPolicy is not null)
        {
            existingPolicy.EventName = policy.EventName;
            existingPolicy.Category = policy.Category;
            existingPolicy.RecipientStrategy = policy.RecipientStrategy;
            existingPolicy.Remark ??= policy.Remark;
            existingPolicy.UpdatedAt = DateTimeOffset.UtcNow;
            return;
        }

        dbContext.NotificationPolicies.Add(policy);
    }

    private async Task EnsureAlertRuleAsync(AlertRule rule, CancellationToken cancellationToken)
    {
        var existingRule = await dbContext.AlertRules.SingleOrDefaultAsync(
            x => x.Code == rule.Code,
            cancellationToken);
        if (existingRule is not null)
        {
            existingRule.Name = rule.Name;
            existingRule.Description = rule.Description;
            existingRule.Metric = rule.Metric;
            existingRule.Operator = rule.Operator;
            existingRule.Sort = rule.Sort;
            existingRule.UpdatedAt = DateTimeOffset.UtcNow;
            return;
        }

        dbContext.AlertRules.Add(rule);
    }

    private async Task EnsureDefaultAlertRuleRecipientAsync(Guid alertRuleId, CancellationToken cancellationToken)
    {
        if (await dbContext.AlertRuleRecipients.AnyAsync(
            recipient => recipient.AlertRuleId == alertRuleId,
            cancellationToken))
        {
            return;
        }

        dbContext.AlertRuleRecipients.Add(new AlertRuleRecipient
        {
            Id = Guid.NewGuid(),
            AlertRuleId = alertRuleId,
            RecipientType = "Role",
            RecipientId = MiniAdminSeedIds.AdminRoleId,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private async Task EnsureMenuParentAndOrderAsync(
        Guid menuId,
        Guid parentId,
        int order,
        CancellationToken cancellationToken)
    {
        var menu = await dbContext.Menus.SingleAsync(x => x.Id == menuId, cancellationToken);
        menu.ParentId = parentId;
        menu.Order = order;
    }

    private async Task EnsureParentRoleMenusAsync(
        Guid parentMenuId,
        IReadOnlyCollection<Guid> childMenuIds,
        CancellationToken cancellationToken)
    {
        var roleIds = await dbContext.RoleMenus
            .Where(x => childMenuIds.Contains(x.MenuId))
            .Select(x => x.RoleId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        foreach (var roleId in roleIds)
        {
            if (await dbContext.RoleMenus.AnyAsync(
                x => x.RoleId == roleId && x.MenuId == parentMenuId,
                cancellationToken))
            {
                continue;
            }

            dbContext.RoleMenus.Add(new RoleMenu
            {
                RoleId = roleId,
                MenuId = parentMenuId
            });
        }
    }

    private async Task EnsureAdminUserRoleAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.UserRoles.AnyAsync(
            x => x.UserId == MiniAdminSeedIds.AdminUserId && x.RoleId == MiniAdminSeedIds.AdminRoleId,
            cancellationToken))
        {
            return;
        }

        dbContext.UserRoles.Add(new UserRole
        {
            UserId = MiniAdminSeedIds.AdminUserId,
            RoleId = MiniAdminSeedIds.AdminRoleId
        });
    }

    private async Task RemoveLegacyAdminRoleFromNonAdminSeedUsersAsync(CancellationToken cancellationToken)
    {
        var legacyAdminRoles = await dbContext.UserRoles
            .Where(x =>
                (x.UserId == MiniAdminSeedIds.DemoUserId || x.UserId == MiniAdminSeedIds.AuditorUserId) &&
                x.RoleId == MiniAdminSeedIds.AdminRoleId)
            .ToArrayAsync(cancellationToken);
        if (legacyAdminRoles.Length == 0)
        {
            return;
        }

        dbContext.UserRoles.RemoveRange(legacyAdminRoles);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureRoleMenuAsync(Guid menuId, CancellationToken cancellationToken)
    {
        await EnsureRoleMenuAsync(MiniAdminSeedIds.AdminRoleId, menuId, cancellationToken);
    }

    private async Task EnsureRoleMenuAsync(Guid roleId, Guid menuId, CancellationToken cancellationToken)
    {
        if (await dbContext.RoleMenus.AnyAsync(
            x => x.RoleId == roleId && x.MenuId == menuId,
            cancellationToken))
        {
            return;
        }

        dbContext.RoleMenus.Add(new RoleMenu
        {
            RoleId = roleId,
            MenuId = menuId
        });
    }

    private async Task EnsureTenantAdminRoleMenusAsync(CancellationToken cancellationToken)
    {
        foreach (var menuId in GetTenantAdminMenuIds())
        {
            if (await dbContext.RoleMenus.AnyAsync(
                x => x.RoleId == MiniAdminSeedIds.TenantAdminRoleId && x.MenuId == menuId,
                cancellationToken))
            {
                continue;
            }

            dbContext.RoleMenus.Add(new RoleMenu
            {
                RoleId = MiniAdminSeedIds.TenantAdminRoleId,
                MenuId = menuId
            });
        }
    }

    private async Task<bool> HasAdminRoleMenusAsync(CancellationToken cancellationToken)
    {
        return await dbContext.RoleMenus.AnyAsync(
            x => x.RoleId == MiniAdminSeedIds.AdminRoleId,
            cancellationToken);
    }

    private async Task EnsureAdminPermissionIfParentAssignedAsync(
        Guid parentMenuId,
        Guid permissionMenuId,
        CancellationToken cancellationToken)
    {
        var hasParent = await dbContext.RoleMenus.AnyAsync(
            x => x.RoleId == MiniAdminSeedIds.AdminRoleId && x.MenuId == parentMenuId,
            cancellationToken);
        if (!hasParent)
        {
            return;
        }

        await EnsureRoleMenuAsync(permissionMenuId, cancellationToken);
    }

    private static IReadOnlyList<Guid> GetAdminMenuIds()
    {
        return
        [
            MiniAdminSeedIds.DashboardMenuId,
            MiniAdminSeedIds.AnalyticsMenuId,
            MiniAdminSeedIds.WorkspaceMenuId,
            MiniAdminSeedIds.PlatformManagementMenuId,
            MiniAdminSeedIds.PlatformTenantMenuId,
            MiniAdminSeedIds.PlatformTenantQueryPermissionId,
            MiniAdminSeedIds.PlatformTenantCreatePermissionId,
            MiniAdminSeedIds.PlatformTenantUpdatePermissionId,
            MiniAdminSeedIds.PlatformTenantEnablePermissionId,
            MiniAdminSeedIds.PlatformTenantDisablePermissionId,
            MiniAdminSeedIds.DevelopmentToolsMenuId,
            MiniAdminSeedIds.CodeGeneratorMenuId,
            MiniAdminSeedIds.CodeGeneratorQueryPermissionId,
            MiniAdminSeedIds.CodeGeneratorPreviewPermissionId,
            MiniAdminSeedIds.CodeGeneratorGeneratePermissionId,
            MiniAdminSeedIds.CodeGeneratorRollbackPermissionId,
            MiniAdminSeedIds.ProjectRuntimeMenuId,
            MiniAdminSeedIds.ProjectRuntimeQueryPermissionId,
            MiniAdminSeedIds.ProjectRuntimeManagePermissionId,
            MiniAdminSeedIds.ProjectRuntimeLogPermissionId,
            MiniAdminSeedIds.WorkflowManagementMenuId,
            MiniAdminSeedIds.WorkflowCenterMenuId,
            MiniAdminSeedIds.WorkflowCenterQueryPermissionId,
            MiniAdminSeedIds.WorkflowDefinitionManagePermissionId,
            MiniAdminSeedIds.WorkflowInstanceStartPermissionId,
            MiniAdminSeedIds.WorkflowTaskApprovePermissionId,
            MiniAdminSeedIds.TenantPackageMenuId,
            MiniAdminSeedIds.UserQueryPermissionId,
            MiniAdminSeedIds.UserCreatePermissionId,
            MiniAdminSeedIds.UserUpdatePermissionId,
            MiniAdminSeedIds.UserDeletePermissionId,
            MiniAdminSeedIds.UserUnlockPermissionId,
            MiniAdminSeedIds.UserResetPasswordPermissionId,
            MiniAdminSeedIds.UserImportPermissionId,
            MiniAdminSeedIds.UserExportPermissionId,
            MiniAdminSeedIds.SystemMenuId,
            MiniAdminSeedIds.UserManagementMenuId,
            MiniAdminSeedIds.FileManagementMenuId,
            MiniAdminSeedIds.FileQueryPermissionId,
            MiniAdminSeedIds.FileUploadPermissionId,
            MiniAdminSeedIds.FileDownloadPermissionId,
            MiniAdminSeedIds.FileDeletePermissionId,
            MiniAdminSeedIds.FileMarkInvalidPermissionId,
            MiniAdminSeedIds.RoleManagementMenuId,
            MiniAdminSeedIds.RoleQueryPermissionId,
            MiniAdminSeedIds.RoleCreatePermissionId,
            MiniAdminSeedIds.RoleUpdatePermissionId,
            MiniAdminSeedIds.RoleDeletePermissionId,
            MiniAdminSeedIds.RoleAssignPermissionId,
            MiniAdminSeedIds.MenuManagementMenuId,
            MiniAdminSeedIds.MenuQueryPermissionId,
            MiniAdminSeedIds.MenuCreatePermissionId,
            MiniAdminSeedIds.MenuUpdatePermissionId,
            MiniAdminSeedIds.MenuDeletePermissionId,
            MiniAdminSeedIds.DepartmentManagementMenuId,
            MiniAdminSeedIds.DepartmentQueryPermissionId,
            MiniAdminSeedIds.DepartmentCreatePermissionId,
            MiniAdminSeedIds.DepartmentUpdatePermissionId,
            MiniAdminSeedIds.DepartmentDeletePermissionId,
            MiniAdminSeedIds.PositionManagementMenuId,
            MiniAdminSeedIds.PositionQueryPermissionId,
            MiniAdminSeedIds.PositionCreatePermissionId,
            MiniAdminSeedIds.PositionUpdatePermissionId,
            MiniAdminSeedIds.PositionDeletePermissionId,
            MiniAdminSeedIds.PositionImportPermissionId,
            MiniAdminSeedIds.PositionExportPermissionId,
            MiniAdminSeedIds.DictionaryManagementMenuId,
            MiniAdminSeedIds.DictionaryQueryPermissionId,
            MiniAdminSeedIds.DictionaryCreatePermissionId,
            MiniAdminSeedIds.DictionaryUpdatePermissionId,
            MiniAdminSeedIds.DictionaryDeletePermissionId,
            MiniAdminSeedIds.ParameterSettingMenuId,
            MiniAdminSeedIds.ParameterQueryPermissionId,
            MiniAdminSeedIds.ParameterCreatePermissionId,
            MiniAdminSeedIds.ParameterUpdatePermissionId,
            MiniAdminSeedIds.ParameterDeletePermissionId,
            MiniAdminSeedIds.NoticeAnnouncementMenuId,
            MiniAdminSeedIds.NoticeQueryPermissionId,
            MiniAdminSeedIds.NoticeCreatePermissionId,
            MiniAdminSeedIds.NoticeUpdatePermissionId,
            MiniAdminSeedIds.NoticeDeletePermissionId,
            MiniAdminSeedIds.LogManagementMenuId,
            MiniAdminSeedIds.LogQueryPermissionId,
            MiniAdminSeedIds.LogExportPermissionId,
            MiniAdminSeedIds.LoginLogMenuId,
            MiniAdminSeedIds.LoginLogQueryPermissionId,
            MiniAdminSeedIds.SystemMonitorDashboardMenuId,
            MiniAdminSeedIds.SystemMonitorDashboardQueryPermissionId,
            MiniAdminSeedIds.AlertCenterMenuId,
            MiniAdminSeedIds.AlertQueryPermissionId,
            MiniAdminSeedIds.AlertAcknowledgePermissionId,
            MiniAdminSeedIds.AlertRuleMenuId,
            MiniAdminSeedIds.AlertRuleQueryPermissionId,
            MiniAdminSeedIds.AlertRuleUpdatePermissionId,
            MiniAdminSeedIds.SecurityCenterMenuId,
            MiniAdminSeedIds.SecurityCenterQueryPermissionId,
            MiniAdminSeedIds.SecurityEventQueryPermissionId,
            MiniAdminSeedIds.SecurityPolicyQueryPermissionId,
            MiniAdminSeedIds.SecurityPolicyUpdatePermissionId,
            MiniAdminSeedIds.NotificationCenterMenuId,
            MiniAdminSeedIds.NotificationQueryPermissionId,
            MiniAdminSeedIds.OnlineUserMenuId,
            MiniAdminSeedIds.OnlineUserQueryPermissionId,
            MiniAdminSeedIds.OnlineUserForceLogoutPermissionId,
            MiniAdminSeedIds.PermissionDiagnosticsMenuId,
            MiniAdminSeedIds.PermissionDiagnosticsQueryPermissionId,
            MiniAdminSeedIds.PermissionDiagnosticsRefreshCachePermissionId,
            MiniAdminSeedIds.ScheduledJobMenuId,
            MiniAdminSeedIds.ScheduledJobQueryPermissionId,
            MiniAdminSeedIds.ScheduledJobUpdatePermissionId,
            MiniAdminSeedIds.ScheduledJobRunPermissionId
        ];
    }

    private static IReadOnlyList<Guid> GetTenantAdminMenuIds()
    {
        return
        [
            MiniAdminSeedIds.DashboardMenuId,
            MiniAdminSeedIds.AnalyticsMenuId,
            MiniAdminSeedIds.WorkspaceMenuId,
            MiniAdminSeedIds.SystemMenuId,
            MiniAdminSeedIds.UserManagementMenuId,
            MiniAdminSeedIds.UserQueryPermissionId,
            MiniAdminSeedIds.UserCreatePermissionId,
            MiniAdminSeedIds.UserUpdatePermissionId,
            MiniAdminSeedIds.UserDeletePermissionId,
            MiniAdminSeedIds.RoleManagementMenuId,
            MiniAdminSeedIds.RoleQueryPermissionId,
            MiniAdminSeedIds.RoleCreatePermissionId,
            MiniAdminSeedIds.RoleUpdatePermissionId,
            MiniAdminSeedIds.RoleDeletePermissionId,
            MiniAdminSeedIds.RoleAssignPermissionId,
            MiniAdminSeedIds.DepartmentManagementMenuId,
            MiniAdminSeedIds.DepartmentQueryPermissionId,
            MiniAdminSeedIds.DepartmentCreatePermissionId,
            MiniAdminSeedIds.DepartmentUpdatePermissionId,
            MiniAdminSeedIds.DepartmentDeletePermissionId,
            MiniAdminSeedIds.PositionManagementMenuId,
            MiniAdminSeedIds.PositionQueryPermissionId,
            MiniAdminSeedIds.PositionCreatePermissionId,
            MiniAdminSeedIds.PositionUpdatePermissionId,
            MiniAdminSeedIds.PositionDeletePermissionId,
            MiniAdminSeedIds.PositionImportPermissionId,
            MiniAdminSeedIds.PositionExportPermissionId,
            MiniAdminSeedIds.WorkflowManagementMenuId,
            MiniAdminSeedIds.WorkflowCenterMenuId,
            MiniAdminSeedIds.WorkflowCenterQueryPermissionId,
            MiniAdminSeedIds.WorkflowDefinitionManagePermissionId,
            MiniAdminSeedIds.WorkflowInstanceStartPermissionId,
            MiniAdminSeedIds.WorkflowTaskApprovePermissionId
        ];
    }

    private async Task RefreshSeedUserAuthorizationCacheAsync(CancellationToken cancellationToken)
    {
        await userAuthorizationCache.RemoveUserAsync(
            MiniAdminSeedIds.AdminUserId,
            "admin",
            cancellationToken);
        await userAuthorizationCache.RemoveUserAsync(
            MiniAdminSeedIds.DemoUserId,
            "demo",
            cancellationToken);
        await userAuthorizationCache.RemoveUserAsync(
            MiniAdminSeedIds.AuditorUserId,
            "auditor",
            cancellationToken);
    }

    private async Task RefreshTenantAdminAuthorizationCacheAsync(CancellationToken cancellationToken)
    {
        var tenantAdminUsers = await dbContext.UserRoles
            .AsNoTracking()
            .Where(x => x.RoleId == MiniAdminSeedIds.TenantAdminRoleId)
            .Select(x => new { x.User.Id, x.User.UserName })
            .ToArrayAsync(cancellationToken);

        foreach (var user in tenantAdminUsers)
        {
            await userAuthorizationCache.RemoveUserAsync(
                user.Id,
                user.UserName,
                cancellationToken);
        }
    }
}
