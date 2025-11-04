using Honua.Tools.DataSeeder;

var parseResult = SeedOptions.TryParse(args);
if (!parseResult.Success)
{
    if (!string.IsNullOrWhiteSpace(parseResult.ErrorMessage))
    {
        Console.Error.WriteLine(parseResult.ErrorMessage);
    }

    SeedOptions.PrintUsage();
    return 1;
}

var options = parseResult.Options!;
var repositoryRoot = RepositoryLocator.FindRoot();
if (repositoryRoot is null)
{
    Console.Error.WriteLine("Unable to locate repository root (Honua.Next.sln not found).");
    return 1;
}

var features = FeatureFixtures.Default;

try
{
    if (options.TargetsPostgres)
    {
        var postgresSeeder = new PostgresSeeder(options.PostgresConnection!);
        await postgresSeeder.SeedAsync(features);
    }

    if (options.TargetsSqlite)
    {
        var sqlitePath = options.ResolveSqlitePath(repositoryRoot);
        var sqliteSeeder = new SqliteSeeder(sqlitePath);
        await sqliteSeeder.SeedAsync(features);
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Seeding failed: {ex.Message}");
    Console.Error.WriteLine(ex);
    return 1;
}

Console.WriteLine("Seeding completed successfully.");
return 0;
