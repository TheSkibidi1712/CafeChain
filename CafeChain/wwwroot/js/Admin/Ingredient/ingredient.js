(() => {
    "use strict";

    let isEdit = false;
    let unitCache = [];
    const $ = window.jQuery;
    const token = () => document.querySelector('#ingredientAntiForgeryForm input[name="__RequestVerificationToken"]')?.value || "";

    function resolveMessage(payload, response, fallback) {
        return window.AdminFeedback?.resolveMessage?.(payload, {
            status: response?.status,
            fallback
        }) || (typeof payload?.message === "string" ? payload.message : fallback);
    }

    async function requestJson(url, options = {}, config = {}) {
        const requireSuccess = config.requireSuccess !== false;
        const fallback = config.fallback || "Không thể thực hiện thao tác. Vui lòng thử lại.";
        let response;
        try {
            response = await fetch(url, options);
        } catch {
            throw new Error(window.AdminFeedback?.networkMessage?.()
                || "Không thể kết nối máy chủ. Vui lòng kiểm tra mạng và thử lại.");
        }

        let result;
        try {
            result = await response.json();
        } catch {
            throw new Error(resolveMessage(null, response,
                response.ok ? "Máy chủ trả về dữ liệu không hợp lệ." : fallback));
        }

        if (!response.ok || (requireSuccess && result?.success !== true)) {
            throw new Error(resolveMessage(result, response, fallback));
        }
        return result;
    }

    document.addEventListener("DOMContentLoaded", () => {
        preloadUnits();
        $("#btnCreate").on("click", openCreateModal);
        $("#btnFilter").on("click", applyFilter);
        $("#btnReset").on("click", () => { window.location.href = "/Admin/AdminIngredient"; });
        $("#btnSave").on("click", saveIngredient);
        $(document).on("click", ".edit-btn", function () { openEditModal($(this).data("id")); });
        $(document).on("click", ".toggle-btn", function () { toggleStatus($(this).data("id"), this); });
        document.getElementById("ingredientModal")?.addEventListener("hidden.bs.modal", clearForm);
    });

    async function preloadUnits() {
        try {
            const data = await requestJson("/Admin/AdminIngredient/GetUnits", {}, {
                requireSuccess: false,
                fallback: "Không thể tải danh sách đơn vị."
            });
            if (!Array.isArray(data)) throw new Error("Máy chủ trả về danh sách đơn vị không hợp lệ.");
            unitCache = data;
            renderUnits();
        } catch (error) {
            unitCache = [];
            renderUnits();
            toast(error.message || "Không thể tải danh sách đơn vị.", "error");
        }
    }

    function renderUnits(selectedId) {
        const select = document.getElementById("baseUnitId");
        select.innerHTML = '<option value="">-- Chọn đơn vị --</option>';
        unitCache.forEach(unit => select.add(new Option(unit.text, unit.id)));
        if (selectedId) select.value = String(selectedId);
    }

    function openIngredientModal() {
        const element = document.getElementById("ingredientModal");
        if (element) bootstrap.Modal.getOrCreateInstance(element).show();
    }
    function clearForm() {
        $("#ingredientId").val("");
        $("#code").val("");
        $("#name").val("");
        renderUnits();
    }

    function openCreateModal() {
        isEdit = false;
        clearForm();
        $("#modalTitle").text("Thêm nguyên liệu");
        openIngredientModal();
    }

    async function openEditModal(id) {
        isEdit = true;
        clearForm();
        try {
            const result = await requestJson(`/Admin/AdminIngredient/GetById?id=${id}`, {}, {
                fallback: "Không thể tải thông tin nguyên liệu."
            });
            const item = result.data;
            if (!item) throw new Error("Máy chủ không trả về thông tin nguyên liệu.");
            $("#ingredientId").val(item.ingredientId);
            $("#code").val(item.code);
            $("#name").val(item.name);
            renderUnits(item.baseUnitId);
            $("#modalTitle").text("Cập nhật nguyên liệu");
            openIngredientModal();
        } catch (error) {
            toast(error.message || "Không thể tải thông tin nguyên liệu.", "error");
        }
    }

    function payload() {
        return {
            ingredientId: Number($("#ingredientId").val() || 0),
            code: $("#code").val(),
            name: $("#name").val(),
            baseUnitId: Number($("#baseUnitId").val())
        };
    }

    function saveIngredient() {
        const data = payload();
        if (!data.code?.trim() || !data.name?.trim() || !data.baseUnitId)
            return toast("Vui lòng nhập mã, tên và đơn vị tồn kho cơ sở.", "warning");

        const button = document.getElementById("btnSave");
        void AdminMutationGuard.run("ingredient-save", button, async () => {
            try {
                const action = isEdit ? "Update" : "Create";
                const result = await requestJson(`/Admin/AdminIngredient/${action}`, {
                    method: "POST",
                    headers: { "Content-Type": "application/json", "RequestVerificationToken": token() },
                    body: JSON.stringify(data)
                }, {
                    fallback: isEdit ? "Không thể cập nhật nguyên liệu." : "Không thể tạo nguyên liệu."
                });
                toast(result.message || (isEdit ? "Cập nhật thành công" : "Thêm thành công"), "success");
                window.location.reload();
            } catch (error) {
                toast(error.message || "Không thể lưu nguyên liệu.", "error");
            }
        });
    }

    function toggleStatus(id, button) {
        const isActive = button.classList.contains("ingredient-btn-danger");
        const actionText = isActive ? "ngưng hoạt động" : "kích hoạt";

        if (typeof Swal !== 'undefined') {
            Swal.fire({
                title: 'Xác nhận ' + actionText,
                text: 'Bạn có chắc chắn muốn ' + actionText + ' nguyên liệu này không?',
                icon: 'warning',
                showCancelButton: true,
                confirmButtonText: 'Đồng ý',
                cancelButtonText: 'Hủy bỏ',
                reverseButtons: true,
                customClass: {
                    confirmButton: 'btn btn-swal-confirm me-2',
                    cancelButton: 'btn btn-swal-cancel'
                },
                buttonsStyling: false
            }).then((result) => {
                if (result.isConfirmed) {
                    executeToggle(id, button);
                }
            });
        } else {
            if (confirm('Bạn có chắc chắn muốn ' + actionText + ' nguyên liệu này không?')) {
                executeToggle(id, button);
            }
        }
    }

    function executeToggle(id, button) {
        void AdminMutationGuard.run(`ingredient-toggle-${id}`, button, async () => {
            try {
                const result = await requestJson(`/Admin/AdminIngredient/ToggleStatus?id=${id}`, {
                    method: "POST",
                    headers: { "RequestVerificationToken": token() }
                }, {
                    fallback: "Không thể cập nhật trạng thái nguyên liệu."
                });
                toast(result.message || "Đã cập nhật trạng thái", "success");
                window.location.reload();
            } catch (error) {
                toast(error.message || "Không thể cập nhật trạng thái nguyên liệu.", "error");
            }
        });
    }

    function applyFilter() {
        const params = new URLSearchParams();
        const search = $("#searchBox").val()?.trim();
        const status = $("#statusFilter").val();
        if (search) params.set("search", search);
        if (status !== "") params.set("status", status);
        params.set("page", "1");
        window.location.href = `/Admin/AdminIngredient?${params}`;
    }
})();
