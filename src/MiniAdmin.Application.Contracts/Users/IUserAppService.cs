using MiniAdmin.Application.Contracts.Common;

namespace MiniAdmin.Application.Contracts.Users;

public interface IUserAppService
{
    Task<CurrentUserDto> GetCurrentUserAsync(string userName, CancellationToken cancellationToken = default);

    Task<PageResult<UserListItemDto>> GetListAsync(
        UserListQuery query,
        CancellationToken cancellationToken = default);

    Task<UserExportFileDto> ExportAsync(
        UserListQuery query,
        CancellationToken cancellationToken = default);

    Task<UserExportFileDto> GetImportTemplateAsync(
        CancellationToken cancellationToken = default);

    Task<UserImportResultDto> PreviewImportAsync(
        Stream stream,
        string? currentUserName,
        CancellationToken cancellationToken = default);

    Task<UserExportFileDto> ExportImportErrorsAsync(
        Stream stream,
        string? currentUserName,
        CancellationToken cancellationToken = default);

    Task<UserImportResultDto> ImportAsync(
        Stream stream,
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
        ChangeCurrentUserPasswordRequest request,
        CancellationToken cancellationToken = default);

    Task<PasswordOperationResult> ResetPasswordAsync(
        Guid id,
        ResetUserPasswordRequest request,
        CancellationToken cancellationToken = default);

    Task<DeleteUserResult> DeleteAsync(
        Guid id,
        string? currentUserName,
        CancellationToken cancellationToken = default);
}
