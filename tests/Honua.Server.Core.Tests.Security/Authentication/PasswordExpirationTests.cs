using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Honua.Server.Core.Authentication;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Data.Auth;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Tests.Security.Authentication;

[Collection("UnitTests")]
[Trait("Category", "Integration")]
public sealed class PasswordExpirationTests
{
    [Fact]
    public async Task AuthenticateAsync_WithExpiredPassword_ReturnsPasswordExpiredStatus()
    {
        // Arrange
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("Secret123!");
        var now = DateTimeOffset.UtcNow;

        var credentials = new AuthUserCredentials(
            Id: "user-1",
            Username: "admin",
            Email: null,
            IsActive: true,
            IsLocked: false,
            IsServiceAccount: false,
            FailedAttempts: 0,
            LastFailedAt: null,
            LastLoginAt: null,
            PasswordChangedAt: now.AddDays(-100),
            PasswordExpiresAt: now.AddDays(-10), // Expired 10 days ago
            PasswordHash: hash.Hash,
            PasswordSalt: hash.Salt,
            HashAlgorithm: hash.Algorithm,
            HashParameters: hash.Parameters,
            Roles: new[] { "administrator" });

        var repository = new FakeRepository(credentials);
        var tokenService = new FakeTokenService();
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local,
            Local = new HonuaAuthenticationOptions.LocalOptions
            {
                PasswordExpiration = new HonuaAuthenticationOptions.PasswordExpirationOptions
                {
                    Enabled = true,
                    ExpirationPeriod = TimeSpan.FromDays(90),
                    GracePeriodAfterExpiration = TimeSpan.Zero
                }
            }
        };

        var service = new LocalAuthenticationService(
            repository,
            passwordHasher,
            tokenService,
            new TestOptionsMonitor(options),
            new PasswordComplexityValidator(),
            NullLogger<LocalAuthenticationService>.Instance);

        // Act
        var result = await service.AuthenticateAsync("admin", "Secret123!");

        // Assert
        result.Status.Should().Be(LocalAuthenticationStatus.PasswordExpired);
        result.Token.Should().BeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_WithPasswordExpiringSoon_ReturnsSuccessWithWarning()
    {
        // Arrange
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("Secret123!");
        var now = DateTimeOffset.UtcNow;

        var credentials = new AuthUserCredentials(
            Id: "user-1",
            Username: "admin",
            Email: null,
            IsActive: true,
            IsLocked: false,
            IsServiceAccount: false,
            FailedAttempts: 0,
            LastFailedAt: null,
            LastLoginAt: null,
            PasswordChangedAt: now.AddDays(-85),
            PasswordExpiresAt: now.AddDays(5), // Expires in 5 days
            PasswordHash: hash.Hash,
            PasswordSalt: hash.Salt,
            HashAlgorithm: hash.Algorithm,
            HashParameters: hash.Parameters,
            Roles: new[] { "administrator" });

        var repository = new FakeRepository(credentials);
        var tokenService = new FakeTokenService();
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local,
            Local = new HonuaAuthenticationOptions.LocalOptions
            {
                PasswordExpiration = new HonuaAuthenticationOptions.PasswordExpirationOptions
                {
                    Enabled = true,
                    ExpirationPeriod = TimeSpan.FromDays(90),
                    FirstWarningThreshold = TimeSpan.FromDays(7)
                }
            }
        };

        var service = new LocalAuthenticationService(
            repository,
            passwordHasher,
            tokenService,
            new TestOptionsMonitor(options),
            new PasswordComplexityValidator(),
            NullLogger<LocalAuthenticationService>.Instance);

        // Act
        var result = await service.AuthenticateAsync("admin", "Secret123!");

        // Assert
        result.Status.Should().Be(LocalAuthenticationStatus.PasswordExpiresSoon);
        result.Token.Should().NotBeNull();
        result.PasswordExpiresAt.Should().NotBeNull();
        result.DaysUntilExpiration.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AuthenticateAsync_WithPasswordNotExpiringSoon_ReturnsSuccess()
    {
        // Arrange
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("Secret123!");
        var now = DateTimeOffset.UtcNow;

        var credentials = new AuthUserCredentials(
            Id: "user-1",
            Username: "admin",
            Email: null,
            IsActive: true,
            IsLocked: false,
            IsServiceAccount: false,
            FailedAttempts: 0,
            LastFailedAt: null,
            LastLoginAt: null,
            PasswordChangedAt: now.AddDays(-30),
            PasswordExpiresAt: now.AddDays(60), // Expires in 60 days
            PasswordHash: hash.Hash,
            PasswordSalt: hash.Salt,
            HashAlgorithm: hash.Algorithm,
            HashParameters: hash.Parameters,
            Roles: new[] { "administrator" });

        var repository = new FakeRepository(credentials);
        var tokenService = new FakeTokenService();
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local,
            Local = new HonuaAuthenticationOptions.LocalOptions
            {
                PasswordExpiration = new HonuaAuthenticationOptions.PasswordExpirationOptions
                {
                    Enabled = true,
                    ExpirationPeriod = TimeSpan.FromDays(90),
                    FirstWarningThreshold = TimeSpan.FromDays(7)
                }
            }
        };

        var service = new LocalAuthenticationService(
            repository,
            passwordHasher,
            tokenService,
            new TestOptionsMonitor(options),
            new PasswordComplexityValidator(),
            NullLogger<LocalAuthenticationService>.Instance);

        // Act
        var result = await service.AuthenticateAsync("admin", "Secret123!");

        // Assert
        result.Status.Should().Be(LocalAuthenticationStatus.Success);
        result.Token.Should().NotBeNull();
        result.PasswordExpiresAt.Should().BeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_ServiceAccount_IgnoresPasswordExpiration()
    {
        // Arrange
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("Secret123!");
        var now = DateTimeOffset.UtcNow;

        var credentials = new AuthUserCredentials(
            Id: "service-1",
            Username: "service-account",
            Email: null,
            IsActive: true,
            IsLocked: false,
            IsServiceAccount: true, // Service account
            FailedAttempts: 0,
            LastFailedAt: null,
            LastLoginAt: null,
            PasswordChangedAt: now.AddDays(-100),
            PasswordExpiresAt: now.AddDays(-10), // Expired but should be ignored
            PasswordHash: hash.Hash,
            PasswordSalt: hash.Salt,
            HashAlgorithm: hash.Algorithm,
            HashParameters: hash.Parameters,
            Roles: new[] { "administrator" });

        var repository = new FakeRepository(credentials);
        var tokenService = new FakeTokenService();
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local,
            Local = new HonuaAuthenticationOptions.LocalOptions
            {
                PasswordExpiration = new HonuaAuthenticationOptions.PasswordExpirationOptions
                {
                    Enabled = true,
                    ExpirationPeriod = TimeSpan.FromDays(90)
                }
            }
        };

        var service = new LocalAuthenticationService(
            repository,
            passwordHasher,
            tokenService,
            new TestOptionsMonitor(options),
            new PasswordComplexityValidator(),
            NullLogger<LocalAuthenticationService>.Instance);

        // Act
        var result = await service.AuthenticateAsync("service-account", "Secret123!");

        // Assert
        result.Status.Should().Be(LocalAuthenticationStatus.Success);
        result.Token.Should().NotBeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_PasswordExpirationDisabled_AllowsExpiredPassword()
    {
        // Arrange
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("Secret123!");
        var now = DateTimeOffset.UtcNow;

        var credentials = new AuthUserCredentials(
            Id: "user-1",
            Username: "admin",
            Email: null,
            IsActive: true,
            IsLocked: false,
            IsServiceAccount: false,
            FailedAttempts: 0,
            LastFailedAt: null,
            LastLoginAt: null,
            PasswordChangedAt: now.AddDays(-100),
            PasswordExpiresAt: now.AddDays(-10), // Expired
            PasswordHash: hash.Hash,
            PasswordSalt: hash.Salt,
            HashAlgorithm: hash.Algorithm,
            HashParameters: hash.Parameters,
            Roles: new[] { "administrator" });

        var repository = new FakeRepository(credentials);
        var tokenService = new FakeTokenService();
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local,
            Local = new HonuaAuthenticationOptions.LocalOptions
            {
                PasswordExpiration = new HonuaAuthenticationOptions.PasswordExpirationOptions
                {
                    Enabled = false // Disabled
                }
            }
        };

        var service = new LocalAuthenticationService(
            repository,
            passwordHasher,
            tokenService,
            new TestOptionsMonitor(options),
            new PasswordComplexityValidator(),
            NullLogger<LocalAuthenticationService>.Instance);

        // Act
        var result = await service.AuthenticateAsync("admin", "Secret123!");

        // Assert
        result.Status.Should().Be(LocalAuthenticationStatus.Success);
        result.Token.Should().NotBeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_WithGracePeriod_AllowsLoginAfterExpiration()
    {
        // Arrange
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("Secret123!");
        var now = DateTimeOffset.UtcNow;

        var credentials = new AuthUserCredentials(
            Id: "user-1",
            Username: "admin",
            Email: null,
            IsActive: true,
            IsLocked: false,
            IsServiceAccount: false,
            FailedAttempts: 0,
            LastFailedAt: null,
            LastLoginAt: null,
            PasswordChangedAt: now.AddDays(-92),
            PasswordExpiresAt: now.AddDays(-2), // Expired 2 days ago
            PasswordHash: hash.Hash,
            PasswordSalt: hash.Salt,
            HashAlgorithm: hash.Algorithm,
            HashParameters: hash.Parameters,
            Roles: new[] { "administrator" });

        var repository = new FakeRepository(credentials);
        var tokenService = new FakeTokenService();
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local,
            Local = new HonuaAuthenticationOptions.LocalOptions
            {
                PasswordExpiration = new HonuaAuthenticationOptions.PasswordExpirationOptions
                {
                    Enabled = true,
                    ExpirationPeriod = TimeSpan.FromDays(90),
                    GracePeriodAfterExpiration = TimeSpan.FromDays(7) // 7 day grace period
                }
            }
        };

        var service = new LocalAuthenticationService(
            repository,
            passwordHasher,
            tokenService,
            new TestOptionsMonitor(options),
            new PasswordComplexityValidator(),
            NullLogger<LocalAuthenticationService>.Instance);

        // Act
        var result = await service.AuthenticateAsync("admin", "Secret123!");

        // Assert
        result.Status.Should().Be(LocalAuthenticationStatus.PasswordExpiresSoon);
        result.Token.Should().NotBeNull(); // Still allowed within grace period
    }

    [Fact]
    public async Task AuthenticateAsync_GracePeriodExpired_ReturnsPasswordExpired()
    {
        // Arrange
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("Secret123!");
        var now = DateTimeOffset.UtcNow;

        var credentials = new AuthUserCredentials(
            Id: "user-1",
            Username: "admin",
            Email: null,
            IsActive: true,
            IsLocked: false,
            IsServiceAccount: false,
            FailedAttempts: 0,
            LastFailedAt: null,
            LastLoginAt: null,
            PasswordChangedAt: now.AddDays(-98),
            PasswordExpiresAt: now.AddDays(-8), // Expired 8 days ago
            PasswordHash: hash.Hash,
            PasswordSalt: hash.Salt,
            HashAlgorithm: hash.Algorithm,
            HashParameters: hash.Parameters,
            Roles: new[] { "administrator" });

        var repository = new FakeRepository(credentials);
        var tokenService = new FakeTokenService();
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local,
            Local = new HonuaAuthenticationOptions.LocalOptions
            {
                PasswordExpiration = new HonuaAuthenticationOptions.PasswordExpirationOptions
                {
                    Enabled = true,
                    ExpirationPeriod = TimeSpan.FromDays(90),
                    GracePeriodAfterExpiration = TimeSpan.FromDays(7) // 7 day grace period
                }
            }
        };

        var service = new LocalAuthenticationService(
            repository,
            passwordHasher,
            tokenService,
            new TestOptionsMonitor(options),
            new PasswordComplexityValidator(),
            NullLogger<LocalAuthenticationService>.Instance);

        // Act
        var result = await service.AuthenticateAsync("admin", "Secret123!");

        // Assert
        result.Status.Should().Be(LocalAuthenticationStatus.PasswordExpired);
        result.Token.Should().BeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_WithoutPasswordExpiresAt_ReturnsSuccess()
    {
        // Arrange
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("Secret123!");

        var credentials = new AuthUserCredentials(
            Id: "user-1",
            Username: "admin",
            Email: null,
            IsActive: true,
            IsLocked: false,
            IsServiceAccount: false,
            FailedAttempts: 0,
            LastFailedAt: null,
            LastLoginAt: null,
            PasswordChangedAt: null,
            PasswordExpiresAt: null, // No expiration set
            PasswordHash: hash.Hash,
            PasswordSalt: hash.Salt,
            HashAlgorithm: hash.Algorithm,
            HashParameters: hash.Parameters,
            Roles: new[] { "administrator" });

        var repository = new FakeRepository(credentials);
        var tokenService = new FakeTokenService();
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local,
            Local = new HonuaAuthenticationOptions.LocalOptions
            {
                PasswordExpiration = new HonuaAuthenticationOptions.PasswordExpirationOptions
                {
                    Enabled = true,
                    ExpirationPeriod = TimeSpan.FromDays(90)
                }
            }
        };

        var service = new LocalAuthenticationService(
            repository,
            passwordHasher,
            tokenService,
            new TestOptionsMonitor(options),
            new PasswordComplexityValidator(),
            NullLogger<LocalAuthenticationService>.Instance);

        // Act
        var result = await service.AuthenticateAsync("admin", "Secret123!");

        // Assert
        result.Status.Should().Be(LocalAuthenticationStatus.Success);
        result.Token.Should().NotBeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_PasswordExpiresInOneDay_ShowsUrgentWarning()
    {
        // Arrange
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("Secret123!");
        var now = DateTimeOffset.UtcNow;

        var credentials = new AuthUserCredentials(
            Id: "user-1",
            Username: "admin",
            Email: null,
            IsActive: true,
            IsLocked: false,
            IsServiceAccount: false,
            FailedAttempts: 0,
            LastFailedAt: null,
            LastLoginAt: null,
            PasswordChangedAt: now.AddDays(-89),
            PasswordExpiresAt: now.AddHours(20), // Expires in less than 1 day
            PasswordHash: hash.Hash,
            PasswordSalt: hash.Salt,
            HashAlgorithm: hash.Algorithm,
            HashParameters: hash.Parameters,
            Roles: new[] { "administrator" });

        var repository = new FakeRepository(credentials);
        var tokenService = new FakeTokenService();
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local,
            Local = new HonuaAuthenticationOptions.LocalOptions
            {
                PasswordExpiration = new HonuaAuthenticationOptions.PasswordExpirationOptions
                {
                    Enabled = true,
                    ExpirationPeriod = TimeSpan.FromDays(90),
                    FirstWarningThreshold = TimeSpan.FromDays(7),
                    UrgentWarningThreshold = TimeSpan.FromDays(1)
                }
            }
        };

        var service = new LocalAuthenticationService(
            repository,
            passwordHasher,
            tokenService,
            new TestOptionsMonitor(options),
            new PasswordComplexityValidator(),
            NullLogger<LocalAuthenticationService>.Instance);

        // Act
        var result = await service.AuthenticateAsync("admin", "Secret123!");

        // Assert
        result.Status.Should().Be(LocalAuthenticationStatus.PasswordExpiresSoon);
        result.Token.Should().NotBeNull();
        result.DaysUntilExpiration.Should().Be(1);
    }

    [Fact]
    public async Task AuthenticateAsync_InvalidPassword_StillReturnsInvalidCredentials()
    {
        // Arrange
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("Secret123!");
        var now = DateTimeOffset.UtcNow;

        var credentials = new AuthUserCredentials(
            Id: "user-1",
            Username: "admin",
            Email: null,
            IsActive: true,
            IsLocked: false,
            IsServiceAccount: false,
            FailedAttempts: 0,
            LastFailedAt: null,
            LastLoginAt: null,
            PasswordChangedAt: now.AddDays(-100),
            PasswordExpiresAt: now.AddDays(-10), // Expired
            PasswordHash: hash.Hash,
            PasswordSalt: hash.Salt,
            HashAlgorithm: hash.Algorithm,
            HashParameters: hash.Parameters,
            Roles: new[] { "administrator" });

        var repository = new FakeRepository(credentials);
        var tokenService = new FakeTokenService();
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local,
            Local = new HonuaAuthenticationOptions.LocalOptions
            {
                PasswordExpiration = new HonuaAuthenticationOptions.PasswordExpirationOptions
                {
                    Enabled = true,
                    ExpirationPeriod = TimeSpan.FromDays(90)
                }
            }
        };

        var service = new LocalAuthenticationService(
            repository,
            passwordHasher,
            tokenService,
            new TestOptionsMonitor(options),
            new PasswordComplexityValidator(),
            NullLogger<LocalAuthenticationService>.Instance);

        // Act
        var result = await service.AuthenticateAsync("admin", "WrongPassword!");

        // Assert - Password expiration check happens before password verification
        result.Status.Should().Be(LocalAuthenticationStatus.PasswordExpired);
        result.Token.Should().BeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_PasswordExpiredButCorrectCredentials_ReturnsPasswordExpired()
    {
        // Arrange
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("Secret123!");
        var now = DateTimeOffset.UtcNow;

        var credentials = new AuthUserCredentials(
            Id: "user-1",
            Username: "admin",
            Email: null,
            IsActive: true,
            IsLocked: false,
            IsServiceAccount: false,
            FailedAttempts: 0,
            LastFailedAt: null,
            LastLoginAt: null,
            PasswordChangedAt: now.AddDays(-91),
            PasswordExpiresAt: now.AddDays(-1), // Expired yesterday
            PasswordHash: hash.Hash,
            PasswordSalt: hash.Salt,
            HashAlgorithm: hash.Algorithm,
            HashParameters: hash.Parameters,
            Roles: new[] { "administrator" });

        var repository = new FakeRepository(credentials);
        var tokenService = new FakeTokenService();
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local,
            Local = new HonuaAuthenticationOptions.LocalOptions
            {
                PasswordExpiration = new HonuaAuthenticationOptions.PasswordExpirationOptions
                {
                    Enabled = true,
                    ExpirationPeriod = TimeSpan.FromDays(90),
                    GracePeriodAfterExpiration = TimeSpan.Zero
                }
            }
        };

        var service = new LocalAuthenticationService(
            repository,
            passwordHasher,
            tokenService,
            new TestOptionsMonitor(options),
            new PasswordComplexityValidator(),
            NullLogger<LocalAuthenticationService>.Instance);

        // Act
        var result = await service.AuthenticateAsync("admin", "Secret123!");

        // Assert
        result.Status.Should().Be(LocalAuthenticationStatus.PasswordExpired);
        result.Token.Should().BeNull();
        repository.SuccessCalls.Should().Be(0); // Should not call UpdateLoginSuccess
    }

    // Helper classes
    private sealed class FakeRepository : IAuthRepository
    {
        private readonly AuthUserCredentials? _credentials;

        public FakeRepository(AuthUserCredentials? credentials)
        {
            _credentials = credentials;
        }

        public int SuccessCalls { get; private set; }

        public (int failedAttempts, bool locked)? LastFailure { get; private set; }

        public ValueTask EnsureInitializedAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<BootstrapState> GetBootstrapStateAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(new BootstrapState(false, null));

        public ValueTask MarkBootstrapCompletedAsync(string mode, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask CreateLocalAdministratorAsync(string username, string? email, byte[] passwordHash, byte[] salt, string hashAlgorithm, string hashParameters, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask CreateOidcAdministratorAsync(string subject, string? username, string? email, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<AuthUserCredentials?> GetCredentialsByUsernameAsync(string username, CancellationToken cancellationToken = default)
        {
            if (_credentials is null)
            {
                return ValueTask.FromResult<AuthUserCredentials?>(null);
            }

            if (!string.Equals(username, _credentials.Username, StringComparison.OrdinalIgnoreCase))
            {
                return ValueTask.FromResult<AuthUserCredentials?>(null);
            }

            return ValueTask.FromResult<AuthUserCredentials?>(_credentials);
        }

        public ValueTask<AuthUserCredentials?> GetCredentialsByIdAsync(string userId, CancellationToken cancellationToken = default)
        {
            if (_credentials is null || !string.Equals(userId, _credentials.Id, StringComparison.OrdinalIgnoreCase))
            {
                return ValueTask.FromResult<AuthUserCredentials?>(null);
            }

            return ValueTask.FromResult<AuthUserCredentials?>(_credentials);
        }

        public ValueTask UpdateLoginFailureAsync(string userId, int failedAttempts, DateTimeOffset failedAtUtc, bool lockUser, AuditContext? auditContext = null, CancellationToken cancellationToken = default)
        {
            LastFailure = (failedAttempts, lockUser);
            return ValueTask.CompletedTask;
        }

        public ValueTask UpdateLoginSuccessAsync(string userId, DateTimeOffset loginAtUtc, AuditContext? auditContext = null, CancellationToken cancellationToken = default)
        {
            SuccessCalls++;
            return ValueTask.CompletedTask;
        }

        public ValueTask<string> CreateLocalUserAsync(string username, string? email, byte[] passwordHash, byte[] salt, string hashAlgorithm, string hashParameters, IReadOnlyCollection<string> roles, AuditContext? auditContext = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public ValueTask SetLocalUserPasswordAsync(string userId, byte[] passwordHash, byte[] salt, string hashAlgorithm, string hashParameters, AuditContext? auditContext = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public ValueTask AssignRolesAsync(string userId, IReadOnlyCollection<string> roles, AuditContext? auditContext = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public ValueTask<AuthUser?> GetUserAsync(string username, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public ValueTask<IReadOnlyList<AuditRecord>> GetAuditRecordsAsync(string userId, int limit = 100, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<IReadOnlyList<AuditRecord>>(Array.Empty<AuditRecord>());
        }

        public ValueTask<IReadOnlyList<AuditRecord>> GetAuditRecordsByActionAsync(string action, int limit = 100, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<IReadOnlyList<AuditRecord>>(Array.Empty<AuditRecord>());
        }

        public ValueTask<IReadOnlyList<AuditRecord>> GetRecentFailedAuthenticationsAsync(TimeSpan window, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<IReadOnlyList<AuditRecord>>(Array.Empty<AuditRecord>());
        }

        public ValueTask<int> PurgeOldAuditRecordsAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(0);
        }
    }

    private sealed class FakeTokenService : ILocalTokenService
    {
        public Task<string> CreateTokenAsync(string subject, IReadOnlyCollection<string> roles, TimeSpan? lifetime = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult($"token:{subject}");
        }
    }

    private sealed class TestOptionsMonitor : IOptionsMonitor<HonuaAuthenticationOptions>
    {
        private HonuaAuthenticationOptions _current;

        public TestOptionsMonitor(HonuaAuthenticationOptions value)
        {
            _current = value;
        }

        public HonuaAuthenticationOptions CurrentValue => _current;

        public HonuaAuthenticationOptions Get(string? name) => _current;

        public IDisposable OnChange(Action<HonuaAuthenticationOptions, string> listener) => new NoopDisposable();

        private sealed class NoopDisposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
