(() => {
    "use strict";

    document.addEventListener("DOMContentLoaded", () => {
        const form = document.getElementById("negativeInventorySettingsForm");
        const negativeTabButton = document.getElementById("negative-inventory-tab");
        const generalActions = document.getElementById("generalSettingsActions");
        const storeFilter = document.getElementById("negativeInventoryStoreFilter");
        const itemSearch = document.getElementById("negativeInventoryItemSearch");
        const visibleCount = document.getElementById("negativeInventoryVisibleCount");
        const itemRows = Array.from(document.querySelectorAll(".negative-inventory-item-row"));

        const updateGeneralActions = (targetSelector) => {
            if (!generalActions) return;
            generalActions.classList.toggle("d-none", targetSelector === "#negative-inventory");
        };

        document.querySelectorAll("#settingsTab [data-bs-toggle='tab']").forEach((button) => {
            button.addEventListener("shown.bs.tab", (event) => {
                const target = event.target.getAttribute("data-bs-target");
                updateGeneralActions(target);
                if (target === "#negative-inventory") {
                    history.replaceState(null, "", "#negative-inventory");
                } else if (location.hash === "#negative-inventory") {
                    history.replaceState(null, "", location.pathname + location.search);
                }
            });
        });

        if (location.hash === "#negative-inventory" && negativeTabButton && window.bootstrap) {
            window.bootstrap.Tab.getOrCreateInstance(negativeTabButton).show();
        }

        const syncCustomLimit = (modeSelect) => {
            const row = modeSelect.closest("tr");
            const input = row?.querySelector(".negative-custom-limit");
            if (!input) return;
            input.disabled = modeSelect.disabled || modeSelect.value !== "CUSTOM";
            if (!input.disabled) input.focus({ preventScroll: true });
        };

        document.querySelectorAll(".negative-limit-mode").forEach((select) => {
            select.addEventListener("change", () => syncCustomLimit(select));
        });

        const filterItems = () => {
            const selectedStore = storeFilter?.value ?? "";
            const query = (itemSearch?.value ?? "").trim().toLocaleLowerCase("vi");
            let count = 0;

            itemRows.forEach((row) => {
                const storeMatches = !selectedStore || row.dataset.storeId === selectedStore;
                const itemMatches = !query || (row.dataset.search ?? "").includes(query);
                const visible = storeMatches && itemMatches;
                row.classList.toggle("d-none", !visible);
                if (visible) count += 1;
            });

            if (visibleCount) visibleCount.textContent = String(count);
        };

        storeFilter?.addEventListener("change", filterItems);
        itemSearch?.addEventListener("input", filterItems);

        if (!form) return;

        form.addEventListener("submit", async (event) => {
            event.preventDefault();

            const invalidCustomLimit = Array.from(document.querySelectorAll(".negative-limit-mode"))
                .find((select) => {
                    if (select.value !== "CUSTOM") return false;
                    const input = select.closest("tr")?.querySelector(".negative-custom-limit");
                    const value = Number(input?.value);
                    return !input?.value || !Number.isFinite(value) || value <= 0;
                });

            if (invalidCustomLimit) {
                invalidCustomLimit.closest("tr")?.querySelector(".negative-custom-limit")?.focus();
                await showMessage("Dữ liệu chưa hợp lệ", "Hạn mức riêng phải lớn hơn 0.", "error");
                return;
            }

            const enabled = document.getElementById("negativeInventoryEnabled")?.checked === true;
            const confirmed = await confirmSave(enabled);
            if (!confirmed) return;

            const submitButton = document.querySelector(`button[form="${form.id}"]`);
            if (submitButton) submitButton.disabled = true;

            try {
                const response = await fetch(form.action, {
                    method: "POST",
                    body: new FormData(form),
                    headers: { "X-Requested-With": "XMLHttpRequest" }
                });
                const payload = await response.json().catch(() => ({}));
                if (!response.ok) {
                    const prefix = response.status === 409
                        ? "Cấu hình đã thay đổi"
                        : response.status === 403
                            ? "Không có quyền"
                            : "Không thể lưu cấu hình";
                    await showMessage(prefix, payload.message ?? `HTTP ${response.status}`, "error");
                    return;
                }

                await showMessage("Đã lưu", payload.message ?? "Đã cập nhật cấu hình âm kho.", "success");
                window.location.hash = "negative-inventory";
                window.location.reload();
            } catch {
                await showMessage("Lỗi kết nối", "Không thể gửi yêu cầu cập nhật cấu hình.", "error");
            } finally {
                if (submitButton) submitButton.disabled = false;
            }
        });
    });

    async function confirmSave(enabled) {
        const title = enabled ? "Bật xuất âm có kiểm soát?" : "Tắt xuất âm thủ công?";
        const text = enabled
            ? "Chỉ tiếp tục nếu SQL Server gate đã đạt. Các approval đang chờ có thể trở thành stale."
            : "Đây là kill switch và sẽ chặn yêu cầu xuất âm mới từ request kế tiếp.";

        if (window.Swal) {
            const result = await window.Swal.fire({
                title,
                text,
                icon: "warning",
                showCancelButton: true,
                confirmButtonText: enabled ? "Xác nhận bật/lưu" : "Xác nhận tắt/lưu",
                cancelButtonText: "Quay lại",
                confirmButtonColor: "#d93d20"
            });
            return result.isConfirmed;
        }

        return window.confirm(`${title}\n\n${text}`);
    }

    async function showMessage(title, text, icon) {
        if (window.Swal) {
            await window.Swal.fire({ title, text, icon, confirmButtonColor: "#d93d20" });
            return;
        }
        window.alert(`${title}: ${text}`);
    }
})();
