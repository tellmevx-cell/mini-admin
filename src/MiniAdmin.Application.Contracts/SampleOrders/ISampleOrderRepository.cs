using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.CodeGenerators;

namespace MiniAdmin.Application.Contracts.SampleOrders;

public interface ISampleOrderRepository : IGeneratedCrudRepository
{
    Task<PageResult<SampleOrderDto>> GetListAsync(SampleOrderListQuery query, CancellationToken cancellationToken = default);

    Task<SampleOrderDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SampleOrderDto> CreateAsync(SaveSampleOrderRequest request, CancellationToken cancellationToken = default);

    Task<SampleOrderDto?> UpdateAsync(Guid id, SaveSampleOrderRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
