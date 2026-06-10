using System.Data.Common;
using System.Text.RegularExpressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.Caching;
using MiniAdmin.Application.Contracts.CodeGenerators;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Domain.Entities;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class EfCodeGeneratorRepository(
    MiniAdminDbContext dbContext,
    IUserAuthorizationCache userAuthorizationCache) : ICodeGeneratorRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
    private static readonly Regex SafeTableNameRegex = new("^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.Compiled);

    public async Task<IReadOnlyList<CodeGeneratorTableDto>> GetTablesAsync(
        CancellationToken cancellationToken = default)
    {
        if (!CanReadInformationSchema())
        {
            return Array.Empty<CodeGeneratorTableDto>();
        }

        var connection = dbContext.Database.GetDbConnection();
        await OpenIfNeededAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select table_name, coalesce(table_comment, '') as table_comment
            from information_schema.tables
            where table_schema = database() and table_type = 'BASE TABLE'
            order by table_name
            """;

        var tables = new List<CodeGeneratorTableDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tables.Add(new CodeGeneratorTableDto(
                reader.GetString(0),
                reader.GetString(1),
                Array.Empty<CodeGeneratorColumnDto>()));
        }

        return tables;
    }

    public async Task<CodeGeneratorTableDto?> GetTableAsync(
        string tableName,
        CancellationToken cancellationToken = default)
    {
        if (!CanReadInformationSchema())
        {
            return null;
        }

        var connection = dbContext.Database.GetDbConnection();
        await OpenIfNeededAsync(connection, cancellationToken);

        var tableComment = await GetTableCommentAsync(connection, tableName, cancellationToken);
        if (tableComment is null)
        {
            return null;
        }

        var columns = await GetColumnsAsync(connection, tableName, cancellationToken);
        return new CodeGeneratorTableDto(tableName, tableComment, columns);
    }

    public async Task<bool> TableExistsAsync(
        string tableName,
        CancellationToken cancellationToken = default)
    {
        if (!CanReadInformationSchema())
        {
            return false;
        }

        var connection = dbContext.Database.GetDbConnection();
        await OpenIfNeededAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            select count(1)
            from information_schema.tables
            where table_schema = database() and table_name = @tableName
            limit 1
            """;
        AddParameter(command, "@tableName", tableName);

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value) > 0;
    }

    public async Task<bool> GeneratedMenusInstalledAsync(
        IReadOnlyList<Guid> menuIds,
        CancellationToken cancellationToken = default)
    {
        if (menuIds.Count == 0)
        {
            return false;
        }

        var existingMenuIds = await dbContext.Menus
            .AsNoTracking()
            .Select(menu => menu.Id)
            .ToArrayAsync(cancellationToken);
        var adminRoleMenuIds = await dbContext.RoleMenus
            .AsNoTracking()
            .Where(roleMenu => roleMenu.RoleId == MiniAdminSeedIds.AdminRoleId)
            .Select(roleMenu => roleMenu.MenuId)
            .ToArrayAsync(cancellationToken);

        return menuIds.All(menuId =>
            existingMenuIds.Contains(menuId) &&
            adminRoleMenuIds.Contains(menuId));
    }

    public async Task<CodeGeneratorAutoInstallResultDto> AutoInstallAsync(
        CodeGeneratorAutoInstallRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var tableInstalled = false;
        var tableSkipped = true;
        if (!string.IsNullOrWhiteSpace(request.CreateTableSql) &&
            dbContext.Database.IsRelational() &&
            (dbContext.Database.ProviderName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            await dbContext.Database.ExecuteSqlRawAsync(request.CreateTableSql, cancellationToken);
            tableInstalled = true;
            tableSkipped = false;
        }

        foreach (var menu in request.Menus)
        {
            await UpsertMenuAsync(menu, cancellationToken);
        }

        foreach (var menuId in request.Menus.Select(menu => menu.Id))
        {
            if (await dbContext.RoleMenus.AnyAsync(
                roleMenu => roleMenu.RoleId == MiniAdminSeedIds.AdminRoleId && roleMenu.MenuId == menuId,
                cancellationToken))
            {
                continue;
            }

            dbContext.RoleMenus.Add(new RoleMenu
            {
                RoleId = MiniAdminSeedIds.AdminRoleId,
                MenuId = menuId
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var adminUserName = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == MiniAdminSeedIds.AdminUserId)
            .Select(user => user.UserName)
            .SingleOrDefaultAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(adminUserName))
        {
            await userAuthorizationCache.RemoveUserAsync(
                MiniAdminSeedIds.AdminUserId,
                adminUserName,
                cancellationToken);
        }

        return new CodeGeneratorAutoInstallResultDto(
            tableInstalled,
            tableSkipped,
            await GeneratedMenusInstalledAsync(
                request.Menus.Select(menu => menu.Id).ToArray(),
                cancellationToken));
    }

    public async Task<CodeGenerationHistoryDto> AddHistoryAsync(
        CodeGeneratorPreviewRequest request,
        IReadOnlyList<CodeGeneratorPreviewFileDto> files,
        string status,
        string? errorMessage,
        Guid? operatorUserId,
        string? operatorUserName,
        CancellationToken cancellationToken = default)
    {
        var history = new CodeGenerationHistory
        {
            Id = Guid.NewGuid(),
            TableName = request.TableName.Trim(),
            ModuleName = request.ModuleName.Trim(),
            BusinessName = request.BusinessName.Trim(),
            PermissionPrefix = request.PermissionPrefix.Trim(),
            TenantMode = request.TenantMode.Trim(),
            RequestJson = JsonSerializer.Serialize(request, JsonOptions),
            FilesJson = JsonSerializer.Serialize(files, JsonOptions),
            Status = status,
            ErrorMessage = errorMessage,
            OperatorUserId = operatorUserId,
            OperatorUserName = operatorUserName,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.CodeGenerationHistories.Add(history);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(history);
    }

    public async Task<PageResult<CodeGenerationHistoryDto>> GetHistoriesAsync(
        CodeGeneratorHistoryListQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var source = dbContext.CodeGenerationHistories.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.ModuleName))
        {
            source = source.Where(x => x.ModuleName.Contains(query.ModuleName));
        }

        if (!string.IsNullOrWhiteSpace(query.TableName))
        {
            source = source.Where(x => x.TableName.Contains(query.TableName));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            source = source.Where(x => x.Status == query.Status);
        }

        var total = await source.CountAsync(cancellationToken);
        var items = await source
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => ToDto(x))
            .ToArrayAsync(cancellationToken);

        return new PageResult<CodeGenerationHistoryDto>(items, total);
    }

    public async Task<CodeGenerationHistoryDetailSourceDto?> GetHistoryDetailAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var history = await dbContext.CodeGenerationHistories
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return history is null ? null : ToDetailSourceDto(history);
    }

    public async Task<int> RollbackGeneratedMenusAsync(
        Guid historyId,
        IReadOnlyList<Guid> menuIds,
        Guid? operatorUserId,
        string? operatorUserName,
        CancellationToken cancellationToken = default)
    {
        var history = await dbContext.CodeGenerationHistories
            .SingleOrDefaultAsync(x => x.Id == historyId, cancellationToken)
            ?? throw new InvalidOperationException("生成记录不存在。");

        if (!string.Equals(history.Status, "Success", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("只有成功生成的记录可以回滚。");
        }

        var roleMenus = await dbContext.RoleMenus.ToArrayAsync(cancellationToken);
        roleMenus = roleMenus
            .Where(roleMenu => menuIds.Contains(roleMenu.MenuId))
            .ToArray();
        dbContext.RoleMenus.RemoveRange(roleMenus);

        var menus = await dbContext.Menus.ToArrayAsync(cancellationToken);
        menus = menus
            .Where(menu => menuIds.Contains(menu.Id))
            .OrderByDescending(menu => menu.ParentId.HasValue)
            .ToArray();
        dbContext.Menus.RemoveRange(menus);

        history.Status = "RolledBack";
        history.ErrorMessage = $"已由 {operatorUserName ?? operatorUserId?.ToString() ?? "unknown"} 回滚。";

        await dbContext.SaveChangesAsync(cancellationToken);

        var adminUserName = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == MiniAdminSeedIds.AdminUserId)
            .Select(user => user.UserName)
            .SingleOrDefaultAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(adminUserName))
        {
            await userAuthorizationCache.RemoveUserAsync(
                MiniAdminSeedIds.AdminUserId,
                adminUserName,
                cancellationToken);
        }

        return menus.Length;
    }

    public async Task<int> CleanupGeneratedMenusAsync(
        IReadOnlyList<Guid> menuIds,
        Guid? operatorUserId,
        string? operatorUserName,
        CancellationToken cancellationToken = default)
    {
        if (menuIds.Count == 0)
        {
            return 0;
        }

        var roleMenus = await dbContext.RoleMenus.ToArrayAsync(cancellationToken);
        roleMenus = roleMenus
            .Where(roleMenu => menuIds.Contains(roleMenu.MenuId))
            .ToArray();
        dbContext.RoleMenus.RemoveRange(roleMenus);

        var menus = await dbContext.Menus.ToArrayAsync(cancellationToken);
        menus = menus
            .Where(menu => menuIds.Contains(menu.Id))
            .OrderByDescending(menu => menu.ParentId.HasValue)
            .ToArray();
        dbContext.Menus.RemoveRange(menus);

        await dbContext.SaveChangesAsync(cancellationToken);

        var adminUserName = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == MiniAdminSeedIds.AdminUserId)
            .Select(user => user.UserName)
            .SingleOrDefaultAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(adminUserName))
        {
            await userAuthorizationCache.RemoveUserAsync(
                MiniAdminSeedIds.AdminUserId,
                adminUserName,
                cancellationToken);
        }

        return menus.Length;
    }

    public async Task<CodeGeneratorTableDropResultDto> DropGeneratedTableAsync(
        string tableName,
        CancellationToken cancellationToken = default)
    {
        var normalizedTableName = tableName.Trim();
        if (!SafeTableNameRegex.IsMatch(normalizedTableName))
        {
            return new CodeGeneratorTableDropResultDto(
                false,
                true,
                "表名不是安全标识符，已跳过业务表删除。");
        }

        if (!CanReadInformationSchema())
        {
            return new CodeGeneratorTableDropResultDto(
                false,
                true,
                "当前数据库不支持自动删除业务表，已跳过。");
        }

        if (!await TableExistsAsync(normalizedTableName, cancellationToken))
        {
            return new CodeGeneratorTableDropResultDto(
                false,
                true,
                "业务表不存在，已跳过。");
        }

        try
        {
            var connection = dbContext.Database.GetDbConnection();
            await OpenIfNeededAsync(connection, cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP TABLE IF EXISTS `{normalizedTableName}`;";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (DbException exception)
        {
            return new CodeGeneratorTableDropResultDto(
                false,
                true,
                $"业务表删除失败：{exception.Message}");
        }

        return new CodeGeneratorTableDropResultDto(
            true,
            false,
            $"已删除业务表 {normalizedTableName}，表内数据也已清理。");
    }

    private async Task UpsertMenuAsync(
        CodeGeneratorGeneratedMenuInstallDto menu,
        CancellationToken cancellationToken)
    {
        var existingMenu = await dbContext.Menus.SingleOrDefaultAsync(x => x.Id == menu.Id, cancellationToken);
        if (existingMenu is null)
        {
            dbContext.Menus.Add(new Menu
            {
                Id = menu.Id,
                ParentId = menu.ParentId,
                Name = menu.Name,
                Path = menu.Path,
                Component = menu.Component,
                Title = menu.Title,
                Icon = menu.Icon,
                Order = menu.Order,
                PermissionCode = menu.PermissionCode,
                IsEnabled = menu.IsEnabled,
                IsVisible = menu.IsVisible
            });
            return;
        }

        existingMenu.ParentId = menu.ParentId;
        existingMenu.Name = menu.Name;
        existingMenu.Path = menu.Path;
        existingMenu.Component = menu.Component;
        existingMenu.Title = menu.Title;
        existingMenu.Icon = menu.Icon;
        existingMenu.Order = menu.Order;
        existingMenu.PermissionCode = menu.PermissionCode;
        existingMenu.IsEnabled = menu.IsEnabled;
        existingMenu.IsVisible = menu.IsVisible;
    }

    private bool CanReadInformationSchema()
    {
        return dbContext.Database.IsRelational() &&
               (dbContext.Database.ProviderName?.Contains("MySql", StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static async Task<string?> GetTableCommentAsync(
        DbConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select coalesce(table_comment, '')
            from information_schema.tables
            where table_schema = database() and table_name = @tableName
            limit 1
            """;
        AddParameter(command, "@tableName", tableName);

        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? null : value.ToString();
    }

    private static async Task<IReadOnlyList<CodeGeneratorColumnDto>> GetColumnsAsync(
        DbConnection connection,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select
                column_name,
                column_type,
                data_type,
                is_nullable,
                column_key,
                coalesce(column_comment, ''),
                ordinal_position
            from information_schema.columns
            where table_schema = database() and table_name = @tableName
            order by ordinal_position
            """;
        AddParameter(command, "@tableName", tableName);

        var columns = new List<CodeGeneratorColumnDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var dataType = reader.GetString(2);
            var isNullable = string.Equals(reader.GetString(3), "YES", StringComparison.OrdinalIgnoreCase);
            columns.Add(new CodeGeneratorColumnDto(
                reader.GetString(0),
                reader.GetString(1),
                MapDotNetType(dataType, isNullable),
                MapTsType(dataType),
                reader.GetString(5),
                string.Equals(reader.GetString(4), "PRI", StringComparison.OrdinalIgnoreCase),
                isNullable,
                reader.GetInt32(6)));
        }

        return columns;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static async Task OpenIfNeededAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }
    }

    private static string MapDotNetType(string dataType, bool isNullable)
    {
        var type = dataType.ToLowerInvariant() switch
        {
            "bigint" => "long",
            "int" or "integer" or "mediumint" or "smallint" or "tinyint" => "int",
            "decimal" or "numeric" => "decimal",
            "double" => "double",
            "float" => "float",
            "bit" or "bool" or "boolean" => "bool",
            "datetime" or "timestamp" or "date" => "DateTimeOffset",
            "char" or "varchar" or "text" or "longtext" or "mediumtext" or "json" => "string",
            _ => "string"
        };

        return isNullable && type != "string" ? $"{type}?" : type;
    }

    private static string MapTsType(string dataType)
    {
        return dataType.ToLowerInvariant() switch
        {
            "bigint" or "int" or "integer" or "mediumint" or "smallint" or "tinyint" or "decimal" or "numeric" or
                "double" or "float" => "number",
            "bit" or "bool" or "boolean" => "boolean",
            _ => "string"
        };
    }

    private static CodeGenerationHistoryDto ToDto(CodeGenerationHistory history)
    {
        var files = JsonSerializer.Deserialize<IReadOnlyList<CodeGeneratorPreviewFileDto>>(
            history.FilesJson,
            JsonOptions) ?? Array.Empty<CodeGeneratorPreviewFileDto>();

        return new CodeGenerationHistoryDto(
            history.Id.ToString(),
            history.TableName,
            history.ModuleName,
            history.BusinessName,
            history.PermissionPrefix,
            history.TenantMode,
            history.Status,
            history.ErrorMessage,
            files,
            history.CreatedAt);
    }

    private static CodeGenerationHistoryDetailSourceDto ToDetailSourceDto(CodeGenerationHistory history)
    {
        var preview = JsonSerializer.Deserialize<CodeGeneratorPreviewRequest>(
            history.RequestJson,
            JsonOptions) ?? throw new InvalidOperationException("代码生成历史参数无法解析。");
        var files = JsonSerializer.Deserialize<IReadOnlyList<CodeGeneratorPreviewFileDto>>(
            history.FilesJson,
            JsonOptions) ?? Array.Empty<CodeGeneratorPreviewFileDto>();

        return new CodeGenerationHistoryDetailSourceDto(
            history.Id.ToString(),
            history.TableName,
            history.ModuleName,
            history.BusinessName,
            history.PermissionPrefix,
            history.TenantMode,
            history.Status,
            history.ErrorMessage,
            history.OperatorUserName,
            preview,
            files,
            history.CreatedAt);
    }
}
