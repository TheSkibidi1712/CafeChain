let currentInventoryId = 0;

// OPEN MODAL
function openTransactionModal() {
    const modal = document.getElementById("transactionModal");
    const content = document.getElementById("transactionContent");

    modal.style.display = "block";
    content.innerHTML = "Đang tải...";

    loadTransactionPage(1);
}

// LOAD PAGE (AJAX)
function loadTransactionPage(page) {
    fetch(`/Admin/AdminStoreInventory/Transactions?page=${page}`)
        .then(res => res.text())
        .then(html => {
            document.getElementById("transactionContent").innerHTML = html;
        })
        .catch(() => {
            document.getElementById("transactionContent").innerHTML = "Lỗi tải dữ liệu";
        });
}

// CLOSE MODAL
function closeModal() {
    document.getElementById("transactionModal").style.display = "none";
}

// CLICK OUTSIDE
window.onclick = function (event) {
    let modal = document.getElementById("transactionModal");
    if (event.target === modal) {
        modal.style.display = "none";
    }
};