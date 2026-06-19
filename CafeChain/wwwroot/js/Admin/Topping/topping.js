// =====================================================
// GLOBAL VARIABLES
// =====================================================

let currentToppingId = 0;
let currentToppingName = "";
let drinkModalInstance = null;

// =====================================================
// IMAGE CONFIG
// =====================================================

const MAX_FILE_SIZE = 3 * 1024 * 1024;

const ALLOWED_EXTENSIONS = [
    ".jpg",
    ".jpeg",
    ".png",
    ".webp"
];

const ALLOWED_MIME_TYPES = [
    "image/jpeg",
    "image/png",
    "image/webp"
];

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

// =====================================================
// BUSINESS VALIDATION
// =====================================================

function validateName(name) {

    if (!name || !name.trim()) {

        toast(
            "Tên topping không được để trống",
            "error"
        );

        return false;
    }

    if (name.trim().length > 100) {

        toast(
            "Tên topping tối đa 100 ký tự",
            "error"
        );

        return false;
    }

    return true;
}

function validatePrice(price) {

    const value = Number(price);

    if (!value) {

        toast(
            "Vui lòng nhập giá topping",
            "error"
        );

        return false;
    }

    if (value <= 1000) {

        toast(
            "Giá topping phải lớn hơn hoặc bằng 1000",
            "error"
        );

        return false;
    }

    return true;
}

// =====================================================
// IMAGE VALIDATION
// =====================================================

function validateImageFile(
    file,
    isRequired = false
) {

    if (!file) {

        if (isRequired) {

            toast(
                "Vui lòng chọn ảnh topping",
                "error"
            );

            return false;
        }

        return true;
    }

    if (file.size <= 0) {

        toast(
            "File ảnh không hợp lệ",
            "error"
        );

        return false;
    }

    if (file.size > MAX_FILE_SIZE) {

        toast(
            "Ảnh không được vượt quá 3MB",
            "error"
        );

        return false;
    }

    const extension =
        file.name
            .toLowerCase()
            .substring(
                file.name.lastIndexOf(".")
            );

    if (!ALLOWED_EXTENSIONS.includes(extension)) {

        toast(
            "Chỉ chấp nhận JPG, JPEG, PNG hoặc WEBP",
            "error"
        );

        return false;
    }

    if (!ALLOWED_MIME_TYPES.includes(file.type)) {

        toast(
            "Định dạng ảnh không hợp lệ",
            "error"
        );

        return false;
    }

    return true;
}

// =====================================================
// CREATE IMAGE PREVIEW
// =====================================================

function previewCreateImage(event) {

    const file =
        event.target.files[0];

    if (!validateImageFile(file, true)) {

        removeCreateImage();

        return;
    }

    const preview =
        document.getElementById(
            "create-preview"
        );

    const removeBtn =
        document.getElementById(
            "create-remove-btn"
        );

    const reader = new FileReader();

    reader.onload = function (e) {

        preview.src = e.target.result;

        preview.classList.remove(
            "d-none"
        );

        removeBtn.classList.remove(
            "d-none"
        );
    };

    reader.readAsDataURL(file);
}
function removeCreateImage() {

    const input =
        document.getElementById(
            "create-image-input"
        );

    const preview =
        document.getElementById(
            "create-preview"
        );

    const removeBtn =
        document.getElementById(
            "create-remove-btn"
        );

    input.value = "";

    preview.src = "";

    preview.classList.add(
        "d-none"
    );

    removeBtn.classList.add(
        "d-none"
    );
}

// =====================================================
// EDIT IMAGE PREVIEW
// =====================================================

function previewEditImage(event) {

    const file =
        event.target.files[0];

    if (!validateImageFile(file)) {

        event.target.value = "";

        return;
    }

    const reader =
        new FileReader();

    reader.onload = function (e) {

        document.getElementById(
            "edit-preview"
        ).src =
            e.target.result;
    };

    reader.readAsDataURL(file);
}

function removeEditImage() {

    const input =
        document.getElementById(
            "edit-image-input"
        );

    input.value = "";

    const preview =
        document.getElementById(
            "edit-preview"
        );

    preview.src =
        preview.getAttribute(
            "data-original"
        ) ||
        "/images/no-image.png";
}

// =====================================================
// OPEN EDIT MODAL
// =====================================================

function openEditModal(id, name, price, imageUrl)
{
    document.getElementById("edit-id").value = id;

    document.getElementById("edit-name").value = name || "";

    document.getElementById("edit-price").value = price || 0;

    const preview = document.getElementById("edit-preview");

    const image = imageUrl && imageUrl.trim() !== "" ? imageUrl : "/images/no-image.png";

    preview.src = image;

    preview.setAttribute("data-original", image);

    document.getElementById("edit-image-input").value = "";
}

// =====================================================
// DOM READY
// =====================================================

document.addEventListener(
    "DOMContentLoaded",
    () => {

        document
            .querySelectorAll(
                ".btn-edit-topping"
            )
            .forEach(btn => {

                btn.addEventListener(
                    "click",
                    () => {

                        openEditModal(
                            btn.dataset.id,
                            btn.dataset.name,
                            btn.dataset.price,
                            btn.dataset.image
                        );
                    });
            });

        // CREATE FORM

        const createForm =
            document.getElementById(
                "createToppingForm"
            );

        if (createForm) {

            createForm.addEventListener(
                "submit",
                async function (e) {

                    e.preventDefault();

                    const name =
                        this.querySelector(
                            '[name="Name"]'
                        ).value;

                    const price =
                        this.querySelector(
                            '[name="Price"]'
                        ).value;

                    const file =
                        document
                            .getElementById(
                                "create-image-input"
                            )
                            .files[0];

                    if (
                        !validateName(name) ||
                        !validatePrice(price) ||
                        !validateImageFile(
                            file,
                            true
                        )
                    ) {
                        return;
                    }

                    const btn =
                        document.getElementById(
                            "btnCreateTopping"
                        );

                    lockButton(
                        btn,
                        "Đang lưu..."
                    );

                    try {

                        const formData =
                            new FormData(this);

                        const response =
                            await fetch(
                                this.action,
                                {
                                    method: "POST",
                                    body: formData
                                });

                        const result =
                            await response.json();

                        if (!result.success) {

                            toast(
                                result.message,
                                "error"
                            );

                            unlockButton(
                                btn,
                                '<i class="fas fa-save me-2"></i>Lưu thông tin'
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
                                    "createModal"
                                )
                            )
                            .hide();

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
                            '<i class="fas fa-save me-2"></i>Lưu thông tin'
                        );
                    }
                });
        }

        // EDIT FORM

        const editForm =
            document.getElementById(
                "editForm"
            );

        if (editForm) {

            editForm.addEventListener(
                "submit",
                async function (e) {

                    e.preventDefault();

                    const btn =
                        document.getElementById(
                            "btnEditTopping"
                        );

                    lockButton(
                        btn,
                        "Đang lưu..."
                    );

                    try {

                        const formData =
                            new FormData(this);

                        const response =
                            await fetch(
                                this.action,
                                {
                                    method: "POST",
                                    body: formData
                                });

                        const result =
                            await response.json();

                        if (!result.success) {

                            toast(
                                result.message,
                                "error"
                            );

                            unlockButton(
                                btn,
                                '<i class="fas fa-save me-2"></i>Lưu thay đổi'
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
                                    "editModal"
                                )
                            )
                            .hide();

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
                            '<i class="fas fa-save me-2"></i>Lưu thay đổi'
                        );
                    }
                });
        }
    });

// =====================================================
// OPEN DRINK TOPPING MODAL
// =====================================================

async function openDrinkModal(
    toppingId,
    toppingName
) {

    currentToppingId = toppingId;

    currentToppingName = toppingName;

    document.getElementById(
        "currentToppingBadge"
    ).textContent = toppingName;

    try {

        const response =
            await fetch(
                `/Admin/AdminTopping/GetDrinks?toppingId=${toppingId}`
            );

        const result =
            await response.json();

        if (!result.success) {

            toast(
                result.message,
                "error"
            );

            return;
        }

        renderDrinkUI(
            result.data
        );

        const modalElement =
            document.getElementById(
                "drinkModal"
            );

        if (!drinkModalInstance) {

            drinkModalInstance =
                new bootstrap.Modal(
                    modalElement
                );
        }

        drinkModalInstance.show();
    }
    catch {

        toast(
            "Không thể tải danh sách drink",
            "error"
        );
    }
}

// =====================================================
// ASSIGN TOPPING TO DRINK
// =====================================================

async function assignTopping(
    drinkId,
    button
) {

    try {

        button.disabled = true;

        button.innerHTML =
            '<i class="fas fa-spinner fa-spin me-1"></i>Đang gán';

        const response =
            await fetch(
                "/Admin/AdminTopping/Assign",
                {
                    method: "POST",

                    headers: {
                        "Content-Type":
                            "application/json",

                        RequestVerificationToken:
                            getAntiForgeryToken()
                    },

                    body: JSON.stringify({
                        drinkId: drinkId,
                        toppingId: currentToppingId
                    })
                });

        const result =
            await response.json();

        if (!result.success) {

            toast(
                result.message,
                "error"
            );

            button.disabled = false;

            button.innerHTML =
                "Gán";

            return;
        }

        toast(
            result.message,
            "success"
        );

        await reloadDrinkData();
    }
    catch {

        toast(
            "Có lỗi xảy ra",
            "error"
        );

        button.disabled = false;

        button.innerHTML =
            "Gán";
    }
}

// =====================================================
// TOGGLE DRINK TOPPING
// =====================================================

async function toggleDrinkTopping(
    drinkToppingId
) {

    try {

        const response =
            await fetch(
                `/Admin/AdminTopping/Toggle?id=${drinkToppingId}`,
                {
                    method: "POST",

                    headers: {
                        RequestVerificationToken:
                            getAntiForgeryToken()
                    }
                });

        const result =
            await response.json();

        if (!result.success) {

            toast(
                result.message,
                "error"
            );

            return;
        }

        toast(
            result.message,
            "success"
        );

        await reloadDrinkData();
    }
    catch {

        toast(
            "Có lỗi xảy ra",
            "error"
        );
    }
}

// =====================================================
// RELOAD DRINK DATA
// =====================================================

async function reloadDrinkData() {

    try {

        const response =
            await fetch(
                `/Admin/AdminTopping/GetDrinks?toppingId=${currentToppingId}`
            );

        const result =
            await response.json();

        if (!result.success) {

            toast(
                result.message,
                "error"
            );

            return;
        }

        renderDrinkUI(
            result.data
        );
    }
    catch {

        toast(
            "Không thể tải lại dữ liệu",
            "error"
        );
    }
}

// =====================================================
// TOGGLE TOPPING STATUS
// =====================================================

function toggleTopping(
    toppingId
) {

    if (
        !confirm(
            "Bạn có chắc muốn thay đổi trạng thái topping này?"
        )
    ) {
        return;
    }

    const form =
        document.createElement(
            "form"
        );

    form.method = "POST";

    form.action =
        `/Admin/AdminTopping/ToggleStatus?id=${toppingId}`;

    const token =
        document.querySelector(
            'input[name="__RequestVerificationToken"]'
        );

    if (token) {

        const hiddenToken =
            document.createElement(
                "input"
            );

        hiddenToken.type =
            "hidden";

        hiddenToken.name =
            "__RequestVerificationToken";

        hiddenToken.value =
            token.value;

        form.appendChild(
            hiddenToken
        );
    }

    document.body.appendChild(
        form
    );

    form.submit();
}

// =====================================================
// RENDER DRINK UI
// =====================================================

function renderDrinkUI(
    drinks
) {

    const assignedList =
        document.getElementById(
            "assignedList"
        );

    const unassignedList =
        document.getElementById(
            "unassignedList"
        );

    assignedList.innerHTML = "";

    unassignedList.innerHTML = "";

    if (
        !drinks ||
        drinks.length === 0
    ) {

        assignedList.innerHTML =
            `<div class="col-12 text-muted">
                Không có dữ liệu
            </div>`;

        return;
    }

    drinks.forEach(drink => {

        const cardHtml =
            createDrinkCard(
                drink
            );

        if (
            drink.isAssigned
        ) {

            assignedList.insertAdjacentHTML(
                "beforeend",
                cardHtml
            );
        }
        else {

            unassignedList.insertAdjacentHTML(
                "beforeend",
                cardHtml
            );
        }
    });
}

// =====================================================
// CREATE DRINK CARD
// =====================================================

function createDrinkCard(
    drink
) {

    const imageUrl =
        drink.imageUrl ||
        "/images/no-image.png";

    if (
        drink.isAssigned
    ) {

        return `
            <div class="col-12">

                <div class="card shadow-sm">

                    <div class="card-body d-flex align-items-center">

                        <img
                            src="${imageUrl}"
                            style="
                                width:70px;
                                height:70px;
                                object-fit:cover;
                            "
                            class="rounded me-3" />

                        <div class="flex-grow-1">

                            <div class="fw-bold">
                                ${drink.name}
                            </div>

                            <small class="text-muted">
                                ${drink.categoryName ?? ""}
                            </small>

                        </div>

                        <button
                            class="btn btn-sm ${drink.active
                ? "btn-danger"
                : "btn-success"}"
                            onclick="toggleDrinkTopping(${drink.drinkToppingId})">

                            ${drink.active
                ? "Ngừng"
                : "Kích hoạt"}

                        </button>

                    </div>

                </div>

            </div>
        `;
    }

    return `
        <div class="col-12">

            <div class="card shadow-sm">

                <div class="card-body d-flex align-items-center">

                    <img
                        src="${imageUrl}"
                        style="
                            width:70px;
                            height:70px;
                            object-fit:cover;
                        "
                        class="rounded me-3" />

                    <div class="flex-grow-1">

                        <div class="fw-bold">
                            ${drink.name}
                        </div>

                        <small class="text-muted">
                            ${drink.categoryName ?? ""}
                        </small>

                    </div>

                    <button
                        class="btn btn-primary btn-sm"
                        onclick="assignTopping(${drink.drinkId}, this)">

                        Gán

                    </button>

                </div>

            </div>

        </div>
    `;
}

// =====================================================
// EXPOSE GLOBAL FUNCTIONS
// =====================================================

window.openDrinkModal =
    openDrinkModal;

window.assignTopping =
    assignTopping;

window.toggleDrinkTopping =
    toggleDrinkTopping;

window.toggleTopping =
    toggleTopping;

window.previewCreateImage =
    previewCreateImage;

window.removeCreateImage =
    removeCreateImage;

window.previewEditImage =
    previewEditImage;

window.removeEditImage =
    removeEditImage;

window.openEditModal =
    openEditModal;