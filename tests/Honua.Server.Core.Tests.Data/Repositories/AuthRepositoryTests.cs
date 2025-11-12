// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Data.Auth;
using Xunit;

namespace Honua.Server.Core.Tests.Data.Repositories;

/// <summary>
/// Tests for IAuthRepository interface implementations.
/// These tests verify the contract that all repository implementations must follow.
/// </summary>
public class AuthRepositoryTests
{
    [Fact]
    public void BootstrapState_Record_CanBeCreated()
    {
        // Arrange & Act
        var state = new BootstrapState(IsCompleted: true, Mode: "local");

        // Assert
        state.IsCompleted.Should().BeTrue();
        state.Mode.Should().Be("local");
    }

    [Fact]
    public void AuthUserCredentials_Record_CanBeCreated()
    {
        // Arrange & Act
        var credentials = new AuthUserCredentials(
            Id: "user-123",
            Username: "testuser",
            Email: "test@example.com",
            IsActive: true,
            IsLocked: false,
            IsServiceAccount: false,
            FailedAttempts: 0,
            LastFailedAt: null,
            LastLoginAt: DateTimeOffset.UtcNow,
            PasswordChangedAt: DateTimeOffset.UtcNow,
            PasswordExpiresAt: null,
            PasswordHash: new byte[] { 1, 2, 3 },
            PasswordSalt: new byte[] { 4, 5, 6 },
            HashAlgorithm: "Argon2id",
            HashParameters: "timeCost=4",
            Roles: new List<string> { "Admin" });

        // Assert
        credentials.Should().NotBeNull();
        credentials.Id.Should().Be("user-123");
        credentials.Username.Should().Be("testuser");
        credentials.Email.Should().Be("test@example.com");
        credentials.IsActive.Should().BeTrue();
        credentials.IsLocked.Should().BeFalse();
        credentials.FailedAttempts.Should().Be(0);
        credentials.Roles.Should().ContainSingle().Which.Should().Be("Admin");
    }

    [Fact]
    public void AuditContext_Empty_HasNullValues()
    {
        // Arrange & Act
        var context = AuditContext.Empty;

        // Assert
        context.ActorId.Should().BeNull();
        context.IpAddress.Should().BeNull();
        context.UserAgent.Should().BeNull();
    }

    [Fact]
    public void AuditContext_WithParameters_StoresValues()
    {
        // Arrange & Act
        var context = new AuditContext(
            ActorId: "admin-123",
            IpAddress: "192.168.1.1",
            UserAgent: "Mozilla/5.0");

        // Assert
        context.ActorId.Should().Be("admin-123");
        context.IpAddress.Should().Be("192.168.1.1");
        context.UserAgent.Should().Be("Mozilla/5.0");
    }

    [Fact]
    public void AuditRecord_Record_CanBeCreated()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;

        // Act
        var record = new AuditRecord(
            Id: 1,
            UserId: "user-123",
            Action: "login",
            Details: "Successful login",
            OldValue: null,
            NewValue: null,
            ActorId: null,
            IpAddress: "192.168.1.1",
            UserAgent: "Mozilla/5.0",
            OccurredAt: now);

        // Assert
        record.Should().NotBeNull();
        record.Id.Should().Be(1);
        record.UserId.Should().Be("user-123");
        record.Action.Should().Be("login");
        record.Details.Should().Be("Successful login");
        record.IpAddress.Should().Be("192.168.1.1");
        record.OccurredAt.Should().Be(now);
    }

    [Fact]
    public void AuthUser_Record_CanBeCreated()
    {
        // Arrange & Act
        var user = new AuthUser(
            Id: "user-123",
            Username: "testuser",
            Email: "test@example.com",
            IsActive: true,
            IsLocked: false,
            Roles: new List<string> { "Admin", "User" });

        // Assert
        user.Should().NotBeNull();
        user.Id.Should().Be("user-123");
        user.Username.Should().Be("testuser");
        user.Email.Should().Be("test@example.com");
        user.IsActive.Should().BeTrue();
        user.IsLocked.Should().BeFalse();
        user.Roles.Should().HaveCount(2);
        user.Roles.Should().Contain(new[] { "Admin", "User" });
    }

    [Fact]
    public void AuthUserCredentials_SupportsEmptyRoles()
    {
        // Arrange & Act
        var credentials = new AuthUserCredentials(
            Id: "user-123",
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
            Roles: Array.Empty<string>());

        // Assert
        credentials.Roles.Should().BeEmpty();
    }

    [Fact]
    public void AuthUserCredentials_SupportsServiceAccount()
    {
        // Arrange & Act
        var credentials = new AuthUserCredentials(
            Id: "service-123",
            Username: "service-account",
            Email: null,
            IsActive: true,
            IsLocked: false,
            IsServiceAccount: true, // Service account
            FailedAttempts: 0,
            LastFailedAt: null,
            LastLoginAt: null,
            PasswordChangedAt: null,
            PasswordExpiresAt: null,
            PasswordHash: new byte[] { 1, 2, 3 },
            PasswordSalt: new byte[] { 4, 5, 6 },
            HashAlgorithm: "Argon2id",
            HashParameters: "timeCost=4",
            Roles: new List<string> { "Service" });

        // Assert
        credentials.IsServiceAccount.Should().BeTrue();
        credentials.Username.Should().Be("service-account");
    }

    [Fact]
    public void AuthUserCredentials_SupportsPasswordExpiration()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddDays(90);

        // Act
        var credentials = new AuthUserCredentials(
            Id: "user-123",
            Username: "testuser",
            Email: null,
            IsActive: true,
            IsLocked: false,
            IsServiceAccount: false,
            FailedAttempts: 0,
            LastFailedAt: null,
            LastLoginAt: null,
            PasswordChangedAt: now,
            PasswordExpiresAt: expiresAt, // Password expiration set
            PasswordHash: new byte[] { 1, 2, 3 },
            PasswordSalt: new byte[] { 4, 5, 6 },
            HashAlgorithm: "Argon2id",
            HashParameters: "timeCost=4",
            Roles: new List<string> { "User" });

        // Assert
        credentials.PasswordChangedAt.Should().Be(now);
        credentials.PasswordExpiresAt.Should().Be(expiresAt);
    }

    [Fact]
    public void AuthUserCredentials_RecordEquality_WorksCorrectly()
    {
        // Arrange
        var roles = new List<string> { "Admin" };
        var hash = new byte[] { 1, 2, 3 };
        var salt = new byte[] { 4, 5, 6 };

        var credentials1 = new AuthUserCredentials(
            "user-123", "testuser", "test@example.com", true, false, false,
            0, null, null, null, null, hash, salt, "Argon2id", "timeCost=4", roles);

        var credentials2 = new AuthUserCredentials(
            "user-123", "testuser", "test@example.com", true, false, false,
            0, null, null, null, null, hash, salt, "Argon2id", "timeCost=4", roles);

        // Act & Assert
        credentials1.Should().Be(credentials2);
    }

    [Fact]
    public void BootstrapState_WithNullMode_IsValid()
    {
        // Arrange & Act
        var state = new BootstrapState(IsCompleted: false, Mode: null);

        // Assert
        state.IsCompleted.Should().BeFalse();
        state.Mode.Should().BeNull();
    }

    [Fact]
    public void AuditRecord_SupportsNullableFields()
    {
        // Arrange & Act
        var record = new AuditRecord(
            Id: 1,
            UserId: "user-123",
            Action: "login_failure",
            Details: null,
            OldValue: null,
            NewValue: null,
            ActorId: null,
            IpAddress: null,
            UserAgent: null,
            OccurredAt: DateTimeOffset.UtcNow);

        // Assert
        record.Details.Should().BeNull();
        record.OldValue.Should().BeNull();
        record.NewValue.Should().BeNull();
        record.ActorId.Should().BeNull();
        record.IpAddress.Should().BeNull();
        record.UserAgent.Should().BeNull();
    }

    [Fact]
    public void AuthUser_WithMultipleRoles_MaintainsOrder()
    {
        // Arrange
        var roles = new List<string> { "Admin", "User", "Viewer", "Editor" };

        // Act
        var user = new AuthUser(
            Id: "user-123",
            Username: "testuser",
            Email: null,
            IsActive: true,
            IsLocked: false,
            Roles: roles);

        // Assert
        user.Roles.Should().ContainInOrder(roles);
        user.Roles.Should().HaveCount(4);
    }
}
