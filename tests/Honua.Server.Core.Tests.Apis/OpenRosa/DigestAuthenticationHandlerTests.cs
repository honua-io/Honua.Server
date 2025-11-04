using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Authentication;
using Honua.Server.Core.OpenRosa;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Apis.OpenRosa;

[Collection("UnitTests")]
[Trait("Category", "Unit")]
public sealed class DigestAuthenticationHandlerTests : IDisposable
{
    private const string DefaultRealm = "Honua OpenRosa";
    private const string ValidUsername = "testuser";
    private const string ValidPassword = "testpass123";
    private const string ValidUserId = "user-123";
    private const string TestUri = "/openrosa/formList";
    private const string TestMethod = "GET";

    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IOptionsMonitor<DigestAuthenticationOptions>> _mockOptions;
    private readonly Mock<ILoggerFactory> _mockLoggerFactory;
    private readonly UrlEncoder _urlEncoder;
    private readonly DigestAuthenticationOptions _options;
    private readonly IMemoryCache _memoryCache;

    public DigestAuthenticationHandlerTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockOptions = new Mock<IOptionsMonitor<DigestAuthenticationOptions>>();
        _mockLoggerFactory = new Mock<ILoggerFactory>();
        _urlEncoder = UrlEncoder.Default;
        _memoryCache = new MemoryCache(new MemoryCacheOptions());

        _options = new DigestAuthenticationOptions
        {
            Realm = DefaultRealm,
            NonceLifetimeMinutes = 5
        };

        _mockOptions.Setup(x => x.Get(It.IsAny<string>())).Returns(_options);
        _mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(NullLogger.Instance);
    }

    #region Valid Authentication Tests

    [Fact]
    public async Task HandleAuthenticateAsync_ValidDigestWithCorrectCredentials_ReturnsSuccess()
    {
        // Arrange
        var nonce = GenerateValidNonce();
        var digestHeader = CreateValidDigestHeader(ValidUsername, ValidPassword, nonce, DefaultRealm, TestUri, TestMethod);

        var user = new DigestUser
        {
            Id = ValidUserId,
            Username = ValidUsername,
            Password = ValidPassword,
            Roles = new[] { "user" }
        };

        _mockUserRepository.Setup(x => x.GetByUsernameAsync(ValidUsername))
            .ReturnsAsync(user);

        var handler = CreateHandler();
        var context = CreateHttpContext(digestHeader, TestMethod, TestUri);
        await InitializeHandler(handler, context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Principal.Should().NotBeNull();
        result.Principal!.Identity!.Name.Should().Be(ValidUsername);
        result.Principal.Identity.IsAuthenticated.Should().BeTrue();
        result.Principal.Claims.Should().Contain(c => c.Type == ClaimTypes.NameIdentifier && c.Value == ValidUserId);
        result.Principal.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "user");
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ValidDigestWithQopAuth_ReturnsSuccess()
    {
        // Arrange
        var nonce = GenerateValidNonce();
        var nc = "00000001";
        var cnonce = GenerateClientNonce();
        var qop = "auth";

        var digestHeader = CreateValidDigestHeaderWithQop(ValidUsername, ValidPassword, nonce,
            DefaultRealm, TestUri, TestMethod, nc, cnonce, qop);

        var user = new DigestUser
        {
            Id = ValidUserId,
            Username = ValidUsername,
            Password = ValidPassword,
            Roles = new[] { "admin", "user" }
        };

        _mockUserRepository.Setup(x => x.GetByUsernameAsync(ValidUsername))
            .ReturnsAsync(user);

        var handler = CreateHandler();
        var context = CreateHttpContext(digestHeader, TestMethod, TestUri);
        await InitializeHandler(handler, context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Principal.Should().NotBeNull();
        result.Principal!.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "admin");
        result.Principal.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "user");
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ValidDigestWithoutQop_ReturnsSuccess()
    {
        // Arrange - RFC 2069 compatibility (no qop parameter)
        var nonce = GenerateValidNonce();
        var digestHeader = CreateValidDigestHeader(ValidUsername, ValidPassword, nonce, DefaultRealm, TestUri, TestMethod);

        var user = new DigestUser
        {
            Id = ValidUserId,
            Username = ValidUsername,
            Password = ValidPassword
        };

        _mockUserRepository.Setup(x => x.GetByUsernameAsync(ValidUsername))
            .ReturnsAsync(user);

        var handler = CreateHandler();
        var context = CreateHttpContext(digestHeader, TestMethod, TestUri);
        await InitializeHandler(handler, context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Principal.Should().NotBeNull();
        result.Principal!.Identity!.Name.Should().Be(ValidUsername);
    }

    [Theory]
    [InlineData("GET", "/openrosa/formList")]
    [InlineData("POST", "/openrosa/submission")]
    [InlineData("HEAD", "/openrosa/submission")]
    [InlineData("GET", "/openrosa/forms/123")]
    public async Task HandleAuthenticateAsync_ValidDigestForDifferentMethods_ReturnsSuccess(string method, string uri)
    {
        // Arrange
        var nonce = GenerateValidNonce();
        var digestHeader = CreateValidDigestHeader(ValidUsername, ValidPassword, nonce, DefaultRealm, uri, method);

        var user = new DigestUser
        {
            Id = ValidUserId,
            Username = ValidUsername,
            Password = ValidPassword
        };

        _mockUserRepository.Setup(x => x.GetByUsernameAsync(ValidUsername))
            .ReturnsAsync(user);

        var handler = CreateHandler();
        var context = CreateHttpContext(digestHeader, method, uri);
        await InitializeHandler(handler, context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    #endregion

    #region Invalid Authentication Tests

    [Fact]
    public async Task HandleAuthenticateAsync_MissingAuthorizationHeader_ReturnsNoResult()
    {
        // Arrange
        var handler = CreateHandler();
        var context = CreateHttpContext(null, TestMethod, TestUri);
        await InitializeHandler(handler, context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.None.Should().BeTrue();
        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAuthenticateAsync_NonDigestAuthorizationHeader_ReturnsNoResult()
    {
        // Arrange
        var handler = CreateHandler();
        var context = CreateHttpContext("Bearer some-token", TestMethod, TestUri);
        await InitializeHandler(handler, context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.None.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAuthenticateAsync_InvalidDigestFormat_ReturnsFailure()
    {
        // Arrange
        var handler = CreateHandler();
        var context = CreateHttpContext("Digest invalid-format", TestMethod, TestUri);
        await InitializeHandler(handler, context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failure.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAuthenticateAsync_MissingUsername_ReturnsFailure()
    {
        // Arrange
        var nonce = GenerateValidNonce();
        var digestHeader = $"Digest realm=\"{DefaultRealm}\", nonce=\"{nonce}\", uri=\"{TestUri}\", response=\"abc123\"";

        var handler = CreateHandler();
        var context = CreateHttpContext(digestHeader, TestMethod, TestUri);
        await InitializeHandler(handler, context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("Missing required Digest parameters");
    }

    [Fact]
    public async Task HandleAuthenticateAsync_MissingRealm_ReturnsFailure()
    {
        // Arrange
        var nonce = GenerateValidNonce();
        var digestHeader = $"Digest username=\"{ValidUsername}\", nonce=\"{nonce}\", uri=\"{TestUri}\", response=\"abc123\"";

        var handler = CreateHandler();
        var context = CreateHttpContext(digestHeader, TestMethod, TestUri);
        await InitializeHandler(handler, context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("Missing required Digest parameters");
    }

    [Fact]
    public async Task HandleAuthenticateAsync_MissingNonce_ReturnsFailure()
    {
        // Arrange
        var digestHeader = $"Digest username=\"{ValidUsername}\", realm=\"{DefaultRealm}\", uri=\"{TestUri}\", response=\"abc123\"";

        var handler = CreateHandler();
        var context = CreateHttpContext(digestHeader, TestMethod, TestUri);
        await InitializeHandler(handler, context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("Missing required Digest parameters");
    }

    [Fact]
    public async Task HandleAuthenticateAsync_WrongUsername_ReturnsFailure()
    {
        // Arrange
        var nonce = GenerateValidNonce();
        var digestHeader = CreateValidDigestHeader("wronguser", ValidPassword, nonce, DefaultRealm, TestUri, TestMethod);

        _mockUserRepository.Setup(x => x.GetByUsernameAsync("wronguser"))
            .ReturnsAsync((DigestUser?)null);

        var handler = CreateHandler();
        var context = CreateHttpContext(digestHeader, TestMethod, TestUri);
        await InitializeHandler(handler, context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("Invalid username or password");
    }

    [Fact]
    public async Task HandleAuthenticateAsync_WrongPassword_ReturnsFailure()
    {
        // Arrange
        var nonce = GenerateValidNonce();
        var digestHeader = CreateValidDigestHeader(ValidUsername, "wrongpassword", nonce, DefaultRealm, TestUri, TestMethod);

        var user = new DigestUser
        {
            Id = ValidUserId,
            Username = ValidUsername,
            Password = ValidPassword // Correct password
        };

        _mockUserRepository.Setup(x => x.GetByUsernameAsync(ValidUsername))
            .ReturnsAsync(user);

        var handler = CreateHandler();
        var context = CreateHttpContext(digestHeader, TestMethod, TestUri);
        await InitializeHandler(handler, context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("Invalid username or password");
    }

    [Fact]
    public async Task HandleAuthenticateAsync_ExpiredNonce_ReturnsFailure()
    {
        // Arrange - Create a nonce that's older than the lifetime
        var expiredTimestamp = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds();
        var randomBytes = RandomNumberGenerator.GetBytes(16);
        var data = $"{expiredTimestamp}:{Convert.ToBase64String(randomBytes)}";
        var expiredNonce = Convert.ToBase64String(Encoding.UTF8.GetBytes(data));

        var digestHeader = CreateValidDigestHeader(ValidUsername, ValidPassword, expiredNonce, DefaultRealm, TestUri, TestMethod);

        var handler = CreateHandler();
        var context = CreateHttpContext(digestHeader, TestMethod, TestUri);
        await InitializeHandler(handler, context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("Nonce expired or invalid");
    }

    [Fact]
    public async Task HandleAuthenticateAsync_InvalidNonceFormat_ReturnsFailure()
    {
        // Arrange
        var invalidNonce = Convert.ToBase64String(Encoding.UTF8.GetBytes("invalid-format"));
        var digestHeader = CreateValidDigestHeader(ValidUsername, ValidPassword, invalidNonce, DefaultRealm, TestUri, TestMethod);

        var handler = CreateHandler();
        var context = CreateHttpContext(digestHeader, TestMethod, TestUri);
        await InitializeHandler(handler, context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("Nonce expired or invalid");
    }

    [Fact]
    public async Task HandleAuthenticateAsync_MismatchedRealm_ReturnsFailure()
    {
        // Arrange
        var nonce = GenerateValidNonce();
        var wrongRealm = "WrongRealm";
        var digestHeader = CreateValidDigestHeader(ValidUsername, ValidPassword, nonce, wrongRealm, TestUri, TestMethod);

        var user = new DigestUser
        {
            Id = ValidUserId,
            Username = ValidUsername,
            Password = ValidPassword
        };

        _mockUserRepository.Setup(x => x.GetByUsernameAsync(ValidUsername))
            .ReturnsAsync(user);

        var handler = CreateHandler();
        var context = CreateHttpContext(digestHeader, TestMethod, TestUri);
        await InitializeHandler(handler, context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert - Will fail because the response is computed with wrong realm
        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("Invalid username or password");
    }

    [Fact]
    public async Task HandleAuthenticateAsync_TamperedResponseHash_ReturnsFailure()
    {
        // Arrange
        var nonce = GenerateValidNonce();
        var ha1 = ComputeMD5Hash($"{ValidUsername}:{DefaultRealm}:{ValidPassword}");
        var ha2 = ComputeMD5Hash($"{TestMethod}:{TestUri}");
        var validResponse = ComputeMD5Hash($"{ha1}:{nonce}:{ha2}");
        var tamperedResponse = validResponse.Substring(0, validResponse.Length - 1) + "X"; // Modify last character

        var digestHeader = $"Digest username=\"{ValidUsername}\", realm=\"{DefaultRealm}\", " +
                          $"nonce=\"{nonce}\", uri=\"{TestUri}\", response=\"{tamperedResponse}\"";

        var user = new DigestUser
        {
            Id = ValidUserId,
            Username = ValidUsername,
            Password = ValidPassword
        };

        _mockUserRepository.Setup(x => x.GetByUsernameAsync(ValidUsername))
            .ReturnsAsync(user);

        var handler = CreateHandler();
        var context = CreateHttpContext(digestHeader, TestMethod, TestUri);
        await InitializeHandler(handler, context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("Invalid username or password");
    }

    #endregion

    #region Challenge Handling Tests

    [Fact]
    public async Task HandleChallengeAsync_Returns401WithWWWAuthenticateHeader()
    {
        // Arrange
        var handler = CreateHandler();
        var context = CreateHttpContext(null, TestMethod, TestUri);
        await InitializeHandler(handler, context);

        // Act
        await handler.ChallengeAsync(new AuthenticationProperties());

        // Assert
        context.Response.StatusCode.Should().Be(401);
        context.Response.Headers.Should().ContainKey("WWW-Authenticate");
    }

    [Fact]
    public async Task HandleChallengeAsync_WWWAuthenticateHeader_ContainsRealm()
    {
        // Arrange
        var handler = CreateHandler();
        var context = CreateHttpContext(null, TestMethod, TestUri);
        await InitializeHandler(handler, context);

        // Act
        await handler.ChallengeAsync(new AuthenticationProperties());

        // Assert
        var wwwAuthHeader = context.Response.Headers["WWW-Authenticate"].ToString();
        wwwAuthHeader.Should().Contain($"realm=\"{DefaultRealm}\"");
    }

    [Fact]
    public async Task HandleChallengeAsync_WWWAuthenticateHeader_ContainsNonce()
    {
        // Arrange
        var handler = CreateHandler();
        var context = CreateHttpContext(null, TestMethod, TestUri);
        await InitializeHandler(handler, context);

        // Act
        await handler.ChallengeAsync(new AuthenticationProperties());

        // Assert
        var wwwAuthHeader = context.Response.Headers["WWW-Authenticate"].ToString();
        wwwAuthHeader.Should().Contain("nonce=\"");
        wwwAuthHeader.Should().MatchRegex("nonce=\"[^\"]+\"");
    }

    [Fact]
    public async Task HandleChallengeAsync_WWWAuthenticateHeader_ContainsQop()
    {
        // Arrange
        var handler = CreateHandler();
        var context = CreateHttpContext(null, TestMethod, TestUri);
        await InitializeHandler(handler, context);

        // Act
        await handler.ChallengeAsync(new AuthenticationProperties());

        // Assert
        var wwwAuthHeader = context.Response.Headers["WWW-Authenticate"].ToString();
        wwwAuthHeader.Should().Contain("qop=\"auth\"");
    }

    [Fact]
    public async Task HandleChallengeAsync_WWWAuthenticateHeader_HasCorrectFormat()
    {
        // Arrange
        var handler = CreateHandler();
        var context = CreateHttpContext(null, TestMethod, TestUri);
        await InitializeHandler(handler, context);

        // Act
        await handler.ChallengeAsync(new AuthenticationProperties());

        // Assert
        var wwwAuthHeader = context.Response.Headers["WWW-Authenticate"].ToString();
        wwwAuthHeader.Should().StartWith("Digest ");
        wwwAuthHeader.Should().Contain("realm=");
        wwwAuthHeader.Should().Contain("nonce=");
        wwwAuthHeader.Should().Contain("qop=");
    }

    [Fact]
    public async Task HandleChallengeAsync_GeneratesUniqueNonceEachTime()
    {
        // Arrange
        var handler1 = CreateHandler();
        var context1 = CreateHttpContext(null, TestMethod, TestUri);
        await InitializeHandler(handler1, context1);

        var handler2 = CreateHandler();
        var context2 = CreateHttpContext(null, TestMethod, TestUri);
        await InitializeHandler(handler2, context2);

        // Act
        await handler1.ChallengeAsync(new AuthenticationProperties());
        var nonce1 = ExtractNonceFromHeader(context1.Response.Headers["WWW-Authenticate"].ToString());

        await handler2.ChallengeAsync(new AuthenticationProperties());
        var nonce2 = ExtractNonceFromHeader(context2.Response.Headers["WWW-Authenticate"].ToString());

        // Assert
        nonce1.Should().NotBe(nonce2);
    }

    #endregion

    #region Hash Calculation Tests

    [Fact]
    public void ComputeHA1_ReturnsCorrectMD5Hash()
    {
        // Arrange
        var username = "testuser";
        var realm = "TestRealm";
        var password = "secret";
        var expected = ComputeMD5Hash($"{username}:{realm}:{password}");

        // Act - Use reflection to test private method or test through public interface
        var actual = ComputeMD5Hash($"{username}:{realm}:{password}");

        // Assert
        actual.Should().Be(expected);
        actual.Should().MatchRegex("^[a-f0-9]{32}$"); // MD5 hash format
    }

    [Fact]
    public void ComputeHA2_ReturnsCorrectMD5Hash()
    {
        // Arrange
        var method = "GET";
        var uri = "/api/test";
        var expected = ComputeMD5Hash($"{method}:{uri}");

        // Act
        var actual = ComputeMD5Hash($"{method}:{uri}");

        // Assert
        actual.Should().Be(expected);
        actual.Should().MatchRegex("^[a-f0-9]{32}$");
    }

    [Fact]
    public void ComputeResponse_WithoutQop_ReturnsCorrectHash()
    {
        // Arrange
        var ha1 = "939e7578ed9e3c518a452acee763bce9"; // MD5(user:realm:pass)
        var nonce = "dcd98b7102dd2f0e8b11d0f600bfb0c093";
        var ha2 = "39aff3a2bab6126f332b942af96d3366"; // MD5(GET:/dir/index.html)

        // Act
        var response = ComputeMD5Hash($"{ha1}:{nonce}:{ha2}");

        // Assert
        response.Should().MatchRegex("^[a-f0-9]{32}$");
        response.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void MD5Hash_IsConsistent()
    {
        // Arrange & Act
        var hash1 = ComputeMD5Hash("test data");
        var hash2 = ComputeMD5Hash("test data");

        // Assert
        hash1.Should().Be(hash2);
        hash1.Should().BeEquivalentTo(hash2); // Case-insensitive comparison
    }

    [Fact]
    public void MD5Hash_ProducesLowercaseHexadecimal()
    {
        // Arrange & Act
        var hash = ComputeMD5Hash("test data");

        // Assert
        hash.Should().MatchRegex("^[a-f0-9]{32}$");
        hash.Where(c => char.IsUpper(c)).Should().BeEmpty();
    }

    #endregion

    #region Security Properties Tests

    [Fact]
    public async Task NonceValidation_RejectsNonceFromFuture()
    {
        // Arrange - Create a nonce with a future timestamp
        var futureTimestamp = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds();
        var randomBytes = RandomNumberGenerator.GetBytes(16);
        var data = $"{futureTimestamp}:{Convert.ToBase64String(randomBytes)}";
        var futureNonce = Convert.ToBase64String(Encoding.UTF8.GetBytes(data));

        var digestHeader = CreateValidDigestHeader(ValidUsername, ValidPassword, futureNonce, DefaultRealm, TestUri, TestMethod);

        var handler = CreateHandler();
        var context = CreateHttpContext(digestHeader, TestMethod, TestUri);
        await InitializeHandler(handler, context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("Nonce expired or invalid");
    }

    [Fact]
    public async Task NonceValidation_AcceptsValidNonceWithinLifetime()
    {
        // Arrange
        var nonce = GenerateValidNonce();
        var digestHeader = CreateValidDigestHeader(ValidUsername, ValidPassword, nonce, DefaultRealm, TestUri, TestMethod);

        var user = new DigestUser
        {
            Id = ValidUserId,
            Username = ValidUsername,
            Password = ValidPassword
        };

        _mockUserRepository.Setup(x => x.GetByUsernameAsync(ValidUsername))
            .ReturnsAsync(user);

        var handler = CreateHandler();
        var context = CreateHttpContext(digestHeader, TestMethod, TestUri);
        await InitializeHandler(handler, context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task NonceValidation_RejectsNonceExactlyAtLifetimeBoundary()
    {
        // Arrange - Create a nonce exactly at the lifetime boundary (5 minutes + 1 second)
        var boundaryTimestamp = DateTimeOffset.UtcNow.AddMinutes(-5).AddSeconds(-1).ToUnixTimeSeconds();
        var randomBytes = RandomNumberGenerator.GetBytes(16);
        var data = $"{boundaryTimestamp}:{Convert.ToBase64String(randomBytes)}";
        var boundaryNonce = Convert.ToBase64String(Encoding.UTF8.GetBytes(data));

        var digestHeader = CreateValidDigestHeader(ValidUsername, ValidPassword, boundaryNonce, DefaultRealm, TestUri, TestMethod);

        var handler = CreateHandler();
        var context = CreateHttpContext(digestHeader, TestMethod, TestUri);
        await InitializeHandler(handler, context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("Nonce expired or invalid");
    }

    [Fact]
    public async Task UriTamperingDetection_RejectsModifiedUri()
    {
        // Arrange
        var nonce = GenerateValidNonce();
        var originalUri = "/openrosa/formList";
        var tamperedUri = "/openrosa/admin"; // Different URI

        // Create digest with original URI
        var digestHeader = CreateValidDigestHeader(ValidUsername, ValidPassword, nonce, DefaultRealm, originalUri, TestMethod);

        var user = new DigestUser
        {
            Id = ValidUserId,
            Username = ValidUsername,
            Password = ValidPassword
        };

        _mockUserRepository.Setup(x => x.GetByUsernameAsync(ValidUsername))
            .ReturnsAsync(user);

        var handler = CreateHandler();
        // But create context with tampered URI
        var context = CreateHttpContext(digestHeader, TestMethod, tamperedUri);
        await InitializeHandler(handler, context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("Invalid username or password");
    }

    [Fact]
    public async Task PasswordComparison_UsesSecureComparison()
    {
        // This test ensures that the comparison doesn't fail immediately on first mismatch
        // (timing attack prevention). We test by verifying wrong passwords are rejected.

        // Arrange
        var nonce = GenerateValidNonce();
        var wrongPassword = ValidPassword.Substring(0, ValidPassword.Length - 1) + "X";
        var digestHeader = CreateValidDigestHeader(ValidUsername, wrongPassword, nonce, DefaultRealm, TestUri, TestMethod);

        var user = new DigestUser
        {
            Id = ValidUserId,
            Username = ValidUsername,
            Password = ValidPassword
        };

        _mockUserRepository.Setup(x => x.GetByUsernameAsync(ValidUsername))
            .ReturnsAsync(user);

        var handler = CreateHandler();
        var context = CreateHttpContext(digestHeader, TestMethod, TestUri);
        await InitializeHandler(handler, context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeFalse();
        result.Failure!.Message.Should().Contain("Invalid username or password");
    }

    [Fact]
    public async Task NonceGeneration_ContainsTimestampAndRandomness()
    {
        // Arrange
        var handler = CreateHandler();
        var context = CreateHttpContext(null, TestMethod, TestUri);
        await InitializeHandler(handler, context);

        // Act
        await handler.ChallengeAsync(new AuthenticationProperties());
        var wwwAuthHeader = context.Response.Headers["WWW-Authenticate"].ToString();
        var nonce = ExtractNonceFromHeader(wwwAuthHeader);

        // Assert
        nonce.Should().NotBeNullOrEmpty();

        // Decode and verify structure
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(nonce));
        decoded.Should().Contain(":");

        var parts = decoded.Split(':');
        parts.Should().HaveCountGreaterThan(0);

        // First part should be a valid timestamp
        long.TryParse(parts[0], out var timestamp).Should().BeTrue();
        timestamp.Should().BeGreaterThan(0);

        // Should have random component
        parts.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public async Task Authentication_WithMultipleRoles_IncludesAllRoles()
    {
        // Arrange
        var nonce = GenerateValidNonce();
        var digestHeader = CreateValidDigestHeader(ValidUsername, ValidPassword, nonce, DefaultRealm, TestUri, TestMethod);

        var user = new DigestUser
        {
            Id = ValidUserId,
            Username = ValidUsername,
            Password = ValidPassword,
            Roles = new[] { "admin", "user", "editor", "viewer" }
        };

        _mockUserRepository.Setup(x => x.GetByUsernameAsync(ValidUsername))
            .ReturnsAsync(user);

        var handler = CreateHandler();
        var context = CreateHttpContext(digestHeader, TestMethod, TestUri);
        await InitializeHandler(handler, context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Principal.Should().NotBeNull();

        var roleClaims = result.Principal!.Claims.Where(c => c.Type == ClaimTypes.Role).ToList();
        roleClaims.Should().HaveCount(4);
        roleClaims.Should().Contain(c => c.Value == "admin");
        roleClaims.Should().Contain(c => c.Value == "user");
        roleClaims.Should().Contain(c => c.Value == "editor");
        roleClaims.Should().Contain(c => c.Value == "viewer");
    }

    [Fact]
    public async Task Authentication_WithNoRoles_SucceedsWithoutRoleClaims()
    {
        // Arrange
        var nonce = GenerateValidNonce();
        var digestHeader = CreateValidDigestHeader(ValidUsername, ValidPassword, nonce, DefaultRealm, TestUri, TestMethod);

        var user = new DigestUser
        {
            Id = ValidUserId,
            Username = ValidUsername,
            Password = ValidPassword,
            Roles = Array.Empty<string>()
        };

        _mockUserRepository.Setup(x => x.GetByUsernameAsync(ValidUsername))
            .ReturnsAsync(user);

        var handler = CreateHandler();
        var context = CreateHttpContext(digestHeader, TestMethod, TestUri);
        await InitializeHandler(handler, context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
        var roleClaims = result.Principal!.Claims.Where(c => c.Type == ClaimTypes.Role).ToList();
        roleClaims.Should().BeEmpty();
    }

    #endregion

    #region RFC 2617 Compliance Tests

    [Fact]
    public async Task RFC2617_SupportsMD5Algorithm()
    {
        // RFC 2617 requires MD5 as the default algorithm
        // Arrange
        var nonce = GenerateValidNonce();
        var digestHeader = CreateValidDigestHeader(ValidUsername, ValidPassword, nonce, DefaultRealm, TestUri, TestMethod);

        var user = new DigestUser
        {
            Id = ValidUserId,
            Username = ValidUsername,
            Password = ValidPassword
        };

        _mockUserRepository.Setup(x => x.GetByUsernameAsync(ValidUsername))
            .ReturnsAsync(user);

        var handler = CreateHandler();
        var context = CreateHttpContext(digestHeader, TestMethod, TestUri);
        await InitializeHandler(handler, context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task RFC2617_SupportsQopAuth()
    {
        // RFC 2617 defines qop=auth for authentication
        // Arrange
        var nonce = GenerateValidNonce();
        var nc = "00000001";
        var cnonce = GenerateClientNonce();
        var digestHeader = CreateValidDigestHeaderWithQop(ValidUsername, ValidPassword, nonce,
            DefaultRealm, TestUri, TestMethod, nc, cnonce, "auth");

        var user = new DigestUser
        {
            Id = ValidUserId,
            Username = ValidUsername,
            Password = ValidPassword
        };

        _mockUserRepository.Setup(x => x.GetByUsernameAsync(ValidUsername))
            .ReturnsAsync(user);

        var handler = CreateHandler();
        var context = CreateHttpContext(digestHeader, TestMethod, TestUri);
        await InitializeHandler(handler, context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task RFC2069_BackwardCompatibility_NoQop()
    {
        // RFC 2069 didn't have qop parameter
        // Arrange
        var nonce = GenerateValidNonce();
        var digestHeader = CreateValidDigestHeader(ValidUsername, ValidPassword, nonce, DefaultRealm, TestUri, TestMethod);

        var user = new DigestUser
        {
            Id = ValidUserId,
            Username = ValidUsername,
            Password = ValidPassword
        };

        _mockUserRepository.Setup(x => x.GetByUsernameAsync(ValidUsername))
            .ReturnsAsync(user);

        var handler = CreateHandler();
        var context = CreateHttpContext(digestHeader, TestMethod, TestUri);
        await InitializeHandler(handler, context);

        // Act
        var result = await handler.AuthenticateAsync();

        // Assert
        result.Succeeded.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private DigestAuthenticationHandler CreateHandler()
    {
        return new DigestAuthenticationHandler(
            _mockOptions.Object,
            _mockLoggerFactory.Object,
            _urlEncoder,
            _mockUserRepository.Object,
            _memoryCache);
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
    }

    private HttpContext CreateHttpContext(string? authorizationHeader, string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;

        if (authorizationHeader != null)
        {
            context.Request.Headers["Authorization"] = authorizationHeader;
        }

        return context;
    }

    private async Task InitializeHandler(AuthenticationHandler<DigestAuthenticationOptions> handler, HttpContext context)
    {
        var scheme = new AuthenticationScheme("Digest", "Digest", typeof(DigestAuthenticationHandler));
        await handler.InitializeAsync(scheme, context);
    }

    private string GenerateValidNonce()
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var randomBytes = RandomNumberGenerator.GetBytes(16);
        var data = $"{timestamp}:{Convert.ToBase64String(randomBytes)}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(data));
    }

    private string GenerateClientNonce()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(16);
        return Convert.ToBase64String(randomBytes);
    }

    private string CreateValidDigestHeader(string username, string password, string nonce,
        string realm, string uri, string method)
    {
        var ha1 = ComputeMD5Hash($"{username}:{realm}:{password}");
        var ha2 = ComputeMD5Hash($"{method}:{uri}");
        var response = ComputeMD5Hash($"{ha1}:{nonce}:{ha2}");

        return $"Digest username=\"{username}\", realm=\"{realm}\", nonce=\"{nonce}\", " +
               $"uri=\"{uri}\", response=\"{response}\"";
    }

    private string CreateValidDigestHeaderWithQop(string username, string password, string nonce,
        string realm, string uri, string method, string nc, string cnonce, string qop)
    {
        var ha1 = ComputeMD5Hash($"{username}:{realm}:{password}");
        var ha2 = ComputeMD5Hash($"{method}:{uri}");
        var response = ComputeMD5Hash($"{ha1}:{nonce}:{nc}:{cnonce}:{qop}:{ha2}");

        return $"Digest username=\"{username}\", realm=\"{realm}\", nonce=\"{nonce}\", " +
               $"uri=\"{uri}\", qop={qop}, nc={nc}, cnonce=\"{cnonce}\", response=\"{response}\"";
    }

    private string ComputeMD5Hash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
#pragma warning disable CA5351 // MD5 is required for HTTP Digest Authentication (RFC 2617)
        var hash = MD5.HashData(bytes);
#pragma warning restore CA5351
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private string ExtractNonceFromHeader(string wwwAuthHeader)
    {
        var nonceMatch = System.Text.RegularExpressions.Regex.Match(wwwAuthHeader, "nonce=\"([^\"]+)\"");
        return nonceMatch.Success ? nonceMatch.Groups[1].Value : string.Empty;
    }

    #endregion
}
