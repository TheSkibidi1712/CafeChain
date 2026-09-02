let currentSizeId = 0;
let currentSizeName = ""; // 🔥 thêm dòng này
let selectedDrinkId = 0;
let drinkModalInstance = null;
let priceModalInstance = null;
const sizeCatalog = window.CafeChainUiCatalog.read('sizeUiCatalog');
const sizeText = (key, values) => window.CafeChainUiCatalog.text(sizeCatalog, key, values);
const sizeLocale = document.documentElement.dataset.culture || 'vi-VN';
const sizeNumber = new Intl.NumberFormat(sizeLocale);
const sizeCurrency = new Intl.NumberFormat(sizeLocale, { style: 'currency', currency: 'VND', maximumFractionDigits: 0 });

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
        return sizeText('Size.Cup');
    }

    if (normalized === '2') {
        return sizeText('Size.Volume');
    }

    return sizeText('Size.Js.Unknown');
}

// =========================
// RENDER UI (QUAN TRỌNG)
// =========================
function renderDrinkUI(data) {

    let assignedHtml = "";
    let unassignedHtml = "";

    if (!data || data.length === 0) {
        document.getElementById("assignedList").innerHTML = `<div class="text-center py-4 text-muted small"><i class="fas fa-info-circle me-1"></i>${sizeText('Size.Js.NoDrinkData')}</div>`;
        document.getElementById("unassignedList").innerHTML = `<div class="text-center py-4 text-muted small"><i class="fas fa-info-circle me-1"></i>${sizeText('Size.Js.NoDrinkData')}</div>`;
        return;
    }

    data.forEach(d => {

        let image = d.imageUrl || "/Images/DrinkImages/no-image.jpg";
        let canAssign = d.canAssign !== false;
        let assignmentBlockReason = d.assignmentBlockReason || sizeText('Size.Js.Incompatible');
        let formattedPrice = sizeCurrency.format(Number(d.price || 0));

        // ===== ĐÃ GÁN =====
        if (d.isAssigned) {
            assignedHtml += `
                <div class="drink-card">
                    <img src="${image}" class="drink-img" alt="${d.name}" />
                    <div class="drink-info">
                        <h6 class="fw-bold text-dark mb-1 text-truncate" title="${d.name}">${d.name}</h6>
                        <small class="text-muted d-block text-truncate mb-1">${d.categoryName || sizeText('Size.Js.Other')} · ${d.productTypeName || ""}</small>
                        <div class="d-flex align-items-center gap-2 mt-1">
                            <span class="badge bg-light text-dark border fw-semibold">${formattedPrice}</span>
                            <a href="/Admin/AdminDrinkProfitability" class="small text-decoration-none text-primary" title="${sizeText('Size.Js.Profitability')}">
                                <i class="fas fa-edit me-1"></i>${sizeText('Size.Js.Price')}
                            </a>
                        </div>
                    </div>
                    <div class="drink-action">
                        <button class="btn btn-sm ${d.active ? "btn-outline-danger" : "btn-outline-success"} text-nowrap px-2.5 py-1.5"
                                onclick="toggleDrinkSize(${d.drinkSizeId})"
                                title="${d.active ? sizeText('Size.Js.DisableHint') : sizeText('Size.Js.EnableHint')}">
                            <i class="fas ${d.active ? "fa-ban" : "fa-check"} me-1"></i>${d.active ? sizeText('Size.Js.Disable') : sizeText('Size.Js.Enable')}
                        </button>
                    </div>
                </div>
            `;
        }

        // ===== CHƯA GÁN =====
        else {
            unassignedHtml += `
                <div class="drink-card">
                    <img src="${image}" class="drink-img" alt="${d.name}" />
                    <div class="drink-info">
                        <h6 class="fw-bold text-dark mb-1 text-truncate" title="${d.name}">${d.name}</h6>
                        <small class="text-muted d-block text-truncate mb-1">${d.categoryName || sizeText('Size.Js.Other')} · ${d.productTypeName || ""}</small>
                        ${!canAssign ? `<small class="text-danger d-block text-truncate" title="${assignmentBlockReason}"><i class="fas fa-exclamation-triangle me-1"></i>${assignmentBlockReason}</small>` : ''}
                    </div>
                    <div class="drink-action">
                        ${
                            canAssign
                                ? `<button class="btn btn-sm btn-orange text-nowrap px-3 py-1.5"
                                           onclick="openPriceModal(${d.drinkId})">
                                       <i class="fas fa-plus me-1"></i>${sizeText('Size.Js.Assign')}
                                   </button>`
                                : `<button class="btn btn-sm disabled-action-btn text-nowrap px-2 py-1.5"
                                           disabled title="${assignmentBlockReason}">
                                       <i class="fas fa-lock me-1"></i>${sizeText('Size.Js.Invalid')}
                                   </button>`
                        }
                    </div>
                </div>
            `;
        }
    });

    if (!assignedHtml) {
        assignedHtml = `<div class="text-center py-4 text-muted small"><i class="fas fa-inbox d-block fa-2x mb-2 opacity-50"></i>${sizeText('Size.Js.NoAssigned')}</div>`;
    }

    if (!unassignedHtml) {
        unassignedHtml = `<div class="text-center py-4 text-muted small"><i class="fas fa-check-circle d-block fa-2x mb-2 text-success opacity-50"></i>${sizeText('Size.Js.AllAssigned')}</div>`;
    }

    document.getElementById("assignedList").innerHTML = assignedHtml;
    document.getElementById("unassignedList").innerHTML = unassignedHtml;

    // chỉ tạo modal 1 lần
    if (!drinkModalInstance) {
        drinkModalInstance = bootstrap.Modal.getOrCreateInstance(document.getElementById('drinkModal'));
    }

    drinkModalInstance.show();
}
// =========================
// OPEN DRINK MODAL
// =========================
function openDrinkModal(sizeId, sizeName) {
    currentSizeId = sizeId;
    currentSizeName = sizeName;

    const titleEl = document.getElementById("drinkModalTitle");
    if (titleEl) {
        titleEl.innerText = sizeText('Size.Js.ManageTitle', { name: sizeName });
    }

    const badgeEl = document.getElementById("currentSizeBadge");
    if (badgeEl) {
        badgeEl.innerText = `Size: ${sizeName}`;
    }

    fetch(`/Admin/AdminSize/GetDrinks?sizeId=${sizeId}`)
        .then(res => {
            if (!res.ok) {
                return res.text().then(err => { throw new Error(err); });
            }
            return res.json();
        })
        .then(data => renderDrinkUI(data))
        .catch(err => toast(err.message || sizeText('Size.Js.LoadError'), "error"));
}

// =========================
// OPEN PRICE MODAL (PREMIUM)
// =========================
function openPriceModal(drinkId) {
    selectedDrinkId = drinkId;
    const input = document.getElementById("priceInput");
    if (input) {
        input.value = "";
        updatePricePreview(0);
    }

    if (!priceModalInstance) {
        priceModalInstance = bootstrap.Modal.getOrCreateInstance(document.getElementById('priceModal'));
    }

    priceModalInstance.show();
    setTimeout(() => input?.focus(), 300);
}

function selectPresetPrice(val) {
    const input = document.getElementById("priceInput");
    if (input) {
        input.value = val;
        updatePricePreview(val);
    }
}

function updatePricePreview(val) {
    const preview = document.getElementById("priceFormattedPreview");
    if (!preview) return;
    const num = Number(val || 0);
    preview.innerText = sizeNumber.format(num) + " VND";
}

document.addEventListener("DOMContentLoaded", function () {
    const input = document.getElementById("priceInput");
    if (input) {
        input.addEventListener("input", function () {
            updatePricePreview(this.value);
        });
    }
});

// =========================
// CONFIRM ASSIGN
// =========================
function confirmAssign() {
    const priceVal = document.getElementById("priceInput")?.value;
    const price = Number(priceVal);

    if (isNaN(price) || price < 0 || priceVal === "") {
        toast(sizeText('Size.Js.InvalidPrice'), "warning");
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
            priceModalInstance?.hide();
            toast(sizeText('Size.Js.AssignSuccess'), "success");
            return fetch(`/Admin/AdminSize/GetDrinks?sizeId=${currentSizeId}`);
        })
        .then(res => res.json())
        .then(data => renderDrinkUI(data))
        .catch(err => toast(err.message || sizeText('Size.Js.AssignError'), "error"));
}
// =========================
// EDIT PRICE INLINE
// =========================
function toggleEdit(drinkSizeId) {
    window.location.href = '/Admin/AdminDrinkProfitability';
}

// =========================
// TOGGLE ACTIVE
// =========================
async function toggleDrinkSize(id) {
    if (window.Swal) {
        const result = await window.Swal.fire({
            title: sizeText('Size.Js.ConfirmTitle'),
            text: sizeText('Size.Js.DrinkStatusConfirm'),
            icon: 'warning',
            showCancelButton: true,
            confirmButtonColor: '#70482f',
            cancelButtonColor: '#6c757d',
            confirmButtonText: sizeText('Size.Js.Confirm'),
            cancelButtonText: sizeText('Common.Cancel')
        });
        if (!result.isConfirmed) return;
    } else if (!confirm(sizeText('Size.Js.DrinkStatusConfirm'))) {
        return;
    }

    fetch(`/Admin/AdminSize/ToggleDrinkSize?id=${id}`, {
        method: 'POST',
        headers: { 'RequestVerificationToken': getSizeAntiForgeryToken() }
    })
        .then(res => {
            if (!res.ok) {
                return res.text().then(err => { throw new Error(err); });
            }

            // ✅ CHỈ TOAST KHI REQUEST THÀNH CÔNG
            toast(sizeText('Size.Js.StatusSuccess'), "success");

            return fetch(`/Admin/AdminSize/GetDrinks?sizeId=${currentSizeId}`);
        })
        .then(res => {
            if (!res.ok) {
                return res.text().then(err => { throw new Error(err); });
            }
            return res.json();
        })
        .then(data => renderDrinkUI(data))
        .catch(err => toast(err.message || sizeText('Size.Js.UpdateError'), "error"));
}

// =========================
// TOGGLE SIZE
// =========================
async function toggleSize(id) {
    if (window.Swal) {
        const result = await window.Swal.fire({
            title: sizeText('Size.Js.ConfirmTitle'),
            text: sizeText('Size.Js.SizeStatusConfirm'),
            icon: 'warning',
            showCancelButton: true,
            confirmButtonColor: '#70482f',
            cancelButtonColor: '#6c757d',
            confirmButtonText: sizeText('Size.Js.Confirm'),
            cancelButtonText: sizeText('Common.Cancel')
        });
        if (!result.isConfirmed) return;
    } else if (!confirm(sizeText('Size.Js.SizeStatusConfirm'))) {
        return;
    }

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

            try {
                sessionStorage.setItem('toast_message', sizeText('Size.Js.StatusSuccess'));
                sessionStorage.setItem('toast_type', 'success');
            } catch (e) {
                // Fallback
            }
            location.reload();
        })
        .catch(err => toast(err.message || sizeText('Size.Js.UpdateError'), "error"));
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
        toast(sizeText('Size.Js.CodeRequired'), "warning");
        return false;
    }

    if (sizeCode.length > 20) {
        toast(sizeText('Size.Js.CodeMax'), "warning");
        return false;
    }

    if (!name) {
        toast(sizeText('Size.Js.NameRequired'), "warning");
        return false;
    }

    if (name.length > 50) {
        toast(sizeText('Size.Js.NameMax'), "warning");
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
            message: window.AdminFeedback.resolveMessage(
                { message: text },
                { status: response.status, fallback: sizeText('Size.Js.InvalidResponse') })
        };
    }

    const result = await response.json();

    if (!response.ok && result.success !== false) {
        return {
            success: false,
            message: window.AdminFeedback.resolveMessage(result, {
                status: response.status,
                fallback: sizeText('Size.Js.ActionFailed')
            })
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
            toast(sizeText('Size.Js.NotFound'), "warning");
            return;
        }

        if (!validateSizeForm(sizeCode, name.trim())) {
            form.querySelector(":invalid")?.focus();
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

            toast(result.message || sizeText('Size.Js.UpdateSuccess'), "success");

            const modal = bootstrap.Modal.getInstance(
                document.getElementById("editModal")
            );

            modal?.hide();

            setTimeout(() => {
                location.reload();
            }, 700);
        } catch (err) {
            toast(err.message || window.AdminFeedback.actionFallback("update", "size"), "error");
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
        if (!name.value.trim()) return toast(sizeText('Size.Js.AiNameRequired'), 'error');
        controller?.abort();
        controller = new AbortController();
        const timeout = setTimeout(() => controller.abort(), 15000);
        const original = button.innerHTML;
        button.disabled = true;
        button.innerHTML = `<i class="fas fa-spinner fa-spin me-1"></i>${sizeText('Size.Js.AiLoading')}`;
        clear();
        try {
            const response = await fetch('/Admin/AdminSize/AiSuggestion', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getSizeAntiForgeryToken() },
                body: JSON.stringify({ name: name.value.trim(), description: description.value.trim(), sizeType: Number(sizeType.value) }),
                signal: controller.signal
            });
            const result = await response.json();
            if (!response.ok || !result.success) throw new Error(result.message || sizeText('Size.Js.AiFailed'));
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
        if (code.value.trim() && !window.confirm(sizeText('Size.Js.AiOverwriteCode'))) return;
        code.value = suggestion.sizeCode;
        clear();
        toast(sizeText('Size.Js.AiCodeApplied'), 'success');
    });
    document.getElementById('btnDismissSizeAi').addEventListener('click', clear);
    [name, description].forEach(x => x.addEventListener('input', clear));
    sizeType.addEventListener('change', clear);
});

document.addEventListener('DOMContentLoaded', function initFullSizeAiSuggestion() {
    const button = document.getElementById('btnSizeAiSuggestion');
    if (!button) return;
    const idea = document.getElementById('sizeAiIdea');
    const generationMode = document.getElementById('sizeAiGenerationMode');
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
            title.textContent = option.title || fields.name || sizeText('Size.Js.AiTitle');
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
        button.innerHTML = `<i class="fas fa-spinner fa-spin me-1"></i>${sizeText('Size.Js.AiLoading')}`;
        clear();
        try {
            const currentSizeType = normalizeSizeTypeValue(sizeType.value);
            const response = await fetch('/Admin/AdminSize/AiSuggestion', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getSizeAntiForgeryToken() },
                body: JSON.stringify({
                    generationMode: Number(generationMode?.value || 0),
                    idea: idea.value.trim() || null,
                    currentSizeCode: code.value.trim() || null,
                    currentName: name.value.trim() || null,
                    currentDescription: description.value.trim() || null,
                    currentSizeType: currentSizeType ? Number(currentSizeType) : null
                }), signal: activeController.signal
            });
            const result = await response.json();
            if (!response.ok || !result.success) throw new Error(result.message || sizeText('Size.Js.AiFailed'));
            renderOptions(result);
        } catch (error) {
            if (error.name !== 'AbortError') toast(error.message, 'error');
        } finally {
            clearTimeout(timeout);
            if (controller === activeController) { button.disabled = false; button.innerHTML = original; }
        }
    });
    applyButton.addEventListener('click', () => {
        if (!selectedOption?.canApply) return toast(sizeText('Size.Js.AiChooseValid'), 'error');
        const suggestion = selectedOption.fields;
        const suggestedSizeType = normalizeSizeTypeValue(suggestion.sizeType);
        if (!suggestedSizeType) {
            return toast(sizeText('Size.Js.AiInvalidType'), 'error');
        }
        if ([name, code, description].some(x => x.value.trim()) || sizeType.value) {
            if (!window.confirm(sizeText('Size.Js.AiOverwrite'))) return;
        }
        name.value = suggestion.name;
        code.value = suggestion.sizeCode;
        description.value = suggestion.description;
        sizeType.value = suggestedSizeType;
        clear();
        toast(sizeText('Size.Js.AiApplied'), 'success');
    });
    document.getElementById('btnDismissSizeAi').addEventListener('click', clear);
    const invalidate = () => { controller?.abort(); clear(); };
    [idea, name, code, description].forEach(x => x.addEventListener('input', invalidate));
    generationMode?.addEventListener('change', invalidate);
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
            toast(sizeText('Size.Js.TypeRequired'), "warning");
            sizeTypeElement.focus();
            return;
        }

        const sizeType = Number(sizeTypeValue);

        if (!validateSizeForm(sizeCode, name)) {
            createForm.querySelector(":invalid")?.focus();
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

            toast(result.message || sizeText('Size.Js.CreateSuccess'), "success");

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
            toast(err.message || window.AdminFeedback.actionFallback("create", "size"), "error");
        }
    });

    document.getElementById("createModal")?.addEventListener("hidden.bs.modal", function () {
        createForm.reset();
        document.getElementById("create-size-type").value = "";
        document.getElementById("sizeAiSuggestionPanel")?.classList.add("d-none");
    });
});
