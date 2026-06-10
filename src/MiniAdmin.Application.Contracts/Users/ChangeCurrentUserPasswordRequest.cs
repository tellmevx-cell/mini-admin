namespace MiniAdmin.Application.Contracts.Users;

public sealed record ChangeCurrentUserPasswordRequest(
    string OldPassword,
    string NewPassword,
    string ConfirmPassword);
