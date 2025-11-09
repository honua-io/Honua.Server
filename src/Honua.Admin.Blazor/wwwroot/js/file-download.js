/**
 * File Download Utilities
 * Provides functions for downloading files from Blazor to the browser
 */

/**
 * Download a file from a stream reference
 * @param {string} fileName - The name of the file to download
 * @param {object} contentStreamReference - The .NET stream reference
 */
window.downloadFileFromStream = async (fileName, contentStreamReference) => {
    try {
        const arrayBuffer = await contentStreamReference.arrayBuffer();
        const blob = new Blob([arrayBuffer]);
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(url);
    } catch (error) {
        console.error('Error downloading file from stream:', error);
        throw error;
    }
};

/**
 * Download a file from a base64 string
 * @param {string} fileName - The name of the file to download
 * @param {string} mimeType - The MIME type of the file
 * @param {string} base64Data - The base64-encoded file data
 */
window.downloadFileFromBase64 = async (fileName, mimeType, base64Data) => {
    try {
        // Convert base64 to blob
        const byteCharacters = atob(base64Data);
        const byteNumbers = new Array(byteCharacters.length);
        for (let i = 0; i < byteCharacters.length; i++) {
            byteNumbers[i] = byteCharacters.charCodeAt(i);
        }
        const byteArray = new Uint8Array(byteNumbers);
        const blob = new Blob([byteArray], { type: mimeType });

        // Create download link
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(url);
    } catch (error) {
        console.error('Error downloading file from base64:', error);
        throw error;
    }
};

/**
 * Download a file from a byte array
 * @param {string} fileName - The name of the file to download
 * @param {string} mimeType - The MIME type of the file
 * @param {Uint8Array} byteArray - The byte array data
 */
window.downloadFileFromByteArray = async (fileName, mimeType, byteArray) => {
    try {
        const blob = new Blob([byteArray], { type: mimeType });
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(url);
    } catch (error) {
        console.error('Error downloading file from byte array:', error);
        throw error;
    }
};

/**
 * Download a file from a URL (legacy support)
 * @param {string} fileName - The name of the file to download
 * @param {string} contentType - The content type of the file
 * @param {string} base64Data - The base64-encoded file data
 */
window.downloadFile = async (fileName, contentType, base64Data) => {
    // Redirect to the new function for backward compatibility
    return await window.downloadFileFromBase64(fileName, contentType, base64Data);
};

console.log('File download utilities loaded');
