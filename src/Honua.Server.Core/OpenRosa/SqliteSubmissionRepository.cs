// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Data.Sqlite;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Newtonsoft.Json;

namespace Honua.Server.Core.OpenRosa;

/// <summary>
/// SQLite-based repository for staged OpenRosa submissions.
/// </summary>
public class SqliteSubmissionRepository : ISubmissionRepository
{
    private readonly string _connectionString;
    private readonly WKTWriter _wktWriter = new();
    private readonly WKTReader _wktReader = new();
    private Task? _initializationTask;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public SqliteSubmissionRepository(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        // NOTE: Initialization moved to lazy async pattern to avoid constructor blocking.
        // Each public method calls EnsureInitializedAsync() before operation.
    }

    private async ValueTask EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initializationTask?.IsCompleted == true)
            return;

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initializationTask is null)
            {
                _initializationTask = EnsureSchemaAsync();
            }
            await _initializationTask;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task CreateAsync(Submission submission, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        var sql = @"
            INSERT INTO openrosa_submissions (
                id, instance_id, form_id, form_version, layer_id, service_id,
                submitted_by, submitted_at, device_id, status,
                reviewed_by, reviewed_at, review_notes,
                xml_data, geometry_wkt, geometry_srid, attributes_json, attachments_json
            ) VALUES (
                @id, @instance_id, @form_id, @form_version, @layer_id, @service_id,
                @submitted_by, @submitted_at, @device_id, @status,
                @reviewed_by, @reviewed_at, @review_notes,
                @xml_data, @geometry_wkt, @geometry_srid, @attributes_json, @attachments_json
            )";

        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@id", submission.Id);
        cmd.Parameters.AddWithValue("@instance_id", submission.InstanceId);
        cmd.Parameters.AddWithValue("@form_id", submission.FormId);
        cmd.Parameters.AddWithValue("@form_version", submission.FormVersion);
        cmd.Parameters.AddWithValue("@layer_id", submission.LayerId);
        cmd.Parameters.AddWithValue("@service_id", submission.ServiceId);
        cmd.Parameters.AddWithValue("@submitted_by", submission.SubmittedBy);
        cmd.Parameters.AddWithValue("@submitted_at", submission.SubmittedAt.ToString("o"));
        cmd.Parameters.AddWithValue("@device_id", (object?)submission.DeviceId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@status", submission.Status.ToString());
        cmd.Parameters.AddWithValue("@reviewed_by", (object?)submission.ReviewedBy ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@reviewed_at", submission.ReviewedAt?.ToString("o") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@review_notes", (object?)submission.ReviewNotes ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@xml_data", submission.XmlData.ToString());
        cmd.Parameters.AddWithValue("@geometry_wkt", submission.Geometry != null ? _wktWriter.Write(submission.Geometry) : DBNull.Value);
        cmd.Parameters.AddWithValue("@geometry_srid", submission.Geometry?.SRID ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@attributes_json", JsonConvert.SerializeObject(submission.Attributes));
        cmd.Parameters.AddWithValue("@attachments_json", JsonConvert.SerializeObject(submission.Attachments));

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task<Submission?> GetAsync(string id, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        // Explicitly select all columns to avoid SELECT * bandwidth waste
        var sql = @"SELECT id, instance_id, form_id, form_version, layer_id, service_id,
            submitted_by, submitted_at, device_id, status,
            reviewed_by, reviewed_at, review_notes,
            xml_data, geometry_wkt, geometry_srid, attributes_json, attachments_json
            FROM openrosa_submissions WHERE id = @id";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (await reader.ReadAsync(ct))
        {
            return MapSubmission(reader);
        }

        return null;
    }

    public async Task<IReadOnlyList<Submission>> GetPendingAsync(string? layerId = null, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        // Explicitly select all columns to avoid SELECT * bandwidth waste
        var sql = @"SELECT id, instance_id, form_id, form_version, layer_id, service_id,
            submitted_by, submitted_at, device_id, status,
            reviewed_by, reviewed_at, review_notes,
            xml_data, geometry_wkt, geometry_srid, attributes_json, attachments_json
            FROM openrosa_submissions WHERE status = @status";
        if (layerId is not null)
        {
            sql += " AND layer_id = @layer_id";
        }
        sql += " ORDER BY submitted_at DESC";

        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@status", SubmissionStatus.Pending.ToString());
        if (layerId is not null)
        {
            cmd.Parameters.AddWithValue("@layer_id", layerId);
        }

        var results = new List<Submission>();
        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct))
        {
            results.Add(MapSubmission(reader));
        }

        return results;
    }

    public async Task UpdateAsync(Submission submission, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        var sql = @"
            UPDATE openrosa_submissions SET
                status = @status,
                reviewed_by = @reviewed_by,
                reviewed_at = @reviewed_at,
                review_notes = @review_notes
            WHERE id = @id";

        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@id", submission.Id);
        cmd.Parameters.AddWithValue("@status", submission.Status.ToString());
        cmd.Parameters.AddWithValue("@reviewed_by", (object?)submission.ReviewedBy ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@reviewed_at", submission.ReviewedAt?.ToString("o") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@review_notes", (object?)submission.ReviewNotes ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private Submission MapSubmission(IDataReader reader)
    {
        var geometryWkt = reader["geometry_wkt"] as string;
        var geometry = geometryWkt != null ? _wktReader.Read(geometryWkt) : null;

        // Restore SRID if geometry exists and SRID was stored
        if (geometry != null && reader["geometry_srid"] is long sridValue)
        {
            geometry.SRID = (int)sridValue;
        }

        var attributesJson = reader["attributes_json"] as string ?? "{}";
        var attributes = JsonConvert.DeserializeObject<Dictionary<string, object?>>(attributesJson)
            ?? new Dictionary<string, object?>();

        var attachmentsJson = reader["attachments_json"] as string ?? "[]";
        var attachments = JsonConvert.DeserializeObject<List<SubmissionAttachment>>(attachmentsJson)
            ?? new List<SubmissionAttachment>();

        var xmlData = XDocument.Parse(reader["xml_data"] as string ?? "<data/>");

        var reviewedAtStr = reader["reviewed_at"] as string;
        DateTimeOffset? reviewedAt = reviewedAtStr != null ? DateTimeOffset.Parse(reviewedAtStr) : null;

        return new Submission
        {
            Id = reader["id"] as string ?? string.Empty,
            InstanceId = reader["instance_id"] as string ?? string.Empty,
            FormId = reader["form_id"] as string ?? string.Empty,
            FormVersion = reader["form_version"] as string ?? string.Empty,
            LayerId = reader["layer_id"] as string ?? string.Empty,
            ServiceId = reader["service_id"] as string ?? string.Empty,
            SubmittedBy = reader["submitted_by"] as string ?? string.Empty,
            SubmittedAt = DateTimeOffset.Parse(reader["submitted_at"] as string ?? DateTimeOffset.UtcNow.ToString("o")),
            DeviceId = reader["device_id"] as string,
            Status = Enum.Parse<SubmissionStatus>(reader["status"] as string ?? "Pending"),
            ReviewedBy = reader["reviewed_by"] as string,
            ReviewedAt = reviewedAt,
            ReviewNotes = reader["review_notes"] as string,
            XmlData = xmlData,
            Geometry = geometry,
            Attributes = attributes,
            Attachments = attachments
        };
    }

    private async Task EnsureSchemaAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);

        var sql = @"
            CREATE TABLE IF NOT EXISTS openrosa_submissions (
                id TEXT PRIMARY KEY,
                instance_id TEXT NOT NULL UNIQUE,
                form_id TEXT NOT NULL,
                form_version TEXT NOT NULL,
                layer_id TEXT NOT NULL,
                service_id TEXT NOT NULL,
                submitted_by TEXT NOT NULL,
                submitted_at TEXT NOT NULL,
                device_id TEXT,
                status TEXT NOT NULL,
                reviewed_by TEXT,
                reviewed_at TEXT,
                review_notes TEXT,
                xml_data TEXT NOT NULL,
                geometry_wkt TEXT,
                geometry_srid INTEGER,
                attributes_json TEXT NOT NULL,
                attachments_json TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_submissions_status
                ON openrosa_submissions(status);
            CREATE INDEX IF NOT EXISTS idx_submissions_layer
                ON openrosa_submissions(layer_id);
            CREATE INDEX IF NOT EXISTS idx_submissions_submitted_at
                ON openrosa_submissions(submitted_at);
        ";

        using var cmd = new SqliteCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}
