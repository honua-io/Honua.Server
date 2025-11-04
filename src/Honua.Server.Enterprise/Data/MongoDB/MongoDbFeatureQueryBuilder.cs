// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using MongoDB.Bson;
using MongoDB.Driver;
using Honua.Server.Core.Data;
using Honua.Server.Core.Data.Query;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Extensions;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Enterprise.Data.MongoDB;

internal sealed class MongoDbFeatureQueryBuilder
{
    private readonly LayerDefinition _layer;

    public MongoDbFeatureQueryBuilder(LayerDefinition layer)
    {
        _layer = layer ?? throw new ArgumentNullException(nameof(layer));
    }

    public FilterDefinition<BsonDocument> BuildFilter(FeatureQuery query)
    {
        Guard.NotNull(query);

        var builder = Builders<BsonDocument>.Filter;
        var filters = new List<FilterDefinition<BsonDocument>>();

        // Spatial filter (bbox)
        if (query.Bbox is not null)
        {
            var bbox = query.Bbox;
            // Use LayerMetadataHelper to get geometry column
            var geometryField = LayerMetadataHelper.GetGeometryColumn(_layer);

            // MongoDB $geoWithin with $box (simplified bounding box check)
            var geoQuery = new BsonDocument
            {
                {
                    geometryField, new BsonDocument
                    {
                        {
                            "$geoWithin", new BsonDocument
                            {
                                {
                                    "$box", new BsonArray
                                    {
                                        new BsonArray { bbox.MinX, bbox.MinY },
                                        new BsonArray { bbox.MaxX, bbox.MaxY }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            filters.Add(new BsonDocumentFilterDefinition<BsonDocument>(geoQuery));
        }

        // Temporal filter
        var temporalColumn = _layer.Storage?.TemporalColumn;
        if (query.Temporal is not null && temporalColumn.HasValue())
        {
            var temporal = query.Temporal;

            if (temporal.Start.HasValue)
            {
                filters.Add(builder.Gte(temporalColumn, temporal.Start.Value));
            }

            if (temporal.End.HasValue)
            {
                filters.Add(builder.Lte(temporalColumn, temporal.End.Value));
            }
        }

        return filters.Count > 0 ? builder.And(filters) : builder.Empty;
    }

    public FilterDefinition<BsonDocument> BuildByIdFilter(string featureId)
    {
        Guard.NotNullOrWhiteSpace(featureId);

        // Use LayerMetadataHelper to get primary key column
        var keyField = LayerMetadataHelper.GetPrimaryKeyColumn(_layer);
        var builder = Builders<BsonDocument>.Filter;

        return builder.Eq(keyField, featureId);
    }

    public FindOptions<BsonDocument> BuildFindOptions(FeatureQuery query)
    {
        Guard.NotNull(query);

        var options = new FindOptions<BsonDocument>();

        if (query.Limit.HasValue)
        {
            options.Limit = query.Limit.Value;
        }

        if (query.Offset.HasValue && query.Offset.Value > 0)
        {
            options.Skip = query.Offset.Value;
        }

        if (query.SortOrders is { Count: > 0 })
        {
            var sortBuilder = Builders<BsonDocument>.Sort;
            var sortDefinitions = new List<SortDefinition<BsonDocument>>();

            foreach (var sortOrder in query.SortOrders)
            {
                if (sortOrder.Direction == FeatureSortDirection.Descending)
                {
                    sortDefinitions.Add(sortBuilder.Descending(sortOrder.Field));
                }
                else
                {
                    sortDefinitions.Add(sortBuilder.Ascending(sortOrder.Field));
                }
            }

            options.Sort = sortBuilder.Combine(sortDefinitions);
        }

        return options;
    }
}
