// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Honua.Cli.AI.Secrets;
using Spectre.Console;
using Spectre.Console.Cli;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.Commands;

/// <summary>
/// Command to securely store a secret.
/// </summary>
public sealed class SecretsSetCommand : AsyncCommand<SecretsSetCommand.Settings>
{
    private readonly ISecretsManager _secretsManager;

    public SecretsSetCommand(ISecretsManager secretsManager)
    {
        _secretsManager = secretsManager ?? throw new ArgumentNullException(nameof(secretsManager));
    }

    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<NAME>")]
        [Description("The name of the secret to store")]
        public string Name { get; init; } = string.Empty;

        [CommandOption("--value <VALUE>")]
        [Description("The secret value (if not provided, will be prompted securely)")]
        public string? Value { get; init; }

        [CommandOption("--description <DESCRIPTION>")]
        [Description("Optional description of the secret")]
        public string? Description { get; init; }

        [CommandOption("--type <TYPE>")]
        [Description("Type of secret (Generic, DatabaseConnection, ApiKey, Certificate, SshKey, AccessToken)")]
        public string? Type { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            // Get secret value (either from --value or secure prompt)
            var secretValue = settings.Value;
            if (secretValue.IsNullOrWhiteSpace())
            {
                secretValue = AnsiConsole.Prompt(
                    new TextPrompt<string>($"Enter value for secret '[green]{settings.Name}[/]':")
                        .PromptStyle("red")
                        .Secret());
            }

            // Parse secret type
            SecretType secretType = SecretType.Generic;
            if (settings.Type.HasValue())
            {
                if (!Enum.TryParse<SecretType>(settings.Type, ignoreCase: true, out secretType))
                {
                    AnsiConsole.MarkupLine($"[yellow]⚠[/] Unknown secret type '{settings.Type}', using Generic");
                    secretType = SecretType.Generic;
                }
            }

            // Create metadata if provided
            SecretMetadata? metadata = null;
            if (settings.Description.HasValue() || secretType != SecretType.Generic)
            {
                metadata = new SecretMetadata
                {
                    Description = settings.Description,
                    Type = secretType
                };
            }

            // Store the secret
            await _secretsManager.SetSecretAsync(settings.Name, secretValue, metadata);

            AnsiConsole.MarkupLine($"[green]✓[/] Secret '[cyan]{settings.Name}[/]' stored successfully");
            AnsiConsole.MarkupLine($"[dim]Storage: ~/.honua/secrets.enc (AES-256 encrypted)[/]");

            if (metadata != null)
            {
                AnsiConsole.MarkupLine($"[dim]Type: {metadata.Type}[/]");
                if (metadata.Description.HasValue())
                {
                    AnsiConsole.MarkupLine($"[dim]Description: {metadata.Description}[/]");
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Failed to store secret: {ex.Message}");
            return 1;
        }
    }
}
