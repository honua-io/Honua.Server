// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Data.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Authentication;

public interface ILocalAuthenticationService
{
    Task<LocalAuthenticationResult> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default);

    Task ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(
        string targetUserId,
        string newPassword,
        string? actorUserId = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default);
}

public enum LocalAuthenticationStatus
{
    Success,
    InvalidCredentials,
    LockedOut,
    Disabled,
    NotConfigured,
    PasswordExpired,
    PasswordExpiresSoon
}

public sealed record LocalAuthenticationResult(
    LocalAuthenticationStatus Status,
    string? Token,
    IReadOnlyCollection<string> Roles,
    DateTimeOffset? LockedUntil,
    string? UserId,
    DateTimeOffset? PasswordExpiresAt = null,
    int? DaysUntilExpiration = null)
{
    public static LocalAuthenticationResult Success(string token, IReadOnlyCollection<string> roles, string userId) =>
        new(LocalAuthenticationStatus.Success, token, roles, null, userId, null, null);

    public static LocalAuthenticationResult SuccessWithWarning(string token, IReadOnlyCollection<string> roles, string userId, DateTimeOffset passwordExpiresAt, int daysUntilExpiration) =>
        new(LocalAuthenticationStatus.PasswordExpiresSoon, token, roles, null, userId, passwordExpiresAt, daysUntilExpiration);

    public static LocalAuthenticationResult Failure(LocalAuthenticationStatus status, DateTimeOffset? lockedUntil = null) =>
        new(status, null, Array.Empty<string>(), lockedUntil, null, null, null);
}

public sealed class LocalAuthenticationService : ILocalAuthenticationService
{
    private readonly IAuthRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILocalTokenService _tokenService;
    private readonly IOptionsMonitor<HonuaAuthenticationOptions> _options;
    private readonly IPasswordComplexityValidator _passwordComplexityValidator;
    private readonly ILogger<LocalAuthenticationService> _logger;

    public LocalAuthenticationService(
        IAuthRepository repository,
        IPasswordHasher passwordHasher,
        ILocalTokenService tokenService,
        IOptionsMonitor<HonuaAuthenticationOptions> options,
        IPasswordComplexityValidator passwordComplexityValidator,
        ILogger<LocalAuthenticationService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _passwordComplexityValidator = passwordComplexityValidator ?? throw new ArgumentNullException(nameof(passwordComplexityValidator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<LocalAuthenticationResult> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        if (username.IsNullOrWhiteSpace() || password.IsNullOrWhiteSpace())
        {
            return LocalAuthenticationResult.Failure(LocalAuthenticationStatus.InvalidCredentials);
        }

        var options = _options.CurrentValue;
        if (options.Mode != HonuaAuthenticationOptions.AuthenticationMode.Local)
        {
            _logger.LogWarning("Local authentication attempted while mode is {Mode}", options.Mode);
            return LocalAuthenticationResult.Failure(LocalAuthenticationStatus.NotConfigured);
        }

        var credentials = await _repository.GetCredentialsByUsernameAsync(username, cancellationToken).ConfigureAwait(false);
        if (credentials is null)
        {
            _logger.LogInformation("Local authentication failed for {Username}: credentials not found.", username);
            return LocalAuthenticationResult.Failure(LocalAuthenticationStatus.InvalidCredentials);
        }

        var lockoutSettings = ResolveLockoutSettings(options.Local);
        var now = DateTimeOffset.UtcNow;
        var windowExpired = credentials.LastFailedAt.HasValue && now - credentials.LastFailedAt.Value > lockoutSettings.LockoutDuration;
        var effectiveFailedAttempts = windowExpired ? 0 : credentials.FailedAttempts;
        var lockoutExpiresAt = credentials.LastFailedAt?.Add(lockoutSettings.LockoutDuration);
        var isLocked = credentials.IsLocked && lockoutExpiresAt.HasValue && lockoutExpiresAt > now;

        if (!credentials.IsActive)
        {
            _logger.LogWarning("Local authentication failed for {Username}: account disabled.", username);
            return LocalAuthenticationResult.Failure(LocalAuthenticationStatus.Disabled);
        }

        if (isLocked)
        {
            _logger.LogWarning("Local authentication failed for {Username}: account locked until {LockedUntil:u}.", username, lockoutExpiresAt);
            return LocalAuthenticationResult.Failure(LocalAuthenticationStatus.LockedOut, lockoutExpiresAt);
        }

        if (credentials.PasswordHash.Length == 0 || credentials.PasswordSalt.Length == 0)
        {
            _logger.LogWarning("Local authentication failed for {Username}: no local credentials stored.", username);
            return LocalAuthenticationResult.Failure(LocalAuthenticationStatus.InvalidCredentials);
        }

        // Check password expiration (skip for service accounts)
        var passwordExpirationPolicy = options.Local.PasswordExpiration;
        if (passwordExpirationPolicy.Enabled && !credentials.IsServiceAccount)
        {
            var passwordExpirationCheck = CheckPasswordExpiration(credentials, passwordExpirationPolicy, now);
            if (passwordExpirationCheck.IsExpired)
            {
                _logger.LogWarning("Local authentication failed for {Username}: password expired on {ExpiresAt:u}.", username, credentials.PasswordExpiresAt);
                return LocalAuthenticationResult.Failure(LocalAuthenticationStatus.PasswordExpired);
            }
        }

        var verified = _passwordHasher.VerifyPassword(password, credentials.PasswordHash, credentials.PasswordSalt, credentials.HashAlgorithm, credentials.HashParameters);
        if (!verified)
        {
            var nextFailedCount = effectiveFailedAttempts + 1;
            var shouldLock = nextFailedCount >= lockoutSettings.MaxFailedAttempts;

            await _repository.UpdateLoginFailureAsync(credentials.Id, nextFailedCount, now, shouldLock, null, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Local authentication failed for {Username}: invalid password (attempt {Attempt}/{Max}).", username, nextFailedCount, lockoutSettings.MaxFailedAttempts);

            return LocalAuthenticationResult.Failure(
                shouldLock ? LocalAuthenticationStatus.LockedOut : LocalAuthenticationStatus.InvalidCredentials,
                shouldLock ? now.Add(lockoutSettings.LockoutDuration) : null);
        }

        await _repository.UpdateLoginSuccessAsync(credentials.Id, now, null, cancellationToken).ConfigureAwait(false);

        var roles = credentials.Roles.Count == 0
            ? Array.Empty<string>()
            : credentials.Roles;

        var token = await _tokenService.CreateTokenAsync(credentials.Id, roles, lifetime: null, cancellationToken: cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Local authentication succeeded for {Username}.", username);

        // Check for password expiration warnings (skip for service accounts)
        if (passwordExpirationPolicy.Enabled && !credentials.IsServiceAccount && credentials.PasswordExpiresAt.HasValue)
        {
            var passwordExpirationCheck = CheckPasswordExpiration(credentials, passwordExpirationPolicy, now);
            if (passwordExpirationCheck.ShowWarning)
            {
                _logger.LogInformation("Password for {Username} expires in {Days} days on {ExpiresAt:u}.",
                    username, passwordExpirationCheck.DaysUntilExpiration, credentials.PasswordExpiresAt);
                return LocalAuthenticationResult.SuccessWithWarning(token, roles, credentials.Id, credentials.PasswordExpiresAt.Value, passwordExpirationCheck.DaysUntilExpiration);
            }
        }

        return LocalAuthenticationResult.Success(token, roles, credentials.Id);
    }

    public async Task ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(userId);
        Guard.NotNullOrWhiteSpace(currentPassword);
        Guard.NotNullOrWhiteSpace(newPassword);

        var options = _options.CurrentValue;
        if (options.Mode != HonuaAuthenticationOptions.AuthenticationMode.Local)
        {
            throw new InvalidOperationException("Local authentication is not enabled.");
        }

        var credentials = await _repository.GetCredentialsByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (credentials is null)
        {
            throw new KeyNotFoundException("User not found.");
        }

        if (!credentials.IsActive)
        {
            throw new InvalidOperationException("Account is disabled.");
        }

        if (credentials.PasswordHash is null || credentials.PasswordSalt is null || credentials.HashAlgorithm.IsNullOrWhiteSpace())
        {
            throw new InvalidOperationException("Account does not have local credentials.");
        }

        var passwordVerified = _passwordHasher.VerifyPassword(
            currentPassword,
            credentials.PasswordHash,
            credentials.PasswordSalt,
            credentials.HashAlgorithm,
            credentials.HashParameters);

        if (!passwordVerified)
        {
            throw new InvalidOperationException("Current password is incorrect.");
        }

        var complexity = _passwordComplexityValidator.Validate(newPassword);
        if (!complexity.IsValid)
        {
            throw new InvalidOperationException(string.Join(" ", complexity.Errors));
        }

        var hashResult = _passwordHasher.HashPassword(newPassword);
        var audit = new AuditContext(userId, ipAddress, userAgent);

        await _repository.SetLocalUserPasswordAsync(
            userId,
            hashResult.Hash,
            hashResult.Salt,
            hashResult.Algorithm,
            hashResult.Parameters,
            audit,
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Password changed for user {UserId}.", userId);
    }

    public async Task ResetPasswordAsync(
        string targetUserId,
        string newPassword,
        string? actorUserId = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNullOrWhiteSpace(targetUserId);
        Guard.NotNullOrWhiteSpace(newPassword);

        var options = _options.CurrentValue;
        if (options.Mode != HonuaAuthenticationOptions.AuthenticationMode.Local)
        {
            throw new InvalidOperationException("Local authentication is not enabled.");
        }

        var credentials = await _repository.GetCredentialsByIdAsync(targetUserId, cancellationToken).ConfigureAwait(false);
        if (credentials is null)
        {
            throw new KeyNotFoundException("User not found.");
        }

        var complexity = _passwordComplexityValidator.Validate(newPassword);
        if (!complexity.IsValid)
        {
            throw new InvalidOperationException(string.Join(" ", complexity.Errors));
        }

        var hashResult = _passwordHasher.HashPassword(newPassword);
        var audit = new AuditContext(actorUserId, ipAddress, userAgent);

        await _repository.SetLocalUserPasswordAsync(
            targetUserId,
            hashResult.Hash,
            hashResult.Salt,
            hashResult.Algorithm,
            hashResult.Parameters,
            audit,
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Password reset for user {TargetUserId} by {ActorUserId}.", targetUserId, actorUserId ?? "(system)");
    }

    private static (int MaxFailedAttempts, TimeSpan LockoutDuration) ResolveLockoutSettings(HonuaAuthenticationOptions.LocalOptions options)
    {
        var maxAttempts = options.MaxFailedAttempts <= 0 ? 5 : options.MaxFailedAttempts;
        var lockoutDuration = options.LockoutDuration <= TimeSpan.Zero ? TimeSpan.FromMinutes(15) : options.LockoutDuration;
        return (maxAttempts, lockoutDuration);
    }

    private static (bool IsExpired, bool ShowWarning, int DaysUntilExpiration) CheckPasswordExpiration(
        AuthUserCredentials credentials,
        HonuaAuthenticationOptions.PasswordExpirationOptions policy,
        DateTimeOffset now)
    {
        if (!credentials.PasswordExpiresAt.HasValue)
        {
            return (false, false, 0);
        }

        var expiresAt = credentials.PasswordExpiresAt.Value;
        var timeUntilExpiration = expiresAt - now;

        // Check if password has expired (with grace period)
        if (timeUntilExpiration <= -policy.GracePeriodAfterExpiration)
        {
            return (true, false, 0);
        }

        // Check if password is expired but within grace period
        if (timeUntilExpiration < TimeSpan.Zero && timeUntilExpiration > -policy.GracePeriodAfterExpiration)
        {
            var daysUntilGraceExpires = (int)Math.Ceiling((-timeUntilExpiration + policy.GracePeriodAfterExpiration).TotalDays);
            return (false, true, -daysUntilGraceExpires); // Negative indicates grace period
        }

        // Check if we should show warnings
        var daysUntilExpiration = (int)Math.Ceiling(timeUntilExpiration.TotalDays);
        var showWarning = timeUntilExpiration <= policy.FirstWarningThreshold;

        return (false, showWarning, daysUntilExpiration);
    }
}
