/**
 * HonuaDataGrid JavaScript Utilities
 * Provides helper functions for the HonuaDataGrid Blazor component
 */

/**
 * Download a file from base64 data
 * @param {string} filename - Name of the file to download
 * @param {string} base64Content - Base64 encoded file content
 * @param {string} contentType - MIME type of the file
 */
window.downloadFile = function (filename, base64Content, contentType) {
    try {
        // Convert base64 to blob
        const byteCharacters = atob(base64Content);
        const byteNumbers = new Array(byteCharacters.length);

        for (let i = 0; i < byteCharacters.length; i++) {
            byteNumbers[i] = byteCharacters.charCodeAt(i);
        }

        const byteArray = new Uint8Array(byteNumbers);
        const blob = new Blob([byteArray], { type: contentType });

        // Create download link
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = filename;

        // Trigger download
        document.body.appendChild(link);
        link.click();

        // Cleanup
        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);

        console.log(`Downloaded file: ${filename}`);
    } catch (error) {
        console.error('Error downloading file:', error);
    }
};

/**
 * Copy text to clipboard
 * @param {string} text - Text to copy
 * @returns {Promise<boolean>} Success status
 */
window.copyToClipboard = async function (text) {
    try {
        await navigator.clipboard.writeText(text);
        console.log('Text copied to clipboard');
        return true;
    } catch (error) {
        console.error('Error copying to clipboard:', error);
        return false;
    }
};

/**
 * Format a date string for display
 * @param {string} dateString - ISO date string
 * @param {string} locale - Locale for formatting (default: 'en-US')
 * @returns {string} Formatted date string
 */
window.formatDate = function (dateString, locale = 'en-US') {
    try {
        const date = new Date(dateString);
        return date.toLocaleDateString(locale, {
            year: 'numeric',
            month: 'short',
            day: 'numeric',
            hour: '2-digit',
            minute: '2-digit'
        });
    } catch (error) {
        console.error('Error formatting date:', error);
        return dateString;
    }
};

/**
 * Scroll to a specific row in the grid
 * @param {string} gridId - ID of the grid container
 * @param {number} rowIndex - Index of the row to scroll to
 */
window.scrollToRow = function (gridId, rowIndex) {
    try {
        const grid = document.getElementById(gridId);
        if (!grid) {
            console.warn(`Grid not found: ${gridId}`);
            return;
        }

        const rows = grid.querySelectorAll('.mud-table-row');
        if (rowIndex >= 0 && rowIndex < rows.length) {
            rows[rowIndex].scrollIntoView({
                behavior: 'smooth',
                block: 'center'
            });
        }
    } catch (error) {
        console.error('Error scrolling to row:', error);
    }
};

/**
 * Get the current scroll position of the grid
 * @param {string} gridId - ID of the grid container
 * @returns {object} Scroll position {top, left}
 */
window.getScrollPosition = function (gridId) {
    try {
        const grid = document.getElementById(gridId);
        if (!grid) {
            return { top: 0, left: 0 };
        }

        const container = grid.querySelector('.mud-table-container');
        if (container) {
            return {
                top: container.scrollTop,
                left: container.scrollLeft
            };
        }

        return { top: 0, left: 0 };
    } catch (error) {
        console.error('Error getting scroll position:', error);
        return { top: 0, left: 0 };
    }
};

/**
 * Set the scroll position of the grid
 * @param {string} gridId - ID of the grid container
 * @param {number} top - Top scroll position
 * @param {number} left - Left scroll position
 */
window.setScrollPosition = function (gridId, top, left) {
    try {
        const grid = document.getElementById(gridId);
        if (!grid) {
            console.warn(`Grid not found: ${gridId}`);
            return;
        }

        const container = grid.querySelector('.mud-table-container');
        if (container) {
            container.scrollTop = top;
            container.scrollLeft = left;
        }
    } catch (error) {
        console.error('Error setting scroll position:', error);
    }
};

/**
 * Resize columns to fit content
 * @param {string} gridId - ID of the grid container
 */
window.autoResizeColumns = function (gridId) {
    try {
        const grid = document.getElementById(gridId);
        if (!grid) {
            console.warn(`Grid not found: ${gridId}`);
            return;
        }

        // This is a placeholder - actual implementation would measure content
        // and adjust column widths accordingly
        console.log('Auto-resize columns requested for:', gridId);
    } catch (error) {
        console.error('Error auto-resizing columns:', error);
    }
};

console.log('HonuaDataGrid utilities loaded');
