// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Import;

public interface IDataIngestionQueueStore
{
    Task PersistAsync(DataIngestionQueueRecord record, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DataIngestionQueueRecord>> LoadPendingAsync(CancellationToken cancellationToken = default);
    Task RemoveAsync(Guid jobId, CancellationToken cancellationToken = default);
}

public sealed record DataIngestionQueueRecord(
    Guid JobId,
    DateTimeOffset CreatedAtUtc,
    DataIngestionRequest Request);

internal sealed class FileDataIngestionQueueStore : IDataIngestionQueueStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private readonly string _queueDirectory;
    private readonly ILogger<FileDataIngestionQueueStore> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public FileDataIngestionQueueStore(string queueDirectory, ILogger<FileDataIngestionQueueStore> logger)
    {
        _queueDirectory = queueDirectory ?? throw new ArgumentNullException(nameof(queueDirectory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        Directory.CreateDirectory(_queueDirectory);
    }

    public async Task PersistAsync(DataIngestionQueueRecord record, CancellationToken cancellationToken = default)
    {
        if (record is null)
        {
            throw new ArgumentNullException(nameof(record));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var path = ResolvePath(record.JobId);
        var tempPath = path + ".tmp";

        try
        {
            Directory.CreateDirectory(_queueDirectory);

            await using (var stream = new FileStream(
                tempPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, record, SerializerOptions, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(tempPath, path, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist ingestion queue record for job {JobId} at {Path}.", record.JobId, path);
            throw;
        }
        finally
        {
            TryDeleteTemp(tempPath);
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<DataIngestionQueueRecord>> LoadPendingAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (!Directory.Exists(_queueDirectory))
            {
                return Array.Empty<DataIngestionQueueRecord>();
            }

            var files = Directory
                .EnumerateFiles(_queueDirectory, "*.json", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (files.Length == 0)
            {
                return Array.Empty<DataIngestionQueueRecord>();
            }

            var records = new List<DataIngestionQueueRecord>(files.Length);
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    await using var stream = new FileStream(
                        file,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        bufferSize: 81920,
                        useAsync: true);

                    var record = await JsonSerializer
                        .DeserializeAsync<DataIngestionQueueRecord>(stream, SerializerOptions, cancellationToken)
                        .ConfigureAwait(false);

                    if (record is not null)
                    {
                        records.Add(record);
                    }
                    else
                    {
                        _logger.LogWarning("Skipping ingestion queue file {Path} because it could not be deserialized.", file);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to read ingestion queue file {Path}.", file);
                }
            }

            return records;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RemoveAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        var path = ResolvePath(jobId);

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete ingestion queue file {Path} for job {JobId}.", path, jobId);
        }
        finally
        {
            _gate.Release();
        }
    }

    private string ResolvePath(Guid jobId) => Path.Combine(_queueDirectory, $"{jobId:N}.json");

    private static void TryDeleteTemp(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Ignored – temp cleanup best effort.
        }
    }
}
