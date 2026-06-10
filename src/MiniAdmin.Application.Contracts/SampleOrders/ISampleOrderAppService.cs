using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.CodeGenerators;
using MiniAdmin.Application.Contracts.Workflows;

namespace MiniAdmin.Application.Contracts.SampleOrders;

public interface ISampleOrderAppService : IGeneratedCrudAppService
{
    Task<PageResult<SampleOrderDto>> GetListAsync(SampleOrderListQuery query, CancellationToken cancellationToken = default);

    Task<SampleOrderDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SampleOrderDto> CreateAsync(SaveSampleOrderRequest request, CancellationToken cancellationToken = default);

    Task<SampleOrderDto?> UpdateAsync(Guid id, SaveSampleOrderRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<SampleOrderDto?> SubmitWorkflowAsync(
        Guid id,
        SubmitSampleOrderWorkflowRequest request,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default);

    Task<SampleOrderDto?> WithdrawWorkflowAsync(
        Guid id,
        WithdrawSampleOrderWorkflowRequest request,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default);
}
