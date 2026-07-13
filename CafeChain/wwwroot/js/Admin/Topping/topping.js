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
    const name = form.querySelector('[name="Name"]');
    const code = form.querySelector('[name="ToppingCode"]');
    const price = form.querySelector('[name="Price"]');
    const imageInput = document.getElementById('create-image-input');
    const panel = document.getElementById('toppingAiSuggestionPanel');
    const preview = document.getElementById('toppingAiImage');
    const attribution = document.getElementById('toppingAiImageAttribution');
    const retryImage = document.getElementById('btnRegenerateToppingAiImage');
    const optionList = document.getElementById('toppingAiOptionList');
    const applyButton = document.getElementById('btnApplyToppingAi');
    const warnings = document.getElementById('toppingAiWarnings');
    let selectedOption = null;
    let generatedImageFile = null;
    let generatedImageUrl = null;
    let textController = null;
    let imageController = null;
    const clearImage = () => {
        imageController?.abort(); imageController = null; generatedImageFile = null;
        if (generatedImageUrl) URL.revokeObjectURL(generatedImageUrl);
        generatedImageUrl = null;
        preview.removeAttribute('src'); preview.classList.add('d-none');
        attribution.replaceChildren(); attribution.classList.add('d-none');
    };
    const clear = () => {
        clearImage(); selectedOption = null; optionList.replaceChildren(); warnings.textContent = '';
        applyButton.disabled = true; retryImage.disabled = true; panel.classList.add('d-none');
    };
    const postJson = async (url, body, timeoutMs, controller) => {
        const timeout = setTimeout(() => controller.abort(), timeoutMs);
        try {
            const response = await fetch(url, {
                method: 'POST', headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getAntiForgeryToken() },
                body: JSON.stringify(body), signal: controller.signal
            });
            const result = await readJsonResult(response);
            if (!response.ok || !result.success) throw new Error(result.message || 'Không thể tạo gợi ý.');
            return result;
        } finally { clearTimeout(timeout); }
    };
    const base64File = data => {
        const bytes = atob(data.base64Data); const array = new Uint8Array(bytes.length);
        for (let index = 0; index < bytes.length; index++) array[index] = bytes.charCodeAt(index);
        return new File([array], data.fileName || 'topping-ai.png', { type: data.contentType || 'image/png' });
    };
    const safePexelsUrl = value => {
        try {
            const url = new URL(value);
            return url.protocol === 'https:' && (url.hostname === 'pexels.com' || url.hostname.endsWith('.pexels.com'))
                ? url.href : null;
        } catch { return null; }
    };
    const renderAttribution = data => {
        attribution.replaceChildren();
        if (data.imageSource === 'Pexels') {
            attribution.append(document.createTextNode('Photo by '));
            const photographerUrl = safePexelsUrl(data.photographerUrl);
            if (photographerUrl) {
                const photographerLink = document.createElement('a');
                photographerLink.href = photographerUrl;
                photographerLink.target = '_blank';
                photographerLink.rel = 'noopener noreferrer';
                photographerLink.textContent = data.photographer || 'Pexels contributor';
                attribution.append(photographerLink);
            } else {
                attribution.append(document.createTextNode(data.photographer || 'Pexels contributor'));
            }
            attribution.append(document.createTextNode(' on '));
            const photoLink = document.createElement('a');
            photoLink.href = safePexelsUrl(data.photoUrl) || 'https://www.pexels.com';
            photoLink.target = '_blank';
            photoLink.rel = 'noopener noreferrer';
            photoLink.textContent = 'Pexels';
            attribution.append(photoLink);
        } else {
            attribution.textContent = 'Ảnh được tạo local bằng ComfyUI.';
        }
        attribution.classList.remove('d-none');
    };
    const squareJpeg = file => new Promise((resolve, reject) => {
        const objectUrl = URL.createObjectURL(file); const img = new Image();
        img.onload = () => {
            const canvas = document.createElement('canvas'); canvas.width = canvas.height = 1000;
            const context = canvas.getContext('2d'); const side = Math.min(img.width, img.height);
            context.drawImage(img, (img.width - side) / 2, (img.height - side) / 2, side, side, 0, 0, 1000, 1000);
            URL.revokeObjectURL(objectUrl);
            canvas.toBlob(blob => blob ? resolve(new File([blob], 'topping-ai.jpg', { type: 'image/jpeg' })) : reject(new Error('Không thể xử lý ảnh AI.')), 'image/jpeg', 0.86);
        };
        img.onerror = () => { URL.revokeObjectURL(objectUrl); reject(new Error('Ảnh AI không hợp lệ.')); };
        img.src = objectUrl;
    });
    const generateImage = async () => {
        if (!selectedOption?.fields?.imagePrompt) return;
        clearImage(); imageController = new AbortController();
        const activeImageController = imageController;
        applyButton.disabled = true;
        retryImage.disabled = true; retryImage.textContent = 'Đang tạo ảnh...';
        try {
            const result = await postJson('/Admin/AdminTopping/AiImageSuggestion', {
                imagePrompt: selectedOption.fields.imagePrompt,
                fileNamePrefix: selectedOption.fields.toppingCode || 'topping_ai',
                excludedExternalImageIds: selectedOption.excludedExternalImageIds || []
            }, 190000, activeImageController);
            if (result.data.externalImageId) {
                selectedOption.excludedExternalImageIds = [
                    ...(selectedOption.excludedExternalImageIds || []),
                    Number(result.data.externalImageId)
                ];
            }
            generatedImageFile = await squareJpeg(base64File(result.data));
            if (generatedImageFile.size > MAX_FILE_SIZE) throw new Error('Ảnh sau xử lý vượt quá 3MB.');
            generatedImageUrl = URL.createObjectURL(generatedImageFile);
            preview.src = generatedImageUrl; preview.classList.remove('d-none');
            renderAttribution(result.data);
        } catch (error) {
            if (error.name !== 'AbortError') showToast(`${error.message} Bạn vẫn có thể áp dụng phần nội dung.`, 'error');
        } finally {
            if (imageController === activeImageController) {
                applyButton.disabled = !selectedOption?.canApply;
                retryImage.disabled = !selectedOption;
                retryImage.textContent = 'Tạo lại ảnh';
            }
        }
    };
    const selectOption = (option, card) => {
        selectedOption = option;
        optionList.querySelectorAll('.ai-option-card').forEach(x => x.classList.remove('is-selected'));
        card.classList.add('is-selected');
        applyButton.disabled = !option.canApply;
        retryImage.disabled = false;
        generateImage();
    };
    const renderOptions = result => {
        optionList.replaceChildren();
        (result.options || []).slice(0, 3).forEach(option => {
            const fields = option.fields || {};
            const card = document.createElement('button');
            card.type = 'button'; card.className = 'ai-option-card text-start';
            const title = document.createElement('strong');
            title.textContent = option.title || fields.name || 'Gợi ý topping';
            const meta = document.createElement('div');
            meta.className = 'small text-muted mt-1';
            meta.textContent = `${fields.toppingCode || ''} · ${Number(fields.price || 0).toLocaleString('vi-VN')} đ`;
            card.append(title, meta);
            card.addEventListener('click', () => selectOption(option, card));
            optionList.appendChild(card);
        });
        warnings.textContent = (result.warnings || []).join(' ');
        document.getElementById('toppingAiSource').textContent = result.usedOllama ? 'Ollama + C#' : 'C# fallback';
        panel.classList.remove('d-none');
    };

    button.addEventListener('click', async () => {
        if (price.value && Number(price.value) <= 0) return showToast('Giá topping không hợp lệ.', 'error');
        textController?.abort(); textController = new AbortController();
        const activeTextController = textController;
        const original = button.innerHTML; lockButton(button, 'Đang gợi ý...'); clear();
        try {
            const result = await postJson('/Admin/AdminTopping/AiSuggestion', {
                idea: idea.value.trim() || null,
                currentToppingCode: code.value.trim() || null,
                currentName: name.value.trim() || null,
                currentPrice: price.value ? Number(price.value) : null
            }, 130000, activeTextController);
            renderOptions(result);
        } catch (error) {
            if (error.name !== 'AbortError') showToast(error.message, 'error');
        } finally { if (textController === activeTextController) unlockButton(button, original); }
    });
    retryImage.addEventListener('click', generateImage);
    applyButton.addEventListener('click', () => {
        if (!selectedOption?.canApply) return showToast('Vui lòng chọn một gợi ý hợp lệ.', 'error');
        const suggestion = selectedOption.fields;
        if ((name.value.trim() || code.value.trim() || price.value || imageInput.files.length) &&
            !window.confirm('Một số dữ liệu hoặc ảnh hiện tại sẽ được thay thế. Tiếp tục áp dụng?')) return;
        name.value = suggestion.name; code.value = suggestion.toppingCode; price.value = suggestion.price;
        if (generatedImageFile) {
            const transfer = new DataTransfer(); transfer.items.add(generatedImageFile); imageInput.files = transfer.files;
            previewCreateImage({ target: imageInput });
        }
        clear(); showToast('Đã áp dụng gợi ý vào form. Vui lòng kiểm tra trước khi lưu.');
    });
    document.getElementById('btnDismissToppingAi').addEventListener('click', clear);
    const invalidate = () => { textController?.abort(); clear(); };
    [idea, name, code, price].forEach(x => x.addEventListener('input', invalidate));
});

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

function validateCode(code) {

    if (!code || !code.trim()) {

        toast(
            "Mã topping không được để trống",
            "error"
        );

        return false;
    }

    if (code.trim().length > 50) {

        toast(
            "Mã topping tối đa 50 ký tự",
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

    if (value < 1000) {

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

                    const file =
                        document
                            .getElementById(
                                "create-image-input"
                            )
                            .files[0];

                    if (
                        !validateCode(code) ||
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

async function toggleTopping(
    toppingId,
    url
) {

    if (
        !confirm(
            "Bạn có chắc muốn thay đổi trạng thái topping này?"
        )
    ) {
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
                        RequestVerificationToken: token
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
