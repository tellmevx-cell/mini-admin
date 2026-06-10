using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.Positions;

namespace MiniAdmin.Application.Positions;

public sealed class PositionAppService(
    IPositionRepository positionRepository,
    IWorkbookService workbookService) : IPositionAppService
{
    private static readonly string[] ImportHeaders =
    [
        "岗位编码",
        "岗位名称",
        "排序",
        "备注",
        "启用状态"
    ];

    public Task<PageResult<PositionDto>> GetListAsync(
        PositionListQuery query,
        CancellationToken cancellationToken = default)
    {
        return positionRepository.GetListAsync(query, cancellationToken);
    }

    public async Task<PositionExportFileDto> ExportAsync(
        PositionListQuery query,
        CancellationToken cancellationToken = default)
    {
        var positions = await positionRepository.GetExportListAsync(query, cancellationToken: cancellationToken);
        var rows = new List<IReadOnlyList<string>>
        {
            ImportHeaders
        };
        rows.AddRange(positions.Select(position => (IReadOnlyList<string>)new[]
        {
            position.Code,
            position.Name,
            position.Order.ToString(),
            position.Remark ?? string.Empty,
            position.IsEnabled ? "启用" : "停用"
        }));

        return new PositionExportFileDto(
            "mini-admin-positions.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            workbookService.CreateWorkbook(rows));
    }

    public Task<PositionExportFileDto> GetImportTemplateAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = new List<IReadOnlyList<string>>
        {
            ImportHeaders,
            new[]
            {
                "developer",
                "开发工程师",
                "10",
                "示例岗位",
                "启用"
            }
        };

        return Task.FromResult(new PositionExportFileDto(
            "mini-admin-position-import-template.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            workbookService.CreateWorkbook(rows)));
    }

    public async Task<PositionImportResultDto> PreviewImportAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var rows = workbookService.ReadWorkbook(stream);
        var importRows = ParseImportRows(rows);

        if (importRows.Errors.Count > 0)
        {
            return new PositionImportResultDto(0, importRows.Errors);
        }

        return await positionRepository.ValidateImportAsync(importRows.Rows, cancellationToken);
    }

    public async Task<PositionImportResultDto> ImportAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var rows = workbookService.ReadWorkbook(stream);
        var importRows = ParseImportRows(rows);

        if (importRows.Errors.Count > 0)
        {
            return new PositionImportResultDto(0, importRows.Errors);
        }

        return await positionRepository.ImportAsync(importRows.Rows, cancellationToken);
    }

    public async Task<PositionExportFileDto> ExportImportErrorsAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var rows = workbookService.ReadWorkbook(stream);
        var importRows = ParseImportRows(rows);
        var validationResult = importRows.Errors.Count > 0
            ? new PositionImportResultDto(0, importRows.Errors)
            : await positionRepository.ValidateImportAsync(importRows.Rows, cancellationToken);
        var errorRows = new List<IReadOnlyList<string>>
        {
            ImportHeaders.Concat(["失败原因"]).ToArray()
        };

        foreach (var error in validationResult.Errors)
        {
            var sourceRow = error.RowNumber - 1 >= 0 && error.RowNumber - 1 < rows.Count
                ? rows[error.RowNumber - 1]
                : Array.Empty<string>();
            errorRows.Add(new[]
            {
                GetCell(sourceRow, 0),
                GetCell(sourceRow, 1),
                GetCell(sourceRow, 2),
                GetCell(sourceRow, 3),
                GetCell(sourceRow, 4),
                error.Message
            });
        }

        return new PositionExportFileDto(
            "mini-admin-position-import-errors.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            workbookService.CreateWorkbook(errorRows));
    }

    public Task<PositionDto> CreateAsync(
        SavePositionRequest request,
        CancellationToken cancellationToken = default)
    {
        return positionRepository.CreateAsync(request, cancellationToken);
    }

    public Task<PositionDto?> UpdateAsync(
        Guid id,
        SavePositionRequest request,
        CancellationToken cancellationToken = default)
    {
        return positionRepository.UpdateAsync(id, request, cancellationToken);
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return positionRepository.DeleteAsync(id, cancellationToken);
    }

    private static (IReadOnlyList<PositionImportRowDto> Rows, List<PositionImportErrorDto> Errors) ParseImportRows(
        IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var parsedRows = new List<PositionImportRowDto>();
        var errors = new List<PositionImportErrorDto>();
        var importedCodeSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (rows.Count < 2)
        {
            errors.Add(new PositionImportErrorDto(1, string.Empty, "导入文件没有数据行."));
            return (parsedRows, errors);
        }

        for (var i = 1; i < rows.Count; i++)
        {
            var rowNumber = i + 1;
            var row = rows[i];
            if (row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var code = GetCell(row, 0);
            var name = GetCell(row, 1);
            var orderValue = GetCell(row, 2);
            var remark = GetOptionalCell(row, 3);
            var enabledValue = GetCell(row, 4);

            if (string.IsNullOrWhiteSpace(code))
            {
                errors.Add(new PositionImportErrorDto(rowNumber, string.Empty, "岗位编码不能为空."));
                continue;
            }

            if (!importedCodeSet.Add(code))
            {
                errors.Add(new PositionImportErrorDto(rowNumber, code, "导入文件中岗位编码重复."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(new PositionImportErrorDto(rowNumber, code, "岗位名称不能为空."));
                continue;
            }

            if (!int.TryParse(orderValue, out var order))
            {
                errors.Add(new PositionImportErrorDto(rowNumber, code, "排序必须是整数."));
                continue;
            }

            if (!TryParseEnabled(enabledValue, out var isEnabled))
            {
                errors.Add(new PositionImportErrorDto(rowNumber, code, "启用状态只能填写 启用、停用、1、0."));
                continue;
            }

            parsedRows.Add(new PositionImportRowDto(
                rowNumber,
                code.Trim(),
                name.Trim(),
                order,
                remark,
                isEnabled));
        }

        return (parsedRows, errors);
    }

    private static string GetCell(IReadOnlyList<string> row, int index)
    {
        return index < row.Count ? row[index].Trim() : string.Empty;
    }

    private static string? GetOptionalCell(IReadOnlyList<string> row, int index)
    {
        var value = GetCell(row, index);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static bool TryParseEnabled(string value, out bool isEnabled)
    {
        var normalized = value.Trim();
        if (string.Equals(normalized, "启用", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "true", StringComparison.OrdinalIgnoreCase) ||
            normalized == "1")
        {
            isEnabled = true;
            return true;
        }

        if (string.Equals(normalized, "停用", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalized, "false", StringComparison.OrdinalIgnoreCase) ||
            normalized == "0")
        {
            isEnabled = false;
            return true;
        }

        isEnabled = false;
        return false;
    }
}
