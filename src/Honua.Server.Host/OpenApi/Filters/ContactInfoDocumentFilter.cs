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
    private readonly ContactInfoOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactInfoDocumentFilter"/> class.
    /// </summary>
    /// <param name="options">Configuration options for contact information.</param>
    public ContactInfoDocumentFilter(ContactInfoOptions? options = null)
    {
        this.options = options ?? new ContactInfoOptions();
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
        if (this.options.ContactName != null || this.options.ContactEmail != null || this.options.ContactUrl != null)
        {
            swaggerDoc.Info.Contact = new OpenApiContact
            {
                Name = this.options.ContactName ?? swaggerDoc.Info.Contact?.Name,
                Email = this.options.ContactEmail ?? swaggerDoc.Info.Contact?.Email,
                Url = this.options.ContactUrl ?? swaggerDoc.Info.Contact?.Url
            };
        }

        // Add license information if provided
        if (this.options.LicenseName != null || this.options.LicenseUrl != null)
        {
            swaggerDoc.Info.License = new OpenApiLicense
            {
                Name = this.options.LicenseName ?? swaggerDoc.Info.License?.Name,
                Url = this.options.LicenseUrl ?? swaggerDoc.Info.License?.Url
            };
        }

        // Add terms of service if provided
        if (this.options.TermsOfServiceUrl != null)
        {
            swaggerDoc.Info.TermsOfService = this.options.TermsOfServiceUrl;
        }

        // Add external documentation if provided
        if (this.options.ExternalDocsDescription != null || this.options.ExternalDocsUrl != null)
        {
            swaggerDoc.ExternalDocs = new OpenApiExternalDocs
            {
                Description = this.options.ExternalDocsDescription ?? "External Documentation",
                Url = this.options.ExternalDocsUrl!
            };
        }

        // Add support information to description
        var supportInfo = new List<string>();

        if (this.options.SupportEmail != null)
        {
            supportInfo.Add($"- Support Email: [{this.options.SupportEmail}](mailto:{this.options.SupportEmail})");
        }

        if (this.options.SupportUrl != null)
        {
            supportInfo.Add($"- Support Portal: [{this.options.SupportUrl}]({this.options.SupportUrl})");
        }

        if (this.options.DocumentationUrl != null)
        {
            supportInfo.Add($"- Documentation: [{this.options.DocumentationUrl}]({this.options.DocumentationUrl})");
        }

        if (this.options.IssueTrackerUrl != null)
        {
            supportInfo.Add($"- Issue Tracker: [{this.options.IssueTrackerUrl}]({this.options.IssueTrackerUrl})");
        }

        if (supportInfo.Any())
        {
            var supportBlock = $"\n\n**Support & Resources:**\n{string.Join("\n", supportInfo)}";
            swaggerDoc.Info.Description = swaggerDoc.Info.Description.IsNullOrEmpty()
                ? supportBlock.TrimStart()
                : $"{swaggerDoc.Info.Description}{supportBlock}";
        }

        // Add custom extensions for contact metadata
        if (this.options.SupportEmail != null)
        {
            swaggerDoc.Extensions["x-support-email"] = new Microsoft.OpenApi.Any.OpenApiString(this.options.SupportEmail);
        }

        if (this.options.SupportUrl != null)
        {
            swaggerDoc.Extensions["x-support-url"] = new Microsoft.OpenApi.Any.OpenApiString(this.options.SupportUrl.ToString());
        }

        if (this.options.ApiStatus != null)
        {
            swaggerDoc.Extensions["x-api-status"] = new Microsoft.OpenApi.Any.OpenApiString(this.options.ApiStatus);
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
