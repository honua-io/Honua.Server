/**
 * Honua 3D Model Loader
 *
 * Advanced GLTF/GLB model loading with Three.js integration.
 * Provides model positioning, animation, LOD, and interactive picking on MapLibre maps.
 *
 * Dependencies:
 * - Three.js (r150+)
 * - GLTFLoader
 * - MapLibre GL JS
 *
 * @module HonuaModel3D
 */

window.HonuaModel3D = (function () {
    'use strict';

    // Model instances by map ID
    const _modelInstances = new Map();

    // Three.js scenes by map ID
    const _scenes = new Map();

    // Animation mixers
    const _animationMixers = new Map();

    // LOD groups
    const _lodGroups = new Map();

    // Raycaster for picking
    const _raycaster = typeof THREE !== 'undefined' ? new THREE.Raycaster() : null;

    // Performance monitoring
    let _frameCount = 0;
    let _lastFpsUpdate = performance.now();
    let _currentFps = 60;

    /**
     * Check if Three.js is loaded
     */
    function isThreeJSAvailable() {
        return typeof THREE !== 'undefined' && THREE.GLTFLoader !== 'undefined';
    }

    /**
     * Initialize 3D model system for a map
     *
     * @param {string} mapId - Map container ID
     * @param {object} mapLibreMap - MapLibre GL map instance
     * @param {object} options - Initialization options
     * @returns {boolean} Success status
     */
    function initialize(mapId, mapLibreMap, options = {}) {
        if (!isThreeJSAvailable()) {
            console.warn('Three.js or GLTFLoader not loaded. Load from: https://unpkg.com/three@0.150.0/build/three.min.js');
            return false;
        }

        if (_scenes.has(mapId)) {
            console.warn(`3D model system already initialized for map '${mapId}'`);
            return true;
        }

        // Create Three.js scene
        const scene = new THREE.Scene();

        // Add lights
        if (options.enableLighting !== false) {
            // Ambient light
            const ambientLight = new THREE.AmbientLight(0xffffff, 0.5);
            scene.add(ambientLight);

            // Directional light (sun)
            const directionalLight = new THREE.DirectionalLight(0xffffff, 0.8);
            directionalLight.position.set(100, 100, 50);
            directionalLight.castShadow = true;
            scene.add(directionalLight);

            // Hemisphere light for outdoor scenes
            const hemisphereLight = new THREE.HemisphereLight(0x87ceeb, 0x8b7355, 0.3);
            scene.add(hemisphereLight);
        }

        // Store scene and map reference
        _scenes.set(mapId, {
            scene: scene,
            map: mapLibreMap,
            models: new Map(),
            camera: null,
            renderer: null,
            options: options
        });

        _modelInstances.set(mapId, new Map());

        // Setup rendering loop
        setupRenderLoop(mapId);

        console.log(`HonuaModel3D initialized for map '${mapId}'`);
        return true;
    }

    /**
     * Load a GLTF/GLB model and add it to the map
     *
     * @param {string} mapId - Map ID
     * @param {string} modelId - Unique model instance ID
     * @param {string} modelUrl - URL to GLTF/GLB file
     * @param {object} options - Model options
     * @returns {Promise<object>} Model info
     */
    async function loadModel(mapId, modelId, modelUrl, options = {}) {
        const sceneData = _scenes.get(mapId);
        if (!sceneData) {
            throw new Error(`Map '${mapId}' not initialized`);
        }

        if (!isThreeJSAvailable()) {
            throw new Error('Three.js not available');
        }

        const startTime = performance.now();

        return new Promise((resolve, reject) => {
            const loader = new THREE.GLTFLoader();

            loader.load(
                modelUrl,
                (gltf) => {
                    try {
                        const model = gltf.scene;
                        const modelInfo = processLoadedModel(mapId, modelId, model, gltf, options);
                        modelInfo.loadTimeMs = performance.now() - startTime;

                        // Position model on map
                        positionModelOnMap(mapId, modelId, options);

                        // Setup animations if available
                        if (gltf.animations && gltf.animations.length > 0 && options.enableAnimation) {
                            setupAnimations(mapId, modelId, model, gltf.animations, options);
                        }

                        // Setup LOD if configured
                        if (options.enableLOD && options.lodLevels) {
                            setupLOD(mapId, modelId, options.lodLevels);
                        }

                        console.log(`Model '${modelId}' loaded: ${modelInfo.triangleCount} triangles, ${modelInfo.loadTimeMs.toFixed(0)}ms`);
                        resolve(modelInfo);
                    } catch (error) {
                        reject(error);
                    }
                },
                (progress) => {
                    // Progress callback
                    if (options.onProgress) {
                        const percent = (progress.loaded / progress.total) * 100;
                        options.onProgress(percent);
                    }
                },
                (error) => {
                    console.error(`Failed to load model '${modelId}':`, error);
                    reject(error);
                }
            );
        });
    }

    /**
     * Process loaded model and extract metadata
     */
    function processLoadedModel(mapId, modelId, model, gltf, options) {
        const sceneData = _scenes.get(mapId);

        // Apply transformations
        if (options.scale !== undefined) {
            const scale = typeof options.scale === 'number' ? options.scale : 1;
            model.scale.set(scale, scale, scale);
        }

        if (options.rotation) {
            model.rotation.set(
                options.rotation.x || 0,
                options.rotation.y || 0,
                options.rotation.z || 0
            );
        }

        // Calculate bounding box
        const bbox = new THREE.Box3().setFromObject(model);
        const center = bbox.getCenter(new THREE.Vector3());
        const size = bbox.getSize(new THREE.Vector3());

        // Count triangles and vertices
        let triangleCount = 0;
        let vertexCount = 0;
        let meshCount = 0;
        let materialCount = new Set();

        model.traverse((child) => {
            if (child.isMesh) {
                meshCount++;
                if (child.geometry) {
                    const geometry = child.geometry;
                    if (geometry.index) {
                        triangleCount += geometry.index.count / 3;
                    } else {
                        triangleCount += geometry.attributes.position.count / 3;
                    }
                    vertexCount += geometry.attributes.position.count;
                }
                if (child.material) {
                    if (Array.isArray(child.material)) {
                        child.material.forEach(m => materialCount.add(m.uuid));
                    } else {
                        materialCount.add(child.material.uuid);
                    }
                }
            }
        });

        // Extract animations
        const animations = gltf.animations.map((anim, index) => ({
            name: anim.name || `Animation ${index}`,
            index: index,
            duration: anim.duration,
            channelCount: anim.tracks.length
        }));

        // Store model
        sceneData.models.set(modelId, {
            object3D: model,
            gltf: gltf,
            options: options,
            boundingBox: { min: bbox.min, max: bbox.max, center, size }
        });

        // Add to scene
        sceneData.scene.add(model);

        // Return metadata
        return {
            modelId: modelId,
            modelUrl: options.modelUrl || '',
            format: options.modelUrl?.endsWith('.glb') ? 'GLB' : 'GLTF',
            triangleCount: Math.floor(triangleCount),
            vertexCount: vertexCount,
            meshCount: meshCount,
            materialCount: materialCount.size,
            animations: animations,
            boundingBox: {
                min: { x: bbox.min.x, y: bbox.min.y, z: bbox.min.z },
                max: { x: bbox.max.x, y: bbox.max.y, z: bbox.max.z },
                center: { x: center.x, y: center.y, z: center.z },
                size: { x: size.x, y: size.y, z: size.z }
            }
        };
    }

    /**
     * Position model on map using mercator projection
     */
    function positionModelOnMap(mapId, modelId, options) {
        const sceneData = _scenes.get(mapId);
        if (!sceneData) return;

        const modelData = sceneData.models.get(modelId);
        if (!modelData) return;

        const position = options.position || { latitude: 0, longitude: 0 };
        const altitude = options.altitude || 0;

        // Convert lat/lng to mercator meters
        const mercatorCoords = latLngToMercator(position.latitude, position.longitude);

        // Position in 3D space
        modelData.object3D.position.set(
            mercatorCoords.x,
            mercatorCoords.y,
            altitude
        );

        // Store position for updates
        modelData.position = position;
        modelData.altitude = altitude;
    }

    /**
     * Setup animations for a model
     */
    function setupAnimations(mapId, modelId, model, animations, options) {
        if (animations.length === 0) return;

        const mixer = new THREE.AnimationMixer(model);
        const mixerKey = `${mapId}_${modelId}`;

        const actions = animations.map((anim, index) => {
            const action = mixer.clipAction(anim);
            if (index === (options.animationIndex || 0)) {
                action.play();
            }
            return action;
        });

        _animationMixers.set(mixerKey, {
            mixer: mixer,
            actions: actions,
            activeIndex: options.animationIndex || 0
        });

        console.log(`Setup ${animations.length} animations for model '${modelId}'`);
    }

    /**
     * Setup Level of Detail (LOD)
     */
    function setupLOD(mapId, modelId, lodLevels) {
        const sceneData = _scenes.get(mapId);
        if (!sceneData) return;

        const modelData = sceneData.models.get(modelId);
        if (!modelData) return;

        const lod = new THREE.LOD();

        // Add LOD levels
        lodLevels.forEach((level, index) => {
            // For now, use the same model at different distances
            // In production, you'd load different model files
            const lodObject = modelData.object3D.clone();
            lod.addLevel(lodObject, level.minDistance || index * 100);
        });

        // Replace model with LOD group
        sceneData.scene.remove(modelData.object3D);
        sceneData.scene.add(lod);
        modelData.object3D = lod;
        modelData.isLOD = true;

        _lodGroups.set(`${mapId}_${modelId}`, lod);
        console.log(`Setup LOD with ${lodLevels.length} levels for model '${modelId}'`);
    }

    /**
     * Pick (raycast) a model at screen coordinates
     *
     * @param {string} mapId - Map ID
     * @param {number} x - Screen X coordinate
     * @param {number} y - Screen Y coordinate
     * @returns {object|null} Pick result
     */
    function pickModel(mapId, x, y) {
        if (!_raycaster) return null;

        const sceneData = _scenes.get(mapId);
        if (!sceneData || !sceneData.camera) return null;

        // Convert screen coordinates to NDC
        const canvas = sceneData.renderer?.domElement;
        if (!canvas) return null;

        const rect = canvas.getBoundingClientRect();
        const mouse = new THREE.Vector2(
            ((x - rect.left) / rect.width) * 2 - 1,
            -((y - rect.top) / rect.height) * 2 + 1
        );

        _raycaster.setFromCamera(mouse, sceneData.camera);

        // Get all model objects
        const objects = [];
        sceneData.models.forEach((modelData) => {
            objects.push(modelData.object3D);
        });

        const intersects = _raycaster.intersectObjects(objects, true);

        if (intersects.length > 0) {
            const hit = intersects[0];
            return {
                point: { x: hit.point.x, y: hit.point.y, z: hit.point.z },
                distance: hit.distance,
                normal: hit.face ? { x: hit.face.normal.x, y: hit.face.normal.y, z: hit.face.normal.z } : null,
                uv: hit.uv ? { u: hit.uv.x, v: hit.uv.y } : null,
                object: hit.object
            };
        }

        return null;
    }

    /**
     * Update animation for a model
     *
     * @param {string} mapId - Map ID
     * @param {string} modelId - Model ID
     * @param {object} options - Animation options
     */
    function updateAnimation(mapId, modelId, options) {
        const mixerKey = `${mapId}_${modelId}`;
        const mixerData = _animationMixers.get(mixerKey);
        if (!mixerData) return;

        // Stop current animation
        if (mixerData.actions[mixerData.activeIndex]) {
            mixerData.actions[mixerData.activeIndex].stop();
        }

        // Play new animation
        if (options.animationIndex !== undefined && mixerData.actions[options.animationIndex]) {
            const action = mixerData.actions[options.animationIndex];

            if (options.timeScale !== undefined) {
                action.timeScale = options.timeScale;
            }

            if (options.loop !== undefined) {
                action.loop = options.loop ? THREE.LoopRepeat : THREE.LoopOnce;
            }

            if (options.play !== false) {
                action.play();
            }

            mixerData.activeIndex = options.animationIndex;
        }
    }

    /**
     * Remove a model from the map
     */
    function removeModel(mapId, modelId) {
        const sceneData = _scenes.get(mapId);
        if (!sceneData) return;

        const modelData = sceneData.models.get(modelId);
        if (!modelData) return;

        // Remove from scene
        sceneData.scene.remove(modelData.object3D);

        // Cleanup animations
        const mixerKey = `${mapId}_${modelId}`;
        const mixerData = _animationMixers.get(mixerKey);
        if (mixerData) {
            mixerData.mixer.stopAllAction();
            _animationMixers.delete(mixerKey);
        }

        // Cleanup LOD
        _lodGroups.delete(mixerKey);

        // Dispose geometries and materials
        modelData.object3D.traverse((child) => {
            if (child.geometry) {
                child.geometry.dispose();
            }
            if (child.material) {
                if (Array.isArray(child.material)) {
                    child.material.forEach(m => m.dispose());
                } else {
                    child.material.dispose();
                }
            }
        });

        sceneData.models.delete(modelId);
        console.log(`Model '${modelId}' removed`);
    }

    /**
     * Setup render loop
     */
    function setupRenderLoop(mapId) {
        const sceneData = _scenes.get(mapId);
        if (!sceneData) return;

        const clock = new THREE.Clock();

        function animate() {
            requestAnimationFrame(animate);

            const delta = clock.getDelta();

            // Update animations
            _animationMixers.forEach((mixerData) => {
                mixerData.mixer.update(delta);
            });

            // Update LOD
            if (sceneData.camera) {
                _lodGroups.forEach((lod) => {
                    lod.update(sceneData.camera);
                });
            }

            // Update FPS counter
            _frameCount++;
            const now = performance.now();
            if (now - _lastFpsUpdate >= 1000) {
                _currentFps = Math.round((_frameCount * 1000) / (now - _lastFpsUpdate));
                _frameCount = 0;
                _lastFpsUpdate = now;
            }

            // Render would happen here if we had a separate renderer
            // For MapLibre integration, rendering happens via custom layer
        }

        animate();
    }

    /**
     * Convert lat/lng to Web Mercator meters
     */
    function latLngToMercator(lat, lng) {
        const earthRadius = 6378137; // meters
        const x = (lng * Math.PI / 180) * earthRadius;
        const y = Math.log(Math.tan((90 + lat) * Math.PI / 360)) * earthRadius;
        return { x, y };
    }

    /**
     * Get current FPS
     */
    function getFPS() {
        return _currentFps;
    }

    /**
     * Get all models for a map
     */
    function getModels(mapId) {
        const sceneData = _scenes.get(mapId);
        if (!sceneData) return [];

        return Array.from(sceneData.models.keys());
    }

    /**
     * Dispose of all resources for a map
     */
    function dispose(mapId) {
        const sceneData = _scenes.get(mapId);
        if (!sceneData) return;

        // Remove all models
        const modelIds = Array.from(sceneData.models.keys());
        modelIds.forEach(modelId => removeModel(mapId, modelId));

        // Cleanup scene
        sceneData.scene.clear();

        _scenes.delete(mapId);
        _modelInstances.delete(mapId);

        console.log(`HonuaModel3D disposed for map '${mapId}'`);
    }

    // Public API
    return {
        initialize,
        loadModel,
        removeModel,
        updateAnimation,
        pickModel,
        positionModelOnMap,
        getModels,
        getFPS,
        dispose,
        isAvailable: isThreeJSAvailable
    };
})();

// Export for Node.js environments
if (typeof module !== 'undefined' && module.exports) {
    module.exports = window.HonuaModel3D;
}
