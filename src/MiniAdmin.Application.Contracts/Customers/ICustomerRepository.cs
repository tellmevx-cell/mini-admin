using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.CodeGenerators;

namespace MiniAdmin.Application.Contracts.Customers;

public interface ICustomerRepository : IGeneratedCrudRepository
{
    Task<PageResult<CustomerDto>> GetListAsync(CustomerListQuery query, CancellationToken cancellationToken = default);

    Task<CustomerDto> CreateAsync(SaveCustomerRequest request, CancellationToken cancellationToken = default);

    Task<CustomerDto?> UpdateAsync(Guid id, SaveCustomerRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}