namespace MiniAdmin.Application.Contracts.Files;

public sealed class FileUnavailableException(string message) : Exception(message);
