export function loadStockTakeTable() {
    const storeId = document.getElementById("store").value;

    if (!storeId) {
        console.warn("Chưa chọn kho");
        return;
    }

    fetch(`/api/admin/inventory-documents/store-inventories?storeId=${storeId}`)
        .then(async r => {
            if (!r.ok) {
                const err = await r.json();
                throw new Error(err.message || "API error");
            }
            return r.json();
        })
        .then(data => {
            if (!Array.isArray(data)) {
                console.error("Data không phải array:", data);
                return;
            }

            const tbody = document.querySelector("#detailTable tbody");
            tbody.innerHTML = "";

            data.forEach(x => {
                const tr = document.createElement("tr");

                tr.innerHTML = `
                <td>
                    <input type="hidden" class="ingredient" value="${x.ingredientId}" />
                    ${x.name}
                </td>

                <td>
                    ${x.unitName}
                    <input type="hidden" class="unit" value="${x.baseUnitId}" />
                </td>

                <td>
                    <span class="stock">${x.stock}</span>
                </td>

                <td>
                    <input type="number" class="qty realQty" min="0">
                </td>

                <td>
                    <span class="diff">0</span>
                </td>

                <td>
                    <input class="note">
                </td>

                <td></td>
            `;

                tbody.appendChild(tr);

                tr.querySelector(".realQty")
                    .addEventListener("input", () => calcDiff(tr));
            });
        })
        .catch(err => {
            console.error("Load inventory lỗi:", err.message);
        });
}
export function addStockTakeRow() {
    // ❌ Không dùng nữa nhưng giữ để không crash import
    console.warn("StockTake dùng loadStockTakeTable(), không dùng addRow");
}

function calcDiff(tr) {
    const stock = parseFloat(tr.querySelector(".stock").innerText || 0);
    const real = parseFloat(tr.querySelector(".realQty").value || 0);

    const diff = real - stock; // ✅ tính trước

    const diffEl = tr.querySelector(".diff");

    diffEl.innerText = isNaN(diff) ? 0 : diff;

    // 🎨 màu trực quan
    diffEl.style.color =
        diff > 0 ? "green" :
            diff < 0 ? "red" :
                "black";
}