// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Honua.MapSDK.Models.Streaming;

/// <summary>
/// Configuration for a WebSocket-based streaming data source
/// </summary>
public class StreamingDataSource
{
    /// <summary>
    /// Unique identifier for this data source
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// WebSocket endpoint URL
    /// </summary>
    public string WebSocketUrl { get; set; } = string.Empty;

    /// <summary>
    /// Message format for incoming data
    /// </summary>
    public MessageFormat Format { get; set; } = MessageFormat.GeoJson;

    /// <summary>
    /// Strategy for updating features
    /// </summary>
    public UpdateStrategy UpdateStrategy { get; set; } = UpdateStrategy.Upsert;

    /// <summary>
    /// Maximum number of features to keep in memory (0 = unlimited)
    /// </summary>
    public int MaxFeatures { get; set; } = 1000;

    /// <summary>
    /// Enable automatic reconnection on connection loss
    /// </summary>
    public bool EnableAutoReconnect { get; set; } = true;

    /// <summary>
    /// Initial delay before reconnecting (milliseconds)
    /// </summary>
    public int ReconnectDelay { get; set; } = 1000;

    /// <summary>
    /// Maximum reconnect delay with exponential backoff (milliseconds)
    /// </summary>
    public int MaxReconnectDelay { get; set; } = 30000;

    /// <summary>
    /// Maximum number of reconnection attempts (0 = unlimited)
    /// </summary>
    public int MaxReconnectAttempts { get; set; } = 0;

    /// <summary>
    /// Enable heartbeat/ping-pong for connection health monitoring
    /// </summary>
    public bool EnableHeartbeat { get; set; } = true;

    /// <summary>
    /// Heartbeat interval (milliseconds)
    /// </summary>
    public int HeartbeatInterval { get; set; } = 30000;

    /// <summary>
    /// Timeout for heartbeat response (milliseconds)
    /// </summary>
    public int HeartbeatTimeout { get; set; } = 5000;

    /// <summary>
    /// Authentication token for WebSocket connection
    /// </summary>
    public string? AuthToken { get; set; }

    /// <summary>
    /// Custom headers to send with WebSocket connection
    /// </summary>
    public Dictionary<string, string> CustomHeaders { get; set; } = new();

    /// <summary>
    /// Buffer size for incoming messages (number of messages)
    /// </summary>
    public int BufferSize { get; set; } = 100;

    /// <summary>
    /// Update throttle interval (milliseconds) - minimum time between updates
    /// </summary>
    public int UpdateThrottle { get; set; } = 100;

    /// <summary>
    /// Protocol/subprotocol for WebSocket connection
    /// </summary>
    public string? SubProtocol { get; set; }

    /// <summary>
    /// Custom connection options
    /// </summary>
    public Dictionary<string, object> Options { get; set; } = new();
}

/// <summary>
/// Message format for streaming data
/// </summary>
public enum MessageFormat
{
    /// <summary>
    /// GeoJSON format (Feature or FeatureCollection)
    /// </summary>
    GeoJson,

    /// <summary>
    /// Custom JSON format
    /// </summary>
    Json,

    /// <summary>
    /// Binary format (requires custom parser)
    /// </summary>
    Binary,

    /// <summary>
    /// Protocol Buffers
    /// </summary>
    Protobuf,

    /// <summary>
    /// MessagePack
    /// </summary>
    MessagePack
}

/// <summary>
/// Strategy for updating features on the map
/// </summary>
public enum UpdateStrategy
{
    /// <summary>
    /// Replace all features with new data
    /// </summary>
    Replace,

    /// <summary>
    /// Insert or update features by ID
    /// </summary>
    Upsert,

    /// <summary>
    /// Accumulate features (add new, keep old)
    /// </summary>
    Accumulate,

    /// <summary>
    /// Only update existing features (no new additions)
    /// </summary>
    UpdateOnly
}

/// <summary>
/// Connection state for streaming data source
/// </summary>
public enum ConnectionState
{
    /// <summary>
    /// Not connected
    /// </summary>
    Disconnected,

    /// <summary>
    /// Connecting to WebSocket
    /// </summary>
    Connecting,

    /// <summary>
    /// Connected and active
    /// </summary>
    Connected,

    /// <summary>
    /// Connection failed or error occurred
    /// </summary>
    Failed,

    /// <summary>
    /// Reconnecting after disconnect
    /// </summary>
    Reconnecting
}

/// <summary>
/// Streaming update message containing feature data
/// </summary>
public class StreamingUpdate
{
    /// <summary>
    /// Update type
    /// </summary>
    public UpdateType Type { get; set; } = UpdateType.Feature;

    /// <summary>
    /// Feature ID (for updates/deletes)
    /// </summary>
    public string? FeatureId { get; set; }

    /// <summary>
    /// GeoJSON feature data
    /// </summary>
    public object? Data { get; set; }

    /// <summary>
    /// Timestamp of update
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Type of streaming update
/// </summary>
public enum UpdateType
{
    /// <summary>
    /// Add or update a feature
    /// </summary>
    Feature,

    /// <summary>
    /// Delete a feature
    /// </summary>
    Delete,

    /// <summary>
    /// Clear all features
    /// </summary>
    Clear,

    /// <summary>
    /// Batch update
    /// </summary>
    Batch,

    /// <summary>
    /// Heartbeat/ping message
    /// </summary>
    Heartbeat
}

/// <summary>
/// Statistics for streaming connection
/// </summary>
public class StreamingStatistics
{
    /// <summary>
    /// Current connection state
    /// </summary>
    public ConnectionState State { get; set; } = ConnectionState.Disconnected;

    /// <summary>
    /// Total messages received
    /// </summary>
    public long MessagesReceived { get; set; }

    /// <summary>
    /// Total features updated
    /// </summary>
    public long FeaturesUpdated { get; set; }

    /// <summary>
    /// Total features deleted
    /// </summary>
    public long FeaturesDeleted { get; set; }

    /// <summary>
    /// Current feature count
    /// </summary>
    public int CurrentFeatureCount { get; set; }

    /// <summary>
    /// Messages per second (average)
    /// </summary>
    public double MessagesPerSecond { get; set; }

    /// <summary>
    /// Connection start time
    /// </summary>
    public DateTime? ConnectedAt { get; set; }

    /// <summary>
    /// Last message received time
    /// </summary>
    public DateTime? LastMessageAt { get; set; }

    /// <summary>
    /// Number of reconnection attempts
    /// </summary>
    public int ReconnectAttempts { get; set; }

    /// <summary>
    /// Total errors encountered
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Bytes received
    /// </summary>
    public long BytesReceived { get; set; }

    /// <summary>
    /// Connection uptime
    /// </summary>
    public TimeSpan Uptime => ConnectedAt.HasValue
        ? DateTime.UtcNow - ConnectedAt.Value
        : TimeSpan.Zero;
}

/// <summary>
/// Reconnection policy configuration
/// </summary>
public class ReconnectionPolicy
{
    /// <summary>
    /// Enable automatic reconnection
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Initial delay (milliseconds)
    /// </summary>
    public int InitialDelay { get; set; } = 1000;

    /// <summary>
    /// Maximum delay (milliseconds)
    /// </summary>
    public int MaxDelay { get; set; } = 30000;

    /// <summary>
    /// Exponential backoff multiplier
    /// </summary>
    public double BackoffMultiplier { get; set; } = 1.5;

    /// <summary>
    /// Maximum number of attempts (0 = unlimited)
    /// </summary>
    public int MaxAttempts { get; set; } = 0;

    /// <summary>
    /// Reset attempt counter after successful connection duration (milliseconds)
    /// </summary>
    public int ResetAfter { get; set; } = 60000;
}

/// <summary>
/// Authentication configuration for WebSocket connection
/// </summary>
public class StreamingAuthConfiguration
{
    /// <summary>
    /// Authentication type
    /// </summary>
    public AuthenticationType Type { get; set; } = AuthenticationType.None;

    /// <summary>
    /// Bearer token for authorization header
    /// </summary>
    public string? BearerToken { get; set; }

    /// <summary>
    /// API key for query parameter or header
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// API key header name
    /// </summary>
    public string ApiKeyHeader { get; set; } = "X-API-Key";

    /// <summary>
    /// Custom headers for authentication
    /// </summary>
    public Dictionary<string, string> CustomHeaders { get; set; } = new();

    /// <summary>
    /// Query parameters for authentication
    /// </summary>
    public Dictionary<string, string> QueryParameters { get; set; } = new();
}

/// <summary>
/// Authentication type for WebSocket connection
/// </summary>
public enum AuthenticationType
{
    /// <summary>
    /// No authentication
    /// </summary>
    None,

    /// <summary>
    /// Bearer token in authorization header
    /// </summary>
    Bearer,

    /// <summary>
    /// API key in header or query parameter
    /// </summary>
    ApiKey,

    /// <summary>
    /// Basic authentication
    /// </summary>
    Basic,

    /// <summary>
    /// Custom authentication
    /// </summary>
    Custom
}
