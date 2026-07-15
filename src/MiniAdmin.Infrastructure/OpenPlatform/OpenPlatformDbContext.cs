using Microsoft.EntityFrameworkCore;

namespace MiniAdmin.Infrastructure.OpenPlatform;

public sealed class OpenPlatformDbContext(
    DbContextOptions<OpenPlatformDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.UseOpenIddict<Guid>();
    }
}
