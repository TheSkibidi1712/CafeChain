// =====================================================
// INVENTORY DOCUMENT MODULE
// =====================================================

const InventoryDocument = (() => {

    const selectors = {
        modal: "#inventoryDetailModal",
        content: "#inventoryDetailContent",
        filterForm: "#filterForm",

        exportModal: "#exportModal",
        exportDocumentId: "#exportDocumentId",
        exportButton: "#btnConfirmExport"
    };

    const createSelector = {

        modal: "#inventoryCreateModal",

        content: "#inventoryCreateContent"

    };

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

        form.submit();
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
            new bootstrap.Modal(
                document.querySelector(selectors.modal)
            );

        modal.show();

        try {

            const response =
                await fetch(
                    `/Admin/AdminInventoryDocument/DetailModal?documentId=${id}`
                );

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
            new bootstrap.Modal(
                document.querySelector(selectors.modal)
            );

        modal.show();

        try {

            const response =
                await fetch(
                    `/Admin/AdminInventoryDocument/DetailModal?documentId=${id}`
                );

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

    async function postDocumentAction(url, documentId) {

        const response =
            await fetch(
                `${url}?documentId=${encodeURIComponent(documentId)}`,
                {
                    method: "POST",
                    headers: {
                        "X-Requested-With": "XMLHttpRequest"
                    }
                });

        if (!response.ok) {

            const message =
                await readActionResponseMessage(response);

            throw new Error(message || "Không thể xử lý phiếu.");
        }

        return response.json();
    }

    async function readActionResponseMessage(response) {

        const contentType =
            response.headers.get("content-type") || "";

        if (contentType.includes("application/json")) {

            const json =
                await response.json();

            return json.message || json.error || "Không thể xử lý phiếu.";
        }

        return await response.text();
    }

    async function confirmDraft(documentId, button) {

        setActionBusy(button, true);

        try {

            const result =
                await postDocumentAction(
                "/Admin/AdminInventoryDocument/ConfirmDraft",
                documentId);

            await showStockWarnings(
                result.warnings || result.Warnings || []);

            window.location.reload();
        }
        catch (error) {

            alert(error.message || "Không thể xác nhận phiếu.");

            setActionBusy(button, false);
        }
    }

    async function cancelDraft(documentId, button) {

        if (!confirm("Bạn chắc chắn muốn hủy phiếu này?")) {

            return;
        }

        setActionBusy(button, true);

        try {

            await postDocumentAction(
                "/Admin/AdminInventoryDocument/CancelInventoryDocument",
                documentId);

            window.location.reload();
        }
        catch (error) {

            alert(error.message || "Không thể hủy phiếu.");

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
    function openExportModal(documentId) {

        document.querySelector(selectors.exportDocumentId).value = documentId;

        const modal = new bootstrap.Modal(document.querySelector(selectors.exportModal));

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
                    "/Admin/AdminInventoryDocument/ExportFile",
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
