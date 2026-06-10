using MiniAdmin.Api.CodeGenerators;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.Customers;
using MiniAdmin.Shared;

namespace MiniAdmin.Api.Generated;

public sealed class CustomerEndpoints : IGeneratedCrudEndpointDefinition
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/business/customer/list", async (
            [AsParameters] CustomerListQuery query,
            ICustomerAppService customerAppService,
            CancellationToken cancellationToken) =>
        {
            var result = await customerAppService.GetListAsync(query, cancellationToken);
            return Results.Ok(ApiResponse<PageResult<CustomerDto>>.Ok(result));
        }).RequirePermission("business:customer:query");

        endpoints.MapPost("/business/customer", async (
            SaveCustomerRequest request,
            ICustomerAppService customerAppService,
            CancellationToken cancellationToken) =>
        {
            var result = await customerAppService.CreateAsync(request, cancellationToken);
            return Results.Ok(ApiResponse<CustomerDto>.Ok(result));
        }).RequirePermission("business:customer:create");

        endpoints.MapPut("/business/customer/{id:guid}", async (
            Guid id,
            SaveCustomerRequest request,
            ICustomerAppService customerAppService,
            CancellationToken cancellationToken) =>
        {
            var result = await customerAppService.UpdateAsync(id, request, cancellationToken);
            return result is null
                ? Results.NotFound(ApiResponse<CustomerDto?>.Fail("Customer not found."))
                : Results.Ok(ApiResponse<CustomerDto>.Ok(result));
        }).RequirePermission("business:customer:update");

        endpoints.MapDelete("/business/customer/{id:guid}", async (
            Guid id,
            ICustomerAppService customerAppService,
            CancellationToken cancellationToken) =>
        {
            var deleted = await customerAppService.DeleteAsync(id, cancellationToken);
            return deleted
                ? Results.Ok(ApiResponse<bool>.Ok(true))
                : Results.NotFound(ApiResponse<bool>.Fail("Customer not found."));
        }).RequirePermission("business:customer:delete");
    }
}