const InventoryTransferCreate = (() => {
    const selector = {
        form: "#inventoryTransferForm",
        id: "#transferId",
        fromStore: "#transferFromStore",
        toStore: "#transferToStore",
        purpose: "#transferPurpose",
        date: "#transferDate",
        note: "#transferNote",
        addIngredient: "#btnAddTransferIngredient",
        saveDraft: "#btnSaveTransferDraft",
        confirm: "#btnConfirmTransfer",
        tableBody: "#transferDetailBody",
        stockStatus: "#transferStockStatus",
        warnings: "#transferWarnings",
        actionStatus: "#transferActionStatus",
        codePreview: "#transferCodePreview"
    };

    let ingredients = [];
    let createDraftRequestKey = createRequestKey();
    let confirmRequestKey = null;
    let isSaving = false;
    let isConfirming = false;
    let validateTimer = null;

    function init() {
        const form = document.querySelector(selector.form);

        if (!form) {
            return;
        }

        document
            .querySelector(selector.fromStore)
            ?.addEventListener("change", onFromStoreChanged);

        document
            .querySelector(selector.toStore)
            ?.addEventListener("change", () => {
                hideWarnings();
                syncConfirmState();
            });

        document
            .querySelector(selector.addIngredient)
            ?.addEventListener("click", addRow);

        document
            .querySelector(selector.saveDraft)
            ?.addEventListener("click", saveDraft);

        document
            .querySelector(selector.confirm)
            ?.addEventListener("click", confirmTransfer);

        syncConfirmState();
    }

    async function onFromStoreChanged() {
        ingredients = [];
        clearRows();
        hideWarnings();
        setTransferId(null);
        createDraftRequestKey = createRequestKey();
        confirmRequestKey = null;
        updateCodePreview("");
        syncConfirmState();

        const fromStoreId = getNumber(selector.fromStore);
        const addButton = document.querySelector(selector.addIngredient);
        const status = document.querySelector(selector.stockStatus);

        if (addButton) {
            addButton.disabled = true;
        }

        if (!fromStoreId) {
            if (status) {
                status.textContent = "Chọn kho nguồn để tải nguyên liệu.";
            }

            return;
        }

        if (status) {
            status.textContent = "Đang tải nguyên liệu...";
        }

        try {
            const response = await fetch(
                `/Admin/AdminInventoryTransfer/Ingredients?fromStoreId=${encodeURIComponent(fromStoreId)}`);

            if (!response.ok) {
                throw new Error(await readResponseMessage(response));
            }

            ingredients = await response.json();

            if (status) {
                status.textContent = `${ingredients.length} nguyên liệu có thể chuyển.`;
            }

            if (addButton) {
                addButton.disabled = ingredients.length === 0;
            }
        }
        catch (error) {
            if (status) {
                status.textContent = error.message || "Không tải được nguyên liệu.";
            }
        }
    }

    function addRow() {
        const body = document.querySelector(selector.tableBody);

        if (!body || ingredients.length === 0) {
            return;
        }

        body.querySelector(".transfer-empty-row")?.remove();

        const row = document.createElement("tr");
        row.className = "transfer-detail-row";
        row.innerHTML = `
            <td class="transfer-row-index"></td>
            <td>
                <select class="transfer-input transfer-ingredient">
                    <option value="">Chọn nguyên liệu</option>
                    ${ingredients.map(toIngredientOption).join("")}
                </select>
                <span class="transfer-row-warning" hidden></span>
            </td>
            <td>
                <select class="transfer-input transfer-unit"></select>
            </td>
            <td class="transfer-available">0</td>
            <td>
                <input class="transfer-input transfer-quantity" type="number" min="0" step="any" value="1" />
            </td>
            <td class="transfer-diff">0</td>
            <td class="transfer-row-status">-</td>
            <td class="text-end">
                <button class="transfer-remove" type="button" title="Xóa dòng">
                    <i class="fas fa-trash"></i>
                </button>
                <input class="transfer-price" type="hidden" value="0" />
            </td>`;

        body.appendChild(row);
        bindRow(row);
        refreshRowIndexes();
        syncConfirmState();
    }

    function bindRow(row) {
        row
            .querySelector(".transfer-ingredient")
            ?.addEventListener("change", () => {
                fillUnits(row);
                recalculateRow(row);
                debounceValidateStock();
                syncConfirmState();
            });

        row
            .querySelector(".transfer-unit")
            ?.addEventListener("change", () => {
                recalculateRow(row);
                debounceValidateStock();
                syncConfirmState();
            });

        row
            .querySelector(".transfer-quantity")
            ?.addEventListener("input", () => {
                recalculateRow(row);
                debounceValidateStock();
                syncConfirmState();
            });

        row
            .querySelector(".transfer-price")
            ?.addEventListener("input", debounceValidateStock);

        row
            .querySelector(".transfer-remove")
            ?.addEventListener("click", () => {
                row.remove();
                ensureEmptyRow();
                refreshRowIndexes();
                debounceValidateStock();
                syncConfirmState();
            });
    }

    function fillUnits(row) {
        const ingredient = getRowIngredient(row);
        const unitSelect = row.querySelector(".transfer-unit");

        if (!unitSelect) {
            return;
        }

        if (!ingredient) {
            unitSelect.innerHTML = "";
            return;
        }

        const unitOptions = read(ingredient, "unitOptions", "UnitOptions") || [];

        unitSelect.innerHTML = unitOptions
            .map(unit => {
                const id = read(unit, "unitId", "UnitId");
                const name = escapeHtml(read(unit, "unitName", "UnitName") || "");
                const code = escapeHtml(read(unit, "unitCode", "UnitCode") || "");
                const factor = read(unit, "conversionFactorToBase", "ConversionFactorToBase") || 0;

                return `<option value="${id}" data-factor="${factor}">${name} (${code})</option>`;
            })
            .join("");

        const price = read(ingredient, "suggestedUnitPrice", "SuggestedUnitPrice")
            || read(ingredient, "currentPrice", "CurrentPrice")
            || 0;

        row.querySelector(".transfer-price").value = formatNumberInput(price);
    }

    function recalculateRow(row) {
        const ingredient = getRowIngredient(row);
        const quantity = Number(row.querySelector(".transfer-quantity")?.value || 0);
        const unit = row.querySelector(".transfer-unit")?.selectedOptions?.[0];
        const factor = Number(unit?.dataset.factor || 0);
        const baseQuantity = quantity > 0 && factor > 0 ? quantity * factor : 0;
        const available = ingredient
            ? read(ingredient, "availableBaseQuantity", "AvailableBaseQuantity") || 0
            : 0;
        const baseUnitCode = ingredient
            ? read(ingredient, "baseUnitCode", "BaseUnitCode") || ""
            : "";

        row.querySelector(".transfer-available").textContent =
            `${formatQuantity(available)} ${baseUnitCode}`.trim();

        const warning = row.querySelector(".transfer-row-warning");

        if (!warning) {
            return;
        }

        if (ingredient && baseQuantity > available) {
            warning.hidden = false;
            warning.textContent = "Vượt tồn hiện có, hệ thống sẽ kiểm tra cấu hình âm kho khi xác nhận.";
        }
        else {
            warning.hidden = true;
            warning.textContent = "";
        }

        renderRowState(row, available, baseQuantity, baseUnitCode);
    }

    async function saveDraft() {
        if (isSaving) {
            return;
        }

        isSaving = true;
        setButtonBusy(selector.saveDraft, true);

        try {
            validateClient();

            const transferId = getTransferId();
            const dto = buildDto(
                transferId > 0
                    ? createRequestKey()
                    : createDraftRequestKey);

            const url = transferId > 0
                ? `/Admin/AdminInventoryTransfer/UpdateDraft?id=${encodeURIComponent(transferId)}`
                : "/Admin/AdminInventoryTransfer/CreateDraft";

            const result = await postJson(url, dto);
            const saved = read(result.transfer, "inventoryTransferId", "InventoryTransferId");
            const code = read(result.transfer, "code", "Code");

            setTransferId(saved);
            updateCodePreview(code);
            notifySuccess("Đã lưu nháp phiếu chuyển kho.");
        }
        catch (error) {
            notifyError(error.message || "Không lưu được phiếu chuyển kho.");
        }
        finally {
            isSaving = false;
            setButtonBusy(selector.saveDraft, false);
            syncConfirmState();
        }
    }

    async function confirmTransfer() {
        if (isConfirming) {
            return;
        }

        isConfirming = true;
        confirmRequestKey ??= createRequestKey();
        setButtonBusy(selector.confirm, true);

        try {
            validateClient();

            let transferId = getTransferId();

            if (transferId <= 0) {
                const draft = await postJson(
                    "/Admin/AdminInventoryTransfer/CreateDraft",
                    buildDto(createDraftRequestKey));

                transferId = read(draft.transfer, "inventoryTransferId", "InventoryTransferId");
                setTransferId(transferId);
                updateCodePreview(read(draft.transfer, "code", "Code"));
            }

            const result = await postJson(
                `/Admin/AdminInventoryTransfer/Confirm?id=${encodeURIComponent(transferId)}&requestKey=${encodeURIComponent(confirmRequestKey)}`,
                {});
            updateCodePreview(read(result.transfer, "code", "Code"));

            notifySuccess("Đã xác nhận phiếu chuyển kho.");
            confirmRequestKey = null;
            redirectToDetail(read(result.transfer, "inventoryTransferId", "InventoryTransferId") || transferId);
        }
        catch (error) {
            notifyError(error.message || "Không xác nhận được phiếu chuyển kho.");
        }
        finally {
            isConfirming = false;
            setButtonBusy(selector.confirm, false);
            syncConfirmState();
        }
    }

    function debounceValidateStock() {
        window.clearTimeout(validateTimer);

        validateTimer = window.setTimeout(validateStock, 350);
    }

    async function validateStock() {
        if (document.querySelectorAll(".transfer-detail-row").length === 0) {
            hideWarnings();
            return;
        }

        try {
            const result = await postJson(
                "/Admin/AdminInventoryTransfer/ValidateStock",
                buildDto(null, false));
            const warnings = result.warnings || result.Warnings || [];

            renderWarnings(warnings);
        }
        catch {
            hideWarnings();
        }
    }

    function buildDto(requestKey, requireRequestKey = true) {
        const details = Array.from(document.querySelectorAll(".transfer-detail-row"))
            .map(row => {
                const ingredientId = Number(row.querySelector(".transfer-ingredient")?.value || 0);
                const unitId = Number(row.querySelector(".transfer-unit")?.value || 0);
                const quantity = Number(row.querySelector(".transfer-quantity")?.value || 0);
                const unitPriceValue = row.querySelector(".transfer-price")?.value;

                return {
                    ingredientId,
                    unitId,
                    quantity,
                    baseQuantity: 0,
                    unitPrice: unitPriceValue === "" ? null : Number(unitPriceValue),
                    note: null
                };
            })
            .filter(x => x.ingredientId > 0);

        const dto = {
            requestKey: requestKey || null,
            fromStoreId: getNumber(selector.fromStore),
            toStoreId: getNumber(selector.toStore),
            purpose: getNumber(selector.purpose),
            documentDate: document.querySelector(selector.date)?.value,
            note: document.querySelector(selector.note)?.value || null,
            details
        };

        if (requireRequestKey && !dto.requestKey) {
            dto.requestKey = createRequestKey();
        }

        return dto;
    }

    function validateClient() {
        const dto = buildDto(createRequestKey(), false);

        if (!dto.fromStoreId) {
            throw new Error("Vui lòng chọn kho đi.");
        }

        if (!dto.toStoreId) {
            throw new Error("Vui lòng chọn kho đến.");
        }

        if (dto.fromStoreId === dto.toStoreId) {
            throw new Error("Kho đi và kho đến phải khác nhau.");
        }

        if (!dto.details.length) {
            throw new Error("Vui lòng thêm ít nhất một nguyên liệu.");
        }

        const seen = new Set();

        for (const detail of dto.details) {
            if (!detail.ingredientId) {
                throw new Error("Vui lòng chọn nguyên liệu.");
            }

            if (seen.has(detail.ingredientId)) {
                throw new Error("Mỗi nguyên liệu chỉ được xuất hiện một lần.");
            }

            seen.add(detail.ingredientId);

            if (!detail.unitId) {
                throw new Error("Vui lòng chọn đơn vị tính.");
            }

            if (detail.quantity <= 0) {
                throw new Error("Số lượng chuyển phải lớn hơn 0.");
            }
        }
    }

    async function postJson(url, payload) {
        const response = await fetch(
            url,
            {
                method: "POST",
                headers: {
                    "Content-Type": "application/json"
                },
                body: JSON.stringify(payload)
            });

        let data = null;

        try {
            data = await response.json();
        }
        catch {
            data = null;
        }

        if (!response.ok || data?.success === false) {
            throw new Error(data?.message || "Yêu cầu không thành công.");
        }

        return data;
    }

    async function readResponseMessage(response) {
        try {
            const data = await response.json();

            return data?.message || "Yêu cầu không thành công.";
        }
        catch {
            return await response.text() || "Yêu cầu không thành công.";
        }
    }

    function renderWarnings(warnings) {
        const container = document.querySelector(selector.warnings);

        if (!container) {
            return;
        }

        if (!warnings || warnings.length === 0) {
            hideWarnings();
            return;
        }

        container.hidden = false;
        container.innerHTML = warnings
            .map(x => `<div><i class="fas fa-triangle-exclamation"></i> ${escapeHtml(read(x, "message", "Message") || "")}</div>`)
            .join("");

        updateActionStatus(warnings.length);
    }

    function hideWarnings() {
        const container = document.querySelector(selector.warnings);

        if (!container) {
            return;
        }

        container.hidden = true;
        container.innerHTML = "";
        updateActionStatus();
        syncConfirmState();
    }

    function clearRows() {
        const body = document.querySelector(selector.tableBody);

        if (!body) {
            return;
        }

        body.innerHTML = "";
        ensureEmptyRow();
    }

    function ensureEmptyRow() {
        const body = document.querySelector(selector.tableBody);

        if (!body || body.querySelector(".transfer-detail-row")) {
            return;
        }

        body.innerHTML = '<tr class="transfer-empty-row"><td colspan="8">Chưa có nguyên liệu.</td></tr>';
        syncConfirmState();
    }

    function refreshRowIndexes() {
        document
            .querySelectorAll(".transfer-detail-row")
            .forEach((row, index) => {
                const cell = row.querySelector(".transfer-row-index");

                if (cell) {
                    cell.textContent = String(index + 1);
                }
            });
    }

    function renderRowState(row, available, baseQuantity, unitCode) {
        const diff = available - baseQuantity;
        const diffCell = row.querySelector(".transfer-diff");
        const statusCell = row.querySelector(".transfer-row-status");

        if (diffCell) {
            diffCell.textContent =
                `${diff >= 0 ? "+" : ""}${formatQuantity(diff)} ${unitCode}`.trim();
            diffCell.classList.toggle("transfer-diff-positive", diff >= 0);
            diffCell.classList.toggle("transfer-diff-negative", diff < 0);
        }

        if (statusCell) {
            statusCell.textContent = diff >= 0 ? "OK" : "Vượt";
            statusCell.classList.toggle("transfer-status-ok", diff >= 0);
            statusCell.classList.toggle("transfer-status-bad", diff < 0);
        }
    }

    function updateActionStatus(warningCount = 0) {
        const status = document.querySelector(selector.actionStatus);

        if (!status) {
            return;
        }

        if (warningCount > 0) {
            status.classList.add("has-error");
            status.innerHTML =
                `<i class="fas fa-triangle-exclamation"></i> ${warningCount} dòng cần kiểm tra tồn kho trước khi xác nhận.`;
            return;
        }

        status.classList.remove("has-error");
        status.innerHTML =
            '<i class="fas fa-circle-info"></i> Kiểm tra tồn kho trước khi xác nhận.';
    }

    function updateCodePreview(code) {
        const input = document.querySelector(selector.codePreview);

        if (input) {
            input.value = code || "Tự động sinh";
        }
    }

    function syncConfirmState() {
        const button = document.querySelector(selector.confirm);

        if (!button || isConfirming) {
            return;
        }

        const hasRows = document.querySelectorAll(".transfer-detail-row").length > 0;

        if (!hasRows) {
            button.disabled = true;
            return;
        }

        try {
            validateClient();
            button.disabled = false;
        }
        catch {
            button.disabled = true;
        }
    }

    function getRowIngredient(row) {
        const ingredientId = Number(row.querySelector(".transfer-ingredient")?.value || 0);

        if (!ingredientId) {
            return null;
        }

        return ingredients.find(x =>
            Number(read(x, "ingredientId", "IngredientId")) === ingredientId) || null;
    }

    function toIngredientOption(ingredient) {
        const id = read(ingredient, "ingredientId", "IngredientId");
        const name = escapeHtml(read(ingredient, "ingredientName", "IngredientName") || "");
        const available = read(ingredient, "availableBaseQuantity", "AvailableBaseQuantity") || 0;
        const unitCode = escapeHtml(read(ingredient, "baseUnitCode", "BaseUnitCode") || "");

        return `<option value="${id}">${name} - tồn ${formatQuantity(available)} ${unitCode}</option>`;
    }

    function getTransferId() {
        return Number(document.querySelector(selector.id)?.value || 0);
    }

    function setTransferId(value) {
        const input = document.querySelector(selector.id);

        if (input) {
            input.value = value || "";
        }
    }

    function getNumber(cssSelector) {
        return Number(document.querySelector(cssSelector)?.value || 0);
    }

    function read(source, camelName, pascalName) {
        if (!source) {
            return undefined;
        }

        return source[camelName] ?? source[pascalName];
    }

    function createRequestKey() {
        if (window.crypto && crypto.randomUUID) {
            return crypto.randomUUID();
        }

        return "xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx".replace(/[xy]/g, c => {
            const r = Math.random() * 16 | 0;
            const v = c === "x" ? r : (r & 0x3 | 0x8);
            return v.toString(16);
        });
    }

    function formatNumberInput(value) {
        const number = Number(value || 0);

        return Number.isFinite(number)
            ? number.toFixed(3).replace(/\.?0+$/, "")
            : "0";
    }

    function formatQuantity(value) {
        const number = Number(value || 0);

        return Number.isFinite(number)
            ? number.toLocaleString("vi-VN", { maximumFractionDigits: 3 })
            : "0";
    }

    function escapeHtml(value) {
        return String(value)
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#039;");
    }

    function notifySuccess(message) {
        if (window.Swal) {
            Swal.fire({
                icon: "success",
                text: message,
                timer: 1400,
                showConfirmButton: false
            });

            return;
        }

        alert(message);
    }

    function notifyError(message) {
        if (window.Swal) {
            Swal.fire({
                icon: "error",
                text: message
            });

            return;
        }

        alert(message);
    }

    function setButtonBusy(cssSelector, isBusy) {
        const button = document.querySelector(cssSelector);

        if (button) {
            button.disabled = isBusy;
        }
    }

    function redirectToDetail(transferId) {
        if (!transferId) {
            return;
        }

        window.setTimeout(() => {
            window.location.href =
                `/Admin/AdminInventoryTransfer/Detail?id=${encodeURIComponent(transferId)}`;
        }, 650);
    }

    return {
        init
    };
})();

document.addEventListener("DOMContentLoaded", InventoryTransferCreate.init);
