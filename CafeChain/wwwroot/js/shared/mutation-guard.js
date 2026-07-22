(function (window, document) {
    "use strict";

    const running = new Map();
    const originalLabels = new WeakMap();

    function setBusy(button, busy) {
        if (!button) return;
        if (busy) {
            if (!originalLabels.has(button)) originalLabels.set(button, button.innerHTML);
            button.disabled = true;
            button.setAttribute("aria-busy", "true");
            button.classList.add("is-submitting");
        } else {
            button.disabled = false;
            button.removeAttribute("aria-busy");
            button.classList.remove("is-submitting");
            if (originalLabels.has(button)) button.innerHTML = originalLabels.get(button);
        }
    }

    async function run(key, button, operation) {
        if (running.has(key)) return running.get(key);
        setBusy(button, true);
        const promise = Promise.resolve().then(operation);
        running.set(key, promise);
        try {
            return await promise;
        } finally {
            running.delete(key);
            setBusy(button, false);
        }
    }

    function unlockForm(form) {
        form.removeAttribute("data-submit-busy");
        form.removeAttribute("data-submit-pending");
        form.removeAttribute("aria-busy");
        form.querySelectorAll("button[aria-busy='true'],input[aria-busy='true']")
            .forEach(button => setBusy(button, false));
    }

    document.addEventListener("submit", function (event) {
        const form = event.target;
        if (!(form instanceof HTMLFormElement)) return;
        if ((form.method || "get").toLowerCase() !== "post" || form.dataset.allowRepeat === "true") return;
        if (!form.noValidate && !form.checkValidity()) return;

        if (form.dataset.submitBusy === "true" || form.dataset.submitPending === "true") {
            event.preventDefault();
            event.stopImmediatePropagation();
            return;
        }

        const submitter = event.submitter || form.querySelector("button[type='submit'],input[type='submit']");
        form.dataset.submitPending = "true";

        // Wait until other submit handlers have completed. If validation/confirm/AJAX
        // cancels the native submit, the form must remain usable.
        queueMicrotask(function () {
            form.removeAttribute("data-submit-pending");
            if (event.defaultPrevented) {
                unlockForm(form);
                return;
            }

            form.dataset.submitBusy = "true";
            form.setAttribute("aria-busy", "true");
            setBusy(submitter, true);
        });
    }, true);

    window.addEventListener("pageshow", function () {
        document.querySelectorAll("form[data-submit-busy='true']").forEach(unlockForm);
        running.clear();
    });

    window.AdminMutationGuard = Object.freeze({ run, setBusy, unlockForm });
})(window, document);
