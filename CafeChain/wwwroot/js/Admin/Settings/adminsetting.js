(() => {
    "use strict";

    document.addEventListener("DOMContentLoaded", () => {
        const form = document.getElementById("negativeInventorySettingsForm");
        const negativeTabButton = document.getElementById("negative-inventory-tab");
        const generalActions = document.getElementById("generalSettingsActions");
        const negativeInventory = document.getElementById("negative-inventory");
        const storeTabs = Array.from(document.querySelectorAll(".negative-store-tab"));
        const itemSearch = document.getElementById("negativeInventoryItemSearch");
        const pageSizeSelect = document.getElementById("negativeInventoryPageSize");
        const rangeStart = document.getElementById("negativeInventoryRangeStart");
        const rangeEnd = document.getElementById("negativeInventoryRangeEnd");
        const filteredCount = document.getElementById("negativeInventoryFilteredCount");
        const pageStatus = document.getElementById("negativeInventoryPageStatus");
        const pagination = document.getElementById("negativeInventoryPagination");
        const emptyRow = document.getElementById("negativeInventoryEmptyRow");
        const itemRows = Array.from(document.querySelectorAll(".negative-inventory-item-row"));
        let activeStoreId = storeTabs.find((tab) => tab.classList.contains("active"))?.dataset.storeId
            ?? itemRows[0]?.dataset.storeId
            ?? "";
        let currentPage = 1;
        let pageSize = Number(pageSizeSelect?.value) || 10;

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

        const formatQuantity = (value) => Number(value || 0).toLocaleString("vi-VN", {
            maximumFractionDigits: 3
        });

        const applyDisplayUnit = (select) => {
            const row = select.closest("tr");
            const option = select.selectedOptions[0];
            if (!row || !option) return;

            const oldFactor = Number(row.dataset.currentFactor || 1);
            const newFactor = Number(option.dataset.factor || 0);
            if (!Number.isFinite(newFactor) || newFactor <= 0) return;

            const customInput = row.querySelector(".negative-custom-limit");
            if (customInput?.value) {
                const currentDisplay = Number(customInput.value);
                if (Number.isFinite(currentDisplay) && currentDisplay > 0) {
                    const baseValue = currentDisplay * oldFactor;
                    customInput.value = String(Math.round((baseValue / newFactor) * 1000) / 1000);
                }
            }

            const available = Number(row.dataset.baseAvailable || 0) / newFactor;
            const reserved = Number(row.dataset.baseReserved || 0) / newFactor;
            const effective = Number(row.dataset.baseEffectiveLimit || 0) / newFactor;
            row.querySelectorAll(".negative-display-unit-code").forEach(x => x.textContent = option.dataset.code || "");
            const availableElement = row.querySelector(".negative-available-qty");
            const reservedElement = row.querySelector(".negative-reserved-qty");
            const effectiveElement = row.querySelector(".negative-effective-limit");
            if (availableElement) availableElement.textContent = formatQuantity(available);
            if (reservedElement) reservedElement.textContent = formatQuantity(reserved);
            if (effectiveElement) effectiveElement.textContent = formatQuantity(effective);
            row.dataset.currentFactor = String(newFactor);
        };

        document.querySelectorAll(".negative-display-unit").forEach((select) => {
            applyDisplayUnit(select);
            select.addEventListener("change", () => applyDisplayUnit(select));
        });

        const getFilteredRows = () => {
            const query = (itemSearch?.value ?? "").trim().toLocaleLowerCase("vi");
            return itemRows.filter((row) => {
                const storeMatches = row.dataset.storeId === activeStoreId;
                const itemMatches = !query || (row.dataset.search ?? "").includes(query);
                return storeMatches && itemMatches;
            });
        };

        const updateStoreTabs = () => {
            storeTabs.forEach((tab) => {
                const active = tab.dataset.storeId === activeStoreId;
                tab.classList.toggle("active", active);
                tab.setAttribute("aria-selected", active ? "true" : "false");
                tab.setAttribute("tabindex", active ? "0" : "-1");
            });
        };

        const createPageItem = (label, targetPage, options = {}) => {
            const item = document.createElement("li");
            item.className = "page-item";
            item.classList.toggle("active", options.active === true);
            item.classList.toggle("disabled", options.disabled === true);

            const button = document.createElement("button");
            button.type = "button";
            button.className = "page-link";
            button.textContent = label;
            if (options.label) button.setAttribute("aria-label", options.label);
            if (options.active) button.setAttribute("aria-current", "page");
            button.disabled = options.disabled === true;
            if (!button.disabled && !options.active) {
                button.addEventListener("click", () => {
                    currentPage = targetPage;
                    renderItems();
                });
            }

            item.appendChild(button);
            return item;
        };

        const getPageNumbers = (totalPages) => {
            if (totalPages <= 7) {
                return Array.from({ length: totalPages }, (_, index) => index + 1);
            }

            const pages = [1];
            const start = Math.max(2, currentPage - 1);
            const end = Math.min(totalPages - 1, currentPage + 1);
            if (start > 2) pages.push("ellipsis-start");
            for (let page = start; page <= end; page += 1) pages.push(page);
            if (end < totalPages - 1) pages.push("ellipsis-end");
            pages.push(totalPages);
            return pages;
        };

        const renderPagination = (totalPages) => {
            if (!pagination) return;
            pagination.replaceChildren();
            pagination.appendChild(createPageItem("‹", currentPage - 1, {
                disabled: currentPage <= 1,
                label: "Trang trước"
            }));

            getPageNumbers(totalPages).forEach((page) => {
                if (typeof page !== "number") {
                    pagination.appendChild(createPageItem("…", currentPage, { disabled: true }));
                    return;
                }
                pagination.appendChild(createPageItem(String(page), page, { active: page === currentPage }));
            });

            pagination.appendChild(createPageItem("›", currentPage + 1, {
                disabled: currentPage >= totalPages,
                label: "Trang sau"
            }));
        };

        const renderItems = () => {
            const matchingRows = getFilteredRows();
            const totalItems = matchingRows.length;
            const totalPages = Math.max(1, Math.ceil(totalItems / pageSize));
            currentPage = Math.min(Math.max(currentPage, 1), totalPages);

            const startIndex = (currentPage - 1) * pageSize;
            const rowsOnPage = new Set(matchingRows.slice(startIndex, startIndex + pageSize));
            itemRows.forEach((row) => row.classList.toggle("d-none", !rowsOnPage.has(row)));

            const firstItem = totalItems === 0 ? 0 : startIndex + 1;
            const lastItem = totalItems === 0 ? 0 : Math.min(startIndex + pageSize, totalItems);
            if (rangeStart) rangeStart.textContent = String(firstItem);
            if (rangeEnd) rangeEnd.textContent = String(lastItem);
            if (filteredCount) filteredCount.textContent = String(totalItems);
            if (pageStatus) pageStatus.textContent = `Trang ${currentPage} / ${totalPages}`;
            emptyRow?.classList.toggle("d-none", totalItems !== 0);
            renderPagination(totalPages);
        };

        const selectStore = (storeId) => {
            activeStoreId = storeId;
            currentPage = 1;
            updateStoreTabs();
            renderItems();
        };

        const revealRow = (row) => {
            activeStoreId = row.dataset.storeId ?? activeStoreId;
            if (itemSearch) itemSearch.value = "";
            updateStoreTabs();
            const storeRows = getFilteredRows();
            const rowIndex = storeRows.indexOf(row);
            currentPage = rowIndex >= 0 ? Math.floor(rowIndex / pageSize) + 1 : 1;
            renderItems();
        };

        storeTabs.forEach((tab, index) => {
            tab.addEventListener("click", () => selectStore(tab.dataset.storeId ?? ""));
            tab.addEventListener("keydown", (event) => {
                let targetIndex = index;
                if (event.key === "ArrowRight") targetIndex = (index + 1) % storeTabs.length;
                else if (event.key === "ArrowLeft") targetIndex = (index - 1 + storeTabs.length) % storeTabs.length;
                else if (event.key === "Home") targetIndex = 0;
                else if (event.key === "End") targetIndex = storeTabs.length - 1;
                else return;

                event.preventDefault();
                const targetTab = storeTabs[targetIndex];
                targetTab.focus();
                selectStore(targetTab.dataset.storeId ?? "");
            });
        });
        itemSearch?.addEventListener("input", () => {
            currentPage = 1;
            renderItems();
        });
        pageSizeSelect?.addEventListener("change", () => {
            pageSize = Number(pageSizeSelect.value) || 10;
            currentPage = 1;
            renderItems();
        });
        updateStoreTabs();
        renderItems();

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
                const invalidRow = invalidCustomLimit.closest("tr");
                if (invalidRow) revealRow(invalidRow);
                await showMessage("Dữ liệu chưa hợp lệ", "Hạn mức riêng phải lớn hơn 0.", "error");
                invalidRow?.querySelector(".negative-custom-limit")?.focus();
                return;
            }

            const enabled = document.getElementById("negativeInventoryEnabled")?.checked === true;
            const pendingApprovalCount = Number(negativeInventory?.dataset.pendingApprovalCount) || 0;
            const confirmed = await confirmSave(enabled, pendingApprovalCount);
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

    async function confirmSave(enabled, pendingApprovalCount) {
        const title = enabled ? "Bật xuất âm có kiểm soát?" : "Tắt xuất âm thủ công?";
        const pendingText = pendingApprovalCount > 0
            ? ` Hiện có ${pendingApprovalCount} yêu cầu đang chờ và có thể cần được tạo lại nếu policy thay đổi.`
            : "";
        const text = enabled
            ? `Tính năng vẫn bắt buộc phê duyệt và không tự xác nhận phiếu.${pendingText}`
            : `Đây là kill switch và sẽ chặn yêu cầu xuất âm mới từ request kế tiếp.${pendingText}`;

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
