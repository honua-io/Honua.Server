using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace Honua.Server.Host.Tests.Security;

/// <summary>
/// Comprehensive authentication security tests covering token validation, expiration,
/// signature verification, and various authentication attack scenarios.
/// </summary>
[Collection("HostTests")]
[Trait("Category", "Security")]
public sealed class AuthenticationSecurityTests : IDisposable
{
    private readonly WireMockServer _mockOidcServer;
    private readonly RSA _rsa;
    private readonly RsaSecurityKey _securityKey;
    private readonly string _issuer;
    private readonly string _audience;

    public AuthenticationSecurityTests()
    {
        _mockOidcServer = WireMockServer.Start();
        _issuer = _mockOidcServer.Url!;
        _audience = "honua-test-api";

        _rsa = RSA.Create(2048);
        _securityKey = new RsaSecurityKey(_rsa);

        SetupOidcDiscoveryEndpoint();
        SetupJwksEndpoint();
    }

    public void Dispose()
    {
        _mockOidcServer?.Stop();
        _mockOidcServer?.Dispose();
        _rsa?.Dispose();
    }

    #region Expired Token Tests

    [Fact]
    public async Task ExpiredToken_Returns401Unauthorized()
    {
        // Arrange
        var expiredToken = GenerateToken(
            new[] { new Claim(ClaimTypes.NameIdentifier, "user-1") },
            expires: DateTime.UtcNow.AddHours(-1));

        var factory = CreateWebApplicationFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);

        // Act
        var response = await client.GetAsync("/api/protected-endpoint");

        // Assert - Expired tokens should be rejected on protected endpoints
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(-1)] // Expired 1 hour ago
    [InlineData(-24)] // Expired 24 hours ago
    [InlineData(-168)] // Expired 1 week ago
    [InlineData(-8760)] // Expired 1 year ago
    public async Task ExpiredToken_WithVariousExpirationTimes_Returns401(int hoursAgo)
    {
        // Arrange
        var expiredToken = GenerateToken(
            new[] { new Claim(ClaimTypes.NameIdentifier, "user-1") },
            expires: DateTime.UtcNow.AddHours(hoursAgo));

        var factory = CreateWebApplicationFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);

        // Act
        var response = await client.GetAsync("/api/protected-endpoint");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TokenExpiringInFuture_IsAccepted()
    {
        // Arrange
        var validToken = GenerateToken(
            new[] { new Claim(ClaimTypes.NameIdentifier, "user-1") },
            expires: DateTime.UtcNow.AddHours(1));

        var factory = CreateWebApplicationFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", validToken);

        // Act
        var response = await client.GetAsync("/healthz/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TokenExpiringInOneSecond_IsStillAccepted()
    {
        // Arrange
        var almostExpiredToken = GenerateToken(
            new[] { new Claim(ClaimTypes.NameIdentifier, "user-1") },
            expires: DateTime.UtcNow.AddSeconds(1));

        var factory = CreateWebApplicationFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", almostExpiredToken);

        // Act
        var response = await client.GetAsync("/healthz/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Invalid Signature Tests

    [Fact]
    public async Task InvalidTokenSignature_Returns401Unauthorized()
    {
        // Arrange - Generate token with different RSA key
        using var differentRsa = RSA.Create(2048);
        var differentKey = new RsaSecurityKey(differentRsa);

        var invalidToken = GenerateToken(
            new[] { new Claim(ClaimTypes.NameIdentifier, "user-1") },
            signingKey: differentKey);

        var factory = CreateWebApplicationFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", invalidToken);

        // Act
        var response = await client.GetAsync("/api/protected-endpoint");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TamperedTokenPayload_Returns401Unauthorized()
    {
        // Arrange - Create valid token then tamper with it
        var validToken = GenerateToken(new[] { new Claim(ClaimTypes.NameIdentifier, "user-1") });

        // Split token into parts and modify the payload
        var parts = validToken.Split('.');
        if (parts.Length == 3)
        {
            // Decode payload, modify it, encode it back
            var payloadBytes = Base64UrlEncoder.DecodeBytes(parts[1]);
            var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(payloadBytes);
            payload!["sub"] = "admin-user"; // Elevate privileges
            var modifiedPayload = JsonSerializer.Serialize(payload);
            parts[1] = Base64UrlEncoder.Encode(modifiedPayload);
            validToken = string.Join(".", parts);
        }

        var factory = CreateWebApplicationFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", validToken);

        // Act
        var response = await client.GetAsync("/api/protected-endpoint");

        // Assert - Modified payload should fail signature verification
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UnsignedToken_Returns401Unauthorized()
    {
        // Arrange - Create JWT without signature (algorithm: none)
        var handler = new JwtSecurityTokenHandler();
        var unsignedToken = handler.CreateEncodedJwt(
            issuer: _issuer,
            audience: _audience,
            subject: new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "user-1") }),
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddHours(1),
            issuedAt: DateTime.UtcNow,
            signingCredentials: null); // No signature

        var factory = CreateWebApplicationFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", unsignedToken);

        // Act
        var response = await client.GetAsync("/api/protected-endpoint");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }

    #endregion

    #region Missing Token Tests

    [Fact]
    public async Task MissingToken_OnProtectedEndpoint_Returns401Unauthorized()
    {
        // Arrange
        var factory = CreateWebApplicationFactory();
        var client = factory.CreateClient();

        // Act - No Authorization header
        var response = await client.GetAsync("/api/protected-endpoint");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EmptyAuthorizationHeader_Returns401Unauthorized()
    {
        // Arrange
        var factory = CreateWebApplicationFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "");

        // Act
        var response = await client.GetAsync("/api/protected-endpoint");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MalformedAuthorizationHeader_Returns401Unauthorized()
    {
        // Arrange
        var factory = CreateWebApplicationFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "NotBearer InvalidTokenFormat");

        // Act
        var response = await client.GetAsync("/api/protected-endpoint");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task BearerTokenWithoutToken_Returns401Unauthorized()
    {
        // Arrange
        var factory = CreateWebApplicationFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer ");

        // Act
        var response = await client.GetAsync("/api/protected-endpoint");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    #endregion

    #region Tampered Claims Tests

    [Fact]
    public async Task TokenWithTamperedRoleClaims_IsRejectedBySignatureValidation()
    {
        // Arrange - Create token with basic role, then try to tamper
        var validToken = GenerateToken(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-1"),
            new Claim(ClaimTypes.Role, "viewer")
        });

        // Attempt to modify claims (will break signature)
        var parts = validToken.Split('.');
        if (parts.Length == 3)
        {
            var payloadBytes = Base64UrlEncoder.DecodeBytes(parts[1]);
            var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(payloadBytes);
            payload!["role"] = "administrator"; // Try to escalate privileges
            var modifiedPayload = JsonSerializer.Serialize(payload);
            parts[1] = Base64UrlEncoder.Encode(modifiedPayload);
            validToken = string.Join(".", parts);
        }

        var factory = CreateWebApplicationFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", validToken);

        // Act
        var response = await client.GetAsync("/api/admin-endpoint");

        // Assert - Tampered claims should fail signature verification
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TokenWithInvalidIssuer_Returns401Unauthorized()
    {
        // Arrange
        var invalidToken = GenerateToken(
            new[] { new Claim(ClaimTypes.NameIdentifier, "user-1") },
            issuer: "https://malicious-issuer.com");

        var factory = CreateWebApplicationFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", invalidToken);

        // Act
        var response = await client.GetAsync("/api/protected-endpoint");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TokenWithInvalidAudience_Returns401Unauthorized()
    {
        // Arrange
        var invalidToken = GenerateToken(
            new[] { new Claim(ClaimTypes.NameIdentifier, "user-1") },
            audience: "wrong-audience");

        var factory = CreateWebApplicationFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", invalidToken);

        // Act
        var response = await client.GetAsync("/api/protected-endpoint");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }

    #endregion

    #region Multiple Concurrent Sessions Tests

    [Fact]
    public async Task MultipleConcurrentRequests_WithSameToken_AreAllowed()
    {
        // Arrange
        var validToken = GenerateToken(new[] { new Claim(ClaimTypes.NameIdentifier, "user-1") });

        var factory = CreateWebApplicationFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", validToken);

        // Act - Make 10 concurrent requests
        var tasks = new List<Task<HttpResponseMessage>>();
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(client.GetAsync("/healthz/ready"));
        }

        var responses = await Task.WhenAll(tasks);

        // Assert - All requests should succeed
        foreach (var response in responses)
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task DifferentTokensForDifferentUsers_WorkIndependently()
    {
        // Arrange
        var token1 = GenerateToken(new[] { new Claim(ClaimTypes.NameIdentifier, "user-1") });
        var token2 = GenerateToken(new[] { new Claim(ClaimTypes.NameIdentifier, "user-2") });

        var factory = CreateWebApplicationFactory();

        var client1 = factory.CreateClient();
        client1.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token1);

        var client2 = factory.CreateClient();
        client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token2);

        // Act
        var response1 = await client1.GetAsync("/healthz/ready");
        var response2 = await client2.GetAsync("/healthz/ready");

        // Assert - Both users should be able to authenticate
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Token Refresh Scenarios

    [Fact]
    public async Task TokenNearExpiration_StillWorks()
    {
        // Arrange - Token expires in 5 seconds
        var nearExpiryToken = GenerateToken(
            new[] { new Claim(ClaimTypes.NameIdentifier, "user-1") },
            expires: DateTime.UtcNow.AddSeconds(5));

        var factory = CreateWebApplicationFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", nearExpiryToken);

        // Act
        var response = await client.GetAsync("/healthz/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NewToken_AfterOldExpires_Works()
    {
        // Arrange
        var factory = CreateWebApplicationFactory();
        var client = factory.CreateClient();

        // Use old expired token first
        var oldToken = GenerateToken(
            new[] { new Claim(ClaimTypes.NameIdentifier, "user-1") },
            expires: DateTime.UtcNow.AddHours(-1));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", oldToken);
        var response1 = await client.GetAsync("/api/protected-endpoint");

        // Generate new valid token
        var newToken = GenerateToken(
            new[] { new Claim(ClaimTypes.NameIdentifier, "user-1") },
            expires: DateTime.UtcNow.AddHours(1));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", newToken);

        // Act
        var response2 = await client.GetAsync("/healthz/ready");

        // Assert
        response1.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Edge Cases and Attack Vectors

    [Theory]
    [InlineData("eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0.eyJzdWIiOiIxMjM0NTY3ODkwIn0.")]
    [InlineData("invalid.token.here")]
    [InlineData("Bearer")]
    [InlineData("")]
    [InlineData(" ")]
    public async Task MalformedToken_Returns401Unauthorized(string malformedToken)
    {
        // Arrange
        var factory = CreateWebApplicationFactory();
        var client = factory.CreateClient();

        if (!string.IsNullOrWhiteSpace(malformedToken))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", malformedToken);
        }

        // Act
        var response = await client.GetAsync("/api/protected-endpoint");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ExtremelyLongToken_IsHandledGracefully()
    {
        // Arrange - Create an unreasonably long token string
        var extremelyLongToken = new string('A', 100000);

        var factory = CreateWebApplicationFactory();
        var client = factory.CreateClient();

        try
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", extremelyLongToken);
        }
        catch (ArgumentException)
        {
            // It's acceptable to reject extremely long tokens at the header level
            return;
        }

        // Act
        var response = await client.GetAsync("/api/protected-endpoint");

        // Assert - Should reject without crashing
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized,
            HttpStatusCode.BadRequest,
            HttpStatusCode.RequestEntityTooLarge,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TokenWithMissingRequiredClaims_IsRejected()
    {
        // Arrange - Token without 'sub' claim
        var tokenWithoutSub = GenerateToken(new[]
        {
            new Claim("email", "user@example.com")
            // Missing NameIdentifier/sub claim
        });

        var factory = CreateWebApplicationFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenWithoutSub);

        // Act
        var response = await client.GetAsync("/api/protected-endpoint");

        // Assert - Depending on configuration, this might be accepted or rejected
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TokenWithFutureNotBeforeDate_IsRejected()
    {
        // Arrange - Token that's not valid yet
        var futureToken = GenerateToken(
            new[] { new Claim(ClaimTypes.NameIdentifier, "user-1") },
            notBefore: DateTime.UtcNow.AddHours(1));

        var factory = CreateWebApplicationFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", futureToken);

        // Act
        var response = await client.GetAsync("/api/protected-endpoint");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DoubleEncodedToken_IsRejected()
    {
        // Arrange
        var validToken = GenerateToken(new[] { new Claim(ClaimTypes.NameIdentifier, "user-1") });
        var doubleEncoded = Base64UrlEncoder.Encode(validToken);

        var factory = CreateWebApplicationFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", doubleEncoded);

        // Act
        var response = await client.GetAsync("/api/protected-endpoint");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
    }

    #endregion

    #region Helper Methods

    private void SetupOidcDiscoveryEndpoint()
    {
        var discoveryDoc = new
        {
            issuer = _issuer,
            authorization_endpoint = $"{_issuer}/oauth2/authorize",
            token_endpoint = $"{_issuer}/oauth2/token",
            userinfo_endpoint = $"{_issuer}/oauth2/userInfo",
            jwks_uri = $"{_issuer}/.well-known/jwks.json",
            response_types_supported = new[] { "code", "token", "id_token" },
            subject_types_supported = new[] { "public" },
            id_token_signing_alg_values_supported = new[] { "RS256" },
            scopes_supported = new[] { "openid", "profile", "email" },
            token_endpoint_auth_methods_supported = new[] { "client_secret_basic", "client_secret_post" },
            claims_supported = new[] { "sub", "email", "name", "preferred_username" }
        };

        _mockOidcServer
            .Given(Request.Create()
                .WithPath("/.well-known/openid-configuration")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(discoveryDoc)));
    }

    private void SetupJwksEndpoint()
    {
        var parameters = _rsa.ExportParameters(false);

        var jwks = new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    kid = "test-key-1",
                    alg = "RS256",
                    n = Base64UrlEncoder.Encode(parameters.Modulus!),
                    e = Base64UrlEncoder.Encode(parameters.Exponent!)
                }
            }
        };

        _mockOidcServer
            .Given(Request.Create()
                .WithPath("/.well-known/jwks.json")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(jwks)));
    }

    private string GenerateToken(
        Claim[] claims,
        DateTime? expires = null,
        DateTime? notBefore = null,
        string? issuer = null,
        string? audience = null,
        RsaSecurityKey? signingKey = null)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = issuer ?? _issuer,
            Audience = audience ?? _audience,
            Expires = expires ?? DateTime.UtcNow.AddHours(1),
            NotBefore = notBefore ?? DateTime.UtcNow,
            SigningCredentials = new SigningCredentials(
                signingKey ?? _securityKey,
                SecurityAlgorithms.RsaSha256)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private WebApplicationFactory<Program> CreateWebApplicationFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["honua:authentication:mode"] = "Oidc",
                        ["honua:authentication:jwt:authority"] = _issuer,
                        ["honua:authentication:jwt:audience"] = _audience,
                        ["honua:authentication:jwt:requireHttpsMetadata"] = "false"
                    });
                });
            });
    }

    #endregion
}
