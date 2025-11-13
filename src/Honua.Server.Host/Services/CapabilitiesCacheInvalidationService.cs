// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Honua.Server.Host.Services;

/// <summary>
/// Background service that monitors metadata registry changes and automatically
/// invalidates capabilities cache entries when metadata is updated.
/// </summary>
/// <remarks>
/// <para>
/// This service ensures that cached capabilities documents remain consistent with
/// the current metadata state. It subscribes to the metadata registry's change token
/// and invalidates all cached capabilities when metadata is reloaded or updated.
/// </para>
/// <para>
/// <strong>Change Detection:</strong>
/// <list type="bullet">
/// <item>Monitors IMetadataRegistry.GetChangeToken() for metadata updates</item>
/// <item>Automatically reregisters change callback after each notification</item>
/// <item>Invalidates all capabilities cache entries on metadata change</item>
/// </list>
/// </para>
/// <para>
/// <strong>Performance Impact:</strong>
/// Cache invalidation is typically fast (&lt;1ms for 100 entries) and happens
/// infrequently (only on metadata reloads). The next GetCapabilities request
/// will repopulate the cache.
/// </para>
/// </remarks>
public sealed class CapabilitiesCacheInvalidationService : BackgroundService
{
    private readonly IMetadataRegistry metadataRegistry;
    private readonly ICapabilitiesCache capabilitiesCache;
    private readonly ILogger<CapabilitiesCacheInvalidationService> logger;
    private readonly CapabilitiesCacheOptions options;
    private IDisposable? _changeTokenRegistration;

    public CapabilitiesCacheInvalidationService(
        IMetadataRegistry metadataRegistry,
        ICapabilitiesCache capabilitiesCache,
        ILogger<CapabilitiesCacheInvalidationService> logger,
        IOptions<CapabilitiesCacheOptions> options)
    {
        this.metadataRegistry = metadataRegistry ?? throw new ArgumentNullException(nameof(metadataRegistry));
        this.capabilitiesCache = capabilitiesCache ?? throw new ArgumentNullException(nameof(capabilitiesCache));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!this.options.EnableCaching || !this.options.AutoInvalidateOnMetadataChange)
        {
            this.logger.LogInformation(
                "Capabilities cache invalidation service disabled " +
                "(EnableCaching={EnableCaching}, AutoInvalidateOnMetadataChange={AutoInvalidate})",
                this.options.EnableCaching,
                this.options.AutoInvalidateOnMetadataChange);
            return Task.CompletedTask;
        }

        this.logger.LogInformation("Capabilities cache invalidation service started");

        // Register for metadata change notifications
        RegisterChangeCallback();

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        _changeTokenRegistration?.Dispose();
        base.Dispose();
    }

    /// <summary>
    /// Registers a callback for metadata change notifications.
    /// </summary>
    private void RegisterChangeCallback()
    {
        try
        {
            var changeToken = this.metadataRegistry.GetChangeToken();
            this.changeTokenRegistration = ChangeToken.OnChange(
                () => this.metadataRegistry.GetChangeToken(),
                OnMetadataChanged);

            this.logger.LogDebug("Registered metadata change callback for capabilities cache invalidation");
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to register metadata change callback");
        }
    }

    /// <summary>
    /// Called when metadata registry signals a change.
    /// </summary>
    private void OnMetadataChanged()
    {
        try
        {
            this.logger.LogInformation("Metadata change detected, invalidating capabilities cache");

            // Get statistics before invalidation for logging
            var statsBefore = this.capabilitiesCache.GetStatistics();

            // Invalidate all cached capabilities documents
            this.capabilitiesCache.InvalidateAll();

            this.logger.LogInformation(
                "Invalidated {EntryCount} capabilities cache entries. " +
                "Previous hit rate: {HitRate:P2} ({Hits} hits / {Misses} misses)",
                statsBefore.EntryCount,
                statsBefore.HitRate,
                statsBefore.Hits,
                statsBefore.Misses);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error invalidating capabilities cache on metadata change");
        }
    }
}
