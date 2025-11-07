// Responsive helper for Honua MapSDK

window.HonuaMapSDK = window.HonuaMapSDK || {};

window.HonuaMapSDK.Responsive = {
    dotNetHelper: null,
    resizeHandler: null,

    initialize: function (dotNetHelper) {
        this.dotNetHelper = dotNetHelper;

        // Set up resize listener
        this.resizeHandler = () => {
            const width = window.innerWidth;
            const height = window.innerHeight;
            this.dotNetHelper.invokeMethodAsync('OnResize', width, height);
        };

        window.addEventListener('resize', this.resizeHandler);

        // Return initial screen info
        return {
            width: window.innerWidth,
            height: window.innerHeight,
            isTouchDevice: 'ontouchstart' in window || navigator.maxTouchPoints > 0
        };
    },

    getScreenSize: function () {
        return {
            width: window.innerWidth,
            height: window.innerHeight,
            isTouchDevice: 'ontouchstart' in window || navigator.maxTouchPoints > 0
        };
    },

    matchesMediaQuery: function (query) {
        return window.matchMedia(query).matches;
    },

    dispose: function () {
        if (this.resizeHandler) {
            window.removeEventListener('resize', this.resizeHandler);
            this.resizeHandler = null;
        }
        this.dotNetHelper = null;
    }
};
