// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Cli.AI.Services.VectorSearch;

public sealed class VectorSearchOptions
{
    public const string SectionName = "VectorSearch";

    public string Provider { get; set; } = VectorSearchProviders.InMemory;

    public string IndexName { get; set; } = "deployment-patterns";

    public int Dimensions { get; set; } = 1536;

    public AzureVectorSearchOptions Azure { get; set; } = new();
}

public static class VectorSearchProviders
{
    public const string InMemory = "InMemory";
    public const string AzureAiSearch = "AzureAiSearch";
}

public sealed class AzureVectorSearchOptions
{
    public string? Endpoint { get; set; }

    public string? ApiKey { get; set; }

    public string IndexPrefix { get; set; } = "honua";

    public int Dimensions { get; set; } = 1536;
}
