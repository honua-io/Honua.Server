// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Enterprise.ETL.Templates;

/// <summary>
/// File-based workflow template repository that loads templates from JSON files
/// </summary>
public class JsonWorkflowTemplateRepository : IWorkflowTemplateRepository
{
    private readonly ILogger<JsonWorkflowTemplateRepository> _logger;
    private readonly string _templatesPath;
    private readonly Dictionary<string, WorkflowTemplate> _templates = new();
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private bool _isLoaded = false;

    public JsonWorkflowTemplateRepository(ILogger<JsonWorkflowTemplateRepository> logger)
    {
        _logger = logger;

        // Default path: Templates/Library directory relative to assembly
        var assemblyPath = Path.GetDirectoryName(typeof(JsonWorkflowTemplateRepository).Assembly.Location);
        _templatesPath = Path.Combine(assemblyPath!, "ETL", "Templates", "Library");
    }

    public JsonWorkflowTemplateRepository(ILogger<JsonWorkflowTemplateRepository> logger, string templatesPath)
    {
        _logger = logger;
        _templatesPath = templatesPath;
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_isLoaded)
        {
            return;
        }

        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            if (_isLoaded)
            {
                return;
            }

            await LoadTemplatesAsync(cancellationToken);
            _isLoaded = true;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private async Task LoadTemplatesAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Loading workflow templates from {Path}", _templatesPath);

        if (!Directory.Exists(_templatesPath))
        {
            _logger.LogWarning("Templates directory not found: {Path}", _templatesPath);
            return;
        }

        var jsonFiles = Directory.GetFiles(_templatesPath, "*.json", SearchOption.AllDirectories);
        _logger.LogInformation("Found {Count} template files", jsonFiles.Length);

        foreach (var file in jsonFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken);
                var template = JsonSerializer.Deserialize<WorkflowTemplate>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (template != null && !string.IsNullOrWhiteSpace(template.Id))
                {
                    _templates[template.Id] = template;
                    _logger.LogDebug("Loaded template: {Id} - {Name}", template.Id, template.Name);
                }
                else
                {
                    _logger.LogWarning("Invalid template in file: {File}", file);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading template from file: {File}", file);
            }
        }

        _logger.LogInformation("Loaded {Count} workflow templates", _templates.Count);
    }

    public async Task<List<WorkflowTemplate>> GetAllTemplatesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        return _templates.Values.OrderBy(t => t.Category).ThenBy(t => t.Name).ToList();
    }

    public async Task<WorkflowTemplate?> GetTemplateByIdAsync(string templateId, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        return _templates.TryGetValue(templateId, out var template) ? template : null;
    }

    public async Task<List<WorkflowTemplate>> GetTemplatesByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        return _templates.Values
            .Where(t => t.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.Name)
            .ToList();
    }

    public async Task<List<WorkflowTemplate>> GetTemplatesByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        return _templates.Values
            .Where(t => t.Tags.Any(tg => tg.Equals(tag, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(t => t.Name)
            .ToList();
    }

    public async Task<List<WorkflowTemplate>> GetFeaturedTemplatesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        return _templates.Values
            .Where(t => t.IsFeatured)
            .OrderByDescending(t => t.UsageCount)
            .ThenBy(t => t.Name)
            .ToList();
    }

    public async Task<List<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        return _templates.Values
            .Select(t => t.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToList();
    }

    public async Task<List<string>> GetTagsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);
        return _templates.Values
            .SelectMany(t => t.Tags)
            .Distinct()
            .OrderBy(t => t)
            .ToList();
    }

    public async Task<List<WorkflowTemplate>> SearchTemplatesAsync(string keyword, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);

        var lowerKeyword = keyword.ToLowerInvariant();
        return _templates.Values
            .Where(t =>
                t.Name.Contains(lowerKeyword, StringComparison.OrdinalIgnoreCase) ||
                t.Description.Contains(lowerKeyword, StringComparison.OrdinalIgnoreCase) ||
                t.Category.Contains(lowerKeyword, StringComparison.OrdinalIgnoreCase) ||
                t.Tags.Any(tag => tag.Contains(lowerKeyword, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(t => t.Category)
            .ThenBy(t => t.Name)
            .ToList();
    }

    public async Task IncrementUsageCountAsync(string templateId, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken);

        if (_templates.TryGetValue(templateId, out var template))
        {
            template.UsageCount++;
            _logger.LogDebug("Incremented usage count for template {Id} to {Count}", templateId, template.UsageCount);
        }
    }
}
