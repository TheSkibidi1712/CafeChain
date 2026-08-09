// =====================================================
// INVENTORY DOCUMENT MODULE
// =====================================================

const InventoryDocument = (() => {

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

    const selectors = {
        modal: "#inventoryDetailModal",
        content: "#inventoryDetailContent",
        filterForm: "#filterForm",

        exportModal: "#exportModal",
        exportDocumentId: "#exportDocumentId",
        exportButton: "#btnConfirmExport",
        excelExportButton: "#btnExportExcel"
    };

    const createSelector = {

        modal: "#inventoryCreateModal",

        content: "#inventoryCreateContent"

    };

    function getEndpoint(name) {
        const endpoint = document.querySelector("#inventoryDocumentPage")?.dataset[name];
        if (!endpoint) {
            throw new Error(`Thiếu cấu hình endpoint: ${name}.`);
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

    // =====================================================
    // PAGINATION
    // =====================================================

    function goToPage(page) {

        const form =
            document.querySelector(selectors.filterForm);

        if (!form) return;

        let pageInput =
            form.querySelector('input[name="Page"]');

        if (!pageInput) {

            pageInput =
                document.createElement("input");

            pageInput.type = "hidden";
            pageInput.name = "Page";

            form.appendChild(pageInput);
        }

        pageInput.value = page;

        if (!syncDateFilterInputs()) {
            return;
        }

        form.submit();
    }

    // =====================================================
    // FILTER DATE / TYPE / PURPOSE
    // =====================================================

    function initDateFilters() {

        const form =
            document.querySelector(selectors.filterForm);

        if (!form) {
            return;
        }

        const fromDate = form.querySelector('[name="Filter.FromDate"]');
        const toDate = form.querySelector('[name="Filter.ToDate"]');
        const typeSelect = form.querySelector("#inventoryFilterType");

        const syncDateBounds = () => {
            if (toDate) toDate.min = fromDate?.value || "";
            if (fromDate) fromDate.max = toDate?.value || "";
            fromDate?.setCustomValidity("");
            toDate?.setCustomValidity("");
        };

        fromDate?.addEventListener("change", syncDateBounds);
        toDate?.addEventListener("change", syncDateBounds);
        typeSelect?.addEventListener("change", syncPurposeFilter);

        syncDateBounds();
        syncPurposeFilter();

        form.addEventListener(
            "submit",
            event => {
                if (!syncDateFilterInputs()) {
                    event.preventDefault();
                }
            });
    }

    function syncDateFilterInputs() {

        const form = document.querySelector(selectors.filterForm);
        if (!form) return true;

        const fromDate = form.querySelector('[name="Filter.FromDate"]');
        const toDate = form.querySelector('[name="Filter.ToDate"]');
        fromDate?.setCustomValidity("");
        toDate?.setCustomValidity("");

        if (fromDate?.value && toDate?.value && fromDate.value > toDate.value) {
            const message = '"Từ ngày" không được lớn hơn "Đến ngày".';
            fromDate.setCustomValidity(message);
            fromDate.reportValidity();
            return false;
        }

        return true;
    }

    function syncPurposeFilter() {

        const form = document.querySelector(selectors.filterForm);
        const typeSelect = form?.querySelector("#inventoryFilterType");
        const purposeSelect = form?.querySelector("#inventoryFilterPurpose");
        if (!typeSelect || !purposeSelect) return;

        const selectedType = typeSelect.value;
        const currentPurpose = purposeSelect.value;
        let currentPurposeStillValid = !currentPurpose;

        Array.from(purposeSelect.options).forEach(option => {
            if (!option.value) {
                option.textContent = selectedType ? "Mục đích" : "Chọn loại phiếu trước";
                option.hidden = false;
                return;
            }

            const isVisible = option.dataset.documentType === selectedType;
            option.hidden = !isVisible;
            option.disabled = !isVisible;
            if (isVisible && option.value === currentPurpose) {
                currentPurposeStillValid = true;
            }
        });

        purposeSelect.disabled = !selectedType;
        if (!currentPurposeStillValid) {
            purposeSelect.value = "";
        }
    }

    // =====================================================
    // DETAIL MODAL
    // =====================================================

    async function openDetail(id) {

        const container =
            document.querySelector(selectors.content);

        container.innerHTML = `
            <div class="p-5 text-center">
                <div class="spinner-border"></div>
            </div>
        `;

        const modal =
            bootstrap.Modal.getOrCreateInstance(
                document.querySelector(selectors.modal)
            );

        modal.show();

        try {

            const response =
                await fetch(appendQuery(getEndpoint("detailUrl"), { documentId: id }));

            const html =
                await response.text();

            container.innerHTML = html;
            calculateSummary();
        }
        catch {

            container.innerHTML = `
                <div class="alert alert-danger">
                    Không tải được dữ liệu.
                </div>
            `;
        }
    }

    async function openDraftPreview(id) {

        const container =
            document.querySelector(selectors.content);

        container.innerHTML = `
            <div class="p-5 text-center">
                <div class="spinner-border"></div>
            </div>
        `;

        const modal =
            bootstrap.Modal.getOrCreateInstance(
                document.querySelector(selectors.modal)
            );

        modal.show();

        try {

            const response =
                await fetch(appendQuery(getEndpoint("detailUrl"), { documentId: id }));

            if (!response.ok) {

                throw new Error("Không tải được dữ liệu.");
            }

            const html =
                await response.text();

            container.innerHTML =
                html + renderDraftPreviewActions(id);

            calculateSummary();
        }
        catch (error) {

            container.innerHTML = `
                <div class="alert alert-danger">
                    ${error.message || "Không tải được dữ liệu."}
                </div>
            `;
        }
    }

    function renderDraftPreviewActions(id) {

        return `
            <div class="modal-footer draft-preview-actions"
                 data-document-id="${id}">

                <button type="button"
                        class="btn btn-outline-secondary"
                        data-bs-dismiss="modal">
                    Đóng
                </button>

                <button type="button"
                        class="btn btn-outline-danger btn-cancel-draft-final"
                        data-id="${id}">
                    <i class="fas fa-ban"></i>
                    Hủy phiếu
                </button>

                <button type="button"
                        class="btn btn-primary btn-confirm-draft-final"
                        data-id="${id}">
                    <i class="fas fa-check"></i>
                    Xác nhận phiếu
                </button>

            </div>
        `;
    }

    function buildAdminUrl(action, params = {}) {
        const endpointName = action === "ConfirmDraft"
            ? "confirmDraftUrl"
            : action === "CancelInventoryDocument"
                ? "cancelUrl"
                : null;

        if (!endpointName) {
            throw new Error(`Action phiếu kho không được hỗ trợ: ${action}.`);
        }

        return appendQuery(getEndpoint(endpointName), params);
    }

    async function postDocumentAction(action, documentId, requestKey) {

        const id =
            Number(documentId || 0);

        if (!Number.isInteger(id) || id <= 0) {
            throw new Error("Mã phiếu không hợp lệ.");
        }

        const requestUrl =
            buildAdminUrl(
                action,
                {
                    documentId: id,
                    requestKey: requestKey || ""
                });

        const response =
            await fetch(
                requestUrl,
                {
                    method: "POST",
                    headers: {
                        "X-Requested-With": "XMLHttpRequest"
                    }
                });

        if (!response.ok) {

            const message =
                await readActionResponseMessage(response);

            console.warn(
                "Inventory document action failed",
                {
                    action,
                    url: requestUrl,
                    documentId: id,
                    requestKey,
                    status: response.status,
                    message,
                    traceId: response._inventoryTraceId
                });

            throw new Error(message || "Không thể xử lý phiếu.");
        }

        const result =
            await readActionResponse(response);

        if (result && result.success === false) {
            throw new Error(
                result.message ||
                result.error ||
                "Không thể xử lý phiếu.");
        }

        return result || {};
    }

    async function readActionResponseMessage(response) {

        const fallback =
            response.status === 404
                ? "Không tìm thấy phiếu cần xử lý."
                : "Không thể xử lý phiếu.";

        try {
            const data =
                await readActionResponse(response);

            if (typeof data === "string") {
                return data || fallback;
            }

            response._inventoryTraceId =
                data?.traceId ||
                data?.TraceId ||
                "";

            return data?.message ||
                data?.error ||
                data?.title ||
                fallback;
        }
        catch {
            return fallback;
        }
    }

    async function readActionResponse(response) {

        const contentType =
            response.headers.get("content-type") || "";

        if (contentType.includes("application/json")) {
            return await response.json();
        }

        const text =
            await response.text();

        if (text && (text.includes("<!DOCTYPE") || text.includes("<html") || text.includes("<body"))) {
            try {
                const doc = new DOMParser().parseFromString(text, "text/html");
                const titleNode = doc.querySelector("title");
                const cleanTitle = titleNode ? titleNode.textContent.replace(/- CafeChain/gi, "").trim() : "";
                if (cleanTitle && cleanTitle.length < 80) return cleanTitle;
            } catch { }
            return response.status === 403 ? "Không có quyền truy cập thao tác này." : "Không thể xử lý yêu cầu.";
        }

        return text;
    }

    function showActionMessage(message, icon = "error") {

        const text =
            message || "Không thể xử lý phiếu.";

        if (window.Swal) {

            Swal.fire({
                icon,
                title: icon === "success" ? "Thành công" : "Không thể xử lý",
                text,
                confirmButtonText: "OK"
            });

            return;
        }

        if (window.toastr) {

            const method =
                icon === "success" ? "success" : "error";

            window.toastr[method](text);

            return;
        }

        alert(text);
    }

    async function confirmDraft(documentId, button) {

        try {
            const id =
                Number(documentId || 0);

            if (!Number.isInteger(id) || id <= 0) {
                throw new Error("Mã phiếu không hợp lệ.");
            }

            setActionBusy(button, true);
            const requestKey =
                getButtonRequestKey(button);

            const result =
                await postDocumentAction(
                "ConfirmDraft",
                id,
                requestKey);

            await showStockWarnings(
                result.warnings || result.Warnings || []);

            window.location.reload();
        }
        catch (error) {

            showActionMessage(
                error.message ||
                "Không thể xác nhận phiếu.");

            setActionBusy(button, false);
        }
    }

    async function cancelDraft(documentId, button) {

        if (!confirm("Bạn chắc chắn muốn hủy phiếu này?")) {

            return;
        }

        try {
            const id =
                Number(documentId || 0);

            if (!Number.isInteger(id) || id <= 0) {
                throw new Error("Mã phiếu không hợp lệ.");
            }

            setActionBusy(button, true);
            const requestKey =
                getButtonRequestKey(button);

            await postDocumentAction(
                "CancelInventoryDocument",
                id,
                requestKey);

            window.location.reload();
        }
        catch (error) {

            showActionMessage(
                error.message ||
                "Không thể hủy phiếu.");

            setActionBusy(button, false);
        }
    }

    function setActionBusy(button, isBusy) {

        if (!button) {

            return;
        }

        button.disabled = isBusy;

        button.classList.toggle(
            "is-loading",
            isBusy);
    }

    function getButtonRequestKey(button) {

        if (!button.dataset.requestKey) {
            button.dataset.requestKey =
                createRequestKey();
        }

        return button.dataset.requestKey;
    }

    function createRequestKey() {

        if (window.crypto?.randomUUID) {
            return window.crypto.randomUUID();
        }

        return `${Date.now()}-${Math.random().toString(16).slice(2)}`;
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

        if (window.Swal) {

            await Swal.fire({
                icon: "warning",
                title: "Nguyên liệu sắp hết",
                html,
                confirmButtonText: "OK"
            });

            return;
        }

        alert(
            warnings
                .map(item => item.message || item.Message || "")
                .join("\n")
        );
    }

    function escapeHtml(value) {

        const div =
            document.createElement("div");

        div.textContent =
            value;

        return div.innerHTML;
    }

    async function reviewNegativeApproval(button, approve) {
        const documentId = Number(button?.dataset.documentId || 0);
        const endpoint = button?.dataset.url;
        if (!Number.isInteger(documentId) || documentId <= 0 || !endpoint) {
            showActionMessage("Không xác định được yêu cầu phê duyệt.");
            return;
        }

        let reviewNote = null;
        if (window.Swal) {
            const dialogTarget = button.closest(selectors.modal) || document.body;
            const dialog = await Swal.fire({
                target: dialogTarget,
                returnFocus: false,
                icon: approve ? "question" : "warning",
                title: approve ? "Duyệt xuất âm?" : "Từ chối xuất âm?",
                text: approve
                    ? "Hệ thống sẽ kiểm tra lại tồn kho, phạm vi và policy trước khi xác nhận."
                    : "Phiếu sẽ chuyển sang trạng thái đã hủy.",
                input: "textarea",
                inputLabel: approve ? "Ghi chú duyệt (không bắt buộc)" : "Lý do từ chối",
                inputPlaceholder: approve ? "Nhập ghi chú nếu cần..." : "Nhập lý do từ chối...",
                showCancelButton: true,
                confirmButtonText: approve ? "Duyệt xuất âm" : "Từ chối",
                cancelButtonText: "Đóng",
                confirmButtonColor: approve ? "#198754" : "#dc3545",
                inputValidator: value => !approve && !String(value || "").trim()
                    ? "Lý do từ chối là bắt buộc."
                    : undefined,
                didOpen: popup => {
                    const input = popup.querySelector(".swal2-textarea");
                    if (input) {
                        window.requestAnimationFrame(() => input.focus({ preventScroll: true }));
                    }
                }
            });

            if (button.isConnected && !button.disabled) {
                button.focus({ preventScroll: true });
            }

            if (!dialog.isConfirmed) {
                return;
            }

            reviewNote = String(dialog.value || "").trim() || null;
        }
        else if (approve) {
            if (!confirm("Duyệt yêu cầu xuất âm này?")) {
                return;
            }
            reviewNote = prompt("Ghi chú duyệt (không bắt buộc):", "")?.trim() || null;
        }
        else {
            reviewNote = prompt("Nhập lý do từ chối:", "")?.trim() || null;
            if (!reviewNote) {
                showActionMessage("Lý do từ chối là bắt buộc.");
                return;
            }
        }

        setActionBusy(button, true);
        try {
            const response = await fetch(endpoint, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "X-Requested-With": "XMLHttpRequest"
                },
                body: JSON.stringify({ reviewNote })
            });

            if (!response.ok) {
                throw new Error(await readActionResponseMessage(response));
            }

            const result = await readActionResponse(response);
            if (result?.success === false) {
                throw new Error(result.message || "Không thể xử lý yêu cầu phê duyệt.");
            }

            showActionMessage(
                approve ? "Đã duyệt và xác nhận phiếu xuất âm." : "Đã từ chối yêu cầu xuất âm.",
                "success");
            window.setTimeout(() => window.location.reload(), 500);
        }
        catch (error) {
            showActionMessage(error.message || "Không thể xử lý yêu cầu phê duyệt.");
            setActionBusy(button, false);
        }
    }

    // =====================================================
    // EVENTS
    // =====================================================

    function bindEvents() {

        document.addEventListener(
            "click",
            e => {

                const createBtn =
                    e.target.closest(
                        "#btnCreateInventory"
                    );

                if (createBtn) {

                    e.preventDefault();

                    InventoryCreate.openTypeSelector();

                    return;

                }

                const excelExportBtn =
                    e.target.closest(
                        selectors.excelExportButton
                    );

                if (excelExportBtn) {

                    e.preventDefault();

                    exportExcelList(
                        excelExportBtn
                    );

                    return;

                }
                
                const detailBtn =
                    e.target.closest(
                        ".btn-detail"
                    );

                if (detailBtn) {

                    openDetail(
                        detailBtn.dataset.id
                    );

                    return;
                }

                const draftConfirmBtn =
                    e.target.closest(
                        ".btn-draft-confirm"
                    );

                if (draftConfirmBtn) {

                    openDraftPreview(
                        draftConfirmBtn.dataset.id
                    );

                    return;
                }

                const exportBtn =
                    e.target.closest(
                        ".btn-export"
                    );

                if (exportBtn) {

                    openExportModal(
                        exportBtn.dataset.id
                    );

                    return;
                }

                const pageBtn =
                    e.target.closest(
                        "[data-page]"
                    );

                if (pageBtn) {

                    goToPage(
                        pageBtn.dataset.page
                    );

                    return;
                }

                const confirmDraftBtn =
                    e.target.closest(
                        ".btn-confirm-draft-final"
                    );

                if (confirmDraftBtn) {

                    confirmDraft(
                        confirmDraftBtn.dataset.id,
                        confirmDraftBtn
                    );

                    return;
                }

                const cancelDraftBtn =
                    e.target.closest(
                        ".btn-cancel-draft-final"
                    );

                if (cancelDraftBtn) {

                    cancelDraft(
                        cancelDraftBtn.dataset.id,
                        cancelDraftBtn
                    );

                    return;
                }

                const approveNegativeBtn = e.target.closest(".btn-approve-negative");
                if (approveNegativeBtn) {
                    reviewNegativeApproval(approveNegativeBtn, true);
                    return;
                }

                const rejectNegativeBtn = e.target.closest(".btn-reject-negative");
                if (rejectNegativeBtn) {
                    reviewNegativeApproval(rejectNegativeBtn, false);
                }
            });

        const confirmBtn =
            document.querySelector(
                selectors.exportButton
            );

        if (confirmBtn) {

            confirmBtn.addEventListener(
                "click",
                exportFile
            );
        }
    }

    // =====================================================
    // CALC AND FORMAT FUNCTIONS (IF NEEDED)
    // =====================================================
    function formatCurrency(number) {

        return Number(number || 0)
            .toLocaleString(
                "vi-VN"
            );
    }

    function calculateSummary() {

        const summaryBox =
            document.querySelector(
                ".summary-box"
            );

        if (!summaryBox)
            return;

        let total =
            Number(
                summaryBox.dataset.total || 0
            );

        let vat =
            Number(
                summaryBox.dataset.vat || 0
            );

        let final =
            Number(
                summaryBox.dataset.final || 0
            );

        if (total <= 0) {

            document
                .querySelectorAll(
                    ".item-total"
                )
                .forEach(item => {

                    total += Number(
                        item.dataset.value || 0
                    );

                });

        }

        if (vat <= 0) {

            vat = 0;
        }

        if (final <= 0) {

            final = total + vat;
        }

        document.getElementById(
            "totalAmount"
        ).innerText =
            formatCurrency(total);

        document.getElementById(
            "vatAmount"
        ).innerText =
            formatCurrency(vat);

        document.getElementById(
            "finalAmount"
        ).innerText =
            formatCurrency(final);
    }

    function formatTableAmounts() {

        document
            .querySelectorAll(".amount-cell")
            .forEach(cell => {

                const amount =
                    Number(
                        cell.dataset.amount || 0
                    );
                const hasAmount =
                    cell.dataset.hasAmount === "true"
                    && amount > 0;

                if (!hasAmount) {

                    cell.innerHTML = `

                <span class="amount-placeholder">
                    —
                </span>

            `;

                    return;
                }

                cell.innerHTML = `

                <span class="amount-value">
                    ${formatCurrency(amount)}
                </span>

                <small class="amount-unit">
                    VNĐ
                </small>

            `;
            });
    }

    // ====================================================
    // EXPORT FUNCTION
    // ====================================================

    async function exportExcelList(button) {

        if (!syncDateFilterInputs()) {
            return;
        }

        const query =
            buildExcelExportQuery();

        const url = query
            ? `${getEndpoint("exportExcelUrl")}?${query}`
            : getEndpoint("exportExcelUrl");

        setActionBusy(
            button,
            true
        );

        try {

            const response =
                await fetch(
                    url,
                    {
                        method:
                            "GET",

                        headers: {
                            "X-Requested-With":
                                "XMLHttpRequest"
                        }
                    });

            if (!response.ok) {

                const message =
                    await response.text();

                throw new Error(
                    message || "Không thể xuất Excel."
                );
            }

            const contentType =
                response.headers.get("content-type") || "";

            if (!contentType.includes("spreadsheetml.sheet")) {

                const message =
                    await response.text();

                throw new Error(
                    message || "Phản hồi xuất Excel không hợp lệ."
                );
            }

            const blob =
                await response.blob();

            if (!blob.size) {

                throw new Error(
                    "File Excel trả về không có dữ liệu."
                );
            }

            downloadBlob(
                blob,
                getDownloadFileName(
                    response,
                    buildExcelFileName()
                )
            );
        }
        catch (error) {

            alert(
                error.message || "Không thể xuất Excel."
            );
        }
        finally {

            setActionBusy(
                button,
                false
            );
        }
    }

    function downloadBlob(blob, fileName) {

        const url =
            window.URL.createObjectURL(blob);

        const a =
            document.createElement("a");

        a.href =
            url;

        a.download =
            fileName;

        document.body.appendChild(a);

        a.click();

        a.remove();

        window.URL.revokeObjectURL(url);
    }

    function getDownloadFileName(response, fallback) {

        const disposition =
            response.headers.get("content-disposition") || "";

        const encodedFileName =
            disposition.match(/filename\*=UTF-8''([^;]+)/i);

        if (encodedFileName?.[1]) {

            return decodeURIComponent(
                encodedFileName[1].replace(/"/g, "")
            );
        }

        const fileName =
            disposition.match(/filename="?([^";]+)"?/i);

        if (fileName?.[1]) {

            return fileName[1];
        }

        return fallback;
    }

    function buildExcelFileName() {

        const now =
            new Date();

        const pad =
            value => String(value).padStart(2, "0");

        return `PhieuKho_${now.getFullYear()}${pad(now.getMonth() + 1)}${pad(now.getDate())}_${pad(now.getHours())}${pad(now.getMinutes())}.xlsx`;

    }

    function buildExcelExportQuery() {

        const form =
            document.querySelector(
                selectors.filterForm
            );

        if (!form) {

            return "";
        }

        const formData =
            new FormData(form);

        const params =
            new URLSearchParams();

        formData.forEach(
            (value, key) => {

                if (!value) {

                    return;
                }

                const normalizedKey =
                    key.startsWith("Filter.")
                        ? key.substring("Filter.".length)
                        : key;

                if (
                    normalizedKey === "Page"
                    || normalizedKey === "PageSize"
                ) {

                    return;
                }

                params.append(
                    normalizedKey,
                    value
                );
            });

        return params.toString();
    }

    function openExportModal(documentId) {

        const modalElement = document.querySelector(selectors.exportModal);
        if (modalElement && modalElement.parentNode !== document.body) {
            document.body.appendChild(modalElement);
        }

        document.querySelector(selectors.exportDocumentId).value = documentId;

        const modal = bootstrap.Modal.getOrCreateInstance(modalElement);

        modal.show();
    }

    async function exportFile() {

        const documentId =
            Number(
                document.querySelector(
                    selectors.exportDocumentId
                ).value
            );

        const exportType =
            Number(
                document.querySelector(
                    'input[name="exportType"]:checked'
                ).value
            );

        try {

            const response =
                await fetch(
                    getEndpoint("exportFileUrl"),
                    {
                        method: "POST",

                        headers: {
                            "Content-Type":
                                "application/json"
                        },

                        body: JSON.stringify({
                            documentId,
                            exportType
                        })
                    });

            if (!response.ok) {

                const message =
                    await response.text();

                alert(message);

                return;
            }

            const blob =
                await response.blob();

            const url =
                window.URL.createObjectURL(blob);

            const a =
                document.createElement("a");

            a.href = url;

            a.download =
                exportType === 1
                    ? `InventoryDocument_${documentId}.pdf`
                    : `InventoryDocument_${documentId}.docx`;

            document.body.appendChild(a);

            a.click();

            a.remove();

            window.URL.revokeObjectURL(url);

            bootstrap.Modal.getInstance(
                document.querySelector(
                    selectors.exportModal
                )
            )?.hide();

        }
        catch {

            alert(
                "Không thể xuất file."
            );
        }
    }

    // =====================================================
    // INIT
    // =====================================================

    function init() {

        bindEvents();
        initDateFilters();
        formatTableAmounts();
    }

    return {
        init,
        goToPage
    };

})();

document.addEventListener(
    "DOMContentLoaded",
    InventoryDocument.init
);
