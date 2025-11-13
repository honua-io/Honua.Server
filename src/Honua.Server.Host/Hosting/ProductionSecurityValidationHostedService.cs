// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Logging;
using Honua.Server.Core.Metadata;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Host.Hosting;

/// <summary>
/// Hosted service that validates production security configuration at startup.
/// Checks authentication, CORS, and other security-sensitive settings.
/// </summary>
public sealed class ProductionSecurityValidationHostedService : IHostedService
{
    private readonly IOptions<HonuaAuthenticationOptions> authOptions;
    private readonly IMetadataRegistry metadataRegistry;
    private readonly IHostEnvironment hostEnvironment;
    private readonly ILoggerFactory loggerFactory;
    private readonly ILogger<ProductionSecurityValidationHostedService> logger;

    public ProductionSecurityValidationHostedService(
        IOptions<HonuaAuthenticationOptions> authOptions,
        IMetadataRegistry metadataRegistry,
        IHostEnvironment hostEnvironment,
        ILoggerFactory loggerFactory,
        ILogger<ProductionSecurityValidationHostedService> logger)
    {
        this.authOptions = authOptions;
        this.metadataRegistry = metadataRegistry;
        this.hostEnvironment = hostEnvironment;
        this.loggerFactory = loggerFactory;
        this.logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!this.hostEnvironment.IsProduction())
        {
            this.logger.LogInformation(
                "Skipping production security validation - Environment={Environment}",
                this.hostEnvironment.EnvironmentName);
            return;
        }

        this.logger.LogInformation(
            "Running production security configuration validation for Environment={Environment}",
            this.hostEnvironment.EnvironmentName);

        try
        {
            this.logger.LogInformation(
                "Starting {ServiceName} validation. AuthMode={AuthMode}",
                nameof(ProductionSecurityValidationHostedService),
                this.authOptions.Value.Mode);

            // Validate authentication settings
            var validator = new ProductionSecurityValidator(this.loggerFactory.CreateLogger<ProductionSecurityValidator>());
            validator.ValidateProductionSecurity(this.authOptions.Value, isProduction: true);

            // Validate CORS settings from metadata
            await this.metadataRegistry.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
            var snapshot = await this.metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
            ValidateCorsConfiguration(snapshot);

            this.logger.LogInformation(
                "{ServiceName} validation completed successfully. AuthMode={AuthMode}",
                nameof(ProductionSecurityValidationHostedService),
                this.authOptions.Value.Mode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            this.logger.LogWarning(
                "{ServiceName} validation cancelled during startup",
                nameof(ProductionSecurityValidationHostedService));
            throw;
        }
        catch (Exception ex)
        {
            this.logger.LogCritical(
                ex,
                "FATAL: {ServiceName} validation failed in Environment={Environment}. " +
                "Typical causes: " +
                "1. Database/Redis unreachable (cannot load metadata), " +
                "2. Invalid security configuration, " +
                "3. Network connectivity issues, " +
                "4. Missing required security settings. " +
                "Application cannot start. ExceptionType={ExceptionType}",
                nameof(ProductionSecurityValidationHostedService),
                this.hostEnvironment.EnvironmentName,
                ex.GetType().Name);

            // For production: Fail fast with clear error
            throw new InvalidOperationException(
                $"{nameof(ProductionSecurityValidationHostedService)} validation failed. " +
                $"Check security configuration and database connectivity. Error: {ex.Message}",
                ex);
        }
    }

    /// <summary>
    /// Validates CORS configuration for production security.
    /// Warns if AllowAnyOrigin is enabled in production.
    /// </summary>
    private void ValidateCorsConfiguration(MetadataSnapshot snapshot)
    {
        var cors = snapshot.Server.Cors;

        if (!cors.Enabled)
        {
            return;
        }

        if (cors.AllowAnyOrigin)
        {
            this.logger.LogWarning(
                "SECURITY WARNING: CORS is configured with AllowAnyOrigin=true in production. " +
                "This allows requests from any domain and may expose your API to CSRF attacks. " +
                "Configure specific allowed origins in metadata for production deployments. " +
                "See: https://developer.mozilla.org/en-US/docs/Web/HTTP/CORS");
        }

        if (cors.AllowCredentials && cors.AllowedOrigins.Count > 0)
        {
            this.logger.LogInformation(
                "CORS is configured with credentials support for {OriginCount} specific origins. " +
                "Ensure these origins are trusted domains.",
                cors.AllowedOrigins.Count);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        this.logger.LogInformation(
            "{ServiceName} stopping",
            nameof(ProductionSecurityValidationHostedService));
        return Task.CompletedTask;
    }
}
