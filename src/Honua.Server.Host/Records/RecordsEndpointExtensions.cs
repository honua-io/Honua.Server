// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Honua.Server.Host.Records;

internal static class RecordsEndpointExtensions
{
    private const string OpenApiMediaType = "application/vnd.oai.openapi+json;version=3.0";

    private static readonly string[] ConformanceUris =
    {
        "http://www.opengis.net/spec/ogcapi-records-1/1.0/conf/core",
        "http://www.opengis.net/spec/ogcapi-records-1/1.0/conf/basic"
    };

    public static IEndpointRouteBuilder MapOgcRecords(this IEndpointRouteBuilder endpoints)
    {
        Guard.NotNull(endpoints);

        var group = endpoints.MapGroup("/records");

        group.MapGet(string.Empty, (HttpRequest request) =>
        {
            var response = new RecordsLandingResponse
            {
                Title = "Honua Records",
                ConformsTo = ConformanceUris,
                Links = new[]
                {
                    CreateLink(request.BuildAbsoluteUrl("/records"), "self", "application/json", "This document"),
                    CreateLink(request.BuildAbsoluteUrl("/records/conformance"), "conformance", "application/json", "Conformance declaration"),
                    CreateLink(request.BuildAbsoluteUrl("/records/collections"), "collections", "application/json", "Record collections"),
                    CreateLink(request.BuildAbsoluteUrl("/records/search"), "search", "application/json", "Search records"),
                    CreateLink(request.BuildAbsoluteUrl("/records/api"), "service-desc", OpenApiMediaType, "API definition"),
                    CreateLink("https://github.com/your-org/HonuaCore/blob/main/docs/user/endpoints.md", "service-doc", "text/markdown", "Records documentation")
                }
            };

            return Results.Json(response);
        });

        group.MapGet("/conformance", (HttpRequest request) =>
        {
            Guard.NotNull(request);

            return Results.Json(new { conformsTo = ConformanceUris });
        });

        group.MapGet("/api", (HttpRequest request) =>
        {
            Guard.NotNull(request);

            var document = new
            {
                openapi = "3.0.3",
                info = new { title = "Honua Records API", version = "1.0.0" },
                paths = new Dictionary<string, object>
                {
                    ["/records"] = new
                    {
                        get = new
                        {
                            summary = "Records landing",
                            responses = new Dictionary<string, object>
                            {
                                ["200"] = new { description = "Landing response" }
                            }
                        }
                    },
                    ["/records/collections"] = new
                    {
                        get = new
                        {
                            summary = "List record collections",
                            responses = new Dictionary<string, object>
                            {
                                ["200"] = new { description = "Collections response" }
                            }
                        }
                    },
                    ["/records/search"] = new
                    {
                        get = new
                        {
                            summary = "Search records",
                            parameters = new[]
                            {
                                new { name = "q", @in = "query", schema = new { type = "string" } },
                                new { name = "bbox", @in = "query", schema = new { type = "string" } },
                                new { name = "datetime", @in = "query", schema = new { type = "string" } },
                                new { name = "ids", @in = "query", schema = new { type = "string" } }
                            },
                            responses = new Dictionary<string, object>
                            {
                                ["200"] = new { description = "Search response" }
                            }
                        }
                    }
                }
            };

            var payload = JsonSerializer.Serialize(document);
            return Results.Content(payload, OpenApiMediaType);
        });

        group.MapGet("/collections", (HttpRequest request, [FromServices] ICatalogProjectionService catalog) =>
        {
            Guard.NotNull(request);
            Guard.NotNull(catalog);

            var snapshot = catalog.GetSnapshot();
            var collections = snapshot.Groups
                .OrderBy(g => g.Order ?? int.MaxValue)
                .ThenBy(g => g.Title, StringComparer.OrdinalIgnoreCase)
                .Select(group => BuildCollectionResponse(request, group, snapshot.RecordIndex.Values.Where(r => string.Equals(r.GroupId, group.Id, StringComparison.OrdinalIgnoreCase))))
                .ToList();

            var response = new RecordsCollectionsResponse
            {
                Collections = collections,
                Links = new[]
                {
                    CreateLink(request.BuildAbsoluteUrl("/records"), "up", "application/json", "Records landing"),
                    CreateLink(request.BuildAbsoluteUrl("/records/collections"), "self", "application/json", "Collections")
                }
            };

            return Results.Json(response);
        });

        group.MapGet("/search", (HttpRequest request, [FromServices] ICatalogProjectionService catalog) =>
        {
            Guard.NotNull(request);
            Guard.NotNull(catalog);

            var (bbox, bboxError) = QueryParsingHelpers.ParseBoundingBox(request.Query);
            if (bboxError is not null)
            {
                return bboxError;
            }

            var (temporal, temporalError) = QueryParsingHelpers.ParseTemporalRange(request.Query["datetime"]);
            if (temporalError is not null)
            {
                return temporalError;
            }

            var ids = QueryParsingHelpers.ParseCsv(request.Query.TryGetValue("ids", out var idValues) ? idValues.ToString() : null);
            var groupId = request.Query.TryGetValue("groupId", out var groupValues) ? groupValues.ToString() : null;
            var queryText = request.Query.TryGetValue("q", out var queryValues) ? queryValues.ToString() : null;

            var baseRecords = catalog.Search(queryText, string.IsNullOrWhiteSpace(groupId) ? null : groupId);
            IEnumerable<CatalogDiscoveryRecord> filtered = baseRecords;

            if (ids.Count > 0)
            {
                filtered = filtered.Where(record => ids.Contains(record.Id, StringComparer.OrdinalIgnoreCase));
            }

            if (bbox is not null)
            {
                filtered = filtered.Where(record => RecordIntersects(record, bbox!));
            }

            if (temporal is not null)
            {
                filtered = filtered.Where(record => RecordIntersects(record, temporal.Value));
            }

            var sortBy = request.Query.TryGetValue("sortby", out var sortValues) ? sortValues.ToString() : null;
            filtered = ApplySort(filtered, sortBy);

            var pagination = QueryParsingHelpers.ParsePagination(request.Query, defaultLimit: 50, defaultOffset: 0, minLimit: 1, maxLimit: 500);
            var limit = pagination.Limit;
            var offset = pagination.Offset;

            // Count without materializing the full result set
            var numberMatched = filtered.Count();

            // Apply pagination directly over the queryable (defer materialization)
            var paged = filtered.Skip(offset).Take(limit).ToList();

            var items = paged.Select(record => MapRecordResponse(request, record.GroupId, record)).ToList();

            var links = BuildSearchLinks(request, limit, offset, numberMatched);

            var response = new RecordSearchResponse
            {
                NumberMatched = numberMatched,
                NumberReturned = paged.Count,
                TimeStamp = DateTimeOffset.UtcNow,
                Items = items,
                Links = links
            };

            return Results.Json(response);
        });

        group.MapGet("/collections/{collectionId}", (HttpRequest request, string collectionId, [FromServices] ICatalogProjectionService catalog) =>
        {
            Guard.NotNull(request);
            Guard.NotNull(catalog);

            if (string.IsNullOrWhiteSpace(collectionId))
            {
                return Results.BadRequest("Collection identifier is required.");
            }

            var snapshot = catalog.GetSnapshot();
            if (!snapshot.GroupIndex.TryGetValue(collectionId, out var group))
            {
                return Results.NotFound();
            }

            var records = snapshot.RecordIndex.Values.Where(r => string.Equals(r.GroupId, group.Id, StringComparison.OrdinalIgnoreCase));
            var collection = BuildCollectionResponse(request, group, records);

            return Results.Json(collection);
        });

        group.MapGet("/collections/{collectionId}/items", (HttpRequest request, string collectionId, [FromServices] ICatalogProjectionService catalog) =>
        {
            Guard.NotNull(request);
            Guard.NotNull(catalog);

            if (string.IsNullOrWhiteSpace(collectionId))
            {
                return Results.BadRequest("Collection identifier is required.");
            }

            var snapshot = catalog.GetSnapshot();
            if (!snapshot.GroupIndex.TryGetValue(collectionId, out var group))
            {
                return Results.NotFound();
            }

            var queryRecords = snapshot.RecordIndex.Values
                .Where(r => string.Equals(r.GroupId, group.Id, StringComparison.OrdinalIgnoreCase))
                .OrderBy(r => r.Ordering ?? int.MaxValue)
                .ThenBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var pagination = QueryParsingHelpers.ParsePagination(request.Query, defaultLimit: 50, defaultOffset: 0, minLimit: 1, maxLimit: 500);
            var limit = pagination.Limit;
            var offset = pagination.Offset;

            var paged = queryRecords.Skip(offset).Take(limit).ToList();
            var items = paged.Select(record => MapRecordResponse(request, collectionId, record)).ToList();

            var links = new List<RecordLink>
            {
                CreateLink(request.GenerateSelfLink(), "self", "application/json")
            };

            var prevOffset = PaginationHelper.CalculatePrevOffset(offset, limit);
            if (prevOffset.HasValue)
            {
                links.Add(CreateLink(request.GeneratePrevLink(prevOffset.Value, limit), "prev", "application/json"));
            }

            var nextOffset = PaginationHelper.CalculateNextOffset(offset, limit, paged.Count);
            if (nextOffset.HasValue)
            {
                links.Add(CreateLink(request.GenerateNextLink(nextOffset.Value, limit), "next", "application/json"));
            }

            links.Add(CreateLink(request.BuildAbsoluteUrl($"/records/collections/{Uri.EscapeDataString(collectionId)}"), "collection", "application/json"));

            var response = new RecordItemsResponse
            {
                CollectionId = collectionId,
                NumberMatched = queryRecords.Count,
                NumberReturned = paged.Count,
                TimeStamp = DateTimeOffset.UtcNow,
                Items = items,
                Links = links
            };

            return Results.Json(response);
        });

        group.MapGet("/collections/{collectionId}/items/{recordId}", (HttpRequest request, string collectionId, string recordId, [FromServices] ICatalogProjectionService catalog) =>
        {
            Guard.NotNull(request);
            Guard.NotNull(catalog);

            if (string.IsNullOrWhiteSpace(collectionId) || string.IsNullOrWhiteSpace(recordId))
            {
                return Results.BadRequest("Collection and record identifiers are required.");
            }

            var decodedRecordId = Uri.UnescapeDataString(recordId);
            var record = catalog.GetRecord(decodedRecordId);
            if (record is null || !string.Equals(record.GroupId, collectionId, StringComparison.OrdinalIgnoreCase))
            {
                return Results.NotFound();
            }

            var response = MapRecordResponse(request, collectionId, record);
            return Results.Json(response);
        });

        return endpoints;
    }

    private static RecordCollection BuildCollectionResponse(HttpRequest request, CatalogGroupView group, IEnumerable<CatalogDiscoveryRecord> records)
    {
        var recordList = records.ToList();
        var extent = AggregateExtent(recordList);
        var keywords = recordList.SelectMany(r => r.Keywords).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        var collectionLinks = new List<RecordLink>
        {
            CreateLink(request.BuildAbsoluteUrl($"/records/collections/{Uri.EscapeDataString(group.Id)}"), "self", "application/json", group.Title),
            CreateLink(request.BuildAbsoluteUrl($"/records/collections/{Uri.EscapeDataString(group.Id)}/items"), "items", "application/json", "Records")
        };

        return new RecordCollection
        {
            Id = group.Id,
            Title = group.Title,
            ItemType = "record",
            Keywords = keywords,
            Extent = extent,
            Links = collectionLinks
        };
    }

    private static RecordResponse MapRecordResponse(HttpRequest request, string collectionId, CatalogDiscoveryRecord record)
    {
        var extent = BuildExtent(record);
        var contacts = record.Contacts.Select(MapContact).ToList();
        var links = new List<RecordLink>();

        foreach (var link in record.Links)
        {
            if (string.IsNullOrWhiteSpace(link.Href))
            {
                continue;
            }

            links.Add(new RecordLink
            {
                Rel = link.Rel ?? "alternate",
                Href = link.Href,
                Type = link.Type,
                Title = link.Title
            });
        }

        links.Add(CreateLink(
            request.BuildAbsoluteUrl($"/records/collections/{Uri.EscapeDataString(collectionId)}/items/{Uri.EscapeDataString(record.Id)}"),
            "self",
            "application/json",
            record.Title));

        return new RecordResponse
        {
            Id = record.Id,
            Title = record.Title,
            Description = record.Summary,
            Keywords = record.Keywords,
            Themes = record.Themes,
            Extent = extent,
            Contacts = contacts,
            Links = links,
            Thumbnail = record.Thumbnail,
            ServiceId = record.ServiceId,
            LayerId = record.LayerId,
            GroupId = record.GroupId
        };
    }

    private static List<RecordLink> BuildSearchLinks(HttpRequest request, int limit, int offset, int numberMatched)
    {
        var links = new List<RecordLink>
        {
            CreateLink(request.GenerateSelfLink(), "self", "application/json")
        };

        // Use PaginationHelper to calculate next/prev offsets
        var nextOffset = PaginationHelper.HasNextPage(numberMatched - offset, limit)
            ? PaginationHelper.CalculateNextOffset(offset, limit, numberMatched - offset)
            : null;

        if (nextOffset.HasValue)
        {
            links.Add(CreateLink(request.GenerateNextLink(nextOffset.Value, limit), "next", "application/json"));
        }

        var prevOffset = PaginationHelper.CalculatePrevOffset(offset, limit);
        if (prevOffset.HasValue)
        {
            links.Add(CreateLink(request.GeneratePrevLink(prevOffset.Value, limit), "prev", "application/json"));
        }

        return links;
    }

    private static bool RecordIntersects(CatalogDiscoveryRecord record, double[] bbox)
    {
        if (record.SpatialExtent?.Bbox is not { Count: > 0 } extents)
        {
            return false;
        }

        foreach (var candidate in extents)
        {
            if (candidate.Length < 4)
            {
                continue;
            }

            if (BoundingBoxesIntersect(candidate, bbox))
            {
                return true;
            }
        }

        return false;
    }

    private static bool RecordIntersects(CatalogDiscoveryRecord record, QueryParsingHelpers.QueryTemporalRange temporal)
    {
        var extent = record.TemporalExtent;
        if (extent is null)
        {
            return false;
        }

        var recordStart = extent.Start ?? DateTimeOffset.MinValue;
        var recordEnd = extent.End ?? DateTimeOffset.MaxValue;
        var queryStart = temporal.Start ?? DateTimeOffset.MinValue;
        var queryEnd = temporal.End ?? DateTimeOffset.MaxValue;

        return recordStart <= queryEnd && recordEnd >= queryStart;
    }

    private static bool BoundingBoxesIntersect(double[] a, double[] b)
    {
        var aMinX = a[0];
        var aMinY = a[1];
        var aMaxX = a.Length > 2 ? a[2] : a[0];
        var aMaxY = a.Length > 3 ? a[3] : a[1];

        var bMinX = b[0];
        var bMinY = b[1];
        var bMaxX = b[2];
        var bMaxY = b[3];

        return aMinX <= bMaxX && aMaxX >= bMinX && aMinY <= bMaxY && aMaxY >= bMinY;
    }

    private static IEnumerable<CatalogDiscoveryRecord> ApplySort(IEnumerable<CatalogDiscoveryRecord> records, string? sortBy)
    {
        if (string.IsNullOrWhiteSpace(sortBy))
        {
            return records;
        }

        var normalized = sortBy.Trim();
        var descending = normalized.StartsWith('-');
        var field = normalized.TrimStart('+', '-');

        return field.Equals("title", StringComparison.OrdinalIgnoreCase)
            ? descending
                ? records.OrderByDescending(r => r.Title, StringComparer.OrdinalIgnoreCase)
                : records.OrderBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
            : records;
    }

    private static RecordExtent? AggregateExtent(IReadOnlyCollection<CatalogDiscoveryRecord> records)
    {
        if (records.Count == 0)
        {
            return null;
        }

        var spatialBoxes = new List<double[]>();
        string? crs = null;
        DateTimeOffset? start = null;
        DateTimeOffset? end = null;

        foreach (var record in records)
        {
            if (record.SpatialExtent is { Bbox: { Count: > 0 } bbox })
            {
                foreach (var box in bbox)
                {
                    if (box.Length >= 4)
                    {
                        spatialBoxes.Add(box);
                    }
                }

                crs ??= record.SpatialExtent.Crs;
            }

            if (record.TemporalExtent is { } temporal)
            {
                if (temporal.Start is { } temporalStart)
                {
                    start = start is null || temporalStart < start ? temporalStart : start;
                }

                if (temporal.End is { } temporalEnd)
                {
                    end = end is null || temporalEnd > end ? temporalEnd : end;
                }
            }
        }

        RecordSpatialExtent? spatial = null;
        if (spatialBoxes.Count > 0)
        {
            var merged = MergeBoundingBoxes(spatialBoxes);
            spatial = new RecordSpatialExtent
            {
                Bbox = new[] { merged },
                Crs = crs
            };
        }

        RecordTemporalExtent? temporalExtent = null;
        if (start is not null || end is not null)
        {
            temporalExtent = new RecordTemporalExtent
            {
                Start = start?.ToString("o"),
                End = end?.ToString("o")
            };
        }

        if (spatial is null && temporalExtent is null)
        {
            return null;
        }

        return new RecordExtent
        {
            Spatial = spatial,
            Temporal = temporalExtent
        };
    }

    private static double[] MergeBoundingBoxes(IReadOnlyCollection<double[]> boxes)
    {
        var minX = double.PositiveInfinity;
        var minY = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxY = double.NegativeInfinity;

        foreach (var box in boxes)
        {
            minX = Math.Min(minX, box[0]);
            minY = Math.Min(minY, box[1]);
            maxX = Math.Max(maxX, box[2]);
            maxY = Math.Max(maxY, box[3]);
        }

        return new[] { minX, minY, maxX, maxY };
    }

    private static RecordExtent? BuildExtent(CatalogDiscoveryRecord record)
    {
        RecordSpatialExtent? spatial = null;
        if (record.SpatialExtent is { Bbox: { Count: > 0 } bbox })
        {
            var validBoxes = bbox.Where(b => b.Length >= 4).ToArray();
            if (validBoxes.Length > 0)
            {
                spatial = new RecordSpatialExtent
                {
                    Bbox = validBoxes,
                    Crs = record.SpatialExtent.Crs
                };
            }
        }

        RecordTemporalExtent? temporal = null;
        if (record.TemporalExtent is { } temporalExtent)
        {
            temporal = new RecordTemporalExtent
            {
                Start = temporalExtent.Start?.ToString("o"),
                End = temporalExtent.End?.ToString("o")
            };
        }

        if (spatial is null && temporal is null)
        {
            return null;
        }

        return new RecordExtent
        {
            Spatial = spatial,
            Temporal = temporal
        };
    }

    private static RecordContact MapContact(CatalogContactDefinition contact)
    {
        return new RecordContact
        {
            Name = contact.Name,
            Email = contact.Email,
            Organization = contact.Organization,
            Phone = contact.Phone,
            Url = contact.Url,
            Role = contact.Role
        };
    }

    private static RecordLink CreateLink(string href, string rel, string? type = null, string? title = null)
    {
        return new RecordLink
        {
            Rel = rel,
            Href = href,
            Type = type,
            Title = title
        };
    }
}
