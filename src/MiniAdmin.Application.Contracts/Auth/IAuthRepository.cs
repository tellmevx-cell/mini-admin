namespace MiniAdmin.Application.Contracts.Auth;

public interface IAuthRepository
{
    Task<AuthenticatedUserDto?> FindByUserNameAsync(string userName, CancellationToken cancellationToken = default);
}
