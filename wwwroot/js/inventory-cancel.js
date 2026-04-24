export function addWasteRow() {
    const storeId = Number(document.getElementById("store").value);

    if (!storeId) {
        toast("Chọn kho trước", "error");
        return;
    }

    const tr = document.createElement("tr");

    tr.innerHTML = `
        <td><select class="ingredient"></select></td>
        <td><input class="unitName" readonly /></td>
        <td><input type="number" class="qty" value="1" min="1"></td>
        <td><input class="stock" readonly /></td>
        <td><input class="note"></td>
        <td class="col-action">
            <button type="button" class="btn-remove-row">✕</button>
        </td>
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
        })
        .catch(() => {
            toast("Không tải được tồn kho", "error");
        });

    const qtyInput = tr.querySelector(".qty");

    // ✅ GẮN 1 LẦN DUY NHẤT
    qtyInput.addEventListener("input", () => {
        const stock = Number(tr.querySelector(".stock").value || 0);
        let qty = Number(qtyInput.value || 0);

        if (qty > stock) {
            toast("Số lượng vượt tồn kho", "error");
            qty = stock;
        }

        if (qty < 1) qty = 1;

        qtyInput.value = qty;
    });
    
    // 🔥 ON CHANGE INGREDIENT
    ingSelect.addEventListener("change", () => {
        const ingId = Number(ingSelect.value);
        if (!tr._data) return;

        const item = tr._data.find(x => x.ingredientId === ingId);

        if (!item) {
            tr.querySelector(".unitName").value = "";
            tr.querySelector(".stock").value = 0;
            tr.querySelector(".qty").value = 1;
            tr.dataset.unitId = "";
            return;
        }

        // ✅ BASE UNIT
        tr.querySelector(".unitName").value = item.unitName;

        // ✅ STOCK
        tr.querySelector(".stock").value = item.stock;

        // 🔥 GÁN hidden unitId (base unit)
        tr.dataset.unitId = item.baseUnitId;
    });

    tr.querySelector("button").addEventListener("click", () => {
        tr.remove();

    });
}

function loadUnits(tr) {
    const ingId = Number(tr.querySelector(".ingredient").value);

    fetch(`/api/admin/inventory-documents/units?ingredientId=${ingId}`)
        .then(r => r.json())
        .then(data => {
            const unitSelect = tr.querySelector(".unit");

            unitSelect.innerHTML =
                `<option value="">-- Chọn đơn vị --</option>` +
                data.map(x =>
                    `<option value="${x.unitId}">${x.name}</option>`
                ).join("");
        })
        .catch(() => {
            toast("Không tải được tồn kho", "error");
        });
}