(function (root, factory) {
    'use strict';

    const interactions = factory();
    if (typeof module === 'object' && module.exports) module.exports = interactions;
    if (root) root.AIImportInteractions = interactions;
})(typeof globalThis !== 'undefined' ? globalThis : this, function () {
    'use strict';

    function createOperationGuard(onStateChange = () => {}) {
        let activeKey = null;

        async function run(key, operation) {
            if (activeKey !== null) return { started: false };

            activeKey = key;
            onStateChange(true, key);
            try {
                return { started: true, value: await operation() };
            } finally {
                activeKey = null;
                onStateChange(false, key);
            }
        }

        return {
            isBusy: () => activeKey !== null,
            run
        };
    }

    function createAlertCoordinator({ swal, target }) {
        if (!swal || typeof swal.fire !== 'function') {
            throw new TypeError('SweetAlert2 is required.');
        }

        let generation = 0;
        let tail = Promise.resolve();
        const pending = new Map();

        function show(key, options) {
            const existing = pending.get(key);
            if (existing) return existing;

            const scheduledGeneration = generation;
            const alert = tail
                .catch(() => undefined)
                .then(() => {
                    if (scheduledGeneration !== generation) {
                        return { isDismissed: true, cancelled: true };
                    }

                    return swal.fire({
                        ...options,
                        target,
                        topLayer: true
                    });
                })
                .finally(() => pending.delete(key));

            pending.set(key, alert);
            tail = alert.then(() => undefined, () => undefined);
            return alert;
        }

        function close() {
            generation++;
            if (typeof swal.close === 'function') swal.close();
            tail = Promise.resolve();
        }

        return { close, show };
    }

    return {
        createAlertCoordinator,
        createOperationGuard
    };
});
