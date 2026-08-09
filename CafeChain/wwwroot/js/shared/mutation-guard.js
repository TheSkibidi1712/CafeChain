(function (window, document) {
    "use strict";

    const running = new Map();
    const originalLabels = new WeakMap();
    const originalDisabled = new WeakMap();
    let validationToastPending = false;
    const overlayContextClasses = [
        "cc-warehouse-page",
        "procurement-page",
        "pa-page",
        "po-page",
        "reorder-page",
        "branch-receipt-page"
    ];

    function preserveOverlayContext(overlay) {
        const selector = overlayContextClasses.map(function (className) { return `.${className}`; }).join(",");
        const context = overlay.closest(selector);
        if (!context) return;
        overlayContextClasses.forEach(function (className) {
            if (context.classList.contains(className)) overlay.classList.add(className);
        });
    }

    function normalizeBootstrapModalPlacement() {
        const host = document.getElementById("cc-modal-host");
        if (!host) return;
        document.querySelectorAll(".modal, .offcanvas").forEach(function (overlay) {
            preserveOverlayContext(overlay);
            if (overlay.parentElement !== host) host.appendChild(overlay);
        });
    }

    function setBusy(button, busy) {
        if (!button) return;
        const isButton = button instanceof HTMLButtonElement;
        if (busy) {
            if (isButton && !originalLabels.has(button)) originalLabels.set(button, button.innerHTML);
            if (!originalDisabled.has(button)) originalDisabled.set(button, button.disabled);
            button.disabled = true;
            button.setAttribute("aria-busy", "true");
            button.classList.add("is-submitting");
            const loadingText = button.dataset.loadingText;
            if (isButton && loadingText) button.textContent = loadingText;
        } else {
            button.disabled = originalDisabled.get(button) === true;
            button.removeAttribute("aria-busy");
            button.classList.remove("is-submitting");
            if (isButton && originalLabels.has(button)) button.innerHTML = originalLabels.get(button);
            originalLabels.delete(button);
            originalDisabled.delete(button);
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

    document.addEventListener("invalid", function (event) {
        const field = event.target;
        if (!(field instanceof HTMLElement)) return;
        field.classList.add("is-invalid");
        field.setAttribute("aria-invalid", "true");
        const validationFeedback = field.closest("form")?.dataset.validationFeedback;
        if (validationFeedback === "sweetalert" || validationFeedback === "inline") return;
        if (validationToastPending) return;
        validationToastPending = true;
        queueMicrotask(function () {
            validationToastPending = false;
            const firstInvalid = document.querySelector(":invalid");
            if (firstInvalid instanceof HTMLElement) firstInvalid.focus({ preventScroll: false });
            if (typeof window.toast === "function") {
                window.toast("Vui lòng kiểm tra và nhập đầy đủ các trường bắt buộc.", "warning");
            }
        });
    }, true);

    document.addEventListener("input", function (event) {
        const field = event.target;
        if (!(field instanceof HTMLInputElement || field instanceof HTMLSelectElement || field instanceof HTMLTextAreaElement)) return;
        // Reading ValidityState does not dispatch an `invalid` event. Calling
        // checkValidity() here made every keystroke before minlength was met
        // create another global warning toast.
        if (field.validity.valid) {
            field.classList.remove("is-invalid");
            field.removeAttribute("aria-invalid");
        }
    }, true);

    // Bootstrap modal phải nằm ngoài ancestor có isolation/overflow. Layout nạp
    // helper này trước script module, vì vậy việc chuyển node không làm mất handler.
    normalizeBootstrapModalPlacement();
    document.addEventListener("show.bs.modal", function (event) {
        const modal = event.target;
        const host = document.getElementById("cc-modal-host");
        if (host && modal instanceof HTMLElement && modal.classList.contains("modal") && modal.parentElement !== host) {
            preserveOverlayContext(modal);
            host.appendChild(modal);
        }
    });
    document.addEventListener("show.bs.offcanvas", function (event) {
        const offcanvas = event.target;
        const host = document.getElementById("cc-modal-host");
        if (host && offcanvas instanceof HTMLElement && offcanvas.classList.contains("offcanvas") && offcanvas.parentElement !== host) {
            preserveOverlayContext(offcanvas);
            host.appendChild(offcanvas);
        }
    });

    window.AdminMutationGuard = Object.freeze({ run, setBusy, unlockForm, normalizeBootstrapModalPlacement });
})(window, document);
