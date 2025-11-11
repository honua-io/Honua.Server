// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Authentication;
using Honua.Server.Enterprise.Authentication;
using Honua.Server.Enterprise.Multitenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Host.Authentication;

/// <summary>
/// Endpoints for SAML 2.0 Single Sign-On
/// </summary>
public static class SamlEndpoints
{
    /// <summary>
    /// Maps SAML SSO endpoints
    /// </summary>
    public static IEndpointRouteBuilder MapSamlEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/auth/saml");

        // SSO initiation endpoint
        group.MapGet("/login", InitiateSsoAsync)
            .WithName("SamlLogin")
            .WithOpenApi(op =>
            {
                op.Summary = "Initiate SAML SSO";
                op.Description = "Redirects to the IdP for authentication";
                return op;
            });

        // Assertion Consumer Service (ACS) endpoint
        group.MapPost("/acs", AssertionConsumerServiceAsync)
            .WithName("SamlACS")
            .WithOpenApi(op =>
            {
                op.Summary = "SAML Assertion Consumer Service";
                op.Description = "Receives and validates SAML assertions from IdP";
                return op;
            })
            .DisableAntiforgery(); // SAML POST doesn't include anti-forgery token

        // Service Provider metadata endpoint
        group.MapGet("/metadata", GetMetadataAsync)
            .WithName("SamlMetadata")
            .WithOpenApi(op =>
            {
                op.Summary = "Get Service Provider metadata";
                op.Description = "Returns SP metadata XML for IdP configuration";
                return op;
            });

        return endpoints;
    }

    private static async Task<IResult> InitiateSsoAsync(
        HttpContext context,
        [FromServices] ISamlService samlService,
        [FromServices] ITenantProvider tenantProvider,
        [FromServices] ILogger<SamlService> logger,
        string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get current tenant
            var tenant = await tenantProvider.GetCurrentTenantAsync(context, cancellationToken);
            if (tenant == null)
            {
                logger.LogWarning("SAML SSO initiated without tenant context");
                return Results.BadRequest(new { error = "Tenant not found" });
            }

            logger.LogInformation("Initiating SAML SSO for tenant {TenantId}", tenant.TenantId);

            // Parse tenant ID to Guid
            if (!Guid.TryParse(tenant.TenantId, out var tenantGuid))
            {
                logger.LogError("Invalid tenant ID format: {TenantId}", tenant.TenantId);
                return Results.BadRequest(new { error = "Invalid tenant ID format" });
            }

            // Create authentication request
            var authRequest = await samlService.CreateAuthenticationRequestAsync(
                tenantGuid,
                returnUrl,
                cancellationToken);

            // For HTTP-POST binding, return HTML form
            if (authRequest.BindingType == SamlBindingType.HttpPost)
            {
                var html = GenerateAutoSubmitForm(
                    authRequest.RedirectUrl,
                    authRequest.SamlRequest!,
                    authRequest.RelayState);

                return Results.Content(html, "text/html");
            }

            // For HTTP-Redirect binding, return redirect
            return Results.Redirect(authRequest.RedirectUrl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error initiating SAML SSO");
            return Results.Problem(
                title: "SAML SSO Error",
                detail: "Failed to initiate SAML authentication. Please contact your administrator.",
                statusCode: 500);
        }
    }

    private static async Task<IResult> AssertionConsumerServiceAsync(
        HttpContext context,
        [FromServices] ISamlService samlService,
        [FromServices] ISamlUserProvisioningService provisioningService,
        [FromServices] ITenantProvider tenantProvider,
        [FromServices] IUserContext userContext,
        [FromServices] ILogger<SamlService> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get SAML response from form
            var samlResponse = context.Request.Form["SAMLResponse"].ToString();
            var relayState = context.Request.Form["RelayState"].ToString();

            if (string.IsNullOrEmpty(samlResponse))
            {
                logger.LogWarning("SAML ACS called without SAMLResponse");
                return Results.BadRequest(new { error = "SAMLResponse is required" });
            }

            logger.LogInformation("Processing SAML assertion");

            // Validate SAML response
            var assertionResult = await samlService.ValidateResponseAsync(
                samlResponse,
                relayState,
                cancellationToken);

            if (!assertionResult.IsValid)
            {
                logger.LogWarning("SAML assertion validation failed: {Errors}",
                    string.Join(", ", assertionResult.Errors));

                return Results.Unauthorized();
            }

            logger.LogInformation("SAML assertion validated successfully for NameID {NameId}",
                assertionResult.NameId);

            // Get tenant from relay state or session
            var tenant = await tenantProvider.GetCurrentTenantAsync(context, cancellationToken);
            if (tenant == null)
            {
                logger.LogWarning("Cannot determine tenant for SAML user {NameId}", assertionResult.NameId);
                return Results.BadRequest(new { error = "Tenant not found" });
            }

            // Get IdP configuration (we need this for provisioning)
            // In production, this should be retrieved from the session store
            var idpConfig = await samlService.ValidateResponseAsync(samlResponse, relayState, cancellationToken);
            // This is simplified - in reality we'd get the IdP config from session

            // Parse tenant ID to Guid for provisioning
            if (!Guid.TryParse(tenant.TenantId, out var tenantGuid))
            {
                logger.LogError("Invalid tenant ID format: {TenantId}", tenant.TenantId);
                return Results.BadRequest(new { error = "Invalid tenant ID format" });
            }

            // Provision user (JIT or existing)
            // Use session ID from user context for tracking the SAML session
            var provisionedUser = await provisioningService.ProvisionUserAsync(
                tenantGuid,
                userContext.SessionId,
                assertionResult,
                cancellationToken);

            logger.LogInformation(
                "User {UserId} provisioned successfully (IsNew: {IsNewUser})",
                provisionedUser.UserId,
                provisionedUser.IsNewUser);

            // Create authentication claims
            var claims = new[]
            {
                new System.Security.Claims.Claim(ClaimTypes.NameIdentifier, provisionedUser.UserId.ToString()),
                new System.Security.Claims.Claim(ClaimTypes.Email, provisionedUser.Email),
                new System.Security.Claims.Claim(ClaimTypes.Name, provisionedUser.DisplayName),
                new System.Security.Claims.Claim(ClaimTypes.Role, provisionedUser.Role),
                new System.Security.Claims.Claim("tenant_id", tenant.TenantId),
                new System.Security.Claims.Claim("saml_name_id", assertionResult.NameId!),
                new System.Security.Claims.Claim("auth_method", "saml")
            };

            var identity = new ClaimsIdentity(claims, "SAML");
            var principal = new ClaimsPrincipal(identity);

            // Sign in the user
            await context.SignInAsync("Cookies", principal);

            logger.LogInformation("User {UserId} signed in via SAML SSO", provisionedUser.UserId);

            // Redirect to return URL or default page
            var returnUrl = relayState ?? "/";
            return Results.Redirect(returnUrl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing SAML assertion");
            return Results.Problem(
                title: "SAML Authentication Error",
                detail: "Failed to process SAML assertion. Please contact your administrator.",
                statusCode: 500);
        }
    }

    private static async Task<IResult> GetMetadataAsync(
        HttpContext context,
        [FromServices] ISamlService samlService,
        [FromServices] ITenantProvider tenantProvider,
        [FromServices] ILogger<SamlService> logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get current tenant (optional for metadata)
            var tenant = await tenantProvider.GetCurrentTenantAsync(context, cancellationToken);

            logger.LogInformation("Generating SP metadata for tenant {TenantId}", tenant?.TenantId);

            // Parse tenant ID to Guid if present
            Guid? tenantGuid = null;
            if (tenant != null && Guid.TryParse(tenant.TenantId, out var parsedGuid))
            {
                tenantGuid = parsedGuid;
            }

            // Generate metadata
            var metadata = await samlService.GenerateServiceProviderMetadataAsync(
                tenantGuid,
                cancellationToken);

            return Results.Content(metadata, "application/samlmetadata+xml");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating SP metadata");
            return Results.Problem(
                title: "Metadata Generation Error",
                detail: "Failed to generate service provider metadata.",
                statusCode: 500);
        }
    }

    private static string GenerateAutoSubmitForm(string action, string samlRequest, string? relayState)
    {
        var relayStateInput = !string.IsNullOrEmpty(relayState)
            ? $"<input type=\"hidden\" name=\"RelayState\" value=\"{relayState}\" />"
            : string.Empty;

        return $@"
<!DOCTYPE html>
<html>
<head>
    <title>SAML SSO</title>
</head>
<body onload=""document.forms[0].submit()"">
    <noscript>
        <p><strong>Note:</strong> Since your browser does not support JavaScript, you must press the button below to proceed.</p>
    </noscript>
    <form method=""post"" action=""{action}"">
        <input type=""hidden"" name=""SAMLRequest"" value=""{samlRequest}"" />
        {relayStateInput}
        <noscript>
            <button type=""submit"">Continue</button>
        </noscript>
    </form>
</body>
</html>";
    }
}
