(function (global) {
    "use strict";

    const cache = new Map();

    function readUiCatalog(elementId) {
        if (cache.has(elementId)) return cache.get(elementId);

        const element = document.getElementById(elementId);
        if (!element) throw new Error(`UI catalog "${elementId}" was not found.`);

        let messages;
        try {
            messages = JSON.parse(element.textContent || "{}");
        } catch (error) {
            throw new Error(`UI catalog "${elementId}" contains invalid JSON.`, { cause: error });
        }

        const catalog = Object.freeze({ ...messages });
        cache.set(elementId, catalog);
        return catalog;
    }

    function uiText(catalog, key, values = {}) {
        if (!Object.prototype.hasOwnProperty.call(catalog, key)) {
            throw new Error(`Missing UI localization key "${key}".`);
        }

        return String(catalog[key]).replace(/\{([A-Za-z][A-Za-z0-9]*)\}/g, (placeholder, name) => {
            if (!Object.prototype.hasOwnProperty.call(values, name)) {
                throw new Error(`Missing UI localization value "${name}" for "${key}".`);
            }
            return String(values[name]);
        });
    }

    global.CafeChainUiCatalog = Object.freeze({ read: readUiCatalog, text: uiText });
})(window);
