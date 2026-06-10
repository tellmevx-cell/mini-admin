namespace MiniAdmin.Application.Contracts.Auth;

public interface IAuthAppService
{
    Task<LoginResult?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetAccessCodesAsync(string userName, CancellationToken cancellationToken = default);
}
