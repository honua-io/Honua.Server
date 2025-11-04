using System;
using System.Collections.Generic;
using Honua.Server.Host.Middleware;
using Xunit;

namespace Honua.Server.Host.Tests.Middleware;

/// <summary>
/// Comprehensive unit tests for SensitiveDataRedactor.
/// Tests redaction of passwords, tokens, API keys, secrets in headers, query params, and JSON bodies.
/// </summary>
[Collection("HostTests")]
[Trait("Category", "Integration")]
public sealed class SensitiveDataRedactorTests
{
    private readonly SensitiveDataRedactor _redactor;
    private readonly SensitiveDataRedactionOptions _options;

    public SensitiveDataRedactorTests()
    {
        _options = new SensitiveDataRedactionOptions();
        _redactor = new SensitiveDataRedactor(_options);
    }

    #region IsSensitiveField Tests

    [Theory]
    [InlineData("password")]
    [InlineData("Password")]
    [InlineData("PASSWORD")]
    [InlineData("passwd")]
    [InlineData("pwd")]
    [InlineData("secret")]
    [InlineData("Secret")]
    [InlineData("token")]
    [InlineData("Token")]
    [InlineData("api_key")]
    [InlineData("apiKey")]
    [InlineData("api-key")]
    [InlineData("x-api-key")]
    [InlineData("X-API-Key")]
    [InlineData("authorization")]
    [InlineData("Authorization")]
    [InlineData("bearer")]
    [InlineData("access_token")]
    [InlineData("refresh_token")]
    [InlineData("client_secret")]
    [InlineData("private_key")]
    [InlineData("encryption_key")]
    [InlineData("cookie")]
    [InlineData("set-cookie")]
    [InlineData("ssn")]
    [InlineData("credit_card")]
    [InlineData("cvv")]
    [InlineData("pin")]
    public void IsSensitiveField_CommonSensitiveFields_ReturnsTrue(string fieldName)
    {
        // Act
        var result = _redactor.IsSensitiveField(fieldName);

        // Assert
        Assert.True(result, $"Field '{fieldName}' should be detected as sensitive");
    }

    [Theory]
    [InlineData("user_password")]
    [InlineData("myPassword123")]
    [InlineData("password_hash")]
    [InlineData("api_secret")]
    [InlineData("secret_key")]
    [InlineData("auth_token")]
    [InlineData("access_token_secret")]
    [InlineData("api_key_prod")]
    [InlineData("user_credentials")]
    [InlineData("db_password")]
    public void IsSensitiveField_FieldsMatchingPatterns_ReturnsTrue(string fieldName)
    {
        // Act
        var result = _redactor.IsSensitiveField(fieldName);

        // Assert
        Assert.True(result, $"Field '{fieldName}' should be detected as sensitive by pattern matching");
    }

    [Theory]
    [InlineData("username")]
    [InlineData("email")]
    [InlineData("name")]
    [InlineData("id")]
    [InlineData("timestamp")]
    [InlineData("content-type")]
    [InlineData("accept")]
    [InlineData("user-agent")]
    public void IsSensitiveField_NonSensitiveFields_ReturnsFalse(string fieldName)
    {
        // Act
        var result = _redactor.IsSensitiveField(fieldName);

        // Assert
        Assert.False(result, $"Field '{fieldName}' should not be detected as sensitive");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsSensitiveField_NullOrWhitespace_ReturnsFalse(string fieldName)
    {
        // Act
        var result = _redactor.IsSensitiveField(fieldName);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region RedactDictionary Tests

    [Fact]
    public void RedactDictionary_PasswordHeader_RedactsValue()
    {
        // Arrange
        var data = new Dictionary<string, string>
        {
            ["password"] = "SuperSecret123!",
            ["username"] = "john.doe"
        };

        // Act
        var result = _redactor.RedactDictionary(data);

        // Assert
        Assert.Equal("***REDACTED***", result["password"]);
        Assert.Equal("john.doe", result["username"]);
    }

    [Fact]
    public void RedactDictionary_AuthorizationHeader_RedactsValue()
    {
        // Arrange
        var data = new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
            ["Content-Type"] = "application/json"
        };

        // Act
        var result = _redactor.RedactDictionary(data);

        // Assert
        Assert.Equal("***REDACTED***", result["Authorization"]);
        Assert.Equal("application/json", result["Content-Type"]);
    }

    [Fact]
    public void RedactDictionary_ApiKeyHeader_RedactsValue()
    {
        // Arrange
        var data = new Dictionary<string, string>
        {
            ["X-API-Key"] = "sk_live_1234567890abcdef",
            ["Accept"] = "application/json"
        };

        // Act
        var result = _redactor.RedactDictionary(data);

        // Assert
        Assert.Equal("***REDACTED***", result["X-API-Key"]);
        Assert.Equal("application/json", result["Accept"]);
    }

    [Fact]
    public void RedactDictionary_MultipleSensitiveHeaders_RedactsAll()
    {
        // Arrange
        var data = new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer token123",
            ["X-API-Key"] = "key123",
            ["Cookie"] = "session=abc123",
            ["User-Agent"] = "Mozilla/5.0"
        };

        // Act
        var result = _redactor.RedactDictionary(data);

        // Assert
        Assert.Equal("***REDACTED***", result["Authorization"]);
        Assert.Equal("***REDACTED***", result["X-API-Key"]);
        Assert.Equal("***REDACTED***", result["Cookie"]);
        Assert.Equal("Mozilla/5.0", result["User-Agent"]);
    }

    [Fact]
    public void RedactDictionary_EmptyDictionary_ReturnsEmpty()
    {
        // Arrange
        var data = new Dictionary<string, string>();

        // Act
        var result = _redactor.RedactDictionary(data);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void RedactDictionary_NullDictionary_ReturnsNull()
    {
        // Act
        var result = _redactor.RedactDictionary(null);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region RedactQueryString Tests

    [Fact]
    public void RedactQueryString_PasswordParam_RedactsValue()
    {
        // Arrange
        var queryString = "?username=john&password=secret123";

        // Act
        var result = _redactor.RedactQueryString(queryString);

        // Assert
        Assert.Contains("username=john", result);
        Assert.Contains("password=***REDACTED***", result);
        Assert.DoesNotContain("secret123", result);
    }

    [Fact]
    public void RedactQueryString_ApiKeyParam_RedactsValue()
    {
        // Arrange
        var queryString = "?api_key=sk_test_12345&format=json";

        // Act
        var result = _redactor.RedactQueryString(queryString);

        // Assert
        Assert.Contains("api_key=***REDACTED***", result);
        Assert.Contains("format=json", result);
        Assert.DoesNotContain("sk_test_12345", result);
    }

    [Fact]
    public void RedactQueryString_TokenParam_RedactsValue()
    {
        // Arrange
        var queryString = "token=abc123xyz&page=1";

        // Act
        var result = _redactor.RedactQueryString(queryString);

        // Assert
        Assert.Contains("token=***REDACTED***", result);
        Assert.Contains("page=1", result);
        Assert.DoesNotContain("abc123xyz", result);
    }

    [Fact]
    public void RedactQueryString_MultipleParams_RedactsOnlySensitive()
    {
        // Arrange
        var queryString = "?user=john&token=secret&api_key=key123&page=5";

        // Act
        var result = _redactor.RedactQueryString(queryString);

        // Assert
        Assert.Contains("user=john", result);
        Assert.Contains("page=5", result);
        Assert.Contains("token=***REDACTED***", result);
        Assert.Contains("api_key=***REDACTED***", result);
        Assert.DoesNotContain("secret", result);
        Assert.DoesNotContain("key123", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RedactQueryString_NullOrWhitespace_ReturnsSameValue(string queryString)
    {
        // Act
        var result = _redactor.RedactQueryString(queryString);

        // Assert
        Assert.Equal(queryString, result);
    }

    [Fact]
    public void RedactQueryString_WithoutQuestionMark_HandlesCorrectly()
    {
        // Arrange
        var queryString = "password=secret&user=john";

        // Act
        var result = _redactor.RedactQueryString(queryString);

        // Assert
        Assert.Contains("password=***REDACTED***", result);
        Assert.Contains("user=john", result);
        Assert.DoesNotContain("?", result);
    }

    [Fact]
    public void RedactQueryString_CaseInsensitive_RedactsRegardlessOfCase()
    {
        // Arrange
        var queryString = "?PASSWORD=secret&ApiKey=key123&Token=token456";

        // Act
        var result = _redactor.RedactQueryString(queryString);

        // Assert
        Assert.Contains("PASSWORD=***REDACTED***", result);
        Assert.Contains("ApiKey=***REDACTED***", result);
        Assert.Contains("Token=***REDACTED***", result);
    }

    #endregion

    #region RedactJson Tests

    [Fact]
    public void RedactJson_PasswordField_RedactsValue()
    {
        // Arrange
        var json = @"{""username"":""john"",""password"":""secret123""}";

        // Act
        var result = _redactor.RedactJson(json);

        // Assert
        Assert.Contains("\"username\":\"john\"", result.Replace(" ", ""));
        Assert.Contains("\"password\":\"***REDACTED***\"", result.Replace(" ", ""));
        Assert.DoesNotContain("secret123", result);
    }

    [Fact]
    public void RedactJson_ApiKeyField_RedactsValue()
    {
        // Arrange
        var json = @"{
            ""apiKey"": ""sk_live_12345"",
            ""environment"": ""production""
        }";

        // Act
        var result = _redactor.RedactJson(json);

        // Assert
        Assert.Contains("\"apiKey\":\"***REDACTED***\"", result.Replace(" ", ""));
        Assert.Contains("\"environment\":\"production\"", result.Replace(" ", ""));
        Assert.DoesNotContain("sk_live_12345", result);
    }

    [Fact]
    public void RedactJson_NestedObject_RedactsSensitiveFields()
    {
        // Arrange
        var json = @"{
            ""user"": {
                ""name"": ""John Doe"",
                ""credentials"": {
                    ""password"": ""secret123"",
                    ""apiKey"": ""key456""
                }
            }
        }";

        // Act
        var result = _redactor.RedactJson(json);

        // Assert
        Assert.Contains("\"name\":\"JohnDoe\"", result.Replace(" ", ""));
        Assert.Contains("\"password\":\"***REDACTED***\"", result.Replace(" ", ""));
        Assert.Contains("\"apiKey\":\"***REDACTED***\"", result.Replace(" ", ""));
        Assert.DoesNotContain("secret123", result);
        Assert.DoesNotContain("key456", result);
    }

    [Fact]
    public void RedactJson_ArrayWithSensitiveFields_RedactsValues()
    {
        // Arrange
        var json = @"{
            ""users"": [
                {""username"": ""john"", ""password"": ""pass1""},
                {""username"": ""jane"", ""password"": ""pass2""}
            ]
        }";

        // Act
        var result = _redactor.RedactJson(json);

        // Assert
        Assert.Contains("\"username\":\"john\"", result.Replace(" ", ""));
        Assert.Contains("\"username\":\"jane\"", result.Replace(" ", ""));
        Assert.Contains("\"password\":\"***REDACTED***\"", result.Replace(" ", ""));
        Assert.DoesNotContain("pass1", result);
        Assert.DoesNotContain("pass2", result);
    }

    [Fact]
    public void RedactJson_TokenField_RedactsValue()
    {
        // Arrange
        var json = @"{
            ""access_token"": ""eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9"",
            ""token_type"": ""bearer"",
            ""expires_in"": 3600
        }";

        // Act
        var result = _redactor.RedactJson(json);

        // Assert
        Assert.Contains("\"access_token\":\"***REDACTED***\"", result.Replace(" ", ""));
        Assert.Contains("\"token_type\":\"bearer\"", result.Replace(" ", ""));
        Assert.Contains("\"expires_in\":3600", result.Replace(" ", ""));
    }

    [Fact]
    public void RedactJson_ClientSecretField_RedactsValue()
    {
        // Arrange
        var json = @"{
            ""client_id"": ""my-client"",
            ""client_secret"": ""super-secret-value"",
            ""grant_type"": ""client_credentials""
        }";

        // Act
        var result = _redactor.RedactJson(json);

        // Assert
        Assert.Contains("\"client_id\":\"my-client\"", result.Replace(" ", ""));
        Assert.Contains("\"client_secret\":\"***REDACTED***\"", result.Replace(" ", ""));
        Assert.DoesNotContain("super-secret-value", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RedactJson_NullOrWhitespace_ReturnsSameValue(string json)
    {
        // Act
        var result = _redactor.RedactJson(json);

        // Assert
        Assert.Equal(json, result);
    }

    [Fact]
    public void RedactJson_InvalidJson_ReturnsOriginal()
    {
        // Arrange
        var invalidJson = "{not valid json";

        // Act
        var result = _redactor.RedactJson(invalidJson);

        // Assert
        Assert.Equal(invalidJson, result);
    }

    [Fact]
    public void RedactJson_ComplexRealWorldExample_RedactsAllSensitiveFields()
    {
        // Arrange
        var json = @"{
            ""user_id"": ""12345"",
            ""username"": ""john.doe"",
            ""email"": ""john@example.com"",
            ""password"": ""MyP@ssw0rd!"",
            ""profile"": {
                ""name"": ""John Doe"",
                ""ssn"": ""123-45-6789"",
                ""payment"": {
                    ""credit_card"": ""4111-1111-1111-1111"",
                    ""cvv"": ""123""
                }
            },
            ""api_credentials"": {
                ""api_key"": ""sk_live_abcdef123456"",
                ""secret"": ""secret_key_xyz"",
                ""access_token"": ""token_abc""
            }
        }";

        // Act
        var result = _redactor.RedactJson(json);

        // Assert
        // Non-sensitive fields should be preserved
        Assert.Contains("\"user_id\":\"12345\"", result.Replace(" ", ""));
        Assert.Contains("\"username\":\"john.doe\"", result.Replace(" ", ""));
        Assert.Contains("\"email\":\"john@example.com\"", result.Replace(" ", ""));
        Assert.Contains("\"name\":\"JohnDoe\"", result.Replace(" ", ""));

        // Sensitive fields should be redacted
        Assert.Contains("\"password\":\"***REDACTED***\"", result.Replace(" ", ""));
        Assert.Contains("\"ssn\":\"***REDACTED***\"", result.Replace(" ", ""));
        Assert.Contains("\"credit_card\":\"***REDACTED***\"", result.Replace(" ", ""));
        Assert.Contains("\"cvv\":\"***REDACTED***\"", result.Replace(" ", ""));
        Assert.Contains("\"api_key\":\"***REDACTED***\"", result.Replace(" ", ""));
        Assert.Contains("\"secret\":\"***REDACTED***\"", result.Replace(" ", ""));
        Assert.Contains("\"access_token\":\"***REDACTED***\"", result.Replace(" ", ""));

        // Sensitive values should not appear
        Assert.DoesNotContain("MyP@ssw0rd!", result);
        Assert.DoesNotContain("123-45-6789", result);
        Assert.DoesNotContain("4111-1111-1111-1111", result);
        Assert.DoesNotContain("sk_live_abcdef123456", result);
        Assert.DoesNotContain("secret_key_xyz", result);
    }

    #endregion

    #region Custom Configuration Tests

    [Fact]
    public void CustomSensitiveFields_AdditionalField_IsRedacted()
    {
        // Arrange
        var customOptions = new SensitiveDataRedactionOptions();
        customOptions.SensitiveFieldNames.Add("custom_secret_field");
        var customRedactor = new SensitiveDataRedactor(customOptions);

        var data = new Dictionary<string, string>
        {
            ["custom_secret_field"] = "secret-value",
            ["normal_field"] = "normal-value"
        };

        // Act
        var result = customRedactor.RedactDictionary(data);

        // Assert
        Assert.Equal("***REDACTED***", result["custom_secret_field"]);
        Assert.Equal("normal-value", result["normal_field"]);
    }

    [Fact]
    public void CustomPattern_MatchingField_IsRedacted()
    {
        // Arrange
        var customOptions = new SensitiveDataRedactionOptions();
        customOptions.SensitiveFieldPatterns.Add(@".*_internal$");
        var customRedactor = new SensitiveDataRedactor(customOptions);

        // Act
        var result = customRedactor.IsSensitiveField("data_internal");

        // Assert
        Assert.True(result);
    }

    #endregion
}
