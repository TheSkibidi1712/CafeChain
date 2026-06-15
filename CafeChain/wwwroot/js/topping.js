let currentToppingId = 0;
let currentToppingName = "";
let selectedDrinkId = 0;
let drinkModalInstance = null;


// =========================
// OPEN MODAL
// =========================
function openDrinkModal(toppingId, toppingName) {
    currentToppingId = toppingId;
    currentToppingName = toppingName;

    document.getElementById("drinkModalTitle").innerText =
        `Quản lý Drink - Topping ${toppingName}`;

    document.getElementById("currentToppingBadge").innerText =
        `Topping: ${toppingName}`;

    fetch(`/Admin/AdminTopping/GetDrinks?toppingId=${toppingId}`)
        .then(res => res.json())
        .then(data => renderDrinkUI(data));
}

// =========================
// RENDER UI
// =========================
function renderDrinkUI(data) {

    let assignedHtml = "";
    let unassignedHtml = "";

    data.forEach(d => {

        let image = d.imageUrl ?? "/images/no-image.png";

        let card = `
            <div class="col-6">
                <div class="drink-card p-2">
                    <img src="${image}" class="drink-img mb-2"/>
                    <h6 class="mb-1">${d.name}</h6>
                    <small class="text-muted">${d.categoryName ?? ""} - ${d.productTypeName ?? ""}</small>
            `;

        if (d.isAssigned) {
            assignedHtml += card + `
                    <div class="mt-2">

                        <button class="btn btn-sm btn-outline-danger w-100"
                                onclick="toggleDrinkTopping(${d.drinkToppingId})">
                            ${d.active ? "Tắt" : "Bật"}
                        </button>

                    </div>
                </div>
            </div>`;
        } else {
            unassignedHtml += card + `
                    <button class="btn btn-sm btn-orange w-100 mt-2"
                            onclick="assignTopping(${d.drinkId})">
                        Gán
                    </button>
                </div>
            </div>`;
        }
    });

    document.getElementById("assignedList").innerHTML = assignedHtml;
    document.getElementById("unassignedList").innerHTML = unassignedHtml;

    if (!drinkModalInstance) {
        drinkModalInstance = new bootstrap.Modal(document.getElementById('drinkModal'));
    }

    drinkModalInstance.show();
}

// =========================
// ASSIGN
// =========================
function assignTopping(drinkId) {

    fetch('/Admin/AdminTopping/Assign', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            drinkId: drinkId,
            toppingId: currentToppingId
        })
    })
        .then(res => {
            if (!res.ok) {
                return res.text().then(err => { throw new Error(err); });
            }
        })
        .then(() => {
            toast("Gán thành công", "success");

            return fetch(`/Admin/AdminTopping/GetDrinks?toppingId=${currentToppingId}`);
        })
        .then(res => res.json())
        .then(data => renderDrinkUI(data))
        .catch(err => toast(err.message || "Lỗi khi gán", "error"));
}

// =========================
// TOGGLE
// =========================
function toggleDrinkTopping(id) {
    fetch(`/Admin/AdminTopping/Toggle?id=${id}`, {
        method: 'POST'
    })
        .then(res => {
            if (!res.ok) {
                return res.text().then(err => { throw new Error(err); });
            }

            toast("Đã cập nhật trạng thái", "success");

            return fetch(`/Admin/AdminTopping/GetDrinks?toppingId=${currentToppingId}`);
        })
        .then(res => res.json())
        .then(data => renderDrinkUI(data))
        .catch(err => toast(err.message || "Lỗi cập nhật", "error"));
}

// ========================
// TOGGLE TOPPING
// ========================
function toggleTopping(id) {
    fetch(`/Admin/AdminTopping/ToggleStatus?id=${id}`, {
        method: 'POST'
    })
        .then(res => {
            if (!res.ok) {
                return res.text().then(err => { throw new Error(err); });
            }

            toast("Đã cập nhật trạng thái", "success");

            setTimeout(() => {
                location.reload();
            }, 800);
        })
        .catch(err => toast(err.message || "Lỗi cập nhật", "error"));
}

// ===== CREATE =====
function previewCreateImage(event) {
    const file = event.target.files[0];
    if (!file) return;

    const img = document.getElementById('create-preview');
    const btn = document.getElementById('create-remove-btn');

    img.src = URL.createObjectURL(file);
    img.classList.remove('d-none');
    btn.classList.remove('d-none');
}

function removeCreateImage() {
    const input = document.getElementById('create-image-input');
    const img = document.getElementById('create-preview');
    const btn = document.getElementById('create-remove-btn');

    input.value = "";
    img.src = "";
    img.classList.add('d-none');
    btn.classList.add('d-none');
}

// ===== EDIT =====
function previewEditImage(event) {
    const file = event.target.files[0];
    if (!file) return;

    const img = document.getElementById('edit-preview');

    img.src = URL.createObjectURL(file);
}

function removeEditImage() {
    const input = document.getElementById('edit-image-input');
    const img = document.getElementById('edit-preview');

    input.value = "";
    img.src = "/Images/DrinkImages/no-image.jpg"; // fallback
}

function openEditModal(id, name, price, image) {
    document.getElementById('edit-id').value = id;
    document.getElementById('edit-name').value = name;
    document.getElementById('edit-price').value = price;
    document.getElementById('edit-old-image').value = image;

    const img = document.getElementById('edit-preview');
    img.src = image ? image : "/Images/DrinkImages/no-image.jpg";
}