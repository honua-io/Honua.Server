# Large Service File Refactoring Plan

## Overview
Refactoring 4 large service files that violate Single Responsibility Principle.

## Target Files

### 1. PostgresSensorThingsRepository.cs (2,356 lines)
**Current State**: Single class handling all SensorThings API entities
**Violations**:
- Manages 8 different entity types in one class
- 2,356 lines of tightly coupled CRUD operations
- Difficult to test individual entity operations
- Hard to maintain and extend

**Refactoring Plan**:
1. **Extract Internal Interfaces** (in Postgres folder):
   - `IInternalThingRepository`
   - `IInternalObservationRepository`
   - `IInternalLocationRepository`
   - `IInternalSensorRepository`
   - `IInternalObservedPropertyRepository`
   - `IInternalDatastreamRepository`
   - `IInternalFeatureOfInterestRepository`
   - `IInternalHistoricalLocationRepository`

2. **Create Repository Implementations**:
   - `PostgresThingRepository` (~300 lines) - Thing CRUD
   - `PostgresObservationRepository` (~400 lines) - Observation CRUD + batch
   - `PostgresLocationRepository` (~250 lines) - Location CRUD
   - `PostgresSensorRepository` (~200 lines) - Sensor CRUD
   - `PostgresObservedPropertyRepository` (~200 lines) - ObservedProperty CRUD
   - `PostgresDatastreamRepository` (~300 lines) - Datastream CRUD
   - `PostgresFeatureOfInterestRepository` (~350 lines) - FeatureOfInterest CRUD
   - `PostgresHistoricalLocationRepository` (~150 lines) - Read-only

3. **Extract Shared Logic**:
   - `PostgresQueryHelper` (~200 lines) - Filter translation, parameter building

4. **Convert to Facade**:
   - Update `PostgresSensorThingsRepository` to delegate to sub-repositories
   - Maintain backward compatibility with `ISensorThingsRepository`
   - Lazy-initialize sub-repositories on demand

**Benefits**:
- Each repository has single responsibility
- ~250-400 lines per class (manageable size)
- Easier to test individual entity operations
- Better code organization and maintainability
- Can mock sub-repositories for unit testing

---

### 2. GenerateInfrastructureCodeStep.cs (2,109 lines)
**Current State**: Single class generating Terraform for AWS/Azure/GCP
**Violations**:
- Mixed cloud provider concerns in one class
- 800-1300 lines per provider embedded as strings
- Difficult to test individual provider logic
- Hard to add new cloud providers

**Refactoring Plan**:
1. **Create Provider-Specific Generators**:
   - `AwsTerraformGenerator` (~450 lines) - AWS-specific Terraform
   - `AzureTerraformGenerator` (~350 lines) - Azure-specific Terraform
   - `GcpTerraformGenerator` (~400 lines) - GCP-specific Terraform

2. **Create Common Generator Components**:
   - `TerraformVariablesGenerator` (~300 lines) - Variables.tf generation
   - `TerraformConfigGenerator` (~200 lines) - TfVars generation
   - `ITerraformGenerator` interface - Common abstraction

3. **Extract Infrastructure Helpers**:
   - `InfrastructureNamingHelper` (~100 lines) - Name sanitization
   - `InfrastructureCostEstimator` (~100 lines) - Cost calculation

4. **Update Coordinator**:
   - `GenerateInfrastructureCodeStep` becomes coordinator (~200 lines)
   - Uses strategy pattern to delegate to provider-specific generators
   - Maintains backward compatibility

**Benefits**:
- Each generator has single cloud responsibility
- ~300-450 lines per generator
- Easy to add new cloud providers
- Testable in isolation
- Clear separation of provider concerns

---

### 3. RelationalStacCatalogStore.cs (1,974 lines)
**Current State**: Single class handling all STAC operations
**Violations**:
- Collection, Item, and Search operations mixed
- Complex search logic embedded in large class
- Difficult to optimize individual operations
- Hard to test search vs CRUD separately

**Refactoring Plan**:
1. **Extract Store Components**:
   - `StacCollectionStore` (~350 lines) - Collection CRUD
   - `StacItemStore` (~400 lines) - Item CRUD + bulk operations
   - `StacSearchEngine` (~500 lines) - Search + streaming

2. **Extract Query Components**:
   - `StacSearchQueryBuilder` (~200 lines) - SQL query building
   - `StacCountOptimizer` (~150 lines) - Count optimization strategies

3. **Extract Shared Logic**:
   - `StacParameterBuilder` (~100 lines) - Parameter building
   - `StacRecordMapper` (~150 lines) - Row to record mapping

4. **Update Base Class**:
   - `RelationalStacCatalogStore` becomes coordinator (~250 lines)
   - Delegates to specialized components
   - Maintains backward compatibility with `IStacCatalogStore`

**Benefits**:
- Separate concerns: CRUD vs Search
- Search engine can be optimized independently
- Easier to test collection vs item operations
- Better performance optimization opportunities
- Clear component boundaries

---

### 4. ZarrTimeSeriesService.cs (1,791 lines)
**Current State**: Single class handling Zarr conversion, queries, and metadata
**Violations**:
- Python interop, queries, metadata parsing all mixed
- Complex coordinate and aggregation logic embedded
- Difficult to test individual operations
- Hard to add new data sources

**Refactoring Plan**:
1. **Extract Operation Components**:
   - `ZarrPythonInterop` (~250 lines) - Python script execution
   - `ZarrMetadataParser` (~400 lines) - Metadata extraction
   - `ZarrTimeSeriesQuery` (~350 lines) - Time queries

2. **Extract Processing Components**:
   - `ZarrSpatialProcessor` (~200 lines) - Spatial calculations
   - `ZarrAggregator` (~200 lines) - Time series aggregation
   - `ZarrDataConverter` (~200 lines) - Type conversions

3. **Update Coordinator**:
   - `ZarrTimeSeriesService` becomes facade (~250 lines)
   - Delegates to specialized components
   - Maintains `IZarrTimeSeriesService` compatibility

**Benefits**:
- Each component has focused responsibility
- ~200-400 lines per component
- Can swap Python interop implementation
- Testable aggregation and conversion logic
- Clear data flow through components

---

## Implementation Priority
1. **PostgresSensorThingsRepository.cs** (Highest priority - most complex)
2. **GenerateInfrastructureCodeStep.cs** (High priority - clear boundaries)
3. **RelationalStacCatalogStore.cs** (Medium priority)

## Success Criteria
- All existing tests pass
- Each new class < 500 lines
- Clear single responsibility per class
- Backward compatible with existing interfaces
- Well-documented new architecture
