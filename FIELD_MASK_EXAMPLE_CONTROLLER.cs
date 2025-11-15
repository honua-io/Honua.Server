// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

// EXAMPLE CONTROLLER - Demonstrates Field Mask Usage
// This is a reference implementation showing how to use field masks in controllers.
// Place actual controllers in src/Honua.Server.Host/Controllers or appropriate module.

using Microsoft.AspNetCore.Mvc;
using Honua.Server.Host.Filters;
using Honua.Server.Host.Pagination;

namespace Honua.Server.Host.Examples;

/// <summary>
/// Example controller demonstrating field mask usage for partial responses.
/// </summary>
/// <remarks>
/// This controller shows various field mask scenarios:
/// - Simple field selection
/// - Nested field selection
/// - Collection field masking
/// - Custom query parameter names
/// - Combining with database projections
/// </remarks>
[ApiController]
[Route("api/v1.0/[controller]")]
public class ExampleSharesController : ControllerBase
{
    private readonly ILogger<ExampleSharesController> _logger;
    private readonly IShareService _shareService;

    public ExampleSharesController(
        ILogger<ExampleSharesController> logger,
        IShareService shareService)
    {
        _logger = logger;
        _shareService = shareService;
    }

    /// <summary>
    /// Gets a share by ID with optional field masking.
    /// </summary>
    /// <param name="id">The share ID.</param>
    /// <param name="fields">
    /// Optional comma-separated field list for partial responses.
    /// Available fields: id, name, token, permission, owner.name, owner.email,
    /// metadata.tags, metadata.description, createdAt, updatedAt
    /// </param>
    /// <returns>The requested share with optional field filtering.</returns>
    /// <response code="200">Returns the share (optionally filtered).</response>
    /// <response code="404">Share not found.</response>
    /// <example>
    /// Examples:
    /// - GET /api/v1.0/shares/abc123
    ///   Returns complete share object
    ///
    /// - GET /api/v1.0/shares/abc123?fields=id,name,token
    ///   Returns: { "id": "abc123", "name": "My Share", "token": "xyz789" }
    ///
    /// - GET /api/v1.0/shares/abc123?fields=id,owner.name,owner.email
    ///   Returns: { "id": "abc123", "owner": { "name": "John", "email": "john@example.com" } }
    ///
    /// - GET /api/v1.0/shares/abc123?fields=*
    ///   Returns complete share object (wildcard)
    /// </example>
    [HttpGet("{id}")]
    [FieldMask] // Enable field masking for this endpoint
    [ProducesResponseType(typeof(ShareDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShareDto>> GetShare(
        string id,
        [FromQuery] string? fields = null)
    {
        // Optional: Optimize database query based on requested fields
        // This is a performance optimization to avoid fetching unnecessary data
        var includeOwner = fields?.Contains("owner") ?? true;
        var includeMetadata = fields?.Contains("metadata") ?? true;

        var share = await _shareService.GetShareAsync(id, includeOwner, includeMetadata);

        if (share == null)
        {
            return NotFound();
        }

        // Return the share - field mask filter will automatically apply if fields parameter is present
        return Ok(share);
    }

    /// <summary>
    /// Gets a paginated list of shares with optional field masking.
    /// </summary>
    /// <param name="pageSize">Number of items per page (default: 20, max: 100).</param>
    /// <param name="pageToken">Optional page token for pagination.</param>
    /// <param name="fields">
    /// Optional comma-separated field list for partial responses.
    /// Use array notation for collection items: items(id,name),total,nextPageToken
    /// Available fields: items.id, items.name, items.token, items.permission,
    /// items.owner.name, items.createdAt, total, nextPageToken
    /// </param>
    /// <returns>Paginated list of shares with optional field filtering.</returns>
    /// <response code="200">Returns the paginated share list (optionally filtered).</response>
    /// <example>
    /// Examples:
    /// - GET /api/v1.0/shares
    ///   Returns complete paginated response
    ///
    /// - GET /api/v1.0/shares?fields=items(id,name,token),total
    ///   Returns: { "items": [{ "id": "1", "name": "Share 1", "token": "x" }], "total": 100 }
    ///
    /// - GET /api/v1.0/shares?fields=items(id,owner.name),nextPageToken
    ///   Returns: { "items": [{ "id": "1", "owner": { "name": "John" } }], "nextPageToken": "abc" }
    /// </example>
    [HttpGet]
    [FieldMask] // Enable field masking for this endpoint
    [ProducesResponseType(typeof(PagedResponse<ShareDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ShareDto>>> GetShares(
        [FromQuery] int pageSize = 20,
        [FromQuery] string? pageToken = null,
        [FromQuery] string? fields = null)
    {
        // Validate page size
        if (pageSize < 1 || pageSize > 100)
        {
            return BadRequest("Page size must be between 1 and 100");
        }

        // Optional: Optimize database query based on requested fields
        var includeOwner = fields?.Contains("owner") ?? fields?.Contains("items.owner") ?? true;
        var includeMetadata = fields?.Contains("metadata") ?? fields?.Contains("items.metadata") ?? true;

        var shares = await _shareService.GetSharesAsync(pageSize, pageToken, includeOwner, includeMetadata);

        // Return the paginated response - field mask filter will automatically apply
        return Ok(shares);
    }

    /// <summary>
    /// Example endpoint with custom query parameter name for field selection.
    /// </summary>
    /// <param name="id">The share ID.</param>
    /// <param name="select">
    /// Optional comma-separated field list (uses 'select' instead of 'fields').
    /// </param>
    /// <returns>The requested share with optional field filtering.</returns>
    /// <example>
    /// GET /api/v1.0/shares/abc123/summary?select=id,name,createdAt
    /// </example>
    [HttpGet("{id}/summary")]
    [FieldMask(QueryParameterName = "select")] // Use 'select' instead of 'fields'
    [ProducesResponseType(typeof(ShareDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShareDto>> GetShareSummary(
        string id,
        [FromQuery] string? select = null)
    {
        var share = await _shareService.GetShareAsync(id);

        if (share == null)
        {
            return NotFound();
        }

        return Ok(share);
    }

    /// <summary>
    /// Example endpoint with field masking explicitly disabled.
    /// </summary>
    /// <param name="id">The share ID.</param>
    /// <returns>Complete share object (field masking disabled).</returns>
    /// <remarks>
    /// This endpoint always returns the complete share object regardless of any
    /// fields parameter. Useful for specific endpoints that should always return
    /// full data for compatibility or business logic reasons.
    /// </remarks>
    [HttpGet("{id}/complete")]
    [FieldMask(Enabled = false)] // Explicitly disable field masking
    [ProducesResponseType(typeof(ShareDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShareDto>> GetCompleteShare(string id)
    {
        var share = await _shareService.GetShareAsync(id);

        if (share == null)
        {
            return NotFound();
        }

        // Always returns complete object, even if ?fields parameter is provided
        return Ok(share);
    }
}

#region Example DTOs and Services

/// <summary>
/// Example share DTO for demonstration purposes.
/// </summary>
public class ShareDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string Permission { get; set; } = string.Empty;
    public UserDto? Owner { get; set; }
    public ShareMetadataDto? Metadata { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Example user DTO for demonstration purposes.
/// </summary>
public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Avatar { get; set; }
}

/// <summary>
/// Example metadata DTO for demonstration purposes.
/// </summary>
public class ShareMetadataDto
{
    public List<string> Tags { get; set; } = new();
    public string? Description { get; set; }
}

/// <summary>
/// Example service interface (replace with actual service implementation).
/// </summary>
public interface IShareService
{
    Task<ShareDto?> GetShareAsync(
        string id,
        bool includeOwner = true,
        bool includeMetadata = true);

    Task<PagedResponse<ShareDto>> GetSharesAsync(
        int pageSize,
        string? pageToken,
        bool includeOwner = true,
        bool includeMetadata = true);
}

#endregion

/* USAGE EXAMPLES:

1. SIMPLE FIELD SELECTION:
   GET /api/v1.0/shares/abc123?fields=id,name,token
   Response: { "id": "abc123", "name": "My Share", "token": "xyz789" }

2. NESTED FIELD SELECTION:
   GET /api/v1.0/shares/abc123?fields=id,owner.name,owner.email
   Response: { "id": "abc123", "owner": { "name": "John", "email": "john@example.com" } }

3. COLLECTION FIELD SELECTION:
   GET /api/v1.0/shares?fields=items(id,name,createdAt),total,nextPageToken
   Response: { "items": [{ "id": "1", "name": "Share 1", "createdAt": "..." }], "total": 100, "nextPageToken": "abc" }

4. WILDCARD (ALL FIELDS):
   GET /api/v1.0/shares/abc123?fields=*
   Response: Complete share object with all fields

5. CUSTOM QUERY PARAMETER:
   GET /api/v1.0/shares/abc123/summary?select=id,name
   Response: { "id": "abc123", "name": "My Share" }

6. NO FIELD MASK:
   GET /api/v1.0/shares/abc123
   Response: Complete share object (no filtering)

7. DISABLED FIELD MASK:
   GET /api/v1.0/shares/abc123/complete?fields=id,name
   Response: Complete share object (field mask disabled for this endpoint)

PERFORMANCE BEST PRACTICES:

1. Combine field masks with database projections:
   var includeOwner = fields?.Contains("owner") ?? true;
   var share = await _repository.GetShareAsync(id, includeOwner);

2. For large collections, use field masks to reduce bandwidth:
   GET /api/v1.0/shares?fields=items(id,name),total

3. Mobile apps should request minimal fields:
   GET /api/v1.0/shares?fields=items(id,name,thumbnail),nextPageToken

SECURITY NOTES:

1. Field masks do NOT bypass authorization - always check permissions
2. Use DTOs to control which fields are exposed (don't return domain entities)
3. Invalid field names are silently ignored (fail-safe)
4. Field masks work on response data only (not database queries)

*/
