namespace MiniAdmin.Application.Contracts.SystemMonitor;

public sealed record SystemMonitorOverviewDto(
    SystemMonitorApiDto Api,
    SystemMonitorCpuDto Cpu,
    SystemMonitorMemoryDto Memory,
    SystemMonitorApplicationDto Application,
    SystemMonitorServerDto Server,
    IReadOnlyList<SystemMonitorDependencyDto> Dependencies,
    SystemMonitorRecentDto Recent);

public sealed record SystemMonitorApiDto(
    string Status,
    DateTimeOffset Timestamp);

public sealed record SystemMonitorCpuDto(
    int ProcessorCount,
    int ThreadCount,
    double ProcessCpuPercent);

public sealed record SystemMonitorMemoryDto(
    long TotalPhysicalMemoryBytes,
    long AvailablePhysicalMemoryBytes,
    long UsedPhysicalMemoryBytes,
    double PhysicalMemoryUsedPercent,
    long WorkingSetBytes,
    long ManagedHeapBytes,
    long GcTotalMemoryBytes,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections);

public sealed record SystemMonitorApplicationDto(
    string Environment,
    string RuntimeVersion,
    DateTimeOffset StartedAt,
    long UptimeSeconds,
    string ContentRootPath);

public sealed record SystemMonitorServerDto(
    string MachineName,
    string OperatingSystem,
    string Architecture);

public sealed record SystemMonitorDependencyDto(
    string Name,
    string Status,
    string Description,
    long? ElapsedMilliseconds);

public sealed record SystemMonitorRecentDto(
    int FailedScheduledJobCount,
    int FailedAuditLogCount,
    int OnlineUserCount,
    int AbnormalFileCount);
