// Honua Elevation Profile Module
// Handles elevation profile generation, charting, and visualization

import * as turf from 'https://cdn.skypack.dev/@turf/turf';

const maps = new Map();
const charts = new Map();
const profileData = new Map();
const markerLayers = new Map();

// Initialize elevation profile component
export function initializeElevationProfile(mapId, componentId, dotNetRef) {
    try {
        const mapContainer = document.getElementById(mapId);
        if (!mapContainer || !mapContainer._map) {
            console.error('Map not found:', mapId);
            return;
        }

        const map = mapContainer._map;
        maps.set(componentId, { map, dotNetRef, mapId });

        // Add profile path layer
        if (!map.getSource(`elevation-path-${componentId}`)) {
            map.addSource(`elevation-path-${componentId}`, {
                type: 'geojson',
                data: {
                    type: 'FeatureCollection',
                    features: []
                }
            });

            map.addLayer({
                id: `elevation-path-${componentId}`,
                type: 'line',
                source: `elevation-path-${componentId}`,
                paint: {
                    'line-color': '#3b82f6',
                    'line-width': 3,
                    'line-opacity': 0.8
                }
            });
        }

        // Add marker layer for hover position
        if (!map.getSource(`elevation-marker-${componentId}`)) {
            map.addSource(`elevation-marker-${componentId}`, {
                type: 'geojson',
                data: {
                    type: 'FeatureCollection',
                    features: []
                }
            });

            map.addLayer({
                id: `elevation-marker-${componentId}`,
                type: 'circle',
                source: `elevation-marker-${componentId}`,
                paint: {
                    'circle-radius': 8,
                    'circle-color': '#3b82f6',
                    'circle-stroke-width': 2,
                    'circle-stroke-color': '#ffffff'
                }
            });
        }

        console.log('Elevation profile initialized for map:', mapId);
    } catch (error) {
        console.error('Error initializing elevation profile:', error);
    }
}

// Enable path drawing mode
export function enableDrawPath(mapId) {
    try {
        const mapContainer = document.getElementById(mapId);
        if (!mapContainer || !mapContainer._map) {
            console.error('Map not found:', mapId);
            return;
        }

        const map = mapContainer._map;

        // Change cursor to crosshair
        map.getCanvas().style.cursor = 'crosshair';

        console.log('Draw path mode enabled');
    } catch (error) {
        console.error('Error enabling draw path:', error);
    }
}

// Generate elevation profile from coordinates
export async function generateElevationProfile(coordinatesJson, optionsJson) {
    try {
        const coordinates = JSON.parse(coordinatesJson);
        const options = JSON.parse(optionsJson);

        console.log('Generating elevation profile with', coordinates.length, 'points');
        console.log('Options:', options);

        // Sample points along the line
        const line = turf.lineString(coordinates);
        const length = turf.length(line, { units: 'meters' });
        const samplePoints = options.SamplePoints || 100;
        const step = length / samplePoints;

        const points = [];
        for (let i = 0; i <= samplePoints; i++) {
            const distance = i * step;
            const point = turf.along(line, distance / 1000, { units: 'kilometers' });
            points.push({
                coordinates: point.geometry.coordinates,
                distance: distance
            });
        }

        // Query elevation for each point
        const elevationData = await queryElevation(points, options);

        // Calculate profile statistics
        const profile = calculateProfileStatistics(elevationData, options);

        console.log('Elevation profile generated:', profile);
        return JSON.stringify(profile);
    } catch (error) {
        console.error('Error generating elevation profile:', error);
        throw error;
    }
}

// Query elevation data from various sources
async function queryElevation(points, options) {
    const source = options.Source || 'OpenElevation';

    try {
        switch (source) {
            case 'MapboxAPI':
                return await queryMapboxElevation(points, options.ApiKey);
            case 'OpenElevation':
                return await queryOpenElevation(points);
            case 'USGSAPI':
                return await queryUSGSElevation(points);
            case 'GoogleAPI':
                return await queryGoogleElevation(points, options.ApiKey);
            default:
                return await queryOpenElevation(points);
        }
    } catch (error) {
        console.error('Error querying elevation:', error);
        // Fallback to simulated elevation if API fails
        return simulateElevation(points);
    }
}

// Query Mapbox Terrain API
async function queryMapboxElevation(points, apiKey) {
    if (!apiKey) {
        throw new Error('Mapbox API key required');
    }

    const batchSize = 100; // Mapbox limit
    const results = [];

    for (let i = 0; i < points.length; i += batchSize) {
        const batch = points.slice(i, i + batchSize);
        const coords = batch.map(p => p.coordinates.join(',')).join(';');

        const url = `https://api.mapbox.com/v4/mapbox.mapbox-terrain-v2/tilequery/${coords}.json?access_token=${apiKey}`;

        try {
            const response = await fetch(url);
            const data = await response.json();

            batch.forEach((point, idx) => {
                results.push({
                    ...point,
                    elevation: data.features[idx]?.properties?.ele || 0
                });
            });
        } catch (error) {
            console.error('Mapbox API error:', error);
            // Add fallback values
            batch.forEach(point => {
                results.push({ ...point, elevation: 0 });
            });
        }
    }

    return results;
}

// Query Open-Elevation API (free, no key required)
async function queryOpenElevation(points) {
    const batchSize = 100; // Reasonable batch size
    const results = [];

    for (let i = 0; i < points.length; i += batchSize) {
        const batch = points.slice(i, i + batchSize);
        const locations = batch.map(p => ({
            latitude: p.coordinates[1],
            longitude: p.coordinates[0]
        }));

        try {
            const response = await fetch('https://api.open-elevation.com/api/v1/lookup', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ locations })
            });

            const data = await response.json();

            batch.forEach((point, idx) => {
                results.push({
                    ...point,
                    elevation: data.results[idx]?.elevation || 0
                });
            });
        } catch (error) {
            console.error('Open-Elevation API error:', error);
            // Add fallback values
            batch.forEach(point => {
                results.push({ ...point, elevation: 0 });
            });
        }

        // Add delay to avoid rate limiting
        if (i + batchSize < points.length) {
            await new Promise(resolve => setTimeout(resolve, 500));
        }
    }

    return results;
}

// Query USGS Elevation API
async function queryUSGSElevation(points) {
    const results = [];

    for (const point of points) {
        try {
            const url = `https://epqs.nationalmap.gov/v1/json?x=${point.coordinates[0]}&y=${point.coordinates[1]}&units=Meters`;
            const response = await fetch(url);
            const data = await response.json();

            results.push({
                ...point,
                elevation: parseFloat(data.value) || 0
            });
        } catch (error) {
            console.error('USGS API error:', error);
            results.push({ ...point, elevation: 0 });
        }

        // Rate limiting
        await new Promise(resolve => setTimeout(resolve, 100));
    }

    return results;
}

// Query Google Elevation API
async function queryGoogleElevation(points, apiKey) {
    if (!apiKey) {
        throw new Error('Google API key required');
    }

    const batchSize = 512; // Google limit
    const results = [];

    for (let i = 0; i < points.length; i += batchSize) {
        const batch = points.slice(i, i + batchSize);
        const locations = batch.map(p => `${p.coordinates[1]},${p.coordinates[0]}`).join('|');

        const url = `https://maps.googleapis.com/maps/api/elevation/json?locations=${locations}&key=${apiKey}`;

        try {
            const response = await fetch(url);
            const data = await response.json();

            if (data.status === 'OK') {
                batch.forEach((point, idx) => {
                    results.push({
                        ...point,
                        elevation: data.results[idx]?.elevation || 0
                    });
                });
            } else {
                throw new Error(data.error_message || 'Google API error');
            }
        } catch (error) {
            console.error('Google API error:', error);
            batch.forEach(point => {
                results.push({ ...point, elevation: 0 });
            });
        }
    }

    return results;
}

// Simulate elevation data (for testing/fallback)
function simulateElevation(points) {
    return points.map((point, idx) => ({
        ...point,
        // Simulate elevation with some variation
        elevation: 100 + Math.sin(idx / 10) * 50 + Math.random() * 20
    }));
}

// Calculate profile statistics
function calculateProfileStatistics(elevationData, options) {
    const points = elevationData.map((point, idx) => {
        const grade = idx > 0
            ? calculateGrade(elevationData[idx - 1], point)
            : 0;

        return {
            Coordinates: point.coordinates,
            Elevation: point.elevation,
            Distance: point.distance,
            Grade: grade,
            CumulativeGain: 0,
            CumulativeLoss: 0,
            Index: idx
        };
    });

    // Calculate cumulative gain/loss
    let cumulativeGain = 0;
    let cumulativeLoss = 0;

    for (let i = 1; i < points.length; i++) {
        const elevChange = points[i].Elevation - points[i - 1].Elevation;
        if (elevChange > 0) {
            cumulativeGain += elevChange;
        } else {
            cumulativeLoss += Math.abs(elevChange);
        }
        points[i].CumulativeGain = cumulativeGain;
        points[i].CumulativeLoss = cumulativeLoss;
    }

    // Find steep sections
    const steepSections = findSteepSections(points, options.SteepGradeThreshold || 10);

    // Calculate waypoints (start, end, summit, valleys)
    const waypoints = calculateWaypoints(points);

    // Calculate time estimate
    const timeEstimate = calculateTimeEstimate(points, options.ActivityType || 'Hiking');

    const elevations = points.map(p => p.Elevation);
    const grades = points.filter(p => p.Grade !== 0).map(p => p.Grade);

    return {
        Id: generateId(),
        Name: 'Elevation Profile',
        Points: points,
        TotalDistance: points[points.length - 1].Distance,
        ElevationGain: cumulativeGain,
        ElevationLoss: cumulativeLoss,
        MaxElevation: Math.max(...elevations),
        MinElevation: Math.min(...elevations),
        AverageGrade: grades.length > 0 ? grades.reduce((a, b) => a + b, 0) / grades.length : 0,
        MaxGrade: grades.length > 0 ? Math.max(...grades) : 0,
        MinGrade: grades.length > 0 ? Math.min(...grades) : 0,
        NetElevationChange: elevations[elevations.length - 1] - elevations[0],
        CumulativeElevationGain: cumulativeGain,
        CumulativeElevationLoss: cumulativeLoss,
        Source: options.Source || 'OpenElevation',
        SampleCount: points.length,
        SteepSections: steepSections,
        Waypoints: waypoints,
        TimeEstimate: timeEstimate,
        CreatedAt: new Date().toISOString(),
        Metadata: {}
    };
}

// Calculate grade between two points
function calculateGrade(point1, point2) {
    const elevChange = point2.elevation - point1.elevation;
    const distChange = point2.distance - point1.distance;

    if (distChange === 0) return 0;

    return (elevChange / distChange) * 100;
}

// Find steep sections along the route
function findSteepSections(points, threshold) {
    const sections = [];
    let currentSection = null;

    for (let i = 1; i < points.length; i++) {
        const grade = Math.abs(points[i].Grade);

        if (grade >= threshold) {
            if (!currentSection) {
                currentSection = {
                    startIdx: i - 1,
                    endIdx: i,
                    maxGrade: grade,
                    grades: [grade]
                };
            } else {
                currentSection.endIdx = i;
                currentSection.maxGrade = Math.max(currentSection.maxGrade, grade);
                currentSection.grades.push(grade);
            }
        } else if (currentSection) {
            // End of steep section
            sections.push(createSteepSection(points, currentSection));
            currentSection = null;
        }
    }

    // Add last section if still open
    if (currentSection) {
        sections.push(createSteepSection(points, currentSection));
    }

    return sections;
}

// Create steep section object
function createSteepSection(points, section) {
    const start = points[section.startIdx];
    const end = points[section.endIdx];
    const avgGrade = section.grades.reduce((a, b) => a + b, 0) / section.grades.length;
    const elevChange = end.Elevation - start.Elevation;
    const length = end.Distance - start.Distance;

    return {
        StartDistance: start.Distance,
        EndDistance: end.Distance,
        AverageGrade: avgGrade,
        MaxGrade: section.maxGrade,
        ElevationChange: elevChange,
        Length: length,
        Severity: classifyGrade(avgGrade),
        StartCoordinates: start.Coordinates,
        EndCoordinates: end.Coordinates
    };
}

// Classify grade severity
function classifyGrade(grade) {
    const absGrade = Math.abs(grade);
    if (absGrade < 5) return 'Flat';
    if (absGrade < 10) return 'Low';
    if (absGrade < 15) return 'Moderate';
    if (absGrade < 20) return 'High';
    return 'Extreme';
}

// Calculate waypoints
function calculateWaypoints(points) {
    const waypoints = [];

    // Start point
    waypoints.push({
        Id: generateId(),
        Name: 'Start',
        Coordinates: points[0].Coordinates,
        Distance: points[0].Distance,
        Elevation: points[0].Elevation,
        Type: 'Start'
    });

    // End point
    waypoints.push({
        Id: generateId(),
        Name: 'End',
        Coordinates: points[points.length - 1].Coordinates,
        Distance: points[points.length - 1].Distance,
        Elevation: points[points.length - 1].Elevation,
        Type: 'End'
    });

    // Find summit (highest point)
    const summitIdx = points.reduce((maxIdx, point, idx, arr) =>
        point.Elevation > arr[maxIdx].Elevation ? idx : maxIdx, 0);

    if (summitIdx !== 0 && summitIdx !== points.length - 1) {
        waypoints.push({
            Id: generateId(),
            Name: 'Summit',
            Coordinates: points[summitIdx].Coordinates,
            Distance: points[summitIdx].Distance,
            Elevation: points[summitIdx].Elevation,
            Type: 'Summit'
        });
    }

    return waypoints;
}

// Calculate time estimate based on activity type
function calculateTimeEstimate(points, activityType) {
    const distance = points[points.length - 1].Distance / 1000; // km
    const elevGain = points[points.length - 1].CumulativeGain;

    // Activity speeds (km/h on flat terrain)
    const speeds = {
        'Hiking': 4.0,
        'Running': 10.0,
        'Cycling': 20.0,
        'MountainBiking': 15.0,
        'Walking': 5.0,
        'Skiing': 12.0
    };

    const baseSpeed = speeds[activityType] || 5.0;

    // Naismith's rule: 1 hour per 5km + 1 hour per 600m ascent
    const flatTime = (distance / baseSpeed) * 60; // minutes
    const elevTime = (elevGain / 600) * 60; // minutes
    const totalMinutes = flatTime + elevTime;

    return {
        TotalMinutes: totalMinutes,
        MovingMinutes: totalMinutes * 0.85,
        BreakMinutes: totalMinutes * 0.15,
        ActivityType: activityType,
        AverageSpeed: distance / (totalMinutes / 60),
        Pace: totalMinutes / distance,
        Difficulty: calculateDifficulty(distance, elevGain)
    };
}

// Calculate difficulty rating (1-5)
function calculateDifficulty(distanceKm, elevGain) {
    const score = (distanceKm * 0.5) + (elevGain / 100);

    if (score < 5) return 1;
    if (score < 10) return 2;
    if (score < 20) return 3;
    if (score < 30) return 4;
    return 5;
}

// Create elevation chart using Chart.js
export async function createElevationChart(chartId, profileJson, configJson, dotNetRef) {
    try {
        const profile = JSON.parse(profileJson);
        const config = JSON.parse(configJson);

        // Load Chart.js if not already loaded
        if (typeof Chart === 'undefined') {
            await loadChartJs();
        }

        const canvas = document.getElementById(chartId);
        if (!canvas) {
            console.error('Chart canvas not found:', chartId);
            return;
        }

        // Destroy existing chart if any
        if (charts.has(chartId)) {
            charts.get(chartId).destroy();
        }

        const ctx = canvas.getContext('2d');

        // Prepare data
        const distances = profile.Points.map(p => p.Distance / 1000); // Convert to km
        const elevations = profile.Points.map(p => p.Elevation);
        const grades = profile.Points.map(p => p.Grade);

        // Create gradient colors based on grade
        const backgroundColors = config.ShowGradeColors
            ? grades.map(grade => getGradeColor(grade, config))
            : config.FlatColor || '#3b82f6';

        const chart = new Chart(ctx, {
            type: 'line',
            data: {
                labels: distances,
                datasets: [{
                    label: 'Elevation',
                    data: elevations,
                    borderColor: '#3b82f6',
                    backgroundColor: config.FillUnderCurve
                        ? createGradient(ctx, '#3b82f6', 0.3)
                        : 'transparent',
                    borderWidth: config.LineWidth || 2,
                    fill: config.FillUnderCurve,
                    tension: config.SmoothCurve ? 0.4 : 0,
                    pointRadius: 0,
                    pointHoverRadius: 6,
                    pointHoverBackgroundColor: '#3b82f6',
                    pointHoverBorderColor: '#ffffff',
                    pointHoverBorderWidth: 2
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: {
                    mode: 'index',
                    intersect: false
                },
                plugins: {
                    legend: {
                        display: false
                    },
                    tooltip: {
                        enabled: config.ShowTooltip !== false,
                        callbacks: {
                            title: (items) => {
                                const distance = items[0].label;
                                return `Distance: ${parseFloat(distance).toFixed(2)} km`;
                            },
                            label: (context) => {
                                const elevation = context.parsed.y;
                                const grade = grades[context.dataIndex];
                                return [
                                    `Elevation: ${elevation.toFixed(0)} m`,
                                    `Grade: ${grade.toFixed(1)}%`
                                ];
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        title: {
                            display: true,
                            text: 'Distance (km)'
                        },
                        grid: {
                            display: config.ShowGrid !== false
                        }
                    },
                    y: {
                        title: {
                            display: true,
                            text: 'Elevation (m)'
                        },
                        grid: {
                            display: config.ShowGrid !== false
                        }
                    }
                },
                onHover: (event, activeElements) => {
                    if (activeElements.length > 0 && dotNetRef) {
                        const index = activeElements[0].index;
                        const distance = profile.Points[index].Distance;
                        dotNetRef.invokeMethodAsync('OnChartHover', distance, index);
                    }
                },
                onClick: (event, activeElements) => {
                    if (activeElements.length > 0 && dotNetRef) {
                        const index = activeElements[0].index;
                        const distance = profile.Points[index].Distance;
                        dotNetRef.invokeMethodAsync('OnChartClick', distance);
                    }
                }
            }
        });

        charts.set(chartId, chart);
        profileData.set(chartId, profile);

        console.log('Elevation chart created:', chartId);
    } catch (error) {
        console.error('Error creating elevation chart:', error);
    }
}

// Get color based on grade
function getGradeColor(grade, config) {
    const absGrade = Math.abs(grade);

    if (absGrade < 5) return config.FlatColor || '#10B981';
    if (absGrade < 15) return config.ModerateColor || '#F59E0B';
    return config.SteepColor || '#EF4444';
}

// Create gradient for fill
function createGradient(ctx, color, alpha) {
    const gradient = ctx.createLinearGradient(0, 0, 0, 400);
    gradient.addColorStop(0, color + Math.round(alpha * 255).toString(16).padStart(2, '0'));
    gradient.addColorStop(1, color + '00');
    return gradient;
}

// Load Chart.js library
async function loadChartJs() {
    return new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = 'https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.min.js';
        script.onload = resolve;
        script.onerror = reject;
        document.head.appendChild(script);
    });
}

// Update map marker position
export function updateMapMarker(mapId, coordinates) {
    try {
        const mapContainer = document.getElementById(mapId);
        if (!mapContainer || !mapContainer._map) return;

        const map = mapContainer._map;
        const componentId = Array.from(maps.entries())
            .find(([_, data]) => data.mapId === mapId)?.[0];

        if (!componentId) return;

        const source = map.getSource(`elevation-marker-${componentId}`);
        if (source) {
            source.setData({
                type: 'FeatureCollection',
                features: [{
                    type: 'Feature',
                    geometry: {
                        type: 'Point',
                        coordinates: coordinates
                    }
                }]
            });
        }
    } catch (error) {
        console.error('Error updating map marker:', error);
    }
}

// Highlight steep sections on map
export function highlightSteepSections(mapId, sectionsJson) {
    try {
        const sections = JSON.parse(sectionsJson);
        const mapContainer = document.getElementById(mapId);
        if (!mapContainer || !mapContainer._map) return;

        const map = mapContainer._map;
        const componentId = Array.from(maps.entries())
            .find(([_, data]) => data.mapId === mapId)?.[0];

        if (!componentId) return;

        // Create features for steep sections
        const features = sections.map(section => ({
            type: 'Feature',
            geometry: {
                type: 'LineString',
                coordinates: [section.StartCoordinates, section.EndCoordinates]
            },
            properties: {
                severity: section.Severity,
                grade: section.AverageGrade
            }
        }));

        // Add or update steep sections layer
        const sourceId = `steep-sections-${componentId}`;
        if (!map.getSource(sourceId)) {
            map.addSource(sourceId, {
                type: 'geojson',
                data: { type: 'FeatureCollection', features }
            });

            map.addLayer({
                id: sourceId,
                type: 'line',
                source: sourceId,
                paint: {
                    'line-color': [
                        'match',
                        ['get', 'severity'],
                        'Low', '#F59E0B',
                        'Moderate', '#F97316',
                        'High', '#EF4444',
                        'Extreme', '#DC2626',
                        '#10B981'
                    ],
                    'line-width': 5,
                    'line-opacity': 0.7
                }
            }, `elevation-path-${componentId}`);
        } else {
            map.getSource(sourceId).setData({ type: 'FeatureCollection', features });
        }
    } catch (error) {
        console.error('Error highlighting steep sections:', error);
    }
}

// Export elevation profile
export async function exportElevationProfile(chartId, profileJson, format) {
    try {
        const profile = JSON.parse(profileJson);

        switch (format) {
            case 'png':
                await exportAsPNG(chartId);
                break;
            case 'csv':
                exportAsCSV(profile);
                break;
            case 'gpx':
                exportAsGPX(profile);
                break;
            case 'json':
                exportAsJSON(profile);
                break;
            default:
                console.error('Unknown export format:', format);
        }
    } catch (error) {
        console.error('Error exporting profile:', error);
    }
}

// Export chart as PNG
async function exportAsPNG(chartId) {
    const canvas = document.getElementById(chartId);
    if (!canvas) return;

    const link = document.createElement('a');
    link.download = `elevation-profile-${Date.now()}.png`;
    link.href = canvas.toDataURL('image/png');
    link.click();
}

// Export data as CSV
function exportAsCSV(profile) {
    const headers = ['Distance (m)', 'Elevation (m)', 'Grade (%)', 'Cumulative Gain (m)', 'Cumulative Loss (m)'];
    const rows = profile.Points.map(p => [
        p.Distance.toFixed(2),
        p.Elevation.toFixed(2),
        p.Grade.toFixed(2),
        p.CumulativeGain.toFixed(2),
        p.CumulativeLoss.toFixed(2)
    ]);

    const csv = [headers, ...rows].map(row => row.join(',')).join('\n');
    downloadFile(csv, `elevation-profile-${Date.now()}.csv`, 'text/csv');
}

// Export as GPX
function exportAsGPX(profile) {
    const gpx = `<?xml version="1.0" encoding="UTF-8"?>
<gpx version="1.1" creator="Honua MapSDK">
  <metadata>
    <name>${profile.Name}</name>
    <time>${profile.CreatedAt}</time>
  </metadata>
  <trk>
    <name>${profile.Name}</name>
    <trkseg>
${profile.Points.map(p => `      <trkpt lat="${p.Coordinates[1]}" lon="${p.Coordinates[0]}">
        <ele>${p.Elevation}</ele>
      </trkpt>`).join('\n')}
    </trkseg>
  </trk>
</gpx>`;

    downloadFile(gpx, `elevation-profile-${Date.now()}.gpx`, 'application/gpx+xml');
}

// Export as JSON
function exportAsJSON(profile) {
    const json = JSON.stringify(profile, null, 2);
    downloadFile(json, `elevation-profile-${Date.now()}.json`, 'application/json');
}

// Download file helper
function downloadFile(content, filename, mimeType) {
    const blob = new Blob([content], { type: mimeType });
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = filename;
    link.click();
    URL.revokeObjectURL(link.href);
}

// Clear elevation profile from map
export function clearElevationProfile(mapId) {
    try {
        const mapContainer = document.getElementById(mapId);
        if (!mapContainer || !mapContainer._map) return;

        const map = mapContainer._map;
        const componentId = Array.from(maps.entries())
            .find(([_, data]) => data.mapId === mapId)?.[0];

        if (!componentId) return;

        // Clear marker
        const markerSource = map.getSource(`elevation-marker-${componentId}`);
        if (markerSource) {
            markerSource.setData({ type: 'FeatureCollection', features: [] });
        }

        // Clear path
        const pathSource = map.getSource(`elevation-path-${componentId}`);
        if (pathSource) {
            pathSource.setData({ type: 'FeatureCollection', features: [] });
        }

        // Clear steep sections
        const steepSource = map.getSource(`steep-sections-${componentId}`);
        if (steepSource) {
            steepSource.setData({ type: 'FeatureCollection', features: [] });
        }
    } catch (error) {
        console.error('Error clearing elevation profile:', error);
    }
}

// Cleanup
export function cleanup(mapId) {
    try {
        const componentId = Array.from(maps.entries())
            .find(([_, data]) => data.mapId === mapId)?.[0];

        if (componentId) {
            maps.delete(componentId);

            const chart = charts.get(componentId);
            if (chart) {
                chart.destroy();
                charts.delete(componentId);
            }

            profileData.delete(componentId);
            markerLayers.delete(componentId);
        }
    } catch (error) {
        console.error('Error cleaning up:', error);
    }
}

// Generate unique ID
function generateId() {
    return `${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
}

console.log('Honua Elevation Profile module loaded');
