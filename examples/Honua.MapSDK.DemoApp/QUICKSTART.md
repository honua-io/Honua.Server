# Honua MapSDK Demo - Quick Start Guide

Get the demo running in under 5 minutes!

## Prerequisites

- .NET 9.0 SDK installed
- A modern web browser

## Run the Demo

### Option 1: Using dotnet CLI

```bash
# Navigate to the demo directory
cd examples/Honua.MapSDK.DemoApp

# Run the application
dotnet run
```

### Option 2: Using Visual Studio

1. Open `Honua.Server.sln` in Visual Studio
2. Set `Honua.MapSDK.DemoApp` as the startup project
3. Press F5 to run

### Option 3: Using VS Code

1. Open the `examples/Honua.MapSDK.DemoApp` folder in VS Code
2. Press F5 or run `dotnet run` in the terminal

## Access the Demo

Once running, open your browser to:
- **HTTPS**: `https://localhost:5001`
- **HTTP**: `http://localhost:5000`

The exact URL will be shown in the console output.

## Explore the Demos

### 1. Start at the Landing Page

The home page (`/`) provides an overview and links to all demos.

### 2. Try Each Demo Scenario

Click on any demo card to explore:

- **Property Dashboard** - Real estate analysis
- **Environmental Monitoring** - Sensor network
- **Vehicle Tracking** - Fleet management
- **Emergency Response** - Multi-layer infrastructure
- **Urban Planning** - City analysis
- **Component Showcase** - Feature reference

### 3. Interact with Components

**Try these interactions:**

1. **Map Clicks** - Click on features to view details
2. **Chart Filtering** - Click chart segments to filter data
3. **Grid Selection** - Click rows to highlight on map
4. **Search** - Use the search box to find specific items
5. **Export** - Download data as JSON, CSV, or GeoJSON
6. **Dark Mode** - Toggle dark mode with the moon/sun icon

## Key Features to Explore

### Auto-Sync in Action

1. Go to **Property Dashboard**
2. Click on a property parcel in the map
3. Watch the grid scroll to that property
4. Notice the charts update to show related data

### Click-to-Filter

1. Go to **Environmental Monitoring**
2. Click on a bar in the "Air Quality by Sensor" chart
3. Watch all components filter to show only that sensor type
4. The map, grid, and other charts all update automatically!

### Multi-Layer Management

1. Go to **Emergency Response**
2. Use the sidebar to toggle different layers on/off
3. See how hospitals, fire stations, schools, and parks can be shown/hidden
4. Filter by facility type using the filter panel

## Common Tasks

### View Component Source Code

1. Navigate to `Pages/` directory
2. Open any `.razor` file to see the implementation
3. Each demo is self-contained and well-commented

### Modify Sample Data

1. Edit files in `wwwroot/data/`
2. All data is in GeoJSON format
3. Add or modify features to see them in the demo
4. Refresh the browser to see your changes

### Customize Styling

1. Edit `wwwroot/css/app.css`
2. Modify color schemes, layouts, or add new styles
3. The CSS includes extensive comments

## Understanding the Code

### Basic Pattern

Every demo follows this pattern:

```razor
<!-- 1. Map is the sync source -->
<HonuaMap Id="my-map" ... />

<!-- 2. Components sync with the map -->
<HonuaDataGrid SyncWith="my-map" ... />
<HonuaChart SyncWith="my-map" ... />
<HonuaFilterPanel SyncWith="my-map" ... />
```

### No Manual Wiring Required!

Components automatically communicate through ComponentBus. Just set:
- A unique `Id` on the map
- `SyncWith="[map-id]"` on other components

That's it! The SDK handles the rest.

## Next Steps

### Learn More

- Read the full [README.md](README.md) for detailed documentation
- Explore the **Component Showcase** demo for API reference
- Check out the [Honua.Server repository](https://github.com/honua-io/Honua.Server)

### Build Your Own

1. Copy a demo page as a starting point
2. Replace the data source with your own GeoJSON
3. Customize the components for your use case
4. Deploy to your preferred hosting platform

### Customize the Demo

- Change basemaps in map components
- Add new chart types
- Modify grid columns
- Add custom filters
- Integrate with your own data APIs

## Troubleshooting

### Port Already in Use

If you see an error about ports being in use:

```bash
dotnet run --urls "https://localhost:5011;http://localhost:5010"
```

### Missing Dependencies

If components don't load:

```bash
dotnet restore
dotnet clean
dotnet build
```

### Browser Compatibility

- Use a modern browser (Chrome, Firefox, Edge, Safari)
- Clear browser cache if you see outdated content
- Check browser console for any JavaScript errors

## Performance Tips

For the best experience:

1. **Use Chrome or Edge** for optimal WebAssembly performance
2. **Enable GPU acceleration** in your browser settings
3. **Close unused browser tabs** to free up memory
4. **Use pagination** when working with large datasets

## Getting Help

- **Component Showcase** - In-app reference and examples
- **README.md** - Comprehensive documentation
- **Source Code** - Well-commented implementation
- **GitHub Issues** - Report bugs or request features

## What's Next?

After exploring the demos, you might want to:

1. **Integrate with real data** - Replace sample GeoJSON with your APIs
2. **Add authentication** - Protect your dashboards
3. **Deploy to production** - Host on Azure, AWS, or any static host
4. **Extend components** - Add custom features and styling
5. **Build new scenarios** - Create dashboards for your specific needs

---

**Happy Mapping with Honua MapSDK!** 🗺️
