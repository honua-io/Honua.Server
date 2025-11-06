// Honua Timeline JavaScript Module
// Provides smooth animations and slider interactions for the HonuaTimeline component

const timelines = new Map();
let animationFrameId = null;

/**
 * Creates a new timeline instance
 * @param {HTMLElement} container - The container element for the timeline
 * @param {Object} options - Timeline configuration options
 * @param {Object} dotNetRef - Reference to .NET component for callbacks
 * @returns {Object} Timeline API object
 */
export function createTimeline(container, options, dotNetRef) {
    const timelineId = options.id;

    // Store timeline instance and metadata
    const timelineData = {
        container: container,
        dotNetRef: dotNetRef,
        options: options,
        currentStep: options.currentStep || 0,
        totalSteps: options.totalSteps || 100,
        isPlaying: false,
        isDragging: false,
        animationSpeed: 1.0,
        visibility: {
            hidden: false,
            lastUpdate: Date.now()
        }
    };

    timelines.set(timelineId, timelineData);

    // Setup event listeners
    setupEventListeners(timelineId);

    // Setup visibility change detection for pausing when tab is hidden
    setupVisibilityHandler(timelineId);

    return createTimelineAPI(timelineId);
}

/**
 * Sets up event listeners for timeline interactions
 */
function setupEventListeners(timelineId) {
    const timelineData = timelines.get(timelineId);
    if (!timelineData) return;

    const slider = timelineData.container.querySelector('.timeline-slider input[type="range"]');
    if (!slider) return;

    // Track dragging state
    slider.addEventListener('mousedown', () => {
        timelineData.isDragging = true;
    });

    slider.addEventListener('mouseup', () => {
        timelineData.isDragging = false;
    });

    slider.addEventListener('touchstart', () => {
        timelineData.isDragging = true;
    });

    slider.addEventListener('touchend', () => {
        timelineData.isDragging = false;
    });

    // Smooth slider updates using requestAnimationFrame
    let rafId = null;
    let pendingValue = null;

    slider.addEventListener('input', (e) => {
        const value = parseInt(e.target.value, 10);
        pendingValue = value;

        if (!rafId) {
            rafId = requestAnimationFrame(() => {
                if (pendingValue !== null) {
                    updateTimelinePosition(timelineId, pendingValue);
                    pendingValue = null;
                }
                rafId = null;
            });
        }
    });
}

/**
 * Sets up visibility change handler to pause playback when tab is hidden
 */
function setupVisibilityHandler(timelineId) {
    const timelineData = timelines.get(timelineId);
    if (!timelineData) return;

    // Use Page Visibility API to detect when tab is hidden
    document.addEventListener('visibilitychange', () => {
        if (document.hidden) {
            timelineData.visibility.hidden = true;
            // Pause any ongoing animations
            if (timelineData.isPlaying) {
                pauseAnimation(timelineId);
            }
        } else {
            timelineData.visibility.hidden = false;
            timelineData.visibility.lastUpdate = Date.now();
        }
    });
}

/**
 * Updates timeline position
 */
function updateTimelinePosition(timelineId, step) {
    const timelineData = timelines.get(timelineId);
    if (!timelineData) return;

    timelineData.currentStep = step;

    // Notify Blazor component
    try {
        timelineData.dotNetRef.invokeMethodAsync('OnSliderChangedFromJS', step);
    } catch (error) {
        console.error('Error notifying Blazor of timeline change:', error);
    }
}

/**
 * Starts smooth animation playback
 */
function startAnimation(timelineId, speed = 1.0) {
    const timelineData = timelines.get(timelineId);
    if (!timelineData) return;

    timelineData.isPlaying = true;
    timelineData.animationSpeed = speed;

    animate(timelineId);
}

/**
 * Pauses animation playback
 */
function pauseAnimation(timelineId) {
    const timelineData = timelines.get(timelineId);
    if (!timelineData) return;

    timelineData.isPlaying = false;
}

/**
 * Animation loop using requestAnimationFrame for smooth playback
 */
function animate(timelineId) {
    const timelineData = timelines.get(timelineId);
    if (!timelineData || !timelineData.isPlaying) return;

    // Skip animation if tab is hidden
    if (timelineData.visibility.hidden) {
        animationFrameId = requestAnimationFrame(() => animate(timelineId));
        return;
    }

    // Calculate time delta
    const now = Date.now();
    const delta = now - timelineData.visibility.lastUpdate;
    timelineData.visibility.lastUpdate = now;

    // Update position based on speed
    // This provides smoother interpolation than discrete steps
    const stepIncrement = (delta / 1000) * timelineData.animationSpeed;

    // Update slider position
    const slider = timelineData.container.querySelector('.timeline-slider input[type="range"]');
    if (slider) {
        const currentValue = parseFloat(slider.value);
        const newValue = Math.min(timelineData.totalSteps - 1, currentValue + stepIncrement);
        slider.value = newValue.toString();

        // Trigger visual update
        const event = new Event('input', { bubbles: true });
        slider.dispatchEvent(event);
    }

    // Continue animation
    animationFrameId = requestAnimationFrame(() => animate(timelineId));
}

/**
 * Creates the public API for a timeline instance
 */
function createTimelineAPI(timelineId) {
    return {
        /**
         * Updates timeline configuration
         */
        updateConfig: (config) => {
            const timelineData = timelines.get(timelineId);
            if (!timelineData) return;

            Object.assign(timelineData.options, config);

            if (config.totalSteps !== undefined) {
                timelineData.totalSteps = config.totalSteps;
            }

            if (config.currentStep !== undefined) {
                timelineData.currentStep = config.currentStep;
                updateSliderPosition(timelineId, config.currentStep);
            }
        },

        /**
         * Starts playback animation
         */
        play: (speed = 1.0) => {
            startAnimation(timelineId, speed);
        },

        /**
         * Pauses playback animation
         */
        pause: () => {
            pauseAnimation(timelineId);
        },

        /**
         * Stops playback and resets to start
         */
        stop: () => {
            pauseAnimation(timelineId);
            updateTimelinePosition(timelineId, 0);
            updateSliderPosition(timelineId, 0);
        },

        /**
         * Seeks to specific step
         */
        seekTo: (step) => {
            const timelineData = timelines.get(timelineId);
            if (!timelineData) return;

            const clampedStep = Math.max(0, Math.min(timelineData.totalSteps - 1, step));
            updateTimelinePosition(timelineId, clampedStep);
            updateSliderPosition(timelineId, clampedStep);
        },

        /**
         * Steps forward one frame
         */
        stepForward: () => {
            const timelineData = timelines.get(timelineId);
            if (!timelineData) return;

            const nextStep = Math.min(timelineData.totalSteps - 1, timelineData.currentStep + 1);
            updateTimelinePosition(timelineId, nextStep);
            updateSliderPosition(timelineId, nextStep);
        },

        /**
         * Steps backward one frame
         */
        stepBackward: () => {
            const timelineData = timelines.get(timelineId);
            if (!timelineData) return;

            const prevStep = Math.max(0, timelineData.currentStep - 1);
            updateTimelinePosition(timelineId, prevStep);
            updateSliderPosition(timelineId, prevStep);
        },

        /**
         * Gets current playback state
         */
        getState: () => {
            const timelineData = timelines.get(timelineId);
            if (!timelineData) return null;

            return {
                currentStep: timelineData.currentStep,
                totalSteps: timelineData.totalSteps,
                isPlaying: timelineData.isPlaying,
                animationSpeed: timelineData.animationSpeed
            };
        },

        /**
         * Sets animation speed
         */
        setSpeed: (speed) => {
            const timelineData = timelines.get(timelineId);
            if (!timelineData) return;

            timelineData.animationSpeed = speed;
        },

        /**
         * Adds a bookmark marker to the timeline
         */
        addBookmark: (time, label, color = '#3b82f6') => {
            const timelineData = timelines.get(timelineId);
            if (!timelineData) return;

            const bookmarksContainer = timelineData.container.querySelector('.timeline-bookmarks');
            if (!bookmarksContainer) return;

            const bookmark = document.createElement('div');
            bookmark.className = 'timeline-bookmark';
            bookmark.style.backgroundColor = color;
            bookmark.style.left = `${calculateBookmarkPosition(time, timelineData.options)}%`;
            bookmark.title = label;

            bookmarksContainer.appendChild(bookmark);
        },

        /**
         * Removes all bookmark markers
         */
        clearBookmarks: () => {
            const timelineData = timelines.get(timelineId);
            if (!timelineData) return;

            const bookmarksContainer = timelineData.container.querySelector('.timeline-bookmarks');
            if (!bookmarksContainer) return;

            // Use replaceChildren() instead of innerHTML = '' for better practice
            bookmarksContainer.replaceChildren();
        },

        /**
         * Highlights a time range on the timeline
         */
        highlightRange: (startStep, endStep, color = 'rgba(59, 130, 246, 0.2)') => {
            const timelineData = timelines.get(timelineId);
            if (!timelineData) return;

            // Create range overlay
            const rangeOverlay = document.createElement('div');
            rangeOverlay.className = 'timeline-range-highlight';
            rangeOverlay.style.position = 'absolute';
            rangeOverlay.style.left = `${(startStep / timelineData.totalSteps) * 100}%`;
            rangeOverlay.style.width = `${((endStep - startStep) / timelineData.totalSteps) * 100}%`;
            rangeOverlay.style.height = '8px';
            rangeOverlay.style.backgroundColor = color;
            rangeOverlay.style.pointerEvents = 'none';

            const sliderContainer = timelineData.container.querySelector('.timeline-slider-container');
            if (sliderContainer) {
                sliderContainer.appendChild(rangeOverlay);
            }
        },

        /**
         * Exports current timeline state
         */
        exportState: () => {
            const timelineData = timelines.get(timelineId);
            if (!timelineData) return null;

            return {
                id: timelineId,
                currentStep: timelineData.currentStep,
                totalSteps: timelineData.totalSteps,
                isPlaying: timelineData.isPlaying,
                options: { ...timelineData.options }
            };
        },

        /**
         * Enables smooth scrubbing mode
         */
        enableSmoothScrubbing: (enabled = true) => {
            const timelineData = timelines.get(timelineId);
            if (!timelineData) return;

            const slider = timelineData.container.querySelector('.timeline-slider input[type="range"]');
            if (!slider) return;

            if (enabled) {
                slider.classList.add('smooth-scrubbing');
                // Add CSS for smooth transition
                slider.style.transition = 'all 0.1s ease-out';
            } else {
                slider.classList.remove('smooth-scrubbing');
                slider.style.transition = '';
            }
        },

        /**
         * Attaches custom event listeners
         */
        addEventListener: (eventName, callback) => {
            const timelineData = timelines.get(timelineId);
            if (!timelineData) return;

            if (!timelineData.eventListeners) {
                timelineData.eventListeners = {};
            }

            if (!timelineData.eventListeners[eventName]) {
                timelineData.eventListeners[eventName] = [];
            }

            timelineData.eventListeners[eventName].push(callback);
        },

        /**
         * Triggers custom events
         */
        emit: (eventName, data) => {
            const timelineData = timelines.get(timelineId);
            if (!timelineData || !timelineData.eventListeners) return;

            const listeners = timelineData.eventListeners[eventName];
            if (listeners) {
                listeners.forEach(callback => callback(data));
            }
        },

        /**
         * Resizes timeline (useful for responsive layouts)
         */
        resize: () => {
            const timelineData = timelines.get(timelineId);
            if (!timelineData) return;

            // Force layout recalculation
            const container = timelineData.container;
            if (container) {
                const display = container.style.display;
                container.style.display = 'none';
                // Trigger reflow
                container.offsetHeight;
                container.style.display = display;
            }
        },

        /**
         * Disposes timeline and cleans up resources
         */
        dispose: () => {
            pauseAnimation(timelineId);

            if (animationFrameId) {
                cancelAnimationFrame(animationFrameId);
                animationFrameId = null;
            }

            timelines.delete(timelineId);
        }
    };
}

/**
 * Updates slider position visually
 */
function updateSliderPosition(timelineId, step) {
    const timelineData = timelines.get(timelineId);
    if (!timelineData) return;

    const slider = timelineData.container.querySelector('.timeline-slider input[type="range"]');
    if (slider) {
        slider.value = step.toString();
    }
}

/**
 * Calculates bookmark position percentage
 */
function calculateBookmarkPosition(time, options) {
    const start = new Date(options.startTime);
    const end = new Date(options.endTime);
    const bookmark = new Date(time);

    const totalDuration = end - start;
    const bookmarkDuration = bookmark - start;

    return (bookmarkDuration / totalDuration) * 100;
}

/**
 * Gets timeline instance by ID (for debugging)
 */
export function getTimeline(timelineId) {
    return timelines.get(timelineId);
}

/**
 * Gets all active timelines (for debugging)
 */
export function getAllTimelines() {
    return Array.from(timelines.keys());
}

/**
 * Utility: Format time duration
 */
export function formatDuration(milliseconds) {
    const seconds = Math.floor(milliseconds / 1000);
    const minutes = Math.floor(seconds / 60);
    const hours = Math.floor(minutes / 60);
    const days = Math.floor(hours / 24);

    if (days > 0) return `${days}d ${hours % 24}h`;
    if (hours > 0) return `${hours}h ${minutes % 60}m`;
    if (minutes > 0) return `${minutes}m ${seconds % 60}s`;
    return `${seconds}s`;
}

/**
 * Utility: Calculate optimal step count for a time range
 */
export function calculateOptimalSteps(startTime, endTime) {
    const start = new Date(startTime);
    const end = new Date(endTime);
    const duration = end - start;

    const minutes = duration / (1000 * 60);
    const hours = duration / (1000 * 60 * 60);
    const days = duration / (1000 * 60 * 60 * 24);

    if (minutes < 60) return 60; // 1 minute steps
    if (hours < 24) return 100; // ~15 minute steps
    if (days < 7) return 168; // 1 hour steps
    if (days < 30) return 120; // ~6 hour steps
    if (days < 365) return 365; // 1 day steps

    return 100; // Default
}

// Export helper functions for external use
export const TimelineUtils = {
    formatDuration,
    calculateOptimalSteps
};
