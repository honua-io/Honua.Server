# HonuaPrint - Usage Examples

Practical examples showing how to integrate HonuaPrint into your Blazor applications.

## Table of Contents

1. [Basic Print Button](#basic-print-button)
2. [Property Report Dashboard](#property-report-dashboard)
3. [Automated Batch Printing](#automated-batch-printing)
4. [Custom Print Templates](#custom-print-templates)
5. [Print with Filters](#print-with-filters)
6. [Multi-Page Print Report](#multi-page-print-report)
7. [Print Queue System](#print-queue-system)

---

## Basic Print Button

Simple map with print functionality.

```razor
@page "/simple-map"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Print

<div class="map-container">
    <HonuaMap Id="mainMap"
              MapStyle="https://demotiles.maplibre.org/style.json"
              Center="@(new[] { -122.4194, 37.7749 })"
              Zoom="12" />

    <div class="map-controls">
        <HonuaPrint SyncWith="mainMap"
                    ButtonText="Export Map"
                    PrintServiceUrl="http://localhost:8080/print" />
    </div>
</div>

<style>
    .map-container {
        position: relative;
        width: 100%;
        height: 600px;
    }

    .map-controls {
        position: absolute;
        top: 20px;
        right: 20px;
        z-index: 1000;
    }
</style>
```

---

## Property Report Dashboard

Real estate dashboard with customized print reports.

```razor
@page "/property-report"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Print
@using Honua.MapSDK.Models
@inject IMapFishPrintService PrintService

<div class="property-dashboard">
    <!-- Property Details -->
    <div class="property-info">
        <h2>@_selectedProperty.Address</h2>
        <p><strong>Parcel ID:</strong> @_selectedProperty.ParcelId</p>
        <p><strong>Owner:</strong> @_selectedProperty.Owner</p>
        <p><strong>Assessed Value:</strong> @_selectedProperty.Value.ToString("C")</p>
    </div>

    <!-- Map View -->
    <div class="map-section">
        <HonuaMap Id="propertyMap"
                  MapStyle="https://api.maptiler.com/maps/streets/style.json"
                  Center="@_propertyCenter"
                  Zoom="16" />
    </div>

    <!-- Action Buttons -->
    <div class="action-buttons">
        <MudButton Variant="Variant.Filled"
                   Color="Color.Primary"
                   OnClick="GenerateReport">
            Generate Property Report
        </MudButton>

        <HonuaPrint SyncWith="propertyMap"
                    ButtonText="Export Map Only"
                    OnPrintComplete="HandlePrintComplete"
                    OnPrintError="HandlePrintError" />
    </div>
</div>

@code {
    private PropertyInfo _selectedProperty = new()
    {
        Address = "123 Main Street",
        ParcelId = "12-345-678",
        Owner = "John Doe",
        Value = 525000
    };

    private double[] _propertyCenter = new[] { -122.4194, 37.7749 };

    private async Task GenerateReport()
    {
        var config = new PrintConfiguration
        {
            Title = $"Property Report - {_selectedProperty.Address}",
            Description = $"Parcel ID: {_selectedProperty.ParcelId}",
            Author = "Property Management System",
            Copyright = "© 2024 My Company",
            Layout = "property-report",
            PaperSize = PaperSize.Letter,
            Orientation = PageOrientation.Portrait,
            Format = PrintFormat.Pdf,
            Dpi = 300,
            Scale = 1000,
            Center = _propertyCenter,
            Zoom = 16,
            IncludeLegend = true,
            IncludeScaleBar = true,
            IncludeNorthArrow = true,
            Attributes = new Dictionary<string, object>
            {
                ["parcel_id"] = _selectedProperty.ParcelId,
                ["owner"] = _selectedProperty.Owner,
                ["value"] = _selectedProperty.Value.ToString("C"),
                ["report_date"] = DateTime.Today.ToString("MMMM dd, yyyy")
            }
        };

        var jobId = await PrintService.SubmitPrintJobAsync(config);

        if (jobId != null)
        {
            await MonitorPrintJob(jobId);
        }
    }

    private async Task MonitorPrintJob(string jobId)
    {
        PrintJobStatus? status;
        do
        {
            await Task.Delay(2000);
            status = await PrintService.GetJobStatusAsync(jobId);
            StateHasChanged();
        } while (status?.Status == PrintJobState.Processing);

        if (status?.Status == PrintJobState.Completed)
        {
            var data = await PrintService.DownloadPrintAsync(jobId);
            // Download handled automatically
        }
    }

    private void HandlePrintComplete(PrintJobStatus status)
    {
        Console.WriteLine("Print completed successfully!");
    }

    private void HandlePrintError(string error)
    {
        Console.WriteLine($"Print failed: {error}");
    }

    public class PropertyInfo
    {
        public string Address { get; set; } = "";
        public string ParcelId { get; set; } = "";
        public string Owner { get; set; } = "";
        public decimal Value { get; set; }
    }
}
```

---

## Automated Batch Printing

Print multiple maps automatically for a report series.

```razor
@page "/batch-print"
@using Honua.MapSDK.Models
@inject IMapFishPrintService PrintService

<div class="batch-print-page">
    <h1>Batch Print Reports</h1>

    <MudButton Variant="Variant.Filled"
               Color="Color.Primary"
               OnClick="StartBatchPrint"
               Disabled="_isProcessing">
        @if (_isProcessing)
        {
            <MudProgressCircular Size="Size.Small" Indeterminate="true" Class="mr-2" />
            <span>Processing (@_currentIndex/@_totalMaps)...</span>
        }
        else
        {
            <span>Print All District Maps</span>
        }
    </MudButton>

    @if (_results.Any())
    {
        <MudTable Items="_results" Class="mt-4">
            <HeaderContent>
                <MudTh>District</MudTh>
                <MudTh>Status</MudTh>
                <MudTh>Job ID</MudTh>
                <MudTh>Action</MudTh>
            </HeaderContent>
            <RowTemplate>
                <MudTd>@context.DistrictName</MudTd>
                <MudTd>
                    <MudChip Color="@GetStatusColor(context.Status)">
                        @context.Status
                    </MudChip>
                </MudTd>
                <MudTd>@context.JobId</MudTd>
                <MudTd>
                    @if (context.Status == PrintJobState.Completed)
                    {
                        <MudButton Size="Size.Small"
                                   OnClick="@(() => DownloadPrint(context.JobId))">
                            Download
                        </MudButton>
                    }
                </MudTd>
            </RowTemplate>
        </MudTable>
    }
</div>

@code {
    private bool _isProcessing;
    private int _currentIndex;
    private int _totalMaps;
    private List<BatchPrintResult> _results = new();

    private readonly List<DistrictMap> _districts = new()
    {
        new() { Name = "District 1 - Downtown", Center = new[] { -122.41, 37.77 }, Zoom = 13 },
        new() { Name = "District 2 - Mission", Center = new[] { -122.42, 37.76 }, Zoom = 13 },
        new() { Name = "District 3 - Richmond", Center = new[] { -122.48, 37.78 }, Zoom = 13 },
        new() { Name = "District 4 - Sunset", Center = new[] { -122.49, 37.75 }, Zoom = 13 },
        new() { Name = "District 5 - Haight", Center = new[] { -122.44, 37.77 }, Zoom = 13 }
    };

    private async Task StartBatchPrint()
    {
        _isProcessing = true;
        _results.Clear();
        _totalMaps = _districts.Count;
        _currentIndex = 0;

        foreach (var district in _districts)
        {
            _currentIndex++;
            StateHasChanged();

            var config = new PrintConfiguration
            {
                Title = district.Name,
                Description = $"District boundary and features map",
                Author = "Planning Department",
                PaperSize = PaperSize.Letter,
                Orientation = PageOrientation.Landscape,
                Format = PrintFormat.Pdf,
                Dpi = 300,
                Scale = 25000,
                Center = district.Center,
                Zoom = district.Zoom,
                IncludeLegend = true,
                IncludeScaleBar = true,
                IncludeNorthArrow = true
            };

            try
            {
                var jobId = await PrintService.SubmitPrintJobAsync(config);

                if (jobId != null)
                {
                    // Wait for completion
                    var status = await WaitForJobCompletion(jobId);

                    _results.Add(new BatchPrintResult
                    {
                        DistrictName = district.Name,
                        JobId = jobId,
                        Status = status?.Status ?? PrintJobState.Failed
                    });
                }
            }
            catch (Exception ex)
            {
                _results.Add(new BatchPrintResult
                {
                    DistrictName = district.Name,
                    JobId = "N/A",
                    Status = PrintJobState.Failed
                });
            }

            StateHasChanged();
        }

        _isProcessing = false;
    }

    private async Task<PrintJobStatus?> WaitForJobCompletion(string jobId)
    {
        PrintJobStatus? status;
        int maxAttempts = 60; // 2 minutes max
        int attempts = 0;

        do
        {
            await Task.Delay(2000);
            status = await PrintService.GetJobStatusAsync(jobId);
            attempts++;
        } while (status?.Status == PrintJobState.Processing && attempts < maxAttempts);

        return status;
    }

    private async Task DownloadPrint(string jobId)
    {
        var data = await PrintService.DownloadPrintAsync(jobId);
        // Download handled automatically
    }

    private Color GetStatusColor(PrintJobState status) => status switch
    {
        PrintJobState.Completed => Color.Success,
        PrintJobState.Failed => Color.Error,
        PrintJobState.Processing => Color.Info,
        _ => Color.Default
    };

    public class DistrictMap
    {
        public string Name { get; set; } = "";
        public double[] Center { get; set; } = Array.Empty<double>();
        public double Zoom { get; set; }
    }

    public class BatchPrintResult
    {
        public string DistrictName { get; set; } = "";
        public string JobId { get; set; } = "";
        public PrintJobState Status { get; set; }
    }
}
```

---

## Custom Print Templates

Using custom MapFish Print templates with additional fields.

### MapFish Print Config

```yaml
# config.yaml
apps:
  - name: infrastructure
    layouts:
      - name: infrastructure-report
        label: "Infrastructure Report"
        map:
          width: 1000
          height: 700
          maxDPI: 300
        attributes:
          - name: title
            type: String
          - name: project_name
            type: String
          - name: project_manager
            type: String
          - name: report_date
            type: Date
          - name: status
            type: String
          - name: budget
            type: String
          - name: notes
            type: String
          - name: logo
            type: DataSourceAttribute
```

### Blazor Component

```razor
@page "/infrastructure-report"
@using Honua.MapSDK.Components.Print
@using Honua.MapSDK.Models
@inject IMapFishPrintService PrintService

<div class="infrastructure-report">
    <h1>Infrastructure Project Report</h1>

    <MudGrid>
        <MudItem xs="12" md="6">
            <MudTextField @bind-Value="_projectName"
                          Label="Project Name"
                          Variant="Variant.Outlined" />
        </MudItem>
        <MudItem xs="12" md="6">
            <MudTextField @bind-Value="_projectManager"
                          Label="Project Manager"
                          Variant="Variant.Outlined" />
        </MudItem>
        <MudItem xs="12" md="6">
            <MudSelect @bind-Value="_status"
                       Label="Status"
                       Variant="Variant.Outlined">
                <MudSelectItem Value="@("Planning")">Planning</MudSelectItem>
                <MudSelectItem Value="@("In Progress")">In Progress</MudSelectItem>
                <MudSelectItem Value="@("Completed")">Completed</MudSelectItem>
            </MudSelect>
        </MudItem>
        <MudItem xs="12" md="6">
            <MudTextField @bind-Value="_budget"
                          Label="Budget"
                          Variant="Variant.Outlined" />
        </MudItem>
        <MudItem xs="12">
            <MudTextField @bind-Value="_notes"
                          Label="Notes"
                          Variant="Variant.Outlined"
                          Lines="3" />
        </MudItem>
    </MudGrid>

    <div class="map-container mt-4">
        <HonuaMap Id="infraMap"
                  MapStyle="https://api.maptiler.com/maps/streets/style.json"
                  Center="@_mapCenter"
                  Zoom="14" />
    </div>

    <div class="action-buttons mt-4">
        <MudButton Variant="Variant.Filled"
                   Color="Color.Primary"
                   OnClick="GenerateCustomReport">
            Generate Infrastructure Report
        </MudButton>
    </div>
</div>

@code {
    private string _projectName = "Highway 101 Expansion";
    private string _projectManager = "Jane Smith";
    private string _status = "In Progress";
    private string _budget = "$2.5M";
    private string _notes = "Phase 1 completion expected Q3 2024";
    private double[] _mapCenter = new[] { -122.4194, 37.7749 };

    private async Task GenerateCustomReport()
    {
        var config = new PrintConfiguration
        {
            Layout = "infrastructure-report",
            PaperSize = PaperSize.Letter,
            Orientation = PageOrientation.Portrait,
            Format = PrintFormat.Pdf,
            Dpi = 300,
            Center = _mapCenter,
            Zoom = 14,
            Attributes = new Dictionary<string, object>
            {
                ["title"] = "Infrastructure Project Report",
                ["project_name"] = _projectName,
                ["project_manager"] = _projectManager,
                ["report_date"] = DateTime.Today.ToString("MMMM dd, yyyy"),
                ["status"] = _status,
                ["budget"] = _budget,
                ["notes"] = _notes,
                ["logo"] = "data:image/png;base64,..." // Company logo as base64
            }
        };

        var jobId = await PrintService.SubmitPrintJobAsync(config);

        if (jobId != null)
        {
            await MonitorAndDownload(jobId);
        }
    }

    private async Task MonitorAndDownload(string jobId)
    {
        PrintJobStatus? status;
        do
        {
            await Task.Delay(2000);
            status = await PrintService.GetJobStatusAsync(jobId);
        } while (status?.Status == PrintJobState.Processing);

        if (status?.Status == PrintJobState.Completed)
        {
            await PrintService.DownloadPrintAsync(jobId);
        }
    }
}
```

---

## Print with Filters

Print map with applied spatial and attribute filters.

```razor
@page "/filtered-print"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.FilterPanel
@using Honua.MapSDK.Components.Print
@using Honua.MapSDK.Models

<div class="filtered-map-page">
    <div class="sidebar">
        <h3>Filters</h3>

        <HonuaFilterPanel SyncWith="dataMap"
                          ShowSpatialFilters="true"
                          ShowAttributeFilters="true"
                          ShowTemporalFilters="true" />

        <div class="print-section mt-4">
            <h4>Print Filtered Results</h4>
            <MudButton Variant="Variant.Filled"
                       Color="Color.Primary"
                       FullWidth="true"
                       OnClick="PrintFilteredMap">
                Export Filtered Map
            </MudButton>
        </div>
    </div>

    <div class="map-main">
        <HonuaMap Id="dataMap"
                  MapStyle="https://demotiles.maplibre.org/style.json"
                  Center="@(new[] { -122.4, 37.7 })"
                  Zoom="11" />
    </div>
</div>

@code {
    private async Task PrintFilteredMap()
    {
        // Get current filter state from ComponentBus
        var filters = await GetActiveFilters();

        var config = new PrintConfiguration
        {
            Title = "Filtered Map Export",
            Description = GetFilterDescription(filters),
            Author = "Data Analysis Team",
            PaperSize = PaperSize.A4,
            Orientation = PageOrientation.Landscape,
            Format = PrintFormat.Pdf,
            Dpi = 300,
            IncludeLegend = true,
            Attributes = new Dictionary<string, object>
            {
                ["filter_summary"] = GetFilterSummary(filters),
                ["record_count"] = filters.RecordCount,
                ["generated_date"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
            }
        };

        // Print via service
        var jobId = await PrintService.SubmitPrintJobAsync(config);
        // ... handle completion
    }

    private async Task<FilterState> GetActiveFilters()
    {
        // Implementation would retrieve current filter state from ComponentBus
        return new FilterState
        {
            SpatialFilter = "Within City Limits",
            AttributeFilters = new[] { "Category = Residential", "Value > $500,000" },
            TemporalFilter = "Last 30 days",
            RecordCount = 1247
        };
    }

    private string GetFilterDescription(FilterState filters)
    {
        return $"Spatial: {filters.SpatialFilter} | " +
               $"Attributes: {string.Join(", ", filters.AttributeFilters)} | " +
               $"Temporal: {filters.TemporalFilter}";
    }

    private string GetFilterSummary(FilterState filters)
    {
        return $"{filters.RecordCount} records matching filter criteria";
    }

    public class FilterState
    {
        public string SpatialFilter { get; set; } = "";
        public string[] AttributeFilters { get; set; } = Array.Empty<string>();
        public string TemporalFilter { get; set; } = "";
        public int RecordCount { get; set; }
    }
}
```

---

## Multi-Page Print Report

Generate a multi-page PDF report with cover page, maps, and data tables.

```csharp
public class ReportGenerator
{
    private readonly IMapFishPrintService _printService;

    public ReportGenerator(IMapFishPrintService printService)
    {
        _printService = printService;
    }

    public async Task<byte[]> GenerateMultiPageReport(string projectId)
    {
        var jobIds = new List<string>();

        // Page 1: Cover page
        var coverConfig = new PrintConfiguration
        {
            Layout = "cover-page",
            Title = "Annual Environmental Report",
            Attributes = new Dictionary<string, object>
            {
                ["subtitle"] = "Water Quality Analysis 2024",
                ["prepared_by"] = "Environmental Services",
                ["date"] = DateTime.Today.ToString("MMMM yyyy")
            }
        };
        jobIds.Add(await _printService.SubmitPrintJobAsync(coverConfig));

        // Page 2: Overview map
        var overviewConfig = new PrintConfiguration
        {
            Layout = "full-page-map",
            Title = "Study Area Overview",
            Center = new[] { -122.4, 37.7 },
            Zoom = 10,
            Scale = 100000
        };
        jobIds.Add(await _printService.SubmitPrintJobAsync(overviewConfig));

        // Page 3: Detail map 1
        var detail1Config = new PrintConfiguration
        {
            Layout = "detail-map",
            Title = "Northern Watershed",
            Center = new[] { -122.42, 37.78 },
            Zoom = 13,
            Scale = 25000
        };
        jobIds.Add(await _printService.SubmitPrintJobAsync(detail1Config));

        // Page 4: Detail map 2
        var detail2Config = new PrintConfiguration
        {
            Layout = "detail-map",
            Title = "Southern Watershed",
            Center = new[] { -122.38, 37.72 },
            Zoom = 13,
            Scale = 25000
        };
        jobIds.Add(await _printService.SubmitPrintJobAsync(detail2Config));

        // Wait for all jobs to complete
        await Task.WhenAll(jobIds.Select(WaitForJobCompletion));

        // Download and merge PDFs
        var pages = new List<byte[]>();
        foreach (var jobId in jobIds)
        {
            var data = await _printService.DownloadPrintAsync(jobId);
            if (data != null)
            {
                pages.Add(data);
            }
        }

        // Merge PDFs (would use a PDF library like iTextSharp)
        return MergePdfs(pages);
    }

    private async Task WaitForJobCompletion(string jobId)
    {
        PrintJobStatus? status;
        do
        {
            await Task.Delay(2000);
            status = await _printService.GetJobStatusAsync(jobId);
        } while (status?.Status == PrintJobState.Processing);
    }

    private byte[] MergePdfs(List<byte[]> pages)
    {
        // Implementation would use iTextSharp or similar
        // to merge multiple PDFs into one document
        throw new NotImplementedException("Implement with PDF library");
    }
}
```

---

## Print Queue System

Enterprise print queue with job management and notifications.

```csharp
public class PrintQueueService
{
    private readonly IMapFishPrintService _printService;
    private readonly Queue<PrintQueueItem> _queue = new();
    private readonly SemaphoreSlim _semaphore = new(3); // Max 3 concurrent prints
    private bool _isProcessing;

    public event EventHandler<PrintQueueItem>? JobCompleted;
    public event EventHandler<PrintQueueItem>? JobFailed;

    public PrintQueueService(IMapFishPrintService printService)
    {
        _printService = printService;
    }

    public void EnqueuePrint(PrintConfiguration config, string userId, string description)
    {
        var item = new PrintQueueItem
        {
            Id = Guid.NewGuid().ToString(),
            Config = config,
            UserId = userId,
            Description = description,
            QueuedAt = DateTime.UtcNow,
            Status = PrintJobState.Pending
        };

        _queue.Enqueue(item);

        if (!_isProcessing)
        {
            _ = ProcessQueue();
        }
    }

    private async Task ProcessQueue()
    {
        _isProcessing = true;

        while (_queue.TryDequeue(out var item))
        {
            await _semaphore.WaitAsync();

            _ = Task.Run(async () =>
            {
                try
                {
                    item.Status = PrintJobState.Processing;
                    item.StartedAt = DateTime.UtcNow;

                    var jobId = await _printService.SubmitPrintJobAsync(item.Config);

                    if (jobId != null)
                    {
                        item.JobId = jobId;
                        await MonitorJob(item);
                    }
                    else
                    {
                        item.Status = PrintJobState.Failed;
                        item.Error = "Failed to submit print job";
                        JobFailed?.Invoke(this, item);
                    }
                }
                catch (Exception ex)
                {
                    item.Status = PrintJobState.Failed;
                    item.Error = ex.Message;
                    JobFailed?.Invoke(this, item);
                }
                finally
                {
                    _semaphore.Release();
                }
            });
        }

        _isProcessing = false;
    }

    private async Task MonitorJob(PrintQueueItem item)
    {
        PrintJobStatus? status;
        do
        {
            await Task.Delay(2000);
            status = await _printService.GetJobStatusAsync(item.JobId!);
        } while (status?.Status == PrintJobState.Processing);

        if (status?.Status == PrintJobState.Completed)
        {
            item.Status = PrintJobState.Completed;
            item.CompletedAt = DateTime.UtcNow;
            item.DownloadUrl = status.DownloadUrl;
            JobCompleted?.Invoke(this, item);
        }
        else
        {
            item.Status = PrintJobState.Failed;
            item.Error = status?.Error ?? "Print failed";
            JobFailed?.Invoke(this, item);
        }
    }
}

public class PrintQueueItem
{
    public string Id { get; set; } = "";
    public PrintConfiguration Config { get; set; } = new();
    public string UserId { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime QueuedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public PrintJobState Status { get; set; }
    public string? JobId { get; set; }
    public string? DownloadUrl { get; set; }
    public string? Error { get; set; }
}
```

---

## More Examples

For additional examples, see:
- [HonuaMap Examples](../Map/Examples.md)
- [HonuaBookmarks Examples](../Bookmarks/Examples.md)
- [ComponentBus Patterns](../../docs/ComponentBus.md)
