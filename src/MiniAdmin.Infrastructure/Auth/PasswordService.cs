using System.Security.Cryptography;
using MiniAdmin.Application.Contracts.Auth;

namespace MiniAdmin.Infrastructure.Auth;

public sealed class PasswordService : IPasswordService
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;

    public string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);

        return $"pbkdf2-sha256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public bool VerifyPassword(string passwordHash, string password)
    {
        var parts = passwordHash.Split('$');
        if (parts is not ["pbkdf2-sha256", var iterationText, var saltText, var hashText])
        {
            return false;
        }

        if (!int.TryParse(iterationText, out var iterations))
        {
            return false;
        }

        var salt = Convert.FromBase64String(saltText);
        var expectedHash = Convert.FromBase64String(hashText);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
