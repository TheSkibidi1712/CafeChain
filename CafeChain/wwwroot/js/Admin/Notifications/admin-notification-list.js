(function () {
    "use strict";

    const root = document.querySelector("[data-admin-notification-list]");
    if (!root) return;

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
    }

    window.addEventListener("admin-notification-list-updated", function (event) {
        if (event.detail) render(event.detail);
    });
})();
