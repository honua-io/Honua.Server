// Keyboard shortcuts management for Honua Admin
// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

window.HonuaAdmin = window.HonuaAdmin || {};

window.HonuaAdmin.KeyboardShortcuts = {
    dotNetHelper: null,
    keydownHandler: null,
    dialogOpenCount: 0,

    initialize: function (dotNetHelper) {
        this.dotNetHelper = dotNetHelper;

        this.keydownHandler = (e) => {
            // Don't handle shortcuts if typing in input fields (except Escape)
            if (this.isInputElement(e.target) && e.key !== 'Escape') {
                return;
            }

            const shortcut = this.getShortcutString(e);
            const isModified = e.ctrlKey || e.metaKey || e.altKey || e.shiftKey;

            // Check if this is a handled shortcut
            if (this.shouldPreventDefault(e, shortcut, isModified)) {
                e.preventDefault();
            }

            // Invoke .NET handler
            this.dotNetHelper.invokeMethodAsync('OnShortcutPressed', shortcut, isModified);
        };

        document.addEventListener('keydown', this.keydownHandler);
    },

    getShortcutString: function (e) {
        const parts = [];

        // Add modifiers in alphabetical order for consistency
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
            '=': 'Equals',
            'ArrowUp': 'Up',
            'ArrowDown': 'Down',
            'ArrowLeft': 'Left',
            'ArrowRight': 'Right',
            'Enter': 'Enter',
            'Escape': 'Escape',
            '/': 'Slash',
            '?': '?'
        };

        return keyMap[key] || key.toUpperCase();
    },

    shouldPreventDefault: function (e, shortcut, isModified) {
        // Prevent default for specific shortcuts that might conflict with browser
        const preventDefaultShortcuts = [
            'Ctrl+K',  // Chrome search
            'Ctrl+F',  // Browser find
            'Ctrl+S',  // Browser save
            'Ctrl+P',  // Browser print
            'Ctrl+R',  // Browser refresh
        ];

        // Also prevent default for single-key shortcuts when not in input
        if (!this.isInputElement(document.activeElement)) {
            // Prevent default for navigation shortcuts
            if ((e.key === 'n' || e.key === 'N' || e.key === 'r' || e.key === 'R' || e.key === '?') && !isModified) {
                return true;
            }

            // Prevent default for sequence shortcuts (G + something)
            if ((e.key === 'g' || e.key === 'G') && !isModified) {
                return true;
            }
        }

        return preventDefaultShortcuts.includes(shortcut);
    },

    isInputElement: function (element) {
        if (!element) return false;

        const tagName = element.tagName?.toLowerCase();
        return tagName === 'input' ||
               tagName === 'textarea' ||
               tagName === 'select' ||
               element.contentEditable === 'true' ||
               element.isContentEditable;
    },

    // Helper to focus an element by ID
    focusElement: function (elementId) {
        const element = document.getElementById(elementId);
        if (element) {
            element.focus();
            return true;
        }
        return false;
    },

    // Helper to focus an element by selector
    focusElementBySelector: function (selector) {
        const element = document.querySelector(selector);
        if (element) {
            // If it's a MudBlazor autocomplete, focus the input inside it
            const input = element.querySelector('input');
            if (input) {
                input.focus();
                return true;
            }
            element.focus();
            return true;
        }
        return false;
    },

    // Helper to click an element
    clickElement: function (selector) {
        const element = document.querySelector(selector);
        if (element) {
            element.click();
            return true;
        }
        return false;
    },

    // Track dialog state (called from Blazor)
    notifyDialogOpened: function () {
        this.dialogOpenCount++;
    },

    notifyDialogClosed: function () {
        this.dialogOpenCount = Math.max(0, this.dialogOpenCount - 1);
    },

    hasOpenDialogs: function () {
        return this.dialogOpenCount > 0;
    },

    dispose: function () {
        if (this.keydownHandler) {
            document.removeEventListener('keydown', this.keydownHandler);
            this.keydownHandler = null;
        }
        this.dotNetHelper = null;
        this.dialogOpenCount = 0;
    }
};
