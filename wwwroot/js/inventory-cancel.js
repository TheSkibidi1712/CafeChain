export function addWasteRow() {
    const storeId = document.getElementById("store").value;

    if (!storeId) {
        toast("Chọn kho trước", false);
        return;
    }

    const tr = document.createElement("tr");

    tr.innerHTML = `
        <td><select class="ingredient"></select></td>
        <td><select class="unit"></select></td>
        <td><input type="number" class="qty" value="1"></td>
        <td><input class="note"></td>
        <td><button onclick="this.closest('tr').remove()">X</button></td>
    `;

    document.querySelector("#detailTable tbody").appendChild(tr);

    const ingSelect = tr.querySelector(".ingredient");

    fetch(`/api/admin/inventory-documents/store-ingredients?storeId=${storeId}`)
        .then(r => r.json())
        .then(data => {
            ingSelect.innerHTML =
                `<option value="">-- Chọn nguyên liệu --</option>` +
                data.map(x =>
                    `<option value="${x.ingredientId}">
                        ${x.name} (Tồn: ${x.stock})
                    </option>`
                ).join("");
        });

    ingSelect.addEventListener("change", () => loadUnits(tr));
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