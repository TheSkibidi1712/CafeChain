// =====================================================
// COMMON HELPERS
// =====================================================

function getAntiForgeryToken() {

    return document.querySelector(
        'input[name="__RequestVerificationToken"]'
    )?.value;
}

function lockButton(btn, text) {

    if (!btn) return;

    btn.disabled = true;

    btn.innerHTML =
        `<i class="fas fa-spinner fa-spin me-1"></i>${text}`;
}

function unlockButton(btn, html) {

    if (!btn) return;

    btn.disabled = false;

    btn.innerHTML = html;
}

const createCategoryButtonHtml =
    '<i class="fas fa-save me-2"></i>Lưu danh mục';
const editCategoryButtonHtml =
    '<i class="fas fa-save me-2"></i>Cập nhật';

function restoreCategorySubmitButton(btn, html) {
    if (!btn) return;
    btn.removeAttribute("aria-busy");
    btn.innerHTML = html;
    btn.disabled = btn.dataset.canSubmit === "false";
}

function setCategorySubmitState(form, btn, isSubmitting, idleHtml) {
    if (!form || !btn) return;

    if (isSubmitting) {
        form.dataset.submitting = "true";
        btn.disabled = true;
        btn.setAttribute("aria-busy", "true");
        btn.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i>Đang lưu...';
        return;
    }

    window.AdminMutationGuard?.unlockForm?.(form);
    delete form.dataset.submitting;
    form.removeAttribute("data-submit-busy");
    form.removeAttribute("data-submit-pending");
    form.removeAttribute("aria-busy");
    btn.classList.remove("is-submitting");
    restoreCategorySubmitButton(btn, idleHtml);
}

function resolveCategoryError(result, response, action) {
    try {
        return window.AdminFeedback?.resolveMessage?.(result, {
            status: response?.status,
            action,
            entityName: "danh mục"
        }) || result?.message || "Không thể lưu danh mục.";
    } catch {
        return result?.message || "Không thể lưu danh mục.";
    }
}

function resolveCategoryNetworkError() {
    try {
        return window.AdminFeedback?.networkMessage?.() || "Không thể kết nối đến máy chủ.";
    } catch {
        return "Không thể kết nối đến máy chủ.";
    }
}

function resetCategoryForm(form, submitButtonId, submitButtonHtml) {
    if (!form) return;

    form.reset();
    delete form.dataset.submitting;
    form.querySelectorAll(".is-invalid").forEach(element =>
        element.classList.remove("is-invalid"));

    form.querySelectorAll("[data-category-icon-picker]").forEach(root => {
        const iconInput = root.querySelector("[data-icon-input]");
        const iconSearch = root.querySelector("[data-icon-search]");
        const iconGroups = root.querySelector("[data-icon-groups]");

        if (iconSearch) iconSearch.value = "";
        iconGroups?.replaceChildren();
        iconInput?.dispatchEvent(new Event("input", { bubbles: true }));
    });

    closeCategoryIconPickers();
    restoreCategorySubmitButton(
        document.getElementById(submitButtonId),
        submitButtonHtml
    );
}

function resetCategorySuggestions() {
    categorySuggestionController?.abort();
    categorySuggestionController = null;
    categorySuggestionInFlight = false;

    document.getElementById("categoryAiSuggestions")?.classList.add("d-none");
    document.getElementById("categoryAiSuggestionList")?.replaceChildren();

    const message = document.getElementById("categoryAiSuggestionMessage");
    if (message) {
        message.textContent = "Chọn một gợi ý để điền vào form:";
    }
}

function hideCategoryModal(modalId) {
    const modalElement = document.getElementById(modalId);
    if (!modalElement || !window.bootstrap?.Modal) {
        return Promise.resolve();
    }

    const modal = bootstrap.Modal.getOrCreateInstance(modalElement);
    if (!modalElement.classList.contains("show")) {
        modal.hide();
        return Promise.resolve();
    }

    return new Promise(resolve => {
        modalElement.addEventListener("hidden.bs.modal", resolve, { once: true });
        modal.hide();
    });
}

async function showCategoryAlert(message, icon = "success") {
    if (window.Swal) {
        const suspendedModal = icon === "error"
            ? document.querySelector(".modal.show")
            : null;

        // Không xếp SweetAlert chồng lên Bootstrap modal. Một số trình duyệt vẫn
        // giữ focus trap/backdrop của modal và chặn toàn bộ click vào alert.
        // Modal được ẩn tạm thời, nhưng cờ này giữ nguyên dữ liệu form.
        if (suspendedModal?.id && window.bootstrap?.Modal) {
            suspendedModal.dataset.preserveCategoryForm = "true";
            await hideCategoryModal(suspendedModal.id);
        }

        try {
            await window.Swal.fire({
                title: icon === "success" ? "Thành công" : "Không thể thực hiện",
                text: message,
                icon,
                confirmButtonColor: "#70482f",
                confirmButtonText: "Đóng",
                heightAuto: false,
                returnFocus: false,
                allowOutsideClick: false
            });
        } finally {
            if (suspendedModal?.id && window.bootstrap?.Modal) {
                delete suspendedModal.dataset.preserveCategoryForm;
                const modal = window.bootstrap.Modal.getOrCreateInstance(suspendedModal);
                const shown = new Promise(resolve => {
                    suspendedModal.addEventListener("shown.bs.modal", resolve, { once: true });
                });
                modal.show();
                await shown;

                const focusTarget = suspendedModal.querySelector(
                    "input:not([disabled]), textarea:not([disabled]), select:not([disabled]), button:not([disabled])"
                );
                focusTarget?.focus({ preventScroll: true });
            }
        }
        return;
    }

    toast(message, icon === "error" ? "error" : icon);
}

function buildCategoryFormData(form) {

    const formData = new FormData(form);

    const activeCheckbox =
        form.querySelector(
            'input[type="checkbox"][name="Active"]'
        );

    formData.delete("Active");

    formData.append(
        "Active",
        activeCheckbox?.checked ? "true" : "false"
    );

    return formData;
}

const categoryIconCatalog = [
    {
        name: "Đồ uống",
        items: [
            ["☕", "Cà phê"], ["🧋", "Trà sữa"], ["🍵", "Trà"],
            ["🥤", "Nước uống"], ["🥛", "Sữa"], ["🧃", "Nước ép"],
            ["🍹", "Nước trái cây"], ["🍸", "Mocktail"], ["🍺", "Đồ uống lạnh"]
        ]
    },
    {
        name: "Trái cây",
        items: [
            ["🍓", "Dâu"], ["🍊", "Cam"], ["🍋", "Chanh"], ["🍎", "Táo"],
            ["🍉", "Dưa hấu"], ["🥭", "Xoài"], ["🍑", "Đào"], ["🍒", "Anh đào"],
            ["🍍", "Dứa"], ["🥑", "Bơ"], ["🥥", "Dừa"]
        ]
    },
    {
        name: "Đồ ăn",
        items: [
            ["🥐", "Bánh sừng bò"], ["🥪", "Bánh mì kẹp"], ["🍞", "Bánh mì"],
            ["🥨", "Bánh xoắn"], ["🍫", "Sô-cô-la"], ["🥜", "Hạt"]
        ]
    },
    {
        name: "Bánh và kem",
        items: [
            ["🍨", "Kem ly"], ["🍦", "Kem"], ["🍰", "Bánh ngọt"],
            ["🧁", "Bánh cupcake"], ["🍪", "Bánh quy"], ["🍩", "Bánh vòng"]
        ]
    },
    {
        name: "Phổ biến",
        items: [
            ["⭐", "Nổi bật"], ["🔥", "Bán chạy"], ["🌿", "Thảo mộc"],
            ["❤️", "Yêu thích"], ["✨", "Theo mùa"], ["💚", "Tốt cho sức khỏe"],
            ["🧊", "Đá xay"], ["♨️", "Đồ uống nóng"]
        ]
    }
];

function normalizeIconSearch(value) {
    return String(value || "")
        .normalize("NFD")
        .replace(/\p{M}/gu, "")
        .toLocaleLowerCase("vi-VN");
}

function validateCategoryIcon(icon, showMessage = true) {
    const value = String(icon || "").trim();
    let message = "";

    if (!value) return true;
    if (value.length > 10) message = "Icon tối đa 10 ký tự.";
    else if (/[<>&]/u.test(value)) message = "Icon không được chứa HTML.";
    else {
        const segments = typeof Intl.Segmenter === "function"
            ? Array.from(new Intl.Segmenter("vi", { granularity: "grapheme" }).segment(value), item => item.segment)
            : Array.from(value);
        if (segments.length !== 1) message = "Chỉ được chọn một biểu tượng Unicode.";
        else {
            let containsSymbol = false;
            for (const character of value) {
                if (/\p{S}/u.test(character)) containsSymbol = true;
                else if (!/[\p{M}\u200D]/u.test(character)) {
                    message = "Icon phải là một biểu tượng Unicode, không phải chữ hoặc số.";
                    break;
                }
            }
            if (!message && !containsSymbol) message = "Icon phải là một biểu tượng Unicode hợp lệ.";
        }
    }

    if (message && showMessage) toast(message, "error");
    return !message;
}

function closeCategoryIconPickers(exceptRoot = null) {
    document.querySelectorAll("[data-category-icon-picker]").forEach(root => {
        if (root === exceptRoot) return;
        root.querySelector("[data-icon-panel]")?.classList.add("d-none");
        root.querySelector("[data-icon-toggle]")?.setAttribute("aria-expanded", "false");
    });
}

function initializeCategoryIconPicker(root) {
    const input = root.querySelector("[data-icon-input]");
    const preview = root.querySelector("[data-icon-preview]");
    const toggle = root.querySelector("[data-icon-toggle]");
    const clear = root.querySelector("[data-icon-clear]");
    const panel = root.querySelector("[data-icon-panel]");
    const search = root.querySelector("[data-icon-search]");
    const groups = root.querySelector("[data-icon-groups]");
    if (!input || !preview || !toggle || !clear || !panel || !search || !groups) return;

    const updatePreview = () => {
        const icon = input.value.trim();
        preview.textContent = icon || "—";
        preview.classList.toggle("is-empty", !icon);
    };

    const close = () => {
        panel.classList.add("d-none");
        toggle.setAttribute("aria-expanded", "false");
    };

    const render = () => {
        const keyword = normalizeIconSearch(search.value);
        groups.replaceChildren();
        categoryIconCatalog.forEach(group => {
            const items = group.items.filter(([icon, label]) =>
                !keyword || normalizeIconSearch(`${group.name} ${label} ${icon}`).includes(keyword));
            if (!items.length) return;

            const section = document.createElement("section");
            section.className = "category-icon-group";
            const title = document.createElement("div");
            title.className = "category-icon-group-title";
            title.textContent = group.name;
            const grid = document.createElement("div");
            grid.className = "category-icon-grid";
            items.forEach(([icon, label]) => {
                const option = document.createElement("button");
                option.type = "button";
                option.className = "category-icon-option";
                option.dataset.icon = icon;
                option.title = label;
                option.setAttribute("aria-label", `${label}: ${icon}`);
                option.textContent = icon;
                grid.appendChild(option);
            });
            section.append(title, grid);
            groups.appendChild(section);
        });

        if (!groups.children.length) {
            const empty = document.createElement("div");
            empty.className = "category-icon-no-result";
            empty.textContent = "Không tìm thấy biểu tượng phù hợp.";
            groups.appendChild(empty);
        }
    };

    toggle.addEventListener("click", () => {
        const willOpen = panel.classList.contains("d-none");
        closeCategoryIconPickers(willOpen ? root : null);
        panel.classList.toggle("d-none", !willOpen);
        toggle.setAttribute("aria-expanded", String(willOpen));
        if (willOpen) {
            render();
            search.focus();
        }
    });
    clear.addEventListener("click", () => {
        input.value = "";
        input.dispatchEvent(new Event("input", { bubbles: true }));
        input.focus();
    });
    search.addEventListener("input", render);
    groups.addEventListener("click", event => {
        const option = event.target.closest("[data-icon]");
        if (!option) return;
        input.value = option.dataset.icon || "";
        input.dispatchEvent(new Event("input", { bubbles: true }));
        close();
        input.focus();
    });
    input.addEventListener("input", updatePreview);
    input.addEventListener("blur", () => validateCategoryIcon(input.value, Boolean(input.value.trim())));
    document.addEventListener("click", event => {
        if (!root.contains(event.target)) close();
    });
    root.addEventListener("keydown", event => {
        if (event.key === "Escape") {
            close();
            toggle.focus();
        }
    });
    updatePreview();
}

// =====================================================
// VALIDATION
// =====================================================

function validateCategoryName(name) {

    if (!name || !name.trim()) {

        toast(
            "Tên danh mục không được để trống.",
            "warning"
        );

        return false;
    }

    const value = name.trim();

    if (value.length < 2) {

        toast(
            "Tên danh mục phải từ 2 ký tự trở lên.",
            "warning"
        );

        return false;
    }

    if (value.length > 100) {

        toast(
            "Tên danh mục tối đa 100 ký tự.",
            "warning"
        );

        return false;
    }

    return true;
}

function validateCategoryCode(code) {

    if (!code || !code.trim()) {

        toast(
            "Mã danh mục không được để trống.",
            "warning"
        );

        return false;
    }

    const value = code.trim();

    if (value.length < 2) {

        toast(
            "Mã danh mục phải từ 2 ký tự trở lên.",
            "warning"
        );

        return false;
    }

    if (value.length > 30) {

        toast(
            "Mã danh mục tối đa 30 ký tự.",
            "warning"
        );

        return false;
    }

    return true;
}

// =====================================================
// CREATE
// =====================================================

async function createCategory(form) {

    if (form.dataset.submitting === "true") return;

    const code =
    form.querySelector('[name="CategoryCode"]').value;

    const name =
        form.querySelector('[name="Name"]').value;

    const icon = form.querySelector('[name="Icon"]')?.value;

    if (!validateCategoryCode(code)) {
        form.querySelector('[name="CategoryCode"]')?.focus();
        return;
    }

    if (!validateCategoryName(name)) {
        form.querySelector('[name="Name"]')?.focus();
        return;
    }

    if (!validateCategoryIcon(icon)) {
        return;
    }

    const btn =
        document.getElementById(
            "btnCreateCategory"
        );

    let result = null;
    let response = null;
    let errorMessage = "";
    setCategorySubmitState(form, btn, true, createCategoryButtonHtml);

    try {
        response = await fetch(form.action, {
            method: "POST",
            body: buildCategoryFormData(form)
        });
        result = await response.json().catch(() => ({}));
        if (!response.ok || !result.success)
            errorMessage = resolveCategoryError(result, response, "create");
    } catch {
        errorMessage = resolveCategoryNetworkError();
    } finally {
        setCategorySubmitState(form, btn, false, createCategoryButtonHtml);
    }

    if (errorMessage) {
        await showCategoryAlert(errorMessage, "error");
        return;
    }

    await hideCategoryModal("createCategoryModal");
    resetCategorySuggestions();
    resetCategoryForm(form, "btnCreateCategory", createCategoryButtonHtml);
    await showCategoryAlert(result?.message || "Đã tạo danh mục.", "success");
    location.reload();
}

let categorySuggestionInFlight = false;
let categorySuggestionController = null;

async function suggestCategories() {
    const form = document.getElementById("createCategoryForm");
    const button = document.getElementById("btnSuggestCategories");
    const panel = document.getElementById("categoryAiSuggestions");
    const list = document.getElementById("categoryAiSuggestionList");
    if (!form || !button || !panel || !list || categorySuggestionInFlight) return;

    const originalHtml = button.innerHTML;
    const controller = new AbortController();
    categorySuggestionController = controller;
    categorySuggestionInFlight = true;
    lockButton(button, "Đang gợi ý...");
    try {
        const payload = {
            currentName: form.querySelector('[name="Name"]')?.value.trim() || null,
            currentCategoryCode: form.querySelector('[name="CategoryCode"]')?.value.trim() || null,
            currentIcon: form.querySelector('[name="Icon"]')?.value.trim() || null
        };
        const response = await fetch("/Admin/AdminCategory/AiSuggestions", {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "RequestVerificationToken": form.querySelector('[name="__RequestVerificationToken"]')?.value || ""
            },
            body: JSON.stringify(payload),
            signal: controller.signal
        });
        const result = await response.json().catch(() => ({}));
        if (!response.ok || !result.success) throw new Error(result.message || "Không thể tạo gợi ý.");

        const suggestionMessage = document.getElementById("categoryAiSuggestionMessage");
        if (suggestionMessage) {
            suggestionMessage.textContent = (result.data.warnings || []).length
                ? `Chọn một gợi ý để điền vào form. ${result.data.warnings.join(" ")}`
                : "Chọn một gợi ý để điền vào form:";
        }

        list.innerHTML = (result.data.options || []).map((option, index) => `
            <button type="button" class="category-ai-option" data-category-option="${index}">
                <span class="category-ai-option-icon">${escapeCategoryHtml(option.icon)}</span>
                <strong>${escapeCategoryHtml(option.name)}</strong>
                <span class="category-ai-option-code">${escapeCategoryHtml(option.categoryCode)}</span>
            </button>`).join("");
        list.querySelectorAll("[data-category-option]").forEach(optionButton => {
            optionButton.addEventListener("click", () => {
                const option = result.data.options[Number(optionButton.dataset.categoryOption)];
                form.querySelector('[name="Name"]').value = option.name;
                form.querySelector('[name="CategoryCode"]').value = option.categoryCode;
                const iconInput = form.querySelector('[name="Icon"]');
                iconInput.value = option.icon;
                iconInput.dispatchEvent(new Event("input", { bubbles: true }));
                const active = form.querySelector('input[type="checkbox"][name="Active"]');
                if (active) active.checked = true;
                panel.classList.add("d-none");
                list.innerHTML = "";
                toast("Đã điền gợi ý. Vui lòng kiểm tra trước khi lưu.", "success");
            });
        });
        panel.classList.remove("d-none");
    }
    catch (error) {
        if (error.name !== "AbortError") {
            toast(error.message || "Không thể tạo gợi ý danh mục.", "error");
        }
    }
    finally {
        if (categorySuggestionController === controller) {
            categorySuggestionController = null;
            categorySuggestionInFlight = false;
            unlockButton(button, originalHtml);
        }
    }
}

function escapeCategoryHtml(value) {
    const element = document.createElement("div");
    element.textContent = value || "";
    return element.innerHTML;
}

// =====================================================
// EDIT
// =====================================================

async function editCategory(form) {

    if (form.dataset.submitting === "true") return;

    const code =
    document.getElementById("editCategoryCode").value;

    const name =
        document.getElementById("editCategoryName").value;

    const icon = document.getElementById("editCategoryIcon").value;

    if (!validateCategoryCode(code)) {
        return;
    }

    if (!validateCategoryName(name)) {
        return;
    }

    if (!validateCategoryIcon(icon)) {
        return;
    }

    const btn =
        document.getElementById(
            "btnEditCategory"
        );

    let result = null;
    let response = null;
    let errorMessage = "";
    setCategorySubmitState(form, btn, true, editCategoryButtonHtml);

    try {
        response = await fetch(form.action, {
            method: "POST",
            body: buildCategoryFormData(form)
        });
        result = await response.json().catch(() => ({}));
        if (!response.ok || !result.success)
            errorMessage = resolveCategoryError(result, response, "update");
    } catch {
        errorMessage = resolveCategoryNetworkError();
    } finally {
        setCategorySubmitState(form, btn, false, editCategoryButtonHtml);
    }

    if (errorMessage) {
        await showCategoryAlert(errorMessage, "error");
        return;
    }

    await hideCategoryModal("editCategoryModal");
    resetCategoryForm(form, "btnEditCategory", editCategoryButtonHtml);
    await showCategoryAlert(result?.message || "Đã cập nhật danh mục.", "success");
    location.reload();
}

// =====================================================
// OPEN EDIT MODAL
// =====================================================

async function openEditModal(categoryId) {

    try {

        const response =
            await fetch(
                `/Admin/AdminCategory/GetById?id=${categoryId}`
            );

        const result =
            await response.json();

        if (!response.ok || !result.success) {

            toast(
                result.message || "Không tìm thấy danh mục",
                "error"
            );

            return;
        }

        const category = result.data;

        document.getElementById(
            "editCategoryId"
        ).value = category.categoryId;

        document.getElementById(
            "editCategoryCode"
        ).value = category.categoryCode ?? "";

        document.getElementById(
            "editCategoryName"
        ).value = category.name;

        const iconInput = document.getElementById(
            "editCategoryIcon"
        );
        iconInput.value = category.icon ?? "";
        iconInput.dispatchEvent(new Event("input", { bubbles: true }));

        document.getElementById(
            "editCategoryActive"
        ).checked = category.active;

    }
    catch {

        toast(
            "Có lỗi xảy ra",
            "error"
        );
    }
}

// =====================================================
// TOGGLE STATUS
// =====================================================

async function toggleCategory(id) {

    if (window.Swal) {
        const result = await window.Swal.fire({
            title: 'Xác nhận',
            text: 'Bạn có chắc muốn thay đổi trạng thái danh mục này?',
            icon: 'warning',
            showCancelButton: true,
            confirmButtonColor: '#70482f',
            cancelButtonColor: '#6c757d',
            confirmButtonText: 'Đồng ý',
            cancelButtonText: 'Hủy'
        });
        if (!result.isConfirmed) return;
    } else if (!confirm("Bạn có chắc muốn thay đổi trạng thái danh mục này?")) {
        return;
    }

    try {

        const response =
            await fetch(
                `/Admin/AdminCategory/ToggleStatus?id=${id}`,
                {
                    method: "POST",
                    headers:
                    {
                        RequestVerificationToken:
                            getAntiForgeryToken()
                    }
                });

        const result =
            await response.json();

        if (!response.ok || !result.success) {

            await showCategoryAlert(
                window.AdminFeedback.resolveMessage(result, {
                    status: response.status,
                    action: "update",
                    entityName: "trạng thái danh mục"
                }),
                "error"
            );

            return;
        }

        try {
            sessionStorage.setItem('toast_message', 'Thay đổi trạng thái thành công.');
            sessionStorage.setItem('toast_type', 'success');
        } catch (e) {
            // Fallback
        }

        location.reload();
    }
    catch {

        await showCategoryAlert(window.AdminFeedback.networkMessage(), "error");
    }
}

// =====================================================
// DOM READY
// =====================================================

document.addEventListener(
    "DOMContentLoaded",
    () => {

        const createForm =
            document.getElementById(
                "createCategoryForm"
            );

        if (createForm) {

            createForm.addEventListener(
                "submit",
                function (e) {

                    e.preventDefault();

                    createCategory(this);
                });
        }

        document.getElementById("btnSuggestCategories")?.addEventListener("click", suggestCategories);

        document.querySelectorAll("[data-category-icon-picker]")
            .forEach(initializeCategoryIconPicker);

        document.getElementById("createCategoryModal")?.addEventListener("hidden.bs.modal", event => {
            if (event.currentTarget.dataset.preserveCategoryForm === "true") return;
            resetCategorySuggestions();
            resetCategoryForm(createForm, "btnCreateCategory", createCategoryButtonHtml);
        });

        const editForm =
            document.getElementById(
                "editCategoryForm"
            );

        if (editForm) {

            editForm.addEventListener(
                "submit",
                function (e) {

                    e.preventDefault();

                    editCategory(this);
                });
        }

        document.getElementById("editCategoryModal")?.addEventListener("hidden.bs.modal", event => {
            if (event.currentTarget.dataset.preserveCategoryForm === "true") return;
            resetCategoryForm(editForm, "btnEditCategory", editCategoryButtonHtml);
        });

        document
            .querySelectorAll(
                ".btn-edit-category"
            )
            .forEach(btn => {

                btn.addEventListener(
                    "click",
                    () => openEditModal(btn.dataset.id)
                );

            });
    });

// =====================================================
// EXPOSE GLOBALS
// =====================================================

window.toggleCategory =
    toggleCategory;

window.openEditModal =
    openEditModal;
