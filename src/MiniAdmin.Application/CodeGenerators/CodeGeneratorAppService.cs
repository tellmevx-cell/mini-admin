using System.Text;
using System.Text.RegularExpressions;
using MiniAdmin.Application.Contracts.CodeGenerators;
using MiniAdmin.Application.Contracts.Common;

namespace MiniAdmin.Application.CodeGenerators;

public sealed class CodeGeneratorAppService(
    ICodeGeneratorRepository codeGeneratorRepository,
    CodeGeneratorTemplateRenderer templateRenderer) : ICodeGeneratorAppService
{
    private static readonly Regex IdentifierRegex = new("^[A-Za-z][A-Za-z0-9]*$", RegexOptions.Compiled);
    private static readonly Regex PermissionRegex = new("^[a-z][a-z0-9:-]*$", RegexOptions.Compiled);
    private static readonly Regex DbContextEntityMappingRegex = new(
        "Entity<(?<module>[A-Za-z][A-Za-z0-9]*)>\\s*\\([\\s\\S]*?\\.ToTable\\(\"(?<table>[^\"]+)\"\\)",
        RegexOptions.Compiled);
    private static readonly Regex EntityConfigurationMappingRegex = new(
        "class\\s+(?<module>[A-Za-z][A-Za-z0-9]*)EntityTypeConfiguration[\\s\\S]*?\\.ToTable\\(\"(?<table>[^\"]+)\"\\)",
        RegexOptions.Compiled);
    private static readonly Regex GeneratedMenuPathRegex = new("Path\\s*=\\s*\"(?<path>[^\"]+)\"", RegexOptions.Compiled);
    private static readonly Regex GeneratedMenuComponentRegex = new("Component\\s*=\\s*\"(?<component>[^\"]+)\"", RegexOptions.Compiled);
    private static readonly Regex SafeModuleNameRegex = new("^[A-Za-z][A-Za-z0-9]*$", RegexOptions.Compiled);

    private static readonly string[] AllowedRoots =
    [
        "src/MiniAdmin.Domain/Entities/",
        "src/MiniAdmin.Application.Contracts/",
        "src/MiniAdmin.Application/",
        "src/MiniAdmin.Infrastructure/Persistence/",
        "src/MiniAdmin.Api/",
        "frontend/vue-vben-admin/apps/web-antd/src/api/",
        "frontend/vue-vben-admin/apps/web-antd/src/views/"
    ];

    public async Task<IReadOnlyList<CodeGeneratorTableDto>> GetTablesAsync(CancellationToken cancellationToken = default)
    {
        var tables = await codeGeneratorRepository.GetTablesAsync(cancellationToken);
        var modules = GetMappedModules();
        return tables
            .Select(table =>
            {
                var existingModule = FindExistingModule(modules, table.TableName);
                return table with
                {
                    ExistingModule = existingModule,
                    GenerationBlockReason = GetGenerationBlockReason(table.TableName, existingModule)
                };
            })
            .ToArray();
    }

    public async Task<CodeGeneratorTableDto?> GetTableAsync(string tableName, CancellationToken cancellationToken = default)
    {
        var table = await codeGeneratorRepository.GetTableAsync(tableName, cancellationToken);
        if (table is null)
        {
            return null;
        }

        var modules = GetMappedModules();
        var existingModule = FindExistingModule(modules, table.TableName);
        return table with
        {
            ExistingModule = existingModule,
            GenerationBlockReason = GetGenerationBlockReason(table.TableName, existingModule)
        };
    }

    public Task<PageResult<CodeGenerationHistoryDto>> GetHistoriesAsync(
        CodeGeneratorHistoryListQuery query,
        CancellationToken cancellationToken = default)
    {
        return codeGeneratorRepository.GetHistoriesAsync(query, cancellationToken);
    }

    public async Task<CodeGenerationHistoryDetailDto?> GetHistoryDetailAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var source = await codeGeneratorRepository.GetHistoryDetailAsync(id, cancellationToken);
        if (source is null)
        {
            return null;
        }

        var installPlan = await BuildInstallPlanAsync(
            source.Preview,
            source.Files.Any(file => file.HasConflict),
            cancellationToken);

        return new CodeGenerationHistoryDetailDto(
            source.Id,
            source.TableName,
            source.ModuleName,
            source.BusinessName,
            source.PermissionPrefix,
            source.TenantMode,
            source.Status,
            source.ErrorMessage,
            source.OperatorUserName,
            source.Preview,
            source.Files,
            installPlan,
            source.CreatedAt);
    }

    public async Task<CodeGeneratorRollbackResultDto> RollbackAsync(
        Guid id,
        CodeGeneratorRollbackRequest request,
        Guid? operatorUserId,
        string? operatorUserName,
        CancellationToken cancellationToken = default)
    {
        var source = await codeGeneratorRepository.GetHistoryDetailAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("生成记录不存在。");

        if (string.Equals(source.Status, "RolledBack", StringComparison.OrdinalIgnoreCase) && request.DropTable)
        {
            var rolledBackTableDropResult = await codeGeneratorRepository.DropGeneratedTableAsync(
                source.TableName,
                cancellationToken);
            return new CodeGeneratorRollbackResultDto(
                source.Id,
                "RolledBack",
                0,
                0,
                rolledBackTableDropResult.TableDropped,
                rolledBackTableDropResult.TableDropSkipped,
                rolledBackTableDropResult.TableDropMessage);
        }

        if (!string.Equals(source.Status, "Success", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("只有成功生成的记录可以回滚。");
        }

        var deletedFileCount = DeleteGeneratedFiles(source.Preview, source.Files);
        var generatedMenuIds = BuildGeneratedMenus(source.Preview).Select(menu => menu.Id).ToArray();
        var deletedMenuCount = await codeGeneratorRepository.RollbackGeneratedMenusAsync(
            id,
            generatedMenuIds,
            operatorUserId,
            operatorUserName,
            cancellationToken);
        var tableDropResult = request.DropTable
            ? await codeGeneratorRepository.DropGeneratedTableAsync(source.TableName, cancellationToken)
            : new CodeGeneratorTableDropResultDto(
                false,
                true,
                "未勾选删除业务表，已保留业务表和业务数据。");

        return new CodeGeneratorRollbackResultDto(
            source.Id,
            "RolledBack",
            deletedFileCount,
            deletedMenuCount,
            tableDropResult.TableDropped,
            tableDropResult.TableDropSkipped,
            tableDropResult.TableDropMessage);
    }

    public async Task<CodeGeneratorArtifactGovernanceResultDto> GetArtifactGovernanceAsync(
        CancellationToken cancellationToken = default)
    {
        var modules = GetMappedModules()
            .Values
            .Where(module => module.ModuleKind.Equals("Generated", StringComparison.OrdinalIgnoreCase))
            .OrderBy(module => module.ModuleName)
            .ToArray();
        var items = new List<CodeGeneratorArtifactGovernanceDto>();

        foreach (var module in modules)
        {
            var hasHistory = await HasGenerationHistoryAsync(module.ModuleName, module.TableName, cancellationToken);
            var menuId = CodeGeneratorTemplateRenderer.CreateDeterministicGuid(module.ModuleName, "menu");
            var hasMenu = await codeGeneratorRepository.GeneratedMenusInstalledAsync([menuId], cancellationToken);
            var isReservedTable = IsReservedSystemTable(module.TableName);
            var riskReason = hasHistory
                ? null
                : "存在代码产物但没有生成历史，建议补登记或清理。";
            if (isReservedTable)
            {
                riskReason = "系统维护表不适合作为业务模块，建议清理误生成产物。";
            }

            items.Add(new CodeGeneratorArtifactGovernanceDto(
                module.ModuleName,
                module.TableName,
                module.ModuleKind,
                module.RoutePath,
                module.Component,
                hasHistory,
                hasMenu,
                true,
                isReservedTable,
                riskReason,
                module.Files));
        }

        return new CodeGeneratorArtifactGovernanceResultDto(items);
    }

    public async Task<CodeGeneratorArtifactCleanupResultDto> CleanupArtifactAsync(
        string moduleName,
        CodeGeneratorArtifactCleanupRequest request,
        Guid? operatorUserId,
        string? operatorUserName,
        CancellationToken cancellationToken = default)
    {
        var module = FindGeneratedModule(moduleName);
        if (await HasGenerationHistoryAsync(module.ModuleName, module.TableName, cancellationToken))
        {
            throw new InvalidOperationException("该模块已有生成历史，请使用生成历史里的回滚功能。");
        }

        var deletedFileCount = DeleteGeneratedArtifactFiles(module);
        var menuIds = BuildGeneratedMenuIds(module.ModuleName);
        var deletedMenuCount = await codeGeneratorRepository.CleanupGeneratedMenusAsync(
            menuIds,
            operatorUserId,
            operatorUserName,
            cancellationToken);
        var tableDropResult = request.DropTable
            ? await codeGeneratorRepository.DropGeneratedTableAsync(module.TableName, cancellationToken)
            : new CodeGeneratorTableDropResultDto(false, true, "未勾选删除业务表，已保留业务表和业务数据。");

        return new CodeGeneratorArtifactCleanupResultDto(
            module.ModuleName,
            deletedFileCount,
            deletedMenuCount,
            tableDropResult.TableDropped,
            tableDropResult.TableDropSkipped,
            tableDropResult.TableDropMessage);
    }

    public async Task<CodeGeneratorArtifactRegisterHistoryResultDto> RegisterArtifactHistoryAsync(
        string moduleName,
        Guid? operatorUserId,
        string? operatorUserName,
        CancellationToken cancellationToken = default)
    {
        var module = FindGeneratedModule(moduleName);
        if (await HasGenerationHistoryAsync(module.ModuleName, module.TableName, cancellationToken))
        {
            throw new InvalidOperationException("该模块已经存在生成历史。");
        }

        var table = await codeGeneratorRepository.GetTableAsync(module.TableName, cancellationToken)
            ?? throw new InvalidOperationException("业务表不存在，无法补登记生成历史。");
        var routePath = module.RoutePath ?? $"/business/{ToKebabCase(module.ModuleName)}";
        var routeSegment = CodeGeneratorTemplateRenderer.GetRouteSegment(routePath);
        var enableWorkflow = table.Columns.Any(column =>
            column.ColumnName.Equals("workflow_instance_id", StringComparison.OrdinalIgnoreCase) ||
            column.ColumnName.Equals("approval_status", StringComparison.OrdinalIgnoreCase)) ||
            module.Files.Any(file => file.EndsWith("WorkflowStateHandler.cs", StringComparison.OrdinalIgnoreCase));
        var request = new CodeGeneratorPreviewRequest(
            module.TableName,
            module.ModuleName,
            string.IsNullOrWhiteSpace(table.TableComment) ? module.ModuleName : table.TableComment,
            routePath,
            null,
            $"business:{routeSegment}",
            table.Columns.Any(column => column.ColumnName.Equals("tenant_id", StringComparison.OrdinalIgnoreCase)) ? "Tenant" : "None",
            table.Columns
                .Where(column => !IsReservedSystemColumn(column.ColumnName))
                .OrderBy(column => column.Sort)
                .Select(CreateFieldConfigFromColumn)
                .ToArray(),
            EnableImportExport: module.Files.Any(file => file.Contains("import", StringComparison.OrdinalIgnoreCase)),
            EnableWorkflow: enableWorkflow,
            WorkflowBusinessType: enableWorkflow ? routeSegment.Replace('-', '_') : null);
        var files = module.Files
            .Where(file => File.Exists(ToFullPath(FindWorkspaceRoot(), file)))
            .Select(file => new CodeGeneratorPreviewFileDto(
                file,
                File.ReadAllText(ToFullPath(FindWorkspaceRoot(), file)),
                true))
            .ToArray();
        var history = await codeGeneratorRepository.AddHistoryAsync(
            request,
            files,
            "Success",
            "由产物治理补登记生成历史。",
            operatorUserId,
            operatorUserName,
            CancellationToken.None);

        return new CodeGeneratorArtifactRegisterHistoryResultDto(history);
    }

    public async Task<CodeGeneratorPreviewResultDto> PreviewAsync(
        CodeGeneratorPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        var files = BuildPreviewFiles(request);
        var permissions = CodeGeneratorTemplateRenderer.GetPermissionCodes(
            request.PermissionPrefix,
            request.EnableImportExport,
            request.EnableWorkflow);
        var hasConflicts = files.Any(file => file.HasConflict);
        var installPlan = await BuildInstallPlanAsync(request, hasConflicts, cancellationToken);

        return new CodeGeneratorPreviewResultDto(
            files,
            permissions,
            hasConflicts,
            installPlan);
    }

    public async Task<CodeGenerationHistoryDto> GenerateAsync(
        CodeGeneratorGenerateRequest request,
        Guid? operatorUserId,
        string? operatorUserName,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request.Preview);
        var files = BuildPreviewFiles(request.Preview);
        var operationToken = CancellationToken.None;

        if (!request.Overwrite && files.Any(file => file.HasConflict))
        {
            await codeGeneratorRepository.AddHistoryAsync(
                request.Preview,
                files,
                "Failed",
                "文件已存在，请先处理冲突或显式允许覆盖。",
                operatorUserId,
                operatorUserName,
                operationToken);
            throw new InvalidOperationException("文件已存在，请先处理冲突或显式允许覆盖。");
        }

        try
        {
            foreach (var file in files)
            {
                var fullPath = ResolveSafeFullPath(file.RelativePath);
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllTextAsync(fullPath, file.Content, operationToken);
            }

            if (request.AutoInstall)
            {
                await AutoInstallAsync(request.Preview, operationToken);
            }

            return await codeGeneratorRepository.AddHistoryAsync(
                request.Preview,
                files,
                "Success",
                null,
                operatorUserId,
                operatorUserName,
                operationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await codeGeneratorRepository.AddHistoryAsync(
                request.Preview,
                files,
                "Failed",
                exception.Message,
                operatorUserId,
                operatorUserName,
                operationToken);
            throw;
        }
    }

    private async Task AutoInstallAsync(
        CodeGeneratorPreviewRequest request,
        CancellationToken cancellationToken)
    {
        var tableName = request.TableName.Trim();
        var tableExists = await codeGeneratorRepository.TableExistsAsync(tableName, cancellationToken);
        var createTableSql = tableExists ? null : BuildCreateTableSql(request);
        var installRequest = BuildAutoInstallRequest(request, createTableSql);
        await codeGeneratorRepository.AutoInstallAsync(installRequest, cancellationToken);
    }

    private static void ValidateRequest(CodeGeneratorPreviewRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TableName))
        {
            throw new InvalidOperationException("表名不能为空。");
        }

        ValidateTableIsNotReserved(request.TableName);
        ValidateTableIsNotAlreadyMapped(request.TableName);

        if (!IdentifierRegex.IsMatch(request.ModuleName.Trim()))
        {
            throw new InvalidOperationException("模块名必须是 PascalCase 英文标识符。");
        }

        if (string.IsNullOrWhiteSpace(request.BusinessName))
        {
            throw new InvalidOperationException("业务名称不能为空。");
        }

        if (!request.RoutePath.StartsWith('/'))
        {
            throw new InvalidOperationException("路由必须以 / 开头。");
        }

        if (!PermissionRegex.IsMatch(request.PermissionPrefix.Trim()))
        {
            throw new InvalidOperationException("权限前缀只能包含小写字母、数字、冒号和中划线。");
        }

        if (request.EnableWorkflow && string.IsNullOrWhiteSpace(request.WorkflowBusinessType))
        {
            throw new InvalidOperationException("启用审批时必须填写业务类型编码。");
        }

        if (request.Fields.Count == 0)
        {
            throw new InvalidOperationException("至少需要配置一个字段。");
        }

        foreach (var field in request.Fields)
        {
            if (!IdentifierRegex.IsMatch(field.PropertyName.Trim()))
            {
                throw new InvalidOperationException($"字段 {field.ColumnName} 的属性名不合法。");
            }
        }

        if (!IsDataScopeMode(request.DataScopeMode, "None"))
        {
            if (!IsDataScopeMode(request.DataScopeMode, "Department") &&
                !IsDataScopeMode(request.DataScopeMode, "Self"))
            {
                throw new InvalidOperationException("数据权限模式只能选择 None、Department 或 Self。");
            }

            if (string.IsNullOrWhiteSpace(request.DataScopeField))
            {
                throw new InvalidOperationException("启用数据权限时必须选择数据权限字段。");
            }

            if (!request.Fields.Any(field =>
                    field.PropertyName.Equals(request.DataScopeField.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("数据权限字段必须来自当前生成字段。");
            }
        }
    }

    private static void ValidateTableIsNotAlreadyMapped(string tableName)
    {
        var normalizedTableName = tableName.Trim();
        var existingModule = FindExistingModule(GetMappedModules(), normalizedTableName);
        if (existingModule is null)
        {
            return;
        }

        throw new InvalidOperationException(
            $"数据表 {normalizedTableName} 已被当前系统实体 {existingModule.ModuleName} 映射，不能重复生成模块。请换一张业务表，或直接维护已有系统功能。");
    }

    private async Task<bool> HasGenerationHistoryAsync(
        string moduleName,
        string tableName,
        CancellationToken cancellationToken)
    {
        var histories = await codeGeneratorRepository.GetHistoriesAsync(
            new CodeGeneratorHistoryListQuery(1, 1000, moduleName),
            cancellationToken);
        return histories.Items.Any(history =>
            history.ModuleName.Equals(moduleName, StringComparison.OrdinalIgnoreCase) &&
            history.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase));
    }

    private static CodeGeneratorExistingModuleDto FindGeneratedModule(string moduleName)
    {
        if (!SafeModuleNameRegex.IsMatch(moduleName.Trim()))
        {
            throw new InvalidOperationException("模块名不合法。");
        }

        return GetMappedModules()
            .Values
            .SingleOrDefault(module =>
                module.ModuleKind.Equals("Generated", StringComparison.OrdinalIgnoreCase) &&
                module.ModuleName.Equals(moduleName.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("未找到可治理的生成模块。");
    }

    private static IReadOnlyList<Guid> BuildGeneratedMenuIds(string moduleName)
    {
        return
        [
            CodeGeneratorTemplateRenderer.CreateDeterministicGuid(moduleName, "menu"),
            CodeGeneratorTemplateRenderer.CreateDeterministicGuid(moduleName, "query"),
            CodeGeneratorTemplateRenderer.CreateDeterministicGuid(moduleName, "create"),
            CodeGeneratorTemplateRenderer.CreateDeterministicGuid(moduleName, "update"),
            CodeGeneratorTemplateRenderer.CreateDeterministicGuid(moduleName, "delete"),
            CodeGeneratorTemplateRenderer.CreateDeterministicGuid(moduleName, "import"),
            CodeGeneratorTemplateRenderer.CreateDeterministicGuid(moduleName, "export"),
            CodeGeneratorTemplateRenderer.CreateDeterministicGuid(moduleName, "submit-workflow"),
            CodeGeneratorTemplateRenderer.CreateDeterministicGuid(moduleName, "withdraw-workflow")
        ];
    }

    private static int DeleteGeneratedArtifactFiles(CodeGeneratorExistingModuleDto module)
    {
        var deletedFileCount = 0;
        var modulePlural = CodeGeneratorTemplateRenderer.ToPlural(module.ModuleName);
        var routeSegment = module.RoutePath is null
            ? ToKebabCase(module.ModuleName)
            : CodeGeneratorTemplateRenderer.GetRouteSegment(module.RoutePath);

        foreach (var file in module.Files)
        {
            if (!IsAllowedExistingModuleFile(file))
            {
                continue;
            }

            var fullPath = ToFullPath(FindWorkspaceRoot(), file);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                deletedFileCount++;
            }

            DeleteGeneratedDirectoryIfEmpty(fullPath, modulePlural, routeSegment);
        }

        return deletedFileCount;
    }

    private static bool IsAllowedExistingModuleFile(string relativePath)
    {
        return !string.IsNullOrWhiteSpace(relativePath) &&
               !relativePath.Contains('\\') &&
               !relativePath.Split('/').Any(segment => segment == "..") &&
               !Path.IsPathRooted(relativePath) &&
               AllowedRoots.Any(root => relativePath.StartsWith(root, StringComparison.Ordinal));
    }

    private static void ValidateTableIsNotReserved(string tableName)
    {
        var reason = GetGenerationBlockReason(tableName.Trim(), null);
        if (!string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException(reason);
        }
    }

    private static string? GetGenerationBlockReason(
        string tableName,
        CodeGeneratorExistingModuleDto? existingModule)
    {
        return IsReservedSystemTable(tableName)
            ? $"数据表 {tableName.Trim()} 是系统维护表，不允许生成业务模块。"
            : null;
    }

    private static bool IsReservedSystemTable(string tableName)
    {
        var normalized = tableName
            .Trim()
            .Replace("_", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal)
            .ToLowerInvariant();
        return normalized.Contains("efmigrationshistory", StringComparison.OrdinalIgnoreCase) ||
               normalized is "minidataseedversions" or "minicodegenerationhistories";
    }

    private static bool IsReservedSystemColumn(string columnName)
    {
        return columnName.Equals("id", StringComparison.OrdinalIgnoreCase) ||
               columnName.Equals("tenant_id", StringComparison.OrdinalIgnoreCase) ||
               columnName.Equals("workflow_instance_id", StringComparison.OrdinalIgnoreCase) ||
               columnName.Equals("approval_status", StringComparison.OrdinalIgnoreCase) ||
               columnName.Equals("created_at", StringComparison.OrdinalIgnoreCase) ||
               columnName.Equals("updated_at", StringComparison.OrdinalIgnoreCase) ||
               columnName.Equals("deleted_at", StringComparison.OrdinalIgnoreCase) ||
               columnName.Equals("create_time", StringComparison.OrdinalIgnoreCase) ||
               columnName.Equals("update_time", StringComparison.OrdinalIgnoreCase) ||
               columnName.Equals("create_by", StringComparison.OrdinalIgnoreCase) ||
               columnName.Equals("update_by", StringComparison.OrdinalIgnoreCase) ||
               columnName.Equals("is_deleted", StringComparison.OrdinalIgnoreCase);
    }

    private static CodeGeneratorFieldConfigDto CreateFieldConfigFromColumn(CodeGeneratorColumnDto column)
    {
        return new CodeGeneratorFieldConfigDto(
            column.ColumnName,
            ToPascalCase(column.ColumnName),
            string.IsNullOrWhiteSpace(column.ColumnComment) ? column.ColumnName : column.ColumnComment,
            column.DotNetType,
            column.TsType,
            column.IsPrimaryKey,
            !column.IsNullable,
            !column.IsPrimaryKey,
            !column.IsPrimaryKey,
            !column.IsPrimaryKey,
            !column.IsPrimaryKey,
            GetDefaultControlType(column),
            null,
            column.Sort,
            GetDefaultQueryMode(column),
            column.TsType.Equals("string", StringComparison.OrdinalIgnoreCase) ? 256 : null);
    }

    private static string GetDefaultControlType(CodeGeneratorColumnDto column)
    {
        if (column.DotNetType.Equals("bool", StringComparison.OrdinalIgnoreCase))
        {
            return "Switch";
        }

        if (column.DotNetType.Equals("DateTime", StringComparison.OrdinalIgnoreCase) ||
            column.DotNetType.Equals("DateTimeOffset", StringComparison.OrdinalIgnoreCase))
        {
            return "DatePicker";
        }

        return column.TsType.Equals("number", StringComparison.OrdinalIgnoreCase) ? "InputNumber" : "Input";
    }

    private static string GetDefaultQueryMode(CodeGeneratorColumnDto column)
    {
        return column.TsType.Equals("string", StringComparison.OrdinalIgnoreCase) ? "Contains" : "Equals";
    }

    private static string ToPascalCase(string value)
    {
        return string.Join(
            string.Empty,
            value
                .Split(['_', '-'], StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part[..1].ToUpperInvariant() + part[1..]));
    }

    private static CodeGeneratorExistingModuleDto? FindExistingModule(
        IReadOnlyDictionary<string, CodeGeneratorExistingModuleDto> modules,
        string tableName)
    {
        return modules.TryGetValue(tableName.Trim(), out var module) ? module : null;
    }

    private static IReadOnlyDictionary<string, CodeGeneratorExistingModuleDto> GetMappedModules()
    {
        var workspaceRoot = FindWorkspaceRoot();
        var persistenceRoot = Path.Combine(workspaceRoot, "src", "MiniAdmin.Infrastructure", "Persistence");
        var modules = new Dictionary<string, CodeGeneratorExistingModuleDto>(StringComparer.OrdinalIgnoreCase);
        var dbContextPath = Path.Combine(persistenceRoot, "MiniAdminDbContext.cs");
        if (File.Exists(dbContextPath))
        {
            foreach (var module in ParseDbContextMappedModules(workspaceRoot, dbContextPath))
            {
                modules[module.TableName] = module;
            }
        }

        var generatedRoot = Path.Combine(persistenceRoot, "Generated");
        if (Directory.Exists(generatedRoot))
        {
            foreach (var file in Directory.EnumerateFiles(generatedRoot, "*EntityTypeConfiguration.cs"))
            {
                foreach (var module in ParseGeneratedMappedModules(workspaceRoot, file))
                {
                    modules[module.TableName] = module;
                }
            }
        }

        return modules;
    }

    private static IEnumerable<CodeGeneratorExistingModuleDto> ParseDbContextMappedModules(
        string workspaceRoot,
        string dbContextPath)
    {
        var content = File.ReadAllText(dbContextPath);
        foreach (Match match in DbContextEntityMappingRegex.Matches(content))
        {
            var moduleName = match.Groups["module"].Value;
            var tableName = match.Groups["table"].Value;
            yield return BuildExistingModule(workspaceRoot, tableName, moduleName, "System");
        }
    }

    private static IEnumerable<CodeGeneratorExistingModuleDto> ParseGeneratedMappedModules(
        string workspaceRoot,
        string configurationPath)
    {
        var content = File.ReadAllText(configurationPath);
        foreach (Match match in EntityConfigurationMappingRegex.Matches(content))
        {
            var moduleName = match.Groups["module"].Value;
            var tableName = match.Groups["table"].Value;
            yield return BuildExistingModule(workspaceRoot, tableName, moduleName, "Generated");
        }
    }

    private static CodeGeneratorExistingModuleDto BuildExistingModule(
        string workspaceRoot,
        string tableName,
        string moduleName,
        string moduleKind)
    {
        var pluralModuleName = CodeGeneratorTemplateRenderer.ToPlural(moduleName);
        var generatedMenuSeedPath = ToFullPath(
            workspaceRoot,
            $"src/MiniAdmin.Infrastructure/Persistence/Generated/{moduleName}MenuSeed.cs");
        var (routePath, component) = ReadGeneratedMenuRoute(generatedMenuSeedPath);
        var files = BuildExistingModuleFiles(workspaceRoot, moduleName, pluralModuleName, routePath, component);

        return new CodeGeneratorExistingModuleDto(
            tableName,
            moduleName,
            moduleKind,
            routePath,
            component,
            files);
    }

    private static (string? RoutePath, string? Component) ReadGeneratedMenuRoute(string generatedMenuSeedPath)
    {
        if (!File.Exists(generatedMenuSeedPath))
        {
            return (null, null);
        }

        var content = File.ReadAllText(generatedMenuSeedPath);
        var path = GeneratedMenuPathRegex.Match(content).Groups["path"].Value;
        var component = GeneratedMenuComponentRegex.Match(content).Groups["component"].Value;
        return (
            string.IsNullOrWhiteSpace(path) ? null : path,
            string.IsNullOrWhiteSpace(component) ? null : component);
    }

    private static IReadOnlyList<string> BuildExistingModuleFiles(
        string workspaceRoot,
        string moduleName,
        string pluralModuleName,
        string? routePath,
        string? component)
    {
        var routeSegment = !string.IsNullOrWhiteSpace(routePath)
            ? CodeGeneratorTemplateRenderer.GetRouteSegment(routePath)
            : CodeGeneratorTemplateRenderer.GetRouteSegment($"/business/{ToKebabCase(moduleName)}");
        var apiSegment = !string.IsNullOrWhiteSpace(routePath)
            ? routePath.Trim('/')
            : $"business/{routeSegment}";
        var relativePaths = new[]
        {
            $"src/MiniAdmin.Domain/Entities/{moduleName}.cs",
            $"src/MiniAdmin.Application.Contracts/{pluralModuleName}/I{moduleName}AppService.cs",
            $"src/MiniAdmin.Application.Contracts/{pluralModuleName}/{moduleName}Dtos.cs",
            $"src/MiniAdmin.Application/{pluralModuleName}/{moduleName}AppService.cs",
            $"src/MiniAdmin.Infrastructure/Persistence/Ef{moduleName}Repository.cs",
            $"src/MiniAdmin.Infrastructure/Persistence/Generated/{moduleName}EntityTypeConfiguration.cs",
            $"src/MiniAdmin.Infrastructure/Persistence/Generated/{moduleName}MenuSeed.cs",
            $"src/MiniAdmin.Api/Generated/{moduleName}Endpoints.cs",
            $"frontend/vue-vben-admin/apps/web-antd/src/api/{apiSegment}.ts",
            component is null
                ? $"frontend/vue-vben-admin/apps/web-antd/src/views/business/{routeSegment}/index.vue"
                : $"frontend/vue-vben-admin/apps/web-antd/src/views/{component.TrimStart('/')}.vue"
        };

        return relativePaths
            .Where(path => File.Exists(ToFullPath(workspaceRoot, path)))
            .ToArray();
    }

    private static string ToKebabCase(string value)
    {
        return Regex.Replace(value, "([a-z0-9])([A-Z])", "$1-$2").ToLowerInvariant();
    }

    private static string ToFullPath(string workspaceRoot, string relativePath)
    {
        return Path.Combine(workspaceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }

    private IReadOnlyList<CodeGeneratorPreviewFileDto> BuildPreviewFiles(CodeGeneratorPreviewRequest request)
    {
        return templateRenderer
            .Render(request)
            .Select(file =>
            {
                var fullPath = ResolveSafeFullPath(file.RelativePath);
                return file with { HasConflict = File.Exists(fullPath) };
            })
            .ToArray();
    }

    private static int DeleteGeneratedFiles(
        CodeGeneratorPreviewRequest request,
        IReadOnlyList<CodeGeneratorPreviewFileDto> files)
    {
        var deletedFileCount = 0;
        var modulePlural = CodeGeneratorTemplateRenderer.ToPlural(request.ModuleName.Trim());
        var routeSegment = CodeGeneratorTemplateRenderer.GetRouteSegment(request.RoutePath);

        foreach (var file in files)
        {
            var fullPath = ResolveSafeFullPath(file.RelativePath);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                deletedFileCount++;
            }

            DeleteGeneratedDirectoryIfEmpty(fullPath, modulePlural, routeSegment);
        }

        return deletedFileCount;
    }

    private static void DeleteGeneratedDirectoryIfEmpty(
        string generatedFilePath,
        string modulePlural,
        string routeSegment)
    {
        var directory = Path.GetDirectoryName(generatedFilePath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return;
        }

        var directoryName = Path.GetFileName(directory);
        if (!directoryName.Equals(modulePlural, StringComparison.OrdinalIgnoreCase) &&
            !directoryName.Equals(routeSegment, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!Directory.EnumerateFileSystemEntries(directory).Any())
        {
            Directory.Delete(directory);
        }
    }

    private async Task<CodeGeneratorInstallPlanDto> BuildInstallPlanAsync(
        CodeGeneratorPreviewRequest request,
        bool hasConflicts,
        CancellationToken cancellationToken)
    {
        var tableName = request.TableName.Trim();
        var tableExists = await codeGeneratorRepository.TableExistsAsync(tableName, cancellationToken);
        var createTableSql = tableExists ? null : BuildCreateTableSql(request);
        var generatedMenuIds = BuildGeneratedMenus(request).Select(menu => menu.Id).ToArray();
        var menusInstalled = await codeGeneratorRepository.GeneratedMenusInstalledAsync(
            generatedMenuIds,
            cancellationToken);
        var steps = new[]
        {
            new CodeGeneratorInstallStepDto(
                "file-preview",
                "文件检查",
                hasConflicts ? "存在同名文件，生成前需要处理冲突或改名。" : "生成文件路径可写，未发现同名冲突。",
                hasConflicts ? "Warning" : "Done"),
            new CodeGeneratorInstallStepDto(
                "database-table",
                "数据表",
                tableExists ? "目标业务表已存在，可直接对接生成的 CRUD。" : "目标业务表不存在，生成时可自动执行下方 MySQL 建表脚本。",
                tableExists ? "Done" : "Warning"),
            new CodeGeneratorInstallStepDto(
                "auto-install",
                "自动安装",
                menusInstalled ? "数据库侧安装已完成，菜单权限已可用于授权。" : "生成时默认会自动建表并注册菜单权限，也可以关闭后手工安装。",
                menusInstalled ? "Done" : "Pending"),
            new CodeGeneratorInstallStepDto(
                "menu-permissions",
                "菜单权限",
                menusInstalled
                    ? (request.EnableImportExport
                        ? "业务菜单和查询、新增、编辑、删除、导入、导出按钮权限已注册，并已授予 Admin 角色。"
                        : "业务菜单和查询、新增、编辑、删除按钮权限已注册，并已授予 Admin 角色。")
                    : (request.EnableImportExport
                        ? "生成后会注册业务菜单和查询、新增、编辑、删除、导入、导出按钮权限，并授予 Admin 角色。"
                        : "生成后会注册业务菜单和按钮权限，并授予 Admin 角色。"),
                menusInstalled ? "Done" : "Pending"),
            new CodeGeneratorInstallStepDto(
                "tenant-scope",
                "租户隔离",
                IsTenantMode(request.TenantMode) ? "生成模板会写入 TenantId，并按当前租户过滤数据。" : "当前模块不生成租户过滤，请确认它确实属于平台级或共享数据。",
                IsTenantMode(request.TenantMode) ? "Done" : "Warning"),
            new CodeGeneratorInstallStepDto(
                "data-scope",
                "数据权限",
                IsDataScopeMode(request.DataScopeMode, "None") ? "未启用数据权限过滤。" : $"生成模板会通过 {request.DataScopeField} 接入 {request.DataScopeMode} 数据范围，列表、编辑、删除都会校验。",
                IsDataScopeMode(request.DataScopeMode, "None") ? "Warning" : "Done"),
            new CodeGeneratorInstallStepDto(
                "audit",
                "审计追踪",
                request.EnableAudit ? "生成模块走 Minimal API 和 EF Core，系统审计会记录请求与实体变更。" : "已关闭审计提示，请确认该模块不需要统一追踪。",
                request.EnableAudit ? "Done" : "Warning"),
            new CodeGeneratorInstallStepDto(
                "generate-files",
                "生成代码",
                "确认预览内容后点击生成，系统会写入后端和 Vben 前端文件。",
                "Pending"),
            new CodeGeneratorInstallStepDto(
                "restart-backend",
                "重启后端",
                "生成后重启后端，让新增 Endpoint、仓储、实体配置进入运行时。",
                "Pending"),
            new CodeGeneratorInstallStepDto(
                "verify-module",
                "验证模块",
                "重新进入系统，检查菜单、按钮权限、列表查询、新增、编辑和删除接口。",
                "Pending")
        };

        return new CodeGeneratorInstallPlanDto(tableExists, createTableSql, steps);
    }

    private static CodeGeneratorAutoInstallRequestDto BuildAutoInstallRequest(
        CodeGeneratorPreviewRequest request,
        string? createTableSql)
    {
        return new CodeGeneratorAutoInstallRequestDto(createTableSql, BuildGeneratedMenus(request));
    }

    private static IReadOnlyList<CodeGeneratorGeneratedMenuInstallDto> BuildGeneratedMenus(
        CodeGeneratorPreviewRequest request)
    {
        var moduleName = request.ModuleName.Trim();
        var businessName = request.BusinessName.Trim();
        var routePath = request.RoutePath.Trim().TrimEnd('/');
        var routeSegment = CodeGeneratorTemplateRenderer.GetRouteSegment(routePath);
        var permissionPrefix = request.PermissionPrefix.Trim();
        Guid? parentMenuId = null;
        if (!string.IsNullOrWhiteSpace(request.ParentMenuId))
        {
            parentMenuId = Guid.Parse(request.ParentMenuId.Trim());
        }

        var menuId = CodeGeneratorTemplateRenderer.CreateDeterministicGuid(moduleName, "menu");
        var queryPermissionId = CodeGeneratorTemplateRenderer.CreateDeterministicGuid(moduleName, "query");
        var createPermissionId = CodeGeneratorTemplateRenderer.CreateDeterministicGuid(moduleName, "create");
        var updatePermissionId = CodeGeneratorTemplateRenderer.CreateDeterministicGuid(moduleName, "update");
        var deletePermissionId = CodeGeneratorTemplateRenderer.CreateDeterministicGuid(moduleName, "delete");
        var importPermissionId = CodeGeneratorTemplateRenderer.CreateDeterministicGuid(moduleName, "import");
        var exportPermissionId = CodeGeneratorTemplateRenderer.CreateDeterministicGuid(moduleName, "export");
        var submitWorkflowPermissionId = CodeGeneratorTemplateRenderer.CreateDeterministicGuid(moduleName, "submit-workflow");
        var withdrawWorkflowPermissionId = CodeGeneratorTemplateRenderer.CreateDeterministicGuid(moduleName, "withdraw-workflow");

        List<CodeGeneratorGeneratedMenuInstallDto> menus =
        [
            new CodeGeneratorGeneratedMenuInstallDto(
                menuId,
                parentMenuId,
                moduleName,
                routePath,
                $"/business/{routeSegment}/index",
                businessName,
                "lucide:table-2",
                100,
                $"{permissionPrefix}:query",
                true,
                true),
            new CodeGeneratorGeneratedMenuInstallDto(
                queryPermissionId,
                menuId,
                $"{moduleName}QueryPermission",
                $"{permissionPrefix}:query",
                null,
                $"{permissionPrefix}:query",
                null,
                1,
                $"{permissionPrefix}:query",
                true,
                false),
            new CodeGeneratorGeneratedMenuInstallDto(
                createPermissionId,
                menuId,
                $"{moduleName}CreatePermission",
                $"{permissionPrefix}:create",
                null,
                $"{permissionPrefix}:create",
                null,
                2,
                $"{permissionPrefix}:create",
                true,
                false),
            new CodeGeneratorGeneratedMenuInstallDto(
                updatePermissionId,
                menuId,
                $"{moduleName}UpdatePermission",
                $"{permissionPrefix}:update",
                null,
                $"{permissionPrefix}:update",
                null,
                3,
                $"{permissionPrefix}:update",
                true,
                false),
            new CodeGeneratorGeneratedMenuInstallDto(
                deletePermissionId,
                menuId,
                $"{moduleName}DeletePermission",
                $"{permissionPrefix}:delete",
                null,
                $"{permissionPrefix}:delete",
                null,
                4,
                $"{permissionPrefix}:delete",
                true,
                false)
        ];
        if (request.EnableImportExport)
        {
            menus.Add(new CodeGeneratorGeneratedMenuInstallDto(
                importPermissionId,
                menuId,
                $"{moduleName}ImportPermission",
                $"{permissionPrefix}:import",
                null,
                $"{permissionPrefix}:import",
                null,
                5,
                $"{permissionPrefix}:import",
                true,
                false));
            menus.Add(new CodeGeneratorGeneratedMenuInstallDto(
                exportPermissionId,
                menuId,
                $"{moduleName}ExportPermission",
                $"{permissionPrefix}:export",
                null,
                $"{permissionPrefix}:export",
                null,
                6,
                $"{permissionPrefix}:export",
                true,
                false));
        }

        if (request.EnableWorkflow)
        {
            menus.Add(new CodeGeneratorGeneratedMenuInstallDto(
                submitWorkflowPermissionId,
                menuId,
                $"{moduleName}SubmitWorkflowPermission",
                $"{permissionPrefix}:submit-workflow",
                null,
                $"{permissionPrefix}:submit-workflow",
                null,
                7,
                $"{permissionPrefix}:submit-workflow",
                true,
                false));
            menus.Add(new CodeGeneratorGeneratedMenuInstallDto(
                withdrawWorkflowPermissionId,
                menuId,
                $"{moduleName}WithdrawWorkflowPermission",
                $"{permissionPrefix}:withdraw-workflow",
                null,
                $"{permissionPrefix}:withdraw-workflow",
                null,
                8,
                $"{permissionPrefix}:withdraw-workflow",
                true,
                false));
        }

        return menus;
    }

    private static string BuildCreateTableSql(CodeGeneratorPreviewRequest request)
    {
        var builder = new StringBuilder();
        var columns = new List<string>
        {
            "  `id` char(36) not null"
        };

        if (IsTenantMode(request.TenantMode))
        {
            columns.Add("  `TenantId` char(36) null");
        }

        columns.AddRange(request.Fields
            .Where(field => !field.IsPrimaryKey && !IsReservedSystemField(field))
            .OrderBy(field => field.Sort)
            .Select(field =>
                $"  `{EscapeIdentifier(field.ColumnName.Trim())}` {MapMySqlType(field)} {(field.IsRequired ? "not null" : "null")}"));

        if (request.EnableWorkflow)
        {
            columns.Add("  `workflow_instance_id` char(36) null");
            columns.Add("  `approval_status` varchar(32) not null default 'Draft'");
        }

        columns.Add("  `created_at` datetime(6) not null default current_timestamp(6)");
        columns.Add("  primary key (`id`)");

        if (IsTenantMode(request.TenantMode))
        {
            columns.Add("  key `idx_" + EscapeIdentifier(request.TableName.Trim()) + "_TenantId` (`TenantId`)");
        }

        builder.Append("create table `");
        builder.Append(EscapeIdentifier(request.TableName.Trim()));
        builder.AppendLine("` (");
        builder.AppendLine(string.Join("," + Environment.NewLine, columns));
        builder.Append(") engine=InnoDB default charset=utf8mb4 collate=utf8mb4_0900_ai_ci");

        if (!string.IsNullOrWhiteSpace(request.BusinessName))
        {
            builder.Append(" comment='");
            builder.Append(EscapeSqlLiteral(request.BusinessName.Trim()));
            builder.Append('\'');
        }

        builder.AppendLine(";");
        return builder.ToString();
    }

    private static string MapMySqlType(CodeGeneratorFieldConfigDto field)
    {
        var dotNetType = field.DotNetType.Trim().TrimEnd('?');
        if (dotNetType.Equals("int", StringComparison.OrdinalIgnoreCase))
        {
            return "int";
        }

        if (dotNetType.Equals("long", StringComparison.OrdinalIgnoreCase))
        {
            return "bigint";
        }

        if (dotNetType.Equals("decimal", StringComparison.OrdinalIgnoreCase))
        {
            return "decimal(18,2)";
        }

        if (dotNetType.Equals("double", StringComparison.OrdinalIgnoreCase))
        {
            return "double";
        }

        if (dotNetType.Equals("float", StringComparison.OrdinalIgnoreCase))
        {
            return "float";
        }

        if (dotNetType.Equals("bool", StringComparison.OrdinalIgnoreCase) ||
            dotNetType.Equals("boolean", StringComparison.OrdinalIgnoreCase))
        {
            return "tinyint(1)";
        }

        if (dotNetType.Equals("DateTime", StringComparison.OrdinalIgnoreCase) ||
            dotNetType.Equals("DateTimeOffset", StringComparison.OrdinalIgnoreCase))
        {
            return "datetime(6)";
        }

        return $"varchar({field.MaxLength ?? 256})";
    }

    private static bool IsTenantMode(string tenantMode)
    {
        return string.Equals(tenantMode, "Tenant", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDataScopeMode(string dataScopeMode, string expected)
    {
        return string.Equals(dataScopeMode, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReservedSystemField(CodeGeneratorFieldConfigDto field)
    {
        var propertyName = field.PropertyName.Trim();
        var columnName = field.ColumnName.Trim();
        return propertyName.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("TenantId", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("WorkflowInstanceId", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("ApprovalStatus", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("CreatedAt", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("UpdatedAt", StringComparison.OrdinalIgnoreCase) ||
               propertyName.Equals("DeletedAt", StringComparison.OrdinalIgnoreCase) ||
               columnName.Equals("id", StringComparison.OrdinalIgnoreCase) ||
               columnName.Equals("tenant_id", StringComparison.OrdinalIgnoreCase) ||
               columnName.Equals("workflow_instance_id", StringComparison.OrdinalIgnoreCase) ||
               columnName.Equals("approval_status", StringComparison.OrdinalIgnoreCase) ||
               columnName.Equals("created_at", StringComparison.OrdinalIgnoreCase) ||
               columnName.Equals("updated_at", StringComparison.OrdinalIgnoreCase) ||
               columnName.Equals("deleted_at", StringComparison.OrdinalIgnoreCase) ||
               columnName.Equals("create_time", StringComparison.OrdinalIgnoreCase) ||
               columnName.Equals("update_time", StringComparison.OrdinalIgnoreCase) ||
               columnName.Equals("create_by", StringComparison.OrdinalIgnoreCase) ||
               columnName.Equals("update_by", StringComparison.OrdinalIgnoreCase) ||
               columnName.Equals("is_deleted", StringComparison.OrdinalIgnoreCase);
    }

    private static string EscapeIdentifier(string value)
    {
        return value.Replace("`", "``", StringComparison.Ordinal);
    }

    private static string EscapeSqlLiteral(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }

    private static string ResolveSafeFullPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) ||
            relativePath.Contains('\\') ||
            relativePath.Split('/').Any(segment => segment == "..") ||
            Path.IsPathRooted(relativePath) ||
            !AllowedRoots.Any(root => relativePath.StartsWith(root, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("生成路径不在允许范围内。");
        }

        var workspaceRoot = FindWorkspaceRoot();
        var fullPath = Path.GetFullPath(Path.Combine(workspaceRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var normalizedRoot = Path.GetFullPath(workspaceRoot);
        if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("生成路径越界。");
        }

        return fullPath;
    }

    private static string FindWorkspaceRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "MiniAdmin.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
