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
    )?.value || '';
}

async function readJsonResult(response) {

    const contentType =
        response.headers.get("content-type") || "";

    if (!contentType.toLowerCase().includes("application/json")) {

        return {
            success: false,
            message: response.ok
                ? "Phản hồi từ máy chủ không hợp lệ"
                : "Có lỗi xảy ra khi xử lý yêu cầu"
        };
    }

    const result =
        await response.json();

    if (!response.ok && result.success !== false) {

        return {
            success: false,
            message: result.message || "Có lỗi xảy ra"
        };
    }

    return result;
}

function showToast(message, type = "success") {

    if (typeof toast === "function") {
        toast(message, type);
    } else if (typeof window.toast === "function") {
        window.toast(message, type);
    } else {
        alert(message);
    }
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

document.addEventListener('DOMContentLoaded', function initToppingAiSuggestion() {
    const button = document.getElementById('btnToppingAiSuggestionLegacy');
    if (!button) return;
    const form = document.getElementById('createToppingForm');
    const name = form.querySelector('[name="Name"]');
    const price = form.querySelector('[name="Price"]');
    const code = form.querySelector('[name="ToppingCode"]');
    const panel = document.getElementById('toppingAiSuggestionPanel');
    let suggestion = null;
    let controller = null;
    const clear = () => { suggestion = null; panel.classList.add('d-none'); };

    button.addEventListener('click', async () => {
        if (!name.value.trim()) return showToast('Vui lòng nhập tên topping.', 'error');
        if (price.value && Number(price.value) <= 0) return showToast('Giá topping không hợp lệ.', 'error');
        controller?.abort();
        controller = new AbortController();
        const timeout = setTimeout(() => controller.abort(), 15000);
        const original = button.innerHTML;
        lockButton(button, 'Đang gợi ý...');
        clear();
        try {
            const response = await fetch('/Admin/AdminTopping/AiSuggestion', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getAntiForgeryToken() },
                body: JSON.stringify({ name: name.value.trim(), price: price.value ? Number(price.value) : null }),
                signal: controller.signal
            });
            const result = await readJsonResult(response);
            if (!result.success) throw new Error(result.message || 'Không thể tạo gợi ý.');
            suggestion = result.data;
            document.getElementById('toppingAiCode').textContent = suggestion.toppingCode;
            panel.classList.remove('d-none');
        } catch (error) {
            if (error.name !== 'AbortError') showToast(error.message, 'error');
        } finally {
            clearTimeout(timeout);
            unlockButton(button, original);
        }
    });
    document.getElementById('btnApplyToppingAi').addEventListener('click', () => {
        if (!suggestion) return;
        if (code.value.trim() && !window.confirm('Ghi đè mã topping hiện tại?')) return;
        code.value = suggestion.toppingCode;
        clear();
        showToast('Đã điền mã gợi ý. Dữ liệu chưa được lưu.');
    });
    document.getElementById('btnDismissToppingAi').addEventListener('click', clear);
    [name, price].forEach(x => x.addEventListener('input', clear));
});

document.addEventListener('DOMContentLoaded', function initFullToppingAiSuggestion() {
    const button = document.getElementById('btnToppingAiSuggestion');
    if (!button) return;
    const form = document.getElementById('createToppingForm');
    const idea = document.getElementById('toppingAiIdea');
    const generationMode = document.getElementById('toppingAiGenerationMode');
    const name = form.querySelector('[name="Name"]');
    const code = form.querySelector('[name="ToppingCode"]');
    const price = form.querySelector('[name="Price"]');
    const imageInput = document.getElementById('create-image-input');
    if (window.CafeChainAIImagePipeline) {
        window.CafeChainAIImagePipeline.create({
            ids: {
                button: 'btnToppingAiSuggestion', form: 'createToppingForm', idea: 'toppingAiIdea',
                panel: 'toppingAiSuggestionPanel', optionList: 'toppingAiOptionList',
                referenceList: 'toppingAiReferenceList', generatedList: 'toppingAiGeneratedList',
                status: 'toppingAiStatus', warnings: 'toppingAiWarnings', source: 'toppingAiSource',
                usePexels: 'btnUseToppingPexels', generate: 'btnGenerateToppingAi',
                fallback: 'btnGenerateToppingAiWithoutReference', retrySearch: 'btnRetryToppingAiSearch',
                apply: 'btnApplyToppingAi', dismiss: 'btnDismissToppingAi'
            },
            urls: {
                suggestions: '/Admin/AdminTopping/AiSuggestion',
                references: '/Admin/AdminTopping/AiReferenceImages',
                usePexels: '/Admin/AdminTopping/AiUsePexelsImage',
                generate: '/Admin/AdminTopping/AiGenerateFromReference',
                generateWithoutReference: '/Admin/AdminTopping/AiGenerateWithoutReference'
            },
            defaultFileName: 'topping-ai.png',
            notify: (message, type) => showToast(message, type),
            suggestionPayload: () => ({
                generationMode: Number(generationMode?.value || 0),
                idea: idea.value.trim() || null,
                currentToppingCode: code.value.trim() || null,
                currentName: name.value.trim() || null,
                currentPrice: price.value ? Number(price.value) : null
            }),
            renderSuggestion: (card, option) => {
                const value = option.fields || {};
                const title = document.createElement('strong');
                title.textContent = option.title || value.name || 'Gợi ý topping';
                const meta = document.createElement('div');
                meta.className = 'small text-muted mt-1';
                meta.textContent = `${value.toppingCode || ''} · ${Number(value.price || 0).toLocaleString('vi-VN')} đ`;
                card.append(title, meta);
            },
            fileNamePrefix: option => option.fields?.toppingCode || 'topping_ai',
            invalidateElements: () => [idea, generationMode, name, code, price],
            willOverwrite: () => Boolean(name.value.trim() || code.value.trim() || price.value || imageInput?.files?.length),
            apply: async (option, file) => {
                const value = option.fields || {};
                name.value = value.name || '';
                code.value = value.toppingCode || '';
                price.value = value.price || '';
                if (imageInput && file) {
                    const transfer = new DataTransfer();
                    transfer.items.add(file);
                    imageInput.files = transfer.files;
                    previewCreateImage({ target: imageInput });
                }
                return true;
            }
        });
        return;
    }
});
// =====================================================
// BUSINESS VALIDATION
// =====================================================

function validateName(name) {

    if (!name || !name.trim()) {

        toast(
            "Tên topping không được để trống",
            "warning"
        );

        return false;
    }

    if (name.trim().length > 100) {

        toast(
            "Tên topping tối đa 100 ký tự",
            "warning"
        );

        return false;
    }

    return true;
}

function validateCode(code) {

    if (!code || !code.trim()) {

        toast(
            "Mã topping không được để trống",
            "warning"
        );

        return false;
    }

    if (code.trim().length > 50) {

        toast(
            "Mã topping tối đa 50 ký tự",
            "warning"
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
            "warning"
        );

        return false;
    }

    if (value < 1000) {

        toast(
            "Giá topping phải lớn hơn hoặc bằng 1000",
            "warning"
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
                "warning"
            );

            return false;
        }

        return true;
    }

    if (file.size <= 0) {

        toast(
            "File ảnh không hợp lệ",
            "warning"
        );

        return false;
    }

    if (file.size > MAX_FILE_SIZE) {

        toast(
            "Ảnh không được vượt quá 3MB",
            "warning"
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
            "warning"
        );

        return false;
    }

    if (!ALLOWED_MIME_TYPES.includes(file.type)) {

        toast(
            "Định dạng ảnh không hợp lệ",
            "warning"
        );

        return false;
    }

    return true;
}

// =====================================================
// FILE SIZE FORMATTER
// =====================================================

function formatFileSize(bytes) {
    if (!bytes || bytes === 0) return "0 KB";
    const k = 1024;
    const sizes = ["B", "KB", "MB", "GB"];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(1)) + " " + sizes[i];
}

// =====================================================
// CREATE IMAGE PREVIEW & UPLOAD HANDLER
// =====================================================

function displayCreateImageFile(file) {
    if (!file) {
        removeCreateImage();
        return;
    }
    if (!validateImageFile(file, false)) {
        removeCreateImage();
        return;
    }
    const preview = document.getElementById("create-preview");
    const previewWrap = document.getElementById("create-file-preview-container");
    const statusText = document.getElementById("create-file-status-text");

    if (statusText) {
        statusText.textContent = `${file.name} (${formatFileSize(file.size)})`;
        statusText.classList.add("text-success", "fw-bold");
    }

    const reader = new FileReader();
    reader.onload = function (e) {
        if (preview) {
            preview.src = e.target.result;
        }
        if (previewWrap) previewWrap.classList.remove("d-none");
    };
    reader.readAsDataURL(file);
}

function previewCreateImage(event) {
    const file = event?.target?.files?.[0];
    if (file) {
        displayCreateImageFile(file);
    }
}

function removeCreateImage(event) {
    if (event && typeof event.stopPropagation === 'function') {
        event.stopPropagation();
    }
    const input = document.getElementById("create-image-input");
    const preview = document.getElementById("create-preview");
    const previewWrap = document.getElementById("create-file-preview-container");
    const statusText = document.getElementById("create-file-status-text");

    if (input) input.value = "";
    if (preview) preview.src = "";
    if (statusText) {
        statusText.textContent = "Chưa chọn file nào";
        statusText.classList.remove("text-success", "fw-bold");
    }
    if (previewWrap) previewWrap.classList.add("d-none");
}

function initCreateToppingDropzone() {
    const box = document.getElementById("createToppingUploadBox");
    const input = document.getElementById("create-image-input");
    if (!box || !input) return;

    // Standard input change
    input.addEventListener("change", function () {
        if (this.files && this.files.length > 0) {
            displayCreateImageFile(this.files[0]);
        }
    });

    // Drag & Drop events on the upload box
    ["dragenter", "dragover"].forEach(eventName => {
        box.addEventListener(eventName, function (e) {
            e.preventDefault();
            e.stopPropagation();
            box.classList.add("is-dragover");
        }, false);
    });

    ["dragleave", "dragend", "drop"].forEach(eventName => {
        box.addEventListener(eventName, function (e) {
            e.preventDefault();
            e.stopPropagation();
            box.classList.remove("is-dragover");
        }, false);
    });

    box.addEventListener("drop", function (e) {
        const dt = e.dataTransfer;
        const files = dt ? dt.files : null;
        if (files && files.length > 0) {
            const file = files[0];
            if (validateImageFile(file, false)) {
                const transfer = new DataTransfer();
                transfer.items.add(file);
                input.files = transfer.files;
                displayCreateImageFile(file);
            }
        }
    }, false);
}

// =====================================================
// EDIT IMAGE PREVIEW
// =====================================================

function previewEditImage(event) {

    const file =
        event.target.files[0];

    const fileNameSpan =
        document.getElementById("edit-image-file-name");

    if (!validateImageFile(file)) {

        event.target.value = "";

        if (fileNameSpan) {
            fileNameSpan.textContent = "Chưa chọn tệp nào";
            fileNameSpan.title = "Chưa chọn tệp nào";
        }

        return;
    }

    if (fileNameSpan && file) {
        fileNameSpan.textContent = file.name;
        fileNameSpan.title = file.name;
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

    const fileNameSpan =
        document.getElementById("edit-image-file-name");

    if (fileNameSpan) {
        fileNameSpan.textContent = "Chưa chọn tệp nào";
        fileNameSpan.title = "Chưa chọn tệp nào";
    }

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

function openEditModal(id, code, name, price, imageUrl)
{
    document.getElementById("edit-id").value = id;

    document.getElementById("edit-code").value = code || "";

    document.getElementById("edit-name").value = name || "";

    document.getElementById("edit-price").value = price || 0;

    const preview = document.getElementById("edit-preview");

    const image = imageUrl && imageUrl.trim() !== "" ? imageUrl : "/images/no-image.png";

    preview.src = image;

    preview.setAttribute("data-original", image);

    document.getElementById("edit-image-input").value = "";

    const fileNameSpan = document.getElementById("edit-image-file-name");
    if (fileNameSpan) {
        fileNameSpan.textContent = "Chưa chọn tệp nào";
        fileNameSpan.title = "Chưa chọn tệp nào";
    }
}

// =====================================================
// DOM READY
// =====================================================

document.addEventListener(
    "DOMContentLoaded",
    () => {

        initCreateToppingDropzone();

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
                            btn.dataset.code,
                            btn.dataset.name,
                            btn.dataset.price,
                            btn.dataset.image
                        );
                    });
            });

        document.addEventListener(
            "click",
            function (e) {

                const btn =
                    e.target.closest(
                        ".js-toggle-topping"
                    );

                if (!btn) {
                    return;
                }

                e.preventDefault();

                toggleTopping(
                    btn.dataset.id,
                    btn.dataset.url
                );
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

                    const code =
                        this.querySelector(
                            '[name="ToppingCode"]'
                        ).value;

                    const price =
                        this.querySelector(
                            '[name="Price"]'
                        ).value;

                    const fileInput =
                        document
                            .getElementById(
                                "create-image-input"
                            );

                    const file = fileInput?.files?.[0];

                    if (
                        !validateCode(code) ||
                        !validateName(name) ||
                        !validatePrice(price) ||
                        (file && !validateImageFile(file, false))
                    ) {
                        this.querySelector(":invalid, [name='ToppingCode'], [name='Name'], [name='Price']")?.focus();
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

                            toast(window.AdminFeedback.resolveMessage(result, {
                                status: response.status,
                                action: "create",
                                entityName: "topping"
                            }), "error");

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
                            window.AdminFeedback.networkMessage(),
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

                    const code =
                        this.querySelector(
                            '[name="ToppingCode"]'
                        ).value;

                    const name =
                        this.querySelector(
                            '[name="Name"]'
                        ).value;

                    const price =
                        this.querySelector(
                            '[name="Price"]'
                        ).value;

                    if (
                        !validateCode(code) ||
                        !validateName(name) ||
                        !validatePrice(price)
                    ) {
                        this.querySelector(":invalid, [name='ToppingCode'], [name='Name'], [name='Price']")?.focus();
                        return;
                    }

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

                            toast(window.AdminFeedback.resolveMessage(result, {
                                status: response.status,
                                action: "update",
                                entityName: "topping"
                            }), "error");

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
                            window.AdminFeedback.networkMessage(),
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
                bootstrap.Modal.getOrCreateInstance(
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

async function toggleTopping(
    toppingId,
    url
) {

    if (window.Swal) {
        const result = await window.Swal.fire({
            title: 'Xác nhận',
            text: 'Bạn có chắc muốn thay đổi trạng thái topping này?',
            icon: 'warning',
            showCancelButton: true,
            confirmButtonColor: '#70482f',
            cancelButtonColor: '#6c757d',
            confirmButtonText: 'Đồng ý',
            cancelButtonText: 'Hủy'
        });
        if (!result.isConfirmed) return;
    } else if (!confirm("Bạn có chắc muốn thay đổi trạng thái topping này?")) {
        return;
    }

    const token =
        getAntiForgeryToken();

    if (!token) {

        showToast(
            "Không tìm thấy token bảo mật",
            "error"
        );

        return;
    }

    try {

        const response =
            await fetch(
                url ||
                `/Admin/AdminTopping/ToggleStatus?id=${toppingId}`,
                {
                    method: "POST",

                    headers: {
                        "Accept": "application/json",
                        "X-Requested-With": "XMLHttpRequest",
                        "RequestVerificationToken": token
                    }
                });

        const result =
            await readJsonResult(response);

        if (!result.success) {

            showToast(
                result.message || "Cập nhật trạng thái thất bại",
                "error"
            );

            return;
        }

        showToast(
            result.message || "Cập nhật trạng thái thành công",
            "success"
        );

        setTimeout(() => {
            location.reload();
        }, 700);
    }
    catch {

        showToast(
            "Có lỗi xảy ra",
            "error"
        );
    }
}

// =====================================================
// RENDER DRINK UI
// =====================================================

function renderDrinkUI(drinks) {
    const assignedList = document.getElementById("assignedList");
    const unassignedList = document.getElementById("unassignedList");

    assignedList.innerHTML = "";
    unassignedList.innerHTML = "";

    if (!drinks || drinks.length === 0) {
        assignedList.innerHTML = `<div class="text-center py-4 text-muted small"><i class="fas fa-info-circle me-1"></i>Không có dữ liệu đồ uống</div>`;
        unassignedList.innerHTML = `<div class="text-center py-4 text-muted small"><i class="fas fa-info-circle me-1"></i>Không có dữ liệu đồ uống</div>`;
        return;
    }

    let hasAssigned = false;
    let hasUnassigned = false;

    drinks.forEach(drink => {
        const cardHtml = createDrinkCard(drink);

        if (drink.isAssigned) {
            hasAssigned = true;
            assignedList.insertAdjacentHTML("beforeend", cardHtml);
        } else {
            hasUnassigned = true;
            unassignedList.insertAdjacentHTML("beforeend", cardHtml);
        }
    });

    if (!hasAssigned) {
        assignedList.innerHTML = `<div class="text-center py-4 text-muted small"><i class="fas fa-inbox d-block fa-2x mb-2 opacity-50"></i>Chưa có đồ uống nào gán topping này</div>`;
    }

    if (!hasUnassigned) {
        unassignedList.innerHTML = `<div class="text-center py-4 text-muted small"><i class="fas fa-check-circle d-block fa-2x mb-2 text-success opacity-50"></i>Tất cả đồ uống đã được gán topping</div>`;
    }
}

// =====================================================
// CREATE DRINK CARD
// =====================================================

function createDrinkCard(drink) {
    const imageUrl = drink.imageUrl || "/Images/DrinkImages/no-image.jpg";

    if (drink.isAssigned) {
        return `
            <div class="drink-card">
                <img src="${imageUrl}" class="drink-img" alt="${drink.name}" />
                <div class="drink-info">
                    <h6 class="fw-bold text-dark mb-1 text-truncate" title="${drink.name}">${drink.name}</h6>
                    <small class="text-muted d-block text-truncate">${drink.categoryName || "Đồ uống"}</small>
                </div>
                <div class="drink-action">
                    <button class="btn btn-sm ${drink.active ? "btn-outline-danger" : "btn-outline-success"} text-nowrap px-2.5 py-1.5"
                            onclick="toggleDrinkTopping(${drink.drinkToppingId})"
                            title="${drink.active ? "Ngừng sử dụng topping cho đồ uống này" : "Kích hoạt lại topping"}">
                        <i class="fas ${drink.active ? "fa-ban" : "fa-check"} me-1"></i>${drink.active ? "Tắt" : "Bật"}
                    </button>
                </div>
            </div>
        `;
    }

    return `
        <div class="drink-card">
            <img src="${imageUrl}" class="drink-img" alt="${drink.name}" />
            <div class="drink-info">
                <h6 class="fw-bold text-dark mb-1 text-truncate" title="${drink.name}">${drink.name}</h6>
                <small class="text-muted d-block text-truncate">${drink.categoryName || "Đồ uống"}</small>
            </div>
            <div class="drink-action">
                <button class="btn btn-sm btn-orange text-nowrap px-3 py-1.5"
                        onclick="assignTopping(${drink.drinkId}, this)">
                    <i class="fas fa-plus me-1"></i>Gán
                </button>
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
