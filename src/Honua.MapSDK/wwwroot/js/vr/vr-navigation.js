/**
 * VR Navigation - Handles locomotion in VR space
 * Supports teleportation, smooth locomotion, and grab-move
 */

import * as THREE from 'three';

export class VRNavigation {
    constructor(scene, camera, referenceSpace) {
        this.scene = scene;
        this.camera = camera;
        this.referenceSpace = referenceSpace;
        this.mode = 'teleport'; // teleport, smooth, grab-move
        this.movementSpeed = 2.0; // m/s
        this.snapTurnAngle = 45; // degrees
        this.heightOffset = 0; // Additional height above floor

        // Teleport visualization
        this.teleportMarker = null;
        this.teleportCurve = null;
        this.validTeleportLocation = false;

        // Movement state
        this.isMoving = false;
        this.moveDirection = new THREE.Vector3();
        this.grabStartPosition = null;
        this.grabOffset = new THREE.Vector3();

        this._initializeTeleportMarker();
    }

    /**
     * Sets the locomotion mode
     */
    setMode(mode) {
        if (['teleport', 'smooth', 'grab-move'].includes(mode)) {
            this.mode = mode;
            console.log('Locomotion mode set to:', mode);
        }
    }

    /**
     * Sets movement speed for smooth locomotion
     */
    setMovementSpeed(speed) {
        this.movementSpeed = speed;
    }

    /**
     * Sets snap turn angle
     */
    setSnapTurnAngle(angle) {
        this.snapTurnAngle = angle;
    }

    /**
     * Updates navigation based on controller input
     */
    update(deltaTime, controllerState) {
        switch (this.mode) {
            case 'teleport':
                this._updateTeleport(controllerState);
                break;

            case 'smooth':
                this._updateSmoothLocomotion(deltaTime, controllerState);
                break;

            case 'grab-move':
                this._updateGrabMove(controllerState);
                break;
        }
    }

    /**
     * Teleports user to specified position
     */
    teleportTo(position) {
        if (!this.referenceSpace) return;

        // Create offset reference space
        const offsetPosition = {
            x: -position.x,
            y: -position.y - this.heightOffset,
            z: -position.z
        };

        const offsetTransform = new XRRigidTransform(offsetPosition);

        // Update reference space (this moves the user)
        this.referenceSpace = this.referenceSpace.getOffsetReferenceSpace(offsetTransform);

        console.log('Teleported to:', position);
        this._hideTeleportMarker();
    }

    /**
     * Performs snap turn rotation
     */
    snapTurn(direction) {
        if (!this.referenceSpace) return;

        const angleRadians = (direction * this.snapTurnAngle) * Math.PI / 180;
        const rotation = new THREE.Quaternion();
        rotation.setFromAxisAngle(new THREE.Vector3(0, 1, 0), angleRadians);

        const offsetTransform = new XRRigidTransform(
            { x: 0, y: 0, z: 0 },
            { x: rotation.x, y: rotation.y, z: rotation.z, w: rotation.w }
        );

        this.referenceSpace = this.referenceSpace.getOffsetReferenceSpace(offsetTransform);
        console.log('Snap turned:', direction > 0 ? 'right' : 'left');
    }

    /**
     * Adjusts height (flying mode)
     */
    adjustHeight(deltaY) {
        this.heightOffset += deltaY;

        const offsetTransform = new XRRigidTransform({
            x: 0,
            y: -deltaY,
            z: 0
        });

        this.referenceSpace = this.referenceSpace.getOffsetReferenceSpace(offsetTransform);
    }

    // Private methods

    _initializeTeleportMarker() {
        // Create teleport destination marker
        const geometry = new THREE.CylinderGeometry(0.5, 0.5, 0.1, 32);
        const material = new THREE.MeshBasicMaterial({
            color: 0x00ff00,
            transparent: true,
            opacity: 0.6
        });
        this.teleportMarker = new THREE.Mesh(geometry, material);
        this.teleportMarker.visible = false;
        this.scene.addObject(this.teleportMarker);
    }

    _updateTeleport(controllerState) {
        const rightController = controllerState.right;
        if (!rightController) return;

        if (rightController.isSelecting) {
            // Show teleport marker and calculate destination
            const targetPosition = this._calculateTeleportDestination(rightController.pose);

            if (targetPosition) {
                this._showTeleportMarker(targetPosition);
                this.validTeleportLocation = true;
            } else {
                this._hideTeleportMarker();
                this.validTeleportLocation = false;
            }
        } else {
            // Controller released - execute teleport if valid
            if (this.validTeleportLocation && this.teleportMarker.visible) {
                this.teleportTo(this.teleportMarker.position);
            }
            this._hideTeleportMarker();
        }
    }

    _updateSmoothLocomotion(deltaTime, controllerState) {
        const rightController = controllerState.right;
        if (!rightController || !rightController.gamepad) return;

        // Get thumbstick input
        const axes = rightController.gamepad.axes;
        if (axes && axes.length >= 2) {
            const x = axes[0]; // Left/right
            const y = axes[1]; // Forward/back

            if (Math.abs(x) > 0.1 || Math.abs(y) > 0.1) {
                // Calculate movement direction based on head orientation
                const headOrientation = this.camera.quaternion;
                const forward = new THREE.Vector3(0, 0, -1).applyQuaternion(headOrientation);
                const right = new THREE.Vector3(1, 0, 0).applyQuaternion(headOrientation);

                // Flatten to XZ plane
                forward.y = 0;
                right.y = 0;
                forward.normalize();
                right.normalize();

                // Calculate move direction
                this.moveDirection.set(0, 0, 0);
                this.moveDirection.addScaledVector(forward, -y);
                this.moveDirection.addScaledVector(right, x);
                this.moveDirection.normalize();

                // Apply movement
                const movement = this.moveDirection.multiplyScalar(this.movementSpeed * deltaTime);

                const offsetTransform = new XRRigidTransform({
                    x: -movement.x,
                    y: 0,
                    z: -movement.z
                });

                this.referenceSpace = this.referenceSpace.getOffsetReferenceSpace(offsetTransform);
            }
        }
    }

    _updateGrabMove(controllerState) {
        const rightController = controllerState.right;
        if (!rightController) return;

        if (rightController.isGripping) {
            if (!this.grabStartPosition) {
                // Start grab
                this.grabStartPosition = rightController.pose.transform.position;
            } else {
                // Calculate movement from grab
                const currentPosition = rightController.pose.transform.position;
                const delta = {
                    x: this.grabStartPosition.x - currentPosition.x,
                    y: this.grabStartPosition.y - currentPosition.y,
                    z: this.grabStartPosition.z - currentPosition.z
                };

                const offsetTransform = new XRRigidTransform(delta);
                this.referenceSpace = this.referenceSpace.getOffsetReferenceSpace(offsetTransform);

                this.grabStartPosition = currentPosition;
            }
        } else {
            // Release grab
            this.grabStartPosition = null;
        }
    }

    _calculateTeleportDestination(controllerPose) {
        if (!controllerPose) return null;

        // Cast ray from controller
        const origin = new THREE.Vector3(
            controllerPose.transform.position.x,
            controllerPose.transform.position.y,
            controllerPose.transform.position.z
        );

        const orientation = controllerPose.transform.orientation;
        const forward = new THREE.Vector3(0, 0, -1);
        const quaternion = new THREE.Quaternion(
            orientation.x,
            orientation.y,
            orientation.z,
            orientation.w
        );
        forward.applyQuaternion(quaternion);

        // Simple parabolic arc for teleport
        const maxDistance = 10;
        const targetPoint = origin.clone().add(forward.multiplyScalar(maxDistance));

        // Clamp to floor level
        targetPoint.y = 0;

        return targetPoint;
    }

    _showTeleportMarker(position) {
        this.teleportMarker.position.copy(position);
        this.teleportMarker.visible = true;
        this.teleportMarker.material.color.setHex(0x00ff00); // Green for valid
    }

    _hideTeleportMarker() {
        this.teleportMarker.visible = false;
    }
}

// Export for module usage
window.VRNavigation = VRNavigation;
export default VRNavigation;
