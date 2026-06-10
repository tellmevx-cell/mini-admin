using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.Customers;

namespace MiniAdmin.Application.Customers;

public sealed class CustomerAppService(ICustomerRepository customerRepository) : ICustomerAppService
{
    public Task<PageResult<CustomerDto>> GetListAsync(CustomerListQuery query, CancellationToken cancellationToken = default)
    {
        return customerRepository.GetListAsync(query, cancellationToken);
    }

    public Task<CustomerDto> CreateAsync(SaveCustomerRequest request, CancellationToken cancellationToken = default)
    {
        return customerRepository.CreateAsync(request, cancellationToken);
    }

    public Task<CustomerDto?> UpdateAsync(Guid id, SaveCustomerRequest request, CancellationToken cancellationToken = default)
    {
        return customerRepository.UpdateAsync(id, request, cancellationToken);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return customerRepository.DeleteAsync(id, cancellationToken);
    }
}