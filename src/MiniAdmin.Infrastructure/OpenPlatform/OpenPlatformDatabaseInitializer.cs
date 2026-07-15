using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using MiniAdmin.Infrastructure.Persistence;
using OpenIddict.Abstractions;

namespace MiniAdmin.Infrastructure.OpenPlatform;

public interface IOpenPlatformDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

public sealed class OpenPlatformDatabaseInitializer(
    OpenPlatformDbContext dbContext,
    IOptions<DatabaseOptions> databaseOptions,
    IConfiguration configuration,
    IOpenIddictScopeManager scopeManager) : IOpenPlatformDatabaseInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (databaseOptions.Value.SchemaManagement.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (databaseOptions.Value.Provider.Equals("MySql", StringComparison.OrdinalIgnoreCase))
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        }

        if (await scopeManager.FindByNameAsync(OpenPlatformScopeNames.Api, cancellationToken) is null)
        {
            var descriptor = new OpenIddictScopeDescriptor
            {
                Name = OpenPlatformScopeNames.Api,
                DisplayName = "MiniAdmin API"
            };
            descriptor.Resources.Add(configuration["Jwt:Audience"] ?? "MiniAdmin.Vben");
            await scopeManager.CreateAsync(descriptor, cancellationToken);
        }
    }
}
