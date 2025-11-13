// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Honua.Server.Core.Models.Dashboard;
using Honua.Server.Core.Data.Dashboard;
using System.Security.Claims;

namespace Honua.Server.Host.API;

/// <summary>
/// REST API for dashboard management.
/// Provides endpoints for creating, reading, updating, and deleting dashboards.
/// </summary>
[ApiController]
[Route("api/dashboards")]
[Produces("application/json")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly ILogger<DashboardController> logger;
    private readonly IDashboardRepository repository;

    public DashboardController(
        ILogger<DashboardController> logger,
        IDashboardRepository repository)
    {
        this.logger = logger;
        this.repository = repository;
    }

    /// <summary>
    /// Get a dashboard by ID.
    /// </summary>
    /// <param name="id">Dashboard ID</param>
    /// <response code="200">Dashboard found</response>
    /// <response code="404">Dashboard not found</response>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(DashboardDefinition), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetDashboard(Guid id)
    {
        var dashboard = await this.repository.GetByIdAsync(id);
        if (dashboard == null)
        {
            return this.NotFound(new { message = "Dashboard not found" });
        }

        var userId = GetUserId();
        var isOwner = dashboard.OwnerId == userId;

        // Check access permissions
        if (!dashboard.IsPublic && !isOwner)
        {
            return Forbid();
        }

        // Return different DTO based on ownership
        if (isOwner)
        {
            return this.Ok(ToOwnerDto(dashboard));
        }
        else
        {
            return this.Ok(ToPublicDto(dashboard));
        }
    }

    /// <summary>
    /// Get all dashboards owned by the current user.
    /// </summary>
    /// <response code="200">List of dashboards</response>
    [HttpGet("my-dashboards")]
    [ProducesResponseType(typeof(List<DashboardDefinition>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<DashboardDefinition>>> GetMyDashboards()
    {
        var userId = GetUserId();
        var dashboards = await this.repository.GetByOwnerAsync(userId);
        return this.Ok(dashboards);
    }

    /// <summary>
    /// Get all public dashboards.
    /// </summary>
    /// <response code="200">List of public dashboards</response>
    [HttpGet("public")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<PublicDashboardDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PublicDashboardDto>>> GetPublicDashboards()
    {
        var dashboards = await this.repository.GetPublicDashboardsAsync();
        var dtos = dashboards.Select(d => ToPublicDto(d)).ToList();
        return this.Ok(dtos);
    }

    /// <summary>
    /// Get all dashboard templates.
    /// </summary>
    /// <response code="200">List of template dashboards</response>
    [HttpGet("templates")]
    [ProducesResponseType(typeof(List<DashboardDefinition>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<DashboardDefinition>>> GetTemplates()
    {
        var templates = await this.repository.GetTemplatesAsync();
        return this.Ok(templates);
    }

    /// <summary>
    /// Search dashboards by name, description, or tags.
    /// </summary>
    /// <param name="q">Search query</param>
    /// <response code="200">List of matching dashboards</response>
    [HttpGet("search")]
    [ProducesResponseType(typeof(List<PublicDashboardDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<PublicDashboardDto>>> SearchDashboards([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return this.BadRequest(new { message = "Search query cannot be empty" });
        }

        var dashboards = await this.repository.SearchAsync(q);

        // Filter by access permissions and return appropriate DTOs
        var userId = GetUserId();
        var accessible = dashboards
            .Where(d => d.IsPublic || d.OwnerId == userId)
            .Select(d => d.OwnerId == userId ? (PublicDashboardDto)ToOwnerDto(d) : ToPublicDto(d))
            .ToList();

        return this.Ok(accessible);
    }

    /// <summary>
    /// Create a new dashboard.
    /// </summary>
    /// <param name="request">Dashboard creation request</param>
    /// <response code="201">Dashboard created</response>
    /// <response code="400">Invalid request</response>
    [HttpPost]
    [ProducesResponseType(typeof(DashboardDefinition), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DashboardDefinition>> CreateDashboard([FromBody] CreateDashboardRequest request)
    {
        var userId = GetUserId();

        var dashboard = new DashboardDefinition
        {
            Name = request.Name,
            Description = request.Description,
            OwnerId = userId,
            Tags = request.Tags ?? new List<string>(),
            Layout = request.Layout ?? new DashboardLayout(),
            Widgets = request.Widgets ?? new List<WidgetDefinition>(),
            Connections = request.Connections ?? new List<WidgetConnection>(),
            IsPublic = request.IsPublic,
            IsTemplate = request.IsTemplate,
            RefreshInterval = request.RefreshInterval,
            Theme = request.Theme
        };

        var id = await this.repository.CreateAsync(dashboard);
        dashboard.Id = id;

        this.logger.LogInformation("User {UserId} created dashboard {DashboardId}", userId, id);

        return this.CreatedAtAction(nameof(GetDashboard), new { id }, dashboard);
    }

    /// <summary>
    /// Update an existing dashboard.
    /// </summary>
    /// <param name="id">Dashboard ID</param>
    /// <param name="request">Dashboard update request</param>
    /// <response code="200">Dashboard updated</response>
    /// <response code="404">Dashboard not found</response>
    /// <response code="403">Forbidden - user does not own this dashboard</response>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(DashboardDefinition), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DashboardDefinition>> UpdateDashboard(Guid id, [FromBody] UpdateDashboardRequest request)
    {
        var existing = await this.repository.GetByIdAsync(id);
        if (existing == null)
        {
            return this.NotFound(new { message = "Dashboard not found" });
        }

        var userId = GetUserId();
        if (existing.OwnerId != userId)
        {
            return Forbid();
        }

        // Update properties
        existing.Name = request.Name ?? existing.Name;
        existing.Description = request.Description ?? existing.Description;
        existing.Tags = request.Tags ?? existing.Tags;
        existing.Layout = request.Layout ?? existing.Layout;
        existing.Widgets = request.Widgets ?? existing.Widgets;
        existing.Connections = request.Connections ?? existing.Connections;
        existing.RefreshInterval = request.RefreshInterval ?? existing.RefreshInterval;
        existing.Theme = request.Theme ?? existing.Theme;

        if (request.IsPublic.HasValue)
        {
            existing.IsPublic = request.IsPublic.Value;
        }

        var success = await this.repository.UpdateAsync(existing);
        if (!success)
        {
            return this.StatusCode(500, new { message = "Failed to update dashboard" });
        }

        this.logger.LogInformation("User {UserId} updated dashboard {DashboardId}", userId, id);

        return this.Ok(existing);
    }

    /// <summary>
    /// Delete a dashboard.
    /// </summary>
    /// <param name="id">Dashboard ID</param>
    /// <response code="204">Dashboard deleted</response>
    /// <response code="404">Dashboard not found</response>
    /// <response code="403">Forbidden - user does not own this dashboard</response>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteDashboard(Guid id)
    {
        var existing = await this.repository.GetByIdAsync(id);
        if (existing == null)
        {
            return this.NotFound(new { message = "Dashboard not found" });
        }

        var userId = GetUserId();
        if (existing.OwnerId != userId)
        {
            return Forbid();
        }

        var success = await this.repository.DeleteAsync(id);
        if (!success)
        {
            return this.StatusCode(500, new { message = "Failed to delete dashboard" });
        }

        this.logger.LogInformation("User {UserId} deleted dashboard {DashboardId}", userId, id);

        return this.NoContent();
    }

    /// <summary>
    /// Share or unshare a dashboard (make it public or private).
    /// </summary>
    /// <param name="id">Dashboard ID</param>
    /// <param name="request">Share request</param>
    /// <response code="200">Dashboard sharing updated</response>
    /// <response code="404">Dashboard not found</response>
    /// <response code="403">Forbidden - user does not own this dashboard</response>
    [HttpPost("{id}/share")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ShareDashboard(Guid id, [FromBody] ShareDashboardRequest request)
    {
        var existing = await this.repository.GetByIdAsync(id);
        if (existing == null)
        {
            return this.NotFound(new { message = "Dashboard not found" });
        }

        var userId = GetUserId();
        if (existing.OwnerId != userId)
        {
            return Forbid();
        }

        var success = await this.repository.ShareAsync(id, request.IsPublic);
        if (!success)
        {
            return this.StatusCode(500, new { message = "Failed to update sharing settings" });
        }

        this.logger.LogInformation("User {UserId} changed dashboard {DashboardId} sharing to {IsPublic}",
            userId, id, request.IsPublic);

        return this.Ok(new { message = $"Dashboard is now {(request.IsPublic ? "public" : "private")}" });
    }

    /// <summary>
    /// Clone a dashboard (create a copy).
    /// </summary>
    /// <param name="id">Source dashboard ID</param>
    /// <param name="request">Clone request</param>
    /// <response code="201">Dashboard cloned</response>
    /// <response code="404">Source dashboard not found</response>
    [HttpPost("{id}/clone")]
    [ProducesResponseType(typeof(DashboardDefinition), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DashboardDefinition>> CloneDashboard(Guid id, [FromBody] CloneDashboardRequest request)
    {
        var userId = GetUserId();
        var cloned = await this.repository.CloneAsync(id, userId, request.Name);

        if (cloned == null)
        {
            return this.NotFound(new { message = "Source dashboard not found" });
        }

        this.logger.LogInformation("User {UserId} cloned dashboard {SourceId} to {ClonedId}",
            userId, id, cloned.Id);

        return this.CreatedAtAction(nameof(GetDashboard), new { id = cloned.Id }, cloned);
    }

    /// <summary>
    /// Export dashboard definition as JSON.
    /// </summary>
    /// <param name="id">Dashboard ID</param>
    /// <response code="200">Dashboard JSON</response>
    /// <response code="404">Dashboard not found</response>
    [HttpGet("{id}/export")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(DashboardDefinition), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DashboardDefinition>> ExportDashboard(Guid id)
    {
        var dashboard = await this.repository.GetByIdAsync(id);
        if (dashboard == null)
        {
            return this.NotFound(new { message = "Dashboard not found" });
        }

        var userId = GetUserId();
        if (!dashboard.IsPublic && dashboard.OwnerId != userId)
        {
            return Forbid();
        }

        return this.Ok(dashboard);
    }

    /// <summary>
    /// Import dashboard definition from JSON.
    /// </summary>
    /// <param name="dashboard">Dashboard definition</param>
    /// <response code="201">Dashboard imported</response>
    /// <response code="400">Invalid dashboard definition</response>
    [HttpPost("import")]
    [ProducesResponseType(typeof(DashboardDefinition), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DashboardDefinition>> ImportDashboard([FromBody] DashboardDefinition dashboard)
    {
        var userId = GetUserId();

        // Override owner and generate new ID
        dashboard.Id = Guid.NewGuid();
        dashboard.OwnerId = userId;
        dashboard.IsPublic = false; // Imported dashboards are private by default

        var id = await this.repository.CreateAsync(dashboard);
        dashboard.Id = id;

        this.logger.LogInformation("User {UserId} imported dashboard {DashboardId}", userId, id);

        return this.CreatedAtAction(nameof(GetDashboard), new { id }, dashboard);
    }

    private PublicDashboardDto ToPublicDto(DashboardDefinition dashboard)
    {
        return new PublicDashboardDto
        {
            Id = dashboard.Id,
            Name = dashboard.Name,
            Description = dashboard.Description,
            Tags = dashboard.Tags,
            Layout = dashboard.Layout,
            Widgets = dashboard.Widgets,
            Connections = dashboard.Connections,
            IsPublic = dashboard.IsPublic,
            RefreshInterval = dashboard.RefreshInterval,
            Theme = dashboard.Theme,
            CreatedAt = dashboard.CreatedAt,
            UpdatedAt = dashboard.UpdatedAt
        };
    }

    private OwnerDashboardDto ToOwnerDto(DashboardDefinition dashboard)
    {
        return new OwnerDashboardDto
        {
            Id = dashboard.Id,
            Name = dashboard.Name,
            Description = dashboard.Description,
            Tags = dashboard.Tags,
            Layout = dashboard.Layout,
            Widgets = dashboard.Widgets,
            Connections = dashboard.Connections,
            IsPublic = dashboard.IsPublic,
            RefreshInterval = dashboard.RefreshInterval,
            Theme = dashboard.Theme,
            CreatedAt = dashboard.CreatedAt,
            UpdatedAt = dashboard.UpdatedAt,
            OwnerId = dashboard.OwnerId,
            IsTemplate = dashboard.IsTemplate
        };
    }

    private string GetUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? "anonymous";
    }
}

#region Request/Response DTOs

/// <summary>
/// Request to create a new dashboard.
/// </summary>
public class CreateDashboardRequest
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public List<string>? Tags { get; set; }
    public DashboardLayout? Layout { get; set; }
    public List<WidgetDefinition>? Widgets { get; set; }
    public List<WidgetConnection>? Connections { get; set; }
    public bool IsPublic { get; set; } = false;
    public bool IsTemplate { get; set; } = false;
    public int? RefreshInterval { get; set; }
    public DashboardTheme? Theme { get; set; }
}

/// <summary>
/// Request to update an existing dashboard.
/// </summary>
public class UpdateDashboardRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public List<string>? Tags { get; set; }
    public DashboardLayout? Layout { get; set; }
    public List<WidgetDefinition>? Widgets { get; set; }
    public List<WidgetConnection>? Connections { get; set; }
    public bool? IsPublic { get; set; }
    public int? RefreshInterval { get; set; }
    public DashboardTheme? Theme { get; set; }
}

/// <summary>
/// Request to share/unshare a dashboard.
/// </summary>
public class ShareDashboardRequest
{
    public bool IsPublic { get; set; }
}

/// <summary>
/// Request to clone a dashboard.
/// </summary>
public class CloneDashboardRequest
{
    public string? Name { get; set; }
}

/// <summary>
/// Public dashboard view without sensitive metadata.
/// </summary>
public class PublicDashboardDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> Tags { get; set; } = new();
    public DashboardLayout Layout { get; set; } = new();
    public List<WidgetDefinition> Widgets { get; set; } = new();
    public List<WidgetConnection> Connections { get; set; } = new();
    public bool IsPublic { get; set; }
    public int? RefreshInterval { get; set; }
    public DashboardTheme? Theme { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Explicitly exclude: OwnerId, IsTemplate, internal metadata
}

/// <summary>
/// Full dashboard view with all metadata for owners.
/// </summary>
public class OwnerDashboardDto : PublicDashboardDto
{
    public string OwnerId { get; set; } = string.Empty;
    public bool IsTemplate { get; set; }
}

#endregion
