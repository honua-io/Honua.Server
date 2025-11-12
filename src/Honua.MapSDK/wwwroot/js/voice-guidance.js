// Honua.MapSDK - Voice Guidance JavaScript Module
// Web Speech API integration for turn-by-turn navigation

let synthesis = null;
let currentUtterance = null;
let config = {
    language: 'en-US',
    volume: 1.0,
    rate: 1.0,
    pitch: 1.0,
    voice: null
};
let utteranceQueue = [];
let isSpeaking = false;

/**
 * Initialize voice guidance
 * @param {Object} options - Configuration options
 */
export function initialize(options = {}) {
    if (!window.speechSynthesis) {
        console.error('Speech Synthesis API not supported in this browser');
        return false;
    }

    synthesis = window.speechSynthesis;

    // Update configuration
    config = {
        language: options.language || 'en-US',
        volume: typeof options.volume === 'number' ? options.volume : 1.0,
        rate: typeof options.rate === 'number' ? options.rate : 1.0,
        pitch: typeof options.pitch === 'number' ? options.pitch : 1.0,
        voice: options.voice || null
    };

    // Load voices
    loadVoices();

    // Listen for voices changed event (some browsers load voices asynchronously)
    if (synthesis.onvoiceschanged !== undefined) {
        synthesis.onvoiceschanged = loadVoices;
    }

    console.log('Voice guidance initialized', config);
    return true;
}

/**
 * Load available voices
 */
function loadVoices() {
    const voices = synthesis.getVoices();

    // Auto-select voice if not set
    if (!config.voice && voices.length > 0) {
        // Try to find a voice matching the language
        const langVoice = voices.find(v => v.lang.startsWith(config.language.substring(0, 2)));
        if (langVoice) {
            config.voice = langVoice.name;
        } else {
            // Fall back to default
            const defaultVoice = voices.find(v => v.default);
            config.voice = defaultVoice ? defaultVoice.name : voices[0].name;
        }
    }
}

/**
 * Speak text using Web Speech API
 * @param {string} text - Text to speak
 * @param {Object} options - Speech options
 */
export function speak(text, options = {}) {
    if (!synthesis) {
        console.error('Voice guidance not initialized');
        return;
    }

    if (!text || text.trim().length === 0) {
        return;
    }

    // Cancel current speech if higher priority
    const priority = options.priority || 0;
    if (isSpeaking && priority > 5) {
        synthesis.cancel();
        utteranceQueue = [];
    }

    // Create utterance
    const utterance = new SpeechSynthesisUtterance(text);

    // Set voice
    const voices = synthesis.getVoices();
    const selectedVoice = voices.find(v => v.name === (options.voice || config.voice));
    if (selectedVoice) {
        utterance.voice = selectedVoice;
    }

    // Set language
    utterance.lang = options.language || config.language;

    // Set volume (0 to 1)
    utterance.volume = Math.max(0, Math.min(1, options.volume ?? config.volume));

    // Set rate (0.1 to 10)
    utterance.rate = Math.max(0.1, Math.min(10, options.rate ?? config.rate));

    // Set pitch (0 to 2)
    utterance.pitch = Math.max(0, Math.min(2, options.pitch ?? config.pitch));

    // Event handlers
    utterance.onstart = () => {
        isSpeaking = true;
        console.log('Speaking:', text);
    };

    utterance.onend = () => {
        isSpeaking = false;
        currentUtterance = null;

        // Process queue
        if (utteranceQueue.length > 0) {
            const next = utteranceQueue.shift();
            synthesis.speak(next);
        }
    };

    utterance.onerror = (event) => {
        console.error('Speech error:', event.error);
        isSpeaking = false;
        currentUtterance = null;
    };

    utterance.onpause = () => {
        console.log('Speech paused');
    };

    utterance.onresume = () => {
        console.log('Speech resumed');
    };

    // Speak or queue
    if (!isSpeaking) {
        currentUtterance = utterance;
        synthesis.speak(utterance);
    } else {
        // Add to queue based on priority
        if (priority > 5) {
            utteranceQueue.unshift(utterance);
        } else {
            utteranceQueue.push(utterance);
        }
    }
}

/**
 * Stop all speech
 */
export function stop() {
    if (synthesis) {
        synthesis.cancel();
        utteranceQueue = [];
        isSpeaking = false;
        currentUtterance = null;
        console.log('Voice guidance stopped');
    }
}

/**
 * Pause speech
 */
export function pause() {
    if (synthesis && isSpeaking) {
        synthesis.pause();
        console.log('Voice guidance paused');
    }
}

/**
 * Resume speech
 */
export function resume() {
    if (synthesis && synthesis.paused) {
        synthesis.resume();
        console.log('Voice guidance resumed');
    }
}

/**
 * Set volume
 * @param {number} volume - Volume level (0 to 1)
 */
export function setVolume(volume) {
    config.volume = Math.max(0, Math.min(1, volume));
}

/**
 * Set speech rate
 * @param {number} rate - Speech rate (0.1 to 10)
 */
export function setRate(rate) {
    config.rate = Math.max(0.1, Math.min(10, rate));
}

/**
 * Set pitch
 * @param {number} pitch - Pitch level (0 to 2)
 */
export function setPitch(pitch) {
    config.pitch = Math.max(0, Math.min(2, pitch));
}

/**
 * Set language
 * @param {string} language - Language code (e.g., 'en-US')
 */
export function setLanguage(language) {
    config.language = language;
    loadVoices(); // Reload to find matching voice
}

/**
 * Set voice
 * @param {string} voiceName - Voice name
 */
export function setVoice(voiceName) {
    config.voice = voiceName;
}

/**
 * Get available voices
 * @returns {Array} Array of voice objects
 */
export function getVoices() {
    if (!synthesis) {
        return [];
    }

    const voices = synthesis.getVoices();
    return voices.map(voice => ({
        name: voice.name,
        lang: voice.lang,
        localService: voice.localService,
        default: voice.default,
        voiceURI: voice.voiceURI
    }));
}

/**
 * Check if speech synthesis is supported
 * @returns {boolean} True if supported
 */
export function isSupported() {
    return typeof window.speechSynthesis !== 'undefined';
}

/**
 * Get current speaking state
 * @returns {boolean} True if currently speaking
 */
export function getSpeakingState() {
    return isSpeaking;
}

/**
 * Get queue length
 * @returns {number} Number of queued utterances
 */
export function getQueueLength() {
    return utteranceQueue.length;
}

/**
 * Clear queue
 */
export function clearQueue() {
    utteranceQueue = [];
}

/**
 * Speak with SSML support (if available)
 * Note: SSML support is limited in Web Speech API
 * @param {string} ssml - SSML markup
 * @param {Object} options - Speech options
 */
export function speakSSML(ssml, options = {}) {
    // Most browsers don't support SSML, so we'll extract text
    const text = extractTextFromSSML(ssml);
    speak(text, options);
}

/**
 * Extract plain text from SSML markup
 * @param {string} ssml - SSML markup
 * @returns {string} Plain text
 */
function extractTextFromSSML(ssml) {
    // Simple SSML tag removal
    // In production, you might want a more sophisticated parser
    return ssml.replace(/<[^>]*>/g, ' ').replace(/\s+/g, ' ').trim();
}

/**
 * Format distance for voice output
 * @param {number} meters - Distance in meters
 * @param {string} units - 'metric' or 'imperial'
 * @returns {string} Formatted distance
 */
export function formatDistanceForVoice(meters, units = 'metric') {
    if (units === 'imperial') {
        const feet = meters * 3.28084;
        if (feet < 528) {
            return `${Math.round(feet / 10) * 10} feet`;
        }
        const miles = feet / 5280;
        if (miles < 0.1) {
            return 'less than a tenth of a mile';
        }
        if (miles < 1) {
            return `${(Math.round(miles * 10) / 10).toFixed(1)} miles`;
        }
        return `${Math.round(miles)} ${miles === 1 ? 'mile' : 'miles'}`;
    } else {
        if (meters < 50) {
            return `${Math.round(meters / 10) * 10} meters`;
        }
        if (meters < 1000) {
            return `${Math.round(meters / 50) * 50} meters`;
        }
        const km = meters / 1000;
        if (km < 1) {
            return 'less than a kilometer';
        }
        if (km < 10) {
            return `${(Math.round(km * 10) / 10).toFixed(1)} kilometers`;
        }
        return `${Math.round(km)} kilometers`;
    }
}

/**
 * Create a navigation announcement
 * @param {string} maneuver - Maneuver type
 * @param {string} streetName - Street name
 * @param {number} distance - Distance in meters
 * @param {string} units - Distance units
 * @returns {string} Announcement text
 */
export function createNavigationAnnouncement(maneuver, streetName, distance, units = 'metric') {
    const distanceText = formatDistanceForVoice(distance, units);
    const action = getManeuverVoiceAction(maneuver);

    if (distance === 0) {
        // Immediate instruction
        if (streetName) {
            return `${action} onto ${streetName}`;
        }
        return action;
    }

    // Advance instruction
    if (streetName) {
        return `In ${distanceText}, ${action} onto ${streetName}`;
    }
    return `In ${distanceText}, ${action}`;
}

/**
 * Get voice-friendly maneuver action
 * @param {string} maneuver - Maneuver type
 * @returns {string} Voice action
 */
function getManeuverVoiceAction(maneuver) {
    const actions = {
        'TurnLeft': 'turn left',
        'TurnRight': 'turn right',
        'TurnSlightLeft': 'keep left',
        'TurnSlightRight': 'keep right',
        'TurnSharpLeft': 'make a sharp left',
        'TurnSharpRight': 'make a sharp right',
        'UTurn': 'make a U-turn',
        'Straight': 'continue straight',
        'Continue': 'continue',
        'Merge': 'merge',
        'Fork': 'at the fork',
        'OnRamp': 'take the ramp',
        'OffRamp': 'take the exit',
        'Roundabout': 'enter the roundabout',
        'RoundaboutLeft': 'at the roundabout, turn left',
        'RoundaboutRight': 'at the roundabout, turn right',
        'Arrive': 'arrive at your destination',
        'Depart': 'head'
    };

    return actions[maneuver] || 'continue';
}

/**
 * Test voice guidance
 * @param {string} text - Optional test text
 */
export function test(text = 'Voice guidance is working') {
    speak(text, { priority: 10 });
}

// Export for debugging
window.honuaVoiceGuidance = {
    speak,
    stop,
    pause,
    resume,
    setVolume,
    setRate,
    setLanguage,
    setVoice,
    getVoices,
    isSupported,
    getSpeakingState,
    test,
    formatDistanceForVoice,
    createNavigationAnnouncement
};
