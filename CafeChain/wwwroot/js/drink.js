$(document).ready(function () {

    // ================= CONFIG =================
    const MAX_FILE_SIZE = 5 * 1024 * 1024;
    const ALLOWED_EXT = ['.jpg', '.jpeg', '.png'];

    let selectedFiles = [];

    // ================= VALIDATE FILE =================
    function validateFile(file) {
        if (!file) return false;

        const ext = file.name.substring(file.name.lastIndexOf('.')).toLowerCase();

        if (!ALLOWED_EXT.includes(ext)) {
            toast("Chỉ chấp nhận JPG, JPEG, PNG", "error");
            return false;
        }

        if (file.size > MAX_FILE_SIZE) {
            toast("Ảnh vượt quá 5MB!", "error");
            return false;
        }

        return true;
    }

    // ================= IMAGE MANAGER (GIỮ NGUYÊN) =================

    $('.btn-manage-images').click(function () {
        var drinkId = $(this).data('id');
        var drinkName = $(this).data('name');

        $('#currentDrinkId').val(drinkId);
        $('#modalDrinkName').text(drinkName);

        loadImages(drinkId);

        var myModal = new bootstrap.Modal(document.getElementById('imageModal'));
        myModal.show();
    });

    function loadImages(drinkId) {
        $('#imageListContainer').html('<div class="col-12 text-center text-muted"><i class="fas fa-spinner fa-spin"></i> Đang tải ảnh...</div>');

        $.get('/Admin/AdminDrink/GetImages?drinkId=' + drinkId, function (res) {
            if (res.success) {
                renderImages(res.data);
            } else {
                $('#imageListContainer').html('<div class="col-12 text-danger text-center">Lỗi tải danh sách ảnh.</div>');
                toast("Lỗi tải ảnh", "error");
            }
        });
    }

    function renderImages(images) {
        var container = $('#imageListContainer');
        container.empty();

        if (!images || images.length === 0) {
            container.html('<div class="col-12 text-center text-muted py-3">Chưa có ảnh nào.</div>');
            return;
        }

        images.forEach(function (img) {

            var defaultBadge = img.isDefault
                ? '<span class="badge bg-success position-absolute top-0 start-0 m-1">Mặc định</span>'
                : '';

            var actionBtns = img.isDefault
                ? `<div class="d-flex gap-1">
                        <button class="btn btn-sm btn-outline-warning flex-grow-1 btn-edit-img" data-imgid="${img.drinkImageId}" data-imgurl="${img.imageUrl}">
                            <i class="fas fa-pencil-alt"></i> Sửa
                        </button>
                        <button class="btn btn-sm btn-outline-danger btn-delete-img" data-imgid="${img.drinkImageId}">
                            <i class="fas fa-trash"></i>
                        </button>
                   </div>`
                : `<div class="d-flex gap-1">
                        <button class="btn btn-sm btn-outline-success flex-grow-1 btn-set-default" data-imgid="${img.drinkImageId}">
                            <i class="fas fa-check-circle"></i> Mặc định
                        </button>
                        <button class="btn btn-sm btn-outline-warning btn-edit-img" data-imgid="${img.drinkImageId}" data-imgurl="${img.imageUrl}">
                            <i class="fas fa-pencil-alt"></i>
                        </button>
                        <button class="btn btn-sm btn-outline-danger btn-delete-img" data-imgid="${img.drinkImageId}">
                            <i class="fas fa-trash"></i>
                        </button>
                   </div>`;

            var html = `
                <div class="col-md-4 col-sm-6">
                    <div class="card h-100 shadow-sm ${img.isDefault ? 'border-success' : ''}">
                        <div class="position-relative" style="height:150px;">
                            ${defaultBadge}
                            <img src="${img.imageUrl}" class="w-100 h-100" style="object-fit:cover;">
                        </div>
                        <div class="card-body p-2 text-center">
                            ${actionBtns}
                        </div>
                    </div>
                </div>
            `;
            container.append(html);
        });
    }

    // ================= ACTION =================
    $(document).on('click', '.btn-set-default', function () {
        var drinkImgId = $(this).data('imgid');
        var drinkId = $('#currentDrinkId').val();

        $.post('/Admin/AdminDrink/SetDefaultImage', { drinkId: drinkId, drinkImageId: drinkImgId }, function (res) {
            if (res.success) {
                toast(res.message ?? "Đã cập nhật ảnh mặc định");

                // reload modal images
                loadImages(drinkId);

                // 🔥 reload index table (QUAN TRỌNG)
                reloadDrinkTable();

            } else {
                toast(res.message, "error");
            }
        });
    });

    $(document).on('click', '.btn-delete-img', function () {
        const drinkImgId = $(this).data('imgid');
        const drinkId = $('#currentDrinkId').val();

        if (!confirm("Bạn có chắc muốn xóa ảnh này?")) return;

        $.post('/Admin/AdminDrink/DeleteImage', {
            drinkImageId: drinkImgId
        }, function (res) {

            if (res.success) {
                toast(res.message || "Đã xóa ảnh thành công", "success");

                loadImages(drinkId);
                reloadDrinkTable();
            }
            else {
                toast(res.message || "Xóa ảnh thất bại", "error");
            }
        }).fail(function () {
            toast("Có lỗi hệ thống khi xóa ảnh", "error");
        });
    });


    // ================= EDIT IMAGE =================

    $(document).on('click', '.btn-edit-img', function () {
        $('#editImageId').val($(this).data('imgid'));
        $('#editCurrentPreview').attr('src', $(this).data('imgurl'));
        $('#newImageFileInput').val('');
        $('#newImagePreviewWrapper').hide();

        new bootstrap.Modal(document.getElementById('editImageModal')).show();
    });

    $('#newImageFileInput').change(function () {
        var file = this.files[0];

        if (!validateFile(file)) {
            this.value = "";
            return;
        }

        var reader = new FileReader();
        reader.onload = function (e) {
            $('#newImagePreview').attr('src', e.target.result);
            $('#newImagePreviewWrapper').show();
        };
        reader.readAsDataURL(file);
    });

    $('#btnConfirmEditImage').click(function () {

        var fileInput = document.getElementById('newImageFileInput');

        if (!fileInput.files || fileInput.files.length === 0) {
            toast("Vui lòng chọn ảnh", "error");
            return;
        }

        var file = fileInput.files[0];

        if (!validateFile(file)) return;

        var formData = new FormData();
        formData.append('drinkImageId', $('#editImageId').val());
        formData.append('newImageFile', file);

        var btn = $(this);
        btn.prop('disabled', true).html('<i class="fas fa-spinner fa-spin"></i> Đang lưu...');

        $.ajax({
            url: '/Admin/AdminDrink/UpdateImage',
            type: 'POST',
            data: formData,
            processData: false,
            contentType: false,
            success: function (res) {
                btn.prop('disabled', false).html('<i class="fas fa-save"></i> Lưu ảnh mới');

                if (res.success) {
                    toast("Cập nhật thành công");
                    bootstrap.Modal.getInstance(document.getElementById('editImageModal')).hide();
                    loadImages($('#currentDrinkId').val());
                } else {
                    toast(res.message, "error");
                }
            },
            error: function () {
                btn.prop('disabled', false).html('<i class="fas fa-save"></i> Lưu ảnh mới');
                toast("Có lỗi xảy ra", "error");
            }
        });
    });

    // ================= CREATE - MULTI IMAGE + DRAG DROP =================

    $('#imageFilesInput').on('change', function (e) {
        handleFiles(e.target.files);
        this.value = "";
    });

    function handleFiles(files) {
        Array.from(files).forEach(file => {
            if (validateFile(file)) {
                selectedFiles.push(file);
            }
        });

        renderPreview();
    }

    function renderPreview() {
        const container = $('#previewContainer');
        container.empty();

        selectedFiles.forEach((file, index) => {

            const reader = new FileReader();

            reader.onload = function (e) {
                container.append(`
                    <div class="col-4 position-relative">
                        <img src="${e.target.result}" class="img-fluid rounded">
                        <button class="btn btn-sm btn-danger position-absolute top-0 end-0 btn-remove-img" data-index="${index}">
                            <i class="fas fa-times"></i>
                        </button>
                    </div>
                `);
            };

            reader.readAsDataURL(file);
        });
    }

    $(document).on('click', '.btn-remove-img', function () {
        const index = $(this).data('index');
        selectedFiles.splice(index, 1);
        renderPreview();
    });

    // DRAG DROP
    $('#dropZone').on('dragover', function (e) {
        e.preventDefault();
        $(this).addClass('border-primary bg-light');
    });

    $('#dropZone').on('dragleave drop', function (e) {
        e.preventDefault();
        $(this).removeClass('border-primary bg-light');
    });

    $('#dropZone').on('drop', function (e) {
        e.preventDefault();
        e.stopPropagation();
        $(this).removeClass('border-primary');

        const files = e.originalEvent.dataTransfer.files;
        handleFiles(files);
    });


    // SYNC FILES TRƯỚC KHI SUBMIT
    $('form').on('submit', function () {

        if (selectedFiles.length === 0) return;

        const dataTransfer = new DataTransfer();

        selectedFiles.forEach(file => {
            dataTransfer.items.add(file);
        });

        document.getElementById('imageFilesInput').files = dataTransfer.files;
    });

    $('form').on('submit', function (e) {
        e.preventDefault();

        const formData = new FormData(this);

        $.ajax({
            url: this.action,
            type: this.method,
            data: formData,
            processData: false,
            contentType: false,

            success: function (res) {
                if (res.success) {
                    toast(res.message, "success");

                    setTimeout(() => {
                        window.location.href = res.redirectUrl;
                    }, 1000);
                } else {
                    toast(res.message, "error");
                }
            },

            error: function () {
                toast("Có lỗi xảy ra", "error");
            }
        });
    });
    // ================= STATUS =================

    function updateStatusLabel() {
        if ($('#activeStatusSwitch').is(':checked')) {
            $('#activeStatusLabel').text('Trạng thái: Đang bán')
                .removeClass('text-danger')
                .addClass('text-success');
        } else {
            $('#activeStatusLabel').text('Trạng thái: Ngừng bán')
                .removeClass('text-success')
                .addClass('text-danger');
        }
    }

    updateStatusLabel();

    $('#activeStatusSwitch').change(function () {
        updateStatusLabel();
    });
    function reloadDrinkTable() {
        $.get('/Admin/AdminDrink/IndexPartial', function (html) {

            const table = $('#datatablesSimple').DataTable();

            // clear data cũ
            table.clear();

            // parse rows mới
            const newRows = $(html).find('tbody tr');

            newRows.each(function () {
                table.row.add($(this));
            });

            // vẽ lại table
            table.draw();
        });
    }
});