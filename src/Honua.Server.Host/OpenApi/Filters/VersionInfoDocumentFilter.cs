// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.OpenApi.Filters;

/// <summary>
/// Document filter that enhances the OpenAPI document with comprehensive version information.
/// This includes assembly version, build date, environment details, and custom version metadata.
/// </summary>
public sealed class VersionInfoDocumentFilter : IDocumentFilter
{
    private readonly string environmentName;
    private readonly string? buildVersion;
    private readonly string? buildDate;

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionInfoDocumentFilter"/> class.
    /// </summary>
    public VersionInfoDocumentFilter()
    {
        this.environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        this.buildVersion = Environment.GetEnvironmentVariable("BUILD_VERSION");
        this.buildDate = Environment.GetEnvironmentVariable("BUILD_DATE");
    }

    /// <summary>
    /// Applies version information enhancements to the OpenAPI document.
    /// </summary>
    /// <param name="swaggerDoc">The OpenAPI document being processed.</param>
    /// <param name="context">Context containing schema and document information.</param>
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        if (swaggerDoc.Info == null)
        {
            swaggerDoc.Info = new OpenApiInfo();
        }

        // Get assembly information
        var assembly = Assembly.GetExecutingAssembly();
        var assemblyVersion = assembly.GetName().Version;
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var fileVersion = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;

        // Build comprehensive version string
        var versionParts = new List<string>();

        if (!this.buildVersion.IsNullOrEmpty())
        {
            versionParts.Add($"Build: {_buildVersion}");
        }
        else if (assemblyVersion != null)
        {
            versionParts.Add($"v{assemblyVersion}");
        }

        if (!informationalVersion.IsNullOrEmpty())
        {
            versionParts.Add($"Info: {informationalVersion}");
        }

        if (!fileVersion.IsNullOrEmpty())
        {
            versionParts.Add($"File: {fileVersion}");
        }

        // Update version in info
        if (versionParts.Any())
        {
            swaggerDoc.Info.Version = versionParts.First().Replace("Build: ", "").Replace("v", "");
        }

        // Add extended version information to description
        var versionInfo = new List<string>();

        if (versionParts.Any())
        {
            versionInfo.Add($"**Version Information:**");
            versionInfo.AddRange(versionParts.Select(v => $"- {v}"));
        }

        if (!this.buildDate.IsNullOrEmpty())
        {
            versionInfo.Add($"- Build Date: {_buildDate}");
        }

        versionInfo.Add($"- Environment: {_environmentName}");

        // Add .NET runtime information
        var runtimeVersion = Environment.Version;
        versionInfo.Add($"- Runtime: .NET {runtimeVersion}");

        // Append version info to description
        if (versionInfo.Any())
        {
            var versionBlock = string.Join("\n", versionInfo);
            swaggerDoc.Info.Description = swaggerDoc.Info.Description.IsNullOrEmpty()
                ? versionBlock
                : $"{swaggerDoc.Info.Description}\n\n{versionBlock}";
        }

        // Add custom extensions for version metadata
        swaggerDoc.Extensions["x-api-version"] = new Microsoft.OpenApi.Any.OpenApiString(swaggerDoc.Info.Version);
        swaggerDoc.Extensions["x-environment"] = new Microsoft.OpenApi.Any.OpenApiString(_environmentName);

        if (!this.buildDate.IsNullOrEmpty())
        {
            swaggerDoc.Extensions["x-build-date"] = new Microsoft.OpenApi.Any.OpenApiString(_buildDate);
        }

        if (assemblyVersion != null)
        {
            swaggerDoc.Extensions["x-assembly-version"] = new Microsoft.OpenApi.Any.OpenApiString(assemblyVersion.ToString());
        }
    }
}
