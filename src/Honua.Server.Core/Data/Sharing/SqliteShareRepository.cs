// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Dapper;
using Honua.Server.Core.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Honua.Server.Core.Data.Sharing;

/// <summary>
/// SQLite implementation of share repository
/// </summary>
public class SqliteShareRepository : IShareRepository
{
    private readonly string _connectionString;
    private readonly ILogger<SqliteShareRepository> _logger;

    public SqliteShareRepository(string connectionString, ILogger<SqliteShareRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async ValueTask EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Create share_tokens table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS share_tokens (
                token TEXT PRIMARY KEY,
                map_id TEXT NOT NULL,
                created_by TEXT,
                permission TEXT NOT NULL DEFAULT 'view',
                allow_guest_access INTEGER NOT NULL DEFAULT 1,
                expires_at TEXT,
                created_at TEXT NOT NULL,
                access_count INTEGER NOT NULL DEFAULT 0,
                last_accessed_at TEXT,
                is_active INTEGER NOT NULL DEFAULT 1,
                password_hash TEXT,
                embed_settings TEXT
            )
        ");

        await connection.ExecuteAsync(@"
            CREATE INDEX IF NOT EXISTS idx_share_tokens_map_id ON share_tokens(map_id)
        ");

        await connection.ExecuteAsync(@"
            CREATE INDEX IF NOT EXISTS idx_share_tokens_created_by ON share_tokens(created_by)
        ");

        // Create share_comments table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS share_comments (
                id TEXT PRIMARY KEY,
                share_token TEXT NOT NULL,
                map_id TEXT NOT NULL,
                author TEXT NOT NULL,
                is_guest INTEGER NOT NULL DEFAULT 1,
                guest_email TEXT,
                comment_text TEXT NOT NULL,
                created_at TEXT NOT NULL,
                is_approved INTEGER NOT NULL DEFAULT 0,
                is_deleted INTEGER NOT NULL DEFAULT 0,
                parent_id TEXT,
                location_x REAL,
                location_y REAL,
                ip_address TEXT,
                user_agent TEXT,
                FOREIGN KEY (share_token) REFERENCES share_tokens(token) ON DELETE CASCADE
            )
        ");

        await connection.ExecuteAsync(@"
            CREATE INDEX IF NOT EXISTS idx_share_comments_token ON share_comments(share_token)
        ");

        await connection.ExecuteAsync(@"
            CREATE INDEX IF NOT EXISTS idx_share_comments_map_id ON share_comments(map_id)
        ");

        _logger.LogInformation("Share repository schema initialized");
    }

    public async ValueTask<ShareToken> CreateShareTokenAsync(ShareToken token, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(@"
            INSERT INTO share_tokens (token, map_id, created_by, permission, allow_guest_access,
                expires_at, created_at, access_count, is_active, password_hash, embed_settings)
            VALUES (@Token, @MapId, @CreatedBy, @Permission, @AllowGuestAccess,
                @ExpiresAt, @CreatedAt, @AccessCount, @IsActive, @PasswordHash, @EmbedSettings)
        ", new
        {
            token.Token,
            token.MapId,
            token.CreatedBy,
            token.Permission,
            AllowGuestAccess = token.AllowGuestAccess ? 1 : 0,
            ExpiresAt = token.ExpiresAt?.ToString("O"),
            CreatedAt = token.CreatedAt.ToString("O"),
            token.AccessCount,
            IsActive = token.IsActive ? 1 : 0,
            token.PasswordHash,
            token.EmbedSettings
        });

        _logger.LogInformation("Created share token {Token} for map {MapId}", token.Token, token.MapId);
        return token;
    }

    public async ValueTask<ShareToken?> GetShareTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var result = await connection.QueryFirstOrDefaultAsync<ShareTokenDto>(@"
            SELECT token, map_id, created_by, permission, allow_guest_access,
                expires_at, created_at, access_count, last_accessed_at, is_active,
                password_hash, embed_settings
            FROM share_tokens
            WHERE token = @Token
        ", new { Token = token });

        return result != null ? MapToShareToken(result) : null;
    }

    public async ValueTask<IReadOnlyList<ShareToken>> GetShareTokensByMapIdAsync(string mapId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var results = await connection.QueryAsync<ShareTokenDto>(@"
            SELECT token, map_id, created_by, permission, allow_guest_access,
                expires_at, created_at, access_count, last_accessed_at, is_active,
                password_hash, embed_settings
            FROM share_tokens
            WHERE map_id = @MapId
            ORDER BY created_at DESC
        ", new { MapId = mapId });

        return results.Select(MapToShareToken).ToList();
    }

    public async ValueTask UpdateShareTokenAsync(ShareToken token, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(@"
            UPDATE share_tokens
            SET permission = @Permission,
                allow_guest_access = @AllowGuestAccess,
                expires_at = @ExpiresAt,
                is_active = @IsActive,
                password_hash = @PasswordHash,
                embed_settings = @EmbedSettings
            WHERE token = @Token
        ", new
        {
            token.Token,
            token.Permission,
            AllowGuestAccess = token.AllowGuestAccess ? 1 : 0,
            ExpiresAt = token.ExpiresAt?.ToString("O"),
            IsActive = token.IsActive ? 1 : 0,
            token.PasswordHash,
            token.EmbedSettings
        });

        _logger.LogInformation("Updated share token {Token}", token.Token);
    }

    public async ValueTask DeactivateShareTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(@"
            UPDATE share_tokens
            SET is_active = 0
            WHERE token = @Token
        ", new { Token = token });

        _logger.LogInformation("Deactivated share token {Token}", token);
    }

    public async ValueTask IncrementAccessCountAsync(string token, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(@"
            UPDATE share_tokens
            SET access_count = access_count + 1,
                last_accessed_at = @Now
            WHERE token = @Token
        ", new { Token = token, Now = DateTime.UtcNow.ToString("O") });
    }

    public async ValueTask<int> DeleteExpiredTokensAsync(CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var deleted = await connection.ExecuteAsync(@"
            DELETE FROM share_tokens
            WHERE expires_at IS NOT NULL
                AND expires_at < @Now
        ", new { Now = DateTime.UtcNow.ToString("O") });

        if (deleted > 0)
        {
            _logger.LogInformation("Deleted {Count} expired share tokens", deleted);
        }

        return deleted;
    }

    public async ValueTask<ShareComment> CreateCommentAsync(ShareComment comment, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(@"
            INSERT INTO share_comments (id, share_token, map_id, author, is_guest, guest_email,
                comment_text, created_at, is_approved, is_deleted, parent_id,
                location_x, location_y, ip_address, user_agent)
            VALUES (@Id, @ShareToken, @MapId, @Author, @IsGuest, @GuestEmail,
                @CommentText, @CreatedAt, @IsApproved, @IsDeleted, @ParentId,
                @LocationX, @LocationY, @IpAddress, @UserAgent)
        ", new
        {
            comment.Id,
            comment.ShareToken,
            comment.MapId,
            comment.Author,
            IsGuest = comment.IsGuest ? 1 : 0,
            comment.GuestEmail,
            comment.CommentText,
            CreatedAt = comment.CreatedAt.ToString("O"),
            IsApproved = comment.IsApproved ? 1 : 0,
            IsDeleted = comment.IsDeleted ? 1 : 0,
            comment.ParentId,
            comment.LocationX,
            comment.LocationY,
            comment.IpAddress,
            comment.UserAgent
        });

        _logger.LogInformation("Created comment {CommentId} on share token {Token}", comment.Id, comment.ShareToken);
        return comment;
    }

    public async ValueTask<IReadOnlyList<ShareComment>> GetCommentsByTokenAsync(string token, bool includeUnapproved = false, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = @"
            SELECT id, share_token, map_id, author, is_guest, guest_email,
                comment_text, created_at, is_approved, is_deleted, parent_id,
                location_x, location_y, ip_address, user_agent
            FROM share_comments
            WHERE share_token = @Token
                AND is_deleted = 0";

        if (!includeUnapproved)
        {
            sql += " AND is_approved = 1";
        }

        sql += " ORDER BY created_at DESC";

        var results = await connection.QueryAsync<ShareCommentDto>(sql, new { Token = token });
        return results.Select(MapToShareComment).ToList();
    }

    public async ValueTask<IReadOnlyList<ShareComment>> GetCommentsByMapIdAsync(string mapId, bool includeUnapproved = false, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = @"
            SELECT id, share_token, map_id, author, is_guest, guest_email,
                comment_text, created_at, is_approved, is_deleted, parent_id,
                location_x, location_y, ip_address, user_agent
            FROM share_comments
            WHERE map_id = @MapId
                AND is_deleted = 0";

        if (!includeUnapproved)
        {
            sql += " AND is_approved = 1";
        }

        sql += " ORDER BY created_at DESC";

        var results = await connection.QueryAsync<ShareCommentDto>(sql, new { MapId = mapId });
        return results.Select(MapToShareComment).ToList();
    }

    public async ValueTask ApproveCommentAsync(string commentId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(@"
            UPDATE share_comments
            SET is_approved = 1
            WHERE id = @Id
        ", new { Id = commentId });

        _logger.LogInformation("Approved comment {CommentId}", commentId);
    }

    public async ValueTask DeleteCommentAsync(string commentId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await connection.ExecuteAsync(@"
            UPDATE share_comments
            SET is_deleted = 1
            WHERE id = @Id
        ", new { Id = commentId });

        _logger.LogInformation("Deleted comment {CommentId}", commentId);
    }

    public async ValueTask<IReadOnlyList<ShareComment>> GetPendingCommentsAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var results = await connection.QueryAsync<ShareCommentDto>(@"
            SELECT id, share_token, map_id, author, is_guest, guest_email,
                comment_text, created_at, is_approved, is_deleted, parent_id,
                location_x, location_y, ip_address, user_agent
            FROM share_comments
            WHERE is_approved = 0
                AND is_deleted = 0
            ORDER BY created_at ASC
            LIMIT @Limit
        ", new { Limit = limit });

        return results.Select(MapToShareComment).ToList();
    }

    // Helper classes for mapping
    private class ShareTokenDto
    {
        public string token { get; set; } = string.Empty;
        public string map_id { get; set; } = string.Empty;
        public string? created_by { get; set; }
        public string permission { get; set; } = string.Empty;
        public int allow_guest_access { get; set; }
        public string? expires_at { get; set; }
        public string created_at { get; set; } = string.Empty;
        public int access_count { get; set; }
        public string? last_accessed_at { get; set; }
        public int is_active { get; set; }
        public string? password_hash { get; set; }
        public string? embed_settings { get; set; }
    }

    private class ShareCommentDto
    {
        public string id { get; set; } = string.Empty;
        public string share_token { get; set; } = string.Empty;
        public string map_id { get; set; } = string.Empty;
        public string author { get; set; } = string.Empty;
        public int is_guest { get; set; }
        public string? guest_email { get; set; }
        public string comment_text { get; set; } = string.Empty;
        public string created_at { get; set; } = string.Empty;
        public int is_approved { get; set; }
        public int is_deleted { get; set; }
        public string? parent_id { get; set; }
        public double? location_x { get; set; }
        public double? location_y { get; set; }
        public string? ip_address { get; set; }
        public string? user_agent { get; set; }
    }

    private static ShareToken MapToShareToken(ShareTokenDto dto)
    {
        return new ShareToken
        {
            Token = dto.token,
            MapId = dto.map_id,
            CreatedBy = dto.created_by,
            Permission = dto.permission,
            AllowGuestAccess = dto.allow_guest_access == 1,
            ExpiresAt = string.IsNullOrEmpty(dto.expires_at) ? null : DateTime.Parse(dto.expires_at),
            CreatedAt = DateTime.Parse(dto.created_at),
            AccessCount = dto.access_count,
            LastAccessedAt = string.IsNullOrEmpty(dto.last_accessed_at) ? null : DateTime.Parse(dto.last_accessed_at),
            IsActive = dto.is_active == 1,
            PasswordHash = dto.password_hash,
            EmbedSettings = dto.embed_settings
        };
    }

    private static ShareComment MapToShareComment(ShareCommentDto dto)
    {
        return new ShareComment
        {
            Id = dto.id,
            ShareToken = dto.share_token,
            MapId = dto.map_id,
            Author = dto.author,
            IsGuest = dto.is_guest == 1,
            GuestEmail = dto.guest_email,
            CommentText = dto.comment_text,
            CreatedAt = DateTime.Parse(dto.created_at),
            IsApproved = dto.is_approved == 1,
            IsDeleted = dto.is_deleted == 1,
            ParentId = dto.parent_id,
            LocationX = dto.location_x,
            LocationY = dto.location_y,
            IpAddress = dto.ip_address,
            UserAgent = dto.user_agent
        };
    }
}
