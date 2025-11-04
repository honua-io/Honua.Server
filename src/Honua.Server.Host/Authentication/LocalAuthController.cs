// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Authentication;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Logging;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Honua.Server.Host.Authentication;

[ApiController]
[Route("api/auth/local")]
public sealed class LocalAuthController : ControllerBase
{
    private readonly ILocalAuthenticationService _authenticationService;
    private readonly IOptionsMonitor<HonuaAuthenticationOptions> _options;
    private readonly ISecurityAuditLogger _auditLogger;

    public LocalAuthController(
        ILocalAuthenticationService authenticationService,
        IOptionsMonitor<HonuaAuthenticationOptions> options,
        ISecurityAuditLogger auditLogger)
    {
        _authenticationService = Guard.NotNull(authenticationService);
        _options = Guard.NotNull(options);
        _auditLogger = Guard.NotNull(auditLogger);
    }

    /// <summary>
    /// Authenticates a user with username and password.
    /// SECURITY: Rate limited to 5 attempts per 15 minutes per IP address to prevent brute force attacks.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    public async Task<IActionResult> Login([FromBody] LocalLoginRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var options = _options.CurrentValue;
        if (options.Mode != HonuaAuthenticationOptions.AuthenticationMode.Local)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Local authentication disabled",
                Detail = "Local authentication is not enabled on this server.",
                Instance = Request.Path
            });
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        var result = await _authenticationService.AuthenticateAsync(request.Username, request.Password, cancellationToken).ConfigureAwait(false);

        return result.Status switch
        {
            LocalAuthenticationStatus.Success => HandleSuccess(result, request.Username, ipAddress, userAgent),
            LocalAuthenticationStatus.InvalidCredentials => HandleInvalidCredentials(request.Username, ipAddress, userAgent),
            LocalAuthenticationStatus.LockedOut => HandleLockedOut(request.Username, ipAddress, userAgent, result.LockedUntil),
            LocalAuthenticationStatus.Disabled => HandleDisabled(request.Username, ipAddress, userAgent),
            LocalAuthenticationStatus.NotConfigured => StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Local authentication disabled",
                Detail = "Local authentication is not configured on this server.",
                Instance = Request.Path
            }),
            _ => Problem(detail: "Authentication failed.", statusCode: StatusCodes.Status500InternalServerError, title: "Authentication error", instance: Request.Path)
        };
    }

    private IActionResult HandleSuccess(LocalAuthenticationResult result, string username, string? ipAddress, string? userAgent)
    {
        _auditLogger.LogLoginSuccess(username, ipAddress, userAgent);
        return Ok(new { token = result.Token, roles = result.Roles });
    }

    private IActionResult HandleInvalidCredentials(string username, string? ipAddress, string? userAgent)
    {
        _auditLogger.LogLoginFailure(username, ipAddress, userAgent, "invalid_credentials");
        return CreateUniformFailureResponse();
    }

    private IActionResult HandleLockedOut(string username, string? ipAddress, string? userAgent, DateTimeOffset? lockedUntil)
    {
        if (lockedUntil.HasValue)
        {
            _auditLogger.LogAccountLockout(username, ipAddress, lockedUntil.Value);
        }
        _auditLogger.LogLoginFailure(username, ipAddress, userAgent, "account_locked");
        return CreateUniformFailureResponse();
    }

    private IActionResult HandleDisabled(string username, string? ipAddress, string? userAgent)
    {
        _auditLogger.LogLoginFailure(username, ipAddress, userAgent, "account_disabled");
        return CreateUniformFailureResponse();
    }

    private IActionResult CreateUniformFailureResponse()
    {
        return Unauthorized(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Authentication failed",
            Detail = "Authentication failed. Verify your credentials and try again.",
            Instance = Request.Path
        });
    }

    public sealed class LocalLoginRequest
    {
        [Required]
        [StringLength(256, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 256 characters")]
        public string Username { get; init; } = string.Empty;

        [Required]
        [StringLength(128, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 128 characters")]
        public string Password { get; init; } = string.Empty;
    }
}
