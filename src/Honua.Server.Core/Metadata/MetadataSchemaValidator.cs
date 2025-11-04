// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Honua.Server.Core.Caching;
using Json.Schema;
using Microsoft.Extensions.Caching.Memory;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Metadata;

public sealed class MetadataSchemaValidator : IMetadataSchemaValidator
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly object FetchInitializationLock = new();
    private static Func<Uri, IBaseDocument?>? PreviousFetch;

    private readonly IMemoryCache _schemaCache;
    private readonly JsonSchema _schema;
    private readonly Uri _schemaUri;

    private MetadataSchemaValidator(JsonSchema schema, Uri schemaUri, IReadOnlyDictionary<string, JsonSchema>? definitions, IMemoryCache cache)
    {
        _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        _schemaUri = schemaUri ?? throw new ArgumentNullException(nameof(schemaUri));
        _schemaCache = cache ?? throw new ArgumentNullException(nameof(cache));

        var cacheOptions = CacheOptionsBuilder.ForSchemaValidation().BuildMemory();

        _schemaCache.Set(_schemaUri.ToString(), _schema, cacheOptions);
        SchemaRegistry.Global.Register(_schemaUri, _schema);

        if (definitions is not null)
        {
            foreach (var pair in definitions)
            {
                var defUri = $"{_schemaUri}#/$defs/{pair.Key}";
                _schemaCache.Set(defUri, pair.Value, cacheOptions);
            }
        }

        if (PreviousFetch is null)
        {
            lock (FetchInitializationLock)
            {
                if (PreviousFetch is null)
                {
                    PreviousFetch = SchemaRegistry.Global.Fetch;
                    SchemaRegistry.Global.Fetch = uri =>
                    {
                        var key = uri.ToString();
                        if (_schemaCache.TryGetValue(key, out JsonSchema? cached))
                        {
                            return cached;
                        }

                        var baseKey = uri.GetLeftPart(UriPartial.Path);
                        if (_schemaCache.TryGetValue(baseKey, out cached))
                        {
                            return cached;
                        }

                        return PreviousFetch?.Invoke(uri);
                    };
                }
            }
        }
    }

    public static MetadataSchemaValidator CreateDefault(IMemoryCache? cache = null)
    {
        // Create a default memory cache if none provided
        cache ??= new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 100 // Limit to 100 schema entries
        });

        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Honua.Server.Core.schemas.metadata-schema.json")
            ?? throw new InvalidOperationException("Embedded metadata schema was not found.");

        using var reader = new StreamReader(stream);
        var schemaText = reader.ReadToEnd();
        var schemaNode = JsonNode.Parse(schemaText) ?? throw new InvalidDataException("Metadata schema JSON is invalid.");

        if (schemaNode is not JsonObject root)
        {
            throw new InvalidDataException("Metadata schema root is not an object.");
        }

        if (!root.TryGetPropertyValue("$defs", out var defsValue) || defsValue is not JsonObject defsObject)
        {
            defsObject = new JsonObject();
            root["$defs"] = defsObject;
        }

        var referencedDefinitions = ExtractReferencedDefinitions(schemaText);

        if (root.TryGetPropertyValue("properties", out var propertiesValue) && propertiesValue is JsonObject propertiesObject)
        {
            foreach (var name in referencedDefinitions)
            {
                AddDefinitionIfMissing(name, propertiesObject, defsObject);
            }
        }

        var updatedSchemaText = schemaNode.ToJsonString();

        var definitions = new Dictionary<string, JsonSchema>(StringComparer.Ordinal);
        foreach (var (name, node) in defsObject)
        {
            if (node is JsonNode defNode)
            {
                var defSchema = JsonSchema.FromText(defNode.ToJsonString());
                definitions[name] = defSchema;
            }
        }

        var schema = JsonSchema.FromText(updatedSchemaText);
        var schemaUri = new Uri("https://honua.dev/schemas/metadata.schema.json", UriKind.Absolute);
        return new MetadataSchemaValidator(schema, schemaUri, definitions, cache);
    }

    public MetadataSchemaValidationResult Validate(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return MetadataSchemaValidationResult.Failure(new[] { "Metadata payload is empty." });
        }

        JsonNode? document;
        try
        {
            document = JsonNode.Parse(json, documentOptions: SerializerOptions.GetDocumentOptions());
        }
        catch (JsonException ex)
        {
            return MetadataSchemaValidationResult.Failure(new[] { $"Metadata contains invalid JSON: {ex.Message}" });
        }

        var options = new EvaluationOptions { OutputFormat = OutputFormat.Hierarchical };
        options.SchemaRegistry.Fetch = uri =>
        {
            var key = uri.ToString();
            if (_schemaCache.TryGetValue(key, out JsonSchema? cached))
            {
                return cached;
            }

            var baseKey = uri.GetLeftPart(UriPartial.Path);
            if (_schemaCache.TryGetValue(baseKey, out cached))
            {
                return cached;
            }

            return PreviousFetch?.Invoke(uri);
        };
        options.SchemaRegistry.Register(_schemaUri, _schema);
        options.SchemaRegistry.Initialize(_schemaUri, _schema);

        EvaluationResults evaluation;
        try
        {
            evaluation = _schema.Evaluate(document, options);
        }
        catch (RefResolutionException ex)
        {
            return MetadataSchemaValidationResult.Failure(new[] { $"Metadata schema resolution failed: {ex.Message}" });
        }
        if (evaluation.IsValid)
        {
            return MetadataSchemaValidationResult.Success();
        }

        var errors = new List<string>();
        CollectErrors(evaluation, errors);
        if (errors.Count == 0)
        {
            errors.Add("Metadata failed schema validation.");
        }

        return MetadataSchemaValidationResult.Failure(errors);
}

    private static void AddDefinitionIfMissing(string name, JsonObject properties, JsonObject defs)
    {
        if (!properties.TryGetPropertyValue(name, out var value) || value is not JsonNode node)
        {
            return;
        }

        if (!defs.ContainsKey(name))
        {
            defs[name] = node.DeepClone();
        }
    }

    private static HashSet<string> ExtractReferencedDefinitions(string schemaText)
    {
        var references = new HashSet<string>(StringComparer.Ordinal);
        const string marker = "#/$defs/";

        var index = -1;
        while ((index = schemaText.IndexOf(marker, index + 1, StringComparison.Ordinal)) >= 0)
        {
            var start = index + marker.Length;
            var end = start;
            while (end < schemaText.Length)
            {
                var ch = schemaText[end];
                if (char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' )
                {
                    end++;
                    continue;
                }

                break;
            }

            if (end > start)
            {
                var name = schemaText.Substring(start, end - start);
                references.Add(name);
            }
        }

        return references;
    }

    private static void CollectErrors(EvaluationResults results, List<string> errors)
    {
        if (results.Errors is not null)
        {
            foreach (var pair in results.Errors)
            {
                var location = results.InstanceLocation.ToString();
                var message = pair.Value;
                if (!string.IsNullOrWhiteSpace(pair.Key))
                {
                    message = $"{message} (keyword: {pair.Key})";
                }

                if (!string.IsNullOrWhiteSpace(location) && location != "#")
                {
                    errors.Add($"{location}: {message}");
                }
                else
                {
                    errors.Add(message);
                }
            }
        }

        if (results.Details is null)
        {
            return;
        }

        foreach (var detail in results.Details)
        {
            CollectErrors(detail, errors);
        }
    }
}

internal static class JsonSerializerOptionsExtensions
{
    public static JsonDocumentOptions GetDocumentOptions(this JsonSerializerOptions options)
        => new()
        {
            AllowTrailingCommas = options.AllowTrailingCommas,
            CommentHandling = options.ReadCommentHandling
        };
}
