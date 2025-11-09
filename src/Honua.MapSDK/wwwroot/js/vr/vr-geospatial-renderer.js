/**
 * VR Geospatial Renderer - Renders geospatial data in VR space
 * Converts WGS84 coordinates to local VR coordinates
 */

import * as THREE from 'three';

export class VRGeospatialRenderer {
    constructor(scene) {
        this.scene = scene;
        this.origin = { lon: 0, lat: 0, elevation: 0 };
        this.scale = 1.0; // 1:1 by default
        this.features = new Map();
        this.materials = new Map();
        this.instancedMeshes = new Map();
    }

    /**
     * Sets the origin point for coordinate conversion
     */
    setOrigin(lon, lat, elevation = 0) {
        this.origin = { lon, lat, elevation };
        console.log('VR origin set to:', this.origin);
    }

    /**
     * Sets the scale factor (e.g., 100 for 1:100 scale)
     */
    setScale(scale) {
        this.scale = scale;
        this._updateAllFeatureScales();
    }

    /**
     * Converts WGS84 (lon, lat, elevation) to local VR coordinates
     */
    geoToVRSpace(lon, lat, elevation) {
        // Approximate conversion (for local areas)
        // 1 degree longitude ≈ 111.32 km * cos(latitude)
        // 1 degree latitude ≈ 110.57 km

        const metersPerDegreeLon = 111319.9 * Math.cos(this.origin.lat * Math.PI / 180);
        const metersPerDegreeLat = 111319.9;

        const x = (lon - this.origin.lon) * metersPerDegreeLon / this.scale;
        const z = -(lat - this.origin.lat) * metersPerDegreeLat / this.scale; // Negative Z is forward
        const y = (elevation - this.origin.elevation) / this.scale;

        return new THREE.Vector3(x, y, z);
    }

    /**
     * Renders GeoJSON features in VR
     */
    renderGeoJSON(geoJSON, options = {}) {
        const {
            color = 0x3388ff,
            extruded = false,
            getHeight = (properties) => properties.height || 0,
            wireframe = false
        } = options;

        if (!geoJSON.features) return;

        geoJSON.features.forEach(feature => {
            this.renderFeature(feature, {
                color,
                extruded,
                getHeight,
                wireframe
            });
        });
    }

    /**
     * Renders a single GeoJSON feature
     */
    renderFeature(feature, options = {}) {
        const geometry = feature.geometry;
        const properties = feature.properties || {};

        let mesh = null;

        switch (geometry.type) {
            case 'Point':
                mesh = this._renderPoint(geometry.coordinates, properties, options);
                break;

            case 'LineString':
                mesh = this._renderLineString(geometry.coordinates, properties, options);
                break;

            case 'Polygon':
                mesh = this._renderPolygon(geometry.coordinates, properties, options);
                break;

            case 'MultiPolygon':
                geometry.coordinates.forEach(polygonCoords => {
                    const polyFeature = { type: 'Polygon', coordinates: polygonCoords };
                    this._renderPolygon(polyFeature.coordinates, properties, options);
                });
                break;
        }

        if (mesh) {
            const featureId = feature.id || `feature_${Date.now()}_${Math.random()}`;
            mesh.userData.featureId = featureId;
            mesh.userData.properties = properties;
            this.features.set(featureId, mesh);
            this.scene.addObject(mesh);
        }
    }

    /**
     * Renders terrain elevation data
     */
    renderTerrain(elevationData, bounds, options = {}) {
        const {
            width = elevationData.width || 256,
            height = elevationData.height || 256,
            heightScale = 1.0
        } = options;

        // Convert bounds to VR space
        const minCorner = this.geoToVRSpace(bounds.west, bounds.south, 0);
        const maxCorner = this.geoToVRSpace(bounds.east, bounds.north, 0);

        const terrainWidth = maxCorner.x - minCorner.x;
        const terrainDepth = minCorner.z - maxCorner.z;

        // Create plane geometry
        const geometry = new THREE.PlaneGeometry(
            terrainWidth,
            terrainDepth,
            width - 1,
            height - 1
        );

        // Apply elevation data to vertices
        const vertices = geometry.attributes.position.array;
        for (let i = 0; i < elevationData.data.length; i++) {
            const elevation = elevationData.data[i] * heightScale / this.scale;
            vertices[i * 3 + 2] = elevation; // Z is up initially
        }

        // Rotate to make Y up
        geometry.rotateX(-Math.PI / 2);
        geometry.computeVertexNormals();

        // Create material
        const material = new THREE.MeshStandardMaterial({
            color: 0x8b7355,
            wireframe: false,
            flatShading: false
        });

        const terrainMesh = new THREE.Mesh(geometry, material);
        terrainMesh.receiveShadow = true;
        terrainMesh.position.set(
            (minCorner.x + maxCorner.x) / 2,
            0,
            (minCorner.z + maxCorner.z) / 2
        );

        this.scene.addObject(terrainMesh);
        return terrainMesh;
    }

    /**
     * Clears all rendered features
     */
    clearFeatures() {
        this.features.forEach(mesh => {
            this.scene.removeObject(mesh);
            if (mesh.geometry) mesh.geometry.dispose();
            if (mesh.material) mesh.material.dispose();
        });
        this.features.clear();
    }

    /**
     * Removes a specific feature by ID
     */
    removeFeature(featureId) {
        const mesh = this.features.get(featureId);
        if (mesh) {
            this.scene.removeObject(mesh);
            if (mesh.geometry) mesh.geometry.dispose();
            if (mesh.material) mesh.material.dispose();
            this.features.delete(featureId);
        }
    }

    // Private rendering methods

    _renderPoint(coordinates, properties, options) {
        const [lon, lat, elevation = 0] = coordinates;
        const position = this.geoToVRSpace(lon, lat, elevation);

        const geometry = new THREE.SphereGeometry(2 / this.scale, 16, 16);
        const material = this._getMaterial('point', options);
        const mesh = new THREE.Mesh(geometry, material);
        mesh.position.copy(position);

        return mesh;
    }

    _renderLineString(coordinates, properties, options) {
        const points = coordinates.map(coord => {
            const [lon, lat, elevation = 0] = coord;
            return this.geoToVRSpace(lon, lat, elevation);
        });

        const geometry = new THREE.BufferGeometry().setFromPoints(points);
        const material = new THREE.LineBasicMaterial({
            color: options.color || 0x3388ff,
            linewidth: 2
        });

        const line = new THREE.Line(geometry, material);
        return line;
    }

    _renderPolygon(coordinates, properties, options) {
        // Outer ring
        const outerRing = coordinates[0];
        const points = outerRing.map(coord => {
            const [lon, lat, elevation = 0] = coord;
            const vrPos = this.geoToVRSpace(lon, lat, elevation);
            return new THREE.Vector2(vrPos.x, vrPos.z);
        });

        const shape = new THREE.Shape(points);

        // Handle holes
        for (let i = 1; i < coordinates.length; i++) {
            const hole = coordinates[i].map(coord => {
                const [lon, lat, elevation = 0] = coord;
                const vrPos = this.geoToVRSpace(lon, lat, elevation);
                return new THREE.Vector2(vrPos.x, vrPos.z);
            });
            shape.holes.push(new THREE.Path(hole));
        }

        let geometry;
        if (options.extruded) {
            const height = options.getHeight(properties) / this.scale;
            geometry = new THREE.ExtrudeGeometry(shape, {
                depth: height,
                bevelEnabled: false
            });
            geometry.rotateX(-Math.PI / 2);
        } else {
            geometry = new THREE.ShapeGeometry(shape);
            geometry.rotateX(-Math.PI / 2);
        }

        const material = this._getMaterial('polygon', options);
        const mesh = new THREE.Mesh(geometry, material);
        mesh.castShadow = true;
        mesh.receiveShadow = true;

        return mesh;
    }

    _getMaterial(type, options) {
        const key = `${type}_${options.color}_${options.wireframe}`;

        if (this.materials.has(key)) {
            return this.materials.get(key);
        }

        const material = new THREE.MeshStandardMaterial({
            color: options.color || 0x3388ff,
            wireframe: options.wireframe || false,
            side: THREE.DoubleSide,
            metalness: 0.1,
            roughness: 0.8
        });

        this.materials.set(key, material);
        return material;
    }

    _updateAllFeatureScales() {
        // Re-render all features with new scale
        // In production, you'd optimize this
        console.log('Scale updated to', this.scale);
    }
}

// Export for module usage
window.VRGeospatialRenderer = VRGeospatialRenderer;
export default VRGeospatialRenderer;
