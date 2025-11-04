using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Honua.Tools.DataSeeder;

internal sealed class SqliteSeeder
{
    private const string UpsertSql = @"INSERT OR REPLACE INTO roads_primary (road_id, name, geom, observed_at)
VALUES (@id, @name, @geom, @observed);";

    private readonly string _databasePath;

    public SqliteSeeder(string databasePath)
    {
        _databasePath = string.IsNullOrWhiteSpace(databasePath)
            ? throw new ArgumentException("A SQLite database path must be provided.", nameof(databasePath))
            : databasePath;
    }

    public async Task SeedAsync(IReadOnlyList<FeatureDefinition> features, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(features);

        if (!File.Exists(_databasePath))
        {
            throw new FileNotFoundException($"SQLite database not found at '{_databasePath}'.", _databasePath);
        }

        await using var connection = new SqliteConnection($"Data Source={_databasePath};Mode=ReadWrite");
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        foreach (var feature in features)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = UpsertSql;
            command.Parameters.AddWithValue("@id", feature.Id);
            command.Parameters.AddWithValue("@name", feature.Name);
            command.Parameters.AddWithValue("@geom", feature.ToGeoJson());
            command.Parameters.AddWithValue("@observed", DateTime.Parse(feature.ObservedAt, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal).ToString("O", CultureInfo.InvariantCulture));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"Seeded {features.Count} features into {_databasePath}.");
    }
}
