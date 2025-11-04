// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.ComponentModel;
using Honua.Cli.AI.Services.Migration;
using Microsoft.SemanticKernel;

namespace Honua.Cli.AI.Services.Plugins;

/// <summary>
/// Semantic Kernel plugin for Esri/ArcGIS service migration assistance.
/// Provides AI with capabilities to analyze, plan, and execute migrations from ArcGIS to Honua.
/// </summary>
public sealed class MigrationPlugin
{
    private readonly ArcGISServiceAnalyzer _serviceAnalyzer;
    private readonly MigrationPlanner _planner;
    private readonly MigrationValidator _validator;
    private readonly MigrationScriptGenerator _scriptGenerator;
    private readonly MigrationTroubleshooter _troubleshooter;

    public MigrationPlugin()
    {
        _serviceAnalyzer = new ArcGISServiceAnalyzer();
        _planner = new MigrationPlanner();
        _validator = new MigrationValidator();
        _scriptGenerator = new MigrationScriptGenerator();
        _troubleshooter = new MigrationTroubleshooter();
    }

    /// <summary>
    /// Analyzes an ArcGIS REST service and reports structure, layers, and capabilities.
    /// </summary>
    [KernelFunction, Description("Analyzes an ArcGIS REST service and reports structure, layers, and capabilities")]
    public string AnalyzeArcGISService(
        [Description("ArcGIS REST service URL (e.g., https://server/arcgis/rest/services/MyService/MapServer)")] string serviceUrl)
    {
        return _serviceAnalyzer.AnalyzeService(serviceUrl);
    }

    /// <summary>
    /// Creates a detailed migration plan from ArcGIS to Honua.
    /// </summary>
    [KernelFunction, Description("Creates a detailed migration plan from ArcGIS to Honua")]
    public string PlanMigration(
        [Description("Service metadata as JSON (layers, fields, geometry types)")] string serviceMetadata,
        [Description("Target Honua configuration as JSON (database type, deployment mode)")] string targetConfig)
    {
        return _planner.CreateMigrationPlan(serviceMetadata, targetConfig);
    }

    /// <summary>
    /// Validates migration readiness and identifies compatibility issues.
    /// </summary>
    [KernelFunction, Description("Validates migration readiness and identifies compatibility issues")]
    public string ValidateMigrationReadiness(
        [Description("Source ArcGIS service info as JSON")] string sourceInfo,
        [Description("Target Honua environment info as JSON")] string targetInfo)
    {
        return _validator.ValidateReadiness(sourceInfo, targetInfo);
    }

    /// <summary>
    /// Generates complete migration script with GDAL commands.
    /// </summary>
    [KernelFunction, Description("Generates complete migration script with GDAL commands")]
    public string GenerateMigrationScript(
        [Description("ArcGIS service URL")] string serviceUrl,
        [Description("Migration options as JSON (target database, layer mapping, transformations)")] string options)
    {
        return _scriptGenerator.GenerateScript(serviceUrl, options);
    }

    /// <summary>
    /// Provides troubleshooting guidance for migration-specific errors.
    /// </summary>
    [KernelFunction, Description("Provides troubleshooting guidance for migration-specific errors")]
    public string TroubleshootMigrationIssue(
        [Description("Error type (connection, data, geometry, performance)")] string errorType,
        [Description("Error context and messages")] string context)
    {
        return _troubleshooter.TroubleshootIssue(errorType, context);
    }
}
