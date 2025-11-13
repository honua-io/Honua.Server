// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.OpenApi.Filters;

/// <summary>
/// Document filter that adds deprecation information and sunset dates to the OpenAPI document.
/// This helps API consumers understand the lifecycle of API versions and plan for migrations.
/// </summary>
public sealed class DeprecationInfoDocumentFilter : IDocumentFilter
{
    private readonly DeprecationInfoOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeprecationInfoDocumentFilter"/> class.
    /// </summary>
    /// <param name="options">Configuration options for deprecation information.</param>
    public DeprecationInfoDocumentFilter(DeprecationInfoOptions? options = null)
    {
        this.options = options ?? new DeprecationInfoOptions();
    }

    /// <summary>
    /// Applies deprecation information to the OpenAPI document.
    /// </summary>
    /// <param name="swaggerDoc">The OpenAPI document being processed.</param>
    /// <param name="context">Context containing schema and document information.</param>
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        if (swaggerDoc.Info == null)
        {
            swaggerDoc.Info = new OpenApiInfo();
        }

        // Add deprecation notice if this version is deprecated
        if (this.options.IsDeprecated)
        {
            var deprecationInfo = new List<string>
            {
                "⚠️ **DEPRECATION NOTICE**",
                $"This API version is deprecated and will be removed in a future release."
            };

            if (this.options.SunsetDate.HasValue)
            {
                deprecationInfo.Add($"**Sunset Date:** {this.options.SunsetDate.Value:yyyy-MM-dd}");
            }

            if (!this.options.ReplacementVersion.IsNullOrEmpty())
            {
                deprecationInfo.Add($"**Replacement Version:** {this.options.ReplacementVersion}");
            }

            if (!this.options.MigrationGuideUrl.IsNullOrEmpty())
            {
                deprecationInfo.Add($"**Migration Guide:** [{this.options.MigrationGuideUrl}]({this.options.MigrationGuideUrl})");
            }

            if (!this.options.DeprecationReason.IsNullOrEmpty())
            {
                deprecationInfo.Add($"**Reason:** {this.options.DeprecationReason}");
            }

            var deprecationBlock = $"\n\n{string.Join("\n\n", deprecationInfo)}";
            swaggerDoc.Info.Description = swaggerDoc.Info.Description.IsNullOrEmpty()
                ? deprecationBlock.TrimStart()
                : $"{swaggerDoc.Info.Description}{deprecationBlock}";

            // Add deprecation extensions
            swaggerDoc.Extensions["x-deprecated"] = new Microsoft.OpenApi.Any.OpenApiBoolean(true);

            if (this.options.SunsetDate.HasValue)
            {
                swaggerDoc.Extensions["x-sunset-date"] = new Microsoft.OpenApi.Any.OpenApiString(
                    this.options.SunsetDate.Value.ToString("yyyy-MM-dd"));
            }

            if (!this.options.ReplacementVersion.IsNullOrEmpty())
            {
                swaggerDoc.Extensions["x-replacement-version"] = new Microsoft.OpenApi.Any.OpenApiString(
                    this.options.ReplacementVersion);
            }
        }
        else if (!this.options.Stability.IsNullOrEmpty())
        {
            // Add stability information for non-deprecated versions
            swaggerDoc.Extensions["x-stability"] = new Microsoft.OpenApi.Any.OpenApiString(this.options.Stability);

            var stabilityInfo = $"\n\n**API Stability:** {this.options.Stability}";
            swaggerDoc.Info.Description = swaggerDoc.Info.Description.IsNullOrEmpty()
                ? stabilityInfo.TrimStart()
                : $"{swaggerDoc.Info.Description}{stabilityInfo}";
        }

        // Add changelog information if provided
        if (!this.options.ChangelogUrl.IsNullOrEmpty())
        {
            swaggerDoc.Extensions["x-changelog"] = new Microsoft.OpenApi.Any.OpenApiString(this.options.ChangelogUrl);

            var changelogInfo = $"\n\n**Changelog:** [{this.options.ChangelogUrl}]({this.options.ChangelogUrl})";
            swaggerDoc.Info.Description = swaggerDoc.Info.Description.IsNullOrEmpty()
                ? changelogInfo.TrimStart()
                : $"{swaggerDoc.Info.Description}{changelogInfo}";
        }
    }
}

/// <summary>
/// Configuration options for deprecation information in OpenAPI documentation.
/// </summary>
public sealed class DeprecationInfoOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether this API version is deprecated.
    /// </summary>
    public bool IsDeprecated { get; set; }

    /// <summary>
    /// Gets or sets the date when this API version will be sunset (removed).
    /// </summary>
    public DateTimeOffset? SunsetDate { get; set; }

    /// <summary>
    /// Gets or sets the replacement version that should be used instead.
    /// </summary>
    public string? ReplacementVersion { get; set; }

    /// <summary>
    /// Gets or sets the URL to the migration guide.
    /// </summary>
    public string? MigrationGuideUrl { get; set; }

    /// <summary>
    /// Gets or sets the reason for deprecation.
    /// </summary>
    public string? DeprecationReason { get; set; }

    /// <summary>
    /// Gets or sets the stability level (e.g., "stable", "beta", "alpha", "experimental").
    /// </summary>
    public string? Stability { get; set; }

    /// <summary>
    /// Gets or sets the URL to the changelog.
    /// </summary>
    public string? ChangelogUrl { get; set; }
}
