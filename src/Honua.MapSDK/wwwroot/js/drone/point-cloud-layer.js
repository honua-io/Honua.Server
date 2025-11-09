/**
 * Deck.gl Point Cloud Layer for Drone Data
 * Handles streaming and rendering of large point clouds with LOD support
 */

import { PointCloudLayer } from '@deck.gl/layers';
import { COORDINATE_SYSTEM } from '@deck.gl/core';

/**
 * Point cloud renderer for drone survey data
 */
export class DronePointCloudRenderer {
    constructor(deckInstance, options = {}) {
        this.deckInstance = deckInstance;
        this.options = {
            baseUrl: options.baseUrl || '/api/drone',
            pointSize: options.pointSize || 2,
            maxPoints: options.maxPoints || 1000000,
            ...options
        };

        this.currentSurveyId = null;
        this.points = [];
        this.colorMode = 'rgb';
        this.lodLevel = 0;
        this.classificationFilter = null;
        this.layer = null;
    }

    /**
     * Load and render point cloud for a survey
     */
    async loadSurvey(surveyId, options = {}) {
        console.log(`Loading point cloud for survey ${surveyId}`);

        this.currentSurveyId = surveyId;
        this.colorMode = options.colorMode || 'rgb';
        this.lodLevel = options.lod || 0;
        this.classificationFilter = options.classificationFilter || null;

        // Get viewport bounds
        const viewport = this.deckInstance.getViewports()[0];
        const bounds = this.getViewportBounds(viewport);

        // Build query URL
        const url = this.buildQueryUrl(surveyId, bounds, options);

        // Fetch and stream points
        await this.streamPoints(url);

        // Update layer
        this.updateLayer();
    }

    /**
     * Stream points from API
     */
    async streamPoints(url) {
        console.log(`Streaming points from ${url}`);

        const response = await fetch(url);
        if (!response.ok) {
            throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }

        const reader = response.body.getReader();
        const decoder = new TextDecoder();

        this.points = [];
        let buffer = '';
        let pointCount = 0;

        while (true) {
            const { done, value } = await reader.read();

            if (done) break;

            buffer += decoder.decode(value, { stream: true });

            // Process complete lines
            const lines = buffer.split('\n');
            buffer = lines.pop() || ''; // Keep incomplete line

            for (const line of lines) {
                if (line.trim()) {
                    try {
                        const feature = JSON.parse(line);
                        this.points.push(this.parseFeature(feature));
                        pointCount++;

                        // Update layer incrementally every 10k points
                        if (pointCount % 10000 === 0) {
                            this.updateLayer();
                        }

                        // Respect max points limit
                        if (pointCount >= this.options.maxPoints) {
                            reader.cancel();
                            break;
                        }
                    } catch (e) {
                        console.error('Failed to parse line:', e, line);
                    }
                }
            }
        }

        console.log(`Loaded ${pointCount} points`);
    }

    /**
     * Parse GeoJSON feature to point data
     */
    parseFeature(feature) {
        const coords = feature.geometry.coordinates;
        const props = feature.properties;

        return {
            position: coords,
            color: this.getPointColor(props),
            classification: props.classification,
            intensity: props.intensity,
            red: props.red,
            green: props.green,
            blue: props.blue
        };
    }

    /**
     * Get point color based on color mode
     */
    getPointColor(properties) {
        switch (this.colorMode) {
            case 'rgb':
                return [
                    properties.red / 256,
                    properties.green / 256,
                    properties.blue / 256
                ];

            case 'classification':
                return this.getClassificationColor(properties.classification);

            case 'intensity':
                const intensity = (properties.intensity || 0) / 65535;
                return [intensity * 255, intensity * 255, intensity * 255];

            case 'elevation':
                // Color by Z value (height)
                const z = properties.z || 0;
                return this.getElevationColor(z);

            default:
                return [200, 200, 200];
        }
    }

    /**
     * Get color for classification code
     */
    getClassificationColor(classification) {
        const colors = {
            0: [128, 128, 128],  // Never Classified - Gray
            1: [128, 128, 128],  // Unclassified - Gray
            2: [139, 69, 19],    // Ground - Brown
            3: [34, 139, 34],    // Low Vegetation - Green
            4: [0, 128, 0],      // Medium Vegetation - Dark Green
            5: [0, 255, 0],      // High Vegetation - Bright Green
            6: [255, 0, 0],      // Building - Red
            7: [255, 255, 0],    // Low Point (Noise) - Yellow
            9: [0, 0, 255],      // Water - Blue
            10: [128, 0, 128],   // Rail - Purple
            11: [64, 64, 64],    // Road Surface - Dark Gray
            17: [255, 255, 0],   // Bridge Deck - Yellow
            18: [255, 0, 255]    // High Noise - Magenta
        };

        return colors[classification] || [200, 200, 200];
    }

    /**
     * Get color based on elevation
     */
    getElevationColor(z) {
        // Simple height-based coloring (blue to red)
        const normalized = Math.max(0, Math.min(1, (z + 100) / 200));

        if (normalized < 0.5) {
            // Blue to cyan to green
            const t = normalized * 2;
            return [0, t * 255, (1 - t) * 255];
        } else {
            // Green to yellow to red
            const t = (normalized - 0.5) * 2;
            return [t * 255, (1 - t) * 255, 0];
        }
    }

    /**
     * Update Deck.gl layer
     */
    updateLayer() {
        this.layer = new PointCloudLayer({
            id: 'drone-point-cloud',
            data: this.points,

            // Position
            getPosition: d => d.position,

            // Color
            getColor: d => d.color,

            // Size
            pointSize: this.options.pointSize,
            sizeUnits: 'pixels',

            // Picking
            pickable: true,
            autoHighlight: true,

            // Performance
            coordinateSystem: COORDINATE_SYSTEM.LNGLAT,
            parameters: {
                depthTest: true,
                blend: true,
                blendFunc: ['SRC_ALPHA', 'ONE_MINUS_SRC_ALPHA']
            },

            // Update triggers
            updateTriggers: {
                getColor: [this.colorMode]
            },

            // Callbacks
            onHover: info => this.onPointHover(info),
            onClick: info => this.onPointClick(info)
        });

        // Update deck instance
        const existingLayers = this.deckInstance.props.layers || [];
        const filteredLayers = existingLayers.filter(l => l.id !== 'drone-point-cloud');

        this.deckInstance.setProps({
            layers: [...filteredLayers, this.layer]
        });
    }

    /**
     * Handle point hover
     */
    onPointHover(info) {
        if (info.object) {
            const point = info.object;
            console.log('Hovered point:', {
                position: point.position,
                classification: point.classification,
                intensity: point.intensity
            });
        }
    }

    /**
     * Handle point click
     */
    onPointClick(info) {
        if (info.object) {
            const point = info.object;
            console.log('Clicked point:', point);

            // Emit event for Blazor interop
            if (window.dronePointCloudCallback) {
                window.dronePointCloudCallback(point);
            }
        }
    }

    /**
     * Change color mode
     */
    setColorMode(mode) {
        this.colorMode = mode;

        // Recompute colors
        this.points = this.points.map(point => ({
            ...point,
            color: this.getPointColor({
                classification: point.classification,
                intensity: point.intensity,
                red: point.red,
                green: point.green,
                blue: point.blue,
                z: point.position[2]
            })
        }));

        this.updateLayer();
    }

    /**
     * Change point size
     */
    setPointSize(size) {
        this.options.pointSize = size;
        this.updateLayer();
    }

    /**
     * Clear point cloud
     */
    clear() {
        this.points = [];
        this.updateLayer();
    }

    /**
     * Build query URL with parameters
     */
    buildQueryUrl(surveyId, bounds, options) {
        const params = new URLSearchParams({
            minX: bounds.minX.toString(),
            minY: bounds.minY.toString(),
            maxX: bounds.maxX.toString(),
            maxY: bounds.maxY.toString(),
            minZ: (bounds.minZ || -10000).toString(),
            maxZ: (bounds.maxZ || 10000).toString(),
            lod: this.lodLevel.toString(),
            limit: this.options.maxPoints.toString()
        });

        if (this.classificationFilter && this.classificationFilter.length > 0) {
            this.classificationFilter.forEach(c => {
                params.append('classifications', c.toString());
            });
        }

        return `${this.options.baseUrl}/surveys/${surveyId}/pointcloud?${params}`;
    }

    /**
     * Get viewport bounds
     */
    getViewportBounds(viewport) {
        // Get viewport corners in lat/lng
        const nw = viewport.unproject([0, 0]);
        const se = viewport.unproject([viewport.width, viewport.height]);

        return {
            minX: Math.min(nw[0], se[0]),
            minY: Math.min(nw[1], se[1]),
            maxX: Math.max(nw[0], se[0]),
            maxY: Math.max(nw[1], se[1])
        };
    }
}

/**
 * Create and initialize point cloud renderer
 */
export function createPointCloudRenderer(deckInstance, options) {
    return new DronePointCloudRenderer(deckInstance, options);
}

// Export for window global access
if (typeof window !== 'undefined') {
    window.DronePointCloudRenderer = DronePointCloudRenderer;
    window.createPointCloudRenderer = createPointCloudRenderer;
}
