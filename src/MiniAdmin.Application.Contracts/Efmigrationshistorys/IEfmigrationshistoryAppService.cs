using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.CodeGenerators;
using MiniAdmin.Application.Contracts.Workflows;


namespace MiniAdmin.Application.Contracts.Efmigrationshistorys;

public interface IEfmigrationshistoryAppService : IGeneratedCrudAppService
{
    Task<PageResult<EfmigrationshistoryDto>> GetListAsync(EfmigrationshistoryListQuery query, CancellationToken cancellationToken = default);

    Task<EfmigrationshistoryExportFileDto> ExportAsync(EfmigrationshistoryListQuery query, CancellationToken cancellationToken = default);

    Task<EfmigrationshistoryExportFileDto> GetImportTemplateAsync(CancellationToken cancellationToken = default);

    Task<EfmigrationshistoryImportResultDto> PreviewImportAsync(Stream stream, CancellationToken cancellationToken = default);

    Task<EfmigrationshistoryImportResultDto> ImportAsync(Stream stream, CancellationToken cancellationToken = default);

    Task<EfmigrationshistoryExportFileDto> ExportImportErrorsAsync(Stream stream, CancellationToken cancellationToken = default);

    Task<EfmigrationshistoryDto> CreateAsync(SaveEfmigrationshistoryRequest request, CancellationToken cancellationToken = default);

    Task<EfmigrationshistoryDto?> UpdateAsync(Guid id, SaveEfmigrationshistoryRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<EfmigrationshistoryDto?> SubmitWorkflowAsync(
        Guid id,
        SubmitEfmigrationshistoryWorkflowRequest request,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default);

    Task<EfmigrationshistoryDto?> WithdrawWorkflowAsync(
        Guid id,
        WithdrawEfmigrationshistoryWorkflowRequest request,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default);
}