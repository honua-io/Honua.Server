// Keyboard shortcuts management for Honua MapSDK

window.HonuaMapSDK = window.HonuaMapSDK || {};

window.HonuaMapSDK.KeyboardShortcuts = {
    dotNetHelper: null,
    keydownHandler: null,

    initialize: function (dotNetHelper) {
        this.dotNetHelper = dotNetHelper;

        this.keydownHandler = (e) => {
            const shortcut = this.getShortcutString(e);

            // Don't handle shortcuts if typing in input fields
            if (this.isInputElement(e.target)) {
                return;
            }

            // Invoke .NET handler
            this.dotNetHelper.invokeMethodAsync('OnShortcutPressed', shortcut);
        };

        document.addEventListener('keydown', this.keydownHandler);
    },

    getShortcutString: function (e) {
        const parts = [];

        if (e.altKey) parts.push('Alt');
        if (e.ctrlKey || e.metaKey) parts.push('Ctrl');
        if (e.shiftKey) parts.push('Shift');

        const key = this.normalizeKey(e.key);
        parts.push(key);

        return parts.join('+');
    },

    normalizeKey: function (key) {
        // Normalize key names
        const keyMap = {
            ' ': 'Space',
            '+': 'Plus',
            '-': 'Minus',
            'ArrowUp': 'Up',
            'ArrowDown': 'Down',
            'ArrowLeft': 'Left',
            'ArrowRight': 'Right'
        };

        return keyMap[key] || key.toUpperCase();
    },

    isInputElement: function (element) {
        const tagName = element.tagName.toLowerCase();
        return tagName === 'input' ||
               tagName === 'textarea' ||
               tagName === 'select' ||
               element.contentEditable === 'true';
    },

    dispose: function () {
        if (this.keydownHandler) {
            document.removeEventListener('keydown', this.keydownHandler);
            this.keydownHandler = null;
        }
        this.dotNetHelper = null;
    }
};
