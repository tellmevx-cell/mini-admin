using MiniAdmin.Application.Contracts.Common;

namespace MiniAdmin.Application.Contracts.Parameters;

public interface ISystemParameterRepository
{
    Task<PageResult<SystemParameterDto>> GetListAsync(
        SystemParameterListQuery query,
        CancellationToken cancellationToken = default);

    Task<string?> GetValueByKeyAsync(
        string key,
        CancellationToken cancellationToken = default);

    Task<SystemParameterDto> UpsertValueByKeyAsync(
        string key,
        string name,
        string value,
        string group,
        string? remark,
        int order,
        bool isEnabled,
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
