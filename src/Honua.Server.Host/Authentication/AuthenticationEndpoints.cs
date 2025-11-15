// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Host.Authentication;

/// <summary>
/// Extension methods for registering authentication-related endpoints.
/// </summary>
public static class AuthenticationEndpoints
{
    /// <summary>
    /// Maps authentication endpoints (login, logout, callback) for OIDC providers.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The endpoint route builder for method chaining.</returns>
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/auth");

        // GET /auth/login/{provider}
        group.MapGet("/login/{provider}", LoginEndpoint)
            .WithName("Login")
            .WithTags("Authentication")
            .WithOpenApi(operation =>
            {
                operation.Summary = "Initiates login flow for the specified OIDC provider";
                operation.Description = "Redirects the user to the specified OIDC provider's login page. Supported providers: oidc, azuread, google.";
                return operation;
            })
            .Produces(StatusCodes.Status302Found)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        // GET /auth/logout
        group.MapGet("/logout", LogoutEndpoint)
            .WithName("Logout")
            .WithTags("Authentication")
            .WithOpenApi(operation =>
            {
                operation.Summary = "Logs out the current user";
                operation.Description = "Signs out the user from the application and optionally from the OIDC provider.";
                return operation;
            })
            .Produces(StatusCodes.Status302Found)
            .Produces(StatusCodes.Status200OK);

        // GET /auth/user
        group.MapGet("/user", GetCurrentUserEndpoint)
            .WithName("GetCurrentUser")
            .WithTags("Authentication")
            .WithOpenApi(operation =>
            {
                operation.Summary = "Gets information about the currently authenticated user";
                operation.Description = "Returns the user's claims and authentication status.";
                return operation;
            })
            .Produces<UserInfo>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);

        return endpoints;
    }

    /// <summary>
    /// Handles the login endpoint.
    /// Challenges the user with the specified authentication provider.
    /// </summary>
    private static IResult LoginEndpoint(
        string provider,
        [FromQuery] string? returnUrl,
        [FromServices] IOptions<HonuaAuthenticationOptions> authOptions,
        [FromServices] ILogger<Program> logger,
        HttpContext context)
    {
        // Validate provider
        var validProviders = GetValidProviders(authOptions.Value);
        if (!validProviders.Contains(provider, StringComparer.OrdinalIgnoreCase))
        {
            logger.LogWarning("Invalid authentication provider requested: {Provider}", provider);
            return Results.Problem(
                title: "Invalid Provider",
                detail: $"Provider '{provider}' is not configured or supported. Valid providers: {string.Join(", ", validProviders)}",
                statusCode: StatusCodes.Status400BadRequest);
        }

        // Sanitize return URL
        returnUrl = SanitizeReturnUrl(returnUrl, context);

        logger.LogInformation("Initiating login for provider: {Provider}, ReturnUrl: {ReturnUrl}", provider, returnUrl);

        // Create authentication properties
        var properties = new AuthenticationProperties
        {
            RedirectUri = returnUrl
        };

        // Challenge the user with the specified provider
        return Results.Challenge(properties, new[] { provider });
    }

    /// <summary>
    /// Handles the logout endpoint.
    /// Signs out the user from the application and optionally from the OIDC provider.
    /// </summary>
    private static async Task<IResult> LogoutEndpoint(
        [FromQuery] string? returnUrl,
        [FromServices] ILogger<Program> logger,
        HttpContext context)
    {
        // Sanitize return URL
        returnUrl = SanitizeReturnUrl(returnUrl, context);

        logger.LogInformation("User logout requested. ReturnUrl: {ReturnUrl}", returnUrl);

        // Determine which authentication schemes to sign out from
        var schemesToSignOut = new List<string>();

        // Check if user is authenticated with OIDC
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var authenticationType = context.User.Identity.AuthenticationType;

            if (authenticationType?.Contains("oidc", StringComparison.OrdinalIgnoreCase) == true)
            {
                schemesToSignOut.Add("oidc");
            }
            else if (authenticationType?.Contains("azuread", StringComparison.OrdinalIgnoreCase) == true)
            {
                schemesToSignOut.Add("azuread");
            }
            else if (authenticationType?.Contains("google", StringComparison.OrdinalIgnoreCase) == true)
            {
                schemesToSignOut.Add("google");
            }

            // Also sign out from cookie authentication if present
            if (context.User.HasClaim(c => c.Type == ".AspNetCore.Identity.Application"))
            {
                schemesToSignOut.Add(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        }

        // Sign out from all identified schemes
        foreach (var scheme in schemesToSignOut)
        {
            await context.SignOutAsync(scheme);
            logger.LogInformation("Signed out from authentication scheme: {Scheme}", scheme);
        }

        // Clear any application cookies
        context.Response.Cookies.Delete(".AspNetCore.Cookies");
        context.Response.Cookies.Delete(".AspNetCore.Session");

        // Return redirect or success
        if (!string.IsNullOrWhiteSpace(returnUrl))
        {
            return Results.Redirect(returnUrl);
        }

        return Results.Ok(new { message = "Logout successful" });
    }

    /// <summary>
    /// Returns information about the currently authenticated user.
    /// </summary>
    private static IResult GetCurrentUserEndpoint(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            return Results.Unauthorized();
        }

        var userInfo = new UserInfo
        {
            IsAuthenticated = true,
            Name = context.User.Identity.Name,
            AuthenticationType = context.User.Identity.AuthenticationType,
            Claims = context.User.Claims.Select(c => new ClaimInfo
            {
                Type = c.Type,
                Value = c.Value
            }).ToList()
        };

        return Results.Ok(userInfo);
    }

    /// <summary>
    /// Gets the list of valid authentication providers based on configuration.
    /// </summary>
    private static List<string> GetValidProviders(HonuaAuthenticationOptions authOptions)
    {
        var providers = new List<string>();

        if (authOptions.Oidc.Enabled)
        {
            providers.Add("oidc");
        }

        if (authOptions.AzureAd.Enabled)
        {
            providers.Add("azuread");
        }

        if (authOptions.Google.Enabled)
        {
            providers.Add("google");
        }

        return providers;
    }

    /// <summary>
    /// Sanitizes the return URL to prevent open redirect vulnerabilities.
    /// </summary>
    /// <param name="returnUrl">The return URL to sanitize.</param>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A safe return URL.</returns>
    private static string SanitizeReturnUrl(string? returnUrl, HttpContext context)
    {
        // Default to root if no return URL specified
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return "/";
        }

        // SECURITY: Only allow local URLs to prevent open redirect attacks
        // A local URL starts with "/" but not "//" (which could be //evil.com)
        if (returnUrl.StartsWith("/") && !returnUrl.StartsWith("//"))
        {
            return returnUrl;
        }

        // If it's not a local URL, check if it matches the current host
        if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var uri))
        {
            var currentHost = context.Request.Host.Host;
            if (uri.Host.Equals(currentHost, StringComparison.OrdinalIgnoreCase))
            {
                return returnUrl;
            }
        }

        // If we can't validate it, return root
        return "/";
    }
}

/// <summary>
/// Represents information about the currently authenticated user.
/// </summary>
public sealed class UserInfo
{
    /// <summary>
    /// Gets or sets a value indicating whether the user is authenticated.
    /// </summary>
    public bool IsAuthenticated { get; set; }

    /// <summary>
    /// Gets or sets the user's name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the authentication type.
    /// </summary>
    public string? AuthenticationType { get; set; }

    /// <summary>
    /// Gets or sets the user's claims.
    /// </summary>
    public List<ClaimInfo> Claims { get; set; } = new();
}

/// <summary>
/// Represents a single claim.
/// </summary>
public sealed class ClaimInfo
{
    /// <summary>
    /// Gets or sets the claim type.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the claim value.
    /// </summary>
    public string Value { get; set; } = string.Empty;
}
