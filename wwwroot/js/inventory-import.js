const TYPE = {
    IMPORT: "1"
};

let ingredientsBySupplier = [];

export function initImport() {
    const supplier = document.getElementById("supplier");

    if (supplier) {
        supplier.addEventListener("change", loadIngredientsBySupplier);
    }
}

function loadIngredientsBySupplier() {
    const supplierId = document.getElementById("supplier").value;

    if (!supplierId) {
        ingredientsBySupplier = [];
        return;
    }

    fetch(`/api/admin/inventory-documents/ingredient-suppliers?supplierId=${supplierId}`)
        .then(r => r.json())
        .then(data => {
            ingredientsBySupplier = data;
        });
}

export function addImportRow() {
    if (ingredientsBySupplier.length === 0) {
        toast("Vui lòng chọn nhà cung cấp trước", false);
        return;
    }

    const tr = document.createElement("tr");

    tr.innerHTML = `
        <td><select class="ingredient"></select></td>
        <td><select class="unit" disabled></select></td>
        <td><input type="number" class="qty" value="1"></td>
        <td><input class="price" readonly></td>
        <td><span class="rowTotal">0</span></td>
        <td><input class="note"></td>
        <td><button onclick="this.closest('tr').remove(); calcTotal()">X</button></td>
    `;

    document.querySelector("#detailTable tbody").appendChild(tr);

    const ingSelect = tr.querySelector(".ingredient");

    ingSelect.innerHTML =
        `<option value="">-- Chọn nguyên liệu --</option>` +
        ingredientsBySupplier.map(x =>
            `<option value="${x.ingredientId}">${x.ingredient.name}</option>`
        ).join("");

    ingSelect.addEventListener("change", () => loadImportUnitPrice(tr));
    tr.querySelector(".qty").addEventListener("input", () => calcRow(tr));
}

function loadImportUnitPrice(tr) {
    const ingId = tr.querySelector(".ingredient").value;
    const supplierId = document.getElementById("supplier").value;

    if (!ingId) return;

    fetch(`/api/admin/inventory-documents/import-info?ingredientId=${ingId}&supplierId=${supplierId}`)
        .then(r => r.json())
        .then(data => {

            const unitSelect = tr.querySelector(".unit");

            unitSelect.innerHTML =
                `<option value="${data.unitId}">${data.unitName}</option>`;

            const priceInput = tr.querySelector(".price");
            priceInput.value = data.price;

            calcRow(tr);
        });
}