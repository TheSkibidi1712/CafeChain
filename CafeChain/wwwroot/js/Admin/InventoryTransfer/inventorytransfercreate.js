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
        warnings: "#transferWarnings"
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
            .querySelector(selector.addIngredient)
            ?.addEventListener("click", addRow);

        document
            .querySelector(selector.saveDraft)
            ?.addEventListener("click", saveDraft);

        document
            .querySelector(selector.confirm)
            ?.addEventListener("click", confirmTransfer);
    }

    async function onFromStoreChanged() {
        ingredients = [];
        clearRows();
        hideWarnings();

        const fromStoreId = getNumber(selector.fromStore);
        const addButton = document.querySelector(selector.addIngredient);
        const status = document.querySelector(selector.stockStatus);

        if (addButton) {
            addButton.disabled = true;
        }

        if (!fromStoreId) {
            if (status) {
                status.textContent = "Chon kho nguon de tai nguyen lieu.";
            }

            return;
        }

        if (status) {
            status.textContent = "Dang tai nguyen lieu...";
        }

        try {
            const response = await fetch(
                `/Admin/AdminInventoryTransfer/Ingredients?fromStoreId=${encodeURIComponent(fromStoreId)}`);

            if (!response.ok) {
                throw new Error(await response.text());
            }

            ingredients = await response.json();

            if (status) {
                status.textContent = `${ingredients.length} nguyen lieu co the chuyen.`;
            }

            if (addButton) {
                addButton.disabled = ingredients.length === 0;
            }
        }
        catch (error) {
            if (status) {
                status.textContent = error.message || "Khong tai duoc nguyen lieu.";
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
            <td>
                <select class="form-control transfer-ingredient">
                    <option value="">Chon nguyen lieu</option>
                    ${ingredients.map(toIngredientOption).join("")}
                </select>
                <span class="transfer-row-warning" hidden></span>
            </td>
            <td>
                <select class="form-control transfer-unit"></select>
            </td>
            <td>
                <input class="form-control transfer-quantity" type="number" min="0" step="any" value="1" />
            </td>
            <td class="transfer-available">0</td>
            <td class="transfer-base-qty">0</td>
            <td>
                <input class="form-control transfer-price" type="number" min="0" step="any" value="0" />
            </td>
            <td class="text-end">
                <button class="btn btn-sm btn-outline-danger transfer-remove" type="button">
                    <i class="fas fa-trash"></i>
                </button>
            </td>`;

        body.appendChild(row);
        bindRow(row);
    }

    function bindRow(row) {
        row
            .querySelector(".transfer-ingredient")
            ?.addEventListener("change", () => {
                fillUnits(row);
                recalculateRow(row);
                debounceValidateStock();
            });

        row
            .querySelector(".transfer-unit")
            ?.addEventListener("change", () => {
                recalculateRow(row);
                debounceValidateStock();
            });

        row
            .querySelector(".transfer-quantity")
            ?.addEventListener("input", () => {
                recalculateRow(row);
                debounceValidateStock();
            });

        row
            .querySelector(".transfer-price")
            ?.addEventListener("input", debounceValidateStock);

        row
            .querySelector(".transfer-remove")
            ?.addEventListener("click", () => {
                row.remove();
                ensureEmptyRow();
                debounceValidateStock();
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
        row.querySelector(".transfer-base-qty").textContent =
            `${formatQuantity(baseQuantity)} ${baseUnitCode}`.trim();

        const warning = row.querySelector(".transfer-row-warning");

        if (!warning) {
            return;
        }

        if (ingredient && baseQuantity > available) {
            warning.hidden = false;
            warning.textContent = "Vuot ton hien co, he thong se kiem tra cau hinh am kho khi xac nhan.";
        }
        else {
            warning.hidden = true;
            warning.textContent = "";
        }
    }

    async function saveDraft() {
        if (isSaving) {
            return;
        }

        isSaving = true;
        setButtonBusy(selector.saveDraft, true);

        try {
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

            setTransferId(saved);
            notifySuccess("Da luu nhap phieu chuyen kho.");
        }
        catch (error) {
            notifyError(error.message || "Khong luu duoc phieu chuyen kho.");
        }
        finally {
            isSaving = false;
            setButtonBusy(selector.saveDraft, false);
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
            let transferId = getTransferId();

            if (transferId <= 0) {
                const draft = await postJson(
                    "/Admin/AdminInventoryTransfer/CreateDraft",
                    buildDto(createDraftRequestKey));

                transferId = read(draft.transfer, "inventoryTransferId", "InventoryTransferId");
                setTransferId(transferId);
            }

            await postJson(
                `/Admin/AdminInventoryTransfer/Confirm?id=${encodeURIComponent(transferId)}&requestKey=${encodeURIComponent(confirmRequestKey)}`,
                {});

            notifySuccess("Da xac nhan phieu chuyen kho.");
            confirmRequestKey = null;
        }
        catch (error) {
            notifyError(error.message || "Khong xac nhan duoc phieu chuyen kho.");
        }
        finally {
            isConfirming = false;
            setButtonBusy(selector.confirm, false);
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
            throw new Error(data?.message || "Request failed.");
        }

        return data;
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
            .map(x => `<div>${escapeHtml(read(x, "message", "Message") || "")}</div>`)
            .join("");
    }

    function hideWarnings() {
        const container = document.querySelector(selector.warnings);

        if (!container) {
            return;
        }

        container.hidden = true;
        container.innerHTML = "";
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

        body.innerHTML = '<tr class="transfer-empty-row"><td colspan="7">Chua co nguyen lieu.</td></tr>';
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

        return `<option value="${id}">${name} - ton ${formatQuantity(available)} ${unitCode}</option>`;
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

    return {
        init
    };
})();

document.addEventListener("DOMContentLoaded", InventoryTransferCreate.init);
