using MiniAdmin.Application.Contracts.AppBranding;
using MiniAdmin.Application.Contracts.Parameters;

namespace MiniAdmin.Application.AppBranding;

public sealed class AppBrandingAppService(
    ISystemParameterRepository systemParameterRepository) : IAppBrandingAppService
{
    private const string DefaultName = "MiniAdmin";
    private const string DefaultLoginTitle = "MiniAdmin 企业后台";

    public async Task<AppBrandingDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var configuredName = await GetTextAsync("app.brand.name", DefaultName, cancellationToken);
        var legacySiteName = await GetOptionalTextAsync("site_name", cancellationToken);
        var name = ResolveBrandName(configuredName, legacySiteName);
        var configuredShortName = await GetTextAsync("app.brand.shortName", name, cancellationToken);
        var configuredLoginTitle = await GetTextAsync("app.brand.loginTitle", DefaultLoginTitle, cancellationToken);
        var shortName = ResolveDefaultSensitiveText(configuredShortName, DefaultName, name);
        var loginTitle = ResolveLoginTitle(configuredLoginTitle, name);
        var copyright = await GetOptionalTextAsync("app.brand.copyright", cancellationToken);
        var watermarkEnabled = await GetBooleanAsync("app.watermark.enabled", false, cancellationToken);
        var watermarkText = await GetOptionalTextAsync("app.watermark.text", cancellationToken);

        return new AppBrandingDto(
            name,
            shortName,
            loginTitle,
            copyright,
            new AppWatermarkDto(watermarkEnabled, watermarkText));
    }

    private async Task<string> GetTextAsync(
        string key,
        string defaultValue,
        CancellationToken cancellationToken)
    {
        var value = await systemParameterRepository.GetValueByKeyAsync(key, cancellationToken);
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
    }

    private async Task<string?> GetOptionalTextAsync(string key, CancellationToken cancellationToken)
    {
        var value = await systemParameterRepository.GetValueByKeyAsync(key, cancellationToken);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private async Task<bool> GetBooleanAsync(
        string key,
        bool defaultValue,
        CancellationToken cancellationToken)
    {
        var value = await systemParameterRepository.GetValueByKeyAsync(key, cancellationToken);
        return bool.TryParse(value, out var result) ? result : defaultValue;
    }

    private static string ResolveBrandName(string configuredName, string? legacySiteName)
    {
        if (!string.Equals(configuredName, DefaultName, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(legacySiteName) ||
            string.Equals(legacySiteName, DefaultName, StringComparison.Ordinal))
        {
            return configuredName;
        }

        return legacySiteName.Trim();
    }

    private static string ResolveDefaultSensitiveText(
        string configuredValue,
        string defaultValue,
        string brandName)
    {
        return string.Equals(configuredValue, defaultValue, StringComparison.Ordinal)
            ? brandName
            : configuredValue;
    }

    private static string ResolveLoginTitle(string configuredLoginTitle, string brandName)
    {
        return string.Equals(configuredLoginTitle, DefaultLoginTitle, StringComparison.Ordinal) &&
               !string.Equals(brandName, DefaultName, StringComparison.Ordinal)
            ? $"{brandName} 企业后台"
            : configuredLoginTitle;
    }
}
