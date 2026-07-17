using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.AuditLogs;
using MiniAdmin.Domain.Entities;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class MiniAdminDbContext(
    DbContextOptions<MiniAdminDbContext> options,
    IAuditEntityChangeCollector? auditEntityChangeCollector = null) : DbContext(options)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private static readonly HashSet<Type> IgnoredAuditEntityTypes =
    [
        typeof(AuditLog),
        typeof(AuditEntityChange),
        typeof(LoginLog),
        typeof(OnlineUser),
        typeof(SecurityEvent),
        typeof(UserNotification),
        typeof(ChatConversation),
        typeof(ChatMessage),
        typeof(NotificationDelivery),
        typeof(NotificationSubscription),
        typeof(TenantResourceQuotaWarning),
        typeof(TenantLifecycleRecord),
        typeof(OutboxMessage),
        typeof(InboxMessage)
    ];

    private static readonly HashSet<string> SensitivePropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Password",
        "PasswordHash",
        "SecurityStamp",
        "Token",
        "AccessToken",
        "RefreshToken",
        "Secret",
        "SecretCiphertext",
        "SigningKey"
    };

    public DbSet<User> Users => Set<User>();

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<TenantPackage> TenantPackages => Set<TenantPackage>();

    public DbSet<TenantResourceQuotaWarning> TenantResourceQuotaWarnings =>
        Set<TenantResourceQuotaWarning>();

    public DbSet<TenantLifecycleRecord> TenantLifecycleRecords => Set<TenantLifecycleRecord>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<AbacPolicy> AbacPolicies => Set<AbacPolicy>();

    public DbSet<Menu> Menus => Set<Menu>();

    public DbSet<Department> Departments => Set<Department>();

    public DbSet<Position> Positions => Set<Position>();

    public DbSet<Notice> Notices => Set<Notice>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<AuditEntityChange> AuditEntityChanges => Set<AuditEntityChange>();

    public DbSet<LoginLog> LoginLogs => Set<LoginLog>();

    public DbSet<OnlineUser> OnlineUsers => Set<OnlineUser>();

    public DbSet<SecurityEvent> SecurityEvents => Set<SecurityEvent>();

    public DbSet<ManagedFile> ManagedFiles => Set<ManagedFile>();

    public DbSet<DictionaryType> DictionaryTypes => Set<DictionaryType>();

    public DbSet<DictionaryItem> DictionaryItems => Set<DictionaryItem>();

    public DbSet<SystemParameter> SystemParameters => Set<SystemParameter>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<RoleMenu> RoleMenus => Set<RoleMenu>();

    public DbSet<DataSeedVersion> DataSeedVersions => Set<DataSeedVersion>();

    public DbSet<ScheduledJob> ScheduledJobs => Set<ScheduledJob>();

    public DbSet<ScheduledJobLog> ScheduledJobLogs => Set<ScheduledJobLog>();

    public DbSet<ScheduledJobLogDetail> ScheduledJobLogDetails => Set<ScheduledJobLogDetail>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();

    public DbSet<Alert> Alerts => Set<Alert>();

    public DbSet<AlertRule> AlertRules => Set<AlertRule>();

    public DbSet<AlertRuleRecipient> AlertRuleRecipients => Set<AlertRuleRecipient>();

    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

    public DbSet<ChatConversation> ChatConversations => Set<ChatConversation>();

    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    public DbSet<OpenApiCredential> OpenApiCredentials => Set<OpenApiCredential>();

    public DbSet<OpenApiNonce> OpenApiNonces => Set<OpenApiNonce>();

    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();

    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();

    public DbSet<NotificationPolicy> NotificationPolicies => Set<NotificationPolicy>();

    public DbSet<NotificationSubscription> NotificationSubscriptions => Set<NotificationSubscription>();

    public DbSet<CodeGenerationHistory> CodeGenerationHistories => Set<CodeGenerationHistory>();

    public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();

    public DbSet<WorkflowNode> WorkflowNodes => Set<WorkflowNode>();

    public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();

    public DbSet<WorkflowTask> WorkflowTasks => Set<WorkflowTask>();

    public DbSet<WorkflowActionLog> WorkflowActionLogs => Set<WorkflowActionLog>();

    public DbSet<WorkflowCcRecord> WorkflowCcRecords => Set<WorkflowCcRecord>();

    public DbSet<WorkflowAttachment> WorkflowAttachments => Set<WorkflowAttachment>();

    public DbSet<WorkflowComment> WorkflowComments => Set<WorkflowComment>();

    public DbSet<WorkflowBusinessBinding> WorkflowBusinessBindings => Set<WorkflowBusinessBinding>();

    public override int SaveChanges()
    {
        var capturedChanges = CaptureEntityChanges();
        var result = base.SaveChanges();
        auditEntityChangeCollector?.AddRange(capturedChanges);
        return result;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var capturedChanges = CaptureEntityChanges();
        var result = await base.SaveChangesAsync(cancellationToken);
        auditEntityChangeCollector?.AddRange(capturedChanges);
        return result;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("mini_users");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserName).IsUnique();
            entity.HasIndex(x => x.TenantId);
            entity.Property(x => x.UserName).HasMaxLength(64).IsRequired();
            entity.Property(x => x.RealName).HasMaxLength(64).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
            entity.Property(x => x.SecurityStamp).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(256);
            entity.HasOne(x => x.Department)
                .WithMany()
                .HasForeignKey(x => x.DepartmentId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Position)
                .WithMany()
                .HasForeignKey(x => x.PositionId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("mini_tenants");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(x => x.InitializationTemplateCode).HasMaxLength(64).IsRequired();
            entity.Property(x => x.InitializationStatus).HasMaxLength(32).IsRequired();
            entity.Property(x => x.InitializationError).HasMaxLength(512);
            entity.Property(x => x.ContactName).HasMaxLength(64);
            entity.Property(x => x.ContactPhone).HasMaxLength(32);
            entity.Property(x => x.ContactEmail).HasMaxLength(256);
            entity.Property(x => x.Remark).HasMaxLength(512);
            entity.HasOne(x => x.Package)
                .WithMany()
                .HasForeignKey(x => x.PackageId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TenantPackage>(entity =>
        {
            entity.ToTable("mini_tenant_packages");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Name).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(128).IsRequired();
            entity.Property(x => x.MenuIds).HasColumnType("longtext").IsRequired();
            entity.Property(x => x.Remark).HasMaxLength(512);
        });

        modelBuilder.Entity<TenantResourceQuotaWarning>(entity =>
        {
            entity.ToTable("mini_tenant_resource_quota_warnings");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.ResourceType }).IsUnique();
            entity.HasIndex(x => x.LastCheckedAt);
            entity.Property(x => x.ResourceType).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.LastNotifiedStatus).HasMaxLength(32);
        });

        modelBuilder.Entity<TenantLifecycleRecord>(entity =>
        {
            entity.ToTable("mini_tenant_lifecycle_records");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.CreatedAt });
            entity.HasIndex(x => x.DeduplicationKey).IsUnique();
            entity.Property(x => x.EventType).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Source).HasMaxLength(32).IsRequired();
            entity.Property(x => x.OperatorUserName).HasMaxLength(64);
            entity.Property(x => x.FromStatus).HasMaxLength(32);
            entity.Property(x => x.ToStatus).HasMaxLength(32);
            entity.Property(x => x.Description).HasMaxLength(512).IsRequired();
            entity.Property(x => x.DeduplicationKey).HasMaxLength(128);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("mini_roles");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(128).IsRequired();
            entity.Property(x => x.DataScope).HasMaxLength(32).IsRequired();
            entity.Property(x => x.CustomDepartmentIds).HasColumnType("longtext");
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AbacPolicy>(entity =>
        {
            entity.ToTable("mini_abac_policies");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.Resource, x.Action, x.IsEnabled });
            entity.HasIndex(x => new { x.SubjectType, x.SubjectId });
            entity.Property(x => x.Name).HasMaxLength(128).IsRequired();
            entity.Property(x => x.SubjectType).HasMaxLength(16).IsRequired();
            entity.Property(x => x.SubjectId).HasMaxLength(128);
            entity.Property(x => x.Resource).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Action).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Effect).HasMaxLength(16).IsRequired();
            entity.Property(x => x.ConditionsJson).HasColumnType("longtext").IsRequired();
            entity.Property(x => x.Description).HasMaxLength(512);
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Menu>(entity =>
        {
            entity.ToTable("mini_menus");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Path).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Component).HasMaxLength(256);
            entity.Property(x => x.Redirect).HasMaxLength(256);
            entity.Property(x => x.Title).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Icon).HasMaxLength(128);
            entity.Property(x => x.PermissionCode).HasMaxLength(128);
            entity.Property(x => x.IsVisible).HasDefaultValue(true);
            entity.HasOne(x => x.Parent)
                .WithMany(x => x.Children)
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Department>(entity =>
        {
            entity.ToTable("mini_departments");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Leader).HasMaxLength(64);
            entity.Property(x => x.Phone).HasMaxLength(32);
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Parent)
                .WithMany(x => x.Children)
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Position>(entity =>
        {
            entity.ToTable("mini_positions");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Remark).HasMaxLength(512);
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Notice>(entity =>
        {
            entity.ToTable("mini_notices");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Type).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Content).HasMaxLength(4000).IsRequired();
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("mini_audit_logs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UserId).HasMaxLength(64);
            entity.Property(x => x.UserName).HasMaxLength(64);
            entity.Property(x => x.Method).HasMaxLength(16).IsRequired();
            entity.Property(x => x.Path).HasMaxLength(256).IsRequired();
            entity.Property(x => x.QueryString).HasMaxLength(1024);
            entity.Property(x => x.Module).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Action).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ResourceId).HasMaxLength(64);
            entity.Property(x => x.IpAddress).HasMaxLength(64);
            entity.Property(x => x.UserAgent).HasMaxLength(512);
            entity.Property(x => x.RequestBody).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.ErrorMessage).HasMaxLength(1024);
        });

        modelBuilder.Entity<AuditEntityChange>(entity =>
        {
            entity.ToTable("mini_audit_entity_changes");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.AuditLogId);
            entity.Property(x => x.EntityName).HasMaxLength(64).IsRequired();
            entity.Property(x => x.EntityId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.OperationType).HasMaxLength(16).IsRequired();
            entity.Property(x => x.BeforeJson).HasColumnType("longtext");
            entity.Property(x => x.AfterJson).HasColumnType("longtext");
            entity.Property(x => x.DiffJson).HasColumnType("longtext").IsRequired();
            entity.HasOne(x => x.AuditLog)
                .WithMany(x => x.EntityChanges)
                .HasForeignKey(x => x.AuditLogId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LoginLog>(entity =>
        {
            entity.ToTable("mini_login_logs");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.CreatedAt);
            entity.Property(x => x.UserId)
                .HasConversion<string>()
                .HasColumnType("char(64)")
                .HasMaxLength(64);
            entity.Property(x => x.UserName).HasMaxLength(64).IsRequired();
            entity.Property(x => x.RealName).HasMaxLength(64);
            entity.Property(x => x.IpAddress).HasMaxLength(64);
            entity.Property(x => x.UserAgent).HasMaxLength(512);
            entity.Property(x => x.Message).HasMaxLength(256).IsRequired();
        });

        modelBuilder.Entity<OnlineUser>(entity =>
        {
            entity.ToTable("mini_online_users");
            entity.HasKey(x => x.SessionId);
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.UserName);
            entity.Property(x => x.SessionId)
                .HasColumnType("char(36)")
                .HasMaxLength(36);
            entity.Property(x => x.UserId)
                .HasColumnType("char(36)")
                .HasMaxLength(36);
            entity.Property(x => x.UserName).HasMaxLength(64).IsRequired();
            entity.Property(x => x.RealName).HasMaxLength(64).IsRequired();
            entity.Property(x => x.IpAddress).HasMaxLength(64);
            entity.Property(x => x.UserAgent).HasMaxLength(512);
            entity.Property(x => x.DeviceName).HasMaxLength(64);
            entity.Property(x => x.BrowserName).HasMaxLength(64);
        });

        modelBuilder.Entity<SecurityEvent>(entity =>
        {
            entity.ToTable("mini_security_events");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => x.EventType);
            entity.HasIndex(x => x.UserName);
            entity.Property(x => x.EventType).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Level).HasMaxLength(32).IsRequired();
            entity.Property(x => x.UserId)
                .HasConversion<string>()
                .HasColumnType("char(64)")
                .HasMaxLength(64);
            entity.Property(x => x.UserName).HasMaxLength(64);
            entity.Property(x => x.IpAddress).HasMaxLength(64);
            entity.Property(x => x.UserAgent).HasMaxLength(512);
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(1024).IsRequired();
            entity.Property(x => x.RelatedEntityType).HasMaxLength(64);
            entity.Property(x => x.RelatedEntityId).HasMaxLength(128);
        });

        modelBuilder.Entity<ManagedFile>(entity =>
        {
            entity.ToTable("mini_files");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => x.TenantId);
            entity.Property(x => x.OriginalName).HasMaxLength(255).IsRequired();
            entity.Property(x => x.StoredName).HasMaxLength(255).IsRequired();
            entity.Property(x => x.ContentType).HasMaxLength(128).IsRequired();
            entity.Property(x => x.StorageProvider).HasMaxLength(32).IsRequired();
            entity.Property(x => x.StoragePath).HasMaxLength(512).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
        });

        modelBuilder.Entity<DictionaryType>(entity =>
        {
            entity.ToTable("mini_dictionary_types");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(128).IsRequired();
        });

        modelBuilder.Entity<DictionaryItem>(entity =>
        {
            entity.ToTable("mini_dictionary_items");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TypeId, x.Value }).IsUnique();
            entity.Property(x => x.Label).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Value).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Color).HasMaxLength(32);
            entity.HasOne(x => x.Type)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.TypeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SystemParameter>(entity =>
        {
            entity.ToTable("mini_system_parameters");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Key).IsUnique();
            entity.Property(x => x.Key).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Value).HasMaxLength(1024).IsRequired();
            entity.Property(x => x.Group).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Remark).HasMaxLength(512);
        });

        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.ToTable("mini_user_roles");
            entity.HasKey(x => new { x.UserId, x.RoleId });
            entity.HasOne(x => x.User).WithMany(x => x.UserRoles).HasForeignKey(x => x.UserId);
            entity.HasOne(x => x.Role).WithMany(x => x.UserRoles).HasForeignKey(x => x.RoleId);
        });

        modelBuilder.Entity<RoleMenu>(entity =>
        {
            entity.ToTable("mini_role_menus");
            entity.HasKey(x => new { x.RoleId, x.MenuId });
            entity.HasOne(x => x.Role).WithMany(x => x.RoleMenus).HasForeignKey(x => x.RoleId);
            entity.HasOne(x => x.Menu).WithMany(x => x.RoleMenus).HasForeignKey(x => x.MenuId);
        });

        modelBuilder.Entity<DataSeedVersion>(entity =>
        {
            entity.ToTable("mini_data_seed_versions");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Version).IsUnique();
            entity.Property(x => x.Version).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(128).IsRequired();
        });

        modelBuilder.Entity<ScheduledJob>(entity =>
        {
            entity.ToTable("mini_scheduled_jobs");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.JobKey).IsUnique();
            entity.HasIndex(x => x.NextRunAt);
            entity.Property(x => x.JobKey).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(512);
            entity.Property(x => x.LastStatus).HasMaxLength(32).IsRequired();
            entity.Property(x => x.LastMessage).HasMaxLength(1024);
            entity.Property(x => x.LeaseOwner).HasMaxLength(128);
            entity.HasIndex(x => x.LeaseExpiresAt);
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("mini_outbox_messages");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.Status, x.NextAttemptAt });
            entity.HasIndex(x => x.LeaseExpiresAt);
            entity.HasIndex(x => x.CreatedAt);
            entity.Property(x => x.EventType).HasMaxLength(512).IsRequired();
            entity.Property(x => x.Payload).HasColumnType("longtext").IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.CorrelationId).HasMaxLength(128);
            entity.Property(x => x.LeaseOwner).HasMaxLength(128);
            entity.Property(x => x.LastError).HasMaxLength(4000);
        });

        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.ToTable("mini_inbox_messages");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.MessageId, x.ConsumerName }).IsUnique();
            entity.HasIndex(x => x.ProcessedAt);
            entity.Property(x => x.ConsumerName).HasMaxLength(512).IsRequired();
        });

        modelBuilder.Entity<ScheduledJobLog>(entity =>
        {
            entity.ToTable("mini_scheduled_job_logs");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.JobId);
            entity.HasIndex(x => x.StartedAt);
            entity.Property(x => x.JobKey).HasMaxLength(64).IsRequired();
            entity.Property(x => x.JobName).HasMaxLength(128).IsRequired();
            entity.Property(x => x.TriggerType).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(1024).IsRequired();
            entity.HasOne(x => x.Job)
                .WithMany(x => x.Logs)
                .HasForeignKey(x => x.JobId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ScheduledJobLogDetail>(entity =>
        {
            entity.ToTable("mini_scheduled_job_log_details");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.LogId);
            entity.HasIndex(x => x.JobId);
            entity.Property(x => x.JobKey).HasMaxLength(64).IsRequired();
            entity.Property(x => x.DetailType).HasMaxLength(64).IsRequired();
            entity.Property(x => x.TargetType).HasMaxLength(64).IsRequired();
            entity.Property(x => x.TargetId).HasMaxLength(64);
            entity.Property(x => x.TargetName).HasMaxLength(256);
            entity.Property(x => x.StorageProvider).HasMaxLength(32);
            entity.Property(x => x.StoragePath).HasMaxLength(512);
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(1024).IsRequired();
            entity.HasOne(x => x.Log)
                .WithMany(x => x.Details)
                .HasForeignKey(x => x.LogId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Alert>(entity =>
        {
            entity.ToTable("mini_alerts");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.Type, x.Source, x.Status });
            entity.HasIndex(x => x.LastTriggeredAt);
            entity.Property(x => x.Type).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Level).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Content).HasMaxLength(1024).IsRequired();
            entity.Property(x => x.Source).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.AcknowledgedBy).HasMaxLength(64);
            entity.Property(x => x.AcknowledgeRemark).HasMaxLength(512);
        });

        modelBuilder.Entity<AlertRule>(entity =>
        {
            entity.ToTable("mini_alert_rules");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(512).IsRequired();
            entity.Property(x => x.Metric).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Operator).HasMaxLength(16).IsRequired();
            entity.Property(x => x.Threshold).HasPrecision(18, 2);
            entity.Property(x => x.Level).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Remark).HasMaxLength(512);
        });

        modelBuilder.Entity<AlertRuleRecipient>(entity =>
        {
            entity.ToTable("mini_alert_rule_recipients");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.AlertRuleId, x.RecipientType, x.RecipientId }).IsUnique();
            entity.Property(x => x.RecipientType).HasMaxLength(16).IsRequired();
            entity.HasOne(x => x.AlertRule)
                .WithMany(x => x.Recipients)
                .HasForeignKey(x => x.AlertRuleId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserNotification>(entity =>
        {
            entity.ToTable("mini_user_notifications");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => new { x.UserId, x.SourceType, x.SourceId }).IsUnique();
            entity.HasIndex(x => x.CreatedAt);
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Message).HasMaxLength(1024).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Level).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Link).HasMaxLength(256);
            entity.Property(x => x.SourceType).HasMaxLength(64).IsRequired();
            entity.Property(x => x.SourceId).HasMaxLength(64).IsRequired();
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatConversation>(entity =>
        {
            entity.ToTable("mini_chat_conversations");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new
            {
                x.TenantScopeKey,
                x.ParticipantOneId,
                x.ParticipantTwoId
            }).IsUnique();
            entity.HasIndex(x => x.UpdatedAt);
            entity.Property(x => x.TenantScopeKey).HasMaxLength(64).IsRequired();
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ParticipantOne)
                .WithMany()
                .HasForeignKey(x => x.ParticipantOneId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ParticipantTwo)
                .WithMany()
                .HasForeignKey(x => x.ParticipantTwoId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("mini_chat_messages");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ConversationId, x.CreatedAt });
            entity.HasIndex(x => new { x.ReceiverId, x.ReadAt });
            entity.Property(x => x.Content).HasMaxLength(2000).IsRequired();
            entity.HasOne(x => x.Conversation)
                .WithMany(x => x.Messages)
                .HasForeignKey(x => x.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Sender)
                .WithMany()
                .HasForeignKey(x => x.SenderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Receiver)
                .WithMany()
                .HasForeignKey(x => x.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OpenApiCredential>(entity =>
        {
            entity.ToTable("mini_openapi_credentials");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.AppKey).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.IsEnabled });
            entity.Property(x => x.Name).HasMaxLength(128).IsRequired();
            entity.Property(x => x.AppKey).HasMaxLength(64).IsRequired();
            entity.Property(x => x.SecretCiphertext).HasMaxLength(1024).IsRequired();
            entity.Property(x => x.PermissionsJson).HasColumnType("longtext").IsRequired();
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OpenApiNonce>(entity =>
        {
            entity.ToTable("mini_openapi_nonces");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.CredentialId, x.Nonce }).IsUnique();
            entity.HasIndex(x => x.ExpiresAt);
            entity.Property(x => x.Nonce).HasMaxLength(128).IsRequired();
            entity.HasOne(x => x.Credential)
                .WithMany()
                .HasForeignKey(x => x.CredentialId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NotificationDelivery>(entity =>
        {
            entity.ToTable("mini_notification_deliveries");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.Channel, x.SourceType, x.SourceId, x.UserId }).IsUnique();
            entity.HasIndex(x => x.CreatedAt);
            entity.Property(x => x.Channel).HasMaxLength(32).IsRequired();
            entity.Property(x => x.RecipientAddress).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Content).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.SourceType).HasMaxLength(64).IsRequired();
            entity.Property(x => x.SourceId).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.ErrorMessage).HasMaxLength(1024);
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NotificationTemplate>(entity =>
        {
            entity.ToTable("mini_notification_templates");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            entity.HasIndex(x => x.Category);
            entity.Property(x => x.Code).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Level).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Channel).HasMaxLength(32);
            entity.Property(x => x.TitleTemplate).HasMaxLength(200).IsRequired();
            entity.Property(x => x.MessageTemplate).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.LinkTemplate).HasMaxLength(256);
            entity.Property(x => x.Remark).HasMaxLength(512);
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<NotificationPolicy>(entity =>
        {
            entity.ToTable("mini_notification_policies");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.EventCode).IsUnique();
            entity.HasIndex(x => x.Category);
            entity.Property(x => x.EventCode).HasMaxLength(64).IsRequired();
            entity.Property(x => x.EventName).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(64).IsRequired();
            entity.Property(x => x.RecipientStrategy).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Remark).HasMaxLength(512);
        });

        modelBuilder.Entity<NotificationSubscription>(entity =>
        {
            entity.ToTable("mini_notification_subscriptions");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.UserId, x.EventCode }).IsUnique();
            entity.HasIndex(x => x.EventCode);
            entity.Property(x => x.EventCode).HasMaxLength(64).IsRequired();
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CodeGenerationHistory>(entity =>
        {
            entity.ToTable("mini_code_generation_histories");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => x.ModuleName);
            entity.Property(x => x.TableName).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ModuleName).HasMaxLength(128).IsRequired();
            entity.Property(x => x.BusinessName).HasMaxLength(128).IsRequired();
            entity.Property(x => x.PermissionPrefix).HasMaxLength(128).IsRequired();
            entity.Property(x => x.TenantMode).HasMaxLength(32).IsRequired();
            entity.Property(x => x.RequestJson).HasColumnType("longtext").IsRequired();
            entity.Property(x => x.FilesJson).HasColumnType("longtext").IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.ErrorMessage).HasMaxLength(1024);
            entity.Property(x => x.OperatorUserName).HasMaxLength(64);
        });

        modelBuilder.Entity<WorkflowDefinition>(entity =>
        {
            entity.ToTable("mini_workflow_definitions");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.Code, x.Version }).IsUnique();
            entity.Property(x => x.Code).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(128).IsRequired();
            entity.Property(x => x.FormName).HasMaxLength(128);
            entity.Property(x => x.Description).HasMaxLength(512);
            entity.Property(x => x.DesignerJson).HasColumnType("longtext").IsRequired();
            entity.Property(x => x.FormSchemaJson).HasColumnType("longtext").IsRequired();
            entity.Property(x => x.PublishStatus).HasMaxLength(32).IsRequired();
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkflowNode>(entity =>
        {
            entity.ToTable("mini_workflow_nodes");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.DefinitionId);
            entity.Property(x => x.Name).HasMaxLength(128).IsRequired();
            entity.Property(x => x.DesignerNodeId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.NodeType).HasMaxLength(32).IsRequired();
            entity.Property(x => x.ApprovalMode).HasMaxLength(32).HasDefaultValue("Any").IsRequired();
            entity.Property(x => x.SlaMinutes);
            entity.Property(x => x.ApproverType).HasMaxLength(32).IsRequired();
            entity.HasOne(x => x.Definition)
                .WithMany(x => x.Nodes)
                .HasForeignKey(x => x.DefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ApproverUser)
                .WithMany()
                .HasForeignKey(x => x.ApproverUserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.ApproverRole)
                .WithMany()
                .HasForeignKey(x => x.ApproverRoleId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<WorkflowInstance>(entity =>
        {
            entity.ToTable("mini_workflow_instances");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TenantId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.InitiatorUserId);
            entity.Property(x => x.DefinitionCode).HasMaxLength(64).IsRequired();
            entity.Property(x => x.DefinitionName).HasMaxLength(128).IsRequired();
            entity.Property(x => x.DefinitionSnapshotJson).HasColumnType("longtext").IsRequired();
            entity.Property(x => x.Title).HasMaxLength(200).IsRequired();
            entity.Property(x => x.BusinessKey).HasMaxLength(128);
            entity.Property(x => x.FormDataJson).HasColumnType("longtext").IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.CurrentNodeName).HasMaxLength(128);
            entity.Property(x => x.InitiatorUserName).HasMaxLength(64).IsRequired();
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Definition)
                .WithMany()
                .HasForeignKey(x => x.DefinitionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.InitiatorUser)
                .WithMany()
                .HasForeignKey(x => x.InitiatorUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkflowTask>(entity =>
        {
            entity.ToTable("mini_workflow_tasks");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.InstanceId);
            entity.HasIndex(x => new { x.ApproverUserId, x.Status });
            entity.Property(x => x.NodeName).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ApproverUserName).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Comment).HasMaxLength(512);
            entity.Property(x => x.DueAt);
            entity.Property(x => x.LastAutoRemindedAt);
            entity.HasOne(x => x.Instance)
                .WithMany(x => x.Tasks)
                .HasForeignKey(x => x.InstanceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Node)
                .WithMany()
                .HasForeignKey(x => x.NodeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ApproverUser)
                .WithMany()
                .HasForeignKey(x => x.ApproverUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkflowActionLog>(entity =>
        {
            entity.ToTable("mini_workflow_action_logs");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.InstanceId);
            entity.Property(x => x.NodeName).HasMaxLength(128);
            entity.Property(x => x.Action).HasMaxLength(32).IsRequired();
            entity.Property(x => x.OperatorUserName).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Comment).HasMaxLength(512);
            entity.HasOne(x => x.Instance)
                .WithMany(x => x.ActionLogs)
                .HasForeignKey(x => x.InstanceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkflowCcRecord>(entity =>
        {
            entity.ToTable("mini_workflow_cc_records");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.InstanceId);
            entity.HasIndex(x => new { x.RecipientUserId, x.ReadAt });
            entity.HasIndex(x => new { x.InstanceId, x.NodeId, x.RecipientUserId }).IsUnique();
            entity.Property(x => x.NodeName).HasMaxLength(128);
            entity.Property(x => x.RecipientUserName).HasMaxLength(64).IsRequired();
            entity.Property(x => x.SenderUserName).HasMaxLength(64);
            entity.HasOne(x => x.Instance)
                .WithMany(x => x.CcRecords)
                .HasForeignKey(x => x.InstanceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.RecipientUser)
                .WithMany()
                .HasForeignKey(x => x.RecipientUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkflowAttachment>(entity =>
        {
            entity.ToTable("mini_workflow_attachments");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.InstanceId);
            entity.HasIndex(x => new { x.InstanceId, x.FileId }).IsUnique();
            entity.Property(x => x.Remark).HasMaxLength(512);
            entity.Property(x => x.UploaderUserName).HasMaxLength(64).IsRequired();
            entity.HasOne(x => x.Instance)
                .WithMany(x => x.Attachments)
                .HasForeignKey(x => x.InstanceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.File)
                .WithMany()
                .HasForeignKey(x => x.FileId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<WorkflowComment>(entity =>
        {
            entity.ToTable("mini_workflow_comments");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.InstanceId);
            entity.Property(x => x.Content).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.AuthorUserName).HasMaxLength(64).IsRequired();
            entity.HasOne(x => x.Instance)
                .WithMany(x => x.Comments)
                .HasForeignKey(x => x.InstanceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkflowBusinessBinding>(entity =>
        {
            entity.ToTable("mini_workflow_business_bindings");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.BusinessType }).IsUnique();
            entity.HasIndex(x => x.DefinitionId);
            entity.Property(x => x.BusinessType).HasMaxLength(64).IsRequired();
            entity.Property(x => x.BusinessName).HasMaxLength(128).IsRequired();
            entity.Property(x => x.Remark).HasMaxLength(512);
            entity.HasOne(x => x.Tenant)
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Definition)
                .WithMany()
                .HasForeignKey(x => x.DefinitionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MiniAdminDbContext).Assembly);
    }

    private IReadOnlyList<CapturedAuditEntityChange> CaptureEntityChanges()
    {
        if (auditEntityChangeCollector?.IsEnabled != true)
        {
            return Array.Empty<CapturedAuditEntityChange>();
        }

        ChangeTracker.DetectChanges();

        return ChangeTracker
            .Entries()
            .Where(ShouldCaptureEntry)
            .Select(CreateCapturedChange)
            .Where(change => change is not null)
            .Select(change => change!)
            .ToArray();
    }

    private static bool ShouldCaptureEntry(EntityEntry entry)
    {
        return entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted &&
               !IgnoredAuditEntityTypes.Contains(entry.Entity.GetType()) &&
               (entry.State != EntityState.Modified || HasRealModifiedProperties(entry));
    }

    private static bool HasRealModifiedProperties(EntityEntry entry)
    {
        return entry.Properties.Any(property =>
            property.IsModified &&
            !Equals(property.OriginalValue, property.CurrentValue));
    }

    private static CapturedAuditEntityChange? CreateCapturedChange(EntityEntry entry)
    {
        var operationType = entry.State switch
        {
            EntityState.Added => "Create",
            EntityState.Modified => "Update",
            EntityState.Deleted => "Delete",
            _ => null
        };

        if (operationType is null)
        {
            return null;
        }

        var beforeValues = entry.State == EntityState.Added
            ? null
            : GetPropertyValues(entry, useOriginalValues: true);
        var afterValues = entry.State == EntityState.Deleted
            ? null
            : GetPropertyValues(entry, useOriginalValues: false);
        var diffValues = GetDiffValues(entry, beforeValues, afterValues);

        return new CapturedAuditEntityChange(
            entry.Metadata.ClrType.Name,
            GetEntityId(entry),
            operationType,
            beforeValues is null ? null : JsonSerializer.Serialize(beforeValues, JsonOptions),
            afterValues is null ? null : JsonSerializer.Serialize(afterValues, JsonOptions),
            JsonSerializer.Serialize(diffValues, JsonOptions),
            DateTimeOffset.UtcNow);
    }

    private static Dictionary<string, object?> GetPropertyValues(
        EntityEntry entry,
        bool useOriginalValues)
    {
        return entry.Properties
            .Where(property => !property.Metadata.IsShadowProperty())
            .OrderBy(property => property.Metadata.Name, StringComparer.Ordinal)
            .ToDictionary(
                property => property.Metadata.Name,
                property => GetAuditedPropertyValue(property, useOriginalValues));
    }

    private static Dictionary<string, object?> GetDiffValues(
        EntityEntry entry,
        IReadOnlyDictionary<string, object?>? beforeValues,
        IReadOnlyDictionary<string, object?>? afterValues)
    {
        if (entry.State == EntityState.Added)
        {
            return afterValues?.ToDictionary(
                item => item.Key,
                item => (object?)new
                {
                    Before = (object?)null,
                    After = item.Value
                }) ?? [];
        }

        if (entry.State == EntityState.Deleted)
        {
            return beforeValues?.ToDictionary(
                item => item.Key,
                item => (object?)new
                {
                    Before = item.Value,
                    After = (object?)null
                }) ?? [];
        }

        return entry.Properties
            .Where(property =>
                !property.Metadata.IsShadowProperty() &&
                property.IsModified &&
                !Equals(property.OriginalValue, property.CurrentValue))
            .OrderBy(property => property.Metadata.Name, StringComparer.Ordinal)
            .ToDictionary(
                property => property.Metadata.Name,
                property => (object?)new
                {
                    Before = GetAuditedPropertyValue(property, useOriginalValues: true),
                    After = GetAuditedPropertyValue(property, useOriginalValues: false)
                });
    }

    private static object? GetAuditedPropertyValue(
        PropertyEntry property,
        bool useOriginalValues)
    {
        if (SensitivePropertyNames.Contains(property.Metadata.Name))
        {
            return "***";
        }

        return useOriginalValues ? property.OriginalValue : property.CurrentValue;
    }

    private static string GetEntityId(EntityEntry entry)
    {
        var primaryKey = entry.Metadata.FindPrimaryKey();
        if (primaryKey is null)
        {
            return string.Empty;
        }

        return string.Join(
            ":",
            primaryKey.Properties.Select(property =>
            {
                var propertyEntry = entry.Property(property.Name);
                var value = entry.State == EntityState.Deleted
                    ? propertyEntry.OriginalValue
                    : propertyEntry.CurrentValue;
                return value?.ToString() ?? string.Empty;
            }));
    }
}
