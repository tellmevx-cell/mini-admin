using System.Security.Claims;
using MiniAdmin.Application.Contracts.Security;

namespace MiniAdmin.Api.Security;

public sealed class HttpContextCurrentUserContext(
    IHttpContextAccessor httpContextAccessor) : ICurrentUserContext
{
    private ClaimsPrincipal Principal => httpContextAccessor.HttpContext?.User
        ?? throw new InvalidOperationException("当前请求上下文不可用。");

    public bool IsAuthenticated => Principal.Identity?.IsAuthenticated == true;

    public Guid UserId => Guid.TryParse(
        Principal.FindFirstValue(ClaimTypes.NameIdentifier),
        out var userId)
            ? userId
            : throw new InvalidOperationException("当前用户标识无效。");

    public string UserName => Principal.Identity?.Name
        ?? Principal.FindFirstValue(ClaimTypes.Name)
        ?? throw new InvalidOperationException("当前用户名不存在。");

    public Guid? TenantId => Guid.TryParse(Principal.FindFirstValue("tenant_id"), out var tenantId)
        ? tenantId
        : null;

    public string? TenantCode => Principal.FindFirstValue("tenant_code");
}
