// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Services.Plugins;

/// <summary>
/// Semantic Kernel plugin for RAG-powered documentation search.
/// Provides context-aware documentation retrieval for the AI assistant.
/// </summary>
public class DocumentationSearchPlugin
{
    private readonly string _docsPath;
    private static readonly string[] SearchableExtensions = { ".md", ".txt" };

    public DocumentationSearchPlugin(string? docsPath = null)
    {
        // Default to project docs/rag directory
        _docsPath = docsPath ?? Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "docs", "rag");
    }

    [KernelFunction, Description("Search Honua documentation for relevant information")]
    public async Task<string> SearchDocumentation(
        [Description("The search query or question about Honua")] string query,
        [Description("Maximum number of results to return")] int maxResults = 5)
    {
        if (query.IsNullOrWhiteSpace())
        {
            return JsonSerializer.Serialize(new { success = false, error = "Query cannot be empty" });
        }

        try
        {
            var results = await SearchDocumentsAsync(query, maxResults);

            if (!results.Any())
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    results = Array.Empty<object>(),
                    message = "No relevant documentation found. Try rephrasing your query."
                });
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                query,
                results = results.Select(r => new
                {
                    file = r.FilePath,
                    title = r.Title,
                    excerpt = r.Excerpt,
                    relevance = r.RelevanceScore,
                    section = r.Section
                }).ToArray()
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Documentation search failed: {ex.Message}"
            });
        }
    }

    [KernelFunction, Description("Get detailed content from a specific documentation file")]
    public async Task<string> GetDocumentContent(
        [Description("Path to the documentation file")] string filePath,
        [Description("Specific section to extract (optional)")] string? section = null)
    {
        try
        {
            var fullPath = Path.IsPathRooted(filePath)
                ? filePath
                : Path.Combine(_docsPath, filePath);

            if (!File.Exists(fullPath))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Document not found: {filePath}"
                });
            }

            var content = await File.ReadAllTextAsync(fullPath);

            // Extract specific section if requested
            if (section.HasValue())
            {
                content = ExtractSection(content, section);
            }

            return JsonSerializer.Serialize(new
            {
                success = true,
                file = filePath,
                content,
                section
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Failed to read document: {ex.Message}"
            });
        }
    }

    [KernelFunction, Description("List all available documentation topics and categories")]
    public Task<string> ListDocumentationTopics()
    {
        try
        {
            if (!Directory.Exists(_docsPath))
            {
                return Task.FromResult(JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Documentation directory not found"
                }));
            }

            var topics = Directory.GetDirectories(_docsPath)
                .Select(d => new
                {
                    category = Path.GetFileName(d),
                    path = d,
                    files = Directory.GetFiles(d, "*.md")
                        .Select(f => new
                        {
                            name = Path.GetFileName(f),
                            path = Path.GetRelativePath(_docsPath, f),
                            title = ExtractTitle(File.ReadAllText(f))
                        })
                        .ToArray()
                })
                .ToArray();

            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = true,
                basePath = _docsPath,
                topics
            }));
        }
        catch (Exception ex)
        {
            return Task.FromResult(JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Failed to list topics: {ex.Message}"
            }));
        }
    }

    private async Task<DocumentSearchResult[]> SearchDocumentsAsync(string query, int maxResults)
    {
        if (!Directory.Exists(_docsPath))
        {
            return Array.Empty<DocumentSearchResult>();
        }

        var results = new List<DocumentSearchResult>();
        var queryTerms = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Recursively search all markdown files
        var files = Directory.GetFiles(_docsPath, "*.md", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            var content = await File.ReadAllTextAsync(file);
            var relativePath = Path.GetRelativePath(_docsPath, file);

            // Calculate relevance score
            var score = CalculateRelevanceScore(content, queryTerms);

            if (score > 0)
            {
                var title = ExtractTitle(content);
                var excerpt = ExtractRelevantExcerpt(content, queryTerms);
                var section = ExtractSection(content, queryTerms);

                results.Add(new DocumentSearchResult
                {
                    FilePath = relativePath,
                    Title = title,
                    Excerpt = excerpt,
                    Section = section,
                    RelevanceScore = score
                });
            }
        }

        return results
            .OrderByDescending(r => r.RelevanceScore)
            .Take(maxResults)
            .ToArray();
    }

    private double CalculateRelevanceScore(string content, string[] queryTerms)
    {
        var lowerContent = content.ToLowerInvariant();
        var score = 0.0;

        foreach (var term in queryTerms)
        {
            // Title matches are worth more
            if (lowerContent.StartsWith($"# {term}"))
                score += 10.0;

            // Heading matches
            var headingMatches = System.Text.RegularExpressions.Regex.Matches(
                lowerContent, $@"#{1,6}\s+[^\n]*{term}[^\n]*");
            score += headingMatches.Count * 5.0;

            // Keyword matches
            if (lowerContent.Contains($"**keywords**") &&
                lowerContent.Contains(term))
                score += 8.0;

            // Content matches
            var contentMatches = System.Text.RegularExpressions.Regex.Matches(
                lowerContent, $@"\b{term}\b");
            score += contentMatches.Count * 1.0;
        }

        return score;
    }

    private string ExtractTitle(string content)
    {
        var lines = content.Split('\n');
        var titleLine = lines.FirstOrDefault(l => l.TrimStart().StartsWith("# "));
        return titleLine?.TrimStart('#', ' ').Trim() ?? "Untitled";
    }

    private string ExtractRelevantExcerpt(string content, string[] queryTerms)
    {
        const int excerptLength = 200;
        var lowerContent = content.ToLowerInvariant();

        // Find first occurrence of any query term
        var firstMatchIndex = queryTerms
            .Select(term => lowerContent.IndexOf(term, StringComparison.Ordinal))
            .Where(idx => idx >= 0)
            .OrderBy(idx => idx)
            .FirstOrDefault();

        if (firstMatchIndex < 0)
            return content.Substring(0, Math.Min(excerptLength, content.Length)) + "...";

        // Extract context around the match
        var startIndex = Math.Max(0, firstMatchIndex - 50);
        var endIndex = Math.Min(content.Length, startIndex + excerptLength);

        var excerpt = content.Substring(startIndex, endIndex - startIndex);
        return (startIndex > 0 ? "..." : "") + excerpt.Trim() + "...";
    }

    private string ExtractSection(string content, string[] queryTerms)
    {
        var lines = content.Split('\n');
        var currentSection = "";

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].TrimStart().StartsWith("##"))
            {
                currentSection = lines[i].TrimStart('#', ' ').Trim();
            }

            if (queryTerms.Any(term => lines[i].ToLowerInvariant().Contains(term)))
            {
                return currentSection;
            }
        }

        return "";
    }

    private string ExtractSection(string content, string sectionName)
    {
        if (sectionName.IsNullOrWhiteSpace())
            return content;

        var lines = content.Split('\n');
        var inSection = false;
        var sectionContent = new StringBuilder();
        var sectionLevel = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();

            if (trimmed.StartsWith("#"))
            {
                var currentLevel = trimmed.TakeWhile(c => c == '#').Count();
                var currentTitle = trimmed.TrimStart('#', ' ').Trim();

                if (currentTitle.Contains(sectionName, StringComparison.OrdinalIgnoreCase))
                {
                    inSection = true;
                    sectionLevel = currentLevel;
                    sectionContent.AppendLine(line);
                    continue;
                }

                if (inSection && currentLevel <= sectionLevel)
                {
                    // End of section
                    break;
                }
            }

            if (inSection)
            {
                sectionContent.AppendLine(line);
            }
        }

        return sectionContent.Length > 0 ? sectionContent.ToString() : content;
    }

    private class DocumentSearchResult
    {
        public required string FilePath { get; set; }
        public required string Title { get; set; }
        public required string Excerpt { get; set; }
        public required string Section { get; set; }
        public double RelevanceScore { get; set; }
    }
}
