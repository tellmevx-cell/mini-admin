namespace MiniAdmin.Application.Contracts.Users;

public sealed record ResetUserPasswordRequest(
    string NewPassword,
    string ConfirmPassword);
