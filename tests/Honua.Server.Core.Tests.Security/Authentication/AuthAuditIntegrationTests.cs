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

/// <summary>
/// Integration tests that verify the complete audit trail flow with LocalAuthenticationService.
/// </summary>
[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class AuthAuditIntegrationTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly SqliteAuthRepository _repository;
    private readonly LocalAuthenticationService _authService;

    public AuthAuditIntegrationTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"auth_audit_integration_test_{Guid.NewGuid():N}.db");
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local,
            Local =
            {
                StorePath = _testDbPath,
                MaxFailedAttempts = 3,
                LockoutDuration = TimeSpan.FromMinutes(15)
            }
        };

        _repository = new SqliteAuthRepository(
            Path.GetTempPath(),
            new TestOptionsMonitor(options),
            NullLogger<SqliteAuthRepository>.Instance);

        var passwordHasher = new PasswordHasher();
        var tokenService = new FakeTokenService();

        _authService = new LocalAuthenticationService(
            _repository,
            passwordHasher,
            tokenService,
            new TestOptionsMonitor(options),
            new PasswordComplexityValidator(),
            NullLogger<LocalAuthenticationService>.Instance);
    }

    public void Dispose()
    {
        if (File.Exists(_testDbPath))
        {
            File.Delete(_testDbPath);
        }
    }

    [Fact]
    public async Task SuccessfulAuthentication_CreatesAuditTrail()
    {
        // Arrange
        await _repository.EnsureInitializedAsync();
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("Test123!");

        var userId = await _repository.CreateLocalUserAsync(
            "inttest1",
            "int1@test.com",
            hash.Hash,
            hash.Salt,
            hash.Algorithm,
            hash.Parameters,
            new[] { "viewer" });

        // Act
        var result = await _authService.AuthenticateAsync("inttest1", "Test123!");

        // Assert
        result.Status.Should().Be(LocalAuthenticationStatus.Success);

        var auditRecords = await _repository.GetAuditRecordsAsync(userId, 100);
        var loginRecord = auditRecords.FirstOrDefault(r => r.Action == "login_success");
        loginRecord.Should().NotBeNull();
    }

    [Fact]
    public async Task FailedAuthentication_CreatesAuditTrail()
    {
        // Arrange
        await _repository.EnsureInitializedAsync();
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("Test123!");

        var userId = await _repository.CreateLocalUserAsync(
            "inttest2",
            "int2@test.com",
            hash.Hash,
            hash.Salt,
            hash.Algorithm,
            hash.Parameters,
            new[] { "viewer" });

        // Act
        var result = await _authService.AuthenticateAsync("inttest2", "WrongPassword!");

        // Assert
        result.Status.Should().Be(LocalAuthenticationStatus.InvalidCredentials);

        var auditRecords = await _repository.GetAuditRecordsAsync(userId, 100);
        var failureRecord = auditRecords.FirstOrDefault(r => r.Action == "login_failed");
        failureRecord.Should().NotBeNull();
        failureRecord!.Details.Should().Be("Login attempt failed.");
    }

    [Fact]
    public async Task AccountLockout_CreatesAuditTrail()
    {
        // Arrange
        await _repository.EnsureInitializedAsync();
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("Test123!");

        var userId = await _repository.CreateLocalUserAsync(
            "inttest3",
            "int3@test.com",
            hash.Hash,
            hash.Salt,
            hash.Algorithm,
            hash.Parameters,
            new[] { "viewer" });

        // Act - fail authentication 3 times to trigger lockout
        await _authService.AuthenticateAsync("inttest3", "Wrong1!");
        await _authService.AuthenticateAsync("inttest3", "Wrong2!");
        var lockoutResult = await _authService.AuthenticateAsync("inttest3", "Wrong3!");

        // Assert
        lockoutResult.Status.Should().Be(LocalAuthenticationStatus.LockedOut);

        var auditRecords = await _repository.GetAuditRecordsAsync(userId, 100);
        var lockoutRecord = auditRecords.FirstOrDefault(r => r.Action == "account_locked");
        lockoutRecord.Should().NotBeNull();
        lockoutRecord!.Details.Should().Contain("Account locked");
    }

    [Fact]
    public async Task GetRecentFailedAuthentications_ShowsAttackPatterns()
    {
        // Arrange
        await _repository.EnsureInitializedAsync();
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("Test123!");

        var user1 = await _repository.CreateLocalUserAsync("attack1", "a1@test.com", hash.Hash, hash.Salt, hash.Algorithm, hash.Parameters, Array.Empty<string>());
        var user2 = await _repository.CreateLocalUserAsync("attack2", "a2@test.com", hash.Hash, hash.Salt, hash.Algorithm, hash.Parameters, Array.Empty<string>());

        var auditContext1 = new AuditContext(IpAddress: "192.168.1.100");
        var auditContext2 = new AuditContext(IpAddress: "192.168.1.100"); // Same IP

        // Act - simulate attack from same IP
        await _repository.UpdateLoginFailureAsync(user1, 1, DateTimeOffset.UtcNow, false, auditContext1);
        await _repository.UpdateLoginFailureAsync(user2, 1, DateTimeOffset.UtcNow, false, auditContext2);
        await _repository.UpdateLoginFailureAsync(user1, 2, DateTimeOffset.UtcNow, false, auditContext1);

        var recentFailures = await _repository.GetRecentFailedAuthenticationsAsync(TimeSpan.FromMinutes(5));

        // Assert
        recentFailures.Should().HaveCountGreaterThanOrEqualTo(3);
        var attackerIpRecords = recentFailures.Where(r => r.IpAddress == "192.168.1.100").ToList();
        attackerIpRecords.Should().HaveCountGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task AuditRetention_PurgesOldRecords()
    {
        // Arrange
        await _repository.EnsureInitializedAsync();
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("Test123!");

        var userId = await _repository.CreateLocalUserAsync("retention", "ret@test.com", hash.Hash, hash.Salt, hash.Algorithm, hash.Parameters, Array.Empty<string>());

        // Create audit records
        await _repository.UpdateLoginSuccessAsync(userId, DateTimeOffset.UtcNow);
        await _repository.UpdateLoginSuccessAsync(userId, DateTimeOffset.UtcNow);

        var before = await _repository.GetAuditRecordsAsync(userId, 100);

        // Act - purge everything older than a negative retention period
        // This sets the cutoff to a future time, ensuring all current records are purged
        await Task.Delay(100); // Ensure a small gap
        await _repository.PurgeOldAuditRecordsAsync(TimeSpan.FromSeconds(-1));

        var after = await _repository.GetAuditRecordsAsync(userId, 100);

        // Assert
        before.Should().NotBeEmpty();
        after.Should().BeEmpty();
    }

    private sealed class FakeTokenService : ILocalTokenService
    {
        public Task<string> CreateTokenAsync(string subject, System.Collections.Generic.IReadOnlyCollection<string> roles, TimeSpan? lifetime = null, System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.FromResult($"token:{subject}");
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
