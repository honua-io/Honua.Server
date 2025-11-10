// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using SQLite;

namespace HonuaField.Models;

/// <summary>
/// GPS track entity representing a recorded breadcrumb trail
/// Stored in SQLite database
/// </summary>
[Table("gps_tracks")]
public class GpsTrack
{
	[PrimaryKey]
	[Column("id")]
	public string Id { get; set; } = Guid.NewGuid().ToString();

	[Column("name")]
	public string? Name { get; set; }

	[Column("start_time")]
	[NotNull]
	public long StartTime { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

	[Column("end_time")]
	public long? EndTime { get; set; }

	[Column("point_count")]
	[NotNull]
	public int PointCount { get; set; } = 0;

	[Column("total_distance")]
	[NotNull]
	public double TotalDistance { get; set; } = 0;

	[Column("duration_seconds")]
	[NotNull]
	public long DurationSeconds { get; set; } = 0;

	[Column("max_speed")]
	public double? MaxSpeed { get; set; }

	[Column("average_speed")]
	public double? AverageSpeed { get; set; }

	[Column("min_elevation")]
	public double? MinElevation { get; set; }

	[Column("max_elevation")]
	public double? MaxElevation { get; set; }

	[Column("elevation_gain")]
	public double? ElevationGain { get; set; }

	[Column("elevation_loss")]
	public double? ElevationLoss { get; set; }

	[Column("status")]
	[NotNull]
	public string Status { get; set; } = "Completed";

	[Column("created_at")]
	[NotNull]
	public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

	[Column("updated_at")]
	[NotNull]
	public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

	/// <summary>
	/// Convert to GpsTrackInfo
	/// </summary>
	public GpsTrackInfo ToInfo()
	{
		return new GpsTrackInfo
		{
			Id = Id,
			Name = Name,
			StartTime = DateTimeOffset.FromUnixTimeSeconds(StartTime),
			EndTime = EndTime.HasValue ? DateTimeOffset.FromUnixTimeSeconds(EndTime.Value) : null,
			PointCount = PointCount,
			TotalDistance = TotalDistance,
			Duration = TimeSpan.FromSeconds(DurationSeconds),
			MaxSpeed = MaxSpeed,
			AverageSpeed = AverageSpeed,
			MinElevation = MinElevation,
			MaxElevation = MaxElevation,
			ElevationGain = ElevationGain,
			ElevationLoss = ElevationLoss
		};
	}
}
