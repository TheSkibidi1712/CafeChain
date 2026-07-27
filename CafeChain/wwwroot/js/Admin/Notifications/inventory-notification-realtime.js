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
        const isScheduleGap = message.type === "STAFF_SCHEDULE_GAP";
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
                title: isScheduleGap
                    ? "Thiếu lịch nhân sự"
                    : message.changeKind === "Escalated"
                        ? "Cảnh báo kho đã tăng mức độ"
                        : "Có thông báo kho mới",
                text: isScheduleGap
                    ? "Mở mục Thông báo để xem ca còn thiếu người và nhân viên phù hợp."
                    : "Mở mục Thông báo để xem chi tiết."
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

    connection.onreconnected(function () {
        void refresh();
    });

    void refresh();
    window.setInterval(refresh, 60000);
    connection.start().catch(function (error) {
        console.warn("[admin-notifications] SignalR unavailable; polling remains active.", error);
    });
})();
