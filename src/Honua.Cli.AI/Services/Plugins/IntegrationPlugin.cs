// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Honua.Cli.AI.Serialization;


namespace Honua.Cli.AI.Services.Plugins;

/// <summary>
/// Semantic Kernel plugin for third-party integrations.
/// </summary>
public sealed class IntegrationPlugin
{
    [KernelFunction, Description("Generates QGIS connection configuration")]
    public string GenerateQgisConnection(
        [Description("Server URL")] string serverUrl = "http://localhost:5000",
        [Description("Authentication info as JSON")] string authInfo = "{}")
    {
        return JsonSerializer.Serialize(new
        {
            serverUrl,
            qgisConnection = new
            {
                method = "Add WFS 3.0 / OGC API Features connection",
                steps = new[]
                {
                    "1. Layer > Add Layer > Add WFS Layer",
                    "2. Click 'New' to create connection",
                    $"3. Name: Honua Server",
                    $"4. URL: {serverUrl}",
                    "5. Version: OGC API - Features",
                    "6. Authentication: Basic/OAuth (if required)",
                    "7. Click OK",
                    "8. Connect and select layers"
                },
                connectionXML = $@"
<qgsWFSConnection>
  <uri url=""{serverUrl}"" />
  <version>OGC API - Features</version>
  <maxnumfeatures>1000</maxnumfeatures>
  <ignoreAxisOrientation>false</ignoreAxisOrientation>
  <invertAxisOrientation>false</invertAxisOrientation>
</qgsWFSConnection>",
                pythonScript = $@"
from qgis.core import QgsVectorLayer, QgsProject

# Add OGC API Features layer
uri = 'url={serverUrl}/collections/buildings/items'
layer = QgsVectorLayer(uri, 'Buildings', 'WFS')

if layer.isValid():
    QgsProject.instance().addMapLayer(layer)
else:
    print('Layer failed to load!')"
            },
            styling = new
            {
                qmlStyle = "Export styles as QML and share with team",
                sld = "Use SLD for interoperable styling across clients"
            }
        }, CliJsonOptions.Indented);
    }

    [KernelFunction, Description("Configures ArcGIS Pro connection")]
    public string ConfigureArcGisProConnection(
        [Description("Server URL")] string serverUrl = "http://localhost:5000")
    {
        return JsonSerializer.Serialize(new
        {
            serverUrl,
            arcgisProConnection = new
            {
                method = "Add OGC API Features service",
                steps = new[]
                {
                    "1. Insert > Connections > New OGC API",
                    $"2. Server URL: {serverUrl}",
                    "3. Authentication: Select appropriate method",
                    "4. Browse and add collections",
                    "5. Add to map"
                },
                pythonAPI = $@"
import arcpy

# Add OGC API Features service
ogc_url = '{serverUrl}'
aprx = arcpy.mp.ArcGISProject('CURRENT')
map = aprx.listMaps()[0]

# Add WFS layer (OGC API Features)
wfs_layer = map.addDataFromPath(ogc_url)
print(f'Added layer: {{wfs_layer.name}}')"
            },
            limitations = new[]
            {
                "ArcGIS Pro 2.6+ required for OGC API Features support",
                "Some advanced OGC features may not be fully supported",
                "Check Esri documentation for version-specific capabilities"
            }
        }, CliJsonOptions.Indented);
    }

    [KernelFunction, Description("Suggests web map library based on requirements")]
    public string SuggestWebMapLibrary(
        [Description("Requirements as JSON (features, browser support, license)")] string requirements = "{\"features\":[\"basemaps\",\"overlays\"],\"browserSupport\":\"modern\",\"license\":\"open-source\"}")
    {
        return JsonSerializer.Serialize(new
        {
            libraries = new[]
            {
                new
                {
                    name = "OpenLayers",
                    version = "8.x",
                    license = "BSD 2-Clause",
                    strengths = new[] { "OGC standards support", "Vector tiles", "Advanced projections", "Extensible" },
                    weaknesses = new[] { "Steeper learning curve", "Larger bundle size" },
                    bestFor = "OGC API compliance, complex GIS applications, custom projections",
                    note = (string?)null,
                    ogcApiExample = (string?)@"
import Map from 'ol/Map';
import View from 'ol/View';
import VectorLayer from 'ol/layer/Vector';
import {ogcFeatures} from 'ol/format';
import VectorSource from 'ol/source/Vector';

const map = new Map({
  target: 'map',
  layers: [
    new VectorLayer({
      source: new VectorSource({
        format: new ogcFeatures(),
        url: 'https://api.example.com/collections/buildings/items'
      })
    })
  ],
  view: new View({center: [0, 0], zoom: 2})
});"
                },
                new
                {
                    name = "Leaflet",
                    version = "1.9.x",
                    license = "BSD 2-Clause",
                    strengths = new[] { "Lightweight", "Easy to learn", "Large plugin ecosystem", "Mobile-friendly" },
                    weaknesses = new[] { "Limited OGC support (needs plugins)", "Basic vector tile support" },
                    bestFor = "Simple web maps, mobile apps, quick prototypes",
                    note = (string?)null,
                    ogcApiExample = (string?)@"
import L from 'leaflet';

const map = L.map('map').setView([37.7, -122.4], 12);

// Use GeoJSON from OGC API
fetch('https://api.example.com/collections/buildings/items?bbox=-122.5,37.7,-122.3,37.9')
  .then(res => res.json())
  .then(data => {
    L.geoJSON(data).addTo(map);
  });"
                },
                new
                {
                    name = "Mapbox GL JS",
                    version = "3.x",
                    license = "Proprietary (requires Mapbox account)",
                    strengths = new[] { "Beautiful rendering", "Vector tiles", "3D support", "Performance" },
                    weaknesses = new[] { "Requires Mapbox account", "Proprietary", "Limited OGC support" },
                    bestFor = "Modern web apps, vector tiles, stunning visualizations",
                    note = (string?)null,
                    ogcApiExample = (string?)@"
import mapboxgl from 'mapbox-gl';

mapboxgl.accessToken = 'YOUR_MAPBOX_TOKEN';
const map = new mapboxgl.Map({
  container: 'map',
  style: 'mapbox://styles/mapbox/streets-v11',
  center: [-122.4, 37.7],
  zoom: 12
});

map.on('load', () => {
  map.addSource('buildings', {
    type: 'geojson',
    data: 'https://api.example.com/collections/buildings/items?bbox=-122.5,37.7,-122.3,37.9'
  });
  map.addLayer({
    id: 'buildings-layer',
    type: 'fill',
    source: 'buildings',
    paint: {'fill-color': '#088', 'fill-opacity': 0.5}
  });
});"
                },
                new
                {
                    name = "MapLibre GL JS",
                    version = "3.x",
                    license = "BSD 3-Clause (Open Source)",
                    strengths = new[] { "Fork of Mapbox GL JS", "No vendor lock-in", "Vector tiles", "Active community" },
                    weaknesses = new[] { "Newer project", "Some Mapbox features missing" },
                    bestFor = "Modern web maps without Mapbox dependency, vector tiles, open source projects",
                    note = (string?)"Drop-in replacement for Mapbox GL JS with open source license",
                    ogcApiExample = (string?)null
                }
            },
            comparisonMatrix = new
            {
                headers = new[] { "Library", "OGC Support", "Vector Tiles", "3D", "Bundle Size", "Learning Curve" },
                rows = new[]
                {
                    new { library = "OpenLayers", ogc = "Excellent", vectorTiles = "Yes", threeD = "Limited", size = "~400KB", learning = "Medium" },
                    new { library = "Leaflet", ogc = "Plugin needed", vectorTiles = "Plugin", threeD = "No", size = "~150KB", learning = "Easy" },
                    new { library = "Mapbox GL", ogc = "Limited", vectorTiles = "Excellent", threeD = "Yes", size = "~250KB", learning = "Medium" },
                    new { library = "MapLibre GL", ogc = "Limited", vectorTiles = "Excellent", threeD = "Yes", size = "~250KB", learning = "Medium" }
                }
            }
        }, CliJsonOptions.Indented);
    }

    [KernelFunction, Description("Generates JavaScript map client code")]
    public string GenerateMapClientCode(
        [Description("Endpoint URL")] string endpoint,
        [Description("Library: openlayers, leaflet, or mapbox")] string library)
    {
        var code = library.ToLowerInvariant() switch
        {
            "openlayers" => $@"
import Map from 'ol/Map';
import View from 'ol/View';
import {{Tile as TileLayer, Vector as VectorLayer}} from 'ol/layer';
import {{OSM, Vector as VectorSource}} from 'ol/source';
import GeoJSON from 'ol/format/GeoJSON';

// Create map
const map = new Map({{
  target: 'map',
  layers: [
    new TileLayer({{source: new OSM()}}),
    new VectorLayer({{
      source: new VectorSource({{
        format: new GeoJSON(),
        url: '{endpoint}/collections/buildings/items?bbox=' +
             map.getView().calculateExtent(map.getSize()).join(',')
      }}),
      style: feature => {{
        return new Style({{
          fill: new Fill({{color: 'rgba(255, 0, 0, 0.2)'}}),
          stroke: new Stroke({{color: '#ff0000', width: 2}})
        }});
      }}
    }})
  ],
  view: new View({{center: [0, 0], zoom: 2}})
}});

// Reload on map move
map.on('moveend', () => {{
  const extent = map.getView().calculateExtent(map.getSize());
  const bbox = extent.join(',');
  vectorLayer.getSource().setUrl(`{endpoint}/collections/buildings/items?bbox=${{bbox}}`);
}});",

            "leaflet" => $@"
import L from 'leaflet';

const map = L.map('map').setView([37.7, -122.4], 12);

// Base layer
L.tileLayer('https://{{s}}.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png', {{
  attribution: '&copy; OpenStreetMap contributors'
}}).addTo(map);

// OGC API Features layer
let currentLayer;

async function loadFeatures() {{
  const bounds = map.getBounds();
  const bbox = [
    bounds.getWest(), bounds.getSouth(),
    bounds.getEast(), bounds.getNorth()
  ].join(',');

  const response = await fetch(
    `{endpoint}/collections/buildings/items?bbox=${{bbox}}&limit=1000`
  );
  const geojson = await response.json();

  if (currentLayer) map.removeLayer(currentLayer);

  currentLayer = L.geoJSON(geojson, {{
    style: {{ color: '#ff0000', weight: 2, fillOpacity: 0.2 }},
    onEachFeature: (feature, layer) => {{
      layer.bindPopup(`<b>${{feature.properties.name}}</b>`);
    }}
  }}).addTo(map);
}}

map.on('moveend', loadFeatures);
loadFeatures();",

            _ => $@"
// Mapbox GL JS / MapLibre GL JS
import maplibregl from 'maplibre-gl';

const map = new maplibregl.Map({{
  container: 'map',
  style: {{
    version: 8,
    sources: {{
      osm: {{
        type: 'raster',
        tiles: ['https://a.tile.openstreetmap.org/{{z}}/{{x}}/{{y}}.png'],
        tileSize: 256
      }}
    }},
    layers: [{{id: 'osm', type: 'raster', source: 'osm'}}]
  }},
  center: [-122.4, 37.7],
  zoom: 12
}});

map.on('load', () => {{
  // Add OGC API Features source
  map.addSource('buildings', {{
    type: 'geojson',
    data: '{endpoint}/collections/buildings/items?limit=1000'
  }});

  // Add fill layer
  map.addLayer({{
    id: 'buildings-fill',
    type: 'fill',
    source: 'buildings',
    paint: {{
      'fill-color': '#088',
      'fill-opacity': 0.5
    }}
  }});

  // Add outline layer
  map.addLayer({{
    id: 'buildings-outline',
    type: 'line',
    source: 'buildings',
    paint: {{
      'line-color': '#000',
      'line-width': 2
    }}
  }});

  // Popups on click
  map.on('click', 'buildings-fill', (e) => {{
    new maplibregl.Popup()
      .setLngLat(e.lngLat)
      .setHTML(`<b>${{e.features[0].properties.name}}</b>`)
      .addTo(map);
  }});
}});"
        };

        return JsonSerializer.Serialize(new
        {
            library,
            endpoint,
            code,
            html = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <title>Honua Map</title>
    <meta name='viewport' content='width=device-width, initial-scale=1' />
    <link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/ol@8/ol.css' />
    <style>
        #map { position: absolute; top: 0; bottom: 0; width: 100%; }
    </style>
</head>
<body>
    <div id='map'></div>
    <script type='module' src='./app.js'></script>
</body>
</html>",
            packageJson = new
            {
                dependencies = (object)(library.ToLowerInvariant() switch
                {
                    "openlayers" => new { ol = "^8.0.0" },
                    "leaflet" => new { leaflet = "^1.9.0" },
                    _ => new { maplibregl = "^3.0.0" }
                })
            }
        }, CliJsonOptions.Indented);
    }

    [KernelFunction, Description("Configures GeoServer interoperability")]
    public string IntegrateWithGeoserver(
        [Description("Configuration options as JSON")] string configOptions)
    {
        return JsonSerializer.Serialize(new
        {
            integration = new
            {
                approach = "GeoServer as WMS/WFS, Honua as OGC API Features",
                benefits = new[] { "Leverage GeoServer for traditional OGC services", "Use Honua for modern OGC APIs", "Share PostGIS backend" },
                architecture = @"
┌─────────────┐
│  Clients    │
└──────┬──────┘
       │
   ┌───┴────┐
   │        │
┌──▼──┐  ┌─▼────────┐
│NGINX│  │          │
└──┬──┘  │          │
   │     │          │
┌──▼───────────┐ ┌──▼─────────┐
│  GeoServer   │ │   Honua    │
│  (WMS/WFS)   │ │ (OGC API)  │
└──────┬───────┘ └──────┬─────┘
       │                │
       └────────┬───────┘
                │
         ┌──────▼──────┐
         │  PostGIS    │
         └─────────────┘"
            },
            geoserverConfig = new
            {
                sharedDatastore = @"
<!-- GeoServer datastore.xml for shared PostGIS -->
<dataStore>
  <name>honua_postgis</name>
  <type>PostGIS</type>
  <connectionParameters>
    <host>postgis-server</host>
    <port>5432</port>
    <database>honua</database>
    <user>geoserver_user</user>
    <passwd>xxx</passwd>
    <schema>public</schema>
  </connectionParameters>
</dataStore>",
                layerConfig = "Publish same tables as WMS/WFS in GeoServer and OGC API in Honua",
                styling = "Use SLD in GeoServer, convert to client-side styling for Honua"
            },
            nginxRouting = @"
# NGINX routing configuration
upstream geoserver {
    server geoserver:8080;
}

upstream honua {
    server honua:5000;
}

server {
    listen 80;

    # Traditional OGC services to GeoServer
    location /geoserver {
        proxy_pass http://geoserver;
    }

    # WMS
    location /wms {
        proxy_pass http://geoserver/geoserver/wms;
    }

    # WFS
    location /wfs {
        proxy_pass http://geoserver/geoserver/wfs;
    }

    # Modern OGC API to Honua
    location / {
        proxy_pass http://honua;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }
}",
            crosswalking = new
            {
                wfsToOgcApi = new[]
                {
                    new { wfs = "GetCapabilities", ogcApi = "GET /collections", notes = "List layers vs collections" },
                    new { wfs = "DescribeFeatureType", ogcApi = "GET /collections/{id}/schema", notes = "Schema retrieval" },
                    new { wfs = "GetFeature", ogcApi = "GET /collections/{id}/items", notes = "Feature retrieval" }
                },
                wmsToOgcApiTiles = new[]
                {
                    new { wms = "GetMap", ogcApiTiles = "GET /tiles/{z}/{x}/{y}", notes = "Raster tiles" }
                }
            }
        }, CliJsonOptions.Indented);
    }
}
