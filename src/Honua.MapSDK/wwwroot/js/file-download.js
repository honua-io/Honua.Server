// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

/**
 * Downloads a file from a base64-encoded string.
 * @param {string} fileName - The name of the file to download.
 * @param {string} contentType - The MIME type of the file.
 * @param {string} base64Data - The base64-encoded file data.
 */
window.downloadFile = function (fileName, contentType, base64Data) {
    try {
        // Convert base64 to blob
        const byteCharacters = atob(base64Data);
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
        link.download = fileName;

        // Trigger download
        document.body.appendChild(link);
        link.click();

        // Cleanup
        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);

        console.log(`File downloaded: ${fileName}`);
    } catch (error) {
        console.error('Error downloading file:', error);
        throw error;
    }
};

/**
 * Downloads a file from a Uint8Array.
 * @param {string} fileName - The name of the file to download.
 * @param {string} contentType - The MIME type of the file.
 * @param {Uint8Array} data - The file data as a Uint8Array.
 */
window.downloadFileFromBytes = function (fileName, contentType, data) {
    try {
        const blob = new Blob([data], { type: contentType });
        const url = window.URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = fileName;

        document.body.appendChild(link);
        link.click();

        document.body.removeChild(link);
        window.URL.revokeObjectURL(url);

        console.log(`File downloaded: ${fileName}`);
    } catch (error) {
        console.error('Error downloading file:', error);
        throw error;
    }
};

export { downloadFile, downloadFileFromBytes };
