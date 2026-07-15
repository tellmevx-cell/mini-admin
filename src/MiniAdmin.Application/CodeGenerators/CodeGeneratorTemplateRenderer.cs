using System.Text;
using System.Security.Cryptography;
using MiniAdmin.Application.Contracts.CodeGenerators;

namespace MiniAdmin.Application.CodeGenerators;

public sealed class CodeGeneratorTemplateRenderer
{
    public IReadOnlyList<CodeGeneratorPreviewFileDto> Render(CodeGeneratorPreviewRequest request)
    {
        var moduleName = request.ModuleName.Trim();
        var modulePlural = ToPlural(moduleName);
        var tableName = request.TableName.Trim();
        var routePath = request.RoutePath.Trim().TrimEnd('/');
        var routeSegment = GetRouteSegment(request.RoutePath);
        var permissionPrefix = request.PermissionPrefix.Trim();
        var tenantMode = request.TenantMode.Trim();
        var dataScopeMode = request.DataScopeMode.Trim();
        var dataScopeField = request.DataScopeField?.Trim();
        var enableWorkflow = request.EnableWorkflow;
        var workflowBusinessType = request.WorkflowBusinessType?.Trim() ?? string.Empty;
        var entityFields = request.Fields
            .Where(field => !field.IsPrimaryKey && !IsReservedSystemField(field))
            .OrderBy(field => field.Sort)
            .ToArray();

        var files = new List<CodeGeneratorPreviewFileDto>
        {
            new CodeGeneratorPreviewFileDto(
                $"src/MiniAdmin.Domain/Entities/{moduleName}.cs",
                RenderEntity(moduleName, entityFields, tenantMode, enableWorkflow, workflowBusinessType),
                false),
            new CodeGeneratorPreviewFileDto(
                $"src/MiniAdmin.Application.Contracts/{modulePlural}/{moduleName}Dtos.cs",
                RenderContracts(moduleName, modulePlural, entityFields, dataScopeMode, request.EnableImportExport, enableWorkflow),
                false),
            new CodeGeneratorPreviewFileDto(
                $"src/MiniAdmin.Application.Contracts/{modulePlural}/I{moduleName}AppService.cs",
                RenderAppServiceInterface(moduleName, modulePlural, dataScopeMode, request.EnableImportExport, enableWorkflow),
                false),
            new CodeGeneratorPreviewFileDto(
                $"src/MiniAdmin.Application.Contracts/{modulePlural}/I{moduleName}Repository.cs",
                RenderRepositoryInterface(moduleName, modulePlural, dataScopeMode, request.EnableImportExport, enableWorkflow),
                false),
            new CodeGeneratorPreviewFileDto(
                $"src/MiniAdmin.Application/{modulePlural}/{moduleName}AppService.cs",
                RenderAppService(
                    moduleName,
                    modulePlural,
                    request.BusinessName,
                    routePath,
                    permissionPrefix,
                    dataScopeMode,
                    entityFields,
                    request.EnableImportExport,
                    enableWorkflow,
                    workflowBusinessType),
                false),
            new CodeGeneratorPreviewFileDto(
                $"src/MiniAdmin.Infrastructure/Persistence/Ef{moduleName}Repository.cs",
                RenderRepository(moduleName, modulePlural, entityFields, tenantMode, dataScopeMode, dataScopeField, request.EnableImportExport, enableWorkflow),
                false),
            new CodeGeneratorPreviewFileDto(
                $"src/MiniAdmin.Infrastructure/Persistence/Generated/{moduleName}EntityTypeConfiguration.cs",
                RenderEntityConfiguration(tableName, moduleName, entityFields, tenantMode, enableWorkflow),
                false)
        };

        if (enableWorkflow)
        {
            files.Add(new CodeGeneratorPreviewFileDto(
                $"src/MiniAdmin.Infrastructure/Persistence/Generated/{moduleName}WorkflowStateHandler.cs",
                RenderWorkflowStateHandler(moduleName, tenantMode),
                false));
        }

        if (request.EnableImportExport)
        {
            files.Add(new CodeGeneratorPreviewFileDto(
                $"src/MiniAdmin.Api/Generated/{moduleName}TransportEndpoints.cs",
                RenderTransportEndpoints(moduleName, modulePlural, routePath, permissionPrefix),
                false));
        }

        files.Add(new CodeGeneratorPreviewFileDto(
            $"src/MiniAdmin.Application/{modulePlural}/{moduleName}PageDefinitionProvider.cs",
            RenderPageDefinitionProvider(
                moduleName,
                modulePlural,
                request.BusinessName,
                routePath,
                routeSegment,
                permissionPrefix,
                request.ParentMenuId,
                request.EnableImportExport,
                enableWorkflow),
            false));
        files.Add(new CodeGeneratorPreviewFileDto(
            $"frontend/vue-vben-admin/apps/web-antd/src/api/business/{routeSegment}.ts",
            RenderFrontendApi(moduleName, routeSegment, entityFields, request.EnableImportExport, enableWorkflow),
            false));
        files.Add(new CodeGeneratorPreviewFileDto(
            $"frontend/vue-vben-admin/apps/web-antd/src/views/business/{routeSegment}/index.vue",
            RenderFrontendPage(moduleName, request.BusinessName, routeSegment, permissionPrefix, entityFields, request.EnableImportExport, enableWorkflow),
            false));

        return files;
    }

    private static string RenderWorkflowStateHandler(
        string moduleName,
        string tenantMode)
    {
        var isTenantMode = IsTenantMode(tenantMode);
        var tenantUsing = isTenantMode
            ? "using MiniAdmin.Application.Contracts.MultiTenancy;" + Environment.NewLine
            : string.Empty;
        var constructorParameters = isTenantMode
            ? "MiniAdminDbContext dbContext," + Environment.NewLine + "    ICurrentTenant currentTenant"
            : "MiniAdminDbContext dbContext";
        var entitySource = isTenantMode
            ? $"ApplyTenantFilter(dbContext.Set<{moduleName}>())"
            : $"dbContext.Set<{moduleName}>()";
        var tenantFilterMethod = isTenantMode
            ? $$"""

                   private IQueryable<{{moduleName}}> ApplyTenantFilter(IQueryable<{{moduleName}}> source)
                   {
                       return currentTenant.IsTenant
                           ? source.Where(x => x.TenantId == currentTenant.TenantId)
                           : source.Where(x => x.TenantId == null);
                   }
               """
            : string.Empty;

        return $$"""
               using Microsoft.EntityFrameworkCore;
               {{tenantUsing}}using MiniAdmin.Application.Contracts.Workflows;
               using MiniAdmin.Domain.Entities;

               namespace MiniAdmin.Infrastructure.Persistence.Generated;

               public sealed class {{moduleName}}WorkflowStateHandler(
                   {{constructorParameters}}) : IWorkflowBusinessStateHandler
               {
                   public async Task HandleAsync(
                       WorkflowInstanceDto instance,
                       CancellationToken cancellationToken = default)
                   {
                       if (!{{moduleName}}.TryParseBusinessKey(instance.BusinessKey, out var id))
                       {
                           return;
                       }

                       var entity = await {{entitySource}}
                           .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
                       if (entity is null)
                       {
                           return;
                       }

                       entity.WorkflowInstanceId = instance.Id;
                       entity.ApprovalStatus = instance.Status switch
                       {
                           "Pending" => "Pending",
                           "Approved" => "Approved",
                           "Rejected" => "Rejected",
                           "Withdrawn" => "Withdrawn",
                           _ => entity.ApprovalStatus
                       };

                       await dbContext.SaveChangesAsync(cancellationToken);
                   }
               {{tenantFilterMethod}}
               }
               """;
    }

    public static IReadOnlyList<string> GetPermissionCodes(
        string permissionPrefix,
        bool enableImportExport = false,
        bool enableWorkflow = false)
    {
        var prefix = permissionPrefix.Trim();
        var permissions = new List<string>
        {
            $"{prefix}:query",
            $"{prefix}:create",
            $"{prefix}:update",
            $"{prefix}:delete"
        };
        if (enableImportExport)
        {
            permissions.Add($"{prefix}:import");
            permissions.Add($"{prefix}:export");
        }

        if (enableWorkflow)
        {
            permissions.Add($"{prefix}:submit-workflow");
            permissions.Add($"{prefix}:withdraw-workflow");
        }

        return permissions;
    }

    public static string ToPlural(string moduleName)
    {
        return moduleName.EndsWith('s') ? moduleName : $"{moduleName}s";
    }

    public static string GetRouteSegment(string routePath)
    {
        var normalized = routePath.Trim().Trim('/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "generated";
        }

        return normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Last();
    }

    private static string RenderEntity(
        string moduleName,
        IReadOnlyList<CodeGeneratorFieldConfigDto> fields,
        string tenantMode,
        bool enableWorkflow,
        string workflowBusinessType)
    {
        var builder = new StringBuilder();
        builder.AppendLine("namespace MiniAdmin.Domain.Entities;");
        builder.AppendLine();
        builder.AppendLine($"public sealed class {moduleName}");
        builder.AppendLine("{");
        builder.AppendLine("    public Guid Id { get; set; }");
        builder.AppendLine();
        if (enableWorkflow)
        {
            builder.AppendLine($"    private const string WorkflowBusinessType = \"{EscapeCSharpString(workflowBusinessType)}\";");
            builder.AppendLine();
        }

        if (IsTenantMode(tenantMode))
        {
            builder.AppendLine("    public Guid? TenantId { get; set; }");
            builder.AppendLine();
        }

        if (enableWorkflow)
        {
            builder.AppendLine("    public string? WorkflowInstanceId { get; set; }");
            builder.AppendLine();
            builder.AppendLine("    public string ApprovalStatus { get; set; } = \"Draft\";");
            builder.AppendLine();
        }

        foreach (var field in fields)
        {
            builder.AppendLine($"    public {GetEntityType(field)} {field.PropertyName} {{ get; set; }}{GetInitializer(field)}");
            builder.AppendLine();
        }

        builder.AppendLine("    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;");
        if (enableWorkflow)
        {
            builder.AppendLine();
            builder.AppendLine("    public static string CreateBusinessKey(Guid id)");
            builder.AppendLine("    {");
            builder.AppendLine("        return $\"{WorkflowBusinessType}:{id}\";");
            builder.AppendLine("    }");
            builder.AppendLine();
            builder.AppendLine("    public static bool TryParseBusinessKey(string? businessKey, out Guid id)");
            builder.AppendLine("    {");
            builder.AppendLine("        id = Guid.Empty;");
            builder.AppendLine("        if (string.IsNullOrWhiteSpace(businessKey))");
            builder.AppendLine("        {");
            builder.AppendLine("            return false;");
            builder.AppendLine("        }");
            builder.AppendLine();
            builder.AppendLine("        var prefix = $\"{WorkflowBusinessType}:\";");
            builder.AppendLine("        return businessKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&");
            builder.AppendLine("               Guid.TryParse(businessKey[prefix.Length..], out id);");
            builder.AppendLine("    }");
        }

        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string RenderContracts(
        string moduleName,
        string modulePlural,
        IReadOnlyList<CodeGeneratorFieldConfigDto> fields,
        string dataScopeMode,
        bool enableImportExport,
        bool enableWorkflow)
    {
        var isDataScopeEnabled = IsDataScopeEnabled(dataScopeMode);
        var dtoParameters = string.Join(
            "," + Environment.NewLine,
            new[] { "    string Id" }
                .Concat(enableWorkflow
                    ? ["    string? WorkflowInstanceId", "    string ApprovalStatus"]
                    : [])
                .Concat(fields.Select(field => $"    {GetDtoType(field)} {field.PropertyName}"))
                .Concat(["    DateTimeOffset CreatedAt"]));
        var saveParameters = string.Join(
            "," + Environment.NewLine,
            fields
                .Where(field => field.CreateVisible || field.UpdateVisible)
                .Select(field => $"    {GetDtoType(field)} {field.PropertyName}"));
        var queryParameters = string.Join(
            "," + Environment.NewLine,
            new[]
                {
                    "    int Page = 1",
                    "    int PageSize = 20",
                    "    string? Keyword = null"
                }
                .Concat(fields
                    .Where(IsQueryEnabled)
                    .SelectMany(RenderQueryParameters))
                .Concat(isDataScopeEnabled ? ["    string? CurrentUserName = null"] : []));

        var importExportDtos = enableImportExport
            ? $$"""

               public sealed record {{moduleName}}ExportFileDto(
                   string FileName,
                   string ContentType,
                   byte[] Content);

               public sealed record {{moduleName}}ImportResultDto(
                   int CreatedCount,
                   IReadOnlyList<{{moduleName}}ImportErrorDto> Errors);

               public sealed record {{moduleName}}ImportErrorDto(
                   int RowNumber,
                   string Field,
                   string Message);
               """
            : string.Empty;

        return $$"""
               namespace MiniAdmin.Application.Contracts.{{modulePlural}};

               public sealed record {{moduleName}}Dto(
               {{dtoParameters}});

               public sealed record {{moduleName}}ListQuery(
               {{queryParameters}});

               public sealed record Save{{moduleName}}Request(
               {{saveParameters}});
               {{RenderWorkflowContracts(moduleName, enableWorkflow)}}
               {{importExportDtos}}
               """;
    }

    private static string RenderWorkflowContracts(string moduleName, bool enableWorkflow)
    {
        return enableWorkflow
            ? $$"""

               public sealed record Submit{{moduleName}}WorkflowRequest(string? Comment);

               public sealed record Withdraw{{moduleName}}WorkflowRequest(string? Comment);
               """
            : string.Empty;
    }

    private static string RenderAppServiceInterface(
        string moduleName,
        string modulePlural,
        string dataScopeMode,
        bool enableImportExport,
        bool enableWorkflow)
    {
        const string deleteParameters = "Guid id, CancellationToken cancellationToken = default";
        var updateParameters = "Guid id, Save" + moduleName +
            "Request request, CancellationToken cancellationToken = default";
        var importExportMethods = enableImportExport
            ? $$"""

                   Task<{{moduleName}}ExportFileDto> ExportAsync({{moduleName}}ListQuery query, CancellationToken cancellationToken = default);

                   Task<{{moduleName}}ExportFileDto> GetImportTemplateAsync(CancellationToken cancellationToken = default);

                   Task<{{moduleName}}ImportResultDto> PreviewImportAsync(Stream stream, CancellationToken cancellationToken = default);

                   Task<{{moduleName}}ImportResultDto> ImportAsync(Stream stream, CancellationToken cancellationToken = default);

                   Task<{{moduleName}}ExportFileDto> ExportImportErrorsAsync(Stream stream, CancellationToken cancellationToken = default);
               """
            : string.Empty;
        var workflowMethods = enableWorkflow
            ? $$"""

                   Task<{{moduleName}}Dto?> SubmitWorkflowAsync(
                       Guid id,
                       Submit{{moduleName}}WorkflowRequest request,
                       CancellationToken cancellationToken = default);

                   Task<{{moduleName}}Dto?> WithdrawWorkflowAsync(
                       Guid id,
                       Withdraw{{moduleName}}WorkflowRequest request,
                       CancellationToken cancellationToken = default);
               """
            : string.Empty;
        return $$"""
               using MiniAdmin.Application.Contracts.Common;
               using MiniAdmin.Application.Contracts.CodeGenerators;

               namespace MiniAdmin.Application.Contracts.{{modulePlural}};

               public interface I{{moduleName}}AppService : IGeneratedCrudAppService
               {
                   Task<PageResult<{{moduleName}}Dto>> GetListAsync({{moduleName}}ListQuery query, CancellationToken cancellationToken = default);
               {{importExportMethods}}

                   Task<{{moduleName}}Dto> CreateAsync(Save{{moduleName}}Request request, CancellationToken cancellationToken = default);

                   Task<{{moduleName}}Dto?> UpdateAsync({{updateParameters}});

                   Task<bool> DeleteAsync({{deleteParameters}});
               {{workflowMethods}}
               }
               """;
    }

    private static IEnumerable<string> RenderQueryParameters(CodeGeneratorFieldConfigDto field)
    {
        if (IsRangeQuery(field))
        {
            var type = GetNullableDtoType(field);
            yield return $"    {type} {field.PropertyName}Begin = null";
            yield return $"    {type} {field.PropertyName}End = null";
            yield break;
        }

        yield return $"    {GetNullableDtoType(field)} {field.PropertyName} = null";
    }

    private static string RenderQueryFilter(CodeGeneratorFieldConfigDto field)
    {
        var propertyName = field.PropertyName;
        if (IsRangeQuery(field))
        {
            return $$"""
                       if (query.{{propertyName}}Begin is not null)
                       {
                           source = source.Where(entity => entity.{{propertyName}} >= query.{{propertyName}}Begin);
                       }

                       if (query.{{propertyName}}End is not null)
                       {
                           source = source.Where(entity => entity.{{propertyName}} <= query.{{propertyName}}End);
                       }
               """;
        }

        if (IsStringField(field))
        {
            return IsEqualsQuery(field)
                ? $$"""
                       if (!string.IsNullOrWhiteSpace(query.{{propertyName}}))
                       {
                           source = source.Where(entity => entity.{{propertyName}} == query.{{propertyName}});
                       }
               """
                : $$"""
                       if (!string.IsNullOrWhiteSpace(query.{{propertyName}}))
                       {
                           source = source.Where(entity => entity.{{propertyName}}.Contains(query.{{propertyName}}));
                       }
               """;
        }

        return $$"""
                   if (query.{{propertyName}} is not null)
                   {
                       source = source.Where(entity => entity.{{propertyName}} == query.{{propertyName}});
                   }
               """;
    }

    private static string RenderRepositoryInterface(
        string moduleName,
        string modulePlural,
        string dataScopeMode,
        bool enableImportExport,
        bool enableWorkflow)
    {
        var isDataScopeEnabled = IsDataScopeEnabled(dataScopeMode);
        var updateParameters = IsDataScopeEnabled(dataScopeMode)
            ? "Guid id, Save" + moduleName + "Request request, string? currentUserName, CancellationToken cancellationToken = default"
            : "Guid id, Save" + moduleName + "Request request, CancellationToken cancellationToken = default";
        var deleteParameters = IsDataScopeEnabled(dataScopeMode)
            ? "Guid id, string? currentUserName, CancellationToken cancellationToken = default"
            : "Guid id, CancellationToken cancellationToken = default";
        var getParameters = isDataScopeEnabled
            ? "Guid id, string? currentUserName, CancellationToken cancellationToken = default"
            : "Guid id, CancellationToken cancellationToken = default";
        var workflowSetParameters = isDataScopeEnabled
            ? "Guid id, string approvalStatus, string? workflowInstanceId, string? currentUserName, CancellationToken cancellationToken = default"
            : "Guid id, string approvalStatus, string? workflowInstanceId, CancellationToken cancellationToken = default";
        var importExportMethods = enableImportExport
            ? $$"""

                   Task<IReadOnlyList<{{moduleName}}Dto>> GetExportListAsync({{moduleName}}ListQuery query, int limit = 10000, CancellationToken cancellationToken = default);
               """
            : string.Empty;
        var workflowMethods = enableWorkflow
            ? $$"""

                   Task<{{moduleName}}Dto?> GetAsync({{getParameters}});

                   Task<{{moduleName}}Dto?> SetWorkflowStateAsync({{workflowSetParameters}});
               """
            : string.Empty;
        return $$"""
               using MiniAdmin.Application.Contracts.Common;
               using MiniAdmin.Application.Contracts.CodeGenerators;

               namespace MiniAdmin.Application.Contracts.{{modulePlural}};

               public interface I{{moduleName}}Repository : IGeneratedCrudRepository
               {
                   Task<PageResult<{{moduleName}}Dto>> GetListAsync({{moduleName}}ListQuery query, CancellationToken cancellationToken = default);
               {{importExportMethods}}

                   Task<{{moduleName}}Dto> CreateAsync(Save{{moduleName}}Request request, CancellationToken cancellationToken = default);

                   Task<{{moduleName}}Dto?> UpdateAsync({{updateParameters}});

                   Task<bool> DeleteAsync({{deleteParameters}});
               {{workflowMethods}}
               }
               """;
    }

    private static string RenderAppService(
        string moduleName,
        string modulePlural,
        string businessName,
        string routePath,
        string permissionPrefix,
        string dataScopeMode,
        IReadOnlyList<CodeGeneratorFieldConfigDto> fields,
        bool enableImportExport,
        bool enableWorkflow,
        string workflowBusinessType)
    {
        var fieldName = char.ToLowerInvariant(moduleName[0]) + moduleName[1..];
        var constructorDependencies = new List<string>();
        if (enableImportExport)
        {
            constructorDependencies.Add("IWorkbookService workbookService");
        }

        var requiresCurrentUser = IsDataScopeEnabled(dataScopeMode) || enableWorkflow;
        if (requiresCurrentUser)
        {
            constructorDependencies.Add("ICurrentUserContext currentUser");
        }

        if (enableWorkflow)
        {
            constructorDependencies.Add("IWorkflowAppService workflowAppService");
        }

        var constructorSuffix = constructorDependencies.Count == 0
            ? string.Empty
            : ", " + string.Join(", ", constructorDependencies);
        var updateSignature = "Guid id, Save" + moduleName +
            "Request request, CancellationToken cancellationToken = default";
        var updateCall = IsDataScopeEnabled(dataScopeMode)
            ? $"{fieldName}Repository.UpdateAsync(id, request, currentUser.UserName, cancellationToken)"
            : $"{fieldName}Repository.UpdateAsync(id, request, cancellationToken)";
        const string deleteSignature = "Guid id, CancellationToken cancellationToken = default";
        var deleteCall = IsDataScopeEnabled(dataScopeMode)
            ? $"{fieldName}Repository.DeleteAsync(id, currentUser.UserName, cancellationToken)"
            : $"{fieldName}Repository.DeleteAsync(id, cancellationToken)";
        var listQuery = IsDataScopeEnabled(dataScopeMode)
            ? "query with { CurrentUserName = currentUser.UserName }"
            : "query";
        var importExportMethods = enableImportExport
            ? RenderAppServiceImportExport(moduleName, fieldName, fields, IsDataScopeEnabled(dataScopeMode))
            : string.Empty;
        var securityUsing = requiresCurrentUser
            ? "using MiniAdmin.Application.Contracts.Security;" + Environment.NewLine
            : string.Empty;
        var workflowUsing = enableWorkflow
            ? "using MiniAdmin.Application.Contracts.Workflows;" + Environment.NewLine +
              "using MiniAdmin.Domain.Entities;" + Environment.NewLine +
              "using System.Text.Json;" + Environment.NewLine
            : string.Empty;
        var workflowMethods = enableWorkflow
            ? RenderAppServiceWorkflow(
                moduleName,
                fieldName,
                businessName,
                permissionPrefix,
                dataScopeMode,
                workflowBusinessType)
            : string.Empty;
        var apiRoute = routePath.Trim().Trim('/');
        var resource = permissionPrefix.Replace(':', '.');
        return $$"""
               using MiniAdmin.Application.Contracts.Common;
               using MiniAdmin.Application.Contracts.{{modulePlural}};
               {{securityUsing}}using MiniAdmin.Platform.DynamicApi;
               {{workflowUsing}}

               namespace MiniAdmin.Application.{{modulePlural}};

               [DynamicApi("{{apiRoute}}", Name = "{{moduleName}}", Tag = "Generated Business")]
               public sealed class {{moduleName}}AppService(I{{moduleName}}Repository {{fieldName}}Repository{{constructorSuffix}}) : I{{moduleName}}AppService
               {
                   [DynamicGet(
                       "list",
                       Permission = "{{permissionPrefix}}:query",
                       Resource = "{{resource}}",
                       Action = "query",
                       OperationId = "{{moduleName}}_GetList",
                       Summary = "查询{{EscapeCSharpString(businessName)}}")]
                   public Task<PageResult<{{moduleName}}Dto>> GetListAsync({{moduleName}}ListQuery query, CancellationToken cancellationToken = default)
                   {
                       return {{fieldName}}Repository.GetListAsync({{listQuery}}, cancellationToken);
                   }
               {{importExportMethods}}

                   [DynamicPost(
                       Permission = "{{permissionPrefix}}:create",
                       Resource = "{{resource}}",
                       Action = "create",
                       OperationId = "{{moduleName}}_Create",
                       Summary = "创建{{EscapeCSharpString(businessName)}}")]
                   public Task<{{moduleName}}Dto> CreateAsync(Save{{moduleName}}Request request, CancellationToken cancellationToken = default)
                   {
                       return {{fieldName}}Repository.CreateAsync(request, cancellationToken);
                   }

                   [DynamicPut(
                       "{id:guid}",
                       Permission = "{{permissionPrefix}}:update",
                       Resource = "{{resource}}",
                       Action = "update",
                       OperationId = "{{moduleName}}_Update",
                       Summary = "更新{{EscapeCSharpString(businessName)}}")]
                   public Task<{{moduleName}}Dto?> UpdateAsync({{updateSignature}})
                   {
                       return {{updateCall}};
                   }

                   [DynamicDelete(
                       "{id:guid}",
                       Permission = "{{permissionPrefix}}:delete",
                       Resource = "{{resource}}",
                       Action = "delete",
                       OperationId = "{{moduleName}}_Delete",
                       Summary = "删除{{EscapeCSharpString(businessName)}}")]
                   public Task<bool> DeleteAsync({{deleteSignature}})
                   {
                       return {{deleteCall}};
                   }
               {{workflowMethods}}
               }
               """;
    }

    private static string RenderAppServiceWorkflow(
        string moduleName,
        string fieldName,
        string businessName,
        string permissionPrefix,
        string dataScopeMode,
        string workflowBusinessType)
    {
        var entityTypeReference = $"global::MiniAdmin.Domain.Entities.{moduleName}";
        var isDataScopeEnabled = IsDataScopeEnabled(dataScopeMode);
        var getCall = isDataScopeEnabled
            ? $"{fieldName}Repository.GetAsync(id, currentUser.UserName, cancellationToken)"
            : $"{fieldName}Repository.GetAsync(id, cancellationToken)";
        var setPendingCall = isDataScopeEnabled
            ? $"{fieldName}Repository.SetWorkflowStateAsync(id, \"Pending\", instance.Id, currentUser.UserName, cancellationToken)"
            : $"{fieldName}Repository.SetWorkflowStateAsync(id, \"Pending\", instance.Id, cancellationToken)";
        var setWithdrawnCall = isDataScopeEnabled
            ? $"{fieldName}Repository.SetWorkflowStateAsync(id, \"Withdrawn\", withdrawn.Id, currentUser.UserName, cancellationToken)"
            : $"{fieldName}Repository.SetWorkflowStateAsync(id, \"Withdrawn\", withdrawn.Id, cancellationToken)";
        var resource = permissionPrefix.Replace(':', '.');

        return $$"""

                   private static readonly JsonSerializerOptions WorkflowJsonOptions = new()
                   {
                       WriteIndented = false
                   };

                   [DynamicPost(
                       "{id:guid}/submit-workflow",
                       Permission = "{{permissionPrefix}}:submit-workflow",
                       Resource = "{{resource}}",
                       Action = "submit-workflow",
                       OperationId = "{{moduleName}}_SubmitWorkflow",
                       Summary = "提交{{EscapeCSharpString(businessName)}}审批")]
                   public async Task<{{moduleName}}Dto?> SubmitWorkflowAsync(
                       Guid id,
                       Submit{{moduleName}}WorkflowRequest request,
                       CancellationToken cancellationToken = default)
                   {
                       var user = new WorkflowUserContext(currentUser.UserId, currentUser.UserName);
                       var item = await {{getCall}};
                       if (item is null)
                       {
                           return null;
                       }

                       if (item.ApprovalStatus is "Pending")
                       {
                           throw new InvalidOperationException("{{businessName}}已在审批中，不能重复提交。");
                       }

                       if (item.ApprovalStatus is "Approved")
                       {
                           throw new InvalidOperationException("{{businessName}}已审批通过，不能重新提交。");
                       }

                       var definition = await workflowAppService.ResolveBusinessDefinitionAsync("{{EscapeCSharpString(workflowBusinessType)}}", cancellationToken)
                           ?? throw new InvalidOperationException("未配置可用的{{businessName}}审批流程，请先在审批中心配置业务绑定。");
                       var formDataJson = JsonSerializer.Serialize(new
                       {
                           id = item.Id,
                           businessName = "{{EscapeCSharpString(businessName)}}",
                           approvalStatus = item.ApprovalStatus,
                           comment = request.Comment
                       }, WorkflowJsonOptions);
                       var instance = await workflowAppService.StartInstanceAsync(
                           new StartWorkflowInstanceRequest(
                               Guid.Parse(definition.DefinitionId),
                               "{{EscapeCSharpString(businessName)}}审批：" + item.Id,
                               {{entityTypeReference}}.CreateBusinessKey(id),
                               formDataJson),
                           user,
                           cancellationToken);

                       return await {{setPendingCall}};
                   }

                   [DynamicPost(
                       "{id:guid}/withdraw-workflow",
                       Permission = "{{permissionPrefix}}:withdraw-workflow",
                       Resource = "{{resource}}",
                       Action = "withdraw-workflow",
                       OperationId = "{{moduleName}}_WithdrawWorkflow",
                       Summary = "撤回{{EscapeCSharpString(businessName)}}审批")]
                   public async Task<{{moduleName}}Dto?> WithdrawWorkflowAsync(
                       Guid id,
                       Withdraw{{moduleName}}WorkflowRequest request,
                       CancellationToken cancellationToken = default)
                   {
                       var user = new WorkflowUserContext(currentUser.UserId, currentUser.UserName);
                       var item = await {{getCall}};
                       if (item is null)
                       {
                           return null;
                       }

                       if (item.ApprovalStatus is not "Pending")
                       {
                           throw new InvalidOperationException("只有审批中的{{businessName}}可以撤回。");
                       }

                       if (string.IsNullOrWhiteSpace(item.WorkflowInstanceId) ||
                           !Guid.TryParse(item.WorkflowInstanceId, out var workflowInstanceId))
                       {
                           throw new InvalidOperationException("{{businessName}}没有关联的流程实例，无法撤回。");
                       }

                       var withdrawn = await workflowAppService.WithdrawAsync(
                           workflowInstanceId,
                           new WorkflowActionRequest(request.Comment),
                           user,
                           cancellationToken) ?? throw new InvalidOperationException("关联流程实例不存在，无法撤回。");

                       return await {{setWithdrawnCall}};
                   }
               """;
    }

    private static string RenderAppServiceImportExport(
        string moduleName,
        string fieldName,
        IReadOnlyList<CodeGeneratorFieldConfigDto> fields,
        bool isDataScopeEnabled)
    {
        var exportQuery = isDataScopeEnabled
            ? "query with { CurrentUserName = currentUser.UserName }"
            : "query";
        var importFields = fields
            .Where(field => field.CreateVisible || field.UpdateVisible)
            .OrderBy(field => field.Sort)
            .ToArray();
        var exportHeaders = string.Join(", ", fields.Select(field => $"\"{EscapeCSharpString(field.DisplayName)}\""));
        var importHeaders = string.Join(", ", importFields.Select(field => $"\"{EscapeCSharpString(field.DisplayName)}\""));
        var importErrorHeaders = string.Join(
            ", ",
            importFields
                .Select(field => $"\"{EscapeCSharpString(field.DisplayName)}\"")
                .Concat(["\"失败原因\""]));
        var exportValues = string.Join(
            "," + Environment.NewLine,
            fields.Select(field => $"                Convert.ToString(item.{field.PropertyName}) ?? string.Empty"));
        var templateValues = string.Join(
            ", ",
            importFields.Select(GetImportTemplateValue));
        var requiredChecks = string.Join(
            Environment.NewLine,
            importFields
                .Where(field => field.IsRequired)
                .Select((field, index) => $$"""
                           if (string.IsNullOrWhiteSpace(GetCell(row, {{index}})))
                           {
                               errors.Add(new {{moduleName}}ImportErrorDto(rowNumber, "{{field.PropertyName}}", "{{EscapeCSharpString(field.DisplayName)}}不能为空."));
                               continue;
                           }
                   """));
        var saveValues = string.Join(
            "," + Environment.NewLine,
            importFields.Select((field, index) => $"                ConvertCell<{GetDtoType(field)}>(GetCell(row, {index}))"));
        var errorRows = string.Join(
            "," + Environment.NewLine,
            importFields.Select((field, index) => $"                GetCell(sourceRow, {index})"));

        return $$"""

                   public async Task<{{moduleName}}ExportFileDto> ExportAsync({{moduleName}}ListQuery query, CancellationToken cancellationToken = default)
                   {
                       var items = await {{fieldName}}Repository.GetExportListAsync({{exportQuery}}, cancellationToken: cancellationToken);
                       var rows = new List<IReadOnlyList<string>>
                       {
                           new[] { {{exportHeaders}} }
                       };
                       rows.AddRange(items.Select(item => (IReadOnlyList<string>)new[]
                       {
               {{exportValues}}
                       }));

                       return new {{moduleName}}ExportFileDto(
                           "mini-admin-{{fieldName}}.xlsx",
                           "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                           workbookService.CreateWorkbook(rows));
                   }

                   public Task<{{moduleName}}ExportFileDto> GetImportTemplateAsync(CancellationToken cancellationToken = default)
                   {
                       var rows = new List<IReadOnlyList<string>>
                       {
                           new[] { {{importHeaders}} },
                           new[] { {{templateValues}} }
                       };

                       return Task.FromResult(new {{moduleName}}ExportFileDto(
                           "mini-admin-{{fieldName}}-import-template.xlsx",
                           "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                           workbookService.CreateWorkbook(rows)));
                   }

                   public Task<{{moduleName}}ImportResultDto> PreviewImportAsync(Stream stream, CancellationToken cancellationToken = default)
                   {
                       var parsed = ParseImportRows(workbookService.ReadWorkbook(stream));
                       return Task.FromResult(new {{moduleName}}ImportResultDto(
                           parsed.Errors.Count == 0 ? parsed.Requests.Count : 0,
                           parsed.Errors));
                   }

                   public async Task<{{moduleName}}ImportResultDto> ImportAsync(Stream stream, CancellationToken cancellationToken = default)
                   {
                       var parsed = ParseImportRows(workbookService.ReadWorkbook(stream));
                       if (parsed.Errors.Count > 0)
                       {
                           return new {{moduleName}}ImportResultDto(0, parsed.Errors);
                       }

                       foreach (var request in parsed.Requests)
                       {
                           await {{fieldName}}Repository.CreateAsync(request, cancellationToken);
                       }

                       return new {{moduleName}}ImportResultDto(parsed.Requests.Count, []);
                   }

                   public Task<{{moduleName}}ExportFileDto> ExportImportErrorsAsync(Stream stream, CancellationToken cancellationToken = default)
                   {
                       var rows = workbookService.ReadWorkbook(stream);
                       var parsed = ParseImportRows(rows);
                       var errorRows = new List<IReadOnlyList<string>>
                       {
                           new[] { {{importErrorHeaders}} }
                       };
                       foreach (var error in parsed.Errors)
                       {
                           var sourceRow = error.RowNumber - 1 >= 0 && error.RowNumber - 1 < rows.Count
                               ? rows[error.RowNumber - 1]
                               : Array.Empty<string>();
                           errorRows.Add(new[]
                           {
               {{errorRows}},
                               error.Message
                           });
                       }

                       return Task.FromResult(new {{moduleName}}ExportFileDto(
                           "mini-admin-{{fieldName}}-import-errors.xlsx",
                           "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                           workbookService.CreateWorkbook(errorRows)));
                   }

                   private static (IReadOnlyList<Save{{moduleName}}Request> Requests, IReadOnlyList<{{moduleName}}ImportErrorDto> Errors) ParseImportRows(
                       IReadOnlyList<IReadOnlyList<string>> rows)
                   {
                       var requests = new List<Save{{moduleName}}Request>();
                       var errors = new List<{{moduleName}}ImportErrorDto>();
                       if (rows.Count < 2)
                       {
                           errors.Add(new {{moduleName}}ImportErrorDto(1, string.Empty, "导入文件没有数据行."));
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

               {{requiredChecks}}
                           try
                           {
                               requests.Add(new Save{{moduleName}}Request(
               {{saveValues}}));
                           }
                           catch (Exception ex)
                           {
                               errors.Add(new {{moduleName}}ImportErrorDto(rowNumber, string.Empty, $"数据格式不正确：{ex.Message}"));
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
               """;
    }

    private static string RenderRepository(
        string moduleName,
        string modulePlural,
        IReadOnlyList<CodeGeneratorFieldConfigDto> fields,
        string tenantMode,
        string dataScopeMode,
        string? dataScopeField,
        bool enableImportExport,
        bool enableWorkflow)
    {
        var isTenantMode = IsTenantMode(tenantMode);
        var isDataScopeEnabled = IsDataScopeEnabled(dataScopeMode);
        var tenantUsing = isTenantMode
            ? "using MiniAdmin.Application.Contracts.MultiTenancy;" + Environment.NewLine
            : string.Empty;
        var dataScopeUsing = isDataScopeEnabled
            ? "using MiniAdmin.Application.Contracts.DataScopes;" + Environment.NewLine
            : string.Empty;
        var constructorDependencies = new List<string> { "MiniAdminDbContext dbContext" };
        if (isTenantMode)
        {
            constructorDependencies.Add("ICurrentTenant currentTenant");
        }

        if (isDataScopeEnabled)
        {
            constructorDependencies.Add("IDataScopeProvider dataScopeProvider");
        }

        var constructorParameters = string.Join(", ", constructorDependencies);
        var queryTenantFilter = isTenantMode
            ? Environment.NewLine + "        source = ApplyTenantFilter(source);" + Environment.NewLine
            : string.Empty;
        var queryDataScopeFilter = isDataScopeEnabled
            ? Environment.NewLine + "        source = await ApplyDataScopeAsync(source, query.CurrentUserName, cancellationToken);" + Environment.NewLine
            : string.Empty;
        var createTenantAssignment = isTenantMode
            ? Environment.NewLine + "        entity.TenantId = currentTenant.TenantId;"
            : string.Empty;
        var baseSetSource = isTenantMode
            ? "ApplyTenantFilter(dbContext.Set<" + moduleName + ">())"
            : "dbContext.Set<" + moduleName + ">()";
        var updateSignature = isDataScopeEnabled
            ? $"Guid id, Save{moduleName}Request request, string? currentUserName, CancellationToken cancellationToken = default"
            : $"Guid id, Save{moduleName}Request request, CancellationToken cancellationToken = default";
        var deleteSignature = isDataScopeEnabled
            ? "Guid id, string? currentUserName, CancellationToken cancellationToken = default"
            : "Guid id, CancellationToken cancellationToken = default";
        var updateSource = isDataScopeEnabled
            ? $"        var source = await ApplyDataScopeAsync({baseSetSource}, currentUserName, cancellationToken);" + Environment.NewLine +
              "        var entity = await source.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);"
            : $"        var entity = await {baseSetSource}.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);";
        var deleteSource = isDataScopeEnabled
            ? $"        var source = await ApplyDataScopeAsync({baseSetSource}, currentUserName, cancellationToken);" + Environment.NewLine +
              "        var entity = await source.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);"
            : $"        var entity = await {baseSetSource}.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);";
        var workflowGetSignature = isDataScopeEnabled
            ? $"Guid id, string? currentUserName, CancellationToken cancellationToken = default"
            : $"Guid id, CancellationToken cancellationToken = default";
        var workflowSetSignature = isDataScopeEnabled
            ? $"Guid id, string approvalStatus, string? workflowInstanceId, string? currentUserName, CancellationToken cancellationToken = default"
            : $"Guid id, string approvalStatus, string? workflowInstanceId, CancellationToken cancellationToken = default";
        var workflowGetSource = isDataScopeEnabled
            ? $"        var source = await ApplyDataScopeAsync({baseSetSource}.AsNoTracking(), currentUserName, cancellationToken);" + Environment.NewLine +
              "        var entity = await source.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);"
            : $"        var entity = await {baseSetSource}.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);";
        var workflowSetSource = isDataScopeEnabled
            ? $"        var source = await ApplyDataScopeAsync({baseSetSource}, currentUserName, cancellationToken);" + Environment.NewLine +
              "        var entity = await source.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);"
            : $"        var entity = await {baseSetSource}.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);";
        var tenantFilterMethod = isTenantMode
            ? $$"""

                   private IQueryable<{{moduleName}}> ApplyTenantFilter(IQueryable<{{moduleName}}> source)
                   {
                       return currentTenant.IsTenant
                           ? source.Where(x => x.TenantId == currentTenant.TenantId)
                           : source.Where(x => x.TenantId == null);
                   }
               """
            : string.Empty;
        var dataScopeFilterMethod = isDataScopeEnabled
            ? RenderDataScopeFilterMethod(
                moduleName,
                dataScopeMode,
                fields.SingleOrDefault(field =>
                    field.PropertyName.Equals(dataScopeField, StringComparison.OrdinalIgnoreCase)))
            : string.Empty;
        var assignments = string.Join(
            Environment.NewLine,
            fields
                .Where(field => field.CreateVisible || field.UpdateVisible)
                .Select(field => $"        entity.{field.PropertyName} = request.{field.PropertyName};"));
        var dtoValues = string.Join(
            "," + Environment.NewLine,
            new[] { "            entity.Id.ToString()" }
                .Concat(enableWorkflow
                    ? ["            entity.WorkflowInstanceId", "            entity.ApprovalStatus"]
                    : [])
                .Concat(fields.Select(field => $"            entity.{field.PropertyName}"))
                .Concat(["            entity.CreatedAt"]));
        var queryFilters = string.Join(
            Environment.NewLine,
            fields
                .Where(IsQueryEnabled)
                .Select(RenderQueryFilter)
                .Where(filter => !string.IsNullOrWhiteSpace(filter)));
        var exportMethod = enableImportExport
            ? $$"""

                   public async Task<IReadOnlyList<{{moduleName}}Dto>> GetExportListAsync({{moduleName}}ListQuery query, int limit = 10000, CancellationToken cancellationToken = default)
                   {
                       var take = Math.Clamp(limit, 1, 10000);
                       var source = dbContext.Set<{{moduleName}}>().AsNoTracking();
               {{queryTenantFilter}}
               {{queryDataScopeFilter}}
               {{queryFilters}}
                       return await source
                           .OrderByDescending(entity => entity.CreatedAt)
                           .Take(take)
                           .Select(entity => ToDto(entity))
                           .ToArrayAsync(cancellationToken);
                   }
               """
            : string.Empty;
        var workflowMethods = enableWorkflow
            ? $$"""

                   public async Task<{{moduleName}}Dto?> GetAsync({{workflowGetSignature}})
                   {
               {{workflowGetSource}}
                       return entity is null ? null : ToDto(entity);
                   }

                   public async Task<{{moduleName}}Dto?> SetWorkflowStateAsync({{workflowSetSignature}})
                   {
               {{workflowSetSource}}
                       if (entity is null)
                       {
                           return null;
                       }

                       entity.ApprovalStatus = approvalStatus;
                       entity.WorkflowInstanceId = workflowInstanceId;
                       await dbContext.SaveChangesAsync(cancellationToken);
                       return ToDto(entity);
                   }
               """
            : string.Empty;

        return $$"""
               using Microsoft.EntityFrameworkCore;
               using MiniAdmin.Application.Contracts.Common;
               {{dataScopeUsing}}{{tenantUsing}}using MiniAdmin.Application.Contracts.{{modulePlural}};
               using MiniAdmin.Domain.Entities;

               namespace MiniAdmin.Infrastructure.Persistence;

               public sealed class Ef{{moduleName}}Repository({{constructorParameters}}) : I{{moduleName}}Repository
               {
                   public async Task<PageResult<{{moduleName}}Dto>> GetListAsync({{moduleName}}ListQuery query, CancellationToken cancellationToken = default)
                   {
                       var page = Math.Max(query.Page, 1);
                       var pageSize = Math.Clamp(query.PageSize, 1, 100);
                       var source = dbContext.Set<{{moduleName}}>().AsNoTracking();
               {{queryTenantFilter}}
               {{queryDataScopeFilter}}
               {{queryFilters}}
                       var total = await source.CountAsync(cancellationToken);
                       var items = await source
                           .OrderByDescending(entity => entity.CreatedAt)
                           .Skip((page - 1) * pageSize)
                           .Take(pageSize)
                           .Select(entity => ToDto(entity))
                           .ToArrayAsync(cancellationToken);

                       return new PageResult<{{moduleName}}Dto>(items, total);
                   }
               {{exportMethod}}

                   public async Task<{{moduleName}}Dto> CreateAsync(Save{{moduleName}}Request request, CancellationToken cancellationToken = default)
                   {
                       var entity = new {{moduleName}} { Id = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow };
                       Apply(entity, request);
               {{createTenantAssignment}}
                       dbContext.Set<{{moduleName}}>().Add(entity);
                       await dbContext.SaveChangesAsync(cancellationToken);
                       return ToDto(entity);
                   }

                   public async Task<{{moduleName}}Dto?> UpdateAsync({{updateSignature}})
                   {
               {{updateSource}}
                       if (entity is null)
                       {
                           return null;
                       }

                       Apply(entity, request);
                       await dbContext.SaveChangesAsync(cancellationToken);
                       return ToDto(entity);
                   }

                   public async Task<bool> DeleteAsync({{deleteSignature}})
                   {
               {{deleteSource}}
                       if (entity is null)
                       {
                           return false;
                       }

                       dbContext.Set<{{moduleName}}>().Remove(entity);
                       await dbContext.SaveChangesAsync(cancellationToken);
                       return true;
                   }
               {{workflowMethods}}

                   private static void Apply({{moduleName}} entity, Save{{moduleName}}Request request)
                   {
               {{assignments}}
                   }

                   private static {{moduleName}}Dto ToDto({{moduleName}} entity)
                   {
                       return new {{moduleName}}Dto(
               {{dtoValues}});
                   }
               {{tenantFilterMethod}}
               {{dataScopeFilterMethod}}
               }
               """;
    }

    private static string RenderEntityConfiguration(
        string tableName,
        string moduleName,
        IReadOnlyList<CodeGeneratorFieldConfigDto> fields,
        string tenantMode,
        bool enableWorkflow)
    {
        var tenantConfiguration = IsTenantMode(tenantMode)
            ? """
                      entity.HasIndex(x => x.TenantId);
                      entity.Property(x => x.TenantId).HasColumnName("TenantId");
              """
            : string.Empty;
        var workflowConfiguration = enableWorkflow
            ? """
                      entity.Property(x => x.WorkflowInstanceId).HasColumnName("workflow_instance_id").HasMaxLength(36);
                      entity.Property(x => x.ApprovalStatus).HasColumnName("approval_status").HasMaxLength(32).IsRequired();
                      entity.HasIndex(x => x.WorkflowInstanceId);
              """
            : string.Empty;
        var propertyMappings = string.Join(
            Environment.NewLine,
            fields.Select(field =>
            {
                var builder = new StringBuilder();
                builder.Append($"        entity.Property(x => x.{field.PropertyName})");
                builder.Append($".HasColumnName(\"{field.ColumnName}\")");
                if (field.DotNetType == "string")
                {
                    builder.Append($".HasMaxLength({field.MaxLength ?? 256})");
                }
                else if (field.DotNetType == "decimal")
                {
                    builder.Append(".HasPrecision(18, 2)");
                }

                if (field.IsRequired)
                {
                    builder.Append(".IsRequired()");
                }

                builder.Append(';');
                return builder.ToString();
            }));
        var uniqueIndexes = string.Join(
            Environment.NewLine,
            fields
                .Where(field => field.IsUnique)
                .Select(field => $"        entity.HasIndex(x => x.{field.PropertyName}).IsUnique();"));

        return $$"""
               using Microsoft.EntityFrameworkCore;
               using Microsoft.EntityFrameworkCore.Metadata.Builders;
               using MiniAdmin.Domain.Entities;
               using MiniAdmin.Infrastructure.Persistence;

               namespace MiniAdmin.Infrastructure.Persistence.Generated;

               public sealed class {{moduleName}}EntityTypeConfiguration : IEntityTypeConfiguration<{{moduleName}}>
               {
                   public void Configure(EntityTypeBuilder<{{moduleName}}> entity)
                   {
                       entity.ToTable("{{tableName}}");
                       entity.HasKey(x => x.Id);
                       entity.Property(x => x.Id).HasColumnName("id");
               {{tenantConfiguration}}
               {{workflowConfiguration}}
               {{uniqueIndexes}}
               {{propertyMappings}}
                       entity.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
                   }
               }
               """;
    }

    private static string RenderDataScopeFilterMethod(
        string moduleName,
        string dataScopeMode,
        CodeGeneratorFieldConfigDto? dataScopeField)
    {
        var propertyName = dataScopeField is null
            ? "Id"
            : dataScopeField.PropertyName.Trim();
        var isNullable = dataScopeField is not null && GetEntityType(dataScopeField).EndsWith('?');
        var departmentAndChildrenCondition = isNullable
            ? $"x.{propertyName}.HasValue && dataScope.DepartmentIds.Contains(x.{propertyName}.Value)"
            : $"dataScope.DepartmentIds.Contains(x.{propertyName})";

        if (string.Equals(dataScopeMode, "Self", StringComparison.OrdinalIgnoreCase))
        {
            return $$"""

                   private async Task<IQueryable<{{moduleName}}> ApplyDataScopeAsync(
                       IQueryable<{{moduleName}}> source,
                       string? currentUserName,
                       CancellationToken cancellationToken)
                   {
                       var dataScope = await dataScopeProvider.GetAsync(currentUserName, cancellationToken);
                       if (dataScope.IsUnrestricted)
                       {
                           return source;
                       }

                       if (dataScope.IsDenied || dataScope.UserId is not Guid userId)
                       {
                           return source.Where(x => false);
                       }

                       return source.Where(x => x.{{propertyName}} == userId);
                   }
               """;
        }

        return $$"""

                   private async Task<IQueryable<{{moduleName}}> ApplyDataScopeAsync(
                       IQueryable<{{moduleName}}> source,
                       string? currentUserName,
                       CancellationToken cancellationToken)
                   {
                       var dataScope = await dataScopeProvider.GetAsync(currentUserName, cancellationToken);
                       if (dataScope.IsUnrestricted)
                       {
                           return source;
                       }

                       if (dataScope.IsDenied)
                       {
                           return source.Where(x => false);
                       }

                       if (dataScope.Level == DataScopeLevel.DepartmentAndChildren)
                       {
                           return source.Where(x => {{departmentAndChildrenCondition}});
                       }

                       if (dataScope.Level == DataScopeLevel.Department && dataScope.DepartmentId is Guid departmentId)
                       {
                           return source.Where(x => x.{{propertyName}} == departmentId);
                       }

                       return source.Where(x => false);
                   }
               """;
    }

    private static string RenderTransportEndpoints(
        string moduleName,
        string modulePlural,
        string routePath,
        string permissionPrefix)
    {
        var fieldName = char.ToLowerInvariant(moduleName[0]) + moduleName[1..];
        return $$"""
               using MiniAdmin.Api.CodeGenerators;
               using MiniAdmin.Application.Contracts.{{modulePlural}};
               using MiniAdmin.Shared;

               namespace MiniAdmin.Api.Generated;

               // File upload and download require transport-specific HTTP results and stay as a thin adapter.
               public sealed class {{moduleName}}TransportEndpoints : IGeneratedTransportEndpointDefinition
               {
                   public void MapEndpoints(IEndpointRouteBuilder endpoints)
                   {
                       endpoints.MapGet("{{routePath}}/export", async (
                           [AsParameters] {{moduleName}}ListQuery query,
                           I{{moduleName}}AppService {{fieldName}}AppService,
                           CancellationToken cancellationToken) =>
                       {
                           var file = await {{fieldName}}AppService.ExportAsync(query, cancellationToken);
                           return Results.File(file.Content, file.ContentType, file.FileName);
                       }).RequirePermission("{{permissionPrefix}}:export");

                       endpoints.MapGet("{{routePath}}/import-template", async (
                           I{{moduleName}}AppService {{fieldName}}AppService,
                           CancellationToken cancellationToken) =>
                       {
                           var file = await {{fieldName}}AppService.GetImportTemplateAsync(cancellationToken);
                           return Results.File(file.Content, file.ContentType, file.FileName);
                       }).RequirePermission("{{permissionPrefix}}:import");

                       endpoints.MapPost("{{routePath}}/import/preview", async (
                           IFormFile file,
                           I{{moduleName}}AppService {{fieldName}}AppService,
                           CancellationToken cancellationToken) =>
                       {
                           await using var stream = file.OpenReadStream();
                           var result = await {{fieldName}}AppService.PreviewImportAsync(stream, cancellationToken);
                           return Results.Ok(ApiResponse<{{moduleName}}ImportResultDto>.Ok(result));
                       }).DisableAntiforgery().RequirePermission("{{permissionPrefix}}:import");

                       endpoints.MapPost("{{routePath}}/import/error-report", async (
                           IFormFile file,
                           I{{moduleName}}AppService {{fieldName}}AppService,
                           CancellationToken cancellationToken) =>
                       {
                           await using var stream = file.OpenReadStream();
                           var report = await {{fieldName}}AppService.ExportImportErrorsAsync(stream, cancellationToken);
                           return Results.File(report.Content, report.ContentType, report.FileName);
                       }).DisableAntiforgery().RequirePermission("{{permissionPrefix}}:import");

                       endpoints.MapPost("{{routePath}}/import", async (
                           IFormFile file,
                           I{{moduleName}}AppService {{fieldName}}AppService,
                           CancellationToken cancellationToken) =>
                       {
                           await using var stream = file.OpenReadStream();
                           var result = await {{fieldName}}AppService.ImportAsync(stream, cancellationToken);
                           return Results.Ok(ApiResponse<{{moduleName}}ImportResultDto>.Ok(result));
                       }).DisableAntiforgery().RequirePermission("{{permissionPrefix}}:import");
                   }
               }
               """;
    }

    private static string RenderPageDefinitionProvider(
        string moduleName,
        string modulePlural,
        string businessName,
        string routePath,
        string routeSegment,
        string permissionPrefix,
        string? parentMenuId,
        bool enableImportExport,
        bool enableWorkflow)
    {
        var menuId = CreateDeterministicGuid(moduleName, "menu");
        var permissions = new List<(string Action, Guid Id)>
        {
            ("query", CreateDeterministicGuid(moduleName, "query")),
            ("create", CreateDeterministicGuid(moduleName, "create")),
            ("update", CreateDeterministicGuid(moduleName, "update")),
            ("delete", CreateDeterministicGuid(moduleName, "delete"))
        };
        if (enableImportExport)
        {
            permissions.Add(("import", CreateDeterministicGuid(moduleName, "import")));
            permissions.Add(("export", CreateDeterministicGuid(moduleName, "export")));
        }

        if (enableWorkflow)
        {
            permissions.Add(("submit-workflow", CreateDeterministicGuid(moduleName, "submit-workflow")));
            permissions.Add(("withdraw-workflow", CreateDeterministicGuid(moduleName, "withdraw-workflow")));
        }

        var permissionDefinitions = string.Join(
            "," + Environment.NewLine,
            permissions.Select(permission =>
                $"                Permission(\"{permission.Action}\", \"{permissionPrefix}:{permission.Action}\", \"{permission.Id}\")"));
        var parentExpression = string.IsNullOrWhiteSpace(parentMenuId)
            ? "null"
            : $"Guid.Parse(\"{parentMenuId.Trim()}\")";
        var resource = permissionPrefix.Replace(':', '.');

        return $$"""
               using MiniAdmin.Platform.Navigation;

               namespace MiniAdmin.Application.{{modulePlural}};

               public sealed class {{moduleName}}PageDefinitionProvider : IPageDefinitionProvider
               {
                   public IEnumerable<PageDefinition> GetPages()
                   {
                       yield return new PageDefinition(
                           Key: "{{resource}}",
                           ParentKey: null,
                           Path: "{{routePath}}",
                           Component: "/business/{{routeSegment}}/index",
                           Redirect: null,
                           Icon: "lucide:table-2",
                           Order: 100,
                           I18nKey: "page.generated.{{moduleName}}",
                           Title: new LocalizedText("{{EscapeCSharpString(businessName)}}", "{{EscapeCSharpString(businessName)}}"),
                           IsVisible: true,
                           Permissions:
                           [
               {{permissionDefinitions}}
                           ],
                           Id: Guid.Parse("{{menuId}}"),
                           ParentId: {{parentExpression}});
                   }

                   private static PermissionDefinition Permission(string action, string code, string id)
                   {
                       return new PermissionDefinition(
                           code,
                           "{{resource}}",
                           action,
                           $"permission.generated.{{moduleName}}.{action}",
                           new LocalizedText(code, code),
                           Guid.Parse(id));
                   }
               }
               """;
    }

    private static string RenderFrontendApi(
        string moduleName,
        string routeSegment,
        IReadOnlyList<CodeGeneratorFieldConfigDto> fields,
        bool enableImportExport,
        bool enableWorkflow)
    {
        var interfaceFields = string.Join(
            Environment.NewLine,
            fields.Select(field => $"  {char.ToLowerInvariant(field.PropertyName[0])}{field.PropertyName[1..]}: {field.TsType};"));
        var saveFields = string.Join(
            Environment.NewLine,
            fields
                .Where(field => field.CreateVisible || field.UpdateVisible)
                .Select(field => $"  {char.ToLowerInvariant(field.PropertyName[0])}{field.PropertyName[1..]}: {field.TsType};"));
        var typeName = $"{moduleName}Item";
        var importExportImports = enableImportExport
            ? """
               import { useAppConfig } from '@vben/hooks';
               import { preferences } from '@vben/preferences';
               import { useAccessStore } from '@vben/stores';

              """
            : string.Empty;
        var apiUrlDeclaration = enableImportExport
            ? "const { apiURL } = useAppConfig(import.meta.env, import.meta.env.PROD);" + Environment.NewLine
            : string.Empty;
        var importExportTypes = enableImportExport
            ? $$"""

               export interface {{moduleName}}ImportError {
                 field: string;
                 message: string;
                 rowNumber: number;
               }

               export interface {{moduleName}}ImportResult {
                 createdCount: number;
                 errors: {{moduleName}}ImportError[];
               }

               interface ApiEnvelope<T> {
                 code: number;
                 data: T;
                 message: string;
               }
               """
            : string.Empty;
        var importExportApi = enableImportExport
            ? $$"""

               export async function export{{moduleName}}Api(params: Record<string, unknown>) {
                 return download{{moduleName}}Workbook('/business/{{routeSegment}}/export', params);
               }

               export async function download{{moduleName}}ImportTemplateApi() {
                 return download{{moduleName}}Workbook('/business/{{routeSegment}}/import-template');
               }

               export async function import{{moduleName}}Api(file: File) {
                 return upload{{moduleName}}Workbook('/business/{{routeSegment}}/import', file);
               }

               export async function previewImport{{moduleName}}Api(file: File) {
                 return upload{{moduleName}}Workbook('/business/{{routeSegment}}/import/preview', file);
               }

               export async function download{{moduleName}}ImportErrorReportApi(file: File) {
                 const accessStore = useAccessStore();
                 const formData = new FormData();
                 formData.append('file', file);
                 const response = await fetch(
                   `${apiURL.replace(/\/$/, '')}/business/{{routeSegment}}/import/error-report`,
                   {
                     body: formData,
                     headers: {
                       'Accept-Language': preferences.app.locale,
                       ...(accessStore.accessToken
                         ? { Authorization: `Bearer ${accessStore.accessToken}` }
                         : {}),
                     },
                     method: 'POST',
                   },
                 );

                 if (!response.ok) {
                   throw new Error(`Download failed: ${response.status}`);
                 }

                 return response.blob();
               }

               async function upload{{moduleName}}Workbook(path: string, file: File) {
                 const accessStore = useAccessStore();
                 const formData = new FormData();
                 formData.append('file', file);
                 const response = await fetch(`${apiURL.replace(/\/$/, '')}${path}`, {
                   body: formData,
                   headers: {
                     'Accept-Language': preferences.app.locale,
                     ...(accessStore.accessToken
                       ? { Authorization: `Bearer ${accessStore.accessToken}` }
                       : {}),
                   },
                   method: 'POST',
                 });

                 if (!response.ok) {
                   throw new Error(`Import failed: ${response.status}`);
                 }

                 const result = (await response.json()) as ApiEnvelope<{{moduleName}}ImportResult>;
                 if (result.code !== 0) {
                   throw new Error(result.message || 'Import failed');
                 }

                 return result.data;
               }

               async function download{{moduleName}}Workbook(path: string, params?: Record<string, unknown>) {
                 const accessStore = useAccessStore();
                 const url = new URL(`${apiURL.replace(/\/$/, '')}${path}`, window.location.origin);

                 Object.entries(params ?? {}).forEach(([key, value]) => {
                   if (value !== undefined && value !== '') {
                     url.searchParams.set(key, String(value));
                   }
                 });

                 const response = await fetch(url, {
                   headers: {
                     'Accept-Language': preferences.app.locale,
                     ...(accessStore.accessToken
                       ? { Authorization: `Bearer ${accessStore.accessToken}` }
                       : {}),
                   },
                 });

                 if (!response.ok) {
                   throw new Error(`Download failed: ${response.status}`);
                 }

                 return response.blob();
               }
               """
            : string.Empty;
        var workflowInterfaceFields = enableWorkflow
            ? """
                 approvalStatus: string;
                 workflowInstanceId?: null | string;
              """
            : string.Empty;
        var workflowTypes = enableWorkflow
            ? $$"""

               export interface Submit{{moduleName}}WorkflowParams {
                 comment?: null | string;
               }

               export interface Withdraw{{moduleName}}WorkflowParams {
                 comment?: null | string;
               }
               """
            : string.Empty;
        var workflowApi = enableWorkflow
            ? $$"""

               export async function submit{{moduleName}}WorkflowApi(
                 id: string,
                 data: Submit{{moduleName}}WorkflowParams = {},
               ) {
                 return requestClient.post<{{typeName}}>(
                   `/business/{{routeSegment}}/${id}/submit-workflow`,
                   data,
                   rawResponse,
                 );
               }

               export async function withdraw{{moduleName}}WorkflowApi(
                 id: string,
                 data: Withdraw{{moduleName}}WorkflowParams = {},
               ) {
                 return requestClient.post<{{typeName}}>(
                   `/business/{{routeSegment}}/${id}/withdraw-workflow`,
                   data,
                   rawResponse,
                 );
               }
               """
            : string.Empty;

        return $$"""
               {{importExportImports}}
               import { requestClient } from '#/api/request';

               {{apiUrlDeclaration}}
               const rawResponse = { responseReturn: 'body' as const };

               export interface {{typeName}} {
                 id: string;
               {{workflowInterfaceFields}}
               {{interfaceFields}}
                 createdAt: string;
               }

               export interface {{moduleName}}ListResult {
                 items: {{typeName}}[];
                 total: number;
               }

               export interface Save{{moduleName}}Params {
               {{saveFields}}
               }
               {{workflowTypes}}
               {{importExportTypes}}

               export async function get{{moduleName}}ListApi(params: Record<string, unknown>) {
                 return requestClient.get<{{moduleName}}ListResult>('/business/{{routeSegment}}/list', {
                   params,
                   ...rawResponse,
                 });
               }

               export async function create{{moduleName}}Api(data: Save{{moduleName}}Params) {
                 return requestClient.post<{{typeName}}>('/business/{{routeSegment}}', data, rawResponse);
               }

               export async function update{{moduleName}}Api(id: string, data: Save{{moduleName}}Params) {
                 return requestClient.put<{{typeName}}>(`/business/{{routeSegment}}/${id}`, data, rawResponse);
               }

               export async function delete{{moduleName}}Api(id: string) {
                 return requestClient.delete<boolean>(`/business/{{routeSegment}}/${id}`, rawResponse);
               }
               {{workflowApi}}
               {{importExportApi}}
               """;
    }

    private static string RenderFrontendPage(
        string moduleName,
        string businessName,
        string routeSegment,
        string permissionPrefix,
        IReadOnlyList<CodeGeneratorFieldConfigDto> fields,
        bool enableImportExport,
        bool enableWorkflow)
    {
        var editableFields = fields
            .Where(field => field.CreateVisible || field.UpdateVisible)
            .ToArray();
        var columns = string.Join(
            Environment.NewLine,
            fields
                .Where(field => field.ListVisible)
                .Select(field => $"  {{ dataIndex: '{ToCamelCase(field.PropertyName)}', title: '{field.DisplayName}' }},")
                .DefaultIfEmpty("  { dataIndex: 'id', title: 'ID' },")
                .Concat(enableWorkflow
                    ? ["  { dataIndex: 'approvalStatus', title: '审批状态', width: 120 },"]
                    : [])
                .Concat([
                    "  { dataIndex: 'createdAt', title: '创建时间', width: 180 },",
                    enableWorkflow
                        ? "  { dataIndex: 'action', title: '操作', width: 260 },"
                        : "  { dataIndex: 'action', title: '操作', width: 150 },"
                ]));
        var formInterfaceFields = string.Join(
            Environment.NewLine,
            editableFields.Select(field => $"  {ToCamelCase(field.PropertyName)}: {field.TsType};"));
        var formDefaults = string.Join(
            Environment.NewLine,
            editableFields.Select(field => $"  {ToCamelCase(field.PropertyName)}: {GetTsDefaultValue(field)},"));
        var resetAssignments = string.Join(
            Environment.NewLine,
            editableFields.Select(field => $"  formState.{ToCamelCase(field.PropertyName)} = {GetTsDefaultValue(field)};"));
        var editAssignments = string.Join(
            Environment.NewLine,
            editableFields.Select(field => $"  formState.{ToCamelCase(field.PropertyName)} = item.{ToCamelCase(field.PropertyName)};"));
        var payloadFields = string.Join(
            Environment.NewLine,
            editableFields.Select(field => $"      {ToCamelCase(field.PropertyName)}: formState.{ToCamelCase(field.PropertyName)},"));
        var formItems = string.Join(
            Environment.NewLine,
            editableFields.Select(RenderFrontendFormItem));
        var queryFields = fields
            .Where(IsQueryEnabled)
            .ToArray();
        var queryStateFields = string.Join(
            Environment.NewLine,
            queryFields.SelectMany(RenderFrontendQueryStateFields));
        var queryDefaults = string.Join(
            Environment.NewLine,
            queryFields.SelectMany(RenderFrontendQueryDefaults));
        var queryParams = string.Join(
            Environment.NewLine,
            queryFields.SelectMany(RenderFrontendQueryParams));
        var queryResetAssignments = string.Join(
            Environment.NewLine,
            queryFields.SelectMany(RenderFrontendQueryResetAssignments));
        var queryControls = string.Join(
            Environment.NewLine,
            queryFields.Select(RenderFrontendQueryControl));
        var importExportApiImports = enableImportExport
            ? $$"""
                 download{{moduleName}}ImportErrorReportApi,
                 download{{moduleName}}ImportTemplateApi,
                 export{{moduleName}}Api,
                 import{{moduleName}}Api,
                 previewImport{{moduleName}}Api,
                 type {{moduleName}}ImportResult,
               """
            : string.Empty;
        var workflowApiImports = enableWorkflow
            ? $$"""
                 submit{{moduleName}}WorkflowApi,
                 withdraw{{moduleName}}WorkflowApi,
               """
            : string.Empty;
        var importExportRefs = enableImportExport
            ? $$"""
               const exportingItems = ref(false);
               const importingItems = ref(false);
               const previewingImport = ref(false);
               const downloadingTemplate = ref(false);
               const downloadingErrorReport = ref(false);
               const importPreviewModalOpen = ref(false);
               const importInputRef = ref<HTMLInputElement>();
               const importPreviewFile = ref<File>();
               const importPreviewResult = ref<{{moduleName}}ImportResult>();
               """
            : string.Empty;
        var workflowRefs = enableWorkflow
            ? """
               const submittingWorkflowId = ref('');
               const withdrawingWorkflowId = ref('');
              """
            : string.Empty;
        var importExportAccess = enableImportExport
            ? $$"""
               const canImport = computed(() => hasAccessByCodes(['{{permissionPrefix}}:import']));
               const canExport = computed(() => hasAccessByCodes(['{{permissionPrefix}}:export']));
               """
            : string.Empty;
        var workflowAccess = enableWorkflow
            ? $$"""
               const canSubmitWorkflow = computed(() => hasAccessByCodes(['{{permissionPrefix}}:submit-workflow']));
               const canWithdrawWorkflow = computed(() => hasAccessByCodes(['{{permissionPrefix}}:withdraw-workflow']));
               """
            : string.Empty;
        var importExportFunctions = enableImportExport
            ? $$"""

               async function exportItems() {
                 exportingItems.value = true;
                 try {
                   const blob = await export{{moduleName}}Api({
                     keyword: query.keyword || undefined,
               {{queryParams}}
                     page: query.page,
                     pageSize: query.pageSize,
                   });
                   downloadBlob(blob, 'mini-admin-{{routeSegment}}.xlsx');
                   message.success('{{businessName}}已导出');
                 } finally {
                   exportingItems.value = false;
                 }
               }

               async function downloadImportTemplate() {
                 downloadingTemplate.value = true;
                 try {
                   const blob = await download{{moduleName}}ImportTemplateApi();
                   downloadBlob(blob, 'mini-admin-{{routeSegment}}-import-template.xlsx');
                 } finally {
                   downloadingTemplate.value = false;
                 }
               }

               function openImportFilePicker() {
                 importInputRef.value?.click();
               }

               async function handleImportFile(event: Event) {
                 const input = event.target as HTMLInputElement;
                 const file = input.files?.[0];
                 input.value = '';
                 if (!file) {
                   return;
                 }

                 if (!file.name.toLowerCase().endsWith('.xlsx')) {
                   message.warning('请上传 .xlsx 文件');
                   return;
                 }

                 previewingImport.value = true;
                 try {
                   importPreviewFile.value = file;
                   importPreviewResult.value = await previewImport{{moduleName}}Api(file);
                   importPreviewModalOpen.value = true;
                 } finally {
                   previewingImport.value = false;
                 }
               }

               async function confirmImportItems() {
                 if (!importPreviewFile.value || !importPreviewResult.value) {
                   return;
                 }

                 if (importPreviewResult.value.errors.length > 0) {
                   message.warning('请先修正失败行后再导入');
                   return;
                 }

                 importingItems.value = true;
                 try {
                   const result = await import{{moduleName}}Api(importPreviewFile.value);
                   message.success(`导入成功 ${result.createdCount} 条数据`);
                   importPreviewModalOpen.value = false;
                   importPreviewFile.value = undefined;
                   importPreviewResult.value = undefined;
                   await loadData();
                 } finally {
                   importingItems.value = false;
                 }
               }

               async function downloadImportErrorReport() {
                 if (!importPreviewFile.value) {
                   return;
                 }

                 downloadingErrorReport.value = true;
                 try {
                   const blob = await download{{moduleName}}ImportErrorReportApi(importPreviewFile.value);
                   downloadBlob(blob, 'mini-admin-{{routeSegment}}-import-errors.xlsx');
                 } finally {
                   downloadingErrorReport.value = false;
                 }
               }

               function downloadBlob(blob: Blob, fileName: string) {
                 const url = URL.createObjectURL(blob);
                 const anchor = document.createElement('a');
                 anchor.href = url;
                 anchor.download = fileName;
                 anchor.click();
                 URL.revokeObjectURL(url);
               }
               """
            : string.Empty;
        var workflowFunctions = enableWorkflow
            ? $$"""

               function getApprovalStatusColor(status: string) {
                 const colors: Record<string, string> = {
                   Approved: 'green',
                   Draft: 'default',
                   Pending: 'blue',
                   Rejected: 'red',
                   Withdrawn: 'orange',
                 };
                 return colors[status] ?? 'default';
               }

               function getApprovalStatusText(status: string) {
                 const texts: Record<string, string> = {
                   Approved: '已通过',
                   Draft: '草稿',
                   Pending: '审批中',
                   Rejected: '已驳回',
                   Withdrawn: '已撤回',
                 };
                 return texts[status] ?? status;
               }

               function canSubmitRecord(record: {{moduleName}}Item) {
                 return !['Approved', 'Pending'].includes(record.approvalStatus);
               }

               function canWithdrawRecord(record: {{moduleName}}Item) {
                 return record.approvalStatus === 'Pending' && Boolean(record.workflowInstanceId);
               }

               async function submitWorkflow(record: {{moduleName}}Item) {
                 submittingWorkflowId.value = record.id;
                 try {
                   await submit{{moduleName}}WorkflowApi(record.id, {});
                   message.success('{{businessName}}已提交审批');
                   await loadData();
                 } finally {
                   submittingWorkflowId.value = '';
                 }
               }

               async function withdrawWorkflow(record: {{moduleName}}Item) {
                 withdrawingWorkflowId.value = record.id;
                 try {
                   await withdraw{{moduleName}}WorkflowApi(record.id, {});
                   message.success('{{businessName}}已撤回审批');
                   await loadData();
                 } finally {
                   withdrawingWorkflowId.value = '';
                 }
               }

               function openWorkflowCenter() {
                 void router.push('/workflow/center');
               }
               """
            : string.Empty;
        var workflowRouterImport = enableWorkflow
            ? "import { useRouter } from 'vue-router';" + Environment.NewLine
            : string.Empty;
        var workflowRouterDeclaration = enableWorkflow
            ? "const router = useRouter();" + Environment.NewLine
            : string.Empty;
        var workflowStatusCell = enableWorkflow
            ? """
                        <template v-if="column.dataIndex === 'approvalStatus'">
                          <Tag :color="getApprovalStatusColor(record.approvalStatus)">
                            {{ getApprovalStatusText(record.approvalStatus) }}
                          </Tag>
                        </template>
              """
            : string.Empty;
        var workflowActionButtons = enableWorkflow
            ? """
                            <Button
                              v-if="canSubmitWorkflow && canSubmitRecord(record)"
                              size="small"
                              type="link"
                              :loading="submittingWorkflowId === record.id"
                              @click="submitWorkflow(record)"
                            >
                              提交
                            </Button>
                            <Popconfirm title="确认撤回这条审批？" @confirm="withdrawWorkflow(record)">
                              <Button
                                v-if="canWithdrawWorkflow && canWithdrawRecord(record)"
                                size="small"
                                type="link"
                                :loading="withdrawingWorkflowId === record.id"
                              >
                                撤回
                              </Button>
                            </Popconfirm>
                            <Button
                              v-if="record.workflowInstanceId"
                              size="small"
                              type="link"
                              @click="openWorkflowCenter"
                            >
                              流程
                            </Button>
              """
            : string.Empty;
        var importExportButtons = enableImportExport
            ? """
                             <Button v-if="canExport" :loading="exportingItems" @click="exportItems">导出</Button>
                             <Button v-if="canImport" :loading="downloadingTemplate" @click="downloadImportTemplate">下载模板</Button>
                             <Button v-if="canImport" :loading="previewingImport" @click="openImportFilePicker">导入</Button>
                             <input ref="importInputRef" accept=".xlsx" class="hidden-import-input" type="file" @change="handleImportFile" />
              """
            : string.Empty;
        var importExportModal = enableImportExport
            ? """

                   <Modal
                     v-model:open="importPreviewModalOpen"
                     :confirm-loading="importingItems"
                     :ok-button-props="{ disabled: Boolean(importPreviewResult?.errors.length) }"
                     title="导入预检"
                     @ok="confirmImportItems"
                   >
                     <div class="import-preview-summary">
                       <Tag color="green">预计成功 {{ importPreviewResult?.createdCount ?? 0 }} 条</Tag>
                       <Tag :color="importPreviewResult?.errors.length ? 'red' : 'green'">
                         失败 {{ importPreviewResult?.errors.length ?? 0 }} 条
                       </Tag>
                     </div>
                     <Table
                       v-if="importPreviewResult?.errors.length"
                       class="mt-3"
                       row-key="rowNumber"
                       size="small"
                       :columns="[
                         { dataIndex: 'rowNumber', title: '行号', width: 80 },
                         { dataIndex: 'field', title: '字段', width: 140 },
                         { dataIndex: 'message', title: '失败原因' },
                       ]"
                       :data-source="importPreviewResult.errors"
                       :pagination="{ pageSize: 5 }"
                     />
                     <Button
                       v-if="importPreviewResult?.errors.length"
                       class="mt-3"
                       :loading="downloadingErrorReport"
                       @click="downloadImportErrorReport"
                     >
                       下载失败明细
                     </Button>
                   </Modal>
              """
            : string.Empty;
        var importExportStyles = enableImportExport
            ? """

               .hidden-import-input {
                 display: none;
               }

               .import-preview-summary {
                 display: flex;
                 flex-wrap: wrap;
                 gap: 8px;
               }
              """
            : string.Empty;

        return $$"""
               <script setup lang="ts">
               import type { TablePaginationConfig } from 'ant-design-vue';

               import { computed, onMounted, reactive, ref } from 'vue';
               {{workflowRouterImport}}

               import { useAccess } from '@vben/access';
               import { Page } from '@vben/common-ui';
               import {
                 Button,
                 DatePicker,
                 Form,
                 FormItem,
                 Input,
                 InputNumber,
                 Modal,
                 Popconfirm,
                 Select,
                 Space,
                 Switch,
                 Table,
                 Tag,
                 Textarea,
                 message,
               } from 'ant-design-vue';
               import {
                 create{{moduleName}}Api,
                 delete{{moduleName}}Api,
               {{importExportApiImports}}
                 get{{moduleName}}ListApi,
               {{workflowApiImports}}
                 type {{moduleName}}Item,
                 update{{moduleName}}Api,
               } from '#/api/business/{{routeSegment}}';

               interface {{moduleName}}FormState {
               {{formInterfaceFields}}
               }

               const loading = ref(false);
               const saving = ref(false);
               {{importExportRefs}}
               {{workflowRefs}}
               const modalOpen = ref(false);
               const editingItem = ref<{{moduleName}}Item>();
               const items = ref<{{moduleName}}Item[]>([]);
               const total = ref(0);
               const query = reactive({
                 keyword: '',
               {{queryDefaults}}
                 page: 1,
                 pageSize: 10,
               });
               const formState = reactive<{{moduleName}}FormState>({
               {{formDefaults}}
               });
               const { hasAccessByCodes } = useAccess();
               {{workflowRouterDeclaration}}
               const columns = [
               {{columns}}
               ];

               const pagination = computed<TablePaginationConfig>(() => ({
                 current: query.page,
                 pageSize: query.pageSize,
                 showSizeChanger: true,
                 showTotal: (count) => `共 ${count} 条记录`,
                 total: total.value,
               }));
               const modalTitle = computed(() => editingItem.value ? '编辑{{businessName}}' : '新增{{businessName}}');
               const canCreate = computed(() => hasAccessByCodes(['{{permissionPrefix}}:create']));
               const canUpdate = computed(() => hasAccessByCodes(['{{permissionPrefix}}:update']));
               const canDelete = computed(() => hasAccessByCodes(['{{permissionPrefix}}:delete']));
               {{importExportAccess}}
               {{workflowAccess}}

               function getDictionaryOptions(options: { dictionaryCode: string }) {
                 return [
                   {
                     label: options.dictionaryCode,
                     value: '',
                   },
                 ];
               }

               async function loadData() {
                 loading.value = true;
                 try {
                   const result = await get{{moduleName}}ListApi({
                     keyword: query.keyword || undefined,
               {{queryParams}}
                     page: query.page,
                     pageSize: query.pageSize,
                   });
                   items.value = result.items;
                   total.value = result.total;
                 } finally {
                   loading.value = false;
                 }
               }

               function handleSearch() {
                 query.page = 1;
                 void loadData();
               }

               function handleReset() {
                 query.keyword = '';
               {{queryResetAssignments}}
                 query.page = 1;
                 void loadData();
               }

               function handleTableChange(nextPagination: TablePaginationConfig) {
                 query.page = nextPagination.current ?? 1;
                 query.pageSize = nextPagination.pageSize ?? 10;
                 void loadData();
               }

               function resetForm() {
                 editingItem.value = undefined;
               {{resetAssignments}}
               }

               function openCreateModal() {
                 resetForm();
                 modalOpen.value = true;
               }

               function openEditModal(item: {{moduleName}}Item) {
                 editingItem.value = item;
               {{editAssignments}}
                 modalOpen.value = true;
               }

               async function submitItem() {
                 saving.value = true;
                 try {
                   const payload = {
               {{payloadFields}}
                   };

                   if (editingItem.value) {
                     await update{{moduleName}}Api(editingItem.value.id, payload);
                     message.success('{{businessName}}已更新');
                   } else {
                     await create{{moduleName}}Api(payload);
                     message.success('{{businessName}}已新增');
                   }

                   modalOpen.value = false;
                   await loadData();
                 } finally {
                   saving.value = false;
                 }
               }

               async function removeItem(item: {{moduleName}}Item) {
                 const deleted = await delete{{moduleName}}Api(item.id);
                 if (deleted) {
                   message.success('{{businessName}}已删除');
                 }
                 await loadData();
               }
               {{importExportFunctions}}
               {{workflowFunctions}}

               onMounted(loadData);
               </script>

               <template>
                 <Page auto-content-height>
                   <div class="generated-page">
                     <div class="query-bar">
                       <Space wrap>
                         <span class="query-label">关键词</span>
                         <Input
                           v-model:value="query.keyword"
                           allow-clear
                           class="query-input"
                           placeholder="请输入"
                         />
               {{queryControls}}
                       </Space>
                       <Space>
                         <Button @click="handleReset">重置</Button>
                         <Button type="primary" @click="handleSearch">搜索</Button>
                       </Space>
                     </div>
                     <div class="toolbar">
                       <h3>{{businessName}}列表</h3>
                       <Space>
               {{importExportButtons}}
                         <Button v-if="canCreate" type="primary" @click="openCreateModal">新增</Button>
                       </Space>
                     </div>
                     <Table
                       row-key="id"
                       size="small"
                       bordered
                       :columns="columns"
                       :data-source="items"
                       :loading="loading"
                       :pagination="pagination"
                       @change="handleTableChange"
                     >
                     <template #bodyCell="{ column, record }">
               {{workflowStatusCell}}
                        <template v-if="column.dataIndex === 'action'">
                          <Space>
               {{workflowActionButtons}}
                            <Button v-if="canUpdate" type="link" size="small" @click="openEditModal(record)">编辑</Button>
                             <Popconfirm title="确认删除这条数据？" @confirm="removeItem(record)">
                               <Button v-if="canDelete" danger type="link" size="small">删除</Button>
                             </Popconfirm>
                           </Space>
                         </template>
                       </template>
                     </Table>
                   </div>

                   <Modal
                     v-model:open="modalOpen"
                     :confirm-loading="saving"
                     :title="modalTitle"
                     @ok="submitItem"
                   >
                     <Form :model="formState" layout="vertical">
               {{formItems}}
                     </Form>
                   </Modal>
               {{importExportModal}}
                 </Page>
               </template>

               <style scoped>
               .generated-page {
                 min-height: calc(100vh - 150px);
                 border-radius: 4px;
                 background: hsl(var(--background));
                 padding: 10px;
               }

               .query-bar {
                 display: flex;
                 align-items: center;
                 justify-content: space-between;
                 gap: 10px;
                 padding-bottom: 10px;
               }

               .query-label {
                 color: hsl(var(--muted-foreground));
                 font-size: 13px;
               }

               .query-input {
                 width: 220px;
               }

               .toolbar {
                 display: flex;
                 align-items: center;
                 justify-content: space-between;
                 padding-bottom: 10px;
               }

               .toolbar h3 {
                 margin: 0;
                 font-size: 15px;
                 font-weight: 600;
               }
               {{importExportStyles}}
               </style>
               """;
    }

    private static string GetEntityType(CodeGeneratorFieldConfigDto field)
    {
        return field.IsRequired ? field.DotNetType : field.DotNetType.EndsWith('?') ? field.DotNetType : $"{field.DotNetType}?";
    }

    private static string ToCamelCase(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? value
            : char.ToLowerInvariant(value[0]) + value[1..];
    }

    private static string GetTsDefaultValue(CodeGeneratorFieldConfigDto field)
    {
        if (!string.IsNullOrWhiteSpace(field.DefaultValue))
        {
            return field.TsType switch
            {
                "boolean" => field.DefaultValue.Equals("true", StringComparison.OrdinalIgnoreCase) ? "true" : "false",
                "number" => field.DefaultValue,
                _ => $"'{EscapeJsString(field.DefaultValue)}'"
            };
        }

        return field.TsType switch
        {
            "boolean" => "false",
            "number" => "0",
            _ => "''"
        };
    }

    private static string GetImportTemplateValue(CodeGeneratorFieldConfigDto field)
    {
        if (!string.IsNullOrWhiteSpace(field.DefaultValue))
        {
            return $"\"{EscapeCSharpString(field.DefaultValue)}\"";
        }

        return field.DotNetType switch
        {
            "int" or "long" => "\"1\"",
            "decimal" => "\"1.00\"",
            "bool" => "\"启用\"",
            "Guid" => "\"00000000-0000-0000-0000-000000000000\"",
            "DateTimeOffset" => "\"2026-01-01 00:00:00\"",
            _ => $"\"示例{EscapeCSharpString(field.DisplayName)}\""
        };
    }

    private static string RenderFrontendFormItem(CodeGeneratorFieldConfigDto field)
    {
        var propertyName = ToCamelCase(field.PropertyName);
        var dictionaryCode = field.DictionaryCode ?? string.Empty;
        if (field.ControlType.Equals("Select", StringComparison.OrdinalIgnoreCase))
        {
            return $$"""
                         <FormItem label="{{field.DisplayName}}">
                           <Select
                             v-model:value="formState.{{propertyName}}"
                             :options="getDictionaryOptions({ dictionaryCode: '{{EscapeJsString(dictionaryCode)}}' })"
                           />
                         </FormItem>
                   """;
        }

        if (field.ControlType.Equals("DatePicker", StringComparison.OrdinalIgnoreCase))
        {
            return $$"""
                         <FormItem label="{{field.DisplayName}}">
                           <DatePicker v-model:value="formState.{{propertyName}}" class="form-control" />
                         </FormItem>
                   """;
        }

        if (field.ControlType.Equals("Textarea", StringComparison.OrdinalIgnoreCase))
        {
            return $$"""
                         <FormItem label="{{field.DisplayName}}">
                           <Textarea v-model:value="formState.{{propertyName}}" allow-clear />
                         </FormItem>
                   """;
        }

        if (field.TsType == "boolean" || field.ControlType.Equals("Switch", StringComparison.OrdinalIgnoreCase))
        {
            return $$"""
                         <FormItem label="{{field.DisplayName}}">
                           <Switch v-model:checked="formState.{{propertyName}}" />
                         </FormItem>
                   """;
        }

        if (field.TsType == "number" || field.ControlType.Equals("InputNumber", StringComparison.OrdinalIgnoreCase))
        {
            return $$"""
                         <FormItem label="{{field.DisplayName}}">
                           <InputNumber v-model:value="formState.{{propertyName}}" class="form-control" />
                         </FormItem>
                   """;
        }

        return $$"""
                     <FormItem label="{{field.DisplayName}}">
                       <Input v-model:value="formState.{{propertyName}}" allow-clear />
                     </FormItem>
               """;
    }

    private static IEnumerable<string> RenderFrontendQueryStateFields(CodeGeneratorFieldConfigDto field)
    {
        if (IsRangeQuery(field))
        {
            var propertyName = ToCamelCase(field.PropertyName);
            yield return $"  {propertyName}Begin: {GetTsDefaultValue(field)},";
            yield return $"  {propertyName}End: {GetTsDefaultValue(field)},";
            yield break;
        }

        yield return $"  {ToCamelCase(field.PropertyName)}: {GetTsDefaultValue(field)},";
    }

    private static IEnumerable<string> RenderFrontendQueryDefaults(CodeGeneratorFieldConfigDto field)
    {
        return RenderFrontendQueryStateFields(field);
    }

    private static IEnumerable<string> RenderFrontendQueryParams(CodeGeneratorFieldConfigDto field)
    {
        var propertyName = ToCamelCase(field.PropertyName);
        if (IsRangeQuery(field))
        {
            yield return $"      {propertyName}Begin: query.{propertyName}Begin || undefined,";
            yield return $"      {propertyName}End: query.{propertyName}End || undefined,";
            yield break;
        }

        yield return $"      {propertyName}: query.{propertyName} || undefined,";
    }

    private static IEnumerable<string> RenderFrontendQueryResetAssignments(CodeGeneratorFieldConfigDto field)
    {
        var propertyName = ToCamelCase(field.PropertyName);
        if (IsRangeQuery(field))
        {
            yield return $"  query.{propertyName}Begin = {GetTsDefaultValue(field)};";
            yield return $"  query.{propertyName}End = {GetTsDefaultValue(field)};";
            yield break;
        }

        yield return $"  query.{propertyName} = {GetTsDefaultValue(field)};";
    }

    private static string RenderFrontendQueryControl(CodeGeneratorFieldConfigDto field)
    {
        var propertyName = ToCamelCase(field.PropertyName);
        var dictionaryCode = field.DictionaryCode ?? string.Empty;
        if (IsRangeQuery(field))
        {
            return $$"""
                         <span class="query-label">{{field.DisplayName}}</span>
                         <DatePicker v-model:value="query.{{propertyName}}Begin" class="query-input" />
                         <DatePicker v-model:value="query.{{propertyName}}End" class="query-input" />
                   """;
        }

        if (field.ControlType.Equals("Select", StringComparison.OrdinalIgnoreCase))
        {
            return $$"""
                         <span class="query-label">{{field.DisplayName}}</span>
                         <Select
                           v-model:value="query.{{propertyName}}"
                           allow-clear
                           class="query-input"
                           :options="getDictionaryOptions({ dictionaryCode: '{{EscapeJsString(dictionaryCode)}}' })"
                         />
                   """;
        }

        if (field.TsType == "boolean" || field.ControlType.Equals("Switch", StringComparison.OrdinalIgnoreCase))
        {
            return $$"""
                         <span class="query-label">{{field.DisplayName}}</span>
                         <Switch v-model:checked="query.{{propertyName}}" />
                   """;
        }

        if (field.TsType == "number" || field.ControlType.Equals("InputNumber", StringComparison.OrdinalIgnoreCase))
        {
            return $$"""
                         <span class="query-label">{{field.DisplayName}}</span>
                         <InputNumber v-model:value="query.{{propertyName}}" class="query-input" />
                   """;
        }

        return $$"""
                     <span class="query-label">{{field.DisplayName}}</span>
                     <Input
                       v-model:value="query.{{propertyName}}"
                       allow-clear
                       class="query-input"
                       placeholder="请输入{{field.DisplayName}}"
                     />
               """;
    }

    public static Guid CreateDeterministicGuid(string moduleName, string purpose)
    {
        var input = Encoding.UTF8.GetBytes($"mini-admin:code-generator:{moduleName}:{purpose}");
        var hash = MD5.HashData(input);
        return new Guid(hash);
    }

    private static bool IsTenantMode(string tenantMode)
    {
        return string.Equals(tenantMode, "Tenant", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDataScopeEnabled(string dataScopeMode)
    {
        return !string.Equals(dataScopeMode, "None", StringComparison.OrdinalIgnoreCase);
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

    private static string GetDtoType(CodeGeneratorFieldConfigDto field)
    {
        return field.IsRequired ? field.DotNetType : field.DotNetType.EndsWith('?') ? field.DotNetType : $"{field.DotNetType}?";
    }

    private static string GetNullableDtoType(CodeGeneratorFieldConfigDto field)
    {
        return field.DotNetType.EndsWith('?') ? field.DotNetType : $"{field.DotNetType}?";
    }

    private static string GetInitializer(CodeGeneratorFieldConfigDto field)
    {
        if (!string.IsNullOrWhiteSpace(field.DefaultValue))
        {
            if (field.DotNetType == "string")
            {
                return $" = \"{EscapeCSharpString(field.DefaultValue)}\";";
            }

            if (field.DotNetType == "bool")
            {
                return field.DefaultValue.Equals("true", StringComparison.OrdinalIgnoreCase)
                    ? " = true;"
                    : " = false;";
            }

            if (field.DotNetType is "int" or "long" or "decimal" or "double" or "float")
            {
                return $" = {field.DefaultValue};";
            }
        }

        if (!field.IsRequired || field.DotNetType != "string")
        {
            return string.Empty;
        }

        return " = string.Empty;";
    }

    private static bool IsStringField(CodeGeneratorFieldConfigDto field)
    {
        return field.DotNetType.TrimEnd('?').Equals("string", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEqualsQuery(CodeGeneratorFieldConfigDto field)
    {
        return field.QueryMode.Equals("Equals", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRangeQuery(CodeGeneratorFieldConfigDto field)
    {
        return field.QueryMode.Equals("Range", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsQueryEnabled(CodeGeneratorFieldConfigDto field)
    {
        return field.QueryVisible && !field.QueryMode.Equals("None", StringComparison.OrdinalIgnoreCase);
    }

    private static string EscapeCSharpString(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string EscapeJsString(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "\\'", StringComparison.Ordinal);
    }
}
