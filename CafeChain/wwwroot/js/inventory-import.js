import { PURPOSE } from "./constants/inventory-constants.js";

let currentSupplier = null;
let ingredientsBySupplier = [];

// ================= INIT =================
export function initImport() {
    const supplier = document.getElementById("supplier");

    if (supplier) {
        supplier.addEventListener("change", loadIngredientsBySupplier);
    }
}

export function getCurrentSupplier() {
    return currentSupplier;
}

// ================= LOAD INGREDIENT BY SUPPLIER =================
async function loadIngredientsBySupplier() {
    const supplierSelect = document.getElementById("supplier");
    const supplierId = supplierSelect.value;

    ingredientsBySupplier = [];
    currentSupplier = null;

    if (!supplierId) return;

    const selectedOption = supplierSelect.selectedOptions[0];

    currentSupplier = {
        id: Number(supplierId),
        name: selectedOption?.text || ""
    };

    try {
        const r = await fetch(`/api/admin/inventory-documents/ingredient-suppliers?supplierId=${supplierId}`);
        if (!r.ok) throw new Error();

        const data = await r.json();
        ingredientsBySupplier = Array.isArray(data) ? data : [];

    } catch {
        toast("Không tải được nguyên liệu từ nhà cung cấp", "error");
    }
}

// ================= ADD ROW =================
export async function addImportRow() {

    const purpose = Number(document.getElementById("importPurposeSelect").value);

    // chưa chọn mục đích
    if (!purpose) {
        toast("Vui lòng chọn mục đích nhập", "error");
        return;
    }

    // ❌ nhập nội bộ không được add tay
    if (purpose === PURPOSE.IMPORT_INTERNAL) {
        toast("Nhập nội bộ không thêm dòng thủ công", "warning");
        return;
    }

    // ================= ĐIỀU CHỈNH TĂNG =================
    if (purpose === PURPOSE.IMPORT_ADJUSTMENT) {
        const storeId = Number(document.getElementById("store").value);

        if (!storeId) {
            toast("Vui lòng chọn kho trước", "error");
            return;
        }

        const tr = document.createElement("tr");

        tr.innerHTML = `
            <td>
                <select class="ingredient"></select>
            </td>

            <td>
                <select class="unit" disabled></select>
            </td>

            <td>
                <input type="number" class="qty" value="1" min="1">
            </td>

            <td>
                <input type="number" class="price" value="0" min="0">
            </td>

            <td>
                <span class="rowTotal">0</span>
            </td>

            <td>
                <input class="note">
            </td>

            <td class="col-action">
                <button type="button" class="btn-remove-row">✕</button>
            </td>
        `;

        document.querySelector("#detailTable tbody").appendChild(tr);

        const ingSelect = tr.querySelector(".ingredient");
        const unitSelect = tr.querySelector(".unit");
        const qtyInput = tr.querySelector(".qty");
        const priceInput = tr.querySelector(".price");

        // ================= LOAD INGREDIENT THEO STORE =================
        try {
            const r = await fetch(
                `/api/admin/inventory-documents/store-inventories?storeId=${storeId}`
            );

            if (!r.ok) throw new Error();

            const data = await r.json();

            ingSelect.innerHTML =
                `<option value="">-- Chọn nguyên liệu --</option>` +
                data.map(x => `
                    <option value="${x.ingredientId}">
                        ${x.name}
                    </option>
                `).join("");

        } catch {
            toast("Không tải được nguyên liệu trong kho", "error");
        }

        // ================= CHANGE INGREDIENT =================
        ingSelect.addEventListener("change", async () => {
            const ingredientId = Number(ingSelect.value);

            unitSelect.innerHTML = "";
            priceInput.value = 0;
            tr.querySelector(".rowTotal").innerText = "0";

            if (!ingredientId) return;

            try {
                // 🔥 LOAD UNIT
                const r = await fetch(
                    `/api/admin/inventory-documents/units?ingredientId=${ingredientId}`
                );

                if (!r.ok) throw new Error();

                const units = await r.json();

                if (!units || !units.length) {
                    toast("Không tìm thấy đơn vị", "warning");
                    return;
                }

                const baseUnit = units[0];

                unitSelect.innerHTML = `
                    <option value="${baseUnit.unitId}">
                        ${baseUnit.name}
                    </option>
                `;

                // 🔥 backup để submit
                tr.dataset.unitId = baseUnit.unitId;

                // 🔥 LOAD LAST PRICE (chuẩn cho điều chỉnh tồn)
                try {
                    const priceRes = await fetch(
                        `/api/admin/inventory-documents/last-price?storeId=${storeId}&ingredientId=${ingredientId}`
                    );

                    if (!priceRes.ok) throw new Error();

                    const priceData = await priceRes.json();

                    priceInput.value = priceData?.lastPrice || 0;

                } catch {
                    priceInput.value = 0;
                }

                calcRow(tr);
                refreshIngredientOptions();
            } catch {
                toast("Không tải được đơn vị", "error");
            }
        });

        // ================= EVENTS =================
        qtyInput.addEventListener("input", () => calcRow(tr));
        priceInput.addEventListener("input", () => calcRow(tr));

        tr.querySelector("button").addEventListener("click", () => {
            tr.remove();
            calcTotal();
        });

        calcRow(tr);

        return;
    }

    // ================= NHẬP TỪ NCC =================
    if (purpose !== PURPOSE.IMPORT_PURCHASE) {
        toast("Chỉ áp dụng cho nhập từ nhà cung cấp", "error");
        return;
    }

    if (!ingredientsBySupplier.length) {
        toast("Vui lòng chọn nhà cung cấp trước", "error");
        return;
    }

    const tr = document.createElement("tr");

    tr.innerHTML = `
        <td><select class="ingredient"></select></td>
        <td><select class="unit" disabled></select></td>
        <td><input type="number" class="qty" value="1" min="1"></td>
        <td><input type="number" class="price" readonly></td>
        <td><span class="rowTotal">0</span></td>
        <td><input class="note"></td>
        <td class="col-action">
            <button type="button" class="btn-remove-row">✕</button>
        </td>
    `;

    document.querySelector("#detailTable tbody").appendChild(tr);

    const ingSelect = tr.querySelector(".ingredient");
    const unitSelect = tr.querySelector(".unit");
    const priceInput = tr.querySelector(".price");

    // ================= LOAD INGREDIENT =================
    ingSelect.innerHTML =
        `<option value="">-- Chọn nguyên liệu --</option>` +
        ingredientsBySupplier.map(x =>
            `<option value="${x.ingredientId}">${x.ingredientName}</option>`
        ).join("");

    // ================= EVENTS =================

    // chọn nguyên liệu
    ingSelect.addEventListener("change", async () => {

        unitSelect.innerHTML = "";
        priceInput.value = "";
        tr.querySelector(".rowTotal").innerText = "0";

        await loadImportUnitPrice(tr);
        refreshIngredientOptions();
    });

    // đổi số lượng
    tr.querySelector(".qty").addEventListener("input", () => calcRow(tr));

    // xóa dòng
    tr.querySelector("button").addEventListener("click", () => {
        tr.remove();
        calcTotal();
        refreshIngredientOptions();
    });

    calcRow(tr);
    refreshIngredientOptions();
}

// ================= LOAD UNIT + PRICE =================
async function loadImportUnitPrice(tr) {

    const ingId = Number(tr.querySelector(".ingredient").value);
    const supplierId = Number(document.getElementById("supplier").value);

    if (!ingId || !supplierId) return;

    try {
        const r = await fetch(`/api/admin/inventory-documents/import-info?ingredientId=${ingId}&supplierId=${supplierId}`);
        if (!r.ok) throw new Error();

        const data = await r.json();

        if (!data) {
            toast("Nguyên liệu chưa có cấu hình giá nhập", "warning");
            return;
        }

        const unitSelect = tr.querySelector(".unit");
        const priceInput = tr.querySelector(".price");

        // ✅ khóa unit (base unit từ NCC)
        unitSelect.innerHTML =
            `<option value="${data.unitId}">${data.unitName}</option>`;

        // 👉 lưu base unit vào dataset (backup cho submit)
        tr.dataset.unitId = data.unitId;

        // ✅ khóa giá
        priceInput.value = data.price ?? 0;

        calcRow(tr);

    } catch {
        toast("Không lấy được giá nhập từ nhà cung cấp", "error");
    }
}

// ================= CALC =================
function calcRow(tr) {
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

// ================= UTIL =================
function formatVND(n) {
    return Number(n || 0).toLocaleString("vi-VN");
}

function parseMoney(str) {
    return parseFloat((str || "0").replace(/[^\d]/g, "")) || 0;
}

function getSelectedIngredientIds() {
    const ids = [];

    document.querySelectorAll(".ingredient").forEach(sel => {
        const val = Number(sel.value);
        if (val) ids.push(val);
    });

    return ids;
}

function refreshIngredientOptions() {
    const selectedIds = getSelectedIngredientIds();

    document.querySelectorAll("#detailTable tbody tr").forEach(tr => {
        const select = tr.querySelector(".ingredient");
        const currentValue = Number(select.value);

        Array.from(select.options).forEach(opt => {
            const val = Number(opt.value);

            if (!val) return;

            // 🔥 Ẩn nếu đã chọn ở row khác
            opt.hidden = selectedIds.includes(val) && val !== currentValue;
        });
    });
}