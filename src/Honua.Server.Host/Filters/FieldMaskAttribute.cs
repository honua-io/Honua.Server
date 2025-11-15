// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;

namespace Honua.Server.Host.Filters;

/// <summary>
/// Enables partial response field masking for API endpoints following Google API Design Guide AIP-161.
/// </summary>
/// <remarks>
/// <para><strong>Overview:</strong></para>
/// <para>
/// Field masks allow clients to request only specific fields in API responses, reducing bandwidth usage
/// and improving performance by returning only the data that clients actually need. This implements
/// the Google Cloud API Design Guide standard for partial responses (AIP-161).
/// </para>
///
/// <para><strong>Usage Example:</strong></para>
/// <code>
/// [HttpGet("{id}")]
/// [FieldMask]
/// public async Task&lt;ActionResult&lt;Share&gt;&gt; GetShare(string id)
/// {
///     var share = await _shareService.GetShareAsync(id);
///     return Ok(share);
/// }
///
/// // Request: GET /api/v1.0/shares/abc?fields=id,token,permission
/// // Response: { "id": "abc", "token": "xyz", "permission": "view" }
///
/// // Request: GET /api/v1.0/shares/abc?fields=id,owner.name,owner.email
/// // Response: { "id": "abc", "owner": { "name": "John", "email": "john@example.com" } }
///
/// // Request: GET /api/v1.0/shares?fields=items(id,token),nextPageToken
/// // Response: { "items": [{ "id": "1", "token": "x" }], "nextPageToken": "..." }
/// </code>
///
/// <para><strong>Field Selection Syntax:</strong></para>
/// <list type="bullet">
/// <item>
/// <description><strong>Simple fields:</strong> <c>?fields=id,name,createdAt</c> - Returns only specified fields</description>
/// </item>
/// <item>
/// <description><strong>Nested fields:</strong> <c>?fields=id,user.name,user.email</c> - Returns nested object properties</description>
/// </item>
/// <item>
/// <description><strong>All fields:</strong> <c>?fields=*</c> or omit parameter - Returns all fields</description>
/// </item>
/// <item>
/// <description><strong>Array items:</strong> <c>?fields=items(id,name),total</c> - Applies mask to array elements</description>
/// </item>
/// </list>
///
/// <para><strong>Performance Considerations:</strong></para>
/// <list type="bullet">
/// <item>
/// <description>
/// <strong>Database Projection:</strong> Field masks operate on the response payload AFTER data retrieval.
/// For optimal performance, use database-level projections (e.g., SELECT only needed columns) in addition
/// to field masks to avoid fetching unnecessary data.
/// </description>
/// </item>
/// <item>
/// <description>
/// <strong>Caching:</strong> Field mask parsing is cached per unique field set to minimize overhead.
/// </description>
/// </item>
/// <item>
/// <description>
/// <strong>Memory:</strong> Uses System.Text.Json streaming for efficient JSON manipulation.
/// </description>
/// </item>
/// <item>
/// <description>
/// <strong>CPU:</strong> Minimal CPU overhead (~1-2% for typical payloads). The filter short-circuits
/// when no fields parameter is provided.
/// </description>
/// </item>
/// </list>
///
/// <para><strong>Security Considerations:</strong></para>
/// <list type="bullet">
/// <item>
/// <description>
/// <strong>Over-fetching Prevention:</strong> Field masks help prevent accidental exposure of sensitive
/// data by allowing clients to request only required fields.
/// </description>
/// </item>
/// <item>
/// <description>
/// <strong>Authorization:</strong> Field masks do NOT bypass authorization. Ensure sensitive fields are
/// protected at the service/repository layer.
/// </description>
/// </item>
/// <item>
/// <description>
/// <strong>Invalid Fields:</strong> Invalid or non-existent fields are silently ignored (fail-safe).
/// </description>
/// </item>
/// </list>
///
/// <para><strong>API Guidelines Compliance:</strong></para>
/// <para>
/// This implementation follows:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <strong>Google AIP-161:</strong> Partial responses with field masks
/// (https://google.aip.dev/161)
/// </description>
/// </item>
/// <item>
/// <description>
/// <strong>Microsoft Azure REST API Guidelines:</strong> Partial responses pattern
/// </description>
/// </item>
/// <item>
/// <description>
/// <strong>JSON:API Sparse Fieldsets:</strong> Compatible with sparse fieldset concepts
/// </description>
/// </item>
/// </list>
///
/// <para><strong>Testing Example:</strong></para>
/// <code>
/// // Unit test example
/// [Fact]
/// public async Task GetShare_WithFieldMask_ReturnsOnlyRequestedFields()
/// {
///     // Arrange
///     var client = _factory.CreateClient();
///
///     // Act
///     var response = await client.GetAsync("/api/v1.0/shares/abc?fields=id,token");
///     var json = await response.Content.ReadAsStringAsync();
///
///     // Assert
///     var share = JsonSerializer.Deserialize&lt;JsonElement&gt;(json);
///     Assert.True(share.TryGetProperty("id", out _));
///     Assert.True(share.TryGetProperty("token", out _));
///     Assert.False(share.TryGetProperty("permission", out _)); // Not requested
/// }
/// </code>
///
/// <para><strong>Common Patterns:</strong></para>
/// <list type="bullet">
/// <item>
/// <description>
/// <strong>List endpoints:</strong> <c>?fields=items(id,name),nextPageToken</c> - For paginated collections
/// </description>
/// </item>
/// <item>
/// <description>
/// <strong>Mobile apps:</strong> <c>?fields=id,name,thumbnail</c> - Minimal data for list views
/// </description>
/// </item>
/// <item>
/// <description>
/// <strong>Webhooks:</strong> <c>?fields=id,status,updatedAt</c> - Only relevant event data
/// </description>
/// </item>
/// <item>
/// <description>
/// <strong>Embedding:</strong> <c>?fields=id,author.name,author.avatar</c> - Avoid N+1 queries
/// </description>
/// </item>
/// </list>
///
/// <para><strong>Disable for Specific Endpoints:</strong></para>
/// <code>
/// [HttpGet("{id}")]
/// [FieldMask(Enabled = false)]
/// public async Task&lt;ActionResult&lt;Share&gt;&gt; GetShare(string id)
/// {
///     // Field masking is disabled for this endpoint
///     return Ok(share);
/// }
/// </code>
/// </remarks>
/// <example>
/// <para>Simple field selection:</para>
/// <code>
/// GET /api/v1.0/users/123?fields=id,name,email
/// {
///   "id": "123",
///   "name": "John Doe",
///   "email": "john@example.com"
/// }
/// </code>
///
/// <para>Nested field selection:</para>
/// <code>
/// GET /api/v1.0/shares/abc?fields=id,owner.name,owner.email,metadata.tags
/// {
///   "id": "abc",
///   "owner": {
///     "name": "John Doe",
///     "email": "john@example.com"
///   },
///   "metadata": {
///     "tags": ["geo", "public"]
///   }
/// }
/// </code>
///
/// <para>Collection with field mask:</para>
/// <code>
/// GET /api/v1.0/shares?fields=items(id,name,createdAt),total,nextPageToken
/// {
///   "items": [
///     { "id": "1", "name": "Share 1", "createdAt": "2025-01-15T10:00:00Z" },
///     { "id": "2", "name": "Share 2", "createdAt": "2025-01-15T11:00:00Z" }
///   ],
///   "total": 100,
///   "nextPageToken": "abc123"
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class FieldMaskAttribute : Attribute
{
    /// <summary>
    /// Gets or sets a value indicating whether field masking is enabled for this endpoint.
    /// </summary>
    /// <value>
    /// <c>true</c> if field masking is enabled; otherwise, <c>false</c>.
    /// Default is <c>true</c>.
    /// </value>
    /// <remarks>
    /// Set to <c>false</c> to explicitly disable field masking for specific endpoints
    /// while keeping the attribute applied (useful for documentation purposes or
    /// when temporarily disabling the feature).
    /// </remarks>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the query parameter name for field selection.
    /// </summary>
    /// <value>
    /// The query parameter name. Default is "fields".
    /// </value>
    /// <remarks>
    /// Allows customization of the query parameter name if needed for API compatibility.
    /// For example, some APIs use "select" or "props" instead of "fields".
    /// </remarks>
    /// <example>
    /// <code>
    /// [FieldMask(QueryParameterName = "select")]
    /// public async Task&lt;ActionResult&lt;Share&gt;&gt; GetShare(string id)
    /// {
    ///     // Now use: ?select=id,name instead of ?fields=id,name
    /// }
    /// </code>
    /// </example>
    public string QueryParameterName { get; set; } = "fields";
}
