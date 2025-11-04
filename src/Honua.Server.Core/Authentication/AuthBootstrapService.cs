// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Data.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Authentication;

public enum AuthBootstrapStatus
{
    Unknown,
    Pending,
    Completed,
    Failed
}

public sealed record AuthBootstrapResult(AuthBootstrapStatus Status, string Message, string? GeneratedSecret = null);

public interface IAuthBootstrapService
{
    Task<AuthBootstrapResult> BootstrapAsync(CancellationToken cancellationToken = default);
}

public sealed class AuthBootstrapService : IAuthBootstrapService
{
    private const string DefaultAdminUsername = "admin";
    private const int GeneratedPasswordLength = 24;

    private readonly IAuthRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IOptionsMonitor<HonuaAuthenticationOptions> _options;
    private readonly ILogger<AuthBootstrapService> _logger;

    public AuthBootstrapService(
        IAuthRepository repository,
        IPasswordHasher passwordHasher,
        IOptionsMonitor<HonuaAuthenticationOptions> options,
        ILogger<AuthBootstrapService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AuthBootstrapResult> BootstrapAsync(CancellationToken cancellationToken = default)
    {
        await _repository.EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);

        var state = await _repository.GetBootstrapStateAsync(cancellationToken).ConfigureAwait(false);
        if (state.IsCompleted)
        {
            var message = $"Authentication bootstrap already completed for mode '{state.Mode ?? "unknown"}'.";
            _logger.LogInformation(message);
            return new AuthBootstrapResult(AuthBootstrapStatus.Completed, message);
        }

        var mode = _options.CurrentValue.Mode;
        switch (mode)
        {
            case HonuaAuthenticationOptions.AuthenticationMode.Local:
                return await BootstrapLocalAsync(cancellationToken);
            case HonuaAuthenticationOptions.AuthenticationMode.Oidc:
                return await BootstrapOidcAsync(cancellationToken);
            case HonuaAuthenticationOptions.AuthenticationMode.QuickStart:
                const string quickStartMessage = "QuickStart mode does not require bootstrap. Switch to Local or Oidc to enable enforcement.";
                _logger.LogWarning(quickStartMessage);
                return new AuthBootstrapResult(AuthBootstrapStatus.Unknown, quickStartMessage);
            default:
                var unsupported = $"Authentication mode '{mode}' is not supported for bootstrap.";
                _logger.LogError(unsupported);
                return new AuthBootstrapResult(AuthBootstrapStatus.Failed, unsupported);
        }
    }

    private async Task<AuthBootstrapResult> BootstrapLocalAsync(CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        string password;
        var wasGenerated = false;
        var username = options.Bootstrap.AdminUsername.IsNullOrWhiteSpace() ? DefaultAdminUsername : options.Bootstrap.AdminUsername!.Trim();
        if (username.IsNullOrWhiteSpace())
        {
            username = DefaultAdminUsername;
        }

        var email = options.Bootstrap.AdminEmail.IsNullOrWhiteSpace() ? null : options.Bootstrap.AdminEmail;

        if (options.Bootstrap.AdminPassword.HasValue())
        {
            password = options.Bootstrap.AdminPassword!;
        }
        else
        {
            password = GenerateRandomPassword();
            wasGenerated = true;
        }

        if (password.Length < 12)
        {
            const string error = "Bootstrap admin password must be at least 12 characters long.";
            _logger.LogError(error);
            return new AuthBootstrapResult(AuthBootstrapStatus.Failed, error);
        }

        var hash = _passwordHasher.HashPassword(password);

        try
        {
            await _repository.CreateLocalAdministratorAsync(
                username,
                email,
                hash.Hash,
                hash.Salt,
                hash.Algorithm,
                hash.Parameters,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            const string error = "Failed to create local administrator account.";
            _logger.LogError(ex, error);
            return new AuthBootstrapResult(AuthBootstrapStatus.Failed, error);
        }

        try
        {
            await _repository.MarkBootstrapCompletedAsync(options.Mode.ToString(), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            const string error = "Local administrator created but bootstrap state update failed.";
            _logger.LogError(ex, error);
            return new AuthBootstrapResult(AuthBootstrapStatus.Failed, error, wasGenerated ? password : null);
        }

        var message = $"Bootstrap completed. Local administrator '{username}' created.";
        _logger.LogInformation(message);

        return new AuthBootstrapResult(AuthBootstrapStatus.Completed, message, wasGenerated ? password : null);
    }

    private async Task<AuthBootstrapResult> BootstrapOidcAsync(CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        if (options.Bootstrap.AdminSubject.IsNullOrWhiteSpace())
        {
            const string error = "Honua:Authentication:Bootstrap:AdminSubject must be specified for OIDC bootstrap.";
            _logger.LogError(error);
            return new AuthBootstrapResult(AuthBootstrapStatus.Failed, error);
        }

        var username = options.Bootstrap.AdminUsername.IsNullOrWhiteSpace() ? DefaultAdminUsername : options.Bootstrap.AdminUsername!.Trim();
        if (username.IsNullOrWhiteSpace())
        {
            username = DefaultAdminUsername;
        }
        var email = options.Bootstrap.AdminEmail.IsNullOrWhiteSpace() ? null : options.Bootstrap.AdminEmail;

        try
        {
            await _repository.CreateOidcAdministratorAsync(
                options.Bootstrap.AdminSubject!,
                username,
                email,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            const string error = "Failed to register OIDC administrator subject.";
            _logger.LogError(ex, error);
            return new AuthBootstrapResult(AuthBootstrapStatus.Failed, error);
        }

        try
        {
            await _repository.MarkBootstrapCompletedAsync(options.Mode.ToString(), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            const string error = "OIDC administrator stored but bootstrap state update failed.";
            _logger.LogError(ex, error);
            return new AuthBootstrapResult(AuthBootstrapStatus.Failed, error);
        }

        var message = $"Bootstrap completed. OIDC administrator subject '{options.Bootstrap.AdminSubject}' registered.";
        _logger.LogInformation(message);
        return new AuthBootstrapResult(AuthBootstrapStatus.Completed, message);
    }

    private static string GenerateRandomPassword()
    {
        Span<byte> buffer = stackalloc byte[GeneratedPasswordLength];
        RandomNumberGenerator.Fill(buffer);

        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%^&*";
        var builder = new StringBuilder(GeneratedPasswordLength);
        foreach (var b in buffer)
        {
            builder.Append(alphabet[b % alphabet.Length]);
        }

        return builder.ToString();
    }
}
