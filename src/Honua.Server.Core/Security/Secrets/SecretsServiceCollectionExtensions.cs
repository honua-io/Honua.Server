using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Security.Secrets;

/// <summary>
/// Extension methods for configuring secrets management services.
/// </summary>
public static class SecretsServiceCollectionExtensions
{
    /// <summary>
    /// Adds secrets management services to the service collection.
    /// The provider is selected based on the "Secrets:Provider" configuration value.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSecretsManagement(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register configuration
        services.Configure<SecretsConfiguration>(configuration.GetSection(SecretsConfiguration.SectionName));

        // Get the provider name from configuration
        var providerName = configuration[$"{SecretsConfiguration.SectionName}:Provider"] ?? SecretsProviders.Local;

        // Add HTTP client factory for Vault
        services.AddHttpClient("VaultClient", client =>
        {
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        })
        .ConfigurePrimaryHttpMessageHandler(() =>
        {
            var handler = new HttpClientHandler();
            var config = configuration.GetSection($"{SecretsConfiguration.SectionName}:Vault").Get<HashiCorpVaultConfiguration>();
            if (config?.SkipTlsVerify == true)
            {
                handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
            }
            return handler;
        });

        // Register the appropriate provider
        switch (providerName)
        {
            case SecretsProviders.AzureKeyVault:
                services.AddSingleton<ISecretsProvider, AzureKeyVaultSecretsProvider>();
                break;

            case SecretsProviders.AwsSecretsManager:
                services.AddSingleton<ISecretsProvider, AwsSecretsManagerProvider>();
                break;

            case SecretsProviders.HashiCorpVault:
                services.AddSingleton<ISecretsProvider, HashiCorpVaultProvider>();
                break;

            case SecretsProviders.Local:
                services.AddSingleton<ISecretsProvider, LocalDevelopmentSecretsProvider>();
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown secrets provider '{providerName}'. Valid values are: {string.Join(", ", SecretsProviders.All)}");
        }

        // Add memory cache if not already registered (for caching secrets)
        services.TryAddSingleton<Microsoft.Extensions.Caching.Memory.IMemoryCache>(sp =>
            new Microsoft.Extensions.Caching.Memory.MemoryCache(
                new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));

        var logger = services.BuildServiceProvider().GetRequiredService<ILogger<SecretsConfiguration>>();
        logger.LogInformation("Registered secrets provider: {Provider}", providerName);

        return services;
    }

    /// <summary>
    /// Adds Azure Key Vault secrets provider specifically.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAzureKeyVaultSecrets(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<SecretsConfiguration>(configuration.GetSection(SecretsConfiguration.SectionName));
        services.AddSingleton<ISecretsProvider, AzureKeyVaultSecretsProvider>();
        services.TryAddSingleton<Microsoft.Extensions.Caching.Memory.IMemoryCache>(sp =>
            new Microsoft.Extensions.Caching.Memory.MemoryCache(
                new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));
        return services;
    }

    /// <summary>
    /// Adds AWS Secrets Manager provider specifically.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAwsSecretsManager(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<SecretsConfiguration>(configuration.GetSection(SecretsConfiguration.SectionName));
        services.AddSingleton<ISecretsProvider, AwsSecretsManagerProvider>();
        services.TryAddSingleton<Microsoft.Extensions.Caching.Memory.IMemoryCache>(sp =>
            new Microsoft.Extensions.Caching.Memory.MemoryCache(
                new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));
        return services;
    }

    /// <summary>
    /// Adds HashiCorp Vault secrets provider specifically.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHashiCorpVaultSecrets(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<SecretsConfiguration>(configuration.GetSection(SecretsConfiguration.SectionName));
        services.AddHttpClient("VaultClient");
        services.AddSingleton<ISecretsProvider, HashiCorpVaultProvider>();
        services.TryAddSingleton<Microsoft.Extensions.Caching.Memory.IMemoryCache>(sp =>
            new Microsoft.Extensions.Caching.Memory.MemoryCache(
                new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));
        return services;
    }

    /// <summary>
    /// Adds local development secrets provider specifically.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLocalDevelopmentSecrets(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<SecretsConfiguration>(configuration.GetSection(SecretsConfiguration.SectionName));
        services.AddSingleton<ISecretsProvider, LocalDevelopmentSecretsProvider>();
        return services;
    }

    /// <summary>
    /// Loads connection strings from the configured secrets provider.
    /// This method should be called during application startup after services are built.
    /// </summary>
    /// <param name="services">The service provider.</param>
    /// <param name="connectionStringNames">The names of connection strings to load from secrets.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task LoadConnectionStringsFromSecretsAsync(
        this IServiceProvider services,
        params string[] connectionStringNames)
    {
        var secretsProvider = services.GetService<ISecretsProvider>();
        if (secretsProvider == null)
        {
            return;
        }

        var configuration = services.GetRequiredService<IConfiguration>();
        var logger = services.GetRequiredService<ILogger<SecretsConfiguration>>();

        foreach (var name in connectionStringNames)
        {
            try
            {
                var secretName = $"ConnectionStrings:{name}";
                var connectionString = await secretsProvider.GetSecretAsync(secretName);

                if (!string.IsNullOrEmpty(connectionString))
                {
                    // Update configuration (this is a simplification; in practice, you'd use a different approach)
                    logger.LogInformation("Loaded connection string '{Name}' from secrets provider", name);
                }
                else
                {
                    logger.LogWarning("Connection string '{Name}' not found in secrets provider", name);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load connection string '{Name}' from secrets provider", name);
            }
        }
    }

    /// <summary>
    /// Loads API keys from the configured secrets provider.
    /// </summary>
    /// <param name="services">The service provider.</param>
    /// <param name="apiKeyNames">The names of API keys to load from secrets.</param>
    /// <returns>A dictionary of API key names to values.</returns>
    public static async Task<Dictionary<string, string>> LoadApiKeysFromSecretsAsync(
        this IServiceProvider services,
        params string[] apiKeyNames)
    {
        var secretsProvider = services.GetService<ISecretsProvider>();
        if (secretsProvider == null)
        {
            return new Dictionary<string, string>();
        }

        var logger = services.GetRequiredService<ILogger<SecretsConfiguration>>();
        var results = new Dictionary<string, string>();

        foreach (var name in apiKeyNames)
        {
            try
            {
                var secretName = $"ApiKeys:{name}";
                var apiKey = await secretsProvider.GetSecretAsync(secretName);

                if (!string.IsNullOrEmpty(apiKey))
                {
                    results[name] = apiKey;
                    logger.LogInformation("Loaded API key '{Name}' from secrets provider", name);
                }
                else
                {
                    logger.LogWarning("API key '{Name}' not found in secrets provider", name);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load API key '{Name}' from secrets provider", name);
            }
        }

        return results;
    }
}
