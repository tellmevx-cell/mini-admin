namespace MiniAdmin.Application.Contracts.AppBranding;

public sealed record AppBrandingDto(
    string Name,
    string ShortName,
    string LoginTitle,
    string? Copyright,
    AppWatermarkDto Watermark);

public sealed record AppWatermarkDto(
    bool Enabled,
    string? Text);
