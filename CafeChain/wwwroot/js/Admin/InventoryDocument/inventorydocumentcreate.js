const InventoryCreate = (() => {

    const selector = {
        modal: "#inventoryCreateModal",
        content: "#inventoryCreateContent",
        form: "#inventoryCreateForm",
        type: "#inventoryDocumentType",
        supplier: "#supplierSelect",
        partnerName: "#PartnerName",
        store: "#storeSelect",
        note: "#Note",
        date: "#DocumentDate",
        tableBody: "#ingredientTableBody",
        rowTemplate: "#ingredientRowTemplate",
        addIngredient: "#btnAddIngredient",
        saveDraft: "#btnSaveDraft",
        createDocument: "#btnCreateDocument"
    };

    const partnerType = {
        none: 0,
        supplier: 1
    };

    let supplierIngredients = [];
    let summaryTimer = null;

    async function open(type) {

        const container =
            document.querySelector(
                selector.content);

        container.innerHTML =
            spinner();

        const modal =
            new bootstrap.Modal(
                document.querySelector(
                    selector.modal));

        modal.show();

        try {

            const response =
                await fetch(
                    `/Admin/AdminInventoryDocument/CreateModal?type=${encodeURIComponent(type)}`);

            if (!response.ok) {
                throw new Error(await response.text());
            }

            container.innerHTML =
                await response.text();

            bindCreateEvents();

        }
        catch (error) {

            container.innerHTML =
                createLoadError(
                    error.message || "Không tải được form tạo phiếu."
                );

        }

    }

    function openTypeSelector() {

        Swal.fire({

            html:
                typeSelectorTemplate(),

            showConfirmButton:
                false,

            showCancelButton:
                false,

            width:
                560,

            padding:
                0,

            customClass: {
                popup:
                    "inventory-type-popup"
            },

            didOpen: () => {

                const popup =
                    Swal.getPopup();

                popup
                    .querySelectorAll(
                        "[data-inventory-type]"
                    )
                    .forEach(button => {

                        button.addEventListener(
                            "click",
                            () => {

                                Swal.close();

                                open(
                                    button.dataset.inventoryType
                                );

                            });

                    });

                popup
                    .querySelector(
                        "[data-inventory-type-cancel]"
                    )
                    ?.addEventListener(
                        "click",
                        () => Swal.close()
                    );
            }

        });

    }

    function bindCreateEvents() {

        supplierIngredients = [];

        const form =
            document.querySelector(
                selector.form);

        if (!form) {
            return;
        }

        const supplierSelect =
            form.querySelector(
                selector.supplier);

        const addButton =
            form.querySelector(
                selector.addIngredient);

        const tableBody =
            form.querySelector(
                selector.tableBody);

        supplierSelect
            ?.addEventListener(
                "change",
                async () => {

                    syncPartnerFromSupplier();

                    await loadSupplierIngredients(
                        supplierSelect.value
                    );

                });

        addButton
            ?.addEventListener(
                "click",
                () => {

                    addIngredientRow();

                    requestSummary();

                });

        tableBody
            ?.addEventListener(
                "change",
                event => {

                    const row =
                        event.target.closest(
                            ".ingredient-row"
                        );

                    if (!row) {
                        return;
                    }

                    if (event.target.matches(".ingredient-select")) {
                        applyIngredientToRow(
                            row,
                            Number(event.target.value || 0)
                        );
                    }

                });

        tableBody
            ?.addEventListener(
                "input",
                event => {

                    const row =
                        event.target.closest(
                            ".ingredient-row"
                        );

                    if (!row) {
                        return;
                    }

                    if (event.target.matches(".quantity, .unit-price")) {
                        updateRowAmount(row);
                        requestSummary();
                    }

                });

        tableBody
            ?.addEventListener(
                "click",
                event => {

                    const removeButton =
                        event.target.closest(
                            ".btn-remove-row"
                        );

                    if (!removeButton) {
                        return;
                    }

                    removeButton
                        .closest(".ingredient-row")
                        ?.remove();

                    ensureAtLeastOneRow();
                    renumberRows();
                    requestSummary();

                });

        form
            .querySelector(selector.saveDraft)
            ?.addEventListener(
                "click",
                () => submitDocument(true)
            );

        form
            .querySelector(selector.createDocument)
            ?.addEventListener(
                "click",
                () => submitDocument(false)
            );

        syncPartnerFromSupplier();
        renderIngredientOptions();
        renumberRows();
        requestSummary();
    }

    async function loadSupplierIngredients(supplierId) {

        supplierIngredients = [];
        resetRows();
        renderIngredientOptions(true);
        updateSummary(emptySummary());

        if (!supplierId) {
            renderIngredientOptions();
            return;
        }

        try {

            const response =
                await fetch(
                    `/Admin/AdminInventoryDocument/SupplierIngredients?supplierId=${encodeURIComponent(supplierId)}`
                );

            if (!response.ok) {
                throw new Error(await response.text());
            }

            supplierIngredients =
                await response.json();

            renderIngredientOptions();

        }
        catch (error) {

            renderIngredientOptions();

            showError(
                error.message || "Không tải được danh sách nguyên liệu của nhà cung cấp."
            );

        }

    }

    function syncPartnerFromSupplier() {

        const supplierSelect =
            document.querySelector(
                selector.supplier);

        const partnerInput =
            document.querySelector(
                selector.partnerName);

        if (!supplierSelect || !partnerInput) {
            return;
        }

        const selectedOption =
            supplierSelect.options[
                supplierSelect.selectedIndex
            ];

        partnerInput.value =
            selectedOption && selectedOption.value
                ? selectedOption.text.trim()
                : "";

        const hasSupplier =
            Boolean(selectedOption && selectedOption.value);

        partnerInput.readOnly =
            hasSupplier;

        partnerInput.classList.toggle(
            "create-control-readonly",
            hasSupplier);

    }

    function renderIngredientOptions(isLoading = false) {

        document
            .querySelectorAll(
                `${selector.tableBody} .ingredient-select`
            )
            .forEach(select => {

                const currentValue =
                    select.value;

                select.innerHTML = "";

                select.append(
                    new Option(
                        isLoading ? "Đang tải nguyên liệu..." : "Chọn nguyên liệu",
                        ""
                    )
                );

                supplierIngredients
                    .forEach(item => {

                        const option =
                            new Option(
                                item.ingredientName,
                                item.ingredientId
                            );

                        option.dataset.unitId =
                            item.unitId;

                        option.dataset.unitName =
                            item.unitName;

                        option.dataset.price =
                            item.currentPrice;

                        option.dataset.unitCode =
                            item.unitCode || item.UnitCode || item.unitName;

                        option.dataset.baseUnitName =
                            item.baseUnitName || item.BaseUnitName || item.unitName;

                        option.dataset.baseUnitCode =
                            item.baseUnitCode || item.BaseUnitCode || item.unitCode || item.unitName;

                        option.dataset.conversionFactorToBase =
                            item.conversionFactorToBase ?? item.ConversionFactorToBase ?? 0;

                        option.dataset.canConvertToBase =
                            item.canConvertToBase ?? item.CanConvertToBase ?? false;

                        option.title =
                            `${item.ingredientName} - ${formatCurrency(item.currentPrice)}đ/${item.unitName}`;

                        select.append(option);

                    });

                if (supplierIngredients.some(item => String(item.ingredientId) === currentValue)) {
                    select.value = currentValue;
                }
                else {
                    resetRowIngredient(select.closest(".ingredient-row"));
                }

                select.disabled =
                    isLoading || supplierIngredients.length === 0;

            });

    }

    function addIngredientRow() {

        const tableBody =
            document.querySelector(
                selector.tableBody);

        const template =
            document.querySelector(
                selector.rowTemplate);

        if (!tableBody || !template) {
            return null;
        }

        const row =
            template.content
                .firstElementChild
                .cloneNode(true);

        tableBody.append(row);

        renderIngredientOptions();
        renumberRows();

        return row;
    }

    function resetRows() {

        const tableBody =
            document.querySelector(
                selector.tableBody);

        if (!tableBody) {
            return;
        }

        tableBody.innerHTML = "";
        addIngredientRow();
    }

    function ensureAtLeastOneRow() {

        const tableBody =
            document.querySelector(
                selector.tableBody);

        if (!tableBody || tableBody.querySelector(".ingredient-row")) {
            return;
        }

        addIngredientRow();
    }

    function applyIngredientToRow(row, ingredientId) {

        const item =
            supplierIngredients
                .find(ingredient =>
                    Number(ingredient.ingredientId) === ingredientId
                );

        if (!item) {
            resetRowIngredient(row);
            updateRowAmount(row);
            requestSummary();
            return;
        }

        setValue(row, ".ingredient-id", item.ingredientId);
        setValue(row, ".unit-id", item.unitId);
        setValue(row, ".unit-name", item.unitName);
        setText(row, ".base-unit-name", `Base: ${getBaseUnitLabel(item)}`);
        setText(row, ".ingredient-source", `${item.ingredientName} - ${formatCurrency(item.currentPrice)}đ/${getUnitLabel(item)}`);
        setText(row, ".minimum-order-quantity", item.minimumOrderQuantity ? formatQuantity(item.minimumOrderQuantity) : "-");
        setText(row, ".unit-conversion-display", buildConversionPreview(item, readQuantity(row.querySelector(".quantity")?.value) || 1));

        const ingredientSelect =
            row.querySelector(
                ".ingredient-select");

        if (ingredientSelect) {
            ingredientSelect.title =
                item.ingredientName;
        }

        const priceInput =
            row.querySelector(
                ".unit-price");

        if (priceInput) {
            priceInput.value =
                item.currentPrice ?? 0;
        }

        const quantityInput =
            row.querySelector(
                ".quantity");

        if (quantityInput && !readQuantity(quantityInput.value)) {
            quantityInput.value =
                1;
        }

        updateRowAmount(row);
        requestSummary();
    }

    function resetRowIngredient(row) {

        if (!row) {
            return;
        }

        setValue(row, ".ingredient-id", "");
        setValue(row, ".unit-id", "");
        setValue(row, ".unit-name", "");
        setValue(row, ".base-quantity", 0);
        setValue(row, ".total", 0);
        setText(row, ".base-unit-name", "Base unit");
        setText(row, ".ingredient-source", "Chưa chọn");
        setText(row, ".minimum-order-quantity", "-");
        setText(row, ".unit-conversion-display", "Quy đổi: -");
        setText(row, ".line-total-display", "0");

        const ingredientSelect =
            row.querySelector(
                ".ingredient-select");

        if (ingredientSelect) {
            ingredientSelect.title =
                "";
        }
    }

    function updateRowAmount(row) {

        const quantity =
            normalizeRowQuantity(row);

        const unitPrice =
            readNumber(
                row.querySelector(".unit-price")?.value
            );

        const ingredient =
            getSelectedIngredient(row);

        const conversionFactor =
            readConversionFactor(ingredient);

        const canConvert =
            !ingredient || isIngredientConvertible(ingredient);

        const baseQuantity =
            canConvert
                ? quantity * conversionFactor
                : 0;

        const total =
            quantity * unitPrice;

        setValue(row, ".base-quantity", baseQuantity);
        setValue(row, ".total", total);
        setText(row, ".line-total-display", formatCurrency(total));

        if (ingredient) {
            setText(row, ".unit-conversion-display", buildConversionPreview(ingredient, quantity));
        }
    }

    function renumberRows() {

        document
            .querySelectorAll(
                `${selector.tableBody} .ingredient-row`
            )
            .forEach((row, index) => {

                setText(
                    row,
                    ".row-number",
                    index + 1
                );

            });
    }

    function requestSummary() {

        clearTimeout(summaryTimer);

        summaryTimer =
            setTimeout(
                calculateSummary,
                250
            );
    }

    async function calculateSummary() {

        const dto =
            buildDto(false);

        if (!dto.details.length) {
            updateSummary(emptySummary());
            return;
        }

        try {

            const response =
                await fetch(
                    "/Admin/AdminInventoryDocument/Calculate",
                    {
                        method:
                            "POST",

                        headers: {
                            "Content-Type":
                                "application/json"
                        },

                        body:
                            JSON.stringify(dto)
                    });

            if (!response.ok) {
                throw new Error(await response.text());
            }

            updateSummary(
                await response.json()
            );

        }
        catch {

            updateSummary(
                calculateLocalSummary(dto)
            );

        }

    }

    async function submitDocument(saveAsDraft) {

        const invalidConversion =
            findInvalidConversionRow();

        if (invalidConversion) {
            showError("Nguyên liệu đã chọn chưa có cấu hình quy đổi về đơn vị base.");
            return;
        }

        const dto =
            buildDto(saveAsDraft);

        const endpoint =
            saveAsDraft
                ? "/Admin/AdminInventoryDocument/SaveDraft"
                : "/Admin/AdminInventoryDocument/Create";

        const button =
            document.querySelector(
                saveAsDraft
                    ? selector.saveDraft
                    : selector.createDocument
            );

        setButtonBusy(button, true);

        try {

            const response =
                await fetch(
                    endpoint,
                    {
                        method:
                            "POST",

                        headers: {
                            "Content-Type":
                                "application/json"
                        },

                        body:
                            JSON.stringify(dto)
                    });

            if (!response.ok) {
                throw new Error(await readResponseMessage(response));
            }

            const result =
                await response.json();

            await Swal.fire({
                icon:
                    "success",
                title:
                    saveAsDraft ? "Đã lưu nháp" : "Đã tạo và xác nhận",
                text:
                    `Mã hệ thống: #${result.id}`,
                confirmButtonText:
                    "OK"
            });

            if (!saveAsDraft) {
                await showStockWarnings(result.warnings || result.Warnings || []);
            }

            bootstrap.Modal
                .getInstance(
                    document.querySelector(selector.modal)
                )
                ?.hide();

            window.location.reload();

        }
        catch (error) {

            showError(
                error.message || "Không thể lưu phiếu kho."
            );

        }
        finally {

            setButtonBusy(button, false);

        }

    }

    function buildDto(saveAsDraft) {

        const supplierSelect =
            document.querySelector(
                selector.supplier);

        const supplierId =
            readInt(
                supplierSelect?.value
            );

        return {
            type:
                readInt(
                    document.querySelector(selector.type)?.value
                ),

            purpose:
                readInt(
                    document.querySelector('input[name="Purpose"]:checked')?.value
                ),

            storeId:
                readInt(
                    document.querySelector(selector.store)?.value
                ),

            documentDate:
                document.querySelector(selector.date)?.value,

            note:
                document.querySelector(selector.note)?.value || null,

            partnerType:
                supplierId ? partnerType.supplier : partnerType.none,

            supplierId:
                supplierId || null,

            partnerId:
                supplierId || null,

            partnerName:
                document.querySelector(selector.partnerName)?.value || null,

            saveAsDraft,

            details:
                collectDetails()
        };
    }

    function collectDetails() {

        return Array
            .from(
                document.querySelectorAll(
                    `${selector.tableBody} .ingredient-row`
                )
            )
            .map(row => ({
                ingredientId:
                    readInt(
                        row.querySelector(".ingredient-id")?.value ||
                        row.querySelector(".ingredient-select")?.value
                    ),

                unitId:
                    readInt(
                        row.querySelector(".unit-id")?.value
                    ),

                quantity:
                    readQuantity(
                        row.querySelector(".quantity")?.value
                    ),

                baseQuantity:
                    readNumber(
                        row.querySelector(".base-quantity")?.value
                    ),

                unitPrice:
                    readNumber(
                        row.querySelector(".unit-price")?.value
                    ),

                totalAmount:
                    readNumber(
                        row.querySelector(".total")?.value
                    )
            }))
            .filter(item =>
                item.ingredientId > 0
                && item.unitId > 0
                && item.quantity > 0
            );
    }

    function updateSummary(summary) {

        setDocumentText("#summaryItems", summary.totalItems ?? summary.TotalItems ?? 0);
        setDocumentText("#summaryQty", formatQuantity(summary.totalQuantity ?? summary.TotalQuantity ?? 0));
        setDocumentText("#summaryBaseQty", summary.baseQuantityText ?? summary.BaseQuantityText ?? formatBaseQuantityTextFromRows());
        setDocumentText("#summaryTotal", formatCurrency(summary.totalAmount ?? summary.TotalAmount ?? 0));
        setDocumentText("#summaryVat", formatCurrency(summary.vatAmount ?? summary.VatAmount ?? 0));
        setDocumentText("#summaryFinal", formatCurrency(summary.finalAmount ?? summary.FinalAmount ?? 0));
    }

    function calculateLocalSummary(dto) {

        const totalAmount =
            dto.details
                .reduce(
                    (sum, item) => sum + item.totalAmount,
                    0
                );

        return {
            totalItems:
                dto.details.length,
            totalQuantity:
                dto.details
                    .reduce(
                        (sum, item) => sum + item.quantity,
                        0
                    ),
            baseQuantityText:
                formatBaseQuantityTextFromRows(),
            totalAmount,
            vatAmount:
                0,
            finalAmount:
                totalAmount
        };
    }

    function emptySummary() {

        return {
            totalItems:
                0,
            totalQuantity:
                0,
            baseQuantityText:
                "0",
            totalAmount:
                0,
            vatAmount:
                0,
            finalAmount:
                0
        };
    }

    async function readResponseMessage(response) {

        const contentType =
            response.headers.get("content-type") || "";

        if (contentType.includes("application/json")) {
            const json =
                await response.json();

            return json.message || json.error || "Yêu cầu không hợp lệ.";
        }

        return await response.text();
    }

    function setButtonBusy(button, isBusy) {

        if (!button) {
            return;
        }

        if (isBusy) {
            button.dataset.originalText =
                button.innerHTML;

            button.innerHTML =
                `<span class="spinner-border spinner-border-sm"></span> Đang xử lý`;
        }
        else if (button.dataset.originalText) {
            button.innerHTML =
                button.dataset.originalText;
        }

        button.disabled =
            isBusy;
    }

    function showError(message) {

        Swal.fire({
            icon:
                "error",
            title:
                "Không thể xử lý",
            text:
                message,
            confirmButtonText:
                "Đã hiểu"
        });
    }

    function createLoadError(message) {

        return `
        <div class="inventory-create-loading">
            <div class="alert alert-danger mb-0">
                ${escapeHtml(message)}
            </div>
        </div>`;
    }

    function spinner() {

        return `
        <div class="inventory-create-loading">
            <div class="spinner-border"></div>
            <span>Đang tải dữ liệu tạo phiếu...</span>
        </div>`;
    }

    function typeSelectorTemplate() {

        return `
        <div class="inventory-type-modal">
            <div class="inventory-type-header">
                <div class="inventory-type-title">
                    <span class="inventory-type-logo">
                        <i class="fas fa-box-open"></i>
                    </span>
                    <div>
                        <h3>Tạo Chứng Từ Kho</h3>
                        <p>Chọn loại nghiệp vụ để bắt đầu</p>
                    </div>
                </div>

                <button type="button"
                        class="inventory-type-close"
                        data-inventory-type-cancel
                        aria-label="Đóng">
                    <i class="fas fa-times"></i>
                </button>
            </div>

            <div class="inventory-type-copy">
                <span class="inventory-type-eyebrow">Chọn nghiệp vụ</span>
                <strong>Bạn muốn thực hiện nghiệp vụ nào?</strong>
                <p>Hệ thống sẽ tải mã phiếu, cửa hàng, nhà cung cấp và summary mặc định.</p>
            </div>

            <div class="inventory-type-grid">
                <button type="button"
                        class="inventory-type-card inventory-type-import"
                        data-inventory-type="1">
                    <span class="inventory-type-card-icon">
                        <i class="fas fa-arrow-down"></i>
                    </span>
                    <span>
                        <strong>Nhập kho</strong>
                        <small>Nhận hàng từ NCC hoặc nội bộ</small>
                    </span>
                </button>

                <button type="button"
                        class="inventory-type-card inventory-type-export"
                        data-inventory-type="2">
                    <span class="inventory-type-card-icon">
                        <i class="fas fa-arrow-up"></i>
                    </span>
                    <span>
                        <strong>Xuất kho</strong>
                        <small>Xuất hàng bán hoặc nội bộ</small>
                    </span>
                </button>

                <button type="button"
                        class="inventory-type-card inventory-type-stock"
                        data-inventory-type="4">
                    <span class="inventory-type-card-icon">
                        <i class="fas fa-clipboard-check"></i>
                    </span>
                    <span>
                        <strong>Kiểm kê</strong>
                        <small>Kiểm tra và điều chỉnh tồn</small>
                    </span>
                </button>

                <button type="button"
                        class="inventory-type-card inventory-type-waste"
                        data-inventory-type="3">
                    <span class="inventory-type-card-icon">
                        <i class="fas fa-trash-alt"></i>
                    </span>
                    <span>
                        <strong>Hủy hàng</strong>
                        <small>Hủy hàng hết hạn, hỏng</small>
                    </span>
                </button>
            </div>

            <div class="inventory-type-footer">
                <button type="button"
                        class="inventory-type-cancel"
                        data-inventory-type-cancel>
                    Hủy bỏ
                </button>
            </div>
        </div>`;
    }

    function getSelectedIngredient(row) {

        const ingredientId =
            readInt(
                row?.querySelector(".ingredient-id")?.value ||
                row?.querySelector(".ingredient-select")?.value
            );

        if (!ingredientId) {
            return null;
        }

        return supplierIngredients
            .find(item =>
                Number(item.ingredientId ?? item.IngredientId) === ingredientId
            ) || null;
    }

    function isIngredientConvertible(item) {

        const value =
            item?.canConvertToBase ?? item?.CanConvertToBase;

        return value === true || value === "true";
    }

    function readConversionFactor(item) {

        if (!item) {
            return 1;
        }

        const factor =
            Number(
                item.conversionFactorToBase ??
                item.ConversionFactorToBase ??
                0
            );

        return Number.isFinite(factor) && factor > 0
            ? factor
            : 0;
    }

    function getUnitLabel(item) {

        return item?.unitCode ||
            item?.UnitCode ||
            item?.unitName ||
            item?.UnitName ||
            "";
    }

    function getBaseUnitLabel(item) {

        return item?.baseUnitCode ||
            item?.BaseUnitCode ||
            item?.baseUnitName ||
            item?.BaseUnitName ||
            getUnitLabel(item);
    }

    function buildConversionPreview(item, quantity) {

        if (!item) {
            return "Quy đổi: -";
        }

        if (!isIngredientConvertible(item)) {
            return "Chưa cấu hình quy đổi";
        }

        const baseQuantity =
            quantity * readConversionFactor(item);

        return `Quy đổi: ${formatQuantity(quantity)} ${getUnitLabel(item)} = ${formatQuantity(baseQuantity)} ${getBaseUnitLabel(item)}`;
    }

    function findInvalidConversionRow() {

        return Array
            .from(
                document.querySelectorAll(
                    `${selector.tableBody} .ingredient-row`
                )
            )
            .find(row => {

                const item =
                    getSelectedIngredient(row);

                return item && !isIngredientConvertible(item);
            });
    }

    function formatBaseQuantityTextFromRows() {

        const totals =
            new Map();

        document
            .querySelectorAll(
                `${selector.tableBody} .ingredient-row`
            )
            .forEach(row => {

                const item =
                    getSelectedIngredient(row);

                const baseQuantity =
                    readNumber(
                        row.querySelector(".base-quantity")?.value
                    );

                if (!item || baseQuantity <= 0) {
                    return;
                }

                const label =
                    getBaseUnitLabel(item);

                totals.set(
                    label,
                    (totals.get(label) || 0) + baseQuantity
                );
            });

        const text =
            Array
                .from(totals.entries())
                .map(([label, quantity]) =>
                    `${formatQuantity(quantity)} ${label}`
                );

        return text.length
            ? text.join(", ")
            : "0";
    }

    async function showStockWarnings(warnings) {

        if (!Array.isArray(warnings) || warnings.length === 0) {
            return;
        }

        const html =
            `<div class="text-start">
                <p class="mb-2">Một số nguyên liệu đã gần hết tồn khả dụng:</p>
                <ul class="mb-0">
                    ${warnings
                        .map(item => `<li>${escapeHtml(item.message || item.Message || "")}</li>`)
                        .join("")}
                </ul>
            </div>`;

        await Swal.fire({
            icon:
                "warning",
            title:
                "Nguyên liệu sắp hết",
            html,
            confirmButtonText:
                "OK"
        });
    }

    function readNumber(value) {

        return Number(value || 0);
    }

    function readQuantity(value) {

        const quantity =
            Number(value || 0);

        if (!Number.isFinite(quantity) || quantity <= 0) {
            return 0;
        }

        return Math.trunc(quantity);
    }

    function normalizeRowQuantity(row) {

        const input =
            row.querySelector(".quantity");

        const quantity =
            readQuantity(input?.value);

        if (input && input.value && String(quantity) !== input.value) {
            input.value =
                quantity || "";
        }

        return quantity;
    }

    function readInt(value) {

        return Number.parseInt(value || 0, 10) || 0;
    }

    function formatCurrency(number) {

        return Number(number || 0)
            .toLocaleString("vi-VN");
    }

    function formatQuantity(number) {

        const value =
            Number(number || 0);

        if (!value) {
            return "0";
        }

        return value
            .toLocaleString(
                "vi-VN",
                {
                    maximumFractionDigits:
                        3
                });
    }

    function setValue(root, query, value) {

        const element =
            root?.querySelector(query);

        if (element) {
            element.value =
                value ?? "";
        }
    }

    function setText(root, query, value) {

        const element =
            root?.querySelector(query);

        if (element) {
            element.textContent =
                value ?? "";
        }
    }

    function setDocumentText(query, value) {

        const element =
            document.querySelector(query);

        if (element) {
            element.textContent =
                value;
        }
    }

    function escapeHtml(value) {

        const div =
            document.createElement("div");

        div.textContent =
            value;

        return div.innerHTML;
    }

    return {

        open,

        openTypeSelector

    };

})();
