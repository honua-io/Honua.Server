// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Performance;

namespace Honua.Server.Core.Utilities;

/// <summary>
/// Centralized JSON serialization/deserialization utility with consistent error handling and configuration.
/// Provides helpers for both System.Text.Json operations with security-hardened defaults.
/// </summary>
public static class JsonHelper
{
    /// <summary>
    /// Default serializer options for general use.
    /// Delegates to <see cref="JsonSerializerOptionsRegistry.Web"/> for hot metadata cache benefits.
    /// </summary>
    /// <remarks>
    /// PERFORMANCE: This property now delegates to the centralized registry instead of creating
    /// its own options instance. This ensures the metadata cache stays hot across all serialization
    /// calls, providing 2-3x faster serialization via source-generated metadata.
    ///
    /// The registry uses strict Web defaults (no trailing commas, no comments) for standards-compliant
    /// JSON output. If you need relaxed parsing for development/tooling, use
    /// <see cref="JsonSerializerOptionsRegistry.DevTooling"/> directly.
    /// </remarks>
    public static JsonSerializerOptions DefaultOptions =>
        JsonSerializerOptionsRegistry.Web;

    /// <summary>
    /// Security-hardened serializer options with depth limits to prevent DoS attacks.
    /// Delegates to <see cref="JsonSerializerOptionsRegistry.SecureUntrusted"/> for maximum security.
    /// </summary>
    /// <remarks>
    /// SECURITY: This property now delegates to the registry's SecureUntrusted options, which provide:
    /// - MaxDepth=32 (lower than Web) to prevent stack overflow attacks
    /// - AllowTrailingCommas=false to enforce strict parsing
    /// - ReadCommentHandling.Disallow to prevent comment-based attacks
    /// - Hot metadata cache via source-generated resolvers
    ///
    /// Use this when deserializing JSON from untrusted sources (external APIs, user uploads, webhooks).
    /// </remarks>
    public static JsonSerializerOptions SecureOptions =>
        JsonSerializerOptionsRegistry.SecureUntrusted;

    /// <summary>
    /// Serializes an object to a JSON string.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="obj">The object to serialize.</param>
    /// <param name="options">Optional serializer options. If null, uses <see cref="DefaultOptions"/>.</param>
    /// <returns>JSON string representation of the object.</returns>
    /// <exception cref="ArgumentNullException">Thrown when obj is null.</exception>
    /// <exception cref="JsonException">Thrown when serialization fails.</exception>
    public static string Serialize<T>(
        [DisallowNull] T obj,
        JsonSerializerOptions? options = null)
    {
        if (obj is null)
        {
            throw new ArgumentNullException(nameof(obj));
        }

        try
        {
            return JsonSerializer.Serialize(obj, options ?? DefaultOptions);
        }
        catch (Exception ex) when (ex is NotSupportedException)
        {
            throw new JsonException($"Failed to serialize object of type {typeof(T).Name}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Serializes an object to a JSON string with indentation for readability.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="obj">The object to serialize.</param>
    /// <param name="options">Optional serializer options. If null, uses <see cref="JsonSerializerOptionsRegistry.WebIndented"/>.</param>
    /// <returns>Indented JSON string representation of the object.</returns>
    /// <exception cref="ArgumentNullException">Thrown when obj is null.</exception>
    /// <exception cref="JsonException">Thrown when serialization fails.</exception>
    /// <remarks>
    /// PERFORMANCE: Now uses the registry's WebIndented options for hot metadata cache benefits.
    /// </remarks>
    public static string SerializeIndented<T>(
        [DisallowNull] T obj,
        JsonSerializerOptions? options = null)
    {
        if (obj is null)
        {
            throw new ArgumentNullException(nameof(obj));
        }

        var serializerOptions = options ?? JsonSerializerOptionsRegistry.WebIndented;

        try
        {
            return JsonSerializer.Serialize(obj, serializerOptions);
        }
        catch (Exception ex) when (ex is NotSupportedException)
        {
            throw new JsonException($"Failed to serialize object of type {typeof(T).Name}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deserializes a JSON string to an object.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="options">Optional serializer options. If null, uses <see cref="DefaultOptions"/>.</param>
    /// <returns>Deserialized object, or null if the JSON represents null.</returns>
    /// <exception cref="ArgumentException">Thrown when json is null or whitespace.</exception>
    /// <exception cref="JsonException">Thrown when deserialization fails.</exception>
    public static T? Deserialize<T>(
        string json,
        JsonSerializerOptions? options = null)
    {
        if (json.IsNullOrWhiteSpace())
        {
            throw new ArgumentException("JSON string cannot be null or whitespace.", nameof(json));
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, options ?? DefaultOptions);
        }
        catch (JsonException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new JsonException($"Failed to deserialize JSON to type {typeof(T).Name}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Attempts to deserialize a JSON string to an object without throwing exceptions.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="result">The deserialized object if successful, otherwise default(T).</param>
    /// <param name="error">The exception that occurred during deserialization, if any.</param>
    /// <param name="options">Optional serializer options. If null, uses <see cref="DefaultOptions"/>.</param>
    /// <returns>True if deserialization succeeded, false otherwise.</returns>
    public static bool TryDeserialize<T>(
        string? json,
        [NotNullWhen(true)] out T? result,
        out Exception? error,
        JsonSerializerOptions? options = null)
    {
        result = default;
        error = null;

        if (json.IsNullOrWhiteSpace())
        {
            error = new ArgumentException("JSON string is null or whitespace.");
            return false;
        }

        try
        {
            result = JsonSerializer.Deserialize<T>(json, options ?? DefaultOptions);
            return result is not null;
        }
        catch (Exception ex)
        {
            error = ex;
            return false;
        }
    }

    /// <summary>
    /// Deserializes JSON from a stream asynchronously.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="stream">The stream containing JSON data.</param>
    /// <param name="options">Optional serializer options. If null, uses <see cref="DefaultOptions"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Deserialized object, or null if the JSON represents null.</returns>
    /// <exception cref="ArgumentNullException">Thrown when stream is null.</exception>
    /// <exception cref="JsonException">Thrown when deserialization fails.</exception>
    public static async Task<T?> DeserializeAsync<T>(
        Stream stream,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(stream);

        try
        {
            return await JsonSerializer.DeserializeAsync<T>(
                stream,
                options ?? DefaultOptions,
                cancellationToken);
        }
        catch (JsonException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new JsonException($"Failed to deserialize JSON stream to type {typeof(T).Name}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Serializes an object to a stream asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="stream">The stream to write JSON data to.</param>
    /// <param name="obj">The object to serialize.</param>
    /// <param name="options">Optional serializer options. If null, uses <see cref="DefaultOptions"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentNullException">Thrown when stream or obj is null.</exception>
    /// <exception cref="JsonException">Thrown when serialization fails.</exception>
    public static async Task SerializeAsync<T>(
        Stream stream,
        [DisallowNull] T obj,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(stream);
        if (obj is null)
        {
            throw new ArgumentNullException(nameof(obj));
        }

        try
        {
            await JsonSerializer.SerializeAsync(
                stream,
                obj,
                options ?? DefaultOptions,
                cancellationToken);
        }
        catch (Exception ex) when (ex is NotSupportedException)
        {
            throw new JsonException($"Failed to serialize object of type {typeof(T).Name} to stream: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Serializes a JsonNode to a JSON string.
    /// </summary>
    /// <param name="node">The JsonNode to serialize.</param>
    /// <param name="options">Optional serializer options. If null, uses <see cref="DefaultOptions"/>.</param>
    /// <returns>JSON string representation of the node, or null if the node is null.</returns>
    public static string? SerializeNode(JsonNode? node, JsonSerializerOptions? options = null)
    {
        if (node is null)
        {
            return null;
        }

        return node.ToJsonString(options ?? DefaultOptions);
    }

    /// <summary>
    /// Deserializes a JSON string to a JsonNode.
    /// </summary>
    /// <param name="json">The JSON string to parse.</param>
    /// <param name="options">Optional document options for parsing.</param>
    /// <returns>Parsed JsonNode, or null if the JSON is null or whitespace.</returns>
    /// <exception cref="JsonException">Thrown when parsing fails.</exception>
    public static JsonNode? DeserializeNode(string? json, JsonDocumentOptions? options = null)
    {
        if (json.IsNullOrWhiteSpace())
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new JsonException($"Failed to parse JSON to JsonNode: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Creates custom JsonSerializerOptions with specified settings.
    /// </summary>
    /// <param name="writeIndented">Whether to write indented JSON for readability.</param>
    /// <param name="camelCase">Whether to use camelCase naming policy.</param>
    /// <param name="caseInsensitive">Whether property name matching should be case-insensitive.</param>
    /// <param name="ignoreNullValues">Whether to ignore null values during serialization.</param>
    /// <param name="maxDepth">Maximum depth for nested objects (default 64 for security).</param>
    /// <returns>Configured JsonSerializerOptions instance.</returns>
    /// <remarks>
    /// OBSOLETE: This method creates new JsonSerializerOptions instances, which defeats the metadata cache purpose.
    /// Use <see cref="JsonSerializerOptionsRegistry.Web"/>, <see cref="JsonSerializerOptionsRegistry.WebIndented"/>,
    /// <see cref="JsonSerializerOptionsRegistry.SecureUntrusted"/>, or <see cref="JsonSerializerOptionsRegistry.DevTooling"/> instead.
    /// </remarks>
    [Obsolete("Use JsonSerializerOptionsRegistry instead to benefit from hot metadata cache (2-3x faster). " +
              "This method creates new options instances which causes cold metadata cache on every call. " +
              "Use JsonSerializerOptionsRegistry.Web, WebIndented, SecureUntrusted, or DevTooling.")]
    public static JsonSerializerOptions CreateOptions(
        bool writeIndented = false,
        bool camelCase = true,
        bool caseInsensitive = true,
        bool ignoreNullValues = false,
        int maxDepth = 64)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = writeIndented,
            PropertyNameCaseInsensitive = caseInsensitive,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            MaxDepth = maxDepth
        };

        if (camelCase)
        {
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        }

        if (ignoreNullValues)
        {
            options.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        }

        return options;
    }

    /// <summary>
    /// Loads and deserializes JSON from a file.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="filePath">Path to the JSON file.</param>
    /// <param name="options">Optional serializer options. If null, uses <see cref="DefaultOptions"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Deserialized object.</returns>
    /// <exception cref="ArgumentException">Thrown when filePath is null or whitespace.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="JsonException">Thrown when deserialization fails.</exception>
    public static async Task<T?> LoadFromFileAsync<T>(
        string filePath,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"JSON file not found at '{filePath}'", filePath);
        }

        try
        {
            await using var stream = File.OpenRead(filePath);
            return await DeserializeAsync<T>(stream, options, cancellationToken);
        }
        catch (JsonException)
        {
            throw;
        }
        catch (FileNotFoundException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new JsonException($"Failed to read JSON file '{filePath}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Serializes an object and saves it to a file.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="filePath">Path where the JSON file should be saved.</param>
    /// <param name="obj">The object to serialize.</param>
    /// <param name="options">Optional serializer options. If null, uses <see cref="JsonSerializerOptionsRegistry.WebIndented"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentException">Thrown when filePath is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when obj is null.</exception>
    /// <exception cref="JsonException">Thrown when serialization or file write fails.</exception>
    /// <remarks>
    /// PERFORMANCE: Now uses the registry's WebIndented options for hot metadata cache benefits.
    /// </remarks>
    public static async Task SaveToFileAsync<T>(
        string filePath,
        [DisallowNull] T obj,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(filePath);
        if (obj is null)
        {
            throw new ArgumentNullException(nameof(obj));
        }

        var serializerOptions = options ?? JsonSerializerOptionsRegistry.WebIndented;

        try
        {
            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var stream = File.Create(filePath);
            await SerializeAsync(stream, obj, serializerOptions, cancellationToken);
        }
        catch (JsonException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new JsonException($"Failed to write JSON file '{filePath}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Clones an object by serializing and deserializing it.
    /// Useful for creating deep copies of objects that are not ICloneable.
    /// </summary>
    /// <typeparam name="T">The type of the object to clone.</typeparam>
    /// <param name="obj">The object to clone.</param>
    /// <param name="options">Optional serializer options. If null, uses <see cref="DefaultOptions"/>.</param>
    /// <returns>A deep clone of the object.</returns>
    /// <exception cref="ArgumentNullException">Thrown when obj is null.</exception>
    /// <exception cref="JsonException">Thrown when clone operation fails.</exception>
    public static T Clone<T>([DisallowNull] T obj, JsonSerializerOptions? options = null)
    {
        if (obj is null)
        {
            throw new ArgumentNullException(nameof(obj));
        }

        try
        {
            var json = Serialize(obj, options);
            return Deserialize<T>(json, options) ?? throw new JsonException("Clone operation resulted in null value.");
        }
        catch (JsonException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new JsonException($"Failed to clone object of type {typeof(T).Name}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Validates that a JSON string is well-formed without deserializing it.
    /// </summary>
    /// <param name="json">The JSON string to validate.</param>
    /// <param name="error">The validation error message, if any.</param>
    /// <returns>True if the JSON is valid, false otherwise.</returns>
    public static bool IsValidJson(string? json, out string? error)
    {
        error = null;

        if (json.IsNullOrWhiteSpace())
        {
            error = "JSON string is null or whitespace.";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
