/**
 * VR Controller Manager - Handles VR controller input and interaction
 * Supports Quest Touch controllers, Vive wands, Index knuckles, etc.
 */

export class VRControllerManager {
    constructor(session, scene) {
        this.session = session;
        this.scene = scene;
        this.controllers = new Map();
        this.hapticActuators = new Map();
        this.raycastEnabled = true;
        this.selectedObject = null;

        // Controller button mappings
        this.buttonMappings = {
            trigger: 0,      // Primary trigger (select)
            grip: 1,         // Grip button
            thumbstick: 2,   // Thumbstick click
            buttonA: 3,      // A/X button
            buttonB: 4,      // B/Y button
            thumbstickX: 2,  // Thumbstick X axis
            thumbstickY: 3   // Thumbstick Y axis
        };

        this.inputCallbacks = {
            onSelectStart: null,
            onSelectEnd: null,
            onGripStart: null,
            onGripEnd: null,
            onThumbstick: null,
            onButtonA: null,
            onButtonB: null
        };

        this._initializeControllers();
    }

    /**
     * Initializes controller tracking
     */
    _initializeControllers() {
        // Listen for connected controllers
        this.session.addEventListener('inputsourceschange', (event) => {
            this._handleInputSourcesChange(event);
        });

        // Initialize existing input sources
        this.session.inputSources.forEach(source => {
            this._addInputSource(source);
        });
    }

    /**
     * Updates controller state each frame
     */
    update(frame, referenceSpace) {
        if (!this.session) return;

        for (const source of this.session.inputSources) {
            if (!source.gripSpace) continue;

            // Get controller pose
            const pose = frame.getPose(source.gripSpace, referenceSpace);
            if (!pose) continue;

            // Update controller visualization
            this._updateControllerPose(source, pose);

            // Process input
            this._processGamepadInput(source);

            // Perform raycasting if enabled
            if (this.raycastEnabled) {
                this._performRaycast(source, pose, referenceSpace);
            }
        }
    }

    /**
     * Sets input callback functions
     */
    setCallbacks(callbacks) {
        this.inputCallbacks = { ...this.inputCallbacks, ...callbacks };
    }

    /**
     * Triggers haptic feedback on a controller
     */
    triggerHaptic(handedness, intensity = 0.5, duration = 100) {
        const actuator = this.hapticActuators.get(handedness);
        if (actuator) {
            actuator.pulse(intensity, duration);
        }
    }

    /**
     * Gets controller state for a specific hand
     */
    getControllerState(handedness) {
        return this.controllers.get(handedness) || null;
    }

    /**
     * Enables or disables ray-based selection
     */
    setRaycastEnabled(enabled) {
        this.raycastEnabled = enabled;
    }

    // Private methods

    _handleInputSourcesChange(event) {
        event.added.forEach(source => this._addInputSource(source));
        event.removed.forEach(source => this._removeInputSource(source));
    }

    _addInputSource(source) {
        const handedness = source.handedness || 'none';

        this.controllers.set(handedness, {
            source: source,
            handedness: handedness,
            targetRayMode: source.targetRayMode,
            pose: null,
            gamepad: source.gamepad,
            isSelecting: false,
            isGripping: false
        });

        // Store haptic actuator if available
        if (source.gamepad && source.gamepad.hapticActuators && source.gamepad.hapticActuators.length > 0) {
            this.hapticActuators.set(handedness, source.gamepad.hapticActuators[0]);
        }

        console.log(`Controller added: ${handedness} (${source.targetRayMode})`);
    }

    _removeInputSource(source) {
        const handedness = source.handedness || 'none';
        this.controllers.delete(handedness);
        this.hapticActuators.delete(handedness);
        console.log(`Controller removed: ${handedness}`);
    }

    _updateControllerPose(source, pose) {
        const handedness = source.handedness || 'none';
        const controller = this.controllers.get(handedness);

        if (controller) {
            controller.pose = pose;
            controller.position = pose.transform.position;
            controller.orientation = pose.transform.orientation;
        }
    }

    _processGamepadInput(source) {
        if (!source.gamepad) return;

        const handedness = source.handedness || 'none';
        const controller = this.controllers.get(handedness);
        if (!controller) return;

        const gamepad = source.gamepad;

        // Process trigger (select)
        const triggerPressed = gamepad.buttons[this.buttonMappings.trigger]?.pressed;
        if (triggerPressed && !controller.isSelecting) {
            controller.isSelecting = true;
            if (this.inputCallbacks.onSelectStart) {
                this.inputCallbacks.onSelectStart(handedness, controller);
            }
        } else if (!triggerPressed && controller.isSelecting) {
            controller.isSelecting = false;
            if (this.inputCallbacks.onSelectEnd) {
                this.inputCallbacks.onSelectEnd(handedness, controller);
            }
        }

        // Process grip
        const gripPressed = gamepad.buttons[this.buttonMappings.grip]?.pressed;
        if (gripPressed && !controller.isGripping) {
            controller.isGripping = true;
            if (this.inputCallbacks.onGripStart) {
                this.inputCallbacks.onGripStart(handedness, controller);
            }
        } else if (!gripPressed && controller.isGripping) {
            controller.isGripping = false;
            if (this.inputCallbacks.onGripEnd) {
                this.inputCallbacks.onGripEnd(handedness, controller);
            }
        }

        // Process thumbstick
        if (gamepad.axes && gamepad.axes.length >= 4) {
            const thumbstickX = gamepad.axes[this.buttonMappings.thumbstickX];
            const thumbstickY = gamepad.axes[this.buttonMappings.thumbstickY];

            if (Math.abs(thumbstickX) > 0.1 || Math.abs(thumbstickY) > 0.1) {
                if (this.inputCallbacks.onThumbstick) {
                    this.inputCallbacks.onThumbstick(handedness, thumbstickX, thumbstickY);
                }
            }
        }

        // Process A/X button
        if (gamepad.buttons[this.buttonMappings.buttonA]?.pressed) {
            if (this.inputCallbacks.onButtonA) {
                this.inputCallbacks.onButtonA(handedness);
            }
        }

        // Process B/Y button
        if (gamepad.buttons[this.buttonMappings.buttonB]?.pressed) {
            if (this.inputCallbacks.onButtonB) {
                this.inputCallbacks.onButtonB(handedness);
            }
        }
    }

    _performRaycast(source, pose, referenceSpace) {
        // This would integrate with Three.js raycaster
        // Placeholder for raycast logic
        const origin = pose.transform.position;
        const direction = this._getForwardVector(pose.transform.orientation);

        // In a full implementation, this would:
        // 1. Create a ray from controller position and orientation
        // 2. Test intersection with scene objects
        // 3. Update selectedObject
        // 4. Provide visual feedback
    }

    _getForwardVector(quaternion) {
        // Convert quaternion to forward vector
        const x = quaternion.x;
        const y = quaternion.y;
        const z = quaternion.z;
        const w = quaternion.w;

        return {
            x: 2 * (x * z + w * y),
            y: 2 * (y * z - w * x),
            z: 1 - 2 * (x * x + y * y)
        };
    }
}

// Export for module usage
window.VRControllerManager = VRControllerManager;
export default VRControllerManager;
