(function (window, document) {
    "use strict";

    function initialize() {
        const root = document.getElementById("staffShiftApp");
        if (!root || root.dataset.initialized === "true") return;
        root.dataset.initialized = "true";

        const token = root.querySelector('input[name="__RequestVerificationToken"]')?.value || "";
        const canCreate = root.dataset.canCreate === "true";
        const scheduleElement = document.getElementById("scheduleModal");
        const templateElement = document.getElementById("templateModal");
        const scheduleModal = scheduleElement ? bootstrap.Modal.getOrCreateInstance(scheduleElement) : null;
        const templateModal = templateElement ? bootstrap.Modal.getOrCreateInstance(templateElement) : null;
        const scheduleForm = document.getElementById("scheduleForm");
        const templateForm = document.getElementById("templateForm");
        const staffSearch = document.getElementById("staffSearch");
        const statusFilter = document.getElementById("scheduleStatusFilter");
        const resetFilters = document.getElementById("resetRosterFilters");
        const visibleStaffCount = document.getElementById("visibleStaffCount");
        const filterEmpty = document.getElementById("rosterFilterEmpty");
        const rosterContainer = root.querySelector(".roster-container");
        const staffRows = Array.from(root.querySelectorAll("[data-staff-row]"));
        let draggedTemplate = null;

        const field = id => document.getElementById(id);
        const mutationGuard = window.AdminMutationGuard;
        const catalog = window.CafeChainUiCatalog?.read("staff-shift-ui-catalog") || {};
        const t = (key, values) => window.CafeChainUiCatalog?.text(catalog, key, values) || key;

        function normalizeSearch(value) {
            return (value || "")
                .normalize("NFD")
                .replace(/[\u0300-\u036f]/g, "")
                .replace(/đ/g, "d")
                .replace(/Đ/g, "D")
                .toLocaleLowerCase("vi")
                .trim();
        }

        function applyRosterFilters() {
            const query = normalizeSearch(staffSearch?.value);
            const status = statusFilter?.value || "all";
            let visible = 0;

            staffRows.forEach(row => {
                const matchesSearch = !query || normalizeSearch(row.dataset.staffSearch).includes(query);
                const scheduledCount = Number(row.dataset.scheduledCount || 0);
                const cancelledCount = Number(row.dataset.cancelledCount || 0);
                const matchesStatus = status === "all"
                    || (status === "scheduled" && scheduledCount > 0)
                    || (status === "unscheduled" && scheduledCount === 0)
                    || (status === "cancelled" && cancelledCount > 0);
                const matches = matchesSearch && matchesStatus;
                row.hidden = !matches;
                if (matches) visible += 1;
            });

            if (visibleStaffCount) {
                visibleStaffCount.textContent = t("Shift.Js.Showing", { visible, total: staffRows.length });
            }

            const hasActiveFilters = Boolean(query) || status !== "all";
            if (resetFilters) resetFilters.hidden = !hasActiveFilters;

            const hasNoMatches = staffRows.length > 0 && visible === 0;
            if (filterEmpty) filterEmpty.hidden = !hasNoMatches;
            rosterContainer?.classList.toggle("is-filter-empty", hasNoMatches);
        }

        staffSearch?.addEventListener("input", applyRosterFilters);
        staffSearch?.addEventListener("keydown", event => {
            if (event.key !== "Escape" || !staffSearch.value) return;
            staffSearch.value = "";
            applyRosterFilters();
        });
        statusFilter?.addEventListener("change", applyRosterFilters);
        resetFilters?.addEventListener("click", () => {
            if (staffSearch) staffSearch.value = "";
            if (statusFilter) statusFilter.value = "all";
            applyRosterFilters();
            staffSearch?.focus();
        });
        applyRosterFilters();

        function notify(message, type) {
            if (window.Swal) {
                return Swal.fire({
                    title: type === "success" ? t("Shift.Js.Success") : type === "warning" ? t("Shift.Js.DataChanged") : t("Shift.Js.Failed"),
                    text: message,
                    icon: type || "error",
                    confirmButtonColor: "#e8643c"
                });
            }
            window.alert(message);
            return Promise.resolve();
        }

        async function post(url, formData) {
            formData.set("targetStoreId", root.dataset.storeId);
            formData.set("__RequestVerificationToken", token);
            const response = await fetch(url, {
                method: "POST",
                body: formData,
                credentials: "same-origin",
                headers: { "X-Requested-With": "XMLHttpRequest", "Accept": "application/json" }
            });

            let result;
            try {
                result = await response.json();
            } catch {
                result = { message: t("Shift.Js.InvalidResponse") };
            }

            if (!response.ok) {
                const error = new Error(result.message || t("Shift.Js.ActionFailed"));
                error.status = response.status;
                error.errorCode = result.errorCode;
                throw error;
            }
            return result;
        }

        async function handleFailure(error) {
            if (error.status === 409 || error.errorCode === "CONCURRENCY_CONFLICT") {
                await notify(`${error.message} ${t("Shift.Js.ReloadNote")}`, "warning");
                window.location.reload();
                return;
            }
            await notify(error.message || t("Shift.Js.ActionFailed"), "error");
        }

        async function completeMutation(result) {
            await notify(result.message || t("Shift.Js.Done"), "success");
            window.location.reload();
        }

        function formatWorkDate(value) {
            const parts = (value || "").split("-");
            return parts.length === 3 ? `${parts[2]}/${parts[1]}/${parts[0]}` : value;
        }

        function setCustomTimeEnabled(enabled) {
            field("useCustomTime").checked = enabled;
            document.querySelectorAll(".custom-time").forEach(input => {
                input.disabled = !enabled;
                input.required = enabled;
                if (!enabled) input.value = "";
            });
        }

        function openSchedule(options) {
            if (!scheduleForm || !scheduleModal) return;
            scheduleForm.reset();
            field("staffShiftId").value = options.staffShiftId || "";
            field("scheduleStaffId").value = options.staffId || "";
            field("scheduleDate").value = options.workDate || "";
            field("scheduleVersion").value = options.rowVersion || "";
            field("scheduleShift").value = options.shiftId || "";
            field("scheduleContext").textContent = `${options.staffName || t("Shift.Js.StaffFallback")} · ${formatWorkDate(options.workDate)}`;
            field("scheduleModalTitle").textContent = options.staffShiftId ? t("Shift.Js.EditScheduleTitle") : t("Shift.Js.AssignScheduleTitle");
            const usesCustomTime = Boolean(options.customStart || options.customEnd);
            setCustomTimeEnabled(usesCustomTime);
            field("customStart").value = options.customStart || "";
            field("customEnd").value = options.customEnd || "";
            scheduleModal.show();
            scheduleElement.addEventListener("shown.bs.modal", () => field("scheduleShift")?.focus(), { once: true });
        }

        function clearDragState() {
            document.querySelectorAll(".drag-over").forEach(zone => zone.classList.remove("drag-over"));
            document.querySelectorAll(".is-dragging").forEach(item => item.classList.remove("is-dragging"));
            document.body.classList.remove("is-dragging-template");
            draggedTemplate = null;
        }

        root.querySelector("#storeSelector")?.addEventListener("change", event => {
            const url = new URL(root.dataset.indexUrl, window.location.origin);
            url.searchParams.set("targetStoreId", event.target.value);
            url.searchParams.set("startDate", root.dataset.weekStart);
            window.location.assign(url.toString());
        });

        field("useCustomTime")?.addEventListener("change", event => setCustomTimeEnabled(event.target.checked));

        root.addEventListener("click", event => {
            const assignButton = event.target.closest(".assign-schedule");
            if (assignButton) {
                openSchedule({ staffId: assignButton.dataset.staff, staffName: assignButton.dataset.name, workDate: assignButton.dataset.date });
                return;
            }

            const editSchedule = event.target.closest(".edit-schedule");
            if (editSchedule) {
                openSchedule({
                    staffShiftId: editSchedule.dataset.id,
                    staffId: editSchedule.dataset.staff,
                    staffName: editSchedule.dataset.name,
                    workDate: editSchedule.dataset.date,
                    shiftId: editSchedule.dataset.shift,
                    customStart: editSchedule.dataset.customStart,
                    customEnd: editSchedule.dataset.customEnd,
                    rowVersion: editSchedule.dataset.version
                });
                return;
            }

            const createTemplate = event.target.closest('[data-template-mode="create"]');
            if (createTemplate) {
                templateForm?.reset();
                field("templateId").value = "";
                field("templateVersion").value = "";
                field("templateModalTitle").textContent = t("Shift.Js.CreateTemplateTitle");
                templateModal?.show();
                return;
            }

            const editTemplate = event.target.closest(".edit-template");
            if (editTemplate) {
                templateForm?.reset();
                field("templateId").value = editTemplate.dataset.id;
                field("templateVersion").value = editTemplate.dataset.version;
                field("templateName").value = editTemplate.dataset.name;
                field("templateStart").value = editTemplate.dataset.start;
                field("templateEnd").value = editTemplate.dataset.end;
                field("templateNotes").value = editTemplate.dataset.notes || "";
                field("templateModalTitle").textContent = t("Shift.Js.EditTemplateTitle");
                templateModal?.show();
            }
        });

        if (canCreate) {
            root.querySelectorAll(".draggable-template").forEach(template => {
                template.addEventListener("dragstart", event => {
                    draggedTemplate = {
                        shiftId: template.dataset.templateId,
                        name: template.dataset.templateName
                    };
                    template.classList.add("is-dragging");
                    document.body.classList.add("is-dragging-template");
                    event.dataTransfer.effectAllowed = "copy";
                    event.dataTransfer.setData("application/x-cafechain-shift", JSON.stringify(draggedTemplate));
                    event.dataTransfer.setData("text/plain", draggedTemplate.shiftId);
                });
                template.addEventListener("dragend", clearDragState);
            });

            root.querySelectorAll(".schedule-drop-zone").forEach(zone => {
                zone.addEventListener("dragenter", event => {
                    if (!draggedTemplate) return;
                    event.preventDefault();
                    zone.classList.add("drag-over");
                });
                zone.addEventListener("dragover", event => {
                    if (!draggedTemplate) return;
                    event.preventDefault();
                    event.dataTransfer.dropEffect = "copy";
                    zone.classList.add("drag-over");
                });
                zone.addEventListener("dragleave", event => {
                    zone.classList.remove("drag-over");
                });
                zone.addEventListener("drop", event => {
                    event.preventDefault();
                    if (!draggedTemplate) return clearDragState();
                    const selected = draggedTemplate;
                    clearDragState();
                    openSchedule({
                        staffId: zone.dataset.staffId,
                        staffName: zone.dataset.staffName,
                        workDate: zone.dataset.workDate,
                        shiftId: selected.shiftId
                    });
                });
            });
        }

        scheduleForm?.addEventListener("submit", async event => {
            event.preventDefault();
            if (!scheduleForm.checkValidity()) return scheduleForm.reportValidity();
            const editing = Boolean(field("staffShiftId").value);
            const operation = async () => {
                try {
                    await completeMutation(await post(editing ? root.dataset.updateScheduleUrl : root.dataset.assignUrl, new FormData(scheduleForm)));
                } catch (error) {
                    await handleFailure(error);
                }
            };
            await mutationGuard.run(editing ? `update-staff-shift-${field("staffShiftId").value}` : "assign-staff-shift", event.submitter, operation);
        });

        root.addEventListener("click", async event => {
            const cancelButton = event.target.closest(".cancel-schedule");
            if (!cancelButton) return;
            if (cancelButton.dataset.confirming === "true") return;
            cancelButton.dataset.confirming = "true";
            let answer;
            try {
                if (window.Swal) {
                    answer = await Swal.fire({
                        title: t("Shift.Js.CancelScheduleTitle"),
                        input: "textarea",
                        inputLabel: t("Shift.Js.CancelReasonLabel"),
                        inputPlaceholder: t("Shift.Js.CancelReasonPlaceholder"),
                        showCancelButton: true,
                        confirmButtonText: t("Shift.Js.CancelConfirm"),
                        cancelButtonText: t("Shift.Js.Close"),
                        confirmButtonColor: "#dc2626",
                        inputValidator: value => !value?.trim() ? t("Shift.Js.CancelReasonRequired") : undefined
                    });
                } else {
                    const value = window.prompt(t("Shift.Js.CancelPrompt"));
                    answer = { isConfirmed: Boolean(value?.trim()), value };
                }
            } finally {
                delete cancelButton.dataset.confirming;
            }
            if (!answer.isConfirmed || !answer.value?.trim()) return;

            await mutationGuard.run(`cancel-staff-shift-${cancelButton.dataset.id}`, cancelButton, async () => {
                const data = new FormData();
                data.set("StaffShiftId", cancelButton.dataset.id);
                data.set("RowVersion", cancelButton.dataset.version);
                data.set("Reason", answer.value.trim());
                try {
                    await completeMutation(await post(root.dataset.cancelScheduleUrl, data));
                } catch (error) {
                    await handleFailure(error);
                }
            });
        });

        templateForm?.addEventListener("submit", async event => {
            event.preventDefault();
            if (!templateForm.checkValidity()) return templateForm.reportValidity();
            const editing = Boolean(field("templateId").value);
            await mutationGuard.run(editing ? `update-shift-template-${field("templateId").value}` : "create-shift-template", event.submitter, async () => {
                try {
                    await completeMutation(await post(editing ? root.dataset.updateTemplateUrl : root.dataset.createTemplateUrl, new FormData(templateForm)));
                } catch (error) {
                    await handleFailure(error);
                }
            });
        });

        root.addEventListener("click", async event => {
            const toggleButton = event.target.closest(".toggle-template");
            if (!toggleButton) return;

            const isActive = toggleButton.classList.contains("shift-action-toggle-active");
            const actionText = isActive ? t("Shift.Js.StopVerb") : t("Shift.Js.ActivateVerb");

            if (window.Swal) {
                const answer = await Swal.fire({
                    title: t("Shift.Js.ConfirmPrefix") + " " + actionText,
                    text: t("Shift.Js.ConfirmToggle", { action: actionText }),
                    icon: 'warning',
                    showCancelButton: true,
                    confirmButtonText: t("Shift.Js.Agree"),
                    cancelButtonText: t("Shift.Js.CancelShort"),
                    reverseButtons: true,
                    customClass: {
                        confirmButton: 'btn btn-swal-confirm me-2',
                        cancelButton: 'btn btn-swal-cancel'
                    },
                    buttonsStyling: false
                });
                if (!answer.isConfirmed) return;
            } else {
                if (!confirm(t("Shift.Js.ConfirmToggle", { action: actionText }))) return;
            }

            await mutationGuard.run(`toggle-shift-template-${toggleButton.dataset.id}`, toggleButton, async () => {
                const data = new FormData();
                data.set("ShiftId", toggleButton.dataset.id);
                data.set("RowVersion", toggleButton.dataset.version);
                try {
                    await completeMutation(await post(root.dataset.toggleTemplateUrl, data));
                } catch (error) {
                    await handleFailure(error);
                }
            });
        });

        window.addEventListener("blur", clearDragState);
        document.addEventListener("dragend", clearDragState);
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", initialize, { once: true });
    else initialize();
})(window, document);
