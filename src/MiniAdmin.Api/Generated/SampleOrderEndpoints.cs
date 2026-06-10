using System.Security.Claims;
using MiniAdmin.Api.CodeGenerators;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.SampleOrders;
using MiniAdmin.Application.Contracts.Workflows;
using MiniAdmin.Shared;

namespace MiniAdmin.Api.Generated;

public sealed class SampleOrderEndpoints : IGeneratedCrudEndpointDefinition
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/business/sample-order/list", async (
            [AsParameters] SampleOrderListQuery query,
            ISampleOrderAppService sampleOrderAppService,
            CancellationToken cancellationToken) =>
        {
            var result = await sampleOrderAppService.GetListAsync(query, cancellationToken);
            return Results.Ok(ApiResponse<PageResult<SampleOrderDto>>.Ok(result));
        }).RequirePermission("business:sample-order:query");

        endpoints.MapPost("/business/sample-order", async (
            SaveSampleOrderRequest request,
            ISampleOrderAppService sampleOrderAppService,
            CancellationToken cancellationToken) =>
        {
            var result = await sampleOrderAppService.CreateAsync(request, cancellationToken);
            return Results.Ok(ApiResponse<SampleOrderDto>.Ok(result));
        }).RequirePermission("business:sample-order:create");

        endpoints.MapPut("/business/sample-order/{id:guid}", async (
            Guid id,
            SaveSampleOrderRequest request,
            ISampleOrderAppService sampleOrderAppService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await sampleOrderAppService.UpdateAsync(id, request, cancellationToken);
                return result is null
                    ? Results.NotFound(ApiResponse<SampleOrderDto?>.Fail("SampleOrder not found."))
                    : Results.Ok(ApiResponse<SampleOrderDto>.Ok(result));
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(ApiResponse<SampleOrderDto?>.Fail(exception.Message));
            }
        }).RequirePermission("business:sample-order:update");

        endpoints.MapDelete("/business/sample-order/{id:guid}", async (
            Guid id,
            ISampleOrderAppService sampleOrderAppService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var deleted = await sampleOrderAppService.DeleteAsync(id, cancellationToken);
                return deleted
                    ? Results.Ok(ApiResponse<bool>.Ok(true))
                    : Results.NotFound(ApiResponse<bool>.Fail("SampleOrder not found."));
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(ApiResponse<bool>.Fail(exception.Message));
            }
        }).RequirePermission("business:sample-order:delete");

        endpoints.MapPost("/business/sample-order/{id:guid}/submit-workflow", async (
            Guid id,
            SubmitSampleOrderWorkflowRequest request,
            ClaimsPrincipal user,
            ISampleOrderAppService sampleOrderAppService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await sampleOrderAppService.SubmitWorkflowAsync(
                    id,
                    request,
                    GetWorkflowUserContext(user),
                    cancellationToken);
                return result is null
                    ? Results.NotFound(ApiResponse<SampleOrderDto?>.Fail("SampleOrder not found."))
                    : Results.Ok(ApiResponse<SampleOrderDto>.Ok(result));
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(ApiResponse<SampleOrderDto?>.Fail(exception.Message));
            }
        }).RequirePermission("business:sample-order:submit-workflow");

        endpoints.MapPost("/business/sample-order/{id:guid}/withdraw-workflow", async (
            Guid id,
            WithdrawSampleOrderWorkflowRequest request,
            ClaimsPrincipal user,
            ISampleOrderAppService sampleOrderAppService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await sampleOrderAppService.WithdrawWorkflowAsync(
                    id,
                    request,
                    GetWorkflowUserContext(user),
                    cancellationToken);
                return result is null
                    ? Results.NotFound(ApiResponse<SampleOrderDto?>.Fail("SampleOrder not found."))
                    : Results.Ok(ApiResponse<SampleOrderDto>.Ok(result));
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(ApiResponse<SampleOrderDto?>.Fail(exception.Message));
            }
        }).RequirePermission("business:sample-order:withdraw-workflow");
    }

    private static WorkflowUserContext GetWorkflowUserContext(ClaimsPrincipal principal)
    {
        return new WorkflowUserContext(
            GetRequiredUserId(principal),
            GetRequiredUserName(principal));
    }

    private static string GetRequiredUserName(ClaimsPrincipal principal)
    {
        return principal.Identity?.Name
            ?? principal.FindFirstValue(ClaimTypes.Name)
            ?? throw new InvalidOperationException("Authenticated user name is missing.");
    }

    private static Guid GetRequiredUserId(ClaimsPrincipal principal)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userId, out var value)
            ? value
            : throw new InvalidOperationException("Authenticated user id is missing.");
    }
}
