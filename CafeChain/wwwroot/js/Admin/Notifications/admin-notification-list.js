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
        return `${minutesPart}:${secondsPart}`;
    }

    function createOtpCard(activeOtp) {
        if (!activeOtp?.code || !activeOtp?.expiresAtUtc) return null;
        const card = element("div", "notification-otp-card");
        card.dataset.activeOtp = "true";
        card.dataset.expiresAt = activeOtp.expiresAtUtc;

        const codeWrap = element("div");
        codeWrap.appendChild(element("span", "notification-otp-label", "Mã OTP còn hiệu lực"));
        const code = element("code", "", activeOtp.code);
        code.dataset.otpCode = "true";
        codeWrap.appendChild(code);
        card.appendChild(codeWrap);

        const copy = element("button", "cc-button");
        copy.type = "button";
        copy.dataset.copyOtp = "true";
        copy.appendChild(icon("far fa-copy"));
        copy.appendChild(text("Sao chép mã"));
        card.appendChild(copy);

        const countdown = element("small");
        countdown.dataset.otpCountdown = "true";
        card.appendChild(countdown);
        return card;
    }

    function updateOtpCards() {
        let hasExpired = false;
        document.querySelectorAll("[data-active-otp]").forEach(function (card) {
            const expiresAt = new Date(card.dataset.expiresAt || "");
            const remainingSeconds = Math.ceil((expiresAt.getTime() - Date.now()) / 1000);
            const label = card.querySelector("[data-otp-countdown]");
            if (!Number.isFinite(remainingSeconds) || remainingSeconds <= 0) {
                card.hidden = true;
                hasExpired = true;
                return;
            }
            if (label) {
                label.textContent = `Hết hạn sau ${formatCountdown(remainingSeconds)} · ${formatDate(expiresAt.toISOString())}`;
            }
        });
        if (hasExpired && !expiredRefreshRequested) {
            expiredRefreshRequested = true;
            window.dispatchEvent(new CustomEvent("admin-active-otp-expired"));
        }
    }

    document.addEventListener("click", async function (event) {
        const button = event.target.closest?.("[data-copy-otp]");
        if (!button) return;
        const code = button.closest("[data-active-otp]")?.querySelector("[data-otp-code]")?.textContent?.trim();
        if (!code) return;
        try {
            await navigator.clipboard.writeText(code);
            button.textContent = "Đã sao chép";
        } catch {
            button.textContent = "Không thể sao chép";
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

        const otpCard = createOtpCard(item.activeOtp);
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
