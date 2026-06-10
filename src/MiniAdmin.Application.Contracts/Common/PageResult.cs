namespace MiniAdmin.Application.Contracts.Common;

public sealed record PageResult<T>(IReadOnlyList<T> Items, int Total);
