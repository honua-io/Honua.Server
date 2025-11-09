// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

// Auto-save and navigation warning functionality
window.AutoSave = {
    dotNetHelper: null,

    /**
     * Enables navigation warning when there are unsaved changes
     * @param {object} dotNetHelper - DotNet object reference for callbacks
     */
    enableNavigationWarning: function (dotNetHelper) {
        this.dotNetHelper = dotNetHelper;

        // Add beforeunload event listener
        window.onbeforeunload = (e) => {
            if (this.dotNetHelper) {
                const message = this.dotNetHelper.invokeMethod('OnBeforeUnload');
                if (message && message.length > 0) {
                    e.preventDefault();
                    e.returnValue = message;
                    return message;
                }
            }
            return undefined;
        };
    },

    /**
     * Disables navigation warning
     */
    disableNavigationWarning: function () {
        window.onbeforeunload = null;
        this.dotNetHelper = null;
    }
};
