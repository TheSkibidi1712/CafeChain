(function () {
    "use strict";

    const root = document.querySelector(".perm-page");
    if (!root) return;

    const baseUrl = (root.dataset.baseUrl || "/Admin/AdminPermission").replace(/\/$/, "");
    const loginUrl = root.dataset.loginUrl || "/Account/Login";
    const accessDeniedUrl = root.dataset.accessDeniedUrl || "/Account/AccessDenied";

    const scopeTypeLabels = Object.freeze({
        COUNTRY: "Quốc gia",
        PROVINCE: "Tỉnh/Thành phố",
        DISTRICT: "Quận/Huyện",
        WARD: "Phường/Xã",
        STORE: "Cửa hàng"
    });

    const state = {
        activeTab: "roles",
        roles: { pageIndex: 1, pageSize: 10, search: "", totalPages: 1 },
        staff: {
            assign: { pageIndex: 1, pageSize: 10, search: "", totalPages: 1 },
            override: { pageIndex: 1, pageSize: 8, search: "", totalPages: 1 },
            scope: { pageIndex: 1, pageSize: 8, search: "", totalPages: 1 }
        },
        selectedRoleId: null,
        selectedStaffRoleId: null,
        selectedOverrideStaffId: null,
        selectedScopeStaffId: null,
        roleMatrix: null,
        staffRoles: null,
        overrideMatrix: null,
        scopeData: null,
        selectedScopes: [],
        scopeReferenceCache: new Map(),
        mutationRequestKeys: new Map(),
        collapsedGroups: { role: new Set(), override: new Set() }
    };

    const el = {
        refreshBtn: document.getElementById("permRefreshBtn"),
        roleSearch: document.getElementById("permRoleSearch"),
        roleTable: document.getElementById("permRoleTable"),
        roleCount: document.getElementById("permRoleCount"),
        rolePagination: document.getElementById("permRolePagination"),
        assignSearch: document.getElementById("permAssignStaffSearch"),
        assignTable: document.getElementById("permAssignStaffTable"),
        assignCount: document.getElementById("permAssignStaffCount"),
        assignPagination: document.getElementById("permAssignStaffPagination"),
        overrideStaffSearch: document.getElementById("permOverrideStaffSearch"),
        overrideStaffList: document.getElementById("permOverrideStaffList"),
        overrideStaffPagination: document.getElementById("permOverrideStaffPagination"),
        overrideSelected: document.getElementById("permOverrideSelected"),
        overrideSearch: document.getElementById("permOverrideSearch"),
        overrideEmpty: document.getElementById("permOverrideEmpty"),
        overrideGrid: document.getElementById("permOverrideGrid"),
        saveOverrideBtn: document.getElementById("permSaveOverrideBtn"),
        scopeStaffSearch: document.getElementById("permScopeStaffSearch"),
        scopeStaffList: document.getElementById("permScopeStaffList"),
        scopeStaffPagination: document.getElementById("permScopeStaffPagination"),
        scopeSelected: document.getElementById("permScopeSelected"),
        saveScopeBtn: document.getElementById("permSaveScopeBtn"),
        scopeTypeSelect: document.getElementById("permScopeTypeSelect"),
        scopeParentWrap: document.getElementById("permScopeParentWrap"),
        scopeParentSelect: document.getElementById("permScopeParentSelect"),
        scopeRefSelect: document.getElementById("permScopeRefSelect"),
        addScopeBtn: document.getElementById("permAddScopeBtn"),
        scopeList: document.getElementById("permScopeList"),
        roleModal: document.getElementById("permRoleModal"),
        roleModalMeta: document.getElementById("permRoleModalMeta"),
        rolePermissionSearch: document.getElementById("permRolePermissionSearch"),
        rolePermissionGroups: document.getElementById("permRolePermissionGroups"),
        selectedPermissionCount: document.getElementById("permSelectedPermissionCount"),
        selectAllPermissions: document.getElementById("permSelectAllPermissions"),
        clearPermissions: document.getElementById("permClearPermissions"),
        expandPermissions: document.getElementById("permExpandPermissions"),
        saveRolePermissions: document.getElementById("permSaveRolePermissions"),
        staffRoleModal: document.getElementById("permStaffRoleModal"),
        staffRoleMeta: document.getElementById("permStaffRoleMeta"),
        staffRoleOptions: document.getElementById("permStaffRoleOptions"),
        saveStaffRoles: document.getElementById("permSaveStaffRoles")
    };

    const roleModal = bootstrap.Modal.getOrCreateInstance(el.roleModal);
    const staffRoleModal = bootstrap.Modal.getOrCreateInstance(el.staffRoleModal);

    function endpoint(path) {
        return `${baseUrl}/${path}`;
    }

    function mutationRequest(action, targetId, payload) {
        const signature = `${action}:${targetId}:${JSON.stringify(payload)}`;
        let requestKey = state.mutationRequestKeys.get(signature);
        if (!requestKey) {
            requestKey = window.crypto?.randomUUID?.()
                || `${Date.now()}-${Math.random().toString(16).slice(2)}-${Math.random().toString(16).slice(2)}`;
            state.mutationRequestKeys.set(signature, requestKey);
        }

        return {
            body: { requestKey, ...payload },
            complete: () => state.mutationRequestKeys.delete(signature)
        };
    }

    function get(obj, key, fallback) {
        if (!obj) return fallback;
        const pascal = key.charAt(0).toUpperCase() + key.slice(1);
        return obj[key] !== undefined ? obj[key] : (obj[pascal] !== undefined ? obj[pascal] : fallback);
    }

    function normalizeSearch(value) {
        return String(value || "").normalize("NFD")
            .replace(/[\u0300-\u036f]/g, "")
            .replace(/đ/g, "d").replace(/Đ/g, "D")
            .toLowerCase().replace(/\s+/g, " ").trim();
    }

    function getScopeTypeLabel(scopeType) {
        const code = String(
            get(scopeType, "code", get(scopeType, "scopeTypeCode", "")))
            .trim()
            .toUpperCase();

        if (scopeTypeLabels[code]) return scopeTypeLabels[code];

        const scopeTypeId = Number(get(scopeType, "scopeTypeId", 0));
        const configuredType = get(state.scopeData, "scopeTypes", [])
            .find(type => Number(get(type, "scopeTypeId", 0)) === scopeTypeId);
        const configuredCode = String(get(configuredType, "code", "")).trim().toUpperCase();

        return scopeTypeLabels[configuredCode]
            || get(scopeType, "name", get(scopeType, "scopeTypeName", "Phạm vi"));
    }

    function escapeHtml(value) {
        return String(value ?? "")
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    function createRequestError(status, message, payload) {
        const error = new Error(message || "Không thể xử lý yêu cầu.");
        error.status = status;
        error.payload = payload;
        return error;
    }

    function isRedirectUrl(responseUrl, targetUrl) {
        if (!responseUrl || !targetUrl) return false;

        const responsePath = new URL(responseUrl, window.location.origin).pathname.toLowerCase();
        const targetPath = new URL(targetUrl, window.location.origin).pathname.toLowerCase();

        return responsePath === targetPath;
    }

    function buildLoginReturnUrl() {
        const url = new URL(loginUrl, window.location.origin);
        url.searchParams.set(
            "returnUrl",
            `${window.location.pathname}${window.location.search}${window.location.hash}`);

        return `${url.pathname}${url.search}${url.hash}`;
    }

    function setButtonBusy(button, isBusy, loadingText) {
        if (!button) return;

        if (!button.dataset.originalHtml) {
            button.dataset.originalHtml = button.innerHTML;
        }

        button.disabled = isBusy;
        button.classList.toggle("is-loading", isBusy);
        button.setAttribute("aria-busy", isBusy ? "true" : "false");

        button.innerHTML = isBusy
            ? `<i class="fas fa-spinner fa-spin"></i>${escapeHtml(loadingText || "Đang lưu...")}`
            : button.dataset.originalHtml;
    }

    function debounce(fn, delay) {
        let timer;
        return function (...args) {
            clearTimeout(timer);
            timer = setTimeout(() => fn.apply(this, args), delay);
        };
    }

    async function fetchJson(url, options) {
        const antiForgeryToken = document.querySelector(
            '#permissionAntiForgeryForm input[name="__RequestVerificationToken"]')?.value;
        const headers = {
            "Accept": "application/json",
            "X-Requested-With": "XMLHttpRequest",
            ...(options && options.body ? { "Content-Type": "application/json" } : {}),
            ...(options && options.method === "POST" && antiForgeryToken
                ? { "RequestVerificationToken": antiForgeryToken } : {}),
            ...(options && options.headers ? options.headers : {})
        };

        const response = await fetch(url, {
            ...options,
            headers
        });

        if (response.redirected && isRedirectUrl(response.url, loginUrl)) {
            throw createRequestError(
                401,
                "Bạn cần đăng nhập để truy cập chức năng này.");
        }

        if (response.redirected && isRedirectUrl(response.url, accessDeniedUrl)) {
            throw createRequestError(
                403,
                "Bạn không có quyền truy cập chức năng này.");
        }

        const payload = await response.json().catch(() => null);
        if (!response.ok || (payload && payload.success === false)) {
            throw createRequestError(
                response.status,
                (payload && payload.message) || "Không thể xử lý yêu cầu.",
                payload);
        }

        return payload ? get(payload, "data", payload) : null;
    }

    function notifySuccess(message) {
        if (window.Swal) {
            Swal.fire({ icon: "success", title: message || "Đã lưu", timer: 1300, showConfirmButton: false });
        }
    }

    function notifyError(error) {
        if (error.status === 401) {
            if (window.Swal) {
                Swal.fire({
                    icon: "warning",
                    title: "Bạn cần đăng nhập",
                    text: error.message || "Vui lòng đăng nhập để tiếp tục.",
                    confirmButtonText: "Đăng nhập",
                    showCancelButton: true,
                    cancelButtonText: "Đóng",
                    confirmButtonColor: "#f97316"
                }).then(result => {
                    if (result.isConfirmed) {
                        window.location.href = buildLoginReturnUrl();
                    }
                });
                return;
            }

            window.location.href = buildLoginReturnUrl();
            return;
        }

        if (error.status === 403) {
            if (window.Swal) {
                Swal.fire({
                    icon: "error",
                    title: "Bạn không có quyền truy cập",
                    text: error.message ||
                        "Vui lòng liên hệ cấp trên hoặc quản trị viên để được cấp quyền.",
                    confirmButtonText: "Đóng",
                    confirmButtonColor: "#f97316"
                });
                return;
            }

            alert(error.message || "Bạn không có quyền truy cập chức năng này.");
            return;
        }

        if (window.Swal) {
            Swal.fire({ icon: "error", title: "Không thành công", text: error.message || String(error) });
            return;
        }
        alert(error.message || String(error));
    }

    function renderPagination(container, pageIndex, totalPages, onChange) {
        container.innerHTML = "";
        if (!totalPages || totalPages <= 1) return;

        const pages = new Set([1, totalPages, pageIndex, pageIndex - 1, pageIndex + 1]);
        [...pages]
            .filter(page => page >= 1 && page <= totalPages)
            .sort((a, b) => a - b)
            .forEach(page => {
                const button = document.createElement("button");
                button.type = "button";
                button.className = `perm-page-button ${page === pageIndex ? "is-active" : ""}`;
                button.textContent = page;
                button.addEventListener("click", () => onChange(page));
                container.appendChild(button);
            });
    }

    function roleNamesHtml(roleNames) {
        const names = roleNames || [];
        if (!names.length) return '<span class="perm-subtext">Chưa có vai trò</span>';
        return `<div class="perm-role-tags">${names.map(name => `<span class="perm-role-tag">${escapeHtml(name)}</span>`).join("")}</div>`;
    }

    async function loadRoles() {
        el.roleTable.innerHTML = rowLoading(6);
        try {
            const url = `${endpoint("Roles")}?pageIndex=${state.roles.pageIndex}&pageSize=${state.roles.pageSize}&search=${encodeURIComponent(state.roles.search)}`;
            const data = await fetchJson(url);
            const items = get(data, "items", []);
            state.roles.totalPages = get(data, "totalPages", 1);
            el.roleCount.textContent = `${get(data, "totalCount", items.length)} vai trò`;
            el.roleTable.innerHTML = items.length
                ? items.map((item, index) => renderRoleRow(item, index)).join("")
                : emptyRow(6, "Không có vai trò phù hợp.");
            renderPagination(el.rolePagination, state.roles.pageIndex, state.roles.totalPages, page => {
                state.roles.pageIndex = page;
                loadRoles();
            });
        } catch (error) {
            el.roleTable.innerHTML = emptyRow(6, error.message);
        }
    }

    function renderRoleRow(role, index) {
        const ordinal = ((state.roles.pageIndex - 1) * state.roles.pageSize) + index + 1;

        return `
            <tr>
                <td class="text-center">${ordinal}</td>
                <td class="text-center"><div class="perm-name">${escapeHtml(get(role, "name", ""))}</div></td>
                <td class="text-center">${get(role, "userCount", 0)}</td>
                <td class="text-center">${get(role, "permissionCount", 0)}</td>
                <td class="text-center"><span class="perm-badge ${get(role, "active", false) ? "" : "is-muted"}">${get(role, "active", false) ? "Hoạt động" : "Ngưng hoạt động"}</span></td>
                <td class="text-center">
                    <button type="button" class="perm-outline-button" data-action="open-role" data-role-id="${get(role, "roleId", "")}">
                        <i class="fas fa-shield-halved"></i>
                        Phân quyền
                    </button>
                </td>
            </tr>
        `;
    }

    async function loadStaff(context) {
        const cfg = state.staff[context];
        const url = `${endpoint("Staff")}?pageIndex=${cfg.pageIndex}&pageSize=${cfg.pageSize}&search=${encodeURIComponent(cfg.search)}`;

        try {
            const data = await fetchJson(url);
            const items = get(data, "items", []);
            cfg.totalPages = get(data, "totalPages", 1);

            if (context === "assign") {
                el.assignCount.textContent = `${get(data, "totalCount", items.length)} nhân viên`;
                el.assignTable.innerHTML = items.length ? items.map(renderAssignStaffRow).join("") : emptyRow(6, "Không có nhân viên phù hợp.");
                renderPagination(el.assignPagination, cfg.pageIndex, cfg.totalPages, page => {
                    cfg.pageIndex = page;
                    loadStaff("assign");
                });
                return;
            }

            const list = context === "override" ? el.overrideStaffList : el.scopeStaffList;
            const pagination = context === "override" ? el.overrideStaffPagination : el.scopeStaffPagination;
            const selectedId = context === "override" ? state.selectedOverrideStaffId : state.selectedScopeStaffId;

            list.innerHTML = items.length ? items.map(item => renderStaffListItem(item, context, selectedId)).join("") : '<div class="perm-empty">Không có nhân viên phù hợp.</div>';
            renderPagination(pagination, cfg.pageIndex, cfg.totalPages, page => {
                cfg.pageIndex = page;
                loadStaff(context);
            });
        } catch (error) {
            notifyError(error);
        }
    }

    function renderAssignStaffRow(staff) {
        const active = get(staff, "active", false);
        return `
            <tr>
                <td class="text-center">
                    <div class="perm-name">${escapeHtml(get(staff, "fullName", ""))}</div>
                    <div class="perm-subtext">#${get(staff, "staffId", "")}</div>
                </td>
                <td class="text-center">${escapeHtml(get(staff, "email", ""))}</td>
                <td class="text-center">${escapeHtml(get(staff, "storeName", ""))}</td>
                <td class="text-center">${roleNamesHtml(get(staff, "roleNames", []))}</td>
                <td class="text-center"><span class="perm-badge ${active ? "" : "is-muted"}">${active ? "Hoạt động" : "Ngưng hoạt động"}</span></td>
                <td class="text-center">
                    <button type="button" class="perm-outline-button" data-action="open-staff-role" data-staff-id="${get(staff, "staffId", "")}">
                        <i class="fas fa-user-tag"></i>
                        Gán vai trò
                    </button>
                </td>
            </tr>
        `;
    }

    function renderStaffListItem(staff, context, selectedId) {
        const staffId = get(staff, "staffId", 0);
        return `
            <button type="button" class="perm-staff-item ${selectedId === staffId ? "is-active" : ""}" data-action="select-${context}-staff" data-staff-id="${staffId}">
                <span class="perm-staff-copy">
                    <span class="perm-name">${escapeHtml(get(staff, "fullName", ""))}</span>
                    <span class="perm-subtext">${escapeHtml(get(staff, "email", ""))}</span>
                </span>
                <i class="fas fa-chevron-right"></i>
            </button>
        `;
    }

    async function openRolePermission(roleId) {
        state.selectedRoleId = roleId;
        el.rolePermissionGroups.innerHTML = '<div class="perm-empty">Đang tải...</div>';
        roleModal.show();

        try {
            const matrix = await fetchJson(endpoint(`RolePermissions?roleId=${roleId}`));
            state.roleMatrix = matrix;
            el.roleModalMeta.textContent = `Vai trò: ${get(matrix, "roleName", "")} | Người dùng: ${get(matrix, "userCount", 0)} | Quyền: ${get(matrix, "permissionCount", 0)}`;
            el.rolePermissionSearch.value = "";
            renderRolePermissionGroups();
        } catch (error) {
            el.rolePermissionGroups.innerHTML = `<div class="perm-empty">${escapeHtml(error.message)}</div>`;
        }
    }

    function renderRolePermissionGroups() {
        const groups = get(state.roleMatrix, "groups", []);
        el.rolePermissionGroups.innerHTML = groups.map(group => `
            <div class="perm-permission-group" data-group-id="${get(group, "permissionGroupId", 0)}" data-group-text="${escapeHtml(normalizeSearch(`${get(group, "name", "")} ${get(group, "code", "")}`))}">
                <button type="button" class="perm-group-head" data-action="toggle-group">
                    <span><i class="fas fa-folder"></i> ${escapeHtml(get(group, "name", ""))}</span>
                    <i class="fas fa-chevron-up"></i>
                </button>
                <div class="perm-group-body">
                    ${get(group, "permissions", []).map(permission => `
                        <label class="perm-check-row perm-permission-row" data-permission-text="${escapeHtml(normalizeSearch(`${get(permission, "code", "")} ${get(permission, "name", "")} ${get(permission, "description", "")}`))}" title="${escapeHtml(get(permission, "readOnlyReason", ""))}">
                            <input type="checkbox" class="role-permission-check" value="${get(permission, "permissionId", "")}" ${get(permission, "isGranted", false) ? "checked" : ""} ${get(permission, "canChange", true) ? "" : "disabled"} />
                            <span class="perm-permission-copy">
                                <span class="perm-permission-code">${escapeHtml(get(permission, "code", ""))}</span>
                                <span class="perm-permission-name">${escapeHtml(get(permission, "name", ""))}</span>
                            </span>
                        </label>
                    `).join("")}
                </div>
            </div>
        `).join("");
        const hasMutablePermission = document.querySelector(".role-permission-check:not(:disabled)") !== null;
        el.saveRolePermissions.disabled = !hasMutablePermission;
        el.saveRolePermissions.title = hasMutablePermission
            ? ""
            : "Bạn không có permission nào có thể thay đổi trong vai trò này.";
        updateSelectedPermissionCount();
        filterRolePermissions();
    }

    function updateSelectedPermissionCount() {
        const count = document.querySelectorAll(".role-permission-check:checked").length;
        el.selectedPermissionCount.textContent = `Đã chọn: ${count} quyền`;
    }

    async function saveRolePermissions() {
        if (!state.selectedRoleId) return;
        const permissionIds = [...document.querySelectorAll(".role-permission-check:checked")]
            .map(input => Number(input.value))
            .filter(Boolean)
            .sort((a, b) => a - b);
        const button = el.saveRolePermissions;
        const request = mutationRequest("role-permissions", state.selectedRoleId, { permissionIds });

        setButtonBusy(button, true, "Đang lưu...");
        try {
            await AdminMutationGuard.run(`role-permissions-${state.selectedRoleId}`, button, () =>
                fetchJson(endpoint(`SaveRolePermissions?roleId=${state.selectedRoleId}`),
                {
                    method: "POST",
                    body: JSON.stringify(request.body)
                }));
            request.complete();
            notifySuccess("Đã lưu phân quyền vai trò");
            roleModal.hide();
            loadRoles();
        } catch (error) {
            notifyError(error);
        } finally {
            setButtonBusy(button, false);
        }
    }

    async function openStaffRole(staffId) {
        state.selectedStaffRoleId = staffId;
        el.staffRoleOptions.innerHTML = '<div class="perm-empty">Đang tải...</div>';
        staffRoleModal.show();

        try {
            const data = await fetchJson(endpoint(`StaffRoles?staffId=${staffId}`));
            state.staffRoles = data;
            el.staffRoleMeta.textContent = `${get(data, "fullName", "")} | ${get(data, "email", "")}`;
            el.staffRoleOptions.innerHTML = get(data, "roles", []).map(role => `
                <label class="perm-role-option" title="${escapeHtml(get(role, "readOnlyReason", ""))}">
                    <input type="checkbox" class="staff-role-check" value="${get(role, "roleId", "")}" ${get(role, "isAssigned", false) ? "checked" : ""} ${get(role, "canChange", false) ? "" : "disabled"} />
                    <span>${escapeHtml(get(role, "name", ""))}</span>
                </label>
            `).join("");
            el.saveStaffRoles.disabled = !get(data, "canChange", false)
                || document.querySelector(".staff-role-check:not(:disabled)") === null;
            el.saveStaffRoles.title = get(data, "readOnlyReason", "");
        } catch (error) {
            el.staffRoleOptions.innerHTML = `<div class="perm-empty">${escapeHtml(error.message)}</div>`;
        }
    }

    async function saveStaffRoles() {
        if (!state.selectedStaffRoleId) return;
        const roleIds = [...document.querySelectorAll(".staff-role-check:checked")]
            .map(input => Number(input.value))
            .filter(Boolean)
            .sort((a, b) => a - b);
        const button = el.saveStaffRoles;
        const request = mutationRequest("staff-roles", state.selectedStaffRoleId, { roleIds });

        setButtonBusy(button, true, "Đang lưu...");
        try {
            await AdminMutationGuard.run(`staff-roles-${state.selectedStaffRoleId}`, button, () =>
                fetchJson(endpoint(`SaveStaffRoles?staffId=${state.selectedStaffRoleId}`), {
                    method: "POST",
                    body: JSON.stringify(request.body)
                }));
            request.complete();
            notifySuccess("Đã lưu vai trò nhân viên");
            staffRoleModal.hide();
            loadStaff("assign");
        } catch (error) {
            notifyError(error);
        } finally {
            setButtonBusy(button, false);
        }
    }

    async function selectOverrideStaff(staffId) {
        state.selectedOverrideStaffId = staffId;
        el.saveOverrideBtn.disabled = true;
        el.overrideGrid.innerHTML = "";
        el.overrideEmpty.hidden = false;
        await loadStaff("override");

        try {
            const data = await fetchJson(endpoint(`StaffOverrides?staffId=${staffId}`));
            state.overrideMatrix = data;
            el.overrideSelected.innerHTML = selectedHeadHtml(data, "permSaveOverrideBtn", "Lưu");
            el.saveOverrideBtn = document.getElementById("permSaveOverrideBtn");
            el.saveOverrideBtn.addEventListener("click", saveOverrides);
            el.overrideEmpty.hidden = true;
            renderOverrideGrid();
        } catch (error) {
            notifyError(error);
        }
    }

    function renderOverrideGrid() {
        const groups = get(state.overrideMatrix, "groups", []);
        el.overrideGrid.innerHTML = groups.map(group => `
            <div class="perm-override-group" data-group-id="${get(group, "permissionGroupId", 0)}" data-group-text="${escapeHtml(normalizeSearch(`${get(group, "name", "")} ${get(group, "code", "")}`))}">
                <button type="button" class="perm-group-head" data-action="toggle-group">
                    <span><i class="fas fa-folder"></i> ${escapeHtml(get(group, "name", ""))}</span>
                    <i class="fas fa-chevron-up"></i>
                </button>
                <div class="perm-group-body">
                    ${get(group, "permissions", []).map(renderOverrideRow).join("")}
                </div>
            </div>
        `).join("");
        const hasMutablePermission = el.overrideGrid.querySelector("input[type='radio']:not(:disabled)") !== null;
        el.saveOverrideBtn.disabled = !hasMutablePermission;
        el.saveOverrideBtn.title = hasMutablePermission
            ? ""
            : "Bạn không có permission nào có thể thay đổi cho nhân viên này.";
        filterOverrides();
    }

    function renderOverrideRow(permission) {
        const effect = effectValue(get(permission, "overrideEffect", null));
        const permissionId = get(permission, "permissionId", "");
        const rowClass = effect === "Allow" ? "is-allow" : (effect === "Deny" ? "is-deny" : "");
        const roleAllowed = get(permission, "roleAllowed", false);

        return `
            <div class="perm-override-row ${rowClass}" data-permission-text="${escapeHtml(normalizeSearch(`${get(permission, "code", "")} ${get(permission, "name", "")} ${get(permission, "description", "")}`))}" data-permission-id="${permissionId}" title="${escapeHtml(get(permission, "readOnlyReason", ""))}">
                <div>
                    <div class="perm-name">${escapeHtml(get(permission, "code", ""))}</div>
                    <div class="perm-subtext">${escapeHtml(get(permission, "name", ""))}</div>
                </div>
                <div class="perm-role-state ${roleAllowed ? "is-yes" : "is-no"}">
                    <i class="fas ${roleAllowed ? "fa-check" : "fa-xmark"}"></i>
                    <span>${roleAllowed ? "Theo vai trò" : "Chưa có vai trò"}</span>
                </div>
                <div class="perm-radio-set">
                    <label><input type="radio" name="override-${permissionId}" value="Inherit" ${!effect ? "checked" : ""} ${get(permission, "canChange", true) ? "" : "disabled"} /><span>Kế thừa</span></label>
                    <label class="is-allow"><input type="radio" name="override-${permissionId}" value="Allow" ${effect === "Allow" ? "checked" : ""} ${get(permission, "canChange", true) ? "" : "disabled"} /><span>Cho phép</span></label>
                    <label class="is-deny"><input type="radio" name="override-${permissionId}" value="Deny" ${effect === "Deny" ? "checked" : ""} ${get(permission, "canChange", true) ? "" : "disabled"} /><span>Từ chối</span></label>
                </div>
            </div>
        `;
    }

    async function saveOverrides() {
        if (!state.selectedOverrideStaffId) return;
        const overrides = [...document.querySelectorAll(".perm-override-row")].map(row => {
            const permissionId = Number(row.dataset.permissionId);
            const checked = row.querySelector("input[type='radio']:checked");
            const value = checked ? checked.value : "Inherit";
            return {
                permissionId,
                effect: value === "Inherit" ? null : value
            };
        }).sort((a, b) => a.permissionId - b.permissionId);
        const button = el.saveOverrideBtn;
        const request = mutationRequest(
            "staff-overrides",
            state.selectedOverrideStaffId,
            { overrides });

        setButtonBusy(button, true, "Đang lưu...");
        try {
            await AdminMutationGuard.run(`staff-overrides-${state.selectedOverrideStaffId}`, button, () =>
                fetchJson(endpoint(`SaveStaffOverrides?staffId=${state.selectedOverrideStaffId}`), {
                    method: "POST",
                    body: JSON.stringify(request.body)
                }));
            request.complete();
            notifySuccess("Đã lưu quyền ghi đè");
            await selectOverrideStaff(state.selectedOverrideStaffId);
        } catch (error) {
            notifyError(error);
        } finally {
            setButtonBusy(button, false);
        }
    }

    async function selectScopeStaff(staffId) {
        state.selectedScopeStaffId = staffId;
        state.scopeReferenceCache.clear();
        el.saveScopeBtn.disabled = true;
        await loadStaff("scope");

        try {
            const data = await fetchJson(endpoint(`StaffScopes?staffId=${staffId}`));
            state.scopeData = data;
            state.selectedScopes = get(data, "scopes", []).map(scope => ({
                scopeTypeId: get(scope, "scopeTypeId", 0),
                scopeTypeCode: get(scope, "scopeTypeCode", ""),
                scopeTypeName: getScopeTypeLabel(scope),
                scopeRefId: get(scope, "scopeRefId", 0),
                scopeRefName: get(scope, "scopeRefName", "")
            }));
            el.scopeSelected.innerHTML = selectedHeadHtml(data, "permSaveScopeBtn", "Lưu");
            el.saveScopeBtn = document.getElementById("permSaveScopeBtn");
            el.saveScopeBtn.addEventListener("click", saveScopes);
            el.saveScopeBtn.disabled = !get(data, "canChange", false);
            el.saveScopeBtn.title = get(data, "readOnlyReason", "");
            renderScopeTypes();
            renderScopes();
        } catch (error) {
            notifyError(error);
        }
    }

    function selectedHeadHtml(data, buttonId, buttonText) {
        return `
            <div>
                <span>Nhân viên</span>
                <strong>${escapeHtml(get(data, "fullName", ""))}</strong>
                <div class="perm-subtext">${escapeHtml(get(data, "email", ""))}</div>
            </div>
            <button type="button" class="perm-primary-button" id="${buttonId}">
                <i class="fas fa-floppy-disk"></i>
                ${buttonText}
            </button>
        `;
    }

    function renderScopeTypes() {
        const types = get(state.scopeData, "scopeTypes", []);
        el.scopeTypeSelect.innerHTML = '<option value="">-- Chọn phạm vi --</option>' + types.map(type => `
            <option value="${get(type, "scopeTypeId", "")}">${escapeHtml(getScopeTypeLabel(type))}</option>
        `).join("");
        el.scopeTypeSelect.disabled = !get(state.scopeData, "canChange", false);
        el.scopeRefSelect.innerHTML = '<option value="">-- Chọn đối tượng --</option>';
        el.scopeRefSelect.disabled = true;
        el.addScopeBtn.disabled = true;
    }

    async function handleScopeTypeChange() {
        const scopeTypeId = Number(el.scopeTypeSelect.value);
        el.scopeRefSelect.innerHTML = '<option value="">-- Chọn đối tượng --</option>';
        el.scopeRefSelect.disabled = true;
        el.addScopeBtn.disabled = true;

        if (!scopeTypeId) return;

        if (scopeTypeId === 3) {
            await loadParentReferences(2);
            return;
        }

        if (scopeTypeId === 4) {
            await loadParentReferences(3);
            return;
        }

        el.scopeParentWrap.hidden = true;
        await loadScopeReferences(scopeTypeId);
    }

    async function loadParentReferences(parentScopeTypeId) {
        el.scopeParentWrap.hidden = false;
        el.scopeParentSelect.innerHTML = '<option value="">Đang tải...</option>';
        const refs = await fetchJson(endpoint(`ScopeReferences?scopeTypeId=${parentScopeTypeId}`));
        el.scopeParentSelect.innerHTML = '<option value="">-- Chọn khu vực cha --</option>' + refs.map(item => `
            <option value="${get(item, "id", "")}">${escapeHtml(get(item, "name", ""))}</option>
        `).join("");
    }

    async function loadScopeReferences(scopeTypeId, parentId) {
        el.scopeRefSelect.innerHTML = '<option value="">Đang tải...</option>';
        const cacheKey = scopeReferenceCacheKey(scopeTypeId, parentId);
        let refs = state.scopeReferenceCache.get(cacheKey);
        if (!refs) {
            const url = endpoint(`ScopeReferences?scopeTypeId=${scopeTypeId}${parentId ? `&parentId=${parentId}` : ""}`);
            refs = await fetchJson(url);
            state.scopeReferenceCache.set(cacheKey, refs || []);
        }

        renderAvailableScopeReferences(scopeTypeId, refs || []);
    }

    function scopeReferenceCacheKey(scopeTypeId, parentId) {
        return `${scopeTypeId}:${parentId || 0}`;
    }

    function getAvailableScopeReferences(scopeTypeId, refs) {
        const selectedIds = new Set(
            state.selectedScopes
                .filter(scope => Number(scope.scopeTypeId) === Number(scopeTypeId))
                .map(scope => Number(scope.scopeRefId)));

        return refs.filter(item => !selectedIds.has(Number(get(item, "id", 0))));
    }

    function renderAvailableScopeReferences(scopeTypeId, refs) {
        const availableRefs = getAvailableScopeReferences(scopeTypeId, refs);
        const placeholder = availableRefs.length
            ? "-- Chọn đối tượng --"
            : "-- Đã phân quyền tất cả đối tượng --";

        el.scopeRefSelect.innerHTML = `<option value="">${placeholder}</option>` + availableRefs.map(item => `
            <option value="${get(item, "id", "")}">${escapeHtml(get(item, "name", ""))}</option>
        `).join("");
        el.scopeRefSelect.disabled = !availableRefs.length;
        el.addScopeBtn.disabled = true;
    }

    function refreshCurrentScopeReferences() {
        const scopeTypeId = Number(el.scopeTypeSelect.value);
        if (!scopeTypeId) return;

        const requiresParent = scopeTypeId === 3 || scopeTypeId === 4;
        const parentId = requiresParent ? Number(el.scopeParentSelect.value) : null;
        if (requiresParent && !parentId) return;

        const refs = state.scopeReferenceCache.get(scopeReferenceCacheKey(scopeTypeId, parentId));
        if (refs) renderAvailableScopeReferences(scopeTypeId, refs);
    }

    function addScope() {
        const scopeTypeId = Number(el.scopeTypeSelect.value);
        const scopeRefId = Number(el.scopeRefSelect.value);
        if (!scopeTypeId || !scopeRefId) return;

        if (state.selectedScopes.some(scope => scope.scopeTypeId === scopeTypeId && scope.scopeRefId === scopeRefId)) {
            return;
        }

        const typeText = el.scopeTypeSelect.options[el.scopeTypeSelect.selectedIndex].textContent;
        const refText = el.scopeRefSelect.options[el.scopeRefSelect.selectedIndex].textContent;

        state.selectedScopes.push({
            scopeTypeId,
            scopeTypeName: typeText,
            scopeRefId,
            scopeRefName: refText
        });
        renderScopes();
        refreshCurrentScopeReferences();
    }

    function renderScopes() {
        el.scopeList.classList.toggle("is-readonly", !get(state.scopeData, "canChange", false));
        if (!state.selectedScopes.length) {
            el.scopeList.innerHTML = '<div class="perm-empty">Chưa có phạm vi.</div>';
            return;
        }

        el.scopeList.innerHTML = state.selectedScopes.map((scope, index) => `
            <div class="perm-scope-chip">
                <span>${escapeHtml(scope.scopeTypeName)}: ${escapeHtml(scope.scopeRefName || scope.scopeRefId)}</span>
                <button type="button" data-action="remove-scope" data-index="${index}" title="Xóa" ${get(state.scopeData, "canChange", false) ? "" : "disabled"}>
                    <i class="fas fa-xmark"></i>
                </button>
            </div>
        `).join("");
    }

    async function saveScopes() {
        if (!state.selectedScopeStaffId) return;
        const scopes = state.selectedScopes.map(scope => ({
            scopeTypeId: scope.scopeTypeId,
            scopeRefId: scope.scopeRefId
        })).sort((a, b) => a.scopeTypeId - b.scopeTypeId || a.scopeRefId - b.scopeRefId);
        const button = el.saveScopeBtn;
        const request = mutationRequest("staff-scopes", state.selectedScopeStaffId, { scopes });

        setButtonBusy(button, true, "Đang lưu...");
        try {
            await AdminMutationGuard.run(`staff-scopes-${state.selectedScopeStaffId}`, button, () =>
                fetchJson(endpoint(`SaveStaffScopes?staffId=${state.selectedScopeStaffId}`), {
                    method: "POST",
                    body: JSON.stringify(request.body)
                }));
            request.complete();
            notifySuccess("Đã lưu phạm vi cửa hàng");
            await selectScopeStaff(state.selectedScopeStaffId);
        } catch (error) {
            notifyError(error);
        } finally {
            setButtonBusy(button, false);
        }
    }

    function effectValue(value) {
        if (value === 1 || value === "1" || value === "Allow") return "Allow";
        if (value === 2 || value === "2" || value === "Deny") return "Deny";
        return null;
    }

    function filterRolePermissions() {
        const keyword = normalizeSearch(el.rolePermissionSearch.value);
        document.querySelectorAll(".perm-permission-group").forEach(group => {
            let visibleRows = 0;
            const groupMatches = !!keyword && group.dataset.groupText.includes(keyword);
            group.querySelectorAll(".perm-permission-row").forEach(row => {
                const visible = !keyword || groupMatches || row.dataset.permissionText.includes(keyword);
                row.classList.toggle("is-hidden", !visible);
                if (visible) visibleRows += 1;
            });
            group.classList.toggle("is-hidden", visibleRows === 0);
            const body = group.querySelector(".perm-group-body");
            const icon = group.querySelector(".fa-chevron-up, .fa-chevron-down");
            const collapsed = state.collapsedGroups.role.has(Number(group.dataset.groupId));
            body.hidden = keyword ? false : collapsed;
            icon.classList.toggle("fa-chevron-up", !body.hidden);
            icon.classList.toggle("fa-chevron-down", body.hidden);
        });
    }

    function filterOverrides() {
        const keyword = normalizeSearch(el.overrideSearch.value);
        document.querySelectorAll(".perm-override-group").forEach(group => {
            let visibleRows = 0;
            const groupMatches = !!keyword && group.dataset.groupText.includes(keyword);
            group.querySelectorAll(".perm-override-row").forEach(row => {
                const visible = !keyword || groupMatches || row.dataset.permissionText.includes(keyword);
                row.classList.toggle("is-hidden", !visible);
                if (visible) visibleRows += 1;
            });
            group.classList.toggle("is-hidden", visibleRows === 0);
            const body = group.querySelector(".perm-group-body");
            const icon = group.querySelector(".fa-chevron-up, .fa-chevron-down");
            const collapsed = state.collapsedGroups.override.has(Number(group.dataset.groupId));
            body.hidden = keyword ? false : collapsed;
            icon.classList.toggle("fa-chevron-up", !body.hidden);
            icon.classList.toggle("fa-chevron-down", body.hidden);
        });
    }

    function updateOverrideRowState(input) {
        const row = input.closest(".perm-override-row");
        row.classList.toggle("is-allow", input.value === "Allow");
        row.classList.toggle("is-deny", input.value === "Deny");
    }

    function rowLoading(colspan) {
        return `<tr><td colspan="${colspan}" class="text-center text-muted py-4">Đang tải...</td></tr>`;
    }

    function emptyRow(colspan, message) {
        return `<tr><td colspan="${colspan}" class="text-center text-muted py-4">${escapeHtml(message)}</td></tr>`;
    }

    function initTabs() {
        document.querySelectorAll(".perm-tab").forEach(button => {
            button.addEventListener("click", () => {
                const tab = button.dataset.tab;
                state.activeTab = tab;
                document.querySelectorAll(".perm-tab").forEach(item => item.classList.toggle("is-active", item === button));
                document.querySelectorAll(".perm-panel").forEach(panel => panel.classList.toggle("is-active", panel.dataset.panel === tab));

                if (tab === "roles") loadRoles();
                if (tab === "assign") loadStaff("assign");
                if (tab === "override") loadStaff("override");
                if (tab === "scope") loadStaff("scope");
            });
        });
    }

    function bindEvents() {
        el.refreshBtn.addEventListener("click", refreshActiveTab);
        el.roleSearch.addEventListener("input", debounce(() => {
            state.roles.search = el.roleSearch.value;
            state.roles.pageIndex = 1;
            loadRoles();
        }, 250));

        el.assignSearch.addEventListener("input", debounce(() => {
            state.staff.assign.search = el.assignSearch.value;
            state.staff.assign.pageIndex = 1;
            loadStaff("assign");
        }, 250));

        el.overrideStaffSearch.addEventListener("input", debounce(() => {
            state.staff.override.search = el.overrideStaffSearch.value;
            state.staff.override.pageIndex = 1;
            loadStaff("override");
        }, 250));

        el.scopeStaffSearch.addEventListener("input", debounce(() => {
            state.staff.scope.search = el.scopeStaffSearch.value;
            state.staff.scope.pageIndex = 1;
            loadStaff("scope");
        }, 250));

        document.addEventListener("click", event => {
            const target = event.target.closest("[data-action]");
            if (!target) return;

            const action = target.dataset.action;
            if (action === "open-role") openRolePermission(Number(target.dataset.roleId));
            if (action === "open-staff-role") openStaffRole(Number(target.dataset.staffId));
            if (action === "select-override-staff") selectOverrideStaff(Number(target.dataset.staffId));
            if (action === "select-scope-staff") selectScopeStaff(Number(target.dataset.staffId));
            if (action === "remove-scope") {
                state.selectedScopes.splice(Number(target.dataset.index), 1);
                renderScopes();
                refreshCurrentScopeReferences();
            }
            if (action === "toggle-group") {
                const group = target.closest(".perm-permission-group, .perm-override-group");
                const body = group.querySelector(".perm-group-body");
                const icon = target.querySelector(".fa-chevron-up, .fa-chevron-down");
                body.hidden = !body.hidden;
                icon.classList.toggle("fa-chevron-up", !body.hidden);
                icon.classList.toggle("fa-chevron-down", body.hidden);
                const set = group.classList.contains("perm-permission-group")
                    ? state.collapsedGroups.role : state.collapsedGroups.override;
                const groupId = Number(group.dataset.groupId);
                if (body.hidden) set.add(groupId); else set.delete(groupId);
            }
        });

        document.addEventListener("change", event => {
            if (event.target.classList.contains("role-permission-check")) updateSelectedPermissionCount();
            if (event.target.name && event.target.name.startsWith("override-")) updateOverrideRowState(event.target);
        });

        el.rolePermissionSearch.addEventListener("input", filterRolePermissions);
        el.overrideSearch.addEventListener("input", filterOverrides);
        el.selectAllPermissions.addEventListener("click", () => {
            document.querySelectorAll(".role-permission-check:not(:disabled)").forEach(input => input.checked = true);
            updateSelectedPermissionCount();
        });
        el.clearPermissions.addEventListener("click", () => {
            document.querySelectorAll(".role-permission-check:not(:disabled)").forEach(input => input.checked = false);
            updateSelectedPermissionCount();
        });
        el.expandPermissions.addEventListener("click", () => {
            document.querySelectorAll(".perm-permission-group .perm-group-body").forEach(body => body.hidden = false);
            state.collapsedGroups.role.clear();
            document.querySelectorAll(".perm-permission-group .fa-chevron-down").forEach(icon => {
                icon.classList.remove("fa-chevron-down"); icon.classList.add("fa-chevron-up");
            });
        });
        el.saveRolePermissions.addEventListener("click", saveRolePermissions);
        el.saveStaffRoles.addEventListener("click", saveStaffRoles);
        el.saveOverrideBtn.addEventListener("click", saveOverrides);
        el.saveScopeBtn.addEventListener("click", saveScopes);
        el.scopeTypeSelect.addEventListener("change", () => handleScopeTypeChange().catch(notifyError));
        el.scopeParentSelect.addEventListener("change", () => {
            const scopeTypeId = Number(el.scopeTypeSelect.value);
            const parentId = Number(el.scopeParentSelect.value);
            if (scopeTypeId && parentId) loadScopeReferences(scopeTypeId, parentId).catch(notifyError);
        });
        el.scopeRefSelect.addEventListener("change", () => {
            el.addScopeBtn.disabled = !get(state.scopeData, "canChange", false)
                || !Number(el.scopeRefSelect.value);
        });
        el.addScopeBtn.addEventListener("click", addScope);
    }

    function refreshActiveTab() {
        if (state.activeTab === "roles") loadRoles();
        if (state.activeTab === "assign") loadStaff("assign");
        if (state.activeTab === "override") loadStaff("override");
        if (state.activeTab === "scope") loadStaff("scope");
    }

    initTabs();
    bindEvents();
    const initialStaffId = Number(root.dataset.initialStaffId || 0);
    if (initialStaffId > 0) {
        document.querySelector('.perm-tab[data-tab="assign"]')?.click();
        openStaffRole(initialStaffId);
    } else {
        loadRoles();
    }

    window.AdminPermissionPage = {
        refresh: refreshActiveTab
    };
})();
