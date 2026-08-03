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
        const card = element("div", "alert alert-warning d-flex flex-wrap align-items-center gap-3 mb-2");
        card.dataset.activeOtp = "true";
        card.dataset.expiresAt = activeOtp.expiresAtUtc;

        const codeWrap = element("div");
        codeWrap.appendChild(element("span", "d-block small fw-semibold", "Mã OTP còn hiệu lực"));
        const code = element("code", "fs-3 fw-bold", activeOtp.code);
        code.dataset.otpCode = "true";
        codeWrap.appendChild(code);
        card.appendChild(codeWrap);

        const copy = element("button", "btn btn-sm btn-outline-primary", "Sao chép mã");
        copy.type = "button";
        copy.dataset.copyOtp = "true";
        card.appendChild(copy);

        const countdown = element("small", "text-muted");
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
        const row = element(
            "div",
            `list-group-item${item.isRead ? "" : " list-group-item-warning"}`);
        const layout = element("div", "d-flex justify-content-between align-items-start gap-2");
        const content = element("div", "flex-grow-1");
        const heading = element("div", "d-flex align-items-center gap-2 mb-1");
        heading.appendChild(element("strong", "", item.title));
        if (!item.isRead) heading.appendChild(element("span", "badge bg-danger", "Chưa đọc"));
        content.appendChild(heading);

        const body = element("div", "small text-muted mb-2", item.body);
        body.style.whiteSpace = "pre-wrap";
        content.appendChild(body);

        const otpCard = createOtpCard(item.activeOtp);
        if (otpCard) content.appendChild(otpCard);

        const meta = element("div", "small text-secondary", formatDate(item.createdAt));
        if (item.emailAttempted && !item.emailSent) {
            meta.appendChild(element(
                "span",
                "ms-2 text-warning",
                " · Email chưa gửi được, nhưng thông báo realtime đã được ghi nhận."));
        }
        if (item.targetUrl) {
            const link = element("a", "ms-2", "Xem cảnh báo →");
            link.href = item.targetUrl;
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
        if (items.length === 0) {
            const card = element("div", "card border-0 shadow-sm");
            card.appendChild(element(
                "div",
                "card-body text-center text-muted py-5",
                "Chưa có thông báo"));
            root.appendChild(card);
            return;
        }

        const list = element("div", "list-group shadow-sm");
        items.forEach(function (item) {
            list.appendChild(renderItem(item));
        });
        root.appendChild(list);
        root.appendChild(element(
            "small",
            "d-block text-muted mt-3",
            `Trang ${data.page || 1} · Tổng ${data.total || items.length}`));
        updateOtpCards();
    }

    window.addEventListener("admin-notification-list-updated", function (event) {
        if (event.detail) render(event.detail);
    });

    updateOtpCards();
    window.setInterval(updateOtpCards, 1000);
})();
