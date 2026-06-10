using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.Efmigrationshistorys;
using MiniAdmin.Application.Contracts.Workflows;
using MiniAdmin.Domain.Entities;
using System.Text.Json;


namespace MiniAdmin.Application.Efmigrationshistorys;

public sealed class EfmigrationshistoryAppService(IEfmigrationshistoryRepository efmigrationshistoryRepository, IWorkbookService workbookService, IWorkflowAppService workflowAppService) : IEfmigrationshistoryAppService
{
    public Task<PageResult<EfmigrationshistoryDto>> GetListAsync(EfmigrationshistoryListQuery query, CancellationToken cancellationToken = default)
    {
        return efmigrationshistoryRepository.GetListAsync(query, cancellationToken);
    }

    public async Task<EfmigrationshistoryExportFileDto> ExportAsync(EfmigrationshistoryListQuery query, CancellationToken cancellationToken = default)
    {
        var items = await efmigrationshistoryRepository.GetExportListAsync(query, cancellationToken: cancellationToken);
        var rows = new List<IReadOnlyList<string>>
        {
            new[] { "ProductVersion" }
        };
        rows.AddRange(items.Select(item => (IReadOnlyList<string>)new[]
        {
                Convert.ToString(item.ProductVersion) ?? string.Empty
        }));

        return new EfmigrationshistoryExportFileDto(
            "mini-admin-efmigrationshistory.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            workbookService.CreateWorkbook(rows));
    }

    public Task<EfmigrationshistoryExportFileDto> GetImportTemplateAsync(CancellationToken cancellationToken = default)
    {
        var rows = new List<IReadOnlyList<string>>
        {
            new[] { "ProductVersion" },
            new[] { "示例ProductVersion" }
        };

        return Task.FromResult(new EfmigrationshistoryExportFileDto(
            "mini-admin-efmigrationshistory-import-template.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            workbookService.CreateWorkbook(rows)));
    }

    public Task<EfmigrationshistoryImportResultDto> PreviewImportAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var parsed = ParseImportRows(workbookService.ReadWorkbook(stream));
        return Task.FromResult(new EfmigrationshistoryImportResultDto(
            parsed.Errors.Count == 0 ? parsed.Requests.Count : 0,
            parsed.Errors));
    }

    public async Task<EfmigrationshistoryImportResultDto> ImportAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var parsed = ParseImportRows(workbookService.ReadWorkbook(stream));
        if (parsed.Errors.Count > 0)
        {
            return new EfmigrationshistoryImportResultDto(0, parsed.Errors);
        }

        foreach (var request in parsed.Requests)
        {
            await efmigrationshistoryRepository.CreateAsync(request, cancellationToken);
        }

        return new EfmigrationshistoryImportResultDto(parsed.Requests.Count, []);
    }

    public Task<EfmigrationshistoryExportFileDto> ExportImportErrorsAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var rows = workbookService.ReadWorkbook(stream);
        var parsed = ParseImportRows(rows);
        var errorRows = new List<IReadOnlyList<string>>
        {
            new[] { "ProductVersion", "失败原因" }
        };
        foreach (var error in parsed.Errors)
        {
            var sourceRow = error.RowNumber - 1 >= 0 && error.RowNumber - 1 < rows.Count
                ? rows[error.RowNumber - 1]
                : Array.Empty<string>();
            errorRows.Add(new[]
            {
                GetCell(sourceRow, 0),
                error.Message
            });
        }

        return Task.FromResult(new EfmigrationshistoryExportFileDto(
            "mini-admin-efmigrationshistory-import-errors.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            workbookService.CreateWorkbook(errorRows)));
    }

    private static (IReadOnlyList<SaveEfmigrationshistoryRequest> Requests, IReadOnlyList<EfmigrationshistoryImportErrorDto> Errors) ParseImportRows(
        IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var requests = new List<SaveEfmigrationshistoryRequest>();
        var errors = new List<EfmigrationshistoryImportErrorDto>();
        if (rows.Count < 2)
        {
            errors.Add(new EfmigrationshistoryImportErrorDto(1, string.Empty, "导入文件没有数据行."));
            return (requests, errors);
        }

        for (var i = 1; i < rows.Count; i++)
        {
            var rowNumber = i + 1;
            var row = rows[i];
            if (row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

        if (string.IsNullOrWhiteSpace(GetCell(row, 0)))
        {
            errors.Add(new EfmigrationshistoryImportErrorDto(rowNumber, "ProductVersion", "ProductVersion不能为空."));
            continue;
        }
            try
            {
                requests.Add(new SaveEfmigrationshistoryRequest(
                ConvertCell<string>(GetCell(row, 0))));
            }
            catch (Exception ex)
            {
                errors.Add(new EfmigrationshistoryImportErrorDto(rowNumber, string.Empty, $"数据格式不正确：{ex.Message}"));
            }
        }

        return (requests, errors);
    }

    private static string GetCell(IReadOnlyList<string> row, int index)
    {
        return index < row.Count ? row[index].Trim() : string.Empty;
    }

    private static T ConvertCell<T>(string value)
    {
        var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        if (string.IsNullOrWhiteSpace(value))
        {
            return default!;
        }

        if (targetType == typeof(string))
        {
            return (T)(object)value;
        }

        if (targetType == typeof(bool))
        {
            if (value == "启用" || value == "1")
            {
                return (T)(object)true;
            }

            if (value == "停用" || value == "0")
            {
                return (T)(object)false;
            }
        }

        if (targetType == typeof(Guid))
        {
            return (T)(object)Guid.Parse(value);
        }

        if (targetType == typeof(DateTimeOffset))
        {
            return (T)(object)DateTimeOffset.Parse(value);
        }

        return (T)Convert.ChangeType(value, targetType);
    }

    public Task<EfmigrationshistoryDto> CreateAsync(SaveEfmigrationshistoryRequest request, CancellationToken cancellationToken = default)
    {
        return efmigrationshistoryRepository.CreateAsync(request, cancellationToken);
    }

    public Task<EfmigrationshistoryDto?> UpdateAsync(Guid id, SaveEfmigrationshistoryRequest request, CancellationToken cancellationToken = default)
    {
        return efmigrationshistoryRepository.UpdateAsync(id, request, cancellationToken);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return efmigrationshistoryRepository.DeleteAsync(id, cancellationToken);
    }

    private static readonly JsonSerializerOptions WorkflowJsonOptions = new()
    {
        WriteIndented = false
    };

    public async Task<EfmigrationshistoryDto?> SubmitWorkflowAsync(
        Guid id,
        SubmitEfmigrationshistoryWorkflowRequest request,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default)
    {
        var item = await efmigrationshistoryRepository.GetAsync(id, cancellationToken);
        if (item is null)
        {
            return null;
        }

        if (item.ApprovalStatus is "Pending")
        {
            throw new InvalidOperationException("__efmigrationshistory已在审批中，不能重复提交。");
        }

        if (item.ApprovalStatus is "Approved")
        {
            throw new InvalidOperationException("__efmigrationshistory已审批通过，不能重新提交。");
        }

        var definition = await workflowAppService.ResolveBusinessDefinitionAsync("efmigrationshistory", cancellationToken)
            ?? throw new InvalidOperationException("未配置可用的__efmigrationshistory审批流程，请先在审批中心配置业务绑定。");
        var formDataJson = JsonSerializer.Serialize(new
        {
            id = item.Id,
            businessName = "__efmigrationshistory",
            approvalStatus = item.ApprovalStatus,
            comment = request.Comment
        }, WorkflowJsonOptions);
        var instance = await workflowAppService.StartInstanceAsync(
            new StartWorkflowInstanceRequest(
                Guid.Parse(definition.DefinitionId),
                "__efmigrationshistory审批：" + item.Id,
                global::MiniAdmin.Domain.Entities.Efmigrationshistory.CreateBusinessKey(id),
                formDataJson),
            user,
            cancellationToken);

        return await efmigrationshistoryRepository.SetWorkflowStateAsync(id, "Pending", instance.Id, cancellationToken);
    }

    public async Task<EfmigrationshistoryDto?> WithdrawWorkflowAsync(
        Guid id,
        WithdrawEfmigrationshistoryWorkflowRequest request,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default)
    {
        var item = await efmigrationshistoryRepository.GetAsync(id, cancellationToken);
        if (item is null)
        {
            return null;
        }

        if (item.ApprovalStatus is not "Pending")
        {
            throw new InvalidOperationException("只有审批中的__efmigrationshistory可以撤回。");
        }

        if (string.IsNullOrWhiteSpace(item.WorkflowInstanceId) ||
            !Guid.TryParse(item.WorkflowInstanceId, out var workflowInstanceId))
        {
            throw new InvalidOperationException("__efmigrationshistory没有关联的流程实例，无法撤回。");
        }

        var withdrawn = await workflowAppService.WithdrawAsync(
            workflowInstanceId,
            new WorkflowActionRequest(request.Comment),
            user,
            cancellationToken) ?? throw new InvalidOperationException("关联流程实例不存在，无法撤回。");

        return await efmigrationshistoryRepository.SetWorkflowStateAsync(id, "Withdrawn", withdrawn.Id, cancellationToken);
    }
}