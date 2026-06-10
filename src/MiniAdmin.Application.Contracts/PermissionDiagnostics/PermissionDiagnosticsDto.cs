namespace MiniAdmin.Application.Contracts.PermissionDiagnostics;

public sealed record PermissionDiagnosticsDto(
    PermissionDiagnosticsUserDto User,
    PermissionDiagnosticsTenantDto Tenant,
    IReadOnlyList<PermissionDiagnosticsRoleDto> Roles,
    IReadOnlyList<string> PermissionCodes,
    IReadOnlyList<PermissionDiagnosticsMenuDto> MenuItems,
    PermissionDiagnosticsEffectiveDto Effective,
    IReadOnlyList<PermissionDiagnosticsWarningDto> Warnings,
    PermissionDiagnosticsDataScopeDto DataScope,
    PermissionDiagnosticsCacheDto Cache);

public sealed record PermissionDiagnosticsUserDto(
    string Id,
    string UserName,
    string RealName,
    string? DepartmentName,
    string? PositionName,
    bool IsEnabled);

public sealed record PermissionDiagnosticsRoleDto(
    string Id,
    string Code,
    string Name,
    string DataScope,
    IReadOnlyList<string> CustomDepartmentIds,
    IReadOnlyList<string> CustomDepartmentNames,
    bool IsEnabled,
    int MenuCount,
    int VisibleMenuCount,
    int ButtonPermissionCount);

public sealed record PermissionDiagnosticsTenantDto(
    bool IsTenant,
    string? TenantId,
    string? TenantCode,
    string? TenantName,
    string? PackageId,
    string? PackageName,
    int PackageMenuCount,
    bool IsPackageLimited);

public sealed record PermissionDiagnosticsMenuDto(
    string Id,
    string Title,
    string Path,
    string? PermissionCode,
    bool IsVisible);

public sealed record PermissionDiagnosticsDataScopeDto(
    string Level,
    string Description,
    string? DepartmentId,
    IReadOnlyList<string> DepartmentIds,
    IReadOnlyList<string> DepartmentNames);

public sealed record PermissionDiagnosticsEffectiveDto(
    int RoleMenuCount,
    int PackageMenuCount,
    int FinalMenuCount,
    int VisibleMenuCount,
    int ButtonPermissionCount,
    int PermissionCodeCount);

public sealed record PermissionDiagnosticsWarningDto(
    string Code,
    string Level,
    string Message,
    string Suggestion);

public sealed record PermissionDiagnosticsCacheDto(
    string SecurityStampKey,
    string PermissionCodesKey,
    string MenusKey);
