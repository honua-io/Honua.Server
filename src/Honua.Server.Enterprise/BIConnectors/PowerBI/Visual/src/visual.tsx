/*
 * Honua Kepler.gl Power BI Visual
 * Advanced geospatial visualization for Honua data
 */

import powerbi from "powerbi-visuals-api";
import IVisual = powerbi.extensibility.visual.IVisual;
import VisualConstructorOptions = powerbi.extensibility.visual.VisualConstructorOptions;
import VisualUpdateOptions = powerbi.extensibility.visual.VisualUpdateOptions;
import DataView = powerbi.DataView;
import IVisualHost = powerbi.extensibility.visual.IVisualHost;

import * as React from "react";
import * as ReactDOM from "react-dom";
import KeplerGl from "kepler.gl";
import { addDataToMap } from "kepler.gl/actions";
import { Provider } from "react-redux";
import { createStore, applyMiddleware, combineReducers } from "redux";
import keplerGlReducer from "kepler.gl/reducers";

export class Visual implements IVisual {
    private target: HTMLElement;
    private host: IVisualHost;
    private store: any;
    private updateCount: number;
    private mapContainer: HTMLElement;

    constructor(options: VisualConstructorOptions) {
        console.log("Honua Kepler.gl Visual constructor");
        this.target = options.element;
        this.host = options.host;
        this.updateCount = 0;

        // Create map container
        this.mapContainer = document.createElement("div");
        this.mapContainer.className = "kepler-map-container";
        this.mapContainer.style.width = "100%";
        this.mapContainer.style.height = "100%";
        this.mapContainer.style.position = "relative";
        this.target.appendChild(this.mapContainer);

        // Initialize Redux store with Kepler.gl reducer
        const reducers = combineReducers({
            keplerGl: keplerGlReducer
        });

        this.store = createStore(reducers, {}, applyMiddleware());

        // Render Kepler.gl component
        this.renderKeplerMap({});
    }

    public update(options: VisualUpdateOptions) {
        console.log("Visual update");
        this.updateCount++;

        if (!options.dataViews || !options.dataViews[0]) {
            console.log("No data available");
            return;
        }

        const dataView = options.dataViews[0];

        // Extract settings
        const settings = this.getSettings(dataView);

        // Process data
        const data = this.processData(dataView);

        if (data.rows.length === 0) {
            console.log("No rows to display");
            return;
        }

        // Update Kepler.gl with new data
        this.updateKeplerMap(data, settings);
    }

    /**
     * Extract visual settings from data view
     */
    private getSettings(dataView: DataView): any {
        const objects = dataView.metadata.objects;

        return {
            mapStyle: this.getObjectProperty(objects, "mapSettings", "mapStyle", "dark"),
            show3D: this.getObjectProperty(objects, "mapSettings", "show3D", false),
            showLegend: this.getObjectProperty(objects, "mapSettings", "showLegend", true),
            showTooltip: this.getObjectProperty(objects, "mapSettings", "showTooltip", true),
            layerType: this.getObjectProperty(objects, "layerSettings", "layerType", "point"),
            pointRadius: this.getObjectProperty(objects, "layerSettings", "pointRadius", 10),
            opacity: this.getObjectProperty(objects, "layerSettings", "opacity", 0.8),
            elevationScale: this.getObjectProperty(objects, "layerSettings", "elevationScale", 5),
            colorScale: this.getObjectProperty(objects, "colorSettings", "colorScale", "quantize"),
            colorRange: this.getObjectProperty(objects, "colorSettings", "colorRange", "global.uber.6"),
            enableTimeFilter: this.getObjectProperty(objects, "filterSettings", "enableTimeFilter", false),
            animationSpeed: this.getObjectProperty(objects, "filterSettings", "animationSpeed", 1)
        };
    }

    /**
     * Get object property value with default fallback
     */
    private getObjectProperty(objects: any, objectName: string, propertyName: string, defaultValue: any): any {
        if (objects && objects[objectName] && objects[objectName][propertyName] !== undefined) {
            return objects[objectName][propertyName];
        }
        return defaultValue;
    }

    /**
     * Process Power BI data into format suitable for Kepler.gl
     */
    private processData(dataView: DataView): any {
        const table = dataView.table;
        const rows = table.rows;
        const columns = table.columns;

        // Find column indices
        const latIdx = this.findColumnIndex(columns, "latitude");
        const lonIdx = this.findColumnIndex(columns, "longitude");
        const categoryIdx = this.findColumnIndex(columns, "category");
        const sizeIdx = this.findColumnIndex(columns, "size");
        const colorIdx = this.findColumnIndex(columns, "color");
        const tooltipIdx = this.findColumnIndex(columns, "tooltip");
        const timeIdx = this.findColumnIndex(columns, "time");

        if (latIdx === -1 || lonIdx === -1) {
            console.error("Latitude and Longitude columns are required");
            return { fields: [], rows: [] };
        }

        // Build field definitions
        const fields = [
            { name: "latitude", type: "real" },
            { name: "longitude", type: "real" }
        ];

        if (categoryIdx !== -1) {
fields.push({ name: "category", type: "string" });
}
        if (sizeIdx !== -1) {
fields.push({ name: "size", type: "real" });
}
        if (colorIdx !== -1) {
fields.push({ name: "color", type: "real" });
}
        if (tooltipIdx !== -1) {
fields.push({ name: "tooltip", type: "string" });
}
        if (timeIdx !== -1) {
fields.push({ name: "time", type: "timestamp" });
}

        // Transform rows
        const processedRows = rows.map(row => {
            const processedRow: any = {
                latitude: row[latIdx],
                longitude: row[lonIdx]
            };

            if (categoryIdx !== -1) {
processedRow.category = row[categoryIdx];
}
            if (sizeIdx !== -1) {
processedRow.size = row[sizeIdx];
}
            if (colorIdx !== -1) {
processedRow.color = row[colorIdx];
}
            if (tooltipIdx !== -1) {
processedRow.tooltip = row[tooltipIdx];
}
            if (timeIdx !== -1) {
processedRow.time = row[timeIdx];
}

            return processedRow;
        });

        return {
            fields,
            rows: processedRows
        };
    }

    /**
     * Find column index by role name
     */
    private findColumnIndex(columns: any[], roleName: string): number {
        for (let i = 0; i < columns.length; i++) {
            if (columns[i].roles && columns[i].roles[roleName]) {
                return i;
            }
        }
        return -1;
    }

    /**
     * Render Kepler.gl map component
     */
    private renderKeplerMap(_config: any) {
        const KeplerMap = () => (
            <Provider store={this.store}>
                <KeplerGl
                    id="honua-map"
                    mapboxApiAccessToken={this.getMapboxToken()}
                    width={this.mapContainer.clientWidth}
                    height={this.mapContainer.clientHeight}
                    theme="dark"
                />
            </Provider>
        );

        ReactDOM.render(<KeplerMap />, this.mapContainer);
    }

    /**
     * Update Kepler.gl with new data
     */
    private updateKeplerMap(data: any, settings: any) {
        const dataset = {
            info: {
                id: "honua-data",
                label: "Honua Geospatial Data"
            },
            data: {
                fields: data.fields,
                rows: data.rows
            }
        };

        // Dispatch data to Kepler.gl
        this.store.dispatch(
            addDataToMap({
                datasets: dataset,
                options: {
                    centerMap: this.updateCount === 1, // Center only on first update
                    readOnly: false
                },
                config: this.buildKeplerConfig(settings)
            })
        );
    }

    /**
     * Build Kepler.gl configuration from visual settings
     */
    private buildKeplerConfig(settings: any): any {
        const config: any = {
            version: "v1",
            config: {
                visState: {
                    filters: [],
                    layers: [
                        {
                            id: "honua-layer",
                            type: settings.layerType,
                            config: {
                                dataId: "honua-data",
                                label: "Honua Data",
                                color: [255, 153, 31],
                                columns: {
                                    lat: "latitude",
                                    lng: "longitude"
                                },
                                isVisible: true,
                                visConfig: {
                                    radius: settings.pointRadius,
                                    opacity: settings.opacity,
                                    elevationScale: settings.elevationScale,
                                    colorRange: {
                                        name: "Custom",
                                        type: "sequential",
                                        category: "Uber"
                                    }
                                }
                            }
                        }
                    ],
                    interactionConfig: {
                        tooltip: {
                            fieldsToShow: {
                                "honua-data": ["category", "tooltip", "size", "color"]
                            },
                            enabled: settings.showTooltip
                        },
                        brush: {
                            size: 0.5,
                            enabled: false
                        },
                        coordinate: {
                            enabled: false
                        }
                    },
                    layerBlending: "normal",
                    splitMaps: []
                },
                mapState: {
                    bearing: 0,
                    dragRotate: settings.show3D,
                    latitude: 0,
                    longitude: 0,
                    pitch: settings.show3D ? 50 : 0,
                    zoom: 2,
                    isSplit: false
                },
                mapStyle: {
                    styleType: settings.mapStyle,
                    topLayerGroups: {},
                    visibleLayerGroups: {
                        label: true,
                        road: true,
                        border: false,
                        building: settings.show3D,
                        water: true,
                        land: true,
                        "3d building": settings.show3D
                    }
                }
            }
        };

        // Add time filter if enabled
        if (settings.enableTimeFilter) {
            config.config.visState.filters.push({
                dataId: "honua-data",
                id: "time-filter",
                name: "time",
                type: "timeRange",
                value: [],
                enlarged: true,
                plotType: "histogram",
                animationWindow: "free",
                speed: settings.animationSpeed
            });
        }

        return config;
    }

    /**
     * Get Mapbox access token
     * Note: In production, this should be configured via Power BI settings
     */
    private getMapboxToken(): string {
        // Use a public Mapbox token or allow configuration
        // For production, users should provide their own token
        return "pk.eyJ1IjoiaG9udWEiLCJhIjoiY2sxMjM0NTY3ODkwMSJ9.example";
    }

    /**
     * Clean up resources
     */
    public destroy(): void {
        ReactDOM.unmountComponentAtNode(this.mapContainer);
    }

    /**
     * Enumerate objects for property pane
     */
    public enumerateObjectInstances(options: powerbi.EnumerateVisualObjectInstancesOptions): powerbi.VisualObjectInstanceEnumeration {
        const objectName = options.objectName;
        const objectEnumeration: powerbi.VisualObjectInstance[] = [];

        switch (objectName) {
            case "mapSettings":
                objectEnumeration.push({
                    objectName: objectName,
                    properties: {
                        mapStyle: "dark",
                        show3D: false,
                        showLegend: true,
                        showTooltip: true
                    },
                    selector: null
                });
                break;
            case "layerSettings":
                objectEnumeration.push({
                    objectName: objectName,
                    properties: {
                        layerType: "point",
                        pointRadius: 10,
                        opacity: 0.8,
                        elevationScale: 5
                    },
                    selector: null
                });
                break;
            case "colorSettings":
                objectEnumeration.push({
                    objectName: objectName,
                    properties: {
                        colorScale: "quantize",
                        colorRange: "global.uber.6"
                    },
                    selector: null
                });
                break;
            case "filterSettings":
                objectEnumeration.push({
                    objectName: objectName,
                    properties: {
                        enableTimeFilter: false,
                        animationSpeed: 1
                    },
                    selector: null
                });
                break;
        }

        return objectEnumeration;
    }
}
