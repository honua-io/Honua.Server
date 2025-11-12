// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Core.Data;

/// <summary>
/// Builds SQL queries for SQL views with parameter substitution.
/// This class works alongside provider-specific query builders (PostgresFeatureQueryBuilder, etc.)
/// to handle layers backed by SQL views instead of physical tables.
/// </summary>
public sealed class SqlViewQueryBuilder
{
    private readonly LayerDefinition _layer;
    private readonly SqlViewDefinition _sqlView;
    private readonly IReadOnlyDictionary<string, string> _requestParameters;

    public SqlViewQueryBuilder(
        LayerDefinition layer,
        IReadOnlyDictionary<string, string> requestParameters)
    {
        Guard.NotNull(layer);
        Guard.NotNull(requestParameters);

        _layer = layer;
        _sqlView = layer.SqlView ?? throw new ArgumentException("Layer does not have a SQL view defined", nameof(layer));
        _requestParameters = requestParameters;
    }

    /// <summary>
    /// Builds a SELECT query from the SQL view definition.
    /// </summary>
    /// <param name="query">The feature query with filters, sorting, and pagination.</param>
    /// <returns>SQL query definition with parameterized SQL and parameter values.</returns>
    public QueryDefinition BuildSelect(FeatureQuery query)
    {
        Guard.NotNull(query);

        // Process SQL view with parameters
        var (baseSql, sqlViewParams) = SqlViewExecutor.ProcessSqlView(
            _sqlView,
            _requestParameters,
            _layer.Id);

        var sql = new StringBuilder();
        var allParameters = new Dictionary<string, object?>(sqlViewParams, StringComparer.Ordinal);

        // Wrap the SQL view in a subquery so we can apply additional filters
        sql.Append("SELECT * FROM (");
        sql.AppendLine();
        sql.Append(baseSql);
        sql.AppendLine();
        sql.Append(") AS __sqlview");

        // Apply additional WHERE clause if filter is present
        if (query.Filter?.Expression is not null)
        {
            // Note: This would require a filter translator
            // For now, we'll skip this and handle it in the provider-specific implementation
            // TODO: Implement filter translation for SQL views
        }

        // Apply ORDER BY if specified
        if (query.SortOrders is { Count: > 0 })
        {
            sql.Append(" ORDER BY ");
            var first = true;
            foreach (var sortOrder in query.SortOrders)
            {
                if (!first) sql.Append(", ");
                first = false;

                sql.Append(QuoteIdentifier(sortOrder.Field));
                sql.Append(sortOrder.Direction == FeatureSortDirection.Descending ? " DESC" : " ASC");
            }
        }

        // Apply LIMIT and OFFSET
        if (query.Limit.HasValue)
        {
            sql.Append(" LIMIT ");
            sql.Append(query.Limit.Value);
        }

        if (query.Offset.HasValue && query.Offset.Value > 0)
        {
            sql.Append(" OFFSET ");
            sql.Append(query.Offset.Value);
        }

        return new QueryDefinition(sql.ToString(), new ReadOnlyDictionary<string, object?>(allParameters));
    }

    /// <summary>
    /// Builds a COUNT query from the SQL view definition.
    /// </summary>
    public QueryDefinition BuildCount(FeatureQuery query)
    {
        Guard.NotNull(query);

        // Process SQL view with parameters
        var (baseSql, sqlViewParams) = SqlViewExecutor.ProcessSqlView(
            _sqlView,
            _requestParameters,
            _layer.Id);

        var sql = new StringBuilder();
        var allParameters = new Dictionary<string, object?>(sqlViewParams, StringComparer.Ordinal);

        sql.Append("SELECT COUNT(*) FROM (");
        sql.AppendLine();
        sql.Append(baseSql);
        sql.AppendLine();
        sql.Append(") AS __sqlview");

        // Apply additional WHERE clause if filter is present
        if (query.Filter?.Expression is not null)
        {
            // TODO: Implement filter translation for SQL views
        }

        return new QueryDefinition(sql.ToString(), new ReadOnlyDictionary<string, object?>(allParameters));
    }

    /// <summary>
    /// Builds a query to fetch a single feature by ID from the SQL view.
    /// </summary>
    public QueryDefinition BuildById(string featureId, FeatureQuery? query = null)
    {
        Guard.NotNullOrWhiteSpace(featureId);

        // Process SQL view with parameters
        var (baseSql, sqlViewParams) = SqlViewExecutor.ProcessSqlView(
            _sqlView,
            _requestParameters,
            _layer.Id);

        var sql = new StringBuilder();
        var allParameters = new Dictionary<string, object?>(sqlViewParams, StringComparer.Ordinal);

        sql.Append("SELECT * FROM (");
        sql.AppendLine();
        sql.Append(baseSql);
        sql.AppendLine();
        sql.Append(") AS __sqlview WHERE ");
        sql.Append(QuoteIdentifier(_layer.IdField));
        sql.Append(" = @feature_id LIMIT 1");

        allParameters["feature_id"] = featureId;

        return new QueryDefinition(sql.ToString(), new ReadOnlyDictionary<string, object?>(allParameters));
    }

    /// <summary>
    /// Checks if a layer is backed by a SQL view.
    /// </summary>
    public static bool IsSqlView(LayerDefinition layer)
    {
        return layer?.SqlView?.Sql.HasValue() == true;
    }

    /// <summary>
    /// Gets the command timeout for the SQL view.
    /// </summary>
    public TimeSpan? GetCommandTimeout()
    {
        if (_sqlView.TimeoutSeconds.HasValue)
        {
            return TimeSpan.FromSeconds(_sqlView.TimeoutSeconds.Value);
        }

        return null;
    }

    /// <summary>
    /// Checks if the SQL view is read-only.
    /// SQL views are read-only by default for security.
    /// </summary>
    public bool IsReadOnly()
    {
        return _sqlView.ReadOnly;
    }

    /// <summary>
    /// Quotes an identifier (table name, column name) to prevent SQL injection.
    /// This is a simple implementation; provider-specific builders should override.
    /// </summary>
    private static string QuoteIdentifier(string identifier)
    {
        // Use double quotes (ANSI SQL standard)
        // Provider-specific implementations can override this
        return $"\"{identifier}\"";
    }
}

