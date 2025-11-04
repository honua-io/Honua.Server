// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Ogc;

/// <summary>
/// Caches the OGC API definition payload and invalidates when the backing file changes.
/// </summary>
internal sealed class OgcApiDefinitionCache
{
    private readonly IWebHostEnvironment _environment;
    private readonly OgcCacheHeaderService _cacheHeaderService;
    private readonly ILogger<OgcApiDefinitionCache> _logger;
    private readonly SemaphoreSlim _reloadLock = new(1, 1);
    private readonly IFileProvider _fileProvider;
    private readonly string _relativePath = Path.Combine("schemas", OgcSharedHandlers.ApiDefinitionFileName);

    private OgcApiDefinitionCacheEntry? _cachedEntry;
    private IDisposable? _changeRegistration;

    public OgcApiDefinitionCache(
        IWebHostEnvironment environment,
        OgcCacheHeaderService cacheHeaderService,
        ILogger<OgcApiDefinitionCache> logger)
    {
        _environment = Guard.NotNull(environment);
        _cacheHeaderService = Guard.NotNull(cacheHeaderService);
        _logger = Guard.NotNull(logger);
        _fileProvider = _environment.ContentRootFileProvider;
    }

    public async ValueTask<OgcApiDefinitionCacheEntry> GetAsync(CancellationToken cancellationToken)
    {
        var entry = Volatile.Read(ref _cachedEntry);
        if (entry is not null)
        {
            return entry;
        }

        await _reloadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            entry = _cachedEntry;
            if (entry is not null)
            {
                return entry;
            }

            var fileInfo = _fileProvider.GetFileInfo(_relativePath);
            if (!fileInfo.Exists)
            {
                throw new FileNotFoundException(
                    $"API definition '{OgcSharedHandlers.ApiDefinitionFileName}' was not found.",
                    fileInfo.PhysicalPath ?? OgcSharedHandlers.ApiDefinitionFileName);
            }

            await using var stream = fileInfo.CreateReadStream();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

            var bytes = buffer.ToArray();
            var payload = Encoding.UTF8.GetString(bytes);
            var etag = _cacheHeaderService.GenerateETag(bytes);
            var newEntry = new OgcApiDefinitionCacheEntry(payload, etag, fileInfo.LastModified);

            Volatile.Write(ref _cachedEntry, newEntry);
            RegisterChangeCallback();

            return newEntry;
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    private void RegisterChangeCallback()
    {
        void OnFileChanged(object? state)
        {
            Volatile.Write(ref _cachedEntry, null);
            var previous = Interlocked.Exchange(ref _changeRegistration, null);
            previous?.Dispose();

            try
            {
                RegisterChangeCallback();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to re-register change token for OGC API definition updates. " +
                    "API definition cache may become stale until the next request reloads it.");
            }
        }

        var token = _fileProvider.Watch(_relativePath);
        var registration = token.RegisterChangeCallback(OnFileChanged, null);
        var previousRegistration = Interlocked.Exchange(ref _changeRegistration, registration);
        previousRegistration?.Dispose();
    }
}

internal sealed record OgcApiDefinitionCacheEntry(string Payload, string ETag, DateTimeOffset LastModified);
