namespace MiniAdmin.Infrastructure.Persistence;

public interface IMiniAdminDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
