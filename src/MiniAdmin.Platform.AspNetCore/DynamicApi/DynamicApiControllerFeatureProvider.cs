using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;
using MiniAdmin.Platform.DynamicApi;

namespace MiniAdmin.Platform.AspNetCore.DynamicApi;

internal sealed class DynamicApiControllerFeatureProvider : ControllerFeatureProvider
{
    protected override bool IsController(TypeInfo typeInfo)
    {
        return typeInfo.GetCustomAttribute<DynamicApiAttribute>() is not null ||
            base.IsController(typeInfo);
    }
}
