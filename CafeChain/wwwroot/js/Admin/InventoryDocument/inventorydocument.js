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

    // =====================================================
    // EVENTS
    // =====================================================

    function bindEvents() {

        document.addEventListener(
            "click",
            e => {

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