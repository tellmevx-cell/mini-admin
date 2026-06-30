using System.Text;
using System.Text.Json;
using System.Security.Claims;
using MiniAdmin.Api.CodeGenerators;
using MiniAdmin.Application.AppBranding;
using MiniAdmin.Application.Alerts;
using MiniAdmin.Application.AuditLogs;
using MiniAdmin.Application.Auth;
using MiniAdmin.Application.Contracts.AppBranding;
using MiniAdmin.Application.Contracts.AuditLogs;
using MiniAdmin.Application.Contracts.Alerts;
using MiniAdmin.Application.Contracts.Auth;
using MiniAdmin.Application.Contracts.Caching;
using MiniAdmin.Application.Contracts.CodeGenerators;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.Departments;
using MiniAdmin.Application.Contracts.Dictionaries;
using MiniAdmin.Application.Contracts.Files;
using MiniAdmin.Application.Contracts.Menus;
using MiniAdmin.Application.Contracts.MultiTenancy;
using MiniAdmin.Application.Contracts.Notices;
using MiniAdmin.Application.Contracts.OnlineUsers;
using MiniAdmin.Application.Contracts.Parameters;
using MiniAdmin.Application.Contracts.PermissionDiagnostics;
using MiniAdmin.Application.Contracts.Positions;
using MiniAdmin.Application.Contracts.ProjectRuntimes;
using MiniAdmin.Application.Contracts.Roles;
using MiniAdmin.Application.Contracts.ScheduledJobs;
using MiniAdmin.Application.Contracts.Security;
using MiniAdmin.Application.Contracts.SystemMonitor;
using MiniAdmin.Application.Contracts.TenantPackages;
using MiniAdmin.Application.Contracts.Tenants;
using MiniAdmin.Application.Contracts.UserNotifications;
using MiniAdmin.Application.Contracts.Users;
using MiniAdmin.Application.Contracts.Workflows;
using MiniAdmin.Application.CodeGenerators;
using MiniAdmin.Application.Departments;
using MiniAdmin.Application.Dictionaries;
using MiniAdmin.Application.Files;
using MiniAdmin.Application.Menus;
using MiniAdmin.Application.Notices;
using MiniAdmin.Application.OnlineUsers;
using MiniAdmin.Application.Parameters;
using MiniAdmin.Application.PermissionDiagnostics;
using MiniAdmin.Application.Positions;
using MiniAdmin.Application.Roles;
using MiniAdmin.Application.ScheduledJobs;
using MiniAdmin.Application.Security;
using MiniAdmin.Application.TenantPackages;
using MiniAdmin.Application.Tenants;
using MiniAdmin.Application.Users;
using MiniAdmin.Application.UserNotifications;
using MiniAdmin.Application.Workflows;
using MiniAdmin.Infrastructure.Auth;
using MiniAdmin.Infrastructure.MultiTenancy;
using MiniAdmin.Infrastructure.Persistence;
using MiniAdmin.Infrastructure.ProjectRuntimes;
using MiniAdmin.Infrastructure.SystemMonitor;
using MiniAdmin.Domain.Shared.MultiTenancy;
using MiniAdmin.Shared;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json.Serialization;
using static MiniAdmin.Api.Endpoints.EndpointHelpers;

namespace MiniAdmin.Api.Endpoints;

public static class SystemManagementEndpointExtensions
{
    public static IEndpointRouteBuilder MapSystemManagementEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/system/menu/tree", async (
            IMenuAppService menuAppService,
            CancellationToken cancellationToken) =>
        {
            var menus = await menuAppService.GetManagementTreeAsync(cancellationToken);

            return Results.Ok(ApiResponse<IReadOnlyList<MenuTreeNodeDto>>.Ok(menus));
        })
        .RequireAnyPermission("system:menu:query", "system:role:assign");

        app.MapGet("/system/menu/list", async (
            IMenuAppService menuAppService,
            CancellationToken cancellationToken) =>
        {
            var menus = await menuAppService.GetManagementListAsync(cancellationToken);

            return Results.Ok(ApiResponse<IReadOnlyList<MenuManagementItemDto>>.Ok(menus));
        })
        .RequirePermission("system:menu:query");

        app.MapPost("/system/menu", async (
            SaveMenuRequest request,
            IMenuAppService menuAppService,
            CancellationToken cancellationToken) =>
        {
            var menu = await menuAppService.CreateAsync(request, cancellationToken);

            return Results.Ok(ApiResponse<MenuManagementItemDto>.Ok(menu));
        })
        .RequirePermission("system:menu:create");

        app.MapPut("/system/menu/{id:guid}", async (
            Guid id,
            SaveMenuRequest request,
            IMenuAppService menuAppService,
            CancellationToken cancellationToken) =>
        {
            var menu = await menuAppService.UpdateAsync(id, request, cancellationToken);

            return menu is null
                ? Results.NotFound(ApiResponse<MenuManagementItemDto?>.Fail("Menu not found."))
                : Results.Ok(ApiResponse<MenuManagementItemDto>.Ok(menu));
        })
        .RequirePermission("system:menu:update");

        app.MapDelete("/system/menu/{id:guid}", async (
            Guid id,
            IMenuAppService menuAppService,
            CancellationToken cancellationToken) =>
        {
            var deleted = await menuAppService.DeleteAsync(id, cancellationToken);

            return Results.Ok(ApiResponse<bool>.Ok(deleted));
        })
        .RequirePermission("system:menu:delete");

        app.MapGet("/system/department/list", async (
            IDepartmentAppService departmentAppService,
            CancellationToken cancellationToken) =>
        {
            var departments = await departmentAppService.GetListAsync(cancellationToken);

            return Results.Ok(ApiResponse<IReadOnlyList<DepartmentItemDto>>.Ok(departments));
        })
        .RequirePermission("system:department:query");

        app.MapPost("/system/department", async (
            SaveDepartmentRequest request,
            IDepartmentAppService departmentAppService,
            CancellationToken cancellationToken) =>
        {
            var department = await departmentAppService.CreateAsync(request, cancellationToken);

            return Results.Ok(ApiResponse<DepartmentItemDto>.Ok(department));
        })
        .RequirePermission("system:department:create");

        app.MapPut("/system/department/{id:guid}", async (
            Guid id,
            SaveDepartmentRequest request,
            IDepartmentAppService departmentAppService,
            CancellationToken cancellationToken) =>
        {
            var department = await departmentAppService.UpdateAsync(id, request, cancellationToken);

            return department is null
                ? Results.NotFound(ApiResponse<DepartmentItemDto?>.Fail("Department not found."))
                : Results.Ok(ApiResponse<DepartmentItemDto>.Ok(department));
        })
        .RequirePermission("system:department:update");

        app.MapDelete("/system/department/{id:guid}", async (
            Guid id,
            IDepartmentAppService departmentAppService,
            CancellationToken cancellationToken) =>
        {
            var deleted = await departmentAppService.DeleteAsync(id, cancellationToken);

            return Results.Ok(ApiResponse<bool>.Ok(deleted));
        })
        .RequirePermission("system:department:delete");

        app.MapGet("/system/position/list", async (
            int? page,
            int? pageSize,
            string? code,
            string? name,
            IPositionAppService positionAppService,
            CancellationToken cancellationToken) =>
        {
            var query = new PositionListQuery(
                Page: page ?? 1,
                PageSize: pageSize ?? 20,
                Code: code,
                Name: name);
            var positions = await positionAppService.GetListAsync(query, cancellationToken);

            return Results.Ok(ApiResponse<object>.Ok(positions));
        })
        .RequirePermission("system:position:query");

        app.MapGet("/system/position/export", async (
            int? page,
            int? pageSize,
            string? code,
            string? name,
            IPositionAppService positionAppService,
            CancellationToken cancellationToken) =>
        {
            var query = new PositionListQuery(
                Page: page ?? 1,
                PageSize: pageSize ?? 20,
                Code: code,
                Name: name);
            var file = await positionAppService.ExportAsync(query, cancellationToken);

            return Results.File(file.Content, file.ContentType, file.FileName);
        })
        .RequirePermission("system:position:export");

        app.MapGet("/system/position/import-template", async (
            IPositionAppService positionAppService,
            CancellationToken cancellationToken) =>
        {
            var file = await positionAppService.GetImportTemplateAsync(cancellationToken);

            return Results.File(file.Content, file.ContentType, file.FileName);
        })
        .RequirePermission("system:position:import");

        app.MapPost("/system/position/import/preview", async (
            IFormFile file,
            IPositionAppService positionAppService,
            CancellationToken cancellationToken) =>
        {
            if (file.Length == 0)
            {
                return Results.BadRequest(ApiResponse<PositionImportResultDto?>.Fail("导入文件不能为空."));
            }

            await using var stream = file.OpenReadStream();
            var result = await positionAppService.PreviewImportAsync(stream, cancellationToken);

            return Results.Ok(ApiResponse<PositionImportResultDto>.Ok(result));
        })
        .DisableAntiforgery()
        .RequirePermission("system:position:import");

        app.MapPost("/system/position/import/error-report", async (
            IFormFile file,
            IPositionAppService positionAppService,
            CancellationToken cancellationToken) =>
        {
            if (file.Length == 0)
            {
                return Results.BadRequest(ApiResponse<PositionImportResultDto?>.Fail("导入文件不能为空."));
            }

            await using var stream = file.OpenReadStream();
            var report = await positionAppService.ExportImportErrorsAsync(stream, cancellationToken);

            return Results.File(report.Content, report.ContentType, report.FileName);
        })
        .DisableAntiforgery()
        .RequirePermission("system:position:import");

        app.MapPost("/system/position/import", async (
            IFormFile file,
            IPositionAppService positionAppService,
            CancellationToken cancellationToken) =>
        {
            if (file.Length == 0)
            {
                return Results.BadRequest(ApiResponse<PositionImportResultDto?>.Fail("导入文件不能为空."));
            }

            await using var stream = file.OpenReadStream();
            var result = await positionAppService.ImportAsync(stream, cancellationToken);

            return Results.Ok(ApiResponse<PositionImportResultDto>.Ok(result));
        })
        .DisableAntiforgery()
        .RequirePermission("system:position:import");

        app.MapPost("/system/position", async (
            SavePositionRequest request,
            IPositionAppService positionAppService,
            CancellationToken cancellationToken) =>
        {
            var position = await positionAppService.CreateAsync(request, cancellationToken);

            return Results.Ok(ApiResponse<PositionDto>.Ok(position));
        })
        .RequirePermission("system:position:create");

        app.MapPut("/system/position/{id:guid}", async (
            Guid id,
            SavePositionRequest request,
            IPositionAppService positionAppService,
            CancellationToken cancellationToken) =>
        {
            var position = await positionAppService.UpdateAsync(id, request, cancellationToken);

            return position is null
                ? Results.NotFound(ApiResponse<PositionDto?>.Fail("Position not found."))
                : Results.Ok(ApiResponse<PositionDto>.Ok(position));
        })
        .RequirePermission("system:position:update");

        app.MapDelete("/system/position/{id:guid}", async (
            Guid id,
            IPositionAppService positionAppService,
            CancellationToken cancellationToken) =>
        {
            var deleted = await positionAppService.DeleteAsync(id, cancellationToken);

            return Results.Ok(ApiResponse<bool>.Ok(deleted));
        })
        .RequirePermission("system:position:delete");

        app.MapGet("/system/dictionary/list", async (
            IDictionaryAppService dictionaryAppService,
            CancellationToken cancellationToken) =>
        {
            var dictionaries = await dictionaryAppService.GetListAsync(cancellationToken);

            return Results.Ok(ApiResponse<IReadOnlyList<DictionaryTypeDto>>.Ok(dictionaries));
        })
        .RequirePermission("system:dictionary:query");

        app.MapPost("/system/dictionary/type", async (
            SaveDictionaryTypeRequest request,
            IDictionaryAppService dictionaryAppService,
            CancellationToken cancellationToken) =>
        {
            var dictionaryType = await dictionaryAppService.CreateTypeAsync(request, cancellationToken);

            return Results.Ok(ApiResponse<DictionaryTypeDto>.Ok(dictionaryType));
        })
        .RequirePermission("system:dictionary:create");

        app.MapPut("/system/dictionary/type/{id:guid}", async (
            Guid id,
            SaveDictionaryTypeRequest request,
            IDictionaryAppService dictionaryAppService,
            CancellationToken cancellationToken) =>
        {
            var dictionaryType = await dictionaryAppService.UpdateTypeAsync(id, request, cancellationToken);

            return dictionaryType is null
                ? Results.NotFound(ApiResponse<DictionaryTypeDto?>.Fail("Dictionary type not found."))
                : Results.Ok(ApiResponse<DictionaryTypeDto>.Ok(dictionaryType));
        })
        .RequirePermission("system:dictionary:update");

        app.MapDelete("/system/dictionary/type/{id:guid}", async (
            Guid id,
            IDictionaryAppService dictionaryAppService,
            CancellationToken cancellationToken) =>
        {
            var deleted = await dictionaryAppService.DeleteTypeAsync(id, cancellationToken);

            return Results.Ok(ApiResponse<bool>.Ok(deleted));
        })
        .RequirePermission("system:dictionary:delete");

        app.MapPost("/system/dictionary/item", async (
            SaveDictionaryItemRequest request,
            IDictionaryAppService dictionaryAppService,
            CancellationToken cancellationToken) =>
        {
            var dictionaryItem = await dictionaryAppService.CreateItemAsync(request, cancellationToken);

            return Results.Ok(ApiResponse<DictionaryItemDto>.Ok(dictionaryItem));
        })
        .RequirePermission("system:dictionary:create");

        app.MapPut("/system/dictionary/item/{id:guid}", async (
            Guid id,
            SaveDictionaryItemRequest request,
            IDictionaryAppService dictionaryAppService,
            CancellationToken cancellationToken) =>
        {
            var dictionaryItem = await dictionaryAppService.UpdateItemAsync(id, request, cancellationToken);

            return dictionaryItem is null
                ? Results.NotFound(ApiResponse<DictionaryItemDto?>.Fail("Dictionary item not found."))
                : Results.Ok(ApiResponse<DictionaryItemDto>.Ok(dictionaryItem));
        })
        .RequirePermission("system:dictionary:update");

        app.MapDelete("/system/dictionary/item/{id:guid}", async (
            Guid id,
            IDictionaryAppService dictionaryAppService,
            CancellationToken cancellationToken) =>
        {
            var deleted = await dictionaryAppService.DeleteItemAsync(id, cancellationToken);

            return Results.Ok(ApiResponse<bool>.Ok(deleted));
        })
        .RequirePermission("system:dictionary:delete");

        app.MapGet("/system/parameter/list", async (
            int? page,
            int? pageSize,
            string? key,
            string? name,
            string? group,
            ISystemParameterAppService systemParameterAppService,
            CancellationToken cancellationToken) =>
        {
            var query = new SystemParameterListQuery(
                Page: page ?? 1,
                PageSize: pageSize ?? 20,
                Key: key,
                Name: name,
                Group: group);
            var parameters = await systemParameterAppService.GetListAsync(query, cancellationToken);

            return Results.Ok(ApiResponse<object>.Ok(parameters));
        })
        .RequirePermission("system:parameter:query");

        app.MapPost("/system/parameter", async (
            SaveSystemParameterRequest request,
            ISystemParameterAppService systemParameterAppService,
            CancellationToken cancellationToken) =>
        {
            var parameter = await systemParameterAppService.CreateAsync(request, cancellationToken);

            return Results.Ok(ApiResponse<SystemParameterDto>.Ok(parameter));
        })
        .RequirePermission("system:parameter:create");

        app.MapPut("/system/parameter/{id:guid}", async (
            Guid id,
            SaveSystemParameterRequest request,
            ISystemParameterAppService systemParameterAppService,
            CancellationToken cancellationToken) =>
        {
            var parameter = await systemParameterAppService.UpdateAsync(id, request, cancellationToken);

            return parameter is null
                ? Results.NotFound(ApiResponse<SystemParameterDto?>.Fail("System parameter not found."))
                : Results.Ok(ApiResponse<SystemParameterDto>.Ok(parameter));
        })
        .RequirePermission("system:parameter:update");

        app.MapDelete("/system/parameter/{id:guid}", async (
            Guid id,
            ISystemParameterAppService systemParameterAppService,
            CancellationToken cancellationToken) =>
        {
            var deleted = await systemParameterAppService.DeleteAsync(id, cancellationToken);

            return Results.Ok(ApiResponse<bool>.Ok(deleted));
        })
        .RequirePermission("system:parameter:delete");

        app.MapGet("/system/notice/list", async (
            int? page,
            int? pageSize,
            string? title,
            string? type,
            bool? isPublished,
            INoticeAppService noticeAppService,
            CancellationToken cancellationToken) =>
        {
            var query = new NoticeListQuery(
                Page: page ?? 1,
                PageSize: pageSize ?? 20,
                Title: title,
                Type: type,
                IsPublished: isPublished);
            var notices = await noticeAppService.GetListAsync(query, cancellationToken);

            return Results.Ok(ApiResponse<object>.Ok(notices));
        })
        .RequirePermission("system:notice:query");

        app.MapPost("/system/notice", async (
            SaveNoticeRequest request,
            INoticeAppService noticeAppService,
            CancellationToken cancellationToken) =>
        {
            var notice = await noticeAppService.CreateAsync(request, cancellationToken);

            return Results.Ok(ApiResponse<NoticeDto>.Ok(notice));
        })
        .RequirePermission("system:notice:create");

        app.MapPut("/system/notice/{id:guid}", async (
            Guid id,
            SaveNoticeRequest request,
            INoticeAppService noticeAppService,
            CancellationToken cancellationToken) =>
        {
            var notice = await noticeAppService.UpdateAsync(id, request, cancellationToken);

            return notice is null
                ? Results.NotFound(ApiResponse<NoticeDto?>.Fail("Notice not found."))
                : Results.Ok(ApiResponse<NoticeDto>.Ok(notice));
        })
        .RequirePermission("system:notice:update");

        app.MapDelete("/system/notice/{id:guid}", async (
            Guid id,
            INoticeAppService noticeAppService,
            CancellationToken cancellationToken) =>
        {
            var deleted = await noticeAppService.DeleteAsync(id, cancellationToken);

            return Results.Ok(ApiResponse<bool>.Ok(deleted));
        })
        .RequirePermission("system:notice:delete");

        return app;
    }
}
