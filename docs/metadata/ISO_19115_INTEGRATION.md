# ISO 19115 Metadata Integration

**Status:** Design Proposal
**Target:** Phase 2 Enhancement
**Last Updated:** 2025-01-07

---

## Overview

This document proposes adding ISO 19115/19139 geographic metadata support to Honua's semantic metadata model to enhance CSW (Catalog Service for the Web) capabilities beyond basic Dublin Core.

### Current State

**CSW 2.0.2 Implementation:**
- Currently returns Dublin Core (`csw:Record`) format
- Basic metadata: identifier, title, abstract, subjects, bounding box, references
- Adequate for simple discovery but lacks richness for enterprise catalogs

**Gap:**
- No ISO 19115 metadata profile support
- Limited metadata about data quality, lineage, usage constraints
- Missing detailed contact information, distribution formats, update frequency

### Proposed Enhancement

Add optional `layer.iso19115` metadata block that:
1. Enriches CSW `GetRecords` responses with ISO 19139 XML output
2. Maintains backward compatibility with Dublin Core
3. Follows semantic mapping principles (define once, project to multiple formats)

---

## ISO 19115 Core Elements

### Proposed Metadata Structure

```json
{
  "layer": {
    "id": "parcels",
    "title": "Property Parcels",
    "iso19115": {
      "metadataIdentifier": "urn:uuid:550e8400-e29b-41d4-a716-446655440000",
      "metadataStandard": {
        "name": "ISO 19115:2014",
        "version": "1.0"
      },
      "metadataContact": {
        "organisationName": "City Planning Department",
        "contactInfo": {
          "email": "gis@city.gov",
          "phone": "+1-555-0100",
          "address": {
            "deliveryPoint": "123 Main Street",
            "city": "Springfield",
            "administrativeArea": "State",
            "postalCode": "12345",
            "country": "USA"
          }
        },
        "role": "pointOfContact"
      },
      "dateInfo": {
        "creation": "2020-01-15T00:00:00Z",
        "publication": "2020-02-01T00:00:00Z",
        "revision": "2024-12-01T00:00:00Z"
      },
      "spatialRepresentationType": "vector",
      "spatialResolution": {
        "equivalentScale": 2400
      },
      "language": "eng",
      "characterSet": "utf8",
      "topicCategory": ["planningCadastre"],
      "resourceConstraints": {
        "useLimitation": ["For internal use only", "Not for legal boundary determination"],
        "accessConstraints": ["license"],
        "useConstraints": ["otherRestrictions"],
        "otherConstraints": ["Must attribute City Planning Department"]
      },
      "lineage": {
        "statement": "Digitized from survey plats and deed descriptions. Updated quarterly from assessor records.",
        "sources": [
          {
            "description": "County Assessor Parcel Database",
            "scaleDenominator": 2400
          }
        ],
        "processSteps": [
          {
            "description": "Initial digitization from survey plats",
            "dateTime": "2020-01-15T00:00:00Z"
          },
          {
            "description": "Quality control and topology validation",
            "dateTime": "2020-01-20T00:00:00Z"
          }
        ]
      },
      "dataQualityInfo": {
        "scope": "dataset",
        "positionalAccuracy": {
          "value": 0.5,
          "unit": "meter",
          "evaluationMethod": "Survey-grade GPS"
        },
        "completeness": "100% coverage of city limits",
        "logicalConsistency": "Topology validated; no gaps or overlaps"
      },
      "maintenanceInfo": {
        "maintenanceFrequency": "quarterly",
        "nextUpdate": "2025-04-01T00:00:00Z",
        "updateScope": "dataset"
      },
      "distributionInfo": {
        "distributor": {
          "organisationName": "City GIS Portal",
          "contactInfo": {
            "email": "data@city.gov",
            "onlineResource": "https://gis.city.gov"
          }
        },
        "distributionFormats": [
          {
            "name": "GeoPackage",
            "version": "1.3"
          },
          {
            "name": "Shapefile",
            "version": "ESRI Shapefile"
          }
        ],
        "transferOptions": {
          "onlineResource": "https://gis.city.gov/download/parcels"
        }
      },
      "referenceSystemInfo": {
        "code": "EPSG:2227",
        "codeSpace": "EPSG",
        "version": "10.1"
      }
    }
  }
}
```

---

## Crosswalk: Core Metadata → ISO 19115

### Identification Information

| Core Metadata | ISO 19115 Element | XPath (ISO 19139 XML) | Notes |
|---------------|-------------------|----------------------|-------|
| `layer.id` | `MD_Metadata.fileIdentifier` | `/gmd:MD_Metadata/gmd:fileIdentifier` | Unique metadata identifier |
| `layer.title` | `MD_DataIdentification.citation.title` | `.../gmd:identificationInfo/gmd:MD_DataIdentification/gmd:citation/gmd:CI_Citation/gmd:title` | Resource title |
| `layer.description` | `MD_DataIdentification.abstract` | `.../gmd:identificationInfo/.../gmd:abstract` | Abstract/description |
| `layer.keywords` | `MD_DataIdentification.descriptiveKeywords` | `.../gmd:descriptiveKeywords/gmd:MD_Keywords/gmd:keyword` | Keywords |
| `layer.extent.bbox` | `EX_Extent.geographicElement.EX_GeographicBoundingBox` | `.../gmd:extent/gmd:EX_Extent/gmd:geographicElement/gmd:EX_GeographicBoundingBox` | Spatial extent |
| `layer.extent.temporal` | `EX_Extent.temporalElement.EX_TemporalExtent` | `.../gmd:extent/.../gmd:temporalElement/gmd:EX_TemporalExtent` | Temporal extent |

### Contact Information

| Core Metadata | ISO 19115 Element | Notes |
|---------------|-------------------|-------|
| `layer.catalog.contacts` | `MD_DataIdentification.pointOfContact` | Contact for the resource |
| `catalog.contact` | `MD_Metadata.contact` | Contact for the metadata |
| `iso19115.metadataContact` | `MD_Metadata.contact` | Explicit metadata contact |

### Date Information

| Core Metadata | ISO 19115 Element | Date Type | Notes |
|---------------|-------------------|-----------|-------|
| `iso19115.dateInfo.creation` | `CI_Citation.date` | `creation` | Date resource created |
| `iso19115.dateInfo.publication` | `CI_Citation.date` | `publication` | Date resource published |
| `iso19115.dateInfo.revision` | `CI_Citation.date` | `revision` | Date resource last updated |

### Data Quality & Lineage

| ISO 19115 Metadata | Element | Notes |
|-------------------|---------|-------|
| `iso19115.lineage.statement` | `DQ_DataQuality.lineage.LI_Lineage.statement` | General lineage statement |
| `iso19115.lineage.sources` | `LI_Lineage.source` | Source datasets |
| `iso19115.lineage.processSteps` | `LI_Lineage.processStep` | Processing steps |
| `iso19115.dataQualityInfo.positionalAccuracy` | `DQ_DataQuality.report.DQ_PositionalAccuracy` | Spatial accuracy |
| `iso19115.dataQualityInfo.completeness` | `DQ_DataQuality.report.DQ_CompletenessCommission` | Data completeness |

### Constraints

| ISO 19115 Metadata | Element | Notes |
|-------------------|---------|-------|
| `iso19115.resourceConstraints.useLimitation` | `MD_Constraints.useLimitation` | Usage limitations |
| `iso19115.resourceConstraints.accessConstraints` | `MD_LegalConstraints.accessConstraints` | Access restrictions |
| `iso19115.resourceConstraints.useConstraints` | `MD_LegalConstraints.useConstraints` | Use restrictions |

---

## CSW Output Format Selection

### Request Parameter: `outputSchema`

CSW `GetRecords` and `GetRecordById` requests can specify output format:

**Dublin Core (default):**
```
GET /csw?request=GetRecordById&id=parcels&outputSchema=http://www.opengis.net/cat/csw/2.0.2
```

**ISO 19139 (if `iso19115` metadata present):**
```
GET /csw?request=GetRecordById&id=parcels&outputSchema=http://www.isotc211.org/2005/gmd
```

### Response Format Examples

#### Dublin Core Response (Current)
```xml
<csw:GetRecordByIdResponse>
  <csw:Record>
    <dc:identifier>parcels</dc:identifier>
    <dc:title>Property Parcels</dc:title>
    <dct:abstract>Property parcel boundaries for the city</dct:abstract>
    <dc:subject>cadastre</dc:subject>
    <dc:subject>parcels</dc:subject>
    <ows:BoundingBox crs="urn:ogc:def:crs:EPSG::4326">
      <ows:LowerCorner>-122.5 37.7</ows:LowerCorner>
      <ows:UpperCorner>-122.3 37.9</ows:UpperCorner>
    </ows:BoundingBox>
  </csw:Record>
</csw:GetRecordByIdResponse>
```

#### ISO 19139 Response (Proposed)
```xml
<csw:GetRecordByIdResponse>
  <gmd:MD_Metadata>
    <gmd:fileIdentifier>
      <gco:CharacterString>urn:uuid:550e8400-e29b-41d4-a716-446655440000</gco:CharacterString>
    </gmd:fileIdentifier>
    <gmd:language>
      <gco:CharacterString>eng</gco:CharacterString>
    </gmd:language>
    <gmd:characterSet>
      <gmd:MD_CharacterSetCode codeListValue="utf8" codeList="http://www.isotc211.org/2005/resources/Codelist/gmxCodelists.xml#MD_CharacterSetCode">utf8</gmd:MD_CharacterSetCode>
    </gmd:characterSet>
    <gmd:contact>
      <gmd:CI_ResponsibleParty>
        <gmd:organisationName>
          <gco:CharacterString>City Planning Department</gco:CharacterString>
        </gmd:organisationName>
        <gmd:contactInfo>
          <gmd:CI_Contact>
            <gmd:phone>
              <gmd:CI_Telephone>
                <gmd:voice><gco:CharacterString>+1-555-0100</gco:CharacterString></gmd:voice>
              </gmd:CI_Telephone>
            </gmd:phone>
            <gmd:address>
              <gmd:CI_Address>
                <gmd:deliveryPoint><gco:CharacterString>123 Main Street</gco:CharacterString></gmd:deliveryPoint>
                <gmd:city><gco:CharacterString>Springfield</gco:CharacterString></gmd:city>
                <gmd:administrativeArea><gco:CharacterString>State</gco:CharacterString></gmd:administrativeArea>
                <gmd:postalCode><gco:CharacterString>12345</gco:CharacterString></gmd:postalCode>
                <gmd:country><gco:CharacterString>USA</gco:CharacterString></gmd:country>
                <gmd:electronicMailAddress><gco:CharacterString>gis@city.gov</gco:CharacterString></gmd:electronicMailAddress>
              </gmd:CI_Address>
            </gmd:address>
          </gmd:CI_Contact>
        </gmd:contactInfo>
        <gmd:role>
          <gmd:CI_RoleCode codeListValue="pointOfContact" codeList="http://www.isotc211.org/2005/resources/Codelist/gmxCodelists.xml#CI_RoleCode">pointOfContact</gmd:CI_RoleCode>
        </gmd:role>
      </gmd:CI_ResponsibleParty>
    </gmd:contact>
    <gmd:dateStamp>
      <gco:DateTime>2024-12-01T00:00:00Z</gco:DateTime>
    </gmd:dateStamp>
    <gmd:metadataStandardName>
      <gco:CharacterString>ISO 19115:2014</gco:CharacterString>
    </gmd:metadataStandardName>
    <gmd:identificationInfo>
      <gmd:MD_DataIdentification>
        <gmd:citation>
          <gmd:CI_Citation>
            <gmd:title>
              <gco:CharacterString>Property Parcels</gco:CharacterString>
            </gmd:title>
            <gmd:date>
              <gmd:CI_Date>
                <gmd:date><gco:DateTime>2020-01-15T00:00:00Z</gco:DateTime></gmd:date>
                <gmd:dateType>
                  <gmd:CI_DateTypeCode codeListValue="creation" codeList="...">creation</gmd:CI_DateTypeCode>
                </gmd:dateType>
              </gmd:CI_Date>
            </gmd:date>
          </gmd:CI_Citation>
        </gmd:citation>
        <gmd:abstract>
          <gco:CharacterString>Property parcel boundaries for the city</gco:CharacterString>
        </gmd:abstract>
        <gmd:spatialRepresentationType>
          <gmd:MD_SpatialRepresentationTypeCode codeListValue="vector" codeList="...">vector</gmd:MD_SpatialRepresentationTypeCode>
        </gmd:spatialRepresentationType>
        <gmd:extent>
          <gmd:EX_Extent>
            <gmd:geographicElement>
              <gmd:EX_GeographicBoundingBox>
                <gmd:westBoundLongitude><gco:Decimal>-122.5</gco:Decimal></gmd:westBoundLongitude>
                <gmd:eastBoundLongitude><gco:Decimal>-122.3</gco:Decimal></gmd:eastBoundLongitude>
                <gmd:southBoundLatitude><gco:Decimal>37.7</gco:Decimal></gmd:southBoundLatitude>
                <gmd:northBoundLatitude><gco:Decimal>37.9</gco:Decimal></gmd:northBoundLatitude>
              </gmd:EX_GeographicBoundingBox>
            </gmd:geographicElement>
          </gmd:EX_Extent>
        </gmd:extent>
      </gmd:MD_DataIdentification>
    </gmd:identificationInfo>
    <gmd:dataQualityInfo>
      <gmd:DQ_DataQuality>
        <gmd:scope>
          <gmd:DQ_Scope>
            <gmd:level>
              <gmd:MD_ScopeCode codeListValue="dataset" codeList="...">dataset</gmd:MD_ScopeCode>
            </gmd:level>
          </gmd:DQ_Scope>
        </gmd:scope>
        <gmd:lineage>
          <gmd:LI_Lineage>
            <gmd:statement>
              <gco:CharacterString>Digitized from survey plats and deed descriptions. Updated quarterly from assessor records.</gco:CharacterString>
            </gmd:statement>
          </gmd:LI_Lineage>
        </gmd:lineage>
      </gmd:DQ_DataQuality>
    </gmd:dataQualityInfo>
  </gmd:MD_Metadata>
</csw:GetRecordByIdResponse>
```

---

## Implementation Strategy

### Phase 1: Data Model Extension

1. **Add ISO 19115 metadata definitions** to `MetadataSnapshot.cs`:
```csharp
public sealed record Iso19115Metadata
{
    public string? MetadataIdentifier { get; init; }
    public Iso19115MetadataStandard? MetadataStandard { get; init; }
    public Iso19115Contact? MetadataContact { get; init; }
    public Iso19115DateInfo? DateInfo { get; init; }
    public string? SpatialRepresentationType { get; init; }
    public Iso19115SpatialResolution? SpatialResolution { get; init; }
    public string? Language { get; init; }
    public string? CharacterSet { get; init; }
    public IReadOnlyList<string> TopicCategory { get; init; } = Array.Empty<string>();
    public Iso19115ResourceConstraints? ResourceConstraints { get; init; }
    public Iso19115Lineage? Lineage { get; init; }
    public Iso19115DataQualityInfo? DataQualityInfo { get; init; }
    public Iso19115MaintenanceInfo? MaintenanceInfo { get; init; }
    public Iso19115DistributionInfo? DistributionInfo { get; init; }
    public Iso19115ReferenceSystemInfo? ReferenceSystemInfo { get; init; }
}

public sealed record LayerDefinition
{
    // ... existing properties ...
    public Iso19115Metadata? Iso19115 { get; init; }
}
```

2. **Update JSON schema** to include `iso19115` block

### Phase 2: CSW Handler Enhancement

1. **Detect `outputSchema` parameter** in `CswHandlers.cs`
2. **Conditionally generate ISO 19139 XML** if `layer.Iso19115 != null`
3. **Fall back to Dublin Core** if ISO metadata absent or Dublin Core requested
4. **Add unit tests** for ISO 19139 output generation

### Phase 3: Validation

1. **Extend `ProtocolMetadataValidator`** with ISO 19115 validation
2. **Warn if CSW enabled but no ISO metadata** (for enterprise catalogs)
3. **Validate ISO metadata completeness** (required vs. optional elements)

### Phase 4: Documentation & Examples

1. **Update metadata authoring guide** with ISO 19115 examples
2. **Provide sample metadata files** with ISO blocks
3. **Document ISO code lists** (role codes, date types, constraint types, etc.)

---

## Backward Compatibility

### Principles

1. **ISO 19115 metadata is optional** - CSW works without it (Dublin Core only)
2. **No breaking changes** - Existing metadata files continue to work
3. **Graceful degradation** - Missing ISO metadata → Dublin Core response
4. **Explicit opt-in** - Users must add `iso19115` block to enable

### Migration Path

**Existing deployments:**
- No action required
- CSW continues serving Dublin Core

**New deployments needing rich metadata:**
- Add `iso19115` block to layers
- Request ISO 19139 output via `outputSchema` parameter

---

## Benefits

### For CSW Consumers

1. **Richer metadata** - Data quality, lineage, constraints, maintenance info
2. **Enterprise catalog integration** - Compatible with GeoNetwork, pycsw, ArcGIS Geoportal
3. **Harvesting support** - More complete metadata for catalog harvesters
4. **INSPIRE compliance** - Meets EU INSPIRE metadata requirements (with proper population)

### For Honua

1. **Competitive parity** - Matches GeoServer's ISO 19139 support
2. **Enterprise readiness** - Meets government/enterprise metadata requirements
3. **Maintains semantic mapping** - One definition, multiple outputs
4. **Standards compliance** - Full CSW 2.0.2 AP ISO support

---

## Reference ISO 19115 Code Lists

### Topic Categories (MD_TopicCategoryCode)
- `farming` - Agriculture, irrigation, aquaculture, plantations
- `biota` - Flora and fauna, biological sciences
- `boundaries` - Legal boundaries, political boundaries
- `climatologyMeteorologyAtmosphere` - Weather, climate, atmosphere
- `economy` - Economic activities, commerce, revenue
- `elevation` - Altitude, bathymetry, digital elevation models
- `environment` - Environmental monitoring, habitat, ecosystems
- `geoscientificInformation` - Geology, geophysics, soils, minerals
- `health` - Health services, disease, public health
- `imageryBaseMapsEarthCover` - Base maps, land cover, imagery
- `intelligenceMilitary` - Military bases, intelligence activities
- `inlandWaters` - Rivers, lakes, water quality, hydrography
- `location` - Addresses, geodetic networks, control points
- `oceans` - Marine features, oceanography, bathymetry
- `planningCadastre` - Land use, zoning, cadastral surveys, parcels
- `society` - Demographics, anthropology, archaeology, education
- `structure` - Buildings, infrastructure, transportation
- `transportation` - Roads, railways, airports, shipping routes
- `utilitiesCommunication` - Utilities, communication networks

### Maintenance Frequency (MD_MaintenanceFrequencyCode)
- `continual` - Data is repeatedly and frequently updated
- `daily` - Updated each day
- `weekly` - Updated each week
- `fortnightly` - Updated every two weeks
- `monthly` - Updated each month
- `quarterly` - Updated every three months
- `biannually` - Updated twice per year
- `annually` - Updated once per year
- `asNeeded` - Updated as needed
- `irregular` - Updated at irregular intervals
- `notPlanned` - No updates planned
- `unknown` - Frequency unknown

### Role Codes (CI_RoleCode)
- `resourceProvider` - Party that supplies the resource
- `custodian` - Party that manages the resource
- `owner` - Party that owns the resource
- `user` - Party that uses the resource
- `distributor` - Party that distributes the resource
- `originator` - Party that created the resource
- `pointOfContact` - Party that can be contacted for information
- `principalInvestigator` - Key party responsible for gathering information
- `processor` - Party that processed the data
- `publisher` - Party that published the resource
- `author` - Party who authored the resource

### Spatial Representation Type (MD_SpatialRepresentationTypeCode)
- `vector` - Vector data (points, lines, polygons)
- `grid` - Grid data (rasters, grids)
- `textTable` - Textual or tabular data
- `tin` - Triangulated irregular network
- `stereoModel` - Stereoscopic model
- `video` - Video imagery

---

## Example Use Cases

### Use Case 1: Government Parcel Data
**Requirement:** FGDC/ISO compliant metadata for federal catalog harvesting

**Solution:**
- Add full `iso19115` block with data quality, lineage, constraints
- Serve via CSW with ISO 19139 output
- Harvestable by Data.gov, state geoportals

### Use Case 2: Scientific Datasets
**Requirement:** Document data provenance, accuracy, processing history

**Solution:**
- Populate `lineage.processSteps` with processing workflow
- Add `dataQualityInfo.positionalAccuracy` with accuracy metrics
- Include `maintenanceInfo` with update schedule

### Use Case 3: Commercial Data Distribution
**Requirement:** Clearly state usage constraints and licensing

**Solution:**
- Use `resourceConstraints.useLimitation` for license text
- Set `resourceConstraints.accessConstraints` and `useConstraints`
- Add `distributionInfo` with distributor contact and formats

---

## Conclusion

Adding ISO 19115 support to Honua's metadata model:

✅ **Enhances CSW capabilities** - Moves beyond basic Dublin Core
✅ **Maintains semantic mapping** - One definition, multiple output formats
✅ **Preserves backward compatibility** - Fully optional, no breaking changes
✅ **Enables enterprise use cases** - Government, scientific, commercial catalogs
✅ **Competitive with GeoServer** - Matches open-source leader's CSW functionality

**Recommendation:** Implement in Phase 2 after core OGC/Esri functionality is stable.
