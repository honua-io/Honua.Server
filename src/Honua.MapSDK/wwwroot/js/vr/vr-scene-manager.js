/**
 * VR Scene Manager - Manages Three.js scene for VR rendering
 * Handles stereoscopic rendering, lighting, and scene graph
 */

import * as THREE from 'three';

export class VRSceneManager {
    constructor(gl, session) {
        this.gl = gl;
        this.session = session;
        this.scene = null;
        this.camera = null;
        this.renderer = null;
        this.lights = [];
        this.skybox = null;
        this.gridHelper = null;
        this.initialized = false;
    }

    /**
     * Initializes the VR scene
     */
    initialize() {
        // Create scene
        this.scene = new THREE.Scene();
        this.scene.background = new THREE.Color(0x87CEEB); // Sky blue

        // Create camera (will be controlled by VR headset)
        this.camera = new THREE.PerspectiveCamera(
            90, // FOV
            window.innerWidth / window.innerHeight,
            0.1,
            100000 // Far plane for large geospatial scenes
        );

        // Create renderer with XR support
        this.renderer = new THREE.WebGLRenderer({
            canvas: this.gl.canvas,
            context: this.gl,
            antialias: true,
            alpha: false
        });
        this.renderer.setPixelRatio(window.devicePixelRatio);
        this.renderer.xr.enabled = true;
        this.renderer.xr.setReferenceSpaceType('local-floor');
        this.renderer.xr.setSession(this.session);

        // Enable shadows for better depth perception
        this.renderer.shadowMap.enabled = true;
        this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;

        // Set up lighting
        this._setupLighting();

        // Set up environment
        this._setupEnvironment();

        this.initialized = true;
        console.log('VR Scene initialized');
    }

    /**
     * Sets up scene lighting
     */
    _setupLighting() {
        // Ambient light for base illumination
        const ambientLight = new THREE.AmbientLight(0xffffff, 0.6);
        this.scene.add(ambientLight);
        this.lights.push(ambientLight);

        // Directional light (sun)
        const sunLight = new THREE.DirectionalLight(0xffffff, 0.8);
        sunLight.position.set(100, 200, 100);
        sunLight.castShadow = true;
        sunLight.shadow.mapSize.width = 2048;
        sunLight.shadow.mapSize.height = 2048;
        sunLight.shadow.camera.near = 0.5;
        sunLight.shadow.camera.far = 500;
        this.scene.add(sunLight);
        this.lights.push(sunLight);

        // Hemisphere light for outdoor scenes
        const hemiLight = new THREE.HemisphereLight(0x87CEEB, 0x545454, 0.4);
        this.scene.add(hemiLight);
        this.lights.push(hemiLight);
    }

    /**
     * Sets up environment (skybox, grid, etc.)
     */
    _setupEnvironment() {
        // Create skybox
        const skyGeometry = new THREE.SphereGeometry(50000, 32, 32);
        const skyMaterial = new THREE.MeshBasicMaterial({
            color: 0x87CEEB,
            side: THREE.BackSide
        });
        this.skybox = new THREE.Mesh(skyGeometry, skyMaterial);
        this.scene.add(this.skybox);

        // Create ground grid (helpful for orientation)
        this.gridHelper = new THREE.GridHelper(1000, 100, 0x888888, 0xcccccc);
        this.gridHelper.position.y = 0;
        this.gridHelper.visible = false; // Hidden by default
        this.scene.add(this.gridHelper);
    }

    /**
     * Adds an object to the scene
     */
    addObject(object) {
        if (!this.scene) {
            throw new Error('Scene not initialized');
        }
        this.scene.add(object);
    }

    /**
     * Removes an object from the scene
     */
    removeObject(object) {
        if (!this.scene) return;
        this.scene.remove(object);
    }

    /**
     * Sets whether the grid is visible
     */
    setGridVisible(visible) {
        if (this.gridHelper) {
            this.gridHelper.visible = visible;
        }
    }

    /**
     * Updates lighting based on quality settings
     */
    updateLightingQuality(quality) {
        switch (quality) {
            case 'low':
                this.renderer.shadowMap.enabled = false;
                this.lights.forEach(light => {
                    if (light instanceof THREE.DirectionalLight) {
                        light.intensity = 0.5;
                    }
                });
                break;

            case 'medium':
                this.renderer.shadowMap.enabled = true;
                this.renderer.shadowMap.type = THREE.BasicShadowMap;
                this.lights.forEach(light => {
                    if (light instanceof THREE.DirectionalLight) {
                        light.intensity = 0.7;
                    }
                });
                break;

            case 'high':
                this.renderer.shadowMap.enabled = true;
                this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
                this.lights.forEach(light => {
                    if (light instanceof THREE.DirectionalLight) {
                        light.intensity = 0.8;
                    }
                });
                break;
        }
    }

    /**
     * Renders the scene for VR
     */
    render(frame, referenceSpace) {
        if (!this.initialized) return;

        // Get viewer pose
        const pose = frame.getViewerPose(referenceSpace);
        if (!pose) return;

        // Update camera from pose
        const views = pose.views;

        // Render for each eye (stereoscopic)
        for (const view of views) {
            const viewport = this.session.renderState.baseLayer.getViewport(view);
            this.renderer.setViewport(viewport.x, viewport.y, viewport.width, viewport.height);

            // Update camera matrices from XR view
            this.camera.matrix.fromArray(view.transform.matrix);
            this.camera.projectionMatrix.fromArray(view.projectionMatrix);
            this.camera.updateMatrixWorld(true);

            // Render scene
            this.renderer.render(this.scene, this.camera);
        }
    }

    /**
     * Disposes of scene resources
     */
    dispose() {
        if (this.scene) {
            this.scene.traverse((object) => {
                if (object.geometry) {
                    object.geometry.dispose();
                }
                if (object.material) {
                    if (Array.isArray(object.material)) {
                        object.material.forEach(material => material.dispose());
                    } else {
                        object.material.dispose();
                    }
                }
            });
        }

        if (this.renderer) {
            this.renderer.dispose();
        }

        this.initialized = false;
    }

    /**
     * Gets scene statistics for performance monitoring
     */
    getStats() {
        const info = this.renderer.info;
        return {
            geometries: info.memory.geometries,
            textures: info.memory.textures,
            triangles: info.render.triangles,
            drawCalls: info.render.calls,
            fps: this.renderer.info.render.frame
        };
    }
}

// Export for module usage
window.VRSceneManager = VRSceneManager;
export default VRSceneManager;
