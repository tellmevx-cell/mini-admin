using System.Net;
using System.Collections.Immutable;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.IdentityModel.Tokens;
using MiniAdmin.Application.Contracts.OpenPlatform;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace MiniAdmin.Api.OpenPlatform;

public static class OpenPlatformEndpointExtensions
{
    public static IEndpointRouteBuilder MapOpenPlatformEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/connect/session", (Delegate)EstablishSessionAsync)
            .RequireAuthorization()
            .WithTags("开放平台")
            .WithSummary("使用当前登录身份建立短期 OIDC 授权会话");

        app.MapDelete("/connect/session", (Delegate)ClearSessionAsync)
            .WithTags("开放平台")
            .WithSummary("清除 OIDC 授权会话");

        app.MapMethods("/connect/authorize", ["GET", "POST"], (Delegate)AuthorizeAsync)
            .WithTags("开放平台")
            .ExcludeFromDescription();

        app.MapPost("/connect/token", (Delegate)ExchangeAsync)
            .WithTags("开放平台")
            .ExcludeFromDescription();

        app.MapMethods("/connect/userinfo", ["GET", "POST"], (Delegate)UserInfoAsync)
            .WithTags("开放平台")
            .ExcludeFromDescription();

        return app;
    }

    private static async Task<IResult> EstablishSessionAsync(
        HttpContext context,
        IOpenPlatformUserRepository userRepository)
    {
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Results.Unauthorized();
        }

        var user = await userRepository.FindByIdAsync(userId, context.RequestAborted);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var identity = CreateCookieIdentity(user);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(15);
        await context.SignInAsync(
            OpenPlatformConstants.CookieScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                AllowRefresh = false,
                ExpiresUtc = expiresAt,
                IsPersistent = false,
                IssuedUtc = DateTimeOffset.UtcNow
            });
        return Results.Ok(new { expiresAt });
    }

    private static async Task<IResult> ClearSessionAsync(HttpContext context)
    {
        await context.SignOutAsync(OpenPlatformConstants.CookieScheme);
        return Results.NoContent();
    }

    private static async Task<IResult> AuthorizeAsync(
        HttpContext context,
        IAntiforgery antiforgery,
        IOpenPlatformUserRepository userRepository,
        IOpenIddictApplicationManager applicationManager,
        IOpenIddictAuthorizationManager authorizationManager,
        IOpenIddictScopeManager scopeManager)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("无法读取 OpenID Connect 授权请求。");
        var cookie = await context.AuthenticateAsync(OpenPlatformConstants.CookieScheme);
        if (!cookie.Succeeded ||
            !Guid.TryParse(cookie.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            if (request.HasPromptValue(PromptValues.None))
            {
                return ProtocolForbid(Errors.LoginRequired, "用户尚未登录 MiniAdmin。");
            }

            return Results.Json(
                new
                {
                    code = "open_platform_session_required",
                    message = "请先使用当前 MiniAdmin 登录令牌调用 POST /connect/session，再继续授权。"
                },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var user = await userRepository.FindByIdAsync(userId, context.RequestAborted);
        if (user is null)
        {
            return ProtocolForbid(Errors.LoginRequired, "当前用户已停用或不存在。");
        }

        var application = await applicationManager.FindByClientIdAsync(
            request.ClientId!,
            context.RequestAborted)
            ?? throw new InvalidOperationException("请求中的客户端不存在。");
        var applicationId = (await applicationManager.GetIdAsync(application, context.RequestAborted))!;
        var authorizations = new List<object>();
        await foreach (var authorization in authorizationManager.FindAsync(
            user.Id.ToString(),
            applicationId,
            Statuses.Valid,
            AuthorizationTypes.Permanent,
            request.GetScopes(),
            context.RequestAborted))
        {
            authorizations.Add(authorization);
        }

        if (HttpMethods.IsPost(context.Request.Method))
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                return Results.BadRequest(new { message = "授权确认已过期，请重新发起授权。" });
            }

            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            if (string.Equals(form["decision"], "deny", StringComparison.Ordinal))
            {
                return Results.Forbid(
                    authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
            }

            if (!string.Equals(form["decision"], "approve", StringComparison.Ordinal))
            {
                return Results.BadRequest(new { message = "未知的授权决定。" });
            }

            return await IssueUserAuthorizationAsync(
                user,
                request,
                application,
                authorizations.LastOrDefault(),
                applicationManager,
                authorizationManager,
                scopeManager,
                context.RequestAborted);
        }

        if (authorizations.Count > 0 && !request.HasPromptValue(PromptValues.Consent))
        {
            return await IssueUserAuthorizationAsync(
                user,
                request,
                application,
                authorizations[^1],
                applicationManager,
                authorizationManager,
                scopeManager,
                context.RequestAborted);
        }

        if (request.HasPromptValue(PromptValues.None))
        {
            return ProtocolForbid(Errors.ConsentRequired, "需要用户确认授权。");
        }

        var tokens = antiforgery.GetAndStoreTokens(context);
        var applicationName = await applicationManager.GetDisplayNameAsync(
            application,
            context.RequestAborted) ?? request.ClientId!;
        return Results.Content(
            BuildConsentPage(context, applicationName, request.GetScopes(), tokens.RequestToken!),
            "text/html; charset=utf-8",
            Encoding.UTF8);
    }

    private static async Task<IResult> ExchangeAsync(
        HttpContext context,
        IOpenPlatformUserRepository userRepository,
        IOpenIddictApplicationManager applicationManager,
        IOpenIddictScopeManager scopeManager)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("无法读取 OpenID Connect 令牌请求。");
        if (request.IsClientCredentialsGrantType())
        {
            var application = await applicationManager.FindByClientIdAsync(
                request.ClientId!,
                context.RequestAborted)
                ?? throw new InvalidOperationException("请求中的客户端不存在。");
            var properties = await applicationManager.GetPropertiesAsync(application, context.RequestAborted);
            var identity = new ClaimsIdentity(
                TokenValidationParameters.DefaultAuthenticationType,
                Claims.Name,
                Claims.Role);
            identity.SetClaim(Claims.Subject, request.ClientId)
                .SetClaim(Claims.Name, await applicationManager.GetDisplayNameAsync(application, context.RequestAborted))
                .SetClaim(OpenPlatformClaimTypes.PrincipalType, OpenPlatformClaimTypes.Application)
                .SetClaim(OpenPlatformClaimTypes.ClientId, request.ClientId);
            if (TryReadString(properties, OpenPlatformPropertyNames.TenantId) is { } tenantId)
            {
                identity.SetClaim("tenant_id", tenantId);
            }

            identity.SetClaims(
                "permission",
                ReadStringArray(properties, OpenPlatformPropertyNames.ApiPermissions));
            await SetScopesAndResourcesAsync(identity, request.GetScopes(), scopeManager, context.RequestAborted);
            identity.SetDestinations(GetDestinations);
            return Results.SignIn(
                new ClaimsPrincipal(identity),
                authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
        {
            var authentication = await context.AuthenticateAsync(
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var subject = authentication.Principal?.GetClaim(Claims.Subject);
            var user = Guid.TryParse(subject, out var userId)
                ? await userRepository.FindByIdAsync(userId, context.RequestAborted)
                : null;
            if (user is null)
            {
                return ProtocolForbid(Errors.InvalidGrant, "该用户已不存在或被停用。");
            }

            var identity = new ClaimsIdentity(
                authentication.Principal!.Claims,
                TokenValidationParameters.DefaultAuthenticationType,
                Claims.Name,
                Claims.Role);
            ApplyUserClaims(identity, user);
            identity.SetDestinations(GetDestinations);
            return Results.SignIn(
                new ClaimsPrincipal(identity),
                authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return ProtocolForbid(Errors.UnsupportedGrantType, "不支持该授权模式。");
    }

    private static async Task<IResult> UserInfoAsync(HttpContext context)
    {
        var result = await context.AuthenticateAsync(
            OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        if (!result.Succeeded || result.Principal is null)
        {
            return Results.Unauthorized();
        }

        var principal = result.Principal;
        return Results.Ok(new
        {
            sub = principal.GetClaim(Claims.Subject),
            name = principal.GetClaim(Claims.Name),
            preferred_username = principal.GetClaim(Claims.PreferredUsername),
            email = principal.HasScope(Scopes.Email) ? principal.GetClaim(Claims.Email) : null,
            roles = principal.HasScope(Scopes.Roles) ? principal.GetClaims(Claims.Role) : [],
            tenant_id = principal.GetClaim("tenant_id")
        });
    }

    private static async Task<IResult> IssueUserAuthorizationAsync(
        OpenPlatformUserDto user,
        OpenIddictRequest request,
        object application,
        object? authorization,
        IOpenIddictApplicationManager applicationManager,
        IOpenIddictAuthorizationManager authorizationManager,
        IOpenIddictScopeManager scopeManager,
        CancellationToken cancellationToken)
    {
        var identity = new ClaimsIdentity(
            TokenValidationParameters.DefaultAuthenticationType,
            Claims.Name,
            Claims.Role);
        ApplyUserClaims(identity, user);
        await SetScopesAndResourcesAsync(identity, request.GetScopes(), scopeManager, cancellationToken);
        authorization ??= await authorizationManager.CreateAsync(
            identity,
            user.Id.ToString(),
            (await applicationManager.GetIdAsync(application, cancellationToken))!,
            AuthorizationTypes.Permanent,
            identity.GetScopes(),
            cancellationToken);
        identity.SetAuthorizationId(await authorizationManager.GetIdAsync(authorization, cancellationToken));
        identity.SetDestinations(GetDestinations);
        return Results.SignIn(
            new ClaimsPrincipal(identity),
            authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static ClaimsIdentity CreateCookieIdentity(OpenPlatformUserDto user)
    {
        var identity = new ClaimsIdentity(
            OpenPlatformConstants.CookieScheme,
            ClaimTypes.Name,
            ClaimTypes.Role);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Name, user.UserName));
        foreach (var role in user.Roles)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        if (user.TenantId.HasValue)
        {
            identity.AddClaim(new Claim("tenant_id", user.TenantId.Value.ToString()));
        }

        return identity;
    }

    private static void ApplyUserClaims(ClaimsIdentity identity, OpenPlatformUserDto user)
    {
        identity.SetClaim(Claims.Subject, user.Id.ToString())
            .SetClaim(Claims.Name, user.UserName)
            .SetClaim(Claims.PreferredUsername, user.UserName)
            .SetClaim(ClaimTypes.NameIdentifier, user.Id.ToString())
            .SetClaim(ClaimTypes.Name, user.UserName)
            .SetClaim(OpenPlatformClaimTypes.PrincipalType, OpenPlatformClaimTypes.User)
            .SetClaim("security_stamp", user.SecurityStamp)
            .SetClaims(Claims.Role, user.Roles.ToImmutableArray())
            .SetClaims("permission", user.Permissions.ToImmutableArray());
        identity.SetClaim(Claims.Email, user.Email);
        identity.SetClaim("tenant_id", user.TenantId?.ToString());
    }

    private static async Task SetScopesAndResourcesAsync(
        ClaimsIdentity identity,
        IEnumerable<string> scopes,
        IOpenIddictScopeManager scopeManager,
        CancellationToken cancellationToken)
    {
        identity.SetScopes(scopes);
        var resources = new List<string>();
        await foreach (var resource in scopeManager
            .ListResourcesAsync(identity.GetScopes(), cancellationToken)
            .WithCancellation(cancellationToken))
        {
            resources.Add(resource);
        }

        identity.SetResources(resources);
    }

    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        yield return Destinations.AccessToken;
        if (claim.Type is Claims.Name or Claims.PreferredUsername && claim.Subject!.HasScope(Scopes.Profile) ||
            claim.Type == Claims.Email && claim.Subject!.HasScope(Scopes.Email) ||
            claim.Type == Claims.Role && claim.Subject!.HasScope(Scopes.Roles))
        {
            yield return Destinations.IdentityToken;
        }
    }

    private static IResult ProtocolForbid(string error, string description)
    {
        return Results.Forbid(
            new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = description
            }),
            [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
    }

    private static string BuildConsentPage(
        HttpContext context,
        string applicationName,
        IEnumerable<string> scopes,
        string requestToken)
    {
        var encoder = HtmlEncoder.Default;
        var fields = new StringBuilder();
        foreach (var pair in context.Request.Query)
        {
            foreach (var value in pair.Value)
            {
                fields.Append("<input type=\"hidden\" name=\"")
                    .Append(encoder.Encode(pair.Key))
                    .Append("\" value=\"")
                    .Append(encoder.Encode(value ?? string.Empty))
                    .Append("\">");
            }
        }

        fields.Append("<input type=\"hidden\" name=\"__RequestVerificationToken\" value=\"")
            .Append(encoder.Encode(requestToken))
            .Append("\">");
        var scopeItems = string.Join(
            string.Empty,
            scopes.Select(scope => $"<li>{encoder.Encode(scope)}</li>"));
        return $$"""
            <!doctype html>
            <html lang="zh-CN">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width,initial-scale=1">
              <title>MiniAdmin 授权确认</title>
              <style>
                body{margin:0;background:#f4f1e8;color:#18201c;font:16px/1.6 Georgia,"Noto Serif SC",serif}
                main{max-width:680px;margin:10vh auto;padding:44px;background:#fffdf7;border:1px solid #d8d1bf;box-shadow:0 24px 70px #28352a1f}
                h1{margin:0 0 8px;font-size:32px} .app{color:#0f6b50;font-weight:700}
                ul{padding:18px 34px;background:#edf4ef;border-left:4px solid #0f6b50}
                .actions{display:flex;gap:12px;margin-top:30px} button{padding:11px 22px;border:1px solid #0f6b50;background:#0f6b50;color:white;cursor:pointer}
                button[value=deny]{background:transparent;color:#6e302b;border-color:#ad817c}
              </style>
            </head>
            <body><main>
              <p>MiniAdmin 开放平台</p>
              <h1>确认第三方应用授权</h1>
              <p><span class="app">{{encoder.Encode(applicationName)}}</span> 希望访问你的账号。</p>
              <p>申请的授权范围：</p><ul>{{scopeItems}}</ul>
              <form method="post" action="/connect/authorize">{{fields}}
                <div class="actions"><button name="decision" value="approve">同意授权</button><button name="decision" value="deny">拒绝</button></div>
              </form>
            </main></body></html>
            """;
    }

    private static string? TryReadString(
        System.Collections.Immutable.ImmutableDictionary<string, JsonElement> properties,
        string key)
    {
        return properties.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static System.Collections.Immutable.ImmutableArray<string> ReadStringArray(
        System.Collections.Immutable.ImmutableDictionary<string, JsonElement> properties,
        string key)
    {
        return properties.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
                .Select(item => item.GetString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!)
                .ToImmutableArray()
            : [];
    }
}
