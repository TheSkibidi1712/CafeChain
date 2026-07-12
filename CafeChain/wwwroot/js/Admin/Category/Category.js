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

// =====================================================
// VALIDATION
// =====================================================

function validateCategoryName(name) {

    if (!name || !name.trim()) {

        toast(
            "Tên danh mục không được để trống.",
            "error"
        );

        return false;
    }

    const value = name.trim();

    if (value.length < 2) {

        toast(
            "Tên danh mục phải từ 2 ký tự trở lên.",
            "error"
        );

        return false;
    }

    if (value.length > 100) {

        toast(
            "Tên danh mục tối đa 100 ký tự.",
            "error"
        );

        return false;
    }

    return true;
}

function validateCategoryCode(code) {

    if (!code || !code.trim()) {

        toast(
            "Mã danh mục không được để trống.",
            "error"
        );

        return false;
    }

    const value = code.trim();

    if (value.length < 2) {

        toast(
            "Mã danh mục phải từ 2 ký tự trở lên.",
            "error"
        );

        return false;
    }

    if (value.length > 30) {

        toast(
            "Mã danh mục tối đa 30 ký tự.",
            "error"
        );

        return false;
    }

    return true;
}

// =====================================================
// CREATE
// =====================================================

async function createCategory(form) {

    const code =
    form.querySelector('[name="CategoryCode"]').value;

    const name =
        form.querySelector('[name="Name"]').value;

    if (!validateCategoryCode(code)) {
        return;
    }

    if (!validateCategoryName(name)) {
        return;
    }

    const btn =
        document.getElementById(
            "btnCreateCategory"
        );

    lockButton(
        btn,
        "Đang lưu..."
    );

    try {

        const response =
            await fetch(
                form.action,
                {
                    method: "POST",
                    body: buildCategoryFormData(form)
                });

        const result =
            await response.json();

        if (!response.ok || !result.success) {

            toast(
                result.message ||
                "Có lỗi xảy ra",
                "error"
            );

            unlockButton(
                btn,
                '<i class="fas fa-save me-2"></i>Lưu danh mục'
            );

            return;
        }

        toast(
            result.message,
            "success"
        );

        bootstrap.Modal
            .getInstance(
                document.getElementById(
                    "createCategoryModal"
                )
            )
            ?.hide();

        setTimeout(() => {

            location.reload();

        }, 1000);
    }
    catch {

        toast(
            "Có lỗi xảy ra",
            "error"
        );

        unlockButton(
            btn,
            '<i class="fas fa-save me-2"></i>Lưu danh mục'
        );
    }
}

async function suggestCategories() {
    const form = document.getElementById("createCategoryForm");
    const button = document.getElementById("btnSuggestCategories");
    const panel = document.getElementById("categoryAiSuggestions");
    const list = document.getElementById("categoryAiSuggestionList");
    if (!form || !button || !panel || !list) return;

    const originalHtml = button.innerHTML;
    lockButton(button, "Đang gợi ý...");
    try {
        const response = await fetch("/Admin/AdminCategory/AiSuggestions", {
            method: "POST",
            headers: { "RequestVerificationToken": form.querySelector('[name="__RequestVerificationToken"]')?.value || "" }
        });
        const result = await response.json();
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
                form.querySelector('[name="Icon"]').value = option.icon;
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
        toast(error.message || "Không thể tạo gợi ý danh mục.", "error");
    }
    finally {
        unlockButton(button, originalHtml);
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

    const code =
    document.getElementById("editCategoryCode").value;

    const name =
        document.getElementById("editCategoryName").value;

    if (!validateCategoryCode(code)) {
        return;
    }

    if (!validateCategoryName(name)) {
        return;
    }

    const btn =
        document.getElementById(
            "btnEditCategory"
        );

    lockButton(
        btn,
        "Đang lưu..."
    );

    try {

        const response =
            await fetch(
                form.action,
                {
                    method: "POST",
                    body: buildCategoryFormData(form)
                });

        const result =
            await response.json();

        if (!response.ok || !result.success) {

            toast(
                result.message ||
                "Có lỗi xảy ra",
                "error"
            );

            unlockButton(
                btn,
                '<i class="fas fa-save me-2"></i>Cập nhật'
            );

            return;
        }

        toast(
            result.message,
            "success"
        );

        bootstrap.Modal
            .getInstance(
                document.getElementById(
                    "editCategoryModal"
                )
            )
            ?.hide();

        setTimeout(() => {

            location.reload();

        }, 1000);
    }
    catch {

        toast(
            "Có lỗi xảy ra",
            "error"
        );

        unlockButton(
            btn,
            '<i class="fas fa-save me-2"></i>Cập nhật'
        );
    }
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

        document.getElementById(
            "editCategoryIcon"
        ).value = category.icon ?? "";

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

    if (
        !confirm(
            "Bạn có chắc muốn thay đổi trạng thái danh mục này?"
        )
    ) {
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

            toast(
                result.message ||
                "Có lỗi xảy ra",
                "error"
            );

            return;
        }

        toast(
            result.message,
            "success"
        );

        setTimeout(() => {

            location.reload();

        }, 800);
    }
    catch {

        toast(
            "Có lỗi xảy ra",
            "error"
        );
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
