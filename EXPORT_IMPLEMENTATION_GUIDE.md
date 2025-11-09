# Data Export Implementation Guide

## Overview
Comprehensive data export functionality has been implemented for tables in the Honua Admin Blazor application. Users can now export table data to CSV, Excel (`.xlsx`), and PDF formats with support for filtering, column selection, and custom export options.

## Components Implemented

### 1. Core Services

#### DataExportService (`/src/Honua.Admin.Blazor/Shared/Services/DataExportService.cs`)
- **CSV Export**: Generates CSV files with proper escaping and UTF-8 encoding
- **Excel Export**: Creates formatted Excel workbooks using ClosedXML with:
  - Header styling (bold, gray background)
  - Alternating row colors for readability
  - Auto-fitted columns
  - Optional title row
- **PDF Export**: Generates professional PDF documents using QuestPDF with:
  - Landscape orientation for wide tables
  - Headers and footers with page numbers
  - Professional table formatting
  - Generated timestamp

#### ExportDialog (`/src/Honua.Admin.Blazor/Components/Shared/ExportDialog.razor`)
- Interactive dialog for custom export configuration
- Column selection with select all/deselect all functionality
- Export format selection (CSV, Excel, PDF)
- Filename customization
- Filter options (export all data vs. filtered data only)
- Visual count display (filtered vs. total rows)

### 2. JavaScript Support

#### file-download.js (`/src/Honua.Admin.Blazor/wwwroot/js/file-download.js`)
- Browser-based file download functionality
- Support for multiple data formats:
  - Base64-encoded data
  - Stream references
  - Byte arrays
- Automatic cleanup of blob URLs

### 3. Updated Components

#### ServiceList.razor
- **Quick Export Options**:
  - Export to CSV (all service columns)
  - Export to Excel (formatted workbook)
  - Export to PDF (professional document)
- **Custom Export**: Full control over columns and format
- **Columns Available**: Title, Service Type, Folder, Layer Count, Status
- **Filtering**: Respects current search/filter state

#### LayerList.razor
- **Quick Export Options**: CSV, Excel, PDF
- **Custom Export**: Column selection dialog
- **Columns Available**: Layer ID, Title, Service (conditional), Geometry Type, Last Updated
- **Dynamic Columns**: Service column only shown when viewing all layers (not service-specific)
- **Filtering**: Respects search and geometry type filters

### 4. NuGet Packages Added
```xml
<PackageReference Include="ClosedXML" Version="0.102.3" />
<PackageReference Include="QuestPDF" Version="2024.10.3" />
```

## Usage Instructions

### For End Users

#### Quick Export
1. Navigate to any table view (Services, Layers, Users, Maps, Audit Logs)
2. Click the "Export" button in the toolbar
3. Select your desired format:
   - **CSV**: Best for Excel import or data analysis
   - **Excel**: Best for formatted reports and sharing
   - **PDF**: Best for printing or archival

#### Custom Export
1. Click the "Export" button in the toolbar
2. Select "Custom Export..." from the menu
3. In the dialog:
   - Choose your export format
   - Customize the filename (without extension)
   - Select/deselect columns to include
   - Choose whether to export all data or only filtered rows
4. Click "Export" to download

### For Developers

#### Adding Export to a New Component

1. **Inject the service**:
```csharp
@inject DataExportService ExportService
@inject IDialogService DialogService
```

2. **Add the export menu to your toolbar**:
```razor
<MudMenu Icon="@Icons.Material.Filled.FileDownload"
         Label="Export"
         Variant="Variant.Outlined"
         Size="Size.Small"
         Disabled="@(!_filteredData.Any())">
    <MudMenuItem OnClick="@(() => ExportToCsv())">
        <MudStack Row="true" Spacing="2" AlignItems="AlignItems.Center">
            <MudIcon Icon="@Icons.Material.Filled.TableChart" Size="Size.Small" />
            <MudText>Export to CSV</MudText>
        </MudStack>
    </MudMenuItem>
    <!-- Add Excel and PDF menu items -->
    <MudDivider />
    <MudMenuItem OnClick="@ShowExportDialog">
        <MudStack Row="true" Spacing="2" AlignItems="AlignItems.Center">
            <MudIcon Icon="@Icons.Material.Filled.Settings" Size="Size.Small" />
            <MudText>Custom Export...</MudText>
        </MudStack>
    </MudMenuItem>
</MudMenu>
```

3. **Implement export methods**:
```csharp
private async Task ExportToCsv()
{
    try
    {
        var data = _filteredItems.ToList();
        var columns = new[] { "Column1", "Column2", "Column3" };

        await ExportService.ExportToCsvAsync(
            data,
            $"export_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            columns,
            item => new[]
            {
                item.Property1,
                item.Property2,
                item.Property3
            });

        Snackbar.Add("Exported to CSV successfully", Severity.Success);
    }
    catch (Exception ex)
    {
        Snackbar.Add($"Export failed: {ex.Message}", Severity.Error);
        Logger.LogError(ex, "Error exporting to CSV");
    }
}

private async Task ShowExportDialog()
{
    var columns = new List<ExportDialog.ExportColumnDefinition>
    {
        new() { FieldName = "Property1", DisplayName = "Column 1", IsSelected = true },
        new() { FieldName = "Property2", DisplayName = "Column 2", IsSelected = true }
    };

    var parameters = new DialogParameters
    {
        { "AvailableColumns", columns },
        { "TotalCount", _allItems.Count },
        { "FilteredCount", _filteredItems.Count() },
        { "DefaultFilename", $"export_{DateTime.Now:yyyyMMdd_HHmmss}" }
    };

    var dialog = await DialogService.ShowAsync<ExportDialog>("Export Data", parameters);
    var result = await dialog.Result;

    if (!result.Canceled && result.Data is ExportDialog.ExportConfiguration config)
    {
        // Handle export based on config.Format
    }
}
```

## Technical Details

### Data Flow
1. User clicks export button
2. Component gathers filtered/visible data
3. DataExportService generates file in requested format
4. File data converted to base64
5. JavaScript downloads file to browser
6. Temporary blob URL automatically cleaned up

### Performance Considerations
- Large datasets (>10,000 rows) may take several seconds to export
- PDF generation is slower than CSV/Excel due to layout calculations
- Consider adding progress indicators for large exports

### Security
- All exports happen client-side (no server uploads)
- Exports respect current user's data access permissions
- No temporary files created on server

## Future Enhancements (Not Implemented)

The following components could also benefit from export functionality:
- **UserManagement.razor**: Export user lists with roles and status
- **MapList.razor**: Export map configurations metadata
- **AuditLogViewer.razor**: Enhanced export with Excel and PDF (currently has basic CSV/JSON)

To add export to these components, follow the developer guide above using the ServiceList.razor and LayerList.razor implementations as reference.

## Troubleshooting

### Export button is disabled
- Ensure there is data to export (table is not empty)
- Check that the component has `@inject DataExportService ExportService`

### File doesn't download
- Check browser console for JavaScript errors
- Ensure `file-download.js` is loaded in App.razor
- Verify `downloadFileFromBase64` function exists

### Export fails with error
- Check Logger output for detailed error messages
- Verify NuGet packages are installed correctly
- Ensure QuestPDF license is configured (Community license is free)

## Dependencies
- **MudBlazor**: UI components and icons
- **ClosedXML**: Excel file generation
- **QuestPDF**: PDF document generation
- **IJSRuntime**: Browser file download
- **.NET 9.0**: Required framework version
