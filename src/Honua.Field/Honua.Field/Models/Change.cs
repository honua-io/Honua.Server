// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using SQLite;

namespace HonuaField.Models;

/// <summary>
/// Change entity for tracking offline edits in sync queue
/// Used for delta synchronization with server
/// </summary>
[Table("changes")]
public class Change
{
	[PrimaryKey, AutoIncrement]
	[Column("id")]
	public int Id { get; set; }

	[Column("feature_id")]
	[NotNull]
	public string FeatureId { get; set; } = string.Empty;

	[Column("operation")]
	[NotNull]
	public string Operation { get; set; } = ChangeOperation.Insert.ToString();

	[Column("timestamp")]
	[NotNull]
	public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

	[Column("synced")]
	[NotNull]
	public int Synced { get; set; } = 0; // SQLite doesn't have boolean, use 0/1

	// Navigation properties
	[Ignore]
	public Feature? Feature { get; set; }
}

/// <summary>
/// Change operation enum for sync tracking
/// </summary>
public enum ChangeOperation
{
	Insert,   // Feature created locally
	Update,   // Feature modified locally
	Delete    // Feature deleted locally
}
