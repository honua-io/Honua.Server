// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Npgsql;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Attachments;

internal sealed class DatabaseAttachmentStore : IAttachmentStore
{
    private readonly Func<DbConnection> _connectionFactory;
    private readonly string _tableName;
    private readonly string _attachmentIdColumn;
    private readonly string _contentColumn;
    private readonly string? _fileNameColumn;

    public DatabaseAttachmentStore(
        AttachmentDatabaseStorageOptions configuration,
        Func<DbConnection> connectionFactory)
    {
        Guard.NotNull(configuration);
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

        if (configuration.TableName.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException("Database attachment store requires a tableName configuration value.");
        }

        if (configuration.AttachmentIdColumn.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException("Database attachment store requires an attachmentIdColumn configuration value.");
        }

        if (configuration.ContentColumn.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException("Database attachment store requires a contentColumn configuration value.");
        }

        _tableName = configuration.Schema.IsNullOrWhiteSpace()
            ? configuration.TableName!
            : $"{configuration.Schema}.{configuration.TableName}";

        _attachmentIdColumn = configuration.AttachmentIdColumn!;
        _contentColumn = configuration.ContentColumn!;
        _fileNameColumn = configuration.FileNameColumn;
    }

    public async Task<AttachmentStoreWriteResult> PutAsync(Stream content, AttachmentStorePutRequest request, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(content);
        Guard.NotNull(request);

        await using var connection = _connectionFactory();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        var payload = buffer.ToArray();

        var updateCommandText = $"UPDATE {_tableName} SET {_contentColumn} = @content{(_fileNameColumn.IsNullOrWhiteSpace() ? string.Empty : ", " + _fileNameColumn + " = @fileName")} WHERE {_attachmentIdColumn} = @attachmentId";
        await using (var updateCommand = connection.CreateCommand())
        {
            updateCommand.CommandText = updateCommandText;
            updateCommand.AddParameter("@content", payload);
            if (_fileNameColumn.HasValue())
            {
                updateCommand.AddParameter("@fileName", request.FileName);
            }

            updateCommand.AddParameter("@attachmentId", request.AttachmentId);
            var affected = await updateCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            if (affected == 0)
            {
                var insertCommandText = $"INSERT INTO {_tableName} ({_attachmentIdColumn}, {_contentColumn}{(_fileNameColumn.IsNullOrWhiteSpace() ? string.Empty : ", " + _fileNameColumn)}) VALUES (@attachmentId, @content{(_fileNameColumn.IsNullOrWhiteSpace() ? string.Empty : ", @fileName")})";
                await using var insertCommand = connection.CreateCommand();
                insertCommand.CommandText = insertCommandText;
                insertCommand.AddParameter("@attachmentId", request.AttachmentId);
                insertCommand.AddParameter("@content", payload);
                if (_fileNameColumn.HasValue())
                {
                    insertCommand.AddParameter("@fileName", request.FileName);
                }

                await insertCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        return new AttachmentStoreWriteResult
        {
            Pointer = new AttachmentPointer(AttachmentStoreProviderKeys.Database, request.AttachmentId)
        };
    }

    public async Task<AttachmentReadResult?> TryGetAsync(AttachmentPointer pointer, CancellationToken cancellationToken = default)
    {
        var attachmentId = ResolveAttachmentId(pointer);
        await using var connection = _connectionFactory();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var commandText = $"SELECT {_contentColumn}{(_fileNameColumn.IsNullOrWhiteSpace() ? string.Empty : ", " + _fileNameColumn)} FROM {_tableName} WHERE {_attachmentIdColumn} = @attachmentId";
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.AddParameter("@attachmentId", attachmentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var content = (byte[])reader.GetValue(0);
        var fileName = _fileNameColumn.HasValue() && reader.FieldCount > 1 && !reader.IsDBNull(1)
            ? reader.GetString(1)
            : null;

        return new AttachmentReadResult
        {
            Content = new MemoryStream(content, writable: false),
            MimeType = null,
            SizeBytes = content.Length,
            FileName = fileName,
            ChecksumSha256 = null
        };
    }

    public async Task<bool> DeleteAsync(AttachmentPointer pointer, CancellationToken cancellationToken = default)
    {
        var attachmentId = ResolveAttachmentId(pointer);
        await using var connection = _connectionFactory();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var commandText = $"DELETE FROM {_tableName} WHERE {_attachmentIdColumn} = @attachmentId";
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.AddParameter("@attachmentId", attachmentId);

        var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return affected > 0;
    }

    public async IAsyncEnumerable<AttachmentPointer> ListAsync(string? prefix = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var commandText = prefix.IsNullOrWhiteSpace()
            ? $"SELECT {_attachmentIdColumn} FROM {_tableName}"
            : $"SELECT {_attachmentIdColumn} FROM {_tableName} WHERE {_attachmentIdColumn} LIKE @prefix";

        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        if (prefix.HasValue())
        {
            command.AddParameter("@prefix", prefix + "%");
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var attachmentId = reader.GetString(0);
            yield return new AttachmentPointer(AttachmentStoreProviderKeys.Database, attachmentId);
        }
    }

    private static string ResolveAttachmentId(AttachmentPointer pointer)
    {
        if (!string.Equals(pointer.StorageProvider, AttachmentStoreProviderKeys.Database, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Pointer does not belong to database store: {pointer.StorageProvider}");
        }

        return pointer.StorageKey;
    }
}

internal sealed class DatabaseAttachmentStoreProvider : IAttachmentStoreProvider
{
    private readonly ILoggerFactory _loggerFactory;

    public DatabaseAttachmentStoreProvider(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    public string ProviderKey => AttachmentStoreProviderKeys.Database;

    public IAttachmentStore Create(string profileId, AttachmentStorageProfileOptions profileConfiguration)
    {
        Guard.NotNull(profileConfiguration);
        var database = profileConfiguration.Database ?? new AttachmentDatabaseStorageOptions();
        if (database.ConnectionString.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException($"Database attachment profile '{profileId}' must specify database.connectionString.");
        }

        if (database.Provider.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException($"Database attachment profile '{profileId}' must specify database.provider.");
        }

        return new DatabaseAttachmentStore(
            database,
            () => CreateConnection(database.Provider!, database.ConnectionString!));
    }

    private static DbConnection CreateConnection(string provider, string connectionString)
    {
        return provider.Trim().ToLowerInvariant() switch
        {
            "postgres" or "postgresql" or "npgsql" => new NpgsqlConnection(connectionString),
            "sqlserver" or "mssql" or "system.data.sqlclient" => new SqlConnection(connectionString),
            "mysql" or "mariadb" => new MySqlConnection(connectionString),
            "sqlite" or "microsoft.data.sqlite" => new SqliteConnection(connectionString),
            _ => throw new NotSupportedException($"Attachment database provider '{provider}' is not supported.")
        };
    }
}

internal static class DbCommandExtensions
{
    public static void AddParameter(this DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
