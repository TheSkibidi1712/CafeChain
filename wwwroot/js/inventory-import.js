import { PURPOSE } from "./inventorydocument.js";


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
        id: supplierId,
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
export function addImportRow() {

    const purpose = Number(document.getElementById("importPurposeSelect").value);

    // ❌ chỉ cho nhập purchase
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
        <td><button type="button">X</button></td>
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

        // reset UI
        unitSelect.innerHTML = "";
        priceInput.value = "";
        tr.querySelector(".rowTotal").innerText = "0";

        await loadImportUnitPrice(tr);
    });

    // đổi số lượng
    tr.querySelector(".qty").addEventListener("input", () => calcRow(tr));

    // xóa dòng
    tr.querySelector("button").addEventListener("click", () => {
        tr.remove();
        calcTotal();
    });
}

// ================= LOAD UNIT + PRICE =================
async function loadImportUnitPrice(tr) {

    const ingId = tr.querySelector(".ingredient").value;
    const supplierId = document.getElementById("supplier").value;

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