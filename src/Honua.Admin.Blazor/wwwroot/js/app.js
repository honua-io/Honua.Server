// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

/**
 * Downloads a file from base64 data
 * @param {string} filename - The name of the file to download
 * @param {string} contentTypeOrBase64 - The MIME type of the file or base64 data if 3 args
 * @param {string} base64Data - The base64-encoded file data (optional if 2 args)
 */
window.downloadFile = function(filename, contentTypeOrBase64, base64Data) {
    // Support both 2-arg and 3-arg signatures
    let contentType = 'application/octet-stream';
    let data = contentTypeOrBase64;

    if (arguments.length === 3) {
        contentType = contentTypeOrBase64;
        data = base64Data;
    } else {
        // Detect content type from filename extension
        if (filename.endsWith('.json')) {
            contentType = 'application/json';
        } else if (filename.endsWith('.yaml') || filename.endsWith('.yml')) {
            contentType = 'application/x-yaml';
        } else if (filename.endsWith('.csv')) {
            contentType = 'text/csv';
        }
    }

    const linkElement = document.createElement('a');
    linkElement.href = `data:${contentType};base64,${data}`;
    linkElement.download = filename;
    document.body.appendChild(linkElement);
    linkElement.click();
    document.body.removeChild(linkElement);
};
