let currentSizeId = 0;
let currentSizeName = ""; // 🔥 thêm dòng này
let selectedDrinkId = 0;
let drinkModalInstance = null;
let priceModalInstance = null;

// =========================
// RENDER UI (QUAN TRỌNG)
// =========================
function renderDrinkUI(data) {

    let assignedHtml = "";
    let unassignedHtml = "";

    data.forEach(d => {

        let image = d.imageUrl ?? "/Images/DrinkImages/no-image.jpg";
        let canAssign = d.canAssign !== false;
        let assignmentBlockReason = d.assignmentBlockReason ?? "Size không phù hợp với loại sản phẩm";

        let card = `
            <div class="col-6">
                <div class="drink-card p-2">
                    <img src="${image}" class="drink-img mb-2"/>
                    <h6 class="mb-1">${d.name}</h6>
                    <small class="text-muted">${d.categoryName ?? ""} - ${d.productTypeName ?? ""}</small>
            `;

        // ===== ĐÃ GÁN =====
        if (d.isAssigned) {
            assignedHtml += card + `
                    <div class="mt-2">

                        <input type="number"
                               id="price-${d.drinkSizeId}"
                               value="${d.price ?? 0}"
                               class="form-control form-control-sm mb-2"
                               disabled />

                        <button class="btn btn-sm btn-primary w-100 mb-1"
                                id="btn-edit-${d.drinkSizeId}"
                                onclick="toggleEdit(${d.drinkSizeId})">
                            Chỉnh sửa
                        </button>

                        <button class="btn btn-sm btn-outline-danger w-100"
                                onclick="toggleDrinkSize(${d.drinkSizeId})">
                            ${d.active ? "Tắt" : "Bật"}
                        </button>

                    </div>
                </div>
            </div>`;
        }

        // ===== CHƯA GÁN =====
        else {
            unassignedHtml += card + `
                    ${
                        canAssign
                            ? `<button class="btn btn-sm btn-orange w-100 mt-2"
                                       onclick="openPriceModal(${d.drinkId})">
                                   Gán
                               </button>`
                            : `<button class="btn btn-sm btn-secondary w-100 mt-2"
                                       disabled>
                                   Không phù hợp
                               </button>
                               <small class="text-danger d-block mt-1">
                                   ${assignmentBlockReason}
                               </small>`
                    }
                </div>
            </div>`;
        }
    });

    document.getElementById("assignedList").innerHTML = assignedHtml;
    document.getElementById("unassignedList").innerHTML = unassignedHtml;

    // chỉ tạo modal 1 lần
    if (!drinkModalInstance) {
        drinkModalInstance = new bootstrap.Modal(document.getElementById('drinkModal'));
    }

    drinkModalInstance.show();
}
// =========================
// OPEN DRINK MODAL
// =========================
function openDrinkModal(sizeId, sizeName) {
    currentSizeId = sizeId;
    currentSizeName = sizeName;

    document.getElementById("drinkModalTitle").innerText =
        `Quản lý Drink - Size ${sizeName}`;

    document.getElementById("currentSizeBadge").innerText =
        `Size: ${sizeName}`;

    fetch(`/Admin/AdminSize/GetDrinks?sizeId=${sizeId}`)
        .then(res => {
            if (!res.ok) {
                return res.text().then(err => { throw new Error(err); });
            }
            return res.json();
        })
        .then(data => renderDrinkUI(data))
        .catch(err => toast(err.message || "Lỗi tải dữ liệu", "error"));
}

// =========================
// OPEN PRICE MODAL
// =========================

function openPriceModal(drinkId) {
    selectedDrinkId = drinkId;

    document.getElementById("priceInput").value = "";

    if (!priceModalInstance) {
        priceModalInstance = new bootstrap.Modal(document.getElementById('priceModal'));
    }

    priceModalInstance.show();
}

// =========================
// CONFIRM ASSIGN
// =========================
function confirmAssign() {
    let price = document.getElementById("priceInput").value;

    if (!price || price <= 0) {
        toast("Giá không hợp lệ", "error");
        return;
    }

    fetch('/Admin/AdminSize/AssignDrink', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            drinkId: selectedDrinkId,
            sizeId: currentSizeId,
            price: price
        })
    })
        .then(res => {
            if (!res.ok) {
                return res.text().then(err => { throw new Error(err); });
            }
        })
        .then(() => {
            priceModalInstance.hide();
            toast("Gán thành công", "success");

            return fetch(`/Admin/AdminSize/GetDrinks?sizeId=${currentSizeId}`);
        })
        .then(res => res.json())
        .then(data => renderDrinkUI(data))
        .catch(err => toast(err.message || "Lỗi khi gán", "error"));
}
// =========================
// EDIT PRICE INLINE
// =========================
function toggleEdit(drinkSizeId) {
    let input = document.getElementById(`price-${drinkSizeId}`);
    let btn = document.getElementById(`btn-edit-${drinkSizeId}`);

    if (input.disabled) {
        input.disabled = false;
        input.focus();
        btn.innerText = "Lưu";
        btn.classList.replace("btn-primary", "btn-success");
    } else {
        let price = input.value;

        if (!price || price <= 0) {
            toast("Giá không hợp lệ", "error");
            return;
        }

        fetch(`/Admin/AdminSize/UpdatePrice?drinkSizeId=${drinkSizeId}&price=${price}`, {
            method: 'POST'
        })
            .then(() => {
                input.disabled = true;
                btn.innerText = "Chỉnh sửa";
                btn.classList.replace("btn-success", "btn-primary");

                toast("Cập nhật thành công");
            });
    }
}

// =========================
// TOGGLE ACTIVE
// =========================
function toggleDrinkSize(id) {
    fetch(`/Admin/AdminSize/ToggleDrinkSize?id=${id}`, {
        method: 'POST'
    })
        .then(res => {
            if (!res.ok) {
                return res.text().then(err => { throw new Error(err); });
            }

            // ✅ CHỈ TOAST KHI REQUEST THÀNH CÔNG
            toast("Đã cập nhật trạng thái", "success");

            return fetch(`/Admin/AdminSize/GetDrinks?sizeId=${currentSizeId}`);
        })
        .then(res => {
            if (!res.ok) {
                return res.text().then(err => { throw new Error(err); });
            }
            return res.json();
        })
        .then(data => renderDrinkUI(data))
        .catch(err => toast(err.message || "Lỗi cập nhật", "error"));
}

// =========================
// TOGGLE SIZE
// =========================
function toggleSize(id) {
    fetch(`/Admin/AdminSize/ToggleStatus?id=${id}`, {
        method: 'POST'
    })
        .then(res => {
            if (!res.ok) {
                return res.text().then(err => { throw new Error(err); });
            }

            toast("Đã cập nhật trạng thái", "success");

            // ⏳ delay để toast kịp hiển thị
            setTimeout(() => {
                location.reload();
            }, 500); // 500-1000ms là đẹp
        })
        .catch(err => toast(err.message || "Lỗi cập nhật", "error"));
}


// =========================
// EDIT SIZE
// =========================
function openEditModal(id, name, description, sizeType) {
    // reset trước để tránh giữ dữ liệu cũ
    document.getElementById('edit-id').value = "";
    document.getElementById('edit-name').value = "";
    document.getElementById('edit-description').value = "";
    document.getElementById('edit-size-type').value = "1";

    // set dữ liệu mới
    document.getElementById('edit-id').value = id || 0;
    document.getElementById('edit-name').value = name || "";
    document.getElementById('edit-description').value = description || "";
    document.getElementById('edit-size-type').value = String(sizeType || 1);
}

document.addEventListener("DOMContentLoaded", function () {

    const form = document.getElementById("editSizeForm");

    form.addEventListener("submit", function (e) {
        e.preventDefault();

        const sizeId = document.getElementById("edit-id").value;
        const name = document.getElementById("edit-name").value;
        const description = document.getElementById("edit-description").value;
        const sizeType = Number(document.getElementById("edit-size-type").value);

        if (!sizeId || sizeId <= 0) {
            toast("Không tìm thấy size", "error");
            return;
        }

        fetch('/Admin/AdminSize/Edit', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                sizeId: sizeId,
                name: name,
                description: description,
                sizeType: sizeType
            })
        })
            .then(res => {
                if (!res.ok) {
                    return res.text().then(err => {
                        throw new Error(err);
                    });
                }

                return res.text();
            })
            .then(() => {
                toast("Cập nhật size thành công", "success");

                const modal = bootstrap.Modal.getInstance(
                    document.getElementById("editModal")
                );

                modal.hide();

                setTimeout(() => {
                    location.reload();
                }, 700);
            })
            .catch(err => {
                toast(err.message || "Lỗi cập nhật", "error");
            });
    });

});

// =========================
// CREATE SIZE
// =========================

document.addEventListener("DOMContentLoaded", function () {

    const createForm = document.getElementById("createSizeForm");

    createForm.addEventListener("submit", function (e) {
        e.preventDefault();

        const name = document.getElementById("create-name").value.trim();
        const description = document.getElementById("create-description").value.trim();
        const sizeType = Number(document.getElementById("create-size-type").value);

        if (!name) {
            toast("Tên size không được để trống", "error");
            return;
        }

        fetch('/Admin/AdminSize/Create', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({
                name: name,
                description: description,
                sizeType: sizeType
            })
        })
            .then(res => {
                if (!res.ok) {
                    return res.text().then(err => {
                        throw new Error(err);
                    });
                }

                return res.text();
            })
            .then(() => {
                toast("Tạo size thành công", "success");

                document.getElementById("create-name").value = "";
                document.getElementById("create-description").value = "";
                document.getElementById("create-size-type").value = "1";

                const modal = bootstrap.Modal.getInstance(
                    document.getElementById("createModal")
                );

                modal.hide();

                setTimeout(() => {
                    location.reload();
                }, 700);
            })
            .catch(err => {
                toast(err.message || "Lỗi tạo size", "error");
            });
    });

});
