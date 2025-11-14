// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Buffers;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Performance;
using Honua.Server.Core.Utilities;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetTopologySuite;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Core.Export;

public interface IGeoPackageExporter
{
    Task<GeoPackageExportResult> ExportAsync(
        LayerDefinition layer,
        FeatureQuery query,
        string contentCrs,
        IAsyncEnumerable<FeatureRecord> records,
        CancellationToken cancellationToken = default);
}

public sealed record GeoPackageExportResult(Stream Content, string FileName, long FeatureCount);

public sealed class GeoPackageExporter : IGeoPackageExporter
{
    private const string DefaultGeometryColumnName = "geom";
    private static readonly Regex IdentifierRegex = new("[A-Za-z0-9_]+", RegexOptions.Compiled);

    private readonly ILogger<GeoPackageExporter> _logger;
    private readonly GeoPackageExportOptions _options;

    public GeoPackageExporter(ILogger<GeoPackageExporter> logger)
        : this(logger, GeoPackageExportOptions.Default)
    {
    }

    public GeoPackageExporter(ILogger<GeoPackageExporter> logger, IOptions<GeoPackageExportOptions> options)
        : this(logger, options?.Value ?? GeoPackageExportOptions.Default)
    {
    }

    public GeoPackageExporter(ILogger<GeoPackageExporter> logger, GeoPackageExportOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Validate() ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<GeoPackageExportResult> ExportAsync(
        LayerDefinition layer,
        FeatureQuery query,
        string contentCrs,
        IAsyncEnumerable<FeatureRecord> records,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(layer);
        Guard.NotNull(records);

        var srid = CrsHelper.ParseCrs(contentCrs);
        var tableName = SanitizeIdentifier(layer.Storage?.Table ?? layer.Id);
        var fileName = SanitizeFileName(layer.Id) + ".gpkg";
        var tempPath = Path.Combine(Path.GetTempPath(), $"honua-gpkg-{Guid.NewGuid():N}.gpkg");

        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = tempPath,
            Pooling = false
        };

        var connectionString = builder.ConnectionString;
        var featureCount = 0L;
        var connection = new SqliteConnection(connectionString);

        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            await InitializeGeoPackageAsync(connection, srid, cancellationToken).ConfigureAwait(false);

            var columns = BuildColumnDefinitions(layer);
            await CreateFeatureTableAsync(connection, tableName, layer, srid, columns, cancellationToken).ConfigureAwait(false);

            var geometryColumn = layer.GeometryField.IsNullOrWhiteSpace() ? DefaultGeometryColumnName : layer.GeometryField;

            var geoJsonReader = new GeoJsonReader();
            var wkbWriter = new WKBWriter(ByteOrder.LittleEndian);
            var envelope = new Envelope();
            var hasEnvelope = false;

            // Use batched transactions to avoid holding connection for too long
            var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var batchCount = 0;
            var batchSize = _options.BatchSize;

            try
            {
                using var insertBinder = CreateInsertCommandBinder(connection, transaction, tableName, columns, geometryColumn);

                await foreach (var record in records.WithCancellation(cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    featureCount++;
                    var maxFeatures = _options.MaxFeatures;
                    if (maxFeatures.HasValue && maxFeatures.Value > 0 && featureCount > maxFeatures.Value)
                    {
                        throw new InvalidOperationException(
                            $"Export exceeded configured maximum of {maxFeatures.Value:N0} records. " +
                            "Adjust GeoPackageExportOptions.MaxFeatures or apply stronger query filters.");
                    }

                    var geometry = TryReadGeometry(record, layer.GeometryField, geoJsonReader);
                    if (geometry is not null)
                    {
                        geometry.SRID = srid;
                        if (!geometry.IsEmpty)
                        {
                            envelope.ExpandToInclude(geometry.EnvelopeInternal);
                            hasEnvelope = true;
                        }
                    }

                    var attributes = ExtractAttributes(record, layer.GeometryField);
                    await insertBinder.InsertAsync(attributes, geometry, wkbWriter, srid, cancellationToken).ConfigureAwait(false);

                    batchCount++;

                    // Commit batch periodically to avoid holding transaction too long
                    if (batchCount >= batchSize)
                    {
                        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                        await transaction.DisposeAsync().ConfigureAwait(false);

                        // Start new transaction for next batch
                        transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                        insertBinder.UpdateTransaction(transaction);
                        batchCount = 0;

                        // Yield to other operations periodically
                        await Task.Yield();
                    }
                }

                // Commit final batch
                if (batchCount > 0)
                {
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                }

                _logger.LogInformation("GeoPackage export completed: {Count} features", featureCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GeoPackage export failed after {Count} features", featureCount);

                // Rollback current transaction
                try
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // Ignore rollback errors
                }

                throw new InvalidOperationException(
                    $"Export failed after processing {featureCount} features: {ex.Message}", ex);
            }
            finally
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }

            await UpdateContentsEnvelopeAsync(connection, tableName, srid, hasEnvelope ? envelope : null, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }
            catch (SqliteException)
            {
            }

            SqliteConnection.ClearPool(connection);
            await connection.DisposeAsync().ConfigureAwait(false);
        }

        var stream = new FileStream(tempPath, new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            Options = FileOptions.Asynchronous | FileOptions.DeleteOnClose
        });

        return new GeoPackageExportResult(stream, fileName, featureCount);
    }

    private static async Task InitializeGeoPackageAsync(SqliteConnection connection, int srid, CancellationToken cancellationToken)
    {
        await ExecuteAsync(connection, "PRAGMA application_id = 0x47504B47;", cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(connection, "PRAGMA user_version = 10200;", cancellationToken).ConfigureAwait(false);

        const string spatialRefSysSql = @"
            CREATE TABLE IF NOT EXISTS gpkg_spatial_ref_sys (
                srs_name TEXT NOT NULL,
                srs_id INTEGER NOT NULL PRIMARY KEY,
                organization TEXT NOT NULL,
                organization_coordsys_id INTEGER NOT NULL,
                definition TEXT NOT NULL,
                description TEXT
            );";
        await ExecuteAsync(connection, spatialRefSysSql, cancellationToken).ConfigureAwait(false);

        const string contentsSql = @"
            CREATE TABLE IF NOT EXISTS gpkg_contents (
                table_name TEXT NOT NULL PRIMARY KEY,
                data_type TEXT NOT NULL,
                identifier TEXT,
                description TEXT DEFAULT '',
                last_change DATETIME DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
                min_x DOUBLE,
                min_y DOUBLE,
                max_x DOUBLE,
                max_y DOUBLE,
                srs_id INTEGER,
                CONSTRAINT fk_gc_r_srs_id FOREIGN KEY (srs_id) REFERENCES gpkg_spatial_ref_sys(srs_id)
            );";
        await ExecuteAsync(connection, contentsSql, cancellationToken).ConfigureAwait(false);

        const string geometryColumnsSql = @"
            CREATE TABLE IF NOT EXISTS gpkg_geometry_columns (
                table_name TEXT NOT NULL,
                column_name TEXT NOT NULL,
                geometry_type_name TEXT NOT NULL,
                srs_id INTEGER NOT NULL,
                z TINYINT NOT NULL,
                m TINYINT NOT NULL,
                PRIMARY KEY (table_name, column_name),
                CONSTRAINT fk_gc_tn FOREIGN KEY (table_name) REFERENCES gpkg_contents(table_name),
                CONSTRAINT fk_gc_srs FOREIGN KEY (srs_id) REFERENCES gpkg_spatial_ref_sys(srs_id)
            );";
        await ExecuteAsync(connection, geometryColumnsSql, cancellationToken).ConfigureAwait(false);

        const string metadataSql = @"
            CREATE TABLE IF NOT EXISTS gpkg_metadata (
                id INTEGER CONSTRAINT gpkg_metadata_pk PRIMARY KEY ASC NOT NULL,
                md_scope TEXT NOT NULL DEFAULT 'dataset',
                md_standard_uri TEXT NOT NULL,
                mime_type TEXT NOT NULL DEFAULT 'text/xml',
                metadata TEXT NOT NULL
            );";
        await ExecuteAsync(connection, metadataSql, cancellationToken).ConfigureAwait(false);

        await EnsureSpatialReferenceAsync(connection, srid, cancellationToken).ConfigureAwait(false);
        await EnsureSpatialReferenceAsync(connection, 4326, cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureSpatialReferenceAsync(SqliteConnection connection, int srid, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM gpkg_spatial_ref_sys WHERE srs_id = @srid";
        command.Parameters.AddWithValue("@srid", srid);
        var exists = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) > 0;
        if (exists)
        {
            return;
        }

        string srsName;
        string organization;
        int organizationId;
        string definition;
        string description;

        if (srid == 4326)
        {
            srsName = "WGS 84";
            organization = "EPSG";
            organizationId = 4326;
            definition = "GEOGCS['WGS 84',DATUM['WGS_1984',SPHEROID['WGS 84',6378137,298.257223563,AUTHORITY['EPSG','7030']],AUTHORITY['EPSG','6326']],PRIMEM['Greenwich',0,AUTHORITY['EPSG','8901']],UNIT['degree',0.0174532925199433,AUTHORITY['EPSG','9122']],AUTHORITY['EPSG','4326']]";
            description = "longitude/latitude coordinates in decimal degrees on the WGS 84 spheroid";
        }
        else
        {
            srsName = $"SRID {srid}";
            organization = "EPSG";
            organizationId = srid;
            definition = $"EPSG:{srid}";
            description = "Imported spatial reference";
        }

        await using var insert = connection.CreateCommand();
        insert.CommandText = @"INSERT INTO gpkg_spatial_ref_sys (srs_name, srs_id, organization, organization_coordsys_id, definition, description)
                               VALUES (@name, @id, @org, @orgId, @definition, @description)";
        insert.Parameters.AddWithValue("@name", srsName);
        insert.Parameters.AddWithValue("@id", srid);
        insert.Parameters.AddWithValue("@org", organization);
        insert.Parameters.AddWithValue("@orgId", organizationId);
        insert.Parameters.AddWithValue("@definition", definition);
        insert.Parameters.AddWithValue("@description", description);
        await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<ColumnDefinition> BuildColumnDefinitions(LayerDefinition layer)
    {
        var columns = new List<ColumnDefinition>();
        var keyName = layer.IdField.IsNullOrWhiteSpace() ? "fid" : layer.IdField;
        columns.Add(new ColumnDefinition(keyName, ResolveSqlType("int64", layer.Fields.FirstOrDefault(f => string.Equals(f.Name, keyName, StringComparison.OrdinalIgnoreCase))), true, false));

        foreach (var field in layer.Fields)
        {
            if (string.Equals(field.Name, layer.GeometryField, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(field.Name, keyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var sqlType = ResolveSqlType(field.DataType, field);
            if (sqlType.IsNullOrWhiteSpace())
            {
                continue;
            }

            columns.Add(new ColumnDefinition(field.Name, sqlType, false, field.Nullable));
        }

        return columns;
    }

    private static async Task CreateFeatureTableAsync(
        SqliteConnection connection,
        string tableName,
        LayerDefinition layer,
        int srid,
        IReadOnlyList<ColumnDefinition> columns,
        CancellationToken cancellationToken)
    {
        var columnDefinitions = string.Join(", ", columns.Select(c => $"{QuoteIdentifier(c.Name)} {c.SqlType}{(c.IsPrimaryKey ? " PRIMARY KEY" : string.Empty)}{(c.IsNullable ? string.Empty : " NOT NULL")}"));
        var geometryColumn = layer.GeometryField.IsNullOrWhiteSpace() ? DefaultGeometryColumnName : layer.GeometryField;

        var createTableSql = $"CREATE TABLE IF NOT EXISTS {QuoteIdentifier(tableName)} ({columnDefinitions}, {QuoteIdentifier(geometryColumn)} BLOB)";
        await ExecuteAsync(connection, createTableSql, cancellationToken).ConfigureAwait(false);

        await EnsureContentsRowAsync(connection, tableName, layer.Title ?? layer.Id, srid, cancellationToken).ConfigureAwait(false);
        await EnsureGeometryColumnsRowAsync(connection, tableName, geometryColumn, layer.GeometryType ?? "GEOMETRY", srid, cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureContentsRowAsync(SqliteConnection connection, string tableName, string identifier, int srid, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM gpkg_contents WHERE table_name = @name";
        command.Parameters.AddWithValue("@name", tableName);
        var exists = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) > 0;
        if (exists)
        {
            return;
        }

        await using var insert = connection.CreateCommand();
        insert.CommandText = @"INSERT INTO gpkg_contents (table_name, data_type, identifier, description, last_change, srs_id)
                               VALUES (@table, 'features', @identifier, '', strftime('%Y-%m-%dT%H:%M:%fZ','now'), @srid)";
        insert.Parameters.AddWithValue("@table", tableName);
        insert.Parameters.AddWithValue("@identifier", identifier ?? tableName);
        insert.Parameters.AddWithValue("@srid", srid);
        await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureGeometryColumnsRowAsync(SqliteConnection connection, string tableName, string columnName, string geometryType, int srid, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM gpkg_geometry_columns WHERE table_name = @table AND column_name = @column";
        command.Parameters.AddWithValue("@table", tableName);
        command.Parameters.AddWithValue("@column", columnName);
        var exists = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) > 0;
        if (exists)
        {
            return;
        }

        await using var insert = connection.CreateCommand();
        insert.CommandText = @"INSERT INTO gpkg_geometry_columns (table_name, column_name, geometry_type_name, srs_id, z, m)
                               VALUES (@table, @column, @geomType, @srid, 0, 0)";
        insert.Parameters.AddWithValue("@table", tableName);
        insert.Parameters.AddWithValue("@column", columnName);
        insert.Parameters.AddWithValue("@geomType", NormalizeGeometryType(geometryType));
        insert.Parameters.AddWithValue("@srid", srid);
        await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static InsertCommandBinder CreateInsertCommandBinder(
        SqliteConnection connection,
        DbTransaction transaction,
        string tableName,
        IReadOnlyList<ColumnDefinition> columns,
        string geometryColumn)
    {
        var command = connection.CreateCommand();
        if (transaction is not SqliteTransaction sqliteTransaction)
        {
            throw new InvalidOperationException("GeoPackage export requires a SqliteTransaction.");
        }

        command.Transaction = sqliteTransaction;

        var columnNames = new List<string>();
        var parameterNames = new List<string>();
        var parameterLookup = new Dictionary<string, SqliteParameter>(StringComparer.OrdinalIgnoreCase);
        var counters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var ordinal = 0;

        foreach (var column in columns)
        {
            var parameterName = CreateParameterName(column.Name, counters, ordinal++);
            var parameter = command.CreateParameter();
            parameter.ParameterName = parameterName;
            command.Parameters.Add(parameter);

            columnNames.Add(QuoteIdentifier(column.Name));
            parameterNames.Add(parameterName);
            parameterLookup[column.Name] = parameter;
        }

        var geometryParameter = command.CreateParameter();
        geometryParameter.ParameterName = "@geom";
        command.Parameters.Add(geometryParameter);

        columnNames.Add(QuoteIdentifier(geometryColumn));
        parameterNames.Add(geometryParameter.ParameterName);

        command.CommandText = $"INSERT INTO {QuoteIdentifier(tableName)} ({string.Join(", ", columnNames)}) VALUES ({string.Join(", ", parameterNames)})";

        return new InsertCommandBinder(command, parameterLookup, geometryParameter);
    }

    private static string CreateParameterName(string columnName, IDictionary<string, int> counters, int ordinal)
    {
        var builder = new StringBuilder();
        foreach (var ch in columnName)
        {
            if (char.IsLetterOrDigit(ch) || ch == '_')
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }

        if (builder.Length == 0)
        {
            builder.Append($"p{ordinal}");
        }

        if (!char.IsLetter(builder[0]) && builder[0] != '_')
        {
            builder.Insert(0, 'p');
        }

        var core = builder.ToString();
        if (counters.TryGetValue(core, out var count))
        {
            counters[core] = count + 1;
            core = $"{core}_{count + 1}";
        }
        else
        {
            counters[core] = 0;
        }

        return "@" + core;
    }

    private static async Task UpdateContentsEnvelopeAsync(SqliteConnection connection, string tableName, int srid, Envelope? envelope, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        if (envelope is null || envelope.IsNull)
        {
            command.CommandText = "UPDATE gpkg_contents SET min_x = NULL, min_y = NULL, max_x = NULL, max_y = NULL, srs_id = @srid WHERE table_name = @table";
            command.Parameters.AddWithValue("@table", tableName);
            command.Parameters.AddWithValue("@srid", srid);
        }
        else
        {
            command.CommandText = @"UPDATE gpkg_contents
                                    SET min_x = @minx, min_y = @miny, max_x = @maxx, max_y = @maxy, srs_id = @srid
                                    WHERE table_name = @table";
            command.Parameters.AddWithValue("@minx", envelope.MinX);
            command.Parameters.AddWithValue("@miny", envelope.MinY);
            command.Parameters.AddWithValue("@maxx", envelope.MaxX);
            command.Parameters.AddWithValue("@maxy", envelope.MaxY);
            command.Parameters.AddWithValue("@table", tableName);
            command.Parameters.AddWithValue("@srid", srid);
        }

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Geometry? TryReadGeometry(FeatureRecord record, string? geometryField, GeoJsonReader reader)
    {
        if (geometryField.IsNullOrWhiteSpace())
        {
            geometryField = DefaultGeometryColumnName;
        }

        if (!record.Attributes.TryGetValue(geometryField, out var value) || value is null)
        {
            return null;
        }

        try
        {
            return value switch
            {
                Geometry geometry => geometry,
                JsonNode node => reader.Read<Geometry>(node.ToJsonString()),
                JsonElement element when element.ValueKind == JsonValueKind.String => reader.Read<Geometry>(element.GetString()!),
                JsonElement element => reader.Read<Geometry>(element.GetRawText()),
                string text => reader.Read<Geometry>(text),
                _ => reader.Read<Geometry>(value.ToString()!)
            };
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyDictionary<string, object?> ExtractAttributes(FeatureRecord record, string? geometryField)
    {
        var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in record.Attributes)
        {
            if (geometryField.HasValue() && string.Equals(pair.Key, geometryField, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            attributes[pair.Key] = NormalizeValue(pair.Value);
        }

        return attributes;
    }

    private static object? NormalizeValue(object? value)
    {
        if (value is null)
        {
            return null;
        }

        return value switch
        {
            JsonElement element => ConvertJsonElement(element),
            JsonNode node => ConvertJsonElement(node.GetValue<JsonElement>()),
            bool boolean => boolean ? 1 : 0,
            DateTime dateTime => dateTime.Kind == DateTimeKind.Utc ? dateTime.ToString("O", CultureInfo.InvariantCulture) : dateTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset offset => offset.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            _ => value
        };
    }

    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.True => 1,
            JsonValueKind.False => 0,
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number when element.TryGetDouble(out var d) => d,
            JsonValueKind.String when element.TryGetDateTimeOffset(out var dto) => dto.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            JsonValueKind.String => element.GetString(),
            _ => element.GetRawText()
        };
    }

    private static byte[]? BuildGeoPackageGeometry(Geometry? geometry, WKBWriter writer, int srid)
    {
        if (geometry is null)
        {
            return null;
        }

        var wkb = writer.Write(geometry);
        var envelope = geometry.EnvelopeInternal;
        var headerLength = 8;
        var envelopeLength = envelope?.IsNull == false ? 32 : 0;
        var totalLength = headerLength + envelopeLength + wkb.Length;

        // Use ArrayPool for large geometries to reduce GC pressure (hot path - called per feature)
        // For small geometries (< 4KB), the overhead of pooling isn't worth it
        const int poolingThreshold = 4096;
        byte[]? pooledBuffer = null;
        byte[] buffer;

        if (totalLength >= poolingThreshold)
        {
            pooledBuffer = ObjectPools.ByteArrayPool.Rent(totalLength);
            buffer = pooledBuffer;
        }
        else
        {
            buffer = new byte[totalLength];
        }

        try
        {
            buffer[0] = (byte)'G';
            buffer[1] = (byte)'P';
            buffer[2] = 0;

            byte flags = 0;
            if (BitConverter.IsLittleEndian)
            {
                flags |= 1;
            }

            if (envelopeLength > 0)
            {
                flags |= (byte)(1 << 1);
            }

            buffer[3] = flags;
            BitConverter.GetBytes(srid).CopyTo(buffer, 4);

            var offset = headerLength;
            if (envelopeLength > 0 && envelope is not null)
            {
                BitConverter.GetBytes(envelope.MinX).CopyTo(buffer, offset);
                offset += 8;
                BitConverter.GetBytes(envelope.MinY).CopyTo(buffer, offset);
                offset += 8;
                BitConverter.GetBytes(envelope.MaxX).CopyTo(buffer, offset);
                offset += 8;
                BitConverter.GetBytes(envelope.MaxY).CopyTo(buffer, offset);
                offset += 8;
            }

            Buffer.BlockCopy(wkb, 0, buffer, offset, wkb.Length);

            // If using pooled buffer, create exact-sized result and return pool buffer
            if (pooledBuffer is not null)
            {
                var result = new byte[totalLength];
                Buffer.BlockCopy(buffer, 0, result, 0, totalLength);
                return result;
            }

            return buffer;
        }
        finally
        {
            if (pooledBuffer is not null)
            {
                ObjectPools.ByteArrayPool.Return(pooledBuffer);
            }
        }
    }

    private sealed class InsertCommandBinder : IDisposable
    {
        private readonly SqliteCommand _command;
        private readonly IReadOnlyDictionary<string, SqliteParameter> _parameters;
        private readonly SqliteParameter _geometryParameter;

        public InsertCommandBinder(
            SqliteCommand command,
            IReadOnlyDictionary<string, SqliteParameter> parameters,
            SqliteParameter geometryParameter)
        {
            _command = command;
            _parameters = parameters;
            _geometryParameter = geometryParameter;
        }

        public void UpdateTransaction(DbTransaction transaction)
        {
            if (transaction is not SqliteTransaction sqliteTransaction)
            {
                throw new InvalidOperationException("Transaction must be a SqliteTransaction.");
            }

            _command.Transaction = sqliteTransaction;
        }

        public async Task InsertAsync(
            IReadOnlyDictionary<string, object?> attributes,
            Geometry? geometry,
            WKBWriter writer,
            int srid,
            CancellationToken cancellationToken)
        {
            foreach (var pair in _parameters)
            {
                attributes.TryGetValue(pair.Key, out var value);
                pair.Value.Value = NormalizeValue(value) ?? DBNull.Value;
            }

            var geometryValue = BuildGeoPackageGeometry(geometry, writer, srid);
            _geometryParameter.Value = geometryValue ?? (object)DBNull.Value;

            await _command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            _command.Dispose();
        }
    }

    private static string NormalizeGeometryType(string geometryType)
    {
        if (geometryType.IsNullOrWhiteSpace())
        {
            return "GEOMETRY";
        }

        return geometryType.ToUpperInvariant() switch
        {
            "POINT" => "POINT",
            "LINESTRING" => "LINESTRING",
            "POLYGON" => "POLYGON",
            "MULTIPOINT" => "MULTIPOINT",
            "MULTILINESTRING" => "MULTILINESTRING",
            "MULTIPOLYGON" => "MULTIPOLYGON",
            _ => "GEOMETRY"
        };
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string ResolveSqlType(string? dataType, FieldDefinition? field)
    {
        if (field is not null && field.StorageType.HasValue())
        {
            return field.StorageType.ToUpperInvariant();
        }

        return (dataType ?? string.Empty).ToLowerInvariant() switch
        {
            "int" or "int32" or "int64" or "integer" => "INTEGER",
            "double" or "float" => "REAL",
            "decimal" => "NUMERIC",
            "datetime" or "date" => "TEXT",
            "bool" or "boolean" => "INTEGER",
            "string" => "TEXT",
            _ => "TEXT"
        };
    }

    private static string SanitizeIdentifier(string value)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return "layer";
        }

        var matches = IdentifierRegex.Matches(value);
        if (matches.Count == 0)
        {
            return "layer";
        }

        return string.Join('_', matches.Select(m => m.Value.ToLowerInvariant()));
    }

    private static string SanitizeFileName(string value)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return "export";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return sanitized.IsNullOrWhiteSpace() ? "export" : sanitized;
    }

    private static string QuoteIdentifier(string identifier)
    {
        if (identifier.IsNullOrWhiteSpace())
        {
            return "\"column\"";
        }

        var sanitized = identifier.Replace("\"", "\"\"");
        return $"\"{sanitized}\"";
    }

    private sealed record ColumnDefinition(string Name, string SqlType, bool IsPrimaryKey, bool IsNullable);
}
