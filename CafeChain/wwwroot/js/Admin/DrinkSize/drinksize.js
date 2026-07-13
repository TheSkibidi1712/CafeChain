let currentSizeId = 0;
let currentSizeName = ""; // 🔥 thêm dòng này
let selectedDrinkId = 0;
let drinkModalInstance = null;
let priceModalInstance = null;

function getSizeAntiForgeryToken() {
    return document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
}

function normalizeSizeTypeValue(value) {
    const normalized = String(value ?? '').trim().toLowerCase();

    if (normalized === '1' || normalized === 'cup') {
        return '1';
    }

    if (normalized === '2' || normalized === 'volume') {
        return '2';
    }

    return '';
}

function getSizeTypeLabel(value) {
    const normalized = normalizeSizeTypeValue(value);

    if (normalized === '1') {
        return 'Ly';
    }

    if (normalized === '2') {
        return 'Dung tích';
    }

    return 'Chưa xác định';
}

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
        headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getSizeAntiForgeryToken() },
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
            method: 'POST',
            headers: { 'RequestVerificationToken': getSizeAntiForgeryToken() }
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
        method: 'POST',
        headers: { 'RequestVerificationToken': getSizeAntiForgeryToken() }
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
        method: 'POST',
        headers: {
            'Accept': 'application/json',
            'RequestVerificationToken': getSizeAntiForgeryToken()
        }
    })
        .then(readJsonResult)
        .then(result => {
            if (!result.success) {
                throw new Error(result.message);
            }

            toast(result.message || "Đã cập nhật trạng thái", "success");

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
function openEditModal(id, sizeCode, name, description, sizeType) {
    // reset trước để tránh giữ dữ liệu cũ
    document.getElementById('edit-id').value = "";
    document.getElementById('edit-code').value = "";
    document.getElementById('edit-name').value = "";
    document.getElementById('edit-description').value = "";
    document.getElementById('edit-size-type').value = "1";

    // set dữ liệu mới
    document.getElementById('edit-id').value = id || 0;
    document.getElementById('edit-code').value = sizeCode || "";
    document.getElementById('edit-name').value = name || "";
    document.getElementById('edit-description').value = description || "";
    document.getElementById('edit-size-type').value = String(sizeType || 1);
}

function validateSizeForm(sizeCode, name) {
    if (!sizeCode) {
        toast("Mã size không được để trống", "error");
        return false;
    }

    if (sizeCode.length > 20) {
        toast("Mã size tối đa 20 ký tự", "error");
        return false;
    }

    if (!name) {
        toast("Tên size không được để trống", "error");
        return false;
    }

    if (name.length > 50) {
        toast("Tên size tối đa 50 ký tự", "error");
        return false;
    }

    return true;
}

async function readJsonResult(response) {
    const contentType = response.headers.get("content-type") || "";

    if (!contentType.toLowerCase().includes("application/json")) {
        const text = await response.text();

        return {
            success: false,
            message: text || "Phản hồi từ máy chủ không hợp lệ"
        };
    }

    const result = await response.json();

    if (!response.ok && result.success !== false) {
        return {
            success: false,
            message: result.message || "Có lỗi xảy ra"
        };
    }

    return result;
}

function postJson(url, payload) {
    return fetch(url, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'Accept': 'application/json',
            'RequestVerificationToken': getSizeAntiForgeryToken()
        },
        body: JSON.stringify(payload)
    }).then(readJsonResult);
}

document.addEventListener("DOMContentLoaded", function () {

    const form = document.getElementById("editSizeForm");
    if (!form) {
        return;
    }

    form.addEventListener("submit", async function (e) {
        e.preventDefault();

        const sizeId = document.getElementById("edit-id").value;
        const sizeCode = document.getElementById("edit-code").value.trim().toUpperCase();
        const name = document.getElementById("edit-name").value;
        const description = document.getElementById("edit-description").value;
        const sizeType = Number(document.getElementById("edit-size-type").value);

        if (!sizeId || sizeId <= 0) {
            toast("Không tìm thấy size", "error");
            return;
        }

        if (!validateSizeForm(sizeCode, name.trim())) {
            return;
        }

        try {
            const result = await postJson('/Admin/AdminSize/Edit', {
                sizeId: sizeId,
                sizeCode: sizeCode,
                name: name,
                description: description,
                sizeType: sizeType
            });

            if (!result.success) {
                throw new Error(result.message);
            }

            toast(result.message || "Cập nhật size thành công", "success");

            const modal = bootstrap.Modal.getInstance(
                document.getElementById("editModal")
            );

            modal?.hide();

            setTimeout(() => {
                location.reload();
            }, 700);
        } catch (err) {
            toast(err.message || "Lỗi cập nhật", "error");
        }
    });

});

document.addEventListener("DOMContentLoaded", function initSizeAiSuggestion() {
    const button = document.getElementById('btnSizeAiSuggestionLegacy');
    if (!button) return;
    const name = document.getElementById('create-name');
    const description = document.getElementById('create-description');
    const sizeType = document.getElementById('create-size-type');
    const code = document.getElementById('create-code');
    const panel = document.getElementById('sizeAiSuggestionPanel');
    let suggestion = null;
    let controller = null;
    const clear = () => { suggestion = null; panel.classList.add('d-none'); };

    button.addEventListener('click', async () => {
        if (!name.value.trim()) return toast('Vui lòng nhập tên size.', 'error');
        controller?.abort();
        controller = new AbortController();
        const timeout = setTimeout(() => controller.abort(), 15000);
        const original = button.innerHTML;
        button.disabled = true;
        button.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i>Đang gợi ý...';
        clear();
        try {
            const response = await fetch('/Admin/AdminSize/AiSuggestion', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getSizeAntiForgeryToken() },
                body: JSON.stringify({ name: name.value.trim(), description: description.value.trim(), sizeType: Number(sizeType.value) }),
                signal: controller.signal
            });
            const result = await response.json();
            if (!response.ok || !result.success) throw new Error(result.message || 'Không thể tạo gợi ý.');
            suggestion = result.data;
            document.getElementById('sizeAiCode').textContent = suggestion.sizeCode;
            panel.classList.remove('d-none');
        } catch (error) {
            if (error.name !== 'AbortError') toast(error.message, 'error');
        } finally {
            clearTimeout(timeout);
            button.disabled = false;
            button.innerHTML = original;
        }
    });
    document.getElementById('btnApplySizeAi').addEventListener('click', () => {
        if (!suggestion) return;
        if (code.value.trim() && !window.confirm('Ghi đè mã size hiện tại?')) return;
        code.value = suggestion.sizeCode;
        clear();
        toast('Đã điền mã gợi ý. Dữ liệu chưa được lưu.', 'success');
    });
    document.getElementById('btnDismissSizeAi').addEventListener('click', clear);
    [name, description].forEach(x => x.addEventListener('input', clear));
    sizeType.addEventListener('change', clear);
});

document.addEventListener('DOMContentLoaded', function initFullSizeAiSuggestion() {
    const button = document.getElementById('btnSizeAiSuggestion');
    if (!button) return;
    const idea = document.getElementById('sizeAiIdea');
    const name = document.getElementById('create-name');
    const code = document.getElementById('create-code');
    const description = document.getElementById('create-description');
    const sizeType = document.getElementById('create-size-type');
    const panel = document.getElementById('sizeAiSuggestionPanel');
    const optionList = document.getElementById('sizeAiOptionList');
    const applyButton = document.getElementById('btnApplySizeAi');
    const warnings = document.getElementById('sizeAiWarnings');
    let selectedOption = null;
    let controller = null;
    const clear = () => {
        selectedOption = null;
        optionList.replaceChildren();
        warnings.textContent = '';
        applyButton.disabled = true;
        panel.classList.add('d-none');
    };
    const selectOption = (option, card) => {
        selectedOption = option;
        optionList.querySelectorAll('.ai-option-card').forEach(x => x.classList.remove('is-selected'));
        card.classList.add('is-selected');
        applyButton.disabled = !option.canApply;
    };
    const renderOptions = result => {
        optionList.replaceChildren();
        (result.options || []).slice(0, 3).forEach(option => {
            const fields = option.fields || {};
            const card = document.createElement('button');
            card.type = 'button';
            card.className = 'ai-option-card text-start';
            const title = document.createElement('strong');
            title.textContent = option.title || fields.name || 'Gợi ý size';
            const meta = document.createElement('div');
            meta.className = 'small text-muted mt-1';
            meta.textContent = `${fields.sizeCode || ''} · ${getSizeTypeLabel(fields.sizeType)}`;
            const descriptionText = document.createElement('div');
            descriptionText.className = 'small mt-2';
            descriptionText.textContent = fields.description || '';
            card.append(title, meta, descriptionText);
            card.addEventListener('click', () => selectOption(option, card));
            optionList.appendChild(card);
        });
        warnings.textContent = (result.warnings || []).join(' ');
        document.getElementById('sizeAiSource').textContent = result.usedOllama ? 'Ollama + C#' : 'C# fallback';
        panel.classList.remove('d-none');
    };

    button.addEventListener('click', async () => {
        controller?.abort();
        controller = new AbortController();
        const activeController = controller;
        const timeout = setTimeout(() => activeController.abort(), 130000);
        const original = button.innerHTML;
        button.disabled = true;
        button.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i>Đang gợi ý...';
        clear();
        try {
            const currentSizeType = normalizeSizeTypeValue(sizeType.value);
            const response = await fetch('/Admin/AdminSize/AiSuggestion', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getSizeAntiForgeryToken() },
                body: JSON.stringify({
                    idea: idea.value.trim() || null,
                    currentSizeCode: code.value.trim() || null,
                    currentName: name.value.trim() || null,
                    currentDescription: description.value.trim() || null,
                    currentSizeType: currentSizeType ? Number(currentSizeType) : null
                }), signal: activeController.signal
            });
            const result = await response.json();
            if (!response.ok || !result.success) throw new Error(result.message || 'Không thể tạo gợi ý.');
            renderOptions(result);
        } catch (error) {
            if (error.name !== 'AbortError') toast(error.message, 'error');
        } finally {
            clearTimeout(timeout);
            if (controller === activeController) { button.disabled = false; button.innerHTML = original; }
        }
    });
    applyButton.addEventListener('click', () => {
        if (!selectedOption?.canApply) return toast('Vui lòng chọn một gợi ý hợp lệ.', 'error');
        const suggestion = selectedOption.fields;
        const suggestedSizeType = normalizeSizeTypeValue(suggestion.sizeType);
        if (!suggestedSizeType) {
            return toast('Loại size do AI gợi ý không hợp lệ. Vui lòng chọn gợi ý khác.', 'error');
        }
        if ([name, code, description].some(x => x.value.trim()) || sizeType.value) {
            if (!window.confirm('Ghi đè dữ liệu size hiện tại bằng gợi ý AI?')) return;
        }
        name.value = suggestion.name;
        code.value = suggestion.sizeCode;
        description.value = suggestion.description;
        sizeType.value = suggestedSizeType;
        clear();
        toast('Đã áp dụng gợi ý vào form. Vui lòng kiểm tra trước khi lưu.', 'success');
    });
    document.getElementById('btnDismissSizeAi').addEventListener('click', clear);
    const invalidate = () => { controller?.abort(); clear(); };
    [idea, name, code, description].forEach(x => x.addEventListener('input', invalidate));
    sizeType.addEventListener('change', invalidate);
});

// =========================
// CREATE SIZE
// =========================

document.addEventListener("DOMContentLoaded", function () {

    const createForm = document.getElementById("createSizeForm");
    if (!createForm) {
        return;
    }

    createForm.addEventListener("submit", async function (e) {
        e.preventDefault();

        const name = document.getElementById("create-name").value.trim();
        const sizeCode = document.getElementById("create-code").value.trim().toUpperCase();
        const description = document.getElementById("create-description").value.trim();
        const sizeTypeElement = document.getElementById("create-size-type");
        const sizeTypeValue = normalizeSizeTypeValue(sizeTypeElement.value);

        if (!sizeTypeValue) {
            toast("Vui lòng chọn loại size", "error");
            sizeTypeElement.focus();
            return;
        }

        const sizeType = Number(sizeTypeValue);

        if (!validateSizeForm(sizeCode, name)) {
            return;
        }

        try {
            const result = await postJson('/Admin/AdminSize/Create', {
                sizeCode: sizeCode,
                name: name,
                description: description,
                sizeType: sizeType
            });

            if (!result.success) {
                throw new Error(result.message);
            }

            toast(result.message || "Tạo size thành công", "success");

            document.getElementById("create-code").value = "";
            document.getElementById("create-name").value = "";
            document.getElementById("create-description").value = "";
            document.getElementById("create-size-type").value = "";

            const modal = bootstrap.Modal.getInstance(
                document.getElementById("createModal")
            );

            modal?.hide();

            setTimeout(() => {
                location.reload();
            }, 700);
        } catch (err) {
            toast(err.message || "Lỗi tạo size", "error");
        }
    });

    document.getElementById("createModal")?.addEventListener("hidden.bs.modal", function () {
        createForm.reset();
        document.getElementById("create-size-type").value = "";
        document.getElementById("sizeAiSuggestionPanel")?.classList.add("d-none");
    });
});
