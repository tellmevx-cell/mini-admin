using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using MiniAdmin.Platform.DynamicApi;

namespace MiniAdmin.Platform.AspNetCore.DynamicApi;

internal sealed class DynamicApiApplicationModelConvention : IControllerModelConvention
{
    public void Apply(ControllerModel controller)
    {
        var api = controller.ControllerType.GetCustomAttribute<DynamicApiAttribute>();
        if (api is null)
        {
            return;
        }

        controller.ControllerName = api.Name ?? RemoveAppServiceSuffix(controller.ControllerType.Name);
        controller.ApiExplorer.IsVisible = true;
        controller.Selectors.Clear();
        controller.Selectors.Add(new SelectorModel
        {
            AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(api.Route))
        });

        for (var index = controller.Actions.Count - 1; index >= 0; index--)
        {
            var action = controller.Actions[index];
            var method = action.ActionMethod.GetCustomAttribute<DynamicApiMethodAttribute>();
            if (method is null)
            {
                controller.Actions.RemoveAt(index);
                continue;
            }

            ConfigureAction(action, api, method);
        }
    }

    private static void ConfigureAction(
        ActionModel action,
        DynamicApiAttribute api,
        DynamicApiMethodAttribute method)
    {
        action.ActionName = method.OperationId ?? action.ActionMethod.Name;
        action.ApiExplorer.IsVisible = true;
        action.Filters.Add(new DynamicApiAuthorizationFilterFactory(method));
        action.Filters.Add(new DynamicApiExceptionFilter());
        action.Selectors.Clear();

        var selector = new SelectorModel
        {
            AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(method.Route))
        };
        selector.ActionConstraints.Add(new HttpMethodActionConstraint([method.HttpMethod]));
        selector.EndpointMetadata.Add(new HttpMethodMetadata([method.HttpMethod]));
        if (!string.IsNullOrWhiteSpace(method.OperationId))
        {
            selector.EndpointMetadata.Add(new EndpointNameMetadata(method.OperationId));
        }

        if (!string.IsNullOrWhiteSpace(api.Tag))
        {
            selector.EndpointMetadata.Add(new TagsAttribute(api.Tag));
        }

        if (!string.IsNullOrWhiteSpace(method.Summary))
        {
            selector.EndpointMetadata.Add(new EndpointSummaryAttribute(method.Summary));
        }

        selector.EndpointMetadata.Add(method);
        action.Selectors.Add(selector);

        ConfigureParameters(action, method);
    }

    private static void ConfigureParameters(ActionModel action, DynamicApiMethodAttribute method)
    {
        var routeTemplate = method.Route;
        var bodyAssigned = false;
        foreach (var parameter in action.Parameters)
        {
            if (parameter.ParameterInfo.ParameterType == typeof(CancellationToken))
            {
                parameter.BindingInfo = new BindingInfo { BindingSource = BindingSource.Special };
                continue;
            }

            var declared = parameter.ParameterInfo.GetCustomAttribute<DynamicApiParameterAttribute>();
            var source = declared?.Source ?? DynamicApiParameterSource.Auto;
            var bindingSource = source switch
            {
                DynamicApiParameterSource.Route => BindingSource.Path,
                DynamicApiParameterSource.Query => BindingSource.Query,
                DynamicApiParameterSource.Body => BindingSource.Body,
                DynamicApiParameterSource.Header => BindingSource.Header,
                DynamicApiParameterSource.Services => BindingSource.Services,
                _ => ResolveAutomaticSource(parameter, method, routeTemplate, ref bodyAssigned)
            };

            parameter.BindingInfo = new BindingInfo
            {
                BinderModelName = declared?.Name,
                BindingSource = bindingSource
            };

            if (bindingSource == BindingSource.Body)
            {
                bodyAssigned = true;
            }
        }
    }

    private static BindingSource ResolveAutomaticSource(
        ParameterModel parameter,
        DynamicApiMethodAttribute method,
        string routeTemplate,
        ref bool bodyAssigned)
    {
        var name = parameter.ParameterName ?? parameter.ParameterInfo.Name ?? string.Empty;
        if (routeTemplate.Contains($"{{{name}", StringComparison.OrdinalIgnoreCase))
        {
            return BindingSource.Path;
        }

        var type = Nullable.GetUnderlyingType(parameter.ParameterInfo.ParameterType) ??
            parameter.ParameterInfo.ParameterType;
        if (IsSimpleType(type) || method.HttpMethod is "GET" or "DELETE")
        {
            return BindingSource.Query;
        }

        if (!bodyAssigned)
        {
            bodyAssigned = true;
            return BindingSource.Body;
        }

        return BindingSource.Query;
    }

    private static bool IsSimpleType(Type type)
    {
        return type.IsPrimitive ||
            type.IsEnum ||
            type == typeof(string) ||
            type == typeof(decimal) ||
            type == typeof(Guid) ||
            type == typeof(DateOnly) ||
            type == typeof(TimeOnly) ||
            type == typeof(DateTime) ||
            type == typeof(DateTimeOffset) ||
            type == typeof(TimeSpan);
    }

    private static string RemoveAppServiceSuffix(string name)
    {
        const string suffix = "AppService";
        return name.EndsWith(suffix, StringComparison.Ordinal)
            ? name[..^suffix.Length]
            : name;
    }
}
