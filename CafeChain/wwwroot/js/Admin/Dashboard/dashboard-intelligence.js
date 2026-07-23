(() => {
    "use strict";
    const root = document.getElementById("dashboardRoot");
    const prompt = document.getElementById("dashboardAiPrompt");
    const parseButton = document.getElementById("dashboardAiParse");
    const status = document.getElementById("dashboardAiStatus");
    const preview = document.getElementById("dashboardAiPreview");
    const result = document.getElementById("dashboardAiResult");
    if (!root || !prompt || !parseButton || !status || !preview || !result) return;
    const token = document.querySelector("#dashboardAntiForgery input[name='__RequestVerificationToken']")?.value || "";
    let intent = null;
    let analysisId = null;

    const labels = {
        NetSalesTrend: "Doanh thu theo thời gian", StoreRanking: "Doanh thu theo cửa hàng",
        TopProducts: "Top sản phẩm", HourlyOrders: "Theo giờ",
        InventoryWasteByStoreIngredient: "Hao hụt kho", OverduePurchaseOrders: "PO quá hạn",
        SupplierQuality: "Chất lượng nhà cung cấp", WorkforceShiftStatus: "Tình trạng ca"
    };

    async function post(url, body) {
        const response = await fetch(url, {
            method: "POST", headers: { "Content-Type": "application/json", "RequestVerificationToken": token },
            body: JSON.stringify(body), credentials: "same-origin"
        });
        const data = await response.json().catch(() => ({}));
        if (!response.ok || data.success === false) throw new Error(data.message || "Không thể xử lý yêu cầu.");
        return data.data;
    }

    function showStatus(message, isError = false) {
        status.hidden = false; status.textContent = message;
        status.classList.toggle("is-error", isError);
    }

    function button(text, handler) {
        const value = document.createElement("button");
        value.type = "button"; value.className = "analytics-button"; value.textContent = text;
        value.addEventListener("click", handler); return value;
    }

    function renderIntent(value) {
        preview.replaceChildren(); preview.hidden = false;
        const title = document.createElement("strong"); title.textContent = labels[value.widget] || value.widget;
        const meta = document.createElement("div"); meta.className = "dashboard-intelligence__meta";
        [
            `Khoảng: ${value.period.type}${value.period.value ? ` (${value.period.value})` : ""}`,
            `So sánh: ${value.comparison}`, `Biểu đồ: ${value.chart}`,
            `Phạm vi: ${value.storeSelector.storeName || "Các cửa hàng được cấp quyền"}`
        ].forEach(text => { const span = document.createElement("span"); span.textContent = text; meta.append(span); });
        const actions = document.createElement("div"); actions.className = "dashboard-intelligence__actions";
        actions.append(button("Chạy thống kê", execute));
        preview.append(title, meta, actions);
    }

    function format(value) {
        if (value === null || value === undefined) return "—";
        if (typeof value === "number") return new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 2 }).format(value);
        return String(value);
    }

    function renderRows(rows) {
        if (!Array.isArray(rows) || rows.length === 0) { const empty = document.createElement("p"); empty.textContent = "Không có dữ liệu trong kỳ."; return empty; }
        const table = document.createElement("table"); table.className = "dashboard-intelligence__table";
        const keys = Object.keys(rows[0]).slice(0, 8);
        const head = document.createElement("tr"); keys.forEach(key => { const th = document.createElement("th"); th.textContent = key; head.append(th); });
        const thead = document.createElement("thead"); thead.append(head); table.append(thead);
        const tbody = document.createElement("tbody"); rows.slice(0, 20).forEach(row => {
            const tr = document.createElement("tr"); keys.forEach(key => { const td = document.createElement("td"); td.textContent = format(row[key]); tr.append(td); }); tbody.append(tr);
        }); table.append(tbody); return table;
    }

    async function execute() {
        if (!intent) return;
        showStatus("Đang truy vấn dữ liệu trong phạm vi được cấp quyền...");
        try {
            const data = await post(root.dataset.aiExecute, intent); analysisId = data.analysisId;
            result.replaceChildren(); result.hidden = false;
            const meta = document.createElement("div"); meta.className = "dashboard-intelligence__meta";
            meta.textContent = `${data.fromDate.slice(0, 10)} – ${data.toDate.slice(0, 10)} · ${data.storeIds.length} cửa hàng · ${data.dataStatus}`;
            result.append(meta);
            (data.insights || []).forEach(item => { const div = document.createElement("div"); div.className = "dashboard-intelligence__insight"; div.textContent = item.message; result.append(div); });
            result.append(renderRows(data.chart?.rows));
            const actions = document.createElement("div"); actions.className = "dashboard-intelligence__actions";
            actions.append(button("Giải thích AI", explain)); result.append(actions);
            showStatus("Đã hoàn tất thống kê.");
        } catch (error) { showStatus(error.message, true); }
    }

    async function explain() {
        if (!analysisId) return;
        showStatus("Đang tạo giải thích...");
        try {
            const data = await post(root.dataset.aiExplain, analysisId);
            const block = document.createElement("div"); block.className = "dashboard-intelligence__insight";
            block.textContent = data.explanation; result.prepend(block);
            showStatus(data.usedFallback ? "Đã dùng giải thích deterministic." : "Đã nhận giải thích AI.");
        } catch (error) { showStatus(error.message, true); }
    }

    parseButton.addEventListener("click", async () => {
        const value = prompt.value.trim(); if (value.length < 3) return showStatus("Vui lòng nhập câu hỏi từ 3 ký tự.", true);
        parseButton.disabled = true; showStatus("Đang phân tích câu hỏi...");
        try { const data = await post(root.dataset.aiParse, { prompt: value, locale: "vi-VN" }); if (!data.success || !data.intent) throw new Error(data.message); intent = data.intent; renderIntent(intent); showStatus(data.message); }
        catch (error) { showStatus(error.message, true); }
        finally { parseButton.disabled = false; }
    });
})();
