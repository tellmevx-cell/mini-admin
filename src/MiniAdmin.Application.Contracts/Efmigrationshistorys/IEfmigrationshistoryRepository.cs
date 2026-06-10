using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.CodeGenerators;

namespace MiniAdmin.Application.Contracts.Efmigrationshistorys;

public interface IEfmigrationshistoryRepository : IGeneratedCrudRepository
{
    Task<PageResult<EfmigrationshistoryDto>> GetListAsync(EfmigrationshistoryListQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EfmigrationshistoryDto>> GetExportListAsync(EfmigrationshistoryListQuery query, int limit = 10000, CancellationToken cancellationToken = default);

    Task<EfmigrationshistoryDto> CreateAsync(SaveEfmigrationshistoryRequest request, CancellationToken cancellationToken = default);

    Task<EfmigrationshistoryDto?> UpdateAsync(Guid id, SaveEfmigrationshistoryRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<EfmigrationshistoryDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<EfmigrationshistoryDto?> SetWorkflowStateAsync(Guid id, string approvalStatus, string? workflowInstanceId, CancellationToken cancellationToken = default);
}