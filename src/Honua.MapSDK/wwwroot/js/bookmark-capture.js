// Bookmark Thumbnail Capture Module
// Captures map canvas as thumbnail image for bookmarks

/**
 * Capture a thumbnail of the map canvas
 * @param {string} mapId - The ID of the map container
 * @param {number} width - Thumbnail width in pixels
 * @param {number} height - Thumbnail height in pixels
 * @returns {Promise<string>} Base64 data URL of the thumbnail
 */
export async function captureMapThumbnail(mapId, width = 200, height = 150) {
    try {
        // Find the map container
        const mapContainer = document.getElementById(mapId);
        if (!mapContainer) {
            console.error(`Map container not found: ${mapId}`);
            return null;
        }

        // Find the canvas element
        const canvas = mapContainer.querySelector('canvas');
        if (!canvas) {
            console.error(`Canvas not found in map: ${mapId}`);
            return null;
        }

        // Create a temporary canvas for resizing
        const thumbnailCanvas = document.createElement('canvas');
        thumbnailCanvas.width = width;
        thumbnailCanvas.height = height;
        const ctx = thumbnailCanvas.getContext('2d');

        // Draw the map canvas to the thumbnail canvas (scaled)
        ctx.drawImage(canvas, 0, 0, canvas.width, canvas.height, 0, 0, width, height);

        // Convert to data URL (base64)
        const dataUrl = thumbnailCanvas.toDataURL('image/jpeg', 0.8);

        return dataUrl;
    } catch (error) {
        console.error('Error capturing map thumbnail:', error);
        return null;
    }
}

/**
 * Capture a high-quality screenshot of the map
 * @param {string} mapId - The ID of the map container
 * @returns {Promise<string>} Base64 data URL of the screenshot
 */
export async function captureMapScreenshot(mapId) {
    try {
        const mapContainer = document.getElementById(mapId);
        if (!mapContainer) {
            console.error(`Map container not found: ${mapId}`);
            return null;
        }

        const canvas = mapContainer.querySelector('canvas');
        if (!canvas) {
            console.error(`Canvas not found in map: ${mapId}`);
            return null;
        }

        // Convert to high-quality PNG
        const dataUrl = canvas.toDataURL('image/png', 1.0);
        return dataUrl;
    } catch (error) {
        console.error('Error capturing map screenshot:', error);
        return null;
    }
}

/**
 * Download a thumbnail as a file
 * @param {string} mapId - The ID of the map container
 * @param {string} filename - Filename for the download
 */
export async function downloadMapThumbnail(mapId, filename = 'map-thumbnail.jpg') {
    try {
        const dataUrl = await captureMapScreenshot(mapId);
        if (!dataUrl) {
            return;
        }

        // Create a temporary link and trigger download
        const link = document.createElement('a');
        link.href = dataUrl;
        link.download = filename;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    } catch (error) {
        console.error('Error downloading map thumbnail:', error);
    }
}

/**
 * Copy map thumbnail to clipboard
 * @param {string} mapId - The ID of the map container
 */
export async function copyMapThumbnailToClipboard(mapId) {
    try {
        const mapContainer = document.getElementById(mapId);
        if (!mapContainer) {
            console.error(`Map container not found: ${mapId}`);
            return false;
        }

        const canvas = mapContainer.querySelector('canvas');
        if (!canvas) {
            console.error(`Canvas not found in map: ${mapId}`);
            return false;
        }

        // Convert canvas to blob
        return new Promise((resolve) => {
            canvas.toBlob(async (blob) => {
                try {
                    await navigator.clipboard.write([
                        new ClipboardItem({
                            'image/png': blob
                        })
                    ]);
                    resolve(true);
                } catch (error) {
                    console.error('Error copying to clipboard:', error);
                    resolve(false);
                }
            });
        });
    } catch (error) {
        console.error('Error copying map thumbnail:', error);
        return false;
    }
}

/**
 * Generate a thumbnail with custom size and quality
 * @param {string} mapId - The ID of the map container
 * @param {Object} options - Options for thumbnail generation
 * @returns {Promise<string>} Base64 data URL of the thumbnail
 */
export async function generateThumbnail(mapId, options = {}) {
    const {
        width = 200,
        height = 150,
        format = 'jpeg',
        quality = 0.8,
        cropToAspectRatio = true
    } = options;

    try {
        const mapContainer = document.getElementById(mapId);
        if (!mapContainer) {
            return null;
        }

        const canvas = mapContainer.querySelector('canvas');
        if (!canvas) {
            return null;
        }

        // Create thumbnail canvas
        const thumbnailCanvas = document.createElement('canvas');
        thumbnailCanvas.width = width;
        thumbnailCanvas.height = height;
        const ctx = thumbnailCanvas.getContext('2d');

        if (cropToAspectRatio) {
            // Calculate crop dimensions to maintain aspect ratio
            const canvasAspect = canvas.width / canvas.height;
            const targetAspect = width / height;

            let sourceWidth = canvas.width;
            let sourceHeight = canvas.height;
            let sourceX = 0;
            let sourceY = 0;

            if (canvasAspect > targetAspect) {
                // Canvas is wider, crop width
                sourceWidth = canvas.height * targetAspect;
                sourceX = (canvas.width - sourceWidth) / 2;
            } else {
                // Canvas is taller, crop height
                sourceHeight = canvas.width / targetAspect;
                sourceY = (canvas.height - sourceHeight) / 2;
            }

            ctx.drawImage(
                canvas,
                sourceX, sourceY, sourceWidth, sourceHeight,
                0, 0, width, height
            );
        } else {
            // Simple scale without cropping
            ctx.drawImage(canvas, 0, 0, canvas.width, canvas.height, 0, 0, width, height);
        }

        // Convert to data URL
        const mimeType = format === 'png' ? 'image/png' : 'image/jpeg';
        const dataUrl = thumbnailCanvas.toDataURL(mimeType, quality);

        return dataUrl;
    } catch (error) {
        console.error('Error generating thumbnail:', error);
        return null;
    }
}

/**
 * Check if the browser supports thumbnail capture
 * @returns {boolean} True if supported
 */
export function isThumbnailCaptureSupported() {
    try {
        const canvas = document.createElement('canvas');
        return !!(canvas.getContext && canvas.getContext('2d'));
    } catch (error) {
        return false;
    }
}

/**
 * Estimate the size of a thumbnail in bytes
 * @param {string} dataUrl - Base64 data URL
 * @returns {number} Size in bytes
 */
export function estimateThumbnailSize(dataUrl) {
    if (!dataUrl) return 0;

    // Remove data URL prefix
    const base64 = dataUrl.split(',')[1];
    if (!base64) return 0;

    // Calculate size (base64 is ~33% larger than binary)
    return Math.ceil((base64.length * 3) / 4);
}

/**
 * Compress a thumbnail to target size
 * @param {string} mapId - The ID of the map container
 * @param {number} targetSizeKB - Target size in kilobytes
 * @returns {Promise<string>} Compressed thumbnail data URL
 */
export async function compressThumbnail(mapId, targetSizeKB = 50) {
    const targetBytes = targetSizeKB * 1024;
    let quality = 0.9;
    let dataUrl = null;

    // Binary search for optimal quality
    let minQuality = 0.1;
    let maxQuality = 0.9;

    while (maxQuality - minQuality > 0.05) {
        quality = (minQuality + maxQuality) / 2;

        dataUrl = await generateThumbnail(mapId, {
            width: 200,
            height: 150,
            format: 'jpeg',
            quality: quality
        });

        const size = estimateThumbnailSize(dataUrl);

        if (size > targetBytes) {
            maxQuality = quality;
        } else {
            minQuality = quality;
        }
    }

    return dataUrl;
}

/**
 * Create a thumbnail with annotation overlay
 * @param {string} mapId - The ID of the map container
 * @param {string} text - Text to overlay
 * @param {Object} options - Style options
 * @returns {Promise<string>} Thumbnail with annotation
 */
export async function createAnnotatedThumbnail(mapId, text, options = {}) {
    const {
        width = 200,
        height = 150,
        fontSize = 12,
        fontColor = 'white',
        backgroundColor = 'rgba(0, 0, 0, 0.6)',
        position = 'bottom' // 'top', 'bottom', 'center'
    } = options;

    try {
        // First get the base thumbnail
        const thumbnailUrl = await captureMapThumbnail(mapId, width, height);
        if (!thumbnailUrl) return null;

        // Create a canvas for annotation
        const canvas = document.createElement('canvas');
        canvas.width = width;
        canvas.height = height;
        const ctx = canvas.getContext('2d');

        // Load and draw the thumbnail
        return new Promise((resolve) => {
            const img = new Image();
            img.onload = () => {
                ctx.drawImage(img, 0, 0);

                // Draw annotation
                ctx.font = `${fontSize}px Arial`;
                ctx.fillStyle = backgroundColor;
                ctx.textAlign = 'center';

                const padding = 8;
                const textWidth = ctx.measureText(text).width;
                const boxHeight = fontSize + padding * 2;

                let y;
                if (position === 'top') {
                    y = 0;
                } else if (position === 'center') {
                    y = (height - boxHeight) / 2;
                } else {
                    y = height - boxHeight;
                }

                // Draw background
                ctx.fillRect(0, y, width, boxHeight);

                // Draw text
                ctx.fillStyle = fontColor;
                ctx.fillText(text, width / 2, y + fontSize + padding / 2);

                resolve(canvas.toDataURL('image/jpeg', 0.8));
            };
            img.src = thumbnailUrl;
        });
    } catch (error) {
        console.error('Error creating annotated thumbnail:', error);
        return null;
    }
}
