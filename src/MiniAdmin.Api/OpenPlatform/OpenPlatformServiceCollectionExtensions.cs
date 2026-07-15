using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.Tokens;
using MiniAdmin.Infrastructure.OpenPlatform;
using OpenIddict.Abstractions;

namespace MiniAdmin.Api.OpenPlatform;

public static class OpenPlatformServiceCollectionExtensions
{
    public static IServiceCollection AddMiniAdminOpenPlatform(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var issuer = configuration["OpenPlatform:Issuer"] ?? "http://localhost:5021/";
        if (!Uri.TryCreate(issuer, UriKind.Absolute, out var issuerUri))
        {
            throw new InvalidOperationException("OpenPlatform:Issuer 必须是绝对地址。");
        }

        var signingMaterial = configuration["OpenPlatform:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingMaterial))
        {
            signingMaterial = configuration["Jwt:SigningKey"];
        }

        if (string.IsNullOrWhiteSpace(signingMaterial))
        {
            throw new InvalidOperationException("OpenPlatform:SigningKey 或 Jwt:SigningKey 未配置。");
        }
        var signingBytes = Encoding.UTF8.GetBytes(signingMaterial);
        if (signingBytes.Length < 32)
        {
            throw new InvalidOperationException("开放平台签名密钥至少需要 32 字节。");
        }

        var encryptionMaterial = configuration["OpenPlatform:EncryptionKey"];
        var encryptionBytes = string.IsNullOrWhiteSpace(encryptionMaterial)
            ? SHA256.HashData(Encoding.UTF8.GetBytes($"miniadmin:oidc:encryption:{signingMaterial}"))
            : SHA256.HashData(Encoding.UTF8.GetBytes(encryptionMaterial));
        var signingCertificate = LoadSigningCertificate(configuration, environment, signingMaterial);
        var allowInsecureHttp = configuration.GetValue<bool>("OpenPlatform:AllowInsecureHttp");

        services.AddAuthentication()
            .AddCookie(OpenPlatformConstants.CookieScheme, options =>
            {
                options.Cookie.Name = "miniadmin.openplatform.session";
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = environment.IsDevelopment() || allowInsecureHttp
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(15);
                options.SlidingExpiration = false;
            });

        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = "miniadmin.openplatform.csrf";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = environment.IsDevelopment() || allowInsecureHttp
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
            options.FormFieldName = "__RequestVerificationToken";
        });

        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                    .UseDbContext<OpenPlatformDbContext>()
                    .ReplaceDefaultEntities<Guid>();
            })
            .AddServer(options =>
            {
                options.SetIssuer(issuerUri);
                options.SetAuthorizationEndpointUris("connect/authorize");
                options.SetTokenEndpointUris("connect/token");
                options.SetUserInfoEndpointUris("connect/userinfo");
                options.SetIntrospectionEndpointUris("connect/introspect");
                options.SetRevocationEndpointUris("connect/revoke");

                options.AllowAuthorizationCodeFlow();
                options.AllowRefreshTokenFlow();
                options.AllowClientCredentialsFlow();
                options.RequireProofKeyForCodeExchange();
                options.RegisterScopes(
                    OpenIddictConstants.Scopes.OpenId,
                    OpenIddictConstants.Scopes.Profile,
                    OpenIddictConstants.Scopes.Email,
                    OpenIddictConstants.Scopes.Roles,
                    OpenIddictConstants.Scopes.OfflineAccess,
                    OpenPlatformConstants.ApiScope);

                // OpenID Connect identity tokens require an asymmetric key. Access tokens keep
                // using the stable symmetric key, while deployments can opt into a persistent PFX.
                options.AddSigningKey(new SymmetricSecurityKey(signingBytes));
                if (signingCertificate is not null)
                {
                    options.AddSigningCertificate(signingCertificate);
                }
                else
                {
                    options.AddEphemeralSigningKey();
                }
                options.AddEncryptionKey(new SymmetricSecurityKey(encryptionBytes));
                options.DisableAccessTokenEncryption();
                options.SetAccessTokenLifetime(TimeSpan.FromHours(2));
                options.SetAuthorizationCodeLifetime(TimeSpan.FromMinutes(5));
                options.SetRefreshTokenLifetime(TimeSpan.FromDays(30));

                var aspNetCore = options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableTokenEndpointPassthrough()
                    .EnableUserInfoEndpointPassthrough();
                if (!environment.IsProduction() || allowInsecureHttp)
                {
                    aspNetCore.DisableTransportSecurityRequirement();
                }
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        return services;
    }

    private static X509Certificate2? LoadSigningCertificate(
        IConfiguration configuration,
        IHostEnvironment environment,
        string signingMaterial)
    {
        var path = configuration["OpenPlatform:SigningCertificate:Path"];
        if (string.IsNullOrWhiteSpace(path))
        {
            if (!environment.IsProduction())
            {
                return null;
            }

            path = configuration["OpenPlatform:SigningCertificate:AutoGeneratePath"];
            if (string.IsNullOrWhiteSpace(path))
            {
                path = Path.Combine(AppContext.BaseDirectory, "storage", "openplatform-signing.pfx");
            }

            return LoadOrCreateSigningCertificate(
                Path.GetFullPath(path),
                ResolveCertificatePassword(configuration, signingMaterial));
        }

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new InvalidOperationException($"开放平台签名证书不存在：{fullPath}");
        }

        return X509CertificateLoader.LoadPkcs12FromFile(
            fullPath,
            configuration["OpenPlatform:SigningCertificate:Password"],
            X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
    }

    private static X509Certificate2 LoadOrCreateSigningCertificate(string path, string password)
    {
        if (File.Exists(path))
        {
            return LoadCertificate(path, password);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            using var rsa = RSA.Create(3072);
            var request = new CertificateRequest(
                "CN=MiniAdmin OpenID Connect Signing",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature,
                true));
            request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
            using var generated = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddMinutes(-5),
                DateTimeOffset.UtcNow.AddYears(3));
            File.WriteAllBytes(temporaryPath, generated.Export(X509ContentType.Pkcs12, password));
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(
                    temporaryPath,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }

            try
            {
                File.Move(temporaryPath, path, overwrite: false);
            }
            catch (IOException) when (File.Exists(path))
            {
                // Another instance won the first-start race on a shared volume.
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }

        return LoadCertificate(path, password);
    }

    private static X509Certificate2 LoadCertificate(string path, string password)
    {
        return X509CertificateLoader.LoadPkcs12FromFile(
            path,
            password,
            X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
    }

    private static string ResolveCertificatePassword(
        IConfiguration configuration,
        string signingMaterial)
    {
        var configured = configuration["OpenPlatform:SigningCertificate:Password"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"miniadmin:oidc:certificate:{signingMaterial}")));
    }
}
