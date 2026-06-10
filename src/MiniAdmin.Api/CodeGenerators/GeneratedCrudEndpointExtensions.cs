using System.Reflection;
using MiniAdmin.Application.Contracts.CodeGenerators;

namespace MiniAdmin.Api.CodeGenerators;

public interface IGeneratedCrudEndpointDefinition
{
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}

public static class GeneratedCrudEndpointExtensions
{
    public static IServiceCollection AddGeneratedCrudServices(this IServiceCollection services)
    {
        foreach (var assembly in GetMiniAdminAssemblies())
        {
            RegisterGeneratedServices<IGeneratedCrudAppService>(services, assembly);
            RegisterGeneratedServices<IGeneratedCrudRepository>(services, assembly);
        }

        return services;
    }

    public static IEndpointRouteBuilder MapGeneratedCrudEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var endpointDefinitions = typeof(Program).Assembly
            .GetTypes()
            .Where(type =>
                !type.IsAbstract &&
                typeof(IGeneratedCrudEndpointDefinition).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        foreach (var endpointDefinitionType in endpointDefinitions)
        {
            var endpointDefinition = (IGeneratedCrudEndpointDefinition)ActivatorUtilities.CreateInstance(
                endpoints.ServiceProvider,
                endpointDefinitionType);
            endpointDefinition.MapEndpoints(endpoints);
        }

        return endpoints;
    }

    private static void RegisterGeneratedServices<TMarker>(
        IServiceCollection services,
        Assembly assembly)
    {
        var markerType = typeof(TMarker);
        var implementations = assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && markerType.IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

        foreach (var implementation in implementations)
        {
            var serviceInterfaces = implementation
                .GetInterfaces()
                .Where(type =>
                    type != typeof(IGeneratedCrudAppService) &&
                    type != typeof(IGeneratedCrudRepository))
                .ToArray();

            foreach (var serviceInterface in serviceInterfaces)
            {
                services.AddScoped(serviceInterface, implementation);
            }
        }
    }

    private static IReadOnlyList<Assembly> GetMiniAdminAssemblies()
    {
        return AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(assembly => assembly.GetName().Name?.StartsWith("MiniAdmin.", StringComparison.Ordinal) == true)
            .ToArray();
    }
}
