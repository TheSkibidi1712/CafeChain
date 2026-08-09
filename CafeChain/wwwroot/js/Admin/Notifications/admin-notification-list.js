(function () {
    "use strict";

    const root = document.querySelector("[data-admin-notification-list]");
    if (!root) return;
    const antiforgeryToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value || "";
    let expiredRefreshRequested = false;
    const otpPattern = /^[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{6}$/;

    const terminalErrorMessages = Object.freeze({
        OTP_INVALID: "OTP không hợp lệ. Vui lòng nhập đúng 6 ký tự.",
        OTP_EXPIRED: "OTP đã hết hạn. Vui lòng gửi yêu cầu mới.",
        OTP_VERIFICATION_LOCKED: "OTP đã bị khóa do nhập sai quá số lần cho phép.",
        OTP_RESEND_COOLDOWN: "Yêu cầu OTP đang trong thời gian chờ.",
        TERMINAL_APPROVAL_NOT_FOUND: "Không tìm thấy yêu cầu xác nhận Terminal.",
        TERMINAL_ALREADY_APPROVED: "Terminal này đã được xác nhận.",
        TERMINAL_NOT_PENDING: "Terminal không còn ở trạng thái chờ xác nhận.",
        TERMINAL_APPROVAL_FORBIDDEN: "Bạn không có quyền xác nhận Terminal.",
        TERMINAL_STORE_SCOPE_INVALID: "Bạn không có quyền quản lý Terminal của cửa hàng này.",
        TERMINAL_APPROVAL_CONFLICT: "Yêu cầu đang được xử lý. Vui lòng tải lại trạng thái.",
        TERMINAL_REJECTION_FORBIDDEN: "Bạn không có quyền từ chối đăng ký Terminal.",
        TERMINAL_ALREADY_REJECTED: "Yêu cầu đăng ký Terminal này đã được từ chối.",
        TERMINAL_REJECTION_REASON_INVALID: "Lý do từ chối phải có từ 10 đến 500 ký tự và có nội dung cụ thể.",
        INVALID_REQUEST_KEY: "Mã chống gửi trùng không hợp lệ. Vui lòng tải lại trang."
    });

    function text(value) {
        return document.createTextNode(value == null ? "" : String(value));
    }

    function element(tag, className, value) {
        const node = document.createElement(tag);
        if (className) node.className = className;
        if (value != null) node.appendChild(text(value));
        return node;
    }

    function icon(className) {
        const node = element("i", className);
        node.setAttribute("aria-hidden", "true");
        return node;
    }

    function normalizeUtc(value) {
        const timestamp = String(value || "").trim();
        if (!timestamp) return "";
        return /(z|[+-]\d\d:?\d\d)$/i.test(timestamp) ? timestamp : `${timestamp}Z`;
    }

    function formatDate(value) {
        const date = new Date(normalizeUtc(value));
        if (Number.isNaN(date.getTime())) return "";
        return new Intl.DateTimeFormat("vi-VN", {
            timeZone: "Asia/Ho_Chi_Minh",
            day: "2-digit",
            month: "2-digit",
            year: "numeric",
            hour: "2-digit",
            minute: "2-digit"
        }).format(date);
    }

    function formatCountdown(totalSeconds) {
        const seconds = Math.max(0, Math.floor(totalSeconds));
        const minutesPart = String(Math.floor(seconds / 60)).padStart(2, "0");
        const secondsPart = String(seconds % 60).padStart(2, "0");
        return `${minutesPart} phút ${secondsPart} giây`;
    }

    function hiddenInput(name, value) {
        const input = document.createElement("input");
        input.type = "hidden";
        input.name = name;
        input.value = value;
        return input;
    }

    function setCodeInvalid(input, invalid) {
        if (window.VerificationCodeInput?.setInvalid) {
            window.VerificationCodeInput.setInvalid(input, invalid);
        } else if (input) {
            if (invalid) input.setAttribute("aria-invalid", "true");
            else input.removeAttribute("aria-invalid");
        }
    }

    function isCompleteOtp(input) {
        if (!input) return false;
        if (window.VerificationCodeInput?.isComplete) {
            return window.VerificationCodeInput.isComplete(input);
        }
        return otpPattern.test(String(input.value || "").trim().toUpperCase());
    }

    function focusIncompleteOtp(input) {
        if (window.VerificationCodeInput?.focusFirstIncomplete) {
            window.VerificationCodeInput.focusFirstIncomplete(input);
        } else {
            input?.focus();
        }
    }

    function terminalErrorMessage(payload) {
        const code = String(payload?.errorCode || "").trim().toUpperCase();
        let message = terminalErrorMessages[code] || payload?.message || "Không thể xác nhận Terminal.";
        const retryAfter = Number(payload?.data?.retryAfter || 0);
        if ((code === "OTP_VERIFICATION_LOCKED" || code === "OTP_RESEND_COOLDOWN") && retryAfter > 0) {
            message += ` Thử lại sau ${formatCountdown(retryAfter)}.`;
        }
        return message;
    }

    function createTerminalConfirmationForm(notificationId) {
        const form = element("form", "notification-terminal-confirm-form");
        form.method = "post";
        form.action = "/Admin/AdminNotifications/ConfirmTerminal";
        form.noValidate = true;
        form.dataset.validationFeedback = "inline";
        if (antiforgeryToken) {
            form.appendChild(hiddenInput("__RequestVerificationToken", antiforgeryToken));
        }
        form.appendChild(hiddenInput("id", String(notificationId)));
        const requestKey = globalThis.crypto?.randomUUID?.().replaceAll("-", "")
            || `${Date.now()}${Math.random().toString(16).slice(2)}`;
        form.appendChild(hiddenInput("RequestKey", requestKey));

        const label = element("label");
        label.appendChild(element("span", "", "Nhập OTP để xác nhận Terminal"));
        const input = document.createElement("input");
        input.name = "OtpCode";
        input.maxLength = 6;
        input.autocomplete = "one-time-code";
        input.dataset.verificationCodeInput = "true";
        input.required = true;
        label.appendChild(input);
        form.appendChild(label);

        const submit = element("button", "cc-button", "Xác nhận Terminal");
        submit.type = "submit";
        submit.dataset.submitOnce = "true";
        form.appendChild(submit);
        const feedback = element("small");
        feedback.dataset.terminalConfirmFeedback = "true";
        feedback.setAttribute("aria-live", "polite");
        form.appendChild(feedback);
        return form;
    }

    function createTerminalRejectionForm(notificationId) {
        const form = element("form", "notification-terminal-reject-form");
        form.method = "post";
        form.action = "/Admin/AdminNotifications/RejectTerminal";
        form.noValidate = true;
        form.dataset.validationFeedback = "inline";
        if (antiforgeryToken) form.appendChild(hiddenInput("__RequestVerificationToken", antiforgeryToken));
        form.appendChild(hiddenInput("id", String(notificationId)));
        const requestKey = globalThis.crypto?.randomUUID?.().replaceAll("-", "")
            || `${Date.now()}${Math.random().toString(16).slice(2)}`;
        form.appendChild(hiddenInput("RequestKey", requestKey));

        const textareaId = `terminalRejectReason-${notificationId}`;
        const label = element("label", "", "Lý do từ chối đăng ký Terminal");
        label.htmlFor = textareaId;
        const textarea = document.createElement("textarea");
        textarea.id = textareaId;
        textarea.name = "Reason";
        textarea.rows = 3;
        textarea.minLength = 10;
        textarea.maxLength = 500;
        textarea.placeholder = "Nhập lý do cụ thể (10–500 ký tự)";
        form.append(label, textarea);

        const meta = element("div", "notification-terminal-reject-meta");
        const counter = element("small", "", "0 / 500");
        counter.dataset.terminalRejectCounter = "true";
        const feedback = element("small");
        feedback.dataset.terminalRejectFeedback = "true";
        feedback.setAttribute("aria-live", "polite");
        meta.append(counter, feedback);
        form.appendChild(meta);

        const submit = element("button", "cc-button notification-terminal-reject-button", "Từ chối đăng ký Terminal");
        submit.type = "submit";
        submit.dataset.rejectSubmitOnce = "true";
        form.appendChild(submit);
        return form;
    }

    function createOtpCard(otp, notificationId) {
        if (!otp?.challengePublicId) return null;
        const card = element("div", "notification-otp-card");
        card.dataset.operationalOtp = "true";
        card.dataset.serverNow = otp.serverNowUtc;
        card.dataset.expiresAt = otp.expiresAtUtc;
        [
            ["Terminal", otp.terminalName],
            ["Chi nhánh", otp.storeName],
            ["Người gửi", otp.requestedByName],
            ["Người xác nhận", otp.confirmedByName || otp.approverName],
            ["Thời gian gửi", formatDate(otp.sentAtUtc)],
            ["Thời gian hết hạn", formatDate(otp.expiresAtUtc)],
            ["Trạng thái", otp.status]
        ].forEach(([label, value]) => card.appendChild(element("div", "", `${label}: ${value || "—"}`)));
        if (otp.status === "Waiting") {
            const countdown = element("small");
            countdown.dataset.otpCountdown = "true";
            card.appendChild(countdown);
            if (otp.canRevealOtp === true) {
                const footer = element("div", "notification-item-footer");
                const reveal = element("button", "cc-button", "Xem OTP");
                reveal.type = "button";
                reveal.dataset.revealOperationalOtp = "true";
                reveal.dataset.notificationId = String(notificationId);
                footer.appendChild(reveal);
                card.appendChild(footer);
            }
            if (otp.canContinueTerminalConfirmation === true) {
                card.appendChild(createTerminalConfirmationForm(notificationId));
            }
            if (otp.canRejectTerminalRegistration === true) {
                card.appendChild(createTerminalRejectionForm(notificationId));
            }
        } else if (otp.status === "Expired") {
            card.appendChild(element("small", "", "OTP đã hết hạn. Vui lòng gửi yêu cầu mới."));
        } else if (otp.status === "Rejected") {
            card.appendChild(element("small", "", "Yêu cầu đăng ký Terminal đã bị từ chối."));
        }
        return card;
    }

    function updateOtpCards() {
        let hasExpired = false;
        document.querySelectorAll("[data-operational-otp]").forEach(function (card) {
            const expiresAt = new Date(normalizeUtc(card.dataset.expiresAt));
            const serverNow = new Date(normalizeUtc(card.dataset.serverNow));
            if (!card.dataset.clientReceivedAt) card.dataset.clientReceivedAt = String(Date.now());
            const clientReceivedAt = Number(card.dataset.clientReceivedAt);
            const serverOffset = Number.isNaN(serverNow.getTime()) ? 0 : serverNow.getTime() - clientReceivedAt;
            const remainingSeconds = Math.ceil((expiresAt.getTime() - (Date.now() + serverOffset)) / 1000);
            const label = card.querySelector("[data-otp-countdown]");
            if (!Number.isFinite(remainingSeconds) || remainingSeconds <= 0) {
                if (label) label.textContent = "OTP đã hết hạn. Vui lòng gửi yêu cầu mới.";
                card.querySelectorAll("[data-reveal-operational-otp], .notification-terminal-confirm-form, .notification-terminal-reject-form")
                    .forEach((node) => { node.hidden = true; });
                hasExpired = true;
                return;
            }
            if (label) {
                label.textContent = `OTP còn hiệu lực: ${formatCountdown(remainingSeconds)}`;
            }
        });
        if (hasExpired && !expiredRefreshRequested) {
            expiredRefreshRequested = true;
            window.dispatchEvent(new CustomEvent("admin-active-otp-expired"));
        }
    }

    document.addEventListener("click", async function (event) {
        const button = event.target.closest?.("[data-reveal-operational-otp]");
        if (!button || button.disabled) return;
        button.disabled = true;
        try {
            const id = encodeURIComponent(button.dataset.notificationId || "");
            const response = await fetch(`/Admin/AdminNotifications/RevealOperationalOtp?id=${id}`, {
                credentials: "same-origin",
                cache: "no-store",
                headers: { Accept: "application/json" }
            });
            const payload = await response.json();
            if (!response.ok || !payload?.success || !payload?.data?.code) {
                button.textContent = payload?.message || "Không thể xem OTP";
                return;
            }
            const code = element("code", "", payload.data.code);
            code.dataset.otpCode = "true";
            const copy = element("button", "cc-button", "Copy OTP");
            copy.type = "button";
            copy.addEventListener("click", async () => {
                try {
                    await navigator.clipboard.writeText(String(payload.data.code).trim());
                    copy.textContent = "Đã sao chép mã OTP";
                } catch {
                    copy.textContent = "Không thể sao chép OTP";
                }
            });
            const otpInput = button.closest("[data-operational-otp]")
                ?.querySelector('.notification-terminal-confirm-form input[name="OtpCode"]');
            if (otpInput) {
                if (window.VerificationCodeInput?.setValue) {
                    window.VerificationCodeInput.setValue(otpInput, payload.data.code);
                } else {
                    otpInput.value = String(payload.data.code).trim().toUpperCase();
                }
                setCodeInvalid(otpInput, false);
                const feedback = otpInput.closest("form")?.querySelector("[data-terminal-confirm-feedback]");
                if (feedback) feedback.textContent = "OTP đã được điền. Bạn có thể xác nhận Terminal.";
            }
            button.replaceWith(code, copy);
        } catch {
            button.textContent = "Lỗi mạng, thử lại";
            button.disabled = false;
        }
    });

    async function confirmTerminalForm(form) {
        const submit = form.querySelector("[data-submit-once]");
        if (submit?.disabled) {
            return;
        }
        let feedback = form.querySelector("[data-terminal-confirm-feedback]");
        if (!feedback) {
            feedback = element("small");
            feedback.dataset.terminalConfirmFeedback = "true";
            feedback.setAttribute("aria-live", "polite");
            form.appendChild(feedback);
        }
        feedback.textContent = "";
        const otpInput = form.querySelector('input[name="OtpCode"]');
        if (!isCompleteOtp(otpInput)) {
            setCodeInvalid(otpInput, true);
            feedback.textContent = "Vui lòng nhập đủ 6 ký tự OTP hợp lệ.";
            focusIncompleteOtp(otpInput);
            return;
        }
        setCodeInvalid(otpInput, false);
        if (submit) {
            submit.disabled = true;
            submit.textContent = "Đang xác nhận...";
        }
        feedback.textContent = "Đang gửi yêu cầu xác nhận Terminal...";
        let completed = false;
        const abortController = new AbortController();
        const timeoutId = window.setTimeout(() => abortController.abort(), 15000);
        try {
            const formData = new FormData(form);
            formData.set("OtpCode", String(otpInput.value || "").trim().toUpperCase());
            const response = await fetch(form.action, {
                method: "POST",
                body: formData,
                credentials: "same-origin",
                headers: { Accept: "application/json", "X-Requested-With": "XMLHttpRequest" },
                signal: abortController.signal
            });
            const payload = await response.json().catch(() => ({}));
            if (!response.ok || !payload.success) {
                feedback.textContent = terminalErrorMessage(payload);
                if (String(payload?.errorCode || "").toUpperCase() === "OTP_INVALID") {
                    setCodeInvalid(otpInput, true);
                    focusIncompleteOtp(otpInput);
                }
                return;
            }
            completed = true;
            feedback.textContent = payload.data?.alreadyProcessed
                ? "Terminal đã được xác nhận trước đó."
                : "Terminal đã được xác nhận.";
            if (submit) submit.textContent = "Đã xác nhận";
            window.setTimeout(() => window.location.reload(), 500);
        } catch (error) {
            feedback.textContent = error?.name === "AbortError"
                ? "Yêu cầu xác nhận quá thời gian chờ. Vui lòng thử lại."
                : "Lỗi kết nối. Vui lòng thử lại.";
        } finally {
            window.clearTimeout(timeoutId);
            if (submit && !completed) {
                submit.disabled = false;
                submit.textContent = "Xác nhận Terminal";
            }
        }
    }

    document.addEventListener("click", function (event) {
        const submit = event.target.closest?.(".notification-terminal-confirm-form [data-submit-once]");
        if (!submit) return;
        event.preventDefault();
        event.stopImmediatePropagation();
        const form = submit.closest(".notification-terminal-confirm-form");
        if (form) void confirmTerminalForm(form);
    }, true);

    document.addEventListener("submit", function (event) {
        const form = event.target.closest?.(".notification-terminal-confirm-form");
        if (!form) return;
        event.preventDefault();
        event.stopImmediatePropagation();
        void confirmTerminalForm(form);
    });

    document.addEventListener("input", function (event) {
        const input = event.target;
        if (!(input instanceof HTMLInputElement) || input.name !== "OtpCode") return;
        const form = input.closest(".notification-terminal-confirm-form");
        if (!form) return;
        setCodeInvalid(input, false);
        const feedback = form.querySelector("[data-terminal-confirm-feedback]");
        if (feedback) feedback.textContent = "";
    });

    function isValidRejectionReason(value) {
        const reason = String(value || "").trim();
        return reason.length >= 10 && reason.length <= 500 && /[\p{L}\p{N}]/u.test(reason);
    }

    async function rejectTerminalForm(form) {
        const submit = form.querySelector("[data-reject-submit-once]");
        if (submit?.disabled) return;
        const reasonInput = form.querySelector('textarea[name="Reason"]');
        const feedback = form.querySelector("[data-terminal-reject-feedback]");
        const reason = String(reasonInput?.value || "").trim();
        if (!isValidRejectionReason(reason)) {
            if (feedback) feedback.textContent = terminalErrorMessages.TERMINAL_REJECTION_REASON_INVALID;
            reasonInput?.setAttribute("aria-invalid", "true");
            reasonInput?.focus();
            return;
        }

        if (window.Swal) {
            const answer = await window.Swal.fire({
                icon: "warning",
                title: "Từ chối đăng ký Terminal?",
                text: "Thiết bị sẽ không được kích hoạt và nhân viên phải gửi yêu cầu mới.",
                showCancelButton: true,
                confirmButtonText: "Từ chối",
                cancelButtonText: "Quay lại",
                confirmButtonColor: "#9f2d20"
            });
            if (!answer.isConfirmed) return;
        }

        reasonInput?.removeAttribute("aria-invalid");
        if (feedback) feedback.textContent = "Đang xử lý yêu cầu từ chối...";
        if (submit) {
            submit.disabled = true;
            submit.textContent = "Đang từ chối...";
        }
        let completed = false;
        try {
            const formData = new FormData(form);
            formData.set("Reason", reason);
            const response = await fetch(form.action, {
                method: "POST",
                body: formData,
                credentials: "same-origin",
                headers: { Accept: "application/json", "X-Requested-With": "XMLHttpRequest" }
            });
            const payload = await response.json().catch(() => ({}));
            if (!response.ok || !payload?.success) {
                if (feedback) feedback.textContent = terminalErrorMessage(payload);
                return;
            }
            completed = true;
            if (window.Swal) {
                await window.Swal.fire({
                    icon: "success",
                    title: "Đã từ chối",
                    text: payload.message || "Yêu cầu đăng ký Terminal đã bị từ chối.",
                    timer: 1400,
                    showConfirmButton: false
                });
            }
            window.location.reload();
        } catch {
            if (feedback) feedback.textContent = "Lỗi kết nối. Vui lòng thử lại.";
        } finally {
            if (submit && !completed) {
                submit.disabled = false;
                submit.textContent = "Từ chối đăng ký Terminal";
            }
        }
    }

    document.addEventListener("submit", function (event) {
        const form = event.target.closest?.(".notification-terminal-reject-form");
        if (!form) return;
        event.preventDefault();
        event.stopImmediatePropagation();
        void rejectTerminalForm(form);
    }, true);

    document.addEventListener("input", function (event) {
        const textarea = event.target.closest?.('.notification-terminal-reject-form textarea[name="Reason"]');
        if (!textarea) return;
        const form = textarea.closest(".notification-terminal-reject-form");
        const counter = form?.querySelector("[data-terminal-reject-counter]");
        const feedback = form?.querySelector("[data-terminal-reject-feedback]");
        if (counter) counter.textContent = `${textarea.value.length} / 500`;
        textarea.removeAttribute("aria-invalid");
        if (feedback) feedback.textContent = "";
    });

    function renderItem(item) {
        const row = element("article", `notification-item${item.isRead ? "" : " is-unread"}`);
        row.id = `notification-${item.notificationId}`;
        const itemIcon = element("span", "notification-item-icon");
        itemIcon.setAttribute("aria-hidden", "true");
        itemIcon.appendChild(icon(item.isRead ? "fas fa-check" : "fas fa-bell"));
        row.appendChild(itemIcon);

        const layout = element("div", "notification-item-layout");
        const content = element("div", "notification-item-main");
        const heading = element("div", "notification-item-heading");
        heading.appendChild(element("strong", "notification-title", item.title));
        if (!item.isRead) {
            const badge = element("span", "cc-status-badge notification-unread-badge");
            badge.appendChild(text("Chưa đọc"));
            heading.appendChild(badge);
        }
        content.appendChild(heading);

        content.appendChild(element("div", "notification-body", item.body));

        const otpCard = createOtpCard(item.operationalOtp, item.notificationId);
        if (otpCard) content.appendChild(otpCard);

        const meta = element("div", "notification-item-footer");
        const time = element("span", "notification-time");
        time.appendChild(icon("far fa-clock"));
        time.appendChild(text(formatDate(item.createdAt)));
        meta.appendChild(time);
        if (item.emailAttempted && !item.emailSent) {
            const warning = element("span", "notification-email-warning");
            warning.appendChild(icon("fas fa-triangle-exclamation"));
            warning.appendChild(text("Email chưa gửi được"));
            meta.appendChild(warning);
        }
        if (item.targetUrl) {
            const link = element("a", "notification-link");
            link.href = item.targetUrl;
            link.appendChild(text(`${item.targetActionLabel || "Xem chi tiết"} `));
            link.appendChild(icon("fas fa-arrow-right"));
            meta.appendChild(link);
        }
        content.appendChild(meta);
        layout.appendChild(content);
        row.appendChild(layout);
        return row;
    }

    function render(data) {
        expiredRefreshRequested = false;
        root.replaceChildren();
        const items = Array.isArray(data?.items) ? data.items : [];
        const total = Number(data?.total || items.length);
        const unread = Number(data?.unreadCount || 0);
        document.querySelector("[data-notification-total]")?.replaceChildren(text(total));
        const unreadValue = document.querySelector("[data-notification-unread]");
        unreadValue?.replaceChildren(text(unread));
        unreadValue?.closest(".notification-summary-card")?.classList.toggle("has-unread", unread > 0);
        document.querySelector("[data-notification-read]")?.replaceChildren(text(Math.max(0, total - unread)));
        const markAllButton = document.querySelector(".notification-mark-all");
        if (markAllButton) markAllButton.disabled = unread <= 0;
        if (items.length === 0) {
            const card = element("div", "cc-empty-state cc-warehouse-empty");
            card.setAttribute("role", "status");
            const emptyIcon = element("span", "notification-empty-icon");
            emptyIcon.setAttribute("aria-hidden", "true");
            emptyIcon.appendChild(icon("far fa-bell-slash"));
            card.appendChild(emptyIcon);
            card.appendChild(element("h2", "", "Chưa có thông báo"));
            card.appendChild(element("p", "", "Các yêu cầu phê duyệt và cập nhật vận hành phù hợp với phạm vi của bạn sẽ xuất hiện tại đây."));
            root.appendChild(card);
            return;
        }

        const list = element("section", "notification-list cc-warehouse-panel");
        list.setAttribute("aria-label", "Danh sách thông báo");
        items.forEach(function (item) {
            list.appendChild(renderItem(item));
        });
        root.appendChild(list);
        const pagination = element("nav", "notification-pagination");
        pagination.setAttribute("aria-label", "Phân trang thông báo");
        pagination.appendChild(element("small", "", `Trang ${data.page || 1} · Tổng ${total} thông báo`));
        root.appendChild(pagination);
        updateOtpCards();
    }

    window.addEventListener("admin-notification-list-updated", function (event) {
        if (event.detail) render(event.detail);
    });

    updateOtpCards();
    window.setInterval(updateOtpCards, 1000);
})();
