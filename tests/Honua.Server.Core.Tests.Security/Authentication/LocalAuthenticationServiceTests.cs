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
public sealed class LocalAuthenticationServiceTests
{
    private static LocalAuthenticationService CreateService(
        IAuthRepository repository,
        IPasswordHasher passwordHasher,
        ILocalTokenService tokenService,
        HonuaAuthenticationOptions options)
    {
        return new LocalAuthenticationService(
            repository,
            passwordHasher,
            tokenService,
            new TestOptionsMonitor(options),
            new PasswordComplexityValidator(),
            NullLogger<LocalAuthenticationService>.Instance);
    }

    [Fact]
    public async Task AuthenticateAsync_WithValidCredentials_ReturnsSuccessToken()
    {
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
            PasswordExpiresAt: null,
            PasswordHash: hash.Hash,
            PasswordSalt: hash.Salt,
            HashAlgorithm: hash.Algorithm,
            HashParameters: hash.Parameters,
            Roles: new[] { "administrator" });

        var repository = new FakeRepository(credentials);
        var tokenService = new FakeTokenService();
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local
        };

        var service = CreateService(repository, passwordHasher, tokenService, options);

        var result = await service.AuthenticateAsync("admin", "Secret123!");

        result.Status.Should().Be(LocalAuthenticationStatus.Success);
        result.Token.Should().Be("token:user-1");
        result.Roles.Should().Contain("administrator");
        repository.SuccessCalls.Should().Be(1);
        repository.LastFailure.Should().BeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_WithInvalidPassword_IncrementsFailure()
    {
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("Secret123!");

        var credentials = new AuthUserCredentials(
            Id: "user-1",
            Username: "admin",
            Email: null,
            IsActive: true,
            IsLocked: false,
            IsServiceAccount: false,
            FailedAttempts: 1,
            LastFailedAt: DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMinutes(1)),
            LastLoginAt: null,
            PasswordChangedAt: null,
            PasswordExpiresAt: null,
            PasswordHash: hash.Hash,
            PasswordSalt: hash.Salt,
            HashAlgorithm: hash.Algorithm,
            HashParameters: hash.Parameters,
            Roles: Array.Empty<string>());

        var repository = new FakeRepository(credentials);
        var tokenService = new FakeTokenService();
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local,
            Local =
            {
                MaxFailedAttempts = 2,
                LockoutDuration = TimeSpan.FromMinutes(15)
            }
        };

        var service = CreateService(repository, passwordHasher, tokenService, options);

        var result = await service.AuthenticateAsync("admin", "WrongPassword!");

        result.Status.Should().Be(LocalAuthenticationStatus.LockedOut);
        repository.LastFailure.Should().NotBeNull();
        repository.LastFailure!.Value.failedAttempts.Should().Be(2);
        repository.LastFailure!.Value.locked.Should().BeTrue();
        repository.SuccessCalls.Should().Be(0);
    }

    [Fact]
    public async Task AuthenticateAsync_WithNullUsername_ReturnsInvalidCredentials()
    {
        var repository = new FakeRepository(null);
        var tokenService = new FakeTokenService();
        var passwordHasher = new PasswordHasher();
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local
        };

        var service = CreateService(repository, passwordHasher, tokenService, options);

        var result = await service.AuthenticateAsync(null!, "password");

        result.Status.Should().Be(LocalAuthenticationStatus.InvalidCredentials);
        result.Token.Should().BeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_WithEmptyPassword_ReturnsInvalidCredentials()
    {
        var repository = new FakeRepository(null);
        var tokenService = new FakeTokenService();
        var passwordHasher = new PasswordHasher();
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local
        };

        var service = CreateService(repository, passwordHasher, tokenService, options);

        var result = await service.AuthenticateAsync("admin", "");

        result.Status.Should().Be(LocalAuthenticationStatus.InvalidCredentials);
        result.Token.Should().BeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_WhenNotConfiguredForLocalMode_ReturnsNotConfigured()
    {
        var repository = new FakeRepository(null);
        var tokenService = new FakeTokenService();
        var passwordHasher = new PasswordHasher();
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Oidc
        };

        var service = CreateService(repository, passwordHasher, tokenService, options);

        var result = await service.AuthenticateAsync("admin", "password");

        result.Status.Should().Be(LocalAuthenticationStatus.NotConfigured);
    }

    [Fact]
    public async Task AuthenticateAsync_WithNonexistentUser_ReturnsInvalidCredentials()
    {
        var repository = new FakeRepository(null);
        var tokenService = new FakeTokenService();
        var passwordHasher = new PasswordHasher();
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local
        };

        var service = CreateService(repository, passwordHasher, tokenService, options);

        var result = await service.AuthenticateAsync("nonexistent", "password");

        result.Status.Should().Be(LocalAuthenticationStatus.InvalidCredentials);
    }

    [Fact]
    public async Task AuthenticateAsync_WithDisabledAccount_ReturnsDisabled()
    {
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("Secret123!");

        var credentials = new AuthUserCredentials(
            Id: "user-1",
            Username: "admin",
            Email: null,
            IsActive: false,
            IsLocked: false,
            IsServiceAccount: false,
            FailedAttempts: 0,
            LastFailedAt: null,
            LastLoginAt: null,
            PasswordChangedAt: null,
            PasswordExpiresAt: null,
            PasswordHash: hash.Hash,
            PasswordSalt: hash.Salt,
            HashAlgorithm: hash.Algorithm,
            HashParameters: hash.Parameters,
            Roles: new[] { "user" });

        var repository = new FakeRepository(credentials);
        var tokenService = new FakeTokenService();
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local
        };

        var service = CreateService(repository, passwordHasher, tokenService, options);

        var result = await service.AuthenticateAsync("admin", "Secret123!");

        result.Status.Should().Be(LocalAuthenticationStatus.Disabled);
    }

    [Fact]
    public async Task AuthenticateAsync_WithLockedAccount_ReturnsLockedOut()
    {
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("Secret123!");

        var credentials = new AuthUserCredentials(
            Id: "user-1",
            Username: "admin",
            Email: null,
            IsActive: true,
            IsLocked: true,
            IsServiceAccount: false,
            FailedAttempts: 5,
            LastFailedAt: DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMinutes(5)),
            LastLoginAt: null,
            PasswordChangedAt: null,
            PasswordExpiresAt: null,
            PasswordHash: hash.Hash,
            PasswordSalt: hash.Salt,
            HashAlgorithm: hash.Algorithm,
            HashParameters: hash.Parameters,
            Roles: new[] { "user" });

        var repository = new FakeRepository(credentials);
        var tokenService = new FakeTokenService();
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local,
            Local =
            {
                MaxFailedAttempts = 5,
                LockoutDuration = TimeSpan.FromMinutes(15)
            }
        };

        var service = CreateService(repository, passwordHasher, tokenService, options);

        var result = await service.AuthenticateAsync("admin", "Secret123!");

        result.Status.Should().Be(LocalAuthenticationStatus.LockedOut);
        result.LockedUntil.Should().NotBeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_WhenLockoutExpired_AllowsLogin()
    {
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("Secret123!");

        // Lockout expired 1 minute ago
        var credentials = new AuthUserCredentials(
            Id: "user-1",
            Username: "admin",
            Email: null,
            IsActive: true,
            IsLocked: true,
            IsServiceAccount: false,
            FailedAttempts: 5,
            LastFailedAt: DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMinutes(16)),
            LastLoginAt: null,
            PasswordChangedAt: null,
            PasswordExpiresAt: null,
            PasswordHash: hash.Hash,
            PasswordSalt: hash.Salt,
            HashAlgorithm: hash.Algorithm,
            HashParameters: hash.Parameters,
            Roles: new[] { "user" });

        var repository = new FakeRepository(credentials);
        var tokenService = new FakeTokenService();
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local,
            Local =
            {
                MaxFailedAttempts = 5,
                LockoutDuration = TimeSpan.FromMinutes(15)
            }
        };

        var service = CreateService(repository, passwordHasher, tokenService, options);

        var result = await service.AuthenticateAsync("admin", "Secret123!");

        result.Status.Should().Be(LocalAuthenticationStatus.Success);
        result.Token.Should().NotBeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_WithExpiredPassword_ReturnsPasswordExpired()
    {
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
            PasswordChangedAt: DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(100)),
            PasswordExpiresAt: DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(10)),
            PasswordHash: hash.Hash,
            PasswordSalt: hash.Salt,
            HashAlgorithm: hash.Algorithm,
            HashParameters: hash.Parameters,
            Roles: new[] { "user" });

        var repository = new FakeRepository(credentials);
        var tokenService = new FakeTokenService();
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local,
            Local =
            {
                PasswordExpiration =
                {
                    Enabled = true,
                    ExpirationPeriod = TimeSpan.FromDays(90),
                    GracePeriodAfterExpiration = TimeSpan.Zero
                }
            }
        };

        var service = CreateService(repository, passwordHasher, tokenService, options);

        var result = await service.AuthenticateAsync("admin", "Secret123!");

        result.Status.Should().Be(LocalAuthenticationStatus.PasswordExpired);
    }

    [Fact]
    public async Task AuthenticateAsync_WithPasswordExpiringSoon_ReturnsWarning()
    {
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
            PasswordChangedAt: DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(85)),
            PasswordExpiresAt: DateTimeOffset.UtcNow.Add(TimeSpan.FromDays(5)),
            PasswordHash: hash.Hash,
            PasswordSalt: hash.Salt,
            HashAlgorithm: hash.Algorithm,
            HashParameters: hash.Parameters,
            Roles: new[] { "user" });

        var repository = new FakeRepository(credentials);
        var tokenService = new FakeTokenService();
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local,
            Local =
            {
                PasswordExpiration =
                {
                    Enabled = true,
                    ExpirationPeriod = TimeSpan.FromDays(90),
                    FirstWarningThreshold = TimeSpan.FromDays(7)
                }
            }
        };

        var service = CreateService(repository, passwordHasher, tokenService, options);

        var result = await service.AuthenticateAsync("admin", "Secret123!");

        result.Status.Should().Be(LocalAuthenticationStatus.PasswordExpiresSoon);
        result.Token.Should().NotBeNull();
        result.DaysUntilExpiration.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AuthenticateAsync_ServiceAccount_SkipsPasswordExpiration()
    {
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("Secret123!");

        var credentials = new AuthUserCredentials(
            Id: "user-1",
            Username: "service-account",
            Email: null,
            IsActive: true,
            IsLocked: false,
            IsServiceAccount: true,
            FailedAttempts: 0,
            LastFailedAt: null,
            LastLoginAt: null,
            PasswordChangedAt: DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(100)),
            PasswordExpiresAt: DateTimeOffset.UtcNow.Subtract(TimeSpan.FromDays(10)),
            PasswordHash: hash.Hash,
            PasswordSalt: hash.Salt,
            HashAlgorithm: hash.Algorithm,
            HashParameters: hash.Parameters,
            Roles: new[] { "service" });

        var repository = new FakeRepository(credentials);
        var tokenService = new FakeTokenService();
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local,
            Local =
            {
                PasswordExpiration =
                {
                    Enabled = true,
                    ExpirationPeriod = TimeSpan.FromDays(90)
                }
            }
        };

        var service = CreateService(repository, passwordHasher, tokenService, options);

        var result = await service.AuthenticateAsync("service-account", "Secret123!");

        result.Status.Should().Be(LocalAuthenticationStatus.Success);
    }

    [Fact]
    public async Task AuthenticateAsync_FirstFailedAttempt_DoesNotLockAccount()
    {
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
            PasswordExpiresAt: null,
            PasswordHash: hash.Hash,
            PasswordSalt: hash.Salt,
            HashAlgorithm: hash.Algorithm,
            HashParameters: hash.Parameters,
            Roles: new[] { "user" });

        var repository = new FakeRepository(credentials);
        var tokenService = new FakeTokenService();
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local,
            Local =
            {
                MaxFailedAttempts = 5,
                LockoutDuration = TimeSpan.FromMinutes(15)
            }
        };

        var service = CreateService(repository, passwordHasher, tokenService, options);

        var result = await service.AuthenticateAsync("admin", "WrongPassword!");

        result.Status.Should().Be(LocalAuthenticationStatus.InvalidCredentials);
        repository.LastFailure.Should().NotBeNull();
        repository.LastFailure!.Value.failedAttempts.Should().Be(1);
        repository.LastFailure!.Value.locked.Should().BeFalse();
    }

    [Fact]
    public async Task AuthenticateAsync_ConcurrentLogins_HandleCorrectly()
    {
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
            PasswordExpiresAt: null,
            PasswordHash: hash.Hash,
            PasswordSalt: hash.Salt,
            HashAlgorithm: hash.Algorithm,
            HashParameters: hash.Parameters,
            Roles: new[] { "administrator" });

        var repository = new FakeRepository(credentials);
        var tokenService = new FakeTokenService();
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local
        };

        var service = CreateService(repository, passwordHasher, tokenService, options);

        // Simulate concurrent login attempts
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => service.AuthenticateAsync("admin", "Secret123!"))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // All should succeed
        results.Should().AllSatisfy(r =>
        {
            r.Status.Should().Be(LocalAuthenticationStatus.Success);
            r.Token.Should().NotBeNull();
        });

        repository.SuccessCalls.Should().Be(10);
    }

    [Fact]
    public async Task AuthenticateAsync_WithEmptyPasswordHash_ReturnsInvalidCredentials()
    {
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
            PasswordExpiresAt: null,
            PasswordHash: Array.Empty<byte>(),
            PasswordSalt: Array.Empty<byte>(),
            HashAlgorithm: "Argon2id",
            HashParameters: "",
            Roles: new[] { "user" });

        var repository = new FakeRepository(credentials);
        var tokenService = new FakeTokenService();
        var passwordHasher = new PasswordHasher();
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local
        };

        var service = CreateService(repository, passwordHasher, tokenService, options);

        var result = await service.AuthenticateAsync("admin", "Secret123!");

        result.Status.Should().Be(LocalAuthenticationStatus.InvalidCredentials);
    }

    [Fact]
    public async Task AuthenticateAsync_FailedAttemptWindowExpired_ResetsCount()
    {
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("Secret123!");

        // Failed attempts from 20 minutes ago (outside 15-minute lockout window)
        var credentials = new AuthUserCredentials(
            Id: "user-1",
            Username: "admin",
            Email: null,
            IsActive: true,
            IsLocked: false,
            IsServiceAccount: false,
            FailedAttempts: 4,
            LastFailedAt: DateTimeOffset.UtcNow.Subtract(TimeSpan.FromMinutes(20)),
            LastLoginAt: null,
            PasswordChangedAt: null,
            PasswordExpiresAt: null,
            PasswordHash: hash.Hash,
            PasswordSalt: hash.Salt,
            HashAlgorithm: hash.Algorithm,
            HashParameters: hash.Parameters,
            Roles: new[] { "user" });

        var repository = new FakeRepository(credentials);
        var tokenService = new FakeTokenService();
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local,
            Local =
            {
                MaxFailedAttempts = 5,
                LockoutDuration = TimeSpan.FromMinutes(15)
            }
        };

        var service = CreateService(repository, passwordHasher, tokenService, options);

        // Wrong password attempt should start fresh count
        var result = await service.AuthenticateAsync("admin", "WrongPassword!");

        result.Status.Should().Be(LocalAuthenticationStatus.InvalidCredentials);
        repository.LastFailure.Should().NotBeNull();
        repository.LastFailure!.Value.failedAttempts.Should().Be(1);
        repository.LastFailure!.Value.locked.Should().BeFalse();
    }

       public ValueTask CreateOidcAdministratorAsync(string subject, string? username, string? email, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;


    [Fact]
    public async Task ChangePasswordAsync_WithValidCredentials_UpdatesStoredHash()
    {
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("OldSecret123!");

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
            PasswordExpiresAt: null,
            PasswordHash: hash.Hash,
            PasswordSalt: hash.Salt,
            HashAlgorithm: hash.Algorithm,
            HashParameters: hash.Parameters,
            Roles: new[] { "administrator" });

        var repository = new FakeRepository(credentials);
        var tokenService = new FakeTokenService();
        var options = new HonuaAuthenticationOptions
        {
            Mode = HonuaAuthenticationOptions.AuthenticationMode.Local
        };

        var service = CreateService(repository, passwordHasher, tokenService, options);

        await service.ChangePasswordAsync("user-1", "OldSecret123!", "NewSecret123!");

        repository.PasswordUpdateCalls.Should().Be(1);

        var result = await service.AuthenticateAsync("admin", "NewSecret123!");
        result.Status.Should().Be(LocalAuthenticationStatus.Success);
    }

    [Fact]
    public async Task ChangePasswordAsync_WithInvalidCurrentPassword_Throws()
    {
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("OldSecret123!");

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
            PasswordExpiresAt: null,
            PasswordHash: hash.Hash,
            PasswordSalt: hash.Salt,
            HashAlgorithm: hash.Algorithm,
            HashParameters: hash.Parameters,
            Roles: new[] { "administrator" });

        var repository = new FakeRepository(credentials);
        var tokenService = new FakeTokenService();
        var options = new HonuaAuthenticationOptions { Mode = HonuaAuthenticationOptions.AuthenticationMode.Local };
        var service = CreateService(repository, passwordHasher, tokenService, options);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ChangePasswordAsync("user-1", "WrongPassword!", "NewSecret123!"));

        repository.PasswordUpdateCalls.Should().Be(0);
    }

    [Fact]
    public async Task ResetPasswordAsync_WithAdminActor_UpdatesPassword()
    {
        var passwordHasher = new PasswordHasher();
        var hash = passwordHasher.HashPassword("OldSecret123!");

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
            PasswordExpiresAt: null,
            PasswordHash: hash.Hash,
            PasswordSalt: hash.Salt,
            HashAlgorithm: hash.Algorithm,
            HashParameters: hash.Parameters,
            Roles: new[] { "administrator" });

        var repository = new FakeRepository(credentials);
        var tokenService = new FakeTokenService();
        var options = new HonuaAuthenticationOptions { Mode = HonuaAuthenticationOptions.AuthenticationMode.Local };
        var service = CreateService(repository, passwordHasher, tokenService, options);

        await service.ResetPasswordAsync("user-1", "ResetSecret123!", actorUserId: "admin-actor");

        repository.PasswordUpdateCalls.Should().Be(1);

        var result = await service.AuthenticateAsync("admin", "ResetSecret123!");
        result.Status.Should().Be(LocalAuthenticationStatus.Success);
    }
    private sealed class FakeTokenService : ILocalTokenService
    {
        public Task<string> CreateTokenAsync(string subject, IReadOnlyCollection<string> roles, TimeSpan? lifetime = null, CancellationToken cancellationToken = default)
        {
            return Task.FromResult($"token:{subject}");
        }
    }

    private sealed class FakeRepository : IAuthRepository
    {
        private AuthUserCredentials? _credentials;

        public FakeRepository(AuthUserCredentials? credentials)
        {
            _credentials = credentials;
        }

        public int SuccessCalls { get; private set; }
        public (int failedAttempts, bool locked)? LastFailure { get; private set; }
        public int PasswordUpdateCalls { get; private set; }

        public ValueTask EnsureInitializedAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask<BootstrapState> GetBootstrapStateAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(new BootstrapState(false, null));
        public ValueTask MarkBootstrapCompletedAsync(string mode, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask CreateLocalAdministratorAsync(string username, string? email, byte[] passwordHash, byte[] salt, string hashAlgorithm, string hashParameters, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask CreateOidcAdministratorAsync(string subject, string? username, string? email, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<AuthUserCredentials?> GetCredentialsByUsernameAsync(string username, CancellationToken cancellationToken = default)
        {
            if (_credentials is null || !string.Equals(username, _credentials.Username, StringComparison.OrdinalIgnoreCase))
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
            if (_credentials is null || !string.Equals(userId, _credentials.Id, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("User not found");
            }

            _credentials = _credentials with
            {
                PasswordHash = passwordHash,
                PasswordSalt = salt,
                HashAlgorithm = hashAlgorithm,
                HashParameters = hashParameters
            };

            PasswordUpdateCalls++;
            return ValueTask.CompletedTask;
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
