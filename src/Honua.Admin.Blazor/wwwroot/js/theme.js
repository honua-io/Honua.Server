/**
 * Theme Helper - Utilities for theme management and system theme detection
 */
window.ThemeHelper = {
    /**
     * Gets the current system theme preference
     * @returns {string} 'dark' or 'light'
     */
    getSystemTheme: () => {
        return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    },

    /**
     * Watches for system theme changes and notifies .NET
     * @param {object} dotNetHelper - DotNetObjectReference for callbacks
     */
    watchSystemTheme: (dotNetHelper) => {
        const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');

        const handler = (e) => {
            const theme = e.matches ? 'dark' : 'light';
            dotNetHelper.invokeMethodAsync('OnSystemThemeChanged', theme);
        };

        mediaQuery.addEventListener('change', handler);

        // Return cleanup function
        return {
            dispose: () => {
                mediaQuery.removeEventListener('change', handler);
            }
        };
    },

    /**
     * Gets the current theme from localStorage or system preference
     * @returns {string} 'dark' or 'light'
     */
    getCurrentTheme: () => {
        const stored = localStorage.getItem('theme');
        if (stored) {
            return stored;
        }
        return ThemeHelper.getSystemTheme();
    },

    /**
     * Applies theme immediately (useful for preventing flash on page load)
     * @param {string} theme - 'dark' or 'light'
     */
    applyTheme: (theme) => {
        document.documentElement.setAttribute('data-theme', theme);
    },

    /**
     * Initializes theme on page load to prevent flash
     */
    initializeTheme: () => {
        const theme = ThemeHelper.getCurrentTheme();
        ThemeHelper.applyTheme(theme);
    }
};

// Initialize theme immediately to prevent flash
ThemeHelper.initializeTheme();
