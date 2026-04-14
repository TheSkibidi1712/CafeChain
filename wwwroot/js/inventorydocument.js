let ingredientsBySupplier = [];
let currentStoreId = 0;
let currentStaffId = 0;

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

function openCreateModal() {
    fetch("/Admin/AdminInventoryDocument/GetCreateData")
        .then(r => r.json())
        .then(data => {

            document.getElementById("storeName").value = data.storeName;
            document.getElementById("staffName").value = data.staffName;

            currentStoreId = data.storeId;
            currentStaffId = data.staffId;

            const supplier = document.getElementById("supplier");
            supplier.innerHTML =
                `<option value="">-- Chọn Nhà Cung Cấp --</option>` +
                data.suppliers.map(x =>
                    `<option value="${x.supplierId}">${x.name}</option>`
                ).join("");

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
        ingSelect.innerHTML =
            `<option value="">-- Chọn nguyên liệu --</option>` +
            ingredientsBySupplier.map(x =>
                `<option value="${x.ingredientId}">${x.ingredient.name}</option>`
            ).join("");
    } else {
        loadIngredientsByStore(ingSelect);
    }

    ingSelect.addEventListener("change", () => loadUnitPrice(tr));
    tr.querySelector(".qty").addEventListener("input", () => calcRow(tr));
}

function loadIngredientsByStore(selectEl) {
    if (!currentStoreId) {
        toast("Không xác định kho", false);
        return;
    }

    fetch(`/Admin/AdminInventoryDocument/GetIngredientsByStore?storeId=${currentStoreId}`)
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

function loadUnitPrice(tr) {
    const type = document.getElementById("type").value;
    const ingId = tr.querySelector(".ingredient").value;
    const supplierId = document.getElementById("supplier").value;

    if (!ingId) return;

    if (type === "IMPORT") {
        fetch(`/Admin/AdminInventoryDocument/GetImportInfo?ingredientId=${ingId}&supplierId=${supplierId}`)
            .then(r => r.json())
            .then(data => {
                const unitSelect = tr.querySelector(".unit");

                unitSelect.innerHTML = `<option value="${data.unitId}">${data.unitName}</option>`;
                unitSelect.disabled = true;

                const priceInput = tr.querySelector(".price");
                priceInput.value = data.price;
                priceInput.readOnly = true;

                calcRow(tr);
            });
    } else {
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

function calcRow(tr) {
    const qtyInput = tr.querySelector(".qty");
    let qty = parseFloat(qtyInput.value || 0);

    const price = parseFloat(tr.querySelector(".price").value || 0);
    const ingSelect = tr.querySelector(".ingredient");
    const stock = parseFloat(ingSelect.selectedOptions[0]?.dataset.stock || 0);
    const type = document.getElementById("type").value;

    if (type !== "IMPORT" && qty > stock) {
        toast(`Vượt tồn kho! Chỉ còn ${stock}`, false);
        qty = stock;
        qtyInput.value = stock;
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
        storeId: currentStoreId,
        staffId: currentStaffId,
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