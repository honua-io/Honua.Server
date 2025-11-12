// Copyright (c) 2025 HonuaIO
// Dashboard Widget JavaScript Interop
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

// Map widget using Mapbox GL JS
window.initializeMapWidget = function (containerId, options, dotNetRef) {
    if (typeof mapboxgl === 'undefined') {
        console.error('Mapbox GL JS not loaded');
        return;
    }

    const map = new mapboxgl.Map({
        container: containerId,
        style: options.style || 'mapbox://styles/mapbox/streets-v12',
        center: options.center || [-122.4194, 37.7749],
        zoom: options.zoom || 10,
        interactive: options.interactive !== false
    });

    // Add navigation controls if requested
    if (options.interactive) {
        map.addControl(new mapboxgl.NavigationControl());
    }

    // Handle click events
    map.on('click', function (e) {
        const features = map.queryRenderedFeatures(e.point);
        if (features.length > 0 && dotNetRef) {
            dotNetRef.invokeMethodAsync('HandleFeatureClick', features[0]);
        }
    });

    // Store map instance for later use
    window[`map_${containerId}`] = map;

    return map;
};

// Chart widget using Chart.js
window.renderChart = function (containerId, options) {
    if (typeof Chart === 'undefined') {
        console.error('Chart.js not loaded');
        return;
    }

    const canvas = document.getElementById(containerId);
    if (!canvas) {
        console.error(`Canvas element ${containerId} not found`);
        return;
    }

    // Destroy existing chart if present
    const existingChart = Chart.getChart(containerId);
    if (existingChart) {
        existingChart.destroy();
    }

    // Create new chart
    const ctx = canvas.getContext('2d');
    const chart = new Chart(ctx, options);

    // Store chart instance
    window[`chart_${containerId}`] = chart;

    return chart;
};

// Update chart data
window.updateChartData = function (containerId, newData) {
    const chart = window[`chart_${containerId}`];
    if (chart) {
        chart.data = newData;
        chart.update();
    }
};

// Fullscreen utility
window.toggleFullscreen = function () {
    if (!document.fullscreenElement) {
        document.documentElement.requestFullscreen();
        return true;
    } else {
        if (document.exitFullscreen) {
            document.exitFullscreen();
        }
        return false;
    }
};

// Dashboard grid drag-and-drop (using interact.js or similar)
window.initializeDashboardGrid = function (containerId, options) {
    // This would initialize a drag-and-drop grid system
    // For production, consider using libraries like:
    // - gridstack.js
    // - react-grid-layout equivalent
    // - muuri.js
    console.log('Dashboard grid initialized:', containerId, options);
};

// Export dashboard as image
window.exportDashboardAsImage = function (elementId) {
    if (typeof html2canvas === 'undefined') {
        console.error('html2canvas not loaded');
        return null;
    }

    const element = document.getElementById(elementId);
    if (!element) {
        return null;
    }

    return html2canvas(element).then(canvas => {
        return canvas.toDataURL('image/png');
    });
};

// Download dashboard as PDF
window.exportDashboardAsPDF = function (elementId, dashboardName) {
    if (typeof jspdf === 'undefined') {
        console.error('jsPDF not loaded');
        return;
    }

    window.exportDashboardAsImage(elementId).then(dataUrl => {
        const pdf = new jspdf.jsPDF('landscape');
        const imgWidth = 297; // A4 landscape width in mm
        const imgHeight = 210; // A4 landscape height in mm

        pdf.addImage(dataUrl, 'PNG', 0, 0, imgWidth, imgHeight);
        pdf.save(`${dashboardName || 'dashboard'}.pdf`);
    });
};
