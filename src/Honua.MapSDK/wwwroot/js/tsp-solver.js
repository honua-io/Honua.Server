// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

/**
 * Client-side TSP (Traveling Salesman Problem) solver
 * Implements Nearest Neighbor and 2-opt algorithms
 * Supports Web Workers for large problems
 */

window.HonuaTspSolver = (function() {
    'use strict';

    /**
     * Calculate Haversine distance between two coordinates
     * @param {Object} coord1 - {lat, lon}
     * @param {Object} coord2 - {lat, lon}
     * @returns {number} Distance in meters
     */
    function haversineDistance(coord1, coord2) {
        const R = 6371000; // Earth radius in meters
        const lat1 = coord1.lat * Math.PI / 180;
        const lat2 = coord2.lat * Math.PI / 180;
        const dLat = (coord2.lat - coord1.lat) * Math.PI / 180;
        const dLon = (coord2.lon - coord1.lon) * Math.PI / 180;

        const a = Math.sin(dLat / 2) * Math.sin(dLat / 2) +
                  Math.cos(lat1) * Math.cos(lat2) *
                  Math.sin(dLon / 2) * Math.sin(dLon / 2);

        const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));

        return R * c;
    }

    /**
     * Build distance matrix from waypoints
     * @param {Array} waypoints - Array of {lat, lon} objects
     * @returns {Array} 2D distance matrix
     */
    function buildDistanceMatrix(waypoints) {
        const n = waypoints.length;
        const matrix = Array(n).fill(null).map(() => Array(n).fill(0));

        for (let i = 0; i < n; i++) {
            for (let j = 0; j < n; j++) {
                if (i === j) {
                    matrix[i][j] = 0;
                } else {
                    matrix[i][j] = haversineDistance(waypoints[i], waypoints[j]);
                }
            }
        }

        return matrix;
    }

    /**
     * Calculate total distance of a tour
     * @param {Array} tour - Array of indices
     * @param {Array} distanceMatrix - Distance matrix
     * @returns {number} Total distance
     */
    function calculateTourDistance(tour, distanceMatrix) {
        let totalDistance = 0;

        for (let i = 0; i < tour.length - 1; i++) {
            totalDistance += distanceMatrix[tour[i]][tour[i + 1]];
        }

        return totalDistance;
    }

    /**
     * Solve TSP using Nearest Neighbor heuristic
     * O(n²) complexity, ~75-85% optimal
     * @param {Array} distanceMatrix - Distance matrix
     * @param {number} startIndex - Starting point index
     * @returns {Array} Tour as array of indices
     */
    function solveNearestNeighbor(distanceMatrix, startIndex = 0) {
        const n = distanceMatrix.length;
        const tour = [startIndex];
        const visited = Array(n).fill(false);
        visited[startIndex] = true;

        let current = startIndex;

        for (let i = 1; i < n; i++) {
            let nearest = -1;
            let minDistance = Infinity;

            for (let j = 0; j < n; j++) {
                if (!visited[j] && distanceMatrix[current][j] < minDistance) {
                    minDistance = distanceMatrix[current][j];
                    nearest = j;
                }
            }

            if (nearest === -1) break;

            tour.push(nearest);
            visited[nearest] = true;
            current = nearest;
        }

        return tour;
    }

    /**
     * Reverse a segment of the tour (for 2-opt)
     * @param {Array} tour - Tour array
     * @param {number} i - Start index
     * @param {number} j - End index
     * @returns {Array} New tour with reversed segment
     */
    function reverseSegment(tour, i, j) {
        const newTour = [...tour];
        while (i < j) {
            [newTour[i], newTour[j]] = [newTour[j], newTour[i]];
            i++;
            j--;
        }
        return newTour;
    }

    /**
     * Improve tour using 2-opt local search
     * @param {Array} tour - Initial tour
     * @param {Array} distanceMatrix - Distance matrix
     * @param {number} maxIterations - Maximum iterations
     * @param {Function} progressCallback - Progress callback
     * @returns {Array} Improved tour
     */
    function improve2Opt(tour, distanceMatrix, maxIterations = 1000, progressCallback = null) {
        let bestTour = [...tour];
        let improved = true;
        let iterations = 0;

        while (improved && iterations < maxIterations) {
            improved = false;
            iterations++;

            if (progressCallback && iterations % 10 === 0) {
                progressCallback({
                    stage: '2-opt improvement',
                    iteration: iterations,
                    maxIterations: maxIterations
                });
            }

            for (let i = 1; i < bestTour.length - 1; i++) {
                for (let j = i + 1; j < bestTour.length; j++) {
                    const currentDistance = calculateTourDistance(bestTour, distanceMatrix);
                    const newTour = reverseSegment(bestTour, i, j);
                    const newDistance = calculateTourDistance(newTour, distanceMatrix);

                    if (newDistance < currentDistance) {
                        bestTour = newTour;
                        improved = true;
                    }
                }
            }
        }

        return bestTour;
    }

    /**
     * Solve TSP using Nearest Neighbor + 2-opt hybrid
     * @param {Array} distanceMatrix - Distance matrix
     * @param {number} startIndex - Starting point
     * @param {Function} progressCallback - Progress callback
     * @returns {Array} Optimized tour
     */
    function solveHybrid(distanceMatrix, startIndex = 0, progressCallback = null) {
        if (progressCallback) {
            progressCallback({ stage: 'Nearest Neighbor', percent: 30 });
        }

        // Start with nearest neighbor
        let tour = solveNearestNeighbor(distanceMatrix, startIndex);

        if (progressCallback) {
            progressCallback({ stage: '2-opt improvement', percent: 50 });
        }

        // Improve with 2-opt
        tour = improve2Opt(tour, distanceMatrix, 100, progressCallback);

        if (progressCallback) {
            progressCallback({ stage: 'Complete', percent: 100 });
        }

        return tour;
    }

    /**
     * Solve using multi-start approach
     * @param {Array} distanceMatrix - Distance matrix
     * @param {number} numStarts - Number of starting points to try
     * @param {Function} progressCallback - Progress callback
     * @returns {Object} Best tour and distance
     */
    function solveMultiStart(distanceMatrix, numStarts = 5, progressCallback = null) {
        const n = distanceMatrix.length;
        let bestTour = null;
        let bestDistance = Infinity;

        for (let start = 0; start < Math.min(numStarts, n); start++) {
            if (progressCallback) {
                progressCallback({
                    stage: `Multi-start ${start + 1}/${numStarts}`,
                    percent: ((start + 1) / numStarts) * 100
                });
            }

            const tour = solveHybrid(distanceMatrix, start);
            const distance = calculateTourDistance(tour, distanceMatrix);

            if (distance < bestDistance) {
                bestDistance = distance;
                bestTour = tour;
            }
        }

        return {
            tour: bestTour,
            distance: bestDistance
        };
    }

    /**
     * Solve TSP using Simulated Annealing
     * @param {Array} distanceMatrix - Distance matrix
     * @param {Object} options - Algorithm options
     * @param {Function} progressCallback - Progress callback
     * @returns {Object} Tour and distance
     */
    function solveSimulatedAnnealing(distanceMatrix, options = {}, progressCallback = null) {
        const maxIterations = options.maxIterations || 10000;
        const initialTemp = options.initialTemperature || 100;
        const coolingRate = options.coolingRate || 0.995;

        const n = distanceMatrix.length;

        // Start with random tour
        let currentTour = Array.from({length: n}, (_, i) => i);
        for (let i = n - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            [currentTour[i], currentTour[j]] = [currentTour[j], currentTour[i]];
        }

        let bestTour = [...currentTour];
        let currentDistance = calculateTourDistance(currentTour, distanceMatrix);
        let bestDistance = currentDistance;
        let temperature = initialTemp;

        for (let iteration = 0; iteration < maxIterations; iteration++) {
            if (progressCallback && iteration % 100 === 0) {
                progressCallback({
                    stage: 'Simulated Annealing',
                    iteration: iteration,
                    maxIterations: maxIterations,
                    temperature: temperature.toFixed(2)
                });
            }

            // Generate neighbor by swapping two random cities
            const newTour = [...currentTour];
            const i = Math.floor(Math.random() * (n - 1)) + 1;
            const j = Math.floor(Math.random() * (n - 1)) + 1;
            [newTour[i], newTour[j]] = [newTour[j], newTour[i]];

            const newDistance = calculateTourDistance(newTour, distanceMatrix);
            const delta = newDistance - currentDistance;

            // Accept if better, or probabilistically if worse
            if (delta < 0 || Math.random() < Math.exp(-delta / temperature)) {
                currentTour = newTour;
                currentDistance = newDistance;

                if (currentDistance < bestDistance) {
                    bestTour = [...currentTour];
                    bestDistance = currentDistance;
                }
            }

            temperature *= coolingRate;
        }

        return {
            tour: bestTour,
            distance: bestDistance
        };
    }

    /**
     * Optimize route and visualize progress
     * @param {Array} waypoints - Array of waypoint objects
     * @param {Object} options - Optimization options
     * @param {Function} progressCallback - Progress callback
     * @returns {Object} Optimization result
     */
    async function optimizeRoute(waypoints, options = {}, progressCallback = null) {
        const algorithm = options.algorithm || 'hybrid';
        const startTime = performance.now();

        if (progressCallback) {
            progressCallback({ stage: 'Building distance matrix', percent: 10 });
        }

        // Build distance matrix
        const coords = waypoints.map(w => ({
            lat: w.latitude || w.lat,
            lon: w.longitude || w.lon
        }));
        const distanceMatrix = buildDistanceMatrix(coords);

        // Calculate original distance
        const originalTour = Array.from({length: waypoints.length}, (_, i) => i);
        const originalDistance = calculateTourDistance(originalTour, distanceMatrix);

        let result;
        let algorithmName;

        // Choose algorithm
        switch (algorithm) {
            case 'nearest-neighbor':
                algorithmName = 'Nearest Neighbor';
                result = {
                    tour: solveNearestNeighbor(distanceMatrix, 0),
                    distance: 0
                };
                result.distance = calculateTourDistance(result.tour, distanceMatrix);
                break;

            case '2-opt':
                algorithmName = '2-opt';
                const nnTour = solveNearestNeighbor(distanceMatrix, 0);
                result = {
                    tour: improve2Opt(nnTour, distanceMatrix, 1000, progressCallback),
                    distance: 0
                };
                result.distance = calculateTourDistance(result.tour, distanceMatrix);
                break;

            case 'multi-start':
                algorithmName = 'Multi-Start';
                result = solveMultiStart(distanceMatrix, 5, progressCallback);
                break;

            case 'simulated-annealing':
                algorithmName = 'Simulated Annealing';
                result = solveSimulatedAnnealing(distanceMatrix, options, progressCallback);
                break;

            case 'hybrid':
            default:
                algorithmName = 'Hybrid (NN + 2-opt)';
                result = {
                    tour: solveHybrid(distanceMatrix, 0, progressCallback),
                    distance: 0
                };
                result.distance = calculateTourDistance(result.tour, distanceMatrix);
                break;
        }

        const computationTime = performance.now() - startTime;

        // Build optimized sequence
        const optimizedSequence = result.tour.map(i => waypoints[i]);

        return {
            originalSequence: waypoints,
            optimizedSequence: optimizedSequence,
            originalTour: originalTour,
            optimizedTour: result.tour,
            algorithm: algorithmName,
            metrics: {
                originalDistance: originalDistance,
                optimizedDistance: result.distance,
                distanceSaved: originalDistance - result.distance,
                savingsPercent: ((originalDistance - result.distance) / originalDistance * 100).toFixed(2),
                computationTimeMs: computationTime.toFixed(2)
            }
        };
    }

    /**
     * Visualize optimization on a map
     * @param {Object} map - Leaflet map instance
     * @param {Object} result - Optimization result
     * @param {Object} options - Visualization options
     */
    function visualizeOnMap(map, result, options = {}) {
        const originalColor = options.originalColor || '#ff0000';
        const optimizedColor = options.optimizedColor || '#00ff00';

        // Draw original route
        const originalCoords = result.originalSequence.map(w => [w.latitude || w.lat, w.longitude || w.lon]);
        L.polyline(originalCoords, {
            color: originalColor,
            weight: 3,
            opacity: 0.5,
            dashArray: '10, 10'
        }).addTo(map).bindPopup('Original Route');

        // Draw optimized route
        const optimizedCoords = result.optimizedSequence.map(w => [w.latitude || w.lat, w.longitude || w.lon]);
        L.polyline(optimizedCoords, {
            color: optimizedColor,
            weight: 4,
            opacity: 0.8
        }).addTo(map).bindPopup('Optimized Route');

        // Add numbered markers
        result.optimizedSequence.forEach((waypoint, index) => {
            const coord = [waypoint.latitude || waypoint.lat, waypoint.longitude || waypoint.lon];
            const icon = L.divIcon({
                className: 'tsp-marker',
                html: `<div style="background: ${optimizedColor}; color: white; border-radius: 50%; width: 30px; height: 30px; display: flex; align-items: center; justify-content: center; font-weight: bold;">${index + 1}</div>`,
                iconSize: [30, 30]
            });

            L.marker(coord, { icon }).addTo(map)
                .bindPopup(`Stop ${index + 1}: ${waypoint.name || 'Waypoint'}`);
        });

        // Fit bounds
        if (optimizedCoords.length > 0) {
            map.fitBounds(L.latLngBounds(optimizedCoords));
        }
    }

    // Public API
    return {
        haversineDistance,
        buildDistanceMatrix,
        calculateTourDistance,
        solveNearestNeighbor,
        improve2Opt,
        solveHybrid,
        solveMultiStart,
        solveSimulatedAnnealing,
        optimizeRoute,
        visualizeOnMap
    };
})();
