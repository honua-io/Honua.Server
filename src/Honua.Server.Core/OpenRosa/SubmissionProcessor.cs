// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Honua.Server.Core.Editing;
using Honua.Server.Core.Metadata;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Honua.Server.Core.OpenRosa;

/// <summary>
/// Processes OpenRosa submissions from ODK Collect.
/// </summary>
public interface ISubmissionProcessor
{
    Task<SubmissionResult> ProcessAsync(SubmissionRequest request, CancellationToken ct = default);
}

public sealed class SubmissionProcessor : ISubmissionProcessor
{
    private readonly IMetadataRegistry _metadata;
    private readonly IFeatureEditOrchestrator _editOrchestrator;
    private readonly ISubmissionRepository? _submissionRepository;

    public SubmissionProcessor(
        IMetadataRegistry metadata,
        IFeatureEditOrchestrator editOrchestrator,
        ISubmissionRepository? submissionRepository = null)
    {
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        _editOrchestrator = editOrchestrator ?? throw new ArgumentNullException(nameof(editOrchestrator));
        _submissionRepository = submissionRepository;
    }

    public async Task<SubmissionResult> ProcessAsync(SubmissionRequest request, CancellationToken ct = default)
    {
        try
        {
            // 1. Extract metadata from XForm instance
            var root = request.XmlDocument.Root!;
            var instanceId = root.Element("meta")?.Element("instanceID")?.Value;
            var layerId = root.Element("meta")?.Element("layerId")?.Value;
            var serviceId = root.Element("meta")?.Element("serviceId")?.Value;
            var formDate = root.Element("meta")?.Element("formDate")?.Value;

            if (string.IsNullOrWhiteSpace(instanceId))
            {
                return new SubmissionResult
                {
                    Success = false,
                    ErrorMessage = "Missing required field: meta/instanceID",
                    ResultType = SubmissionResultType.Rejected
                };
            }

            if (string.IsNullOrWhiteSpace(layerId) || string.IsNullOrWhiteSpace(serviceId))
            {
                return new SubmissionResult
                {
                    Success = false,
                    ErrorMessage = "Missing required fields: layerId or serviceId",
                    ResultType = SubmissionResultType.Rejected
                };
            }

            // 2. Get layer metadata
            var snapshot = await _metadata.GetSnapshotAsync(ct);
            if (!snapshot.TryGetLayer(serviceId, layerId, out var layer))
            {
                return new SubmissionResult
                {
                    Success = false,
                    ErrorMessage = $"Layer '{serviceId}::{layerId}' not found",
                    ResultType = SubmissionResultType.Rejected
                };
            }

            if (layer.OpenRosa is not { Enabled: true })
            {
                return new SubmissionResult
                {
                    Success = false,
                    ErrorMessage = $"OpenRosa not enabled for layer '{layerId}'",
                    ResultType = SubmissionResultType.Rejected
                };
            }

            // 3. Parse XForm data to feature attributes
            var (geometry, attributes) = ParseXFormInstance(root, layer);

            // 4. Route based on mode
            var mode = layer.OpenRosa.Mode?.ToLowerInvariant() ?? "direct";

            if (mode == "direct")
            {
                // Direct mode: Publish immediately to production layer
                var result = await PublishDirectlyAsync(serviceId, layerId, geometry, attributes, ct);
                return new SubmissionResult
                {
                    Success = true,
                    InstanceId = instanceId,
                    ResultType = SubmissionResultType.DirectPublished
                };
            }
            else if (mode == "staged")
            {
                // Staged mode: Save to staging table for review
                if (_submissionRepository is null)
                {
                    return new SubmissionResult
                    {
                        Success = false,
                        ErrorMessage = "Staged mode requires ISubmissionRepository to be configured",
                        ResultType = SubmissionResultType.Rejected
                    };
                }

                var submission = new Submission
                {
                    Id = Guid.NewGuid().ToString(),
                    InstanceId = instanceId,
                    FormId = layer.OpenRosa.FormId ?? $"{serviceId}_{layerId}",
                    FormVersion = layer.OpenRosa.FormVersion,
                    LayerId = layerId,
                    ServiceId = serviceId,
                    SubmittedBy = request.SubmittedBy,
                    SubmittedAt = DateTimeOffset.UtcNow,
                    DeviceId = request.DeviceId,
                    Status = SubmissionStatus.Pending,
                    XmlData = request.XmlDocument,
                    Geometry = geometry,
                    Attributes = attributes,
                    Attachments = request.Attachments.Select(a => new SubmissionAttachment
                    {
                        Filename = a.Filename,
                        ContentType = a.ContentType,
                        SizeBytes = a.SizeBytes,
                        StoragePath = $"openrosa/{instanceId}/{a.Filename}"
                    }).ToList()
                };

                await _submissionRepository.CreateAsync(submission, ct);

                return new SubmissionResult
                {
                    Success = true,
                    InstanceId = instanceId,
                    ResultType = SubmissionResultType.StagedForReview
                };
            }
            else
            {
                return new SubmissionResult
                {
                    Success = false,
                    ErrorMessage = $"Unknown OpenRosa mode: '{mode}'. Expected 'direct' or 'staged'.",
                    ResultType = SubmissionResultType.Rejected
                };
            }
        }
        catch (Exception ex)
        {
            return new SubmissionResult
            {
                Success = false,
                ErrorMessage = $"Submission processing failed: {ex.Message}",
                ResultType = SubmissionResultType.Rejected
            };
        }
    }

    private (Geometry? geometry, IReadOnlyDictionary<string, object?> attributes) ParseXFormInstance(
        XElement root,
        LayerDefinition layer)
    {
        Geometry? geometry = null;
        var attributes = new Dictionary<string, object?>();

        foreach (var element in root.Elements())
        {
            var elementName = element.Name.LocalName;

            // Skip meta element
            if (elementName == "meta")
                continue;

            // Check if this is the geometry field
            if (string.Equals(elementName, layer.GeometryField, StringComparison.OrdinalIgnoreCase))
            {
                geometry = ParseGeometry(element.Value, layer.GeometryType);
                continue;
            }

            // Find matching field definition
            var field = layer.Fields.FirstOrDefault(f =>
                string.Equals(f.Name, elementName, StringComparison.OrdinalIgnoreCase));

            if (field is null)
                continue;

            // Parse value according to field data type
            var value = ParseFieldValue(element.Value, field.DataType);
            attributes[field.Name] = value;
        }

        return (geometry, attributes);
    }

    private Geometry? ParseGeometry(string odkGeometry, string geometryType)
    {
        if (string.IsNullOrWhiteSpace(odkGeometry))
            return null;

        try
        {
            var type = geometryType.ToLowerInvariant();

            if (type == "point" || type == "multipoint")
            {
                // ODK geopoint format: "lat lon altitude accuracy"
                var parts = odkGeometry.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 &&
                    double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) &&
                    double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
                {
                    return new Point(lon, lat) { SRID = 4326 };
                }
            }
            else if (type == "linestring" || type == "multilinestring")
            {
                // ODK geotrace format: "lat1 lon1 alt1 acc1;lat2 lon2 alt2 acc2;..."
                var coords = odkGeometry.Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(point =>
                    {
                        var parts = point.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2 &&
                            double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) &&
                            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
                        {
                            return new Coordinate(lon, lat);
                        }
                        return null;
                    })
                    .Where(c => c is not null)
                    .ToArray();

                if (coords.Length >= 2)
                {
                    return new LineString(coords!) { SRID = 4326 };
                }
            }
            else if (type == "polygon" || type == "multipolygon")
            {
                // ODK geoshape format: same as geotrace but forms a closed ring
                var coords = odkGeometry.Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(point =>
                    {
                        var parts = point.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2 &&
                            double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) &&
                            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
                        {
                            return new Coordinate(lon, lat);
                        }
                        return null;
                    })
                    .Where(c => c is not null)
                    .ToArray();

                if (coords.Length >= 3)
                {
                    // Ensure ring is closed
                    if (!coords.First()!.Equals2D(coords.Last()!))
                    {
                        coords = coords.Concat(new[] { coords.First() }).ToArray();
                    }

                    var ring = new LinearRing(coords!);
                    return new Polygon(ring) { SRID = 4326 };
                }
            }
        }
        catch
        {
            // Geometry parsing failed, return null
        }

        return null;
    }

    private object? ParseFieldValue(string? value, string? dataType)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var type = (dataType ?? "string").ToLowerInvariant();

        return type switch
        {
            "int" or "integer" or "int32" => int.TryParse(value, out var i) ? i : null,
            "int64" or "long" => long.TryParse(value, out var l) ? l : null,
            "double" or "float" => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null,
            "decimal" or "number" => decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var dec) ? dec : null,
            "bool" or "boolean" => bool.TryParse(value, out var b) ? b : null,
            "date" => DateOnly.TryParse(value, out var date) ? date : null,
            "datetime" or "timestamp" => DateTimeOffset.TryParse(value, out var dt) ? dt : null,
            _ => value
        };
    }

    private async Task<FeatureEditBatchResult> PublishDirectlyAsync(
        string serviceId,
        string layerId,
        Geometry? geometry,
        IReadOnlyDictionary<string, object?> attributes,
        CancellationToken ct)
    {
        // Add geometry to attributes for editing
        var allAttributes = new Dictionary<string, object?>(attributes);
        if (geometry is not null)
        {
            allAttributes["geometry"] = geometry;
        }

        var command = new AddFeatureCommand(serviceId, layerId, allAttributes);
        var batch = new FeatureEditBatch(
            commands: new[] { command },
            rollbackOnFailure: false,
            isAuthenticated: true,
            userRoles: new[] { "DataPublisher" }
        );

        return await _editOrchestrator.ExecuteAsync(batch, ct);
    }
}

/// <summary>
/// Repository for managing staged submissions (only needed for "staged" mode).
/// </summary>
public interface ISubmissionRepository
{
    Task CreateAsync(Submission submission, CancellationToken ct = default);
    Task<Submission?> GetAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<Submission>> GetPendingAsync(string? layerId = null, CancellationToken ct = default);
    Task UpdateAsync(Submission submission, CancellationToken ct = default);
}
