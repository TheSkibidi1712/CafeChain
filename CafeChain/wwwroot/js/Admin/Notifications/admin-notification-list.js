(function () {
    "use strict";

    const root = document.querySelector("[data-admin-notification-list]");
    if (!root) return;
    let expiredRefreshRequested = false;

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

    function formatDate(value) {
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) return "";
        return new Intl.DateTimeFormat("vi-VN", {
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
            const reveal = element("button", "cc-button", "Xem OTP");
            reveal.type = "button";
            reveal.dataset.revealTerminalOtp = "true";
            reveal.dataset.notificationId = String(notificationId);
            card.appendChild(reveal);
        } else if (otp.status === "Expired") {
            card.appendChild(element("small", "", "OTP đã hết hạn. Vui lòng gửi yêu cầu mới."));
        }
        return card;
    }

    function updateOtpCards() {
        let hasExpired = false;
        document.querySelectorAll("[data-operational-otp]").forEach(function (card) {
            const expiresAt = new Date(card.dataset.expiresAt || "");
            const serverNow = new Date(card.dataset.serverNow || "");
            if (!card.dataset.clientReceivedAt) card.dataset.clientReceivedAt = String(Date.now());
            const clientReceivedAt = Number(card.dataset.clientReceivedAt);
            const serverOffset = Number.isNaN(serverNow.getTime()) ? 0 : serverNow.getTime() - clientReceivedAt;
            const remainingSeconds = Math.ceil((expiresAt.getTime() - (Date.now() + serverOffset)) / 1000);
            const label = card.querySelector("[data-otp-countdown]");
            if (!Number.isFinite(remainingSeconds) || remainingSeconds <= 0) {
                if (label) label.textContent = "OTP đã hết hạn. Vui lòng gửi yêu cầu mới.";
                card.querySelectorAll("[data-reveal-terminal-otp], .notification-terminal-confirm-form")
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
        const button = event.target.closest?.("[data-reveal-terminal-otp]");
        if (!button || button.disabled) return;
        button.disabled = true;
        try {
            const id = encodeURIComponent(button.dataset.notificationId || "");
            const response = await fetch(`/Admin/AdminNotifications/RevealTerminalOtp?id=${id}`, {
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
            button.replaceWith(code);
        } catch {
            button.textContent = "Lỗi mạng, thử lại";
            button.disabled = false;
        }
    });

    document.addEventListener("submit", function (event) {
        const form = event.target.closest?.(".notification-terminal-confirm-form");
        if (!form) return;
        const submit = form.querySelector("[data-submit-once]");
        if (submit?.disabled) {
            event.preventDefault();
            return;
        }
        if (submit) {
            submit.disabled = true;
            submit.textContent = "Đang xác nhận...";
        }
    });

    function renderItem(item) {
        const row = element("article", `notification-item${item.isRead ? "" : " is-unread"}`);
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
            link.appendChild(text("Xem chi tiết "));
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
