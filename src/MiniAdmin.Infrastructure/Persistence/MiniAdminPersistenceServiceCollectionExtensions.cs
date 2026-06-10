using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using MiniAdmin.Application.Contracts.AuditLogs;
using MiniAdmin.Application.Contracts.Alerts;
using MiniAdmin.Application.Contracts.Auth;
using MiniAdmin.Application.Contracts.Caching;
using MiniAdmin.Application.Contracts.CodeGenerators;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.DataScopes;
using MiniAdmin.Application.Contracts.Departments;
using MiniAdmin.Application.Contracts.Dictionaries;
using MiniAdmin.Application.Contracts.Events;
using MiniAdmin.Application.Contracts.Files;
using MiniAdmin.Application.Contracts.Menus;
using MiniAdmin.Application.Contracts.MultiTenancy;
using MiniAdmin.Application.Contracts.Notices;
using MiniAdmin.Application.Contracts.OnlineUsers;
using MiniAdmin.Application.Contracts.Parameters;
using MiniAdmin.Application.Contracts.PermissionDiagnostics;
using MiniAdmin.Application.Contracts.Positions;
using MiniAdmin.Application.Contracts.Roles;
using MiniAdmin.Application.Contracts.ScheduledJobs;
using MiniAdmin.Application.Contracts.Security;
using MiniAdmin.Application.Contracts.TenantPackages;
using MiniAdmin.Application.Contracts.UnitOfWork;
using MiniAdmin.Application.Contracts.UserNotifications;
using MiniAdmin.Application.Contracts.Users;
using MiniAdmin.Application.Contracts.Workflows;
using MiniAdmin.Infrastructure.Auth;
using MiniAdmin.Infrastructure.Caching;
using MiniAdmin.Infrastructure.Common;
using MiniAdmin.Infrastructure.Events;
using MiniAdmin.Infrastructure.MultiTenancy;
using MiniAdmin.Infrastructure.Notifications;
using MiniAdmin.Infrastructure.ScheduledJobs;
using MiniAdmin.Infrastructure.Storage;
using MiniAdmin.Infrastructure.UnitOfWork;
using MiniAdmin.Infrastructure.Users;

namespace MiniAdmin.Infrastructure.Persistence;

public static class MiniAdminPersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddMiniAdminPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var databaseOptions = ReadDatabaseOptions(configuration);
        var cacheOptions = ReadCacheOptions(configuration);
        var loginSecurityOptions = ReadLoginSecurityOptions(configuration);
        var onlineUserOptions = ReadOnlineUserOptions(configuration);

        services.AddDbContext<MiniAdminDbContext>(options =>
        {
            if (databaseOptions.Provider.Equals("MySql", StringComparison.OrdinalIgnoreCase))
            {
                var connectionString = configuration.GetConnectionString("MiniAdmin");
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    throw new InvalidOperationException(
                        "ConnectionStrings:MiniAdmin is required when Database:Provider is MySql.");
                }

                options.UseMySql(
                    connectionString,
                    ServerVersion.Parse(databaseOptions.MySqlServerVersion));
                return;
            }

            options.UseInMemoryDatabase(databaseOptions.InMemoryDatabaseName);
        });

        services.Configure<DatabaseOptions>(options =>
        {
            options.Provider = databaseOptions.Provider;
            options.InitializeOnStartup = databaseOptions.InitializeOnStartup;
            options.SchemaManagement = databaseOptions.SchemaManagement;
            options.InMemoryDatabaseName = databaseOptions.InMemoryDatabaseName;
            options.MySqlServerVersion = databaseOptions.MySqlServerVersion;
        });
        services.Configure<CacheOptions>(options =>
        {
            options.Provider = cacheOptions.Provider;
            options.KeyPrefix = cacheOptions.KeyPrefix;
            options.DefaultExpireMinutes = cacheOptions.DefaultExpireMinutes;
            options.SecurityStampExpireMinutes = cacheOptions.SecurityStampExpireMinutes;
            options.PermissionExpireMinutes = cacheOptions.PermissionExpireMinutes;
            options.MenuExpireMinutes = cacheOptions.MenuExpireMinutes;
            options.Redis = cacheOptions.Redis;
        });
        services.Configure<LoginSecurityOptions>(options =>
        {
            options.CaptchaRequiredFailures = loginSecurityOptions.CaptchaRequiredFailures;
            options.LockoutFailures = loginSecurityOptions.LockoutFailures;
            options.LockoutMinutes = loginSecurityOptions.LockoutMinutes;
            options.CaptchaExpireSeconds = loginSecurityOptions.CaptchaExpireSeconds;
        });
        services.Configure<OnlineUserOptions>(options =>
        {
            options.ActiveTimeoutMinutes = onlineUserOptions.ActiveTimeoutMinutes;
            options.TouchThrottleSeconds = onlineUserOptions.TouchThrottleSeconds;
        });
        var emailNotificationOptions = ReadEmailNotificationOptions(configuration);
        services.Configure<EmailNotificationOptions>(options =>
        {
            options.Enabled = emailNotificationOptions.Enabled;
            options.Host = emailNotificationOptions.Host;
            options.Port = emailNotificationOptions.Port;
            options.UserName = emailNotificationOptions.UserName;
            options.Password = emailNotificationOptions.Password;
            options.FromEmail = emailNotificationOptions.FromEmail;
            options.FromName = emailNotificationOptions.FromName;
            options.EnableSsl = emailNotificationOptions.EnableSsl;
        });
        var webhookNotificationOptions = ReadWebhookNotificationOptions(configuration);
        services.Configure<WebhookNotificationOptions>(options =>
        {
            options.Enabled = webhookNotificationOptions.Enabled;
            options.EndpointUrl = webhookNotificationOptions.EndpointUrl;
            options.Secret = webhookNotificationOptions.Secret;
        });
        if (cacheOptions.Provider.Equals("Redis", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(cacheOptions.Redis.Configuration))
        {
            AddResilientRedisCache(services, cacheOptions.Redis.Configuration);
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        var fileStorageOptions = ReadFileStorageOptions(configuration);
        services.Configure<FileStorageOptions>(options =>
        {
            options.Provider = fileStorageOptions.Provider;
            options.Local = fileStorageOptions.Local;
            options.Minio = fileStorageOptions.Minio;
        });
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<ILocalEventBus, LocalEventBus>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        RegisterLocalEventHandlers(services, typeof(MiniAdminDbContext).Assembly);
        services.AddScoped<CurrentTenant>();
        services.AddScoped<ICurrentTenant>(serviceProvider =>
            serviceProvider.GetRequiredService<CurrentTenant>());
        services.AddScoped<ILoginSecurityService, DistributedLoginSecurityService>();
        services.AddScoped<IUserAuthorizationCache, DistributedUserAuthorizationCache>();
        services.AddScoped<IDataScopeProvider, EfDataScopeProvider>();
        services.AddScoped<IAuditEntityChangeCollector, AuditEntityChangeCollector>();
        services.AddScoped<IAuditLogRepository, EfAuditLogRepository>();
        services.AddScoped<IAlertRepository, EfAlertRepository>();
        services.AddScoped<IAlertRuleRepository, EfAlertRuleRepository>();
        services.AddScoped<IAlertNotificationRecipientRepository, EfAlertNotificationRecipientRepository>();
        services.AddScoped<IUserNotificationRepository, EfUserNotificationRepository>();
        services.AddScoped<INotificationTemplateRepository, EfNotificationTemplateRepository>();
        services.AddScoped<INotificationPolicyRepository, EfNotificationPolicyRepository>();
        services.AddScoped<INotificationSubscriptionRepository, EfNotificationSubscriptionRepository>();
        services.AddScoped<IEmailNotificationSender, SmtpEmailNotificationSender>();
        services.AddScoped<IWebhookNotificationSender, HttpWebhookNotificationSender>();
        services.AddScoped<INotificationDeliveryService, NotificationDeliveryService>();
        services.AddScoped<IFileRepository, EfFileRepository>();
        services.AddScoped<IFileStorageService, CompositeFileStorageService>();
        services.AddScoped<IAuthRepository, EfAuthRepository>();
        services.AddScoped<TenantInitializationTemplateService>();
        services.AddScoped<ITenantRepository, EfTenantRepository>();
        services.AddScoped<ITenantPackageRepository, EfTenantPackageRepository>();
        services.AddScoped<IUserRepository, EfUserRepository>();
        services.AddScoped<IWorkbookService, XlsxWorkbookService>();
        services.AddScoped<IUserImportExportService, XlsxUserImportExportService>();
        services.AddScoped<IDepartmentRepository, EfDepartmentRepository>();
        services.AddScoped<IPositionRepository, EfPositionRepository>();
        services.AddScoped<IDictionaryRepository, EfDictionaryRepository>();
        services.AddScoped<ISystemParameterRepository, EfSystemParameterRepository>();
        services.AddScoped<INoticeRepository, EfNoticeRepository>();
        services.AddScoped<IOnlineUserRepository, EfOnlineUserRepository>();
        services.AddScoped<ISecurityEventRepository, EfSecurityEventRepository>();
        services.AddScoped<ISecurityPolicyRepository, EfSecurityPolicyRepository>();
        services.AddScoped<IRoleRepository, EfRoleRepository>();
        services.AddScoped<IMenuRepository, EfMenuRepository>();
        services.AddScoped<IPermissionDiagnosticsRepository, EfPermissionDiagnosticsRepository>();
        services.AddScoped<IScheduledJobRepository, EfScheduledJobRepository>();
        services.AddScoped<ICodeGeneratorRepository, EfCodeGeneratorRepository>();
        services.AddScoped<IWorkflowRepository, EfWorkflowRepository>();
        RegisterWorkflowBusinessStateHandlers(services, typeof(MiniAdminDbContext).Assembly);
        services.AddScoped<IScheduledJobExecutor, ScheduledJobExecutor>();
        services.AddScoped<IMiniAdminDatabaseInitializer, MiniAdminDatabaseInitializer>();
        services.AddHostedService<ScheduledJobWorker>();

        return services;
    }

    private static void RegisterWorkflowBusinessStateHandlers(
        IServiceCollection services,
        Assembly assembly)
    {
        var handlerType = typeof(IWorkflowBusinessStateHandler);
        var implementations = assembly
            .GetTypes()
            .Where(type =>
                !type.IsAbstract &&
                !type.IsInterface &&
                !type.ContainsGenericParameters &&
                handlerType.IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

        foreach (var implementation in implementations)
        {
            services.TryAddEnumerable(ServiceDescriptor.Scoped(handlerType, implementation));
        }
    }

    private static void RegisterLocalEventHandlers(
        IServiceCollection services,
        params Assembly[] assemblies)
    {
        var openHandlerType = typeof(ILocalEventHandler<>);
        var implementations = assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type =>
                !type.IsAbstract &&
                !type.IsInterface &&
                !type.ContainsGenericParameters)
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

        foreach (var implementation in implementations)
        {
            var serviceTypes = implementation
                .GetInterfaces()
                .Where(type =>
                    type.IsGenericType &&
                    type.GetGenericTypeDefinition() == openHandlerType);

            foreach (var serviceType in serviceTypes)
            {
                services.TryAddEnumerable(ServiceDescriptor.Scoped(serviceType, implementation));
            }
        }
    }

    private static void AddResilientRedisCache(IServiceCollection services, string configuration)
    {
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = BuildRedisConfiguration(configuration);
        });

        var redisDescriptor = services.LastOrDefault(descriptor =>
            descriptor.ServiceType == typeof(IDistributedCache));
        if (redisDescriptor is null)
        {
            services.AddDistributedMemoryCache();
            return;
        }

        services.Remove(redisDescriptor);
        services.AddSingleton<IDistributedCache>(serviceProvider =>
        {
            var primary = (IDistributedCache)CreateService(redisDescriptor, serviceProvider);
            var fallback = ActivatorUtilities.CreateInstance<MemoryDistributedCache>(serviceProvider);
            var logger = serviceProvider.GetRequiredService<ILogger<ResilientDistributedCache>>();

            return new ResilientDistributedCache(primary, fallback, logger);
        });
    }

    private static string BuildRedisConfiguration(string configuration)
    {
        var normalized = configuration.Trim();
        var additions = new List<string>();

        AddIfMissing(normalized, additions, "abortConnect", "False");
        AddIfMissing(normalized, additions, "connectRetry", "1");
        AddIfMissing(normalized, additions, "connectTimeout", "1000");
        AddIfMissing(normalized, additions, "syncTimeout", "1000");
        AddIfMissing(normalized, additions, "asyncTimeout", "1000");

        return additions.Count == 0
            ? normalized
            : $"{normalized},{string.Join(',', additions)}";
    }

    private static void AddIfMissing(
        string configuration,
        ICollection<string> additions,
        string key,
        string value)
    {
        if (configuration.Contains($"{key}=", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        additions.Add($"{key}={value}");
    }

    private static object CreateService(ServiceDescriptor descriptor, IServiceProvider serviceProvider)
    {
        if (descriptor.ImplementationInstance is not null)
        {
            return descriptor.ImplementationInstance;
        }

        if (descriptor.ImplementationFactory is not null)
        {
            return descriptor.ImplementationFactory(serviceProvider);
        }

        if (descriptor.ImplementationType is not null)
        {
            return ActivatorUtilities.CreateInstance(serviceProvider, descriptor.ImplementationType);
        }

        throw new InvalidOperationException("Cannot create distributed cache service.");
    }

    private static DatabaseOptions ReadDatabaseOptions(IConfiguration configuration)
    {
        var options = new DatabaseOptions();
        var section = configuration.GetSection("Database");

        options.Provider = section["Provider"] ?? options.Provider;
        options.SchemaManagement = section["SchemaManagement"] ?? options.SchemaManagement;
        options.InMemoryDatabaseName = section["InMemoryDatabaseName"] ?? options.InMemoryDatabaseName;
        options.MySqlServerVersion = section["MySqlServerVersion"] ?? options.MySqlServerVersion;

        if (bool.TryParse(section["InitializeOnStartup"], out var initializeOnStartup))
        {
            options.InitializeOnStartup = initializeOnStartup;
        }

        return options;
    }

    private static CacheOptions ReadCacheOptions(IConfiguration configuration)
    {
        var options = new CacheOptions();
        var section = configuration.GetSection("Cache");
        var redisSection = section.GetSection("Redis");

        options.Provider = section["Provider"] ?? options.Provider;
        options.KeyPrefix = section["KeyPrefix"] ?? options.KeyPrefix;
        options.Redis.Configuration = redisSection["Configuration"] ?? options.Redis.Configuration;

        if (int.TryParse(section["DefaultExpireMinutes"], out var defaultExpireMinutes))
        {
            options.DefaultExpireMinutes = defaultExpireMinutes;
        }

        if (int.TryParse(section["SecurityStampExpireMinutes"], out var securityStampExpireMinutes))
        {
            options.SecurityStampExpireMinutes = securityStampExpireMinutes;
        }

        if (int.TryParse(section["PermissionExpireMinutes"], out var permissionExpireMinutes))
        {
            options.PermissionExpireMinutes = permissionExpireMinutes;
        }

        if (int.TryParse(section["MenuExpireMinutes"], out var menuExpireMinutes))
        {
            options.MenuExpireMinutes = menuExpireMinutes;
        }

        return options;
    }

    private static LoginSecurityOptions ReadLoginSecurityOptions(IConfiguration configuration)
    {
        var options = new LoginSecurityOptions();
        var section = configuration.GetSection("LoginSecurity");

        if (int.TryParse(section["CaptchaRequiredFailures"], out var captchaRequiredFailures))
        {
            options.CaptchaRequiredFailures = captchaRequiredFailures;
        }

        if (int.TryParse(section["LockoutFailures"], out var lockoutFailures))
        {
            options.LockoutFailures = lockoutFailures;
        }

        if (int.TryParse(section["LockoutMinutes"], out var lockoutMinutes))
        {
            options.LockoutMinutes = lockoutMinutes;
        }

        if (int.TryParse(section["CaptchaExpireSeconds"], out var captchaExpireSeconds))
        {
            options.CaptchaExpireSeconds = captchaExpireSeconds;
        }

        return options;
    }

    private static OnlineUserOptions ReadOnlineUserOptions(IConfiguration configuration)
    {
        var options = new OnlineUserOptions();
        var section = configuration.GetSection("OnlineUsers");

        if (int.TryParse(section["ActiveTimeoutMinutes"], out var activeTimeoutMinutes))
        {
            options.ActiveTimeoutMinutes = activeTimeoutMinutes;
        }

        if (int.TryParse(section["TouchThrottleSeconds"], out var touchThrottleSeconds))
        {
            options.TouchThrottleSeconds = touchThrottleSeconds;
        }

        return options;
    }

    private static FileStorageOptions ReadFileStorageOptions(IConfiguration configuration)
    {
        var options = new FileStorageOptions();
        var section = configuration.GetSection("FileStorage");
        var localSection = section.GetSection("Local");
        var minioSection = section.GetSection("Minio");

        options.Provider = section["Provider"] ?? options.Provider;
        options.Local.RootPath = localSection["RootPath"] ?? options.Local.RootPath;
        options.Minio.Endpoint = minioSection["Endpoint"] ?? options.Minio.Endpoint;
        options.Minio.AccessKey = minioSection["AccessKey"] ?? options.Minio.AccessKey;
        options.Minio.SecretKey = minioSection["SecretKey"] ?? options.Minio.SecretKey;
        options.Minio.Bucket = minioSection["Bucket"] ?? options.Minio.Bucket;
        options.Minio.Region = minioSection["Region"] ?? options.Minio.Region;

        if (bool.TryParse(minioSection["UseSsl"], out var useSsl))
        {
            options.Minio.UseSsl = useSsl;
        }

        return options;
    }

    private static EmailNotificationOptions ReadEmailNotificationOptions(IConfiguration configuration)
    {
        var options = new EmailNotificationOptions();
        var section = configuration.GetSection("Notifications:Email");

        options.Host = section["Host"] ?? options.Host;
        options.UserName = section["UserName"] ?? options.UserName;
        options.Password = section["Password"] ?? options.Password;
        options.FromEmail = section["FromEmail"] ?? options.FromEmail;
        options.FromName = section["FromName"] ?? options.FromName;

        if (bool.TryParse(section["Enabled"], out var enabled))
        {
            options.Enabled = enabled;
        }

        if (int.TryParse(section["Port"], out var port))
        {
            options.Port = port;
        }

        if (bool.TryParse(section["EnableSsl"], out var enableSsl))
        {
            options.EnableSsl = enableSsl;
        }

        return options;
    }

    private static WebhookNotificationOptions ReadWebhookNotificationOptions(IConfiguration configuration)
    {
        var options = new WebhookNotificationOptions();
        var section = configuration.GetSection("Notifications:Webhook");

        options.EndpointUrl = section["EndpointUrl"] ?? options.EndpointUrl;
        options.Secret = section["Secret"] ?? options.Secret;

        if (bool.TryParse(section["Enabled"], out var enabled))
        {
            options.Enabled = enabled;
        }

        return options;
    }
}
