// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.OpenApi.Filters;

/// <summary>
/// Document filter that enhances the OpenAPI document with contact and support information.
/// This includes team contact details, support channels, terms of service, and external documentation links.
/// </summary>
public sealed class ContactInfoDocumentFilter : IDocumentFilter
{
    private readonly ContactInfoOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactInfoDocumentFilter"/> class.
    /// </summary>
    /// <param name="options">Configuration options for contact information.</param>
    public ContactInfoDocumentFilter(ContactInfoOptions? options = null)
    {
        _options = options ?? new ContactInfoOptions();
    }

    /// <summary>
    /// Applies contact information enhancements to the OpenAPI document.
    /// </summary>
    /// <param name="swaggerDoc">The OpenAPI document being processed.</param>
    /// <param name="context">Context containing schema and document information.</param>
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        if (swaggerDoc.Info == null)
        {
            swaggerDoc.Info = new OpenApiInfo();
        }

        // Update contact information
        if (_options.ContactName != null || _options.ContactEmail != null || _options.ContactUrl != null)
        {
            swaggerDoc.Info.Contact = new OpenApiContact
            {
                Name = _options.ContactName ?? swaggerDoc.Info.Contact?.Name,
                Email = _options.ContactEmail ?? swaggerDoc.Info.Contact?.Email,
                Url = _options.ContactUrl ?? swaggerDoc.Info.Contact?.Url
            };
        }

        // Add license information if provided
        if (_options.LicenseName != null || _options.LicenseUrl != null)
        {
            swaggerDoc.Info.License = new OpenApiLicense
            {
                Name = _options.LicenseName ?? swaggerDoc.Info.License?.Name,
                Url = _options.LicenseUrl ?? swaggerDoc.Info.License?.Url
            };
        }

        // Add terms of service if provided
        if (_options.TermsOfServiceUrl != null)
        {
            swaggerDoc.Info.TermsOfService = _options.TermsOfServiceUrl;
        }

        // Add external documentation if provided
        if (_options.ExternalDocsDescription != null || _options.ExternalDocsUrl != null)
        {
            swaggerDoc.ExternalDocs = new OpenApiExternalDocs
            {
                Description = _options.ExternalDocsDescription ?? "External Documentation",
                Url = _options.ExternalDocsUrl!
            };
        }

        // Add support information to description
        var supportInfo = new List<string>();

        if (_options.SupportEmail != null)
        {
            supportInfo.Add($"- Support Email: [{_options.SupportEmail}](mailto:{_options.SupportEmail})");
        }

        if (_options.SupportUrl != null)
        {
            supportInfo.Add($"- Support Portal: [{_options.SupportUrl}]({_options.SupportUrl})");
        }

        if (_options.DocumentationUrl != null)
        {
            supportInfo.Add($"- Documentation: [{_options.DocumentationUrl}]({_options.DocumentationUrl})");
        }

        if (_options.IssueTrackerUrl != null)
        {
            supportInfo.Add($"- Issue Tracker: [{_options.IssueTrackerUrl}]({_options.IssueTrackerUrl})");
        }

        if (supportInfo.Any())
        {
            var supportBlock = $"\n\n**Support & Resources:**\n{string.Join("\n", supportInfo)}";
            swaggerDoc.Info.Description = swaggerDoc.Info.Description.IsNullOrEmpty()
                ? supportBlock.TrimStart()
                : $"{swaggerDoc.Info.Description}{supportBlock}";
        }

        // Add custom extensions for contact metadata
        if (_options.SupportEmail != null)
        {
            swaggerDoc.Extensions["x-support-email"] = new Microsoft.OpenApi.Any.OpenApiString(_options.SupportEmail);
        }

        if (_options.SupportUrl != null)
        {
            swaggerDoc.Extensions["x-support-url"] = new Microsoft.OpenApi.Any.OpenApiString(_options.SupportUrl.ToString());
        }

        if (_options.ApiStatus != null)
        {
            swaggerDoc.Extensions["x-api-status"] = new Microsoft.OpenApi.Any.OpenApiString(_options.ApiStatus);
        }
    }
}

/// <summary>
/// Configuration options for contact information in OpenAPI documentation.
/// </summary>
public sealed class ContactInfoOptions
{
    /// <summary>
    /// Gets or sets the contact name for the API.
    /// </summary>
    public string? ContactName { get; set; }

    /// <summary>
    /// Gets or sets the contact email for the API.
    /// </summary>
    public string? ContactEmail { get; set; }

    /// <summary>
    /// Gets or sets the contact URL for the API.
    /// </summary>
    public Uri? ContactUrl { get; set; }

    /// <summary>
    /// Gets or sets the support email address.
    /// </summary>
    public string? SupportEmail { get; set; }

    /// <summary>
    /// Gets or sets the support portal URL.
    /// </summary>
    public Uri? SupportUrl { get; set; }

    /// <summary>
    /// Gets or sets the documentation URL.
    /// </summary>
    public Uri? DocumentationUrl { get; set; }

    /// <summary>
    /// Gets or sets the issue tracker URL.
    /// </summary>
    public Uri? IssueTrackerUrl { get; set; }

    /// <summary>
    /// Gets or sets the license name.
    /// </summary>
    public string? LicenseName { get; set; }

    /// <summary>
    /// Gets or sets the license URL.
    /// </summary>
    public Uri? LicenseUrl { get; set; }

    /// <summary>
    /// Gets or sets the terms of service URL.
    /// </summary>
    public Uri? TermsOfServiceUrl { get; set; }

    /// <summary>
    /// Gets or sets the external documentation description.
    /// </summary>
    public string? ExternalDocsDescription { get; set; }

    /// <summary>
    /// Gets or sets the external documentation URL.
    /// </summary>
    public Uri? ExternalDocsUrl { get; set; }

    /// <summary>
    /// Gets or sets the API status (e.g., "stable", "beta", "deprecated").
    /// </summary>
    public string? ApiStatus { get; set; }
}
