(() => {
    "use strict";

    let isEdit = false;
    let unitCache = [];
    const $ = window.jQuery;
    const catalog = window.CafeChainUiCatalog?.read("ingredient-ui-catalog") || {};
    const t = (key, values) => window.CafeChainUiCatalog?.text(catalog, key, values) || key;
    const token = () => document.querySelector('#ingredientAntiForgeryForm input[name="__RequestVerificationToken"]')?.value || "";

    function resolveMessage(payload, response, fallback) {
        return window.AdminFeedback?.resolveMessage?.(payload, {
            status: response?.status,
            fallback
        }) || (typeof payload?.message === "string" ? payload.message : fallback);
    }

    async function requestJson(url, options = {}, config = {}) {
        const requireSuccess = config.requireSuccess !== false;
        const fallback = config.fallback || t("Ingredient.Js.RequestFailed");
        let response;
        try {
            response = await fetch(url, options);
        } catch {
            throw new Error(window.AdminFeedback?.networkMessage?.()
                || t("Ingredient.Js.NetworkError"));
        }

        let result;
        try {
            result = await response.json();
        } catch {
            throw new Error(resolveMessage(null, response,
                response.ok ? t("Ingredient.Js.InvalidResponse") : fallback));
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
                fallback: t("Ingredient.Js.LoadUnitsFailed")
            });
            if (!Array.isArray(data)) throw new Error(t("Ingredient.Js.InvalidUnitList"));
            unitCache = data;
            renderUnits();
        } catch (error) {
            unitCache = [];
            renderUnits();
            toast(error.message || t("Ingredient.Js.LoadUnitsFailed"), "error");
        }
    }

    function renderUnits(selectedId) {
        const select = document.getElementById("baseUnitId");
        select.innerHTML = `<option value="">${t("Ingredient.Js.SelectUnit")}</option>`;
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
        $("#modalTitle").text(t("Ingredient.Js.ModalCreateTitle"));
        openIngredientModal();
    }

    async function openEditModal(id) {
        isEdit = true;
        clearForm();
        try {
            const result = await requestJson(`/Admin/AdminIngredient/GetById?id=${id}`, {}, {
                fallback: t("Ingredient.Js.LoadDetailFailed")
            });
            const item = result.data;
            if (!item) throw new Error(t("Ingredient.Js.MissingDetail"));
            $("#ingredientId").val(item.ingredientId);
            $("#code").val(item.code);
            $("#name").val(item.name);
            renderUnits(item.baseUnitId);
            $("#modalTitle").text(t("Ingredient.Js.ModalEditTitle"));
            openIngredientModal();
        } catch (error) {
            toast(error.message || t("Ingredient.Js.LoadDetailFailed"), "error");
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
            return toast(t("Ingredient.Js.ValidationError"), "warning");

        const button = document.getElementById("btnSave");
        void AdminMutationGuard.run("ingredient-save", button, async () => {
            try {
                const action = isEdit ? "Update" : "Create";
                const result = await requestJson(`/Admin/AdminIngredient/${action}`, {
                    method: "POST",
                    headers: { "Content-Type": "application/json", "RequestVerificationToken": token() },
                    body: JSON.stringify(data)
                }, {
                    fallback: isEdit ? t("Ingredient.Js.UpdateFailed") : t("Ingredient.Js.CreateFailed")
                });
                toast(result.message || (isEdit ? t("Ingredient.Js.UpdateSuccess") : t("Ingredient.Js.CreateSuccess")), "success");
                window.location.reload();
            } catch (error) {
                toast(error.message || t("Ingredient.Js.SaveFailed"), "error");
            }
        });
    }

    function toggleStatus(id, button) {
        const isActive = button.classList.contains("ingredient-btn-danger");
        const actionText = isActive ? t("Ingredient.Js.Action.Disable") : t("Ingredient.Js.Action.Enable");

        if (typeof Swal !== 'undefined') {
            Swal.fire({
                title: t("Ingredient.Js.ConfirmTitle", { action: actionText }),
                text: t("Ingredient.Js.ConfirmText", { action: actionText }),
                icon: 'warning',
                showCancelButton: true,
                confirmButtonText: t("Common.ConfirmYes"),
                cancelButtonText: t("Common.CancelLong"),
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
            if (confirm(t("Ingredient.Js.ConfirmFallback", { action: actionText }))) {
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
                    fallback: t("Ingredient.Js.ToggleFailed")
                });
                toast(result.message || t("Ingredient.Js.ToastStatusUpdated"), "success");
                window.location.reload();
            } catch (error) {
                toast(error.message || t("Ingredient.Js.ToggleFailed"), "error");
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
