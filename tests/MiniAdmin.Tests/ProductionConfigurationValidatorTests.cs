using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using MiniAdmin.Api.Health;

namespace MiniAdmin.Tests;

public sealed class ProductionConfigurationValidatorTests
{
    [Fact]
    public void ProductionRejectsDevelopmentStorageAndPlaceholderSecrets()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = "replace_with_a_long_key_change_me",
            ["Database:Provider"] = "InMemory",
            ["ConnectionStrings:MiniAdmin"] = "",
            ["Cache:Provider"] = "Memory"
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionConfigurationValidator.Validate(
                configuration,
                new TestHostEnvironment("Production"),
                NullLogger.Instance));

        Assert.Contains("Jwt:SigningKey", exception.Message);
        Assert.Contains("Database:Provider", exception.Message);
        Assert.Contains("ConnectionStrings:MiniAdmin", exception.Message);
        Assert.Contains("Cache:Provider", exception.Message);
    }

    [Fact]
    public void ProductionAcceptsMySqlRedisAndStrongSecrets()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Jwt:SigningKey"] = new string('a', 64),
            ["OpenPlatform:SigningKey"] = new string('b', 64),
            ["OpenPlatform:EncryptionKey"] = new string('c', 64),
            ["OpenPlatform:CredentialEncryptionKey"] = new string('d', 64),
            ["Database:Provider"] = "MySql",
            ["ConnectionStrings:MiniAdmin"] =
                "Server=mysql;Database=mini_admin;User=miniadmin;Password=strong-db-secret;",
            ["Cache:Provider"] = "Redis",
            ["Cache:Redis:Configuration"] = "redis:6379,password=strong-redis-secret",
            ["AllowedHosts"] = "admin.example.com"
        });

        ProductionConfigurationValidator.Validate(
            configuration,
            new TestHostEnvironment("Production"),
            NullLogger.Instance);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "MiniAdmin.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
