using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Authentication;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Data.Auth;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Honua.Server.Core.Tests.Security.Authentication;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class AuthAuditTrailTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteAuthRepository _repository;

    public AuthAuditTrailTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"auth_audit_test_{Guid.NewGuid():N}.db");
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local,
            Local = { StorePath = _testDbPath }
        };

        _repository = new SqliteAuthRepository(
            Path.GetTempPath(),
            new TestOptionsMonitor(options),
            NullLogger<SqliteAuthRepository>.Instance);
    }

    public void Dispose()
    {
        if (File.Exists(_testDbPath))
        {
            File.Delete(_testDbPath);
        }
    }

    [Fact]
    public async Task UpdateLoginFailureAsync_WritesAuditRecord()
    {
        // Arrange
        await _repository.EnsureInitializedAsync();
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("Test123!");

        var userId = await _repository.CreateLocalUserAsync(
            "testuser",
            "test@example.com",
            hash.Hash,
            hash.Salt,
            hash.Algorithm,
            hash.Parameters,
            Array.Empty<string>());

        var auditContext = new AuditContext(
            ActorId: userId,
            IpAddress: "192.168.1.100",
            UserAgent: "TestAgent/1.0");

        // Act
        await _repository.UpdateLoginFailureAsync(userId, 1, DateTimeOffset.UtcNow, false, auditContext);

        // Assert
        var auditRecords = await _repository.GetAuditRecordsAsync(userId, 100);
        auditRecords.Should().HaveCountGreaterThan(0);

        var loginFailureRecord = auditRecords.FirstOrDefault(r => r.Action == "login_failed");
        loginFailureRecord.Should().NotBeNull();
        loginFailureRecord!.IpAddress.Should().Be("192.168.1.100");
        loginFailureRecord.UserAgent.Should().Be("TestAgent/1.0");
        loginFailureRecord.Details.Should().Be("Login attempt failed.");
    }

    [Fact]
    public async Task UpdateLoginFailureAsync_WithLockout_WritesAccountLockedAudit()
    {
        // Arrange
        await _repository.EnsureInitializedAsync();
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("Test123!");

        var userId = await _repository.CreateLocalUserAsync(
            "testuser2",
            "test2@example.com",
            hash.Hash,
            hash.Salt,
            hash.Algorithm,
            hash.Parameters,
            Array.Empty<string>());

        var auditContext = new AuditContext(
            ActorId: userId,
            IpAddress: "192.168.1.101",
            UserAgent: "TestAgent/1.0");

        // Act
        await _repository.UpdateLoginFailureAsync(userId, 5, DateTimeOffset.UtcNow, true, auditContext);

        // Assert
        var auditRecords = await _repository.GetAuditRecordsAsync(userId, 100);
        var lockoutRecord = auditRecords.FirstOrDefault(r => r.Action == "account_locked");
        lockoutRecord.Should().NotBeNull();
        lockoutRecord!.Details.Should().Contain("Account locked");
        lockoutRecord.IpAddress.Should().Be("192.168.1.101");
    }

    [Fact]
    public async Task UpdateLoginSuccessAsync_WritesAuditRecord()
    {
        // Arrange
        await _repository.EnsureInitializedAsync();
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("Test123!");

        var userId = await _repository.CreateLocalUserAsync(
            "testuser3",
            "test3@example.com",
            hash.Hash,
            hash.Salt,
            hash.Algorithm,
            hash.Parameters,
            Array.Empty<string>());

        var auditContext = new AuditContext(
            ActorId: userId,
            IpAddress: "10.0.0.1",
            UserAgent: "Chrome/100.0");

        // Act
        await _repository.UpdateLoginSuccessAsync(userId, DateTimeOffset.UtcNow, auditContext);

        // Assert
        var auditRecords = await _repository.GetAuditRecordsAsync(userId, 100);
        var successRecord = auditRecords.FirstOrDefault(r => r.Action == "login_success");
        successRecord.Should().NotBeNull();
        successRecord!.IpAddress.Should().Be("10.0.0.1");
        successRecord.UserAgent.Should().Be("Chrome/100.0");
        successRecord.Details.Should().Be("Successful authentication.");
    }

    [Fact]
    public async Task SetLocalUserPasswordAsync_WritesAuditRecord()
    {
        // Arrange
        await _repository.EnsureInitializedAsync();
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("Test123!");

        var userId = await _repository.CreateLocalUserAsync(
            "testuser4",
            "test4@example.com",
            hash.Hash,
            hash.Salt,
            hash.Algorithm,
            hash.Parameters,
            Array.Empty<string>());

        var newHash = passwordHasher.HashPassword("NewPassword123!");
        var auditContext = new AuditContext(
            ActorId: "admin-user",
            IpAddress: "10.0.0.5",
            UserAgent: "AdminPanel/1.0");

        // Act
        await _repository.SetLocalUserPasswordAsync(
            userId,
            newHash.Hash,
            newHash.Salt,
            newHash.Algorithm,
            newHash.Parameters,
            auditContext);

        // Assert
        var auditRecords = await _repository.GetAuditRecordsAsync(userId, 100);
        var passwordChangeRecord = auditRecords.FirstOrDefault(r => r.Action == "password_changed");
        passwordChangeRecord.Should().NotBeNull();
        passwordChangeRecord!.ActorId.Should().Be("admin-user");
        passwordChangeRecord.IpAddress.Should().Be("10.0.0.5");
        passwordChangeRecord.Details.Should().Be("User password updated.");
    }

    [Fact]
    public async Task AssignRolesAsync_WritesAuditRecordWithOldAndNewValues()
    {
        // Arrange
        await _repository.EnsureInitializedAsync();
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("Test123!");

        var userId = await _repository.CreateLocalUserAsync(
            "testuser5",
            "test5@example.com",
            hash.Hash,
            hash.Salt,
            hash.Algorithm,
            hash.Parameters,
            new[] { "viewer" });

        var auditContext = new AuditContext(
            ActorId: "admin-user",
            IpAddress: "10.0.0.10");

        // Act
        await _repository.AssignRolesAsync(userId, new[] { "administrator", "datapublisher" }, auditContext);

        // Assert
        var auditRecords = await _repository.GetAuditRecordsAsync(userId, 100);
        var rolesChangeRecord = auditRecords.FirstOrDefault(r => r.Action == "roles_changed");
        rolesChangeRecord.Should().NotBeNull();
        rolesChangeRecord!.OldValue.Should().Be("viewer");
        rolesChangeRecord.NewValue.Should().Be("administrator, datapublisher");
        rolesChangeRecord.ActorId.Should().Be("admin-user");
    }

    [Fact]
    public async Task CreateLocalUserAsync_WritesAuditRecord()
    {
        // Arrange
        await _repository.EnsureInitializedAsync();
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("Test123!");

        var auditContext = new AuditContext(
            ActorId: "admin-user",
            IpAddress: "10.0.0.20");

        // Act
        var userId = await _repository.CreateLocalUserAsync(
            "newuser",
            "new@example.com",
            hash.Hash,
            hash.Salt,
            hash.Algorithm,
            hash.Parameters,
            new[] { "viewer" },
            auditContext);

        // Assert
        var auditRecords = await _repository.GetAuditRecordsAsync(userId, 100);
        var userCreatedRecord = auditRecords.FirstOrDefault(r => r.Action == "user_created");
        userCreatedRecord.Should().NotBeNull();
        userCreatedRecord!.ActorId.Should().Be("admin-user");
        userCreatedRecord.Details.Should().Be("Local user account created.");
        userCreatedRecord.NewValue.Should().Be("viewer");
    }

    [Fact]
    public async Task GetAuditRecordsByActionAsync_ReturnsFilteredRecords()
    {
        // Arrange
        await _repository.EnsureInitializedAsync();
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("Test123!");

        var userId1 = await _repository.CreateLocalUserAsync("user1", "u1@test.com", hash.Hash, hash.Salt, hash.Algorithm, hash.Parameters, Array.Empty<string>());
        var userId2 = await _repository.CreateLocalUserAsync("user2", "u2@test.com", hash.Hash, hash.Salt, hash.Algorithm, hash.Parameters, Array.Empty<string>());

        await _repository.UpdateLoginSuccessAsync(userId1, DateTimeOffset.UtcNow);
        await _repository.UpdateLoginSuccessAsync(userId2, DateTimeOffset.UtcNow);
        await _repository.UpdateLoginFailureAsync(userId1, 1, DateTimeOffset.UtcNow, false);

        // Act
        var successRecords = await _repository.GetAuditRecordsByActionAsync("login_success", 100);
        var failureRecords = await _repository.GetAuditRecordsByActionAsync("login_failed", 100);

        // Assert
        successRecords.Should().HaveCountGreaterThanOrEqualTo(2);
        failureRecords.Should().HaveCountGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task GetRecentFailedAuthenticationsAsync_ReturnsOnlyRecentFailures()
    {
        // Arrange
        await _repository.EnsureInitializedAsync();
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("Test123!");

        var userId = await _repository.CreateLocalUserAsync("user_fail", "fail@test.com", hash.Hash, hash.Salt, hash.Algorithm, hash.Parameters, Array.Empty<string>());

        await _repository.UpdateLoginFailureAsync(userId, 1, DateTimeOffset.UtcNow, false);
        await _repository.UpdateLoginFailureAsync(userId, 2, DateTimeOffset.UtcNow, false);

        // Act
        var recentFailures = await _repository.GetRecentFailedAuthenticationsAsync(TimeSpan.FromHours(1));

        // Assert
        recentFailures.Should().HaveCountGreaterThanOrEqualTo(2);
        recentFailures.All(r => r.Action is "login_failed" or "account_locked").Should().BeTrue();
    }

    [Fact]
    public async Task PurgeOldAuditRecordsAsync_DeletesOldRecords()
    {
        // Arrange
        await _repository.EnsureInitializedAsync();
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("Test123!");

        var userId = await _repository.CreateLocalUserAsync("user_purge", "purge@test.com", hash.Hash, hash.Salt, hash.Algorithm, hash.Parameters, Array.Empty<string>());

        // Create some audit records
        await _repository.UpdateLoginSuccessAsync(userId, DateTimeOffset.UtcNow);
        await _repository.UpdateLoginFailureAsync(userId, 1, DateTimeOffset.UtcNow, false);

        var recordsBefore = await _repository.GetAuditRecordsAsync(userId, 100);
        var countBefore = recordsBefore.Count;

        // Act - purge records older than 0 seconds (should delete all)
        var deletedCount = await _repository.PurgeOldAuditRecordsAsync(TimeSpan.FromSeconds(-1));

        // Assert
        deletedCount.Should().BeGreaterThanOrEqualTo(countBefore);
    }

    [Fact]
    public async Task AuditRecords_ContainAllRequiredFields()
    {
        // Arrange
        await _repository.EnsureInitializedAsync();
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("Test123!");

        var userId = await _repository.CreateLocalUserAsync(
            "fieldtest",
            "fields@test.com",
            hash.Hash,
            hash.Salt,
            hash.Algorithm,
            hash.Parameters,
            Array.Empty<string>());

        var auditContext = new AuditContext(
            ActorId: "test-actor",
            IpAddress: "203.0.113.42",
            UserAgent: "Mozilla/5.0");

        await _repository.UpdateLoginSuccessAsync(userId, DateTimeOffset.UtcNow, auditContext);

        // Act
        var auditRecords = await _repository.GetAuditRecordsAsync(userId, 100);
        var record = auditRecords.First(r => r.Action == "login_success");

        // Assert
        record.Id.Should().BeGreaterThan(0);
        record.UserId.Should().Be(userId);
        record.Action.Should().Be("login_success");
        record.ActorId.Should().Be("test-actor");
        record.IpAddress.Should().Be("203.0.113.42");
        record.UserAgent.Should().Be("Mozilla/5.0");
        record.OccurredAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task GetAuditRecordsAsync_RespectsLimit()
    {
        // Arrange
        await _repository.EnsureInitializedAsync();
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("Test123!");

        var userId = await _repository.CreateLocalUserAsync("limittest", "limit@test.com", hash.Hash, hash.Salt, hash.Algorithm, hash.Parameters, Array.Empty<string>());

        // Create multiple audit records
        for (int i = 0; i < 10; i++)
        {
            await _repository.UpdateLoginSuccessAsync(userId, DateTimeOffset.UtcNow);
        }

        // Act
        var limitedRecords = await _repository.GetAuditRecordsAsync(userId, 5);

        // Assert
        limitedRecords.Count.Should().BeLessThanOrEqualTo(5);
    }

    [Fact]
    public async Task AuditRecords_OrderedByOccurredAtDescending()
    {
        // Arrange
        await _repository.EnsureInitializedAsync();
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("Test123!");

        var userId = await _repository.CreateLocalUserAsync("ordertest", "order@test.com", hash.Hash, hash.Salt, hash.Algorithm, hash.Parameters, Array.Empty<string>());

        await _repository.UpdateLoginFailureAsync(userId, 1, DateTimeOffset.UtcNow.AddMinutes(-2), false);
        await Task.Delay(100);
        await _repository.UpdateLoginFailureAsync(userId, 2, DateTimeOffset.UtcNow.AddMinutes(-1), false);
        await Task.Delay(100);
        await _repository.UpdateLoginSuccessAsync(userId, DateTimeOffset.UtcNow);

        // Act
        var records = await _repository.GetAuditRecordsAsync(userId, 100);

        // Assert
        records.Should().HaveCountGreaterThanOrEqualTo(3);
        for (int i = 0; i < records.Count - 1; i++)
        {
            records[i].OccurredAt.Should().BeOnOrAfter(records[i + 1].OccurredAt.AddMilliseconds(-5));
        }
    }

    private sealed class TestOptionsMonitor : IOptionsMonitor<HonuaAuthenticationOptions>
    {
        private readonly HonuaAuthenticationOptions _current;

        public TestOptionsMonitor(HonuaAuthenticationOptions value)
        {
            _current = value;
        }

        public HonuaAuthenticationOptions CurrentValue => _current;
        public HonuaAuthenticationOptions Get(string? name) => _current;
        public IDisposable OnChange(Action<HonuaAuthenticationOptions, string> listener) => new NoopDisposable();

        private sealed class NoopDisposable : IDisposable
        {
            public void Dispose() { }
        }
    }
}
