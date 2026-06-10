using MiniAdmin.Application.Contracts.Auth;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.Parameters;
using MiniAdmin.Application.Contracts.Users;

namespace MiniAdmin.Application.Users;

public sealed class UserAppService(
    IUserRepository userRepository,
    IUserImportExportService userImportExportService,
    ILoginSecurityService loginSecurityService,
    ISystemParameterRepository systemParameterRepository) : IUserAppService
{
    private static readonly string[] ImportHeaders =
    [
        "用户名",
        "姓名",
        "初始密码",
        "部门编码",
        "岗位编码",
        "角色编码",
        "启用状态"
    ];

    public Task<CurrentUserDto> GetCurrentUserAsync(
        string userName,
        CancellationToken cancellationToken = default)
    {
        return userRepository.GetByUserNameAsync(userName, cancellationToken);
    }

    public async Task<PageResult<UserListItemDto>> GetListAsync(
        UserListQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = await userRepository.GetListAsync(query, cancellationToken);
        var items = new List<UserListItemDto>(page.Items.Count);

        foreach (var item in page.Items)
        {
            var lockRemainingSeconds = await loginSecurityService.GetLockRemainingSecondsAsync(
                item.UserName,
                cancellationToken);
            items.Add(item with
            {
                LoginLockRemainingSeconds = lockRemainingSeconds
            });
        }

        return new PageResult<UserListItemDto>(items, page.Total);
    }

    public async Task<UserExportFileDto> ExportAsync(
        UserListQuery query,
        CancellationToken cancellationToken = default)
    {
        var users = await userRepository.GetExportListAsync(query, cancellationToken);
        var rows = new List<IReadOnlyList<string>>
        {
            new[]
            {
                "用户名",
                "姓名",
                "部门",
                "岗位",
                "角色",
                "状态"
            }
        };
        rows.AddRange(users.Select(user => (IReadOnlyList<string>)new[]
        {
            user.UserName,
            user.RealName,
            user.DepartmentName ?? string.Empty,
            user.PositionName ?? string.Empty,
            string.Join(",", user.Roles),
            user.Status == 1 ? "启用" : "停用"
        }));

        return new UserExportFileDto(
            "mini-admin-users.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            userImportExportService.CreateWorkbook(rows));
    }

    public Task<UserExportFileDto> GetImportTemplateAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = new List<IReadOnlyList<string>>
        {
            ImportHeaders,
            new[]
            {
                "zhangsan",
                "张三",
                "Password123",
                "hq",
                "manager",
                "admin",
                "启用"
            }
        };

        return Task.FromResult(new UserExportFileDto(
            "mini-admin-user-import-template.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            userImportExportService.CreateWorkbook(rows)));
    }

    public async Task<UserImportResultDto> ImportAsync(
        Stream stream,
        string? currentUserName,
        CancellationToken cancellationToken = default)
    {
        var importRows = ReadAndParseImportRows(stream);

        if (importRows.Errors.Count > 0)
        {
            return new UserImportResultDto(0, importRows.Errors);
        }

        return await userRepository.ImportAsync(importRows.Rows, currentUserName, cancellationToken);
    }

    public async Task<UserImportResultDto> PreviewImportAsync(
        Stream stream,
        string? currentUserName,
        CancellationToken cancellationToken = default)
    {
        var rows = userImportExportService.ReadWorkbook(stream);
        var importRows = ParseImportRows(rows);

        if (importRows.Errors.Count > 0)
        {
            return new UserImportResultDto(0, importRows.Errors);
        }

        return await userRepository.ValidateImportAsync(importRows.Rows, currentUserName, cancellationToken);
    }

    public async Task<UserExportFileDto> ExportImportErrorsAsync(
        Stream stream,
        string? currentUserName,
        CancellationToken cancellationToken = default)
    {
        var rows = userImportExportService.ReadWorkbook(stream);
        var importRows = ParseImportRows(rows);
        var validationResult = importRows.Errors.Count > 0
            ? new UserImportResultDto(0, importRows.Errors)
            : await userRepository.ValidateImportAsync(importRows.Rows, currentUserName, cancellationToken);
        var errorRows = new List<IReadOnlyList<string>>
        {
            ImportHeaders.Concat(["失败原因"]).ToArray()
        };

        foreach (var error in validationResult.Errors)
        {
            var sourceRow = error.RowNumber - 1 >= 0 && error.RowNumber - 1 < rows.Count
                ? rows[error.RowNumber - 1]
                : Array.Empty<string>();
            errorRows.Add(new[]
            {
                GetCell(sourceRow, 0),
                GetCell(sourceRow, 1),
                GetCell(sourceRow, 2),
                GetCell(sourceRow, 3),
                GetCell(sourceRow, 4),
                GetCell(sourceRow, 5),
                GetCell(sourceRow, 6),
                error.Message
            });
        }

        return new UserExportFileDto(
            "mini-admin-user-import-errors.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            userImportExportService.CreateWorkbook(errorRows));
    }

    public Task<UserListItemDto> CreateAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        return userRepository.CreateAsync(request, cancellationToken);
    }

    public Task<UserListItemDto?> UpdateAsync(
        Guid id,
        string? currentUserName,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        return userRepository.UpdateAsync(id, currentUserName, request, cancellationToken);
    }

    public async Task<PasswordOperationResult> ChangePasswordAsync(
        string userName,
        ChangeCurrentUserPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var policyResult = await ValidatePasswordPolicyAsync(
            request.NewPassword,
            request.ConfirmPassword,
            cancellationToken);
        if (policyResult is not null)
        {
            return policyResult;
        }

        return await userRepository.ChangePasswordAsync(
            userName,
            request.OldPassword,
            request.NewPassword,
            cancellationToken);
    }

    public async Task<PasswordOperationResult> ResetPasswordAsync(
        Guid id,
        ResetUserPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var policyResult = await ValidatePasswordPolicyAsync(
            request.NewPassword,
            request.ConfirmPassword,
            cancellationToken);
        if (policyResult is not null)
        {
            return policyResult;
        }

        return await userRepository.ResetPasswordAsync(id, request.NewPassword, cancellationToken);
    }

    public Task<DeleteUserResult> DeleteAsync(
        Guid id,
        string? currentUserName,
        CancellationToken cancellationToken = default)
    {
        return userRepository.DeleteAsync(id, currentUserName, cancellationToken);
    }

    private async Task<PasswordOperationResult?> ValidatePasswordPolicyAsync(
        string newPassword,
        string confirmPassword,
        CancellationToken cancellationToken)
    {
        if (newPassword != confirmPassword)
        {
            return PasswordOperationResult.PasswordMismatch();
        }

        var minLength = await GetIntParameterAsync("Security.Password.MinLength", 6, cancellationToken);
        var requireDigit = await GetBoolParameterAsync("Security.Password.RequireDigit", true, cancellationToken);
        var requireLetter = await GetBoolParameterAsync("Security.Password.RequireLetter", true, cancellationToken);

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < minLength)
        {
            return PasswordOperationResult.PasswordPolicyViolation($"密码长度不能少于 {minLength} 位.");
        }

        if (requireDigit && !newPassword.Any(char.IsDigit))
        {
            return PasswordOperationResult.PasswordPolicyViolation("密码必须包含数字.");
        }

        if (requireLetter && !newPassword.Any(char.IsLetter))
        {
            return PasswordOperationResult.PasswordPolicyViolation("密码必须包含字母.");
        }

        return null;
    }

    private static (IReadOnlyList<UserImportRowDto> Rows, List<UserImportErrorDto> Errors) ParseImportRows(
        IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var parsedRows = new List<UserImportRowDto>();
        var errors = new List<UserImportErrorDto>();
        if (rows.Count < 2)
        {
            errors.Add(new UserImportErrorDto(1, string.Empty, "导入文件没有数据行."));
            return (parsedRows, errors);
        }

        for (var i = 1; i < rows.Count; i++)
        {
            var rowNumber = i + 1;
            var row = rows[i];
            if (row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var userName = GetCell(row, 0);
            var realName = GetCell(row, 1);
            var password = GetCell(row, 2);
            var departmentCode = GetOptionalCell(row, 3);
            var positionCode = GetOptionalCell(row, 4);
            var roleCodes = GetOptionalCell(row, 5)?
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ??
                [];
            var status = GetCell(row, 6);

            if (string.IsNullOrWhiteSpace(userName))
            {
                errors.Add(new UserImportErrorDto(rowNumber, string.Empty, "用户名不能为空."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(realName))
            {
                errors.Add(new UserImportErrorDto(rowNumber, userName, "姓名不能为空."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                errors.Add(new UserImportErrorDto(rowNumber, userName, "初始密码不能为空."));
                continue;
            }

            if (!TryParseEnabled(status, out var isEnabled))
            {
                errors.Add(new UserImportErrorDto(rowNumber, userName, "启用状态只能填写 启用、停用、1、0."));
                continue;
            }

            parsedRows.Add(new UserImportRowDto(
                rowNumber,
                userName.Trim(),
                realName.Trim(),
                password,
                departmentCode,
                positionCode,
                roleCodes,
                isEnabled));
        }

        return (parsedRows, errors);
    }

    private (IReadOnlyList<UserImportRowDto> Rows, List<UserImportErrorDto> Errors) ReadAndParseImportRows(
        Stream stream)
    {
        var rows = userImportExportService.ReadWorkbook(stream);
        return ParseImportRows(rows);
    }

    private static string GetCell(IReadOnlyList<string> row, int index)
    {
        return index < row.Count ? row[index].Trim() : string.Empty;
    }

    private static string? GetOptionalCell(IReadOnlyList<string> row, int index)
    {
        var value = GetCell(row, index);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static bool TryParseEnabled(string value, out bool isEnabled)
    {
        isEnabled = true;
        if (string.Equals(value, "启用", StringComparison.OrdinalIgnoreCase) ||
            value == "1" ||
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(value, "停用", StringComparison.OrdinalIgnoreCase) ||
            value == "0" ||
            string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
        {
            isEnabled = false;
            return true;
        }

        return false;
    }

    private async Task<int> GetIntParameterAsync(
        string key,
        int defaultValue,
        CancellationToken cancellationToken)
    {
        var value = await systemParameterRepository.GetValueByKeyAsync(key, cancellationToken);
        return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : defaultValue;
    }

    private async Task<bool> GetBoolParameterAsync(
        string key,
        bool defaultValue,
        CancellationToken cancellationToken)
    {
        var value = await systemParameterRepository.GetValueByKeyAsync(key, cancellationToken);
        return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
    }
}
