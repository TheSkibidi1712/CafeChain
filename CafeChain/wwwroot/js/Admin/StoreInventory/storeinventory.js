let currentStoreId = 0;

// =====================================================
// OPEN MODAL
// =====================================================

function openTransactionModal(storeId) {
    currentStoreId = Number(storeId || 0);

    const modal = document.getElementById("transactionModal");
    const content = document.getElementById("transactionContent");

    if (!modal || !content) return;

    modal.style.display = "block";
    content.innerHTML = "Đang tải dữ liệu...";

    loadTransactionPage(1, currentStoreId);
}

// =====================================================
// LOAD PAGE
// =====================================================

function loadTransactionPage(page, storeId) {
    currentStoreId = Number(storeId ?? currentStoreId ?? 0);

    const content = document.getElementById("transactionContent");

    if (!content) return;

    content.innerHTML = "Đang tải dữ liệu...";

    const query = new URLSearchParams({
        page: Number(page || 1),
        storeId: currentStoreId
    });

    fetch(`/Admin/AdminStoreInventory/Transactions?${query.toString()}`, {
        method: "GET",
        headers: {
            "X-Requested-With": "XMLHttpRequest"
        }
    })
        .then(response => {
            if (!response.ok) {
                throw new Error("Load failed");
            }

            return response.text();
        })
        .then(html => {
            content.innerHTML = html;
        })
        .catch(() => {
            content.innerHTML =
                "<div class='empty-data'>Lỗi tải dữ liệu hoặc bạn không có quyền xem kho này.</div>";
        });
}

// =====================================================
// CLOSE MODAL
// =====================================================

function closeModal() {
    const modal = document.getElementById("transactionModal");

    if (!modal) return;

    modal.style.display = "none";
}

// =====================================================
// CLICK OUTSIDE / ESC
// =====================================================

window.addEventListener("click", function (event) {
    const modal = document.getElementById("transactionModal");

    if (!modal) return;

    if (event.target === modal) {
        closeModal();
    }
});

window.addEventListener("keydown", function (event) {
    if (event.key === "Escape") {
        closeModal();
    }
});
