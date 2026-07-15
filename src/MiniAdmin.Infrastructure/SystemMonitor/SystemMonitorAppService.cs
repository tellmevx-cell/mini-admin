using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MiniAdmin.Application.Contracts.Files;
using MiniAdmin.Application.Contracts.Security;
using MiniAdmin.Application.Contracts.SystemMonitor;
using MiniAdmin.Infrastructure.Caching;
using MiniAdmin.Infrastructure.Persistence;

namespace MiniAdmin.Infrastructure.SystemMonitor;

public sealed class SystemMonitorAppService(
    MiniAdminDbContext dbContext,
    IDistributedCache cache,
    IFileStorageService fileStorageService,
    IHostEnvironment hostEnvironment,
    IOptions<CacheOptions> cacheOptions,
    IOptions<DatabaseOptions> databaseOptions,
    ISecurityPolicyRepository securityPolicyRepository,
    ISystemInformationProvider systemInformationProvider) : ISystemMonitorAppService
{
    private static readonly DateTimeOffset StartedAt = DateTimeOffset.UtcNow;

    public async Task<SystemMonitorOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var process = Process.GetCurrentProcess();
        var now = DateTimeOffset.UtcNow;
        var recentSince = now.AddHours(-24);
        var securityPolicy = await securityPolicyRepository.GetPolicyAsync(cancellationToken);
        var onlineActiveAfter = now.AddMinutes(-Math.Max(securityPolicy.OnlineActiveTimeoutMinutes, 1));
        var systemMemory = GetSystemMemory();
        var dependencies = new List<SystemMonitorDependencyDto>
        {
            await CheckDatabaseAsync(cancellationToken),
            await CheckCacheAsync(cancellationToken),
            CheckFileStorage()
        };

        var recent = new SystemMonitorRecentDto(
            FailedScheduledJobCount: await dbContext.ScheduledJobLogs
                .AsNoTracking()
                .CountAsync(log => log.StartedAt >= recentSince && log.Status != "Succeeded", cancellationToken),
            FailedAuditLogCount: await dbContext.AuditLogs
                .AsNoTracking()
                .CountAsync(log => log.CreatedAt >= recentSince && !log.IsSuccess, cancellationToken),
            OnlineUserCount: await dbContext.OnlineUsers
                .AsNoTracking()
                .CountAsync(user => user.IsOnline && user.LastActiveAt >= onlineActiveAfter, cancellationToken),
            AbnormalFileCount: await dbContext.ManagedFiles
                .AsNoTracking()
                .CountAsync(file => file.Status != "Normal", cancellationToken));

        return new SystemMonitorOverviewDto(
            Api: new SystemMonitorApiDto("Healthy", now),
            Cpu: new SystemMonitorCpuDto(
                Environment.ProcessorCount,
                process.Threads.Count,
                CalculateAverageProcessCpuPercent(process)),
            Memory: new SystemMonitorMemoryDto(
                systemMemory.TotalBytes,
                systemMemory.AvailableBytes,
                systemMemory.UsedBytes,
                systemMemory.UsedPercent,
                process.WorkingSet64,
                GC.GetTotalMemory(forceFullCollection: false),
                GC.GetGCMemoryInfo().HeapSizeBytes,
                GC.CollectionCount(0),
                GC.CollectionCount(1),
                GC.CollectionCount(2)),
            Application: new SystemMonitorApplicationDto(
                hostEnvironment.EnvironmentName,
                RuntimeInformation.FrameworkDescription,
                StartedAt,
                Math.Max(0, (long)(now - StartedAt).TotalSeconds),
                hostEnvironment.ContentRootPath,
                Environment.ProcessId,
                RuntimeInformation.ProcessArchitecture.ToString(),
                Environment.Is64BitProcess,
                System.Runtime.GCSettings.IsServerGC,
                System.Runtime.GCSettings.LatencyMode.ToString()),
            Server: new SystemMonitorServerDto(
                Environment.MachineName,
                RuntimeInformation.OSDescription,
                RuntimeInformation.OSArchitecture.ToString()),
            Dependencies: dependencies,
            Recent: recent,
            Hardware: systemInformationProvider.GetHardware(),
            Disks: systemInformationProvider.GetDisks(),
            Networks: systemInformationProvider.GetNetworks());
    }

    private async Task<SystemMonitorDependencyDto> CheckDatabaseAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
            stopwatch.Stop();
            return new SystemMonitorDependencyDto(
                "MySQL",
                canConnect ? "Healthy" : "Unhealthy",
                canConnect
                    ? $"数据库连接正常，当前 Provider：{databaseOptions.Value.Provider}"
                    : $"数据库连接失败，当前 Provider：{databaseOptions.Value.Provider}",
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            return new SystemMonitorDependencyDto(
                "MySQL",
                "Unhealthy",
                exception.Message,
                stopwatch.ElapsedMilliseconds);
        }
    }

    private async Task<SystemMonitorDependencyDto> CheckCacheAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var key = $"{cacheOptions.Value.KeyPrefix}:monitor:probe";
            await cache.SetStringAsync(
                key,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
                },
                cancellationToken);
            _ = await cache.GetStringAsync(key, cancellationToken);
            stopwatch.Stop();
            return new SystemMonitorDependencyDto(
                "Cache",
                "Healthy",
                $"缓存可用，当前 Provider：{cacheOptions.Value.Provider}",
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            return new SystemMonitorDependencyDto(
                "Cache",
                "Unhealthy",
                exception.Message,
                stopwatch.ElapsedMilliseconds);
        }
    }

    private SystemMonitorDependencyDto CheckFileStorage()
    {
        try
        {
            return new SystemMonitorDependencyDto(
                "FileStorage",
                "Healthy",
                $"文件存储已配置，当前 Provider：{fileStorageService.ProviderName}",
                null);
        }
        catch (Exception exception)
        {
            return new SystemMonitorDependencyDto(
                "FileStorage",
                "Unhealthy",
                exception.Message,
                null);
        }
    }

    private static double CalculateAverageProcessCpuPercent(Process process)
    {
        var uptimeSeconds = Math.Max(1, (DateTimeOffset.UtcNow - StartedAt).TotalSeconds);
        var average = process.TotalProcessorTime.TotalSeconds / uptimeSeconds / Environment.ProcessorCount * 100;
        return Math.Round(Math.Clamp(average, 0, 100), 2);
    }

    private static SystemMemorySnapshot GetSystemMemory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) &&
            TryGetWindowsMemory(out var windowsMemory))
        {
            return windowsMemory;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) &&
            TryGetLinuxMemory(out var linuxMemory))
        {
            return linuxMemory;
        }

        var gcInfo = GC.GetGCMemoryInfo();
        var total = Math.Max(0, gcInfo.TotalAvailableMemoryBytes);
        var used = Math.Max(0, GC.GetTotalMemory(forceFullCollection: false));
        var available = Math.Max(0, total - used);

        return SystemMemorySnapshot.Create(total, available);
    }

    private static bool TryGetWindowsMemory(out SystemMemorySnapshot snapshot)
    {
        var memoryStatus = new MemoryStatusEx
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>()
        };

        if (GlobalMemoryStatusEx(ref memoryStatus))
        {
            snapshot = SystemMemorySnapshot.Create(
                checked((long)memoryStatus.TotalPhysical),
                checked((long)memoryStatus.AvailablePhysical));
            return true;
        }

        snapshot = default;
        return false;
    }

    private static bool TryGetLinuxMemory(out SystemMemorySnapshot snapshot)
    {
        const string memInfoPath = "/proc/meminfo";
        if (!File.Exists(memInfoPath))
        {
            snapshot = default;
            return false;
        }

        long total = 0;
        long available = 0;
        foreach (var line in File.ReadLines(memInfoPath))
        {
            if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
            {
                total = ParseMemInfoKilobytes(line);
            }
            else if (line.StartsWith("MemAvailable:", StringComparison.Ordinal))
            {
                available = ParseMemInfoKilobytes(line);
            }
        }

        if (total <= 0)
        {
            snapshot = default;
            return false;
        }

        snapshot = SystemMemorySnapshot.Create(total, Math.Max(0, available));
        return true;
    }

    private static long ParseMemInfoKilobytes(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && long.TryParse(parts[1], out var kilobytes)
            ? kilobytes * 1024
            : 0;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    private readonly record struct SystemMemorySnapshot(
        long TotalBytes,
        long AvailableBytes,
        long UsedBytes,
        double UsedPercent)
    {
        public static SystemMemorySnapshot Create(long totalBytes, long availableBytes)
        {
            var safeTotal = Math.Max(0, totalBytes);
            var safeAvailable = Math.Clamp(availableBytes, 0, safeTotal);
            var used = Math.Max(0, safeTotal - safeAvailable);
            var percent = safeTotal > 0
                ? Math.Round((double)used / safeTotal * 100, 2)
                : 0;

            return new SystemMemorySnapshot(safeTotal, safeAvailable, used, percent);
        }
    }
}
