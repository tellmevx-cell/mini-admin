namespace MiniAdmin.Application.Contracts.DataScopes;

public enum DataScopeLevel
{
    None = 0,
    Self = 1,
    Department = 2,
    DepartmentAndChildren = 3,
    CustomDepartments = 4,
    Mixed = 5,
    All = 6
}

public sealed record DataScopeContext(
    DataScopeLevel Level,
    Guid? UserId,
    string? UserName,
    Guid? DepartmentId,
    IReadOnlySet<Guid> DepartmentIds)
{
    public bool IsDenied => Level == DataScopeLevel.None;

    public bool IsUnrestricted => Level == DataScopeLevel.All;

    public bool AllowsSelf => !IsDenied && UserId.HasValue;

    public static DataScopeContext Denied()
    {
        return new DataScopeContext(DataScopeLevel.None, null, null, null, new HashSet<Guid>());
    }

    public static DataScopeContext Unrestricted()
    {
        return new DataScopeContext(DataScopeLevel.All, null, null, null, new HashSet<Guid>());
    }
}
