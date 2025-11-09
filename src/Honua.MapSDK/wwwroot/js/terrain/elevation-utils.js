// Honua Elevation Utilities Module
// Provides utilities for elevation queries, profile analysis, and terrain visualization

import mapboxgl from 'https://cdn.jsdelivr.net/npm/mapbox-gl@3/+esm';

const drawingStates = new Map();
const profileData = new Map();

/**
 * Start drawing a path on the map for elevation profile
 */
export function startDrawingPath(mapId, dotNetRef) {
    try {
        const mapContainer = document.getElementById(mapId);
        if (!mapContainer || !mapContainer._map) {
            console.error('Map not found:', mapId);
            return;
        }

        const map = mapContainer._map;
        const drawState = {
            coordinates: [],
            markers: [],
            line: null
        };

        // Add source and layer for the drawing line
        if (!map.getSource('elevation-draw-line')) {
            map.addSource('elevation-draw-line', {
                type: 'geojson',
                data: {
                    type: 'Feature',
                    geometry: {
                        type: 'LineString',
                        coordinates: []
                    }
                }
            });

            map.addLayer({
                id: 'elevation-draw-line',
                type: 'line',
                source: 'elevation-draw-line',
                paint: {
                    'line-color': '#3b82f6',
                    'line-width': 3,
                    'line-dasharray': [2, 2]
                }
            });
        }

        // Change cursor
        map.getCanvas().style.cursor = 'crosshair';

        // Click handler
        const clickHandler = (e) => {
            const coords = [e.lngLat.lng, e.lngLat.lat];
            drawState.coordinates.push(coords);

            // Add marker
            const marker = new mapboxgl.Marker({ color: '#3b82f6' })
                .setLngLat(coords)
                .addTo(map);
            drawState.markers.push(marker);

            // Update line
            map.getSource('elevation-draw-line').setData({
                type: 'Feature',
                geometry: {
                    type: 'LineString',
                    coordinates: drawState.coordinates
                }
            });
        };

        // Double-click handler to finish
        const dblClickHandler = (e) => {
            e.preventDefault();
            finishDrawing();
        };

        // Escape key to cancel
        const keyHandler = (e) => {
            if (e.key === 'Escape') {
                cancelDrawing();
            }
        };

        map.on('click', clickHandler);
        map.on('dblclick', dblClickHandler);
        document.addEventListener('keydown', keyHandler);

        drawState.clickHandler = clickHandler;
        drawState.dblClickHandler = dblClickHandler;
        drawState.keyHandler = keyHandler;
        drawState.map = map;
        drawState.dotNetRef = dotNetRef;

        drawingStates.set(mapId, drawState);

        console.log('Drawing mode started. Click to add points, double-click to finish, ESC to cancel.');

        function finishDrawing() {
            if (drawState.coordinates.length < 2) {
                alert('Please draw at least 2 points');
                return;
            }

            // Clean up handlers
            map.off('click', clickHandler);
            map.off('dblclick', dblClickHandler);
            document.removeEventListener('keydown', keyHandler);

            map.getCanvas().style.cursor = '';

            // Call .NET with coordinates
            dotNetRef.invokeMethodAsync('OnPathDrawn', drawState.coordinates);

            drawingStates.delete(mapId);
        }

        function cancelDrawing() {
            // Clean up
            map.off('click', clickHandler);
            map.off('dblclick', dblClickHandler);
            document.removeEventListener('keydown', keyHandler);

            map.getCanvas().style.cursor = '';

            // Remove markers
            drawState.markers.forEach(m => m.remove());

            // Clear line
            if (map.getSource('elevation-draw-line')) {
                map.getSource('elevation-draw-line').setData({
                    type: 'Feature',
                    geometry: {
                        type: 'LineString',
                        coordinates: []
                    }
                });
            }

            drawingStates.delete(mapId);
            console.log('Drawing cancelled');
        }
    } catch (error) {
        console.error('Error starting drawing:', error);
    }
}

/**
 * Create elevation profile chart
 */
export async function createElevationChart(chartId, profile) {
    try {
        // Load Chart.js if not already loaded
        if (typeof Chart === 'undefined') {
            await loadChartJs();
        }

        const canvas = document.getElementById(chartId);
        if (!canvas) {
            console.error('Chart canvas not found:', chartId);
            return;
        }

        const ctx = canvas.getContext('2d');

        // Prepare data
        const distances = profile.points.map(p => p.distance / 1000); // Convert to km
        const elevations = profile.points.map(p => p.elevation);

        // Calculate gradients for coloring
        const gradients = [];
        for (let i = 1; i < elevations.length; i++) {
            const rise = elevations[i] - elevations[i - 1];
            const run = (distances[i] - distances[i - 1]) * 1000; // Convert to meters
            const gradient = run > 0 ? (rise / run) * 100 : 0;
            gradients.push(gradient);
        }
        gradients.unshift(0); // First point has no gradient

        // Create gradient background
        const backgroundGradient = ctx.createLinearGradient(0, 0, 0, 300);
        backgroundGradient.addColorStop(0, 'rgba(59, 130, 246, 0.4)');
        backgroundGradient.addColorStop(1, 'rgba(59, 130, 246, 0.1)');

        const chart = new Chart(ctx, {
            type: 'line',
            data: {
                labels: distances,
                datasets: [{
                    label: 'Elevation (m)',
                    data: elevations,
                    borderColor: '#3b82f6',
                    backgroundColor: backgroundGradient,
                    borderWidth: 2,
                    fill: true,
                    tension: 0.4,
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
                        enabled: true,
                        callbacks: {
                            title: (items) => {
                                const distance = items[0].label;
                                return `Distance: ${parseFloat(distance).toFixed(2)} km`;
                            },
                            label: (context) => {
                                const elevation = context.parsed.y;
                                const gradient = gradients[context.dataIndex];
                                return [
                                    `Elevation: ${elevation.toFixed(0)} m`,
                                    `Gradient: ${gradient.toFixed(1)}%`
                                ];
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        title: {
                            display: true,
                            text: 'Distance (km)',
                            font: {
                                size: 12,
                                weight: 'bold'
                            }
                        },
                        grid: {
                            display: true,
                            color: 'rgba(0, 0, 0, 0.05)'
                        }
                    },
                    y: {
                        title: {
                            display: true,
                            text: 'Elevation (m)',
                            font: {
                                size: 12,
                                weight: 'bold'
                            }
                        },
                        grid: {
                            display: true,
                            color: 'rgba(0, 0, 0, 0.05)'
                        },
                        beginAtZero: false
                    }
                }
            }
        });

        profileData.set(chartId, { chart, profile });
        console.log('Elevation chart created');
    } catch (error) {
        console.error('Error creating elevation chart:', error);
    }
}

/**
 * Export elevation profile as CSV
 */
export function exportProfileCSV(profile) {
    try {
        const headers = ['Distance (m)', 'Elevation (m)', 'Longitude', 'Latitude'];
        const rows = profile.points.map(p => [
            p.distance.toFixed(2),
            p.elevation.toFixed(2),
            p.longitude.toFixed(6),
            p.latitude.toFixed(6)
        ]);

        const csv = [headers, ...rows].map(row => row.join(',')).join('\n');
        downloadFile(csv, `elevation-profile-${Date.now()}.csv`, 'text/csv');
        console.log('Exported profile as CSV');
    } catch (error) {
        console.error('Error exporting CSV:', error);
    }
}

/**
 * Export elevation profile as GPX
 */
export function exportProfileGPX(profile) {
    try {
        const gpx = `<?xml version="1.0" encoding="UTF-8"?>
<gpx version="1.1" creator="Honua MapSDK" xmlns="http://www.topografix.com/GPX/1/1">
  <metadata>
    <name>Elevation Profile</name>
    <time>${new Date().toISOString()}</time>
  </metadata>
  <trk>
    <name>Elevation Profile</name>
    <trkseg>
${profile.points.map(p => `      <trkpt lat="${p.latitude}" lon="${p.longitude}">
        <ele>${p.elevation}</ele>
      </trkpt>`).join('\n')}
    </trkseg>
  </trk>
</gpx>`;

        downloadFile(gpx, `elevation-profile-${Date.now()}.gpx`, 'application/gpx+xml');
        console.log('Exported profile as GPX');
    } catch (error) {
        console.error('Error exporting GPX:', error);
    }
}

/**
 * Clear elevation profile from map
 */
export function clearProfile(mapId) {
    try {
        const mapContainer = document.getElementById(mapId);
        if (!mapContainer || !mapContainer._map) return;

        const map = mapContainer._map;

        // Remove drawing line
        if (map.getSource('elevation-draw-line')) {
            map.getSource('elevation-draw-line').setData({
                type: 'Feature',
                geometry: {
                    type: 'LineString',
                    coordinates: []
                }
            });
        }

        // Clear any markers
        const drawState = drawingStates.get(mapId);
        if (drawState) {
            drawState.markers.forEach(m => m.remove());
            drawingStates.delete(mapId);
        }

        console.log('Profile cleared');
    } catch (error) {
        console.error('Error clearing profile:', error);
    }
}

/**
 * Calculate viewshed analysis from a point
 */
export async function calculateViewshed(longitude, latitude, elevationData, options = {}) {
    try {
        const {
            radius = 5000,        // meters
            observerHeight = 1.7, // meters above ground
            resolution = 100      // analysis resolution
        } = options;

        // This would:
        // 1. Get elevation grid around the point
        // 2. Calculate line-of-sight to each point in radius
        // 3. Return visible/not visible classification

        console.log('Viewshed analysis not yet fully implemented');
        return {
            visible: [],
            hidden: []
        };
    } catch (error) {
        console.error('Error calculating viewshed:', error);
        return null;
    }
}

/**
 * Calculate slope and aspect for terrain analysis
 */
export function calculateSlopeAspect(elevationGrid) {
    try {
        const height = elevationGrid.length;
        const width = elevationGrid[0].length;
        const slope = [];
        const aspect = [];

        for (let y = 1; y < height - 1; y++) {
            const slopeRow = [];
            const aspectRow = [];

            for (let x = 1; x < width - 1; x++) {
                // Horn's method for slope calculation
                const dzdx = ((elevationGrid[y - 1][x + 1] + 2 * elevationGrid[y][x + 1] + elevationGrid[y + 1][x + 1]) -
                             (elevationGrid[y - 1][x - 1] + 2 * elevationGrid[y][x - 1] + elevationGrid[y + 1][x - 1])) / 8.0;

                const dzdy = ((elevationGrid[y + 1][x - 1] + 2 * elevationGrid[y + 1][x] + elevationGrid[y + 1][x + 1]) -
                             (elevationGrid[y - 1][x - 1] + 2 * elevationGrid[y - 1][x] + elevationGrid[y - 1][x + 1])) / 8.0;

                const slopeValue = Math.atan(Math.sqrt(dzdx * dzdx + dzdy * dzdy)) * 180 / Math.PI;
                const aspectValue = Math.atan2(dzdy, -dzdx) * 180 / Math.PI;

                slopeRow.push(slopeValue);
                aspectRow.push(aspectValue);
            }

            slope.push(slopeRow);
            aspect.push(aspectRow);
        }

        return { slope, aspect };
    } catch (error) {
        console.error('Error calculating slope/aspect:', error);
        return null;
    }
}

/**
 * Generate contour lines from elevation data
 */
export function generateContours(elevationGrid, interval = 100) {
    try {
        // Marching squares algorithm for contour generation
        // This is a simplified version
        const contours = [];

        // Would implement marching squares here
        console.log('Contour generation not yet fully implemented');

        return contours;
    } catch (error) {
        console.error('Error generating contours:', error);
        return [];
    }
}

/**
 * Load Chart.js library
 */
async function loadChartJs() {
    return new Promise((resolve, reject) => {
        if (typeof Chart !== 'undefined') {
            resolve();
            return;
        }

        const script = document.createElement('script');
        script.src = 'https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.min.js';
        script.onload = resolve;
        script.onerror = reject;
        document.head.appendChild(script);
    });
}

/**
 * Download file helper
 */
function downloadFile(content, filename, mimeType) {
    const blob = new Blob([content], { type: mimeType });
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = filename;
    link.click();
    URL.revokeObjectURL(link.href);
}

console.log('Honua Elevation Utilities module loaded');
