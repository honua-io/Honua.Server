/**
 * Honua Fullscreen JavaScript Module
 * Handles fullscreen API and keyboard shortcuts
 */

let fullscreenTarget = null;
let dotNetRef = null;
let keyboardShortcutsEnabled = false;
let fullscreenChangeHandler = null;
let keydownHandler = null;

/**
 * Check if fullscreen API is supported
 * @returns {boolean} True if fullscreen is supported
 */
export function isFullscreenSupported() {
    return !!(
        document.fullscreenEnabled ||
        document.webkitFullscreenEnabled ||
        document.mozFullScreenEnabled ||
        document.msFullscreenEnabled
    );
}

/**
 * Initialize fullscreen functionality
 * @param {string} targetElementId - ID of element to make fullscreen
 * @param {object} dotNetReference - .NET object reference for callbacks
 * @param {boolean} enableKeyboardShortcuts - Enable F11 and Esc shortcuts
 */
export function initializeFullscreen(targetElementId, dotNetReference, enableKeyboardShortcuts = true) {
    try {
        dotNetRef = dotNetReference;
        keyboardShortcutsEnabled = enableKeyboardShortcuts;

        // Set up fullscreen change event listener
        fullscreenChangeHandler = () => handleFullscreenChange();

        document.addEventListener('fullscreenchange', fullscreenChangeHandler);
        document.addEventListener('webkitfullscreenchange', fullscreenChangeHandler);
        document.addEventListener('mozfullscreenchange', fullscreenChangeHandler);
        document.addEventListener('msfullscreenchange', fullscreenChangeHandler);

        // Set up keyboard shortcuts
        if (enableKeyboardShortcuts) {
            keydownHandler = (e) => handleKeydown(e, targetElementId);
            document.addEventListener('keydown', keydownHandler);
        }

        console.log('Fullscreen initialized for:', targetElementId);
        return true;
    } catch (error) {
        console.error('Error initializing fullscreen:', error);
        return false;
    }
}

/**
 * Toggle fullscreen mode
 * @param {string} targetElementId - ID of element to make fullscreen
 * @returns {Promise<boolean>} Success status
 */
export async function toggleFullscreen(targetElementId) {
    try {
        if (isCurrentlyFullscreen()) {
            return await exitFullscreen();
        } else {
            return await enterFullscreen(targetElementId);
        }
    } catch (error) {
        console.error('Error toggling fullscreen:', error);
        return false;
    }
}

/**
 * Enter fullscreen mode
 * @param {string} targetElementId - ID of element to make fullscreen
 * @returns {Promise<boolean>} Success status
 */
export async function enterFullscreen(targetElementId) {
    try {
        if (!isFullscreenSupported()) {
            console.warn('Fullscreen API not supported');
            return false;
        }

        if (isCurrentlyFullscreen()) {
            console.log('Already in fullscreen mode');
            return true;
        }

        // Get target element
        const element = targetElementId ? document.getElementById(targetElementId) : document.documentElement;

        if (!element) {
            console.error('Target element not found:', targetElementId);
            return false;
        }

        fullscreenTarget = element;

        // Request fullscreen using appropriate API
        if (element.requestFullscreen) {
            await element.requestFullscreen();
        } else if (element.webkitRequestFullscreen) {
            await element.webkitRequestFullscreen();
        } else if (element.mozRequestFullScreen) {
            await element.mozRequestFullScreen();
        } else if (element.msRequestFullscreen) {
            await element.msRequestFullscreen();
        } else {
            console.error('No fullscreen method available');
            return false;
        }

        console.log('Entered fullscreen mode');
        return true;
    } catch (error) {
        console.error('Error entering fullscreen:', error);
        return false;
    }
}

/**
 * Exit fullscreen mode
 * @returns {Promise<boolean>} Success status
 */
export async function exitFullscreen() {
    try {
        if (!isCurrentlyFullscreen()) {
            console.log('Not in fullscreen mode');
            return true;
        }

        // Exit fullscreen using appropriate API
        if (document.exitFullscreen) {
            await document.exitFullscreen();
        } else if (document.webkitExitFullscreen) {
            await document.webkitExitFullscreen();
        } else if (document.mozCancelFullScreen) {
            await document.mozCancelFullScreen();
        } else if (document.msExitFullscreen) {
            await document.msExitFullscreen();
        } else {
            console.error('No exit fullscreen method available');
            return false;
        }

        fullscreenTarget = null;
        console.log('Exited fullscreen mode');
        return true;
    } catch (error) {
        console.error('Error exiting fullscreen:', error);
        return false;
    }
}

/**
 * Check if currently in fullscreen mode
 * @returns {boolean} True if in fullscreen
 */
export function isCurrentlyFullscreen() {
    return !!(
        document.fullscreenElement ||
        document.webkitFullscreenElement ||
        document.mozFullScreenElement ||
        document.msFullscreenElement
    );
}

/**
 * Get current fullscreen element
 * @returns {Element|null} Current fullscreen element or null
 */
export function getFullscreenElement() {
    return (
        document.fullscreenElement ||
        document.webkitFullscreenElement ||
        document.mozFullScreenElement ||
        document.msFullscreenElement ||
        null
    );
}

/**
 * Handle fullscreen change events
 */
function handleFullscreenChange() {
    const isFullscreen = isCurrentlyFullscreen();

    // Notify .NET component
    if (dotNetRef) {
        try {
            dotNetRef.invokeMethodAsync('OnFullscreenChange', isFullscreen);
        } catch (error) {
            console.error('Error notifying .NET about fullscreen change:', error);
        }
    }

    // Update map size if it's a map container
    const fullscreenElement = getFullscreenElement();
    if (fullscreenElement) {
        // Try to find and resize MapLibre map
        const map = fullscreenElement._map;
        if (map && typeof map.resize === 'function') {
            // Small delay to ensure DOM has updated
            setTimeout(() => {
                map.resize();
                console.log('Map resized after fullscreen change');
            }, 100);
        }

        // Try to find Leaflet map
        const leafletMap = fullscreenElement._leafletMap;
        if (leafletMap && typeof leafletMap.invalidateSize === 'function') {
            setTimeout(() => {
                leafletMap.invalidateSize();
                console.log('Leaflet map resized after fullscreen change');
            }, 100);
        }
    } else if (!isFullscreen) {
        // Exited fullscreen, try to resize the target map
        if (fullscreenTarget) {
            const map = fullscreenTarget._map;
            if (map && typeof map.resize === 'function') {
                setTimeout(() => {
                    map.resize();
                }, 100);
            }

            const leafletMap = fullscreenTarget._leafletMap;
            if (leafletMap && typeof leafletMap.invalidateSize === 'function') {
                setTimeout(() => {
                    leafletMap.invalidateSize();
                }, 100);
            }
        }
    }

    console.log('Fullscreen state changed:', isFullscreen);
}

/**
 * Handle keyboard shortcuts
 * @param {KeyboardEvent} e - Keyboard event
 * @param {string} targetElementId - Target element ID
 */
function handleKeydown(e, targetElementId) {
    // F11 key - toggle fullscreen
    if (e.key === 'F11') {
        e.preventDefault();
        toggleFullscreen(targetElementId);
    }

    // Escape key - exit fullscreen (only if we're in fullscreen)
    if (e.key === 'Escape' && isCurrentlyFullscreen()) {
        // Let the browser handle Escape naturally, but we can do additional cleanup
        console.log('Escape pressed in fullscreen mode');
    }
}

/**
 * Enable keyboard shortcuts
 * @param {string} targetElementId - Target element ID
 */
export function enableKeyboardShortcuts(targetElementId) {
    if (!keydownHandler) {
        keydownHandler = (e) => handleKeydown(e, targetElementId);
        document.addEventListener('keydown', keydownHandler);
        keyboardShortcutsEnabled = true;
        console.log('Keyboard shortcuts enabled');
    }
}

/**
 * Disable keyboard shortcuts
 */
export function disableKeyboardShortcuts() {
    if (keydownHandler) {
        document.removeEventListener('keydown', keydownHandler);
        keydownHandler = null;
        keyboardShortcutsEnabled = false;
        console.log('Keyboard shortcuts disabled');
    }
}

/**
 * Get fullscreen API vendor prefixes
 * @returns {object} Object containing the correct API methods
 */
export function getFullscreenAPI() {
    return {
        requestMethod:
            HTMLElement.prototype.requestFullscreen ||
            HTMLElement.prototype.webkitRequestFullscreen ||
            HTMLElement.prototype.mozRequestFullScreen ||
            HTMLElement.prototype.msRequestFullscreen,
        exitMethod:
            Document.prototype.exitFullscreen ||
            Document.prototype.webkitExitFullscreen ||
            Document.prototype.mozCancelFullScreen ||
            Document.prototype.msExitFullscreen,
        element:
            document.fullscreenElement ||
            document.webkitFullscreenElement ||
            document.mozFullScreenElement ||
            document.msFullscreenElement,
        changeEvent:
            'fullscreenchange' in document ? 'fullscreenchange' :
            'webkitfullscreenchange' in document ? 'webkitfullscreenchange' :
            'mozfullscreenchange' in document ? 'mozfullscreenchange' :
            'msfullscreenchange'
    };
}

/**
 * Cleanup and remove event listeners
 */
export function cleanup() {
    try {
        // Remove fullscreen change listeners
        if (fullscreenChangeHandler) {
            document.removeEventListener('fullscreenchange', fullscreenChangeHandler);
            document.removeEventListener('webkitfullscreenchange', fullscreenChangeHandler);
            document.removeEventListener('mozfullscreenchange', fullscreenChangeHandler);
            document.removeEventListener('msfullscreenchange', fullscreenChangeHandler);
            fullscreenChangeHandler = null;
        }

        // Remove keyboard listeners
        if (keydownHandler) {
            document.removeEventListener('keydown', keydownHandler);
            keydownHandler = null;
        }

        // Clear references
        dotNetRef = null;
        fullscreenTarget = null;
        keyboardShortcutsEnabled = false;

        console.log('Fullscreen module cleaned up');
    } catch (error) {
        console.error('Error cleaning up fullscreen module:', error);
    }
}
