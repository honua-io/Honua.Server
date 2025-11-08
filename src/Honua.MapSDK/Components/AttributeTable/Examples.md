# HonuaAttributeTable - Examples

Complete working examples demonstrating various use cases for the HonuaAttributeTable component.

## Table of Contents

1. [Basic Attribute Table with Map Sync](#example-1-basic-attribute-table-with-map-sync)
2. [Editable Table with Inline Editing](#example-2-editable-table-with-inline-editing)
3. [Custom Column Templates and Formatting](#example-3-custom-column-templates-and-formatting)
4. [Advanced Filtering and Search](#example-4-advanced-filtering-and-search)
5. [Export to Multiple Formats](#example-5-export-to-multiple-formats)
6. [Summary Row with Calculations](#example-6-summary-row-with-calculations)
7. [Conditional Formatting](#example-7-conditional-formatting)
8. [Bulk Operations and Field Calculator](#example-8-bulk-operations-and-field-calculator)

---

## Example 1: Basic Attribute Table with Map Sync

A simple attribute table synchronized with a map, showing parcel data with click-to-zoom functionality.

```razor
@page "/examples/attribute-table-basic"
@using Honua.MapSDK.Components.AttributeTable
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Models

<PageTitle>Basic Attribute Table</PageTitle>

<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">Real Estate Parcels</MudText>

    <MudGrid>
        <!-- Map View -->
        <MudItem xs="12" md="7">
            <MudPaper Elevation="2" Style="height: 600px;">
                <HonuaMap Id="parcel-map"
                          @ref="_map"
                          Center="@(new[] { -122.4194, 37.7749 })"
                          Zoom="12"
                          Style="mapbox://styles/mapbox/streets-v11" />
            </MudPaper>
        </MudItem>

        <!-- Attribute Table -->
        <MudItem xs="12" md="5">
            <HonuaAttributeTable SyncWith="parcel-map"
                                LayerId="parcels"
                                Features="@_features"
                                Title="Parcel Attributes"
                                HighlightSelected="true"
                                PageSize="50"
                                OnRowSelected="@OnParcelSelected" />
        </MudItem>
    </MudGrid>
</MudContainer>

@code {
    private HonuaMap? _map;
    private List<FeatureRecord> _features = new();

    protected override void OnInitialized()
    {
        // Sample parcel data
        _features = new List<FeatureRecord>
        {
            new FeatureRecord
            {
                Id = "P001",
                Properties = new Dictionary<string, object?>
                {
                    ["ParcelID"] = "P001",
                    ["Address"] = "123 Main St",
                    ["Owner"] = "John Smith",
                    ["AssessedValue"] = 750000,
                    ["LandUse"] = "Residential",
                    ["Acres"] = 0.25,
                    ["YearBuilt"] = 1985,
                    ["Bedrooms"] = 3,
                    ["Bathrooms"] = 2
                },
                GeometryType = "Polygon",
                Geometry = CreateSamplePolygon(-122.42, 37.78)
            },
            new FeatureRecord
            {
                Id = "P002",
                Properties = new Dictionary<string, object?>
                {
                    ["ParcelID"] = "P002",
                    ["Address"] = "456 Oak Ave",
                    ["Owner"] = "Jane Doe",
                    ["AssessedValue"] = 1200000,
                    ["LandUse"] = "Commercial",
                    ["Acres"] = 0.50,
                    ["YearBuilt"] = 2010,
                    ["Bedrooms"] = null,
                    ["Bathrooms"] = null
                },
                GeometryType = "Polygon",
                Geometry = CreateSamplePolygon(-122.41, 37.77)
            },
            new FeatureRecord
            {
                Id = "P003",
                Properties = new Dictionary<string, object?>
                {
                    ["ParcelID"] = "P003",
                    ["Address"] = "789 Pine St",
                    ["Owner"] = "Smith Properties LLC",
                    ["AssessedValue"] = 2500000,
                    ["LandUse"] = "Industrial",
                    ["Acres"] = 2.00,
                    ["YearBuilt"] = 2005,
                    ["Bedrooms"] = null,
                    ["Bathrooms"] = null
                },
                GeometryType = "Polygon",
                Geometry = CreateSamplePolygon(-122.43, 37.79)
            }
        };
    }

    private void OnParcelSelected(FeatureRecord feature)
    {
        Console.WriteLine($"Selected parcel: {feature.Properties["Address"]}");
        // The map will automatically zoom to and highlight the selected feature
    }

    private object CreateSamplePolygon(double lng, double lat)
    {
        // Create a simple square polygon
        return new
        {
            type = "Polygon",
            coordinates = new[]
            {
                new[]
                {
                    new[] { lng - 0.001, lat - 0.001 },
                    new[] { lng + 0.001, lat - 0.001 },
                    new[] { lng + 0.001, lat + 0.001 },
                    new[] { lng - 0.001, lat + 0.001 },
                    new[] { lng - 0.001, lat - 0.001 }
                }
            }
        };
    }
}
```

---

## Example 2: Editable Table with Inline Editing

Allow users to edit feature attributes directly in the table with validation.

```razor
@page "/examples/attribute-table-editable"
@using Honua.MapSDK.Components.AttributeTable
@using Honua.MapSDK.Models

<PageTitle>Editable Attribute Table</PageTitle>

<MudContainer MaxWidth="MaxWidth.Large" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">Asset Management</MudText>

    <MudAlert Severity="Severity.Info" Class="mb-4">
        Click any cell to edit. Changes are tracked and can be saved or reverted.
    </MudAlert>

    <HonuaAttributeTable Features="@_assets"
                        Title="Asset Inventory"
                        AllowEdit="true"
                        AllowDelete="true"
                        SelectionMode="SelectionMode.Multiple"
                        OnRowsUpdated="@OnAssetsUpdated"
                        OnRowDeleted="@OnAssetDeleted"
                        Style="height: 600px;" />

    @if (_modifiedAssets.Count > 0)
    {
        <MudPaper Class="mt-4 pa-4">
            <MudText Typo="Typo.h6" Class="mb-2">Pending Changes (@_modifiedAssets.Count)</MudText>
            <MudStack Row="true" Spacing="2">
                <MudButton Variant="Variant.Filled"
                          Color="Color.Primary"
                          OnClick="@SaveChanges"
                          StartIcon="@Icons.Material.Filled.Save">
                    Save Changes
                </MudButton>
                <MudButton Variant="Variant.Outlined"
                          Color="Color.Default"
                          OnClick="@DiscardChanges"
                          StartIcon="@Icons.Material.Filled.Cancel">
                    Discard
                </MudButton>
            </MudStack>
        </MudPaper>
    }
</MudContainer>

@code {
    private List<FeatureRecord> _assets = new();
    private HashSet<string> _modifiedAssets = new();

    protected override void OnInitialized()
    {
        _assets = new List<FeatureRecord>
        {
            new FeatureRecord
            {
                Id = "A001",
                Properties = new Dictionary<string, object?>
                {
                    ["AssetID"] = "A001",
                    ["Name"] = "Water Pump Station #1",
                    ["Type"] = "Infrastructure",
                    ["Status"] = "Active",
                    ["InstallDate"] = new DateTime(2018, 5, 15),
                    ["LastMaintenance"] = new DateTime(2024, 1, 10),
                    ["Condition"] = "Good",
                    ["EstimatedValue"] = 85000,
                    ["MaintenanceCost"] = 2500
                },
                GeometryType = "Point"
            },
            new FeatureRecord
            {
                Id = "A002",
                Properties = new Dictionary<string, object?>
                {
                    ["AssetID"] = "A002",
                    ["Name"] = "Traffic Signal - Main & 1st",
                    ["Type"] = "Traffic Control",
                    ["Status"] = "Active",
                    ["InstallDate"] = new DateTime(2015, 8, 20),
                    ["LastMaintenance"] = new DateTime(2023, 11, 5),
                    ["Condition"] = "Fair",
                    ["EstimatedValue"] = 45000,
                    ["MaintenanceCost"] = 1200
                },
                GeometryType = "Point"
            },
            new FeatureRecord
            {
                Id = "A003",
                Properties = new Dictionary<string, object?>
                {
                    ["AssetID"] = "A003",
                    ["Name"] = "Bridge - River Crossing",
                    ["Type"] = "Infrastructure",
                    ["Status"] = "Needs Repair",
                    ["InstallDate"] = new DateTime(1995, 3, 10),
                    ["LastMaintenance"] = new DateTime(2022, 6, 15),
                    ["Condition"] = "Poor",
                    ["EstimatedValue"] = 2500000,
                    ["MaintenanceCost"] = 150000
                },
                GeometryType = "LineString"
            }
        };
    }

    private void OnAssetsUpdated(List<FeatureRecord> updated)
    {
        foreach (var asset in updated)
        {
            _modifiedAssets.Add(asset.Id);
        }
        StateHasChanged();
    }

    private void OnAssetDeleted(string assetId)
    {
        Console.WriteLine($"Deleted asset: {assetId}");
        _modifiedAssets.Remove(assetId);
    }

    private async Task SaveChanges()
    {
        // In real application, save to database/API
        Console.WriteLine($"Saving {_modifiedAssets.Count} modified assets");

        // Simulate API call
        await Task.Delay(500);

        _modifiedAssets.Clear();
        StateHasChanged();

        // Show success message
        // Snackbar.Add("Changes saved successfully", Severity.Success);
    }

    private void DiscardChanges()
    {
        // Reload data from source
        OnInitialized();
        _modifiedAssets.Clear();
        StateHasChanged();
    }
}
```

---

## Example 3: Custom Column Templates and Formatting

Advanced column configuration with custom formatting and conditional styling.

```razor
@page "/examples/attribute-table-formatting"
@using Honua.MapSDK.Components.AttributeTable
@using Honua.MapSDK.Models

<PageTitle>Column Formatting</PageTitle>

<MudContainer MaxWidth="MaxWidth.Large" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">Sales Performance Dashboard</MudText>

    <HonuaAttributeTable Features="@_salesData"
                        Configuration="@_tableConfig"
                        Title="Regional Sales"
                        PageSize="25"
                        Style="height: 600px;" />
</MudContainer>

@code {
    private List<FeatureRecord> _salesData = new();
    private TableConfiguration _tableConfig = new();

    protected override void OnInitialized()
    {
        // Configure columns with advanced formatting
        _tableConfig = new TableConfiguration
        {
            ShowSummary = true,
            Columns = new List<ColumnConfig>
            {
                new ColumnConfig
                {
                    FieldName = "Region",
                    DisplayName = "Sales Region",
                    DataType = ColumnDataType.String,
                    Visible = true,
                    Frozen = true,
                    Width = 150
                },
                new ColumnConfig
                {
                    FieldName = "Revenue",
                    DisplayName = "Revenue",
                    DataType = ColumnDataType.Currency,
                    Format = "C0",
                    Visible = true,
                    ConditionalFormats = new List<ConditionalFormat>
                    {
                        new ConditionalFormat
                        {
                            Condition = "> 1000000",
                            BackgroundColor = "#e8f5e9",
                            TextColor = "#2e7d32",
                            FontWeight = "bold"
                        },
                        new ConditionalFormat
                        {
                            Condition = "< 500000",
                            BackgroundColor = "#ffebee",
                            TextColor = "#c62828"
                        }
                    }
                },
                new ColumnConfig
                {
                    FieldName = "GrowthRate",
                    DisplayName = "Growth",
                    DataType = ColumnDataType.Percentage,
                    Format = "P1",
                    Visible = true,
                    ConditionalFormats = new List<ConditionalFormat>
                    {
                        new ConditionalFormat
                        {
                            Condition = "> 0",
                            TextColor = "#2e7d32",
                            Icon = Icons.Material.Filled.TrendingUp
                        },
                        new ConditionalFormat
                        {
                            Condition = "< 0",
                            TextColor = "#c62828",
                            Icon = Icons.Material.Filled.TrendingDown
                        }
                    }
                },
                new ColumnConfig
                {
                    FieldName = "Units",
                    DisplayName = "Units Sold",
                    DataType = ColumnDataType.Integer,
                    Format = "N0",
                    Visible = true
                },
                new ColumnConfig
                {
                    FieldName = "AvgPrice",
                    DisplayName = "Avg Unit Price",
                    DataType = ColumnDataType.Currency,
                    Format = "C2",
                    Visible = true
                },
                new ColumnConfig
                {
                    FieldName = "LastSale",
                    DisplayName = "Last Sale",
                    DataType = ColumnDataType.DateTime,
                    Visible = true
                },
                new ColumnConfig
                {
                    FieldName = "Active",
                    DisplayName = "Active",
                    DataType = ColumnDataType.Boolean,
                    Visible = true,
                    Width = 80
                }
            },
            Summaries = new List<SummaryConfig>
            {
                new SummaryConfig
                {
                    FieldName = "Revenue",
                    Function = AggregateFunction.Sum,
                    Label = "Total Revenue",
                    Format = "C0"
                },
                new SummaryConfig
                {
                    FieldName = "Units",
                    Function = AggregateFunction.Sum,
                    Label = "Total Units",
                    Format = "N0"
                },
                new SummaryConfig
                {
                    FieldName = "GrowthRate",
                    Function = AggregateFunction.Average,
                    Label = "Avg Growth",
                    Format = "P1"
                }
            }
        };

        // Sample sales data
        _salesData = new List<FeatureRecord>
        {
            new FeatureRecord
            {
                Id = "R1",
                Properties = new Dictionary<string, object?>
                {
                    ["Region"] = "North America",
                    ["Revenue"] = 1250000,
                    ["GrowthRate"] = 0.15,
                    ["Units"] = 450,
                    ["AvgPrice"] = 2777.78,
                    ["LastSale"] = DateTime.Now.AddDays(-2),
                    ["Active"] = true
                }
            },
            new FeatureRecord
            {
                Id = "R2",
                Properties = new Dictionary<string, object?>
                {
                    ["Region"] = "Europe",
                    ["Revenue"] = 980000,
                    ["GrowthRate"] = 0.08,
                    ["Units"] = 320,
                    ["AvgPrice"] = 3062.50,
                    ["LastSale"] = DateTime.Now.AddDays(-1),
                    ["Active"] = true
                }
            },
            new FeatureRecord
            {
                Id = "R3",
                Properties = new Dictionary<string, object?>
                {
                    ["Region"] = "Asia Pacific",
                    ["Revenue"] = 1750000,
                    ["GrowthRate"] = 0.25,
                    ["Units"] = 680,
                    ["AvgPrice"] = 2573.53,
                    ["LastSale"] = DateTime.Now,
                    ["Active"] = true
                }
            },
            new FeatureRecord
            {
                Id = "R4",
                Properties = new Dictionary<string, object?>
                {
                    ["Region"] = "South America",
                    ["Revenue"] = 420000,
                    ["GrowthRate"] = -0.05,
                    ["Units"] = 180,
                    ["AvgPrice"] = 2333.33,
                    ["LastSale"] = DateTime.Now.AddDays(-7),
                    ["Active"] = false
                }
            }
        };
    }
}
```

---

## Example 4: Advanced Filtering and Search

Implement complex filtering scenarios with saved presets.

```razor
@page "/examples/attribute-table-filtering"
@using Honua.MapSDK.Components.AttributeTable
@using Honua.MapSDK.Models

<PageTitle>Advanced Filtering</PageTitle>

<MudContainer MaxWidth="MaxWidth.Large" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">Property Search</MudText>

    <MudPaper Class="pa-4 mb-4">
        <MudText Typo="Typo.h6" Class="mb-2">Quick Filters</MudText>
        <MudStack Row="true" Spacing="2">
            <MudButton Variant="Variant.Filled"
                      Color="Color.Primary"
                      Size="Size.Small"
                      OnClick="@(() => ApplyFilter(_highValueFilter))">
                High Value (>$1M)
            </MudButton>
            <MudButton Variant="Variant.Filled"
                      Color="Color.Secondary"
                      Size="Size.Small"
                      OnClick="@(() => ApplyFilter(_newConstructionFilter))">
                New Construction
            </MudButton>
            <MudButton Variant="Variant.Filled"
                      Color="Color.Tertiary"
                      Size="Size.Small"
                      OnClick="@(() => ApplyFilter(_largeLotsFilter))">
                Large Lots (>1 acre)
            </MudButton>
            <MudButton Variant="Variant.Outlined"
                      Color="Color.Default"
                      Size="Size.Small"
                      OnClick="@ClearFilters">
                Clear Filters
            </MudButton>
        </MudStack>
    </MudPaper>

    <HonuaAttributeTable @ref="_table"
                        Features="@_displayedProperties"
                        Title="Property Listings"
                        AllowExport="true"
                        PageSize="50"
                        Style="height: 600px;" />
</MudContainer>

@code {
    private HonuaAttributeTable? _table;
    private List<FeatureRecord> _allProperties = new();
    private List<FeatureRecord> _displayedProperties = new();

    private FilterConfig _highValueFilter = new()
    {
        Name = "High Value Properties",
        Type = FilterType.Simple,
        Field = "Price",
        Operator = FilterOperator.GreaterThan,
        Value = 1000000
    };

    private FilterConfig _newConstructionFilter = new()
    {
        Name = "New Construction",
        Type = FilterType.Simple,
        Field = "YearBuilt",
        Operator = FilterOperator.GreaterThanOrEqual,
        Value = 2020
    };

    private FilterConfig _largeLotsFilter = new()
    {
        Name = "Large Lots",
        Type = FilterType.Simple,
        Field = "LotSize",
        Operator = FilterOperator.GreaterThan,
        Value = 1.0
    };

    protected override void OnInitialized()
    {
        _allProperties = GenerateSampleProperties(100);
        _displayedProperties = _allProperties;
    }

    private void ApplyFilter(FilterConfig filter)
    {
        _displayedProperties = filter.Operator switch
        {
            FilterOperator.GreaterThan => _allProperties
                .Where(p => Convert.ToDouble(p.Properties[filter.Field!]) > Convert.ToDouble(filter.Value))
                .ToList(),
            FilterOperator.GreaterThanOrEqual => _allProperties
                .Where(p => Convert.ToDouble(p.Properties[filter.Field!]) >= Convert.ToDouble(filter.Value))
                .ToList(),
            _ => _allProperties
        };

        StateHasChanged();
    }

    private void ClearFilters()
    {
        _displayedProperties = _allProperties;
        StateHasChanged();
    }

    private List<FeatureRecord> GenerateSampleProperties(int count)
    {
        var random = new Random();
        var properties = new List<FeatureRecord>();

        for (int i = 1; i <= count; i++)
        {
            properties.Add(new FeatureRecord
            {
                Id = $"PROP{i:D4}",
                Properties = new Dictionary<string, object?>
                {
                    ["PropertyID"] = $"PROP{i:D4}",
                    ["Address"] = $"{random.Next(100, 9999)} {GetRandomStreet(random)}",
                    ["Price"] = random.Next(200000, 3000000),
                    ["Bedrooms"] = random.Next(2, 6),
                    ["Bathrooms"] = random.Next(1, 4) + 0.5 * random.Next(0, 2),
                    ["SquareFeet"] = random.Next(1200, 5000),
                    ["LotSize"] = Math.Round(random.NextDouble() * 2 + 0.1, 2),
                    ["YearBuilt"] = random.Next(1950, 2024),
                    ["Status"] = GetRandomStatus(random)
                },
                GeometryType = "Point"
            });
        }

        return properties;
    }

    private string GetRandomStreet(Random random)
    {
        var streets = new[] { "Main St", "Oak Ave", "Pine Rd", "Elm Dr", "Maple Ln", "Cedar Way" };
        return streets[random.Next(streets.Length)];
    }

    private string GetRandomStatus(Random random)
    {
        var statuses = new[] { "For Sale", "Pending", "Sold", "Off Market" };
        return statuses[random.Next(statuses.Length)];
    }
}
```

---

## Example 5: Export to Multiple Formats

Demonstrate all export capabilities with custom export handlers.

```razor
@page "/examples/attribute-table-export"
@using Honua.MapSDK.Components.AttributeTable
@using Honua.MapSDK.Models

<PageTitle>Export Examples</PageTitle>

<MudContainer MaxWidth="MaxWidth.Large" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">Customer Data Export</MudText>

    <MudAlert Severity="Severity.Info" Class="mb-4">
        Select rows and use the export menu to download data in various formats.
        The export will include only visible columns.
    </MudAlert>

    <HonuaAttributeTable Features="@_customers"
                        Title="Customer Database"
                        AllowExport="true"
                        SelectionMode="SelectionMode.Multiple"
                        PageSize="100"
                        OnRowsSelected="@OnCustomersSelected"
                        Style="height: 600px;">
        <ToolbarContent>
            @if (_selectedCount > 0)
            {
                <MudChip Color="Color.Primary" Size="Size.Small">
                    @_selectedCount selected
                </MudChip>
            }
        </ToolbarContent>
    </HonuaAttributeTable>
</MudContainer>

@code {
    private List<FeatureRecord> _customers = new();
    private int _selectedCount = 0;

    protected override void OnInitialized()
    {
        // Generate sample customer data
        var random = new Random();
        _customers = new List<FeatureRecord>();

        for (int i = 1; i <= 250; i++)
        {
            _customers.Add(new FeatureRecord
            {
                Id = $"CUST{i:D4}",
                Properties = new Dictionary<string, object?>
                {
                    ["CustomerID"] = $"CUST{i:D4}",
                    ["Name"] = $"{GetRandomFirstName(random)} {GetRandomLastName(random)}",
                    ["Email"] = $"customer{i}@example.com",
                    ["Phone"] = $"({random.Next(200, 999)}) {random.Next(200, 999)}-{random.Next(1000, 9999)}",
                    ["Company"] = $"{GetRandomCompany(random)}",
                    ["Revenue"] = random.Next(5000, 500000),
                    ["SignupDate"] = DateTime.Now.AddDays(-random.Next(1, 365)),
                    ["Status"] = GetRandomCustomerStatus(random),
                    ["Country"] = GetRandomCountry(random),
                    ["Industry"] = GetRandomIndustry(random)
                },
                GeometryType = "Point"
            });
        }
    }

    private void OnCustomersSelected(List<FeatureRecord> selected)
    {
        _selectedCount = selected.Count;
        StateHasChanged();
    }

    private string GetRandomFirstName(Random random)
    {
        var names = new[] { "John", "Jane", "Michael", "Sarah", "David", "Emma", "Chris", "Lisa" };
        return names[random.Next(names.Length)];
    }

    private string GetRandomLastName(Random random)
    {
        var names = new[] { "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis" };
        return names[random.Next(names.Length)];
    }

    private string GetRandomCompany(Random random)
    {
        var companies = new[] { "Acme Corp", "TechStart", "Global Industries", "Mega Corp", "StartupXYZ" };
        return companies[random.Next(companies.Length)];
    }

    private string GetRandomCustomerStatus(Random random)
    {
        var statuses = new[] { "Active", "Inactive", "Trial", "Suspended" };
        return statuses[random.Next(statuses.Length)];
    }

    private string GetRandomCountry(Random random)
    {
        var countries = new[] { "USA", "Canada", "UK", "Germany", "France", "Japan", "Australia" };
        return countries[random.Next(countries.Length)];
    }

    private string GetRandomIndustry(Random random)
    {
        var industries = new[] { "Technology", "Finance", "Healthcare", "Retail", "Manufacturing", "Education" };
        return industries[random.Next(industries.Length)];
    }
}
```

---

## Example 6: Summary Row with Calculations

Display aggregate statistics in a summary row at the bottom of the table.

```razor
@page "/examples/attribute-table-summary"
@using Honua.MapSDK.Components.AttributeTable
@using Honua.MapSDK.Models

<PageTitle>Summary Calculations</PageTitle>

<MudContainer MaxWidth="MaxWidth.Large" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">Financial Analysis</MudText>

    <HonuaAttributeTable Features="@_transactions"
                        Configuration="@_tableConfig"
                        Title="Transaction History"
                        PageSize="50"
                        Style="height: 600px;" />
</MudContainer>

@code {
    private List<FeatureRecord> _transactions = new();
    private TableConfiguration _tableConfig = new();

    protected override void OnInitialized()
    {
        // Configure table with summary row
        _tableConfig = new TableConfiguration
        {
            ShowSummary = true,
            Columns = new List<ColumnConfig>
            {
                new ColumnConfig
                {
                    FieldName = "TransactionID",
                    DisplayName = "Transaction ID",
                    DataType = ColumnDataType.String,
                    Frozen = true,
                    Width = 150
                },
                new ColumnConfig
                {
                    FieldName = "Date",
                    DisplayName = "Date",
                    DataType = ColumnDataType.Date
                },
                new ColumnConfig
                {
                    FieldName = "Amount",
                    DisplayName = "Amount",
                    DataType = ColumnDataType.Currency,
                    Format = "C2"
                },
                new ColumnConfig
                {
                    FieldName = "Quantity",
                    DisplayName = "Quantity",
                    DataType = ColumnDataType.Integer,
                    Format = "N0"
                },
                new ColumnConfig
                {
                    FieldName = "UnitPrice",
                    DisplayName = "Unit Price",
                    DataType = ColumnDataType.Currency,
                    Format = "C2"
                },
                new ColumnConfig
                {
                    FieldName = "Category",
                    DisplayName = "Category",
                    DataType = ColumnDataType.String
                }
            },
            Summaries = new List<SummaryConfig>
            {
                new SummaryConfig
                {
                    FieldName = "Amount",
                    Function = AggregateFunction.Sum,
                    Label = "Total",
                    Format = "C2"
                },
                new SummaryConfig
                {
                    FieldName = "Amount",
                    Function = AggregateFunction.Average,
                    Label = "Average",
                    Format = "C2"
                },
                new SummaryConfig
                {
                    FieldName = "Amount",
                    Function = AggregateFunction.Min,
                    Label = "Minimum",
                    Format = "C2"
                },
                new SummaryConfig
                {
                    FieldName = "Amount",
                    Function = AggregateFunction.Max,
                    Label = "Maximum",
                    Format = "C2"
                },
                new SummaryConfig
                {
                    FieldName = "Quantity",
                    Function = AggregateFunction.Sum,
                    Label = "Total Units",
                    Format = "N0"
                }
            }
        };

        // Generate sample transactions
        var random = new Random();
        var categories = new[] { "Software", "Hardware", "Services", "Training", "Support" };

        for (int i = 1; i <= 100; i++)
        {
            var quantity = random.Next(1, 50);
            var unitPrice = random.Next(10, 1000);
            var amount = quantity * unitPrice;

            _transactions.Add(new FeatureRecord
            {
                Id = $"TXN{i:D4}",
                Properties = new Dictionary<string, object?>
                {
                    ["TransactionID"] = $"TXN{i:D4}",
                    ["Date"] = DateTime.Now.AddDays(-random.Next(0, 90)),
                    ["Amount"] = amount,
                    ["Quantity"] = quantity,
                    ["UnitPrice"] = unitPrice,
                    ["Category"] = categories[random.Next(categories.Length)]
                }
            });
        }
    }
}
```

---

## Example 7: Conditional Formatting

Apply visual styling based on data values with traffic light indicators.

```razor
@page "/examples/attribute-table-conditional"
@using Honua.MapSDK.Components.AttributeTable
@using Honua.MapSDK.Models

<PageTitle>Conditional Formatting</PageTitle>

<MudContainer MaxWidth="MaxWidth.Large" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">Project Status Dashboard</MudText>

    <MudAlert Severity="Severity.Info" Class="mb-4">
        Rows are color-coded based on project health: Green (On Track), Yellow (At Risk), Red (Critical)
    </MudAlert>

    <HonuaAttributeTable Features="@_projects"
                        Configuration="@_tableConfig"
                        Title="Active Projects"
                        PageSize="25"
                        Style="height: 600px;" />
</MudContainer>

@code {
    private List<FeatureRecord> _projects = new();
    private TableConfiguration _tableConfig = new();

    protected override void OnInitialized()
    {
        _tableConfig = new TableConfiguration
        {
            Columns = new List<ColumnConfig>
            {
                new ColumnConfig
                {
                    FieldName = "ProjectName",
                    DisplayName = "Project",
                    DataType = ColumnDataType.String,
                    Frozen = true,
                    Width = 200
                },
                new ColumnConfig
                {
                    FieldName = "Status",
                    DisplayName = "Status",
                    DataType = ColumnDataType.String,
                    ConditionalFormats = new List<ConditionalFormat>
                    {
                        new ConditionalFormat
                        {
                            Condition = "== 'On Track'",
                            BackgroundColor = "#c8e6c9",
                            TextColor = "#1b5e20",
                            FontWeight = "bold"
                        },
                        new ConditionalFormat
                        {
                            Condition = "== 'At Risk'",
                            BackgroundColor = "#fff9c4",
                            TextColor = "#f57f17",
                            FontWeight = "bold"
                        },
                        new ConditionalFormat
                        {
                            Condition = "== 'Critical'",
                            BackgroundColor = "#ffcdd2",
                            TextColor = "#b71c1c",
                            FontWeight = "bold"
                        }
                    }
                },
                new ColumnConfig
                {
                    FieldName = "Progress",
                    DisplayName = "Progress",
                    DataType = ColumnDataType.Percentage,
                    Format = "P0",
                    ConditionalFormats = new List<ConditionalFormat>
                    {
                        new ConditionalFormat
                        {
                            Condition = ">= 0.8",
                            TextColor = "#1b5e20"
                        },
                        new ConditionalFormat
                        {
                            Condition = "< 0.5",
                            TextColor = "#b71c1c"
                        }
                    }
                },
                new ColumnConfig
                {
                    FieldName = "Budget",
                    DisplayName = "Budget",
                    DataType = ColumnDataType.Currency,
                    Format = "C0"
                },
                new ColumnConfig
                {
                    FieldName = "Spent",
                    DisplayName = "Spent",
                    DataType = ColumnDataType.Currency,
                    Format = "C0",
                    ConditionalFormats = new List<ConditionalFormat>
                    {
                        // Spent more than budget
                        new ConditionalFormat
                        {
                            Condition = "> {Budget}",
                            BackgroundColor = "#ffebee",
                            TextColor = "#c62828",
                            FontWeight = "bold"
                        }
                    }
                },
                new ColumnConfig
                {
                    FieldName = "DueDate",
                    DisplayName = "Due Date",
                    DataType = ColumnDataType.Date
                },
                new ColumnConfig
                {
                    FieldName = "Manager",
                    DisplayName = "Project Manager",
                    DataType = ColumnDataType.String
                }
            }
        };

        // Generate sample projects
        var random = new Random();
        var statuses = new[] { "On Track", "At Risk", "Critical" };
        var managers = new[] { "Alice Johnson", "Bob Smith", "Carol Williams", "David Brown" };

        for (int i = 1; i <= 30; i++)
        {
            var budget = random.Next(50000, 500000);
            var spent = random.Next(budget - 50000, budget + 100000);
            var progress = random.NextDouble();

            _projects.Add(new FeatureRecord
            {
                Id = $"PRJ{i:D3}",
                Properties = new Dictionary<string, object?>
                {
                    ["ProjectID"] = $"PRJ{i:D3}",
                    ["ProjectName"] = $"Project {i}: {GetRandomProjectName(random)}",
                    ["Status"] = statuses[random.Next(statuses.Length)],
                    ["Progress"] = Math.Round(progress, 2),
                    ["Budget"] = budget,
                    ["Spent"] = spent,
                    ["DueDate"] = DateTime.Now.AddDays(random.Next(-30, 90)),
                    ["Manager"] = managers[random.Next(managers.Length)]
                }
            });
        }
    }

    private string GetRandomProjectName(Random random)
    {
        var names = new[] { "Website Redesign", "Mobile App", "Database Migration", "Cloud Infrastructure", "Security Audit" };
        return names[random.Next(names.Length)];
    }
}
```

---

## Example 8: Bulk Operations and Field Calculator

Perform bulk updates and calculations on multiple features.

```razor
@page "/examples/attribute-table-bulk"
@using Honua.MapSDK.Components.AttributeTable
@using Honua.MapSDK.Models

<PageTitle>Bulk Operations</PageTitle>

<MudContainer MaxWidth="MaxWidth.Large" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">Inventory Management</MudText>

    <MudPaper Class="pa-4 mb-4">
        <MudText Typo="Typo.h6" Class="mb-2">Bulk Operations</MudText>
        <MudStack Row="true" Spacing="2">
            <MudButton Variant="Variant.Filled"
                      Color="Color.Primary"
                      OnClick="@ApplyDiscountToSelected"
                      Disabled="@(_selectedCount == 0)">
                Apply 10% Discount (@_selectedCount)
            </MudButton>
            <MudButton Variant="Variant.Filled"
                      Color="Color.Secondary"
                      OnClick="@MarkSelectedAsInStock"
                      Disabled="@(_selectedCount == 0)">
                Mark In Stock
            </MudButton>
            <MudButton Variant="Variant.Filled"
                      Color="Color.Tertiary"
                      OnClick="@RecalculateTotals">
                Recalculate All Totals
            </MudButton>
        </MudStack>
    </MudPaper>

    <HonuaAttributeTable @ref="_table"
                        Features="@_inventory"
                        Title="Product Inventory"
                        AllowEdit="true"
                        SelectionMode="SelectionMode.Multiple"
                        OnRowsSelected="@OnItemsSelected"
                        OnRowsUpdated="@OnItemsUpdated"
                        PageSize="50"
                        Style="height: 600px;" />
</MudContainer>

@code {
    private HonuaAttributeTable? _table;
    private List<FeatureRecord> _inventory = new();
    private List<FeatureRecord> _selectedItems = new();
    private int _selectedCount = 0;

    protected override void OnInitialized()
    {
        // Generate sample inventory
        var random = new Random();
        var categories = new[] { "Electronics", "Furniture", "Clothing", "Books", "Toys" };

        for (int i = 1; i <= 100; i++)
        {
            var quantity = random.Next(0, 100);
            var price = random.Next(10, 500);
            var discount = random.NextDouble() * 0.3; // 0-30% discount

            _inventory.Add(new FeatureRecord
            {
                Id = $"SKU{i:D4}",
                Properties = new Dictionary<string, object?>
                {
                    ["SKU"] = $"SKU{i:D4}",
                    ["Name"] = $"Product {i}",
                    ["Category"] = categories[random.Next(categories.Length)],
                    ["Price"] = (double)price,
                    ["Discount"] = Math.Round(discount, 2),
                    ["Quantity"] = quantity,
                    ["InStock"] = quantity > 0,
                    ["TotalValue"] = Math.Round(price * (1 - discount) * quantity, 2)
                }
            });
        }
    }

    private void OnItemsSelected(List<FeatureRecord> selected)
    {
        _selectedItems = selected;
        _selectedCount = selected.Count;
        StateHasChanged();
    }

    private void OnItemsUpdated(List<FeatureRecord> updated)
    {
        // Recalculate total value when items are updated
        foreach (var item in updated)
        {
            RecalculateItemTotal(item);
        }
    }

    private void ApplyDiscountToSelected()
    {
        foreach (var item in _selectedItems)
        {
            var currentDiscount = Convert.ToDouble(item.Properties["Discount"]);
            item.Properties["Discount"] = Math.Min(currentDiscount + 0.10, 1.0); // Max 100% discount
            RecalculateItemTotal(item);
        }

        StateHasChanged();
        // Snackbar.Add($"Applied 10% discount to {_selectedCount} items", Severity.Success);
    }

    private void MarkSelectedAsInStock()
    {
        foreach (var item in _selectedItems)
        {
            if (Convert.ToInt32(item.Properties["Quantity"]) == 0)
            {
                item.Properties["Quantity"] = 10; // Add default quantity
            }
            item.Properties["InStock"] = true;
            RecalculateItemTotal(item);
        }

        StateHasChanged();
        // Snackbar.Add($"Marked {_selectedCount} items as in stock", Severity.Success);
    }

    private void RecalculateTotals()
    {
        foreach (var item in _inventory)
        {
            RecalculateItemTotal(item);
        }

        StateHasChanged();
        // Snackbar.Add("Recalculated all totals", Severity.Success);
    }

    private void RecalculateItemTotal(FeatureRecord item)
    {
        var price = Convert.ToDouble(item.Properties["Price"]);
        var discount = Convert.ToDouble(item.Properties["Discount"]);
        var quantity = Convert.ToInt32(item.Properties["Quantity"]);

        item.Properties["TotalValue"] = Math.Round(price * (1 - discount) * quantity, 2);
        item.Properties["InStock"] = quantity > 0;
    }
}
```

---

## Additional Resources

- [README.md](./README.md) - Complete documentation
- [API Reference](./README.md#api-reference)
- [Performance Tips](./README.md#performance-tips)

## Need Help?

For more examples or assistance, check the Honua.MapSDK documentation or open an issue in the repository.
