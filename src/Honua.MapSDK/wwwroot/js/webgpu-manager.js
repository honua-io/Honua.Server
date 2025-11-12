// WebGPU Detection and Management Module
// Handles WebGPU support detection, initialization, and automatic WebGL fallback

/**
 * Detects browser information
 * @returns {Object} Browser name and version
 */
function detectBrowser() {
    const ua = navigator.userAgent;
    let browser = 'Unknown';
    let version = 'Unknown';

    if (ua.indexOf('Chrome') > -1 && ua.indexOf('Edg') === -1) {
        browser = 'Chrome';
        const match = ua.match(/Chrome\/(\d+)/);
        version = match ? match[1] : 'Unknown';
    } else if (ua.indexOf('Edg') > -1) {
        browser = 'Edge';
        const match = ua.match(/Edg\/(\d+)/);
        version = match ? match[1] : 'Unknown';
    } else if (ua.indexOf('Firefox') > -1) {
        browser = 'Firefox';
        const match = ua.match(/Firefox\/(\d+)/);
        version = match ? match[1] : 'Unknown';
    } else if (ua.indexOf('Safari') > -1 && ua.indexOf('Chrome') === -1) {
        browser = 'Safari';
        const match = ua.match(/Version\/(\d+)/);
        version = match ? match[1] : 'Unknown';
    }

    return { browser, version };
}

/**
 * Detects WebGPU support in the current browser
 * @returns {Promise<Object>} WebGPU capability information
 */
export async function detectWebGpuSupport() {
    const { browser, version } = detectBrowser();
    const hasNavigatorGpu = 'gpu' in navigator;

    if (!hasNavigatorGpu) {
        return {
            isSupported: false,
            reason: 'navigator.gpu API not available',
            browser,
            browserVersion: version,
            hasNavigatorGpu: false
        };
    }

    try {
        // Try to request a GPU adapter
        const adapter = await navigator.gpu.requestAdapter();

        if (!adapter) {
            return {
                isSupported: false,
                reason: 'No GPU adapter available',
                browser,
                browserVersion: version,
                hasNavigatorGpu: true
            };
        }

        return {
            isSupported: true,
            reason: null,
            browser,
            browserVersion: version,
            hasNavigatorGpu: true
        };
    } catch (error) {
        return {
            isSupported: false,
            reason: `WebGPU initialization failed: ${error.message}`,
            browser,
            browserVersion: version,
            hasNavigatorGpu: true
        };
    }
}

/**
 * Gets detailed GPU adapter information
 * @returns {Promise<Object|null>} GPU adapter information or null
 */
export async function getGpuAdapterInfo() {
    if (!('gpu' in navigator)) {
        return null;
    }

    try {
        const adapter = await navigator.gpu.requestAdapter();

        if (!adapter) {
            return null;
        }

        // Get adapter info (note: some properties may be limited for privacy)
        const info = await adapter.requestAdapterInfo();

        return {
            vendor: info.vendor || 'Unknown',
            architecture: info.architecture || 'Unknown',
            device: info.device || 'Unknown',
            description: info.description || 'Unknown'
        };
    } catch (error) {
        console.warn('Failed to get GPU adapter info:', error);
        return null;
    }
}

/**
 * Performance monitor for tracking FPS and render times
 */
class PerformanceMonitor {
    constructor() {
        this.frames = [];
        this.lastTime = performance.now();
        this.fps = 0;
        this.isMonitoring = false;
    }

    start() {
        this.isMonitoring = true;
        this.tick();
    }

    stop() {
        this.isMonitoring = false;
    }

    tick() {
        if (!this.isMonitoring) return;

        const now = performance.now();
        const delta = now - this.lastTime;

        this.frames.push(delta);

        // Keep only last 60 frames
        if (this.frames.length > 60) {
            this.frames.shift();
        }

        // Calculate average FPS
        if (this.frames.length > 0) {
            const avgDelta = this.frames.reduce((a, b) => a + b, 0) / this.frames.length;
            this.fps = Math.round(1000 / avgDelta);
        }

        this.lastTime = now;

        requestAnimationFrame(() => this.tick());
    }

    getFps() {
        return this.fps;
    }
}

/**
 * WebGPU Renderer Manager
 * Handles WebGPU initialization with automatic WebGL fallback
 */
export class WebGpuRendererManager {
    constructor() {
        this.activeEngine = 'WebGL';
        this.preferredEngine = 'Auto';
        this.isFallback = false;
        this.performanceMonitor = new PerformanceMonitor();
        this.gpuInfo = null;
        this.initTime = null;
    }

    /**
     * Initializes the renderer with the specified engine preference
     * @param {string} enginePreference - 'Auto', 'WebGPU', or 'WebGL'
     * @returns {Promise<Object>} Renderer initialization result
     */
    async initialize(enginePreference = 'Auto') {
        const startTime = performance.now();
        this.preferredEngine = enginePreference;

        console.log(`[WebGPU Manager] Initializing with preference: ${enginePreference}`);

        // Force WebGL if requested
        if (enginePreference === 'WebGL') {
            this.activeEngine = 'WebGL';
            this.isFallback = false;
            this.initTime = performance.now() - startTime;

            console.log(`[WebGPU Manager] Using WebGL (user preference)`);

            return {
                engine: 'WebGL',
                isPreferred: true,
                isFallback: false,
                gpuInfo: await this.getWebGLInfo(),
                initTime: this.initTime
            };
        }

        // Try WebGPU if requested or in Auto mode
        if (enginePreference === 'WebGPU' || enginePreference === 'Auto') {
            const webGpuSupport = await detectWebGpuSupport();

            if (webGpuSupport.isSupported) {
                try {
                    // Try to initialize WebGPU
                    const adapter = await navigator.gpu.requestAdapter();

                    if (adapter) {
                        this.activeEngine = 'WebGPU';
                        this.isFallback = false;
                        this.gpuInfo = await this.getWebGPUInfo(adapter);
                        this.initTime = performance.now() - startTime;

                        console.log(`[WebGPU Manager] Successfully initialized WebGPU`);
                        console.log(`[WebGPU Manager] GPU Info:`, this.gpuInfo);

                        return {
                            engine: 'WebGPU',
                            isPreferred: true,
                            isFallback: false,
                            gpuInfo: this.gpuInfo,
                            initTime: this.initTime
                        };
                    }
                } catch (error) {
                    console.warn(`[WebGPU Manager] WebGPU initialization failed:`, error);
                }
            } else {
                console.log(`[WebGPU Manager] WebGPU not supported: ${webGpuSupport.reason}`);
            }

            // Fallback to WebGL if WebGPU fails and we're in Auto mode
            if (enginePreference === 'Auto') {
                this.activeEngine = 'WebGL';
                this.isFallback = true;
                this.initTime = performance.now() - startTime;

                console.log(`[WebGPU Manager] Falling back to WebGL`);

                return {
                    engine: 'WebGL',
                    isPreferred: false,
                    isFallback: true,
                    gpuInfo: await this.getWebGLInfo(),
                    initTime: this.initTime
                };
            } else {
                // WebGPU was forced but failed
                throw new Error('WebGPU initialization failed and fallback is disabled');
            }
        }

        // Default to WebGL
        this.activeEngine = 'WebGL';
        this.isFallback = false;
        this.initTime = performance.now() - startTime;

        return {
            engine: 'WebGL',
            isPreferred: true,
            isFallback: false,
            gpuInfo: await this.getWebGLInfo(),
            initTime: this.initTime
        };
    }

    /**
     * Gets WebGPU adapter information
     * @param {GPUAdapter} adapter - GPU adapter
     * @returns {Promise<Object>} GPU information
     */
    async getWebGPUInfo(adapter) {
        try {
            const info = await adapter.requestAdapterInfo();
            return {
                vendor: info.vendor || 'Unknown',
                renderer: info.description || 'WebGPU Renderer',
                architecture: info.architecture || 'Unknown'
            };
        } catch (error) {
            return {
                vendor: 'Unknown',
                renderer: 'WebGPU Renderer',
                architecture: 'Unknown'
            };
        }
    }

    /**
     * Gets WebGL renderer information
     * @returns {Promise<Object>} GPU information
     */
    async getWebGLInfo() {
        try {
            const canvas = document.createElement('canvas');
            const gl = canvas.getContext('webgl2') || canvas.getContext('webgl');

            if (!gl) {
                return {
                    vendor: 'Unknown',
                    renderer: 'WebGL (no context)',
                    architecture: 'Unknown'
                };
            }

            const debugInfo = gl.getExtension('WEBGL_debug_renderer_info');
            let vendor = 'Unknown';
            let renderer = 'WebGL';

            if (debugInfo) {
                vendor = gl.getParameter(debugInfo.UNMASKED_VENDOR_WEBGL) || 'Unknown';
                renderer = gl.getParameter(debugInfo.UNMASKED_RENDERER_WEBGL) || 'WebGL';
            }

            return {
                vendor,
                renderer,
                architecture: 'WebGL'
            };
        } catch (error) {
            return {
                vendor: 'Unknown',
                renderer: 'WebGL',
                architecture: 'Unknown'
            };
        }
    }

    /**
     * Starts performance monitoring
     */
    startMonitoring() {
        this.performanceMonitor.start();
    }

    /**
     * Stops performance monitoring
     */
    stopMonitoring() {
        this.performanceMonitor.stop();
    }

    /**
     * Gets current renderer information
     * @returns {Object} Renderer info with FPS
     */
    getRendererInfo() {
        return {
            engine: this.activeEngine,
            isPreferred: !this.isFallback,
            isFallback: this.isFallback,
            fps: this.performanceMonitor.getFps(),
            gpuVendor: this.gpuInfo?.vendor || 'Unknown',
            gpuRenderer: this.gpuInfo?.renderer || 'Unknown',
            initTime: this.initTime
        };
    }

    /**
     * Gets the active rendering engine
     * @returns {string} 'WebGPU' or 'WebGL'
     */
    getActiveEngine() {
        return this.activeEngine;
    }

    /**
     * Checks if the current engine is the preferred one
     * @returns {boolean} True if using preferred engine
     */
    isUsingPreferredEngine() {
        return !this.isFallback;
    }
}

/**
 * Creates a configured MapLibre map with optimal renderer
 * @param {HTMLElement} container - Map container element
 * @param {Object} options - Map options including renderingEngine preference
 * @param {string} options.renderingEngine - 'Auto', 'WebGPU', or 'WebGL'
 * @returns {Promise<Object>} Map instance with renderer info
 */
export async function createMapWithRenderer(container, options) {
    const manager = new WebGpuRendererManager();

    // Initialize renderer
    const rendererInfo = await manager.initialize(options.renderingEngine || 'Auto');

    console.log(`[WebGPU Manager] Renderer initialized:`, rendererInfo);

    // Start performance monitoring
    manager.startMonitoring();

    return {
        manager,
        rendererInfo
    };
}

// Export for standalone use
export default {
    detectWebGpuSupport,
    getGpuAdapterInfo,
    WebGpuRendererManager,
    createMapWithRenderer
};
