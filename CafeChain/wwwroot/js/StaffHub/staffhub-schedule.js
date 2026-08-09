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
        const cancelOpenButton = document.getElementById("cancelOpenPosPreview");
        const resumeButton = document.getElementById("resumeExistingPos");
        const approvalFields = document.getElementById("openPosApprovalFields");
        const otpFields = document.getElementById("openPosOtpFields");
        const reasonInput = document.getElementById("openPosReason");
        const reasonCounter = document.getElementById("openPosReasonCounter");
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
        const cancelTerminalRequestButton = document.getElementById("cancelTerminalRegistrationRequest");
        const registerTerminalButton = document.getElementById("registerTerminalButton");
        const terminalActionTitle = document.getElementById("terminalActionTitle");
        const terminalActionSubtitle = document.getElementById("terminalActionSubtitle");
        const terminalActionBadge = document.getElementById("terminalActionBadge");
        const terminalDeviceState = document.getElementById("terminalDeviceState");
        const terminalDeviceStateIcon = document.getElementById("terminalDeviceStateIcon");
        const terminalDeviceStateBadge = document.getElementById("terminalDeviceStateBadge");
        const terminalDeviceStateTitle = document.getElementById("terminalDeviceStateTitle");
        const terminalDeviceStateDescription = document.getElementById("terminalDeviceStateDescription");
        const terminalDeviceDetails = document.getElementById("terminalDeviceDetails");
        const terminalDeviceName = document.getElementById("terminalDeviceName");
        const terminalDeviceStore = document.getElementById("terminalDeviceStore");
        const terminalDeviceId = document.getElementById("terminalDeviceId");
        const terminalRegistrationFields = document.getElementById("terminalRegistrationFields");
        const terminalRegistrationName = document.getElementById("terminalRegistrationName");
        const terminalRegistrationStatus = document.getElementById("terminalRegistrationStatus");
        const terminalExistingLink = document.getElementById("terminalExistingLink");
        const terminalExistingSelect = document.getElementById("terminalExistingSelect");
        const linkExistingTerminalButton = document.getElementById("linkExistingTerminal");
        const managerApprovalFields = document.getElementById("openPosManagerApprovalFields");
        const requestLateApprovalButton = document.getElementById("requestLateOpenApproval");
        const lateApprovalStatus = document.getElementById("lateOpenApprovalStatus");
        const operatorPinDialog = document.getElementById("operatorPinDialog");
        const operatorPinForm = document.getElementById("operatorPinForm");
        const operatorCurrentPassword = document.getElementById("operatorCurrentPassword");
        const operatorNewPin = document.getElementById("operatorNewPin");
        const operatorPinStatus = document.getElementById("operatorPinStatus");
        const saveOperatorPinButton = document.getElementById("saveOperatorPin");
        const operatorPinStateBadge = document.getElementById("operatorPinStateBadge");
        const operatorPinStateText = document.getElementById("operatorPinStateText");
        const openOperatorPinButton = document.getElementById("openOperatorPinDialog");
        const operatorPinDialogTitle = document.getElementById("operatorPinDialogTitle");

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
        const terminalDeviceStorageKey = "cafechain.staffhub.pos-terminal-device.v1";
        const currentStoreId = Number.parseInt(root.dataset.storeId || "0", 10) || 0;
        const currentStoreName = root.dataset.storeName || "Cửa hàng hiện tại";
        let terminalDeviceIdentity = loadTerminalDeviceIdentity();
        let registrationTerminalId = terminalDeviceIdentity?.terminalId || null;
        let registrationRequestKey = null;
        let registrationChallengeId = null;
        let registrationOtpState = "IDLE";
        let terminalUiState = "UNLINKED";
        let lateOpenApprovalPublicId = null;
        let lateOpenApprovalState = "IDLE";
        let lateApprovalPollTimer = 0;
        let realtimeConnection = null;
        let workShiftRealtimeConnection = null;
        let allowOpenDialogClose = false;
        let terminalResolutionReloading = false;
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

        function setOperatorPinConfigured(configured) {
            root.dataset.operatorPinConfigured = configured ? "true" : "false";
            if (operatorPinStateBadge) {
                operatorPinStateBadge.classList.toggle("is-configured", configured);
                operatorPinStateBadge.classList.toggle("is-unconfigured", !configured);
                operatorPinStateBadge.innerHTML = configured
                    ? '<i class="bi bi-check-circle-fill" aria-hidden="true"></i><span>Đã thiết lập</span>'
                    : '<i class="bi bi-exclamation-circle-fill" aria-hidden="true"></i><span>Chưa thiết lập</span>';
            }
            if (operatorPinStateText) {
                operatorPinStateText.textContent = configured
                    ? "PIN đã sẵn sàng để xác thực khi đổi người thao tác POS."
                    : "Bạn cần thiết lập PIN trước khi có thể trở thành Current Operator.";
            }
            if (openOperatorPinButton) openOperatorPinButton.textContent = configured ? "Đổi PIN" : "Thiết lập PIN";
            if (operatorPinDialogTitle) operatorPinDialogTitle.textContent = configured
                ? "Đổi PIN thao tác POS"
                : "Thiết lập PIN thao tác POS";
        }

        function hasActiveOpenIntent() {
            const otpActive = Boolean(otpChallengePublicId)
                && ["SENT", "PENDING", "APPROVED", "VERIFIED"].includes(String(openOtpState).toUpperCase());
            const approvalActive = Boolean(lateOpenApprovalPublicId)
                && ["PENDING", "APPROVED", "CONVERTED_TO_OUTSIDE_SCHEDULE"].includes(String(lateOpenApprovalState).toUpperCase());
            return otpActive || approvalActive;
        }

        function renderCancelOpenButton() {
            if (cancelOpenButton) cancelOpenButton.textContent = hasActiveOpenIntent()
                ? "Hủy yêu cầu mở ca"
                : "Hủy";
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

        function normalizeTerminalId(value) {
            return String(value || "").trim().toLowerCase();
        }

        function sameTerminalId(left, right) {
            const normalizedLeft = normalizeTerminalId(left);
            return Boolean(normalizedLeft) && normalizedLeft === normalizeTerminalId(right);
        }

        function loadTerminalDeviceIdentity() {
            try {
                const raw = window.localStorage?.getItem(terminalDeviceStorageKey);
                if (!raw) return null;
                const parsed = JSON.parse(raw);
                const terminalId = String(parsed?.terminalId || "").trim();
                const storeId = Number.parseInt(String(parsed?.storeId || "0"), 10) || 0;
                if (parsed?.version !== 1 || !terminalId || storeId <= 0) return null;
                return {
                    version: 1,
                    terminalId,
                    storeId,
                    terminalName: String(parsed?.terminalName || "").trim(),
                    bindingSource: parsed?.bindingSource === "existing" ? "existing" : "registration",
                    createdAt: String(parsed?.createdAt || "")
                };
            } catch {
                return null;
            }
        }

        function saveTerminalDeviceIdentity(terminalId, terminalName, bindingSource) {
            const normalizedId = String(terminalId || "").trim();
            if (!normalizedId || currentStoreId <= 0) return null;
            const identity = {
                version: 1,
                terminalId: normalizedId,
                storeId: currentStoreId,
                terminalName: String(terminalName || "").trim(),
                bindingSource: bindingSource === "existing"
                    ? "existing"
                    : (terminalDeviceIdentity?.bindingSource || "registration"),
                createdAt: terminalDeviceIdentity?.createdAt || new Date().toISOString()
            };
            try {
                window.localStorage?.setItem(terminalDeviceStorageKey, JSON.stringify(identity));
            } catch {
                return null;
            }
            terminalDeviceIdentity = identity;
            registrationTerminalId = identity.terminalId;
            return identity;
        }

        function ensureTerminalDeviceIdentity(terminalName) {
            if (terminalDeviceIdentity?.storeId === currentStoreId && terminalDeviceIdentity.terminalId)
                return terminalDeviceIdentity;
            if (terminalDeviceIdentity && terminalDeviceIdentity.storeId !== currentStoreId)
                return null;
            return saveTerminalDeviceIdentity(crypto.randomUUID(), terminalName, "registration");
        }

        function getActiveTerminal(terminalId) {
            if (!terminalSelect || !terminalId) return null;
            const option = Array.from(terminalSelect.options).find(item =>
                item.value && sameTerminalId(item.value, terminalId));
            return option ? { terminalId: option.value, name: option.textContent?.trim() || option.value } : null;
        }

        function shortTerminalId(terminalId) {
            const value = String(terminalId || "").trim();
            if (value.length <= 18) return value || "—";
            return `${value.slice(0, 8)}…${value.slice(-6)}`;
        }

        function syncBoundTerminalPicker(state, terminalId) {
            if (!terminalSelect || !registerTerminalButton) return;
            const readyTerminal = state === "READY" ? getActiveTerminal(terminalId) : null;
            Array.from(terminalSelect.options).forEach(option => {
                if (!option.value) return;
                option.disabled = !readyTerminal || !sameTerminalId(option.value, readyTerminal.terminalId);
            });
            terminalSelect.disabled = true;
            terminalSelect.value = readyTerminal?.terminalId || "";
        }

        const terminalStatePresentation = {
            UNLINKED: {
                badge: "Chưa liên kết", title: "Thiết bị này chưa được đăng ký",
                description: "Đăng ký thiết bị mới hoặc liên kết với một Terminal active đã có.",
                actionTitle: "Đăng ký thiết bị này", actionSubtitle: "Chưa liên kết Terminal POS", icon: "bi-display"
            },
            READY: {
                badge: "Đã kích hoạt", title: "Thiết bị đã sẵn sàng",
                description: "Terminal đã được Backend xác nhận và đang active tại cửa hàng này.",
                actionTitle: "Thiết bị đã sẵn sàng", actionSubtitle: "Có thể mở POS", icon: "bi-check-circle"
            },
            PENDING: {
                badge: "Đang chờ", title: "Đang chờ xác nhận Terminal",
                description: "Người có quyền phê duyệt cần mở Thông báo và xác nhận bằng OTP.",
                actionTitle: "Đang chờ xác nhận", actionSubtitle: "Yêu cầu đã được gửi", icon: "bi-hourglass-split"
            },
            APPROVED: {
                badge: "Đang xử lý", title: "OTP đã được phê duyệt",
                description: "Hệ thống đang hoàn tất kích hoạt Terminal cho thiết bị này.",
                actionTitle: "Đang kích hoạt Terminal", actionSubtitle: "Vui lòng chờ trong giây lát", icon: "bi-arrow-repeat"
            },
            LOCKED: {
                badge: "Tạm khóa", title: "Yêu cầu OTP đang bị khóa",
                description: "Vui lòng chờ hết thời gian khóa hoặc hủy yêu cầu để thực hiện lại.",
                actionTitle: "Yêu cầu đang bị khóa", actionSubtitle: "Mở để xem thời gian chờ", icon: "bi-lock"
            },
            REJECTED: {
                badge: "Bị từ chối", title: "Đăng ký Terminal bị từ chối",
                description: "Bạn có thể kiểm tra lại tên Terminal và gửi lại yêu cầu cho cùng thiết bị.",
                actionTitle: "Đăng ký bị từ chối", actionSubtitle: "Có thể gửi lại yêu cầu", icon: "bi-x-circle"
            },
            EXPIRED: {
                badge: "Hết hạn", title: "Yêu cầu đăng ký đã hết hạn",
                description: "Mã thiết bị vẫn được giữ nguyên. Hãy gửi lại yêu cầu để nhận OTP mới.",
                actionTitle: "Yêu cầu đã hết hạn", actionSubtitle: "Gửi lại cho cùng thiết bị", icon: "bi-clock-history"
            },
            CANCELLED: {
                badge: "Đã hủy", title: "Yêu cầu đăng ký đã được hủy",
                description: "Bạn có thể gửi lại yêu cầu mà không tạo một Terminal ID khác.",
                actionTitle: "Yêu cầu đã hủy", actionSubtitle: "Có thể đăng ký lại", icon: "bi-slash-circle"
            },
            OTHER_PENDING: {
                badge: "Thiết bị khác", title: "Đang có yêu cầu trên thiết bị khác",
                description: "Mỗi nhân viên chỉ có một yêu cầu đăng ký Terminal đang chờ tại một thời điểm.",
                actionTitle: "Có yêu cầu ở thiết bị khác", actionSubtitle: "Hoàn tất hoặc hủy yêu cầu trước", icon: "bi-pc-display"
            },
            INVALID_BINDING: {
                badge: "Không khả dụng", title: "Liên kết Terminal không còn hợp lệ",
                description: "Terminal có thể đã bị vô hiệu hóa. Hãy liên kết một Terminal active hoặc liên hệ quản lý.",
                actionTitle: "Terminal không khả dụng", actionSubtitle: "Cần kiểm tra lại liên kết", icon: "bi-exclamation-triangle"
            },
            STORE_MISMATCH: {
                badge: "Sai cửa hàng", title: "Thiết bị đang thuộc cửa hàng khác",
                description: "Không thể đăng ký lại thiết bị này tại cửa hàng hiện tại. Vui lòng liên hệ quản lý.",
                actionTitle: "Thiết bị thuộc cửa hàng khác", actionSubtitle: "Không thể sử dụng tại cửa hàng này", icon: "bi-shop-window"
            }
        };

        function renderTerminalDeviceState(state, details) {
            const safeState = terminalStatePresentation[state] ? state : "UNLINKED";
            const presentation = terminalStatePresentation[safeState];
            const terminalId = details?.terminalId || terminalDeviceIdentity?.terminalId || registrationTerminalId;
            const activeTerminal = getActiveTerminal(terminalId);
            const terminalName = details?.terminalName || activeTerminal?.name || terminalDeviceIdentity?.terminalName || "";
            const waiting = ["PENDING", "APPROVED", "LOCKED", "OTHER_PENDING"].includes(safeState);
            const retryable = ["UNLINKED", "REJECTED", "EXPIRED", "CANCELLED"].includes(safeState);
            const canLinkExisting = ["UNLINKED", "INVALID_BINDING"].includes(safeState)
                && Boolean(terminalExistingLink);

            terminalUiState = safeState;
            if (registerTerminalButton) {
                registerTerminalButton.dataset.terminalState = safeState.toLowerCase();
                registerTerminalButton.setAttribute("aria-label", `${presentation.actionTitle}. ${presentation.actionSubtitle}`);
            }
            if (terminalActionTitle) terminalActionTitle.textContent = presentation.actionTitle;
            if (terminalActionSubtitle) terminalActionSubtitle.textContent = terminalName && safeState === "READY"
                ? `${terminalName} · ${currentStoreName}`
                : presentation.actionSubtitle;
            if (terminalActionBadge) {
                terminalActionBadge.className = `staffhub-terminal-action-badge is-${safeState.toLowerCase().replace("_", "-")}`;
                terminalActionBadge.textContent = presentation.badge;
            }
            const actionIcon = document.querySelector("#terminalActionIcon i");
            if (actionIcon) actionIcon.className = `bi ${presentation.icon}`;

            if (terminalDeviceState) terminalDeviceState.className =
                `staffhub-terminal-device-state is-${safeState.toLowerCase().replace("_", "-")}`;
            if (terminalDeviceStateBadge) terminalDeviceStateBadge.textContent = presentation.badge;
            if (terminalDeviceStateTitle) terminalDeviceStateTitle.textContent = presentation.title;
            if (terminalDeviceStateDescription) terminalDeviceStateDescription.textContent =
                details?.description || presentation.description;
            if (terminalDeviceStateIcon) terminalDeviceStateIcon.innerHTML =
                `<i class="bi ${presentation.icon}" aria-hidden="true"></i>`;

            if (terminalDeviceDetails) terminalDeviceDetails.hidden = !terminalId;
            if (terminalDeviceName) terminalDeviceName.textContent = terminalName || "Chưa đặt tên";
            if (terminalDeviceStore) terminalDeviceStore.textContent = currentStoreName;
            if (terminalDeviceId) {
                terminalDeviceId.textContent = shortTerminalId(terminalId);
                terminalDeviceId.title = terminalId || "";
            }

            if (terminalRegistrationFields) terminalRegistrationFields.hidden = !retryable;
            if (terminalRegistrationName) {
                if (terminalName && !terminalRegistrationName.value) terminalRegistrationName.value = terminalName;
                terminalRegistrationName.readOnly = waiting;
            }
            if (terminalExistingLink) terminalExistingLink.hidden = !canLinkExisting;
            if (requestTerminalOtpButton) {
                requestTerminalOtpButton.hidden = !retryable;
                requestTerminalOtpButton.disabled = false;
                requestTerminalOtpButton.textContent = safeState === "UNLINKED"
                    ? "Đăng ký thiết bị này"
                    : "Gửi lại yêu cầu";
            }
            if (resendTerminalOtpButton) resendTerminalOtpButton.hidden = !["PENDING", "APPROVED", "LOCKED"].includes(safeState);
            if (cancelTerminalRequestButton) cancelTerminalRequestButton.hidden = !waiting;
            if (terminalRegistrationStatus) terminalRegistrationStatus.textContent = details?.statusMessage || "";
            syncBoundTerminalPicker(safeState, terminalId);
        }

        function renderStoredTerminalDeviceState() {
            if (!registerTerminalButton) return;
            if (terminalDeviceIdentity && terminalDeviceIdentity.storeId !== currentStoreId) {
                renderTerminalDeviceState("STORE_MISMATCH", { terminalId: terminalDeviceIdentity.terminalId });
                return;
            }
            const activeTerminal = getActiveTerminal(terminalDeviceIdentity?.terminalId);
            if (activeTerminal) {
                renderTerminalDeviceState("READY", activeTerminal);
                return;
            }
            renderTerminalDeviceState(
                terminalDeviceIdentity?.bindingSource === "existing" ? "INVALID_BINDING" : "UNLINKED",
                terminalDeviceIdentity ? {
                    terminalId: terminalDeviceIdentity.terminalId,
                    terminalName: terminalDeviceIdentity.terminalName
                } : undefined);
        }

        function assessmentVersion() {
            return read(assessment, "assessmentVersion", "AssessmentVersion") || "";
        }

        function updateReasonCounter() {
            if (reasonCounter) reasonCounter.textContent = `${reasonInput?.value.length || 0} / 500`;
        }

        function setCodeValue(input, value) {
            if (window.VerificationCodeInput?.setValue) window.VerificationCodeInput.setValue(input, value);
            else if (input) input.value = value;
        }

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
            setCodeValue(otpInput, "");
            if (otpStatus) otpStatus.textContent = "";
            resetResendButton(resendOpenOtpButton);
            renderOpenOtpState();
            renderCancelOpenButton();
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
                    const retry = Number(read(data, "retryAfter", "RetryAfter") || 0);
                    otpStatus.textContent = retry > 0
                        ? `OTP đã bị khóa. Có thể gửi lại sau ${formatCountdown(retry)}.`
                        : "OTP đã bị khóa. Bạn có thể gửi lại mã mới.";
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
            updateReasonCounter();
            if (restoredRequestKey) requestKey = restoredRequestKey;
            setCodeValue(otpInput, "");
            renderOpenOtpState(data);
            renderCancelOpenButton();
            startResendCountdown(
                resendOpenOtpButton,
                read(data, "resendAvailableInSeconds", "ResendAvailableInSeconds"),
                () => Boolean(otpChallengePublicId) && !verifiedOtpChallengePublicId
                    && ["SENT", "PENDING", "INVALID", "LOCKED"].includes(openOtpState));
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
            registrationOtpState = "IDLE";
            registrationRequestKey = null;
            resetResendButton(resendTerminalOtpButton);
            renderStoredTerminalDeviceState();
        }

        async function notifyTerminalResolutionAndReload(statusValue) {
            const resolvedStatus = String(statusValue || "").toUpperCase();
            if (!["USED", "REJECTED"].includes(resolvedStatus) || terminalResolutionReloading)
                return false;

            const marker = `staffhub-terminal-resolution:${registrationChallengeId || "unknown"}:${resolvedStatus}`;
            try {
                if (window.localStorage?.getItem(marker) === "reloaded") return true;
            } catch {
                // Storage can be unavailable in strict privacy mode; the UI still resolves normally.
            }

            terminalResolutionReloading = true;
            let shouldReload = true;
            try {
                window.localStorage?.setItem(marker, "reloaded");
            } catch {
                shouldReload = false;
            }
            const approved = resolvedStatus === "USED";
            const title = approved ? "Terminal đã được xác nhận" : "Đăng ký Terminal bị từ chối";
            const message = approved
                ? "Terminal đã được kích hoạt. StaffHub sẽ tự tải lại để cập nhật danh sách thiết bị."
                : "Chủ doanh nghiệp hoặc Quản lý chi nhánh đã từ chối yêu cầu. StaffHub sẽ tự tải lại.";
            try {
                if (window.Swal) {
                    await window.Swal.fire({
                        icon: approved ? "success" : "warning",
                        title,
                        text: message,
                        timer: 1800,
                        timerProgressBar: true,
                        showConfirmButton: false,
                        allowOutsideClick: false
                    });
                } else {
                    window.alert(message);
                }
            } finally {
                if (shouldReload) window.location.reload();
                else {
                    terminalResolutionReloading = false;
                    await restoreTerminalOtpState();
                }
            }
            return true;
        }

        function applyTerminalOtpResponse(data) {
            if (!data || read(data, "hasActiveChallenge", "HasActiveChallenge") === false) {
                resetTerminalOtpState();
                return;
            }
            const challengeId = read(data, "otpChallengePublicId", "OtpChallengePublicId") || null;
            const challengeTerminalId = String(read(data, "terminalId", "TerminalId") || "").trim();
            const challengeName = read(data, "terminalName", "TerminalName") || read(data, "reason", "Reason") || "";
            const expiresInSeconds = Number(read(data, "expiresInSeconds", "ExpiresInSeconds") || 0);
            let challengeState = String(read(data, "status", "Status") || "PENDING").toUpperCase();
            if (challengeState === "PENDING" && expiresInSeconds <= 0) challengeState = "EXPIRED";
            const sameDevice = terminalDeviceIdentity?.storeId === currentStoreId
                && sameTerminalId(terminalDeviceIdentity.terminalId, challengeTerminalId);
            const activeElsewhere = ["PENDING", "APPROVED", "LOCKED"].includes(challengeState)
                && expiresInSeconds > 0;

            if (!sameDevice) {
                resetResendButton(resendTerminalOtpButton);
                if (activeElsewhere) {
                    registrationChallengeId = challengeId;
                    registrationOtpState = challengeState;
                    const currentActiveTerminal = getActiveTerminal(terminalDeviceIdentity?.terminalId);
                    if (currentActiveTerminal) {
                        renderTerminalDeviceState("READY", {
                            ...currentActiveTerminal,
                            description: `Thiết bị này vẫn sẵn sàng. Nhân viên đang có một yêu cầu khác cho Terminal “${challengeName || shortTerminalId(challengeTerminalId)}”.`,
                            statusMessage: "Yêu cầu trên thiết bị khác không thay đổi liên kết của thiết bị hiện tại."
                        });
                    } else {
                        renderTerminalDeviceState("OTHER_PENDING", {
                            terminalId: challengeTerminalId,
                            terminalName: challengeName,
                            description: `Yêu cầu đang chờ thuộc Terminal “${challengeName || shortTerminalId(challengeTerminalId)}”. Hãy hoàn tất hoặc hủy yêu cầu đó trước.`,
                            statusMessage: "Yêu cầu này không thuộc định danh của thiết bị đang sử dụng."
                        });
                    }
                } else {
                    registrationChallengeId = null;
                    registrationOtpState = "IDLE";
                    renderStoredTerminalDeviceState();
                }
                return;
            }

            registrationChallengeId = challengeId;
            registrationOtpState = challengeState;
            registrationTerminalId = terminalDeviceIdentity.terminalId;
            registrationRequestKey = read(data, "requestKey", "RequestKey") || registrationRequestKey;
            if (challengeName) {
                saveTerminalDeviceIdentity(registrationTerminalId, challengeName);
                if (terminalRegistrationName) terminalRegistrationName.value = challengeName;
            }

            if (["PENDING", "APPROVED", "LOCKED"].includes(challengeState) && expiresInSeconds > 0) {
                renderTerminalDeviceState(challengeState, {
                    terminalId: registrationTerminalId,
                    terminalName: challengeName,
                    statusMessage: challengeState === "LOCKED"
                        ? "OTP đang bị khóa. Theo dõi thời gian chờ trước khi gửi lại."
                        : "✓ Yêu cầu đã được gửi và đang chờ người có quyền xác nhận."
                });
                startResendCountdown(
                    resendTerminalOtpButton,
                    read(data, "resendAvailableInSeconds", "ResendAvailableInSeconds"),
                    () => Boolean(registrationChallengeId));
                return;
            }

            resetResendButton(resendTerminalOtpButton);
            if (challengeState === "USED") {
                const activeTerminal = getActiveTerminal(registrationTerminalId);
                renderTerminalDeviceState(activeTerminal ? "READY" : "APPROVED", {
                    terminalId: registrationTerminalId,
                    terminalName: challengeName,
                    statusMessage: "✓ Terminal đã được xác nhận. StaffHub đang cập nhật danh sách thiết bị."
                });
                void notifyTerminalResolutionAndReload(challengeState);
            } else if (challengeState === "REJECTED") {
                renderTerminalDeviceState("REJECTED", {
                    terminalId: registrationTerminalId,
                    terminalName: challengeName,
                    statusMessage: "Yêu cầu đã bị từ chối. Mã thiết bị được giữ nguyên để bạn có thể gửi lại."
                });
                void notifyTerminalResolutionAndReload(challengeState);
            } else if (["EXPIRED", "CANCELLED"].includes(challengeState)) {
                renderTerminalDeviceState(challengeState, {
                    terminalId: registrationTerminalId,
                    terminalName: challengeName
                });
            } else {
                renderStoredTerminalDeviceState();
            }
        }

        async function restoreTerminalOtpState() {
            try {
                const result = await post(root.dataset.terminalOtpStateUrl, {});
                applyTerminalOtpResponse(result.data);
            } catch (error) {
                renderStoredTerminalDeviceState();
                if (terminalRegistrationStatus) terminalRegistrationStatus.textContent = error.message;
            }
        }

        function initializeRealtime() {
            if (!window.signalR?.HubConnectionBuilder) return;
            realtimeConnection = new signalR.HubConnectionBuilder()
                .withUrl("/hubs/inventory-notifications")
                .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
                .build();
            realtimeConnection.on("TerminalRegistrationChanged", event => {
                const eventId = read(event, "otpChallengePublicId", "OtpChallengePublicId");
                if (registrationChallengeId && eventId
                    && String(eventId).toLowerCase() !== String(registrationChallengeId).toLowerCase()) return;
                const status = read(event, "status", "Status");
                const eventTerminalId = read(event, "terminalId", "TerminalId");
                const belongsToCurrentDevice = eventTerminalId
                    ? sameTerminalId(eventTerminalId, terminalDeviceIdentity?.terminalId)
                    : Boolean(registrationChallengeId && eventId
                        && String(eventId).toLowerCase() === String(registrationChallengeId).toLowerCase());
                if (belongsToCurrentDevice && ["USED", "REJECTED"].includes(String(status || "").toUpperCase())) {
                    registrationChallengeId = eventId || registrationChallengeId;
                    void notifyTerminalResolutionAndReload(status);
                    return;
                }
                void restoreTerminalOtpState();
            });
            const handleLateApprovalChanged = event => {
                const eventId = read(event, "publicId", "PublicId");
                if (!lateOpenApprovalPublicId || !eventId
                    || String(eventId).toLowerCase() !== String(lateOpenApprovalPublicId).toLowerCase()) return;
                void refreshLateOpenApproval();
            };
            realtimeConnection.onreconnected(() => {
                if (registrationChallengeId) void restoreTerminalOtpState();
                if (lateOpenApprovalPublicId) void refreshLateOpenApproval();
            });
            realtimeConnection.start().catch(error => {
                console.warn("[StaffHub realtime] Polling fallback remains active.", error);
            });

            workShiftRealtimeConnection = new signalR.HubConnectionBuilder()
                .withUrl("/hubs/workshifts")
                .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
                .build();
            workShiftRealtimeConnection.on("LateOpenApprovalChanged", handleLateApprovalChanged);
            workShiftRealtimeConnection.onreconnected(() => {
                if (lateOpenApprovalPublicId) void refreshLateOpenApproval();
            });
            workShiftRealtimeConnection.start().catch(error => {
                console.warn("[StaffHub WorkShift realtime] Polling fallback remains active.", error);
            });
            window.addEventListener("pagehide", () => {
                if (realtimeConnection) void realtimeConnection.stop();
                if (workShiftRealtimeConnection) void workShiftRealtimeConnection.stop();
            }, { once: true });
        }

        function contextText(context, minutesEarly) {
            if (context === "EARLY_FOR_SCHEDULE") return "Mở POS quá sớm — cần phê duyệt";
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
                registerTerminalButton && terminalUiState !== "READY"
                    ? "Thiết bị này chưa được liên kết với một Terminal active. Hãy đóng hộp thoại và chọn “Đăng ký thiết bị này” trước."
                    : "Terminal của thiết bị này sẽ được dùng cho phiên POS. Mỗi Terminal chỉ có một ca đang hoạt động.";
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
            const managerApprovalFromMinutes = Number(read(value, "managerApprovalFromMinutes", "ManagerApprovalFromMinutes") || 30);
            const scheduledApprovalMaxLateMinutes = Number(read(value, "scheduledApprovalMaxLateMinutes", "ScheduledApprovalMaxLateMinutes") || 45);
            const canManagerApproveAsScheduled = Boolean(read(value, "canManagerApproveAsScheduled", "CanManagerApproveAsScheduled"));
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
            if (managerApprovalRequired && canManagerApproveAsScheduled) {
                notes.push(`Ca trễ từ ${managerApprovalFromMinutes} đến ${scheduledApprovalMaxLateMinutes} phút. Manager có thể duyệt mở theo lịch.`);
            } else if (managerApprovalRequired) {
                notes.push(`Ca trễ quá ${scheduledApprovalMaxLateMinutes} phút. Manager chỉ có thể từ chối hoặc chuyển ngoài lịch.`);
            }
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
                return notify("Yêu cầu mở ca trễ chưa được Manager xử lý.", false);
            continueButton.disabled = true;
            try {
                const result = await post(root.dataset.issuePosUrl, {
                    TerminalId: terminalSelect.value, RequestKey: requestKey, Reason: reason,
                    AssessmentVersion: assessmentVersion(),
                    OtpChallengePublicId: verifiedOtpChallengePublicId,
                    LateOpenApprovalPublicId: lateOpenApprovalPublicId
                });
                await redirectWithTicket(result);
            } catch (error) {
                continueButton.disabled = false;
                if (error.errorCode === "SHIFT_SCHEDULE_CHANGED") {
                    await notify(error.message, false);
                    await previewOpen();
                    return;
                }
                await notify(error.message, false);
            }
        }

        function applyLateOpenApproval(data) {
            if (!data) return;
            lateOpenApprovalPublicId = read(data, "publicId", "PublicId") || lateOpenApprovalPublicId;
            lateOpenApprovalState = String(read(data, "status", "Status") || "PENDING").toUpperCase();
            renderCancelOpenButton();
            if (lateApprovalStatus) {
                lateApprovalStatus.textContent = lateOpenApprovalState === "PENDING"
                    ? "Đang chờ Manager xử lý..."
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
                    RequestKey: requestKey,
                    AssessmentVersion: assessmentVersion()
                });
                applyLateOpenApproval(result.data);
                if (lateOpenApprovalState === "PENDING" && !lateApprovalPollTimer) {
                    lateApprovalPollTimer = window.setInterval(() => void refreshLateOpenApproval(), 5000);
                }
            } catch (error) {
                requestLateApprovalButton.disabled = false;
                requestLateApprovalButton.textContent = "Gửi yêu cầu Manager";
                if (error.errorCode === "SHIFT_SCHEDULE_CHANGED") {
                    await notify(error.message, false);
                    await previewOpen();
                    return;
                }
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
        cancelOpenButton?.addEventListener("click", async () => {
            if (!hasActiveOpenIntent()) {
                requestKey = crypto.randomUUID();
                allowOpenDialogClose = true;
                closeDialog(previewDialog);
                return;
            }
            if (!window.confirm("Hủy yêu cầu mở ca đang hoạt động? OTP/Manager approval chưa dùng sẽ bị vô hiệu hóa.")) return;
            cancelOpenButton.disabled = true;
            try {
                const result = await post(root.dataset.cancelOpenPosUrl, {
                    TerminalId: terminalSelect?.value || "",
                    RequestKey: requestKey,
                    OtpChallengePublicId: otpChallengePublicId,
                    LateOpenApprovalPublicId: lateOpenApprovalPublicId
                });
                resetOpenOtpState();
                lateOpenApprovalPublicId = null;
                lateOpenApprovalState = "IDLE";
                requestKey = crypto.randomUUID();
                allowOpenDialogClose = true;
                await notify(result.message || "Đã hủy yêu cầu mở ca.", true);
                closeDialog(previewDialog);
            } catch (error) {
                await notify(error.message || "Không thể hủy yêu cầu mở ca.", false);
            } finally {
                cancelOpenButton.disabled = false;
                renderCancelOpenButton();
            }
        });
        continueButton?.addEventListener("click", issueOpenTicket);
        reasonInput?.addEventListener("input", updateReasonCounter);
        updateReasonCounter();
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
                    TerminalId: terminalSelect.value, RequestKey: requestKey, Reason: reason,
                    AssessmentVersion: assessmentVersion()
                });
                applyOpenOtpResponse(result.data);
                await notifyOtp(result.message || "OTP đã được gửi cho người duyệt.", true);
            } catch (error) {
                openOtpState = "IDLE";
                resetResendButton(resendOpenOtpButton);
                if (error.errorCode === "SHIFT_SCHEDULE_CHANGED") {
                    await notifyOtp(error.message, false);
                    await previewOpen();
                    return;
                }
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
                setCodeValue(otpInput, "");
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

        registerTerminalButton?.addEventListener("click", () => {
            showDialog(registrationDialog);
            void restoreTerminalOtpState();
        });
        document.getElementById("cancelTerminalRegistration")?.addEventListener("click", () => {
            closeDialog(registrationDialog);
        });
        cancelTerminalRequestButton?.addEventListener("click", async () => {
            if (!registrationChallengeId) return;
            if (!window.confirm("Hủy yêu cầu đăng ký Terminal đang chờ?")) return;
            cancelTerminalRequestButton.disabled = true;
            try {
                const result = await post(root.dataset.cancelTerminalOtpUrl, {
                    OtpChallengePublicId: registrationChallengeId
                });
                applyTerminalOtpResponse(result.data);
                await notify(result.message || "Đã hủy yêu cầu đăng ký Terminal.", true);
            } catch (error) {
                await notify(error.message || "Không thể hủy yêu cầu đăng ký Terminal.", false);
            } finally {
                cancelTerminalRequestButton.disabled = false;
            }
        });
        linkExistingTerminalButton?.addEventListener("click", async () => {
            const selectedId = terminalExistingSelect?.value || "";
            const activeTerminal = getActiveTerminal(selectedId);
            if (!activeTerminal) {
                await notifyOtp("Vui lòng chọn một Terminal active thuộc cửa hàng hiện tại.", false);
                terminalExistingSelect?.focus();
                return;
            }
            const confirmed = window.Swal
                ? (await window.Swal.fire({
                    icon: "question",
                    title: "Liên kết thiết bị hiện tại?",
                    text: `Thiết bị này sẽ sử dụng Terminal “${activeTerminal.name}”. Liên kết cục bộ không thay đổi quyền trên máy chủ.`,
                    showCancelButton: true,
                    confirmButtonText: "Liên kết Terminal",
                    cancelButtonText: "Quay lại",
                    focusCancel: true
                })).isConfirmed
                : window.confirm(`Liên kết thiết bị này với Terminal “${activeTerminal.name}”?`);
            if (!confirmed) return;

            linkExistingTerminalButton.disabled = true;
            try {
                const identity = saveTerminalDeviceIdentity(activeTerminal.terminalId, activeTerminal.name, "existing");
                if (!identity) throw new Error("Trình duyệt không thể lưu liên kết Terminal. Vui lòng kiểm tra quyền lưu dữ liệu trang web.");
                registrationChallengeId = null;
                registrationRequestKey = null;
                registrationOtpState = "IDLE";
                renderTerminalDeviceState("READY", activeTerminal);
                if (window.Swal) {
                    await window.Swal.fire({
                        icon: "success",
                        title: "Đã liên kết Terminal",
                        text: `${activeTerminal.name} đã được chọn cho thiết bị này.`,
                        confirmButtonText: "Đóng"
                    });
                } else {
                    window.alert(`${activeTerminal.name} đã được liên kết với thiết bị này.`);
                }
            } catch (error) {
                await notifyOtp(error.message || "Không thể liên kết Terminal.", false);
            } finally {
                linkExistingTerminalButton.disabled = false;
            }
        });
        requestTerminalOtpButton?.addEventListener("click", async () => {
            const name = terminalRegistrationName?.value.trim() || "";
            if (!name) return notify("Vui lòng nhập tên terminal.", false);
            if (["READY", "PENDING", "APPROVED", "LOCKED", "OTHER_PENDING", "STORE_MISMATCH", "INVALID_BINDING"].includes(terminalUiState))
                return notify("Thiết bị hiện tại chưa thể tạo yêu cầu đăng ký mới.", false);

            const identity = ensureTerminalDeviceIdentity(name);
            if (!identity) {
                renderStoredTerminalDeviceState();
                return notify("Thiết bị đang được liên kết với cửa hàng khác hoặc trình duyệt không thể lưu mã thiết bị.", false);
            }
            if (["EXPIRED", "CANCELLED", "REJECTED"].includes(registrationOtpState) || !registrationRequestKey) {
                registrationRequestKey = crypto.randomUUID();
                registrationChallengeId = null;
            }
            registrationTerminalId = identity.terminalId;
            requestTerminalOtpButton.disabled = true;
            requestTerminalOtpButton.textContent = "Đang gửi yêu cầu...";
            try {
                const result = await post(root.dataset.requestTerminalOtpUrl, {
                    TerminalId: registrationTerminalId, TerminalName: name, RequestKey: registrationRequestKey
                });
                applyTerminalOtpResponse(result.data);
                await notifyOtp(result.message || "Gửi yêu cầu xác nhận Terminal thành công. Người duyệt có thể mở Thông báo để xử lý.", true);
            } catch (error) {
                resetResendButton(resendTerminalOtpButton);
                await notify(error.message, false);
            } finally {
                if (!requestTerminalOtpButton.hidden) {
                    requestTerminalOtpButton.disabled = false;
                    requestTerminalOtpButton.textContent = terminalUiState === "UNLINKED"
                        ? "Đăng ký thiết bị này"
                        : "Gửi lại yêu cầu";
                }
            }
        });
        resendTerminalOtpButton?.addEventListener("click", async () => {
            if (!registrationChallengeId) return notify("Vui lòng gửi OTP trước.", false);
            resendTerminalOtpButton.disabled = true;
            try {
                const result = await post(root.dataset.resendOtpUrl, { OtpChallengePublicId: registrationChallengeId });
                applyTerminalOtpResponse(result.data);
                document.getElementById("terminalRegistrationStatus").textContent = "✓ Đã gửi lại OTP. Đang chờ Store Manager/người có quyền xác nhận Terminal.";
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

        openOperatorPinButton?.addEventListener("click", () => {
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
                setOperatorPinConfigured(result.pinConfigured === true);
                if (operatorPinStatus) operatorPinStatus.textContent = "PIN thao tác POS đã được thiết lập.";
                if (operatorCurrentPassword) operatorCurrentPassword.value = "";
                setCodeValue(operatorNewPin, "");
                await notify(result.message || "Thiết lập PIN thao tác POS thành công.", true);
                closeDialog(operatorPinDialog);
            } catch (error) {
                if (operatorPinStatus) operatorPinStatus.textContent = error.message;
                await notify(error.message || "Không thể thiết lập PIN thao tác POS.", false);
            } finally {
                if (operatorCurrentPassword) operatorCurrentPassword.value = "";
                setCodeValue(operatorNewPin, "");
                operatorPinForm.removeAttribute("aria-busy");
                if (saveOperatorPinButton) {
                    saveOperatorPinButton.disabled = false;
                    saveOperatorPinButton.textContent = "Lưu PIN";
                }
            }
        });
        previewDialog?.addEventListener("hide.bs.modal", event => {
            if (hasActiveOpenIntent() && !allowOpenDialogClose) {
                event.preventDefault();
                void notify("Yêu cầu mở ca vẫn đang hoạt động. Hãy tiếp tục hoặc chọn Hủy yêu cầu mở ca.", false);
            }
        });
        previewDialog?.addEventListener("hidden.bs.modal", () => {
            allowOpenDialogClose = false;
            setCodeValue(otpInput, "");
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
            setCodeValue(operatorNewPin, "");
            if (operatorPinStatus) operatorPinStatus.textContent = "";
        });

        const launchOptions = document.getElementById("staffHubLaunchOptions");
        renderStoredTerminalDeviceState();
        if (registerTerminalButton) void restoreTerminalOtpState();
        initializeRealtime();
        if (launchOptions?.dataset.autoOpenPos === "true") {
            const requestedTerminalId = launchOptions.dataset.requestedTerminalId || "";
            const posLaunchError = launchOptions.dataset.posLaunchError || "";
            const requestedMatchesDevice = !registerTerminalButton
                || (terminalUiState === "READY" && sameTerminalId(requestedTerminalId, terminalDeviceIdentity?.terminalId));
            if (requestedTerminalId && requestedMatchesDevice
                && terminalSelect?.querySelector(`option[value="${CSS.escape(requestedTerminalId)}"]`)) {
                terminalSelect.value = requestedTerminalId;
            }
            showDialog(previewDialog);
            if (posLaunchError) void notifyOtp(posLaunchError, false);
            if (requestedTerminalId && !requestedMatchesDevice)
                void notifyOtp("Terminal được yêu cầu không phải Terminal đã liên kết với thiết bị này.", false);
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
