// Honua Compare - Side-by-side map comparison component
// Provides swipe, overlay, flicker, spy glass, and side-by-side comparison modes

import maplibregl from 'https://cdn.jsdelivr.net/npm/maplibre-gl@4.7.1/+esm';

class HonuaCompare {
    constructor(container, options, dotNetRef) {
        this.container = container;
        this.options = options;
        this.dotNetRef = dotNetRef;
        this.mode = options.mode || 'swipe';
        this.orientation = options.orientation || 'vertical';
        this.syncNavigation = options.syncNavigation !== false;
        this.dividerPosition = options.dividerPosition || 0.5;

        this.leftMap = null;
        this.rightMap = null;
        this.divider = null;
        this.isDragging = false;
        this.isFlickering = false;
        this.flickerTimer = null;
        this.spyGlassActive = false;

        this.initialize();
    }

    initialize() {
        // Create map containers
        this.leftContainer = document.createElement('div');
        this.leftContainer.className = 'compare-map compare-map-left';
        this.leftContainer.style.cssText = 'position: absolute; top: 0; left: 0; bottom: 0; right: 0;';

        this.rightContainer = document.createElement('div');
        this.rightContainer.className = 'compare-map compare-map-right';
        this.rightContainer.style.cssText = 'position: absolute; top: 0; left: 0; bottom: 0; right: 0;';

        this.container.appendChild(this.leftContainer);
        this.container.appendChild(this.rightContainer);

        // Initialize maps
        this.leftMap = new maplibregl.Map({
            container: this.leftContainer,
            style: this.options.leftStyle,
            center: this.options.center || [0, 0],
            zoom: this.options.zoom || 2,
            bearing: this.options.bearing || 0,
            pitch: this.options.pitch || 0,
            attributionControl: false
        });

        this.rightMap = new maplibregl.Map({
            container: this.rightContainer,
            style: this.options.rightStyle,
            center: this.options.center || [0, 0],
            zoom: this.options.zoom || 2,
            bearing: this.options.bearing || 0,
            pitch: this.options.pitch || 0,
            attributionControl: false
        });

        // Add attribution to right map only
        this.rightMap.addControl(new maplibregl.AttributionControl({
            compact: true
        }));

        // Wait for both maps to load
        Promise.all([
            new Promise(resolve => this.leftMap.on('load', resolve)),
            new Promise(resolve => this.rightMap.on('load', resolve))
        ]).then(() => {
            this.setupMode();
            this.setupSync();
            this.setupEventListeners();
        });
    }

    setupMode() {
        switch (this.mode) {
            case 'sidebyside':
                this.setupSideBySide();
                break;
            case 'swipe':
                this.setupSwipe();
                break;
            case 'overlay':
                this.setupOverlay();
                break;
            case 'flicker':
                this.setupFlicker();
                break;
            case 'spyglass':
                this.setupSpyGlass();
                break;
            default:
                this.setupSwipe();
        }
    }

    setupSideBySide() {
        this.clearMode();

        if (this.orientation === 'vertical') {
            const leftWidth = this.dividerPosition * 100;
            this.leftContainer.style.cssText = `position: absolute; top: 0; left: 0; bottom: 0; width: ${leftWidth}%;`;
            this.rightContainer.style.cssText = `position: absolute; top: 0; right: 0; bottom: 0; width: ${100 - leftWidth}%;`;
        } else {
            const topHeight = this.dividerPosition * 100;
            this.leftContainer.style.cssText = `position: absolute; top: 0; left: 0; right: 0; height: ${topHeight}%;`;
            this.rightContainer.style.cssText = `position: absolute; bottom: 0; left: 0; right: 0; height: ${100 - topHeight}%;`;
        }

        this.createDivider(false);
        this.leftMap.resize();
        this.rightMap.resize();
    }

    setupSwipe() {
        this.clearMode();

        this.leftContainer.style.cssText = 'position: absolute; top: 0; left: 0; bottom: 0; right: 0;';
        this.rightContainer.style.cssText = 'position: absolute; top: 0; left: 0; bottom: 0; right: 0;';

        if (this.orientation === 'vertical') {
            const clipLeft = this.dividerPosition * 100;
            this.leftContainer.style.clipPath = `inset(0 ${100 - clipLeft}% 0 0)`;
            this.rightContainer.style.clipPath = `inset(0 0 0 ${clipLeft}%)`;
        } else {
            const clipTop = this.dividerPosition * 100;
            this.leftContainer.style.clipPath = `inset(0 0 ${100 - clipTop}% 0)`;
            this.rightContainer.style.clipPath = `inset(${clipTop}% 0 0 0)`;
        }

        this.createDivider(true);
        this.leftMap.resize();
        this.rightMap.resize();
    }

    setupOverlay() {
        this.clearMode();

        this.leftContainer.style.cssText = 'position: absolute; top: 0; left: 0; bottom: 0; right: 0;';
        this.rightContainer.style.cssText = 'position: absolute; top: 0; left: 0; bottom: 0; right: 0;';
        this.rightContainer.style.opacity = this.options.overlayOpacity || 0.5;

        this.leftContainer.style.clipPath = 'none';
        this.rightContainer.style.clipPath = 'none';

        this.leftMap.resize();
        this.rightMap.resize();
    }

    setupFlicker() {
        this.clearMode();

        this.leftContainer.style.cssText = 'position: absolute; top: 0; left: 0; bottom: 0; right: 0;';
        this.rightContainer.style.cssText = 'position: absolute; top: 0; left: 0; bottom: 0; right: 0; opacity: 0;';

        this.leftContainer.style.clipPath = 'none';
        this.rightContainer.style.clipPath = 'none';

        this.isFlickering = true;
        this.startFlicker();

        this.leftMap.resize();
        this.rightMap.resize();
    }

    setupSpyGlass() {
        this.clearMode();

        this.leftContainer.style.cssText = 'position: absolute; top: 0; left: 0; bottom: 0; right: 0;';
        this.rightContainer.style.cssText = 'position: absolute; top: 0; left: 0; bottom: 0; right: 0;';

        this.spyGlassActive = true;
        const radius = this.options.spyGlassRadius || 150;

        // Position spy glass in center initially
        const centerX = this.container.clientWidth / 2;
        const centerY = this.container.clientHeight / 2;

        this.rightContainer.style.clipPath = `circle(${radius}px at ${centerX}px ${centerY}px)`;

        this.setupSpyGlassMovement();

        this.leftMap.resize();
        this.rightMap.resize();
    }

    setupSpyGlassMovement() {
        const radius = this.options.spyGlassRadius || 150;

        const moveSpyGlass = (e) => {
            if (!this.spyGlassActive) return;

            const rect = this.container.getBoundingClientRect();
            const x = e.clientX - rect.left;
            const y = e.clientY - rect.top;

            this.rightContainer.style.clipPath = `circle(${radius}px at ${x}px ${y}px)`;
        };

        this.container.addEventListener('mousemove', moveSpyGlass);
        this.container.addEventListener('touchmove', (e) => {
            if (e.touches.length > 0) {
                moveSpyGlass(e.touches[0]);
            }
        });
    }

    startFlicker() {
        if (!this.isFlickering) return;

        let showRight = false;
        this.flickerTimer = setInterval(() => {
            if (!this.isFlickering) {
                clearInterval(this.flickerTimer);
                return;
            }

            showRight = !showRight;
            this.leftContainer.style.opacity = showRight ? '0' : '1';
            this.rightContainer.style.opacity = showRight ? '1' : '0';
        }, this.options.flickerInterval || 1000);
    }

    createDivider(draggable) {
        if (this.divider) {
            this.divider.remove();
        }

        this.divider = document.createElement('div');
        this.divider.className = 'compare-divider';

        if (this.orientation === 'vertical') {
            const leftPos = this.dividerPosition * 100;
            this.divider.style.cssText = `
                position: absolute;
                top: 0;
                bottom: 0;
                left: ${leftPos}%;
                width: 4px;
                background: white;
                cursor: ${draggable ? 'ew-resize' : 'default'};
                z-index: 100;
                box-shadow: 0 0 8px rgba(0,0,0,0.3);
                transform: translateX(-50%);
            `;
        } else {
            const topPos = this.dividerPosition * 100;
            this.divider.style.cssText = `
                position: absolute;
                left: 0;
                right: 0;
                top: ${topPos}%;
                height: 4px;
                background: white;
                cursor: ${draggable ? 'ns-resize' : 'default'};
                z-index: 100;
                box-shadow: 0 0 8px rgba(0,0,0,0.3);
                transform: translateY(-50%);
            `;
        }

        if (draggable) {
            const handle = document.createElement('div');
            handle.className = 'compare-divider-handle';

            if (this.orientation === 'vertical') {
                handle.style.cssText = `
                    position: absolute;
                    top: 50%;
                    left: 50%;
                    transform: translate(-50%, -50%);
                    width: 32px;
                    height: 64px;
                    background: white;
                    border-radius: 32px;
                    box-shadow: 0 2px 8px rgba(0,0,0,0.2);
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    font-size: 20px;
                    color: #666;
                `;
                // Use textContent instead of innerHTML to prevent XSS
                handle.textContent = '⇄';
            } else {
                handle.style.cssText = `
                    position: absolute;
                    top: 50%;
                    left: 50%;
                    transform: translate(-50%, -50%);
                    width: 64px;
                    height: 32px;
                    background: white;
                    border-radius: 32px;
                    box-shadow: 0 2px 8px rgba(0,0,0,0.2);
                    display: flex;
                    align-items: center;
                    justify-content: center;
                    font-size: 20px;
                    color: #666;
                `;
                // Use textContent instead of innerHTML to prevent XSS
                handle.textContent = '⇅';
            }

            this.divider.appendChild(handle);
            this.setupDividerDrag();
        }

        this.container.appendChild(this.divider);
    }

    setupDividerDrag() {
        const startDrag = (e) => {
            e.preventDefault();
            this.isDragging = true;
            document.body.style.cursor = this.orientation === 'vertical' ? 'ew-resize' : 'ns-resize';
        };

        const drag = (e) => {
            if (!this.isDragging) return;

            const rect = this.container.getBoundingClientRect();
            let position;

            if (this.orientation === 'vertical') {
                const clientX = e.clientX || (e.touches && e.touches[0].clientX);
                position = (clientX - rect.left) / rect.width;
            } else {
                const clientY = e.clientY || (e.touches && e.touches[0].clientY);
                position = (clientY - rect.top) / rect.height;
            }

            position = Math.max(0.1, Math.min(0.9, position));
            this.setDividerPosition(position);
        };

        const endDrag = () => {
            if (this.isDragging) {
                this.isDragging = false;
                document.body.style.cursor = '';

                if (this.dotNetRef) {
                    this.dotNetRef.invokeMethodAsync('OnDividerPositionChangedInternal', this.dividerPosition);
                }
            }
        };

        this.divider.addEventListener('mousedown', startDrag);
        this.divider.addEventListener('touchstart', startDrag);
        document.addEventListener('mousemove', drag);
        document.addEventListener('touchmove', drag);
        document.addEventListener('mouseup', endDrag);
        document.addEventListener('touchend', endDrag);
    }

    setupSync() {
        if (!this.syncNavigation) return;

        const syncMove = (source, target) => {
            target.jumpTo({
                center: source.getCenter(),
                zoom: source.getZoom(),
                bearing: source.getBearing(),
                pitch: source.getPitch()
            });
        };

        this.leftMap.on('move', () => {
            if (this.syncNavigation && !this.rightMap._moving) {
                syncMove(this.leftMap, this.rightMap);
            }
        });

        this.rightMap.on('move', () => {
            if (this.syncNavigation && !this.leftMap._moving) {
                syncMove(this.rightMap, this.leftMap);
            }
        });
    }

    setupEventListeners() {
        // Notify Blazor of view changes
        const notifyViewChange = () => {
            if (this.dotNetRef) {
                const center = this.leftMap.getCenter().toArray();
                const zoom = this.leftMap.getZoom();
                const bearing = this.leftMap.getBearing();
                const pitch = this.leftMap.getPitch();

                this.dotNetRef.invokeMethodAsync('OnViewChangedInternal', center, zoom, bearing, pitch);
            }
        };

        this.leftMap.on('moveend', notifyViewChange);
    }

    setMode(mode) {
        this.mode = mode;
        this.setupMode();
    }

    setDividerPosition(position) {
        this.dividerPosition = position;

        if (this.mode === 'swipe') {
            if (this.orientation === 'vertical') {
                const clipLeft = position * 100;
                this.leftContainer.style.clipPath = `inset(0 ${100 - clipLeft}% 0 0)`;
                this.rightContainer.style.clipPath = `inset(0 0 0 ${clipLeft}%)`;

                if (this.divider) {
                    this.divider.style.left = `${clipLeft}%`;
                }
            } else {
                const clipTop = position * 100;
                this.leftContainer.style.clipPath = `inset(0 0 ${100 - clipTop}% 0)`;
                this.rightContainer.style.clipPath = `inset(${clipTop}% 0 0 0)`;

                if (this.divider) {
                    this.divider.style.top = `${clipTop}%`;
                }
            }
        } else if (this.mode === 'sidebyside') {
            if (this.orientation === 'vertical') {
                const leftWidth = position * 100;
                this.leftContainer.style.width = `${leftWidth}%`;
                this.rightContainer.style.width = `${100 - leftWidth}%`;

                if (this.divider) {
                    this.divider.style.left = `${leftWidth}%`;
                }
            } else {
                const topHeight = position * 100;
                this.leftContainer.style.height = `${topHeight}%`;
                this.rightContainer.style.height = `${100 - topHeight}%`;

                if (this.divider) {
                    this.divider.style.top = `${topHeight}%`;
                }
            }

            this.leftMap.resize();
            this.rightMap.resize();
        }
    }

    setOrientation(orientation) {
        this.orientation = orientation;
        this.setupMode();
    }

    setSyncNavigation(sync) {
        this.syncNavigation = sync;

        if (sync) {
            // Sync right map to left map
            this.rightMap.jumpTo({
                center: this.leftMap.getCenter(),
                zoom: this.leftMap.getZoom(),
                bearing: this.leftMap.getBearing(),
                pitch: this.leftMap.getPitch()
            });
        }
    }

    setOpacity(opacity) {
        if (this.mode === 'overlay') {
            this.rightContainer.style.opacity = opacity;
        }
    }

    clearMode() {
        // Stop flicker
        this.isFlickering = false;
        if (this.flickerTimer) {
            clearInterval(this.flickerTimer);
            this.flickerTimer = null;
        }

        // Reset spy glass
        this.spyGlassActive = false;

        // Remove divider
        if (this.divider) {
            this.divider.remove();
            this.divider = null;
        }

        // Reset styles
        this.leftContainer.style.opacity = '1';
        this.rightContainer.style.opacity = '1';
        this.leftContainer.style.clipPath = 'none';
        this.rightContainer.style.clipPath = 'none';
    }

    flyTo(center, zoom, bearing, pitch) {
        const options = { center, zoom };
        if (bearing !== null && bearing !== undefined) options.bearing = bearing;
        if (pitch !== null && pitch !== undefined) options.pitch = pitch;

        this.leftMap.flyTo(options);
        if (this.syncNavigation) {
            this.rightMap.flyTo(options);
        }
    }

    updateLeftStyle(styleUrl) {
        this.leftMap.setStyle(styleUrl);
    }

    updateRightStyle(styleUrl) {
        this.rightMap.setStyle(styleUrl);
    }

    captureScreenshot() {
        return new Promise((resolve) => {
            // Create canvas to combine both maps
            const canvas = document.createElement('canvas');
            const width = this.container.clientWidth;
            const height = this.container.clientHeight;
            canvas.width = width;
            canvas.height = height;
            const ctx = canvas.getContext('2d');

            // Get map canvases
            const leftCanvas = this.leftMap.getCanvas();
            const rightCanvas = this.rightMap.getCanvas();

            // Draw based on mode
            if (this.mode === 'sidebyside') {
                if (this.orientation === 'vertical') {
                    const splitX = width * this.dividerPosition;
                    ctx.drawImage(leftCanvas, 0, 0, splitX, height, 0, 0, splitX, height);
                    ctx.drawImage(rightCanvas, splitX, 0, width - splitX, height, splitX, 0, width - splitX, height);
                } else {
                    const splitY = height * this.dividerPosition;
                    ctx.drawImage(leftCanvas, 0, 0, width, splitY, 0, 0, width, splitY);
                    ctx.drawImage(rightCanvas, 0, splitY, width, height - splitY, 0, splitY, width, height - splitY);
                }
            } else {
                ctx.drawImage(leftCanvas, 0, 0);
                ctx.globalAlpha = this.mode === 'overlay' ? (this.options.overlayOpacity || 0.5) : 1;
                ctx.drawImage(rightCanvas, 0, 0);
            }

            resolve(canvas.toDataURL('image/png'));
        });
    }

    toggleFullscreen() {
        const elem = this.container.parentElement;

        if (!document.fullscreenElement) {
            if (elem.requestFullscreen) {
                elem.requestFullscreen();
            } else if (elem.webkitRequestFullscreen) {
                elem.webkitRequestFullscreen();
            } else if (elem.msRequestFullscreen) {
                elem.msRequestFullscreen();
            }
        } else {
            if (document.exitFullscreen) {
                document.exitFullscreen();
            } else if (document.webkitExitFullscreen) {
                document.webkitExitFullscreen();
            } else if (document.msExitFullscreen) {
                document.msExitFullscreen();
            }
        }

        // Resize maps after fullscreen change
        setTimeout(() => {
            this.leftMap.resize();
            this.rightMap.resize();
        }, 100);
    }

    dispose() {
        this.isFlickering = false;
        if (this.flickerTimer) {
            clearInterval(this.flickerTimer);
        }

        if (this.divider) {
            this.divider.remove();
        }

        if (this.leftMap) {
            this.leftMap.remove();
        }

        if (this.rightMap) {
            this.rightMap.remove();
        }
    }
}

export function createCompare(container, options, dotNetRef) {
    return new HonuaCompare(container, options, dotNetRef);
}
