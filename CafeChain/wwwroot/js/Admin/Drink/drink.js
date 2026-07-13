$(document).ready(function () {
    const MAX_FILE_SIZE = 5 * 1024 * 1024;
    const ALLOWED_EXT = ['.jpg', '.jpeg', '.png'];
    const CROP_SIZE = 1000;

    let selectedCreateFiles = [];
    let editedImageFile = null;
    let isCreateSubmitting = false;
    let isEditSubmitting = false;

    $.ajaxPrefilter(function (options, originalOptions, xhr) {
        if ((options.type || 'GET').toUpperCase() !== 'GET') {
            const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
            if (token) xhr.setRequestHeader('RequestVerificationToken', token);
        }
    });

    initCreateForm();
    initEditForm();
    initStatusSwitch();
    initImageManager();

    $(document).on('click', '.js-toggle-drink', function () {
        const id = Number(this.dataset.id);
        if (!id || !window.confirm('Bạn có chắc muốn đổi trạng thái đồ uống?')) return;
        $.ajax({
            url: '/Admin/AdminDrink/ToggleStatus',
            type: 'POST',
            data: { id },
            success: function (result) {
                if (!result.success) return notify(result.message || 'Không thể cập nhật trạng thái.', 'error');
                notify(result.message || 'Đã cập nhật trạng thái.');
                window.location.reload();
            },
            error: function (xhr) { notify(xhr.responseJSON?.message || 'Không thể cập nhật trạng thái.', 'error'); }
        });
    });

    function notify(message, type) {
        if (typeof toast === 'function') {
            toast(message, type || 'success');
            return;
        }

        alert(message);
    }

    function validateFile(file) {
        if (!file) {
            return false;
        }

        const ext = file.name
            .substring(file.name.lastIndexOf('.'))
            .toLowerCase();

        if (!ALLOWED_EXT.includes(ext)) {
            notify('Chỉ chấp nhận JPG, JPEG, PNG', 'error');
            return false;
        }

        if (file.size > MAX_FILE_SIZE) {
            notify('Ảnh vượt quá 5MB', 'error');
            return false;
        }

        return true;
    }

    async function cropImageToSquare(file) {
        return new Promise((resolve, reject) => {
            const reader = new FileReader();

            reader.onload = function (event) {
                const img = new Image();

                img.onload = function () {
                    const canvas = document.createElement('canvas');
                    canvas.width = CROP_SIZE;
                    canvas.height = CROP_SIZE;

                    const ctx = canvas.getContext('2d');
                    const minSide = Math.min(img.width, img.height);
                    const sx = (img.width - minSide) / 2;
                    const sy = (img.height - minSide) / 2;

                    ctx.drawImage(
                        img,
                        sx,
                        sy,
                        minSide,
                        minSide,
                        0,
                        0,
                        CROP_SIZE,
                        CROP_SIZE
                    );

                    canvas.toBlob(
                        function (blob) {
                            resolve(new File([blob], file.name, { type: 'image/jpeg' }));
                        },
                        'image/jpeg',
                        0.9
                    );
                };

                img.onerror = reject;
                img.src = event.target.result;
            };

            reader.onerror = reject;
            reader.readAsDataURL(file);
        });
    }

    function escapeHtml(value) {
        return String(value ?? '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#039;');
    }

    function initCreateForm() {
        const $form = $('#drinkCreateForm');

        if (!$form.length) {
            return;
        }

        $('#imageFilesInput').on('change', async function (e) {
            await handleCreateFiles(e.target.files);
            this.value = '';
        });

        $('#dropZone').on('dragover', function (e) {
            e.preventDefault();
            $(this).addClass('border-primary bg-light');
        });

        $('#dropZone').on('dragleave drop', function (e) {
            e.preventDefault();
            $(this).removeClass('border-primary bg-light');
        });

        $('#dropZone').on('drop', async function (e) {
            e.preventDefault();
            e.stopPropagation();
            $(this).removeClass('border-primary bg-light');

            await handleCreateFiles(e.originalEvent.dataTransfer.files);
        });

        $(document).on('click', '.btn-remove-img', function () {
            const removedIndex = Number($(this).data('index'));
            const currentDefaultIndex = getCreateDefaultIndex();

            selectedCreateFiles.splice(removedIndex, 1);

            if (!selectedCreateFiles.length) {
                setCreateDefaultIndex(null);
            }
            else if (currentDefaultIndex === removedIndex) {
                setCreateDefaultIndex(0);
            }
            else if (currentDefaultIndex > removedIndex) {
                setCreateDefaultIndex(currentDefaultIndex - 1);
            }

            renderCreatePreview();
        });

        $(document).on('change', 'input[name="createDefaultImage"]', function () {
            setCreateDefaultIndex(Number(this.value));
        });

        $form.on('submit', function (e) {
            e.preventDefault();

            if (isCreateSubmitting) {
                return;
            }

            if ($form.data('validator') && !$form.valid()) {
                return;
            }

            const imageInput = document.getElementById('imageFilesInput');

            if (imageInput) {
                const dataTransfer = new DataTransfer();

                selectedCreateFiles.forEach(file => {
                    dataTransfer.items.add(file);
                });

                imageInput.files = dataTransfer.files;
            }

            isCreateSubmitting = true;

            submitAjaxForm($form, {
                loadingHtml: '<i class="fas fa-spinner fa-spin me-1"></i> Đang tạo...',
                doneHtml: '<i class="fas fa-save me-1"></i> Tạo Nước Uống',
                onComplete: function () {
                    isCreateSubmitting = false;
                }
            });
        });
    }

    async function handleCreateFiles(files) {
        for (const file of files) {
            if (!validateFile(file)) {
                continue;
            }

            try {
                const croppedFile = await cropImageToSquare(file);
                selectedCreateFiles.push(croppedFile);
            }
            catch {
                notify('Không thể xử lý ảnh', 'error');
            }
        }

        if (selectedCreateFiles.length && getCreateDefaultIndex() < 0) {
            setCreateDefaultIndex(0);
        }

        renderCreatePreview();
    }

    function renderCreatePreview() {
        const $container = $('#previewContainer');

        if (!$container.length) {
            return;
        }

        $container.empty();

        if (!selectedCreateFiles.length) {
            setCreateDefaultIndex(null);
            return;
        }

        let defaultIndex = getCreateDefaultIndex();

        if (defaultIndex < 0 || defaultIndex >= selectedCreateFiles.length) {
            defaultIndex = 0;
            setCreateDefaultIndex(defaultIndex);
        }

        selectedCreateFiles.forEach((file, index) => {
            const previewUrl = URL.createObjectURL(file);
            const checked = index === defaultIndex ? 'checked' : '';

            $container.append(`
                <div class="col-6 col-md-4 position-relative">
                    <div class="border rounded overflow-hidden bg-white">
                        <img
                            src="${previewUrl}"
                            class="w-100"
                            style="aspect-ratio:1/1; object-fit:cover;"
                            alt="Ảnh nước uống ${index + 1}">

                        <div class="d-flex align-items-center justify-content-between gap-2 px-2 py-2">
                            <label class="form-check-label small d-flex align-items-center gap-1 mb-0">
                                <input
                                    type="radio"
                                    name="createDefaultImage"
                                    value="${index}"
                                    class="form-check-input mt-0"
                                    ${checked}>
                                Mặc định
                            </label>

                            <button
                                type="button"
                                class="btn btn-sm btn-outline-danger btn-remove-img"
                                data-index="${index}"
                                title="Xóa ảnh">
                                <i class="fas fa-times"></i>
                            </button>
                        </div>
                    </div>
                </div>
            `);
        });
    }

    function getCreateDefaultIndex() {
        const value = $('#defaultImageIndex').val();

        if (value === '') {
            return -1;
        }

        const parsed = Number(value);
        return Number.isInteger(parsed) ? parsed : -1;
    }

    function setCreateDefaultIndex(index) {
        $('#defaultImageIndex').val(index === null ? '' : index);
    }

    function initEditForm() {
        const $form = $('#drinkEditForm');

        if (!$form.length) {
            return;
        }

        $form.on('submit', function (e) {
            e.preventDefault();

            if (isEditSubmitting) {
                return;
            }

            if ($form.data('validator') && !$form.valid()) {
                return;
            }

            isEditSubmitting = true;

            submitAjaxForm($form, {
                loadingHtml: '<i class="fas fa-spinner fa-spin me-1"></i> Đang cập nhật...',
                doneHtml: '<i class="fas fa-save me-1"></i> Cập nhật Nước Uống',
                onComplete: function () {
                    isEditSubmitting = false;
                }
            });
        });
    }

    function submitAjaxForm($form, options) {
        const $submitBtn = $form.find('button[type="submit"]');

        $submitBtn
            .prop('disabled', true)
            .html(options.loadingHtml);

        $.ajax({
            url: $form.attr('action'),
            type: $form.attr('method') || 'POST',
            data: new FormData($form[0]),
            processData: false,
            contentType: false,
            success: function (res) {
                if (res.success) {
                    notify(res.message, 'success');

                    setTimeout(function () {
                        window.location.href = res.redirectUrl;
                    }, 700);

                    return;
                }

                $submitBtn
                    .prop('disabled', false)
                    .html(options.doneHtml);

                options.onComplete();
                notify(res.message || 'Dữ liệu không hợp lệ', 'error');
            },
            error: function () {
                $submitBtn
                    .prop('disabled', false)
                    .html(options.doneHtml);

                options.onComplete();
                notify('Có lỗi xảy ra', 'error');
            }
        });
    }

    function initStatusSwitch() {
        const $switch = $('#activeStatusSwitch');
        const $label = $('#activeStatusLabel');

        if (!$switch.length || !$label.length) {
            return;
        }

        updateStatusLabel();
        $switch.on('change', updateStatusLabel);

        function updateStatusLabel() {
            if ($switch.is(':checked')) {
                $label
                    .text('Trạng thái: Đang bán')
                    .removeClass('text-danger')
                    .addClass('text-success');
                return;
            }

            $label
                .text('Trạng thái: Ngừng bán')
                .removeClass('text-success')
                .addClass('text-danger');
        }
    }

    function initImageManager() {
        $(document).on('click', '.btn-manage-images', function () {
            const drinkId = $(this).data('id');
            const drinkName = $(this).data('name');

            $('#currentDrinkId').val(drinkId);
            $('#modalDrinkName').text(drinkName);
            resetUploadControls();
            loadImages(drinkId);

            const modalElement = document.getElementById('imageModal');

            if (modalElement) {
                new bootstrap.Modal(modalElement).show();
            }
        });

        const editPageDrinkId = $('#currentDrinkId').val();

        if ($('#imageListContainer').length && editPageDrinkId) {
            loadImages(editPageDrinkId);
        }

        $(document).on('click', '.btn-set-default', function () {
            const drinkId = $('#currentDrinkId').val();
            const drinkImageId = $(this).data('imgid');

            $.post(
                '/Admin/AdminDrink/SetDefaultImage',
                {
                    drinkId: drinkId,
                    drinkImageId: drinkImageId
                },
                function (res) {
                    if (res.success) {
                        notify(res.message || 'Đã cập nhật ảnh mặc định', 'success');
                        loadImages(drinkId);
                        reloadDrinkTable();
                        return;
                    }

                    notify(res.message || 'Cập nhật ảnh mặc định thất bại', 'error');
                });
        });

        $(document).on('click', '.btn-delete-img', function () {
            const drinkId = $('#currentDrinkId').val();
            const drinkImageId = $(this).data('imgid');

            if (!confirm('Bạn có chắc muốn xóa ảnh này?')) {
                return;
            }

            $.post(
                '/Admin/AdminDrink/DeleteImage',
                {
                    drinkImageId: drinkImageId
                },
                function (res) {
                    if (res.success) {
                        notify(res.message || 'Đã xóa ảnh thành công', 'success');
                        loadImages(drinkId);
                        reloadDrinkTable();
                        return;
                    }

                    notify(res.message || 'Xóa ảnh thất bại', 'error');
                })
                .fail(function () {
                    notify('Có lỗi hệ thống khi xóa ảnh', 'error');
                });
        });

        $(document).on('click', '.btn-edit-img', function () {
            $('#editImageId').val($(this).data('imgid'));
            $('#editCurrentPreview').attr('src', $(this).data('imgurl'));
            $('#newImageFileInput').val('');
            $('#newImagePreviewWrapper').hide();
            editedImageFile = null;

            const modalElement = document.getElementById('editImageModal');

            if (modalElement) {
                new bootstrap.Modal(modalElement).show();
            }
        });

        $('#newImageFileInput').on('change', async function () {
            const file = this.files[0];

            if (!validateFile(file)) {
                this.value = '';
                return;
            }

            try {
                editedImageFile = await cropImageToSquare(file);
                renderSingleImagePreview(editedImageFile, '#newImagePreview', '#newImagePreviewWrapper');
            }
            catch {
                editedImageFile = null;
                this.value = '';
                notify('Không thể xử lý ảnh', 'error');
            }
        });

        $('#btnConfirmEditImage').on('click', function () {
            if (!editedImageFile) {
                notify('Vui lòng chọn ảnh', 'error');
                return;
            }

            const drinkId = $('#currentDrinkId').val();
            const formData = new FormData();

            formData.append('drinkImageId', $('#editImageId').val());
            formData.append('newImageFile', editedImageFile);

            const $btn = $(this);

            $btn
                .prop('disabled', true)
                .html('<i class="fas fa-spinner fa-spin"></i> Đang lưu...');

            $.ajax({
                url: '/Admin/AdminDrink/UpdateImage',
                type: 'POST',
                data: formData,
                processData: false,
                contentType: false,
                success: function (res) {
                    $btn
                        .prop('disabled', false)
                        .html('<i class="fas fa-save"></i> Lưu ảnh mới');

                    if (res.success) {
                        notify(res.message || 'Cập nhật ảnh thành công', 'success');
                        hideModal('editImageModal');
                        loadImages(drinkId);
                        reloadDrinkTable();
                        return;
                    }

                    notify(res.message || 'Cập nhật ảnh thất bại', 'error');
                },
                error: function () {
                    $btn
                        .prop('disabled', false)
                        .html('<i class="fas fa-save"></i> Lưu ảnh mới');

                    notify('Có lỗi xảy ra', 'error');
                }
            });
        });

        $('#btnUploadDrinkImage').on('click', async function () {
            const drinkId = $('#currentDrinkId').val();
            const input = document.getElementById('uploadImageInput');
            const file = input?.files[0];

            if (!drinkId) {
                notify('Không tìm thấy nước uống cần thêm ảnh', 'error');
                return;
            }

            if (!validateFile(file)) {
                return;
            }

            const $btn = $(this);

            try {
                const croppedFile = await cropImageToSquare(file);
                const formData = new FormData();

                formData.append('drinkId', drinkId);
                formData.append('imageFile', croppedFile);
                formData.append('isDefault', $('#uploadDefaultSwitch').is(':checked'));

                $btn
                    .prop('disabled', true)
                    .html('<i class="fas fa-spinner fa-spin me-1"></i> Đang tải...');

                $.ajax({
                    url: '/Admin/AdminDrink/UploadImage',
                    type: 'POST',
                    data: formData,
                    processData: false,
                    contentType: false,
                    success: function (res) {
                        $btn
                            .prop('disabled', false)
                            .html('<i class="fas fa-upload me-1"></i> Tải ảnh');

                        if (res.success) {
                            notify(res.message || 'Thêm ảnh thành công', 'success');
                            resetUploadControls();
                            loadImages(drinkId);
                            reloadDrinkTable();
                            return;
                        }

                        notify(res.message || 'Thêm ảnh thất bại', 'error');
                    },
                    error: function () {
                        $btn
                            .prop('disabled', false)
                            .html('<i class="fas fa-upload me-1"></i> Tải ảnh');

                        notify('Có lỗi xảy ra khi tải ảnh', 'error');
                    }
                });
            }
            catch {
                notify('Không thể xử lý ảnh', 'error');
            }
        });
    }

    function loadImages(drinkId) {
        const $container = $('#imageListContainer');

        if (!$container.length || !drinkId) {
            return;
        }

        $container.html(
            '<div class="col-12 text-center text-muted"><i class="fas fa-spinner fa-spin"></i> Đang tải ảnh...</div>'
        );

        $.get(
            '/Admin/AdminDrink/GetImages',
            { drinkId: drinkId },
            function (res) {
                if (res.success) {
                    renderImages(res.data);
                    return;
                }

                $container.html(
                    '<div class="col-12 text-danger text-center">Lỗi tải danh sách ảnh.</div>'
                );

                notify('Lỗi tải ảnh', 'error');
            });
    }

    function renderImages(images) {
        const $container = $('#imageListContainer');

        $container.empty();

        if (!images || images.length === 0) {
            $container.html(
                '<div class="col-12 text-center text-muted py-3">Chưa có ảnh nào.</div>'
            );
            return;
        }

        images.forEach(function (img) {
            const defaultBadge = img.isDefault
                ? '<span class="badge bg-success position-absolute top-0 start-0 m-1">Mặc định</span>'
                : '';

            const defaultButton = img.isDefault
                ? ''
                : `
                    <button
                        type="button"
                        class="btn btn-sm btn-outline-success flex-grow-1 btn-set-default"
                        data-imgid="${img.drinkImageId}">
                        <i class="fas fa-check-circle"></i>
                        Mặc định
                    </button>`;

            $container.append(`
                <div class="col-md-4 col-sm-6">
                    <div class="card h-100 shadow-sm ${img.isDefault ? 'border-success' : ''}">
                        <div class="position-relative" style="height:150px;">
                            ${defaultBadge}
                            <img
                                src="${escapeHtml(img.imageUrl)}"
                                alt="Ảnh nước uống"
                                class="w-100 h-100"
                                style="object-fit:cover;">
                        </div>

                        <div class="card-body p-2 text-center">
                            <div class="d-flex gap-1">
                                ${defaultButton}

                                <button
                                    type="button"
                                    class="btn btn-sm btn-outline-warning ${img.isDefault ? 'flex-grow-1' : ''} btn-edit-img"
                                    data-imgid="${img.drinkImageId}"
                                    data-imgurl="${escapeHtml(img.imageUrl)}">
                                    <i class="fas fa-pencil-alt"></i>
                                    ${img.isDefault ? 'Sửa' : ''}
                                </button>

                                <button
                                    type="button"
                                    class="btn btn-sm btn-outline-danger btn-delete-img"
                                    data-imgid="${img.drinkImageId}">
                                    <i class="fas fa-trash"></i>
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            `);
        });
    }

    function renderSingleImagePreview(file, imageSelector, wrapperSelector) {
        const reader = new FileReader();

        reader.onload = function (e) {
            $(imageSelector).attr('src', e.target.result);
            $(wrapperSelector).show();
        };

        reader.readAsDataURL(file);
    }

    function resetUploadControls() {
        $('#uploadImageInput').val('');
        $('#uploadDefaultSwitch').prop('checked', false);
    }

    function hideModal(id) {
        const modalElement = document.getElementById(id);

        if (!modalElement) {
            return;
        }

        const modal = bootstrap.Modal.getInstance(modalElement);

        if (modal) {
            modal.hide();
        }
    }

    document.addEventListener('drink-ai-image-ready', async event => {
        const file = event.detail?.file;
        if (!file) { event.detail?.complete?.(false); return; }
        try {
            const croppedFile = await cropImageToSquare(file);
            selectedCreateFiles = [croppedFile];
            setCreateDefaultIndex(0);
            renderCreatePreview();
            event.detail?.complete?.(true);
        } catch {
            event.detail?.complete?.(false);
        }
    });

    function reloadDrinkTable() {
        const $table = $('#datatablesSimple');

        if (!$table.length) {
            return;
        }

        $.get('/Admin/AdminDrink/IndexPartial' + window.location.search, function (html) {
            const $html = $(html);
            const $tbody = $html.is('tbody')
                ? $html
                : $html.find('tbody');

            if ($tbody.length) {
                $table.find('tbody').replaceWith($tbody);
            }
        });
    }
});

// AI suggestion for Drink Create. It never submits the form or persists relations.
(function initDrinkAiSuggestion() {
    const button = document.getElementById('btnDrinkAiSuggestionLegacy');
    if (!button) return;
    const form = document.getElementById('drinkCreateForm');
    const name = document.getElementById('DrinkCreateDTO_Name');
    const category = document.getElementById('DrinkCreateDTO_CategoryId');
    const productType = document.getElementById('DrinkCreateDTO_ProductTypeId');
    const code = document.getElementById('DrinkCreateDTO_DrinkCode');
    const description = document.getElementById('DrinkCreateDTO_Description');
    const panel = document.getElementById('drinkAiSuggestionPanel');
    let suggestion = null;
    let controller = null;

    const notifyAi = (message, type = 'success') => typeof toast === 'function' ? toast(message, type) : alert(message);
    const clear = () => {
        suggestion = null;
        panel.classList.add('d-none');
        document.getElementById('drinkAiSizes').innerHTML = '';
        document.getElementById('drinkAiToppings').innerHTML = '';
    };
    const chips = items => (items || []).map(x => `<span class="ai-chip">${String(x.code || x.name).replace(/[<>&]/g, '')}</span>`).join('') || '<span class="text-muted">Không có</span>';

    button.addEventListener('click', async () => {
        if (!name.value.trim() || !category.value || !productType.value) {
            notifyAi('Vui lòng nhập tên, danh mục và loại sản phẩm.', 'error');
            return;
        }
        controller?.abort();
        controller = new AbortController();
        const timeout = setTimeout(() => controller.abort(), 125000);
        const original = button.innerHTML;
        button.disabled = true;
        button.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i>Đang gợi ý...';
        clear();
        try {
            const response = await fetch('/Admin/AdminDrink/AiSuggestion', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': form.querySelector('input[name="__RequestVerificationToken"]').value
                },
                body: JSON.stringify({ name: name.value.trim(), categoryId: Number(category.value), productTypeId: Number(productType.value) }),
                signal: controller.signal
            });
            const result = await response.json();
            if (!response.ok || !result.success) throw new Error(result.message || 'Không thể tạo gợi ý.');
            suggestion = result.data;
            document.getElementById('drinkAiCode').textContent = suggestion.drinkCode;
            document.getElementById('drinkAiDescription').textContent = suggestion.description;
            document.getElementById('drinkAiSizes').innerHTML = chips(suggestion.sizes);
            document.getElementById('drinkAiToppings').innerHTML = chips(suggestion.toppings);
            document.getElementById('drinkAiWarnings').textContent = (suggestion.warnings || []).join(' ');
            document.getElementById('drinkAiSource').textContent = suggestion.usedOllama ? 'Ollama' : 'C# fallback';
            panel.classList.remove('d-none');
        } catch (error) {
            if (error.name !== 'AbortError') notifyAi(error.message, 'error');
        } finally {
            clearTimeout(timeout);
            button.disabled = false;
            button.innerHTML = original;
        }
    });

    document.getElementById('btnApplyDrinkAi').addEventListener('click', async () => {
        if (!suggestion) return;
        if ((code.value.trim() || description.value.trim()) && !window.confirm('Ghi đè mã/mô tả hiện tại bằng gợi ý AI?')) return;
        code.value = suggestion.drinkCode;
        description.value = suggestion.description;
        clear();
        notifyAi('Đã điền gợi ý. Dữ liệu chưa được lưu.');
    });
    document.getElementById('btnDismissDrinkAi').addEventListener('click', clear);
    [name, category, productType].forEach(x => x.addEventListener('change', clear));
    name.addEventListener('input', clear);
})();

// AI form options for Drink Create. Nothing is persisted or submitted here.
(function initFullDrinkAiSuggestion() {
    const button = document.getElementById('btnDrinkAiSuggestion');
    if (!button) return;
    const form = document.getElementById('drinkCreateForm');
    const idea = document.getElementById('drinkAiIdea');
    const fields = {
        name: document.getElementById('DrinkCreateDTO_Name'),
        category: document.getElementById('DrinkCreateDTO_CategoryId'),
        productType: document.getElementById('DrinkCreateDTO_ProductTypeId'),
        code: document.getElementById('DrinkCreateDTO_DrinkCode'),
        description: document.getElementById('DrinkCreateDTO_Description')
    };
    const panel = document.getElementById('drinkAiSuggestionPanel');
    const image = document.getElementById('drinkAiImage');
    const attribution = document.getElementById('drinkAiImageAttribution');
    const retryImage = document.getElementById('btnRegenerateDrinkAiImage');
    const optionList = document.getElementById('drinkAiOptionList');
    const applyButton = document.getElementById('btnApplyDrinkAi');
    const warnings = document.getElementById('drinkAiWarnings');
    let options = [];
    let selectedOption = null;
    let generatedImageFile = null;
    let generatedImageUrl = null;
    let textController = null;
    let imageController = null;
    const token = () => form.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const notify = (message, type = 'success') => typeof toast === 'function' ? toast(message, type) : alert(message);
    const clearImage = () => {
        imageController?.abort();
        imageController = null;
        generatedImageFile = null;
        if (generatedImageUrl) URL.revokeObjectURL(generatedImageUrl);
        generatedImageUrl = null;
        image.removeAttribute('src');
        image.classList.add('d-none');
        attribution.replaceChildren();
        attribution.classList.add('d-none');
    };
    const clear = () => {
        clearImage();
        options = [];
        selectedOption = null;
        optionList.replaceChildren();
        warnings.textContent = '';
        applyButton.disabled = true;
        retryImage.disabled = true;
        panel.classList.add('d-none');
    };
    const requestJson = async (url, body, timeoutMs, controller) => {
        const timeout = setTimeout(() => controller.abort(), timeoutMs);
        try {
            const response = await fetch(url, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token() },
                body: JSON.stringify(body), signal: controller.signal
            });
            const result = await response.json();
            if (!response.ok || !result.success) throw new Error(result.message || 'Không thể tạo gợi ý.');
            return result;
        } finally { clearTimeout(timeout); }
    };
    const base64File = data => {
        const bytes = atob(data.base64Data);
        const array = new Uint8Array(bytes.length);
        for (let index = 0; index < bytes.length; index++) array[index] = bytes.charCodeAt(index);
        return new File([array], data.fileName || 'drink-ai.png', { type: data.contentType || 'image/png' });
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
    const generateImage = async () => {
        if (!selectedOption?.fields?.imagePrompt) return;
        clearImage();
        imageController = new AbortController();
        const activeImageController = imageController;
        applyButton.disabled = true;
        retryImage.disabled = true;
        retryImage.textContent = 'Đang tạo ảnh...';
        try {
            const result = await requestJson('/Admin/AdminDrink/AiImageSuggestion', {
                imagePrompt: selectedOption.fields.imagePrompt,
                fileNamePrefix: selectedOption.fields.drinkCode || 'drink_ai',
                excludedExternalImageIds: selectedOption.excludedExternalImageIds || []
            }, 190000, activeImageController);
            if (result.data.externalImageId) {
                selectedOption.excludedExternalImageIds = [
                    ...(selectedOption.excludedExternalImageIds || []),
                    Number(result.data.externalImageId)
                ];
            }
            generatedImageFile = base64File(result.data);
            generatedImageUrl = URL.createObjectURL(generatedImageFile);
            image.src = generatedImageUrl;
            image.classList.remove('d-none');
            renderAttribution(result.data);
        } catch (error) {
            if (error.name !== 'AbortError') notify(`${error.message} Bạn vẫn có thể áp dụng phần nội dung.`, 'error');
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
        options = Array.isArray(result.options) ? result.options.slice(0, 3) : [];
        optionList.replaceChildren();
        options.forEach(option => {
            const f = option.fields || {};
            const card = document.createElement('button');
            card.type = 'button';
            card.className = 'ai-option-card text-start';
            const title = document.createElement('strong');
            title.textContent = option.title || f.name || 'Gợi ý đồ uống';
            const meta = document.createElement('div');
            meta.className = 'small text-muted mt-1';
            meta.textContent = `${f.drinkCode || ''} · ${f.categoryName || ''} · ${f.productTypeName || ''}`;
            const description = document.createElement('div');
            description.className = 'small mt-2';
            description.textContent = f.description || '';
            card.append(title, meta, description);
            card.addEventListener('click', () => selectOption(option, card));
            optionList.appendChild(card);
        });
        warnings.textContent = (result.warnings || []).join(' ');
        document.getElementById('drinkAiSource').textContent = result.usedOllama ? 'Ollama + C#' : 'C# fallback';
        panel.classList.remove('d-none');
    };

    button.addEventListener('click', async () => {
        textController?.abort();
        textController = new AbortController();
        const activeTextController = textController;
        const original = button.innerHTML;
        button.disabled = true;
        button.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i>Đang gợi ý...';
        clear();
        try {
            const result = await requestJson('/Admin/AdminDrink/AiSuggestion', {
                idea: idea.value.trim() || null,
                currentDrinkCode: fields.code.value.trim() || null,
                currentName: fields.name.value.trim() || null,
                currentDescription: fields.description.value.trim() || null,
                currentCategoryId: fields.category.value ? Number(fields.category.value) : null,
                currentProductTypeId: fields.productType.value ? Number(fields.productType.value) : null
            }, 130000, activeTextController);
            renderOptions(result);
        } catch (error) {
            if (error.name !== 'AbortError') notify(error.message, 'error');
        } finally {
            if (textController === activeTextController) {
                button.disabled = false;
                button.innerHTML = original;
            }
        }
    });
    retryImage.addEventListener('click', generateImage);
    applyButton.addEventListener('click', async () => {
        if (!selectedOption?.canApply) return notify('Vui lòng chọn một gợi ý hợp lệ.', 'error');
        const suggestion = selectedOption.fields;
        const willOverwrite = Object.values(fields).some(x => x.value?.trim()) || document.querySelector('#createImagePreview .image-preview-card');
        if (willOverwrite && !window.confirm('Một số dữ liệu hoặc ảnh hiện tại sẽ được thay thế. Tiếp tục áp dụng gợi ý AI?')) return;
        fields.name.value = suggestion.name || '';
        fields.category.value = String(suggestion.categoryId);
        fields.productType.value = String(suggestion.productTypeId);
        fields.code.value = suggestion.drinkCode;
        fields.description.value = suggestion.description;
        if (generatedImageFile) {
            const imageApplied = await new Promise(resolve => document.dispatchEvent(
                new CustomEvent('drink-ai-image-ready', { detail: { file: generatedImageFile, complete: resolve } })));
            if (!imageApplied) notify('Không thể đưa ảnh AI vào trình xử lý ảnh; các trường văn bản vẫn đã được điền.', 'error');
        }
        clear();
        notify('Đã áp dụng gợi ý vào form. Vui lòng kiểm tra trước khi lưu.');
    });
    document.getElementById('btnDismissDrinkAi').addEventListener('click', clear);
    const invalidate = () => { textController?.abort(); clear(); };
    [idea, ...Object.values(fields)].forEach(x => x.addEventListener(x.tagName === 'SELECT' ? 'change' : 'input', invalidate));
})();
