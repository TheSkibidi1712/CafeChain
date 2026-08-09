(function (window, document) {
    "use strict";

    const supportedTypes = new Set(["success", "warning", "error", "info"]);
    const icons = {
        success: "fa-check-circle",
        warning: "fa-exclamation-triangle",
        error: "fa-times-circle",
        info: "fa-info-circle"
    };

    const statusMessages = {
        401: "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
        403: "Bạn không có quyền thực hiện thao tác này.",
        409: "Dữ liệu vừa được thay đổi bởi một thao tác khác. Vui lòng tải lại và thử lại."
    };

    const errorCodeMessages = {
        REQUEST_ALREADY_PROCESSING: "Yêu cầu đang được xử lý. Vui lòng chờ trong giây lát.",
        REQUEST_KEY_PAYLOAD_MISMATCH: "Yêu cầu này đã được dùng với nội dung khác. Vui lòng tải lại và thử lại.",
        CONCURRENCY_CONFLICT: "Dữ liệu vừa được thay đổi. Vui lòng tải lại và thử lại.",
        STAFF_SCOPE_FORBIDDEN: "Bạn không có quyền thao tác trên cửa hàng này.",
        STORE_SCOPE_DENIED: "Bạn không có quyền thao tác trên cửa hàng này.",
        OTP_INVALID: "Mã OTP không chính xác. Vui lòng kiểm tra và thử lại.",
        OTP_EXPIRED: "Mã OTP đã hết hạn. Vui lòng gửi lại yêu cầu OTP.",
        OTP_VERIFICATION_LOCKED: "Yêu cầu OTP đã bị khóa do nhập sai quá nhiều lần.",
        TERMINAL_ALREADY_HAS_OPEN_SHIFT: "Terminal đang có phiên POS chưa kết thúc.",
        STAFF_ALREADY_HAS_OPEN_SHIFT: "Bạn đang chịu trách nhiệm một phiên POS chưa kết thúc."
    };

    function decodeHtmlEntities(text) {
        try {
            const doc = new DOMParser().parseFromString(text, "text/html");
            return doc.documentElement.textContent;
        } catch {
            return text;
        }
    }

    function cleanMessage(value) {
        if (typeof value !== "string") return "";
        var str = value;
        if (str.indexOf("<!DOCTYPE") !== -1 || str.indexOf("<html") !== -1 || str.indexOf("<style") !== -1) {
            var titleMatch = str.match(/<title[^>]*>([\s\S]*?)<\/title>/i);
            if (titleMatch && titleMatch[1]) {
                var t = titleMatch[1].replace(/- CafeChain/gi, "").replace(/<[^>]*>/g, "").trim();
                t = decodeHtmlEntities(t);
                if (t && t.length < 100) return t;
            }
            return "Không có quyền truy cập hoặc hệ thống gặp sự cố.";
        }
        const text = str.replace(/<style[^>]*>[\s\S]*?<\/style>/gi, "")
                        .replace(/<script[^>]*>[\s\S]*?<\/script>/gi, "")
                        .replace(/<[^>]*>/g, " ")
                        .replace(/\s+/g, " ")
                        .trim();
        if (!text || /stack trace|sql(exception| error)|system\.[a-z.]+exception/i.test(text)) return "";
        return decodeHtmlEntities(text.slice(0, 500));
    }

    function toast(message, type = "success", options = {}) {
        const normalizedType = supportedTypes.has(type) ? type : "info";
        const normalizedMessage = cleanMessage(message) || "Không thể xác định kết quả thao tác.";
        const container = document.getElementById("toast-container");
        if (!container) return null;

        const existingItems = container.querySelectorAll(".toast-text");
        for (let i = 0; i < existingItems.length; i++) {
            if (existingItems[i].textContent === normalizedMessage) {
                return null;
            }
        }

        const item = document.createElement("div");
        item.className = `toast-item ${normalizedType}`;
        item.setAttribute("role", normalizedType === "error" ? "alert" : "status");
        item.setAttribute("aria-live", normalizedType === "error" ? "assertive" : "polite");
        item.tabIndex = -1;

        const content = document.createElement("div");
        content.className = "toast-content";
        const iconWrap = document.createElement("div");
        iconWrap.className = "toast-icon";
        iconWrap.setAttribute("aria-hidden", "true");
        const icon = document.createElement("i");
        icon.className = `fa ${icons[normalizedType]}`;
        iconWrap.appendChild(icon);
        const text = document.createElement("div");
        text.className = "toast-text";
        text.textContent = normalizedMessage;
        content.append(iconWrap, text);
        const progress = document.createElement("div");
        progress.className = "toast-progress";
        item.append(content, progress);
        container.appendChild(item);

        const duration = Number.isFinite(options.duration) ? Math.max(1500, options.duration) : 4000;
        window.setTimeout(() => {
            item.classList.add("fade-out");
            window.setTimeout(() => item.remove(), 300);
        }, duration);
        return item;
    }

    function actionFallback(action, entityName) {
        const entity = cleanMessage(entityName) || "dữ liệu";
        switch ((action || "").toLowerCase()) {
            case "create": return `Không thể tạo ${entity}. Dữ liệu chưa được lưu.`;
            case "update": return `Không thể cập nhật ${entity}. Các thay đổi chưa được lưu.`;
            case "delete": return `Không thể xóa ${entity}. Dữ liệu có thể đang được sử dụng.`;
            case "load": return `Không thể tải ${entity}. Vui lòng tải lại trang và thử lại.`;
            default: return "Không thể thực hiện thao tác. Vui lòng thử lại.";
        }
    }

    function resolveMessage(payload, options = {}) {
        const status = Number(options.status || payload?.status || 0);
        if (statusMessages[status]) return statusMessages[status];
        const errorCode = cleanMessage(payload?.errorCode || payload?.code);
        if (errorCode && errorCodeMessages[errorCode]) return errorCodeMessages[errorCode];
        const direct = cleanMessage(payload?.message);
        if (direct) return direct;
        const validation = payload?.errors;
        if (validation && typeof validation === "object") {
            const first = Object.values(validation).flat().map(cleanMessage).find(Boolean);
            if (first) return first;
        }
        if (status >= 500) {
            const correlationId = cleanMessage(payload?.correlationId || options.correlationId);
            return correlationId
                ? `Hệ thống đang gặp lỗi. Vui lòng thử lại sau. Mã tra cứu: ${correlationId}`
                : "Hệ thống đang gặp lỗi. Vui lòng thử lại sau.";
        }
        return cleanMessage(options.fallback) || actionFallback(options.action, options.entityName);
    }

    function networkMessage() {
        return "Không thể kết nối máy chủ. Vui lòng kiểm tra mạng và thử lại.";
    }

    window.toast = toast;
    window.AdminFeedback = Object.freeze({
        toast,
        cleanMessage,
        resolveMessage,
        actionFallback,
        networkMessage
    });
})(window, document);
