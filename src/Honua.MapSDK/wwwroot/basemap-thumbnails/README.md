# Basemap Thumbnails

This directory contains thumbnail preview images for basemap styles in the HonuaBasemapGallery component.

## Required Thumbnails

Create 200x150px PNG images for each basemap:

### Streets Category
- `osm-standard.png` - OpenStreetMap standard style
- `carto-positron.png` - Carto Positron light theme
- `carto-dark-matter.png` - Carto Dark Matter dark theme
- `osm-liberty.png` - OSM Liberty classic style

### Satellite Category
- `esri-world-imagery.png` - ESRI satellite imagery
- `mapbox-satellite.png` - Mapbox satellite
- `satellite-streets.png` - Satellite with street labels

### Terrain Category
- `opentopomap.png` - OpenTopoMap topographic
- `stamen-terrain.png` - Stamen terrain with hillshade
- `maptiler-outdoor.png` - MapTiler outdoor map
- `esri-world-terrain.png` - ESRI terrain

### Specialty Category
- `stamen-watercolor.png` - Watercolor artistic style
- `stamen-toner.png` - Black and white high contrast
- `blueprint.png` - Blueprint technical style
- `vintage.png` - Vintage/retro style

### Default Fallbacks
- `default-streets.png` - Generic streets thumbnail
- `default-satellite.png` - Generic satellite thumbnail
- `default-terrain.png` - Generic terrain thumbnail
- `default-specialty.png` - Generic specialty thumbnail

## Generating Thumbnails

You can generate thumbnails using:

1. **Static Map APIs**:
   - Mapbox Static Images API
   - Google Maps Static API
   - MapTiler Static Maps API

2. **Manual Screenshots**:
   - Load the basemap in a map viewer
   - Screenshot a representative area
   - Crop to 200x150px

3. **Automated Tools**:
   - Puppeteer/Playwright for automated screenshots
   - MapLibre Native for server-side rendering

## Example Generation Script

```javascript
// Using Puppeteer to generate thumbnails
const puppeteer = require('puppeteer');

async function generateThumbnail(styleUrl, outputPath) {
    const browser = await puppeteer.launch();
    const page = await browser.newPage();
    await page.setViewport({ width: 200, height: 150 });

    // Load map with style
    await page.goto(`https://your-map-viewer.com?style=${styleUrl}`);
    await page.waitForTimeout(2000); // Wait for tiles to load

    // Screenshot
    await page.screenshot({ path: outputPath });
    await browser.close();
}
```

## Image Requirements

- **Format**: PNG (supports transparency)
- **Dimensions**: 200x150px (4:3 aspect ratio)
- **File Size**: < 50KB recommended
- **Compression**: Use `pngquant` or similar for optimization
- **Content**: Center on a recognizable area (city, landmark)
- **Zoom Level**: 10-12 typically works best

## Optimization

```bash
# Optimize all PNGs in directory
pngquant --quality=65-80 --ext .png --force *.png

# Or use ImageMagick
mogrify -resize 200x150! -quality 85 *.png
```

## Attribution

Ensure thumbnail images comply with the basemap provider's terms of service. Most providers allow thumbnails for gallery/picker interfaces.
