// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Honua.Cli.AI.Services.VectorSearch;

/// <summary>
/// PostgreSQL-based knowledge store for deployment patterns using pgvector extension.
/// Provides vector similarity search over human-approved patterns.
/// </summary>
public sealed class PostgresVectorSearchProvider : IDeploymentPatternKnowledgeStore
{
    private readonly IConfiguration _configuration;
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly ILogger<PostgresVectorSearchProvider> _logger;
    private readonly string _connectionString;
    private readonly string _tableName;

    public PostgresVectorSearchProvider(
        IConfiguration configuration,
        IEmbeddingProvider embeddingProvider,
        ILogger<PostgresVectorSearchProvider> logger)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _embeddingProvider = embeddingProvider ?? throw new ArgumentNullException(nameof(embeddingProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _connectionString = configuration.GetConnectionString("PostgreSQL")
            ?? throw new InvalidOperationException("PostgreSQL connection string not configured");

        _tableName = configuration["PostgresVectorSearch:TableName"] ?? "deployment_patterns";

        _logger.LogInformation("Initialized PostgreSQL Vector Search - Table: {Table}", _tableName);
    }

    /// <summary>
    /// Ensures the patterns table and pgvector extension are created.
    /// </summary>
    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        bool useVectorExtension = true;

        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            // Check if pgvector extension already exists
            await using (var checkCmd = new NpgsqlCommand(
                "SELECT EXISTS(SELECT 1 FROM pg_extension WHERE extname = 'vector');", connection))
            {
                var exists = (bool?)await checkCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                if (exists == true)
                {
                    _logger.LogInformation("pgvector extension already exists");
                }
                else
                {
                    // Try to create pgvector extension
                    try
                    {
                        await using var createExtCmd = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector;", connection);
                        await createExtCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                        _logger.LogInformation("pgvector extension created successfully");
                    }
                    catch (PostgresException pgEx) when (pgEx.SqlState == "42501") // insufficient_privilege
                    {
                        _logger.LogWarning(
                            "Cannot create pgvector extension due to insufficient privileges (requires superuser). " +
                            "Falling back to regular column storage without vector similarity search. " +
                            "To enable vector search, have a database administrator run: CREATE EXTENSION vector;");
                        useVectorExtension = false;
                    }
                    catch (PostgresException pgEx) when (pgEx.SqlState == "58P01") // undefined_file
                    {
                        _logger.LogWarning(
                            "pgvector extension is not installed on this PostgreSQL server. " +
                            "Falling back to regular column storage without vector similarity search. " +
                            "To enable vector search, install pgvector: https://github.com/pgvector/pgvector");
                        useVectorExtension = false;
                    }
                }
            }

            // Create patterns table - use vector type if extension available, otherwise TEXT
            string createTableSql;
            if (useVectorExtension)
            {
                createTableSql = $@"
                    CREATE TABLE IF NOT EXISTS {_tableName} (
                        id TEXT PRIMARY KEY,
                        content TEXT NOT NULL,
                        embedding vector({_embeddingProvider.Dimensions}) NOT NULL,
                        pattern_name TEXT NOT NULL,
                        cloud_provider TEXT NOT NULL,
                        data_volume_min INTEGER NOT NULL,
                        data_volume_max INTEGER NOT NULL,
                        concurrent_users_min INTEGER NOT NULL,
                        concurrent_users_max INTEGER NOT NULL,
                        success_rate DOUBLE PRECISION NOT NULL,
                        deployment_count INTEGER NOT NULL,
                        configuration JSONB NOT NULL,
                        human_approved BOOLEAN NOT NULL DEFAULT true,
                        approved_by TEXT,
                        approved_date TIMESTAMP,
                        created_at TIMESTAMP DEFAULT NOW()
                    );";
            }
            else
            {
                // Fallback: Store embedding as TEXT (JSON array) when pgvector not available
                createTableSql = $@"
                    CREATE TABLE IF NOT EXISTS {_tableName} (
                        id TEXT PRIMARY KEY,
                        content TEXT NOT NULL,
                        embedding TEXT NOT NULL,
                        pattern_name TEXT NOT NULL,
                        cloud_provider TEXT NOT NULL,
                        data_volume_min INTEGER NOT NULL,
                        data_volume_max INTEGER NOT NULL,
                        concurrent_users_min INTEGER NOT NULL,
                        concurrent_users_max INTEGER NOT NULL,
                        success_rate DOUBLE PRECISION NOT NULL,
                        deployment_count INTEGER NOT NULL,
                        configuration JSONB NOT NULL,
                        human_approved BOOLEAN NOT NULL DEFAULT true,
                        approved_by TEXT,
                        approved_date TIMESTAMP,
                        created_at TIMESTAMP DEFAULT NOW()
                    );";

                _logger.LogWarning(
                    "Vector similarity search disabled. Embeddings will be stored but search will use metadata filters only. " +
                    "Pattern matching will be less accurate without semantic similarity.");
            }

            await using (var cmd = new NpgsqlCommand(createTableSql, connection))
            {
                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            // Create vector index only if pgvector extension is available
            if (useVectorExtension)
            {
                try
                {
                    var createIndexSql = $@"
                        CREATE INDEX IF NOT EXISTS {_tableName}_embedding_idx
                        ON {_tableName}
                        USING ivfflat (embedding vector_cosine_ops)
                        WITH (lists = 100);";

                    await using var cmd = new NpgsqlCommand(createIndexSql, connection);
                    await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

                    _logger.LogInformation("Vector index created successfully");
                }
                catch (Exception indexEx)
                {
                    _logger.LogWarning(indexEx, "Failed to create vector index, vector search may be slower");
                }
            }

            _logger.LogInformation("PostgreSQL schema ensured successfully (vector extension: {VectorEnabled})", useVectorExtension);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure PostgreSQL schema");
            throw;
        }
    }

    /// <summary>
    /// Indexes a human-approved deployment pattern for semantic search.
    /// </summary>
    public async Task IndexApprovedPatternAsync(DeploymentPattern pattern, CancellationToken cancellationToken = default)
    {
        try
        {
            // Generate embedding for semantic search
            var embeddingText = GenerateEmbeddingText(pattern);
            var embeddingResponse = await _embeddingProvider.GetEmbeddingAsync(embeddingText, cancellationToken);

            if (!embeddingResponse.Success)
            {
                throw new InvalidOperationException($"Failed to generate embedding: {embeddingResponse.ErrorMessage}");
            }

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            var sql = $@"
                INSERT INTO {_tableName} (
                    id, content, embedding, pattern_name, cloud_provider,
                    data_volume_min, data_volume_max, concurrent_users_min, concurrent_users_max,
                    success_rate, deployment_count, configuration,
                    human_approved, approved_by, approved_date
                )
                VALUES (
                    @id, @content, @embedding, @pattern_name, @cloud_provider,
                    @data_volume_min, @data_volume_max, @concurrent_users_min, @concurrent_users_max,
                    @success_rate, @deployment_count, @configuration::jsonb,
                    @human_approved, @approved_by, @approved_date
                )
                ON CONFLICT (id) DO UPDATE SET
                    content = EXCLUDED.content,
                    embedding = EXCLUDED.embedding,
                    pattern_name = EXCLUDED.pattern_name,
                    cloud_provider = EXCLUDED.cloud_provider,
                    data_volume_min = EXCLUDED.data_volume_min,
                    data_volume_max = EXCLUDED.data_volume_max,
                    concurrent_users_min = EXCLUDED.concurrent_users_min,
                    concurrent_users_max = EXCLUDED.concurrent_users_max,
                    success_rate = EXCLUDED.success_rate,
                    deployment_count = EXCLUDED.deployment_count,
                    configuration = EXCLUDED.configuration,
                    human_approved = EXCLUDED.human_approved,
                    approved_by = EXCLUDED.approved_by,
                    approved_date = EXCLUDED.approved_date;";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("id", pattern.Id);
            cmd.Parameters.AddWithValue("content", embeddingText);
            cmd.Parameters.AddWithValue("embedding", embeddingResponse.Embedding);
            cmd.Parameters.AddWithValue("pattern_name", pattern.Name);
            cmd.Parameters.AddWithValue("cloud_provider", pattern.CloudProvider);
            cmd.Parameters.AddWithValue("data_volume_min", pattern.DataVolumeMin);
            cmd.Parameters.AddWithValue("data_volume_max", pattern.DataVolumeMax);
            cmd.Parameters.AddWithValue("concurrent_users_min", pattern.ConcurrentUsersMin);
            cmd.Parameters.AddWithValue("concurrent_users_max", pattern.ConcurrentUsersMax);
            cmd.Parameters.AddWithValue("success_rate", pattern.SuccessRate);
            cmd.Parameters.AddWithValue("deployment_count", pattern.DeploymentCount);
            cmd.Parameters.AddWithValue("configuration", JsonSerializer.Serialize(pattern.Configuration));
            cmd.Parameters.AddWithValue("human_approved", pattern.HumanApproved);
            cmd.Parameters.AddWithValue("approved_by", pattern.ApprovedBy ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("approved_date", pattern.ApprovedDate);

            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Indexed pattern {PatternId} - {PatternName} (approved by {ApprovedBy})",
                pattern.Id, pattern.Name, pattern.ApprovedBy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index pattern {PatternId}", pattern.Id);
            throw;
        }
    }

    /// <summary>
    /// Searches for deployment patterns using vector similarity search.
    /// </summary>
    public async Task<List<PatternSearchResult>> SearchPatternsAsync(
        DeploymentRequirements requirements,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Generate query embedding
            var queryText = GenerateQueryText(requirements);
            var embeddingResponse = await _embeddingProvider.GetEmbeddingAsync(queryText, cancellationToken);

            if (!embeddingResponse.Success)
            {
                throw new InvalidOperationException($"Failed to generate query embedding: {embeddingResponse.ErrorMessage}");
            }

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

            // Use pgvector cosine distance operator (<=>)
            // Lower distance = more similar
            var sql = $@"
                SELECT
                    id, pattern_name, content, configuration,
                    success_rate, deployment_count, cloud_provider,
                    1 - (embedding <=> @embedding) as score
                FROM {_tableName}
                WHERE
                    human_approved = true
                    AND cloud_provider = @cloud_provider
                    AND data_volume_min <= @data_volume
                    AND data_volume_max >= @data_volume
                    AND concurrent_users_min <= @concurrent_users
                    AND concurrent_users_max >= @concurrent_users
                ORDER BY embedding <=> @embedding
                LIMIT 3;";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("embedding", embeddingResponse.Embedding);
            cmd.Parameters.AddWithValue("cloud_provider", requirements.CloudProvider);
            cmd.Parameters.AddWithValue("data_volume", requirements.DataVolumeGb);
            cmd.Parameters.AddWithValue("concurrent_users", requirements.ConcurrentUsers);

            var matches = new List<PatternSearchResult>();

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken))
            {
                matches.Add(new PatternSearchResult
                {
                    Id = reader.GetString(0),
                    PatternName = reader.GetString(1),
                    Content = reader.GetString(2),
                    ConfigurationJson = reader.GetString(3),
                    SuccessRate = reader.GetDouble(4),
                    DeploymentCount = reader.GetInt32(5),
                    CloudProvider = reader.GetString(6),
                    Score = reader.GetDouble(7)
                });
            }

            _logger.LogInformation(
                "Pattern search completed - Query: '{Query}', Matches: {Count}",
                queryText, matches.Count);

            return matches;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pattern search failed");
            throw;
        }
    }

    private static string GenerateEmbeddingText(DeploymentPattern pattern)
        => DeploymentPatternTextGenerator.CreateEmbeddingText(pattern);

    private static string GenerateQueryText(DeploymentRequirements requirements)
        => DeploymentPatternTextGenerator.CreateQueryText(requirements);
}
