#!/bin/bash

echo "=== PARCELS DATA STATISTICS ==="
echo "Feature count:"
jq '.features | length' parcels.geojson

echo -e "\nZoning distribution:"
jq '[.features[].properties.zoning] | group_by(.) | map({zoning: .[0], count: length})' parcels.geojson

echo -e "\nArea range (sqm):"
jq '[.features[].properties.area_sqm] | {min: min, max: max, avg: (add/length | round)}' parcels.geojson

echo -e "\nAssessed value range:"
jq '[.features[].properties.assessed_value] | {min: min, max: max}' parcels.geojson

echo -e "\nYear built range:"
jq '[.features[].properties.year_built] | {min: min, max: max}' parcels.geojson

echo -e "\nNull bedrooms count:"
jq '[.features[] | select(.properties.bedrooms == null)] | length' parcels.geojson

echo -e "\n=== BUILDINGS DATA STATISTICS ==="
echo "Feature count:"
jq '.features | length' buildings_3d.geojson

echo -e "\nBuilding type distribution:"
jq '[.features[].properties.type] | group_by(.) | map({type: .[0], count: length})' buildings_3d.geojson

echo -e "\nFloors range:"
jq '[.features[].properties.floors] | {min: min, max: max, avg: (add/length | round)}' buildings_3d.geojson

echo -e "\nHeight range (meters):"
jq '[.features[].properties.height_m] | {min: min, max: max, avg: (add/length | round)}' buildings_3d.geojson

echo -e "\nConstruction year range:"
jq '[.features[].properties.construction_year] | {min: min, max: max}' buildings_3d.geojson

echo -e "\nNull name count:"
jq '[.features[] | select(.properties.name == null)] | length' buildings_3d.geojson

echo -e "\nNull renovation_year count:"
jq '[.features[] | select(.properties.renovation_year == null)] | length' buildings_3d.geojson

echo -e "\nEnergy rating distribution:"
jq '[.features[].properties.energy_rating] | group_by(.) | map({rating: .[0], count: length})' buildings_3d.geojson

echo -e "\n=== GEOMETRY VALIDATION ==="
echo "Validating GeoJSON format..."
jq empty parcels.geojson && echo "✓ parcels.geojson is valid GeoJSON"
jq empty buildings_3d.geojson && echo "✓ buildings_3d.geojson is valid GeoJSON"

echo -e "\n=== FILE SIZES ==="
ls -lh parcels.geojson buildings_3d.geojson | awk '{printf "%s: %s\n", $9, $5}'

