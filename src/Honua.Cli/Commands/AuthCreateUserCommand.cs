// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.Utilities;
using Honua.Server.Core.Authentication;
using Honua.Server.Core.Configuration;
using Honua.Server.Core.Data.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

internal sealed class AuthCreateUserCommand : AsyncCommand<AuthCreateUserCommand.Settings>
{
    private readonly IAnsiConsole _console;
    private readonly IAuthRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPasswordComplexityValidator _passwordValidator;
    private readonly IOptionsMonitor<HonuaAuthenticationOptions> _options;
    private readonly ILogger<AuthCreateUserCommand> _logger;

    public AuthCreateUserCommand(
        IAnsiConsole console,
        IAuthRepository repository,
        IPasswordHasher passwordHasher,
        IPasswordComplexityValidator passwordValidator,
        IOptionsMonitor<HonuaAuthenticationOptions> options,
        ILogger<AuthCreateUserCommand> logger)
    {
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _passwordValidator = passwordValidator ?? throw new ArgumentNullException(nameof(passwordValidator));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        return await CliErrorHandler.ExecuteWithErrorHandlingAsync(
            async () =>
            {
                var configured = _options.CurrentValue;
                if (configured.Mode != HonuaAuthenticationOptions.AuthenticationMode.Local)
                {
                    _console.MarkupLine("[red]Local authentication mode must be enabled to manage local users.[/]");
                    return 1;
                }

                if (settings.Username.IsNullOrWhiteSpace())
                {
                    _console.MarkupLine("[red]Username is required.[/]");
                    return 1;
                }

                var roles = NormalizeRoles(settings.Roles);
                if (roles.Count == 0)
                {
                    roles = new List<string> { "viewer" };
                }

                string password;
                var generated = false;

                if (settings.Password.HasValue())
                {
                    password = settings.Password!;
                }
                else if (settings.GeneratePassword)
                {
                    password = GeneratePassword();
                    generated = true;
                }
                else
                {
                    _console.MarkupLine("[red]Provide --password or use --generate-password to create one automatically.[/]");
                    return 1;
                }

                // Validate password complexity (skip for generated passwords as they're always strong)
                if (!generated)
                {
                    var validationResult = _passwordValidator.Validate(password);
                    if (!validationResult.IsValid)
                    {
                        _console.MarkupLine("[red]Password does not meet complexity requirements:[/]");
                        foreach (var error in validationResult.Errors)
                        {
                            _console.MarkupLine($"  [red]• {error}[/]");
                        }
                        return 1;
                    }
                }

                var hash = _passwordHasher.HashPassword(password);

                var userId = await _repository.CreateLocalUserAsync(
                    settings.Username!,
                    settings.Email.IsNullOrWhiteSpace() ? null : settings.Email,
                    hash.Hash,
                    hash.Salt,
                    hash.Algorithm,
                    hash.Parameters,
                    roles,
                    auditContext: null,
                    CancellationToken.None).ConfigureAwait(false);

                _console.MarkupLine($"[green]Created user[/] [bold]{settings.Username}[/] (ID: {userId}).");
                _console.MarkupLine($"Roles: {string.Join(", ", roles)}");

                if (generated)
                {
                    await HandleGeneratedPasswordAsync(password, settings.OutputPath).ConfigureAwait(false);
                }

                return 0;
            },
            _logger,
            "auth-create-user");
    }

    private async Task HandleGeneratedPasswordAsync(string password, string? outputPath)
    {
        _console.MarkupLine("Generated password (store securely):");
        _console.MarkupLine($"[yellow]{password}[/]");

        if (outputPath.IsNullOrWhiteSpace())
        {
            return;
        }

        try
        {
            await File.WriteAllTextAsync(outputPath, password).ConfigureAwait(false);
            _console.MarkupLine($"Password written to {outputPath}. Ensure file permissions are restricted.");
        }
        catch (Exception ex)
        {
            _console.MarkupLine($"[yellow]Failed to write password to {outputPath}: {ex.Message}[/]");
        }
    }

    private static List<string> NormalizeRoles(IReadOnlyList<string>? roles)
    {
        if (roles is null || roles.Count == 0)
        {
            return new List<string>();
        }

        return roles
            .Where(r => r.HasValue())
            .Select(r => r.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();
    }

    private static string GeneratePassword()
    {
        const int length = 24;
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%^&*";
        Span<byte> buffer = stackalloc byte[length];
        RandomNumberGenerator.Fill(buffer);

        var chars = new char[length];
        for (var i = 0; i < buffer.Length; i++)
        {
            chars[i] = alphabet[buffer[i] % alphabet.Length];
        }

        return new string(chars);
    }

    internal sealed class Settings : CommandSettings
    {
        [CommandOption("--username <USERNAME>")]
        [Description("Username for the new local user.")]
        public string? Username { get; init; }

        [CommandOption("--email <EMAIL>")]
        [Description("Optional email address for the user.")]
        public string? Email { get; init; }

        [CommandOption("--password <PASSWORD>")]
        [Description("Explicit password to assign. Use with caution.")]
        public string? Password { get; init; }

        [CommandOption("--generate-password")]
        [Description("Generate a strong random password.")]
        public bool GeneratePassword { get; init; }

        [CommandOption("--role <ROLE>")]
        [Description("Assign one or more roles (administrator, datapublisher, viewer). Defaults to viewer.")]
        public IReadOnlyList<string>? Roles { get; init; }

        [CommandOption("--output <PATH>")]
        [Description("Optional file path to write a generated password.")]
        public string? OutputPath { get; init; }
    }
}
