using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Authentication;
using Honua.Server.Core.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Honua.Server.Core.Tests.Security.Authentication;

/// <summary>
/// Comprehensive tests for JWT token generation, validation, and security.
/// </summary>
[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class JwtTokenTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly HonuaAuthenticationOptions _options;
    private readonly IOptionsMonitor<HonuaAuthenticationOptions> _optionsMonitor;
    private readonly LocalSigningKeyProvider _signingKeyProvider;
    private readonly LocalTokenService _tokenService;

    public JwtTokenTests()
    {
        _tempRoot = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"honua-jwt-tests-{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(_tempRoot);

        _options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local,
            Local = new HonuaAuthenticationOptions.LocalOptions
            {
                SessionLifetime = TimeSpan.FromMinutes(30),
                SigningKeyPath = System.IO.Path.Combine(_tempRoot, "signing.key")
            }
        };

        _optionsMonitor = new TestOptionsMonitor(_options);
        _signingKeyProvider = new LocalSigningKeyProvider(_optionsMonitor, NullLogger<LocalSigningKeyProvider>.Instance);
        _tokenService = new LocalTokenService(_optionsMonitor, _signingKeyProvider);
    }

    [Fact]
    public async Task CreateTokenAsync_GeneratesValidJwtToken()
    {
        // Arrange
        var subject = "user-123";
        var roles = new[] { "administrator", "user" };

        // Act
        var tokenString = await _tokenService.CreateTokenAsync(subject, roles);

        // Assert
        tokenString.Should().NotBeNullOrWhiteSpace();
        var handler = new JwtSecurityTokenHandler();
        handler.CanReadToken(tokenString).Should().BeTrue();
    }

    [Fact]
    public async Task CreateTokenAsync_IncludesSubjectClaim()
    {
        // Arrange
        var subject = "user-456";
        var roles = Array.Empty<string>();

        // Act
        var tokenString = await _tokenService.CreateTokenAsync(subject, roles);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenString);
        token.Subject.Should().Be(subject);
        token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == subject);
    }

    [Fact]
    public async Task CreateTokenAsync_IncludesAllRoles()
    {
        // Arrange
        var subject = "user-789";
        var roles = new[] { "administrator", "viewer", "editor" };

        // Act
        var tokenString = await _tokenService.CreateTokenAsync(subject, roles);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenString);

        var roleClaims = token.Claims
            .Where(c => c.Type == LocalAuthenticationDefaults.RoleClaimType)
            .Select(c => c.Value)
            .ToList();

        roleClaims.Should().BeEquivalentTo(roles);
    }

    [Fact]
    public async Task CreateTokenAsync_IncludesJtiClaim()
    {
        // Arrange
        var subject = "user-abc";
        var roles = new[] { "user" };

        // Act
        var tokenString = await _tokenService.CreateTokenAsync(subject, roles);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenString);

        var jti = token.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
        jti.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(jti, out _).Should().BeTrue();
    }

    [Fact]
    public async Task CreateTokenAsync_SetsCorrectIssuerAndAudience()
    {
        // Arrange
        var subject = "user-def";
        var roles = new[] { "user" };

        // Act
        var tokenString = await _tokenService.CreateTokenAsync(subject, roles);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenString);

        token.Issuer.Should().Be(LocalAuthenticationDefaults.Issuer);
        token.Audiences.Should().Contain(LocalAuthenticationDefaults.Audience);
    }

    [Fact]
    public async Task CreateTokenAsync_SetsExpirationTime()
    {
        // Arrange
        var subject = "user-ghi";
        var roles = new[] { "user" };
        var beforeCreation = DateTimeOffset.UtcNow;

        // Act
        var tokenString = await _tokenService.CreateTokenAsync(subject, roles);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenString);

        token.ValidTo.Should().BeAfter(beforeCreation.UtcDateTime);
        var expectedExpiration = beforeCreation.Add(_options.Local.SessionLifetime);
        token.ValidTo.Should().BeCloseTo(expectedExpiration.UtcDateTime, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CreateTokenAsync_SignsTokenWithCorrectAlgorithm()
    {
        // Arrange
        var subject = "user-jkl";
        var roles = new[] { "user" };

        // Act
        var tokenString = await _tokenService.CreateTokenAsync(subject, roles);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenString);

        token.SignatureAlgorithm.Should().Be(SecurityAlgorithms.HmacSha256);
    }

    [Fact]
    public async Task CreateTokenAsync_WithEmptyRoles_DoesNotIncludeRoleClaims()
    {
        // Arrange
        var subject = "user-mno";
        var roles = Array.Empty<string>();

        // Act
        var tokenString = await _tokenService.CreateTokenAsync(subject, roles);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenString);

        var roleClaims = token.Claims.Where(c => c.Type == LocalAuthenticationDefaults.RoleClaimType);
        roleClaims.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateTokenAsync_FiltersOutNullAndWhitespaceRoles()
    {
        // Arrange
        var subject = "user-pqr";
        var roles = new[] { "admin", null!, "", " ", "user" };

        // Act
        var tokenString = await _tokenService.CreateTokenAsync(subject, roles);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenString);

        var roleClaims = token.Claims
            .Where(c => c.Type == LocalAuthenticationDefaults.RoleClaimType)
            .Select(c => c.Value)
            .ToList();

        roleClaims.Should().BeEquivalentTo(new[] { "admin", "user" });
    }

    [Fact]
    public async Task CreateTokenAsync_ThrowsOnNullSubject()
    {
        // Arrange
        var roles = new[] { "user" };

        // Act & Assert
        // ArgumentException.ThrowIfNullOrWhiteSpace throws ArgumentNullException for null
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _tokenService.CreateTokenAsync(null!, roles));
    }

    [Fact]
    public async Task CreateTokenAsync_ThrowsOnEmptySubject()
    {
        // Arrange
        var roles = new[] { "user" };

        // Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(
            async () => await _tokenService.CreateTokenAsync("", roles));
    }

    [Fact]
    public async Task CreateTokenAsync_ThrowsOnWhitespaceSubject()
    {
        // Arrange
        var roles = new[] { "user" };

        // Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentException>(
            async () => await _tokenService.CreateTokenAsync("   ", roles));
    }

    [Fact]
    public async Task CreateTokenAsync_ThrowsOnNullRoles()
    {
        // Arrange
        var subject = "user-stu";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _tokenService.CreateTokenAsync(subject, null!));
    }

    [Fact]
    public async Task ValidateToken_WithValidToken_Succeeds()
    {
        // Arrange
        var subject = "user-vwx";
        var roles = new[] { "administrator" };
        var tokenString = await _tokenService.CreateTokenAsync(subject, roles);

        var signingKey = await _signingKeyProvider.GetSigningKeyAsync();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = LocalAuthenticationDefaults.Issuer,
            ValidateAudience = true,
            ValidAudience = LocalAuthenticationDefaults.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(signingKey),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        // Act
        var handler = new JwtSecurityTokenHandler();
        var principal = handler.ValidateToken(tokenString, validationParameters, out var validatedToken);

        // Assert
        principal.Should().NotBeNull();
        validatedToken.Should().NotBeNull();
        principal.Identity!.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateToken_WithInvalidSignature_Fails()
    {
        // Arrange
        var subject = "user-yz";
        var roles = new[] { "user" };
        var tokenString = await _tokenService.CreateTokenAsync(subject, roles);

        // Use a different key for validation
        var wrongKey = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = LocalAuthenticationDefaults.Issuer,
            ValidateAudience = true,
            ValidAudience = LocalAuthenticationDefaults.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(wrongKey),
            ValidateLifetime = true
        };

        // Act & Assert
        var handler = new JwtSecurityTokenHandler();
        // When the signing key doesn't match, the library throws SecurityTokenSignatureKeyNotFoundException
        Assert.Throws<SecurityTokenSignatureKeyNotFoundException>(() =>
            handler.ValidateToken(tokenString, validationParameters, out _));
    }

    [Fact]
    public async Task ValidateToken_WithExpiredToken_Fails()
    {
        // Arrange - Create token with very short lifetime
        var shortLifetimeOptions = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local,
            Local = new HonuaAuthenticationOptions.LocalOptions
            {
                SessionLifetime = TimeSpan.FromMilliseconds(1),
                SigningKeyPath = _options.Local.SigningKeyPath
            }
        };

        var shortLifetimeMonitor = new TestOptionsMonitor(shortLifetimeOptions);
        var shortLifetimeService = new LocalTokenService(shortLifetimeMonitor, _signingKeyProvider);

        var subject = "user-expired";
        var roles = new[] { "user" };
        var tokenString = await shortLifetimeService.CreateTokenAsync(subject, roles);

        // Wait for token to expire
        await Task.Delay(100);

        var signingKey = await _signingKeyProvider.GetSigningKeyAsync();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = LocalAuthenticationDefaults.Issuer,
            ValidateAudience = true,
            ValidAudience = LocalAuthenticationDefaults.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(signingKey),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        // Act & Assert
        var handler = new JwtSecurityTokenHandler();
        Assert.Throws<SecurityTokenExpiredException>(() =>
            handler.ValidateToken(tokenString, validationParameters, out _));
    }

    [Fact]
    public async Task ValidateToken_WithWrongAudience_Fails()
    {
        // Arrange
        var subject = "user-audience";
        var roles = new[] { "user" };
        var tokenString = await _tokenService.CreateTokenAsync(subject, roles);

        var signingKey = await _signingKeyProvider.GetSigningKeyAsync();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = LocalAuthenticationDefaults.Issuer,
            ValidateAudience = true,
            ValidAudience = "wrong-audience",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(signingKey),
            ValidateLifetime = true
        };

        // Act & Assert
        var handler = new JwtSecurityTokenHandler();
        Assert.Throws<SecurityTokenInvalidAudienceException>(() =>
            handler.ValidateToken(tokenString, validationParameters, out _));
    }

    [Fact]
    public async Task ValidateToken_WithWrongIssuer_Fails()
    {
        // Arrange
        var subject = "user-issuer";
        var roles = new[] { "user" };
        var tokenString = await _tokenService.CreateTokenAsync(subject, roles);

        var signingKey = await _signingKeyProvider.GetSigningKeyAsync();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "wrong-issuer",
            ValidateAudience = true,
            ValidAudience = LocalAuthenticationDefaults.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(signingKey),
            ValidateLifetime = true
        };

        // Act & Assert
        var handler = new JwtSecurityTokenHandler();
        Assert.Throws<SecurityTokenInvalidIssuerException>(() =>
            handler.ValidateToken(tokenString, validationParameters, out _));
    }

    [Fact]
    public async Task CreateTokenAsync_GeneratesUniqueJtiForEachToken()
    {
        // Arrange
        var subject = "user-unique";
        var roles = new[] { "user" };

        // Act - Generate multiple tokens
        var token1 = await _tokenService.CreateTokenAsync(subject, roles);
        var token2 = await _tokenService.CreateTokenAsync(subject, roles);
        var token3 = await _tokenService.CreateTokenAsync(subject, roles);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwt1 = handler.ReadJwtToken(token1);
        var jwt2 = handler.ReadJwtToken(token2);
        var jwt3 = handler.ReadJwtToken(token3);

        var jti1 = jwt1.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        var jti2 = jwt2.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        var jti3 = jwt3.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

        jti1.Should().NotBe(jti2);
        jti1.Should().NotBe(jti3);
        jti2.Should().NotBe(jti3);
    }

    [Fact]
    public async Task CreateTokenAsync_SetsNotBeforeToCurrentTime()
    {
        // Arrange
        var subject = "user-nbf";
        var roles = new[] { "user" };
        var beforeCreation = DateTimeOffset.UtcNow;

        // Act
        var tokenString = await _tokenService.CreateTokenAsync(subject, roles);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenString);

        token.ValidFrom.Should().BeOnOrBefore(DateTimeOffset.UtcNow.UtcDateTime);
        // JWT times are in seconds, so allow 1 second tolerance for precision loss
        token.ValidFrom.Should().BeCloseTo(beforeCreation.UtcDateTime, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task CreateTokenAsync_WithCustomSessionLifetime_UsesCorrectExpiration()
    {
        // Arrange
        var customLifetime = TimeSpan.FromHours(2);
        var customOptions = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local,
            Local = new HonuaAuthenticationOptions.LocalOptions
            {
                SessionLifetime = customLifetime,
                SigningKeyPath = _options.Local.SigningKeyPath
            }
        };

        var customMonitor = new TestOptionsMonitor(customOptions);
        var customService = new LocalTokenService(customMonitor, _signingKeyProvider);

        var subject = "user-custom";
        var roles = new[] { "user" };
        var beforeCreation = DateTimeOffset.UtcNow;

        // Act
        var tokenString = await customService.CreateTokenAsync(subject, roles);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenString);

        var expectedExpiration = beforeCreation.Add(customLifetime);
        token.ValidTo.Should().BeCloseTo(expectedExpiration.UtcDateTime, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ExtractClaims_FromValidToken_ReturnsAllClaims()
    {
        // Arrange
        var subject = "user-claims";
        var roles = new[] { "admin", "editor" };
        var tokenString = await _tokenService.CreateTokenAsync(subject, roles);

        // Act
        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(tokenString);

        // Assert
        token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub);
        token.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Jti);
        token.Claims.Should().Contain(c => c.Type == LocalAuthenticationDefaults.RoleClaimType && c.Value == "admin");
        token.Claims.Should().Contain(c => c.Type == LocalAuthenticationDefaults.RoleClaimType && c.Value == "editor");
    }

    public void Dispose()
    {
        try
        {
            if (System.IO.Directory.Exists(_tempRoot))
            {
                System.IO.Directory.Delete(_tempRoot, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private sealed class TestOptionsMonitor : IOptionsMonitor<HonuaAuthenticationOptions>
    {
        private readonly HonuaAuthenticationOptions _value;

        public TestOptionsMonitor(HonuaAuthenticationOptions value)
        {
            _value = value;
        }

        public HonuaAuthenticationOptions CurrentValue => _value;
        public HonuaAuthenticationOptions Get(string? name) => _value;
        public IDisposable OnChange(Action<HonuaAuthenticationOptions, string> listener) => new NoopDisposable();

        private sealed class NoopDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
