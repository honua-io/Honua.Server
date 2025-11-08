// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Migration.GeoservicesRest;

/// <summary>
/// Service for managing migration jobs from ArcGIS Geoservices REST API endpoints to Honua Server.
/// Provides functionality to import feature services, map services, and layers from external ArcGIS servers.
/// </summary>
/// <remarks>
/// This service orchestrates the migration of geospatial data and service configurations from:
/// - ArcGIS Server REST API endpoints
/// - ArcGIS Online feature services
/// - Portal for ArcGIS services
///
/// The migration process includes:
/// - Service metadata and configuration extraction
/// - Layer schema and symbology migration
/// - Feature data transfer with geometry preservation
/// - Field mapping and data type conversion
/// - Progress tracking and error handling
///
/// Migration jobs run asynchronously in the background and can be monitored via job snapshots.
/// </remarks>
public interface IGeoservicesRestMigrationService
{
    /// <summary>
    /// Enqueues a new migration job to import data from an ArcGIS Geoservices REST endpoint.
    /// The job will execute asynchronously in the background.
    /// </summary>
    /// <param name="plan">
    /// The migration plan containing source endpoint URL, authentication credentials,
    /// target layer configuration, and migration options.
    /// </param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>
    /// A <see cref="GeoservicesRestMigrationJobSnapshot"/> containing the job ID and initial status.
    /// Use the job ID to monitor progress via <see cref="TryGetJobAsync"/>.
    /// </returns>
    /// <exception cref="System.ArgumentNullException">Thrown when plan is null.</exception>
    /// <exception cref="System.ArgumentException">Thrown when plan contains invalid configuration.</exception>
    Task<GeoservicesRestMigrationJobSnapshot> EnqueueAsync(GeoservicesRestMigrationPlan plan, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the current status and progress of a migration job.
    /// </summary>
    /// <param name="jobId">The unique identifier of the migration job.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>
    /// A <see cref="GeoservicesRestMigrationJobSnapshot"/> containing job status, progress percentage,
    /// processed feature count, and any error messages. Returns null if the job is not found.
    /// </returns>
    Task<GeoservicesRestMigrationJobSnapshot?> TryGetJobAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all migration jobs, including active, completed, and failed jobs.
    /// Jobs are ordered by creation time (most recent first).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A read-only list of all migration job snapshots.</returns>
    Task<IReadOnlyList<GeoservicesRestMigrationJobSnapshot>> ListJobsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to cancel a running migration job.
    /// Jobs that have already completed or failed cannot be cancelled.
    /// </summary>
    /// <param name="jobId">The unique identifier of the job to cancel.</param>
    /// <param name="reason">Optional reason for cancellation (stored in job history).</param>
    /// <returns>
    /// A <see cref="GeoservicesRestMigrationJobSnapshot"/> with updated status if cancellation succeeded,
    /// or null if the job was not found or could not be cancelled.
    /// </returns>
    Task<GeoservicesRestMigrationJobSnapshot?> CancelAsync(Guid jobId, string? reason = null);
}
