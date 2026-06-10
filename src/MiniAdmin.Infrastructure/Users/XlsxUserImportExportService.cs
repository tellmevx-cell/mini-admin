using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.Users;

namespace MiniAdmin.Infrastructure.Users;

public sealed class XlsxUserImportExportService(
    IWorkbookService workbookService) : IUserImportExportService
{
    public byte[] CreateWorkbook(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        return workbookService.CreateWorkbook(rows);
    }

    public IReadOnlyList<IReadOnlyList<string>> ReadWorkbook(Stream stream)
    {
        return workbookService.ReadWorkbook(stream);
    }
}
