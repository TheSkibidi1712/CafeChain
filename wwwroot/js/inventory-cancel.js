export function addWasteRow() {
    const storeId = document.getElementById("store").value;

    if (!storeId) {
        toast("Chọn kho trước", false);
        return;
    }

    const tr = document.createElement("tr");

    tr.innerHTML = `
        <td><select class="ingredient"></select></td>
        <td><input class="unitName" readonly /></td>
        <td><input type="number" class="qty" value="1" min="0"></td>
        <td><input class="stock" readonly /></td>
        <td><input class="note"></td>
        <td><button onclick="this.closest('tr').remove()">X</button></td>
    `;

    document.querySelector("#detailTable tbody").appendChild(tr);

    const ingSelect = tr.querySelector(".ingredient");

    // 🔥 LOAD INVENTORY
    fetch(`/api/admin/inventory-documents/store-inventories?storeId=${storeId}&onlyAvailable=true`)
        .then(r => r.json())
        .then(data => {

            tr._data = data; // lưu cache

            ingSelect.innerHTML =
                `<option value="">-- Chọn nguyên liệu --</option>` +
                data.map(x =>
                    `<option value="${x.ingredientId}">
                        ${x.name}
                    </option>`
                ).join("");
        });

    // 🔥 ON CHANGE INGREDIENT
    ingSelect.addEventListener("change", () => {
        const ingId = Number(ingSelect.value);
        const item = tr._data.find(x => x.ingredientId == ingId);

        if (!item) return;

        // ✅ BASE UNIT
        tr.querySelector(".unitName").value = item.unitName;

        // ✅ STOCK
        tr.querySelector(".stock").value = item.stock;

        // 🔥 GÁN hidden unitId (base unit)
        tr.dataset.unitId = item.baseUnitId;
    });
}

function loadUnits(tr) {
    const ingId = tr.querySelector(".ingredient").value;

    fetch(`/api/admin/inventory-documents/units?ingredientId=${ingId}`)
        .then(r => r.json())
        .then(data => {
            const unitSelect = tr.querySelector(".unit");

            unitSelect.innerHTML =
                `<option value="">-- Chọn đơn vị --</option>` +
                data.map(x =>
                    `<option value="${x.unitId}">${x.name}</option>`
                ).join("");
        });
}