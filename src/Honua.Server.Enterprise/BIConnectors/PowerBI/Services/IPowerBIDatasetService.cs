// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.PowerBI.Api.Models;

namespace Honua.Server.Enterprise.BIConnectors.PowerBI.Services;

/// <summary>
/// Service for managing Power BI datasets programmatically
/// </summary>
public interface IPowerBIDatasetService
{
    /// <summary>
    /// Creates or updates a Power BI dataset for a smart city dashboard
    /// </summary>
    /// <param name="dashboardType">Type of dashboard (Traffic, AirQuality, etc.)</param>
    /// <param name="collectionIds">OGC Collections to include</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dataset ID</returns>
    Task<string> CreateOrUpdateDatasetAsync(
        string dashboardType,
        IEnumerable<string> collectionIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a streaming dataset for real-time data
    /// </summary>
    /// <param name="datasetName">Name of the dataset</param>
    /// <param name="schema">Dataset schema</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dataset info with push URL</returns>
    Task<(string DatasetId, string PushUrl)> CreateStreamingDatasetAsync(
        string datasetName,
        object schema,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pushes rows to a streaming dataset
    /// </summary>
    /// <param name="datasetId">Dataset ID</param>
    /// <param name="tableName">Table name</param>
    /// <param name="rows">Rows to push</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task PushRowsAsync(
        string datasetId,
        string tableName,
        IEnumerable<object> rows,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all datasets in the configured workspace
    /// </summary>
    Task<IEnumerable<Dataset>> GetDatasetsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a dataset
    /// </summary>
    Task DeleteDatasetAsync(string datasetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggers a dataset refresh
    /// </summary>
    Task RefreshDatasetAsync(string datasetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates an embed token for a report (for embedding in web apps)
    /// </summary>
    Task<string> GenerateEmbedTokenAsync(
        string reportId,
        string datasetId,
        CancellationToken cancellationToken = default);
}
