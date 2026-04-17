const TYPE = {
    IMPORT: "1"
};

let currentSupplier = null;
let ingredientsBySupplier = [];

export function initImport() {
    const supplier = document.getElementById("supplier");

    if (supplier) {
        supplier.addEventListener("change", loadIngredientsBySupplier);
    }
}

export function getCurrentSupplier() {
    return currentSupplier;
}

// ================= LOAD INGREDIENT =================
async function loadIngredientsBySupplier() {
    const supplierSelect = document.getElementById("supplier");
    const supplierId = supplierSelect.value;

    ingredientsBySupplier = [];
    currentSupplier = null;

    if (!supplierId) return;

    // 🔥 LẤY NAME
    const selectedOption = supplierSelect.selectedOptions[0];
    currentSupplier = {
        id: supplierId,
        name: selectedOption.text
    };

    try {
        const r = await fetch(`/api/admin/inventory-documents/ingredient-suppliers?supplierId=${supplierId}`);

        if (!r.ok) throw new Error();

        const data = await r.json();

        ingredientsBySupplier = Array.isArray(data) ? data : [];

    } catch {
        toast("Không tải được nguyên liệu", "error");
    }
}

// ================= ADD ROW =================
export function addImportRow() {
    if (!ingredientsBySupplier || ingredientsBySupplier.length === 0) {
        toast("Vui lòng chọn nhà cung cấp trước", "error");
        return;
    }

    const tr = document.createElement("tr");

    tr.innerHTML = `
        <td><select class="ingredient"></select></td>
        <td><select class="unit" disabled></select></td>
        <td><input type="number" class="qty" value="1" min="1"></td>
        <td><input class="price" readonly></td>
        <td><span class="rowTotal">0</span></td>
        <td><input class="note"></td>
        <td><button type="button">X</button></td>
    `;

    document.querySelector("#detailTable tbody").appendChild(tr);

    const ingSelect = tr.querySelector(".ingredient");
    const unitSelect = tr.querySelector(".unit");
    const priceInput = tr.querySelector(".price");

    // ✅ FIX FIELD CHUẨN
    ingSelect.innerHTML =
        `<option value="">-- Chọn nguyên liệu --</option>` +
        ingredientsBySupplier.map(x =>
            `<option value="${x.ingredientId}">${x.ingredientName}</option>`
        ).join("");

    // ================= EVENTS =================

    // chọn nguyên liệu
    ingSelect.addEventListener("change", async () => {
        // reset unit + price
        unitSelect.innerHTML = "";
        priceInput.value = "";
        tr.querySelector(".rowTotal").innerText = "0";

        await loadImportUnitPrice(tr);
    });

    // thay đổi số lượng
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

        const unitSelect = tr.querySelector(".unit");
        const priceInput = tr.querySelector(".price");

        // ✅ check data tránh undefined
        if (!data) return;

        unitSelect.innerHTML =
            `<option value="${data.unitId}">${data.unitName}</option>`;

        priceInput.value = data.price || 0;

        calcRow(tr);

    } catch {
        toast("Không lấy được giá", "error");
    }
} 