using MiniAdmin.Application.Contracts.Common;

namespace MiniAdmin.Application.Contracts.Users;

public interface IUserRepository
{
    Task<CurrentUserDto> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default);

    Task<PageResult<UserListItemDto>> GetListAsync(
        UserListQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserListItemDto>> GetExportListAsync(
        UserListQuery query,
        CancellationToken cancellationToken = default);

    Task<UserImportResultDto> ValidateImportAsync(
        IReadOnlyList<UserImportRowDto> rows,
        string? currentUserName,
        CancellationToken cancellationToken = default);

    Task<UserImportResultDto> ImportAsync(
        IReadOnlyList<UserImportRowDto> rows,
        string? currentUserName,
        CancellationToken cancellationToken = default);

    Task<UserListItemDto> CreateAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default);

    Task<UserListItemDto?> UpdateAsync(
        Guid id,
        string? currentUserName,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default);

    Task<PasswordOperationResult> ChangePasswordAsync(
        string userName,
        string oldPassword,
        string newPassword,
        CancellationToken cancellationToken = default);

    Task<PasswordOperationResult> ResetPasswordAsync(
        Guid id,
        string newPassword,
        CancellationToken cancellationToken = default);

    Task<DeleteUserResult> DeleteAsync(
        Guid id,
        string? currentUserName,
        CancellationToken cancellationToken = default);
}
