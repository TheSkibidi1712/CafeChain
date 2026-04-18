import { initImport, addImportRow, getCurrentSupplier } from "./inventory-import.js";
import { addExportRow } from "./inventory-export.js";
import { addStockTakeRow, loadStockTakeTable } from "./inventory-stocktake.js";
import { addWasteRow } from "./inventory-cancel.js";


const TYPE = {
    IMPORT: 1,
    EXPORT: 2,
    WASTE: 3,        
    STOCKTAKE: 4     
};

export const PURPOSE = {
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

        // 🔥 FORMAT TYPE
        const typeMap = {
            IMPORT: "Nhập kho",
            EXPORT: "Xuất kho",
            WASTE: "Hủy kho",
            STOCK_TAKE: "Kiểm kê"
        };

        // 🔥 HEADER INFO
        let html = `
            <p><b>Mã:</b> ${d.code}</p>
            <p><b>Loại:</b> ${typeMap[d.type] || d.type}</p>
            <p><b>Kho:</b> ${d.storeName}</p>
            <p><b>Nhân viên:</b> ${d.staffName}</p>
            <p><b>Ngày:</b> ${new Date(d.date).toLocaleString()}</p>
            <p><b>Trạng thái:</b> ${d.status}</p>
            <p><b>Mục đích:</b> ${d.purpose}</p>
        `;

        // 🔥 PARTNER / SUPPLIER
        if (d.type === "IMPORT" && d.supplierName) {
            html += `<p><b>Nhà cung cấp:</b> ${d.supplierName}</p>`;
        }

        if (d.type === "EXPORT" && d.partnerName) {
            html += `<p><b>Đối tượng:</b> ${d.partnerName}</p>`;
        }

        html += `
            <p><b>Ghi chú:</b> ${d.note || ""}</p>
            <hr/>
            <table class="table">
                <thead>
                    <tr>
                        <th>Mã NL</th>
                        <th>Nguyên liệu</th>
                        <th>Số lượng</th>
                        <th>Đơn vị</th>
                        <th>Quy đổi (base)</th>
                        ${d.type === "IMPORT" ? "<th>Giá</th><th>Thành tiền</th>" : ""}
                        <th>Ghi chú</th>
                    </tr>
                </thead>
                <tbody>
        `;

        let total = 0;

        d.details.forEach(x => {

            const rowTotal = (x.unitPrice || 0) * (x.quantity || 0);
            total += rowTotal;

            html += `
                <tr>
                    <td>${x.ingredientCode}</td>
                    <td>${x.ingredientName}</td>
                    <td>${x.quantity}</td>
                    <td>${x.unitName}</td>
                    <td>${x.baseQuantity} ${x.baseUnitName}</td>

                    ${d.type === "IMPORT" ? `
                        <td>${formatVND(x.unitPrice)}</td>
                        <td>${formatVND(rowTotal)}</td>
                    ` : ""}

                    <td>${x.note || ""}</td>
                </tr>
            `;
        });

        html += `</tbody></table>`;

        // 🔥 TOTAL (CHỈ IMPORT)
        if (d.type === "IMPORT") {
            html += `
                <div style="text-align:right; font-weight:bold; margin-top:10px">
                    Tổng tiền: ${formatVND(total)} VND
                </div>
            `;
        }

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

        // 🔥 DEBUG HTTP ERROR
        if (!r.ok) {
            const text = await r.text();
            console.error("API ERROR:", text);
            throw new Error("HTTP " + r.status);
        }

        const data = await r.json();

        console.log("CREATE DATA:", data); // 🔥 DEBUG

        // 🔥 VALIDATE DATA
        if (!data || !data.stores || data.stores.length === 0) {
            toast("Không có dữ liệu kho (Store)", "error");
            return;
        }

        if (!data.suppliers) {
            data.suppliers = [];
        }

        // ================= BIND SELECT =================
        bindSelect("store", data.stores, "storeId", "name", "-- Chọn Kho --");
        document.getElementById("store").onchange = async function () {
            const selected = Number(this.value);

            const filtered = data.stores.filter(x => x.storeId !== selected);
            bindSelect("toStore", filtered, "storeId", "name", "-- Chọn Kho Nhận --");

            const importPurpose = Number(document.getElementById("importPurposeSelect").value);

            if (importPurpose === PURPOSE.IMPORT_INTERNAL) {
                const r = await fetch(`/api/admin/inventory-documents/transfer-sources?storeId=${selected}`);
                const sources = await r.json();

                bindSelect("fromStore", sources, "storeId", "name", "-- Chọn kho xuất --");
            }

            const type = Number(document.getElementById("type").value);
            if (type === TYPE.STOCKTAKE) {
                loadStockTakeTable();
            }
        };
        bindSelect("supplier", data.suppliers, "supplierId", "name", "-- Chọn Nhà Cung Cấp --");


        // ================= DEFAULT DATE =================
        document.getElementById("documentDate").value =
            new Date().toISOString().slice(0, 16);

        // ================= RESET FORM =================
        resetForm();

        document.getElementById("note").value = "";
        document.getElementById("partnerName").value = "";

        document.getElementById("store").value = "";
        document.getElementById("toStore").value = "";
        document.getElementById("supplier").value = "";

        document.getElementById("purpose").value = "";
        document.getElementById("importPurposeSelect").value = "";
        document.getElementById("wasteReason").value = "";

        document.getElementById("toStoreBox").style.display = "none";

        // ================= SHOW MODAL =================
        document.getElementById("createModal").style.display = "block";


    } catch (err) {
        console.error("OPEN CREATE MODAL ERROR:", err);
        toast("Không tải được dữ liệu (API lỗi)", "error");
    }
}

function resetForm() {
    document.querySelector("#detailTable tbody").innerHTML = "";
    document.getElementById("grandTotal").innerText = "0";
}

// ================= TYPE =================
function onTypeChange() {
    const type = Number(document.getElementById("type").value);

    const supplierBox = document.getElementById("supplierBox");
    const exportPartner = document.getElementById("exportPartner");
    const exportPurpose = document.getElementById("exportPurpose");
    const wastePurpose = document.getElementById("wastePurpose");
    const importPurpose = document.getElementById("importPurpose");

    const table = document.getElementById("detailTable");
    const thead = document.querySelector("#detailTable thead tr");
    const addBtn = document.querySelector(".btn-add");
    const totalBox = document.querySelector(".total-box");
    const toStoreBox = document.getElementById("toStoreBox");
    const fromStoreBox = document.getElementById("fromStoreBox");

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
    importPurpose.style.display = "none";
    toStoreBox.style.display = "none";

    table.classList.remove("hide-price");

   switch (type) {
       case TYPE.IMPORT:
           importPurpose.style.display = "block";

           const importSelect = document.getElementById("importPurposeSelect");

           importSelect.onchange = async () => {
               const val = Number(importSelect.value);
               const storeId = Number(document.getElementById("store").value);

               if (val === PURPOSE.IMPORT_PURCHASE) {
                   supplierBox.style.display = "block";
                   fromStoreBox.style.display = "none";
               }
               else if (val === PURPOSE.IMPORT_INTERNAL) {
                   supplierBox.style.display = "none";
                   fromStoreBox.style.display = "block";

                   // 🔥 LOAD KHO XUẤT
                   if (!storeId) {
                       toast("Chọn kho trước", "error");
                       return;
                   }

                   try {
                       const r = await fetch(`/api/admin/inventory-documents/transfer-sources?storeId=${storeId}`);
                       const data = await r.json();

                       bindSelect("fromStore", data, "storeId", "name", "-- Chọn kho xuất --");

                   } catch {
                       toast("Không tải được kho xuất", "error");
                   }
               }
               else {
                   supplierBox.style.display = "none";
                   fromStoreBox.style.display = "none";
               }

               resetForm();
           };

           addBtn.style.display = "inline-block";
           break; // ✅ QUAN TRỌNG

        case TYPE.EXPORT:
            exportPartner.style.display = "none";
            exportPurpose.style.display = "block";

           const purposeSelect = document.getElementById("purpose");

           purposeSelect.onchange = () => {
               const val = Number(purposeSelect.value);

               // INTERNAL TRANSFER
               if (val === PURPOSE.INTERNAL_OUT) {
                   toStoreBox.style.display = "block";
                   exportPartner.style.display = "none";
                   return;
               }

               // KHÔNG CẦN KHÁCH
               const noPartnerPurposes = [
                   PURPOSE.ADJUSTMENT_OUT,
                   PURPOSE.SAMPLE
               ];

               if (noPartnerPurposes.includes(val)) {
                   exportPartner.style.display = "none";
                   toStoreBox.style.display = "none";
               } else {
                   exportPartner.style.display = "block";
                   toStoreBox.style.display = "none";
               }
           };

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
    const type = Number(document.getElementById("type").value);

    if (type === TYPE.IMPORT) addImportRow();
    else if (type === TYPE.EXPORT) addExportRow();
    else if (type === TYPE.STOCKTAKE) return;
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
    const storeId = Number(document.getElementById("store").value);

    if (!storeId) {
        toast("Vui lòng chọn kho", "error");
        return;
    }


    const rows = document.querySelectorAll("#detailTable tbody tr");
    const type = Number(document.getElementById("type").value);

    let isValid = true;

    rows.forEach(tr => {
        const ingredientId = Number(tr.querySelector(".ingredient").value);
        const quantity =
            type === TYPE.STOCKTAKE
                ? Number(tr.querySelector(".realQty")?.value || 0)
                : Number(tr.querySelector(".qty")?.value || 0);
        const unitId = Number(tr.querySelector(".unit")?.value || tr.dataset.unitId);

        if (!ingredientId) {
            toast("Thiếu nguyên liệu", "error");
            isValid = false;
            return;
        }

        if (quantity <= 0) {
            toast("Số lượng phải lớn hơn 0", "error");
            isValid = false;
            return;
        }

        if (!unitId) {
            toast("Thiếu đơn vị", "error");
            isValid = false;
            return;
        }
    });

    if (!isValid) return;


    let partnerType = null;
    let partnerId = null;
    let partnerName = null;

    // ================= PARTNER =================
    if (type === 1) {

        const importPurpose = Number(document.getElementById("importPurposeSelect").value);

        if (!importPurpose) {
            toast("Chọn mục đích trước", "error");
            return;
        }
        // ================= IMPORT PURCHASE =================
        if (importPurpose === PURPOSE.IMPORT_PURCHASE) {

            const supplier = getCurrentSupplier();

            if (!supplier || !supplier.id) {
                toast("Vui lòng chọn nhà cung cấp", "error");
                return;
            }

            partnerType = 1;
            partnerId = supplier.id;
            partnerName = supplier.name;
        }

        // ================= IMPORT INTERNAL =================
        else if (importPurpose === PURPOSE.IMPORT_INTERNAL) {
            const fromStoreId = Number(document.getElementById("fromStore").value);
            const fromStoreName = document.querySelector("#fromStore option:checked")?.text;

            if (!fromStoreId) {
                toast("Vui lòng chọn kho xuất", "error");
                return;
            }

            partnerType = 3;
            partnerId = fromStoreId;
            partnerName = fromStoreName;
        }

        // ================= IMPORT ADJUSTMENT =================
        else {
            partnerType = 0;
            partnerId = null;
            partnerName = null;
        }
    }
    else if (type === 2) {
        const purposeExport = Number(document.getElementById("purpose").value);

        if (purposeExport === PURPOSE.INTERNAL_OUT) {

            const toStoreId = Number(document.getElementById("toStore").value);
            const toStoreName = document.querySelector("#toStore option:checked")?.text;

            partnerType = 3; // STORE
            partnerId = toStoreId;
            partnerName = toStoreName;

        } else {
            partnerType = 2;
            partnerName = document.getElementById("partnerName").value || "Khách lẻ";
            partnerId = null;
        }
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
            purpose = Number(document.getElementById("importPurposeSelect").value);
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

    // ================= INTERNAL TRANSFER =================
    if (type === 2 && purpose === PURPOSE.INTERNAL_OUT) {

        const toStoreId = Number(document.getElementById("toStore").value);

        if (!toStoreId) {
            toast("Thiếu kho nhận", "error");
            return;
        }
        if (storeId === toStoreId) {
            toast("Kho nhận phải khác kho xuất", "error");
            return;
        }
        const transferModel = {
            fromStoreId: Number(document.getElementById("store").value),
            toStoreId: Number(toStoreId),
            note: document.getElementById("note").value,
            items: []
        };

        rows.forEach(tr => {
            transferModel.items.push({
                ingredientId: Number(tr.querySelector(".ingredient").value),
                quantity: Number(tr.querySelector(".qty").value || 0),
                unitId: Number(tr.querySelector(".unit").value),
                unitPrice: 0,
                note: tr.querySelector(".note")?.value || ""
            });
        });

        const r = await fetch("/api/admin/inventory-documents/internal-transfer", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(transferModel)
        });

        const res = await r.json();

        if (res.success) {
            toast("Chuyển kho thành công", "success");
            localStorage.setItem("inventory_tab", type);
            location.reload();
        } else {
            toast(res.message, "error");
        }

        return; // 🔥 CHẶN KHÔNG CHẠY API CŨ
    }

    const supplier = getCurrentSupplier();

    const model = {
        storeId: Number(document.getElementById("store").value),
        type: type,
        purpose: purpose,
        supplierId: type === 1 && supplier ? Number(supplier.id) : null,


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
                detail.unitId = Number(tr.querySelector(".unit").value);
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

        localStorage.setItem("inventory_tab", type);
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