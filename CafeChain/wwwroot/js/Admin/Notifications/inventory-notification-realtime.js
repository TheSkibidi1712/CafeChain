(function () {
    "use strict";

    const bell = document.querySelector("[data-admin-notification-bell]");
    const badge = document.getElementById("adminNotificationBadge");
    const signalRClient = window.signalR;
    if (!bell || !badge || !signalRClient) return;

    const unreadUrl = bell.dataset.unreadUrl;
    const listUrl = bell.dataset.listUrl;
    const seenEventIds = new Set();
    const otpPopupStoragePrefix = "cafechain:operational-otp-popup:";
    let refreshPromise = null;

    function formatBadgeCount(count) {
        return count > 9 ? "9+" : String(count);
    }

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

    function normalizeUtc(value) {
        const timestamp = String(value || "").trim();
        if (!timestamp) return "";
        return /(z|[+-]\d\d:?\d\d)$/i.test(timestamp) ? timestamp : `${timestamp}Z`;
    }

    function otpPopupKey(item) {
        const otp = item?.operationalOtp;
        return otp?.expiresAtUtc
            ? `${otpPopupStoragePrefix}${item.notificationId}:${normalizeUtc(otp.expiresAtUtc)}`
            : null;
    }

    function wasOtpPopupShown(key) {
        if (!key) return true;
        try { return window.sessionStorage.getItem(key) === "1"; }
        catch { return false; }
    }

    function rememberOtpPopup(key) {
        if (!key) return;
        try { window.sessionStorage.setItem(key, "1"); }
        catch { /* Browser privacy mode may disable sessionStorage. */ }
    }

    function showOperationalOtpAttention(item) {
        const otp = item?.operationalOtp;
        if (!otp || otp.status !== "Waiting") return;
        const key = otpPopupKey(item);
        if (wasOtpPopupShown(key)) return;
        rememberOtpPopup(key);

        const target = new URL(bell.href, window.location.origin);
        target.hash = `notification-${item.notificationId}`;
        const title = item.title || "Có yêu cầu xác nhận POS mới";
        const text = `${otp.requestedByName || "Nhân viên"} gửi yêu cầu tại ${otp.storeName || "cửa hàng"}. Mở Thông báo để xem OTP.`;

        if (window.Swal) {
            void window.Swal.fire({
                toast: true,
                position: "top-end",
                timer: 12000,
                timerProgressBar: true,
                showConfirmButton: true,
                confirmButtonText: "Mở Thông báo",
                icon: "warning",
                title,
                text
            }).then((result) => {
                if (result.isConfirmed) window.location.assign(target.toString());
            });
            return;
        }
        if (typeof window.showToast === "function") window.showToast(text, "warning");
    }

    function inspectOperationalOtpNotifications(data) {
        const items = Array.isArray(data?.items) ? data.items : [];
        const unseen = items.find((item) => {
            const otp = item?.operationalOtp;
            return otp?.status === "Waiting" && !wasOtpPopupShown(otpPopupKey(item));
        });
        if (unseen) showOperationalOtpAttention(unseen);
    }

    async function refresh() {
        if (refreshPromise) return refreshPromise;
        refreshPromise = (async function () {
            try {
                const unreadPayload = await getJson(unreadUrl);
                const count = Number(unreadPayload?.data?.unreadCount || 0);
                badge.textContent = formatBadgeCount(count);
                badge.title = `${count} thông báo chưa đọc`;
                bell.setAttribute(
                    "aria-label",
                    count > 0
                        ? `Mở danh sách thông báo, ${count} thông báo chưa đọc`
                        : "Mở danh sách thông báo");
                badge.classList.toggle("d-none", count <= 0);

                const listPayload = await getJson(listUrl);
                const listData = listPayload?.data || null;
                inspectOperationalOtpNotifications(listData);
                if (document.querySelector("[data-admin-notification-list]")) {
                    window.dispatchEvent(new CustomEvent(
                        "admin-notification-list-updated",
                        { detail: listData }));
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
