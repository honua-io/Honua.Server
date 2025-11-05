// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

/**
 * Downloads a file from base64 data
 * @param {string} filename - The name of the file to download
 * @param {string} contentType - The MIME type of the file
 * @param {string} base64Data - The base64-encoded file data
 */
window.downloadFile = function(filename, contentType, base64Data) {
    const linkElement = document.createElement('a');
    linkElement.href = `data:${contentType};base64,${base64Data}`;
    linkElement.download = filename;
    document.body.appendChild(linkElement);
    linkElement.click();
    document.body.removeChild(linkElement);
};
