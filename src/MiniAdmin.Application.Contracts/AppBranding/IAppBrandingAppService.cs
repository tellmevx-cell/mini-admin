namespace MiniAdmin.Application.Contracts.AppBranding;

public interface IAppBrandingAppService
{
    Task<AppBrandingDto> GetAsync(CancellationToken cancellationToken = default);
}
