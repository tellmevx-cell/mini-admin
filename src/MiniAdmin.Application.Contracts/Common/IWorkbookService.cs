namespace MiniAdmin.Application.Contracts.Common;

public interface IWorkbookService
{
    byte[] CreateWorkbook(IReadOnlyList<IReadOnlyList<string>> rows);

    IReadOnlyList<IReadOnlyList<string>> ReadWorkbook(Stream stream);
}
