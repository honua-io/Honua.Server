/**
 * Copyright (c) 2025 HonuaIO
 * Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
 *
 * Honua Comments - JavaScript interop for map comment markers and interactions
 */

window.HonuaComments = {
    markers: {},

    /**
     * Renders comment markers on the map
     * @param {string} containerId - Container element ID
     * @param {Array} comments - Array of comment objects with location data
     */
    renderMarkers: function(containerId, comments) {
        const container = document.getElementById(containerId);
        if (!container) {
            console.warn(`Container ${containerId} not found`);
            return;
        }

        // Clear existing markers
        container.innerHTML = '';

        comments.forEach(comment => {
            if (!comment.longitude || !comment.latitude) return;

            const marker = this.createMarkerElement(comment);
            container.appendChild(marker);

            // Store marker reference
            if (!this.markers[containerId]) {
                this.markers[containerId] = {};
            }
            this.markers[containerId][comment.id] = marker;
        });
    },

    /**
     * Creates a marker DOM element
     */
    createMarkerElement: function(comment) {
        const marker = document.createElement('div');
        marker.className = `comment-marker status-${comment.status} priority-${comment.priority}`;
        marker.dataset.commentId = comment.id;
        marker.style.position = 'absolute';

        // Apply color
        marker.style.backgroundColor = comment.color || '#FF5733';

        // Add icon based on status
        const icon = document.createElement('i');
        icon.className = this.getIconClass(comment.status);
        marker.appendChild(icon);

        // Add priority indicator
        if (comment.priority === 'high' || comment.priority === 'critical') {
            marker.classList.add('priority-alert');
        }

        return marker;
    },

    /**
     * Gets icon class based on comment status
     */
    getIconClass: function(status) {
        switch (status) {
            case 'resolved':
                return 'icon-check';
            case 'closed':
                return 'icon-lock';
            default:
                return 'icon-comment';
        }
    },

    /**
     * Updates marker position (call this when map moves/zooms)
     * @param {string} containerId - Container element ID
     * @param {string} commentId - Comment ID
     * @param {number} x - Pixel X position
     * @param {number} y - Pixel Y position
     */
    updateMarkerPosition: function(containerId, commentId, x, y) {
        const markers = this.markers[containerId];
        if (!markers || !markers[commentId]) return;

        const marker = markers[commentId];
        marker.style.left = `${x}px`;
        marker.style.top = `${y}px`;
    },

    /**
     * Highlights a specific marker
     */
    highlightMarker: function(containerId, commentId) {
        const markers = this.markers[containerId];
        if (!markers) return;

        // Remove highlight from all markers
        Object.values(markers).forEach(m => m.classList.remove('highlighted'));

        // Add highlight to selected marker
        if (markers[commentId]) {
            markers[commentId].classList.add('highlighted');
        }
    },

    /**
     * Removes all markers
     */
    clearMarkers: function(containerId) {
        const container = document.getElementById(containerId);
        if (container) {
            container.innerHTML = '';
        }
        if (this.markers[containerId]) {
            delete this.markers[containerId];
        }
    },

    /**
     * Attaches click handler to markers
     */
    attachMarkerClickHandler: function(containerId, dotNetHelper) {
        const container = document.getElementById(containerId);
        if (!container) return;

        container.addEventListener('click', (e) => {
            const marker = e.target.closest('.comment-marker');
            if (marker) {
                const commentId = marker.dataset.commentId;
                dotNetHelper.invokeMethodAsync('OnMarkerClicked', commentId);
            }
        });
    },

    /**
     * Enables drawing mode for creating new comments
     */
    enableDrawMode: function(mapId, mode) {
        // mode can be: 'point', 'line', 'polygon'
        console.log(`Enabling ${mode} draw mode for map ${mapId}`);

        // This would integrate with your map library (Leaflet, MapLibre, etc.)
        // Example implementation:
        if (window.leafletMap) {
            this.enableLeafletDrawMode(mode);
        } else if (window.mapboxMap) {
            this.enableMapboxDrawMode(mode);
        }
    },

    /**
     * Disables drawing mode
     */
    disableDrawMode: function(mapId) {
        console.log(`Disabling draw mode for map ${mapId}`);
        // Clean up drawing handlers
    },

    /**
     * Example: Leaflet integration
     */
    enableLeafletDrawMode: function(mode) {
        if (!window.leafletMap) return;

        const map = window.leafletMap;

        if (mode === 'point') {
            map.on('click', this.handleLeafletClick);
        }
        // Add line and polygon handlers as needed
    },

    handleLeafletClick: function(e) {
        const { lat, lng } = e.latlng;

        // Call back to Blazor component
        if (window.DotNet && window.commentsComponentRef) {
            window.commentsComponentRef.invokeMethodAsync('OnMapClicked', lng, lat);
        }
    },

    /**
     * Animates marker appearance
     */
    animateMarker: function(containerId, commentId) {
        const markers = this.markers[containerId];
        if (!markers || !markers[commentId]) return;

        const marker = markers[commentId];
        marker.classList.add('marker-pulse');

        setTimeout(() => {
            marker.classList.remove('marker-pulse');
        }, 1000);
    },

    /**
     * Filters visible markers
     */
    filterMarkers: function(containerId, filterFn) {
        const markers = this.markers[containerId];
        if (!markers) return;

        Object.entries(markers).forEach(([id, marker]) => {
            const visible = filterFn(id);
            marker.style.display = visible ? 'block' : 'none';
        });
    },

    /**
     * Creates a clustering effect for nearby markers
     */
    clusterMarkers: function(containerId, threshold = 50) {
        const markers = this.markers[containerId];
        if (!markers) return;

        const markerArray = Object.values(markers);
        const clusters = [];

        // Simple clustering algorithm
        markerArray.forEach(marker => {
            const rect = marker.getBoundingClientRect();
            let addedToCluster = false;

            for (let cluster of clusters) {
                const clusterRect = cluster[0].getBoundingClientRect();
                const distance = Math.sqrt(
                    Math.pow(rect.left - clusterRect.left, 2) +
                    Math.pow(rect.top - clusterRect.top, 2)
                );

                if (distance < threshold) {
                    cluster.push(marker);
                    addedToCluster = true;
                    break;
                }
            }

            if (!addedToCluster) {
                clusters.push([marker]);
            }
        });

        // Update markers to show cluster count
        clusters.forEach(cluster => {
            if (cluster.length > 1) {
                const clusterBadge = document.createElement('span');
                clusterBadge.className = 'cluster-badge';
                clusterBadge.textContent = cluster.length;
                cluster[0].appendChild(clusterBadge);

                // Hide other markers in cluster
                for (let i = 1; i < cluster.length; i++) {
                    cluster[i].style.display = 'none';
                }
            }
        });
    }
};

// CSS for markers (can be moved to a separate CSS file)
const style = document.createElement('style');
style.textContent = `
    .comment-marker {
        width: 32px;
        height: 32px;
        border-radius: 50%;
        display: flex;
        align-items: center;
        justify-content: center;
        color: white;
        font-size: 14px;
        cursor: pointer;
        box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
        transition: transform 0.2s, box-shadow 0.2s;
        pointer-events: auto;
        z-index: 1000;
    }

    .comment-marker:hover {
        transform: scale(1.2);
        box-shadow: 0 4px 12px rgba(0, 0, 0, 0.4);
        z-index: 1001;
    }

    .comment-marker.highlighted {
        transform: scale(1.3);
        box-shadow: 0 0 0 3px rgba(0, 123, 255, 0.5);
        z-index: 1002;
    }

    .comment-marker.priority-alert {
        animation: pulse 2s infinite;
    }

    .comment-marker.status-resolved {
        opacity: 0.6;
    }

    .marker-pulse {
        animation: marker-appear 0.5s ease-out;
    }

    .cluster-badge {
        position: absolute;
        top: -8px;
        right: -8px;
        background: #dc3545;
        color: white;
        border-radius: 50%;
        width: 20px;
        height: 20px;
        display: flex;
        align-items: center;
        justify-content: center;
        font-size: 11px;
        font-weight: bold;
        border: 2px solid white;
    }

    @keyframes pulse {
        0%, 100% {
            box-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
        }
        50% {
            box-shadow: 0 2px 8px rgba(255, 0, 0, 0.8);
        }
    }

    @keyframes marker-appear {
        0% {
            transform: scale(0);
            opacity: 0;
        }
        50% {
            transform: scale(1.2);
        }
        100% {
            transform: scale(1);
            opacity: 1;
        }
    }
`;
document.head.appendChild(style);
