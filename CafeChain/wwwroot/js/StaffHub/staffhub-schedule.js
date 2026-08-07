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
        const otpVerification = document.getElementById("openPosOtpVerification");
        const otpVerified = document.getElementById("openPosOtpVerified");
        const verifyOpenOtpButton = document.getElementById("verifyOpenPosOtp");
        const registrationDialog = document.getElementById("terminalRegistrationDialog");
        const requestOpenOtpButton = document.getElementById("requestOpenPosOtp");
        const resendOpenOtpButton = document.getElementById("resendOpenPosOtp");
        const requestTerminalOtpButton = document.getElementById("requestTerminalOtp");
        const resendTerminalOtpButton = document.getElementById("resendTerminalOtp");
        const managerApprovalFields = document.getElementById("openPosManagerApprovalFields");
        const requestLateApprovalButton = document.getElementById("requestLateOpenApproval");
        const lateApprovalStatus = document.getElementById("lateOpenApprovalStatus");
        const operatorPinDialog = document.getElementById("operatorPinDialog");
        const operatorPinForm = document.getElementById("operatorPinForm");
        const operatorCurrentPassword = document.getElementById("operatorCurrentPassword");
        const operatorNewPin = document.getElementById("operatorNewPin");
        const operatorPinStatus = document.getElementById("operatorPinStatus");
        const saveOperatorPinButton = document.getElementById("saveOperatorPin");

        // Bootstrap appends its backdrop directly to <body>. Keep the modal in the
        // same root stacking context so a transformed/isolated page wrapper can
        // never place the backdrop above the dialog.
        [previewDialog, registrationDialog, operatorPinDialog].forEach(dialog => {
            if (dialog && dialog.parentElement !== document.body) document.body.appendChild(dialog);
        });

        let requestKey = crypto.randomUUID();
        let assessment = null;
        let otpChallengePublicId = null;
        let verifiedOtpChallengePublicId = null;
        let openOtpState = "IDLE";
        let openOtpBusy = false;
        let registrationTerminalId = null;
        let registrationRequestKey = null;
        let registrationChallengeId = null;
        let registrationOtpState = "IDLE";
        let lateOpenApprovalPublicId = null;
        let lateOpenApprovalState = "IDLE";
        let lateApprovalPollTimer = 0;
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

        function notifyOtp(message, success) {
            if (window.Swal) {
                return Swal.fire({
                    icon: success ? "success" : "error",
                    title: success ? "Thành công" : "Không thành công",
                    text: message,
                    confirmButtonText: "Đóng"
                });
            }
            return notify(message, success);
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
            openOtpState = "IDLE";
            if (otpInput) otpInput.value = "";
            if (otpStatus) otpStatus.textContent = "";
            resetResendButton(resendOpenOtpButton);
            renderOpenOtpState();
        }

        function renderOpenOtpState(data) {
            const state = String(openOtpState || "IDLE").toUpperCase();
            const isApproved = state === "APPROVED" || state === "VERIFIED";
            const canEnterCode = ["SENT", "PENDING", "INVALID", "VERIFYING"].includes(state);
            const isTerminalState = ["EXPIRED", "LOCKED"].includes(state);
            const approvalRequired = Boolean(read(assessment, "approvalRequired", "ApprovalRequired"));

            if (requestOpenOtpButton) {
                requestOpenOtpButton.hidden = !["IDLE", "SENDING"].includes(state);
                requestOpenOtpButton.disabled = state === "SENDING" || openOtpBusy;
                requestOpenOtpButton.textContent = state === "SENDING" ? "Đang gửi OTP..." : "Gửi OTP";
            }
            if (otpVerification) otpVerification.hidden = !(canEnterCode || isTerminalState);
            if (otpVerified) otpVerified.hidden = !isApproved;
            if (otpInput) otpInput.disabled = !canEnterCode || state === "VERIFYING";
            if (verifyOpenOtpButton) verifyOpenOtpButton.disabled = !canEnterCode || state === "VERIFYING" || openOtpBusy;
            if (continueButton) continueButton.disabled = (approvalRequired && !isApproved)
                || (Boolean(read(assessment, "managerApprovalRequired", "ManagerApprovalRequired"))
                    && !["APPROVED", "CONVERTED_TO_OUTSIDE_SCHEDULE"].includes(lateOpenApprovalState));

            const contextLocked = Boolean(otpChallengePublicId) && !["EXPIRED", "LOCKED", "IDLE"].includes(state);
            if (terminalSelect) terminalSelect.disabled = contextLocked;
            if (reasonInput) reasonInput.readOnly = contextLocked;

            if (data) {
                const remaining = read(data, "remainingAttempts", "RemainingAttempts");
                if (state === "PENDING" || state === "SENT") {
                    otpStatus.textContent = `OTP đã được gửi. Còn ${remaining ?? "—"} lần thử.`;
                } else if (state === "EXPIRED") {
                    otpStatus.textContent = "OTP đã hết hạn. Đóng hộp thoại và tạo yêu cầu mới.";
                } else if (state === "LOCKED") {
                    otpStatus.textContent = "OTP đã bị khóa do nhập sai quá số lần cho phép.";
                }
            }
        }

        function applyOpenOtpResponse(data) {
            if (!data || read(data, "hasActiveChallenge", "HasActiveChallenge") === false) {
                resetOpenOtpState();
                return;
            }
            otpChallengePublicId = read(data, "otpChallengePublicId", "OtpChallengePublicId") || null;
            const status = String(read(data, "status", "Status") || "PENDING").toUpperCase();
            openOtpState = status === "PENDING" ? "SENT" : status;
            verifiedOtpChallengePublicId = status === "APPROVED" ? otpChallengePublicId : null;
            const restoredTerminal = read(data, "terminalId", "TerminalId");
            const restoredReason = read(data, "reason", "Reason");
            const restoredRequestKey = read(data, "requestKey", "RequestKey");
            if (restoredTerminal && terminalSelect) terminalSelect.value = restoredTerminal;
            if (restoredReason && reasonInput) reasonInput.value = restoredReason;
            if (restoredRequestKey) requestKey = restoredRequestKey;
            if (otpInput) otpInput.value = "";
            renderOpenOtpState(data);
            startResendCountdown(
                resendOpenOtpButton,
                read(data, "resendAvailableInSeconds", "ResendAvailableInSeconds"),
                () => Boolean(otpChallengePublicId) && !verifiedOtpChallengePublicId
                    && ["SENT", "PENDING", "INVALID"].includes(openOtpState));
        }

        async function restoreOpenOtpState() {
            try {
                const result = await post(root.dataset.openOtpStateUrl, {});
                applyOpenOtpResponse(result.data);
            } catch (error) {
                resetOpenOtpState();
                otpStatus.textContent = error.message;
            }
        }

        function resetTerminalOtpState() {
            registrationChallengeId = null;
            const status = document.getElementById("terminalRegistrationStatus");
            if (status) status.textContent = "";
            registrationOtpState = "IDLE";
            if (requestTerminalOtpButton) {
                requestTerminalOtpButton.hidden = false;
                requestTerminalOtpButton.disabled = false;
                requestTerminalOtpButton.textContent = "Gửi OTP";
            }
            if (resendTerminalOtpButton) resendTerminalOtpButton.hidden = true;
            resetResendButton(resendTerminalOtpButton);
        }

        function applyTerminalOtpResponse(data) {
            if (!data || read(data, "hasActiveChallenge", "HasActiveChallenge") === false) {
                resetTerminalOtpState();
                registrationTerminalId ||= crypto.randomUUID();
                registrationRequestKey ||= crypto.randomUUID();
                return;
            }
            registrationChallengeId = read(data, "otpChallengePublicId", "OtpChallengePublicId") || null;
            registrationOtpState = String(read(data, "status", "Status") || "PENDING").toUpperCase();
            registrationTerminalId = read(data, "terminalId", "TerminalId") || registrationTerminalId;
            registrationRequestKey = read(data, "requestKey", "RequestKey") || registrationRequestKey;
            const restoredName = read(data, "terminalName", "TerminalName") || read(data, "reason", "Reason");
            const nameInput = document.getElementById("terminalRegistrationName");
            const status = document.getElementById("terminalRegistrationStatus");
            if (restoredName && nameInput) nameInput.value = restoredName;
            const waiting = ["PENDING", "APPROVED"].includes(registrationOtpState)
                && Number(read(data, "expiresInSeconds", "ExpiresInSeconds") || 0) > 0;
            if (nameInput) nameInput.readOnly = waiting;
            if (requestTerminalOtpButton) {
                requestTerminalOtpButton.hidden = waiting || registrationOtpState === "USED";
                requestTerminalOtpButton.disabled = false;
                requestTerminalOtpButton.textContent = registrationOtpState === "EXPIRED"
                    ? "Gửi yêu cầu mới"
                    : "Gửi OTP";
            }
            if (resendTerminalOtpButton) resendTerminalOtpButton.hidden = !waiting;
            if (waiting) {
                if (status) status.textContent = "✓ OTP đã được gửi. Đang chờ Manager xác nhận Terminal.";
                startResendCountdown(
                    resendTerminalOtpButton,
                    read(data, "resendAvailableInSeconds", "ResendAvailableInSeconds"),
                    () => Boolean(registrationChallengeId));
            } else if (registrationOtpState === "USED") {
                if (status) status.textContent = "✓ Terminal đã được Manager xác nhận. Tải lại trang để sử dụng Terminal.";
                if (resendTerminalOtpButton) resendTerminalOtpButton.hidden = true;
            } else if (registrationOtpState === "EXPIRED") {
                if (status) status.textContent = "OTP đã hết hạn. Vui lòng gửi yêu cầu mới.";
                if (resendTerminalOtpButton) resendTerminalOtpButton.hidden = true;
            } else if (["CANCELLED", "LOCKED"].includes(registrationOtpState)) {
                if (status) status.textContent = "Yêu cầu OTP đã bị hủy. Vui lòng gửi yêu cầu mới.";
                if (resendTerminalOtpButton) resendTerminalOtpButton.hidden = true;
            }
        }

        async function restoreTerminalOtpState() {
            try {
                const result = await post(root.dataset.terminalOtpStateUrl, {});
                applyTerminalOtpResponse(result.data);
            } catch (error) {
                document.getElementById("terminalRegistrationStatus").textContent = error.message;
            }
        }

        function contextText(context, minutesEarly) {
            if (context === "WITHIN_SCHEDULE" && minutesEarly > 0) return "Mở POS sớm";
            if (context === "WITHIN_SCHEDULE") return "Được mở POS bình thường";
            if (context === "LATE_FOR_SCHEDULE") return "Mở POS trễ";
            return "Mở POS ngoài lịch";
        }

        function prepareTerminalSelection() {
            assessment = null;
            continueButton.hidden = true;
            resumeButton.hidden = true;
            approvalFields.hidden = true;
            if (managerApprovalFields) managerApprovalFields.hidden = true;
            document.getElementById("openPosPreviewTitle").textContent = "Chọn terminal POS";
            document.getElementById("openPosContextLabel").textContent = "Chưa xác định";
            document.getElementById("openPosPreviewNotice").textContent =
                "Chọn terminal của két bạn sẽ chịu trách nhiệm. Mỗi terminal chỉ có một ca đang hoạt động.";
        }

        function renderBlocking(data, errorCode, message) {
            const blocking = read(data, "blockingWorkShift", "BlockingWorkShift");
            const recommendedAction = read(data, "recommendedAction", "RecommendedAction")
                || read(blocking, "recommendedAction", "RecommendedAction");
            const isOwnedByRequester = Boolean(read(blocking, "isOwnedByRequester", "IsOwnedByRequester"));
            const responsibleName = read(blocking, "responsibleStaffName", "ResponsibleStaffName") || "nhân viên đang chịu trách nhiệm";
            assessment = data || {};
            let title = message;
            let notice = message;
            if (recommendedAction === "SWITCH_CURRENT_OPERATOR" || (blocking && !isOwnedByRequester && errorCode === "TERMINAL_ALREADY_HAS_OPEN_SHIFT")) {
                title = "Terminal đang có ca của nhân viên khác";
                notice = `${responsibleName} đang chịu trách nhiệm két này. Không mở ca mới và không chiếm terminal. `
                    + "Hãy dùng POS hiện tại tại terminal, chọn Đổi Current Operator và nhập PIN cá nhân của bạn.";
            } else if (blocking && isOwnedByRequester && String(read(blocking, "status", "Status")).toUpperCase() === "OPEN") {
                title = "Bạn đang có một phiên POS hoạt động";
                notice = "Tiếp tục đúng phiên hiện tại; không mở ca mới và không nhập lại tiền đầu ca.";
            } else if (blocking && ["CLOSING", "EXPIRED_PENDING_CLOSE"].includes(String(read(blocking, "status", "Status")).toUpperCase())) {
                title = "Phiên POS phải được chốt két";
                notice = isOwnedByRequester
                    ? "Phiên này không được bán hoặc mở mới. Hãy hoàn tất kiểm đếm và chốt két."
                    : `${responsibleName} đang chịu trách nhiệm phiên này. Terminal bị khóa cho đến khi ca được kiểm đếm và chốt két.`;
            }
            document.getElementById("openPosPreviewTitle").textContent = title;
            document.getElementById("openPosContextLabel").textContent = title;
            document.getElementById("openPosPreviewNotice").textContent = notice;
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
                resumeButton.hidden = !isOwnedByRequester;
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
            const managerApprovalRequired = Boolean(read(value, "managerApprovalRequired", "ManagerApprovalRequired"));
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
            if (managerApprovalRequired) notes.push("Ca làm đã quá hạn hơn 30 phút. Vui lòng liên hệ Quản lý để xác nhận.");
            document.getElementById("openPosPreviewNotice").textContent = notes.join(" ") || "Thông tin mở POS đã được backend xác nhận.";
            approvalFields.hidden = !reasonRequired;
            otpFields.hidden = !approvalRequired;
            if (managerApprovalFields) managerApprovalFields.hidden = !managerApprovalRequired;
            continueButton.hidden = false;
            continueButton.disabled = managerApprovalRequired;
            resumeButton.hidden = true;
            resetOpenOtpState();
            lateOpenApprovalPublicId = null;
            lateOpenApprovalState = managerApprovalRequired ? "IDLE" : "NOT_REQUIRED";
            if (lateApprovalStatus) lateApprovalStatus.textContent = "";
            if (requestLateApprovalButton) {
                requestLateApprovalButton.hidden = !managerApprovalRequired;
                requestLateApprovalButton.disabled = false;
                requestLateApprovalButton.textContent = "Gửi yêu cầu Manager";
            }
            showDialog(previewDialog);
            if (approvalRequired) void restoreOpenOtpState();
        }

        async function previewOpen() {
            const terminalId = terminalSelect?.value;
            if (!terminalId) {
                prepareTerminalSelection();
                showDialog(previewDialog);
                return;
            }
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
            const managerApprovalRequired = Boolean(read(assessment, "managerApprovalRequired", "ManagerApprovalRequired"));
            const reason = reasonInput?.value.trim() || "";
            if (reasonRequired && (reason.length < 10 || reason.length > 500))
                return notify("Lý do phải có từ 10 đến 500 ký tự.", false);
            if (approvalRequired && !verifiedOtpChallengePublicId)
                return notify("Vui lòng xác nhận OTP trước khi sang POS.", false);
            if (managerApprovalRequired && !["APPROVED", "CONVERTED_TO_OUTSIDE_SCHEDULE"].includes(lateOpenApprovalState))
                return notify("Yêu cầu mở ca trễ chưa được Manager duyệt.", false);
            continueButton.disabled = true;
            try {
                const result = await post(root.dataset.issuePosUrl, {
                    TerminalId: terminalSelect.value, RequestKey: requestKey, Reason: reason,
                    OtpChallengePublicId: verifiedOtpChallengePublicId,
                    LateOpenApprovalPublicId: lateOpenApprovalPublicId
                });
                await redirectWithTicket(result);
            } catch (error) {
                continueButton.disabled = false;
                await notify(error.message, false);
            }
        }

        function applyLateOpenApproval(data) {
            if (!data) return;
            lateOpenApprovalPublicId = read(data, "publicId", "PublicId") || lateOpenApprovalPublicId;
            lateOpenApprovalState = String(read(data, "status", "Status") || "PENDING").toUpperCase();
            if (lateApprovalStatus) {
                lateApprovalStatus.textContent = lateOpenApprovalState === "PENDING"
                    ? "Đang chờ Manager duyệt..."
                    : lateOpenApprovalState === "APPROVED"
                        ? "✓ Manager đã duyệt mở ca theo lịch."
                        : lateOpenApprovalState === "CONVERTED_TO_OUTSIDE_SCHEDULE"
                            ? "✓ Manager đã chuyển sang ca ngoài lịch."
                            : lateOpenApprovalState === "REJECTED"
                                ? `Manager đã từ chối. ${read(data, "decisionReason", "DecisionReason") || ""}`
                                : "Yêu cầu duyệt đã hết hiệu lực.";
            }
            const accepted = ["APPROVED", "CONVERTED_TO_OUTSIDE_SCHEDULE"].includes(lateOpenApprovalState);
            if (continueButton) continueButton.disabled = !accepted;
            if (requestLateApprovalButton) requestLateApprovalButton.hidden = ["PENDING", "APPROVED", "CONVERTED_TO_OUTSIDE_SCHEDULE"].includes(lateOpenApprovalState);
            if (lateApprovalPollTimer && lateOpenApprovalState !== "PENDING") {
                window.clearInterval(lateApprovalPollTimer);
                lateApprovalPollTimer = 0;
            }
        }

        async function refreshLateOpenApproval() {
            if (!lateOpenApprovalPublicId) return;
            try {
                const result = await post(root.dataset.lateOpenApprovalStateUrl, { id: lateOpenApprovalPublicId });
                applyLateOpenApproval(result.data);
            } catch (error) {
                if (lateApprovalStatus) lateApprovalStatus.textContent = error.message;
            }
        }

        requestLateApprovalButton?.addEventListener("click", async () => {
            const reason = reasonInput?.value.trim() || "";
            if (reason.length < 10 || reason.length > 500)
                return notify("Lý do phải có từ 10 đến 500 ký tự.", false);
            requestLateApprovalButton.disabled = true;
            requestLateApprovalButton.textContent = "Đang gửi...";
            try {
                const result = await post(root.dataset.requestLateOpenApprovalUrl, {
                    TerminalId: terminalSelect?.value || "",
                    Reason: reason,
                    RequestKey: requestKey
                });
                applyLateOpenApproval(result.data);
                if (lateOpenApprovalState === "PENDING" && !lateApprovalPollTimer) {
                    lateApprovalPollTimer = window.setInterval(() => void refreshLateOpenApproval(), 5000);
                }
            } catch (error) {
                requestLateApprovalButton.disabled = false;
                requestLateApprovalButton.textContent = "Gửi yêu cầu Manager";
                await notify(error.message, false);
            }
        });

        async function openPosFlow() {
            showDialog(previewDialog);
            await previewOpen();
        }

        openButton?.addEventListener("click", () =>
            AdminMutationGuard.run("staffhub-open-pos", openButton, openPosFlow));
        terminalSelect?.addEventListener("change", () => {
            requestKey = crypto.randomUUID();
            resetOpenOtpState();
            if (!terminalSelect.value) {
                prepareTerminalSelection();
                return;
            }
            void AdminMutationGuard.run("staffhub-preview-terminal", terminalSelect, previewOpen);
        });
        document.getElementById("cancelOpenPosPreview")?.addEventListener("click", () => closeDialog(previewDialog));
        continueButton?.addEventListener("click", issueOpenTicket);
        resumeButton?.addEventListener("click", async () => {
            resumeButton.disabled = true;
            try {
                await redirectWithTicket(await post(root.dataset.resumePosUrl, {
                    TerminalId: terminalSelect?.value || ""
                }));
            }
            catch (error) { resumeButton.disabled = false; await notify(error.message, false); }
        });

        requestOpenOtpButton?.addEventListener("click", async () => {
            if (openOtpBusy) return;
            const reason = reasonInput.value.trim();
            if (reason.length < 10 || reason.length > 500) return notify("Lý do phải có từ 10 đến 500 ký tự.", false);
            openOtpBusy = true;
            openOtpState = "SENDING";
            renderOpenOtpState();
            try {
                const result = await post(root.dataset.requestOpenOtpUrl, {
                    TerminalId: terminalSelect.value, RequestKey: requestKey, Reason: reason
                });
                applyOpenOtpResponse(result.data);
                await notifyOtp(result.message || "OTP đã được gửi cho người duyệt.", true);
            } catch (error) {
                openOtpState = "IDLE";
                resetResendButton(resendOpenOtpButton);
                await notifyOtp(error.message, false);
            } finally {
                openOtpBusy = false;
                renderOpenOtpState();
            }
        });

        verifyOpenOtpButton?.addEventListener("click", async () => {
            if (openOtpBusy) return;
            if (!otpChallengePublicId) return notify("Vui lòng gửi OTP trước.", false);
            openOtpBusy = true;
            openOtpState = "VERIFYING";
            renderOpenOtpState();
            try {
                const result = await post(root.dataset.verifyOtpUrl, { OtpChallengePublicId: otpChallengePublicId, OtpCode: otpInput.value.trim().toUpperCase() });
                verifiedOtpChallengePublicId = otpChallengePublicId;
                openOtpState = "APPROVED";
                otpInput.value = "";
                otpStatus.textContent = "OTP đã được phê duyệt.";
                resetResendButton(resendOpenOtpButton);
                renderOpenOtpState(result.data);
                await notifyOtp(result.message || "Xác nhận OTP thành công.", true);
            } catch (error) {
                const errorCode = String(error.errorCode || "").toUpperCase();
                if (errorCode === "OTP_EXPIRED") openOtpState = "EXPIRED";
                else if (errorCode === "OTP_VERIFICATION_LOCKED") openOtpState = "LOCKED";
                else if (errorCode === "OTP_ALREADY_USED") openOtpState = "APPROVED";
                else openOtpState = "INVALID";
                renderOpenOtpState(error.data);
                if (openOtpState === "INVALID") {
                    otpInput?.focus();
                    otpInput?.select();
                }
                await notifyOtp(error.message, false);
            } finally {
                openOtpBusy = false;
                renderOpenOtpState();
            }
        });

        resendOpenOtpButton?.addEventListener("click", async () => {
            if (!otpChallengePublicId) return;
            resendOpenOtpButton.disabled = true;
            try {
                const result = await post(root.dataset.resendOtpUrl, { OtpChallengePublicId: otpChallengePublicId });
                openOtpState = "SENT";
                applyOpenOtpResponse(result.data);
                otpStatus.textContent = "OTP mới đã được gửi.";
                await notifyOtp(result.message || "OTP mới đã được gửi.", true);
            } catch (error) {
                if (error.errorCode === "OTP_EXPIRED") openOtpState = "EXPIRED";
                if (error.errorCode === "OTP_VERIFICATION_LOCKED") openOtpState = "LOCKED";
                const retryAfter = read(error.data, "resendAvailableInSeconds", "ResendAvailableInSeconds");
                if (retryAfter !== undefined) {
                    startResendCountdown(resendOpenOtpButton, retryAfter, () => Boolean(otpChallengePublicId));
                } else {
                    resendOpenOtpButton.disabled = !otpChallengePublicId;
                }
                renderOpenOtpState(error.data);
                await notifyOtp(error.message, false);
            }
        });

        document.getElementById("registerTerminalButton")?.addEventListener("click", () => {
            showDialog(registrationDialog);
            void restoreTerminalOtpState();
        });
        document.getElementById("cancelTerminalRegistration")?.addEventListener("click", () => {
            closeDialog(registrationDialog);
        });
        requestTerminalOtpButton?.addEventListener("click", async () => {
            const name = document.getElementById("terminalRegistrationName").value.trim();
            if (!name) return notify("Vui lòng nhập tên terminal.", false);
            if (["EXPIRED", "CANCELLED", "LOCKED", "USED"].includes(registrationOtpState)) {
                registrationTerminalId = crypto.randomUUID();
                registrationRequestKey = crypto.randomUUID();
                registrationChallengeId = null;
            }
            registrationTerminalId ||= crypto.randomUUID();
            registrationRequestKey ||= crypto.randomUUID();
            requestTerminalOtpButton.disabled = true;
            requestTerminalOtpButton.textContent = "Đang gửi OTP...";
            try {
                const result = await post(root.dataset.requestTerminalOtpUrl, {
                    TerminalId: registrationTerminalId, TerminalName: name, RequestKey: registrationRequestKey
                });
                applyTerminalOtpResponse(result.data);
            } catch (error) {
                resetResendButton(resendTerminalOtpButton);
                await notify(error.message, false);
            } finally {
                if (!requestTerminalOtpButton.hidden) {
                    requestTerminalOtpButton.disabled = false;
                    requestTerminalOtpButton.textContent = "Gửi OTP";
                }
            }
        });
        resendTerminalOtpButton?.addEventListener("click", async () => {
            if (!registrationChallengeId) return notify("Vui lòng gửi OTP trước.", false);
            resendTerminalOtpButton.disabled = true;
            try {
                const result = await post(root.dataset.resendOtpUrl, { OtpChallengePublicId: registrationChallengeId });
                applyTerminalOtpResponse(result.data);
                document.getElementById("terminalRegistrationStatus").textContent = "✓ OTP mới đã được gửi. Đang chờ Manager xác nhận Terminal.";
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
            if (!/^\d{6}$/.test(pin) || /^(\d)\1{5}$/.test(pin) || ["123456", "654321"].includes(pin)) {
                if (operatorPinStatus) operatorPinStatus.textContent = "PIN phải gồm đúng 6 chữ số; không dùng chuỗi lặp, 123456 hoặc 654321.";
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
        previewDialog?.addEventListener("hidden.bs.modal", () => {
            if (otpInput) otpInput.value = "";
            if (lateApprovalPollTimer) {
                window.clearInterval(lateApprovalPollTimer);
                lateApprovalPollTimer = 0;
            }
        });
        registrationDialog?.addEventListener("hidden.bs.modal", () => {
            clearResendCountdown(resendTerminalOtpButton);
        });
        operatorPinDialog?.addEventListener("hidden.bs.modal", () => {
            if (operatorCurrentPassword) operatorCurrentPassword.value = "";
            if (operatorNewPin) operatorNewPin.value = "";
            if (operatorPinStatus) operatorPinStatus.textContent = "";
        });

        const launchOptions = document.getElementById("staffHubLaunchOptions");
        if (launchOptions?.dataset.autoOpenPos === "true") {
            const requestedTerminalId = launchOptions.dataset.requestedTerminalId || "";
            const posLaunchError = launchOptions.dataset.posLaunchError || "";
            if (requestedTerminalId && terminalSelect?.querySelector(`option[value="${CSS.escape(requestedTerminalId)}"]`)) {
                terminalSelect.value = requestedTerminalId;
            }
            showDialog(previewDialog);
            if (posLaunchError) void notifyOtp(posLaunchError, false);
            if (terminalSelect?.value) {
                void AdminMutationGuard.run("staffhub-auto-open-pos", terminalSelect, previewOpen);
            } else {
                prepareTerminalSelection();
            }
        }
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", initialize, { once: true });
    else initialize();
})(window, document);
