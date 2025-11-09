/**
 * VR Session Manager - Handles WebXR session lifecycle
 * Supports Meta Quest 2/3/Pro, HTC Vive, Valve Index, and other WebXR-compatible headsets
 */

export class VRSessionManager {
    constructor() {
        this.session = null;
        this.referenceSpace = null;
        this.gl = null;
        this.xrButton = null;
        this.isVRSupported = false;
        this.frameCallback = null;
        this.sessionCallbacks = {
            onSessionStart: null,
            onSessionEnd: null,
            onFrame: null
        };
    }

    /**
     * Checks if WebXR is supported in the current browser
     */
    async checkVRSupport() {
        if (!navigator.xr) {
            console.warn('WebXR not supported in this browser');
            this.isVRSupported = false;
            return false;
        }

        try {
            this.isVRSupported = await navigator.xr.isSessionSupported('immersive-vr');
            console.log('VR Support:', this.isVRSupported);
            return this.isVRSupported;
        } catch (error) {
            console.error('Error checking VR support:', error);
            this.isVRSupported = false;
            return false;
        }
    }

    /**
     * Enters VR session with specified configuration
     */
    async enterVRSession(config = {}) {
        if (!this.isVRSupported) {
            throw new Error('VR not supported on this device');
        }

        const {
            referenceSpaceType = 'local-floor',
            requiredFeatures = ['local-floor'],
            optionalFeatures = ['bounded-floor', 'hand-tracking'],
            canvas = null
        } = config;

        try {
            // Request VR session
            this.session = await navigator.xr.requestSession('immersive-vr', {
                requiredFeatures,
                optionalFeatures
            });

            console.log('VR Session started:', this.session);

            // Get or create canvas
            const vrCanvas = canvas || this._createCanvas();

            // Initialize WebGL context
            this.gl = vrCanvas.getContext('webgl2', {
                xrCompatible: true,
                alpha: false,
                antialias: true
            });

            if (!this.gl) {
                throw new Error('Failed to create WebGL2 context');
            }

            // Make context XR compatible
            await this.gl.makeXRCompatible();

            // Set up reference space
            this.referenceSpace = await this.session.requestReferenceSpace(referenceSpaceType);

            // Set up session event handlers
            this.session.addEventListener('end', () => this._onSessionEnd());

            // Notify session start
            if (this.sessionCallbacks.onSessionStart) {
                this.sessionCallbacks.onSessionStart(this.session);
            }

            // Start render loop
            this.session.requestAnimationFrame((time, frame) => this._onXRFrame(time, frame));

            return this.session;

        } catch (error) {
            console.error('Failed to enter VR session:', error);
            throw error;
        }
    }

    /**
     * Exits the current VR session
     */
    async exitVRSession() {
        if (this.session) {
            await this.session.end();
            this.session = null;
            this.referenceSpace = null;
            console.log('VR Session ended');
        }
    }

    /**
     * Sets callback functions for session events
     */
    setCallbacks(callbacks) {
        this.sessionCallbacks = { ...this.sessionCallbacks, ...callbacks };
    }

    /**
     * Gets the current session state
     */
    getSessionState() {
        return {
            isActive: this.session !== null,
            referenceSpace: this.referenceSpace,
            inputSources: this.session ? Array.from(this.session.inputSources) : []
        };
    }

    /**
     * Checks if specific feature is supported
     */
    async isFeatureSupported(feature) {
        if (!navigator.xr) return false;

        try {
            const session = await navigator.xr.requestSession('immersive-vr', {
                requiredFeatures: [feature]
            });
            await session.end();
            return true;
        } catch {
            return false;
        }
    }

    // Private methods

    _createCanvas() {
        let canvas = document.getElementById('vr-canvas');
        if (!canvas) {
            canvas = document.createElement('canvas');
            canvas.id = 'vr-canvas';
            document.body.appendChild(canvas);
        }
        return canvas;
    }

    _onXRFrame(time, frame) {
        if (!this.session) return;

        // Schedule next frame
        this.session.requestAnimationFrame((t, f) => this._onXRFrame(t, f));

        // Get pose
        const pose = frame.getViewerPose(this.referenceSpace);
        if (!pose) return;

        // Call frame callback
        if (this.sessionCallbacks.onFrame) {
            this.sessionCallbacks.onFrame(time, frame, pose);
        }
    }

    _onSessionEnd() {
        this.session = null;
        this.referenceSpace = null;

        if (this.sessionCallbacks.onSessionEnd) {
            this.sessionCallbacks.onSessionEnd();
        }

        console.log('VR Session ended');
    }
}

// Export convenience functions
export async function checkVRSupport() {
    const manager = new VRSessionManager();
    return await manager.checkVRSupport();
}

export async function enterVRSession(config) {
    const manager = new VRSessionManager();
    await manager.checkVRSupport();
    return await manager.enterVRSession(config);
}

// Global instance for module usage
window.VRSessionManager = VRSessionManager;
