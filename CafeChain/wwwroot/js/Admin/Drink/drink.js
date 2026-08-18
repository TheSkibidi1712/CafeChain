$(document).ready(function () {
    const MAX_FILE_SIZE = 5 * 1024 * 1024;
    const ALLOWED_EXT = ['.jpg', '.jpeg', '.png'];
    const CROP_SIZE = 1000;

    let selectedCreateFiles = [];
    let editedImageFile = null;
    let isCreateSubmitting = false;
    let isEditSubmitting = false;
    let restoreImageManagerAfterEdit = false;

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

    $(document).on('click', '.js-toggle-drink', async function () {
        const id = Number(this.dataset.id);
        if (!id) return;
        if (window.Swal) {
            const result = await window.Swal.fire({
                title: 'Xác nhận',
                text: 'Bạn có chắc muốn đổi trạng thái đồ uống?',
                icon: 'warning',
                showCancelButton: true,
                confirmButtonColor: '#70482f',
                cancelButtonColor: '#6c757d',
                confirmButtonText: 'Đồng ý',
                cancelButtonText: 'Hủy'
            });
            if (!result.isConfirmed) return;
        } else if (!window.confirm('Bạn có chắc muốn đổi trạng thái đồ uống?')) return;
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

        const submitDoneHtml = '<i class="fas fa-save me-1"></i> Tạo Nước Uống';
        const $validationSummary = $('#drinkCreateValidationSummary');
        let validationFeedbackPending = false;

        function restoreCreateSubmit() {
            isCreateSubmitting = false;
            window.AdminMutationGuard?.unlockForm($form[0]);
            $form.removeAttr('data-submit-busy data-submit-pending aria-busy');
            $form.find('button[type="submit"]')
                .prop('disabled', false)
                .removeAttr('aria-busy')
                .removeClass('is-submitting')
                .html(submitDoneHtml);
        }

        function showValidationFeedback(validator) {
            restoreCreateSubmit();
            const invalidElements = (validator?.errorList || [])
                .map(item => item.element)
                .filter(Boolean);
            invalidElements.forEach(element => {
                element.classList.add('is-invalid');
                element.setAttribute('aria-invalid', 'true');
            });

            const firstInvalid = invalidElements[0]
                || $form[0].querySelector('.input-validation-error, [aria-invalid="true"], :invalid');
            $validationSummary.removeClass('d-none');
            firstInvalid?.focus({ preventScroll: false });

            if (!validationFeedbackPending) {
                validationFeedbackPending = true;
                notify('Vui lòng kiểm tra và nhập đầy đủ các trường bắt buộc.', 'warning');
                window.setTimeout(() => { validationFeedbackPending = false; }, 250);
            }
        }

        $form.on('invalid-form.validate', function (_event, validator) {
            showValidationFeedback(validator);
        });

        $form[0].addEventListener('invalid', function () {
            window.setTimeout(() => showValidationFeedback($form.data('validator')), 0);
        }, true);

        $form.on('input change', 'input, select, textarea', function () {
            if (this.checkValidity()) {
                this.classList.remove('is-invalid', 'input-validation-error');
                this.removeAttribute('aria-invalid');
            }
        });

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

            const validator = $form.data('validator');
            if ((validator && !$form.valid()) || (!validator && !$form[0].checkValidity())) {
                showValidationFeedback(validator);
                return;
            }

            $validationSummary.addClass('d-none');

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
                doneHtml: submitDoneHtml,
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
            error: function (xhr) {
                $submitBtn
                    .prop('disabled', false)
                    .html(options.doneHtml);

                options.onComplete();
                const feedback = window.AdminFeedback;
                const message = xhr.status === 0
                    ? (feedback?.networkMessage?.() || 'Không thể kết nối máy chủ. Vui lòng kiểm tra mạng và thử lại.')
                    : (feedback?.resolveMessage?.(xhr.responseJSON, {
                        status: xhr.status,
                        action: $form.attr('id') === 'drinkCreateForm' ? 'create' : 'update',
                        entityName: 'nước uống'
                    }) || xhr.responseJSON?.message || 'Không thể lưu nước uống. Vui lòng thử lại.');
                notify(message, 'error');
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
        const imageManagerElement = document.getElementById('imageModal');
        const editImageElement = document.getElementById('editImageModal');
        editImageElement?.addEventListener('hidden.bs.modal', function () {
            if (!restoreImageManagerAfterEdit || !imageManagerElement) return;
            restoreImageManagerAfterEdit = false;
            bootstrap.Modal.getOrCreateInstance(imageManagerElement).show();
        });

        $(document).on('change', '#uploadImageInput', function () {
            const fileName = this.files && this.files[0] ? this.files[0].name : 'Chưa chọn file nào';
            $('#uploadFileName').text(fileName);
        });

        $(document).on('click', '.btn-manage-images', function () {
            const drinkId = $(this).data('id');
            const drinkName = $(this).data('name');

            $('#currentDrinkId').val(drinkId);
            $('#modalDrinkName').text(drinkName);
            resetUploadControls();
            loadImages(drinkId);

            const modalElement = document.getElementById('imageModal');

            if (modalElement) {
                bootstrap.Modal.getOrCreateInstance(modalElement).show();
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
                const editModal = bootstrap.Modal.getOrCreateInstance(modalElement);
                const managerElement = document.getElementById('imageModal');
                if (managerElement?.classList.contains('show')) {
                    restoreImageManagerAfterEdit = true;
                    managerElement.addEventListener('hidden.bs.modal', () => editModal.show(), { once: true });
                    bootstrap.Modal.getOrCreateInstance(managerElement).hide();
                } else {
                    restoreImageManagerAfterEdit = false;
                    editModal.show();
                }
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
                '<div class="col-12 text-center text-muted py-4"><i class="fas fa-images fa-2x mb-2 d-block opacity-50"></i>Chưa có ảnh nào.</div>'
            );
            return;
        }

        const isModal = $container.closest('.modal').length > 0;
        const colClass = isModal ? 'col-6 col-sm-4 col-md-3' : 'col-6';

        images.forEach(function (img) {
            const defaultBadge = img.isDefault
                ? '<span class="drink-card-badge-default"><i class="fas fa-check-circle me-1"></i>Mặc định</span>'
                : '';

            const defaultButton = img.isDefault
                ? ''
                : `
                    <button
                        type="button"
                        class="btn btn-sm drink-card-btn drink-card-btn-default flex-grow-1 text-truncate btn-set-default"
                        data-imgid="${img.drinkImageId}"
                        title="Đặt làm ảnh mặc định">
                        <i class="fas fa-check me-1"></i>
                        <span>Mặc định</span>
                    </button>`;

            const editButton = `
                <button
                    type="button"
                    class="btn btn-sm drink-card-btn drink-card-btn-edit ${img.isDefault ? 'flex-grow-1' : ''} btn-edit-img"
                    data-imgid="${img.drinkImageId}"
                    data-imgurl="${escapeHtml(img.imageUrl)}"
                    title="Chỉnh sửa ảnh">
                    <i class="fas fa-pencil-alt"></i>
                    ${img.isDefault ? '<span class="ms-1">Sửa</span>' : ''}
                </button>`;

            const deleteButton = `
                <button
                    type="button"
                    class="btn btn-sm drink-card-btn drink-card-btn-delete btn-delete-img"
                    data-imgid="${img.drinkImageId}"
                    title="Xóa ảnh">
                    <i class="fas fa-trash"></i>
                </button>`;

            $container.append(`
                <div class="${colClass}">
                    <div class="drink-gallery-card ${img.isDefault ? 'is-default' : ''}">
                        <div class="drink-gallery-img-wrapper">
                            ${defaultBadge}
                            <img
                                src="${escapeHtml(img.imageUrl)}"
                                alt="Ảnh nước uống"
                                class="drink-gallery-img">
                        </div>

                        <div class="drink-gallery-card-body">
                            <div class="d-flex align-items-center gap-1 w-100">
                                ${defaultButton}
                                ${editButton}
                                ${deleteButton}
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
        $('#uploadFileName').text('Chưa chọn file nào');
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
    const generationMode = document.getElementById('drinkAiGenerationMode');
    const fields = {
        name: document.getElementById('DrinkCreateDTO_Name'),
        category: document.getElementById('DrinkCreateDTO_CategoryId'),
        productType: document.getElementById('DrinkCreateDTO_ProductTypeId'),
        code: document.getElementById('DrinkCreateDTO_DrinkCode'),
        description: document.getElementById('DrinkCreateDTO_Description')
    };
    if (window.CafeChainAIImagePipeline) {
        window.CafeChainAIImagePipeline.create({
            ids: {
                button: 'btnDrinkAiSuggestion', form: 'drinkCreateForm', idea: 'drinkAiIdea',
                panel: 'drinkAiSuggestionPanel', optionList: 'drinkAiOptionList',
                referenceList: 'drinkAiReferenceList', generatedList: 'drinkAiGeneratedList',
                status: 'drinkAiStatus', warnings: 'drinkAiWarnings', source: 'drinkAiSource',
                usePexels: 'btnUseDrinkPexels', generate: 'btnGenerateDrinkAi',
                fallback: 'btnGenerateDrinkAiWithoutReference', retrySearch: 'btnRetryDrinkAiSearch',
                apply: 'btnApplyDrinkAi', dismiss: 'btnDismissDrinkAi'
            },
            urls: {
                suggestions: '/Admin/AdminDrink/AiSuggestion',
                references: '/Admin/AdminDrink/AiReferenceImages',
                usePexels: '/Admin/AdminDrink/AiUsePexelsImage',
                generate: '/Admin/AdminDrink/AiGenerateFromReference',
                generateWithoutReference: '/Admin/AdminDrink/AiGenerateWithoutReference'
            },
            defaultFileName: 'drink-ai.png',
            notify: (message, type) => typeof toast === 'function' ? toast(message, type) : alert(message),
            suggestionPayload: () => ({
                generationMode: Number(generationMode?.value || 0),
                idea: idea.value.trim() || null,
                currentDrinkCode: fields.code.value.trim() || null,
                currentName: fields.name.value.trim() || null,
                currentDescription: fields.description.value.trim() || null,
                currentCategoryId: fields.category.value ? Number(fields.category.value) : null,
                currentProductTypeId: fields.productType.value ? Number(fields.productType.value) : null
            }),
            renderSuggestion: (card, option) => {
                const value = option.fields || {};
                const title = document.createElement('strong');
                title.textContent = option.title || value.name || 'Gợi ý đồ uống';
                const meta = document.createElement('div');
                meta.className = 'small text-muted mt-1';
                meta.textContent = `${value.drinkCode || ''} · ${value.categoryName || ''} · ${value.productTypeName || ''}`;
                const description = document.createElement('div');
                description.className = 'small mt-2';
                description.textContent = value.description || '';
                card.append(title, meta, description);
            },
            fileNamePrefix: option => option.fields?.drinkCode || 'drink_ai',
            invalidateElements: () => [idea, generationMode, ...Object.values(fields)],
            willOverwrite: () => Object.values(fields).some(x => x.value?.trim()) ||
                Boolean(document.querySelector('#createImagePreview .image-preview-card')),
            apply: async (option, file) => {
                const value = option.fields || {};
                const hasCategory = Array.from(fields.category.options).some(x => x.value === String(value.categoryId));
                const hasProductType = Array.from(fields.productType.options).some(x => x.value === String(value.productTypeId));
                if (!hasCategory || !hasProductType) {
                    (typeof toast === 'function' ? toast : alert)('Category hoặc ProductType gợi ý không còn hợp lệ.', 'error');
                    return false;
                }
                fields.name.value = value.name || '';
                fields.category.value = String(value.categoryId);
                fields.productType.value = String(value.productTypeId);
                fields.code.value = value.drinkCode || '';
                fields.description.value = value.description || '';
                const applied = await new Promise(resolve => document.dispatchEvent(
                    new CustomEvent('drink-ai-image-ready', { detail: { file, complete: resolve } })));
                if (!applied) {
                    (typeof toast === 'function' ? toast : alert)('Không thể đưa ảnh AI vào trình xử lý ảnh.', 'error');
                    return false;
                }
                return true;
            }
        });
        return;
    }
})();
