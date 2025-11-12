// Honua Streaming Manager - WebSocket-based Real-time Data Streaming
// Handles WebSocket connections and real-time feature updates on MapLibre GL JS

const streamingSources = new Map();
const mapInstances = new Map();

/**
 * Initializes the streaming manager with a map instance
 * @param {string} mapId - Map container ID
 * @param {Object} mapInstance - MapLibre GL map instance
 */
export function initializeStreaming(mapId, mapInstance) {
    if (!mapInstance) {
        console.error('Map instance is required');
        return;
    }
    mapInstances.set(mapId, mapInstance);
    console.log(`Streaming manager initialized for map ${mapId}`);
}

/**
 * Creates a new streaming data source with WebSocket connection
 * @param {string} mapId - Map container ID
 * @param {string} sourceId - Unique source identifier
 * @param {Object} options - Streaming configuration
 * @param {Object} dotNetRef - .NET component reference for callbacks
 * @returns {Object} Streaming API object
 */
export function createStreamingSource(mapId, sourceId, options, dotNetRef) {
    const map = mapInstances.get(mapId);
    if (!map) {
        console.error(`Map ${mapId} not found. Call initializeStreaming first.`);
        return null;
    }

    // Validate options
    if (!options.webSocketUrl) {
        console.error('WebSocket URL is required');
        return null;
    }

    try {
        // Initialize GeoJSON source
        const initialData = {
            type: 'FeatureCollection',
            features: []
        };

        map.addSource(sourceId, {
            type: 'geojson',
            data: initialData
        });

        // Create layer if specified
        if (options.layerId) {
            const layerConfig = {
                id: options.layerId,
                type: options.layerType || 'circle',
                source: sourceId,
                paint: options.paint || getDefaultPaint(options.layerType || 'circle')
            };

            if (options.layout) {
                layerConfig.layout = options.layout;
            }

            map.addLayer(layerConfig);
        }

        // Create streaming source
        const streamingSource = new StreamingSource(
            mapId,
            sourceId,
            options,
            map,
            dotNetRef
        );

        streamingSources.set(sourceId, streamingSource);

        // Connect to WebSocket
        streamingSource.connect();

        console.log(`Streaming source ${sourceId} created successfully`);
        return createStreamingAPI(sourceId);
    } catch (error) {
        console.error('Error creating streaming source:', error);
        return null;
    }
}

/**
 * Gets default paint properties for different layer types
 */
function getDefaultPaint(layerType) {
    const defaults = {
        circle: {
            'circle-radius': 6,
            'circle-color': '#0080ff',
            'circle-opacity': 0.8,
            'circle-stroke-width': 2,
            'circle-stroke-color': '#ffffff'
        },
        line: {
            'line-color': '#0080ff',
            'line-width': 3,
            'line-opacity': 0.8
        },
        fill: {
            'fill-color': '#0080ff',
            'fill-opacity': 0.5,
            'fill-outline-color': '#ffffff'
        }
    };

    return defaults[layerType] || defaults.circle;
}

/**
 * StreamingSource class - manages WebSocket connection and real-time updates
 */
class StreamingSource {
    constructor(mapId, sourceId, options, map, dotNetRef) {
        this.mapId = mapId;
        this.sourceId = sourceId;
        this.options = options;
        this.map = map;
        this.dotNetRef = dotNetRef;

        this.ws = null;
        this.features = new Map();
        this.messageQueue = [];
        this.isProcessing = false;
        this.reconnectAttempts = 0;
        this.reconnectTimer = null;

        this.statistics = {
            state: 'disconnected',
            messagesReceived: 0,
            featuresUpdated: 0,
            featuresDeleted: 0,
            currentFeatureCount: 0,
            messagesPerSecond: 0,
            connectedAt: null,
            lastMessageAt: null,
            errorCount: 0,
            bytesReceived: 0
        };

        this.messageRateWindow = [];
        this.messageRateInterval = null;
    }

    /**
     * Connects to the WebSocket endpoint
     */
    connect() {
        if (this.ws && (this.ws.readyState === WebSocket.CONNECTING || this.ws.readyState === WebSocket.OPEN)) {
            console.warn('WebSocket is already connecting or connected');
            return;
        }

        this.statistics.state = this.reconnectAttempts > 0 ? 'reconnecting' : 'connecting';
        this.notifyStateChange();

        try {
            // Build WebSocket URL with auth token if provided
            let url = this.options.webSocketUrl;
            if (this.options.authToken) {
                const separator = url.includes('?') ? '&' : '?';
                url += `${separator}token=${encodeURIComponent(this.options.authToken)}`;
            }

            this.ws = new WebSocket(url, this.options.subProtocol);

            this.ws.onopen = () => this.handleOpen();
            this.ws.onmessage = (event) => this.handleMessage(event);
            this.ws.onerror = (error) => this.handleError(error);
            this.ws.onclose = (event) => this.handleClose(event);

            console.log(`Connecting to WebSocket: ${this.options.webSocketUrl}`);
        } catch (error) {
            console.error('Error creating WebSocket:', error);
            this.statistics.state = 'failed';
            this.statistics.errorCount++;
            this.notifyError(error);
            this.scheduleReconnect();
        }
    }

    /**
     * Handles WebSocket open event
     */
    handleOpen() {
        console.log(`WebSocket connected: ${this.options.webSocketUrl}`);
        this.statistics.state = 'connected';
        this.statistics.connectedAt = new Date();
        this.reconnectAttempts = 0;

        this.notifyConnected();
        this.notifyStateChange();

        // Start message rate calculation
        this.startMessageRateCalculation();
    }

    /**
     * Handles incoming WebSocket messages
     */
    handleMessage(event) {
        try {
            this.statistics.messagesReceived++;
            this.statistics.bytesReceived += event.data.length;
            this.statistics.lastMessageAt = new Date();

            // Track message rate
            this.messageRateWindow.push(Date.now());

            // Add to queue
            this.messageQueue.push(event.data);

            // Limit queue size
            if (this.messageQueue.length > (this.options.bufferSize || 100)) {
                this.messageQueue.shift();
            }

            // Start processing if not already running
            if (!this.isProcessing) {
                this.processMessageQueue();
            }

            // Notify .NET
            if (this.dotNetRef) {
                this.dotNetRef.invokeMethodAsync('OnMessageReceived', event.data);
            }
        } catch (error) {
            console.error('Error handling message:', error);
            this.statistics.errorCount++;
        }
    }

    /**
     * Processes queued messages with throttling
     */
    async processMessageQueue() {
        if (this.isProcessing) return;

        this.isProcessing = true;

        try {
            while (this.messageQueue.length > 0) {
                const message = this.messageQueue.shift();
                await this.processMessage(message);

                // Throttle updates
                if (this.options.updateThrottle && this.options.updateThrottle > 0) {
                    await this.sleep(this.options.updateThrottle);
                }
            }
        } finally {
            this.isProcessing = false;
        }
    }

    /**
     * Processes a single message
     */
    async processMessage(message) {
        try {
            const data = JSON.parse(message);

            // Handle different message formats
            if (data.type === 'Feature') {
                await this.handleFeatureUpdate(data);
            } else if (data.type === 'FeatureCollection') {
                await this.handleFeatureCollection(data);
            } else if (data.action) {
                await this.handleAction(data);
            } else if (data.type === 'ping' || data.type === 'pong') {
                // Heartbeat message - ignore
            } else {
                // Treat as custom format
                await this.handleCustomUpdate(data);
            }
        } catch (error) {
            console.error('Error processing message:', error, message);
            this.statistics.errorCount++;
        }
    }

    /**
     * Handles a single Feature update
     */
    async handleFeatureUpdate(feature) {
        const strategy = this.options.updateStrategy || 'upsert';
        const featureId = feature.id || feature.properties?.id;

        if (strategy === 'replace') {
            this.features.clear();
            if (featureId) {
                this.features.set(featureId, feature);
            }
            this.statistics.featuresUpdated++;
        } else if (strategy === 'upsert' || strategy === 'accumulate') {
            if (featureId) {
                this.features.set(featureId, feature);
            } else {
                // No ID, just add it
                const uniqueId = `feature-${Date.now()}-${Math.random()}`;
                this.features.set(uniqueId, feature);
            }
            this.statistics.featuresUpdated++;
        } else if (strategy === 'updateOnly') {
            if (featureId && this.features.has(featureId)) {
                this.features.set(featureId, feature);
                this.statistics.featuresUpdated++;
            }
        }

        await this.enforceMaxFeatures();
        await this.updateMapSource();

        // Notify .NET
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnFeatureUpdate', feature);
        }
    }

    /**
     * Handles a FeatureCollection update
     */
    async handleFeatureCollection(featureCollection) {
        const strategy = this.options.updateStrategy || 'upsert';

        if (strategy === 'replace') {
            this.features.clear();
        }

        for (const feature of featureCollection.features) {
            const featureId = feature.id || feature.properties?.id;
            if (featureId) {
                this.features.set(featureId, feature);
            } else {
                const uniqueId = `feature-${Date.now()}-${Math.random()}`;
                this.features.set(uniqueId, feature);
            }
            this.statistics.featuresUpdated++;
        }

        await this.enforceMaxFeatures();
        await this.updateMapSource();
    }

    /**
     * Handles action-based updates (add, update, delete, clear)
     */
    async handleAction(data) {
        const action = data.action;
        const featureId = data.id;

        if (action === 'delete' && featureId) {
            if (this.features.delete(featureId)) {
                this.statistics.featuresDeleted++;
            }
            await this.updateMapSource();
        } else if (action === 'clear') {
            this.features.clear();
            this.statistics.featuresDeleted += this.features.size;
            await this.updateMapSource();
        } else if ((action === 'add' || action === 'update') && data.data) {
            await this.handleFeatureUpdate(data.data);
        } else if (data.data?.type === 'FeatureCollection') {
            await this.handleFeatureCollection(data.data);
        }
    }

    /**
     * Handles custom update format
     */
    async handleCustomUpdate(data) {
        // Convert custom format to GeoJSON feature
        const feature = {
            type: 'Feature',
            geometry: data.geometry || data.location,
            properties: data.properties || data
        };

        if (data.id) {
            feature.id = data.id;
        }

        await this.handleFeatureUpdate(feature);
    }

    /**
     * Enforces maximum feature limit
     */
    async enforceMaxFeatures() {
        const maxFeatures = this.options.maxFeatures || 1000;
        if (maxFeatures > 0 && this.features.size > maxFeatures) {
            // Remove oldest features (FIFO)
            const toRemove = this.features.size - maxFeatures;
            const keys = Array.from(this.features.keys());
            for (let i = 0; i < toRemove; i++) {
                this.features.delete(keys[i]);
            }
        }

        this.statistics.currentFeatureCount = this.features.size;
    }

    /**
     * Updates the map source with current features
     */
    async updateMapSource() {
        try {
            const source = this.map.getSource(this.sourceId);
            if (source) {
                const featureCollection = {
                    type: 'FeatureCollection',
                    features: Array.from(this.features.values())
                };

                source.setData(featureCollection);
                this.statistics.currentFeatureCount = this.features.size;
            }
        } catch (error) {
            console.error('Error updating map source:', error);
            this.statistics.errorCount++;
        }
    }

    /**
     * Handles WebSocket errors
     */
    handleError(error) {
        console.error('WebSocket error:', error);
        this.statistics.errorCount++;
        this.notifyError(error);
    }

    /**
     * Handles WebSocket close event
     */
    handleClose(event) {
        console.log(`WebSocket closed: ${event.code} - ${event.reason}`);
        this.statistics.state = 'disconnected';
        this.notifyDisconnected();
        this.notifyStateChange();

        this.stopMessageRateCalculation();

        // Schedule reconnect if enabled
        if (this.options.enableAutoReconnect !== false) {
            this.scheduleReconnect();
        }
    }

    /**
     * Schedules automatic reconnection
     */
    scheduleReconnect() {
        if (this.reconnectTimer) {
            clearTimeout(this.reconnectTimer);
        }

        const maxAttempts = this.options.maxReconnectAttempts || 0;
        if (maxAttempts > 0 && this.reconnectAttempts >= maxAttempts) {
            console.warn(`Max reconnect attempts (${maxAttempts}) reached`);
            this.statistics.state = 'failed';
            this.notifyStateChange();
            return;
        }

        this.reconnectAttempts++;
        const baseDelay = this.options.reconnectDelay || 1000;
        const maxDelay = this.options.maxReconnectDelay || 30000;
        const delay = Math.min(baseDelay * Math.pow(1.5, this.reconnectAttempts - 1), maxDelay);

        console.log(`Reconnecting in ${delay}ms (attempt ${this.reconnectAttempts})`);

        this.reconnectTimer = setTimeout(() => {
            this.connect();
        }, delay);
    }

    /**
     * Starts message rate calculation
     */
    startMessageRateCalculation() {
        this.messageRateInterval = setInterval(() => {
            const now = Date.now();
            const oneSecondAgo = now - 1000;

            // Remove old timestamps
            this.messageRateWindow = this.messageRateWindow.filter(t => t > oneSecondAgo);

            // Calculate rate
            this.statistics.messagesPerSecond = this.messageRateWindow.length;
        }, 1000);
    }

    /**
     * Stops message rate calculation
     */
    stopMessageRateCalculation() {
        if (this.messageRateInterval) {
            clearInterval(this.messageRateInterval);
            this.messageRateInterval = null;
        }
        this.messageRateWindow = [];
    }

    /**
     * Sends a message through the WebSocket
     */
    send(message) {
        if (this.ws && this.ws.readyState === WebSocket.OPEN) {
            this.ws.send(typeof message === 'string' ? message : JSON.stringify(message));
        } else {
            console.warn('WebSocket is not open');
        }
    }

    /**
     * Disconnects the WebSocket
     */
    disconnect() {
        if (this.reconnectTimer) {
            clearTimeout(this.reconnectTimer);
            this.reconnectTimer = null;
        }

        this.stopMessageRateCalculation();

        if (this.ws) {
            this.ws.close();
            this.ws = null;
        }

        this.statistics.state = 'disconnected';
        this.notifyStateChange();
    }

    /**
     * Gets current statistics
     */
    getStatistics() {
        return { ...this.statistics };
    }

    /**
     * Clears all features
     */
    clearFeatures() {
        this.features.clear();
        this.statistics.currentFeatureCount = 0;
        this.updateMapSource();
    }

    /**
     * Sets layer visibility
     */
    setVisibility(visible) {
        if (this.options.layerId) {
            this.map.setLayoutProperty(
                this.options.layerId,
                'visibility',
                visible ? 'visible' : 'none'
            );
        }
    }

    /**
     * Notification methods
     */
    notifyConnected() {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnConnected');
        }
    }

    notifyDisconnected() {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnDisconnected');
        }
    }

    notifyError(error) {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnError', error.toString());
        }
    }

    notifyStateChange() {
        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnStateChanged', this.statistics.state);
        }
    }

    /**
     * Utility: sleep function
     */
    sleep(ms) {
        return new Promise(resolve => setTimeout(resolve, ms));
    }

    /**
     * Disposes the streaming source
     */
    dispose() {
        this.disconnect();

        // Remove layer and source from map
        try {
            if (this.options.layerId && this.map.getLayer(this.options.layerId)) {
                this.map.removeLayer(this.options.layerId);
            }

            if (this.map.getSource(this.sourceId)) {
                this.map.removeSource(this.sourceId);
            }
        } catch (error) {
            console.error('Error disposing streaming source:', error);
        }
    }
}

/**
 * Creates the public API for a streaming source instance
 */
function createStreamingAPI(sourceId) {
    const source = streamingSources.get(sourceId);
    if (!source) return null;

    return {
        connect: () => source.connect(),
        disconnect: () => source.disconnect(),
        send: (message) => source.send(message),
        getStatistics: () => source.getStatistics(),
        clearFeatures: () => source.clearFeatures(),
        setVisibility: (visible) => source.setVisibility(visible),
        dispose: () => {
            source.dispose();
            streamingSources.delete(sourceId);
        }
    };
}

/**
 * Gets a streaming source by ID
 */
export function getStreamingSource(sourceId) {
    return streamingSources.get(sourceId);
}

/**
 * Disconnects a streaming source
 */
export function disconnectStreaming(sourceId) {
    const source = streamingSources.get(sourceId);
    if (source) {
        source.disconnect();
    }
}

/**
 * Disposes a streaming source
 */
export function disposeStreaming(sourceId) {
    const source = streamingSources.get(sourceId);
    if (source) {
        source.dispose();
        streamingSources.delete(sourceId);
    }
}

/**
 * Gets statistics for a streaming source
 */
export function getStreamingStatistics(sourceId) {
    const source = streamingSources.get(sourceId);
    return source ? source.getStatistics() : null;
}

/**
 * Cleans up all streaming sources for a map
 */
export function cleanupStreaming(mapId) {
    const sourcesToRemove = [];

    for (const [sourceId, source] of streamingSources.entries()) {
        if (source.mapId === mapId) {
            source.dispose();
            sourcesToRemove.push(sourceId);
        }
    }

    sourcesToRemove.forEach(id => streamingSources.delete(id));
    mapInstances.delete(mapId);
}
