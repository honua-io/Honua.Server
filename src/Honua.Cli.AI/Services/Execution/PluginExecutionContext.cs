// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Security;

namespace Honua.Cli.AI.Services.Execution;

public class PluginExecutionContext : IPluginExecutionContext
{
    public string WorkspacePath { get; }
    public bool RequireApproval { get; }
    public bool DryRun { get; }
    public List<PluginExecutionAuditEntry> AuditTrail { get; } = new();
    public string SessionId { get; }

    public PluginExecutionContext(string workspacePath, bool requireApproval = true, bool dryRun = false)
    {
        WorkspacePath = workspacePath;
        RequireApproval = requireApproval;
        DryRun = dryRun;
        SessionId = Guid.NewGuid().ToString("N")[..8];
    }

    public void RecordAction(string plugin, string action, string details, bool success, string? error = null)
    {
        // Sanitize sensitive information from details and error messages
        var sanitizedDetails = SecretSanitizer.SanitizeErrorMessage(details);
        var sanitizedError = error != null ? SecretSanitizer.SanitizeErrorMessage(error) : null;

        AuditTrail.Add(new PluginExecutionAuditEntry(
            DateTime.UtcNow,
            plugin,
            action,
            sanitizedDetails,
            success,
            sanitizedError
        ));
    }

    public Task<bool> RequestApprovalAsync(string action, string details, string[] resources)
    {
        if (!RequireApproval)
            return Task.FromResult(true);

        if (DryRun)
            return Task.FromResult(false);

        // Display approval request with clear visual separation
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("  APPROVAL REQUIRED");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.ResetColor();

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Action: {action}");
        Console.ResetColor();

        Console.WriteLine();
        Console.WriteLine("Details:");
        Console.WriteLine(details);

        if (resources != null && resources.Length > 0)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("Affected Resources:");
            Console.ResetColor();
            foreach (var resource in resources)
            {
                Console.WriteLine($"  • {resource}");
            }
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.ResetColor();

        // Prompt for approval with retry logic for invalid input
        const int maxAttempts = 3;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("Do you approve this action? (yes/no): ");
            Console.ResetColor();

            var response = Console.ReadLine()?.Trim().ToLowerInvariant();

            if (response == "yes" || response == "y")
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ Action approved");
                Console.ResetColor();
                Console.WriteLine();
                return Task.FromResult(true);
            }

            if (response == "no" || response == "n")
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("✗ Action denied");
                Console.ResetColor();
                Console.WriteLine();
                return Task.FromResult(false);
            }

            // Invalid input
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Invalid input: '{response}'. Please enter 'yes' or 'no'.");
            Console.ResetColor();

            if (attempt == maxAttempts - 1)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Maximum attempts exceeded. Action denied by default.");
                Console.ResetColor();
                Console.WriteLine();
                return Task.FromResult(false);
            }
        }

        // Fallback: deny by default for safety
        return Task.FromResult(false);
    }
}
