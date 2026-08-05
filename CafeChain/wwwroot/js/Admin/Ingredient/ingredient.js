(() => {
    "use strict";

    let isEdit = false;
    let unitCache = [];
    const $ = window.jQuery;
    const token = () => document.querySelector('#ingredientAntiForgeryForm input[name="__RequestVerificationToken"]')?.value || "";

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

    function preloadUnits() {
        return fetch("/Admin/AdminIngredient/GetUnits")
            .then(response => response.json())
            .then(data => { unitCache = data || []; renderUnits(); });
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
        const response = await fetch(`/Admin/AdminIngredient/GetById?id=${id}`);
        const result = await response.json();
        if (!result.success) return toast(result.message || "Không tìm thấy nguyên liệu", "error");
        const item = result.data;
        $("#ingredientId").val(item.ingredientId);
        $("#code").val(item.code);
        $("#name").val(item.name);
        renderUnits(item.baseUnitId);
        $("#modalTitle").text("Cập nhật nguyên liệu");
        openIngredientModal();
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
        AdminMutationGuard.run("ingredient-save", button, async () => {
            const action = isEdit ? "Update" : "Create";
            const response = await fetch(`/Admin/AdminIngredient/${action}`, {
                method: "POST",
                headers: { "Content-Type": "application/json", "RequestVerificationToken": token() },
                body: JSON.stringify(data)
            });
            const result = await response.json();
            toast(result.message || "Có lỗi xảy ra", result.success ? "success" : "error");
            if (result.success) window.location.reload();
        });
    }

    function toggleStatus(id, button) {
        AdminMutationGuard.run(`ingredient-toggle-${id}`, button, async () => {
            const response = await fetch(`/Admin/AdminIngredient/ToggleStatus?id=${id}`, {
                method: "POST",
                headers: { "RequestVerificationToken": token() }
            });
            const result = await response.json();
            toast(result.message || "Có lỗi xảy ra", result.success ? "success" : "error");
            if (result.success) window.location.reload();
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
