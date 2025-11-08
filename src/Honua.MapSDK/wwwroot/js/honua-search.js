/**
 * Honua Search JavaScript Module
 * Provides geolocation and localStorage utilities for HonuaSearch component
 */

/**
 * Get current position from browser's geolocation API
 * @returns {Promise<number[]>} [latitude, longitude]
 */
export async function getCurrentPosition() {
    return new Promise((resolve, reject) => {
        if (!navigator.geolocation) {
            reject(new Error('Geolocation is not supported by this browser'));
            return;
        }

        navigator.geolocation.getCurrentPosition(
            (position) => {
                resolve([position.coords.latitude, position.coords.longitude]);
            },
            (error) => {
                let errorMessage = 'Unable to get location';
                switch (error.code) {
                    case error.PERMISSION_DENIED:
                        errorMessage = 'Location permission denied';
                        break;
                    case error.POSITION_UNAVAILABLE:
                        errorMessage = 'Location information unavailable';
                        break;
                    case error.TIMEOUT:
                        errorMessage = 'Location request timed out';
                        break;
                }
                reject(new Error(errorMessage));
            },
            {
                enableHighAccuracy: true,
                timeout: 10000,
                maximumAge: 0
            }
        );
    });
}

/**
 * Get item from localStorage
 * @param {string} key - Storage key
 * @returns {string|null} Stored value or null
 */
export function getLocalStorage(key) {
    try {
        return localStorage.getItem(key);
    } catch (error) {
        console.error('Error reading from localStorage:', error);
        return null;
    }
}

/**
 * Set item in localStorage
 * @param {string} key - Storage key
 * @param {string} value - Value to store
 */
export function setLocalStorage(key, value) {
    try {
        localStorage.setItem(key, value);
    } catch (error) {
        console.error('Error writing to localStorage:', error);
    }
}

/**
 * Remove item from localStorage
 * @param {string} key - Storage key
 */
export function removeLocalStorage(key) {
    try {
        localStorage.removeItem(key);
    } catch (error) {
        console.error('Error removing from localStorage:', error);
    }
}

/**
 * Clear all localStorage
 */
export function clearLocalStorage() {
    try {
        localStorage.clear();
    } catch (error) {
        console.error('Error clearing localStorage:', error);
    }
}
