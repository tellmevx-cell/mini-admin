namespace MiniAdmin.Application.Contracts.Users;

public enum PasswordOperationStatus
{
    Succeeded,
    UserNotFound,
    OldPasswordIncorrect,
    PasswordMismatch,
    PasswordPolicyViolation
}

public sealed record PasswordOperationResult(
    PasswordOperationStatus Status,
    string Message)
{
    public static PasswordOperationResult Succeeded() => new(
        PasswordOperationStatus.Succeeded,
        "ok");

    public static PasswordOperationResult UserNotFound() => new(
        PasswordOperationStatus.UserNotFound,
        "用户不存在.");

    public static PasswordOperationResult OldPasswordIncorrect() => new(
        PasswordOperationStatus.OldPasswordIncorrect,
        "旧密码不正确.");

    public static PasswordOperationResult PasswordMismatch() => new(
        PasswordOperationStatus.PasswordMismatch,
        "两次输入的密码不一致.");

    public static PasswordOperationResult PasswordPolicyViolation(string message) => new(
        PasswordOperationStatus.PasswordPolicyViolation,
        message);
}
