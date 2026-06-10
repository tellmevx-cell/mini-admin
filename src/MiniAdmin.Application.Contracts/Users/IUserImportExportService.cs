namespace MiniAdmin.Application.Contracts.Users;

public interface IUserImportExportService
{
    byte[] CreateWorkbook(IReadOnlyList<IReadOnlyList<string>> rows);

    IReadOnlyList<IReadOnlyList<string>> ReadWorkbook(Stream stream);
}

