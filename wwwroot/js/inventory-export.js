export function addExportRow() {
    const storeId = document.getElementById("store").value;

    if (!storeId) {
        toast("Chọn kho trước", "error");
        return;
    }

    const tr = document.createElement("tr");

    tr.innerHTML = `
        <td>
            <select class="ingredient">
                <option value="">-- Chọn nguyên liệu --</option>
            </select>
        </td>

        <td>
            <select class="unit">
                <option value="">-- Đơn vị --</option>
            </select>
        </td>

        <td>
            <input type="number" class="qty" value="1" min="0">
        </td>

        <!-- EXPORT KHÔNG DÙNG GIÁ -->
        <td class="col-price" style="display:none"></td>
        <td class="col-total" style="display:none"></td>

        <td>
            <input class="note">
        </td>

        <td>
            <button type="button" onclick="this.closest('tr').remove(); calcTotal()">X</button>
        </td>
    `;

    document.querySelector("#detailTable tbody").appendChild(tr);

    const ingSelect = tr.querySelector(".ingredient");
    const unitSelect = tr.querySelector(".unit");
    const qtyInput = tr.querySelector(".qty");

    // ================= LOAD NGUYÊN LIỆU =================
    fetch(`/api/admin/inventory-documents/store-inventories?storeId=${storeId}&onlyAvailable=true`)
        .then(r => r.json())
        .then(data => {

            ingSelect.innerHTML =
                `<option value="">-- Chọn nguyên liệu --</option>` +
                data.map(x =>
                    `<option value="${x.ingredientId}" 
                             data-stock="${x.stock}"
                             data-unit="${x.baseUnitId}"
                             data-unit-name="${x.unitName}">
                        ${x.name} (Tồn: ${x.stock})
                    </option>`
                ).join("");
        })
        .catch(() => {
            toast("Không tải được nguyên liệu", "error");
        });

    // ================= CHỌN NGUYÊN LIỆU =================
   ingSelect.addEventListener("change", () => {
       const ingId = ingSelect.value;

       if (!ingId) {
           unitSelect.innerHTML = `<option value="">-- Đơn vị --</option>`;
           return;
       }

       fetch(`/api/admin/inventory-documents/units?ingredientId=${ingId}`)
           .then(r => r.json())
           .then(data => {
               unitSelect.innerHTML =
                   `<option value="">-- Chọn đơn vị --</option>` +
                   data.map(x =>
                       `<option value="${x.unitId}">${x.name}</option>`
                   ).join("");
           })
           .catch(() => toast("Không tải được đơn vị", "error"));
   });

    // ================= VALIDATE SỐ LƯỢNG =================
    qtyInput.addEventListener("input", () => {
        const selected = ingSelect.selectedOptions[0];

        if (!selected || !selected.value) return;

        const stock = Number(selected.getAttribute("data-stock") || 0);
        let qty = Number(qtyInput.value || 0);

        if (qty < 0) {
            qty = 0;
        }

        if (qty > stock) {
            toast("Số lượng vượt tồn kho", "error");
            qty = stock;
        }

        qtyInput.value = qty;

        // vẫn gọi để đồng bộ (dù export không dùng tiền)
        calcRow(tr);
    });
}

// ================= LOAD UNIT (OPTIONAL) =================
export function loadExportUnits(tr) {
    const ingId = tr.querySelector(".ingredient").value;

    if (!ingId) return;

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
            toast("Không tải được đơn vị", "error");
        });
}