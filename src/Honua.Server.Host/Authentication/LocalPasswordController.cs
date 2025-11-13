// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Honua.Server.Core.Authentication;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Authentication;

[ApiController]
[Route("api/auth/local")]
public sealed class LocalPasswordController : ControllerBase
{
    private readonly ILocalAuthenticationService localAuthenticationService;
    private readonly IPasswordComplexityValidator passwordValidator;

    public LocalPasswordController(
        ILocalAuthenticationService localAuthenticationService,
        IPasswordComplexityValidator passwordValidator)
    {
        this.localAuthenticationService = Guard.NotNull(localAuthenticationService);
        this.passwordValidator = Guard.NotNull(passwordValidator);
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (!this.ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!ValidatePasswordComplexity(request.NewPassword, nameof(ChangePasswordRequest.NewPassword)))
        {
            return ValidationProblem(ModelState);
        }

        var userId = UserIdentityHelper.GetUserIdentifierOrNull(User);
        if (userId.IsNullOrWhiteSpace())
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Unable to resolve user identifier.");
        }

        try
        {
            await this.localAuthenticationService.ChangePasswordAsync(
                userId,
                request.CurrentPassword,
                request.NewPassword,
                this.HttpContext.Connection.RemoteIpAddress?.ToString(),
                this.Request.Headers.UserAgent.ToString(),
                cancellationToken).ConfigureAwait(false);

            return this.NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "User not found.", detail: ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Password change failed.", detail: ex.Message);
        }
    }

    [HttpPost("users/{userId}/password")]
    [Authorize(Policy = "RequireAdministrator")]
    public async Task<IActionResult> ResetPassword(
        [FromRoute] string userId,
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!this.ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!ValidatePasswordComplexity(request.NewPassword, nameof(ResetPasswordRequest.NewPassword)))
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            await this.localAuthenticationService.ResetPasswordAsync(
                userId,
                request.NewPassword,
                UserIdentityHelper.GetUserIdentifierOrNull(User),
                this.HttpContext.Connection.RemoteIpAddress?.ToString(),
                this.Request.Headers.UserAgent.ToString(),
                cancellationToken).ConfigureAwait(false);

            return this.NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "User not found.", detail: ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Password reset failed.", detail: ex.Message);
        }
    }

    public sealed class ChangePasswordRequest
    {
        [Required]
        public string CurrentPassword { get; init; } = string.Empty;

        [Required]
        public string NewPassword { get; init; } = string.Empty;
    }

    public sealed class ResetPasswordRequest
    {
        [Required]
        public string NewPassword { get; init; } = string.Empty;
    }

    private bool ValidatePasswordComplexity(string password, string modelStateKey)
    {
        var result = this.passwordValidator.Validate(password);
        if (result.IsValid)
        {
            return true;
        }

        foreach (var error in result.Errors)
        {
            this.ModelState.AddModelError(modelStateKey, error);
        }

        return false;
    }
}
