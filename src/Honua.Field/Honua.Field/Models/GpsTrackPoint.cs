// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using SQLite;

namespace HonuaField.Models;

/// <summary>
/// GPS track point entity representing a single location point in a track
/// Stored in SQLite database
/// </summary>
[Table("gps_track_points")]
public class GpsTrackPoint
{
	[PrimaryKey]
	[Column("id")]
	public string Id { get; set; } = Guid.NewGuid().ToString();

	[Column("track_id")]
	[NotNull]
	[Indexed]
	public string TrackId { get; set; } = string.Empty;

	[Column("latitude")]
	[NotNull]
	public double Latitude { get; set; }

	[Column("longitude")]
	[NotNull]
	public double Longitude { get; set; }

	[Column("altitude")]
	public double? Altitude { get; set; }

	[Column("accuracy")]
	public double? Accuracy { get; set; }

	[Column("speed")]
	public double? Speed { get; set; }

	[Column("course")]
	public double? Course { get; set; }

	[Column("timestamp")]
	[NotNull]
	[Indexed]
	public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

	[Column("sequence_number")]
	[NotNull]
	public int SequenceNumber { get; set; }

	[Column("distance_from_previous")]
	[NotNull]
	public double DistanceFromPrevious { get; set; } = 0;

	[Column("time_from_previous")]
	[NotNull]
	public long TimeFromPrevious { get; set; } = 0;

	/// <summary>
	/// Convert to GpsTrackPointInfo
	/// </summary>
	public GpsTrackPointInfo ToInfo()
	{
		return new GpsTrackPointInfo
		{
			Id = Id,
			TrackId = TrackId,
			Latitude = Latitude,
			Longitude = Longitude,
			Altitude = Altitude,
			Accuracy = Accuracy,
			Speed = Speed,
			Course = Course,
			Timestamp = DateTimeOffset.FromUnixTimeSeconds(Timestamp),
			SequenceNumber = SequenceNumber
		};
	}
}
