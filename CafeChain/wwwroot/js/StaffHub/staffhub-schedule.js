(function (window, document) {
    "use strict";

    function initialize() {
        const root = document.getElementById("staffHubApp");
        if (!root || root.dataset.initialized === "true") return;
        root.dataset.initialized = "true";

        const token = root.querySelector('input[name="__RequestVerificationToken"]')?.value || "";
        const passwordForm = document.getElementById("requiredPasswordForm");
        const passwordModalElement = document.getElementById("passwordModal");

        function notify(message, success) {
            if (window.Swal) return Swal.fire(success ? "Thành công" : "Không thành công", message, success ? "success" : "error");
            window.alert(message);
            return Promise.resolve();
        }

        async function post(url, data) {
            data.set("__RequestVerificationToken", token);
            const response = await fetch(url, {
                method: "POST",
                body: data,
                credentials: "same-origin",
                headers: { "Accept": "application/json", "X-Requested-With": "XMLHttpRequest" }
            });
            let result;
            try {
                result = await response.json();
            } catch {
                result = { message: "Máy chủ trả về dữ liệu không hợp lệ." };
            }
            if (!response.ok) throw new Error(result.message || "Không thể thực hiện thao tác.");
            return result;
        }

        if (root.dataset.requiresPasswordChange === "true" && passwordModalElement) {
            bootstrap.Modal.getOrCreateInstance(passwordModalElement).show();
        }

        passwordForm?.addEventListener("submit", async event => {
            event.preventDefault();
            if (!passwordForm.checkValidity()) return passwordForm.reportValidity();
            await AdminMutationGuard.run("staffhub-change-password", event.submitter, async () => {
                try {
                    const result = await post(root.dataset.changePasswordUrl, new FormData(passwordForm));
                    await notify(result.message, true);
                    bootstrap.Modal.getInstance(passwordModalElement)?.hide();
                    passwordForm.reset();
                } catch (error) {
                    await notify(error.message, false);
                }
            });
        });

        document.getElementById("openPosButton")?.addEventListener("click", async event => {
            await AdminMutationGuard.run("staffhub-open-pos", event.currentTarget, async () => {
                try {
                    const result = await post(root.dataset.issuePosUrl, new FormData());
                    if (!result.token || !result.posUrl) throw new Error("Không nhận được thông tin mở POS.");
                    const separator = result.posUrl.includes("?") ? "&" : "?";
                    window.location.assign(`${result.posUrl}${separator}token=${encodeURIComponent(result.token)}`);
                } catch (error) {
                    await notify(error.message, false);
                }
            });
        });
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", initialize, { once: true });
    else initialize();
})(window, document);
