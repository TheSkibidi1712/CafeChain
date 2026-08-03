(function () {
    "use strict";

    const bell = document.querySelector("[data-admin-notification-bell]");
    const badge = document.getElementById("adminNotificationBadge");
    const signalRClient = window.signalR;
    if (!bell || !badge || !signalRClient) return;

    const unreadUrl = bell.dataset.unreadUrl;
    const listUrl = bell.dataset.listUrl;
    const seenEventIds = new Set();
    let refreshPromise = null;

    function remember(eventId) {
        if (!eventId || seenEventIds.has(eventId)) return false;
        seenEventIds.add(eventId);
        if (seenEventIds.size > 200) {
            const oldest = seenEventIds.values().next().value;
            if (oldest) seenEventIds.delete(oldest);
        }
        return true;
    }

    async function getJson(url) {
        if (!url) return null;
        const response = await fetch(url, {
            credentials: "same-origin",
            headers: {
                "Accept": "application/json",
                "X-Requested-With": "XMLHttpRequest"
            }
        });
        return response.ok ? response.json() : null;
    }

    async function refresh() {
        if (refreshPromise) return refreshPromise;
        refreshPromise = (async function () {
            try {
                const unreadPayload = await getJson(unreadUrl);
                const count = Number(unreadPayload?.data?.unreadCount || 0);
                badge.textContent = count > 99 ? "99+" : String(count);
                badge.classList.toggle("d-none", count <= 0);

                if (document.querySelector("[data-admin-notification-list]")) {
                    const listPayload = await getJson(listUrl);
                    window.dispatchEvent(new CustomEvent(
                        "admin-notification-list-updated",
                        { detail: listPayload?.data || null }));
                }
            } catch {
                // Polling remains the fallback; notification failures must not break Admin navigation.
            } finally {
                refreshPromise = null;
            }
        })();
        return refreshPromise;
    }

    function showRealtimeToast(message) {
        if (!message?.shouldToast || message.changeKind === "Resolved") return;
        if (window.Swal) {
            void window.Swal.fire({
                toast: true,
                position: "top-end",
                timer: 5000,
                timerProgressBar: true,
                showConfirmButton: false,
                icon: message.severity === "URGENT" || message.severity === "CRITICAL"
                    ? "error"
                    : "warning",
                title: message.changeKind === "Escalated"
                    ? "Cảnh báo kho đã tăng mức độ"
                    : "Có thông báo mới",
                text: "Mở mục Thông báo để xem chi tiết."
            });
        }
    }

    function showOperationalOtp(message) {
        if (!message?.eventId || !remember(message.eventId)) return;
        void refresh();

        const expiresAt = new Date(message.expiresAtUtc);
        const remainingMs = expiresAt.getTime() - Date.now();
        if (!Number.isFinite(remainingMs) || remainingMs <= 0 || !window.Swal) return;

        void window.Swal.fire({
            icon: "warning",
            title: "Yêu cầu phê duyệt POS",
            html: '<div class="text-start">' +
                '<p id="operationalOtpContext" class="mb-2"></p>' +
                '<div class="d-flex align-items-center justify-content-between gap-2 p-3 rounded bg-light">' +
                '<code id="operationalOtpCode" class="fs-3 fw-bold"></code>' +
                '<button id="copyOperationalOtp" type="button" class="btn btn-sm btn-outline-primary">Sao chép mã</button>' +
                '</div><small id="operationalOtpExpiry" class="d-block mt-2 text-muted"></small></div>',
            showConfirmButton: true,
            confirmButtonText: "Đã hiểu",
            timer: remainingMs,
            timerProgressBar: true,
            didOpen: function () {
                const context = document.getElementById("operationalOtpContext");
                const code = document.getElementById("operationalOtpCode");
                const expiry = document.getElementById("operationalOtpExpiry");
                if (context) context.textContent = `${message.requesterName} yêu cầu ${message.actionLabel} tại ${message.storeName}.`;
                if (code) code.textContent = message.otpCode;
                if (expiry) expiry.textContent = `Mã hết hạn lúc ${expiresAt.toLocaleTimeString("vi-VN")}.`;
                document.getElementById("copyOperationalOtp")?.addEventListener("click", async function () {
                    try {
                        await navigator.clipboard.writeText(message.otpCode);
                        this.textContent = "Đã sao chép";
                    } catch {
                        this.textContent = "Không thể sao chép";
                    }
                });
            }
        });
    }

    const connection = new signalRClient.HubConnectionBuilder()
        .withUrl("/hubs/inventory-notifications")
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(signalRClient.LogLevel.Warning)
        .build();

    connection.on("InventoryNotificationChanged", function (message) {
        if (!remember(message?.eventId)) return;
        void refresh();
        showRealtimeToast(message);
        window.dispatchEvent(new CustomEvent(
            "admin-notifications-changed",
            { detail: message }));
    });

    connection.on("OperationalOtpNotificationChanged", function (message) {
        if (!remember(message?.eventId)) return;
        void refresh();
        window.dispatchEvent(new CustomEvent("admin-notifications-changed", { detail: message }));
    });

    connection.on("OperationalOtpIssued", showOperationalOtp);

    connection.onreconnected(function () {
        void refresh();
    });

    void refresh();
    window.setInterval(refresh, 60000);
    window.addEventListener("admin-active-otp-expired", function () {
        void refresh();
    });
    connection.start().catch(function (error) {
        console.warn("[admin-notifications] SignalR unavailable; polling remains active.", error);
    });
})();
