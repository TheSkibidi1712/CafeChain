let currentStoreId = 0;
let currentTransactionPage = 1;
let transactionRequestController = null;

function renderTransactionState(message, options = {}) {
    const content = document.getElementById("transactionContent");
    if (!content) return;

    const state = document.createElement("div");
    state.className = `transaction-state ${options.kind || ""}`.trim();
    state.setAttribute(
        "role",
        options.kind === "error" || options.kind === "forbidden" ? "alert" : "status"
    );
    state.textContent = message;

    if (options.retry) {
        const retry = document.createElement("button");
        retry.type = "button";
        retry.className = "transaction-retry";
        retry.textContent = "Thử lại";
        retry.addEventListener("click", function () {
            loadTransactionPage(currentTransactionPage, currentStoreId);
        });
        state.appendChild(retry);
    }

    content.replaceChildren(state);
}

// =====================================================
// OPEN MODAL
// =====================================================

function openTransactionModal(storeId) {
    const selectedStoreId = Number(storeId);
    currentStoreId = Number.isInteger(selectedStoreId) && selectedStoreId > 0
        ? selectedStoreId
        : 0;
    currentTransactionPage = 1;

    const modal = document.getElementById("transactionModal");
    const content = document.getElementById("transactionContent");

    if (!modal || !content) return;

    modal.style.display = "block";
    renderTransactionState("Đang tải lịch sử tồn kho…");

    loadTransactionPage(1, currentStoreId);
}

// =====================================================
// LOAD PAGE
// =====================================================

function loadTransactionPage(page, storeId) {
    const requestedStoreId = Number(storeId ?? currentStoreId);
    if (Number.isInteger(requestedStoreId) && requestedStoreId > 0) {
        currentStoreId = requestedStoreId;
    }
    currentTransactionPage = Math.max(1, Number(page) || 1);

    const content = document.getElementById("transactionContent");
    if (!content) return;

    transactionRequestController?.abort();
    transactionRequestController = new AbortController();
    const requestController = transactionRequestController;

    renderTransactionState("Đang tải lịch sử tồn kho…");

    const query = new URLSearchParams({
        page: currentTransactionPage,
        storeId: currentStoreId
    });

    fetch(`/Admin/AdminStoreInventory/Transactions?${query.toString()}`, {
        method: "GET",
        headers: {
            "X-Requested-With": "XMLHttpRequest"
        },
        signal: requestController.signal
    })
        .then(response => {
            if (response.status === 403) {
                throw new Error("FORBIDDEN");
            }

            if (!response.ok) {
                throw new Error(`HTTP_${response.status}`);
            }

            return response.text();
        })
        .then(html => {
            if (requestController.signal.aborted) return;
            content.innerHTML = html;
        })
        .catch(error => {
            if (error.name === "AbortError") return;

            if (error.message === "FORBIDDEN") {
                renderTransactionState(
                    "Bạn không có quyền xem lịch sử tồn kho của chi nhánh này.",
                    { kind: "forbidden" }
                );
                return;
            }

            renderTransactionState(
                "Không thể tải lịch sử tồn kho. Vui lòng thử lại.",
                { kind: "error", retry: true }
            );
        })
        .finally(() => {
            if (transactionRequestController === requestController) {
                transactionRequestController = null;
            }
        });
}

// =====================================================
// CLOSE MODAL
// =====================================================

function closeModal() {
    const modal = document.getElementById("transactionModal");
    if (!modal) return;

    transactionRequestController?.abort();
    transactionRequestController = null;
    modal.style.display = "none";
}

// =====================================================
// CLICK OUTSIDE / ESC
// =====================================================

window.addEventListener("click", function (event) {
    const modal = document.getElementById("transactionModal");
    if (modal && event.target === modal) {
        closeModal();
    }
});

window.addEventListener("keydown", function (event) {
    if (event.key === "Escape") {
        closeModal();
    }
});
