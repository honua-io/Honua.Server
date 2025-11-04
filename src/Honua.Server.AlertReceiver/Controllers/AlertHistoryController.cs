// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.ComponentModel.DataAnnotations;
using Honua.Server.AlertReceiver.Data;
using Honua.Server.AlertReceiver.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.AlertReceiver.Controllers;

/// <summary>
/// API for querying alert history, acknowledgements, and silencing rules.
/// </summary>
[ApiController]
[Route("api/alerts")]
public sealed class AlertHistoryController : ControllerBase
{
    private readonly IAlertPersistenceService _persistenceService;
    private readonly IAlertSilencingService _silencingService;
    private readonly ILogger<AlertHistoryController> _logger;

    public AlertHistoryController(
        IAlertPersistenceService persistenceService,
        IAlertSilencingService silencingService,
        ILogger<AlertHistoryController> logger)
    {
        _persistenceService = persistenceService;
        _silencingService = silencingService;
        _logger = logger;
    }

    /// <summary>
    /// Get recent alerts.
    /// </summary>
    [HttpGet("history")]
    [Authorize]
    public async Task<IActionResult> GetHistory(
        [FromQuery] int limit = 100,
        [FromQuery] string? severity = null)
    {
        // Validate limit (max 1000)
        if (limit < 1 || limit > 1000)
        {
            return BadRequest(new { error = "Limit must be between 1 and 1000" });
        }

        // Validate severity format
        if (!string.IsNullOrWhiteSpace(severity))
        {
            if (severity.Length > 50)
            {
                return BadRequest(new { error = "Severity must be 50 characters or less" });
            }

            var validSeverities = new[] { "critical", "high", "medium", "low", "info", "warning", "error", "crit", "err", "warn", "fatal", "information" };
            if (!validSeverities.Contains(severity.ToLowerInvariant()))
            {
                return BadRequest(new { error = $"Invalid severity: {severity}. Valid values: critical, high, medium, low, info, warning, error" });
            }
        }

        try
        {
            var alerts = await _persistenceService.GetRecentAlertsAsync(limit, severity);
            return Ok(new { alerts, count = alerts.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get alert history");
            return StatusCode(500, new { error = "Failed to retrieve alert history" });
        }
    }

    /// <summary>
    /// Get alert by fingerprint.
    /// </summary>
    [HttpGet("history/{fingerprint}")]
    [Authorize]
    public async Task<IActionResult> GetByFingerprint(string fingerprint)
    {
        // Validate fingerprint format
        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            return BadRequest(new { error = "Fingerprint is required" });
        }

        if (fingerprint.Length > 256)
        {
            return BadRequest(new { error = "Fingerprint must be 256 characters or less" });
        }

        // Validate fingerprint contains only valid characters (alphanumeric, dash, underscore)
        if (!System.Text.RegularExpressions.Regex.IsMatch(fingerprint, @"^[a-zA-Z0-9\-_]+$"))
        {
            return BadRequest(new { error = "Fingerprint contains invalid characters. Only alphanumeric, dash, and underscore are allowed" });
        }

        try
        {
            var alert = await _persistenceService.GetAlertByFingerprintAsync(fingerprint);
            if (alert == null)
            {
                return NotFound(new { error = "Alert not found" });
            }

            return Ok(alert);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get alert by fingerprint: {Fingerprint}", fingerprint);
            return StatusCode(500, new { error = "Failed to retrieve alert" });
        }
    }

    /// <summary>
    /// Acknowledge an alert.
    /// </summary>
    [HttpPost("acknowledge")]
    [Authorize]
    public async Task<IActionResult> Acknowledge([FromBody] AcknowledgeRequest request)
    {
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Acknowledge request validation failed: {ValidationErrors}",
                string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            return BadRequest(ModelState);
        }

        // Additional validation for date range
        if (request.ExpiresInMinutes.HasValue)
        {
            if (request.ExpiresInMinutes.Value < 1 || request.ExpiresInMinutes.Value > 43200)
            {
                return BadRequest(new { error = "ExpiresInMinutes must be between 1 and 43200 (30 days)" });
            }
        }

        try
        {
            await _silencingService.AcknowledgeAlertAsync(
                request.Fingerprint,
                request.AcknowledgedBy,
                request.Comment,
                request.ExpiresInMinutes.HasValue ? TimeSpan.FromMinutes(request.ExpiresInMinutes.Value) : null);

            return Ok(new { status = "acknowledged", fingerprint = request.Fingerprint });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acknowledge alert: {Fingerprint}", request.Fingerprint);
            return StatusCode(500, new { error = "Failed to acknowledge alert" });
        }
    }

    /// <summary>
    /// Create a silencing rule.
    /// </summary>
    [HttpPost("silence")]
    [Authorize]
    public async Task<IActionResult> CreateSilence([FromBody] CreateSilenceRequest request)
    {
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Create silence request validation failed: {ValidationErrors}",
                string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));
            return BadRequest(ModelState);
        }

        // Additional validation for date range
        var startsAt = request.StartsAt ?? DateTime.UtcNow;
        if (request.EndsAt <= startsAt)
        {
            return BadRequest(new { error = "EndsAt must be after StartsAt" });
        }

        // Validate silencing rule duration (max 1 year)
        var duration = request.EndsAt - startsAt;
        if (duration.TotalDays > 365)
        {
            return BadRequest(new { error = "Silencing rule duration cannot exceed 365 days" });
        }

        // Validate matchers
        if (request.Matchers == null || request.Matchers.Count == 0)
        {
            return BadRequest(new { error = "At least one matcher is required" });
        }

        if (request.Matchers.Count > 50)
        {
            return BadRequest(new { error = "Maximum 50 matchers allowed" });
        }

        foreach (var (key, value) in request.Matchers)
        {
            if (key.Length > 256)
            {
                return BadRequest(new { error = $"Matcher key '{key.Substring(0, Math.Min(50, key.Length))}...' exceeds 256 character limit" });
            }
            if (value.Length > 1000)
            {
                return BadRequest(new { error = $"Matcher value for key '{key}' exceeds 1000 character limit" });
            }
        }

        try
        {
            await _silencingService.CreateSilencingRuleAsync(
                request.Name,
                request.Matchers,
                request.CreatedBy,
                startsAt,
                request.EndsAt,
                request.Comment);

            return Ok(new { status = "created", name = request.Name });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create silencing rule: {Name}", request.Name);
            return StatusCode(500, new { error = "Failed to create silencing rule" });
        }
    }

    /// <summary>
    /// Get active silencing rules.
    /// </summary>
    [HttpGet("silences")]
    [Authorize]
    public async Task<IActionResult> GetSilences()
    {
        try
        {
            var rules = await _silencingService.GetActiveSilencingRulesAsync();
            return Ok(new { rules, count = rules.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get silencing rules");
            return StatusCode(500, new { error = "Failed to retrieve silencing rules" });
        }
    }

    /// <summary>
    /// Deactivate a silencing rule.
    /// </summary>
    [HttpDelete("silences/{ruleId}")]
    [Authorize]
    public async Task<IActionResult> DeleteSilence(long ruleId)
    {
        try
        {
            await _silencingService.DeactivateSilencingRuleAsync(ruleId);
            return Ok(new { status = "deactivated", ruleId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deactivate silencing rule: {RuleId}", ruleId);
            return StatusCode(500, new { error = "Failed to deactivate silencing rule" });
        }
    }
}

public class AcknowledgeRequest
{
    [Required(ErrorMessage = "Fingerprint is required")]
    [StringLength(256, MinimumLength = 1, ErrorMessage = "Fingerprint must be between 1 and 256 characters")]
    public string Fingerprint { get; set; } = string.Empty;

    [Required(ErrorMessage = "AcknowledgedBy is required")]
    [StringLength(256, MinimumLength = 1, ErrorMessage = "AcknowledgedBy must be between 1 and 256 characters")]
    public string AcknowledgedBy { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Comment must be 1000 characters or less")]
    public string? Comment { get; set; }

    [Range(1, 43200, ErrorMessage = "ExpiresInMinutes must be between 1 and 43200 (30 days)")]
    public int? ExpiresInMinutes { get; set; }
}

public class CreateSilenceRequest
{
    [Required(ErrorMessage = "Name is required")]
    [StringLength(256, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 256 characters")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Matchers are required")]
    [MaxLength(50, ErrorMessage = "Maximum 50 matchers allowed")]
    public Dictionary<string, string> Matchers { get; set; } = new();

    [Required(ErrorMessage = "CreatedBy is required")]
    [StringLength(256, MinimumLength = 1, ErrorMessage = "CreatedBy must be between 1 and 256 characters")]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime? StartsAt { get; set; }

    [Required(ErrorMessage = "EndsAt is required")]
    public DateTime EndsAt { get; set; }

    [StringLength(1000, ErrorMessage = "Comment must be 1000 characters or less")]
    public string? Comment { get; set; }
}
