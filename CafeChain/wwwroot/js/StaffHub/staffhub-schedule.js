(function (window, document) {
    "use strict";

    function initialize() {
        const root = document.getElementById("staffHubApp");
        if (!root || root.dataset.initialized === "true") return;
        root.dataset.initialized = "true";

        const token = root.querySelector('input[name="__RequestVerificationToken"]')?.value || "";

        function notify(message, success) {
            if (window.Swal) return Swal.fire(success ? "Thành công" : "Không thành công", message, success ? "success" : "error");
            window.alert(message);
            return Promise.resolve();
        }

        async function post(url, data) {
            data.set("__RequestVerificationToken", token);
            const response = await fetch(url, {
                method: "POST",
                body: data,
                credentials: "same-origin",
                headers: { "Accept": "application/json", "X-Requested-With": "XMLHttpRequest" }
            });
            let result;
            try {
                result = await response.json();
            } catch {
                result = { message: "Máy chủ trả về dữ liệu không hợp lệ." };
            }
            if (!response.ok) throw new Error(result.message || "Không thể thực hiện thao tác.");
            return result;
        }

        const openButton = document.getElementById("openPosButton");
        const dialog = document.getElementById("openPosPreviewDialog");
        const continueButton = document.getElementById("continueOpenPosPreview");
        const cancelButton = document.getElementById("cancelOpenPosPreview");
        let issuingToken = false;

        function parseUtc(value) {
            if (!value) return null;
            const normalized = /z$|[+-]\d\d:\d\d$/i.test(value) ? value : `${value}Z`;
            const date = new Date(normalized);
            return Number.isNaN(date.getTime()) ? null : date;
        }

        function formatDate(value) {
            const date = parseUtc(value);
            if (!date) return "—";

            const parts = new Intl.DateTimeFormat("vi-VN", {
                timeZone: "Asia/Ho_Chi_Minh",
                hourCycle: "h23",
                hour: "2-digit",
                minute: "2-digit",
                day: "2-digit",
                month: "2-digit",
                year: "numeric"
            }).formatToParts(date);
            const valueOf = type => parts.find(part => part.type === type)?.value || "";
            return `${valueOf("hour")}:${valueOf("minute")} ${valueOf("day")}/${valueOf("month")}/${valueOf("year")}`;
        }

        function contextText(context) {
            if (context === "WITHIN_SCHEDULE") return "Trong lịch dự kiến";
            if (context === "LATE_FOR_SCHEDULE") return "Mở POS trễ so với lịch";
            return "Mở POS ngoài lịch";
        }

        function showAssessment(assessment) {
            const context = assessment.openContext || assessment.OpenContext || "OUTSIDE_SCHEDULE";
            const serverNow = assessment.serverNowUtc || assessment.ServerNowUtc;
            const autoCloseAt = assessment.autoCloseAtUtc || assessment.AutoCloseAtUtc;
            const plannedStart = assessment.plannedStartUtc || assessment.PlannedStartUtc;
            const plannedEnd = assessment.plannedEndUtc || assessment.PlannedEndUtc;
            const minutesLate = Number(assessment.minutesLate ?? assessment.MinutesLate ?? 0);
            const reasonRequired = Boolean(assessment.reasonRequired ?? assessment.ReasonRequired);
            const approvalRequired = Boolean(assessment.approvalRequired ?? assessment.ApprovalRequired);

            document.getElementById("openPosPreviewTitle").textContent = contextText(context);
            document.getElementById("openPosContextLabel").textContent = contextText(context);
            document.getElementById("openPosServerTime").textContent = formatDate(serverNow);

            const plannedRow = document.getElementById("openPosPlannedRow");
            plannedRow.hidden = !plannedStart;
            document.getElementById("openPosPlannedTime").textContent = plannedStart
                ? `${formatDate(plannedStart)} → ${formatDate(plannedEnd)}`
                : "—";

            const expiryRow = document.getElementById("openPosExpiryRow");
            expiryRow.hidden = !autoCloseAt;
            document.getElementById("openPosExpiryTime").textContent = formatDate(autoCloseAt);

            const notes = [];
            if (context === "OUTSIDE_SCHEDULE") {
                notes.push("Không có lịch phù hợp. Phiên POS ngoài lịch chỉ hoạt động tối đa 6 giờ.");
            } else if (context === "LATE_FOR_SCHEDULE") {
                notes.push(`Bạn đang mở trễ khoảng ${minutesLate} phút so với giờ dự kiến.`);
            }
            if (reasonRequired) notes.push("POS sẽ yêu cầu lý do cụ thể từ 10 đến 500 ký tự.");
            if (approvalRequired) notes.push("POS sẽ yêu cầu OTP của người có quyền phê duyệt trong phạm vi cửa hàng.");
            document.getElementById("openPosPreviewNotice").textContent = notes.join(" ");

            if (typeof dialog.showModal === "function") dialog.showModal();
            else dialog.setAttribute("open", "open");
        }

        async function issueAndRedirect() {
            if (issuingToken) return;
            issuingToken = true;
            continueButton?.setAttribute("disabled", "disabled");
            openButton?.setAttribute("disabled", "disabled");
            try {
                const result = await post(root.dataset.issuePosUrl, new FormData());
                if (!result.exchangeCode || !result.posUrl || !result.exchangeUrl) {
                    throw new Error("Không nhận được thông tin mở POS.");
                }
                const exchangeUrl = new URL(result.exchangeUrl, window.location.origin).toString();
                const fragment = new URLSearchParams({
                    exchange_code: result.exchangeCode,
                    exchange_url: exchangeUrl
                });
                window.location.assign(`${result.posUrl}#${fragment.toString()}`);
            } catch (error) {
                issuingToken = false;
                continueButton?.removeAttribute("disabled");
                openButton?.removeAttribute("disabled");
                await notify(error.message, false);
            }
        }

        openButton?.addEventListener("click", async event => {
            await AdminMutationGuard.run("staffhub-open-pos", event.currentTarget, async () => {
                try {
                    const result = await post(root.dataset.previewPosUrl, new FormData());
                    const assessment = result.data || result;
                    const context = assessment.openContext || assessment.OpenContext;
                    if (context === "WITHIN_SCHEDULE") {
                        await issueAndRedirect();
                        return;
                    }
                    showAssessment(assessment);
                } catch (error) {
                    await notify(error.message, false);
                }
            });
        });

        cancelButton?.addEventListener("click", () => {
            if (issuingToken) return;
            if (typeof dialog.close === "function") dialog.close();
            else dialog.removeAttribute("open");
        });

        continueButton?.addEventListener("click", issueAndRedirect);
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", initialize, { once: true });
    else initialize();
})(window, document);
