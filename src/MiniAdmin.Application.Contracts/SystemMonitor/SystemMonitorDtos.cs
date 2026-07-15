namespace MiniAdmin.Application.Contracts.SystemMonitor;

public sealed record SystemMonitorOverviewDto(
    SystemMonitorApiDto Api,
    SystemMonitorCpuDto Cpu,
    SystemMonitorMemoryDto Memory,
    SystemMonitorApplicationDto Application,
    SystemMonitorServerDto Server,
    IReadOnlyList<SystemMonitorDependencyDto> Dependencies,
    SystemMonitorRecentDto Recent,
    SystemMonitorHardwareDto Hardware,
    IReadOnlyList<SystemMonitorDiskDto> Disks,
    IReadOnlyList<SystemMonitorNetworkDto> Networks);

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
    string ContentRootPath,
    int ProcessId,
    string ProcessArchitecture,
    bool Is64BitProcess,
    bool ServerGarbageCollection,
    string GarbageCollectionLatencyMode);

public sealed record SystemMonitorServerDto(
    string MachineName,
    string OperatingSystem,
    string Architecture);

public sealed record SystemMonitorHardwareDto(
    string Manufacturer,
    string Model,
    string MotherboardManufacturer,
    string MotherboardModel,
    string CpuModel,
    IReadOnlyList<string> Gpus);

public sealed record SystemMonitorDiskDto(
    string Name,
    string RootPath,
    string DriveType,
    string FileSystem,
    bool IsReady,
    long TotalBytes,
    long AvailableBytes,
    long UsedBytes,
    double UsedPercent);

public sealed record SystemMonitorNetworkDto(
    string Name,
    string Description,
    string InterfaceType,
    string Status,
    long SpeedBitsPerSecond,
    string MacAddress,
    IReadOnlyList<string> Addresses,
    long BytesReceived,
    long BytesSent);

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
