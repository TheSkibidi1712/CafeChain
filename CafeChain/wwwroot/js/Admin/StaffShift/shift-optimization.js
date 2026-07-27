(() => {
    "use strict";

    const root = document.getElementById("shiftOptimizationRoot");
    if (!root) return;

    const token = document.querySelector("#shiftOptimizationToken input")?.value || "";
    const notice = document.getElementById("shiftOptimizationNotice");
    const configOutput = document.getElementById("shiftOptimizationConfig");
    const endpoints = {
        availability: root.dataset.availability,
        constraint: root.dataset.constraint,
        requirement: root.dataset.requirement,
        timeoff: root.dataset.timeoff
    };
    const setupKeys = {
        availability: "availability",
        constraint: "constraints",
        requirement: "requirements",
        timeoff: "timeOffs"
    };
    const dayLabels = {
        0: "Chủ nhật", 1: "Thứ 2", 2: "Thứ 3", 3: "Thứ 4",
        4: "Thứ 5", 5: "Thứ 6", 6: "Thứ 7"
    };
    const statusLabels = {
        APPROVED: "Đã duyệt",
        PENDING: "Chờ duyệt",
        REJECTED: "Từ chối"
    };
    let setup = {};

    try {
        setup = JSON.parse(document.getElementById("shiftOptimizationSetup")?.textContent || "{}");
    } catch {
        setup = {};
    }

    const show = (message, error = false) => {
        if (!notice) return;
        notice.hidden = false;
        notice.className = `alert mt-3 ${error ? "alert-danger" : "alert-success"}`;
        notice.textContent = message;
    };

    const payload = form => Object.fromEntries(
        [...new FormData(form)].map(([key, value]) => {
            if (typeof value !== "string") return [key, value];
            if (/^\d+(\.\d+)?$/.test(value)) return [key, Number(value)];
            return [key, value];
        })
    );

    async function post(url, body) {
        if (!url) throw new Error("Chức năng lưu cấu hình chưa được khai báo.");
        const response = await fetch(url, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "RequestVerificationToken": token,
                "X-Requested-With": "XMLHttpRequest"
            },
            credentials: "same-origin",
            body: JSON.stringify(body)
        });
        const json = await response.json().catch(() => ({}));
        if (!response.ok || json.success === false)
            throw new Error(json.message || "Không thể lưu cấu hình.");
        return json.data;
    }

    function formatDate(value, includeTime = false) {
        if (!value) return "—";
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) return String(value).slice(0, includeTime ? 16 : 10);
        return new Intl.DateTimeFormat("vi-VN", includeTime
            ? { dateStyle: "short", timeStyle: "short" }
            : { dateStyle: "short" }).format(date);
    }

    function displayValue(field, value) {
        if (field === "dayOfWeek") return dayLabels[value] || value;
        if (field === "fromUtc" || field === "toUtc") return formatDate(value, true);
        if (field === "effectiveFrom") return formatDate(value);
        if (field === "status") return statusLabels[String(value).toUpperCase()] || value;
        return value;
    }

    function renderConfig() {
        if (!configOutput) return;
        const sections = [
            ["availability", "Khả dụng", ["staffName", "dayOfWeek", "startTime", "endTime", "effectiveFrom"]],
            ["constraints", "Giới hạn giờ", ["staffName", "targetWeeklyHours", "maxWeeklyHours", "maxDailyHours", "minimumRestMinutes"]],
            ["requirements", "Định mức", ["shiftName", "dayOfWeek", "minimumStaff", "targetStaff", "maximumStaff", "requiredRoleName"]],
            ["timeOffs", "Nghỉ phép", ["staffName", "fromUtc", "toUtc", "reason", "status"]]
        ];
        const labels = {
            staffName: "Nhân viên", dayOfWeek: "Ngày", startTime: "Bắt đầu", endTime: "Kết thúc",
            effectiveFrom: "Hiệu lực từ", targetWeeklyHours: "Mục tiêu giờ/tuần",
            maxWeeklyHours: "Tối đa giờ/tuần", maxDailyHours: "Tối đa giờ/ngày",
            minimumRestMinutes: "Nghỉ tối thiểu (phút)", shiftName: "Ca làm",
            minimumStaff: "Tối thiểu (người)", targetStaff: "Mục tiêu (người)",
            maximumStaff: "Tối đa (người)", requiredRoleName: "Vai trò bắt buộc",
            fromUtc: "Từ", toUtc: "Đến", reason: "Lý do", status: "Trạng thái"
        };

        configOutput.replaceChildren();
        sections.forEach(([key, title, fields]) => {
            const rows = Array.isArray(setup[key]) ? setup[key] : [];
            const col = document.createElement("div");
            col.className = "col-xl-3 col-md-6";
            const card = document.createElement("div");
            card.className = "config-card";
            const heading = document.createElement("h3");
            heading.textContent = `${title} (${rows.length})`;
            card.append(heading);

            if (!rows.length) {
                const empty = document.createElement("p");
                empty.className = "text-muted small mb-0";
                empty.textContent = "Chưa có cấu hình.";
                card.append(empty);
            } else {
                rows.slice(0, 8).forEach((row, index) => {
                    const item = document.createElement("dl");
                    item.className = "config-item";
                    item.setAttribute("aria-label", `${title} ${index + 1}`);
                    fields.forEach(field => {
                        const value = row[field];
                        if (value === undefined || value === null || value === "") return;
                        const term = document.createElement("dt");
                        term.textContent = labels[field] || field;
                        const description = document.createElement("dd");
                        description.textContent = String(displayValue(field, value));
                        item.append(term, description);
                    });
                    card.append(item);
                });
                if (rows.length > 8) {
                    const more = document.createElement("p");
                    more.className = "text-muted small mt-2 mb-0";
                    more.textContent = `Hiển thị 8 mục đầu trong tổng số ${rows.length} mục.`;
                    card.append(more);
                }
            }

            col.append(card);
            configOutput.append(col);
        });
    }

    function selectedText(form, selector) {
        const select = form.querySelector(selector);
        return select?.selectedOptions?.[0]?.textContent?.trim() || "";
    }

    function enrichSavedConfiguration(kind, body, form) {
        const item = { ...body };
        if (kind === "availability" || kind === "constraint" || kind === "timeoff")
            item.staffName = selectedText(form, "[name='staffId']");
        if (kind === "requirement") {
            item.shiftName = selectedText(form, "[name='shiftId']");
            item.requiredRoleName = selectedText(form, "[name='requiredRoleId']");
        }
        if (kind === "timeoff") item.status = "APPROVED";
        return item;
    }

    document.querySelectorAll("form[data-kind]").forEach(form => {
        form.addEventListener("submit", async event => {
            event.preventDefault();
            const kind = form.dataset.kind;
            if (!Object.hasOwn(endpoints, kind)) return;
            const body = payload(form);
            const submit = form.querySelector("[type='submit']");
            if (submit) submit.disabled = true;
            try {
                await post(endpoints[kind], body);
                const key = setupKeys[kind];
                if (!Array.isArray(setup[key])) setup[key] = [];
                setup[key].push(enrichSavedConfiguration(kind, body, form));
                renderConfig();
                show("Đã lưu cấu hình. Dữ liệu mới sẽ được dùng cho cảnh báo thiếu lịch.");
            } catch (error) {
                show(error.message, true);
            } finally {
                if (submit) submit.disabled = false;
            }
        });
    });

    renderConfig();
})();
