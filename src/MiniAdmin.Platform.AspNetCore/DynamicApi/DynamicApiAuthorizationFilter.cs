using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using MiniAdmin.Platform.Authorization;
using MiniAdmin.Platform.AspNetCore.Authorization;
using MiniAdmin.Platform.DynamicApi;

namespace MiniAdmin.Platform.AspNetCore.DynamicApi;

internal sealed class DynamicApiAuthorizationFilter(
    IAuthorizationDecisionService decisionService,
    DynamicApiMethodAttribute metadata) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (metadata.AllowAnonymous)
        {
            return;
        }

        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var decision = await decisionService.AuthorizeAsync(
            new AuthorizationRequest(
                context.HttpContext.User,
                metadata.Permission,
                metadata.Resource,
                metadata.Action,
                AuthorizationRequestAttributeFactory.Create(context.HttpContext)),
            context.HttpContext.RequestAborted);
        context.HttpContext.Items[typeof(AuthorizationDecision)] = decision;

        if (!decision.Allowed)
        {
            context.Result = new ForbidResult();
        }
    }
}

internal sealed class DynamicApiAuthorizationFilterFactory(
    DynamicApiMethodAttribute metadata) : IFilterFactory, IOrderedFilter
{
    public bool IsReusable => false;

    public int Order => int.MinValue + 100;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        return ActivatorUtilities.CreateInstance<DynamicApiAuthorizationFilter>(
            serviceProvider,
            metadata);
    }
}
