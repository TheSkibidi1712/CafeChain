import { initImport, addImportRow } from "./inventory-import.js";
import { addExportRow } from "./inventory-export.js";
import { addStockTakeRow, loadStockTakeTable } from "./inventory-stocktake.js";
import { addWasteRow } from "./inventory-cancel.js";

const TYPE = {
    IMPORT: "1",
    EXPORT: "2",
    STOCKTAKE: "3",
    WASTE: "4"
};

// ================= FILTER =================
function reloadData() {
    const keyword = document.getElementById("search").value;
    const type = document.getElementById("filterType").value;
    const fromDate = document.getElementById("fromDate").value;
    const toDate = document.getElementById("toDate").value;

    const url = `/Admin/AdminInventoryDocument?keyword=${keyword}&type=${type}&fromDate=${fromDate}&toDate=${toDate}`;
    window.location.href = url;
}

function clearFilter() {
    document.getElementById("search").value = "";
    document.getElementById("filterType").value = "";
    document.getElementById("fromDate").value = "";
    document.getElementById("toDate").value = "";
    reloadData();
}

// ================= UTIL =================
function formatVND(n) {
    return Number(n || 0).toLocaleString("vi-VN");
}

function bindSelect(id, data, value, text, placeholder) {
    const el = document.getElementById(id);

    el.innerHTML =
        `<option value="">${placeholder}</option>` +
        data.map(x => `<option value="${x[value]}">${x[text]}</option>`).join("");
}

// ================= DETAIL =================
function openDetail(id) {
    fetch(`/api/admin/inventory-documents/${id}`)
        .then(r => r.json())
        .then(res => {
            if (!res.success) {
                toast("Không tìm thấy dữ liệu", "error");
                return;
            }

            const d = res.data;

            let html = `
                <p><b>Mã:</b> ${d.code}</p>
                <p><b>Kho:</b> ${d.storeName}</p>
                <p><b>Nhân viên:</b> ${d.staffName}</p>
                <p><b>Ngày:</b> ${new Date(d.date).toLocaleDateString()}</p>
                <p><b>Ghi chú:</b> ${d.note || ""}</p>
                <hr/>
                <table class="table">
                    <thead>
                        <tr>
                            <th>Nguyên liệu</th>
                            <th>Số lượng</th>
                            <th>Đơn vị</th>
                            ${d.type == "IMPORT" ? "<th>Giá</th>" : ""}
                        </tr>
                    </thead>
                    <tbody>
            `;

            d.details.forEach(x => {
                html += `
                    <tr>
                        <td>${x.ingredientName}</td>
                        <td>${x.quantity}</td>
                        <td>${x.unitName}</td>
                        ${d.type == "IMPORT" ? `<td>${x.unitPrice || 0}</td>` : ""}
                    </tr>
                `;
            });

            html += `</tbody></table>`;

            document.getElementById("detailContent").innerHTML = html;
            document.getElementById("detailModal").style.display = "block";
        });
}

// ================= CREATE =================
function openCreateModal() {
    fetch("/api/admin/inventory-documents/create-data")
        .then(r => r.json())
        .then(data => {

            bindSelect("store", data.stores, "storeId", "name", "-- Chọn Kho --");
            bindSelect("supplier", data.suppliers, "supplierId", "name", "-- Chọn Nhà Cung Cấp --");

            document.getElementById("documentDate").value = new Date().toISOString().slice(0, 16);

            document.querySelector("#detailTable tbody").innerHTML = "";
            document.getElementById("grandTotal").innerText = "0";
            document.querySelector(".total-box").classList.remove("hidden");
            document.getElementById("createModal").style.display = "block";

            // 🔥 Khi đổi kho → reload kiểm kê
            document.getElementById("store").onchange = () => {
                if (document.getElementById("type").value === TYPE.STOCKTAKE) {
                    loadStockTakeTable();
                }
            };
        });
}

function onTypeChange() {
    const type = document.getElementById("type").value;

    const supplierBox = document.getElementById("supplierBox");
    const exportPartner = document.getElementById("exportPartner");
    const exportPurpose = document.getElementById("exportPurpose");
    const wastePurpose = document.getElementById("wastePurpose");

    const table = document.getElementById("detailTable");
    const thead = document.querySelector("#detailTable thead tr");
    const addBtn = document.querySelector(".btn-add");
    const totalBox = document.querySelector(".total-box");

    document.querySelector("#detailTable tbody").innerHTML = "";
    document.getElementById("grandTotal").innerText = "0";

    // HEADER
    if (type === TYPE.STOCKTAKE) {
        thead.innerHTML = `
            <th>Nguyên liệu</th>
            <th>Đơn vị</th>
            <th>Tồn hệ thống</th>
            <th>Thực tế</th>
            <th>Chênh lệch</th>
            <th>Ghi chú</th>
            <th></th>
        `;
    }
    else if (type === TYPE.WASTE) {
        thead.innerHTML = `
            <th>Nguyên liệu</th>
            <th>Đơn vị</th>
            <th>Số lượng</th>
            <th>Ghi chú</th>
            <th></th>
        `;
    }
    else {
        thead.innerHTML = `
            <th>Nguyên liệu</th>
            <th>Đơn vị</th>
            <th>Số lượng</th>
            <th class="col-price">Giá</th>
            <th class="col-total">Thành tiền</th>
            <th>Ghi chú</th>
            <th></th>
        `;
    }

    // RESET UI
    supplierBox.style.display = "none";
    exportPartner.style.display = "none";
    exportPurpose.style.display = "none";
    wastePurpose.style.display = "none";

    table.classList.remove("hide-price");

    switch (type) {
        case TYPE.IMPORT:
            supplierBox.style.display = "block";
            addBtn.style.display = "inline-block";
            break;

        case TYPE.EXPORT:
            exportPartner.style.display = "block";
            exportPurpose.style.display = "block";
            table.classList.add("hide-price");
            addBtn.style.display = "inline-block";
            break;

        case TYPE.STOCKTAKE:
            addBtn.style.display = "none";

            const storeId = document.getElementById("store").value;
            if (storeId) {
                loadStockTakeTable();
            }
            break;

        case TYPE.WASTE:
            table.classList.add("hide-price");
            addBtn.style.display = "inline-block";
            wastePurpose.style.display = "block";
            break;
    }

    if (type === TYPE.STOCKTAKE || type === TYPE.WASTE || type === TYPE.EXPORT) {
        totalBox.classList.add("hidden");
    } else {
        totalBox.classList.remove("hidden");
    }
}

// ================= TABS =================
function switchTab(evt, tabId) {
    document.querySelectorAll(".tab-content").forEach(t => t.classList.remove("active"));
    document.querySelectorAll(".tab-btn").forEach(t => t.classList.remove("active"));

    document.getElementById(tabId).classList.add("active");
    evt.currentTarget.classList.add("active");
}

// ================= CREATE TYPE =================
function openCreateModalWithType(type) {
    openCreateModal();

    setTimeout(() => {
        const typeInput = document.getElementById("type");
        const typeText = document.getElementById("typeText");

        typeInput.value = type.toString();

        if (type == 1) typeText.value = "Nhập kho";
        if (type == 2) typeText.value = "Xuất kho";
        if (type == 3) typeText.value = "Kiểm kê";
        if (type == 4) typeText.value = "Hủy kho";

        onTypeChange();
    }, 200);
}

// ================= ROW =================
function addRow() {
    const type = document.getElementById("type").value;

    if (type === TYPE.IMPORT) {
        addImportRow();
    }
    else if (type === TYPE.EXPORT) {
        addExportRow();
    }
    else if (type === TYPE.STOCKTAKE) {
        loadStockTakeTable();
    }
    else if (type === TYPE.WASTE) {
        addWasteRow();
    }
}

// ================= CALC =================
function calcRow(tr) {
    const type = document.getElementById("type").value;

    if (type !== TYPE.IMPORT) return;

    const qty = parseFloat(tr.querySelector(".qty").value || 0);
    const price = parseFloat(tr.querySelector(".price").value || 0);

    const total = qty * price;
    tr.querySelector(".rowTotal").innerText = formatVND(total);

    calcTotal();
}

function calcTotal() {
    const type = document.getElementById("type").value;

    if (type !== TYPE.IMPORT) {
        document.getElementById("grandTotal").innerText = "0";
        return;
    }

    let sum = 0;

    document.querySelectorAll(".rowTotal").forEach(x => {
        const raw = x.innerText.replace(/\./g, "");
        sum += parseFloat(raw || 0);
    });

    document.getElementById("grandTotal").innerText = formatVND(sum);
}

// ================= SUBMIT =================
function submitForm() {
    const rows = document.querySelectorAll("#detailTable tbody tr");

    if (rows.length === 0) {
        toast("Phải có ít nhất 1 dòng", "error");
        return;
    }

    const model = {
        storeId: +document.getElementById("store").value,
        supplierId: document.getElementById("type").value === TYPE.IMPORT
            ? (document.getElementById("supplier").value || null)
            : null,
        type: +document.getElementById("type").value,
        purpose: document.getElementById("type").value === TYPE.WASTE
            ? +document.getElementById("wasteReason").value
            : null,
        documentDate: new Date(document.getElementById("documentDate").value).toISOString(),
        note: document.getElementById("note").value,

        details: []
    };

    rows.forEach(tr => {
        const type = document.getElementById("type").value;

        if (type === TYPE.STOCKTAKE) {
            model.details.push({
                ingredientId: +tr.querySelector(".ingredient").value,
                unitId: +tr.querySelector(".unit").value, // 🔥 FIX
                quantity: +tr.querySelector(".realQty").value,
                unitPrice: null,
                note: tr.querySelector(".note").value
            });
        } else {
            model.details.push({
                ingredientId: +tr.querySelector(".ingredient").value,
                unitId: +tr.querySelector(".unit").value,
                quantity: +tr.querySelector(".qty").value,
                unitPrice: type === TYPE.IMPORT
                    ? +tr.querySelector(".price").value
                    : null,
                note: tr.querySelector(".note").value
            });
        }
    });

    fetch("/api/admin/inventory-documents", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(model)
    })
        .then(r => r.json())
        .then(res => {
            if (res.success) {
                toast("Tạo thành công", "success");
                location.reload();
            } else {
                toast(res.message, "error");
            }
        });
}

// ================= GLOBAL =================
window.reloadData = reloadData;
window.clearFilter = clearFilter;
window.openDetail = openDetail;
window.openCreateModal = openCreateModal;
window.onTypeChange = onTypeChange;
window.addRow = addRow;
window.submitForm = submitForm;
window.calcRow = calcRow;
window.calcTotal = calcTotal;
window.switchTab = switchTab;
window.openCreateModalWithType = openCreateModalWithType;

// ================= INIT =================
window.onload = () => {
    initImport();
};

function closeModal(id) {
    document.getElementById(id).style.display = "none";
}

document.addEventListener("DOMContentLoaded", () => {
    const btn = document.getElementById("closeCreateModal");
    if (btn) {
        btn.addEventListener("click", () => {
            closeModal("createModal");
        });
    }
});

window.addEventListener("click", function (e) {
    document.querySelectorAll(".modal").forEach(m => {
        if (e.target === m) {
            m.style.display = "none";
        }
    });
});

window.closeModal = closeModal;

