using MiniAdmin.Application.Contracts.Common;

namespace MiniAdmin.Application.Contracts.Parameters;

public interface ISystemParameterAppService
{
    Task<PageResult<SystemParameterDto>> GetListAsync(
        SystemParameterListQuery query,
        CancellationToken cancellationToken = default);

    Task<SystemParameterDto> CreateAsync(
        SaveSystemParameterRequest request,
        CancellationToken cancellationToken = default);

    Task<SystemParameterDto?> UpdateAsync(
        Guid id,
        SaveSystemParameterRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
