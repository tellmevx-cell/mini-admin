using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.Parameters;

namespace MiniAdmin.Application.Parameters;

public sealed class SystemParameterAppService(
    ISystemParameterRepository systemParameterRepository) : ISystemParameterAppService
{
    public Task<PageResult<SystemParameterDto>> GetListAsync(
        SystemParameterListQuery query,
        CancellationToken cancellationToken = default)
    {
        return systemParameterRepository.GetListAsync(query, cancellationToken);
    }

    public Task<SystemParameterDto> CreateAsync(
        SaveSystemParameterRequest request,
        CancellationToken cancellationToken = default)
    {
        return systemParameterRepository.CreateAsync(request, cancellationToken);
    }

    public Task<SystemParameterDto?> UpdateAsync(
        Guid id,
        SaveSystemParameterRequest request,
        CancellationToken cancellationToken = default)
    {
        return systemParameterRepository.UpdateAsync(id, request, cancellationToken);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return systemParameterRepository.DeleteAsync(id, cancellationToken);
    }
}
