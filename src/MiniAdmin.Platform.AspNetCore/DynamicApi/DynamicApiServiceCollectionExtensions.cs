using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MiniAdmin.Platform.AspNetCore.Authorization;
using MiniAdmin.Platform.Authorization;

namespace MiniAdmin.Platform.AspNetCore.DynamicApi;

public static class DynamicApiServiceCollectionExtensions
{
    public static IMvcBuilder AddMiniAdminDynamicApis(
        this IServiceCollection services,
        params Assembly[] applicationAssemblies)
    {
        services.TryAddScoped<IAuthorizationDecisionService, ClaimsAuthorizationDecisionService>();

        var builder = services.AddControllers(options =>
            options.Conventions.Add(new DynamicApiApplicationModelConvention()));
        builder.ConfigureApplicationPartManager(manager =>
        {
            foreach (var assembly in applicationAssemblies.Distinct())
            {
                if (manager.ApplicationParts.OfType<AssemblyPart>().All(part => part.Assembly != assembly))
                {
                    manager.ApplicationParts.Add(new AssemblyPart(assembly));
                }
            }

            manager.FeatureProviders.Add(new DynamicApiControllerFeatureProvider());
        });

        return builder;
    }
}
