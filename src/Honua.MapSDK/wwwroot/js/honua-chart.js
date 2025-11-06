// Honua Chart JavaScript Module
// Wraps Chart.js for Blazor interop

import Chart from 'https://cdn.jsdelivr.net/npm/chart.js@4.4.0/+esm';

const charts = new Map();

/**
 * Creates a new chart instance
 * @param {HTMLElement} container - The canvas element to render the chart in
 * @param {Object} options - Chart configuration options
 * @param {Object} dotNetRef - Reference to .NET component for callbacks
 * @returns {Object} Chart API object
 */
export function createChart(container, options, dotNetRef) {
    const chartId = options.id;

    // Create canvas if container is not a canvas
    let canvas = container;
    if (container.tagName !== 'CANVAS') {
        canvas = document.createElement('canvas');
        container.appendChild(canvas);
    }

    const config = buildChartConfig(options);

    const chart = new Chart(canvas, config);

    // Store chart instance and metadata
    charts.set(chartId, {
        chart: chart,
        canvas: canvas,
        dotNetRef: dotNetRef,
        options: options,
        data: [],
        filteredData: []
    });

    // Setup click handler for interactivity
    canvas.onclick = (evt) => handleChartClick(chartId, evt);

    return createChartAPI(chartId);
}

/**
 * Builds Chart.js configuration from options
 */
function buildChartConfig(options) {
    const type = getChartType(options.type);

    const config = {
        type: type,
        data: {
            labels: [],
            datasets: [{
                label: options.title || 'Data',
                data: [],
                backgroundColor: generateColors(options.colorScheme || 'default'),
                borderColor: options.theme === 'dark' ? '#666' : '#ddd',
                borderWidth: 1
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    display: options.showLegend !== false,
                    position: options.legendPosition || 'top',
                    labels: {
                        color: options.theme === 'dark' ? '#fff' : '#666'
                    }
                },
                title: {
                    display: !!options.title,
                    text: options.title || '',
                    color: options.theme === 'dark' ? '#fff' : '#333',
                    font: {
                        size: 16,
                        weight: 'bold'
                    }
                },
                tooltip: {
                    enabled: true,
                    mode: 'index',
                    intersect: false,
                    callbacks: {
                        label: function(context) {
                            let label = context.dataset.label || '';
                            if (label) {
                                label += ': ';
                            }
                            if (context.parsed.y !== null) {
                                label += formatValue(context.parsed.y, options.valueFormat);
                            } else if (context.parsed !== null) {
                                label += formatValue(context.parsed, options.valueFormat);
                            }
                            return label;
                        }
                    }
                }
            },
            scales: type !== 'pie' && type !== 'doughnut' ? {
                y: {
                    beginAtZero: true,
                    ticks: {
                        color: options.theme === 'dark' ? '#bbb' : '#666',
                        callback: function(value) {
                            return formatValue(value, options.valueFormat);
                        }
                    },
                    grid: {
                        color: options.theme === 'dark' ? '#333' : '#eee'
                    }
                },
                x: {
                    ticks: {
                        color: options.theme === 'dark' ? '#bbb' : '#666',
                        maxRotation: 45,
                        minRotation: 0
                    },
                    grid: {
                        color: options.theme === 'dark' ? '#333' : '#eee'
                    }
                }
            } : {},
            onClick: (event, elements) => {
                if (elements.length > 0) {
                    const element = elements[0];
                    const chartData = charts.get(options.id);
                    if (chartData) {
                        const index = element.index;
                        const label = config.data.labels[index];
                        const value = config.data.datasets[0].data[index];

                        // Notify Blazor of click
                        chartData.dotNetRef.invokeMethodAsync('OnChartSegmentClicked', label, value, index);
                    }
                }
            }
        }
    };

    // Line chart specific options
    if (type === 'line') {
        config.data.datasets[0].fill = options.fill !== false;
        config.data.datasets[0].tension = options.tension || 0.4;
        config.data.datasets[0].borderColor = options.lineColor || '#3b82f6';
        config.data.datasets[0].backgroundColor = options.fillColor || 'rgba(59, 130, 246, 0.1)';
    }

    // Bar chart specific options
    if (type === 'bar') {
        config.data.datasets[0].maxBarThickness = options.maxBarThickness || 80;
    }

    return config;
}

/**
 * Maps component chart type to Chart.js type
 */
function getChartType(type) {
    const typeMap = {
        'histogram': 'bar',
        'bar': 'bar',
        'pie': 'pie',
        'doughnut': 'doughnut',
        'line': 'line'
    };
    return typeMap[type?.toLowerCase()] || 'bar';
}

/**
 * Generates color palette for chart
 */
function generateColors(scheme) {
    const schemes = {
        default: [
            'rgba(59, 130, 246, 0.8)',   // blue
            'rgba(16, 185, 129, 0.8)',   // green
            'rgba(245, 158, 11, 0.8)',   // yellow
            'rgba(239, 68, 68, 0.8)',    // red
            'rgba(139, 92, 246, 0.8)',   // purple
            'rgba(236, 72, 153, 0.8)',   // pink
            'rgba(20, 184, 166, 0.8)',   // teal
            'rgba(251, 146, 60, 0.8)',   // orange
            'rgba(99, 102, 241, 0.8)',   // indigo
            'rgba(52, 211, 153, 0.8)'    // emerald
        ],
        cool: [
            'rgba(59, 130, 246, 0.8)',
            'rgba(99, 102, 241, 0.8)',
            'rgba(139, 92, 246, 0.8)',
            'rgba(20, 184, 166, 0.8)',
            'rgba(52, 211, 153, 0.8)'
        ],
        warm: [
            'rgba(239, 68, 68, 0.8)',
            'rgba(251, 146, 60, 0.8)',
            'rgba(245, 158, 11, 0.8)',
            'rgba(236, 72, 153, 0.8)',
            'rgba(244, 114, 182, 0.8)'
        ],
        earth: [
            'rgba(120, 113, 108, 0.8)',
            'rgba(168, 162, 158, 0.8)',
            'rgba(87, 83, 78, 0.8)',
            'rgba(214, 211, 209, 0.8)',
            'rgba(68, 64, 60, 0.8)'
        ]
    };

    return schemes[scheme] || schemes.default;
}

/**
 * Formats a value according to specified format
 */
function formatValue(value, format) {
    if (typeof value !== 'number') return value;

    switch (format) {
        case 'currency':
            return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(value);
        case 'percent':
            return `${(value * 100).toFixed(1)}%`;
        case 'decimal':
            return value.toFixed(2);
        case 'integer':
            return Math.round(value).toLocaleString();
        default:
            return value.toLocaleString();
    }
}

/**
 * Handles click events on chart segments
 */
function handleChartClick(chartId, evt) {
    const chartData = charts.get(chartId);
    if (!chartData) return;

    const activeElements = chartData.chart.getElementsAtEventForMode(
        evt,
        'nearest',
        { intersect: true },
        false
    );

    if (activeElements.length > 0) {
        const element = activeElements[0];
        const index = element.index;
        const label = chartData.chart.data.labels[index];
        const value = chartData.chart.data.datasets[0].data[index];

        chartData.dotNetRef.invokeMethodAsync('OnChartSegmentClicked', label, value, index);
    }
}

/**
 * Creates the public API for a chart instance
 */
function createChartAPI(chartId) {
    return {
        /**
         * Updates chart with new data
         */
        updateData: (labels, values, options = {}) => {
            const chartData = charts.get(chartId);
            if (!chartData) return;

            chartData.chart.data.labels = labels;
            chartData.chart.data.datasets[0].data = values;

            if (options.colors) {
                chartData.chart.data.datasets[0].backgroundColor = options.colors;
            }

            if (options.label) {
                chartData.chart.data.datasets[0].label = options.label;
            }

            chartData.chart.update();
        },

        /**
         * Updates chart theme
         */
        setTheme: (theme) => {
            const chartData = charts.get(chartId);
            if (!chartData) return;

            const isDark = theme === 'dark';
            const textColor = isDark ? '#fff' : '#333';
            const gridColor = isDark ? '#333' : '#eee';
            const tickColor = isDark ? '#bbb' : '#666';

            chartData.chart.options.plugins.title.color = textColor;
            chartData.chart.options.plugins.legend.labels.color = tickColor;

            if (chartData.chart.options.scales) {
                if (chartData.chart.options.scales.x) {
                    chartData.chart.options.scales.x.ticks.color = tickColor;
                    chartData.chart.options.scales.x.grid.color = gridColor;
                }
                if (chartData.chart.options.scales.y) {
                    chartData.chart.options.scales.y.ticks.color = tickColor;
                    chartData.chart.options.scales.y.grid.color = gridColor;
                }
            }

            chartData.chart.update();
        },

        /**
         * Shows loading state
         */
        showLoading: () => {
            const chartData = charts.get(chartId);
            if (!chartData) return;

            const overlay = document.createElement('div');
            overlay.className = 'honua-chart-loading';
            overlay.innerHTML = '<div class="spinner"></div><div>Loading...</div>';
            chartData.canvas.parentElement.appendChild(overlay);
        },

        /**
         * Hides loading state
         */
        hideLoading: () => {
            const chartData = charts.get(chartId);
            if (!chartData) return;

            const overlay = chartData.canvas.parentElement.querySelector('.honua-chart-loading');
            if (overlay) {
                overlay.remove();
            }
        },

        /**
         * Exports chart as image
         */
        exportAsImage: (format = 'png') => {
            const chartData = charts.get(chartId);
            if (!chartData) return null;

            return chartData.canvas.toDataURL(`image/${format}`);
        },

        /**
         * Resizes chart
         */
        resize: () => {
            const chartData = charts.get(chartId);
            if (!chartData) return;

            chartData.chart.resize();
        },

        /**
         * Destroys chart and cleans up
         */
        dispose: () => {
            const chartData = charts.get(chartId);
            if (!chartData) return;

            chartData.chart.destroy();
            charts.delete(chartId);
        }
    };
}

/**
 * Gets chart instance by ID (for debugging)
 */
export function getChart(chartId) {
    return charts.get(chartId);
}
