// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Honua.Server.Intake.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Honua.Server.Intake.Services;

/// <summary>
/// PostgreSQL-based conversation store using Dapper.
/// </summary>
public sealed class ConversationStore : IConversationStore
{
    private readonly string _connectionString;
    private readonly ILogger<ConversationStore> _logger;

    public ConversationStore(IOptions<IntakeAgentOptions> options, ILogger<ConversationStore> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        var opts = options?.Value ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(opts.ConnectionString))
        {
            throw new ArgumentException("ConnectionString is required for ConversationStore", nameof(options));
        }

        _connectionString = opts.ConnectionString;
    }

    /// <inheritdoc/>
    public async Task SaveConversationAsync(ConversationRecord conversation, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Saving conversation {ConversationId}", conversation.ConversationId);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            INSERT INTO intake_conversations (
                conversation_id,
                customer_id,
                messages_json,
                status,
                requirements_json,
                created_at,
                updated_at,
                completed_at
            ) VALUES (
                @ConversationId,
                @CustomerId,
                @MessagesJson::jsonb,
                @Status,
                @RequirementsJson::jsonb,
                @CreatedAt,
                @UpdatedAt,
                @CompletedAt
            )
            ON CONFLICT (conversation_id) DO UPDATE SET
                messages_json = EXCLUDED.messages_json,
                status = EXCLUDED.status,
                requirements_json = EXCLUDED.requirements_json,
                updated_at = EXCLUDED.updated_at,
                completed_at = EXCLUDED.completed_at;
        ";

        var rowsAffected = await connection.ExecuteAsync(
            new CommandDefinition(sql, conversation, cancellationToken: cancellationToken));

        _logger.LogInformation("Saved conversation {ConversationId}, affected {RowsAffected} rows",
            conversation.ConversationId, rowsAffected);
    }

    /// <inheritdoc/>
    public async Task<ConversationRecord?> GetConversationAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving conversation {ConversationId}", conversationId);

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            SELECT
                conversation_id AS ConversationId,
                customer_id AS CustomerId,
                messages_json AS MessagesJson,
                status AS Status,
                requirements_json AS RequirementsJson,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt,
                completed_at AS CompletedAt
            FROM intake_conversations
            WHERE conversation_id = @ConversationId;
        ";

        var conversation = await connection.QuerySingleOrDefaultAsync<ConversationRecord>(
            new CommandDefinition(sql, new { ConversationId = conversationId }, cancellationToken: cancellationToken));

        if (conversation == null)
        {
            _logger.LogWarning("Conversation {ConversationId} not found", conversationId);
        }

        return conversation;
    }

    /// <summary>
    /// Initializes the database schema for conversation storage.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task InitializeDatabaseAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing conversation database schema");

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = @"
            CREATE TABLE IF NOT EXISTS intake_conversations (
                conversation_id TEXT PRIMARY KEY,
                customer_id TEXT,
                messages_json JSONB NOT NULL,
                status TEXT NOT NULL,
                requirements_json JSONB,
                created_at TIMESTAMPTZ NOT NULL,
                updated_at TIMESTAMPTZ NOT NULL,
                completed_at TIMESTAMPTZ
            );

            CREATE INDEX IF NOT EXISTS idx_intake_conversations_customer_id
                ON intake_conversations(customer_id)
                WHERE customer_id IS NOT NULL;

            CREATE INDEX IF NOT EXISTS idx_intake_conversations_status
                ON intake_conversations(status);

            CREATE INDEX IF NOT EXISTS idx_intake_conversations_created_at
                ON intake_conversations(created_at DESC);
        ";

        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));

        _logger.LogInformation("Conversation database schema initialized successfully");
    }
}
