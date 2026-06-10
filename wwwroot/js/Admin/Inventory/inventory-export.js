import { getAveragePrice, getUnits } from "./inventory-api.js";
export function addExportRow() {
    const storeId = Number(document.getElementById("store").value);

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
            <input type="number" class="qty" value="1" min="1">
        </td>

        <!-- EXPORT KHÔNG DÙNG GIÁ -->
        <td class="col-price" style="display:none"></td>
        <td class="col-total" style="display:none"></td>

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
    ingSelect.addEventListener("change", async () => {
        const ingId = Number(ingSelect.value);

        if (!ingId) {
            unitSelect.innerHTML = `<option value="">-- Đơn vị --</option>`;
            tr.dataset.price = 0;
            return;
        }

        // ================= PARALLEL REQUEST (CHUẨN HƠN) =================
        const [units, price] = await Promise.all([
            getUnits(ingId),
            getAveragePrice(storeId, ingId)
        ]);

        // ================= UNIT =================
        unitSelect.innerHTML =
            `<option value="">-- Chọn đơn vị --</option>` +
            units.map(x =>
                `<option value="${x.unitId}">${x.name}</option>`
            ).join("");

        // ================= PRICE =================
        tr.dataset.price = price;
    });

    // ================= VALIDATE SỐ LƯỢNG =================
    qtyInput.addEventListener("input", () => {
        const selected = ingSelect.selectedOptions[0];

        if (!selected || !selected.value) return;

        const stock = +selected.dataset.stock || 0;
        let qty = Number(qtyInput.value || 0);

        if (qty < 0) {
            qty = 0;
        }

        if (qty > stock) {
            toast("Số lượng vượt tồn kho", "error");
            qty = stock;
        }

        qtyInput.value = qty;

    });

    tr.querySelector("button").addEventListener("click", () => {
        tr.remove();

    });
}

// ================= LOAD UNIT (OPTIONAL) =================
export function loadExportUnits(tr) {
    const ingId = Number(tr.querySelector(".ingredient").value);

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