using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Honua.Cli.AI.Services.Processes.Steps.Deployment;

namespace Honua.Cli.AI.E2ETests.Infrastructure;

/// <summary>
/// Azure CLI test double that targets the Azurite emulator. Captures versioning and CORS updates.
/// </summary>
internal sealed class AzuriteAzureCli : IAzureCli, IDisposable
{
    private readonly BlobServiceClient _serviceClient;
    private readonly Action<bool>? _onVersioning;
    private readonly Action<IEnumerable<BlobCorsRule>>? _onCors;

    public AzuriteAzureCli(
        BlobServiceClient serviceClient,
        Action<bool>? onVersioning = null,
        Action<IEnumerable<BlobCorsRule>>? onCors = null)
    {
        _serviceClient = serviceClient ?? throw new ArgumentNullException(nameof(serviceClient));
        _onVersioning = onVersioning;
        _onCors = onCors;
    }

    public Task<string> ExecuteAsync(CancellationToken cancellationToken, params string[] arguments)
    {
        if (arguments.Length >= 4 &&
            arguments[0] == "storage" &&
            arguments[1] == "account" &&
            arguments[2] == "blob-service-properties" &&
            arguments[3] == "update")
        {
            var enableIndex = Array.IndexOf(arguments, "--enable-versioning");
            var enabled = enableIndex > -1 &&
                          enableIndex + 1 < arguments.Length &&
                          arguments[enableIndex + 1].Equals("true", StringComparison.OrdinalIgnoreCase);

            _onVersioning?.Invoke(enabled);
            return Task.FromResult(string.Empty);
        }

        if (arguments.Length >= 3 &&
            arguments[0] == "storage" &&
            arguments[1] == "cors" &&
            arguments[2] == "add")
        {
            var originsArg = GetOptionValue(arguments, "--origins");
            var methods = ExtractValues(arguments, "--methods");
            var allowedHeaders = GetOptionValue(arguments, "--allowed-headers");
            var maxAge = GetOptionValue(arguments, "--max-age");

            var parsedOrigins = originsArg?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            var parsedHeaders = allowedHeaders?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>();
            var rule = new BlobCorsRule
            {
                AllowedOrigins = string.Join(",", parsedOrigins),
                AllowedHeaders = string.Join(",", parsedHeaders),
                AllowedMethods = string.Join(",", methods),
                MaxAgeInSeconds = int.TryParse(maxAge, out var age) ? age : 3000
            };

            _onCors?.Invoke(new[] { rule });
            return Task.FromResult(string.Empty);
        }

        throw new InvalidOperationException($"Azurite test CLI received unsupported command: {string.Join(' ', arguments)}");
    }

    private static string? GetOptionValue(string[] args, string option)
    {
        var index = Array.IndexOf(args, option);
        return index > -1 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static string[] ExtractValues(string[] args, string option)
    {
        var index = Array.IndexOf(args, option);
        if (index == -1)
        {
            return Array.Empty<string>();
        }

        var values = new List<string>();
        for (var i = index + 1; i < args.Length && !args[i].StartsWith("--", StringComparison.Ordinal); i++)
        {
            values.Add(args[i]);
        }

        return values.ToArray();
    }

    public void Dispose()
    {
    }
}
