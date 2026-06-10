using MiniAdmin.Application.AppBranding;
using MiniAdmin.Application.Contracts.AppBranding;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.Parameters;

namespace MiniAdmin.Tests;

public sealed class AppBrandingAppServiceTests
{
    [Fact]
    public async Task Reads_Branding_And_Watermark_From_System_Parameters()
    {
        var repository = new InMemorySystemParameterRepository(new Dictionary<string, string?>
        {
            ["app.brand.name"] = "企业管理平台",
            ["app.brand.shortName"] = "企管",
            ["app.brand.loginTitle"] = "企管 SaaS 后台",
            ["app.brand.copyright"] = "Copyright 2026",
            ["app.watermark.enabled"] = "true",
            ["app.watermark.text"] = "内部系统"
        });
        IAppBrandingAppService service = new AppBrandingAppService(repository);

        var branding = await service.GetAsync();

        Assert.Equal("企业管理平台", branding.Name);
        Assert.Equal("企管", branding.ShortName);
        Assert.Equal("企管 SaaS 后台", branding.LoginTitle);
        Assert.Equal("Copyright 2026", branding.Copyright);
        Assert.True(branding.Watermark.Enabled);
        Assert.Equal("内部系统", branding.Watermark.Text);
    }

    [Fact]
    public async Task Uses_Defaults_When_Parameters_Are_Missing_Or_Invalid()
    {
        var repository = new InMemorySystemParameterRepository(new Dictionary<string, string?>
        {
            ["app.watermark.enabled"] = "not-a-bool"
        });
        IAppBrandingAppService service = new AppBrandingAppService(repository);

        var branding = await service.GetAsync();

        Assert.Equal("MiniAdmin", branding.Name);
        Assert.Equal("MiniAdmin", branding.ShortName);
        Assert.Equal("MiniAdmin 企业后台", branding.LoginTitle);
        Assert.Null(branding.Copyright);
        Assert.False(branding.Watermark.Enabled);
        Assert.Null(branding.Watermark.Text);
    }

    [Fact]
    public async Task Uses_Legacy_Site_Name_When_New_Branding_Name_Is_Default()
    {
        var repository = new InMemorySystemParameterRepository(new Dictionary<string, string?>
        {
            ["site_name"] = "南昌业务中台",
            ["app.brand.name"] = "MiniAdmin",
            ["app.brand.shortName"] = "MiniAdmin",
            ["app.brand.loginTitle"] = "MiniAdmin 企业后台"
        });
        IAppBrandingAppService service = new AppBrandingAppService(repository);

        var branding = await service.GetAsync();

        Assert.Equal("南昌业务中台", branding.Name);
        Assert.Equal("南昌业务中台", branding.ShortName);
        Assert.Equal("南昌业务中台 企业后台", branding.LoginTitle);
    }

    private sealed class InMemorySystemParameterRepository(
        IReadOnlyDictionary<string, string?> values) : ISystemParameterRepository
    {
        public Task<PageResult<SystemParameterDto>> GetListAsync(
            SystemParameterListQuery query,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<string?> GetValueByKeyAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            values.TryGetValue(key, out var value);
            return Task.FromResult(value);
        }

        public Task<SystemParameterDto> UpsertValueByKeyAsync(
            string key,
            string name,
            string value,
            string group,
            string? remark,
            int order,
            bool isEnabled,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<SystemParameterDto> CreateAsync(
            SaveSystemParameterRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<SystemParameterDto?> UpdateAsync(
            Guid id,
            SaveSystemParameterRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
