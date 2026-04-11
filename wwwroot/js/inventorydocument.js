let ingredientsBySupplier = [];

function reloadData() {
    const keyword = document.getElementById("search").value;
    const type = document.getElementById("filterType").value;
    const fromDate = document.getElementById("fromDate").value;
    const toDate = document.getElementById("toDate").value;

    const url = `/Admin/AdminInventoryDocument?keyword=${keyword}&type=${type}&fromDate=${fromDate}&toDate=${toDate}`;

    window.location.href = url;
}

function clearFilter() {
    document.getElementById("search").value = "";
    document.getElementById("filterType").value = "";
    document.getElementById("fromDate").value = "";
    document.getElementById("toDate").value = "";

    reloadData();
}


// debounce tránh spam request
function debounce(fn, delay) {
    let t;
    return function () {
        clearTimeout(t);
        t = setTimeout(fn, delay);
    }
}
function toast(msg, ok = true) {
    const t = document.createElement("div");
    t.className = `toast-item ${ok ? "success" : "error"}`;
    t.innerText = msg;
    document.getElementById("toast").appendChild(t);
    setTimeout(() => t.remove(), 3000);
}

function formatVND(n) {
    return Number(n || 0).toLocaleString("vi-VN");
}

function bindSelect(id, data, value, text, placeholder) {
    const el = document.getElementById(id);

    el.innerHTML =
        `<option value="">${placeholder}</option>` +
        data.map(x => `<option value="${x[value]}">${x[text]}</option>`).join("");
}

function openDetail(id) {
    fetch(`/Admin/AdminInventoryDocument/GetDetail?id=${id}`)
        .then(r => r.json())
        .then(res => {
            if (!res.success) {
                toast("Không tìm thấy dữ liệu", false);
                return;
            }

            const d = res.data;

            let html = `
                <p><b>Mã:</b> ${d.code}</p>
                <p><b>Kho:</b> ${d.storeName}</p>
                <p><b>Nhân viên:</b> ${d.staffName}</p>
                <p><b>Nhà cung cấp:</b> ${d.supplierName || ""}</p>
                <p><b>Loại:</b> ${renderType(d.type)}</p>
                <p><b>Ngày:</b> ${new Date(d.date).toLocaleDateString("vi-VN")}</p>
                <p><b>Ghi chú:</b> ${d.note || ""}</p>

                <table class="table">
                    <tr>
                        <th>Nguyên liệu</th>
                        <th>Số lượng</th>
                        <th>Đơn vị</th>
                        <th>Giá</th>
                        <th>Ghi chú</th>
                    </tr>
            `;

            d.details.forEach(x => {
                html += `
                    <tr>
                        <td>${x.ingredientName}</td>
                        <td>${x.quantity}</td>
                        <td>${x.unitName}</td>
                        <td>${formatVND(x.unitPrice)}</td>
                        <td>${x.note || ""}</td>
                    </tr>
                `;
            });

            html += `</table>`;

            document.getElementById("detailContent").innerHTML = html;
            document.getElementById("detailModal").style.display = "block";
        });
}

function renderType(type) {
    if (type === "IMPORT") return "Nhập kho";
    if (type === "EXPORT") return "Xuất kho";
    return "Hủy kho";
}

function openCreateModal() {
    fetch("/Admin/AdminInventoryDocument/GetCreateData")
        .then(r => r.json())
        .then(data => {

            bindSelect("store", data.stores, "storeId", "name", "-- Chọn Kho --");
            bindSelect("staff", data.staffs, "staffId", "fullName", "-- Chọn Nhân Viên --");
            bindSelect("supplier", data.suppliers, "supplierId", "name", "-- Chọn Nhà Cung Cấp --");

            document.getElementById("documentDate").value = new Date().toISOString().slice(0, 16);

            document.querySelector("#detailTable tbody").innerHTML = "";
            document.getElementById("grandTotal").innerText = "0";

            document.getElementById("createModal").style.display = "block";
        });
}

function onTypeChange() {
    const type = document.getElementById("type").value;
    const supplierBox = document.getElementById("supplierBox");

    if (type === "IMPORT") {
        supplierBox.style.display = "block";
    } else {
        supplierBox.style.display = "none";
        ingredientsBySupplier = [];
    }
}

function loadIngredientsBySupplier() {
    const supplierId = document.getElementById("supplier").value;

    if (!supplierId) {
        ingredientsBySupplier = [];
        return;
    }

    fetch(`/Admin/AdminInventoryDocument/GetIngredientSuppliersBySupplier?supplierId=${supplierId}`)
        .then(r => r.json())
        .then(data => {
            ingredientsBySupplier = data;
        });
}

function addRow() {
    const type = document.getElementById("type").value;

    // IMPORT → cần supplier
    if (type === "IMPORT" && ingredientsBySupplier.length === 0) {
        toast("Vui lòng chọn nhà cung cấp trước", false);
        return;
    }

    const tr = document.createElement("tr");

    tr.innerHTML = `
        <td><select class="ingredient"></select></td>
        <td><select class="unit"></select></td>
        <td><input type="number" class="qty" value="1"></td>
        <td><input class="price"></td>
        <td><span class="rowTotal">0</span></td>
        <td><input class="note"></td>
        <td><button onclick="this.closest('tr').remove(); calcTotal()">X</button></td>
    `;

    document.querySelector("#detailTable tbody").appendChild(tr);

    const ingSelect = tr.querySelector(".ingredient");

    if (type === "IMPORT") {
        // IMPORT → theo supplier
        ingSelect.innerHTML =
            `<option value="">-- Chọn nguyên liệu --</option>` +
            ingredientsBySupplier.map(x =>
                `<option value="${x.ingredientId}">${x.ingredient.name}</option>`
            ).join("");
    } else {
        // EXPORT → load theo kho
        loadIngredientsByStore(ingSelect);
    }

    ingSelect.addEventListener("change", () => loadUnitPrice(tr));
    tr.querySelector(".qty").addEventListener("input", () => calcRow(tr));

    loadUnitPrice(tr);
}

function filterByType(type) {
    document.getElementById("filterType").value = type;
    reloadData();
}

function loadUnitPrice(tr) {
    const type = document.getElementById("type").value;
    const ingId = tr.querySelector(".ingredient").value;
    const supplierId = document.getElementById("supplier").value;

    if (!ingId) return;

    if (type === "IMPORT") {
        // IMPORT → lấy từ supplier (readonly)
        fetch(`/Admin/AdminInventoryDocument/GetUnitsWithPrice?ingredientId=${ingId}&supplierId=${supplierId}`)
            .then(r => r.json())
            .then(data => {

                const unitSelect = tr.querySelector(".unit");

                unitSelect.innerHTML =
                    `<option value="">-- Chọn đơn vị --</option>` +
                    data.map(x =>
                        `<option value="${x.unitId}" data-price="${x.price}">${x.name}</option>`
                    ).join("");

                unitSelect.addEventListener("change", () => updatePrice(tr));

                tr.querySelector(".price").readOnly = true;

                updatePrice(tr);
            });
    } else {
        // EXPORT → lấy tất cả unit, giá nhập tay
        fetch(`/Admin/AdminInventoryDocument/GetUnits?ingredientId=${ingId}`)
            .then(r => r.json())
            .then(data => {

                const unitSelect = tr.querySelector(".unit");

                unitSelect.innerHTML =
                    `<option value="">-- Chọn đơn vị --</option>` +
                    data.map(x =>
                        `<option value="${x.unitId}">${x.name}</option>`
                    ).join("");

                tr.querySelector(".price").readOnly = false;
                tr.querySelector(".price").value = 0;
            });
    }
}

function updatePrice(tr) {
    const unit = tr.querySelector(".unit");
    const price = unit.selectedOptions[0]?.dataset.price || 0;

    tr.querySelector(".price").value = price;

    calcRow(tr);
}

function calcRow(tr) {
    const qtyInput = tr.querySelector(".qty");
    const qty = parseFloat(qtyInput.value || 0);

    const unit = tr.querySelector(".unit");
    const price = parseFloat(tr.querySelector(".price").value || 0);

    const ingSelect = tr.querySelector(".ingredient");
    const selectedOption = ingSelect.selectedOptions[0];
    const stock = parseFloat(selectedOption?.dataset.stock || 0);

    const type = document.getElementById("type").value;

    // 🚨 CHECK EXPORT
    if (type !== "IMPORT" && qty > stock) {
        toast(`Vượt tồn kho! Chỉ còn ${stock}`, false);

        qtyInput.value = stock;
        return calcRow(tr); // tính lại
    }

    const total = qty * price;

    tr.querySelector(".rowTotal").innerText = formatVND(total);

    calcTotal();
}

function calcTotal() {
    let sum = 0;

    document.querySelectorAll(".rowTotal").forEach(x => {
        const raw = x.innerText.replace(/\./g, "");
        sum += parseFloat(raw || 0);
    });

    document.getElementById("grandTotal").innerText = formatVND(sum);
}

function submitForm() {

    const rows = document.querySelectorAll("#detailTable tbody tr");

    if (rows.length === 0) {
        toast("Phải có ít nhất 1 dòng", false);
        return;
    }

    const model = {
        storeId: +document.getElementById("store").value,
        staffId: +document.getElementById("staff").value,
        supplierId: document.getElementById("type").value === "IMPORT"
            ? (document.getElementById("supplier").value || null)
            : null,
        type: document.getElementById("type").value,
        documentDate: new Date(document.getElementById("documentDate").value).toISOString(),
        note: document.getElementById("note").value,
        details: []
    };

    rows.forEach(tr => {
        model.details.push({
            ingredientId: +tr.querySelector(".ingredient").value,
            unitId: +tr.querySelector(".unit").value,
            quantity: +tr.querySelector(".qty").value,
            unitPrice: +tr.querySelector(".price").value,
            note: tr.querySelector(".note").value
        });
    });

    fetch("/Admin/AdminInventoryDocument/Create", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(model)
    })
        .then(r => r.json())
        .then(res => {
            if (res.success) {
                toast("Tạo thành công");
                location.reload();
            } else {
                toast(res.message, false);
            }
        });
}

function closeModal(id) {
    document.getElementById(id).style.display = "none";
}

function loadIngredientsByStore(selectEl) {
    const storeId = document.getElementById("store").value;

    if (!storeId) {
        toast("Chọn kho trước", false);
        return;
    }

    fetch(`/Admin/AdminInventoryDocument/GetIngredientsByStore?storeId=${storeId}`)
        .then(r => r.json())
        .then(data => {

            selectEl.innerHTML =
                `<option value="">-- Chọn nguyên liệu --</option>` +
                data.map(x =>
                    `<option value="${x.ingredientId}" data-stock="${x.stock}">
                        ${x.name} (Tồn: ${x.stock})
                     </option>`
                ).join("");
        });
}