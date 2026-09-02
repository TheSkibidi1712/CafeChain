const InventoryTransferCreate = (() => {
    const catalog = window.CafeChainUiCatalog.read("inventoryTransferUiCatalog");
    const t = (key, values) => window.CafeChainUiCatalog.text(catalog, key, values);
    const nativeFetch = window.fetch.bind(window);
    const fetch = (input, init = {}) => {
        const options = { ...init };
        const method = String(options.method || "GET").toUpperCase();
        if (!["GET", "HEAD", "OPTIONS", "TRACE"].includes(method)) {
            const headers = new Headers(options.headers || {});
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            if (token) headers.set("RequestVerificationToken", token);
            options.headers = headers;
        }
        return nativeFetch(input, options);
    };

    const selector = {
        form: "#inventoryTransferForm",
        id: "#transferId",
        rowVersion: "#transferRowVersion",
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

    function getEndpoint(name) {
        const endpoint = document.querySelector(selector.form)?.dataset[name];
        if (!endpoint) {
            throw new Error(t("Transfer.Js.MissingEndpoint", { name }));
        }

        return endpoint;
    }

    function appendQuery(endpoint, params = {}) {
        const url = new URL(endpoint, window.location.origin);
        Object.entries(params).forEach(([key, value]) => {
            if (value !== undefined && value !== null && value !== "") {
                url.searchParams.set(key, String(value));
            }
        });

        return url.toString();
    }

    let ingredients = [];
    let createDraftRequestKey = createRequestKey();
    let confirmRequestKey = null;
    let isSaving = false;
    let isConfirming = false;
    let validateTimer = null;
    let preflightReady = false;
    let preflightBlocked = true;

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
                preflightReady = false;
                preflightBlocked = true;
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
        preflightReady = false;
        preflightBlocked = true;
        ingredients = [];
        clearRows();
        hideWarnings();
        setTransferId(null);
        setTransferRowVersion(null);
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
                status.textContent = t("Transfer.Js.SelectSource");
            }

            return;
        }

        if (status) {
            status.textContent = t("Transfer.Js.LoadingItems");
        }

        try {
            const response = await fetch(appendQuery(getEndpoint("itemsUrl"), { fromStoreId }));

            if (!response.ok) {
                throw new Error(await readResponseMessage(response));
            }

            ingredients = await response.json();

            if (status) {
                status.textContent = t("Transfer.Js.AvailableItems", { count: ingredients.length });
            }

            if (addButton) {
                addButton.disabled = ingredients.length === 0;
            }
        }
        catch (error) {
            if (status) {
                status.textContent = error.message || t("Transfer.Js.LoadItemsFailed");
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
                    <option value="">${escapeHtml(t("Transfer.Js.SelectItem"))}</option>
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
                <button class="transfer-remove" type="button" title="${escapeHtml(t("Transfer.Js.RemoveLine"))}">
                    <i class="fas fa-trash"></i>
                </button>
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
            warning.textContent = t("Transfer.Js.ExceedsSourceStock");
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

            const result = await postJson(getEndpoint("saveDraftUrl"), dto);
            const saved = read(result.transfer, "inventoryTransferId", "InventoryTransferId");
            const code = read(result.transfer, "code", "Code");

            setTransferId(saved);
            setTransferRowVersion(read(result.transfer, "rowVersion", "RowVersion"));
            updateCodePreview(code);
            notifySuccess(t("Transfer.Js.DraftSaved"));
        }
        catch (error) {
            notifyError(error.message || t("Transfer.Js.SaveDraftFailed"));
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
            await validateStock();
            if (!preflightReady || preflightBlocked) {
                throw new Error(t("Transfer.Js.PreflightBlocked"));
            }

            let transferId = getTransferId();

            if (transferId <= 0) {
                const draft = await postJson(
                    getEndpoint("saveDraftUrl"),
                    buildDto(createDraftRequestKey));

                transferId = read(draft.transfer, "inventoryTransferId", "InventoryTransferId");
                setTransferId(transferId);
                setTransferRowVersion(read(draft.transfer, "rowVersion", "RowVersion"));
                updateCodePreview(read(draft.transfer, "code", "Code"));
            }

            const result = await postJson(
                appendQuery(getEndpoint("dispatchUrl"), {
                    id: transferId,
                    requestKey: confirmRequestKey
                }),
                {});
            updateCodePreview(read(result.transfer, "code", "Code"));

            notifySuccess(t("Transfer.Js.TransferConfirmed"));
            confirmRequestKey = null;
            redirectToDetail(read(result.transfer, "inventoryTransferId", "InventoryTransferId") || transferId);
        }
        catch (error) {
            notifyError(error.message || t("Transfer.Js.ConfirmFailed"));
        }
        finally {
            isConfirming = false;
            setButtonBusy(selector.confirm, false);
            syncConfirmState();
        }
    }

    function debounceValidateStock() {
        window.clearTimeout(validateTimer);
        preflightReady = false;
        preflightBlocked = true;
        syncConfirmState();

        validateTimer = window.setTimeout(validateStock, 350);
    }

    async function validateStock() {
        if (document.querySelectorAll(".transfer-detail-row").length === 0) {
            preflightReady = false;
            preflightBlocked = true;
            hideWarnings();
            return false;
        }

        try {
            const result = await postJson(
                getEndpoint("preflightUrl"),
                buildDto(null, false));
            const warnings = result.warnings || result.Warnings || [];

            preflightReady = true;
            preflightBlocked = warnings.length > 0;
            renderWarnings(warnings);
            syncConfirmState();
            return !preflightBlocked;
        }
        catch (error) {
            preflightReady = false;
            preflightBlocked = true;
            renderWarnings([{ message: error.message || t("Transfer.Js.PreflightFailed") }]);
            syncConfirmState();
            return false;
        }
    }

    function buildDto(requestKey, requireRequestKey = true) {
        const details = Array.from(document.querySelectorAll(".transfer-detail-row"))
            .map(row => {
                const item = getRowIngredient(row);
                const unitId = Number(row.querySelector(".transfer-unit")?.value || 0);
                const quantity = Number(row.querySelector(".transfer-quantity")?.value || 0);
                return {
                    ingredientId: read(item, "ingredientId", "IngredientId") || null,
                    preparedItemId: read(item, "preparedItemId", "PreparedItemId") || null,
                    restockRequestId: null,
                    unitId,
                    quantity,
                    baseQuantity: 0,
                    note: null
                };
            })
            .filter(x => x.ingredientId || x.preparedItemId);

        const dto = {
            transferId: getTransferId() || null,
            rowVersion: getTransferRowVersion() || null,
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
            throw new Error(t("Transfer.Js.SourceRequired"));
        }

        if (!dto.toStoreId) {
            throw new Error(t("Transfer.Js.DestinationRequired"));
        }

        if (dto.fromStoreId === dto.toStoreId) {
            throw new Error(t("Transfer.Js.StoresMustDiffer"));
        }

        if (!dto.details.length) {
            throw new Error(t("Transfer.Js.ItemRequired"));
        }

        const seen = new Set();

        for (const detail of dto.details) {
            if ((!detail.ingredientId && !detail.preparedItemId)
                || (detail.ingredientId && detail.preparedItemId)) {
                throw new Error(t("Transfer.Js.SingleItemTypeRequired"));
            }

            const identityKey = detail.ingredientId
                ? `I:${detail.ingredientId}`
                : `P:${detail.preparedItemId}`;
            if (seen.has(identityKey)) {
                throw new Error(t("Transfer.Js.DuplicateItem"));
            }

            seen.add(identityKey);

            if (!detail.unitId) {
                throw new Error(t("Transfer.Js.UnitRequired"));
            }

            if (detail.quantity <= 0) {
                throw new Error(t("Transfer.Js.QuantityPositive"));
            }
        }
    }

    async function postJson(url, payload) {
        const response = await fetch(
            url,
            {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "RequestVerificationToken": document.querySelector('input[name="__RequestVerificationToken"]')?.value || ""
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
            throw new Error(data?.message || t("Transfer.Js.RequestFailed"));
        }

        return data;
    }

    async function readResponseMessage(response) {
        try {
            const data = await response.json();

            return data?.message || t("Transfer.Js.RequestFailed");
        }
        catch {
            return await response.text() || t("Transfer.Js.RequestFailed");
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

        body.innerHTML = `<tr class="transfer-empty-row"><td colspan="8">${escapeHtml(t("Transfer.Js.NoItems"))}</td></tr>`;
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
            statusCell.textContent = diff >= 0 ? t("Transfer.Js.Ok") : t("Transfer.Js.Exceeded");
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
                `<i class="fas fa-triangle-exclamation"></i> ${escapeHtml(t("Transfer.Js.WarningLines", { count: warningCount }))}`;
            return;
        }

        status.classList.remove("has-error");
        status.innerHTML =
            `<i class="fas fa-circle-info"></i> ${escapeHtml(t("Transfer.Js.CheckStockFirst"))}`;
    }

    function updateCodePreview(code) {
        const input = document.querySelector(selector.codePreview);

        if (input) {
            input.value = code || t("Transfer.Js.AutomaticCode");
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
            button.disabled = !preflightReady || preflightBlocked;
        }
        catch {
            button.disabled = true;
        }
    }

    function getRowIngredient(row) {
        const identity = row.querySelector(".transfer-ingredient")?.value || "";

        if (!identity) {
            return null;
        }

        return ingredients.find(x => getItemIdentity(x) === identity) || null;
    }

    function toIngredientOption(ingredient) {
        const id = getItemIdentity(ingredient);
        const name = escapeHtml(read(ingredient, "itemName", "ItemName") || "");
        const type = read(ingredient, "itemType", "ItemType") === "PREPARED_ITEM"
            ? t("Transfer.Js.PreparedItem")
            : t("Transfer.Js.Ingredient");
        const available = read(ingredient, "availableBaseQuantity", "AvailableBaseQuantity") || 0;
        const unitCode = escapeHtml(read(ingredient, "baseUnitCode", "BaseUnitCode") || "");

        return `<option value="${id}">${escapeHtml(t("Transfer.Js.ItemOption", { type, name, available: formatQuantity(available), unit: unitCode }))}</option>`;
    }

    function getItemIdentity(item) {
        const ingredientId = Number(read(item, "ingredientId", "IngredientId") || 0);
        const preparedItemId = Number(read(item, "preparedItemId", "PreparedItemId") || 0);
        return ingredientId > 0 ? `I:${ingredientId}` : `P:${preparedItemId}`;
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

    function getTransferRowVersion() {
        return document.querySelector(selector.rowVersion)?.value || "";
    }

    function setTransferRowVersion(value) {
        const input = document.querySelector(selector.rowVersion);
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
            window.location.href = appendQuery(getEndpoint("detailUrl"), { id: transferId });
        }, 650);
    }

    return {
        init
    };
})();

document.addEventListener("DOMContentLoaded", InventoryTransferCreate.init);
