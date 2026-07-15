using MiniAdmin.Infrastructure.SystemMonitor;

namespace MiniAdmin.Tests;

public sealed class SystemInformationProviderTests
{
    [Fact]
    public void Hardware_disk_and_network_collection_is_safe_on_current_platform()
    {
        var provider = new SystemInformationProvider();

        var hardware = provider.GetHardware();
        var secondHardwareRead = provider.GetHardware();
        var disks = provider.GetDisks();
        var networks = provider.GetNetworks();

        Assert.Same(hardware, secondHardwareRead);
        Assert.False(string.IsNullOrWhiteSpace(hardware.CpuModel));
        Assert.NotNull(hardware.Gpus);
        Assert.NotNull(disks);
        Assert.NotNull(networks);
        Assert.All(disks, disk => Assert.InRange(disk.UsedPercent, 0, 100));
        Assert.All(networks, network => Assert.True(network.SpeedBitsPerSecond >= 0));
    }
}
