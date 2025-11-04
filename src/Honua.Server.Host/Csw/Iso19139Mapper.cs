// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Host.Csw;

/// <summary>
/// Maps Honua metadata to ISO 19139 XML format for CSW GetRecords responses.
/// </summary>
internal static class Iso19139Mapper
{
    private static readonly XNamespace Gmd = "http://www.isotc211.org/2005/gmd";
    private static readonly XNamespace Gco = "http://www.isotc211.org/2005/gco";
    private static readonly XNamespace Gml = "http://www.opengis.net/gml/3.2";
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";
    private static readonly XNamespace XLink = "http://www.w3.org/1999/xlink";

    /// <summary>
    /// Creates an ISO 19139 MD_Metadata element from a catalog record and layer metadata.
    /// </summary>
    public static XElement CreateIso19139Metadata(
        CatalogDiscoveryRecord record,
        LayerDefinition? layer,
        HttpRequest request)
    {
        Guard.NotNull(record);
        Guard.NotNull(request);

        var baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";
        var iso = layer?.Iso19115;

        return new XElement(Gmd + "MD_Metadata",
            // File identifier
            new XElement(Gmd + "fileIdentifier",
                new XElement(Gco + "CharacterString", iso?.MetadataIdentifier ?? record.Id)
            ),

            // Language
            new XElement(Gmd + "language",
                new XElement(Gco + "CharacterString", iso?.Language ?? "eng")
            ),

            // Character set
            new XElement(Gmd + "characterSet",
                CreateCodeListElement("MD_CharacterSetCode", iso?.CharacterSet ?? "utf8")
            ),

            // Metadata contact
            CreateMetadataContact(iso?.MetadataContact),

            // Date stamp (revision date or current)
            new XElement(Gmd + "dateStamp",
                new XElement(Gco + "DateTime",
                    (iso?.DateInfo?.Revision ?? DateTimeOffset.UtcNow).ToString("o"))
            ),

            // Metadata standard
            CreateMetadataStandard(iso?.MetadataStandard),

            // Reference system info
            CreateReferenceSystemInfo(iso?.ReferenceSystemInfo, layer),

            // Identification info
            CreateIdentificationInfo(record, layer, iso, baseUrl),

            // Data quality info
            CreateDataQualityInfo(iso?.DataQualityInfo, iso?.Lineage)
        );
    }

    private static XElement CreateMetadataContact(Iso19115Contact? contact)
    {
        if (contact == null)
        {
            // Default contact
            return new XElement(Gmd + "contact",
                CreateResponsibleParty("Honua Administrator", "admin@honua.io", "pointOfContact")
            );
        }

        return new XElement(Gmd + "contact",
            CreateResponsibleParty(
                contact.OrganisationName,
                contact.IndividualName,
                contact.ContactInfo,
                contact.Role ?? "pointOfContact"
            )
        );
    }

    private static XElement CreateMetadataStandard(Iso19115MetadataStandard? standard)
    {
        return new XElement(Gmd + "metadataStandardName",
            new XElement(Gco + "CharacterString", standard?.Name ?? "ISO 19115:2014")
        );
    }

    private static XElement? CreateReferenceSystemInfo(
        Iso19115ReferenceSystemInfo? refSys,
        LayerDefinition? layer)
    {
        if (refSys != null)
        {
            return new XElement(Gmd + "referenceSystemInfo",
                new XElement(Gmd + "MD_ReferenceSystem",
                    new XElement(Gmd + "referenceSystemIdentifier",
                        new XElement(Gmd + "RS_Identifier",
                            new XElement(Gmd + "code",
                                new XElement(Gco + "CharacterString", refSys.Code)
                            ),
                            refSys.CodeSpace != null
                                ? new XElement(Gmd + "codeSpace",
                                    new XElement(Gco + "CharacterString", refSys.CodeSpace))
                                : null
                        )
                    )
                )
            );
        }

        // Fallback to layer CRS if available
        if (layer?.Crs?.Count > 0)
        {
            var firstCrs = layer.Crs[0];
            var epsgCode = ExtractEpsgCode(firstCrs);
            if (epsgCode != null)
            {
                return new XElement(Gmd + "referenceSystemInfo",
                    new XElement(Gmd + "MD_ReferenceSystem",
                        new XElement(Gmd + "referenceSystemIdentifier",
                            new XElement(Gmd + "RS_Identifier",
                                new XElement(Gmd + "code",
                                    new XElement(Gco + "CharacterString", epsgCode)
                                ),
                                new XElement(Gmd + "codeSpace",
                                    new XElement(Gco + "CharacterString", "EPSG"))
                            )
                        )
                    )
                );
            }
        }

        return null;
    }

    private static XElement CreateIdentificationInfo(
        CatalogDiscoveryRecord record,
        LayerDefinition? layer,
        Iso19115Metadata? iso,
        string baseUrl)
    {
        return new XElement(Gmd + "identificationInfo",
            new XElement(Gmd + "MD_DataIdentification",
                // Citation
                new XElement(Gmd + "citation",
                    CreateCitation(record, iso?.DateInfo)
                ),

                // Abstract
                new XElement(Gmd + "abstract",
                    new XElement(Gco + "CharacterString", record.Summary ?? record.Title)
                ),

                // Point of contact
                iso?.MetadataContact != null
                    ? new XElement(Gmd + "pointOfContact",
                        CreateResponsibleParty(
                            iso.MetadataContact.OrganisationName,
                            iso.MetadataContact.IndividualName,
                            iso.MetadataContact.ContactInfo,
                            iso.MetadataContact.Role ?? "pointOfContact"
                        ))
                    : null,

                // Keywords
                record.Keywords?.Count > 0
                    ? new XElement(Gmd + "descriptiveKeywords",
                        CreateKeywords(record.Keywords))
                    : null,

                // Resource constraints
                CreateResourceConstraints(iso?.ResourceConstraints),

                // Spatial representation type
                iso?.SpatialRepresentationType != null
                    ? new XElement(Gmd + "spatialRepresentationType",
                        CreateCodeListElement("MD_SpatialRepresentationTypeCode",
                            iso.SpatialRepresentationType))
                    : null,

                // Spatial resolution
                iso?.SpatialResolution != null
                    ? CreateSpatialResolution(iso.SpatialResolution)
                    : null,

                // Language
                new XElement(Gmd + "language",
                    new XElement(Gco + "CharacterString", iso?.Language ?? "eng")
                ),

                // Topic category
                iso?.TopicCategory?.Count > 0
                    ? iso.TopicCategory.Select(tc =>
                        new XElement(Gmd + "topicCategory",
                            new XElement(Gmd + "MD_TopicCategoryCode", tc)))
                    : null,

                // Extent
                CreateExtent(record, layer)
            )
        );
    }

    private static XElement CreateCitation(CatalogDiscoveryRecord record, Iso19115DateInfo? dateInfo)
    {
        return new XElement(Gmd + "CI_Citation",
            new XElement(Gmd + "title",
                new XElement(Gco + "CharacterString", record.Title)
            ),
            dateInfo?.Creation != null
                ? CreateCitationDate(dateInfo.Creation.Value, "creation")
                : null,
            dateInfo?.Publication != null
                ? CreateCitationDate(dateInfo.Publication.Value, "publication")
                : null,
            dateInfo?.Revision != null
                ? CreateCitationDate(dateInfo.Revision.Value, "revision")
                : null
        );
    }

    private static XElement CreateCitationDate(DateTimeOffset date, string dateType)
    {
        return new XElement(Gmd + "date",
            new XElement(Gmd + "CI_Date",
                new XElement(Gmd + "date",
                    new XElement(Gco + "DateTime", date.ToString("o"))
                ),
                new XElement(Gmd + "dateType",
                    CreateCodeListElement("CI_DateTypeCode", dateType)
                )
            )
        );
    }

    private static XElement CreateKeywords(IReadOnlyList<string> keywords)
    {
        return new XElement(Gmd + "MD_Keywords",
            keywords.Select(kw =>
                new XElement(Gmd + "keyword",
                    new XElement(Gco + "CharacterString", kw)))
        );
    }

    private static XElement? CreateResourceConstraints(Iso19115ResourceConstraints? constraints)
    {
        if (constraints == null) return null;

        return new XElement(Gmd + "resourceConstraints",
            new XElement(Gmd + "MD_LegalConstraints",
                constraints.UseLimitation?.Select(ul =>
                    new XElement(Gmd + "useLimitation",
                        new XElement(Gco + "CharacterString", ul))),
                constraints.AccessConstraints?.Select(ac =>
                    new XElement(Gmd + "accessConstraints",
                        CreateCodeListElement("MD_RestrictionCode", ac))),
                constraints.UseConstraints?.Select(uc =>
                    new XElement(Gmd + "useConstraints",
                        CreateCodeListElement("MD_RestrictionCode", uc))),
                constraints.OtherConstraints?.Select(oc =>
                    new XElement(Gmd + "otherConstraints",
                        new XElement(Gco + "CharacterString", oc)))
            )
        );
    }

    private static XElement? CreateSpatialResolution(Iso19115SpatialResolution resolution)
    {
        return new XElement(Gmd + "spatialResolution",
            new XElement(Gmd + "MD_Resolution",
                resolution.EquivalentScale != null
                    ? new XElement(Gmd + "equivalentScale",
                        new XElement(Gmd + "MD_RepresentativeFraction",
                            new XElement(Gmd + "denominator",
                                new XElement(Gco + "Integer", resolution.EquivalentScale))))
                    : resolution.Distance != null
                        ? new XElement(Gmd + "distance",
                            new XElement(Gco + "Distance",
                                new XAttribute("uom", "m"),
                                resolution.Distance))
                        : null
            )
        );
    }

    private static XElement? CreateExtent(CatalogDiscoveryRecord record, LayerDefinition? layer)
    {
        if (record.SpatialExtent?.Bbox.IsNullOrEmpty() == true)
            return null;

        var bbox = record.SpatialExtent.Bbox[0];
        if (bbox.Length < 4) return null;

        return new XElement(Gmd + "extent",
            new XElement(Gmd + "EX_Extent",
                new XElement(Gmd + "geographicElement",
                    new XElement(Gmd + "EX_GeographicBoundingBox",
                        new XElement(Gmd + "westBoundLongitude",
                            new XElement(Gco + "Decimal", bbox[0].ToString(CultureInfo.InvariantCulture))),
                        new XElement(Gmd + "eastBoundLongitude",
                            new XElement(Gco + "Decimal", bbox[2].ToString(CultureInfo.InvariantCulture))),
                        new XElement(Gmd + "southBoundLatitude",
                            new XElement(Gco + "Decimal", bbox[1].ToString(CultureInfo.InvariantCulture))),
                        new XElement(Gmd + "northBoundLatitude",
                            new XElement(Gco + "Decimal", bbox[3].ToString(CultureInfo.InvariantCulture)))
                    )
                ),
                layer?.Extent?.Temporal?.Count > 0
                    ? CreateTemporalExtent(layer.Extent.Temporal)
                    : null
            )
        );
    }

    private static XElement CreateTemporalExtent(System.Collections.Generic.IReadOnlyList<TemporalIntervalDefinition> temporal)
    {
        var interval = temporal[0];
        return new XElement(Gmd + "temporalElement",
            new XElement(Gmd + "EX_TemporalExtent",
                new XElement(Gmd + "extent",
                    new XElement(Gml + "TimePeriod",
                        new XAttribute(Gml + "id", "temporal-extent-1"),
                        interval.Start != null
                            ? new XElement(Gml + "beginPosition", interval.Start.Value.ToString("o"))
                            : new XElement(Gml + "beginPosition", new XAttribute("indeterminatePosition", "unknown")),
                        interval.End != null
                            ? new XElement(Gml + "endPosition", interval.End.Value.ToString("o"))
                            : new XElement(Gml + "endPosition", new XAttribute("indeterminatePosition", "now"))
                    )
                )
            )
        );
    }

    private static XElement? CreateDataQualityInfo(Iso19115DataQualityInfo? quality, Iso19115Lineage? lineage)
    {
        if (quality == null && lineage == null) return null;

        return new XElement(Gmd + "dataQualityInfo",
            new XElement(Gmd + "DQ_DataQuality",
                new XElement(Gmd + "scope",
                    new XElement(Gmd + "DQ_Scope",
                        new XElement(Gmd + "level",
                            CreateCodeListElement("MD_ScopeCode", quality?.Scope ?? "dataset")
                        )
                    )
                ),
                quality?.PositionalAccuracy != null
                    ? CreatePositionalAccuracy(quality.PositionalAccuracy)
                    : null,
                lineage != null
                    ? CreateLineage(lineage)
                    : null
            )
        );
    }

    private static XElement CreatePositionalAccuracy(Iso19115PositionalAccuracy accuracy)
    {
        return new XElement(Gmd + "report",
            new XElement(Gmd + "DQ_AbsoluteExternalPositionalAccuracy",
                new XElement(Gmd + "result",
                    new XElement(Gmd + "DQ_QuantitativeResult",
                        new XElement(Gmd + "valueUnit",
                            new XElement(Gml + "UnitDefinition",
                                new XAttribute(Gml + "id", "unit-1"),
                                new XElement(Gml + "identifier",
                                    new XAttribute("codeSpace", "urn:ogc:def:uom:"),
                                    accuracy.Unit ?? "meter")
                            )
                        ),
                        accuracy.Value != null
                            ? new XElement(Gmd + "value",
                                new XElement(Gco + "Record", accuracy.Value.Value.ToString(CultureInfo.InvariantCulture)))
                            : null
                    )
                )
            )
        );
    }

    private static XElement CreateLineage(Iso19115Lineage lineage)
    {
        return new XElement(Gmd + "lineage",
            new XElement(Gmd + "LI_Lineage",
                lineage.Statement.HasValue()
                    ? new XElement(Gmd + "statement",
                        new XElement(Gco + "CharacterString", lineage.Statement))
                    : null,
                lineage.ProcessSteps?.Select(ps =>
                    new XElement(Gmd + "processStep",
                        new XElement(Gmd + "LI_ProcessStep",
                            new XElement(Gmd + "description",
                                new XElement(Gco + "CharacterString", ps.Description)),
                            ps.DateTime != null
                                ? new XElement(Gmd + "dateTime",
                                    new XElement(Gco + "DateTime", ps.DateTime.Value.ToString("o")))
                                : null
                        )
                    ))
            )
        );
    }

    private static XElement CreateResponsibleParty(
        string? organisationName,
        string? email,
        string role)
    {
        return new XElement(Gmd + "CI_ResponsibleParty",
            organisationName.HasValue()
                ? new XElement(Gmd + "organisationName",
                    new XElement(Gco + "CharacterString", organisationName))
                : null,
            email.HasValue()
                ? new XElement(Gmd + "contactInfo",
                    new XElement(Gmd + "CI_Contact",
                        new XElement(Gmd + "address",
                            new XElement(Gmd + "CI_Address",
                                new XElement(Gmd + "electronicMailAddress",
                                    new XElement(Gco + "CharacterString", email))
                            )
                        )
                    ))
                : null,
            new XElement(Gmd + "role",
                CreateCodeListElement("CI_RoleCode", role)
            )
        );
    }

    private static XElement CreateResponsibleParty(
        string? organisationName,
        string? individualName,
        Iso19115ContactInfo? contactInfo,
        string role)
    {
        return new XElement(Gmd + "CI_ResponsibleParty",
            organisationName.HasValue()
                ? new XElement(Gmd + "organisationName",
                    new XElement(Gco + "CharacterString", organisationName))
                : null,
            individualName.HasValue()
                ? new XElement(Gmd + "individualName",
                    new XElement(Gco + "CharacterString", individualName))
                : null,
            contactInfo != null
                ? CreateContactInfo(contactInfo)
                : null,
            new XElement(Gmd + "role",
                CreateCodeListElement("CI_RoleCode", role)
            )
        );
    }

    private static XElement CreateContactInfo(Iso19115ContactInfo contactInfo)
    {
        return new XElement(Gmd + "contactInfo",
            new XElement(Gmd + "CI_Contact",
                contactInfo.Phone.HasValue()
                    ? new XElement(Gmd + "phone",
                        new XElement(Gmd + "CI_Telephone",
                            new XElement(Gmd + "voice",
                                new XElement(Gco + "CharacterString", contactInfo.Phone))))
                    : null,
                new XElement(Gmd + "address",
                    new XElement(Gmd + "CI_Address",
                        contactInfo.Address != null
                            ? CreateAddress(contactInfo.Address)
                            : null,
                        contactInfo.Email.HasValue()
                            ? new XElement(Gmd + "electronicMailAddress",
                                new XElement(Gco + "CharacterString", contactInfo.Email))
                            : null
                    )
                ),
                contactInfo.OnlineResource.HasValue()
                    ? new XElement(Gmd + "onlineResource",
                        new XElement(Gmd + "CI_OnlineResource",
                            new XElement(Gmd + "linkage",
                                new XElement(Gmd + "URL", contactInfo.OnlineResource))))
                    : null
            )
        );
    }

    private static object?[] CreateAddress(Iso19115Address address)
    {
        return new object?[]
        {
            address.DeliveryPoint.HasValue()
                ? new XElement(Gmd + "deliveryPoint",
                    new XElement(Gco + "CharacterString", address.DeliveryPoint))
                : null,
            address.City.HasValue()
                ? new XElement(Gmd + "city",
                    new XElement(Gco + "CharacterString", address.City))
                : null,
            address.AdministrativeArea.HasValue()
                ? new XElement(Gmd + "administrativeArea",
                    new XElement(Gco + "CharacterString", address.AdministrativeArea))
                : null,
            address.PostalCode.HasValue()
                ? new XElement(Gmd + "postalCode",
                    new XElement(Gco + "CharacterString", address.PostalCode))
                : null,
            address.Country.HasValue()
                ? new XElement(Gmd + "country",
                    new XElement(Gco + "CharacterString", address.Country))
                : null
        };
    }

    private static XElement CreateCodeListElement(string codeListName, string value)
    {
        var codeListUrl = $"http://www.isotc211.org/2005/resources/Codelist/gmxCodelists.xml#{codeListName}";
        return new XElement(Gmd + codeListName,
            new XAttribute("codeList", codeListUrl),
            new XAttribute("codeListValue", value),
            value
        );
    }

    private static string? ExtractEpsgCode(string crsUri)
    {
        // Try to extract EPSG code from CRS URI
        // Examples: "http://www.opengis.net/def/crs/EPSG/0/4326" -> "4326"
        //           "EPSG:4326" -> "4326"
        if (crsUri.Contains("EPSG", StringComparison.OrdinalIgnoreCase))
        {
            var parts = crsUri.Split(new[] { '/', ':' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Equals("EPSG", StringComparison.OrdinalIgnoreCase) && i + 1 < parts.Length)
                {
                    return parts[i + 1] == "0" && i + 2 < parts.Length ? parts[i + 2] : parts[i + 1];
                }
            }
        }
        return null;
    }
}
