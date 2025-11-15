// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Domain.Sharing;

namespace Honua.Server.Domain.Tests.Sharing;

/// <summary>
/// Test data builder for ShareAggregate.
/// Provides fluent API for creating test shares with various configurations.
/// </summary>
public sealed class ShareAggregateBuilder
{
    private string _mapId = "test-map-id";
    private string? _createdBy = "test-user";
    private SharePermission _permission = SharePermission.View;
    private bool _allowGuestAccess = true;
    private DateTime? _expiresAt = null;
    private SharePassword? _password = null;
    private ShareConfiguration? _configuration = null;

    /// <summary>
    /// Sets the map ID for the share
    /// </summary>
    public ShareAggregateBuilder WithMapId(string mapId)
    {
        _mapId = mapId;
        return this;
    }

    /// <summary>
    /// Sets the creator of the share
    /// </summary>
    public ShareAggregateBuilder WithCreatedBy(string? createdBy)
    {
        _createdBy = createdBy;
        return this;
    }

    /// <summary>
    /// Sets the permission level
    /// </summary>
    public ShareAggregateBuilder WithPermission(SharePermission permission)
    {
        _permission = permission;
        return this;
    }

    /// <summary>
    /// Configures guest access
    /// </summary>
    public ShareAggregateBuilder WithGuestAccess(bool allow)
    {
        _allowGuestAccess = allow;
        return this;
    }

    /// <summary>
    /// Sets an expiration date
    /// </summary>
    public ShareAggregateBuilder WithExpiresAt(DateTime? expiresAt)
    {
        _expiresAt = expiresAt;
        return this;
    }

    /// <summary>
    /// Sets the share to expire in the specified number of days
    /// </summary>
    public ShareAggregateBuilder ExpiresInDays(int days)
    {
        _expiresAt = DateTime.UtcNow.AddDays(days);
        return this;
    }

    /// <summary>
    /// Sets the share to be already expired
    /// </summary>
    public ShareAggregateBuilder Expired()
    {
        _expiresAt = DateTime.UtcNow.AddDays(-1);
        return this;
    }

    /// <summary>
    /// Adds password protection
    /// </summary>
    public ShareAggregateBuilder WithPassword(string password)
    {
        _password = SharePassword.Create(password);
        return this;
    }

    /// <summary>
    /// Sets a custom configuration
    /// </summary>
    public ShareAggregateBuilder WithConfiguration(ShareConfiguration configuration)
    {
        _configuration = configuration;
        return this;
    }

    /// <summary>
    /// Builds the ShareAggregate
    /// </summary>
    public ShareAggregate Build()
    {
        return ShareAggregate.Create(
            _mapId,
            _createdBy,
            _permission,
            _allowGuestAccess,
            _expiresAt,
            _password,
            _configuration);
    }

    /// <summary>
    /// Creates a default share for testing
    /// </summary>
    public static ShareAggregate CreateDefault()
    {
        return new ShareAggregateBuilder().Build();
    }

    /// <summary>
    /// Creates a share with comment permission
    /// </summary>
    public static ShareAggregate CreateWithCommentPermission()
    {
        return new ShareAggregateBuilder()
            .WithPermission(SharePermission.Comment)
            .Build();
    }

    /// <summary>
    /// Creates a share with edit permission
    /// </summary>
    public static ShareAggregate CreateWithEditPermission()
    {
        return new ShareAggregateBuilder()
            .WithPermission(SharePermission.Edit)
            .Build();
    }

    /// <summary>
    /// Creates a password-protected share
    /// </summary>
    public static ShareAggregate CreatePasswordProtected(string password = "test-password")
    {
        return new ShareAggregateBuilder()
            .WithPassword(password)
            .Build();
    }

    /// <summary>
    /// Creates an expired share
    /// </summary>
    public static ShareAggregate CreateExpired()
    {
        return new ShareAggregateBuilder()
            .Expired()
            .Build();
    }
}
