// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

/**
 * Touch Gesture Handler for Mobile Devices
 * Provides pull-to-refresh, swipe-to-delete, and swipe navigation
 */
window.TouchGestures = (function() {
    'use strict';

    // Constants
    const PULL_TO_REFRESH_THRESHOLD = 80; // pixels to pull before triggering refresh
    const SWIPE_TO_DELETE_THRESHOLD = 100; // pixels to swipe before showing delete
    const SWIPE_NAVIGATION_THRESHOLD = 80; // pixels to swipe before navigation
    const VELOCITY_THRESHOLD = 0.3; // minimum velocity for swipe detection

    // Active gesture trackers
    const activeGestures = new Map();

    /**
     * Initialize pull-to-refresh on a container element
     * @param {string} elementId - Container element ID
     * @param {object} dotNetHelper - .NET object reference for callbacks
     * @returns {object} Cleanup function
     */
    function initPullToRefresh(elementId, dotNetHelper) {
        const element = document.getElementById(elementId);
        if (!element) {
            console.error(`Element ${elementId} not found for pull-to-refresh`);
            return null;
        }

        let startY = 0;
        let currentY = 0;
        let isPulling = false;
        let pullIndicator = null;

        // Create pull indicator
        pullIndicator = document.createElement('div');
        pullIndicator.className = 'pull-to-refresh-indicator';
        pullIndicator.innerHTML = `
            <div class="pull-to-refresh-spinner">
                <svg class="circular-spinner" viewBox="0 0 50 50">
                    <circle class="path" cx="25" cy="25" r="20" fill="none" stroke-width="3"></circle>
                </svg>
                <span class="pull-text">Pull to refresh</span>
            </div>
        `;
        pullIndicator.style.cssText = `
            position: absolute;
            top: -60px;
            left: 0;
            right: 0;
            height: 60px;
            display: flex;
            align-items: center;
            justify-content: center;
            transition: transform 0.3s ease;
            pointer-events: none;
            z-index: 1000;
        `;

        // Insert indicator at the beginning of element
        element.style.position = 'relative';
        element.insertBefore(pullIndicator, element.firstChild);

        const handleTouchStart = (e) => {
            // Only enable on mobile screens
            if (window.innerWidth > 768) return;

            // Check if element is at the top
            if (element.scrollTop === 0) {
                startY = e.touches[0].clientY;
                isPulling = false;
            }
        };

        const handleTouchMove = (e) => {
            if (window.innerWidth > 768 || startY === 0) return;

            currentY = e.touches[0].clientY;
            const diff = currentY - startY;

            // Only allow pulling when at the top and pulling down
            if (diff > 0 && element.scrollTop === 0) {
                isPulling = true;
                e.preventDefault();

                const pullDistance = Math.min(diff, PULL_TO_REFRESH_THRESHOLD + 20);
                const progress = Math.min(pullDistance / PULL_TO_REFRESH_THRESHOLD, 1);

                // Update indicator position and rotation
                pullIndicator.style.transform = `translateY(${pullDistance}px)`;
                const spinner = pullIndicator.querySelector('.circular-spinner');
                if (spinner) {
                    spinner.style.transform = `rotate(${progress * 360}deg)`;
                }

                // Update text based on threshold
                const pullText = pullIndicator.querySelector('.pull-text');
                if (pullText) {
                    pullText.textContent = pullDistance >= PULL_TO_REFRESH_THRESHOLD
                        ? 'Release to refresh'
                        : 'Pull to refresh';
                }

                // Add active class when threshold is met
                if (pullDistance >= PULL_TO_REFRESH_THRESHOLD) {
                    pullIndicator.classList.add('active');
                } else {
                    pullIndicator.classList.remove('active');
                }
            }
        };

        const handleTouchEnd = async (e) => {
            if (!isPulling) return;

            const diff = currentY - startY;

            if (diff >= PULL_TO_REFRESH_THRESHOLD) {
                // Trigger refresh
                pullIndicator.classList.add('loading');
                const pullText = pullIndicator.querySelector('.pull-text');
                if (pullText) {
                    pullText.textContent = 'Refreshing...';
                }

                try {
                    await dotNetHelper.invokeMethodAsync('OnPullToRefresh');
                } catch (error) {
                    console.error('Error triggering refresh:', error);
                } finally {
                    // Reset indicator
                    setTimeout(() => {
                        pullIndicator.style.transform = '';
                        pullIndicator.classList.remove('active', 'loading');
                        isPulling = false;
                        startY = 0;
                        currentY = 0;
                    }, 300);
                }
            } else {
                // Reset indicator
                pullIndicator.style.transform = '';
                pullIndicator.classList.remove('active');
                isPulling = false;
                startY = 0;
                currentY = 0;
            }
        };

        element.addEventListener('touchstart', handleTouchStart, { passive: true });
        element.addEventListener('touchmove', handleTouchMove, { passive: false });
        element.addEventListener('touchend', handleTouchEnd, { passive: true });

        // Store cleanup function
        const gestureId = `pull-${elementId}`;
        activeGestures.set(gestureId, {
            dispose: () => {
                element.removeEventListener('touchstart', handleTouchStart);
                element.removeEventListener('touchmove', handleTouchMove);
                element.removeEventListener('touchend', handleTouchEnd);
                if (pullIndicator && pullIndicator.parentNode) {
                    pullIndicator.parentNode.removeChild(pullIndicator);
                }
                activeGestures.delete(gestureId);
            }
        });

        return { id: gestureId };
    }

    /**
     * Initialize swipe-to-delete on a table row
     * @param {string} rowId - Row element ID
     * @param {object} dotNetHelper - .NET object reference for callbacks
     * @param {string} itemId - Item identifier for deletion
     * @returns {object} Cleanup function
     */
    function initSwipeToDelete(rowId, dotNetHelper, itemId) {
        const row = document.getElementById(rowId);
        if (!row) {
            console.error(`Row ${rowId} not found for swipe-to-delete`);
            return null;
        }

        let startX = 0;
        let currentX = 0;
        let startTime = 0;
        let isSwiping = false;
        let deleteButton = null;

        // Create delete button overlay
        deleteButton = document.createElement('div');
        deleteButton.className = 'swipe-delete-action';
        deleteButton.innerHTML = `
            <svg viewBox="0 0 24 24" width="24" height="24">
                <path fill="currentColor" d="M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z"/>
            </svg>
            <span>Delete</span>
        `;
        deleteButton.style.cssText = `
            position: absolute;
            top: 0;
            right: 0;
            bottom: 0;
            width: 100px;
            background: #d32f2f;
            color: white;
            display: flex;
            align-items: center;
            justify-content: center;
            gap: 8px;
            opacity: 0;
            transform: translateX(100%);
            transition: opacity 0.3s, transform 0.3s;
            cursor: pointer;
            z-index: 1;
        `;

        // Make row position relative
        row.style.position = 'relative';
        row.style.overflow = 'hidden';
        row.appendChild(deleteButton);

        const handleTouchStart = (e) => {
            // Only enable on mobile screens
            if (window.innerWidth > 768) return;

            startX = e.touches[0].clientX;
            startTime = Date.now();
            isSwiping = false;
        };

        const handleTouchMove = (e) => {
            if (window.innerWidth > 768 || startX === 0) return;

            currentX = e.touches[0].clientX;
            const diff = currentX - startX;

            // Only allow swiping left
            if (diff < -10) {
                isSwiping = true;
                e.preventDefault();

                const swipeDistance = Math.min(Math.abs(diff), SWIPE_TO_DELETE_THRESHOLD);
                const progress = swipeDistance / SWIPE_TO_DELETE_THRESHOLD;

                // Move row and show delete button
                row.style.transform = `translateX(-${swipeDistance}px)`;
                deleteButton.style.opacity = progress;
                deleteButton.style.transform = `translateX(${100 - progress * 100}%)`;
            }
        };

        const handleTouchEnd = (e) => {
            if (!isSwiping) return;

            const diff = startX - currentX;
            const duration = Date.now() - startTime;
            const velocity = diff / duration;

            if (diff >= SWIPE_TO_DELETE_THRESHOLD || velocity > VELOCITY_THRESHOLD) {
                // Show delete button
                row.style.transform = `translateX(-${SWIPE_TO_DELETE_THRESHOLD}px)`;
                deleteButton.style.opacity = '1';
                deleteButton.style.transform = 'translateX(0)';

                // Handle delete button click
                const handleDeleteClick = async () => {
                    try {
                        await dotNetHelper.invokeMethodAsync('OnSwipeDelete', itemId);
                        // Reset after deletion is handled by the component
                    } catch (error) {
                        console.error('Error deleting item:', error);
                        resetRow();
                    }
                };

                deleteButton.addEventListener('click', handleDeleteClick, { once: true });

                // Reset if user taps elsewhere
                const handleOutsideClick = (e) => {
                    if (!row.contains(e.target)) {
                        resetRow();
                        document.removeEventListener('click', handleOutsideClick);
                    }
                };
                setTimeout(() => document.addEventListener('click', handleOutsideClick), 100);
            } else {
                resetRow();
            }

            isSwiping = false;
            startX = 0;
            currentX = 0;
        };

        const resetRow = () => {
            row.style.transform = '';
            deleteButton.style.opacity = '0';
            deleteButton.style.transform = 'translateX(100%)';
        };

        row.addEventListener('touchstart', handleTouchStart, { passive: true });
        row.addEventListener('touchmove', handleTouchMove, { passive: false });
        row.addEventListener('touchend', handleTouchEnd, { passive: true });

        // Store cleanup function
        const gestureId = `swipe-${rowId}`;
        activeGestures.set(gestureId, {
            dispose: () => {
                row.removeEventListener('touchstart', handleTouchStart);
                row.removeEventListener('touchmove', handleTouchMove);
                row.removeEventListener('touchend', handleTouchEnd);
                if (deleteButton && deleteButton.parentNode) {
                    deleteButton.parentNode.removeChild(deleteButton);
                }
                row.style.transform = '';
                activeGestures.delete(gestureId);
            }
        });

        return { id: gestureId };
    }

    /**
     * Initialize swipe navigation (swipe right to go back)
     * @param {string} elementId - Container element ID
     * @param {object} dotNetHelper - .NET object reference for callbacks
     * @returns {object} Cleanup function
     */
    function initSwipeNavigation(elementId, dotNetHelper) {
        const element = document.getElementById(elementId);
        if (!element) {
            console.error(`Element ${elementId} not found for swipe navigation`);
            return null;
        }

        let startX = 0;
        let startY = 0;
        let currentX = 0;
        let startTime = 0;
        let isNavigating = false;
        let navigationIndicator = null;

        // Create navigation indicator
        navigationIndicator = document.createElement('div');
        navigationIndicator.className = 'swipe-nav-indicator';
        navigationIndicator.innerHTML = `
            <svg viewBox="0 0 24 24" width="32" height="32">
                <path fill="currentColor" d="M15.41 7.41L14 6l-6 6 6 6 1.41-1.41L10.83 12z"/>
            </svg>
        `;
        navigationIndicator.style.cssText = `
            position: fixed;
            left: 0;
            top: 50%;
            transform: translateY(-50%) translateX(-100%);
            width: 50px;
            height: 50px;
            background: rgba(0, 0, 0, 0.6);
            border-radius: 0 25px 25px 0;
            display: flex;
            align-items: center;
            justify-content: center;
            color: white;
            opacity: 0;
            transition: opacity 0.3s;
            pointer-events: none;
            z-index: 2000;
        `;
        document.body.appendChild(navigationIndicator);

        const handleTouchStart = (e) => {
            // Only enable on mobile screens and near left edge
            if (window.innerWidth > 768) return;

            startX = e.touches[0].clientX;
            startY = e.touches[0].clientY;
            startTime = Date.now();

            // Only activate if touch starts near left edge (first 50px)
            if (startX <= 50) {
                isNavigating = false;
            } else {
                startX = 0;
            }
        };

        const handleTouchMove = (e) => {
            if (window.innerWidth > 768 || startX === 0) return;

            currentX = e.touches[0].clientX;
            const currentY = e.touches[0].clientY;
            const diffX = currentX - startX;
            const diffY = Math.abs(currentY - startY);

            // Only allow swiping right, and horizontal swipe should be dominant
            if (diffX > 10 && diffX > diffY * 2) {
                isNavigating = true;
                e.preventDefault();

                const swipeDistance = Math.min(diffX, SWIPE_NAVIGATION_THRESHOLD + 20);
                const progress = Math.min(swipeDistance / SWIPE_NAVIGATION_THRESHOLD, 1);

                // Update indicator
                navigationIndicator.style.opacity = progress;
                navigationIndicator.style.transform = `translateY(-50%) translateX(${-100 + progress * 100}%)`;
            }
        };

        const handleTouchEnd = async (e) => {
            if (!isNavigating) {
                navigationIndicator.style.opacity = '0';
                navigationIndicator.style.transform = 'translateY(-50%) translateX(-100%)';
                return;
            }

            const diff = currentX - startX;
            const duration = Date.now() - startTime;
            const velocity = diff / duration;

            if (diff >= SWIPE_NAVIGATION_THRESHOLD || velocity > VELOCITY_THRESHOLD) {
                // Trigger navigation
                navigationIndicator.style.opacity = '1';

                try {
                    await dotNetHelper.invokeMethodAsync('OnSwipeBack');
                } catch (error) {
                    console.error('Error navigating back:', error);
                }
            }

            // Reset indicator
            setTimeout(() => {
                navigationIndicator.style.opacity = '0';
                navigationIndicator.style.transform = 'translateY(-50%) translateX(-100%)';
            }, 300);

            isNavigating = false;
            startX = 0;
            currentX = 0;
        };

        element.addEventListener('touchstart', handleTouchStart, { passive: true });
        element.addEventListener('touchmove', handleTouchMove, { passive: false });
        element.addEventListener('touchend', handleTouchEnd, { passive: true });

        // Store cleanup function
        const gestureId = `nav-${elementId}`;
        activeGestures.set(gestureId, {
            dispose: () => {
                element.removeEventListener('touchstart', handleTouchStart);
                element.removeEventListener('touchmove', handleTouchMove);
                element.removeEventListener('touchend', handleTouchEnd);
                if (navigationIndicator && navigationIndicator.parentNode) {
                    navigationIndicator.parentNode.removeChild(navigationIndicator);
                }
                activeGestures.delete(gestureId);
            }
        });

        return { id: gestureId };
    }

    /**
     * Dispose a specific gesture handler
     * @param {string} gestureId - Gesture identifier
     */
    function dispose(gestureId) {
        const gesture = activeGestures.get(gestureId);
        if (gesture) {
            gesture.dispose();
        }
    }

    /**
     * Dispose all active gestures
     */
    function disposeAll() {
        activeGestures.forEach(gesture => gesture.dispose());
        activeGestures.clear();
    }

    // Public API
    return {
        initPullToRefresh,
        initSwipeToDelete,
        initSwipeNavigation,
        dispose,
        disposeAll
    };
})();
