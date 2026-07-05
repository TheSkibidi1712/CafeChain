const InventoryCreate = (() => {

    const selector = {
        modal: "#inventoryCreateModal",
        content: "#inventoryCreateContent",
        form: "#inventoryCreateForm",
        type: "#inventoryDocumentType",
        supplier: "#supplierSelect",
        supplierField: "#supplierField",
        sourceTransfer: "#sourceTransferSelect",
        sourceTransferField: "#sourceTransferField",
        targetStore: "#targetStoreSelect",
        targetStoreField: "#targetStoreField",
        partnerField: "#partnerField",
        partnerType: "#PartnerType",
        partnerLabel: "#partnerNameLabel",
        partnerHint: "#partnerHint",
        partnerName: "#PartnerName",
        store: "#storeSelect",
        note: "#Note",
        notePurposeHint: "#notePurposeHint",
        title: "#inventoryCreateTitle",
        subtitle: "#inventoryCreateSubtitle",
        icon: "#inventoryCreateIcon",
        detailHint: "#createDetailHint",
        date: "#DocumentDate",
        tableBody: "#ingredientTableBody",
        rowTemplate: "#ingredientRowTemplate",
        addIngredient: "#btnAddIngredient",
        saveDraft: "#btnSaveDraft",
        createDocument: "#btnCreateDocument"
    };

    const partnerType = {
        none: 0,
        supplier: 1,
        customer: 2,
        staff: 3,
        store: 4
    };

    const documentType = {
        import: 1,
        export: 2,
        waste: 3,
        stockTake: 4
    };

    const documentPurpose = {
        importPurchase: 1,
        importInternal: 2,
        importAdjustment: 3,
        sale: 5,
        internalOut: 6,
        adjustmentOut: 10
    };

    let supplierIngredients = [];
    let pendingTransfers = [];
    let summaryTimer = null;
    let currentRequestKey = null;

    async function open(type) {

        currentRequestKey =
            createRequestKey();

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

                popup
                    .querySelector(
                        "[data-inventory-transfer]"
                    )
                    ?.addEventListener(
                        "click",
                        () => {
                            Swal.close();
                            window.location.href =
                                "/Admin/AdminInventoryTransfer/Create";
                        });
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

        const storeSelect =
            form.querySelector(
                selector.store);

        const sourceTransferSelect =
            form.querySelector(
                selector.sourceTransfer);

        const targetStoreSelect =
            form.querySelector(
                selector.targetStore);

        supplierSelect
            ?.addEventListener(
                "change",
                async () => {

                    if (!isImportPurchase()) {
                        return;
                    }

                    syncPartnerFromSupplier();

                    await loadSupplierIngredients(
                        supplierSelect.value
                    );

                });

        storeSelect
            ?.addEventListener(
                "change",
                async () => {

                    if (isInternalImport()) {
                        await loadPendingInternalTransfers(
                            storeSelect.value
                        );
                        return;
                    }

                    if (isImportAdjustment()) {
                        await loadActiveIngredients(
                            storeSelect.value
                        );
                        return;
                    }

                    if (usesStoreInventorySource()) {
                        await loadStoreExportIngredients(
                            storeSelect.value
                        );
                        return;
                    }

                    if (usesActiveIngredientSource()) {
                        await loadActiveIngredients(
                            storeSelect.value
                        );
                    }

                });

        sourceTransferSelect
            ?.addEventListener(
                "change",
                async () => {

                    if (sourceTransferSelect.value) {
                        await loadInternalTransferIngredients(
                            sourceTransferSelect.value
                        );
                        return;
                    }

                    supplierIngredients = [];
                    resetRows();
                    renderIngredientOptions();
                    updateSummary(emptySummary());
                    syncPartnerFromTransfer();

                });

        targetStoreSelect
            ?.addEventListener(
                "change",
                syncPartnerFromTargetStore);

        form
            .querySelector(selector.partnerName)
            ?.addEventListener(
                "input",
                () => {
                    if (isExportSale()) {
                        syncManualPartnerType();
                    }
                });

        form
            .querySelectorAll('input[name="Purpose"]')
            .forEach(input => {
                input.addEventListener(
                    "change",
                    applyPurposeMode);
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
                        if (isDuplicateIngredientSelection(event.target)) {
                            notify("Nguyên liệu này đã được thêm ở dòng khác.", "error");
                            resetRowIngredient(row);
                            event.target.value = "";
                            renderIngredientOptions();
                            requestSummary();
                            return;
                        }

                        applyIngredientToRow(
                            row,
                            Number(event.target.value || 0),
                            true
                        );

                        renderIngredientOptions();
                    }

                    if (event.target.matches(".unit-select")) {
                        applyUnitSelectionToRow(row);
                        updateRowAmount(row);
                        requestSummary();
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
                    renderIngredientOptions();
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

        applyPurposeMode();
        renumberRows();
        requestSummary();
    }

    function applyPurposeMode() {

        updateCreateHeader();
        updatePurposeFields();
        updateDetailHint();
        updateQuantityReferenceHeader();
        updateDraftAction();

        if (isImportPurchase()) {
            syncPartnerFromSupplier();

            const supplierId =
                document.querySelector(selector.supplier)?.value;

            if (supplierId) {
                loadSupplierIngredients(supplierId);
            }
            else {
                supplierIngredients = [];
                pendingTransfers = [];
                resetRows();
                renderIngredientOptions();
                updateSummary(emptySummary());
            }

            toggleManualRows(true);
            setPriceEditable(false);
            return;
        }

        if (isInternalImport()) {
            pendingTransfers = [];
            supplierIngredients = [];
            clearStoreSelection();
            resetRows();
            renderIngredientOptions();
            updateSummary(emptySummary());
            syncPartnerFromTransfer();
            toggleManualRows(false);
            setPriceEditable(false);
            renderTransferOptions(false, "Chọn cửa hàng nhận trước");

            return;
        }

        if (isImportAdjustment() || usesActiveIngredientSource()) {
            syncPurposePartner();
            toggleManualRows(true);
            setPriceEditable(isManualPricePurpose());

            const storeId =
                document.querySelector(selector.store)?.value;

            if (isImportAdjustment()) {
                loadActiveIngredients(storeId);
            }
            else if (usesStoreInventorySource()) {
                loadStoreExportIngredients(storeId);
            }
            else {
                loadActiveIngredients(storeId);
            }

            return;
        }

        renderIngredientOptions();
    }

    function updatePurposeFields() {

        const supplierField =
            document.querySelector(selector.supplierField);

        const supplierSelect =
            document.querySelector(selector.supplier);

        const sourceTransferField =
            document.querySelector(selector.sourceTransferField);

        const sourceTransferSelect =
            document.querySelector(selector.sourceTransfer);

        const targetStoreField =
            document.querySelector(selector.targetStoreField);

        const targetStoreSelect =
            document.querySelector(selector.targetStore);

        const partnerField =
            document.querySelector(selector.partnerField);

        const noteInput =
            document.querySelector(selector.note);

        const noteHint =
            document.querySelector(selector.notePurposeHint);

        const showSupplier =
            isImportPurchase();

        const showSourceTransfer =
            isInternalImport();

        supplierField?.classList.toggle("d-none", !showSupplier);
        sourceTransferField?.classList.toggle("d-none", !showSourceTransfer);
        targetStoreField?.classList.toggle("d-none", !isInternalOut());
        partnerField?.classList.toggle("d-none", !shouldShowPartnerField());

        if (supplierSelect) {
            supplierSelect.disabled = !showSupplier;

            if (!showSupplier) {
                supplierSelect.value = "";
            }
        }

        if (sourceTransferSelect) {
            sourceTransferSelect.disabled = !showSourceTransfer;

            if (!showSourceTransfer) {
                sourceTransferSelect.value = "";
            }
        }

        if (targetStoreSelect) {
            targetStoreSelect.disabled = !isInternalOut();

            if (!isInternalOut()) {
                targetStoreSelect.value = "";
            }
        }

        if (noteInput) {
            noteInput.required = isImportAdjustment() || isAdjustmentOut();
            noteInput.placeholder = isImportAdjustment() || isAdjustmentOut()
                ? "Nhập lý do điều chỉnh tồn kho"
                : "Nhập ghi chú cho phiếu kho";
        }

        noteHint?.classList.toggle("d-none", !(isImportAdjustment() || isAdjustmentOut()));

        syncPurposePartner();
    }

    function updateCreateHeader() {

        const title =
            document.querySelector(selector.title);

        const subtitle =
            document.querySelector(selector.subtitle);

        const icon =
            document.querySelector(selector.icon);

        const config =
            getPurposeUiConfig();

        if (title) {
            title.textContent = config.title;
        }

        if (subtitle) {
            subtitle.textContent = config.subtitle;
        }

        if (icon) {
            icon.className = `fas ${config.icon}`;
        }

        syncCodePreview();
    }

    function syncCodePreview() {

        const codeInput =
            document.querySelector("#Code");

        if (!codeInput) {
            return;
        }

        if (!codeInput.dataset.originalCode) {
            codeInput.dataset.originalCode = codeInput.value || "";
        }

        if (isImportAdjustment() || isInternalImport()) {
            codeInput.value = "Tự động sinh khi lưu";
            return;
        }

        if (isImportPurchase() && codeInput.dataset.originalCode) {
            codeInput.value = codeInput.dataset.originalCode;
        }
    }

    function updateDetailHint() {

        const hint =
            document.querySelector(selector.detailHint);

        if (hint) {
            hint.textContent = getPurposeUiConfig().hint;
        }
    }

    function updateQuantityReferenceHeader() {

        const header =
            document.querySelector("#quantityReferenceHeader");

        if (!header) {
            return;
        }

        if (isInternalImport()) {
            header.textContent = "Còn nhận";
            return;
        }

        if (usesStoreInventorySource()) {
            header.textContent = "Tồn khả dụng";
            return;
        }

        header.textContent =
            isImportPurchase()
                ? "MOQ"
                : "Tham chiếu";
    }

    function updateDraftAction() {

        const button =
            document.querySelector(selector.saveDraft);

        if (!button) {
            return;
        }

        button.classList.toggle("d-none", isInternalImport() || isInternalOut());
        button.disabled = isInternalImport() || isInternalOut();
    }

    function getPurposeUiConfig() {

        if (isImportPurchase()) {
            return {
                title: "Tạo Phiếu Nhập Kho",
                subtitle: "Nhập nguyên liệu từ nhà cung cấp",
                icon: "fa-arrow-down",
                hint: "Chọn nhà cung cấp trước, sau đó thêm nguyên liệu và nhập số lượng cần xử lý."
            };
        }

        if (isInternalImport()) {
            return {
                title: "Tạo Phiếu Nhập Nội Bộ",
                subtitle: "Nhận hàng từ phiếu xuất nội bộ đã xác nhận",
                icon: "fa-people-carry",
                hint: "Chọn phiếu xuất nội bộ; hệ thống sẽ tự tải danh sách nguyên liệu cần nhận."
            };
        }

        if (isImportAdjustment()) {
            return {
                title: "Tạo Phiếu Nhập Điều Chỉnh",
                subtitle: "Điều chỉnh tăng tồn kho theo biên bản đối soát",
                icon: "fa-sliders-h",
                hint: "Chọn nguyên liệu cần điều chỉnh tăng và nhập số lượng theo biên bản đối soát."
            };
        }

        if (isExportSale()) {
            return {
                title: "Tạo Phiếu Xuất Bán",
                subtitle: "Xuất nguyên liệu ra khỏi kho cho nghiệp vụ bán hàng hoặc cấp phát",
                icon: "fa-arrow-up",
                hint: "Chọn nguyên liệu cần xuất, nhập số lượng và đơn giá nếu cần ghi nhận giá trị chứng từ."
            };
        }

        if (isAdjustmentOut()) {
            return {
                title: "Tạo Phiếu Xuất Điều Chỉnh",
                subtitle: "Điều chỉnh giảm tồn kho theo biên bản kiểm tra",
                icon: "fa-sliders-h",
                hint: "Chọn nguyên liệu cần điều chỉnh giảm, nhập số lượng và ghi rõ lý do điều chỉnh."
            };
        }

        if (isInternalOut()) {
            return {
                title: "Tạo Phiếu Xuất Nội Bộ",
                subtitle: "Xuất hàng sang cửa hàng khác trong chuỗi",
                icon: "fa-arrow-up",
                hint: "Chọn cửa hàng nhận và nguyên liệu cần xuất nội bộ."
            };
        }

        return {
            title: document.querySelector(selector.title)?.textContent || "Tạo Phiếu Kho",
            subtitle: document.querySelector(selector.subtitle)?.textContent || "Khởi tạo chứng từ kho",
            icon: document.querySelector(selector.icon)?.className?.split(" ").find(x => x.startsWith("fa-") && x !== "fas") || "fa-boxes",
            hint: "Thêm nguyên liệu và nhập số lượng cần xử lý."
        };
    }

    function syncPurposePartner() {

        if (!shouldShowPartnerField()) {
            clearPartner();
            setPartnerType(partnerType.none);
            return;
        }

        if (isImportPurchase()) {
            configurePartnerField(
                "Nhà cung cấp",
                "Đối tác được lấy tự động từ nhà cung cấp đã chọn.",
                "Chọn nhà cung cấp",
                true
            );
            setPartnerType(partnerType.supplier);
            syncPartnerFromSupplier();
            return;
        }

        if (isInternalImport()) {
            configurePartnerField(
                "Cửa hàng xuất",
                "Đối tác được lấy từ phiếu xuất nội bộ đang chờ nhận.",
                "Chọn phiếu xuất nội bộ",
                true
            );
            setPartnerType(partnerType.store);
            syncPartnerFromTransfer();
            return;
        }

        if (isInternalOut()) {
            configurePartnerField(
                "Cửa hàng nhận",
                "Đối tác được lấy tự động từ cửa hàng nhận.",
                "Chọn cửa hàng nhận",
                true
            );
            setPartnerType(partnerType.store);
            syncPartnerFromTargetStore();
            return;
        }

        if (isExportSale()) {
            configurePartnerField(
                "Khách hàng / Đối tác nhận",
                "Có thể nhập tên đối tác để lưu vào chứng từ xuất kho.",
                "Nhập tên khách hàng hoặc đối tác nhận",
                false
            );
            syncManualPartnerType();
            return;
        }

        clearPartner();
        setPartnerType(partnerType.none);
    }

    function configurePartnerField(label, hint, placeholder, readOnly) {

        const labelElement =
            document.querySelector(selector.partnerLabel);

        const hintElement =
            document.querySelector(selector.partnerHint);

        const partnerInput =
            document.querySelector(selector.partnerName);

        if (labelElement) {
            labelElement.textContent = label;
        }

        if (hintElement) {
            hintElement.textContent = hint;
        }

        if (partnerInput) {
            partnerInput.placeholder = placeholder;
            partnerInput.readOnly = readOnly;
            partnerInput.classList.toggle(
                "create-control-readonly",
                readOnly);
        }
    }

    function setPartnerType(value) {

        const input =
            document.querySelector(selector.partnerType);

        if (input) {
            input.value = value;
        }
    }

    function syncManualPartnerType() {

        const partnerInput =
            document.querySelector(selector.partnerName);

        const hasPartnerName =
            Boolean(partnerInput?.value?.trim());

        setPartnerType(
            hasPartnerName
                ? partnerType.customer
                : partnerType.none);
    }

    function clearPartner() {

        const partnerInput =
            document.querySelector(selector.partnerName);

        if (!partnerInput) {
            return;
        }

        partnerInput.value = "";
        partnerInput.readOnly = false;
        partnerInput.classList.toggle("create-control-readonly", false);
        setPartnerType(partnerType.none);
    }

    function clearStoreSelection() {

        const storeSelect =
            document.querySelector(selector.store);

        if (storeSelect) {
            storeSelect.value = "";
        }
    }

    async function loadActiveIngredients(storeId) {

        supplierIngredients = [];
        resetRows();
        renderIngredientOptions(true);
        updateSummary(emptySummary());

        if (!storeId) {
            renderIngredientOptions(false, "Chọn cửa hàng trước");
            return;
        }

        try {

            const response =
                await fetch(
                    `/Admin/AdminInventoryDocument/ActiveIngredients?storeId=${encodeURIComponent(storeId)}&purpose=${encodeURIComponent(documentPurpose.importAdjustment)}`
                );

            if (!response.ok) {
                throw new Error(await response.text());
            }

            supplierIngredients =
                await response.json();

            renderIngredientOptions();
            setPriceEditable(isManualPricePurpose());

        }
        catch (error) {

            renderIngredientOptions();

            showError(
                error.message || "Không tải được danh sách nguyên liệu."
            );

        }
    }

    async function loadStoreExportIngredients(storeId) {

        supplierIngredients = [];
        resetRows();
        renderIngredientOptions(true);
        updateSummary(emptySummary());

        if (!storeId) {
            renderIngredientOptions(false, "Chọn cửa hàng trước");
            return;
        }

        try {

            const response =
                await fetch(
                    `/Admin/AdminInventoryDocument/StoreExportIngredients?storeId=${encodeURIComponent(storeId)}`
                );

            if (!response.ok) {
                throw new Error(await response.text());
            }

            supplierIngredients =
                await response.json();

            renderIngredientOptions();
            setPriceEditable(isManualPricePurpose());

        }
        catch (error) {

            renderIngredientOptions();

            showError(
                error.message || "Không tải được danh sách nguyên liệu tồn kho."
            );

        }
    }

    async function loadPendingInternalTransfers(storeId) {

        pendingTransfers = [];
        supplierIngredients = [];
        resetRows();
        renderIngredientOptions();
        updateSummary(emptySummary());
        renderTransferOptions(true);
        syncPartnerFromTransfer();

        if (!storeId) {
            renderTransferOptions(false, "Chọn cửa hàng nhận trước");
            return;
        }

        try {

            const response =
                await fetch(
                    `/Admin/AdminInventoryDocument/PendingInternalTransfers?storeId=${encodeURIComponent(storeId)}`
                );

            if (!response.ok) {
                throw new Error(await response.text());
            }

            pendingTransfers =
                await response.json();

            renderTransferOptions(false);

        }
        catch (error) {

            renderTransferOptions(false);

            showError(
                error.message || "Không tải được danh sách phiếu xuất nội bộ."
            );

        }
    }

    async function loadInternalTransferIngredients(transferId) {

        supplierIngredients = [];
        resetRows();
        renderIngredientOptions(true);
        updateSummary(emptySummary());
        syncPartnerFromTransfer();

        if (!transferId) {
            renderIngredientOptions();
            return;
        }

        try {

            const response =
                await fetch(
                    `/Admin/AdminInventoryDocument/InternalTransferIngredients?transferId=${encodeURIComponent(transferId)}`
                );

            if (!response.ok) {
                throw new Error(await readResponseMessage(response));
            }

            supplierIngredients =
                await response.json();

            renderInternalTransferRows();
            setPriceEditable(false);

        }
        catch (error) {

            renderIngredientOptions();

            showError(
                error.message || "Không tải được chi tiết phiếu xuất nội bộ."
            );

        }
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
            setPriceEditable(false);

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

        if (!isImportPurchase()) {
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

        setPartnerType(
            hasSupplier
                ? partnerType.supplier
                : partnerType.none);

    }

    function renderTransferOptions(isLoading, placeholder) {

        const select =
            document.querySelector(
                selector.sourceTransfer);

        if (!select) {
            return;
        }

        select.innerHTML = "";

        select.append(
            new Option(
                isLoading ? "Đang tải phiếu xuất nội bộ..." : "Chọn phiếu xuất nội bộ",
                ""
            )
        );

        if (select.options[0]) {
            select.options[0].text =
                isLoading
                    ? "Đang tải phiếu xuất nội bộ..."
                    : placeholder || "Chọn phiếu xuất nội bộ";
        }

        pendingTransfers.forEach(item => {

            const quantity =
                item.totalBaseQuantity ?? item.TotalBaseQuantity ?? 0;

            const label =
                `${item.transferCode || item.TransferCode || ""} - ${item.fromStoreName || item.FromStoreName} (${formatQuantity(quantity)} còn nhận)`;

            const option =
                new Option(
                    label,
                    item.inventoryTransferId ?? item.InventoryTransferId
                );

            option.dataset.fromStoreName =
                item.fromStoreName || item.FromStoreName || "";

            select.append(option);

        });

        select.disabled =
            isLoading || pendingTransfers.length === 0;
    }

    function syncPartnerFromTransfer() {

        const transferSelect =
            document.querySelector(
                selector.sourceTransfer);

        const partnerInput =
            document.querySelector(
                selector.partnerName);

        if (!transferSelect || !partnerInput) {
            return;
        }

        const option =
            transferSelect.options[
                transferSelect.selectedIndex
            ];

        const hasTransfer =
            Boolean(option && option.value);

        partnerInput.value =
            option?.dataset.fromStoreName || "";

        partnerInput.readOnly = true;

        partnerInput.classList.toggle(
            "create-control-readonly",
            true);

        setPartnerType(
            hasTransfer
                ? partnerType.store
                : partnerType.none);
    }

    function syncPartnerFromTargetStore() {

        const targetSelect =
            document.querySelector(
                selector.targetStore);

        const partnerInput =
            document.querySelector(
                selector.partnerName);

        if (!targetSelect || !partnerInput) {
            return;
        }

        if (!isInternalOut()) {
            clearPartner();
            return;
        }

        const option =
            targetSelect.options[
                targetSelect.selectedIndex
            ];

        partnerInput.value =
            option && option.value
                ? option.text.trim()
                : "";

        partnerInput.readOnly = true;

        partnerInput.classList.toggle(
            "create-control-readonly",
            Boolean(option && option.value));

        setPartnerType(
            option && option.value
                ? partnerType.store
                : partnerType.none);
    }

    function renderInternalTransferRows() {

        const tableBody =
            document.querySelector(
                selector.tableBody);

        if (!tableBody) {
            return;
        }

        tableBody.innerHTML = "";

        supplierIngredients.forEach(item => {

            const row =
                addIngredientRow();

            if (!row) {
                return;
            }

            const select =
                row.querySelector(".ingredient-select");

            if (select) {
                select.value =
                    item.ingredientId ?? item.IngredientId;

                select.disabled =
                    true;
            }

            applyIngredientToRow(
                row,
                Number(item.ingredientId ?? item.IngredientId ?? 0)
            );

            const quantity =
                item.minimumOrderQuantity ?? item.MinimumOrderQuantity ?? 0;

            setValue(row, ".quantity", quantity);

            const quantityInput =
                row.querySelector(".quantity");

            if (quantityInput) {
                quantityInput.readOnly = true;
            }

            const removeButton =
                row.querySelector(".btn-remove-row");

            if (removeButton) {
                removeButton.disabled = true;
            }

            updateRowAmount(row);

        });

        renderIngredientOptions();
        toggleManualRows(false);

        document
            .querySelectorAll(`${selector.tableBody} .ingredient-select, ${selector.tableBody} .unit-select, ${selector.tableBody} .quantity`)
            .forEach(input => {
                input.disabled = input.matches(".ingredient-select, .unit-select");
                input.readOnly = input.matches(".quantity");
            });

        renumberRows();
        requestSummary();
    }

    function toggleManualRows(enabled) {

        const addButton =
            document.querySelector(
                selector.addIngredient);

        if (addButton) {
            addButton.disabled = !enabled;
        }

        document
            .querySelectorAll(".btn-remove-row")
            .forEach(button => {
                button.disabled = !enabled;
            });
    }

    function setPriceEditable(editable) {

        document
            .querySelectorAll(`${selector.tableBody} .ingredient-row`)
            .forEach(row => {
                const input =
                    row.querySelector(".unit-price");

                if (!input) {
                    return;
                }

                const item =
                    getSelectedIngredient(row);

                input.readOnly =
                    !editable || !item || isPriceLocked(item);
            });
    }

    function renderIngredientOptions(isLoading = false, placeholder = "Chọn nguyên liệu") {

        document
            .querySelectorAll(
                `${selector.tableBody} .ingredient-select`
            )
            .forEach(select => {

                const currentValue =
                    select.value;

                const selectedIngredientIds =
                    getSelectedIngredientIds(select);

                select.innerHTML = "";

                select.append(
                    new Option(
                        isLoading ? "Đang tải nguyên liệu..." : placeholder,
                        ""
                    )
                );

                supplierIngredients
                    .filter(item => {

                        const ingredientId =
                            String(item.ingredientId ?? item.IngredientId ?? "");

                        return ingredientId
                            && (
                                ingredientId === currentValue
                                || !selectedIngredientIds.has(ingredientId)
                            );

                    })
                    .forEach(item => {

                        const option =
                            new Option(
                                item.ingredientName ?? item.IngredientName,
                                item.ingredientId ?? item.IngredientId
                            );

                        option.dataset.unitId =
                            item.unitId ?? item.UnitId;

                        option.dataset.unitName =
                            item.unitName ?? item.UnitName ?? "";

                        option.dataset.price =
                            item.currentPrice ?? item.CurrentPrice ?? 0;

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

                        option.dataset.availableBaseQuantity =
                            item.availableBaseQuantity ?? item.AvailableBaseQuantity ?? 0;

                        option.title =
                            buildIngredientOptionTitle(item);

                        select.append(option);

                    });

                if (supplierIngredients.some(item => String(item.ingredientId ?? item.IngredientId) === currentValue)) {
                    select.value = currentValue;
                }
                else {
                    resetRowIngredient(select.closest(".ingredient-row"));
                }

                select.disabled =
                    isLoading || supplierIngredients.length === 0 || isInternalImport();

            });

    }

    function getSelectedIngredientIds(exceptSelect = null) {

        const ids =
            new Set();

        document
            .querySelectorAll(`${selector.tableBody} .ingredient-select`)
            .forEach(select => {

                if (select === exceptSelect || !select.value) {
                    return;
                }

                ids.add(String(select.value));

            });

        return ids;
    }

    function isDuplicateIngredientSelection(select) {

        if (!select?.value) {
            return false;
        }

        return getSelectedIngredientIds(select)
            .has(String(select.value));
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
        setPriceEditable(isManualPricePurpose());
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

    function applyIngredientToRow(row, ingredientId, resetQuantity = false) {

        const item =
            supplierIngredients
                .find(ingredient =>
                    Number(ingredient.ingredientId ?? ingredient.IngredientId) === ingredientId
                );

        if (!item) {
            resetRowIngredient(row);
            updateRowAmount(row);
            requestSummary();
            return;
        }

        const remainingQuantity =
            item.minimumOrderQuantity ?? item.MinimumOrderQuantity ?? 0;

        const isLockedQuantity =
            isQuantityLocked(item);

        const displayQuantity =
            isInternalImport() || isLockedQuantity
                ? remainingQuantity
                : resetQuantity
                ? 1
                : readQuantity(row.querySelector(".quantity")?.value) || 1;

        setValue(row, ".ingredient-id", item.ingredientId ?? item.IngredientId);
        setText(row, ".base-unit-name", `Base: ${getBaseUnitLabel(item)}`);
        setText(row, ".ingredient-source", buildIngredientSourceText(item));
        setText(row, ".available-stock-display", buildAvailableStockText(item));
        setText(row, ".price-source-display", buildPriceSourceText(item));
        setText(row, ".minimum-order-quantity", buildQuantityReferenceText(item));

        const ingredientSelect =
            row.querySelector(
                ".ingredient-select");

        if (ingredientSelect) {
            ingredientSelect.title =
                item.ingredientName ?? item.IngredientName ?? "";
        }

        renderUnitOptions(row, item);
        setRowPriceForSelectedUnit(row, item);

        const priceInput =
            row.querySelector(
                ".unit-price");

        if (priceInput) {
            priceInput.readOnly =
                isPriceLocked(item) || !isManualPricePurpose();
        }

        const quantityInput =
            row.querySelector(
                ".quantity");

        if (quantityInput) {
            if (isInternalImport() || isLockedQuantity) {
                quantityInput.value =
                    remainingQuantity;

                quantityInput.readOnly = true;
            }
            else if (resetQuantity || !readQuantity(quantityInput.value)) {
                quantityInput.value =
                    1;
                quantityInput.readOnly = false;
            }
        }

        setText(row, ".unit-conversion-display", buildConversionPreview(item, displayQuantity, row));
        updateRowAmount(row);
        requestSummary();
    }

    function resetRowIngredient(row) {

        if (!row) {
            return;
        }

        setValue(row, ".ingredient-id", "");
        setValue(row, ".unit-id", "");
        setUnitSelectOptions(row, []);
        setValue(row, ".quantity", "");
        setValue(row, ".unit-price", "");
        setValue(row, ".base-quantity", 0);
        setValue(row, ".total", 0);
        setText(row, ".base-unit-name", "Base unit");
        setText(row, ".ingredient-source", "Chưa chọn");
        setText(row, ".available-stock-display", "Tồn: -");
        setText(row, ".price-source-display", "Nguồn giá: -");
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

    function renderUnitOptions(row, item) {

        const options =
            getUnitOptions(item);

        setUnitSelectOptions(row, options);

        const unitSelect =
            row.querySelector(".unit-select");

        if (!unitSelect) {
            return;
        }

        const defaultUnitId =
            String(item.unitId ?? item.UnitId ?? options[0]?.unitId ?? "");

        unitSelect.value =
            options.some(option => String(option.unitId) === defaultUnitId)
                ? defaultUnitId
                : String(options[0]?.unitId ?? "");

        unitSelect.disabled =
            isImportPurchase()
            || isInternalImport()
            || isQuantityLocked(item)
            || options.length <= 1;

        setValue(row, ".unit-id", unitSelect.value);
    }

    function setUnitSelectOptions(row, options) {

        const unitSelect =
            row?.querySelector(".unit-select");

        if (!unitSelect) {
            return;
        }

        unitSelect.innerHTML = "";

        if (!options.length) {
            unitSelect.append(new Option("ĐVT", ""));
            unitSelect.disabled = true;
            return;
        }

        options.forEach(option => {
            const selectOption =
                new Option(
                    option.unitCode || option.unitName || "ĐVT",
                    option.unitId
                );

            selectOption.dataset.conversionFactorToBase =
                option.conversionFactorToBase;

            selectOption.dataset.unitName =
                option.unitName || "";

            selectOption.title =
                option.unitName || option.unitCode || "";

            unitSelect.append(selectOption);
        });
    }

    function applyUnitSelectionToRow(row) {

        const item =
            getSelectedIngredient(row);

        if (!item) {
            return;
        }

        const unitSelect =
            row.querySelector(".unit-select");

        setValue(row, ".unit-id", unitSelect?.value || "");
        setRowPriceForSelectedUnit(row, item);
        setText(row, ".ingredient-source", buildIngredientSourceText(item, row));
    }

    function setRowPriceForSelectedUnit(row, item) {

        const priceInput =
            row.querySelector(".unit-price");

        if (!priceInput) {
            return;
        }

        const unitPrice =
            getSuggestedUnitPrice(item, row);

        priceInput.value =
            unitPrice || "";

        priceInput.readOnly =
            isPriceLocked(item) || !isManualPricePurpose();
    }

    function buildIngredientSourceText(item, row) {

        const unitLabel =
            row
                ? getSelectedUnitLabel(row, item)
                : getUnitLabel(item);

        const price =
            row
                ? getSuggestedUnitPrice(item, row)
                : item.currentPrice ?? item.CurrentPrice ?? 0;

        return `${item.ingredientName ?? item.IngredientName} - ${formatCurrency(price)}đ/${unitLabel}`;
    }

    function buildAvailableStockText(item) {

        const available =
            getAvailableBaseQuantity(item);

        if (!usesStoreInventorySource() && !isImportAdjustment()) {
            return "Tồn: -";
        }

        return `Tồn: ${formatQuantity(available)} ${getBaseUnitLabel(item)}`;
    }

    function buildPriceSourceText(item) {

        const source =
            item.priceSource ?? item.PriceSource ?? "";

        return source
            ? `Nguồn giá: ${source}`
            : "Nguồn giá: -";
    }

    function buildQuantityReferenceText(item) {

        if (isInternalImport()) {
            const quantity =
                item.minimumOrderQuantity ?? item.MinimumOrderQuantity ?? 0;

            return formatQuantity(quantity);
        }

        if (usesStoreInventorySource()) {
            return formatQuantity(getAvailableBaseQuantity(item));
        }

        const moq =
            item.minimumOrderQuantity ?? item.MinimumOrderQuantity;

        return moq
            ? formatQuantity(moq)
            : "-";
    }

    function buildIngredientOptionTitle(item) {

        const name =
            item.ingredientName ?? item.IngredientName ?? "";

        const source =
            item.priceSource ?? item.PriceSource ?? "";

        const available =
            getAvailableBaseQuantity(item);

        return `${name} - ${source || "Chưa có nguồn giá"} - Tồn ${formatQuantity(available)} ${getBaseUnitLabel(item)}`;
    }

    function clampRowQuantityToAvailableStock(row, item, quantity, baseQuantity) {

        if (!item || !usesStoreInventorySource()) {
            return { quantity, baseQuantity };
        }

        const available =
            getAvailableBaseQuantity(item);

        if (available <= 0 || baseQuantity <= available) {
            return { quantity, baseQuantity };
        }

        const conversionFactor =
            readRowConversionFactor(row, item);

        if (conversionFactor <= 0) {
            return { quantity, baseQuantity };
        }

        const maxQuantity =
            available / conversionFactor;

        const normalizedMax =
            roundQuantity(maxQuantity);

        const quantityInput =
            row.querySelector(".quantity");

        if (quantityInput) {
            quantityInput.value =
                normalizedMax || "";
        }

        notify(
            "Số lượng vượt tồn khả dụng, hệ thống đã tự giảm về mức còn tồn.",
            "error"
        );

        return {
            quantity: normalizedMax,
            baseQuantity: normalizedMax * conversionFactor
        };
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
            readRowConversionFactor(row, ingredient);

        const canConvert =
            !ingredient || conversionFactor > 0;

        const baseQuantity =
            canConvert
                ? quantity * conversionFactor
                : 0;

        const safeQuantity =
            clampRowQuantityToAvailableStock(row, ingredient, quantity, baseQuantity);

        const finalQuantity =
            safeQuantity.quantity;

        const finalBaseQuantity =
            safeQuantity.baseQuantity;

        const total =
            finalQuantity * unitPrice;

        setValue(row, ".base-quantity", finalBaseQuantity);
        setValue(row, ".total", total);
        setText(row, ".line-total-display", formatCurrency(total));

        if (ingredient) {
            setText(row, ".unit-conversion-display", buildConversionPreview(ingredient, finalQuantity, row));
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

        if (saveAsDraft && isInternalImport()) {
            showError("Nghiệp vụ nhập nội bộ phải tạo và xác nhận ngay để hệ thống đối soát phiếu chuyển.");
            return;
        }

        if (isImportAdjustment()
            && !document.querySelector(selector.note)?.value?.trim()) {
            showError("Phiếu nhập điều chỉnh phải có ghi chú lý do điều chỉnh.");
            return;
        }

        if (isAdjustmentOut()
            && !document.querySelector(selector.note)?.value?.trim()) {
            showError("Phiếu xuất điều chỉnh phải có ghi chú lý do điều chỉnh.");
            return;
        }

        if (saveAsDraft && isInternalOut()) {
            showError("Xuất nội bộ phải tạo và xác nhận ngay để sinh phiếu chuyển nội bộ.");
            return;
        }

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

            notify(
                `${saveAsDraft ? "Đã lưu nháp" : "Đã tạo và xác nhận"} - Mã hệ thống: #${result.id}`,
                "success"
            );

            if (!saveAsDraft) {
                await showStockWarnings(result.warnings || result.Warnings || []);
            }

            bootstrap.Modal
                .getInstance(
                    document.querySelector(selector.modal)
                )
                ?.hide();

            setTimeout(
                () => window.location.reload(),
                700
            );

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

        const sourceTransferId =
            readInt(
                document.querySelector(selector.sourceTransfer)?.value
            );

        const targetStoreId =
            readInt(
                document.querySelector(selector.targetStore)?.value
            );

        const isStorePartner =
            isInternalImport() || isInternalOut();

        const effectiveSupplierId =
            isImportPurchase()
                ? supplierId
                : 0;

        const effectiveSourceTransferId =
            isInternalImport()
                ? sourceTransferId
                : 0;

        const effectiveTargetStoreId =
            isInternalOut()
                ? targetStoreId
                : 0;

        const partnerNameValue =
            shouldShowPartnerField()
                ? document.querySelector(selector.partnerName)?.value?.trim() || null
                : null;

        const effectivePartnerType =
            effectiveSupplierId
                ? partnerType.supplier
                : isStorePartner && (effectiveTargetStoreId || effectiveSourceTransferId)
                ? partnerType.store
                : isExportSale() && partnerNameValue
                ? partnerType.customer
                : partnerType.none;

        const effectivePartnerId =
            isInternalOut()
                ? effectiveTargetStoreId || null
                : isInternalImport()
                ? effectiveSourceTransferId || null
                : effectiveSupplierId || null;

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
                effectivePartnerType,

            supplierId:
                effectiveSupplierId || null,

            partnerId:
                effectivePartnerId,

            partnerName:
                partnerNameValue,

            targetStoreId:
                effectiveTargetStoreId || null,

            sourceTransferId:
                effectiveSourceTransferId || null,

            saveAsDraft,

            requestKey:
                currentRequestKey,

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
                        row.querySelector(".unit-select")?.value ||
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

        notify(message, "error");
    }

    function notify(message, type = "success") {

        if (typeof window.toast === "function") {
            window.toast(message, type);
            return;
        }

        if (typeof Swal !== "undefined") {
            Swal.fire({
                icon:
                    type === "error" ? "error" : "success",
                title:
                    type === "error" ? "Không thể xử lý" : "Thành công",
                text:
                    message,
                confirmButtonText:
                    "Đã hiểu"
            });
            return;
        }

        const logger =
            type === "error"
                ? console.error
                : console.log;

        logger(message);
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
                        <small>Nhận hàng từ nhà cung cấp</small>
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
                        <small>Xuất bán hàng hoặc điều chỉnh</small>
                    </span>
                </button>

                <button type="button"
                        class="inventory-type-card inventory-type-transfer"
                        data-inventory-transfer>
                    <span class="inventory-type-card-icon">
                        <i class="fas fa-exchange-alt"></i>
                    </span>
                    <span>
                        <strong>Phiếu chuyển kho</strong>
                        <small>Chuyển nguyên liệu giữa các chi nhánh</small>
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

        return value === true
            || value === "true"
            || getUnitOptions(item).some(option => option.conversionFactorToBase > 0);
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

    function readRowConversionFactor(row, item) {

        const selectedUnit =
            getSelectedUnitOption(row, item);

        if (selectedUnit) {
            return selectedUnit.conversionFactorToBase;
        }

        return readConversionFactor(item);
    }

    function getUnitOptions(item) {

        const rawOptions =
            item?.unitOptions ?? item?.UnitOptions ?? [];

        if (Array.isArray(rawOptions) && rawOptions.length) {
            return rawOptions
                .map(option => ({
                    unitId: readInt(option.unitId ?? option.UnitId),
                    unitName: option.unitName ?? option.UnitName ?? "",
                    unitCode: option.unitCode ?? option.UnitCode ?? "",
                    conversionFactorToBase: readNumber(option.conversionFactorToBase ?? option.ConversionFactorToBase),
                    isBaseUnit: option.isBaseUnit ?? option.IsBaseUnit ?? false
                }))
                .filter(option => option.unitId > 0);
        }

        return [{
            unitId: readInt(item?.unitId ?? item?.UnitId),
            unitName: item?.unitName ?? item?.UnitName ?? "",
            unitCode: item?.unitCode ?? item?.UnitCode ?? "",
            conversionFactorToBase: readConversionFactor(item),
            isBaseUnit: readInt(item?.unitId ?? item?.UnitId) === readInt(item?.baseUnitId ?? item?.BaseUnitId)
        }].filter(option => option.unitId > 0);
    }

    function getSelectedUnitOption(row, item) {

        const unitId =
            readInt(row?.querySelector(".unit-select")?.value)
            || readInt(row?.querySelector(".unit-id")?.value)
            || readInt(item?.unitId ?? item?.UnitId);

        return getUnitOptions(item)
            .find(option => option.unitId === unitId)
            || getUnitOptions(item)[0]
            || null;
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

    function getSelectedUnitLabel(row, item) {

        const option =
            getSelectedUnitOption(row, item);

        return option?.unitCode
            || option?.unitName
            || getUnitLabel(item);
    }

    function getSuggestedUnitPrice(item, row) {

        const baseUnitCost =
            readNumber(item?.suggestedBaseUnitCost ?? item?.SuggestedBaseUnitCost);

        const conversionFactor =
            readRowConversionFactor(row, item);

        if (baseUnitCost > 0 && conversionFactor > 0) {
            return baseUnitCost * conversionFactor;
        }

        return readNumber(item?.suggestedUnitPrice ?? item?.SuggestedUnitPrice ?? item?.currentPrice ?? item?.CurrentPrice);
    }

    function getAvailableBaseQuantity(item) {

        return readNumber(item?.availableBaseQuantity ?? item?.AvailableBaseQuantity);
    }

    function isQuantityLocked(item) {

        const value =
            item?.isQuantityLocked ?? item?.IsQuantityLocked;

        return value === true || value === "true";
    }

    function isPriceLocked(item) {

        const value =
            item?.isPriceLocked ?? item?.IsPriceLocked;

        return value === true || value === "true";
    }

    function buildConversionPreview(item, quantity, row) {

        if (!item) {
            return "Quy đổi: -";
        }

        const conversionFactor =
            row
                ? readRowConversionFactor(row, item)
                : readConversionFactor(item);

        if (conversionFactor <= 0) {
            return "Chưa cấu hình quy đổi";
        }

        const baseQuantity =
            quantity * conversionFactor;

        const unitLabel =
            row
                ? getSelectedUnitLabel(row, item)
                : getUnitLabel(item);

        return `Quy đổi: ${formatQuantity(quantity)} ${unitLabel} = ${formatQuantity(baseQuantity)} ${getBaseUnitLabel(item)}`;
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

                return item && readRowConversionFactor(row, item) <= 0;
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

        if (value === null || value === undefined || value === "") {
            return 0;
        }

        const normalized =
            String(value)
                .trim()
                .replace(/\s/g, "")
                .replace(",", ".");

        const number =
            Number(normalized);

        return Number.isFinite(number)
            ? number
            : 0;
    }

    function getCurrentDocumentType() {

        return readInt(
            document.querySelector(selector.type)?.value
        );
    }

    function getCurrentPurpose() {

        return readInt(
            document.querySelector('input[name="Purpose"]:checked')?.value
        );
    }

    function isImportPurchase() {

        return getCurrentDocumentType() === documentType.import
            && getCurrentPurpose() === documentPurpose.importPurchase;
    }

    function isImportAdjustment() {

        return getCurrentDocumentType() === documentType.import
            && getCurrentPurpose() === documentPurpose.importAdjustment;
    }

    function isExportSale() {

        return getCurrentDocumentType() === documentType.export
            && getCurrentPurpose() === documentPurpose.sale;
    }

    function isAdjustmentOut() {

        return getCurrentDocumentType() === documentType.export
            && getCurrentPurpose() === documentPurpose.adjustmentOut;
    }

    function shouldShowPartnerField() {

        return isExportSale();
    }

    function isManualPricePurpose() {

        return isImportAdjustment();
    }

    function usesActiveIngredientSource() {

        const type =
            getCurrentDocumentType();

        return type !== documentType.import;
    }

    function usesStoreInventorySource() {

        const type =
            getCurrentDocumentType();

        return type === documentType.export
            || type === documentType.waste;
    }

    function isInternalImport() {

        return getCurrentDocumentType() === documentType.import
            && getCurrentPurpose() === documentPurpose.importInternal;
    }

    function isInternalOut() {

        return getCurrentDocumentType() === documentType.export
            && readInt(document.querySelector('input[name="Purpose"]:checked')?.value) === documentPurpose.internalOut;
    }

    function createRequestKey() {

        if (window.crypto?.randomUUID) {
            return window.crypto.randomUUID();
        }

        return `${Date.now()}-${Math.random().toString(16).slice(2)}`;
    }

    function readQuantity(value) {

        const quantity =
            readNumber(value);

        if (!Number.isFinite(quantity) || quantity <= 0) {
            return 0;
        }

        return quantity;
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

    function roundQuantity(value) {

        const number =
            Number(value || 0);

        if (!Number.isFinite(number) || number <= 0) {
            return 0;
        }

        return Math.floor(number * 1000) / 1000;
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
