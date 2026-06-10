namespace MiniAdmin.Application.Contracts.Users;

public sealed class UserOperationException(string message) : Exception(message);
