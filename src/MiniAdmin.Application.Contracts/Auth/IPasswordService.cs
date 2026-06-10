namespace MiniAdmin.Application.Contracts.Auth;

public interface IPasswordService
{
    string HashPassword(string password);

    bool VerifyPassword(string passwordHash, string password);
}
