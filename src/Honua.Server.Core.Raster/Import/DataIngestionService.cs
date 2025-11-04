// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Data;
using Honua.Server.Core.Import;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Results;
using Honua.Server.Core.Stac;
using MaxRev.Gdal.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OSGeo.GDAL;
using OSGeo.OGR;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Raster.Import;

/// <summary>
/// Store for active data ingestion jobs.
/// </summary>
internal sealed class ActiveDataIngestionJobStore : ActiveJobStore<DataIngestionJob>
{
    protected override Guid GetJobId(DataIngestionJob job) => job.JobId;
}

/// <summary>
/// Store for completed data ingestion job snapshots.
/// </summary>
internal sealed class CompletedDataIngestionJobStore : CompletedJobStore<DataIngestionJobSnapshot>
{
    public CompletedDataIngestionJobStore() : base(maxCompletedJobs: 100)
    {
    }

    protected override Guid GetJobId(DataIngestionJobSnapshot snapshot) => snapshot.JobId;
}

public interface IDataIngestionService
{
    Task<DataIngestionJobSnapshot> EnqueueAsync(DataIngestionRequest request, CancellationToken cancellationToken = default);
    Task<DataIngestionJobSnapshot?> TryGetJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DataIngestionJobSnapshot>> ListJobsAsync(CancellationToken cancellationToken = default);
    Task<DataIngestionJobSnapshot?> CancelAsync(Guid jobId, string? reason = null);
}

public sealed class DataIngestionService : BackgroundService, IDataIngestionService
{
    private const int QueueCapacity = 32;

    private readonly Channel<DataIngestionWorkItem> _queue;
    private readonly ActiveDataIngestionJobStore _jobs = new();
    private readonly CompletedDataIngestionJobStore _completedJobs = new();
    private readonly IFeatureContextResolver _contextResolver;
    private readonly IRasterStacCatalogSynchronizer _stacSynchronizer;
    private readonly ILogger<DataIngestionService> _logger;
    private readonly DataIngestionOptions _options;

    private static readonly Lazy<bool> GdalConfigured = new(() =>
    {
        try
        {
            GdalBase.ConfigureAll();
            Gdal.AllRegister();
            Ogr.RegisterAll();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to configure GDAL/OGR runtime.", ex);
        }

        return true;
    });

    public DataIngestionService(
        IFeatureContextResolver contextResolver,
        IRasterStacCatalogSynchronizer stacSynchronizer,
        IOptions<DataIngestionOptions> options,
        ILogger<DataIngestionService> logger)
    {
        _contextResolver = contextResolver ?? throw new ArgumentNullException(nameof(contextResolver));
        _stacSynchronizer = stacSynchronizer ?? throw new ArgumentNullException(nameof(stacSynchronizer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new DataIngestionOptions();

        _queue = Channel.CreateBounded<DataIngestionWorkItem>(new BoundedChannelOptions(QueueCapacity)
        {
            AllowSynchronousContinuations = false,
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public async Task<IReadOnlyList<DataIngestionJobSnapshot>> ListJobsAsync(CancellationToken cancellationToken = default)
    {
        var activeJobs = await _jobs.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var completedJobs = await _completedJobs.GetAllAsync(cancellationToken).ConfigureAwait(false);

        return activeJobs.Select(job => job.Snapshot)
            .Concat(completedJobs)
            .OrderByDescending(snapshot => snapshot.CreatedAtUtc)
            .ToArray();
    }

    public async Task<DataIngestionJobSnapshot?> TryGetJobAsync(Guid jobId, CancellationToken cancellationToken = default)
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

    public async Task<DataIngestionJobSnapshot?> CancelAsync(Guid jobId, string? reason = null)
    {
        var job = await _jobs.GetAsync(jobId).ConfigureAwait(false);
        if (job is not null)
        {
            job.RequestCancellation(reason.IsNullOrWhiteSpace() ? "Cancellation requested." : reason);
            return job.Snapshot;
        }

        var snapshot = await _completedJobs.GetAsync(jobId).ConfigureAwait(false);
        if (snapshot is not null)
        {
            return snapshot;
        }

        return null;
    }

    public async Task<DataIngestionJobSnapshot> EnqueueAsync(DataIngestionRequest request, CancellationToken cancellationToken = default)
    {
        return await ActivityScope.ExecuteAsync(
            HonuaTelemetry.Import,
            "DataIngestionService.EnqueueAsync",
            new[]
            {
                ("ingestion.service_id", (object?)request?.ServiceId),
                ("ingestion.layer_id", (object?)request?.LayerId),
                ("ingestion.source_file", (object?)request?.SourceFileName)
            },
            async activity =>
            {
                if (request is null)
                {
                    throw new ArgumentNullException(nameof(request));
                }

                request.EnsureValid();

                EnsureGdalConfigured();

                var job = new DataIngestionJob(request.ServiceId, request.LayerId, request.SourceFileName);
                var registered = await _jobs.RegisterAsync(job, cancellationToken).ConfigureAwait(false);
                if (!registered)
                {
                    throw new InvalidOperationException($"Failed to register ingestion job for service '{request.ServiceId}' layer '{request.LayerId}'.");
                }

                activity?.AddTag("ingestion.job_id", job.JobId.ToString());

                var workItem = new DataIngestionWorkItem(request, job);
                await _queue.Writer.WriteAsync(workItem, cancellationToken).ConfigureAwait(false);

                return job.Snapshot;
            }).ConfigureAwait(false);
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
                CleanupWorkingDirectory(workItem.Request.WorkingDirectory);
                continue;
            }

            try
            {
                await ProcessWorkItemAsync(workItem, jobToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                job.MarkCancelled("Cancelled", "Ingestion cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Data ingestion job {JobId} failed.", job.JobId);
                job.MarkFailed("Failed", ex.Message);
            }
            finally
            {
                CleanupWorkingDirectory(workItem.Request.WorkingDirectory);

                if (job.IsTerminal)
                {
                    await RecordJobFinalStateAsync(job, job.Snapshot, stoppingToken).ConfigureAwait(false);
                }
            }
        }
    }

    private async Task RecordJobFinalStateAsync(DataIngestionJob job, DataIngestionJobSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await _jobs.UnregisterAsync(job.JobId, cancellationToken).ConfigureAwait(false);
        job.Dispose();

        await _completedJobs.RecordCompletionAsync(snapshot, cancellationToken).ConfigureAwait(false);
    }

    private async Task ProcessWorkItemAsync(DataIngestionWorkItem workItem, CancellationToken cancellationToken)
    {
        await ActivityScope.ExecuteAsync(
            HonuaTelemetry.Import,
            "DataIngestionService.ProcessWorkItem",
            new[]
            {
                ("ingestion.job_id", (object?)workItem.Job.JobId.ToString()),
                ("ingestion.service_id", (object?)workItem.Request.ServiceId),
                ("ingestion.layer_id", (object?)workItem.Request.LayerId),
                ("ingestion.source_file", (object?)workItem.Request.SourceFileName)
            },
            async activity =>
            {
                var job = workItem.Job;
                var request = workItem.Request;

                job.MarkStarted("Validating source dataset");
                activity?.AddEvent("JobStarted", ("status", "validating"));

                if (!File.Exists(request.SourcePath))
                {
                    throw new FileNotFoundException("Uploaded dataset could not be found for ingestion.", request.SourcePath);
                }

                FeatureContext featureContext;
                try
                {
                    featureContext = await _contextResolver.ResolveAsync(request.ServiceId, request.LayerId, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException)
                {
                    throw new InvalidOperationException(
                        $"Unable to resolve service '{request.ServiceId}' and layer '{request.LayerId}' for ingestion.",
                        ex);
                }

                activity?.AddTag("ingestion.feature_context", featureContext.Layer.Title ?? featureContext.Layer.Id);

                job.UpdateProgress(DataIngestionJobStatus.Validating, "Opening dataset with OGR", 0.05);

        using var dataSource = OpenDataSource(request.SourcePath);
        if (dataSource is null)
        {
            throw new InvalidOperationException("OGR could not open the provided dataset. Ensure the format is supported by the runtime GDAL build.");
        }

        using var layer = dataSource.GetLayerByIndex(0);
        if (layer is null)
        {
            throw new InvalidOperationException("OGR dataset does not contain any feature layers to ingest.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var metadataFieldMap = BuildFieldIndex(layer);
        var targetFields = featureContext.Layer.Fields
            .ToDictionary(field => field.Name, StringComparer.OrdinalIgnoreCase);

        var geometryFieldName = featureContext.Layer.Storage?.GeometryColumn ?? featureContext.Layer.GeometryField;

        var featureCount = layer.GetFeatureCount(force: 1);
        job.UpdateProgress(DataIngestionJobStatus.Importing, "Importing features", 0.05);

        layer.ResetReading();

        long processed = 0;

        // CRITICAL DATA INTEGRITY FIX: Wrap entire import in transaction for all-or-nothing semantics
        // This prevents partial imports that leave the database in an inconsistent state
        IDataStoreTransaction? transaction = null;
        try
        {
            // Begin transaction if enabled (default: true for data integrity)
            if (_options.UseTransactionalIngestion)
            {
                using var timeoutCts = new CancellationTokenSource(_options.TransactionTimeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                transaction = await featureContext.Provider.BeginTransactionAsync(
                    featureContext.DataSource,
                    linkedCts.Token).ConfigureAwait(false);

                if (transaction is null)
                {
                    _logger.LogWarning(
                        "Provider {Provider} does not support transactions. Import will proceed without transaction protection.",
                        featureContext.Provider.Provider);
                }
                else
                {
                    _logger.LogInformation(
                        "Started transactional import for {FeatureCount} features with isolation level {IsolationLevel} and timeout {Timeout}",
                        featureCount,
                        _options.TransactionIsolationLevel,
                        _options.TransactionTimeout);
                }
            }

            // Use bulk insert if enabled and provider supports it
            if (_options.UseBulkInsert && featureContext.Provider.Capabilities.SupportsBulkOperations)
            {
                processed = await ProcessFeaturesBulkAsync(
                    layer,
                    featureContext,
                    metadataFieldMap,
                    targetFields,
                    geometryFieldName,
                    featureCount,
                    job,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // Fallback to individual inserts (legacy path for debugging)
                processed = await ProcessFeaturesIndividualAsync(
                    layer,
                    featureContext,
                    metadataFieldMap,
                    targetFields,
                    geometryFieldName,
                    featureCount,
                    job,
                    transaction,
                    cancellationToken).ConfigureAwait(false);
            }

            // Commit transaction if all features imported successfully
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation(
                    "Successfully committed transaction for {FeatureCount} features in job {JobId}",
                    processed,
                    job.JobId);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Rollback on cancellation
            if (transaction is not null)
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                    _logger.LogWarning(
                        "Rolled back transaction due to cancellation. No features were imported for job {JobId}",
                        job.JobId);
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback transaction after cancellation for job {JobId}", job.JobId);
                }
            }
            throw;
        }
        catch (Exception ex)
        {
            // Rollback on any error
            if (transaction is not null)
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                    _logger.LogError(
                        ex,
                        "Rolled back transaction due to error after importing {ProcessedCount} of {TotalCount} features for job {JobId}",
                        processed,
                        featureCount,
                        job.JobId);
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback transaction after error for job {JobId}", job.JobId);
                }
            }
            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
        }

        await _stacSynchronizer.SynchronizeServiceLayerAsync(request.ServiceId, request.LayerId, cancellationToken).ConfigureAwait(false);

        job.MarkCompleted($"Imported {processed} feature{(processed == 1 ? string.Empty : "s")}");
            });
    }

    private static void EnsureGdalConfigured()
    {
        _ = GdalConfigured.Value;
    }

    private static DataSource? OpenDataSource(string sourcePath)
    {
        var extension = Path.GetExtension(sourcePath);
        if (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            var vsizipPath = $"/vsizip/{sourcePath.Replace("\\", "/")}";
            return Ogr.OpenShared(vsizipPath, 0);
        }

        return Ogr.OpenShared(sourcePath, 0);
    }

    private static OgrFieldIndexMap BuildFieldIndex(Layer layer)
    {
        var map = new Dictionary<string, OgrFieldDescriptor>(StringComparer.OrdinalIgnoreCase);
        using var definition = layer.GetLayerDefn();
        var fieldCount = definition.GetFieldCount();
        for (var i = 0; i < fieldCount; i++)
        {
            var fieldDefn = definition.GetFieldDefn(i);
            try
            {
                map[fieldDefn.GetName()] = new OgrFieldDescriptor(i, fieldDefn.GetFieldType());
            }
            finally
            {
                fieldDefn.Dispose();
            }
        }

        return new OgrFieldIndexMap(map);
    }

    private static object? ExtractFieldValue(Feature feature, FieldType fieldType, int index)
    {
        if (!feature.IsFieldSet(index))
        {
            return null;
        }

        return fieldType switch
        {
            FieldType.OFTInteger => feature.GetFieldAsInteger(index),
            FieldType.OFTInteger64 => feature.GetFieldAsInteger64(index),
            FieldType.OFTReal => feature.GetFieldAsDouble(index),
            FieldType.OFTIntegerList => FormatIntegerList(feature, index),
            FieldType.OFTRealList => FormatDoubleList(feature, index),
            FieldType.OFTStringList => FormatStringList(feature, index),
            FieldType.OFTBinary => FormatBinary(feature, index),
            FieldType.OFTDate or FieldType.OFTTime or FieldType.OFTDateTime => ParseTemporal(feature.GetFieldAsString(index)),
            _ => feature.GetFieldAsString(index)
        };
    }

    private static string FormatIntegerList(Feature feature, int index)
    {
        var values = feature.GetFieldAsIntegerList(index, out var count) ?? Array.Empty<int>();
        if (count <= 0)
        {
            return string.Empty;
        }

        return string.Join(',', values.Take(Math.Min(count, values.Length)));
    }

    private static string FormatDoubleList(Feature feature, int index)
    {
        var values = feature.GetFieldAsDoubleList(index, out var count) ?? Array.Empty<double>();
        if (count <= 0)
        {
            return string.Empty;
        }

        return string.Join(',', values.Take(Math.Min(count, values.Length)));
    }

    private static string FormatStringList(Feature feature, int index)
    {
        var values = feature.GetFieldAsStringList(index) ?? Array.Empty<string>();
        return values.Length == 0 ? string.Empty : string.Join(',', values);
    }

    private static string FormatBinary(Feature feature, int index)
    {
        var value = feature.GetFieldAsString(index);
        return value.IsNullOrEmpty() ? string.Empty : value;
    }

    private static object? ParseTemporal(string? value)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return null;
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
        {
            return dto;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
        {
            return dt;
        }

        return value;
    }

    private async Task<long> ProcessFeaturesBulkAsync(
        Layer layer,
        FeatureContext featureContext,
        OgrFieldIndexMap metadataFieldMap,
        Dictionary<string, FieldDefinition> targetFields,
        string geometryFieldName,
        long featureCount,
        DataIngestionJob job,
        CancellationToken cancellationToken)
    {
        var batchSize = _options.BatchSize;
        var progressInterval = _options.ProgressReportInterval;
        long totalProcessed = 0;

        // Create an async enumerable of feature records to feed to BulkInsertAsync
        async IAsyncEnumerable<FeatureRecord> EnumerateFeatures([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            Feature? feature = null;
            try
            {
                while ((feature = layer.GetNextFeature()) is not null)
                {
                    using var current = feature;
                    ct.ThrowIfCancellationRequested();

                    var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

                    foreach (var targetField in targetFields.Values)
                    {
                        if (string.Equals(targetField.Name, geometryFieldName, StringComparison.OrdinalIgnoreCase))
                        {
                            var geometry = current.GetGeometryRef();
                            attributes[targetField.Name] = geometry is null ? null : geometry.ExportToJson(Array.Empty<string>());
                            continue;
                        }

                        if (!metadataFieldMap.TryGetValue(targetField.Name, out var descriptor))
                        {
                            attributes[targetField.Name] = null;
                            continue;
                        }

                        var index = descriptor.Index;
                        if (!current.IsFieldSet(index))
                        {
                            attributes[targetField.Name] = null;
                            continue;
                        }

                        attributes[targetField.Name] = ExtractFieldValue(current, descriptor.FieldType, index);
                    }

                    totalProcessed++;
                    if (totalProcessed % progressInterval == 0 || featureCount > 0 && totalProcessed == featureCount)
                    {
                        job.ReportProgress(totalProcessed, featureCount, $"Imported {totalProcessed} of {Math.Max(featureCount, totalProcessed)} features");
                    }

                    // Yield asynchronously to allow cancellation checks and avoid blocking
                    await Task.Yield();
                    yield return new FeatureRecord(attributes);
                }
            }
            finally
            {
                feature?.Dispose();
            }
        }

        var inserted = await featureContext.Provider.BulkInsertAsync(
            featureContext.DataSource,
            featureContext.Service,
            featureContext.Layer,
            EnumerateFeatures(cancellationToken),
            cancellationToken).ConfigureAwait(false);

        return inserted;
    }

    private async Task<long> ProcessFeaturesIndividualAsync(
        Layer layer,
        FeatureContext featureContext,
        OgrFieldIndexMap metadataFieldMap,
        Dictionary<string, FieldDefinition> targetFields,
        string geometryFieldName,
        long featureCount,
        DataIngestionJob job,
        IDataStoreTransaction? transaction,
        CancellationToken cancellationToken)
    {
        var progressInterval = _options.ProgressReportInterval;
        long processed = 0;
        Feature? feature = null;

        try
        {
            while ((feature = layer.GetNextFeature()) is not null)
            {
                using var current = feature;
                cancellationToken.ThrowIfCancellationRequested();

                var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

                foreach (var targetField in targetFields.Values)
                {
                    if (string.Equals(targetField.Name, geometryFieldName, StringComparison.OrdinalIgnoreCase))
                    {
                        var geometry = current.GetGeometryRef();
                        attributes[targetField.Name] = geometry is null ? null : geometry.ExportToJson(Array.Empty<string>());
                        continue;
                    }

                    if (!metadataFieldMap.TryGetValue(targetField.Name, out var descriptor))
                    {
                        attributes[targetField.Name] = null;
                        continue;
                    }

                    var index = descriptor.Index;
                    if (!current.IsFieldSet(index))
                    {
                        attributes[targetField.Name] = null;
                        continue;
                    }

                    attributes[targetField.Name] = ExtractFieldValue(current, descriptor.FieldType, index);
                }

                var record = new FeatureRecord(attributes);
                await featureContext.Provider.CreateAsync(
                    featureContext.DataSource,
                    featureContext.Service,
                    featureContext.Layer,
                    record,
                    transaction,
                    cancellationToken).ConfigureAwait(false);

                processed++;
                if (processed % progressInterval == 0 || featureCount > 0 && processed == featureCount)
                {
                    job.ReportProgress(processed, featureCount, $"Imported {processed} of {Math.Max(featureCount, processed)} features");
                }
            }
        }
        finally
        {
            feature?.Dispose();
        }

        return processed;
    }

    private static void CleanupWorkingDirectory(string workingDirectory)
    {
        try
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
            }
        }
        catch
        {
            // Swallow cleanup failures – temp data will be collected by OS policies.
        }
    }

    private readonly record struct DataIngestionWorkItem(DataIngestionRequest Request, DataIngestionJob Job);

    private readonly record struct OgrFieldDescriptor(int Index, FieldType FieldType);

    private sealed record OgrFieldIndexMap(IReadOnlyDictionary<string, OgrFieldDescriptor> IndexMap)
    {
        public bool TryGetValue(string fieldName, out OgrFieldDescriptor descriptor) => IndexMap.TryGetValue(fieldName, out descriptor);
    }
}
