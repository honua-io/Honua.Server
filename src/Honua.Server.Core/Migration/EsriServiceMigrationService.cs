// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Migration.GeoservicesRest;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Migration;

/// <summary>
/// Store for active Esri service migration jobs.
/// </summary>
internal sealed class ActiveEsriMigrationJobStore : ActiveJobStore<GeoservicesRestMigrationJob>
{
    protected override Guid GetJobId(GeoservicesRestMigrationJob job) => job.JobId;
}

/// <summary>
/// Store for completed Esri service migration job snapshots.
/// </summary>
internal sealed class CompletedEsriMigrationJobStore : CompletedJobStore<GeoservicesRestMigrationJobSnapshot>
{
    public CompletedEsriMigrationJobStore() : base(maxCompletedJobs: 100)
    {
    }

    protected override Guid GetJobId(GeoservicesRestMigrationJobSnapshot snapshot) => snapshot.JobId;
}

public interface IEsriServiceMigrationService
{
    Task<GeoservicesRestMigrationJobSnapshot> EnqueueAsync(EsriServiceMigrationRequest request, CancellationToken cancellationToken = default);
    Task<GeoservicesRestMigrationJobSnapshot?> TryGetJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GeoservicesRestMigrationJobSnapshot>> ListJobsAsync(CancellationToken cancellationToken = default);
    Task<GeoservicesRestMigrationJobSnapshot?> CancelAsync(Guid jobId, string? reason = null);
}

public sealed class EsriServiceMigrationService : BackgroundService, IEsriServiceMigrationService
{
    private const int QueueCapacity = 16;
    private const int DefaultBatchSize = 10000;

    private readonly Channel<EsriServiceMigrationWorkItem> _queue;
    private readonly ActiveEsriMigrationJobStore _jobs = new();
    private readonly CompletedEsriMigrationJobStore _completedJobs = new();
    private readonly GeoservicesRESTMigrationService _metadataMigrationService;
    private readonly LayerSchemaCreator _schemaCreator;
    private readonly IGeoservicesRestServiceClient _esriClient;
    private readonly IFeatureContextResolver _contextResolver;
    private readonly IHonuaConfigurationService _configurationService;
    private readonly ILogger<EsriServiceMigrationService> _logger;

    public EsriServiceMigrationService(
        GeoservicesRESTMigrationService metadataMigrationService,
        LayerSchemaCreator schemaCreator,
        IGeoservicesRestServiceClient esriClient,
        IFeatureContextResolver contextResolver,
        IHonuaConfigurationService configurationService,
        ILogger<EsriServiceMigrationService> logger)
    {
        _metadataMigrationService = metadataMigrationService ?? throw new ArgumentNullException(nameof(metadataMigrationService));
        _schemaCreator = schemaCreator ?? throw new ArgumentNullException(nameof(schemaCreator));
        _esriClient = esriClient ?? throw new ArgumentNullException(nameof(esriClient));
        _contextResolver = contextResolver ?? throw new ArgumentNullException(nameof(contextResolver));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _queue = Channel.CreateBounded<EsriServiceMigrationWorkItem>(new BoundedChannelOptions(QueueCapacity)
        {
            AllowSynchronousContinuations = false,
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public async Task<IReadOnlyList<GeoservicesRestMigrationJobSnapshot>> ListJobsAsync(CancellationToken cancellationToken = default)
    {
        var activeJobs = await _jobs.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var completedJobs = await _completedJobs.GetAllAsync(cancellationToken).ConfigureAwait(false);

        return activeJobs.Select(job => job.Snapshot)
            .Concat(completedJobs)
            .OrderByDescending(snapshot => snapshot.CreatedAtUtc)
            .ToArray();
    }

    public async Task<GeoservicesRestMigrationJobSnapshot?> TryGetJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var activeJob = await _jobs.GetAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (activeJob is not null)
        {
            return activeJob.Snapshot;
        }

        var completedJob = await _completedJobs.GetAsync(jobId, cancellationToken).ConfigureAwait(false);
        if (completedJob is not null)
        {
            return completedJob;
        }

        return null;
    }

    public async Task<GeoservicesRestMigrationJobSnapshot?> CancelAsync(Guid jobId, string? reason = null)
    {
        var job = await _jobs.GetAsync(jobId).ConfigureAwait(false);
        if (job is not null)
        {
            job.RequestCancellation(string.IsNullOrWhiteSpace(reason) ? "Cancellation requested." : reason);
            return job.Snapshot;
        }

        var snapshot = await _completedJobs.GetAsync(jobId).ConfigureAwait(false);
        if (snapshot is not null)
        {
            return snapshot;
        }

        return null;
    }

    public async Task<GeoservicesRestMigrationJobSnapshot> EnqueueAsync(EsriServiceMigrationRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        request.Validate();

        var job = new GeoservicesRestMigrationJob(request.TargetServiceId, request.TargetDataSourceId);
        var registered = await _jobs.RegisterAsync(job, cancellationToken).ConfigureAwait(false);
        if (!registered)
        {
            throw new InvalidOperationException($"Failed to register migration job for service '{request.TargetServiceId}'.");
        }

        var workItem = new EsriServiceMigrationWorkItem(request, job);
        await _queue.Writer.WriteAsync(workItem, cancellationToken).ConfigureAwait(false);

        return job.Snapshot;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var workItem in _queue.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
        {
            var job = workItem.Job;
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, job.Token);
            var jobToken = linkedCts.Token;

            if (jobToken.IsCancellationRequested)
            {
                job.MarkCancelled("Cancelled", "Job cancelled before processing started.");
                continue;
            }

            try
            {
                await ProcessWorkItemAsync(workItem, jobToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                job.MarkCancelled("Cancelled", "Migration cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Esri service migration job {JobId} failed.", job.JobId);
                job.MarkFailed("Failed", ex.Message);
            }
            finally
            {
                if (job.IsTerminal)
                {
                    await RecordJobFinalStateAsync(job, job.Snapshot, stoppingToken).ConfigureAwait(false);
                }
            }
        }
    }

    private async Task RecordJobFinalStateAsync(GeoservicesRestMigrationJob job, GeoservicesRestMigrationJobSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await _jobs.UnregisterAsync(job.JobId, cancellationToken).ConfigureAwait(false);
        job.Dispose();

        await _completedJobs.RecordCompletionAsync(snapshot, cancellationToken).ConfigureAwait(false);
    }

    private async Task ProcessWorkItemAsync(EsriServiceMigrationWorkItem workItem, CancellationToken cancellationToken)
    {
        var job = workItem.Job;
        var request = workItem.Request;

        job.MarkStarted("Analyzing Esri service");
        _logger.LogInformation("Starting migration for Esri service {ServiceUri} to {TargetServiceId}", request.SourceServiceUri, request.TargetServiceId);

        // Step 1: Migrate metadata
        job.UpdateProgress(GeoservicesRestMigrationJobStatus.Initializing, "Creating metadata", 0.05);

        var metadataRequest = new GeoservicesRestServiceMigrationRequest
        {
            ServiceUri = request.SourceServiceUri,
            TargetServiceId = request.TargetServiceId,
            TargetFolderId = request.TargetFolderId,
            TargetDataSourceId = request.TargetDataSourceId,
            LayerIds = request.LayerIds,
            TranslatorOptions = request.TranslatorOptions,
            SecurityProfileId = request.SecurityProfileId
        };

        GeoservicesRestMigrationResult migrationResult;
        try
        {
            migrationResult = await _metadataMigrationService.MigrateAsync(metadataRequest, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to migrate metadata: {ex.Message}", ex);
        }

        var plan = migrationResult.Plan;
        _logger.LogInformation("Metadata migration completed for {LayerCount} layers", plan.Layers.Count);

        if (!request.IncludeData)
        {
            job.MarkCompleted($"Metadata migration completed for {plan.Layers.Count} layer(s). Data migration skipped.");
            return;
        }

        // Step 2: Create database schemas for each layer
        job.UpdateProgress(GeoservicesRestMigrationJobStatus.PreparingSchema, "Creating database schemas", 0.15);

        foreach (var layerPlan in plan.Layers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var featureContext = await _contextResolver.ResolveAsync(request.TargetServiceId, layerPlan.LayerId, cancellationToken).ConfigureAwait(false);
                await _schemaCreator.EnsureLayerSchemaAsync(featureContext, layerPlan.Schema, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Created schema for layer {LayerId} in table {TableName}", layerPlan.LayerId, layerPlan.Schema.TableName);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create schema for layer '{layerPlan.LayerId}': {ex.Message}", ex);
            }
        }

        // Step 3: Import data for each layer
        job.UpdateProgress(GeoservicesRestMigrationJobStatus.CopyingData, "Importing features", 0.25);

        var totalLayers = plan.Layers.Count;
        var completedLayers = 0;

        foreach (var layerPlan in plan.Layers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var layerProgress = (double)completedLayers / totalLayers;
            var overallProgress = 0.25 + (layerProgress * 0.70);

            job.UpdateProgress(
                GeoservicesRestMigrationJobStatus.CopyingData,
                $"Importing layer {completedLayers + 1}/{totalLayers}: {layerPlan.LayerTitle ?? layerPlan.LayerId}",
                overallProgress);

            await ImportLayerDataAsync(request, layerPlan, job, totalLayers, completedLayers, cancellationToken).ConfigureAwait(false);

            completedLayers++;
        }

        job.MarkCompleted($"Migration completed: {plan.Layers.Count} layer(s) migrated");
        _logger.LogInformation("Migration completed for service {TargetServiceId}", request.TargetServiceId);
    }

    private string? ResolveSecurityToken(EsriServiceMigrationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SecurityProfileId))
        {
            return null;
        }

        var config = _configurationService.Current;
        if (!config.ExternalServiceSecurity.Profiles.TryGetValue(request.SecurityProfileId, out var profile))
        {
            _logger.LogWarning("Security profile '{ProfileId}' not found in configuration", request.SecurityProfileId);
            return null;
        }

        if (string.IsNullOrWhiteSpace(profile.Token))
        {
            _logger.LogWarning("Security profile '{ProfileId}' has no token configured", request.SecurityProfileId);
            return null;
        }

        return profile.Token;
    }

    private async Task ImportLayerDataAsync(
        EsriServiceMigrationRequest request,
        GeoservicesRestLayerMigrationPlan layerPlan,
        GeoservicesRestMigrationJob job,
        int totalLayers,
        int completedLayers,
        CancellationToken cancellationToken)
    {
        var featureContext = await _contextResolver.ResolveAsync(request.TargetServiceId, layerPlan.LayerId, cancellationToken).ConfigureAwait(false);
        var batchSize = request.BatchSize ?? DefaultBatchSize;
        var geometryFieldName = featureContext.Layer.Storage?.GeometryColumn ?? featureContext.Layer.GeometryField;
        var token = ResolveSecurityToken(request);

        // Query for total object IDs
        var idQuery = new GeoservicesRestQueryParameters
        {
            Where = "1=1",
            ReturnGeometry = false,
            ReturnIdsOnly = true
        };

        var idResult = await _esriClient.QueryObjectIdsAsync(layerPlan.SourceLayerUri, idQuery, token, cancellationToken).ConfigureAwait(false);
        var objectIds = idResult.ObjectIds ?? new List<int>();
        var totalFeatures = objectIds.Count;

        _logger.LogInformation("Layer {LayerId} has {FeatureCount} features to migrate", layerPlan.LayerId, totalFeatures);

        if (totalFeatures == 0)
        {
            return;
        }

        // Process features in batches
        var processedFeatures = 0;
        for (var offset = 0; offset < objectIds.Count; offset += batchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batchIds = objectIds.Skip(offset).Take(batchSize).ToList();
            var idsString = string.Join(",", batchIds);

            var featureQuery = new GeoservicesRestQueryParameters
            {
                ObjectIds = idsString,
                ReturnGeometry = true,
                OutFields = "*"
            };

            var queryResult = await _esriClient.QueryAsync(layerPlan.SourceLayerUri, featureQuery, token, cancellationToken).ConfigureAwait(false);

            foreach (var esriFeature in queryResult.Features)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

                // Process geometry
                if (!string.IsNullOrWhiteSpace(geometryFieldName))
                {
                    var geoJson = GeoservicesRestGeometryConverter.ToGeoJson(esriFeature.Geometry, layerPlan.SourceLayer.GeometryType);
                    attributes[geometryFieldName] = geoJson;
                }

                // Process attributes
                foreach (var kvp in esriFeature.Attributes)
                {
                    var fieldName = kvp.Key;
                    var value = ExtractFieldValue(kvp.Value);
                    attributes[fieldName] = value;
                }

                var record = new FeatureRecord(attributes);
                await featureContext.Provider.CreateAsync(featureContext.DataSource, featureContext.Service, featureContext.Layer, record, null, cancellationToken).ConfigureAwait(false);

                processedFeatures++;
            }

            // Update progress
            var layerProgress = (double)completedLayers / totalLayers;
            var layerFeatureProgress = (double)processedFeatures / totalFeatures / totalLayers;
            var overallProgress = 0.25 + (layerProgress * 0.70) + (layerFeatureProgress * 0.70);

            job.UpdateProgress(
                GeoservicesRestMigrationJobStatus.CopyingData,
                $"Importing layer {completedLayers + 1}/{totalLayers}: {layerPlan.LayerTitle ?? layerPlan.LayerId} ({processedFeatures}/{totalFeatures} features)",
                overallProgress);
        }

        _logger.LogInformation("Imported {FeatureCount} features for layer {LayerId}", processedFeatures, layerPlan.LayerId);
    }

    private static object? ExtractFieldValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => element.TryGetInt64(out var longValue) ? longValue : element.GetDouble(),
            JsonValueKind.String => element.GetString(),
            _ => element.GetRawText()
        };
    }

    private readonly record struct EsriServiceMigrationWorkItem(EsriServiceMigrationRequest Request, GeoservicesRestMigrationJob Job);
}
