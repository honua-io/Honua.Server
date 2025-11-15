// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Host.Filters;

/// <summary>
/// Attribute for enabling OData-style filtering on controller action methods.
/// Specifies which entity type is being filtered and which properties are allowed.
/// </summary>
/// <remarks>
/// <para>
/// This attribute works in conjunction with <see cref="FilterQueryActionFilter"/> to provide
/// secure, validated filtering for REST API endpoints. It serves as both documentation and
/// runtime configuration for the filter validation process.
/// </para>
/// <para>
/// <b>Security Features:</b>
/// </para>
/// <list type="bullet">
/// <item><description>Whitelists allowed properties to prevent unauthorized data access</description></item>
/// <item><description>Prevents SQL injection by validating property names</description></item>
/// <item><description>Documents the filtering capabilities for API consumers</description></item>
/// <item><description>Enables type-safe filter validation at runtime</description></item>
/// </list>
/// <para>
/// <b>Example Usage:</b>
/// </para>
/// <code>
/// [HttpGet]
/// [FilterQuery(EntityType = typeof(Share), AllowedProperties = new[] { "createdAt", "permission", "isActive" })]
/// public async Task&lt;ActionResult&lt;PagedResponse&lt;Share&gt;&gt;&gt; GetShares(
///     [FromQuery] string? filter)
/// {
///     var query = dbContext.Shares.AsQueryable();
///
///     // Filter applied by action filter, accessible via HttpContext.Items
///     if (HttpContext.Items.TryGetValue("ParsedFilter", out var parsedFilter))
///     {
///         query = query.ApplyFilter((FilterExpression)parsedFilter);
///     }
///
///     var shares = await query.ToListAsync();
///     return Ok(new PagedResponse&lt;Share&gt; { Items = shares });
/// }
/// </code>
/// <para>
/// <b>Supported Filter Examples:</b>
/// </para>
/// <code>
/// // Date comparison
/// GET /api/shares?filter=createdAt gt 2025-01-01
///
/// // String equality
/// GET /api/shares?filter=permission eq 'read'
///
/// // Boolean comparison
/// GET /api/shares?filter=isActive eq true
///
/// // Combined conditions
/// GET /api/shares?filter=createdAt gt 2025-01-01 and permission eq 'write'
///
/// // String functions
/// GET /api/shares?filter=name contains 'project'
/// </code>
/// <para>
/// <b>Best Practices:</b>
/// </para>
/// <list type="bullet">
/// <item><description>Only allow filtering on indexed columns for performance</description></item>
/// <item><description>Use meaningful property names that match client expectations</description></item>
/// <item><description>Consider using DTOs with camelCase properties for API contracts</description></item>
/// <item><description>Document allowed properties in OpenAPI/Swagger documentation</description></item>
/// <item><description>Limit the number of allowed properties to reduce complexity</description></item>
/// </list>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class FilterQueryAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the entity type being filtered.
    /// </summary>
    /// <remarks>
    /// This type is used for:
    /// <list type="bullet">
    /// <item><description>Validating that allowed properties exist on the entity</description></item>
    /// <item><description>Type checking during filter application</description></item>
    /// <item><description>Generating accurate error messages</description></item>
    /// <item><description>OpenAPI/Swagger documentation generation</description></item>
    /// </list>
    /// <para>
    /// <b>Example:</b>
    /// </para>
    /// <code>
    /// [FilterQuery(EntityType = typeof(User), AllowedProperties = new[] { "email", "createdAt", "isActive" })]
    /// </code>
    /// </remarks>
    public Type? EntityType { get; set; }

    /// <summary>
    /// Gets or sets the list of property names that can be used in filter expressions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Property names are matched case-insensitively against the entity type's properties.
    /// This whitelist approach prevents:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Unauthorized access to sensitive properties (e.g., passwords, internal IDs)</description></item>
    /// <item><description>SQL injection through property name manipulation</description></item>
    /// <item><description>Performance issues from filtering on non-indexed columns</description></item>
    /// <item><description>Client confusion about which properties support filtering</description></item>
    /// </list>
    /// <para>
    /// <b>Property Name Conventions:</b>
    /// </para>
    /// <list type="bullet">
    /// <item><description>Use camelCase for consistency with JSON API conventions</description></item>
    /// <item><description>Match the DTO property names exposed to clients</description></item>
    /// <item><description>Support nested properties with dot notation (e.g., "user.email")</description></item>
    /// </list>
    /// <para>
    /// <b>Examples:</b>
    /// </para>
    /// <code>
    /// // Simple properties
    /// AllowedProperties = new[] { "id", "name", "createdAt", "isActive" }
    ///
    /// // Nested properties (if supported by your entity model)
    /// AllowedProperties = new[] { "user.email", "user.department.name" }
    ///
    /// // Date and time properties
    /// AllowedProperties = new[] { "createdAt", "updatedAt", "lastAccessedAt" }
    ///
    /// // Enum properties
    /// AllowedProperties = new[] { "status", "priority", "permission" }
    /// </code>
    /// <para>
    /// <b>Security Note:</b> Never allow filtering on:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Password fields or other credentials</description></item>
    /// <item><description>Internal system identifiers</description></item>
    /// <item><description>Audit fields (unless specifically required)</description></item>
    /// <item><description>Computed properties (use database columns instead)</description></item>
    /// </list>
    /// </remarks>
    public string[]? AllowedProperties { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of conditions allowed in a single filter expression.
    /// </summary>
    /// <remarks>
    /// This limit prevents:
    /// <list type="bullet">
    /// <item><description>DoS attacks through complex filter expressions</description></item>
    /// <item><description>Database performance degradation from complex WHERE clauses</description></item>
    /// <item><description>Timeout errors on long-running queries</description></item>
    /// </list>
    /// <para>
    /// Default: 10 conditions
    /// </para>
    /// <para>
    /// Recommended values:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Simple queries: 5-10 conditions</description></item>
    /// <item><description>Complex queries: 10-20 conditions</description></item>
    /// <item><description>Advanced users: 20-50 conditions (with rate limiting)</description></item>
    /// </list>
    /// </remarks>
    public int MaxConditions { get; set; } = 10;

    /// <summary>
    /// Gets or sets whether to allow nested properties in filter expressions.
    /// </summary>
    /// <remarks>
    /// When enabled, supports dot notation like "user.email" or "department.name".
    /// Nested properties can impact query performance due to JOIN operations.
    /// <para>
    /// Default: false
    /// </para>
    /// </remarks>
    public bool AllowNestedProperties { get; set; } = false;

    /// <summary>
    /// Validates the attribute configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the attribute is misconfigured.
    /// </exception>
    /// <remarks>
    /// Called by the FilterQueryActionFilter during action execution to ensure
    /// the attribute is properly configured before processing filter queries.
    /// </remarks>
    public void Validate()
    {
        if (EntityType == null)
        {
            throw new InvalidOperationException(
                $"{nameof(FilterQueryAttribute)} requires {nameof(EntityType)} to be set");
        }

        if (AllowedProperties == null || AllowedProperties.Length == 0)
        {
            throw new InvalidOperationException(
                $"{nameof(FilterQueryAttribute)} requires at least one property in {nameof(AllowedProperties)}");
        }

        // Validate that all allowed properties exist on the entity type
        foreach (var propertyName in AllowedProperties)
        {
            Guard.NotNullOrWhiteSpace(propertyName);

            // Handle nested properties
            var parts = propertyName.Split('.');
            var currentType = EntityType;

            foreach (var part in parts)
            {
                var property = currentType.GetProperty(
                    part,
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.IgnoreCase);

                if (property == null)
                {
                    throw new InvalidOperationException(
                        $"Property '{part}' in path '{propertyName}' not found on type '{currentType.Name}'. " +
                        $"Available properties: {string.Join(", ", currentType.GetProperties().Select(p => p.Name))}");
                }

                currentType = property.PropertyType;
            }

            // Validate nested property configuration
            if (parts.Length > 1 && !AllowNestedProperties)
            {
                throw new InvalidOperationException(
                    $"Nested property '{propertyName}' is not allowed when {nameof(AllowNestedProperties)} is false");
            }
        }
    }
}
