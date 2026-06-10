using System.Security.Claims;

using MiniAdmin.Api.CodeGenerators;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.Efmigrationshistorys;
using MiniAdmin.Application.Contracts.Workflows;

using MiniAdmin.Shared;

namespace MiniAdmin.Api.Generated;

public sealed class EfmigrationshistoryEndpoints : IGeneratedCrudEndpointDefinition
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/business/efmigrationshistory/list", async (
            [AsParameters] EfmigrationshistoryListQuery query,

            IEfmigrationshistoryAppService efmigrationshistoryAppService,
            CancellationToken cancellationToken) =>
        {
            var result = await efmigrationshistoryAppService.GetListAsync(query, cancellationToken);
            return Results.Ok(ApiResponse<PageResult<EfmigrationshistoryDto>>.Ok(result));
        }).RequirePermission("business:efmigrationshistory:query");

        endpoints.MapGet("/business/efmigrationshistory/export", async (
            [AsParameters] EfmigrationshistoryListQuery query,

            IEfmigrationshistoryAppService efmigrationshistoryAppService,
            CancellationToken cancellationToken) =>
        {
            var file = await efmigrationshistoryAppService.ExportAsync(query, cancellationToken);
            return Results.File(file.Content, file.ContentType, file.FileName);
        }).RequirePermission("business:efmigrationshistory:export");

        endpoints.MapGet("/business/efmigrationshistory/import-template", async (
            IEfmigrationshistoryAppService efmigrationshistoryAppService,
            CancellationToken cancellationToken) =>
        {
            var file = await efmigrationshistoryAppService.GetImportTemplateAsync(cancellationToken);
            return Results.File(file.Content, file.ContentType, file.FileName);
        }).RequirePermission("business:efmigrationshistory:import");

        endpoints.MapPost("/business/efmigrationshistory/import/preview", async (
            IFormFile file,
            IEfmigrationshistoryAppService efmigrationshistoryAppService,
            CancellationToken cancellationToken) =>
        {
            await using var stream = file.OpenReadStream();
            var result = await efmigrationshistoryAppService.PreviewImportAsync(stream, cancellationToken);
            return Results.Ok(ApiResponse<EfmigrationshistoryImportResultDto>.Ok(result));
        }).DisableAntiforgery().RequirePermission("business:efmigrationshistory:import");

        endpoints.MapPost("/business/efmigrationshistory/import/error-report", async (
            IFormFile file,
            IEfmigrationshistoryAppService efmigrationshistoryAppService,
            CancellationToken cancellationToken) =>
        {
            await using var stream = file.OpenReadStream();
            var report = await efmigrationshistoryAppService.ExportImportErrorsAsync(stream, cancellationToken);
            return Results.File(report.Content, report.ContentType, report.FileName);
        }).DisableAntiforgery().RequirePermission("business:efmigrationshistory:import");

        endpoints.MapPost("/business/efmigrationshistory/import", async (
            IFormFile file,
            IEfmigrationshistoryAppService efmigrationshistoryAppService,
            CancellationToken cancellationToken) =>
        {
            await using var stream = file.OpenReadStream();
            var result = await efmigrationshistoryAppService.ImportAsync(stream, cancellationToken);
            return Results.Ok(ApiResponse<EfmigrationshistoryImportResultDto>.Ok(result));
        }).DisableAntiforgery().RequirePermission("business:efmigrationshistory:import");

        endpoints.MapPost("/business/efmigrationshistory", async (
            SaveEfmigrationshistoryRequest request,
            IEfmigrationshistoryAppService efmigrationshistoryAppService,
            CancellationToken cancellationToken) =>
        {
            var result = await efmigrationshistoryAppService.CreateAsync(request, cancellationToken);
            return Results.Ok(ApiResponse<EfmigrationshistoryDto>.Ok(result));
        }).RequirePermission("business:efmigrationshistory:create");

        endpoints.MapPut("/business/efmigrationshistory/{id:guid}", async (
            Guid id,
            SaveEfmigrationshistoryRequest request,

            IEfmigrationshistoryAppService efmigrationshistoryAppService,
            CancellationToken cancellationToken) =>
        {
            var result = await efmigrationshistoryAppService.UpdateAsync(id, request, cancellationToken);
            return result is null
                ? Results.NotFound(ApiResponse<EfmigrationshistoryDto?>.Fail("Efmigrationshistory not found."))
                : Results.Ok(ApiResponse<EfmigrationshistoryDto>.Ok(result));
        }).RequirePermission("business:efmigrationshistory:update");

        endpoints.MapDelete("/business/efmigrationshistory/{id:guid}", async (
            Guid id,

            IEfmigrationshistoryAppService efmigrationshistoryAppService,
            CancellationToken cancellationToken) =>
        {
            var deleted = await efmigrationshistoryAppService.DeleteAsync(id, cancellationToken);
            return deleted
                ? Results.Ok(ApiResponse<bool>.Ok(true))
                : Results.NotFound(ApiResponse<bool>.Fail("Efmigrationshistory not found."));
        }).RequirePermission("business:efmigrationshistory:delete");

        endpoints.MapPost("/business/efmigrationshistory/{id:guid}/submit-workflow", async (
            Guid id,
            SubmitEfmigrationshistoryWorkflowRequest request,
            ClaimsPrincipal user,
            IEfmigrationshistoryAppService efmigrationshistoryAppService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await efmigrationshistoryAppService.SubmitWorkflowAsync(
                    id,
                    request,
                    GetWorkflowUserContext(user),
                    cancellationToken);
                return result is null
                    ? Results.NotFound(ApiResponse<EfmigrationshistoryDto?>.Fail("Efmigrationshistory not found."))
                    : Results.Ok(ApiResponse<EfmigrationshistoryDto>.Ok(result));
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(ApiResponse<EfmigrationshistoryDto?>.Fail(exception.Message));
            }
        }).RequirePermission("business:efmigrationshistory:submit-workflow");

        endpoints.MapPost("/business/efmigrationshistory/{id:guid}/withdraw-workflow", async (
            Guid id,
            WithdrawEfmigrationshistoryWorkflowRequest request,
            ClaimsPrincipal user,
            IEfmigrationshistoryAppService efmigrationshistoryAppService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var result = await efmigrationshistoryAppService.WithdrawWorkflowAsync(
                    id,
                    request,
                    GetWorkflowUserContext(user),
                    cancellationToken);
                return result is null
                    ? Results.NotFound(ApiResponse<EfmigrationshistoryDto?>.Fail("Efmigrationshistory not found."))
                    : Results.Ok(ApiResponse<EfmigrationshistoryDto>.Ok(result));
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(ApiResponse<EfmigrationshistoryDto?>.Fail(exception.Message));
            }
        }).RequirePermission("business:efmigrationshistory:withdraw-workflow");
    }

    private static string GetRequiredUserName(ClaimsPrincipal principal)
    {
        return principal.Identity?.Name
            ?? principal.FindFirstValue(ClaimTypes.Name)
            ?? throw new InvalidOperationException("Authenticated user name is missing.");
    }

    private static WorkflowUserContext GetWorkflowUserContext(ClaimsPrincipal principal)
    {
        return new WorkflowUserContext(
            GetRequiredUserId(principal),
            GetRequiredUserName(principal));
    }

    private static Guid GetRequiredUserId(ClaimsPrincipal principal)
    {
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userId, out var value)
            ? value
            : throw new InvalidOperationException("Authenticated user id is missing.");
    }
}