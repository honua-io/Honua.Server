// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

/**
 * Honua Tour Framework - Interactive guided tours using Shepherd.js
 * This module provides a comprehensive tour system for onboarding and feature discovery
 */

window.HonuaTours = window.HonuaTours || {
    activeTour: null,
    completedTours: new Set(),
    tourSteps: {},

    /**
     * Initialize the tour system
     */
    initialize: function() {
        // Load completed tours from localStorage
        const stored = localStorage.getItem('honua-completed-tours');
        if (stored) {
            try {
                this.completedTours = new Set(JSON.parse(stored));
            } catch (e) {
                console.error('Failed to load completed tours:', e);
            }
        }

        // Apply custom Shepherd.js styles
        this.injectStyles();

        console.log('Honua Tour Framework initialized');
    },

    /**
     * Inject custom styles for tour elements
     */
    injectStyles: function() {
        if (document.getElementById('honua-tour-styles')) return;

        const style = document.createElement('style');
        style.id = 'honua-tour-styles';
        style.textContent = `
            /* Shepherd.js custom styles */
            .shepherd-element {
                z-index: 9999;
                max-width: 400px;
            }

            .shepherd-content {
                border-radius: 8px;
                padding: 0;
                background: var(--mud-palette-surface);
                box-shadow: 0 4px 20px rgba(0, 0, 0, 0.15);
            }

            .shepherd-header {
                padding: 16px 20px;
                border-bottom: 1px solid var(--mud-palette-divider);
                background: var(--mud-palette-primary);
                color: white;
                border-radius: 8px 8px 0 0;
                display: flex;
                align-items: center;
                justify-content: space-between;
            }

            .shepherd-title {
                font-size: 18px;
                font-weight: 600;
                margin: 0;
            }

            .shepherd-cancel-icon {
                background: none;
                border: none;
                color: white;
                cursor: pointer;
                font-size: 24px;
                padding: 0;
                width: 24px;
                height: 24px;
                display: flex;
                align-items: center;
                justify-content: center;
                opacity: 0.8;
                transition: opacity 0.2s;
            }

            .shepherd-cancel-icon:hover {
                opacity: 1;
            }

            .shepherd-text {
                padding: 20px;
                font-size: 14px;
                line-height: 1.6;
                color: var(--mud-palette-text-primary);
            }

            .shepherd-footer {
                padding: 12px 20px;
                border-top: 1px solid var(--mud-palette-divider);
                display: flex;
                justify-content: space-between;
                align-items: center;
                background: var(--mud-palette-background);
                border-radius: 0 0 8px 8px;
            }

            .shepherd-button {
                background: var(--mud-palette-primary);
                color: white;
                border: none;
                padding: 8px 16px;
                border-radius: 4px;
                cursor: pointer;
                font-size: 14px;
                font-weight: 500;
                transition: background 0.2s;
            }

            .shepherd-button:hover {
                background: var(--mud-palette-primary-darken);
            }

            .shepherd-button-secondary {
                background: transparent;
                color: var(--mud-palette-text-secondary);
                border: 1px solid var(--mud-palette-divider);
            }

            .shepherd-button-secondary:hover {
                background: var(--mud-palette-action-hover);
            }

            .shepherd-progress {
                font-size: 12px;
                color: var(--mud-palette-text-secondary);
                font-weight: 500;
            }

            .shepherd-progress-dots {
                display: flex;
                gap: 6px;
                align-items: center;
            }

            .shepherd-progress-dot {
                width: 8px;
                height: 8px;
                border-radius: 50%;
                background: var(--mud-palette-divider);
                transition: background 0.3s;
            }

            .shepherd-progress-dot.active {
                background: var(--mud-palette-primary);
            }

            .shepherd-progress-dot.completed {
                background: var(--mud-palette-success);
            }

            .shepherd-arrow::before {
                background: var(--mud-palette-surface);
            }

            /* Highlight element styles */
            .honua-tour-highlight {
                position: relative;
                z-index: 9998;
                box-shadow: 0 0 0 4px rgba(25, 118, 210, 0.4),
                            0 0 0 99999px rgba(0, 0, 0, 0.5);
                border-radius: 4px;
                transition: box-shadow 0.3s ease;
            }

            .honua-tour-pulse {
                animation: honua-pulse 2s infinite;
            }

            @keyframes honua-pulse {
                0%, 100% {
                    box-shadow: 0 0 0 4px rgba(25, 118, 210, 0.4),
                                0 0 0 99999px rgba(0, 0, 0, 0.5);
                }
                50% {
                    box-shadow: 0 0 0 8px rgba(25, 118, 210, 0.6),
                                0 0 0 99999px rgba(0, 0, 0, 0.5);
                }
            }

            /* Confetti celebration styles */
            .honua-confetti {
                position: fixed;
                width: 10px;
                height: 10px;
                background: var(--mud-palette-primary);
                position: absolute;
                animation: honua-confetti-fall 3s linear forwards;
                z-index: 10000;
            }

            @keyframes honua-confetti-fall {
                to {
                    transform: translateY(100vh) rotate(360deg);
                    opacity: 0;
                }
            }
        `;
        document.head.appendChild(style);
    },

    /**
     * Create and start a tour
     * @param {object} tourConfig - Tour configuration
     * @returns {object} Tour instance
     */
    createTour: function(tourConfig) {
        // Cancel any active tour
        if (this.activeTour) {
            this.activeTour.cancel();
        }

        const tour = new Shepherd.Tour({
            defaultStepOptions: {
                classes: 'shepherd-theme-honua',
                scrollTo: { behavior: 'smooth', block: 'center' },
                cancelIcon: {
                    enabled: true
                },
                modalOverlayOpeningPadding: 4,
                modalOverlayOpeningRadius: 4,
                ...tourConfig.defaultStepOptions
            },
            useModalOverlay: tourConfig.useModalOverlay !== false,
            tourName: tourConfig.id
        });

        // Add steps
        tourConfig.steps.forEach((stepConfig, index) => {
            const step = {
                id: stepConfig.id || `step-${index}`,
                title: stepConfig.title,
                text: stepConfig.text,
                attachTo: stepConfig.attachTo,
                buttons: this.createStepButtons(stepConfig, index, tourConfig.steps.length),
                beforeShowPromise: stepConfig.beforeShow ? () => this.executeFunction(stepConfig.beforeShow) : undefined,
                when: {
                    show: () => this.highlightElement(stepConfig.attachTo?.element),
                    hide: () => this.unhighlightElement(stepConfig.attachTo?.element)
                },
                ...stepConfig.options
            };

            tour.addStep(step);
        });

        // Tour event handlers
        tour.on('complete', () => {
            this.markTourComplete(tourConfig.id);
            this.unhighlightAll();
            if (tourConfig.onComplete) {
                this.executeFunction(tourConfig.onComplete);
            }
            this.celebrateCompletion();
            this.activeTour = null;
        });

        tour.on('cancel', () => {
            this.unhighlightAll();
            if (tourConfig.onCancel) {
                this.executeFunction(tourConfig.onCancel);
            }
            this.activeTour = null;
        });

        this.activeTour = tour;
        this.tourSteps[tourConfig.id] = tourConfig.steps.length;

        return tour;
    },

    /**
     * Create navigation buttons for a tour step
     */
    createStepButtons: function(stepConfig, index, totalSteps) {
        if (stepConfig.buttons) {
            return stepConfig.buttons;
        }

        const buttons = [];

        // Back button (not on first step)
        if (index > 0) {
            buttons.push({
                text: 'Back',
                classes: 'shepherd-button-secondary',
                action() {
                    this.back();
                }
            });
        }

        // Skip button (not on last step)
        if (index < totalSteps - 1) {
            buttons.push({
                text: 'Skip Tour',
                classes: 'shepherd-button-secondary',
                action() {
                    this.cancel();
                }
            });
        }

        // Next/Complete button
        if (index < totalSteps - 1) {
            buttons.push({
                text: 'Next',
                action() {
                    this.next();
                }
            });
        } else {
            buttons.push({
                text: 'Complete',
                action() {
                    this.complete();
                }
            });
        }

        return buttons;
    },

    /**
     * Highlight a target element
     */
    highlightElement: function(selector) {
        if (!selector) return;

        const element = document.querySelector(selector);
        if (element) {
            element.classList.add('honua-tour-highlight', 'honua-tour-pulse');
        }
    },

    /**
     * Remove highlight from element
     */
    unhighlightElement: function(selector) {
        if (!selector) return;

        const element = document.querySelector(selector);
        if (element) {
            element.classList.remove('honua-tour-highlight', 'honua-tour-pulse');
        }
    },

    /**
     * Remove all highlights
     */
    unhighlightAll: function() {
        document.querySelectorAll('.honua-tour-highlight').forEach(el => {
            el.classList.remove('honua-tour-highlight', 'honua-tour-pulse');
        });
    },

    /**
     * Mark tour as completed
     */
    markTourComplete: function(tourId) {
        this.completedTours.add(tourId);
        localStorage.setItem('honua-completed-tours', JSON.stringify([...this.completedTours]));

        // Notify Blazor
        if (window.DotNet) {
            try {
                DotNet.invokeMethodAsync('Honua.Admin.Blazor', 'OnTourCompleted', tourId);
            } catch (e) {
                console.warn('Failed to notify Blazor of tour completion:', e);
            }
        }
    },

    /**
     * Check if tour is completed
     */
    isTourCompleted: function(tourId) {
        return this.completedTours.has(tourId);
    },

    /**
     * Reset tour completion status
     */
    resetTour: function(tourId) {
        this.completedTours.delete(tourId);
        localStorage.setItem('honua-completed-tours', JSON.stringify([...this.completedTours]));
    },

    /**
     * Reset all tours
     */
    resetAllTours: function() {
        this.completedTours.clear();
        localStorage.removeItem('honua-completed-tours');
    },

    /**
     * Get completed tours list
     */
    getCompletedTours: function() {
        return [...this.completedTours];
    },

    /**
     * Celebrate tour completion with confetti
     */
    celebrateCompletion: function() {
        const colors = ['#1976d2', '#4caf50', '#ff9800', '#f44336', '#9c27b0'];
        const confettiCount = 50;

        for (let i = 0; i < confettiCount; i++) {
            setTimeout(() => {
                const confetti = document.createElement('div');
                confetti.className = 'honua-confetti';
                confetti.style.left = Math.random() * 100 + '%';
                confetti.style.top = '-10px';
                confetti.style.background = colors[Math.floor(Math.random() * colors.length)];
                confetti.style.animationDelay = Math.random() * 0.3 + 's';
                confetti.style.animationDuration = (Math.random() * 2 + 2) + 's';

                document.body.appendChild(confetti);

                setTimeout(() => confetti.remove(), 3000);
            }, i * 30);
        }
    },

    /**
     * Execute a function by name
     */
    executeFunction: function(functionName) {
        if (typeof functionName === 'function') {
            return functionName();
        }

        if (typeof functionName === 'string') {
            const fn = this.resolvePath(window, functionName);
            if (typeof fn === 'function') {
                return fn();
            }
        }

        return Promise.resolve();
    },

    /**
     * Resolve a dotted path in an object
     */
    resolvePath: function(obj, path) {
        return path.split('.').reduce((prev, curr) => prev?.[curr], obj);
    },

    /**
     * Start a tour by ID
     */
    startTour: function(tourId, tourConfig) {
        const tour = this.createTour(tourConfig);
        tour.start();
        return tour;
    },

    /**
     * Cancel the active tour
     */
    cancelActiveTour: function() {
        if (this.activeTour) {
            this.activeTour.cancel();
        }
    },

    /**
     * Get tour progress
     */
    getTourProgress: function() {
        const completed = this.completedTours.size;
        const total = Object.keys(this.tourSteps).length || 5; // Default 5 tours
        return {
            completed,
            total,
            percentage: total > 0 ? Math.round((completed / total) * 100) : 0
        };
    }
};

// Initialize on load
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => window.HonuaTours.initialize());
} else {
    window.HonuaTours.initialize();
}
