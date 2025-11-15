// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Authentication;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Data.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Honua.Server.Core.Tests.Security.Authentication;

public class LocalAuthenticationServiceTests
{
    private readonly Mock<IAuthRepository> _mockRepository;
    private readonly Mock<IPasswordHasher> _mockPasswordHasher;
    private readonly Mock<ILocalTokenService> _mockTokenService;
    private readonly Mock<IOptionsMonitor<HonuaAuthenticationOptions>> _mockOptions;
    private readonly Mock<IPasswordComplexityValidator> _mockPasswordValidator;
    private readonly Mock<ILogger<LocalAuthenticationService>> _mockLogger;
    private readonly LocalAuthenticationService _service;
    private readonly HonuaAuthenticationOptions _defaultOptions;

    public LocalAuthenticationServiceTests()
    {
        _mockRepository = new Mock<IAuthRepository>();
        _mockPasswordHasher = new Mock<IPasswordHasher>();
        _mockTokenService = new Mock<ILocalTokenService>();
        _mockOptions = new Mock<IOptionsMonitor<HonuaAuthenticationOptions>>();
        _mockPasswordValidator = new Mock<IPasswordComplexityValidator>();
        _mockLogger = new Mock<ILogger<LocalAuthenticationService>>();

        _defaultOptions = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local,
            Local = new HonuaAuthenticationOptions.LocalOptions
            {
                MaxFailedAttempts = 5,
                LockoutDuration = TimeSpan.FromMinutes(15)
            }
        };

        _mockOptions.Setup(x => x.CurrentValue).Returns(_defaultOptions);

        _service = new LocalAuthenticationService(
            _mockRepository.Object,
            _mockPasswordHasher.Object,
            _mockTokenService.Object,
            _mockOptions.Object,
            _mockPasswordValidator.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task AuthenticateAsync_WithValidCredentials_ReturnsSuccess()
    {
        // Arrange
        var username = "testuser";
        var password = "ValidPassword123!";
        var userId = "user-123";
        var roles = new List<string> { "Admin", "User" };
        var token = "jwt-token-123";

        var credentials = new AuthUserCredentials(
            Id: userId,
            Username: username,
            Email: null,
            IsActive: true,
            IsLocked: false,
            IsServiceAccount: false,
            FailedAttempts: 0,
            LastFailedAt: null,
            LastLoginAt: null,
            PasswordChangedAt: null,
            PasswordExpiresAt: null,
            PasswordHash: new byte[] { 1, 2, 3 },
            PasswordSalt: new byte[] { 4, 5, 6 },
            HashAlgorithm: "Argon2id",
            HashParameters: "timeCost=4;memoryCost=65536",
            Roles: roles
        );

        _mockRepository.Setup(x => x.GetCredentialsByUsernameAsync(username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(credentials);
        _mockPasswordHasher.Setup(x => x.VerifyPassword(password, credentials.PasswordHash, credentials.PasswordSalt, credentials.HashAlgorithm, credentials.HashParameters))
            .Returns(true);
        _mockTokenService.Setup(x => x.CreateTokenAsync(userId, roles, It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        // Act
        var result = await _service.AuthenticateAsync(username, password);

        // Assert
        result.Status.Should().Be(LocalAuthenticationStatus.Success);
        result.Token.Should().Be(token);
        result.UserId.Should().Be(userId);
        result.Roles.Should().BeEquivalentTo(roles);

        _mockRepository.Verify(x => x.UpdateLoginSuccessAsync(userId, It.IsAny<DateTimeOffset>(), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AuthenticateAsync_WithInvalidCredentials_IncreasesFailedAttempts()
    {
        // Arrange
        var username = "testuser";
        var password = "WrongPassword";
        var userId = "user-123";

        var credentials = new AuthUserCredentials(
            Id: userId,
            Username: username,
            Email: null,
            IsActive: true,
            IsLocked: false,
            IsServiceAccount: false,
            FailedAttempts: 2,
            LastFailedAt: null,
            LastLoginAt: null,
            PasswordChangedAt: null,
            PasswordExpiresAt: null,
            PasswordHash: new byte[] { 1, 2, 3 },
            PasswordSalt: new byte[] { 4, 5, 6 },
            HashAlgorithm: "Argon2id",
            HashParameters: "timeCost=4",
            Roles: new List<string>()
        );

        _mockRepository.Setup(x => x.GetCredentialsByUsernameAsync(username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(credentials);
        _mockPasswordHasher.Setup(x => x.VerifyPassword(password, credentials.PasswordHash, credentials.PasswordSalt, credentials.HashAlgorithm, credentials.HashParameters))
            .Returns(false);

        // Act
        var result = await _service.AuthenticateAsync(username, password);

        // Assert
        result.Status.Should().Be(LocalAuthenticationStatus.InvalidCredentials);
        result.Token.Should().BeNull();
        result.UserId.Should().BeNull();

        _mockRepository.Verify(x => x.UpdateLoginFailureAsync(
            userId,
            3, // Failed attempts should be incremented
            It.IsAny<DateTimeOffset>(),
            false, // Should not lock yet (max is 5)
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AuthenticateAsync_WithMaxFailedAttempts_LocksAccount()
    {
        // Arrange
        var username = "testuser";
        var password = "WrongPassword";
        var userId = "user-123";

        var credentials = new AuthUserCredentials(
            Id: userId,
            Username: username,
            Email: null,
            IsActive: true,
            IsLocked: false,
            IsServiceAccount: false,
            FailedAttempts: 4, // One more failure will lock the account
            LastFailedAt: null,
            LastLoginAt: null,
            PasswordChangedAt: null,
            PasswordExpiresAt: null,
            PasswordHash: new byte[] { 1, 2, 3 },
            PasswordSalt: new byte[] { 4, 5, 6 },
            HashAlgorithm: "Argon2id",
            HashParameters: "timeCost=4",
            Roles: new List<string>()
        );

        _mockRepository.Setup(x => x.GetCredentialsByUsernameAsync(username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(credentials);
        _mockPasswordHasher.Setup(x => x.VerifyPassword(password, credentials.PasswordHash, credentials.PasswordSalt, credentials.HashAlgorithm, credentials.HashParameters))
            .Returns(false);

        // Act
        var result = await _service.AuthenticateAsync(username, password);

        // Assert
        result.Status.Should().Be(LocalAuthenticationStatus.LockedOut);
        result.Token.Should().BeNull();
        result.LockedUntil.Should().NotBeNull();
        result.LockedUntil.Value.Should().BeCloseTo(DateTimeOffset.UtcNow.AddMinutes(15), TimeSpan.FromSeconds(5));

        _mockRepository.Verify(x => x.UpdateLoginFailureAsync(
            userId,
            5, // Should reach max failed attempts
            It.IsAny<DateTimeOffset>(),
            true, // Should lock the account
            null,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AuthenticateAsync_WithLockedAccount_ReturnsLockedOut()
    {
        // Arrange
        var username = "testuser";
        var password = "ValidPassword123!";
        var now = DateTimeOffset.UtcNow;

        var credentials = new AuthUserCredentials(
            Id: "user-123",
            Username: username,
            Email: null,
            IsActive: true,
            IsLocked: true,
            IsServiceAccount: false,
            FailedAttempts: 5,
            LastFailedAt: now.AddMinutes(-5), // Locked 5 minutes ago
            LastLoginAt: null,
            PasswordChangedAt: null,
            PasswordExpiresAt: null,
            PasswordHash: new byte[] { 1, 2, 3 },
            PasswordSalt: new byte[] { 4, 5, 6 },
            HashAlgorithm: "Argon2id",
            HashParameters: "timeCost=4",
            Roles: new List<string>()
        );

        _mockRepository.Setup(x => x.GetCredentialsByUsernameAsync(username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(credentials);

        // Act
        var result = await _service.AuthenticateAsync(username, password);

        // Assert
        result.Status.Should().Be(LocalAuthenticationStatus.LockedOut);
        result.Token.Should().BeNull();
        result.LockedUntil.Should().NotBeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_WithDisabledAccount_ReturnsDisabled()
    {
        // Arrange
        var username = "testuser";
        var password = "ValidPassword123!";

        var credentials = new AuthUserCredentials(
            Id: "user-123",
            Username: username,
            Email: null,
            IsActive: false, // Disabled account
            IsLocked: false,
            IsServiceAccount: false,
            FailedAttempts: 0,
            LastFailedAt: null,
            LastLoginAt: null,
            PasswordChangedAt: null,
            PasswordExpiresAt: null,
            PasswordHash: new byte[] { 1, 2, 3 },
            PasswordSalt: new byte[] { 4, 5, 6 },
            HashAlgorithm: "Argon2id",
            HashParameters: "timeCost=4",
            Roles: new List<string>()
        );

        _mockRepository.Setup(x => x.GetCredentialsByUsernameAsync(username, It.IsAny<CancellationToken>()))
            .ReturnsAsync(credentials);

        // Act
        var result = await _service.AuthenticateAsync(username, password);

        // Assert
        result.Status.Should().Be(LocalAuthenticationStatus.Disabled);
        result.Token.Should().BeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_WhenModeNotLocal_ReturnsNotConfigured()
    {
        // Arrange
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Oidc // Not Local
        };
        _mockOptions.Setup(x => x.CurrentValue).Returns(options);

        // Act
        var result = await _service.AuthenticateAsync("testuser", "password");

        // Assert
        result.Status.Should().Be(LocalAuthenticationStatus.NotConfigured);
        result.Token.Should().BeNull();
    }

    [Theory]
    [InlineData(null, "password")]
    [InlineData("", "password")]
    [InlineData("   ", "password")]
    [InlineData("username", null)]
    [InlineData("username", "")]
    [InlineData("username", "   ")]
    public async Task AuthenticateAsync_WithNullOrEmptyCredentials_ReturnsInvalidCredentials(string? username, string? password)
    {
        // Act
        var result = await _service.AuthenticateAsync(username!, password!);

        // Assert
        result.Status.Should().Be(LocalAuthenticationStatus.InvalidCredentials);
        result.Token.Should().BeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_WithNonExistentUser_ReturnsInvalidCredentials()
    {
        // Arrange
        _mockRepository.Setup(x => x.GetCredentialsByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthUserCredentials?)null);

        // Act
        var result = await _service.AuthenticateAsync("nonexistent", "password");

        // Assert
        result.Status.Should().Be(LocalAuthenticationStatus.InvalidCredentials);
        result.Token.Should().BeNull();
    }

    [Fact]
    public async Task ChangePasswordAsync_WithValidCurrentPassword_ChangesPassword()
    {
        // Arrange
        var userId = "user-123";
        var currentPassword = "OldPassword123!";
        var newPassword = "NewPassword456!";

        var credentials = new AuthUserCredentials(
            Id: userId,
            Username: "testuser",
            Email: null,
            IsActive: true,
            IsLocked: false,
            IsServiceAccount: false,
            FailedAttempts: 0,
            LastFailedAt: null,
            LastLoginAt: null,
            PasswordChangedAt: null,
            PasswordExpiresAt: null,
            PasswordHash: new byte[] { 1, 2, 3 },
            PasswordSalt: new byte[] { 4, 5, 6 },
            HashAlgorithm: "Argon2id",
            HashParameters: "timeCost=4",
            Roles: new List<string>()
        );

        var newHashResult = new PasswordHashResult(
            new byte[] { 7, 8, 9 },
            new byte[] { 10, 11, 12 },
            "Argon2id",
            "timeCost=4;memoryCost=65536");

        _mockRepository.Setup(x => x.GetCredentialsByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(credentials);
        _mockPasswordHasher.Setup(x => x.VerifyPassword(currentPassword, credentials.PasswordHash, credentials.PasswordSalt, credentials.HashAlgorithm, credentials.HashParameters))
            .Returns(true);
        _mockPasswordValidator.Setup(x => x.Validate(newPassword))
            .Returns(PasswordComplexityResult.Success());
        _mockPasswordHasher.Setup(x => x.HashPassword(newPassword))
            .Returns(newHashResult);

        // Act
        await _service.ChangePasswordAsync(userId, currentPassword, newPassword);

        // Assert
        _mockRepository.Verify(x => x.SetLocalUserPasswordAsync(
            userId,
            newHashResult.Hash,
            newHashResult.Salt,
            newHashResult.Algorithm,
            newHashResult.Parameters,
            It.IsAny<AuditContext>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithInvalidCurrentPassword_ThrowsException()
    {
        // Arrange
        var userId = "user-123";
        var currentPassword = "WrongPassword";
        var newPassword = "NewPassword456!";

        var credentials = new AuthUserCredentials(
            Id: userId,
            Username: "testuser",
            Email: null,
            IsActive: true,
            IsLocked: false,
            IsServiceAccount: false,
            FailedAttempts: 0,
            LastFailedAt: null,
            LastLoginAt: null,
            PasswordChangedAt: null,
            PasswordExpiresAt: null,
            PasswordHash: new byte[] { 1, 2, 3 },
            PasswordSalt: new byte[] { 4, 5, 6 },
            HashAlgorithm: "Argon2id",
            HashParameters: "timeCost=4",
            Roles: new List<string>()
        );

        _mockRepository.Setup(x => x.GetCredentialsByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(credentials);
        _mockPasswordHasher.Setup(x => x.VerifyPassword(currentPassword, credentials.PasswordHash, credentials.PasswordSalt, credentials.HashAlgorithm, credentials.HashParameters))
            .Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ChangePasswordAsync(userId, currentPassword, newPassword));
    }

    [Fact]
    public async Task ChangePasswordAsync_WithInvalidComplexity_ThrowsException()
    {
        // Arrange
        var userId = "user-123";
        var currentPassword = "OldPassword123!";
        var newPassword = "weak";

        var credentials = new AuthUserCredentials(
            Id: userId,
            Username: "testuser",
            Email: null,
            IsActive: true,
            IsLocked: false,
            IsServiceAccount: false,
            FailedAttempts: 0,
            LastFailedAt: null,
            LastLoginAt: null,
            PasswordChangedAt: null,
            PasswordExpiresAt: null,
            PasswordHash: new byte[] { 1, 2, 3 },
            PasswordSalt: new byte[] { 4, 5, 6 },
            HashAlgorithm: "Argon2id",
            HashParameters: "timeCost=4",
            Roles: new List<string>()
        );

        _mockRepository.Setup(x => x.GetCredentialsByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(credentials);
        _mockPasswordHasher.Setup(x => x.VerifyPassword(currentPassword, credentials.PasswordHash, credentials.PasswordSalt, credentials.HashAlgorithm, credentials.HashParameters))
            .Returns(true);
        _mockPasswordValidator.Setup(x => x.Validate(newPassword))
            .Returns(PasswordComplexityResult.Failure("Password is too weak"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ChangePasswordAsync(userId, currentPassword, newPassword));
        exception.Message.Should().Contain("too weak");
    }

    [Fact]
    public async Task ResetPasswordAsync_WithValidPassword_ResetsPassword()
    {
        // Arrange
        var targetUserId = "user-123";
        var actorUserId = "admin-456";
        var newPassword = "NewPassword456!";

        var credentials = new AuthUserCredentials(
            Id: targetUserId,
            Username: "testuser",
            Email: null,
            IsActive: true,
            IsLocked: false,
            IsServiceAccount: false,
            FailedAttempts: 0,
            LastFailedAt: null,
            LastLoginAt: null,
            PasswordChangedAt: null,
            PasswordExpiresAt: null,
            PasswordHash: new byte[] { 1, 2, 3 },
            PasswordSalt: new byte[] { 4, 5, 6 },
            HashAlgorithm: "Argon2id",
            HashParameters: "timeCost=4",
            Roles: new List<string>()
        );

        var newHashResult = new PasswordHashResult(
            new byte[] { 7, 8, 9 },
            new byte[] { 10, 11, 12 },
            "Argon2id",
            "timeCost=4;memoryCost=65536");

        _mockRepository.Setup(x => x.GetCredentialsByIdAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(credentials);
        _mockPasswordValidator.Setup(x => x.Validate(newPassword))
            .Returns(PasswordComplexityResult.Success());
        _mockPasswordHasher.Setup(x => x.HashPassword(newPassword))
            .Returns(newHashResult);

        // Act
        await _service.ResetPasswordAsync(targetUserId, newPassword, actorUserId);

        // Assert
        _mockRepository.Verify(x => x.SetLocalUserPasswordAsync(
            targetUserId,
            newHashResult.Hash,
            newHashResult.Salt,
            newHashResult.Algorithm,
            newHashResult.Parameters,
            It.Is<AuditContext>(a => a.ActorId == actorUserId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResetPasswordAsync_WithInvalidComplexity_ThrowsException()
    {
        // Arrange
        var targetUserId = "user-123";
        var actorUserId = "admin-456";
        var newPassword = "weak";

        var credentials = new AuthUserCredentials(
            Id: targetUserId,
            Username: "testuser",
            Email: null,
            IsActive: true,
            IsLocked: false,
            IsServiceAccount: false,
            FailedAttempts: 0,
            LastFailedAt: null,
            LastLoginAt: null,
            PasswordChangedAt: null,
            PasswordExpiresAt: null,
            PasswordHash: new byte[] { 1, 2, 3 },
            PasswordSalt: new byte[] { 4, 5, 6 },
            HashAlgorithm: "Argon2id",
            HashParameters: "timeCost=4",
            Roles: new List<string>()
        );

        _mockRepository.Setup(x => x.GetCredentialsByIdAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(credentials);
        _mockPasswordValidator.Setup(x => x.Validate(newPassword))
            .Returns(PasswordComplexityResult.Failure("Password is too weak"));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ResetPasswordAsync(targetUserId, newPassword, actorUserId));
        exception.Message.Should().Contain("too weak");
    }

    [Fact]
    public async Task ChangePasswordAsync_WithNonExistentUser_ThrowsException()
    {
        // Arrange
        var userId = "nonexistent-123";
        _mockRepository.Setup(x => x.GetCredentialsByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthUserCredentials?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.ChangePasswordAsync(userId, "oldpass", "newpass"));
    }

    [Fact]
    public async Task ResetPasswordAsync_WithNonExistentUser_ThrowsException()
    {
        // Arrange
        var userId = "nonexistent-123";
        _mockRepository.Setup(x => x.GetCredentialsByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthUserCredentials?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.ResetPasswordAsync(userId, "newpass", "admin"));
    }
}
