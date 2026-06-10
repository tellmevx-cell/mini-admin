using MiniAdmin.Application.Contracts.Departments;

namespace MiniAdmin.Application.Departments;

public sealed class DepartmentAppService(IDepartmentRepository departmentRepository) : IDepartmentAppService
{
    public Task<IReadOnlyList<DepartmentItemDto>> GetListAsync(CancellationToken cancellationToken = default)
    {
        return departmentRepository.GetListAsync(cancellationToken);
    }

    public Task<DepartmentItemDto> CreateAsync(
        SaveDepartmentRequest request,
        CancellationToken cancellationToken = default)
    {
        return departmentRepository.CreateAsync(request, cancellationToken);
    }

    public Task<DepartmentItemDto?> UpdateAsync(
        Guid id,
        SaveDepartmentRequest request,
        CancellationToken cancellationToken = default)
    {
        return departmentRepository.UpdateAsync(id, request, cancellationToken);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return departmentRepository.DeleteAsync(id, cancellationToken);
    }
}
