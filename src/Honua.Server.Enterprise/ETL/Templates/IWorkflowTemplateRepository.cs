// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Enterprise.ETL.Templates;

/// <summary>
/// Repository for workflow templates
/// </summary>
public interface IWorkflowTemplateRepository
{
    /// <summary>
    /// Get all available templates
    /// </summary>
    Task<List<WorkflowTemplate>> GetAllTemplatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific template by ID
    /// </summary>
    Task<WorkflowTemplate?> GetTemplateByIdAsync(string templateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get templates by category
    /// </summary>
    Task<List<WorkflowTemplate>> GetTemplatesByCategoryAsync(string category, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get templates by tag
    /// </summary>
    Task<List<WorkflowTemplate>> GetTemplatesByTagAsync(string tag, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get featured templates
    /// </summary>
    Task<List<WorkflowTemplate>> GetFeaturedTemplatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all distinct categories
    /// </summary>
    Task<List<string>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all distinct tags
    /// </summary>
    Task<List<string>> GetTagsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Search templates by keyword
    /// </summary>
    Task<List<WorkflowTemplate>> SearchTemplatesAsync(string keyword, CancellationToken cancellationToken = default);

    /// <summary>
    /// Increment usage count for a template
    /// </summary>
    Task IncrementUsageCountAsync(string templateId, CancellationToken cancellationToken = default);
}
