// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Filtering;

namespace Honua.Server.Host.Filters;

/// <summary>
/// Action filter that processes OData-style filter query parameters for REST API endpoints.
/// Works in conjunction with <see cref="FilterQueryAttribute"/> to provide validated filtering.
/// </summary>
/// <remarks>
/// <para>
/// This filter runs before controller actions to:
/// </para>
/// <list type="bullet">
/// <item><description>Read the <c>filter</c> query parameter from the request</description></item>
/// <item><description>Parse OData-style filter expressions into FilterExpression trees</description></item>
/// <item><description>Validate filter syntax and property names against allowed properties</description></item>
/// <item><description>Store the parsed filter in HttpContext.Items for use by the controller</description></item>
/// <item><description>Return 400 Bad Request for invalid filters with detailed error messages</description></item>
/// </list>
/// <para>
/// <b>How It Works:</b>
/// </para>
/// <list type="number">
/// <item><description>Filter detects <see cref="FilterQueryAttribute"/> on the action method</description></item>
/// <item><description>Reads and validates the <c>filter</c> query parameter</description></item>
/// <item><description>Parses the filter string using <see cref="FilterExpressionParser"/></description></item>
/// <item><description>Validates that all referenced properties are in the allowed list</description></item>
/// <item><description>Stores the parsed FilterExpression in HttpContext.Items["ParsedFilter"]</description></item>
/// <item><description>Controller applies the filter using FilterQueryableExtensions.ApplyFilter()</description></item>
/// </list>
/// <para>
/// <b>Example Usage:</b>
/// </para>
/// <code>
/// // 1. Register the filter globally in Program.cs
/// builder.Services.AddControllers(options =>
/// {
///     options.Filters.Add&lt;FilterQueryActionFilter&gt;();
/// });
///
/// // 2. Decorate controller actions with FilterQueryAttribute
/// [HttpGet]
/// [FilterQuery(EntityType = typeof(Share), AllowedProperties = new[] { "createdAt", "status", "isActive" })]
/// public async Task&lt;ActionResult&lt;PagedResponse&lt;Share&gt;&gt;&gt; GetShares([FromQuery] string? filter)
/// {
///     var query = dbContext.Shares.AsQueryable();
///
///     // 3. Apply the parsed filter from HttpContext.Items
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
/// <b>Client Request Examples:</b>
/// </para>
/// <code>
/// // Simple equality
/// GET /api/shares?filter=status eq 'active'
///
/// // Date comparison
/// GET /api/shares?filter=createdAt gt 2025-01-01
///
/// // Combined conditions
/// GET /api/shares?filter=createdAt gt 2025-01-01 and status eq 'active'
///
/// // String function
/// GET /api/shares?filter=name contains 'project'
///
/// // Complex expression
/// GET /api/shares?filter=(status eq 'active' or status eq 'pending') and priority gt 5
/// </code>
/// <para>
/// <b>Error Responses:</b>
/// </para>
/// <code>
/// // Invalid syntax
/// GET /api/shares?filter=status eq
/// → 400 Bad Request: "Expected value after operator 'eq'"
///
/// // Unauthorized property
/// GET /api/shares?filter=password eq 'secret'
/// → 400 Bad Request: "Property 'password' is not allowed in filter expressions"
///
/// // Property doesn't exist
/// GET /api/shares?filter=invalidProp eq 'value'
/// → 400 Bad Request: "Property 'invalidProp' not found on type 'Share'"
/// </code>
/// <para>
/// <b>Performance Considerations:</b>
/// </para>
/// <list type="bullet">
/// <item><description>Filter parsing is cached per unique filter string (future enhancement)</description></item>
/// <item><description>Property validation happens once per request</description></item>
/// <item><description>LINQ expression compilation is handled by Entity Framework Core</description></item>
/// <item><description>Monitor slow queries and add indexes on frequently filtered properties</description></item>
/// </list>
/// </remarks>
public sealed class FilterQueryActionFilter : IAsyncActionFilter
{
    /// <summary>
    /// HttpContext.Items key for storing the parsed FilterExpression.
    /// </summary>
    public const string ParsedFilterKey = "ParsedFilter";

    private readonly ILogger<FilterQueryActionFilter> logger;
    private readonly FilterExpressionParser parser;

    /// <summary>
    /// Initializes a new instance of the <see cref="FilterQueryActionFilter"/> class.
    /// </summary>
    /// <param name="logger">Logger for recording filter parsing and validation events.</param>
    /// <exception cref="ArgumentNullException">Thrown when logger is null.</exception>
    public FilterQueryActionFilter(ILogger<FilterQueryActionFilter> logger)
    {
        this.logger = Guard.NotNull(logger);
        this.parser = new FilterExpressionParser();
    }

    /// <summary>
    /// Executes the filter to process the filter query parameter before the controller action.
    /// </summary>
    /// <param name="context">The action executing context containing request details.</param>
    /// <param name="next">The delegate to invoke the next action filter or the action itself.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        Guard.NotNull(context);
        Guard.NotNull(next);

        var requestId = context.HttpContext.TraceIdentifier;

        // Check if the action has the FilterQueryAttribute
        var filterAttribute = context.ActionDescriptor.EndpointMetadata
            .OfType<FilterQueryAttribute>()
            .FirstOrDefault();

        if (filterAttribute == null)
        {
            // No filter attribute, skip processing
            await next().ConfigureAwait(false);
            return;
        }

        // Validate the attribute configuration
        try
        {
            filterAttribute.Validate();
        }
        catch (InvalidOperationException ex)
        {
            this.logger.LogError(
                ex,
                "FilterQueryAttribute configuration is invalid on {Controller}.{Action} [RequestId: {RequestId}]",
                context.RouteData.Values["controller"],
                context.RouteData.Values["action"],
                requestId);

            context.Result = CreateErrorResult(
                "Filter configuration error",
                ex.Message,
                requestId);
            return;
        }

        // Read the filter query parameter
        var filterString = context.HttpContext.Request.Query["filter"].ToString();

        // If no filter provided, skip processing
        if (string.IsNullOrWhiteSpace(filterString))
        {
            await next().ConfigureAwait(false);
            return;
        }

        // Parse the filter expression
        FilterExpression parsedFilter;
        try
        {
            parsedFilter = this.parser.Parse(filterString);

            this.logger.LogDebug(
                "Parsed filter expression: {Filter} in {Controller}.{Action} [RequestId: {RequestId}]",
                filterString,
                context.RouteData.Values["controller"],
                context.RouteData.Values["action"],
                requestId);
        }
        catch (FilterParseException ex)
        {
            this.logger.LogWarning(
                "Failed to parse filter expression '{Filter}': {Error} [RequestId: {RequestId}]",
                filterString,
                ex.Message,
                requestId);

            context.Result = CreateErrorResult(
                "Invalid filter expression",
                ex.Message,
                requestId);
            return;
        }

        // Validate that all properties used in the filter are allowed
        try
        {
            ValidateFilterProperties(parsedFilter, filterAttribute);
        }
        catch (UnauthorizedFilterPropertyException ex)
        {
            this.logger.LogWarning(
                "Unauthorized property in filter expression '{Filter}': {Error} [RequestId: {RequestId}]",
                filterString,
                ex.Message,
                requestId);

            context.Result = CreateErrorResult(
                "Invalid filter property",
                ex.Message,
                requestId);
            return;
        }

        // Store the parsed filter in HttpContext.Items for the controller to use
        context.HttpContext.Items[ParsedFilterKey] = parsedFilter;

        this.logger.LogInformation(
            "Filter applied: {Filter} in {Controller}.{Action} [RequestId: {RequestId}]",
            filterString,
            context.RouteData.Values["controller"],
            context.RouteData.Values["action"],
            requestId);

        // Proceed to the controller action
        await next().ConfigureAwait(false);
    }

    /// <summary>
    /// Validates that all properties referenced in the filter are in the allowed properties list.
    /// </summary>
    private static void ValidateFilterProperties(
        FilterExpression filter,
        FilterQueryAttribute attribute)
    {
        var allowedProperties = attribute.AllowedProperties!
            .Select(p => p.ToLowerInvariant())
            .ToHashSet();

        ValidateFilterPropertiesRecursive(filter, allowedProperties);
    }

    /// <summary>
    /// Recursively validates properties in a filter expression tree.
    /// </summary>
    private static void ValidateFilterPropertiesRecursive(
        FilterExpression filter,
        HashSet<string> allowedProperties)
    {
        switch (filter)
        {
            case ComparisonExpression comparison:
                ValidateProperty(comparison.Property, allowedProperties);
                break;

            case StringFunctionExpression stringFunc:
                ValidateProperty(stringFunc.Property, allowedProperties);
                break;

            case LogicalExpression logical:
                ValidateFilterPropertiesRecursive(logical.Left, allowedProperties);
                ValidateFilterPropertiesRecursive(logical.Right, allowedProperties);
                break;

            case NotExpression not:
                ValidateFilterPropertiesRecursive(not.Expression, allowedProperties);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported filter expression type: {filter.GetType().Name}");
        }
    }

    /// <summary>
    /// Validates that a single property is in the allowed properties list.
    /// </summary>
    private static void ValidateProperty(string property, HashSet<string> allowedProperties)
    {
        var normalizedProperty = property.ToLowerInvariant();

        if (!allowedProperties.Contains(normalizedProperty))
        {
            throw new UnauthorizedFilterPropertyException(
                $"Property '{property}' is not allowed in filter expressions. " +
                $"Allowed properties: {string.Join(", ", allowedProperties)}");
        }
    }

    /// <summary>
    /// Creates a standardized error result for filter validation failures.
    /// </summary>
    private static BadRequestObjectResult CreateErrorResult(
        string title,
        string detail,
        string requestId)
    {
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = title,
            Detail = detail,
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Instance = requestId
        };

        problemDetails.Extensions["requestId"] = requestId;
        problemDetails.Extensions["timestamp"] = DateTimeOffset.UtcNow;

        return new BadRequestObjectResult(problemDetails);
    }
}

/// <summary>
/// Exception thrown when a filter expression references a property that is not in the allowed list.
/// </summary>
/// <remarks>
/// This exception is used internally by FilterQueryActionFilter to detect and report
/// unauthorized property access attempts in filter expressions. It should be caught and
/// converted to a 400 Bad Request response with a helpful error message.
/// <para>
/// <b>Security Note:</b> This exception prevents:
/// </para>
/// <list type="bullet">
/// <item><description>Access to sensitive properties (e.g., passwords, internal IDs)</description></item>
/// <item><description>SQL injection through property name manipulation</description></item>
/// <item><description>Information disclosure about entity schema</description></item>
/// </list>
/// </remarks>
public sealed class UnauthorizedFilterPropertyException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnauthorizedFilterPropertyException"/> class.
    /// </summary>
    /// <param name="message">
    /// The error message describing which property was unauthorized and what properties are allowed.
    /// </param>
    public UnauthorizedFilterPropertyException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnauthorizedFilterPropertyException"/> class.
    /// </summary>
    /// <param name="message">
    /// The error message describing which property was unauthorized and what properties are allowed.
    /// </param>
    /// <param name="innerException">The exception that caused this exception.</param>
    public UnauthorizedFilterPropertyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
