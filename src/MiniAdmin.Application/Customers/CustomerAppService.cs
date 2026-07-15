using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.Customers;
using MiniAdmin.Platform.DynamicApi;

namespace MiniAdmin.Application.Customers;

[DynamicApi("business/customer", Name = "Customer", Tag = "Business")]
public sealed class CustomerAppService(ICustomerRepository customerRepository) : ICustomerAppService
{
    [DynamicGet(
        "list",
        Permission = "business:customer:query",
        Resource = "business.customer",
        Action = "query",
        OperationId = "Customer_GetList",
        Summary = "查询客户资料")]
    public Task<PageResult<CustomerDto>> GetListAsync(CustomerListQuery query, CancellationToken cancellationToken = default)
    {
        return customerRepository.GetListAsync(query, cancellationToken);
    }

    [DynamicPost(
        Permission = "business:customer:create",
        Resource = "business.customer",
        Action = "create",
        OperationId = "Customer_Create",
        Summary = "创建客户资料")]
    public Task<CustomerDto> CreateAsync(SaveCustomerRequest request, CancellationToken cancellationToken = default)
    {
        return customerRepository.CreateAsync(request, cancellationToken);
    }

    [DynamicPut(
        "{id:guid}",
        Permission = "business:customer:update",
        Resource = "business.customer",
        Action = "update",
        OperationId = "Customer_Update",
        Summary = "更新客户资料")]
    public Task<CustomerDto?> UpdateAsync(Guid id, SaveCustomerRequest request, CancellationToken cancellationToken = default)
    {
        return customerRepository.UpdateAsync(id, request, cancellationToken);
    }

    [DynamicDelete(
        "{id:guid}",
        Permission = "business:customer:delete",
        Resource = "business.customer",
        Action = "delete",
        OperationId = "Customer_Delete",
        Summary = "删除客户资料")]
    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return customerRepository.DeleteAsync(id, cancellationToken);
    }
}
