namespace MiniAdmin.Application.Contracts.Departments;

public interface IDepartmentAppService
{
    Task<IReadOnlyList<DepartmentItemDto>> GetListAsync(CancellationToken cancellationToken = default);

    Task<DepartmentItemDto> CreateAsync(SaveDepartmentRequest request, CancellationToken cancellationToken = default);

    Task<DepartmentItemDto?> UpdateAsync(Guid id, SaveDepartmentRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
