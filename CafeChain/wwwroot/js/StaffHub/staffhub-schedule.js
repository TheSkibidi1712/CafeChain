(function (window, document) {
    "use strict";

    function initialize() {
        const root = document.getElementById("staffHubApp");
        if (!root || root.dataset.initialized === "true") return;
        root.dataset.initialized = "true";
        const antiForgery = root.querySelector('input[name="__RequestVerificationToken"]')?.value || "";

        const openButton = document.getElementById("openPosButton");
        const terminalSelect = document.getElementById("staffHubTerminalSelect");
        const previewDialog = document.getElementById("openPosPreviewDialog");
        const continueButton = document.getElementById("continueOpenPosPreview");
        const resumeButton = document.getElementById("resumeExistingPos");
        const approvalFields = document.getElementById("openPosApprovalFields");
        const otpFields = document.getElementById("openPosOtpFields");
        const reasonInput = document.getElementById("openPosReason");
        const otpInput = document.getElementById("openPosOtpCode");
        const otpStatus = document.getElementById("openPosOtpStatus");
        const registrationDialog = document.getElementById("terminalRegistrationDialog");
        const requestOpenOtpButton = document.getElementById("requestOpenPosOtp");
        const resendOpenOtpButton = document.getElementById("resendOpenPosOtp");
        const requestTerminalOtpButton = document.getElementById("requestTerminalOtp");
        const resendTerminalOtpButton = document.getElementById("resendTerminalOtp");
        const operatorPinDialog = document.getElementById("operatorPinDialog");
        const operatorPinForm = document.getElementById("operatorPinForm");
        const operatorCurrentPassword = document.getElementById("operatorCurrentPassword");
        const operatorNewPin = document.getElementById("operatorNewPin");
        const operatorPinStatus = document.getElementById("operatorPinStatus");
        const saveOperatorPinButton = document.getElementById("saveOperatorPin");

        let requestKey = crypto.randomUUID();
        let assessment = null;
        let otpChallengePublicId = null;
        let verifiedOtpChallengePublicId = null;
        let registrationTerminalId = null;
        let registrationRequestKey = null;
        let registrationChallengeId = null;
        const resendCountdowns = new Map();

        function notify(message, success) {
            if (typeof window.showToast === "function") {
                window.showToast(
                    message || (success ? "Thao tác đã hoàn tất." : "Không thể thực hiện thao tác."),
                    success ? "success" : "error");
                return Promise.resolve();
            }
            if (window.Swal) return Swal.fire(success ? "Thành công" : "Không thành công", message, success ? "success" : "error");
            window.alert(message);
            return Promise.resolve();
        }

        async function post(url, values) {
            const data = new FormData();
            data.set("__RequestVerificationToken", antiForgery);
            Object.entries(values || {}).forEach(([key, value]) => {
                if (value !== null && value !== undefined) data.set(key, String(value));
            });
            const response = await fetch(url, {
                method: "POST", body: data, credentials: "same-origin",
                headers: { Accept: "application/json", "X-Requested-With": "XMLHttpRequest" }
            });
            const result = await response.json().catch(() => ({ message: "Máy chủ trả về dữ liệu không hợp lệ." }));
            if (!response.ok) {
                const statusMessage = {
                    401: "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
                    403: "Bạn không có quyền thực hiện thao tác này.",
                    409: "Dữ liệu vừa thay đổi. Vui lòng tải lại và thử lại."
                }[response.status];
                const serverMessage = response.status >= 500
                    ? `Hệ thống đang gặp lỗi. Vui lòng thử lại sau${result.correlationId ? `. Mã tra cứu: ${result.correlationId}` : "."}`
                    : null;
                const error = new Error(result.message || statusMessage || serverMessage || "Không thể thực hiện thao tác. Vui lòng thử lại.");
                error.errorCode = result.errorCode;
                error.data = result.data;
                throw error;
            }
            return result;
        }

        function showDialog(dialog) {
            if (dialog && window.bootstrap?.Modal) bootstrap.Modal.getOrCreateInstance(dialog).show();
        }

        function closeDialog(dialog) {
            if (dialog && window.bootstrap?.Modal) bootstrap.Modal.getOrCreateInstance(dialog).hide();
        }

        function parseUtc(value) {
            if (!value) return null;
            const normalized = /z$|[+-]\d\d:\d\d$/i.test(value) ? value : `${value}Z`;
            const date = new Date(normalized);
            return Number.isNaN(date.getTime()) ? null : date;
        }

        function formatDate(value) {
            const date = parseUtc(value);
            if (!date) return "—";
            return new Intl.DateTimeFormat("vi-VN", {
                timeZone: "Asia/Ho_Chi_Minh", hourCycle: "h23", hour: "2-digit", minute: "2-digit",
                day: "2-digit", month: "2-digit", year: "numeric"
            }).format(date);
        }

        function read(value, camel, pascal) { return value?.[camel] ?? value?.[pascal]; }

        function formatCountdown(totalSeconds) {
            const safeSeconds = Math.max(0, Math.ceil(Number(totalSeconds) || 0));
            const minutes = Math.floor(safeSeconds / 60);
            const seconds = safeSeconds % 60;
            return `${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}`;
        }

        function clearResendCountdown(button) {
            if (!button) return;
            const timer = resendCountdowns.get(button);
            if (timer) window.clearInterval(timer);
            resendCountdowns.delete(button);
        }

        function resetResendButton(button) {
            if (!button) return;
            clearResendCountdown(button);
            button.textContent = "Gửi lại OTP";
            button.disabled = true;
        }

        function startResendCountdown(button, rawSeconds, canResend) {
            if (!button) return;
            clearResendCountdown(button);
            const parsedSeconds = Number(rawSeconds);
            if (!Number.isFinite(parsedSeconds)) {
                resetResendButton(button);
                return;
            }

            let remaining = Math.max(0, Math.ceil(parsedSeconds));
            const render = () => {
                if (remaining > 0) {
                    button.textContent = `Gửi lại OTP (${formatCountdown(remaining)})`;
                    button.disabled = true;
                    return;
                }
                clearResendCountdown(button);
                button.textContent = "Gửi lại OTP";
                button.disabled = !canResend();
            };

            render();
            if (remaining <= 0) return;
            const timer = window.setInterval(() => {
                remaining -= 1;
                render();
            }, 1000);
            resendCountdowns.set(button, timer);
        }

        function resetOpenOtpState() {
            otpChallengePublicId = null;
            verifiedOtpChallengePublicId = null;
            if (otpInput) otpInput.value = "";
            if (otpStatus) otpStatus.textContent = "";
            resetResendButton(resendOpenOtpButton);
        }

        function resetTerminalOtpState() {
            registrationChallengeId = null;
            const otp = document.getElementById("terminalRegistrationOtp");
            const status = document.getElementById("terminalRegistrationStatus");
            if (otp) otp.value = "";
            if (status) status.textContent = "";
            resetResendButton(resendTerminalOtpButton);
        }

        function contextText(context, minutesEarly) {
            if (context === "WITHIN_SCHEDULE" && minutesEarly > 0) return "Mở POS sớm";
            if (context === "WITHIN_SCHEDULE") return "Được mở POS bình thường";
            if (context === "LATE_FOR_SCHEDULE") return "Mở POS trễ";
            return "Mở POS ngoài lịch";
        }

        function renderBlocking(data, errorCode, message) {
            const blocking = read(data, "blockingWorkShift", "BlockingWorkShift");
            assessment = data || {};
            document.getElementById("openPosPreviewTitle").textContent = errorCode === "TERMINAL_ALREADY_HAS_OPEN_SHIFT"
                ? "Terminal đang được sử dụng" : message;
            document.getElementById("openPosContextLabel").textContent = message;
            document.getElementById("openPosPreviewNotice").textContent = message;
            document.getElementById("openPosBlockingIdRow").hidden = !blocking;
            document.getElementById("openPosBlockingTerminalRow").hidden = !blocking;
            document.getElementById("openPosBlockingStartRow").hidden = !blocking;
            document.getElementById("openPosBlockingStatusRow").hidden = !blocking;
            document.getElementById("openPosBlockingExpiryRow").hidden = !blocking;
            if (blocking) {
                document.getElementById("openPosBlockingId").textContent = `#${read(blocking, "workShiftId", "WorkShiftId")}`;
                document.getElementById("openPosBlockingTerminal").textContent = read(blocking, "terminalName", "TerminalName") || read(blocking, "terminalId", "TerminalId") || "—";
                const status = read(blocking, "status", "Status");
                document.getElementById("openPosBlockingStart").textContent = formatDate(read(blocking, "startTimeUtc", "StartTimeUtc"));
                document.getElementById("openPosBlockingStatus").textContent = status;
                document.getElementById("openPosBlockingExpiry").textContent = formatDate(read(blocking, "autoCloseAtUtc", "AutoCloseAtUtc"));
                resumeButton.hidden = errorCode === "TERMINAL_ALREADY_HAS_OPEN_SHIFT";
                resumeButton.textContent = status === "OPEN" ? "Tiếp tục POS"
                    : status === "CLOSING" ? "Hoàn tất đóng ca" : "Kiểm đếm và đóng";
            }
            continueButton.hidden = true;
            approvalFields.hidden = true;
            showDialog(previewDialog);
        }

        function renderAssessment(value) {
            assessment = value;
            const context = read(value, "openContext", "OpenContext") || "OUTSIDE_SCHEDULE";
            const minutesEarly = Number(read(value, "minutesEarly", "MinutesEarly") || 0);
            const minutesLate = Number(read(value, "minutesLate", "MinutesLate") || 0);
            const reasonRequired = Boolean(read(value, "reasonRequired", "ReasonRequired"));
            const approvalRequired = Boolean(read(value, "approvalRequired", "ApprovalRequired"));
            const label = contextText(context, minutesEarly);
            document.getElementById("openPosPreviewTitle").textContent = label;
            document.getElementById("openPosContextLabel").textContent = label;
            document.getElementById("openPosServerTime").textContent = formatDate(read(value, "serverNowUtc", "ServerNowUtc"));
            const plannedStart = read(value, "plannedStartUtc", "PlannedStartUtc");
            document.getElementById("openPosPlannedRow").hidden = !plannedStart;
            document.getElementById("openPosPlannedTime").textContent = plannedStart
                ? `${formatDate(plannedStart)} → ${formatDate(read(value, "plannedEndUtc", "PlannedEndUtc"))}` : "—";
            const autoClose = read(value, "autoCloseAtUtc", "AutoCloseAtUtc");
            document.getElementById("openPosExpiryRow").hidden = !autoClose;
            document.getElementById("openPosExpiryTime").textContent = formatDate(autoClose);
            document.getElementById("openPosBlockingIdRow").hidden = true;
            document.getElementById("openPosBlockingTerminalRow").hidden = true;
            document.getElementById("openPosBlockingStartRow").hidden = true;
            document.getElementById("openPosBlockingStatusRow").hidden = true;
            document.getElementById("openPosBlockingExpiryRow").hidden = true;
            const notes = [];
            if (minutesEarly) notes.push(`Bạn đang mở sớm khoảng ${minutesEarly} phút.`);
            if (minutesLate) notes.push(`Bạn đang mở trễ khoảng ${minutesLate} phút.`);
            if (context === "OUTSIDE_SCHEDULE") notes.push("Phiên ngoài lịch có thời lượng tối đa 6 giờ.");
            if (reasonRequired) notes.push("Cần nhập lý do từ 10 đến 500 ký tự tại StaffHub.");
            if (approvalRequired) notes.push("Cần OTP phê duyệt hợp lệ trước khi sang POS.");
            document.getElementById("openPosPreviewNotice").textContent = notes.join(" ") || "Thông tin mở POS đã được backend xác nhận.";
            approvalFields.hidden = !reasonRequired;
            otpFields.hidden = !approvalRequired;
            continueButton.hidden = false;
            resumeButton.hidden = true;
            verifiedOtpChallengePublicId = null;
            otpChallengePublicId = null;
            otpStatus.textContent = "";
            resetResendButton(resendOpenOtpButton);
            showDialog(previewDialog);
        }

        async function previewOpen() {
            const terminalId = terminalSelect?.value;
            if (!terminalId) return notify("Vui lòng chọn terminal POS trước khi tiếp tục.", false);
            try {
                const result = await post(root.dataset.previewPosUrl, { TerminalId: terminalId, RequestKey: requestKey });
                renderAssessment(result.data || result);
            } catch (error) {
                if (["STAFF_ALREADY_HAS_OPEN_SHIFT", "TERMINAL_ALREADY_HAS_OPEN_SHIFT", "WORKSHIFT_PENDING_CLOSE"].includes(error.errorCode)) {
                    renderBlocking(error.data, error.errorCode, error.message);
                    return;
                }
                await notify(error.message, false);
            }
        }

        async function redirectWithTicket(result) {
            const exchangeUrl = new URL(result.exchangeUrl, window.location.origin).toString();
            const fragment = new URLSearchParams({ exchange_code: result.exchangeCode, exchange_url: exchangeUrl });
            window.location.assign(`${result.posUrl}#${fragment.toString()}`);
        }

        async function issueOpenTicket() {
            const reasonRequired = Boolean(read(assessment, "reasonRequired", "ReasonRequired"));
            const approvalRequired = Boolean(read(assessment, "approvalRequired", "ApprovalRequired"));
            const reason = reasonInput?.value.trim() || "";
            if (reasonRequired && (reason.length < 10 || reason.length > 500))
                return notify("Lý do phải có từ 10 đến 500 ký tự.", false);
            if (approvalRequired && !verifiedOtpChallengePublicId)
                return notify("Vui lòng xác nhận OTP trước khi sang POS.", false);
            continueButton.disabled = true;
            try {
                const result = await post(root.dataset.issuePosUrl, {
                    TerminalId: terminalSelect.value, RequestKey: requestKey, Reason: reason,
                    OtpChallengePublicId: verifiedOtpChallengePublicId
                });
                await redirectWithTicket(result);
            } catch (error) {
                continueButton.disabled = false;
                await notify(error.message, false);
            }
        }

        openButton?.addEventListener("click", () =>
            AdminMutationGuard.run("staffhub-open-pos", openButton, previewOpen));
        document.getElementById("cancelOpenPosPreview")?.addEventListener("click", () => closeDialog(previewDialog));
        continueButton?.addEventListener("click", issueOpenTicket);
        resumeButton?.addEventListener("click", async () => {
            resumeButton.disabled = true;
            try { await redirectWithTicket(await post(root.dataset.resumePosUrl, {})); }
            catch (error) { resumeButton.disabled = false; await notify(error.message, false); }
        });

        requestOpenOtpButton?.addEventListener("click", async () => {
            const reason = reasonInput.value.trim();
            if (reason.length < 10 || reason.length > 500) return notify("Lý do phải có từ 10 đến 500 ký tự.", false);
            requestOpenOtpButton.disabled = true;
            try {
                const result = await post(root.dataset.requestOpenOtpUrl, {
                    TerminalId: terminalSelect.value, RequestKey: requestKey, Reason: reason
                });
                otpChallengePublicId = read(result.data, "otpChallengePublicId", "OtpChallengePublicId");
                otpStatus.textContent = "OTP đã được gửi cho người duyệt.";
                startResendCountdown(
                    resendOpenOtpButton,
                    read(result.data, "resendAvailableInSeconds", "ResendAvailableInSeconds"),
                    () => Boolean(otpChallengePublicId) && !verifiedOtpChallengePublicId);
            } catch (error) {
                resetResendButton(resendOpenOtpButton);
                await notify(error.message, false);
            } finally {
                requestOpenOtpButton.disabled = false;
            }
        });

        document.getElementById("verifyOpenPosOtp")?.addEventListener("click", async () => {
            if (!otpChallengePublicId) return notify("Vui lòng gửi OTP trước.", false);
            try {
                await post(root.dataset.verifyOtpUrl, { OtpChallengePublicId: otpChallengePublicId, OtpCode: otpInput.value.trim().toUpperCase() });
                verifiedOtpChallengePublicId = otpChallengePublicId;
                otpStatus.textContent = "OTP đã được phê duyệt.";
                resetResendButton(resendOpenOtpButton);
            } catch (error) { await notify(error.message, false); }
        });

        resendOpenOtpButton?.addEventListener("click", async () => {
            if (!otpChallengePublicId) return;
            resendOpenOtpButton.disabled = true;
            try {
                const result = await post(root.dataset.resendOtpUrl, { OtpChallengePublicId: otpChallengePublicId });
                otpStatus.textContent = "OTP mới đã được gửi.";
                startResendCountdown(
                    resendOpenOtpButton,
                    read(result.data, "resendAvailableInSeconds", "ResendAvailableInSeconds"),
                    () => Boolean(otpChallengePublicId) && !verifiedOtpChallengePublicId);
            } catch (error) {
                const retryAfter = read(error.data, "resendAvailableInSeconds", "ResendAvailableInSeconds");
                if (retryAfter !== undefined) {
                    startResendCountdown(resendOpenOtpButton, retryAfter, () => Boolean(otpChallengePublicId));
                } else {
                    resendOpenOtpButton.disabled = !otpChallengePublicId;
                }
                await notify(error.message, false);
            }
        });

        document.getElementById("registerTerminalButton")?.addEventListener("click", () => {
            registrationTerminalId = crypto.randomUUID();
            registrationRequestKey = crypto.randomUUID();
            resetTerminalOtpState();
            showDialog(registrationDialog);
        });
        document.getElementById("cancelTerminalRegistration")?.addEventListener("click", () => {
            resetTerminalOtpState();
            closeDialog(registrationDialog);
        });
        requestTerminalOtpButton?.addEventListener("click", async () => {
            const name = document.getElementById("terminalRegistrationName").value.trim();
            if (!name) return notify("Vui lòng nhập tên terminal.", false);
            requestTerminalOtpButton.disabled = true;
            try {
                const result = await post(root.dataset.requestTerminalOtpUrl, {
                    TerminalId: registrationTerminalId, TerminalName: name, RequestKey: registrationRequestKey
                });
                registrationChallengeId = read(result.data, "otpChallengePublicId", "OtpChallengePublicId");
                document.getElementById("terminalRegistrationStatus").textContent = "OTP đã được gửi.";
                startResendCountdown(
                    resendTerminalOtpButton,
                    read(result.data, "resendAvailableInSeconds", "ResendAvailableInSeconds"),
                    () => Boolean(registrationChallengeId));
            } catch (error) {
                resetResendButton(resendTerminalOtpButton);
                await notify(error.message, false);
            } finally {
                requestTerminalOtpButton.disabled = false;
            }
        });
        resendTerminalOtpButton?.addEventListener("click", async () => {
            if (!registrationChallengeId) return notify("Vui lòng gửi OTP trước.", false);
            resendTerminalOtpButton.disabled = true;
            try {
                const result = await post(root.dataset.resendOtpUrl, { OtpChallengePublicId: registrationChallengeId });
                document.getElementById("terminalRegistrationStatus").textContent = "OTP mới đã được gửi.";
                startResendCountdown(
                    resendTerminalOtpButton,
                    read(result.data, "resendAvailableInSeconds", "ResendAvailableInSeconds"),
                    () => Boolean(registrationChallengeId));
            } catch (error) {
                const retryAfter = read(error.data, "resendAvailableInSeconds", "ResendAvailableInSeconds");
                if (retryAfter !== undefined) {
                    startResendCountdown(resendTerminalOtpButton, retryAfter, () => Boolean(registrationChallengeId));
                } else {
                    resendTerminalOtpButton.disabled = !registrationChallengeId;
                }
                await notify(error.message, false);
            }
        });
        document.getElementById("verifyAndRegisterTerminal")?.addEventListener("click", async () => {
            if (!registrationChallengeId) return notify("Vui lòng gửi OTP trước.", false);
            const name = document.getElementById("terminalRegistrationName").value.trim();
            const code = document.getElementById("terminalRegistrationOtp").value.trim().toUpperCase();
            try {
                await post(root.dataset.verifyOtpUrl, { OtpChallengePublicId: registrationChallengeId, OtpCode: code });
                await post(root.dataset.registerTerminalUrl, {
                    TerminalId: registrationTerminalId, TerminalName: name,
                    RequestKey: registrationRequestKey, OtpChallengePublicId: registrationChallengeId
                });
                resetTerminalOtpState();
                await notify("Đăng ký terminal thành công. Trang sẽ được tải lại.", true);
                window.location.reload();
            } catch (error) { await notify(error.message, false); }
        });

        document.getElementById("openOperatorPinDialog")?.addEventListener("click", () => {
            if (operatorPinStatus) operatorPinStatus.textContent = "";
            showDialog(operatorPinDialog);
            operatorCurrentPassword?.focus();
        });
        document.getElementById("cancelOperatorPin")?.addEventListener("click", () => closeDialog(operatorPinDialog));
        operatorPinForm?.addEventListener("submit", async (event) => {
            event.preventDefault();
            const currentPassword = operatorCurrentPassword?.value || "";
            const pin = operatorNewPin?.value.trim() || "";
            if (!currentPassword) {
                if (operatorPinStatus) operatorPinStatus.textContent = "Vui lòng nhập mật khẩu hiện tại.";
                operatorCurrentPassword?.focus();
                return;
            }
            if (!/^\d{6}$/.test(pin) || /^(\d)\1{5}$/.test(pin)) {
                if (operatorPinStatus) operatorPinStatus.textContent = "PIN phải gồm đúng 6 chữ số và không được lặp một chữ số.";
                operatorNewPin?.focus();
                return;
            }

            if (saveOperatorPinButton) {
                saveOperatorPinButton.disabled = true;
                saveOperatorPinButton.textContent = "Đang lưu...";
            }
            operatorPinForm.setAttribute("aria-busy", "true");
            try {
                const result = await post(root.dataset.setOperatorPinUrl, { CurrentPassword: currentPassword, Pin: pin });
                if (operatorPinStatus) operatorPinStatus.textContent = "PIN thao tác POS đã được thiết lập.";
                if (operatorCurrentPassword) operatorCurrentPassword.value = "";
                if (operatorNewPin) operatorNewPin.value = "";
                await notify(result.message || "Thiết lập PIN thao tác POS thành công.", true);
                closeDialog(operatorPinDialog);
            } catch (error) {
                if (operatorPinStatus) operatorPinStatus.textContent = error.message;
                await notify(error.message || "Không thể thiết lập PIN thao tác POS.", false);
            } finally {
                if (operatorCurrentPassword) operatorCurrentPassword.value = "";
                if (operatorNewPin) operatorNewPin.value = "";
                operatorPinForm.removeAttribute("aria-busy");
                if (saveOperatorPinButton) {
                    saveOperatorPinButton.disabled = false;
                    saveOperatorPinButton.textContent = "Lưu PIN";
                }
            }
        });
        previewDialog?.addEventListener("hidden.bs.modal", resetOpenOtpState);
        registrationDialog?.addEventListener("hidden.bs.modal", resetTerminalOtpState);
        operatorPinDialog?.addEventListener("hidden.bs.modal", () => {
            if (operatorCurrentPassword) operatorCurrentPassword.value = "";
            if (operatorNewPin) operatorNewPin.value = "";
            if (operatorPinStatus) operatorPinStatus.textContent = "";
        });
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", initialize, { once: true });
    else initialize();
})(window, document);
