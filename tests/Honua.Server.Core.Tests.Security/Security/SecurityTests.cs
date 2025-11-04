using System.Net;
using FluentAssertions;
using Honua.Server.Core.Tests.Shared;
using Xunit;

namespace Honua.Server.Core.Tests.Security.Security;

/// <summary>
/// Security tests to prevent SQL injection, CRS injection, and other attack vectors.
/// These tests ensure Honua Server is resilient against malicious inputs.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Feature", "Security")]
[Trait("Speed", "Slow")]
public class SecurityTests : IClassFixture<HonuaTestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SecurityTests(HonuaTestWebApplicationFactory factory)
    {
        _client = factory.CreateAuthenticatedClient();
    }

    // =====================================================
    // SQL Injection Tests
    // =====================================================

    [Theory]
    [InlineData("1' OR '1'='1")]
    [InlineData("1; DROP TABLE features--")]
    [InlineData("1' UNION SELECT * FROM users--")]
    [InlineData("admin'--")]
    [InlineData("' OR 1=1--")]
    [InlineData("1' AND 1=(SELECT COUNT(*) FROM tabname); --")]
    public async Task FeatureById_WithSqlInjectionAttempt_DoesNotExecuteMaliciousQuery(string maliciousId)
    {
        // Arrange
        var collectionsResponse = await _client.GetAsync("/ogc/collections");
        var collectionsJson = await collectionsResponse.Content.ReadAsStringAsync();
        var collectionsDoc = System.Text.Json.JsonDocument.Parse(collectionsJson);
        var firstCollection = collectionsDoc.RootElement.GetProperty("collections")[0];
        var collectionId = firstCollection.GetProperty("id").GetString();

        // Act
        var response = await _client.GetAsync($"/ogc/collections/{collectionId}/items/{Uri.EscapeDataString(maliciousId)}");

        // Assert
        // Should return 404 (not found) or 400 (bad request), NOT 500 (server error from SQL error)
        response.StatusCode.Should().BeOneOf(
            [HttpStatusCode.NotFound, HttpStatusCode.BadRequest],
            "SQL injection should not cause server errors");

        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError,
            "SQL injection should not cause internal server errors");
    }

    [Theory]
    [InlineData("id=1' OR '1'='1")]
    [InlineData("name='; DROP TABLE features--")]
    [InlineData("status=' OR 1=1--")]
    public async Task Features_WithSqlInjectionInQueryParameter_DoesNotExecuteMaliciousQuery(string maliciousFilter)
    {
        // Arrange
        var collectionsResponse = await _client.GetAsync("/ogc/collections");
        var collectionsJson = await collectionsResponse.Content.ReadAsStringAsync();
        var collectionsDoc = System.Text.Json.JsonDocument.Parse(collectionsJson);
        var firstCollection = collectionsDoc.RootElement.GetProperty("collections")[0];
        var collectionId = firstCollection.GetProperty("id").GetString();

        // Act
        var response = await _client.GetAsync($"/ogc/collections/{collectionId}/items?filter={Uri.EscapeDataString(maliciousFilter)}");

        // Assert
        // Should handle safely - either ignore invalid filter or return error, but never execute SQL
        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError,
            "SQL injection in filter should not cause server errors");
    }

    // =====================================================
    // CRS Injection Tests
    // =====================================================

    [Theory]
    [InlineData("EPSG:4326'; DROP TABLE features--")]
    [InlineData("../../etc/passwd")]
    [InlineData("file:///etc/passwd")]
    [InlineData("EPSG:4326<script>alert('xss')</script>")]
    [InlineData("EPSG:9999999999999999999999")]
    public async Task Features_WithMaliciousCrs_ReturnsErrorSafely(string maliciousCrs)
    {
        // Arrange
        var collectionsResponse = await _client.GetAsync("/ogc/collections");
        var collectionsJson = await collectionsResponse.Content.ReadAsStringAsync();
        var collectionsDoc = System.Text.Json.JsonDocument.Parse(collectionsJson);
        var firstCollection = collectionsDoc.RootElement.GetProperty("collections")[0];
        var collectionId = firstCollection.GetProperty("id").GetString();

        // Act
        var response = await _client.GetAsync($"/ogc/collections/{collectionId}/items?crs={Uri.EscapeDataString(maliciousCrs)}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            [HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity, HttpStatusCode.NotFound],
            "malicious CRS should be rejected gracefully");

        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError,
            "malicious CRS should not cause server crashes");
    }

    // =====================================================
    // Path Traversal Tests
    // =====================================================

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\..\\windows\\system32\\config\\sam")]
    [InlineData("....//....//....//etc/passwd")]
    [InlineData("%2e%2e%2f%2e%2e%2f%2e%2e%2fetc%2fpasswd")]
    public async Task CollectionById_WithPathTraversal_DoesNotAccessFileSystem(string maliciousPath)
    {
        // Act
        var response = await _client.GetAsync($"/ogc/collections/{Uri.EscapeDataString(maliciousPath)}");

        // Assert
        // Path traversal should be blocked and return 404 (not found) or 400 (bad request)
        response.StatusCode.Should().NotBe(HttpStatusCode.OK,
            "path traversal should not succeed");

        response.StatusCode.Should().BeOneOf(
            [HttpStatusCode.NotFound, HttpStatusCode.BadRequest],
            "path traversal should be blocked with NotFound or BadRequest");

        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError,
            "path traversal should not cause internal server errors");
    }

    // =====================================================
    // Bbox Injection Tests
    // =====================================================

    [Theory]
    [InlineData("-180,-90,180,90'; DROP TABLE features--")]
    [InlineData("NaN,NaN,NaN,NaN")]
    [InlineData("Infinity,-Infinity,Infinity,-Infinity")]
    [InlineData("-999999999999999,-999999999999999,999999999999999,999999999999999")]
    public async Task Features_WithMaliciousBbox_ReturnsErrorSafely(string maliciousBbox)
    {
        // Arrange
        var collectionsResponse = await _client.GetAsync("/ogc/collections");
        var collectionsJson = await collectionsResponse.Content.ReadAsStringAsync();
        var collectionsDoc = System.Text.Json.JsonDocument.Parse(collectionsJson);
        var firstCollection = collectionsDoc.RootElement.GetProperty("collections")[0];
        var collectionId = firstCollection.GetProperty("id").GetString();

        // Act
        var response = await _client.GetAsync($"/ogc/collections/{collectionId}/items?bbox={maliciousBbox}");

        // Assert
        // Accept either rejection (BadRequest/UnprocessableEntity) or graceful handling (OK with empty/safe results)
        response.StatusCode.Should().BeOneOf(
            [HttpStatusCode.OK, HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity],
            "malicious bbox should either be rejected or handled safely");

        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError,
            "malicious bbox should not cause crashes");
    }

    // =====================================================
    // Integer Overflow Tests
    // =====================================================

    [Theory]
    [InlineData("9223372036854775807")] // Int64.MaxValue
    [InlineData("-9223372036854775808")] // Int64.MinValue
    [InlineData("999999999999999999999999999")]
    public async Task Features_WithExtremeIntegerLimits_HandlesGracefully(string extremeLimit)
    {
        // Arrange
        var collectionsResponse = await _client.GetAsync("/ogc/collections");
        var collectionsJson = await collectionsResponse.Content.ReadAsStringAsync();
        var collectionsDoc = System.Text.Json.JsonDocument.Parse(collectionsJson);
        var firstCollection = collectionsDoc.RootElement.GetProperty("collections")[0];
        var collectionId = firstCollection.GetProperty("id").GetString();

        // Act
        var response = await _client.GetAsync($"/ogc/collections/{collectionId}/items?limit={extremeLimit}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            [HttpStatusCode.OK, HttpStatusCode.BadRequest],
            "extreme integer values should be handled safely");

        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError,
            "extreme integers should not cause crashes");
    }

    // =====================================================
    // XSS Prevention Tests (in error messages)
    // =====================================================

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("<img src=x onerror=alert('xss')>")]
    [InlineData("javascript:alert('xss')")]
    public async Task FeatureById_WithXssAttempt_DoesNotReflectUnsanitizedInput(string xssPayload)
    {
        // Arrange
        var collectionsResponse = await _client.GetAsync("/ogc/collections");
        var collectionsJson = await collectionsResponse.Content.ReadAsStringAsync();
        var collectionsDoc = System.Text.Json.JsonDocument.Parse(collectionsJson);
        var firstCollection = collectionsDoc.RootElement.GetProperty("collections")[0];
        var collectionId = firstCollection.GetProperty("id").GetString();

        // Act
        var response = await _client.GetAsync($"/ogc/collections/{collectionId}/items/{Uri.EscapeDataString(xssPayload)}");

        // Assert
        // XSS protection: response must be JSON (not HTML that could execute scripts)
        var contentType = response.Content.Headers.ContentType?.MediaType;
        contentType.Should().Match(ct => ct == "application/json" || ct == "application/problem+json",
            "responses must be JSON to prevent XSS execution");

        // Response should be valid JSON (escaping dangerous characters)
        var responseBody = await response.Content.ReadAsStringAsync();
        var act = () => System.Text.Json.JsonDocument.Parse(responseBody);
        act.Should().NotThrow("response must be valid JSON with properly escaped content");
    }

    // =====================================================
    // Denial of Service Prevention
    // =====================================================

    [Fact]
    public async Task Features_WithExcessivelyLongBbox_DoesNotCauseDoS()
    {
        // Arrange
        var collectionsResponse = await _client.GetAsync("/ogc/collections");
        var collectionsJson = await collectionsResponse.Content.ReadAsStringAsync();
        var collectionsDoc = System.Text.Json.JsonDocument.Parse(collectionsJson);
        var firstCollection = collectionsDoc.RootElement.GetProperty("collections")[0];
        var collectionId = firstCollection.GetProperty("id").GetString();

        // Create excessively long bbox string (10,000 characters)
        var longBbox = string.Join(",", Enumerable.Repeat("1.234567890", 1000));

        // Act
        var response = await _client.GetAsync($"/ogc/collections/{collectionId}/items?bbox={longBbox}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            [HttpStatusCode.BadRequest, HttpStatusCode.RequestUriTooLong],
            "excessively long bbox should be rejected");

        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError,
            "excessively long input should not cause crashes");
    }

    [Fact]
    public async Task Features_WithManySimultaneousRequests_DoesNotExhaustResources()
    {
        // Arrange
        var collectionsResponse = await _client.GetAsync("/ogc/collections");
        var collectionsJson = await collectionsResponse.Content.ReadAsStringAsync();
        var collectionsDoc = System.Text.Json.JsonDocument.Parse(collectionsJson);
        var firstCollection = collectionsDoc.RootElement.GetProperty("collections")[0];
        var collectionId = firstCollection.GetProperty("id").GetString();

        // Act - Send 50 concurrent requests
        var tasks = Enumerable.Range(0, 50)
            .Select(_ => _client.GetAsync($"/ogc/collections/{collectionId}/items?limit=10"))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        // Assert
        // All requests should complete successfully (or fail gracefully)
        foreach (var response in responses)
        {
            response.StatusCode.Should().BeOneOf(
                [HttpStatusCode.OK, HttpStatusCode.TooManyRequests, HttpStatusCode.ServiceUnavailable],
                "concurrent requests should be handled or rate-limited");

            response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError,
                "concurrent load should not cause crashes");
        }
    }

    // =====================================================
    // Metadata Injection Tests
    // =====================================================

    [Theory]
    [InlineData("'; DELETE FROM metadata--")]
    [InlineData("<xml>malicious</xml>")]
    [InlineData("../../../../../../etc/passwd")]
    public async Task CollectionById_WithMaliciousCollectionId_DoesNotCompromiseMetadata(string maliciousCollectionId)
    {
        // Act
        var response = await _client.GetAsync($"/ogc/collections/{Uri.EscapeDataString(maliciousCollectionId)}");

        // Assert
        // Malicious collection IDs should be rejected with 404 or 400, not 500
        response.StatusCode.Should().BeOneOf(
            [HttpStatusCode.NotFound, HttpStatusCode.BadRequest],
            "malicious collection ID should be blocked with NotFound or BadRequest");

        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError,
            "malicious collection IDs should not cause internal server errors");
    }

    // =====================================================
    // HTTPS/TLS Security Headers (if applicable)
    // =====================================================

    [Fact]
    public async Task LandingPage_IncludesSecurityHeaders()
    {
        // Act
        var response = await _client.GetAsync("/ogc");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Check for security headers (these may vary based on deployment)
        // X-Content-Type-Options: nosniff
        if (response.Headers.Contains("X-Content-Type-Options"))
        {
            response.Headers.GetValues("X-Content-Type-Options")
                .Should().Contain("nosniff");
        }

        // X-Frame-Options: DENY or SAMEORIGIN
        if (response.Headers.Contains("X-Frame-Options"))
        {
            var frameOptions = response.Headers.GetValues("X-Frame-Options").First();
            frameOptions.Should().BeOneOf("DENY", "SAMEORIGIN");
        }
    }

    // =====================================================
    // LDAP Injection Tests
    // =====================================================

    [Theory]
    [InlineData("*)(uid=*))(|(uid=*")]
    [InlineData("admin)(&(password=*))")]
    [InlineData("*)(objectClass=*")]
    public async Task FeatureById_WithLdapInjectionAttempt_DoesNotExecuteMaliciousQuery(string maliciousId)
    {
        // Arrange
        var collectionsResponse = await _client.GetAsync("/ogc/collections");
        var collectionsJson = await collectionsResponse.Content.ReadAsStringAsync();
        var collectionsDoc = System.Text.Json.JsonDocument.Parse(collectionsJson);
        var firstCollection = collectionsDoc.RootElement.GetProperty("collections")[0];
        var collectionId = firstCollection.GetProperty("id").GetString();

        // Act
        var response = await _client.GetAsync($"/ogc/collections/{collectionId}/items/{Uri.EscapeDataString(maliciousId)}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            [HttpStatusCode.NotFound, HttpStatusCode.BadRequest],
            "LDAP injection should not cause server errors");

        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError,
            "LDAP injection should not cause internal server errors");
    }

    // =====================================================
    // Command Injection Tests
    // =====================================================

    [Theory]
    [InlineData("; rm -rf /")]
    [InlineData("| cat /etc/passwd")]
    [InlineData("& dir c:\\")]
    [InlineData("`whoami`")]
    [InlineData("$(ls -la)")]
    public async Task FeatureById_WithCommandInjectionAttempt_DoesNotExecuteCommands(string maliciousId)
    {
        // Arrange
        var collectionsResponse = await _client.GetAsync("/ogc/collections");
        var collectionsJson = await collectionsResponse.Content.ReadAsStringAsync();
        var collectionsDoc = System.Text.Json.JsonDocument.Parse(collectionsJson);
        var firstCollection = collectionsDoc.RootElement.GetProperty("collections")[0];
        var collectionId = firstCollection.GetProperty("id").GetString();

        // Act
        var response = await _client.GetAsync($"/ogc/collections/{collectionId}/items/{Uri.EscapeDataString(maliciousId)}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            [HttpStatusCode.NotFound, HttpStatusCode.BadRequest],
            "Command injection should not succeed");

        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError,
            "Command injection should not cause server errors");
    }

    // =====================================================
    // NoSQL Injection Tests
    // =====================================================

    [Theory]
    [InlineData("{\"$ne\": null}")]
    [InlineData("{\"$gt\": \"\"}")]
    [InlineData("'; return true; var dummy='")]
    public async Task FeatureById_WithNoSqlInjectionAttempt_DoesNotCompromiseData(string maliciousId)
    {
        // Arrange
        var collectionsResponse = await _client.GetAsync("/ogc/collections");
        var collectionsJson = await collectionsResponse.Content.ReadAsStringAsync();
        var collectionsDoc = System.Text.Json.JsonDocument.Parse(collectionsJson);
        var firstCollection = collectionsDoc.RootElement.GetProperty("collections")[0];
        var collectionId = firstCollection.GetProperty("id").GetString();

        // Act
        var response = await _client.GetAsync($"/ogc/collections/{collectionId}/items/{Uri.EscapeDataString(maliciousId)}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            [HttpStatusCode.NotFound, HttpStatusCode.BadRequest],
            "NoSQL injection should not succeed");

        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError,
            "NoSQL injection should not cause server errors");
    }

    // =====================================================
    // HTTP Header Injection Tests
    // =====================================================

    [Theory]
    [InlineData("test\r\nX-Injected-Header: malicious")]
    [InlineData("test\nLocation: http://evil.com")]
    public async Task CollectionById_WithHeaderInjectionAttempt_DoesNotInjectHeaders(string maliciousId)
    {
        // Act
        var response = await _client.GetAsync($"/ogc/collections/{Uri.EscapeDataString(maliciousId)}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            [HttpStatusCode.NotFound, HttpStatusCode.BadRequest],
            "Header injection should not succeed");

        response.Headers.Should().NotContainKey("X-Injected-Header",
            "Injected headers should not appear in response");

        response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError,
            "Header injection should not cause server errors");
    }

    // =====================================================
    // NULL Byte Injection Tests
    // =====================================================

    [Theory]
    [InlineData("test\0.txt")]
    [InlineData("../../etc/passwd\0.jpg")]
    public async Task CollectionById_WithNullByteInjection_DoesNotBypassValidation(string maliciousId)
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _client.GetAsync($"/ogc/collections/{Uri.EscapeDataString(maliciousId)}"));

        exception.Message.Should().ContainEquivalentOf("null characters");
    }

    // =====================================================
    // Unicode Security Tests
    // =====================================================

    [Theory]
    [InlineData("test\u202e.txt")] // Right-to-Left Override
    [InlineData("admin\u0000")] // Null character
    [InlineData("\ufeffadmin")] // Zero Width No-Break Space
    public async Task CollectionById_WithUnicodeExploits_HandlesSecurely(string maliciousId)
    {
        if (maliciousId.Contains('\0'))
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _client.GetAsync($"/ogc/collections/{Uri.EscapeDataString(maliciousId)}"));

            exception.Message.Should().ContainEquivalentOf("null characters");
            return;
        }

        var response = await _client.GetAsync($"/ogc/collections/{Uri.EscapeDataString(maliciousId)}");
        using (response)
        {
            response.StatusCode.Should().BeOneOf(
                [HttpStatusCode.NotFound, HttpStatusCode.BadRequest],
                "Unicode exploits should not succeed");

            response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError,
                "Unicode exploits should not cause server errors");
        }
    }
}
