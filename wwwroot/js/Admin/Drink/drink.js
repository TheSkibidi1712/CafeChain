$(document).ready(function () {

    // ================= CONFIG =================

    const MAX_FILE_SIZE = 5 * 1024 * 1024;

    const ALLOWED_EXT = [
        '.jpg',
        '.jpeg',
        '.png'
    ];

    const CROP_SIZE = 1000;

    let selectedFiles = [];

    window.editedImageFile = null;

    let isSubmitting = false;
    // ================= VALIDATE FILE =================

    function validateFile(file) {

        if (!file) {
            return false;
        }

        const ext = file.name
            .substring(file.name.lastIndexOf('.'))
            .toLowerCase();

        if (!ALLOWED_EXT.includes(ext)) {

            toast(
                "Chỉ chấp nhận JPG, JPEG, PNG",
                "error"
            );

            return false;
        }

        if (file.size > MAX_FILE_SIZE) {

            toast(
                "Ảnh vượt quá 5MB",
                "error"
            );

            return false;
        }

        return true;
    }

    // ================= AUTO CROP 1:1 =================

    async function cropImageToSquare(file) {

        return new Promise((resolve, reject) => {

            const reader = new FileReader();

            reader.onload = function (event) {

                const img = new Image();

                img.onload = function () {

                    const canvas =
                        document.createElement('canvas');

                    canvas.width = CROP_SIZE;
                    canvas.height = CROP_SIZE;

                    const ctx =
                        canvas.getContext('2d');

                    const minSide =
                        Math.min(
                            img.width,
                            img.height
                        );

                    const sx =
                        (img.width - minSide) / 2;

                    const sy =
                        (img.height - minSide) / 2;

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

                            const croppedFile =
                                new File(
                                    [blob],
                                    file.name,
                                    {
                                        type: 'image/jpeg'
                                    });

                            resolve(croppedFile);
                        },
                        'image/jpeg',
                        0.9
                    );
                };

                img.onerror = reject;

                img.src = event.target.result;
            };

            reader.readAsDataURL(file);
        });
    }

    // ================= IMAGE MANAGER =================

    $('.btn-manage-images').click(function () {

        var drinkId =
            $(this).data('id');

        var drinkName =
            $(this).data('name');

        $('#currentDrinkId')
            .val(drinkId);

        $('#modalDrinkName')
            .text(drinkName);

        loadImages(drinkId);

        var myModal =
            new bootstrap.Modal(
                document.getElementById(
                    'imageModal'
                )
            );

        myModal.show();
    });

    function loadImages(drinkId) {

        $('#imageListContainer').html(
            '<div class="col-12 text-center text-muted"><i class="fas fa-spinner fa-spin"></i> Đang tải ảnh...</div>'
        );

        $.get(
            '/Admin/AdminDrink/GetImages?drinkId=' + drinkId,
            function (res) {

                if (res.success) {

                    renderImages(res.data);
                }
                else {

                    $('#imageListContainer').html(
                        '<div class="col-12 text-danger text-center">Lỗi tải danh sách ảnh.</div>'
                    );

                    toast(
                        "Lỗi tải ảnh",
                        "error"
                    );
                }
            });
    }

    function renderImages(images) {

        var container =
            $('#imageListContainer');

        container.empty();

        if (!images || images.length === 0) {

            container.html(
                '<div class="col-12 text-center text-muted py-3">Chưa có ảnh nào.</div>'
            );

            return;
        }

        images.forEach(function (img) {

            var defaultBadge =
                img.isDefault
                    ? '<span class="badge bg-success position-absolute top-0 start-0 m-1">Mặc định</span>'
                    : '';

            var actionBtns =
                img.isDefault
                    ? `
                        <div class="d-flex gap-1">

                            <button
                                class="btn btn-sm btn-outline-warning flex-grow-1 btn-edit-img"
                                data-imgid="${img.drinkImageId}"
                                data-imgurl="${img.imageUrl}">

                                <i class="fas fa-pencil-alt"></i>
                                Sửa

                            </button>

                            <button
                                class="btn btn-sm btn-outline-danger btn-delete-img"
                                data-imgid="${img.drinkImageId}">

                                <i class="fas fa-trash"></i>

                            </button>

                        </div>
                    `
                    : `
                        <div class="d-flex gap-1">

                            <button
                                class="btn btn-sm btn-outline-success flex-grow-1 btn-set-default"
                                data-imgid="${img.drinkImageId}">

                                <i class="fas fa-check-circle"></i>
                                Mặc định

                            </button>

                            <button
                                class="btn btn-sm btn-outline-warning btn-edit-img"
                                data-imgid="${img.drinkImageId}"
                                data-imgurl="${img.imageUrl}">

                                <i class="fas fa-pencil-alt"></i>

                            </button>

                            <button
                                class="btn btn-sm btn-outline-danger btn-delete-img"
                                data-imgid="${img.drinkImageId}">

                                <i class="fas fa-trash"></i>

                            </button>

                        </div>
                    `;

            var html = `
                <div class="col-md-4 col-sm-6">

                    <div class="card h-100 shadow-sm ${img.isDefault ? 'border-success' : ''}">

                        <div
                            class="position-relative"
                            style="height:150px;">

                            ${defaultBadge}

                            <img
                                src="${img.imageUrl}"
                                class="w-100 h-100"
                                style="object-fit:cover;">

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

    // ================= SET DEFAULT =================

    $(document).on(
        'click',
        '.btn-set-default',
        function () {

            var drinkImgId =
                $(this).data('imgid');

            var drinkId =
                $('#currentDrinkId').val();

            $.post(
                '/Admin/AdminDrink/SetDefaultImage',
                {
                    drinkId: drinkId,
                    drinkImageId: drinkImgId
                },
                function (res) {

                    if (res.success) {

                        toast(
                            res.message ??
                            "Đã cập nhật ảnh mặc định"
                        );

                        loadImages(drinkId);

                        reloadDrinkTable();
                    }
                    else {

                        toast(
                            res.message,
                            "error"
                        );
                    }
                });
        });

    // ================= DELETE IMAGE =================

    $(document).on(
        'click',
        '.btn-delete-img',
        function () {

            const drinkImgId =
                $(this).data('imgid');

            const drinkId =
                $('#currentDrinkId').val();

            if (
                !confirm(
                    "Bạn có chắc muốn xóa ảnh này?"
                )
            ) {
                return;
            }

            $.post(
                '/Admin/AdminDrink/DeleteImage',
                {
                    drinkImageId: drinkImgId
                },
                function (res) {

                    if (res.success) {

                        toast(
                            res.message ||
                            "Đã xóa ảnh thành công",
                            "success"
                        );

                        loadImages(drinkId);

                        reloadDrinkTable();
                    }
                    else {

                        toast(
                            res.message ||
                            "Xóa ảnh thất bại",
                            "error"
                        );
                    }
                })
                .fail(function () {

                    toast(
                        "Có lỗi hệ thống khi xóa ảnh",
                        "error"
                    );
                });
        });

    // ================= EDIT IMAGE =================

    $(document).on(
        'click',
        '.btn-edit-img',
        function () {

            $('#editImageId')
                .val(
                    $(this).data('imgid')
                );

            $('#editCurrentPreview')
                .attr(
                    'src',
                    $(this).data('imgurl')
                );

            $('#newImageFileInput')
                .val('');

            $('#newImagePreviewWrapper')
                .hide();

            window.editedImageFile = null;

            new bootstrap.Modal(
                document.getElementById(
                    'editImageModal'
                )
            ).show();
        });

    $('#newImageFileInput')
        .change(async function () {

            const file =
                this.files[0];

            if (!validateFile(file)) {

                this.value = "";

                return;
            }

            try {

                const croppedFile =
                    await cropImageToSquare(file);

                window.editedImageFile =
                    croppedFile;

                const reader =
                    new FileReader();

                reader.onload =
                    function (e) {

                        $('#newImagePreview')
                            .attr(
                                'src',
                                e.target.result
                            );

                        $('#newImagePreviewWrapper')
                            .show();
                    };

                reader.readAsDataURL(
                    croppedFile
                );
            }
            catch {

                toast(
                    "Không thể xử lý ảnh",
                    "error"
                );
            }
        });
    $('#btnConfirmEditImage').click(function () {

        if (!window.editedImageFile) {

            toast(
                "Vui lòng chọn ảnh",
                "error"
            );

            return;
        }

        var formData =
            new FormData();

        formData.append(
            'drinkImageId',
            $('#editImageId').val()
        );

        formData.append(
            'newImageFile',
            window.editedImageFile
        );

        var btn =
            $(this);

        btn
            .prop('disabled', true)
            .html(
                '<i class="fas fa-spinner fa-spin"></i> Đang lưu...'
            );

        $.ajax({

            url:
                '/Admin/AdminDrink/UpdateImage',

            type:
                'POST',

            data:
                formData,

            processData:
                false,

            contentType:
                false,

            success:
                function (res) {

                    btn
                        .prop('disabled', false)
                        .html(
                            '<i class="fas fa-save"></i> Lưu ảnh mới'
                        );

                    if (res.success) {

                        toast(
                            "Cập nhật thành công",
                            "success"
                        );

                        bootstrap
                            .Modal
                            .getInstance(
                                document.getElementById(
                                    'editImageModal'
                                )
                            )
                            .hide();

                        loadImages(
                            $('#currentDrinkId').val()
                        );

                        reloadDrinkTable();
                    }
                    else {

                        toast(
                            res.message,
                            "error"
                        );
                    }
                },

            error:
                function () {

                    btn
                        .prop('disabled', false)
                        .html(
                            '<i class="fas fa-save"></i> Lưu ảnh mới'
                        );

                    toast(
                        "Có lỗi xảy ra",
                        "error"
                    );
                }
        });
    });

    // ================= CREATE - MULTI IMAGE =================

    $('#imageFilesInput').on(
        'change',
        async function (e) {

            await handleFiles(
                e.target.files
            );

            this.value = "";
        });

    async function handleFiles(files) {

        for (const file of files) {

            if (!validateFile(file)) {
                continue;
            }

            try {

                const croppedFile =
                    await cropImageToSquare(file);

                selectedFiles.push(
                    croppedFile
                );
            }
            catch {

                toast(
                    "Không thể xử lý ảnh",
                    "error"
                );
            }
        }

        renderPreview();
    }

    function renderPreview() {

        const container =
            $('#previewContainer');

        container.empty();

        selectedFiles.forEach(
            (file, index) => {

                const reader =
                    new FileReader();

                reader.onload =
                    function (e) {

                        container.append(`
                            <div class="col-4 position-relative">

                                <img
                                    src="${e.target.result}"
                                    class="w-100 rounded border"
                                    style="
                                        aspect-ratio:1/1;
                                        object-fit:cover;
                                    ">

                                <button
                                    class="btn btn-sm btn-danger position-absolute top-0 end-0 btn-remove-img"
                                    data-index="${index}">

                                    <i class="fas fa-times"></i>

                                </button>

                            </div>
                        `);
                    };

                reader.readAsDataURL(
                    file
                );
            });
    }

    $(document).on(
        'click',
        '.btn-remove-img',
        function () {

            const index =
                $(this).data('index');

            selectedFiles.splice(
                index,
                1
            );

            renderPreview();
        });

    // ================= DRAG DROP =================

    $('#dropZone').on(
        'dragover',
        function (e) {

            e.preventDefault();

            $(this)
                .addClass(
                    'border-primary bg-light'
                );
        });

    $('#dropZone').on(
        'dragleave drop',
        function (e) {

            e.preventDefault();

            $(this)
                .removeClass(
                    'border-primary bg-light'
                );
        });

    $('#dropZone').on(
        'drop',
        async function (e) {

            e.preventDefault();

            e.stopPropagation();

            $(this)
                .removeClass(
                    'border-primary'
                );

            const files =
                e.originalEvent
                    .dataTransfer
                    .files;

            await handleFiles(
                files
            );
        });

    // ================= FORM SUBMIT =================

    $('form').on(
        'submit',
        function (e) {

            e.preventDefault();

            if (isSubmitting) {
                return;
            }

            isSubmitting = true;

            const submitBtn =
                $(this).find(
                    'button[type="submit"]'
                );

            submitBtn
                .prop('disabled', true)
                .html(`
                <i class="fas fa-spinner fa-spin me-1"></i>
                Đang tạo...
            `);

            setTimeout(() => {

                if (isSubmitting) {

                    isSubmitting = false;

                    submitBtn
                        .prop('disabled', false)
                        .html(`
                        <i class="fas fa-save me-1"></i>
                        Tạo Nước Uống
                    `);
                }

            }, 5000);

            const imageInput =
                document.getElementById(
                    'imageFilesInput'
                );

            if (imageInput) {

                const dataTransfer =
                    new DataTransfer();

                selectedFiles.forEach(
                    file => {

                        dataTransfer
                            .items
                            .add(file);
                    });

                imageInput.files =
                    dataTransfer.files;
            }

            const formData =
                new FormData(this);

            $.ajax({

                url:
                    this.action,

                type:
                    this.method,

                data:
                    formData,

                processData:
                    false,

                contentType:
                    false,

                success:
                    function (res) {

                        if (res.success) {

                            toast(
                                res.message,
                                "success"
                            );

                            setTimeout(
                                () => {

                                    window.location.href =
                                        res.redirectUrl;

                                },
                                1000
                            );
                        }
                        else {

                            isSubmitting = false;

                            submitBtn
                                .prop('disabled', false)
                                .html(`
                                <i class="fas fa-save me-1"></i>
                                Tạo Nước Uống
                            `);

                            toast(
                                res.message,
                                "error"
                            );
                        }
                    },

                error:
                    function () {

                        isSubmitting = false;

                        submitBtn
                            .prop('disabled', false)
                            .html(`
                            <i class="fas fa-save me-1"></i>
                            Tạo Nước Uống
                        `);

                        toast(
                            "Có lỗi xảy ra",
                            "error"
                        );
                    }
            });
        });

    // ================= STATUS =================

    function updateStatusLabel() {

        if (
            $('#activeStatusSwitch')
                .is(':checked')
        ) {

            $('#activeStatusLabel')
                .text(
                    'Trạng thái: Đang bán'
                )
                .removeClass(
                    'text-danger'
                )
                .addClass(
                    'text-success'
                );
        }
        else {

            $('#activeStatusLabel')
                .text(
                    'Trạng thái: Ngừng bán'
                )
                .removeClass(
                    'text-success'
                )
                .addClass(
                    'text-danger'
                );
        }
    }

    if (
        $('#activeStatusSwitch')
            .length
    ) {

        updateStatusLabel();

        $('#activeStatusSwitch')
            .change(
                function () {

                    updateStatusLabel();
                });
    }

    // ================= RELOAD TABLE =================

    function reloadDrinkTable() {

        if (
            !$('#datatablesSimple')
                .length
        ) {
            return;
        }

        $.get(
            '/Admin/AdminDrink/IndexPartial',
            function (html) {

                const table =
                    $('#datatablesSimple')
                        .DataTable();

                table.clear();

                const newRows =
                    $(html)
                        .find(
                            'tbody tr'
                        );

                newRows.each(
                    function () {

                        table.row.add(
                            $(this)
                        );
                    });

                table.draw();
            });
    }

});