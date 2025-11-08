// Honua Geocoding Search JavaScript Module
// Handles keyboard navigation, autocomplete, and map marker integration

// Store for search control instances
const searchInstances = new Map();

// Store for map instances (to add markers)
const mapInstances = new Map();

/**
 * Initializes keyboard navigation for a geocoding search control
 * @param {HTMLElement} container - The search control container
 * @param {DotNetObjectReference} dotNetRef - Reference to .NET component
 */
export function initializeKeyboardNavigation(container, dotNetRef) {
    const searchId = generateId();

    searchInstances.set(searchId, {
        container,
        dotNetRef,
        input: container.querySelector('.search-input'),
        dropdown: null
    });

    // Setup event listeners
    setupClickOutsideListener(searchId);
    setupDropdownPositioning(searchId);

    return searchId;
}

/**
 * Adds a marker to the map at the specified location
 * @param {string} mapId - The map ID
 * @param {number} longitude - Longitude coordinate
 * @param {number} latitude - Latitude coordinate
 * @param {string} label - Marker label/popup text
 * @param {boolean} clearPrevious - Whether to clear previous markers
 */
export function addMarker(mapId, longitude, latitude, label, clearPrevious = true) {
    // Try to get the map instance from the global scope
    const mapElement = document.getElementById(mapId);
    if (!mapElement) {
        console.warn(`Map element not found: ${mapId}`);
        return;
    }

    // Get the MapLibre map instance
    const map = window.honuaMaps?.get(mapId);
    if (!map) {
        console.warn(`Map instance not found: ${mapId}`);
        return;
    }

    // Clear previous markers if requested
    if (clearPrevious) {
        clearMarkers(mapId);
    }

    // Create marker element safely without innerHTML to prevent XSS
    const markerElement = document.createElement('div');
    markerElement.className = 'geocoding-search-marker';

    // Create SVG element using DOM methods
    const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    svg.setAttribute('width', '32');
    svg.setAttribute('height', '40');
    svg.setAttribute('viewBox', '0 0 32 40');

    const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
    path.setAttribute('d', 'M16 0C7.2 0 0 7.2 0 16c0 8.8 16 24 16 24s16-15.2 16-24c0-8.8-7.2-16-16-16z');
    path.setAttribute('fill', '#1976d2');
    path.setAttribute('stroke', 'white');
    path.setAttribute('stroke-width', '2');

    const circle = document.createElementNS('http://www.w3.org/2000/svg', 'circle');
    circle.setAttribute('cx', '16');
    circle.setAttribute('cy', '16');
    circle.setAttribute('r', '6');
    circle.setAttribute('fill', 'white');

    svg.appendChild(path);
    svg.appendChild(circle);
    markerElement.appendChild(svg);
    markerElement.style.cursor = 'pointer';

    // Create popup
    const popup = new maplibregl.Popup({ offset: 25 })
        .setHTML(`<div style="padding: 8px; font-size: 14px;">${escapeHtml(label)}</div>`);

    // Create and add marker
    const marker = new maplibregl.Marker({ element: markerElement })
        .setLngLat([longitude, latitude])
        .setPopup(popup)
        .addTo(map);

    // Store marker for later removal
    if (!mapInstances.has(mapId)) {
        mapInstances.set(mapId, { markers: [] });
    }
    mapInstances.get(mapId).markers.push(marker);

    // Show popup briefly
    marker.togglePopup();
}

/**
 * Clears all geocoding markers from the map
 * @param {string} mapId - The map ID
 */
export function clearMarkers(mapId) {
    const instance = mapInstances.get(mapId);
    if (!instance || !instance.markers) return;

    instance.markers.forEach(marker => marker.remove());
    instance.markers = [];
}

/**
 * Copies text to clipboard
 * @param {string} text - Text to copy
 */
export function copyToClipboard(text) {
    if (navigator.clipboard && navigator.clipboard.writeText) {
        return navigator.clipboard.writeText(text);
    } else {
        // Fallback for older browsers
        const textarea = document.createElement('textarea');
        textarea.value = text;
        textarea.style.position = 'fixed';
        textarea.style.opacity = '0';
        document.body.appendChild(textarea);
        textarea.select();

        try {
            document.execCommand('copy');
        } catch (err) {
            console.error('Failed to copy text:', err);
        } finally {
            document.body.removeChild(textarea);
        }
    }
}

/**
 * Focuses the search input
 * @param {HTMLElement} container - The search control container
 */
export function focusInput(container) {
    const input = container.querySelector('.search-input');
    if (input) {
        input.focus();
    }
}

/**
 * Sets up click-outside-to-close functionality
 * @param {string} searchId - The search instance ID
 */
function setupClickOutsideListener(searchId) {
    const instance = searchInstances.get(searchId);
    if (!instance) return;

    const handleClickOutside = (event) => {
        const dropdown = instance.container.querySelector('.search-results-dropdown');
        if (dropdown &&
            !instance.container.contains(event.target)) {
            dropdown.style.display = 'none';
        }
    };

    document.addEventListener('click', handleClickOutside);

    // Store cleanup function
    instance.cleanup = () => {
        document.removeEventListener('click', handleClickOutside);
    };
}

/**
 * Sets up dropdown positioning and scroll behavior
 * @param {string} searchId - The search instance ID
 */
function setupDropdownPositioning(searchId) {
    const instance = searchInstances.get(searchId);
    if (!instance) return;

    // Use MutationObserver to detect when dropdown is added/shown
    const observer = new MutationObserver((mutations) => {
        mutations.forEach((mutation) => {
            mutation.addedNodes.forEach((node) => {
                if (node.classList && node.classList.contains('search-results-dropdown')) {
                    positionDropdown(instance.container, node);
                }
            });
        });
    });

    observer.observe(instance.container, {
        childList: true,
        subtree: true
    });

    // Store observer for cleanup
    instance.observer = observer;
}

/**
 * Positions the dropdown to avoid overflow
 * @param {HTMLElement} container - The search control container
 * @param {HTMLElement} dropdown - The dropdown element
 */
function positionDropdown(container, dropdown) {
    const rect = container.getBoundingClientRect();
    const viewportHeight = window.innerHeight;
    const spaceBelow = viewportHeight - rect.bottom;
    const spaceAbove = rect.top;

    // If not enough space below but more space above, position above
    if (spaceBelow < 300 && spaceAbove > spaceBelow) {
        dropdown.style.top = 'auto';
        dropdown.style.bottom = '100%';
        dropdown.style.marginTop = '0';
        dropdown.style.marginBottom = '4px';
    } else {
        dropdown.style.top = '100%';
        dropdown.style.bottom = 'auto';
        dropdown.style.marginTop = '4px';
        dropdown.style.marginBottom = '0';
    }
}

/**
 * Scrolls the selected result item into view
 * @param {HTMLElement} container - The search control container
 * @param {number} selectedIndex - The index of the selected item
 */
export function scrollSelectedIntoView(container, selectedIndex) {
    const dropdown = container.querySelector('.search-results-dropdown');
    if (!dropdown) return;

    const items = dropdown.querySelectorAll('.result-item');
    if (selectedIndex >= 0 && selectedIndex < items.length) {
        const item = items[selectedIndex];
        const dropdownRect = dropdown.getBoundingClientRect();
        const itemRect = item.getBoundingClientRect();

        if (itemRect.bottom > dropdownRect.bottom) {
            item.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
        } else if (itemRect.top < dropdownRect.top) {
            item.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
        }
    }
}

/**
 * Highlights text matching the query in result items
 * @param {string} text - The text to highlight
 * @param {string} query - The search query
 * @returns {string} HTML with highlighted matches
 */
export function highlightMatches(text, query) {
    if (!query || !text) return escapeHtml(text);

    const escapedQuery = escapeRegExp(query);
    const regex = new RegExp(`(${escapedQuery})`, 'gi');
    const escapedText = escapeHtml(text);

    return escapedText.replace(regex, '<mark>$1</mark>');
}

/**
 * Generates a unique ID
 * @returns {string} Unique ID
 */
function generateId() {
    return `search-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
}

/**
 * Escapes HTML to prevent XSS
 * @param {string} text - Text to escape
 * @returns {string} Escaped text
 */
function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
}

/**
 * Escapes special regex characters
 * @param {string} text - Text to escape
 * @returns {string} Escaped text
 */
function escapeRegExp(text) {
    return text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

/**
 * Gets or creates the global map registry
 */
function ensureMapRegistry() {
    if (!window.honuaMaps) {
        window.honuaMaps = new Map();
    }
}

// Initialize map registry
ensureMapRegistry();

// Cleanup on module unload
export function dispose(searchId) {
    const instance = searchInstances.get(searchId);
    if (instance) {
        if (instance.cleanup) {
            instance.cleanup();
        }
        if (instance.observer) {
            instance.observer.disconnect();
        }
        searchInstances.delete(searchId);
    }
}

// Export for debugging
if (typeof window !== 'undefined') {
    window.HonuaGeocodingSearch = {
        searchInstances,
        mapInstances,
        addMarker,
        clearMarkers,
        copyToClipboard
    };
}
