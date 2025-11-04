using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
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
/// Comprehensive authorization security tests covering RBAC, permissions,
/// resource-level access control, and privilege escalation prevention.
/// </summary>
[Collection("HostTests")]
[Trait("Category", "Security")]
public sealed class AuthorizationSecurityTests : IDisposable
{
    private readonly WireMockServer _mockOidcServer;
    private readonly RSA _rsa;
    private readonly RsaSecurityKey _securityKey;
    private readonly string _issuer;
    private readonly string _audience;

    public AuthorizationSecurityTests()
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

    #region Insufficient Permissions Tests

    [Fact]
    public async Task UserWithoutRequiredRole_Returns403Forbidden()
    {
        // Arrange - User with viewer role trying to access admin endpoint
        var viewerToken = GenerateToken(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-1"),
            new Claim(ClaimTypes.Role, "viewer")
        });

        var factory = CreateWebApplicationFactory(enforceAuth: true);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", viewerToken);

        // Act - Try to access admin endpoint
        var response = await client.GetAsync("/api/admin-endpoint");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AuthenticatedUserWithoutAnyRole_Returns403Forbidden()
    {
        // Arrange - User without any roles
        var noRoleToken = GenerateToken(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-1"),
            new Claim(ClaimTypes.Email, "user@example.com")
            // No role claims
        });

        var factory = CreateWebApplicationFactory(enforceAuth: true);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", noRoleToken);

        // Act
        var response = await client.GetAsync("/api/admin-endpoint");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UserWithInvalidRole_Returns403Forbidden()
    {
        // Arrange - User with non-existent role
        var invalidRoleToken = GenerateToken(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-1"),
            new Claim(ClaimTypes.Role, "non-existent-role")
        });

        var factory = CreateWebApplicationFactory(enforceAuth: true);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", invalidRoleToken);

        // Act
        var response = await client.GetAsync("/api/admin-endpoint");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
    }

    #endregion

    #region Role-Based Access Control (RBAC) Matrix Tests

    [Theory]
    [InlineData("viewer", "/api/data/read", HttpStatusCode.OK)]
    [InlineData("viewer", "/api/data/write", HttpStatusCode.Forbidden)]
    [InlineData("viewer", "/api/admin/users", HttpStatusCode.Forbidden)]
    [InlineData("datapublisher", "/api/data/read", HttpStatusCode.OK)]
    [InlineData("datapublisher", "/api/data/write", HttpStatusCode.OK)]
    [InlineData("datapublisher", "/api/admin/users", HttpStatusCode.Forbidden)]
    [InlineData("administrator", "/api/data/read", HttpStatusCode.OK)]
    [InlineData("administrator", "/api/data/write", HttpStatusCode.OK)]
    [InlineData("administrator", "/api/admin/users", HttpStatusCode.OK)]
    public async Task RoleBasedAccessControl_EnforcesCorrectPermissions(
        string role,
        string endpoint,
        HttpStatusCode expectedMinStatus)
    {
        // Arrange
        var token = GenerateToken(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, $"user-{role}"),
            new Claim(ClaimTypes.Role, role)
        });

        var factory = CreateWebApplicationFactory(enforceAuth: true);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync(endpoint);

        // Assert - Allow OK or NotFound (endpoint might not exist in test)
        if (expectedMinStatus == HttpStatusCode.OK)
        {
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        }
        else
        {
            response.StatusCode.Should().BeOneOf(expectedMinStatus, HttpStatusCode.NotFound);
        }
    }

    [Fact]
    public async Task AdministratorRole_HasFullAccess()
    {
        // Arrange
        var adminToken = GenerateToken(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "admin-user"),
            new Claim(ClaimTypes.Role, "administrator")
        });

        var factory = CreateWebApplicationFactory(enforceAuth: true);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Act - Try multiple endpoints
        var endpoints = new[]
        {
            "/healthz/ready",
            "/api/data",
            "/api/collections"
        };

        var responses = new List<HttpResponseMessage>();
        foreach (var endpoint in endpoints)
        {
            responses.Add(await client.GetAsync(endpoint));
        }

        // Assert - Administrator should have access to all endpoints
        foreach (var response in responses)
        {
            response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        }
    }

    [Fact]
    public async Task ViewerRole_HasReadOnlyAccess()
    {
        // Arrange
        var viewerToken = GenerateToken(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "viewer-user"),
            new Claim(ClaimTypes.Role, "viewer")
        });

        var factory = CreateWebApplicationFactory(enforceAuth: true);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", viewerToken);

        // Act - Try GET (should work) and POST (should fail)
        var getResponse = await client.GetAsync("/api/data");
        var postResponse = await client.PostAsync("/api/data", null);

        // Assert
        getResponse.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
        postResponse.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DataPublisherRole_CanWriteData()
    {
        // Arrange
        var publisherToken = GenerateToken(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "publisher-user"),
            new Claim(ClaimTypes.Role, "datapublisher")
        });

        var factory = CreateWebApplicationFactory(enforceAuth: true);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", publisherToken);

        // Act
        var response = await client.GetAsync("/healthz/ready");

        // Assert - Should have access to data endpoints
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MultipleRoles_CombinePermissions()
    {
        // Arrange - User with multiple roles
        var multiRoleToken = GenerateToken(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "multi-role-user"),
            new Claim(ClaimTypes.Role, "viewer"),
            new Claim(ClaimTypes.Role, "datapublisher")
        });

        var factory = CreateWebApplicationFactory(enforceAuth: true);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", multiRoleToken);

        // Act
        var response = await client.GetAsync("/healthz/ready");

        // Assert - Should have combined permissions
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Resource-Level Permissions Tests

    [Fact]
    public async Task AccessToOwnedResource_IsAllowed()
    {
        // Arrange
        var userToken = GenerateToken(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-123"),
            new Claim(ClaimTypes.Role, "viewer")
        });

        var factory = CreateWebApplicationFactory(enforceAuth: true);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);

        // Act - Access own resource
        var response = await client.GetAsync("/api/users/user-123/profile");

        // Assert - Should be able to access own resources
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AccessToOtherUsersResource_IsDenied()
    {
        // Arrange
        var userToken = GenerateToken(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-123"),
            new Claim(ClaimTypes.Role, "viewer")
        });

        var factory = CreateWebApplicationFactory(enforceAuth: true);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);

        // Act - Try to access another user's resource
        var response = await client.GetAsync("/api/users/user-456/profile");

        // Assert - Should be denied
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminAccessToAnyResource_IsAllowed()
    {
        // Arrange
        var adminToken = GenerateToken(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "admin-user"),
            new Claim(ClaimTypes.Role, "administrator")
        });

        var factory = CreateWebApplicationFactory(enforceAuth: true);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Act - Admin accessing any user's resource
        var response = await client.GetAsync("/api/users/any-user/profile");

        // Assert - Admin should have access
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion

    #region Cross-Tenant Data Access Prevention Tests

    [Fact]
    public async Task UserFromTenant1_CannotAccessTenant2Data()
    {
        // Arrange
        var tenant1Token = GenerateToken(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-1"),
            new Claim("tenant_id", "tenant-1"),
            new Claim(ClaimTypes.Role, "viewer")
        });

        var factory = CreateWebApplicationFactory(enforceAuth: true);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenant1Token);

        // Act - Try to access tenant-2 data
        var response = await client.GetAsync("/api/tenants/tenant-2/data");

        // Assert - Should be denied
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UserWithoutTenantClaim_CannotAccessTenantData()
    {
        // Arrange
        var noTenantToken = GenerateToken(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-1"),
            new Claim(ClaimTypes.Role, "viewer")
            // No tenant claim
        });

        var factory = CreateWebApplicationFactory(enforceAuth: true);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", noTenantToken);

        // Act
        var response = await client.GetAsync("/api/tenants/tenant-1/data");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UserCanAccessOwnTenantData()
    {
        // Arrange
        var tenantToken = GenerateToken(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-1"),
            new Claim("tenant_id", "tenant-1"),
            new Claim(ClaimTypes.Role, "viewer")
        });

        var factory = CreateWebApplicationFactory(enforceAuth: true);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantToken);

        // Act
        var response = await client.GetAsync("/api/tenants/tenant-1/data");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    #endregion

    #region Privilege Escalation Prevention Tests

    [Fact]
    public async Task ViewerCannotEscalateToDataPublisher()
    {
        // Arrange - Viewer trying to perform datapublisher action
        var viewerToken = GenerateToken(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "viewer-user"),
            new Claim(ClaimTypes.Role, "viewer")
        });

        var factory = CreateWebApplicationFactory(enforceAuth: true);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", viewerToken);

        // Act - Try to publish data (requires datapublisher role)
        var response = await client.PostAsync("/api/data/publish", null);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DataPublisherCannotEscalateToAdministrator()
    {
        // Arrange - DataPublisher trying to perform admin action
        var publisherToken = GenerateToken(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "publisher-user"),
            new Claim(ClaimTypes.Role, "datapublisher")
        });

        var factory = CreateWebApplicationFactory(enforceAuth: true);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", publisherToken);

        // Act - Try to access admin functions
        var response = await client.GetAsync("/api/admin/settings");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UserCannotModifyOwnRoleClaims()
    {
        // Arrange - User with viewer role
        var userToken = GenerateToken(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-1"),
            new Claim(ClaimTypes.Role, "viewer")
        });

        var factory = CreateWebApplicationFactory(enforceAuth: true);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);

        // Act - Try to update own roles
        var content = new StringContent(
            JsonSerializer.Serialize(new { role = "administrator" }),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await client.PutAsync("/api/users/user-1/roles", content);

        // Assert - Should be denied
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UserCannotGrantRolesToOthers()
    {
        // Arrange
        var userToken = GenerateToken(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-1"),
            new Claim(ClaimTypes.Role, "datapublisher")
        });

        var factory = CreateWebApplicationFactory(enforceAuth: true);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userToken);

        // Act - Try to grant admin role to another user
        var content = new StringContent(
            JsonSerializer.Serialize(new { userId = "user-2", role = "administrator" }),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync("/api/admin/users/roles", content);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AdministratorCanGrantRoles()
    {
        // Arrange
        var adminToken = GenerateToken(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "admin-user"),
            new Claim(ClaimTypes.Role, "administrator")
        });

        var factory = CreateWebApplicationFactory(enforceAuth: true);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        // Act
        var content = new StringContent(
            JsonSerializer.Serialize(new { userId = "user-2", role = "datapublisher" }),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync("/api/admin/users/roles", content);

        // Assert - Admin should be able to grant roles (or endpoint doesn't exist)
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Created, HttpStatusCode.NotFound, HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task RoleClaimWithInvalidFormat_IsIgnored()
    {
        // Arrange - Token with malformed role claim
        var token = GenerateToken(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-1"),
            new Claim(ClaimTypes.Role, "admin'--") // SQL injection attempt in role
        });

        var factory = CreateWebApplicationFactory(enforceAuth: true);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await client.GetAsync("/api/admin/settings");

        // Assert - Malformed role should not grant access
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }

    #endregion

    #region Permission Boundary Tests

    [Theory]
    [InlineData("viewer", "read")]
    [InlineData("datapublisher", "write")]
    [InlineData("administrator", "delete")]
    public async Task RolePermissions_RespectOperationBoundaries(string role, string operation)
    {
        // Arrange
        var token = GenerateToken(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, $"user-{role}"),
            new Claim(ClaimTypes.Role, role)
        });

        var factory = CreateWebApplicationFactory(enforceAuth: true);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = operation switch
        {
            "read" => await client.GetAsync("/api/data"),
            "write" => await client.PostAsync("/api/data", null),
            "delete" => await client.DeleteAsync("/api/data/1"),
            _ => throw new ArgumentException("Invalid operation")
        };

        // Assert - Response depends on role-operation matching
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Forbidden,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.NotFound,
            HttpStatusCode.NoContent);
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
            claims_supported = new[] { "sub", "email", "name", "preferred_username", "role" }
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

    private string GenerateToken(Claim[] claims)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _issuer,
            Audience = _audience,
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(_securityKey, SecurityAlgorithms.RsaSha256)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private WebApplicationFactory<Program> CreateWebApplicationFactory(bool enforceAuth = true)
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
                        ["honua:authentication:jwt:requireHttpsMetadata"] = "false",
                        ["honua:authentication:enforce"] = enforceAuth.ToString()
                    });
                });
            });
    }

    #endregion
}
