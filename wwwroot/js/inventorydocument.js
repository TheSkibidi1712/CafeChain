import { initImport, addImportRow, getCurrentSupplier } from "./inventory-import.js";
import { addExportRow } from "./inventory-export.js";
import { addStockTakeRow, loadStockTakeTable } from "./inventory-stocktake.js";
import { addWasteRow } from "./inventory-cancel.js";

const TYPE = {
    IMPORT: "1",
    EXPORT: "2",
    WASTE: "3",        // ✅ đổi lại
    STOCKTAKE: "4"     // ✅ đổi lại
};

const PURPOSE = {
    NONE: 0,
    IMPORT_PURCHASE: 1,
    IMPORT_INTERNAL: 2,
    IMPORT_ADJUSTMENT: 3,

    SALE: 5,
    INTERNAL_OUT: 6,
    GIFT: 7,
    DEBT: 8,
    SAMPLE: 9,
    ADJUSTMENT_OUT: 10,

    STOCK_TAKE: 11,

    DAMAGED: 12,
    EXPIRED: 13,
    BROKEN: 14,
    CONTAMINATED: 15,
    LOST: 16
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

function parseMoney(str) {
    return parseFloat((str || "0").replace(/[^\d]/g, "")) || 0;
}

function bindSelect(id, data, value, text, placeholder) {
    const el = document.getElementById(id);

    el.innerHTML =
        `<option value="">${placeholder}</option>` +
        data.map(x => `<option value="${x[value]}">${x[text]}</option>`).join("");
}

// ================= DETAIL =================
async function openDetail(id) {
    try {
        const r = await fetch(`/api/admin/inventory-documents/${id}`);
        if (!r.ok) throw new Error();

        const res = await r.json();

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
                        ${d.type == "IMPORT" ? "<th>Giá</th><th>Thành tiền</th>" : ""}
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
                    ${d.type == "IMPORT" ? `
                        <td>${formatVND(x.unitPrice)}</td>
                        <td>${formatVND(x.totalAmount)}</td>
                    ` : ""}
                </tr>
            `;
        });

        html += `</tbody></table>`;

        document.getElementById("detailContent").innerHTML = html;
        document.getElementById("detailModal").style.display = "block";

    } catch {
        toast("Lỗi tải dữ liệu", "error");
    }
}

// ================= CREATE =================
async function openCreateModal() {
    try {
        const r = await fetch("/api/admin/inventory-documents/create-data");
        if (!r.ok) throw new Error();

        const data = await r.json();

        bindSelect("store", data.stores, "storeId", "name", "-- Chọn Kho --");
        bindSelect("supplier", data.suppliers, "supplierId", "name", "-- Chọn Nhà Cung Cấp --");

        document.getElementById("documentDate").value = new Date().toISOString().slice(0, 16);

        resetForm();

        document.getElementById("createModal").style.display = "block";

        document.getElementById("store").onchange = () => {
            if (document.getElementById("type").value === TYPE.STOCKTAKE) {
                loadStockTakeTable();
            }
        };

    } catch {
        toast("Không tải được dữ liệu", "error");
    }
}

function resetForm() {
    document.querySelector("#detailTable tbody").innerHTML = "";
    document.getElementById("grandTotal").innerText = "0";
}

// ================= TYPE =================
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

    resetForm();

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
        <th>Đơn vị (Base)</th>
        <th>Số lượng</th>
        <th>Tồn kho</th>
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
            loadStockTakeTable();
            break;

        case TYPE.WASTE:
            wastePurpose.style.display = "block";
            table.classList.add("hide-price");
            addBtn.style.display = "inline-block";
            break;
    }

    if (type !== TYPE.IMPORT) {
        totalBox.classList.add("hidden");
    } else {
        totalBox.classList.remove("hidden");
    }
}

// ================= ADD ROW =================
function addRow() {
    const type = document.getElementById("type").value;

    if (type === TYPE.IMPORT) addImportRow();
    else if (type === TYPE.EXPORT) addExportRow();
    else if (type === TYPE.STOCKTAKE) loadStockTakeTable();
    else if (type === TYPE.WASTE) addWasteRow();
}

// ================= SWITCH TAB =================
function switchTab(evt, tabId) {
    document.querySelectorAll(".tab-content").forEach(t => t.classList.remove("active"));
    document.querySelectorAll(".tab-btn").forEach(t => t.classList.remove("active"));

    document.getElementById(tabId).classList.add("active");
    evt.currentTarget.classList.add("active");
}

// ================= CREATE TYPE =================
async function openCreateModalWithType(type) {
    await openCreateModal();

    const typeInput = document.getElementById("type");
    const typeText = document.getElementById("typeText");

    typeInput.value = type.toString();

    typeText.value =
        type == 1 ? "Nhập kho" :
            type == 2 ? "Xuất kho" :
                type == 3 ? "Hủy kho" :   // ✅ đổi
                    "Kiểm kê";                // ✅ đổi

    onTypeChange();
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
    let sum = 0;

    document.querySelectorAll(".rowTotal").forEach(x => {
        sum += parseMoney(x.innerText);
    });

    document.getElementById("grandTotal").innerText = formatVND(sum);
}

// ================= 🔥 FIX SUBMIT =================
async function submitForm() {
    const rows = document.querySelectorAll("#detailTable tbody tr");

    if (rows.length === 0) {
        toast("Phải có ít nhất 1 dòng", "error");
        return;
    }
    if (!detail.ingredientId) {
        toast("Thiếu nguyên liệu", "error");
        return;
    }
    if (detail.quantity <= 0) {
        toast("Số lượng phải lớn hơn 0", "error");
        return;
    }
    if (!detail.unitId) {
        toast("Thiếu đơn vị", "error");
        return;
    }

    const type = Number(document.getElementById("type").value);

    let partnerType = null;
    let partnerId = null;
    let partnerName = null;

    // ================= PARTNER =================
    if (type === 1) {
        const supplier = getCurrentSupplier();

        if (!supplier || !supplier.id) {
            toast("Vui lòng chọn nhà cung cấp", "error");
            return;
        }

        partnerType = 1;
        partnerId = supplier.id;
        partnerName = supplier.name;
    }
    else if (type === 2) {
        partnerType = 2;
        partnerName = document.getElementById("partnerName").value || "Khách lẻ";
        partnerId = null;
    }
    else {
        partnerType = 0;
        partnerId = null;
        partnerName = null;
    }

    // ================= PURPOSE =================
    let purpose = 0;

    switch (type) {
        case 1: // IMPORT
            purpose = PURPOSE.IMPORT_PURCHASE;
            break;

        case 2: // EXPORT
            purpose = Number(document.getElementById("purpose").value);
            break;

        case 3: // WASTE
            purpose = Number(document.getElementById("wasteReason").value);
            break;

        case 4: // STOCK TAKE
            purpose = PURPOSE.STOCK_TAKE;
            break;
    }

    if (!purpose) {
        toast("Vui lòng chọn mục đích", "error");
        return;
    }

    const model = {
        storeId: Number(document.getElementById("store").value),
        type: type,
        purpose: purpose,

        supplierId: type === 1 ? Number(getCurrentSupplier().id) : null,

        partnerType: partnerType,
        partnerId: partnerId,
        partnerName: partnerName,

        documentDate: new Date(document.getElementById("documentDate").value).toISOString(),
        note: document.getElementById("note").value,
        details: []
    };

    rows.forEach(tr => {

        let detail = {
            ingredientId: Number(tr.querySelector(".ingredient").value),
            note: tr.querySelector(".note")?.value || ""
        };

        switch (type) {

            // ================= IMPORT =================
            case 1:
                detail.unitId = Number(tr.querySelector(".unit").value);
                detail.quantity = Number(tr.querySelector(".qty").value || 0);
                detail.unitPrice = Number(tr.querySelector(".price").value || 0);
                break;

            // ================= EXPORT =================
            case 2:
                detail.unitId = Number(tr.querySelector(".unit").value);
                detail.quantity = Number(tr.querySelector(".qty").value || 0);
                detail.unitPrice = null;
                break;

            // ================= WASTE =================
            case 3:
                const stock = Number(tr.querySelector(".stock")?.value || 0);
                const qty = Number(tr.querySelector(".qty").value || 0);

                if (qty > stock) {
                    toast("Số lượng hủy vượt tồn kho", "error");
                    throw new Error();
                }
                detail.unitId = Number(tr.dataset.unitId); // 🔥 base unit
                detail.quantity = qty;
                detail.unitPrice = null;
                break;

            // ================= STOCK TAKE =================
            case 4:
                detail.unitId = Number(tr.dataset.unitId); // 🔥 base unit
                detail.quantity = Number(tr.querySelector(".realQty").value || 0);
                detail.unitPrice = null;
                break;
        }

        model.details.push(detail);
    });

    const r = await fetch("/api/admin/inventory-documents", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(model)
    });

    const res = await r.json();

    if (res.success) {
        toast("Tạo thành công", "success");
        location.reload();
    } else {
        toast(res.message, "error");
    }

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

// INIT
window.onload = () => {
    initImport();
};

function closeModal(id) {
    document.getElementById(id).style.display = "none";
}

document.addEventListener("DOMContentLoaded", () => {
    const btn = document.getElementById("closeCreateModal");
    if (btn) {
        btn.addEventListener("click", () => closeModal("createModal"));
    }

    const savedTab = localStorage.getItem("inventory_tab");

    if (!savedTab) return;

    const map = {
        1: "importTab",
        2: "exportTab",
        3: "wasteTab",
        4: "stockTab"
    };

    const tabId = map[savedTab];

    if (tabId) {
        document.querySelectorAll(".tab-content").forEach(t => t.classList.remove("active"));
        document.querySelectorAll(".tab-btn").forEach(t => t.classList.remove("active"));

        document.getElementById(tabId).classList.add("active");

        document.querySelector(`[onclick*="${tabId}"]`)?.classList.add("active");
    }
});

window.addEventListener("click", function(e) {
    document.querySelectorAll(".modal").forEach(m => {
        if (e.target === m) {
            m.style.display = "none";
        }
    });
});

window.closeModal = closeModal;