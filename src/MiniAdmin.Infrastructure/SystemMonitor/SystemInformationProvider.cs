using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.Versioning;
using Microsoft.Win32;
using MiniAdmin.Application.Contracts.SystemMonitor;

namespace MiniAdmin.Infrastructure.SystemMonitor;

public interface ISystemInformationProvider
{
    SystemMonitorHardwareDto GetHardware();

    IReadOnlyList<SystemMonitorDiskDto> GetDisks();

    IReadOnlyList<SystemMonitorNetworkDto> GetNetworks();
}

public sealed class SystemInformationProvider : ISystemInformationProvider
{
    private readonly Lazy<SystemMonitorHardwareDto> hardware = new(CollectHardware, true);

    public SystemMonitorHardwareDto GetHardware() => hardware.Value;

    public IReadOnlyList<SystemMonitorDiskDto> GetDisks()
    {
        var disks = new List<SystemMonitorDiskDto>();
        foreach (var drive in DriveInfo.GetDrives().OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (!drive.IsReady)
                {
                    disks.Add(new SystemMonitorDiskDto(
                        drive.Name,
                        drive.RootDirectory.FullName,
                        drive.DriveType.ToString(),
                        string.Empty,
                        false,
                        0,
                        0,
                        0,
                        0));
                    continue;
                }

                var total = Math.Max(0, drive.TotalSize);
                var available = Math.Clamp(drive.AvailableFreeSpace, 0, total);
                var used = Math.Max(0, total - available);
                disks.Add(new SystemMonitorDiskDto(
                    drive.Name,
                    drive.RootDirectory.FullName,
                    drive.DriveType.ToString(),
                    drive.DriveFormat,
                    true,
                    total,
                    available,
                    used,
                    total == 0 ? 0 : Math.Round((double)used / total * 100, 2)));
            }
            catch (IOException)
            {
                // Removable and network drives may disappear while being enumerated.
            }
            catch (UnauthorizedAccessException)
            {
                // Some mounted volumes intentionally hide their capacity from the process.
            }
        }

        return disks;
    }

    public IReadOnlyList<SystemMonitorNetworkDto> GetNetworks()
    {
        var networks = new List<SystemMonitorNetworkDto>();
        foreach (var network in NetworkInterface.GetAllNetworkInterfaces()
                     .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                var properties = network.GetIPProperties();
                var addresses = properties.UnicastAddresses
                    .Where(item => item.Address.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
                    .Select(item => item.Address.ToString())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                long bytesReceived = 0;
                long bytesSent = 0;
                try
                {
                    var statistics = network.GetIPStatistics();
                    bytesReceived = Math.Max(0, statistics.BytesReceived);
                    bytesSent = Math.Max(0, statistics.BytesSent);
                }
                catch (NetworkInformationException)
                {
                    // Byte counters are optional on some virtual adapters.
                }

                networks.Add(new SystemMonitorNetworkDto(
                    network.Name,
                    network.Description,
                    network.NetworkInterfaceType.ToString(),
                    network.OperationalStatus.ToString(),
                    Math.Max(0, network.Speed),
                    FormatMacAddress(network.GetPhysicalAddress()),
                    addresses,
                    bytesReceived,
                    bytesSent));
            }
            catch (NetworkInformationException)
            {
                // Ignore an adapter that disappeared during collection.
            }
        }

        return networks;
    }

    private static SystemMonitorHardwareDto CollectHardware()
    {
        if (OperatingSystem.IsWindows())
        {
            return CollectWindowsHardware();
        }

        if (OperatingSystem.IsLinux())
        {
            return CollectLinuxHardware();
        }

        return new SystemMonitorHardwareDto(
            "Unknown",
            "Unknown",
            "Unknown",
            "Unknown",
            Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "Unknown",
            []);
    }

    [SupportedOSPlatform("windows")]
    private static SystemMonitorHardwareDto CollectWindowsHardware()
    {
        using var bios = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS");
        using var cpu = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
        return new SystemMonitorHardwareDto(
            ReadRegistryValue(bios, "SystemManufacturer"),
            ReadRegistryValue(bios, "SystemProductName"),
            ReadRegistryValue(bios, "BaseBoardManufacturer"),
            ReadRegistryValue(bios, "BaseBoardProduct"),
            ReadRegistryValue(cpu, "ProcessorNameString"),
            GetWindowsGpuNames());
    }

    private static SystemMonitorHardwareDto CollectLinuxHardware()
    {
        var cpuModel = ReadFirstMatchingValue(
            "/proc/cpuinfo",
            ["model name", "Hardware", "Processor"]);
        var gpus = new List<string>();
        const string nvidiaRoot = "/proc/driver/nvidia/gpus";
        if (Directory.Exists(nvidiaRoot))
        {
            foreach (var information in Directory.EnumerateFiles(
                         nvidiaRoot,
                         "information",
                         SearchOption.AllDirectories))
            {
                var model = ReadFirstMatchingValue(information, ["Model"]);
                if (!string.Equals(model, "Unknown", StringComparison.OrdinalIgnoreCase))
                {
                    gpus.Add(model);
                }
            }
        }

        return new SystemMonitorHardwareDto(
            ReadTextFile("/sys/devices/virtual/dmi/id/sys_vendor"),
            ReadTextFile("/sys/devices/virtual/dmi/id/product_name"),
            ReadTextFile("/sys/devices/virtual/dmi/id/board_vendor"),
            ReadTextFile("/sys/devices/virtual/dmi/id/board_name"),
            cpuModel,
            gpus.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    [SupportedOSPlatform("windows")]
    private static string[] GetWindowsGpuNames()
    {
        var result = new List<string>();
        using var videoMap = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DEVICEMAP\VIDEO");
        if (videoMap is null)
        {
            return [];
        }

        foreach (var valueName in videoMap.GetValueNames())
        {
            var registryPath = ConvertRegistryMachinePath(Convert.ToString(videoMap.GetValue(valueName)));
            if (registryPath is null)
            {
                continue;
            }

            using var adapter = Registry.LocalMachine.OpenSubKey(registryPath);
            var name = Convert.ToString(adapter?.GetValue("Device Description"));
            if (!string.IsNullOrWhiteSpace(name))
            {
                result.Add(name.Trim());
            }
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string? ConvertRegistryMachinePath(string? path)
    {
        const string prefix = @"\Registry\Machine\";
        return !string.IsNullOrWhiteSpace(path) && path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? path[prefix.Length..]
            : null;
    }

    [SupportedOSPlatform("windows")]
    private static string ReadRegistryValue(RegistryKey? key, string name)
    {
        var value = Convert.ToString(key?.GetValue(name));
        return string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();
    }

    private static string ReadTextFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var value = File.ReadAllText(path).Trim();
                return string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return "Unknown";
    }

    private static string ReadFirstMatchingValue(string path, IReadOnlyList<string> keys)
    {
        try
        {
            if (!File.Exists(path))
            {
                return "Unknown";
            }

            foreach (var line in File.ReadLines(path))
            {
                var separator = line.IndexOf(':');
                if (separator <= 0)
                {
                    continue;
                }

                var key = line[..separator].Trim();
                if (!keys.Any(item => string.Equals(item, key, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var value = line[(separator + 1)..].Trim();
                return string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return "Unknown";
    }

    private static string FormatMacAddress(PhysicalAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes.Length == 0 ? string.Empty : string.Join(':', bytes.Select(value => value.ToString("X2")));
    }
}
