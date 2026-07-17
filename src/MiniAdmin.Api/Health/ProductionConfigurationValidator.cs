namespace MiniAdmin.Api.Health;

public static class ProductionConfigurationValidator
{
    private const string DevelopmentSigningKey =
        "MiniAdmin local development signing key, replace before production.";

    public static void Validate(
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger logger)
    {
        if (!environment.IsProduction())
        {
            return;
        }

        var errors = new List<string>();
        var signingKey = configuration["Jwt:SigningKey"] ?? string.Empty;
        if (signingKey.Length < 32 ||
            signingKey.Equals(DevelopmentSigningKey, StringComparison.Ordinal) ||
            ContainsPlaceholder(signingKey))
        {
            errors.Add("Jwt:SigningKey must be a non-placeholder secret with at least 32 characters.");
        }

        var openPlatformSigningKey = ValidateSecret(
            configuration,
            "OpenPlatform:SigningKey",
            errors);
        var openPlatformEncryptionKey = ValidateSecret(
            configuration,
            "OpenPlatform:EncryptionKey",
            errors);
        var credentialEncryptionKey = ValidateSecret(
            configuration,
            "OpenPlatform:CredentialEncryptionKey",
            errors);

        var databaseProvider = configuration["Database:Provider"] ?? string.Empty;
        if (!databaseProvider.Equals("MySql", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Database:Provider must be MySql in Production.");
        }

        var connectionString = configuration.GetConnectionString("MiniAdmin") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(connectionString) || ContainsPlaceholder(connectionString))
        {
            errors.Add("ConnectionStrings:MiniAdmin must be configured without placeholder values.");
        }

        var cacheProvider = configuration["Cache:Provider"] ?? string.Empty;
        var redisConfiguration = configuration["Cache:Redis:Configuration"] ?? string.Empty;
        if (!cacheProvider.Equals("Redis", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(redisConfiguration) ||
            ContainsPlaceholder(redisConfiguration))
        {
            errors.Add("Cache:Provider must be Redis with a valid configuration in Production.");
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Unsafe production configuration:\n- " + string.Join("\n- ", errors));
        }

        if (configuration.GetValue<bool>("OpenPlatform:AllowInsecureHttp"))
        {
            logger.LogWarning(
                "OpenPlatform:AllowInsecureHttp is enabled in Production. Use this only on a trusted private network.");
        }

        if (string.Equals(configuration["AllowedHosts"], "*", StringComparison.Ordinal))
        {
            logger.LogWarning(
                "AllowedHosts is '*'. Configure the public host names at the reverse proxy or application level.");
        }

        if (string.Equals(credentialEncryptionKey, signingKey, StringComparison.Ordinal) ||
            string.Equals(openPlatformSigningKey, signingKey, StringComparison.Ordinal) ||
            string.Equals(openPlatformEncryptionKey, signingKey, StringComparison.Ordinal))
        {
            logger.LogWarning(
                "One or more OpenPlatform keys are not isolated from Jwt:SigningKey. " +
                "Rotate them to stable independent keys during a planned maintenance window.");
        }
    }

    private static string ValidateSecret(
        IConfiguration configuration,
        string key,
        ICollection<string> errors)
    {
        var value = configuration[key] ?? string.Empty;
        if (value.Length < 32 || ContainsPlaceholder(value))
        {
            errors.Add($"{key} must be a non-placeholder secret with at least 32 characters.");
        }

        return value;
    }

    private static bool ContainsPlaceholder(string value)
    {
        return value.Contains("change_me", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("replace_", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("placeholder", StringComparison.OrdinalIgnoreCase);
    }
}
