// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Threading.Tasks;
using Honua.Cli.AI.Secrets;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Honua.Cli.Commands;

/// <summary>
/// Command to list all stored secrets (names only, not values).
/// </summary>
public sealed class SecretsListCommand : AsyncCommand
{
    private readonly ISecretsManager _secretsManager;

    public SecretsListCommand(ISecretsManager secretsManager)
    {
        _secretsManager = secretsManager ?? throw new ArgumentNullException(nameof(secretsManager));
    }

    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        try
        {
            var secretNames = await _secretsManager.ListSecretsAsync();

            if (!secretNames.Any())
            {
                AnsiConsole.MarkupLine("[dim]No secrets stored[/]");
                AnsiConsole.MarkupLine("\n[dim]Use[/] [cyan]honua secrets set <name>[/] [dim]to store a secret[/]");
                return 0;
            }

            // Create a table
            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Grey)
                .AddColumn(new TableColumn("[cyan]Secret Name[/]").Centered());

            foreach (var name in secretNames.OrderBy(n => n))
            {
                table.AddRow(name);
            }

            AnsiConsole.Write(table);

            // Show storage info
            AnsiConsole.MarkupLine($"\n[dim]Storage: ~/.honua/secrets.enc (AES-256 encrypted)[/]");
            AnsiConsole.MarkupLine($"[dim]Total secrets: {secretNames.Count}[/]");

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Failed to list secrets: {ex.Message}");
            return 1;
        }
    }
}
