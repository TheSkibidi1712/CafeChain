import { initImport, addImportRow, getCurrentSupplier } from "./inventory-import.js";
import { addExportRow } from "./inventory-export.js";
import { addStockTakeRow, loadStockTakeTable } from "./inventory-stocktake.js";
import { addWasteRow } from "./inventory-cancel.js";
import { TYPE, PURPOSE } from "./constants/inventory-constants.js";


// ================= FILTER =================
function reloadData() {
    const keyword = document.getElementById("search").value.trim();
    const type = document.getElementById("filterType").value;
    const fromDate = document.getElementById("fromDate").value;
    const toDate = document.getElementById("toDate").value;

    const params = new URLSearchParams({
        keyword,
        type,
        fromDate,
        toDate,
        page: 1
    });

    window.location.href =
        `/Admin/AdminInventoryDocument/Index?${params.toString()}`;
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
            1: "Nhập kho",
            2: "Xuất kho",
            3: "Hủy kho",
            4: "Kiểm kê"
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
        if (d.type === TYPE.IMPORT && d.supplierName) {
            html += `<p><b>Nhà cung cấp:</b> ${d.supplierName}</p>`;
        }

        if (d.type === TYPE.EXPORT && d.partnerName) {
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
                        ${d.type === TYPE.IMPORT ? "<th>Giá</th><th>Thành tiền</th>" : ""}
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

                    ${d.type === TYPE.IMPORT ? `
                        <td>${formatVND(x.unitPrice)}</td>
                        <td>${formatVND(rowTotal)}</td>
                    ` : ""}

                    <td>${x.note || ""}</td>
                </tr>
            `;
        });

        html += `</tbody></table>`;

        // 🔥 TOTAL (CHỈ IMPORT)
        if (d.type === TYPE.IMPORT) {
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

            const type = Number(document.getElementById("type").value);
            if (type === TYPE.STOCK_TAKE) {
                loadStockTakeTable();
            }

            const selected = Number(this.value);

            const filtered = data.stores.filter(x => x.storeId !== selected);
            bindSelect("toStore", filtered, "storeId", "name", "-- Chọn Kho Nhận --");

            const importPurpose = Number(document.getElementById("importPurposeSelect").value);
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
    const transferDocumentBox = document.getElementById("transferDocumentBox");
    const transferDocument = document.getElementById("transferDocument");

    resetForm();

    if (type === TYPE.STOCK_TAKE) {
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
    transferDocumentBox.style.display = "none";

    table.classList.remove("hide-price");

   switch (type) {
       case TYPE.IMPORT:
           importPurpose.style.display = "block";

           const importSelect = document.getElementById("importPurposeSelect");

           importSelect.onchange = async () => {
               const val = Number(importSelect.value);
               const storeId = Number(document.getElementById("store").value);

               supplierBox.style.display = "none";
               transferDocumentBox.style.display = "none";

               // ================= NHẬP MUA =================
               if (val === PURPOSE.IMPORT_PURCHASE) {
                   supplierBox.style.display = "block";
                   addBtn.style.display = "inline-block";

                   resetForm();
                   return;
               }

               // ================= NHẬP NỘI BỘ =================
               if (val === PURPOSE.IMPORT_INTERNAL) {
                   if (!storeId) {
                       toast("Vui lòng chọn kho nhận trước", "error");
                       return;
                   }

                   transferDocumentBox.style.display = "block";

                   try {
                       const r = await fetch(
                           `/api/admin/inventory-documents/pending-internal-exports?storeId=${storeId}`
                       );

                       if (!r.ok) throw new Error();

                       const data = await r.json();

                       bindSelect(
                           "transferDocument",
                           data,
                           "transferId",
                           "code",
                           "-- Chọn phiếu chuyển kho --"
                       );

                       // nhập nội bộ không add tay
                       addBtn.style.display = "none";

                       if (data.length > 0) {
                           document.getElementById("transferDocument").value = data[0].transferId;
                           await loadTransferDocumentDetails();
                       } else {
                           resetForm();
                       }

                   } catch {
                       toast("Không tải được phiếu chuyển kho", "error");
                   }

                   return;
               }

               // ================= ĐIỀU CHỈNH TĂNG =================
               // IMPORT_ADJUSTMENT
               if (val === PURPOSE.IMPORT_ADJUSTMENT) {
                   supplierBox.style.display = "none"; // ❌ không có NCC
                   transferDocumentBox.style.display = "none";

                   addBtn.style.display = "inline-block";
                   resetForm();
                   return;
               }
           };

           addBtn.style.display = "inline-block";
           break;

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

       case TYPE.STOCK_TAKE:
           addBtn.style.display = "none";

           const storeId = document.getElementById("store").value;

           if (storeId) {
               loadStockTakeTable();
           }

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

    if (type === TYPE.IMPORT && Number(document.getElementById("importPurposeSelect").value) === PURPOSE.IMPORT_INTERNAL) {
        toast("Nhập nội bộ không thêm dòng thủ công", "warning");
        return;
    }

    if (type === TYPE.IMPORT) addImportRow();
    else if (type === TYPE.EXPORT) addExportRow();
    else if (type === TYPE.STOCK_TAKE) return;
    else if (type === TYPE.WASTE) addWasteRow();
    
}

// ================= SWITCH TAB =================
function switchTab(evt, tabId) {
    let type = 1;

    switch (tabId) {
        case "importTab":
            type = 1;
            break;
        case "exportTab":
            type = 2;
            break;
        case "wasteTab":
            type = 3;
            break;
        case "stockTab":
            type = 4;
            break;
    }

    const keyword = document.getElementById("search").value.trim();
    const fromDate = document.getElementById("fromDate").value;
    const toDate = document.getElementById("toDate").value;

    const params = new URLSearchParams({
        keyword,
        type,
        fromDate,
        toDate,
        page: 1,
        pageSize: 10
    });

    localStorage.setItem("inventory_tab", type);

    window.location.href =
        `/Admin/AdminInventoryDocument/Index?${params.toString()}`;
}

// ================= CREATE TYPE =================
async function openCreateModalWithType(type) {
    await openCreateModal();

    const typeInput = document.getElementById("type");
    const typeText = document.getElementById("typeText");

    typeInput.value = String(type);

    typeText.value =
        type == 1 ? "Nhập kho" :
            type == 2 ? "Xuất kho" :
                type == 3 ? "Hủy kho" :
                    "Kiểm kê";

    console.log("TYPE SET:", typeInput.value);

    setTimeout(() => {
        onTypeChange();
    }, 0);
}

// ================= CALC =================
function calcRow(tr) {
    const type = Number(document.getElementById("type").value);
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

// ================= LOAD TRANSFER DETAILS =================
async function loadTransferDocumentDetails() {
    const transferId = Number(
        document.getElementById("transferDocument").value
    );

    const importPurpose = Number(
        document.getElementById("importPurposeSelect").value
    );

    const thead = document.querySelector("#detailTable thead tr");
    const totalBox = document.querySelector(".total-box");

    // 🔥 nhập nội bộ -> ẩn giá + thành tiền
    if (importPurpose === PURPOSE.IMPORT_INTERNAL) {
        thead.innerHTML = `
            <th>Nguyên liệu</th>
            <th>Đơn vị</th>
            <th>Số lượng</th>
            <th>Ghi chú</th>
        `;

        if (totalBox) {
            totalBox.classList.add("hidden");
        }
    }

    if (!transferId) {
        resetForm();
        return;
    }

    try {
        const r = await fetch(
            `/api/admin/inventory-documents/transfer/${transferId}`
        );

        if (!r.ok) throw new Error();

        const data = await r.json();

        console.log("TRANSFER DETAIL DATA:", data);

        const tbody = document.querySelector("#detailTable tbody");
        tbody.innerHTML = "";

        if (!data || !data.items || data.items.length === 0) {
            resetForm();
            return;
        }

        data.items.forEach(x => {
            // 🔥 nhập nội bộ dùng remainingQuantity
            const qty = Number(x.remainingQuantity || 0);

            // 🔥 unitPrice thường null với internal transfer
            // fallback sang exportPrice / lastPrice nếu BE có trả
            const price =
                Number(
                    x.unitPrice ??
                    x.exportPrice ??
                    x.lastPrice ??
                    0
                );

            const tr = document.createElement("tr");

            tr.dataset.unitId = x.unitId;

            tr.innerHTML = `
                <td>
                    <input
                        type="hidden"
                        class="ingredient"
                        value="${x.ingredientId}"
                    />
                    ${x.ingredientName}
                </td>

                <td>
                    <input
                        type="hidden"
                        class="unit"
                        value="${x.unitId}"
                    />
                    ${x.unitName}
                </td>

                <td>
                    <input
                        type="number"
                        class="qty"
                        value="${qty}"
                        max="${qty}"
                        min="1"
                    />
                </td>

                <td>
                    <input
                        class="note"
                        value=""
                    />
                </td>

            `;

            // 🔥 validate số lượng nhận
            const qtyInput = tr.querySelector(".qty");

            qtyInput.addEventListener("input", () => {
                let currentQty = Number(qtyInput.value || 0);

                if (currentQty < 1) {
                    currentQty = 1;
                    qtyInput.value = 1;
                }

                if (currentQty > qty) {
                    currentQty = qty;
                    qtyInput.value = qty;
                }
            });

            tbody.appendChild(tr);
        });

        // 🔥 nhập nội bộ không có tổng tiền
        document.getElementById("grandTotal").innerText = "0";

    } catch (err) {
        console.error("LOAD TRANSFER DETAILS ERROR:", err);
        toast("Không tải được chi tiết phiếu chuyển", "error");
    }
}
// Confirm phiếu nhập nội bộ (chỉ còn bước xác nhận nhập kho bên kho nhận)
async function confirmInternalTransfer(transferId) {
    if (!confirm("Xác nhận nhập kho?")) return;

    const rows = document.querySelectorAll("#detailTable tbody tr");

    if (!rows || rows.length === 0) {
        toast("Chưa có dữ liệu nhận (chọn phiếu chuyển trước)", "error");
        return;
    }

    const receiveItems = [];

    try {
        // ================= VALIDATE + BUILD DATA =================
        rows.forEach(tr => {
            const ingredientEl = tr.querySelector(".ingredient");
            const qtyEl = tr.querySelector(".qty");

            const ingredientId = Number(ingredientEl?.value);
            const baseQuantity = Number(qtyEl?.value || 0);

            if (!ingredientId) {
                throw new Error("Thiếu nguyên liệu");
            }

            if (baseQuantity <= 0) {
                throw new Error("Có dòng chưa nhập số lượng nhận");
            }

            receiveItems.push({
                ingredientId: ingredientId,
                baseQuantity: baseQuantity
            });
        });

        if (receiveItems.length === 0) {
            toast("Không có dữ liệu nhận", "error");
            return;
        }

        // ================= STEP 1: RECEIVE =================
        let r = await fetch(
            `/api/admin/inventory-documents/internal-transfer/${transferId}/receive`,
            {
                method: "POST",
                headers: {
                    "Content-Type": "application/json"
                },
                body: JSON.stringify(receiveItems)
            }
        );

        const text = await r.text();
        console.log("RECEIVE RESPONSE:", text);

        let res;

        try {
            res = JSON.parse(text);
        } catch {
            toast("Response không hợp lệ từ server (receive)", "error");
            return;
        }

        if (!r.ok || !res.success) {
            toast(res?.message || "Receive thất bại", "error");
            return;
        }

        // ================= STEP 2: CONFIRM =================
        r = await fetch(
            `/api/admin/inventory-documents/internal-transfer/${transferId}/confirm`,
            {
                method: "POST",
                headers: {
                    "Content-Type": "application/json"
                }
            }
        );

        const confirmText = await r.text();
        console.log("CONFIRM RESPONSE:", confirmText);

        try {
            res = JSON.parse(confirmText);
        } catch {
            toast("Response không hợp lệ từ server (confirm)", "error");
            return;
        }

        if (!r.ok || !res.success) {
            toast(res?.message || "Confirm thất bại", "error");
            return;
        }

        // ================= SUCCESS =================
        localStorage.setItem("inventory_tab", TYPE.IMPORT); // hoặc tab đang đứng

        toast("Đã xác nhận nhập kho", "success");

        setTimeout(() => location.reload(), 800);
    } catch (err) {
        console.error("CONFIRM INTERNAL TRANSFER ERROR:", err);
        toast(err.message || "Lỗi xác nhận phiếu nhập nội bộ", "error");
    }
}

// Confirm nhận hết (dành cho trường hợp kho nhận muốn nhận hết mà không cần nhập số lượng từng dòng, hoặc có nhiều dòng mà nhập thủ công sẽ mất thời gian)
async function confirmAll(transferId) {
    if (!confirm("Xác nhận nhận hết?")) return;

    try {
        const r = await fetch(
            `/api/admin/inventory-documents/internal-transfer/${transferId}/confirm-all`,
            { method: "POST" }
        );

        const text = await r.text();
        console.log("CONFIRM ALL RESPONSE:", text);

        let res;
        try {
            res = JSON.parse(text);
        } catch {
            toast("Response không hợp lệ từ server", "error");
            return;
        }

        if (!r.ok || !res.success) {
            toast(res?.message || "Confirm all thất bại", "error");
            return;
        }

        // 🔥 LƯU TAB TRƯỚC KHI RELOAD
        localStorage.setItem("inventory_tab", 1); // hoặc tab hiện tại

        toast("Đã nhận hết", "success");

        setTimeout(() => location.reload(), 800);
    } catch (err) {
        console.error(err);
        toast("Lỗi confirm all", "error");
    }
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
        const ingredientEl = tr.querySelector(".ingredient");
        const ingredientId = Number(ingredientEl?.value);

        const quantity =
            type === TYPE.STOCK_TAKE
                ? Number(tr.querySelector(".realQty")?.value || 0)
                : Number(tr.querySelector(".qty")?.value || 0);

        const unitEl = tr.querySelector(".unit");
        const unitId = Number(unitEl?.value) || Number(tr.dataset.unitId);

        if (!ingredientId) {
            toast("Thiếu nguyên liệu", "error");
            isValid = false;
            return;
        }

        if (type === TYPE.STOCK_TAKE) {
            if (quantity < 0) {
                toast("Số lượng kiểm kê không hợp lệ", "error");
                isValid = false;
                return;
            }
        } else {
            if (quantity <= 0) {
                toast("Số lượng phải lớn hơn 0", "error");
                isValid = false;
                return;
            }
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
    if (type === TYPE.IMPORT) {

        const importPurpose = Number(document.getElementById("importPurposeSelect").value);

        if (!importPurpose) {
            toast("Chọn mục đích trước", "error");
            return;
        }

        // IMPORT PURCHASE
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

        // IMPORT INTERNAL
        else if (importPurpose === PURPOSE.IMPORT_INTERNAL) {
            const transferId = Number(document.getElementById("transferDocument").value);

            if (!transferId) {
                toast("Vui lòng chọn phiếu chuyển kho", "error");
                return;
            }
        }

        // IMPORT ADJUSTMENT
        else if (importPurpose === PURPOSE.IMPORT_ADJUSTMENT) {
            partnerType = 0;
            partnerId = null;
            partnerName = null;
        }
    }
    else if (type === TYPE.EXPORT) {

        const purposeExport = Number(document.getElementById("purpose").value);

        if (purposeExport === PURPOSE.INTERNAL_OUT) {
            const toStoreId = Number(document.getElementById("toStore").value);
            const toStoreName = document.querySelector("#toStore option:checked")?.text;

            partnerType = 4;
            partnerId = toStoreId;
            partnerName = toStoreName;
        }
        else if (
            purposeExport === PURPOSE.SALE ||
            purposeExport === PURPOSE.GIFT ||
            purposeExport === PURPOSE.DEBT
        ) {
            const name = document.getElementById("partnerName").value?.trim();

            if (!name) {
                toast("Vui lòng nhập tên khách hàng", "error");
                return;
            }

            partnerType = 2;
            partnerName = name;
            partnerId = null;
        }
        else {
            partnerType = 0;
            partnerId = null;
            partnerName = null;
        }
    }
    else if (type === TYPE.WASTE) {
        const storeName = document.querySelector("#store option:checked")?.text;

        partnerType = 4;
        partnerId = storeId;
        partnerName = storeName;
    }
    else {
        partnerType = 0;
        partnerId = null;
        partnerName = null;
    }

    // ================= PURPOSE =================
    let purpose = 0;

    switch (type) {
        case 1:
            purpose = Number(document.getElementById("importPurposeSelect").value);
            break;
        case 2:
            purpose = Number(document.getElementById("purpose").value);
            break;
        case 3:
            purpose = Number(document.getElementById("wasteReason").value);
            break;
        case 4:
            purpose = PURPOSE.STOCK_TAKE;
            break;
    }

    if (!purpose) {
        toast("Vui lòng chọn mục đích", "error");
        return;
    }

    // ================= INTERNAL TRANSFER (EXPORT) =================
    if (type === TYPE.EXPORT && purpose === PURPOSE.INTERNAL_OUT) {

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
            fromStoreId: storeId,
            toStoreId: toStoreId,
            note: document.getElementById("note").value,
            items: []
        };

        rows.forEach(tr => {
            transferModel.items.push({
                ingredientId: Number(tr.querySelector(".ingredient").value),
                quantity: Number(tr.querySelector(".qty").value || 0),
                unitId: Number(tr.querySelector(".unit")?.value || tr.dataset.unitId),
                unitPrice: 0,
                note: tr.querySelector(".note")?.value || ""
            });
        });

        const r = await fetch("/api/admin/inventory-documents/internal-transfer", {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify(transferModel)
        });

        const text = await r.text();
        console.log("INTERNAL TRANSFER RESPONSE:", text);

        let res;

        try {
            res = JSON.parse(text);
        } catch {
            toast("Response không hợp lệ từ server", "error");
            return;
        }

        if (!r.ok || !res.success) {
            toast(res?.message || "Xuất nội bộ thất bại", "error");
            return;
        }

        toast("Chuyển kho thành công", "success");
        localStorage.setItem("inventory_tab", type);
        setTimeout(() => location.reload(), 800);
        return;
    }

    if (type === TYPE.IMPORT &&purpose === PURPOSE.IMPORT_INTERNAL
    )
    {
        const transferId = Number(
            document.getElementById("transferDocument").value
        );

        if (!transferId) {
            toast("Vui lòng chọn phiếu chuyển kho", "error");
            return;
        }

        await confirmInternalTransfer(transferId);
        return;
    }

    const supplier = getCurrentSupplier();

    const model = {
        storeId: storeId,
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

        const unitEl = tr.querySelector(".unit");
        const priceEl = tr.querySelector(".price");

        let detail = {
            ingredientId: Number(tr.querySelector(".ingredient").value),
            note: tr.querySelector(".note")?.value || ""
        };

        switch (type) {

            case 1: // IMPORT
                detail.unitId = Number(unitEl?.value) || Number(tr.dataset.unitId);
                detail.quantity = Number(tr.querySelector(".qty").value || 0);
                detail.unitPrice = priceEl ? Number(priceEl.value || 0) : 0; // 🔥 FIX
                break;

            case 2: // EXPORT
                detail.unitId = Number(unitEl?.value) || Number(tr.dataset.unitId);
                detail.quantity = Number(tr.querySelector(".qty").value || 0);
                detail.unitPrice = null;
                break;

            case 3: // WASTE
                const stock = Number(tr.querySelector(".stock")?.value || 0);
                const qty = Number(tr.querySelector(".qty").value || 0);

                if (qty > stock) {
                    toast("Số lượng hủy vượt tồn kho", "error");
                    throw new Error();
                }

                detail.unitId = Number(tr.dataset.unitId);
                detail.quantity = qty;
                detail.unitPrice = null;
                break;

            case 4: // STOCK TAKE
                detail.unitId = Number(unitEl?.value) || Number(tr.dataset.unitId);
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

    const text = await r.text();
    console.log("CREATE RESPONSE:", text);

    let res;

    try {
        res = JSON.parse(text);
    } catch {
        toast("Response không hợp lệ từ server", "error");
        return;
    }

    if (res.success) {
        localStorage.setItem("inventory_tab", type);
        toast("Tạo thành công", "success");
        setTimeout(() => location.reload(), 800);
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
window.confirmInternalTransfer = confirmInternalTransfer;
window.confirmAll = confirmAll;

// INIT

function closeModal(id) {
    document.getElementById(id).style.display = "none";
}

document.addEventListener("DOMContentLoaded", () => {
    initImport();

    const btn = document.getElementById("closeCreateModal");
    if (btn) {
        btn.addEventListener("click", () => closeModal("createModal"));
    }

    const transferSelect = document.getElementById("transferDocument");

    if (transferSelect) {
        transferSelect.addEventListener(
            "change",
            loadTransferDocumentDetails
        );
    }

    const currentType =
        document.getElementById("filterType")?.value ||
        localStorage.getItem("inventory_tab") ||
        "1";

    const map = {
        1: "importTab",
        2: "exportTab",
        3: "wasteTab",
        4: "stockTab"
    };

    const tabId = map[currentType];

    if (tabId) {
        document.querySelectorAll(".tab-content")
            .forEach(t => t.classList.remove("active"));

        document.querySelectorAll(".tab-btn")
            .forEach(t => t.classList.remove("active"));

        document.getElementById(tabId)?.classList.add("active");

        document.querySelector(`[onclick*="${tabId}"]`)
            ?.classList.add("active");
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