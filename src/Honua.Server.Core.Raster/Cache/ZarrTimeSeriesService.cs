// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Data;
using Honua.Server.Core.Raster.Readers;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Raster.Cache;

/// <summary>
/// Zarr time-series service implementation using Python interop.
/// Executes Python scripts to convert NetCDF/HDF5 to Zarr format using xarray/zarr libraries.
/// </summary>
public sealed class ZarrTimeSeriesService : IZarrTimeSeriesService
{
    private readonly ILogger<ZarrTimeSeriesService> _logger;
    private readonly string? _pythonExecutable;
    private readonly IZarrReader _zarrReader;
    private static readonly SemaphoreSlim _pythonLock = new(1, 1);

    public ZarrTimeSeriesService(
        ILogger<ZarrTimeSeriesService> logger,
        IZarrReader zarrReader,
        string? pythonExecutable = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _zarrReader = zarrReader ?? throw new ArgumentNullException(nameof(zarrReader));
        _pythonExecutable = pythonExecutable ?? FindPythonExecutable();

        if (_pythonExecutable == null)
        {
            _logger.LogWarning("Python executable not found. Zarr support will be limited.");
        }
    }

    public async Task ConvertToZarrAsync(string sourceUri, string zarrUri, ZarrConversionOptions options, CancellationToken cancellationToken = default)
    {
        if (_pythonExecutable == null)
        {
            throw new InvalidOperationException(
                "Python executable not found. Zarr conversion requires Python with xarray and zarr packages installed. " +
                "Install via: pip install xarray zarr netcdf4 h5netcdf");
        }

        // SECURITY: Validate inputs to prevent injection attacks
        ValidateInputForSecurity(sourceUri, nameof(sourceUri));
        ValidateInputForSecurity(zarrUri, nameof(zarrUri));
        ValidateInputForSecurity(options.VariableName, nameof(options.VariableName));
        ValidateInputForSecurity(options.Compression, nameof(options.Compression));

        _logger.LogInformation("Converting {SourceUri} to Zarr at {ZarrUri}", sourceUri, zarrUri);

        // Generate Python script and config file for conversion
        var scriptPath = Path.Combine(Path.GetTempPath(), $"zarr_convert_{Guid.NewGuid()}.py");
        var configPath = Path.Combine(Path.GetTempPath(), $"zarr_config_{Guid.NewGuid()}.json");

        try
        {
            // SECURITY FIX: Pass parameters via JSON config file instead of string interpolation
            // This prevents Python code injection via malicious URIs or option values
            var config = new
            {
                sourceUri = sourceUri,
                zarrUri = zarrUri,
                variableName = options.VariableName,
                timeChunkSize = options.TimeChunkSize,
                latitudeChunkSize = options.LatitudeChunkSize,
                longitudeChunkSize = options.LongitudeChunkSize,
                compression = options.Compression,
                compressionLevel = options.CompressionLevel
            };

            await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(config), cancellationToken);

            var pythonScript = GenerateConversionScript(configPath);
            await File.WriteAllTextAsync(scriptPath, pythonScript, cancellationToken);

            // Execute Python script
            await _pythonLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await ExecutePythonScriptAsync(scriptPath, cancellationToken);
            }
            finally
            {
                _pythonLock.Release();
            }

            _logger.LogInformation("Successfully converted {SourceUri} to Zarr", sourceUri);
        }
        finally
        {
            // Clean up temporary files
            if (File.Exists(scriptPath))
            {
                try
                {
                    File.Delete(scriptPath);
                }
                catch
                {
                    // Best effort cleanup
                }
            }

            if (File.Exists(configPath))
            {
                try
                {
                    File.Delete(configPath);
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }
    }

    public async Task<float[,,]> QueryTimeRangeAsync(
        string zarrUri,
        string variableName,
        DateTime startTime,
        DateTime endTime,
        double[]? bbox = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Querying legacy Zarr time range: {ZarrUri}, variable={VariableName}, {StartTime} to {EndTime}",
            zarrUri, variableName, startTime, endTime);

        var startOffset = new DateTimeOffset(DateTime.SpecifyKind(startTime, DateTimeKind.Utc));
        var endOffset = new DateTimeOffset(DateTime.SpecifyKind(endTime, DateTimeKind.Utc));
        BoundingBox? spatialExtent = bbox != null && bbox.Length >= 4
            ? new BoundingBox(bbox[0], bbox[1], bbox[2], bbox[3])
            : null;

        var series = await QueryTimeRangeAsync(
                zarrUri,
                variableName,
                startOffset,
                endOffset,
                spatialExtent,
                aggregationInterval: null,
                cancellationToken)
            .ConfigureAwait(false);

        return ConvertBytesToFloat3D(series.DataSlices, series.Metadata);
    }

    public async Task<float[,]> QueryTimeSliceAsync(
        string zarrUri,
        string variableName,
        DateTime time,
        double[]? bbox = null,
        CancellationToken cancellationToken = default)
    {
        // Legacy method - convert to DateTimeOffset and call new implementation
        var timeOffset = new DateTimeOffset(time, TimeSpan.Zero);
        BoundingBox? boundingBox = bbox != null && bbox.Length >= 4
            ? new BoundingBox(bbox[0], bbox[1], bbox[2], bbox[3])
            : null;

        var result = await QueryTimeSliceAsync(zarrUri, variableName, timeOffset, boundingBox, cancellationToken);

        // Convert byte array to float[,]
        return ConvertBytesToFloat2D(result.Data, result.Metadata);
    }

    public async Task<ZarrTimeSlice> QueryTimeSliceAsync(
        string zarrPath,
        string variableName,
        DateTimeOffset timestamp,
        BoundingBox? spatialExtent = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Querying Zarr time slice: {ZarrPath}, variable={VariableName}, time={Timestamp}",
            zarrPath, variableName, timestamp);

        // Get time steps to find the index
        var timeSteps = await GetTimeStepsAsync(zarrPath, variableName, cancellationToken);
        var timeIndex = FindClosestTimeIndex(timeSteps, timestamp);

        if (timeIndex < 0)
        {
            throw new InvalidOperationException($"No time steps found in Zarr dataset: {zarrPath}");
        }

        _logger.LogDebug("Found time index {TimeIndex} for timestamp {Timestamp}", timeIndex, timestamp);

        var array = await _zarrReader.OpenArrayAsync(zarrPath, variableName, cancellationToken);
        var metadata = array.Metadata;

        var (spatialStart, spatialCount) = await CalculateSpatialSliceAsync(zarrPath, metadata, spatialExtent, cancellationToken).ConfigureAwait(false);

        var start = new int[metadata.Shape.Length];
        var count = new int[metadata.Shape.Length];

        start[0] = timeIndex;
        count[0] = 1;

        for (int i = 1; i < metadata.Shape.Length; i++)
        {
            start[i] = spatialStart.Length >= i ? spatialStart[i - 1] : 0;
            count[i] = spatialCount.Length >= i ? spatialCount[i - 1] : metadata.Shape[i];
        }

        var data = await _zarrReader.ReadSliceAsync(array, start, count, cancellationToken);
        var byteData = data is byte[] bytes ? bytes : ConvertArrayToBytes(data);

        var actualBbox = spatialExtent ?? await CalculateFullExtentAsync(zarrPath, metadata, cancellationToken).ConfigureAwait(false);

        return new ZarrTimeSlice(
            timeSteps[timeIndex],
            timeIndex,
            byteData,
            metadata,
            actualBbox);
    }

    public async Task<IReadOnlyList<DateTimeOffset>> GetTimeStepsAsync(
        string zarrPath,
        string variableName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Reading time steps from Zarr dataset: {ZarrPath}", zarrPath);

        var metadata = await _zarrReader.GetMetadataAsync(zarrPath, variableName, cancellationToken).ConfigureAwait(false);
        var timeAxisName = ResolveTimeDimensionName(metadata) ?? "time";

        using var timeArray = await _zarrReader.OpenArrayAsync(zarrPath, timeAxisName, cancellationToken).ConfigureAwait(false);
        if (timeArray is null)
        {
            throw new InvalidOperationException($"Time coordinate '{timeAxisName}' not found in Zarr store {zarrPath}.");
        }

        var timeMetadata = timeArray.Metadata;

        // Read all time values
        var start = new int[timeMetadata.Shape.Length];
        var count = timeMetadata.Shape;

        var timeData = await _zarrReader.ReadSliceAsync(timeArray, start, count, cancellationToken);

        // Parse time values (typically stored as seconds/days since epoch)
        return ParseTimeCoordinates(timeData, timeMetadata);
    }

    public async Task<ZarrTimeSeriesData> QueryTimeRangeAsync(
        string zarrPath,
        string variableName,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        BoundingBox? spatialExtent = null,
        TimeSpan? aggregationInterval = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Querying Zarr time range: {ZarrPath}, variable={VariableName}, {StartTime} to {EndTime}",
            zarrPath, variableName, startTime, endTime);

        // Get time steps
        var allTimeSteps = await GetTimeStepsAsync(zarrPath, variableName, cancellationToken);

        // Filter time steps within range
        var timeIndices = new List<int>();
        for (int i = 0; i < allTimeSteps.Count; i++)
        {
            if (allTimeSteps[i] >= startTime && allTimeSteps[i] <= endTime)
            {
                timeIndices.Add(i);
            }
        }

        if (timeIndices.Count == 0)
        {
            throw new InvalidOperationException(
                $"No time steps found in range {startTime} to {endTime}");
        }

        _logger.LogDebug("Found {Count} time steps in range", timeIndices.Count);

        using var array = await _zarrReader.OpenArrayAsync(zarrPath, variableName, cancellationToken);
        var metadata = array.Metadata;

        var dataSlices = new List<byte[]>(timeIndices.Count);
        var timestamps = new List<DateTimeOffset>(timeIndices.Count);

        var (spatialStart, spatialCount) = await CalculateSpatialSliceAsync(zarrPath, metadata, spatialExtent, cancellationToken).ConfigureAwait(false);
        var start = new int[metadata.Shape.Length];
        var count = new int[metadata.Shape.Length];

        count[0] = 1;
        for (int i = 1; i < metadata.Shape.Length; i++)
        {
            start[i] = spatialStart.Length >= i ? spatialStart[i - 1] : 0;
            count[i] = spatialCount.Length >= i ? spatialCount[i - 1] : metadata.Shape[i];
        }

        foreach (var timeIndex in timeIndices)
        {
            start[0] = timeIndex;

            var data = await _zarrReader.ReadSliceAsync(array, start, count, cancellationToken).ConfigureAwait(false);
            var bytes = data is byte[] buffer ? buffer : ConvertArrayToBytes(data);
            dataSlices.Add(bytes);
            timestamps.Add(allTimeSteps[timeIndex]);
        }

        // Apply aggregation if requested
        var aggregationMethod = ZarrAggregationMethod.None;
        if (aggregationInterval.HasValue)
        {
            if (aggregationInterval.Value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(aggregationInterval), "Aggregation interval must be greater than zero.");
            }

            if (!SupportsAggregation(metadata))
            {
                throw new NotSupportedException($"Aggregation requires a floating point dataset. Found dtype '{metadata.DType}'.");
            }

            var (aggregatedTimestamps, aggregatedSlices) = AggregateTimeSlices(
                timestamps,
                dataSlices,
                metadata,
                aggregationInterval.Value);

            timestamps = aggregatedTimestamps;
            dataSlices = aggregatedSlices;
            aggregationMethod = ZarrAggregationMethod.Mean;
        }

        return new ZarrTimeSeriesData(
            timestamps,
            dataSlices,
            metadata,
            aggregationMethod.ToString().ToLowerInvariant());
    }

    public async Task<ZarrMetadata> GetMetadataAsync(string zarrUri, CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(zarrUri);
        cancellationToken.ThrowIfCancellationRequested();

        var attributes = new List<ZarrAttributeInfo>();
        var variableMetadata = new List<VariableMetadata>();
        var dimensionBuilders = new Dictionary<string, DimensionAccumulator>(StringComparer.OrdinalIgnoreCase);
        var dimensionOrder = new List<string>();
        DateTime? timeStart = null;
        DateTime? timeEnd = null;
        double[]? spatialExtent = null;
        int? datasetZarrFormat = null;

        var zmetadataPath = Path.Combine(zarrUri, ".zmetadata");
        var zattrsPath = Path.Combine(zarrUri, ".zattrs");
        var zarrayPath = Path.Combine(zarrUri, ".zarray");

        if (File.Exists(zmetadataPath))
        {
            using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(zmetadataPath, cancellationToken).ConfigureAwait(false));
            if (!doc.RootElement.TryGetProperty("metadata", out var metadataRoot))
            {
                throw new InvalidOperationException($"Invalid consolidated Zarr metadata at {zmetadataPath}. Expected 'metadata' property.");
            }

            if (metadataRoot.TryGetProperty(".zattrs", out var rootAttrs))
            {
                attributes.AddRange(ExtractAttributes(rootAttrs));
                ExtractTemporalAndSpatial(rootAttrs, ref timeStart, ref timeEnd, ref spatialExtent);
            }

            if (metadataRoot.TryGetProperty(".zarray", out var rootArray))
            {
                var parsed = ParseArrayMetadata(rootArray);
                datasetZarrFormat = parsed.ZarrFormat;
                var dimNames = ResolveDimensionNames(metadataRoot.TryGetProperty(".zattrs", out var rootDimAttrs) ? rootDimAttrs : rootAttrs, parsed.Shape.Length);
                RegisterDimensions(dimensionBuilders, dimensionOrder, dimNames, parsed.Shape, parsed.Chunks);
            }

            var variableMap = new Dictionary<string, (JsonElement? Array, JsonElement? Attrs)>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in metadataRoot.EnumerateObject())
            {
                var key = property.Name;
                if (key is ".zattrs" or ".zarray")
                {
                    continue;
                }

                if (key.EndsWith("/.zarray", StringComparison.Ordinal))
                {
                    var variableName = key[..^("/.zarray".Length)];
                    var tuple = variableMap.TryGetValue(variableName, out var existing)
                        ? existing
                        : default;
                    tuple.Array = property.Value;
                    variableMap[variableName] = tuple;
                }
                else if (key.EndsWith("/.zattrs", StringComparison.Ordinal))
                {
                    var variableName = key[..^("/.zattrs".Length)];
                    var tuple = variableMap.TryGetValue(variableName, out var existing)
                        ? existing
                        : default;
                    tuple.Attrs = property.Value;
                    variableMap[variableName] = tuple;
                }
            }

            foreach (var entry in variableMap)
            {
                if (entry.Key.IsNullOrWhiteSpace() || entry.Value.Array is null)
                {
                    continue;
                }

                var parsed = ParseArrayMetadata(entry.Value.Array.Value);
                if (!datasetZarrFormat.HasValue && parsed.ZarrFormat.HasValue)
                {
                    datasetZarrFormat = parsed.ZarrFormat;
                }

                var dimNames = ResolveDimensionNames(entry.Value.Attrs, parsed.Shape.Length);
                RegisterDimensions(dimensionBuilders, dimensionOrder, dimNames, parsed.Shape, parsed.Chunks);

                IReadOnlyList<ZarrAttributeInfo> variableAttrs = entry.Value.Attrs.HasValue
                    ? ExtractAttributes(entry.Value.Attrs.Value)
                    : Array.Empty<ZarrAttributeInfo>();

                variableMetadata.Add(new VariableMetadata(
                    entry.Key,
                    parsed.Shape,
                    parsed.Chunks,
                    dimNames,
                    parsed.DType,
                    parsed.FillValue,
                    parsed.Compressor,
                    variableAttrs));
            }
        }
        else
        {
            JsonElement? rootAttrsElement = null;
            if (File.Exists(zattrsPath))
            {
                using var rootAttrsDoc = JsonDocument.Parse(await File.ReadAllTextAsync(zattrsPath, cancellationToken).ConfigureAwait(false));
                rootAttrsElement = rootAttrsDoc.RootElement.Clone();
                attributes.AddRange(ExtractAttributes(rootAttrsDoc.RootElement));
                ExtractTemporalAndSpatial(rootAttrsDoc.RootElement, ref timeStart, ref timeEnd, ref spatialExtent);
            }

            if (File.Exists(zarrayPath))
            {
                using var rootArrayDoc = JsonDocument.Parse(await File.ReadAllTextAsync(zarrayPath, cancellationToken).ConfigureAwait(false));
                var rootArrayElement = rootArrayDoc.RootElement.Clone();
                var parsed = ParseArrayMetadata(rootArrayElement);
                datasetZarrFormat = parsed.ZarrFormat;
                var dimNames = ResolveDimensionNames(rootAttrsElement, parsed.Shape.Length);
                RegisterDimensions(dimensionBuilders, dimensionOrder, dimNames, parsed.Shape, parsed.Chunks);
            }

            if (Directory.Exists(zarrUri))
            {
                foreach (var directory in Directory.EnumerateDirectories(zarrUri))
                {
                    var variableName = Path.GetFileName(directory);
                    if (variableName.IsNullOrWhiteSpace() || variableName.StartsWith(".", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var variableArrayPath = Path.Combine(directory, ".zarray");
                    if (!File.Exists(variableArrayPath))
                    {
                        continue;
                    }

                    JsonElement? variableAttrsElement = null;
                    var variableAttrsPath = Path.Combine(directory, ".zattrs");
                    if (File.Exists(variableAttrsPath))
                    {
                        using var variableAttrsDoc = JsonDocument.Parse(await File.ReadAllTextAsync(variableAttrsPath, cancellationToken).ConfigureAwait(false));
                        variableAttrsElement = variableAttrsDoc.RootElement.Clone();
                    }

                    using var variableArrayDoc = JsonDocument.Parse(await File.ReadAllTextAsync(variableArrayPath, cancellationToken).ConfigureAwait(false));
                    var variableArrayElement = variableArrayDoc.RootElement.Clone();

                    var parsed = ParseArrayMetadata(variableArrayElement);
                    if (!datasetZarrFormat.HasValue && parsed.ZarrFormat.HasValue)
                    {
                        datasetZarrFormat = parsed.ZarrFormat;
                    }

                    var dimNames = ResolveDimensionNames(variableAttrsElement, parsed.Shape.Length);
                    RegisterDimensions(dimensionBuilders, dimensionOrder, dimNames, parsed.Shape, parsed.Chunks);

                    IReadOnlyList<ZarrAttributeInfo> variableAttrs = variableAttrsElement.HasValue
                        ? ExtractAttributes(variableAttrsElement.Value)
                        : Array.Empty<ZarrAttributeInfo>();

                    variableMetadata.Add(new VariableMetadata(
                        variableName,
                        parsed.Shape,
                        parsed.Chunks,
                        dimNames,
                        parsed.DType,
                        parsed.FillValue,
                        parsed.Compressor,
                        variableAttrs));
                }
            }
        }

        if (datasetZarrFormat.HasValue &&
            !attributes.Any(a => string.Equals(a.Name, "zarr_format", StringComparison.OrdinalIgnoreCase)))
        {
            attributes.Add(new ZarrAttributeInfo("zarr_format", datasetZarrFormat.Value));
        }

        if (variableMetadata.Count > 0 &&
            !attributes.Any(a => string.Equals(a.Name, "variables", StringComparison.OrdinalIgnoreCase)))
        {
            var variablesAttribute = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var variable in variableMetadata)
            {
                var attrDictionary = variable.Attributes
                    .GroupBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.OrdinalIgnoreCase);

                variablesAttribute[variable.Name] = new Dictionary<string, object?>
                {
                    ["dimensions"] = variable.DimensionNames,
                    ["shape"] = variable.Shape,
                    ["chunks"] = variable.Chunks,
                    ["dtype"] = variable.DType,
                    ["fillValue"] = variable.FillValue,
                    ["compressor"] = variable.Compressor,
                    ["attributes"] = attrDictionary
                };
            }

            attributes.Add(new ZarrAttributeInfo("variables", variablesAttribute));
        }

        var dimensions = dimensionOrder
            .Select(name => dimensionBuilders[name].ToDimensionInfo())
            .ToArray();

        if (dimensions.Length == 0)
        {
            var primaryVariable = variableMetadata.FirstOrDefault(vm => vm.DimensionNames.Length > 1) ?? variableMetadata.FirstOrDefault();
            if (primaryVariable is not null)
            {
                dimensions = primaryVariable.DimensionNames
                    .Select((dim, index) =>
                        new ZarrDimensionInfo(
                            dim,
                            index < primaryVariable.Shape.Length ? primaryVariable.Shape[index] : 0,
                            SafeChunk(primaryVariable.Chunks, index, index < primaryVariable.Shape.Length ? primaryVariable.Shape[index] : 0)))
                    .ToArray();
            }
        }

        var variables = variableMetadata
            .Select(v => v.Name)
            .Where(name => name.HasValue())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (variableMetadata.Count > 0 &&
            !attributes.Any(a => string.Equals(a.Name, "primaryVariable", StringComparison.OrdinalIgnoreCase)))
        {
            var primaryVariable = variableMetadata.FirstOrDefault(vm => vm.DimensionNames.Length > 1)
                ?? variableMetadata.First();
            attributes.Add(new ZarrAttributeInfo("primaryVariable", primaryVariable.Name));
            if (primaryVariable.DType.HasValue())
            {
                attributes.Add(new ZarrAttributeInfo("primaryDType", primaryVariable.DType));
            }
        }

        return new ZarrMetadata
        {
            Uri = zarrUri,
            Dimensions = dimensions,
            Variables = variables,
            Attributes = attributes.ToArray(),
            TimeStart = timeStart,
            TimeEnd = timeEnd,
            SpatialExtent = spatialExtent
        };
    }

    public Task<bool> ExistsAsync(string zarrUri, CancellationToken cancellationToken = default)
    {
        // Basic check: Zarr datasets are directories containing .zarray or .zmetadata files
        if (Directory.Exists(zarrUri))
        {
            var zarrayPath = Path.Combine(zarrUri, ".zarray");
            var zmetadataPath = Path.Combine(zarrUri, ".zmetadata");
            return Task.FromResult(File.Exists(zarrayPath) || File.Exists(zmetadataPath));
        }

        return Task.FromResult(false);
    }

    /// <summary>
    /// Generate secure Python conversion script that reads parameters from JSON config file.
    /// SECURITY: This prevents Python code injection by avoiding string interpolation of user input.
    /// </summary>
    private string GenerateConversionScript(string configPath)
    {
        // SECURITY: Only the config file path is interpolated, which is a trusted system-generated path
        // All user-provided data (URIs, variable names, etc.) is read from the JSON config file
        var escapedConfigPath = EscapePythonSingleQuotedString(configPath);

        return $@"
import xarray as xr
import zarr
import sys
import json

try:
    # SECURITY: Read all parameters from JSON config file instead of inline string interpolation
    # This prevents Python code injection via malicious URIs or option values
    with open('{escapedConfigPath}', 'r') as f:
        config = json.load(f)

    source_uri = config['sourceUri']
    zarr_uri = config['zarrUri']
    variable_name = config['variableName']
    time_chunk_size = config['timeChunkSize']
    latitude_chunk_size = config['latitudeChunkSize']
    longitude_chunk_size = config['longitudeChunkSize']
    compression = config['compression']
    compression_level = config['compressionLevel']

    # Open source dataset
    ds = xr.open_dataset(source_uri)

    # Extract variable
    da = ds[variable_name]

    # Configure chunking
    encoding = {{
        variable_name: {{
            'chunks': (time_chunk_size, latitude_chunk_size, longitude_chunk_size),
            'compressor': zarr.Blosc(cname=compression, clevel=compression_level),
        }}
    }}

    # Write to Zarr
    da.to_zarr(zarr_uri, encoding=encoding, mode='w')

    print('SUCCESS')
except Exception as e:
    print(f'ERROR: {{e}}', file=sys.stderr)
    sys.exit(1)
";
    }

    private static string EscapePythonSingleQuotedString(string value)
    {
        if (value.IsNullOrEmpty())
        {
            return string.Empty;
        }

        return value
            .Replace("\\", "\\\\")
            .Replace("'", "\\'");
    }

    private async Task ExecutePythonScriptAsync(string scriptPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo
        {
            FileName = _pythonExecutable!,
            Arguments = $"\"{scriptPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException("Failed to start Python process");
        }

        try
        {
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            var waitTask = process.WaitForExitAsync();

            using var registration = cancellationToken.Register(() =>
            {
                if (!process.HasExited)
                {
                    _logger.LogWarning("Cancelling Python conversion script {ScriptPath}", scriptPath);
                    TryTerminateProcess(process);
                }
            });

            await Task.WhenAll(waitTask, outputTask, errorTask).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var output = await outputTask.ConfigureAwait(false);
            var error = await errorTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Python script failed with exit code {process.ExitCode}: {error}");
            }

            if (output.HasValue())
            {
                _logger.LogDebug("Python script output: {Output}", output);
            }
        }
        finally
        {
            TryTerminateProcess(process);
        }
    }

    private static void TryTerminateProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Process already exited.
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Process is terminating or access denied; nothing else to do.
        }
    }

    private static string? FindPythonExecutable()
    {
        // Try common Python executable names
        var candidates = new[] { "python3", "python", "py" };

        foreach (var candidate in candidates)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = candidate,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    process.WaitForExit(1000);
                    if (process.ExitCode == 0)
                    {
                        return candidate;
                    }
                }
            }
            catch
            {
                // Try next candidate
            }
        }

        return null;
    }

    /// <summary>
    /// Validates input strings to prevent Python code injection attacks.
    /// SECURITY: Defense-in-depth validation to reject suspicious characters that could be used for injection.
    /// Even though we now use JSON config files, this provides additional protection against malicious input.
    /// </summary>
    /// <param name="input">The input string to validate</param>
    /// <param name="parameterName">Name of the parameter for error messages</param>
    /// <exception cref="ArgumentException">Thrown if input contains suspicious characters</exception>
    private static void ValidateInputForSecurity(string input, string parameterName)
    {
        if (input.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("Input cannot be null or whitespace", parameterName);
        }

        // SECURITY: Reject inputs containing characters that could be used for code injection
        // Note: This is defense-in-depth. Primary protection is the JSON config file approach.
        var dangerousPatterns = new[]
        {
            "';",           // Python statement terminator followed by quote
            "\";",          // Python statement terminator with double quote
            "\n",           // Newline (could inject new Python statements)
            "\r",           // Carriage return
            "import ",      // Python import statement
            "exec(",        // Python exec function
            "eval(",        // Python eval function
            "__import__",   // Python import function
            "subprocess",   // Python subprocess module
            "os.system",    // OS command execution
            "open(",        // File operations (unless part of normal path)
        };

        foreach (var pattern in dangerousPatterns)
        {
            if (input.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"Input contains suspicious pattern '{pattern}' that could be used for code injection. " +
                    $"This is not allowed for security reasons.",
                    parameterName);
            }
        }

        // Additional validation: Check for control characters (except common ones in paths)
        foreach (var ch in input)
        {
            if (char.IsControl(ch) && ch != '\t')
            {
                throw new ArgumentException(
                    $"Input contains control character (code: {(int)ch}) which is not allowed for security reasons.",
                    parameterName);
            }
        }
    }

    /// <summary>
    /// Find the closest time index to the requested timestamp.
    /// </summary>
    private static int FindClosestTimeIndex(IReadOnlyList<DateTimeOffset> timeSteps, DateTimeOffset timestamp)
    {
        if (timeSteps.Count == 0)
        {
            return -1;
        }

        int closestIndex = 0;
        var minDiff = Math.Abs((timeSteps[0] - timestamp).TotalSeconds);

        for (int i = 1; i < timeSteps.Count; i++)
        {
            var diff = Math.Abs((timeSteps[i] - timestamp).TotalSeconds);
            if (diff < minDiff)
            {
                minDiff = diff;
                closestIndex = i;
            }
        }

        return closestIndex;
    }

    private async Task<(int[] start, int[] count)> CalculateSpatialSliceAsync(
        string zarrPath,
        ZarrArrayMetadata metadata,
        BoundingBox? spatialExtent,
        CancellationToken cancellationToken)
    {
        var spatialDims = metadata.Shape.Length - 1;
        if (spatialDims <= 0)
        {
            return (Array.Empty<int>(), Array.Empty<int>());
        }

        var start = new int[spatialDims];
        var count = new int[spatialDims];

        for (int i = 0; i < spatialDims; i++)
        {
            count[i] = metadata.Shape[i + 1];
        }

        if (spatialExtent is null)
        {
            return (start, count);
        }

        var dimensionNames = metadata.DimensionNames ?? Array.Empty<string>();
        var longitudeRange = default((int start, int count)?);
        var latitudeRange = default((int start, int count)?);

        for (int i = 1; i < metadata.Shape.Length; i++)
        {
            var axisName = dimensionNames.Length > i ? dimensionNames[i] : $"dim{i}";
            var axisKind = InferAxisKind(axisName, i - 1, spatialDims);
            if (axisKind == AxisKind.Other)
            {
                continue;
            }

            var coordinates = await ReadCoordinateAxisAsync(zarrPath, axisName, cancellationToken).ConfigureAwait(false);
            if (coordinates is null || coordinates.Length == 0)
            {
                continue;
            }

            var range = axisKind switch
            {
                AxisKind.Latitude => TryComputeRange(coordinates, spatialExtent.MinY, spatialExtent.MaxY),
                AxisKind.Longitude => TryComputeRange(coordinates, spatialExtent.MinX, spatialExtent.MaxX),
                _ => null
            };

            if (axisKind == AxisKind.Latitude && range.HasValue)
            {
                latitudeRange ??= range;
            }
            else if (axisKind == AxisKind.Longitude && range.HasValue)
            {
                longitudeRange ??= range;
            }
        }

        for (int i = 1; i < metadata.Shape.Length; i++)
        {
            var axisName = dimensionNames.Length > i ? dimensionNames[i] : $"dim{i}";
            var axisKind = InferAxisKind(axisName, i - 1, spatialDims);
            (int start, int count)? range = axisKind switch
            {
                AxisKind.Latitude => latitudeRange,
                AxisKind.Longitude => longitudeRange,
                _ => null
            };

            if (range.HasValue)
            {
                start[i - 1] = Math.Clamp(range.Value.start, 0, metadata.Shape[i] - 1);
                count[i - 1] = Math.Clamp(range.Value.count, 1, metadata.Shape[i] - start[i - 1]);
            }
            else
            {
                start[i - 1] = 0;
                count[i - 1] = metadata.Shape[i];
            }
        }

        return (start, count);
    }

    private async Task<BoundingBox> CalculateFullExtentAsync(
        string zarrPath,
        ZarrArrayMetadata metadata,
        CancellationToken cancellationToken)
    {
        var dimensionNames = metadata.DimensionNames ?? Array.Empty<string>();
        double? minLon = null, maxLon = null, minLat = null, maxLat = null;

        for (int i = 1; i < metadata.Shape.Length; i++)
        {
            var axisName = dimensionNames.Length > i ? dimensionNames[i] : $"dim{i}";
            var axisKind = InferAxisKind(axisName, i - 1, metadata.Shape.Length - 1);
            if (axisKind == AxisKind.Other)
            {
                continue;
            }

            var coordinates = await ReadCoordinateAxisAsync(zarrPath, axisName, cancellationToken).ConfigureAwait(false);
            if (coordinates is null || coordinates.Length == 0)
            {
                continue;
            }

            var axisMin = coordinates.Min();
            var axisMax = coordinates.Max();

            if (axisKind == AxisKind.Latitude)
            {
                minLat ??= axisMin;
                maxLat ??= axisMax;
                minLat = Math.Min(minLat.Value, axisMin);
                maxLat = Math.Max(maxLat.Value, axisMax);
            }
            else if (axisKind == AxisKind.Longitude)
            {
                minLon ??= axisMin;
                maxLon ??= axisMax;
                minLon = Math.Min(minLon.Value, axisMin);
                maxLon = Math.Max(maxLon.Value, axisMax);
            }
        }

        if (minLon.HasValue && maxLon.HasValue && minLat.HasValue && maxLat.HasValue)
        {
            return new BoundingBox(minLon.Value, minLat.Value, maxLon.Value, maxLat.Value);
        }

        return new BoundingBox(-180, -90, 180, 90);
    }

    private static string? ResolveTimeDimensionName(ZarrArrayMetadata metadata)
    {
        if (metadata.DimensionNames is { Length: > 0 })
        {
            var candidate = metadata.DimensionNames[0];
            if (candidate.HasValue())
            {
                return candidate;
            }
        }

        return null;
    }

    private static (int start, int count)? TryComputeRange(double[] coordinates, double min, double max)
    {
        if (coordinates.Length == 0)
        {
            return null;
        }

        var ascending = coordinates[^1] >= coordinates[0];

        if (ascending)
        {
            var start = 0;
            while (start < coordinates.Length && coordinates[start] < min)
            {
                start++;
            }

            if (start >= coordinates.Length)
            {
                return null;
            }

            var end = coordinates.Length - 1;
            while (end >= start && coordinates[end] > max)
            {
                end--;
            }

            if (end < start)
            {
                return null;
            }

            return (start, end - start + 1);
        }
        else
        {
            var start = 0;
            while (start < coordinates.Length && coordinates[start] > max)
            {
                start++;
            }

            if (start >= coordinates.Length)
            {
                return null;
            }

            var end = coordinates.Length - 1;
            while (end >= start && coordinates[end] < min)
            {
                end--;
            }

            if (end < start)
            {
                return null;
            }

            return (start, end - start + 1);
        }
    }

    private async Task<double[]?> ReadCoordinateAxisAsync(
        string zarrPath,
        string axisName,
        CancellationToken cancellationToken)
    {
        try
        {
            using var axisArray = await _zarrReader.OpenArrayAsync(zarrPath, axisName, cancellationToken).ConfigureAwait(false);
            if (axisArray is null)
            {
                return null;
            }

            var axisMetadata = axisArray.Metadata;
            if (axisMetadata.Shape.Length != 1)
            {
                return null;
            }

            var slice = await _zarrReader.ReadSliceAsync(axisArray, new[] { 0 }, new[] { axisMetadata.Shape[0] }, cancellationToken).ConfigureAwait(false);
            return ConvertToDoubleArray(slice);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or FileNotFoundException or DirectoryNotFoundException)
        {
            _logger.LogDebug(ex, "Unable to read coordinate axis {Axis} from {Path}", axisName, zarrPath);
            return null;
        }
    }

    private static double[]? ConvertToDoubleArray(Array values)
    {
        switch (values)
        {
            case double[] doubles:
                return doubles;
            case float[] floats:
                return floats.Select(f => (double)f).ToArray();
            case long[] longs:
                return longs.Select(l => (double)l).ToArray();
            case int[] ints:
                return ints.Select(i => (double)i).ToArray();
        }

        var result = new double[values.Length];
        var index = 0;
        foreach (var value in values)
        {
            if (value is IConvertible convertible)
            {
                result[index++] = convertible.ToDouble(CultureInfo.InvariantCulture);
            }
            else
            {
                return null;
            }
        }

        return result;
    }

    private static AxisKind InferAxisKind(string name, int spatialIndex, int totalSpatialDims)
    {
        var axisName = name?.ToLowerInvariant() ?? string.Empty;
        if (axisName.Contains("lon") || axisName.Contains("long") || axisName.Contains("x") || axisName.Contains("lambda"))
        {
            return AxisKind.Longitude;
        }

        if (axisName.Contains("lat") || axisName.Contains("y") || axisName.Contains("phi"))
        {
            return AxisKind.Latitude;
        }

        if (totalSpatialDims == 2)
        {
            return spatialIndex == 0 ? AxisKind.Latitude : AxisKind.Longitude;
        }

        return AxisKind.Other;
    }

    private enum AxisKind
    {
        Latitude,
        Longitude,
        Other
    }

    private static DateTime? TryParseDateTime(JsonElement attributes, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (!attributes.TryGetProperty(name, out var element))
            {
                continue;
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                var text = element.GetString();
                if (text.IsNullOrWhiteSpace())
                {
                    continue;
                }

                if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
                {
                    return dto.UtcDateTime;
                }

                if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
                {
                    return dt.ToUniversalTime();
                }
            }
        }

        return null;
    }

    private static double? TryGetDouble(JsonElement attributes, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (!attributes.TryGetProperty(name, out var element))
            {
                continue;
            }

            switch (element.ValueKind)
            {
                case JsonValueKind.Number:
                    return element.GetDouble();
                case JsonValueKind.String:
                    var text = element.GetString();
                    if (text.HasValue() &&
                        double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed))
                    {
                        return parsed;
                    }
                    break;
            }
        }

        return null;
    }

    private static void ExtractTemporalAndSpatial(JsonElement attrsRoot, ref DateTime? timeStart, ref DateTime? timeEnd, ref double[]? spatialExtent)
    {
        timeStart ??= TryParseDateTime(attrsRoot, "time_coverage_start", "start_time");
        timeEnd ??= TryParseDateTime(attrsRoot, "time_coverage_end", "end_time");

        if (spatialExtent is null)
        {
            var lonMin = TryGetDouble(attrsRoot, "geospatial_lon_min", "lon_min");
            var lonMax = TryGetDouble(attrsRoot, "geospatial_lon_max", "lon_max");
            var latMin = TryGetDouble(attrsRoot, "geospatial_lat_min", "lat_min");
            var latMax = TryGetDouble(attrsRoot, "geospatial_lat_max", "lat_max");

            if (lonMin.HasValue && lonMax.HasValue && latMin.HasValue && latMax.HasValue)
            {
                spatialExtent = new[] { lonMin.Value, latMin.Value, lonMax.Value, latMax.Value };
            }
        }
    }

    private static ParsedArrayMetadata ParseArrayMetadata(JsonElement zarrayElement)
    {
        var shape = zarrayElement.TryGetProperty("shape", out var shapeElement)
            ? shapeElement.EnumerateArray().Select(e => e.GetInt32()).ToArray()
            : Array.Empty<int>();

        var chunks = zarrayElement.TryGetProperty("chunks", out var chunksElement)
            ? chunksElement.EnumerateArray().Select(e => e.GetInt32()).ToArray()
            : new int[shape.Length];

        string? dtype = null;
        if (zarrayElement.TryGetProperty("dtype", out var dtypeElement))
        {
            dtype = dtypeElement.ValueKind == JsonValueKind.String ? dtypeElement.GetString() : dtypeElement.GetRawText();
        }

        object? fillValue = null;
        if (zarrayElement.TryGetProperty("fill_value", out var fillValueElement) &&
            fillValueElement.ValueKind != JsonValueKind.Null)
        {
            fillValue = ConvertJsonElement(fillValueElement);
        }

        string? compressorId = null;
        if (zarrayElement.TryGetProperty("compressor", out var compressorElement) &&
            compressorElement.ValueKind != JsonValueKind.Null)
        {
            compressorId = compressorElement.ValueKind == JsonValueKind.Object &&
                           compressorElement.TryGetProperty("id", out var idElement)
                ? idElement.GetString()
                : compressorElement.GetRawText();
        }

        int? zarrFormat = null;
        if (zarrayElement.TryGetProperty("zarr_format", out var zarrFormatElement))
        {
            zarrFormat = zarrFormatElement.GetInt32();
        }

        return new ParsedArrayMetadata(shape, chunks, dtype, fillValue, compressorId, zarrFormat);
    }

    private static List<ZarrAttributeInfo> ExtractAttributes(JsonElement attrsRoot)
    {
        var results = new List<ZarrAttributeInfo>();

        foreach (var property in attrsRoot.EnumerateObject())
        {
            if (string.Equals(property.Name, "_ARRAY_DIMENSIONS", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            results.Add(new ZarrAttributeInfo(property.Name, ConvertJsonElement(property.Value)));
        }

        return results;
    }

    private static string[] ResolveDimensionNames(JsonElement? attrsElement, int dimensionCount)
    {
        if (attrsElement.HasValue &&
            attrsElement.Value.TryGetProperty("_ARRAY_DIMENSIONS", out var dimsElement) &&
            dimsElement.ValueKind == JsonValueKind.Array)
        {
            var dimensionNames = dimsElement
                .EnumerateArray()
                .Select((entry, index) =>
                {
                    if (entry.ValueKind == JsonValueKind.String)
                    {
                        var value = entry.GetString();
                        if (value.HasValue())
                        {
                            return value!;
                        }
                    }

                    return $"dim{index}";
                })
                .ToArray();

            if (dimensionNames.Length == dimensionCount)
            {
                return dimensionNames;
            }
        }

        var fallback = new string[dimensionCount];
        for (var i = 0; i < fallback.Length; i++)
        {
            fallback[i] = $"dim{i}";
        }

        return fallback;
    }

    private static void RegisterDimensions(
        IDictionary<string, DimensionAccumulator> dimensionBuilders,
        IList<string> order,
        IReadOnlyList<string> dimensionNames,
        IReadOnlyList<int> shape,
        IReadOnlyList<int> chunks)
    {
        for (var i = 0; i < dimensionNames.Count; i++)
        {
            var name = dimensionNames[i];
            var size = i < shape.Count ? shape[i] : 0;
            var chunk = SafeChunk(chunks, i, size);

            if (!dimensionBuilders.TryGetValue(name, out var accumulator))
            {
                accumulator = new DimensionAccumulator(name);
                dimensionBuilders[name] = accumulator;
                order.Add(name);
            }

            accumulator.Update(size, chunk);
        }
    }

    private static int SafeChunk(IReadOnlyList<int> chunks, int index, int fallback)
    {
        if (index < chunks.Count)
        {
            var chunkValue = chunks[index];
            return chunkValue > 0 ? chunkValue : fallback;
        }

        return fallback;
    }

    private sealed record ParsedArrayMetadata(
        int[] Shape,
        int[] Chunks,
        string? DType,
        object? FillValue,
        string? Compressor,
        int? ZarrFormat);

    private sealed record VariableMetadata(
        string Name,
        int[] Shape,
        int[] Chunks,
        string[] DimensionNames,
        string? DType,
        object? FillValue,
        string? Compressor,
        IReadOnlyList<ZarrAttributeInfo> Attributes);

    private sealed class DimensionAccumulator
    {
        public DimensionAccumulator(string name)
        {
            Name = name;
        }

        public string Name { get; }
        public int Length { get; private set; }
        public int ChunkSize { get; private set; }

        public void Update(int length, int chunkSize)
        {
            if (length > Length)
            {
                Length = length;
            }

            if (chunkSize > 0 && chunkSize > ChunkSize)
            {
                ChunkSize = chunkSize;
            }
        }

        public ZarrDimensionInfo ToDimensionInfo()
        {
            var chunk = ChunkSize > 0 ? ChunkSize : (Length > 0 ? Length : 1);
            return new ZarrDimensionInfo(Name, Length, chunk);
        }
    }

    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number => element.TryGetInt64(out var longValue) ? longValue : element.GetDouble(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToArray(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value), StringComparer.OrdinalIgnoreCase),
            _ => element.GetRawText()
        };
    }

    /// <summary>
    /// Parse time coordinates from Zarr time array data.
    /// </summary>
    private IReadOnlyList<DateTimeOffset> ParseTimeCoordinates(Array timeData, ZarrArrayMetadata metadata)
    {
        var result = new List<DateTimeOffset>();

        if (timeData is byte[] bytes)
        {
            // Determine element size from dtype
            var elementSize = GetElementSize(metadata.DType);
            var count = bytes.Length / elementSize;

            for (int i = 0; i < count; i++)
            {
                var offset = i * elementSize;
                var value = ParseTimeValue(bytes, offset, metadata.DType);
                result.Add(ConvertToDateTimeOffset(value));
            }
        }

        return result;
    }

    /// <summary>
    /// Parse a single time value from byte array.
    /// </summary>
    private static double ParseTimeValue(byte[] bytes, int offset, string dtype)
    {
        if (dtype.Contains("float64") || dtype.Contains("f8"))
        {
            return BitConverter.ToDouble(bytes, offset);
        }
        if (dtype.Contains("float32") || dtype.Contains("f4"))
        {
            return BitConverter.ToSingle(bytes, offset);
        }
        if (dtype.Contains("int64") || dtype.Contains("i8"))
        {
            return BitConverter.ToInt64(bytes, offset);
        }
        if (dtype.Contains("int32") || dtype.Contains("i4"))
        {
            return BitConverter.ToInt32(bytes, offset);
        }

        return 0.0;
    }

    /// <summary>
    /// Convert numeric time value to DateTimeOffset.
    /// Assumes seconds since Unix epoch (1970-01-01).
    /// </summary>
    private static DateTimeOffset ConvertToDateTimeOffset(double value)
    {
        var epoch = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
        return epoch.AddSeconds(value);
    }

    /// <summary>
    /// Get element size in bytes from dtype string.
    /// </summary>
    private static int GetElementSize(string dtype)
    {
        if (dtype.Contains("float64") || dtype.Contains("f8") || dtype.Contains("int64") || dtype.Contains("i8"))
        {
            return 8;
        }
        if (dtype.Contains("float32") || dtype.Contains("f4") || dtype.Contains("int32") || dtype.Contains("i4"))
        {
            return 4;
        }
        if (dtype.Contains("int16") || dtype.Contains("i2"))
        {
            return 2;
        }
        if (dtype.Contains("uint8") || dtype.Contains("u1") || dtype.Contains("int8") || dtype.Contains("i1"))
        {
            return 1;
        }

        return 4; // Default
    }

    /// <summary>
    /// Convert array to byte array.
    /// </summary>
    private static byte[] ConvertArrayToBytes(Array data)
    {
        if (data is byte[] bytes)
        {
            return bytes;
        }

        // Simple conversion for basic types
        var buffer = new byte[Buffer.ByteLength(data)];
        Buffer.BlockCopy(data, 0, buffer, 0, buffer.Length);
        return buffer;
    }

    /// <summary>
    /// Convert byte array to 2D float array.
    /// </summary>
    private static float[,] ConvertBytesToFloat2D(byte[] data, ZarrArrayMetadata metadata)
    {
        // Assuming shape is [1, lat, lon] or [lat, lon]
        int rows, cols;
        if (metadata.Shape.Length == 3)
        {
            rows = metadata.Shape[1];
            cols = metadata.Shape[2];
        }
        else if (metadata.Shape.Length == 2)
        {
            rows = metadata.Shape[0];
            cols = metadata.Shape[1];
        }
        else
        {
            throw new InvalidOperationException($"Unexpected shape for 2D conversion: [{string.Join(", ", metadata.Shape)}]");
        }

        var result = new float[rows, cols];
        var elementSize = GetElementSize(metadata.DType);

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                var offset = (i * cols + j) * elementSize;
                if (offset + elementSize <= data.Length)
                {
                    result[i, j] = ParseFloatValue(data, offset, metadata.DType);
                }
            }
        }

        return result;
    }

    private static float[,,] ConvertBytesToFloat3D(IReadOnlyList<byte[]> slices, ZarrArrayMetadata metadata)
    {
        if (metadata.Shape.Length < 2)
        {
            throw new InvalidOperationException($"Unexpected shape for 3D conversion: [{string.Join(", ", metadata.Shape)}]");
        }

        var rows = metadata.Shape.Length >= 2 ? metadata.Shape[^2] : metadata.Shape[0];
        var cols = metadata.Shape.Length >= 2 ? metadata.Shape[^1] : 1;
        var timeCount = slices.Count;

        var result = new float[timeCount, rows, cols];

        for (var t = 0; t < timeCount; t++)
        {
            var sliceArray = ConvertBytesToFloat2D(slices[t], metadata);
            for (var i = 0; i < rows; i++)
            {
                for (var j = 0; j < cols; j++)
                {
                    result[t, i, j] = sliceArray[i, j];
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Parse a single float value from byte array.
    /// </summary>
    private static float ParseFloatValue(byte[] bytes, int offset, string dtype)
    {
        if (dtype.Contains("float64") || dtype.Contains("f8"))
        {
            return (float)BitConverter.ToDouble(bytes, offset);
        }
        if (dtype.Contains("float32") || dtype.Contains("f4"))
        {
            return BitConverter.ToSingle(bytes, offset);
        }
        if (dtype.Contains("int32") || dtype.Contains("i4"))
        {
            return BitConverter.ToInt32(bytes, offset);
        }
        if (dtype.Contains("int16") || dtype.Contains("i2"))
        {
            return BitConverter.ToInt16(bytes, offset);
        }

        return 0f;
    }

    private static bool SupportsAggregation(Readers.ZarrArrayMetadata metadata)
        => IsFloatingPointDType(metadata.DType);

    private static bool IsFloatingPointDType(string dtype)
    {
        if (dtype.IsNullOrWhiteSpace())
        {
            return false;
        }

        return dtype.IndexOf("float", StringComparison.OrdinalIgnoreCase) >= 0 ||
               dtype.IndexOf("f4", StringComparison.OrdinalIgnoreCase) >= 0 ||
               dtype.IndexOf("f8", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static (List<DateTimeOffset> Timestamps, List<byte[]> Slices) AggregateTimeSlices(
        IReadOnlyList<DateTimeOffset> timestamps,
        IReadOnlyList<byte[]> slices,
        Readers.ZarrArrayMetadata metadata,
        TimeSpan interval)
    {
        if (timestamps.Count != slices.Count)
        {
            throw new InvalidOperationException("Timestamp and data slice counts must match for aggregation.");
        }

        var resultTimestamps = new List<DateTimeOffset>(timestamps.Count);
        var resultSlices = new List<byte[]>(slices.Count);

        if (timestamps.Count == 0)
        {
            return (resultTimestamps, resultSlices);
        }

        var elementSize = GetElementSize(metadata.DType);
        if (elementSize <= 0)
        {
            throw new InvalidOperationException($"Unsupported element size for dtype '{metadata.DType}'.");
        }

        var elementsPerSlice = slices[0].Length / elementSize;
        var accumulator = new double[elementsPerSlice];

        var groupStartIndex = 0;
        while (groupStartIndex < slices.Count)
        {
            Array.Clear(accumulator, 0, accumulator.Length);
            var groupStartTime = timestamps[groupStartIndex];
            var windowEnd = groupStartTime + interval;
            var groupCount = 0;
            var index = groupStartIndex;

            for (; index < slices.Count; index++)
            {
                var timestamp = timestamps[index];
                if (groupCount > 0 && timestamp >= windowEnd)
                {
                    break;
                }

                AccumulateSlice(slices[index], accumulator, metadata.DType);
                groupCount++;
            }

            var averagedSlice = FinalizeAggregation(accumulator, groupCount, metadata.DType);
            resultTimestamps.Add(groupStartTime);
            resultSlices.Add(averagedSlice);

            groupStartIndex = index;
        }

        return (resultTimestamps, resultSlices);
    }

    private static void AccumulateSlice(byte[] slice, double[] accumulator, string dtype)
    {
        var elementSize = GetElementSize(dtype);
        for (var i = 0; i < accumulator.Length; i++)
        {
            var offset = i * elementSize;
            accumulator[i] += ReadFloatingPoint(slice, offset, dtype);
        }
    }

    private static byte[] FinalizeAggregation(double[] accumulator, int count, string dtype)
    {
        if (count <= 0)
        {
            throw new InvalidOperationException("Aggregation requires at least one slice.");
        }

        var elementSize = GetElementSize(dtype);
        var buffer = new byte[accumulator.Length * elementSize];

        for (var i = 0; i < accumulator.Length; i++)
        {
            var value = accumulator[i] / count;
            WriteFloatingPoint(buffer, i * elementSize, dtype, value);
        }

        return buffer;
    }

    private static double ReadFloatingPoint(byte[] bytes, int offset, string dtype)
    {
        if (dtype.IndexOf("f8", StringComparison.OrdinalIgnoreCase) >= 0 ||
            dtype.IndexOf("float64", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return BitConverter.ToDouble(bytes, offset);
        }

        if (dtype.IndexOf("f4", StringComparison.OrdinalIgnoreCase) >= 0 ||
            dtype.IndexOf("float32", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return BitConverter.ToSingle(bytes, offset);
        }

        throw new NotSupportedException($"Unsupported dtype '{dtype}' for aggregation.");
    }

    private static void WriteFloatingPoint(byte[] buffer, int offset, string dtype, double value)
    {
        if (dtype.IndexOf("f8", StringComparison.OrdinalIgnoreCase) >= 0 ||
            dtype.IndexOf("float64", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            var bytes = BitConverter.GetBytes(value);
            Buffer.BlockCopy(bytes, 0, buffer, offset, 8);
            return;
        }

        if (dtype.IndexOf("f4", StringComparison.OrdinalIgnoreCase) >= 0 ||
            dtype.IndexOf("float32", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            var bytes = BitConverter.GetBytes((float)value);
            Buffer.BlockCopy(bytes, 0, buffer, offset, 4);
            return;
        }

        throw new NotSupportedException($"Unsupported dtype '{dtype}' for aggregation.");
    }
}
