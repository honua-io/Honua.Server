// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text;
using System.Text.Json;

namespace Honua.MapSDK.Services.DataLoading;

/// <summary>
/// Streams large datasets in chunks for progressive rendering.
/// Useful for loading datasets with thousands of features without blocking the UI.
/// </summary>
public class StreamingLoader : IDisposable
{
    private readonly HttpClient _httpClient;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamingLoader"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client for making requests.</param>
    public StreamingLoader(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Streams GeoJSON features in chunks.
    /// </summary>
    /// <param name="url">The URL to load GeoJSON from.</param>
    /// <param name="chunkSize">Number of features per chunk.</param>
    /// <param name="onChunk">Callback invoked for each chunk of features.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StreamGeoJsonFeaturesAsync(
        string url,
        int chunkSize,
        Action<List<JsonElement>> onChunk,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(url))
            throw new ArgumentNullException(nameof(url));

        if (chunkSize <= 0)
            throw new ArgumentException("Chunk size must be greater than 0", nameof(chunkSize));

        if (onChunk == null)
            throw new ArgumentNullException(nameof(onChunk));

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var root = document.RootElement;
        if (!root.TryGetProperty("features", out var featuresArray))
        {
            throw new InvalidOperationException("GeoJSON does not contain 'features' property");
        }

        var chunk = new List<JsonElement>();
        foreach (var feature in featuresArray.EnumerateArray())
        {
            chunk.Add(feature);

            if (chunk.Count >= chunkSize)
            {
                onChunk(new List<JsonElement>(chunk));
                chunk.Clear();

                // Allow UI to update
                await Task.Delay(1, cancellationToken);
            }
        }

        // Send remaining features
        if (chunk.Count > 0)
        {
            onChunk(chunk);
        }
    }

    /// <summary>
    /// Streams large text data line by line.
    /// </summary>
    /// <param name="url">The URL to load data from.</param>
    /// <param name="onLine">Callback invoked for each line.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StreamLinesAsync(
        string url,
        Action<string> onLine,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(url))
            throw new ArgumentNullException(nameof(url));

        if (onLine == null)
            throw new ArgumentNullException(nameof(onLine));

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync();
            if (line != null)
            {
                onLine(line);
            }
        }
    }

    /// <summary>
    /// Streams CSV data with progressive parsing.
    /// </summary>
    /// <param name="url">The URL to load CSV from.</param>
    /// <param name="onRow">Callback invoked for each row (as string array).</param>
    /// <param name="hasHeader">Whether the CSV has a header row.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The header row if hasHeader is true; otherwise, null.</returns>
    public async Task<string[]?> StreamCsvAsync(
        string url,
        Action<string[]> onRow,
        bool hasHeader = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(url))
            throw new ArgumentNullException(nameof(url));

        if (onRow == null)
            throw new ArgumentNullException(nameof(onRow));

        string[]? header = null;
        var isFirstRow = true;

        await StreamLinesAsync(url, line =>
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            var values = ParseCsvLine(line);

            if (isFirstRow && hasHeader)
            {
                header = values;
                isFirstRow = false;
                return;
            }

            isFirstRow = false;
            onRow(values);
        }, cancellationToken);

        return header;
    }

    /// <summary>
    /// Streams JSON array elements progressively.
    /// </summary>
    /// <param name="url">The URL to load JSON array from.</param>
    /// <param name="chunkSize">Number of elements per chunk.</param>
    /// <param name="onChunk">Callback invoked for each chunk.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StreamJsonArrayAsync(
        string url,
        int chunkSize,
        Action<List<JsonElement>> onChunk,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(url))
            throw new ArgumentNullException(nameof(url));

        if (chunkSize <= 0)
            throw new ArgumentException("Chunk size must be greater than 0", nameof(chunkSize));

        if (onChunk == null)
            throw new ArgumentNullException(nameof(onChunk));

        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("JSON root is not an array");
        }

        var chunk = new List<JsonElement>();
        foreach (var element in root.EnumerateArray())
        {
            chunk.Add(element);

            if (chunk.Count >= chunkSize)
            {
                onChunk(new List<JsonElement>(chunk));
                chunk.Clear();

                // Allow UI to update
                await Task.Delay(1, cancellationToken);
            }
        }

        // Send remaining elements
        if (chunk.Count > 0)
        {
            onChunk(chunk);
        }
    }

    /// <summary>
    /// Parses a CSV line, handling quoted values and commas within quotes.
    /// </summary>
    private static string[] ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    // Escaped quote
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        values.Add(current.ToString());
        return values.ToArray();
    }

    /// <summary>
    /// Disposes the streaming loader and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
