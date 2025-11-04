using System;
using System.IO;

namespace Honua.Tools.DataSeeder;

internal sealed class SeedOptions
{
    private SeedOptions()
    {
    }

    public bool TargetsPostgres { get; private set; } = true;
    public bool TargetsSqlite { get; private set; } = true;
    public string? PostgresConnection { get; private set; } = "Host=localhost;Database=honua;Username=honua;Password=secret";
    public string? SqlitePath { get; private set; } = "samples/ogc/ogc-sample.db";

    public static ParseResult TryParse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var options = new SeedOptions();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--provider":
                case "-p":
                    if (i + 1 >= args.Length)
                    {
                        return ParseResult.Fail("Missing value for --provider.");
                    }

                    var provider = args[++i].ToLowerInvariant();
                    options.TargetsPostgres = provider is "postgres" or "postgis" or "both";
                    options.TargetsSqlite = provider is "sqlite" or "both";
                    if (!options.TargetsPostgres && !options.TargetsSqlite)
                    {
                        return ParseResult.Fail("Provider must be 'postgres', 'postgis', 'sqlite', or 'both'.");
                    }
                    break;

                case "--postgres-connection":
                case "--pg":
                    if (i + 1 >= args.Length)
                    {
                        return ParseResult.Fail("Missing value for --postgres-connection.");
                    }

                    options.PostgresConnection = args[++i];
                    break;

                case "--sqlite-path":
                case "--sqlite":
                    if (i + 1 >= args.Length)
                    {
                        return ParseResult.Fail("Missing value for --sqlite-path.");
                    }

                    options.SqlitePath = args[++i];
                    break;

                case "--help":
                case "-h":
                    return ParseResult.Fail(string.Empty);

                default:
                    return ParseResult.Fail($"Unknown argument '{arg}'.");
            }
        }

        return ParseResult.Ok(options);
    }

    public static void PrintUsage()
    {
        Console.WriteLine("Usage: dotnet run --project tools/DataSeeder [options]");
        Console.WriteLine("Options:");
        Console.WriteLine("  --provider <postgres|sqlite|both>   Targets to seed (default: both)");
        Console.WriteLine("  --postgres-connection <string>      Npgsql connection string (default: Host=localhost;Database=honua;Username=honua;Password=secret)");
        Console.WriteLine("  --sqlite-path <path>                Relative or absolute path to SQLite db (default: samples/ogc/ogc-sample.db)");
    }

    public string ResolveSqlitePath(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(SqlitePath);

        return Path.IsPathRooted(SqlitePath!)
            ? SqlitePath!
            : Path.GetFullPath(Path.Combine(repositoryRoot, SqlitePath!));
    }
}
