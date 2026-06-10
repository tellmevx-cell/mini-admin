namespace MiniAdmin.Application.Contracts.DataScopes;

public interface IDataScopeProvider
{
    Task<DataScopeContext> GetAsync(
        string? currentUserName,
        CancellationToken cancellationToken = default);
}
