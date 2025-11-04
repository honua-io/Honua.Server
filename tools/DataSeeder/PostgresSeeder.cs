using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace Honua.Tools.DataSeeder;

internal sealed class PostgresSeeder
{
    private const string UpsertSql = @"INSERT INTO public.roads_primary (road_id, name, geom, observed_at)
VALUES (@id, @name, ST_GeomFromGeoJSON(@geom)::geometry(LineString, 4326), @observed)
ON CONFLICT (road_id) DO UPDATE SET
    name = EXCLUDED.name,
    geom = EXCLUDED.geom,
    observed_at = EXCLUDED.observed_at;";

    private readonly string _connectionString;

    public PostgresSeeder(string connectionString)
    {
        _connectionString = string.IsNullOrWhiteSpace(connectionString)
            ? throw new ArgumentException("A connection string must be provided.", nameof(connectionString))
            : connectionString;
    }

    public async Task SeedAsync(IReadOnlyList<FeatureDefinition> features, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(features);
        if (features.Count == 0)
        {
            return;
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        foreach (var feature in features)
        {
            await using var command = new NpgsqlCommand(UpsertSql, connection, transaction);
            command.Parameters.AddWithValue("@id", feature.Id);
            command.Parameters.AddWithValue("@name", feature.Name);
            command.Parameters.AddWithValue("@geom", feature.ToGeoJson());
            command.Parameters.AddWithValue("@observed", DateTime.Parse(feature.ObservedAt, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"Seeded {features.Count} features into Postgres.");
    }
}
