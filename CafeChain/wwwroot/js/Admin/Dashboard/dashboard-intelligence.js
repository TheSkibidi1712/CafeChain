(() => {
    "use strict";

    const root = document.getElementById("dashboardRoot");
    const prompt = document.getElementById("dashboardAiPrompt");
    const analyzeButton = document.getElementById("dashboardAiParse");
    const status = document.getElementById("dashboardAiStatus");
    const preview = document.getElementById("dashboardAiPreview");
    const result = document.getElementById("dashboardAiResult");
    const toggleResult = document.getElementById("dashboardAiToggleResult");
    const toggleLabel = toggleResult?.querySelector("[data-ai-toggle-label]");
    if (!root || !prompt || !analyzeButton || !status || !preview || !result || !toggleResult || !toggleLabel) return;

    const token = document.querySelector(
        "#dashboardAntiForgery input[name='__RequestVerificationToken']"
    )?.value || "";
    const chartTypes = new Set(["Kpi", "Line", "Bar", "Heatmap", "Table"]);

    function applyQuestionFromGuide() {
        const url = new URL(window.location.href);
        const suggestedQuestion = url.searchParams.get("aiQuestion")?.trim();
        if (!suggestedQuestion) return;

        prompt.value = suggestedQuestion.slice(0, Number(prompt.maxLength) || 500);
        prompt.focus();
        url.searchParams.delete("aiQuestion");
        window.history.replaceState({}, document.title, `${url.pathname}${url.search}${url.hash}`);
    }

    async function post(url, body) {
        const response = await fetch(url, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "RequestVerificationToken": token,
                "X-Requested-With": "XMLHttpRequest"
            },
            body: JSON.stringify(body),
            credentials: "same-origin"
        });
        const envelope = await response.json().catch(() => ({}));
        if (!response.ok || envelope.success === false)
            throw new Error(envelope.message || "Không thể xử lý yêu cầu.");
        return envelope.data;
    }

    function showStatus(message, isError = false) {
        status.hidden = false;
        status.textContent = message;
        status.classList.toggle("is-error", isError);
    }

    function setResultVisibility(isVisible) {
        result.hidden = !isVisible;
        toggleResult.setAttribute("aria-expanded", String(isVisible));
        toggleLabel.textContent = isVisible ? "Ẩn phân tích" : "Hiện phân tích";
        toggleResult.querySelector("i")?.classList.toggle("bi-eye", isVisible);
        toggleResult.querySelector("i")?.classList.toggle("bi-eye-slash", !isVisible);
    }

    function element(tag, className, text) {
        const node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined) node.textContent = text;
        return node;
    }

    function format(value) {
        if (value === null || value === undefined) return "—";
        if (typeof value === "number")
            return new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 2 }).format(value);
        if (typeof value === "boolean") return value ? "Có" : "Không";
        return String(value);
    }

    function renderTable(rows) {
        if (!Array.isArray(rows) || rows.length === 0)
            return element("p", "analytics-notice", "Không có dữ liệu trong kỳ.");
        const table = element("table", "dashboard-intelligence__table");
        const keys = Object.keys(rows[0]).filter(key => key !== "dataStatus").slice(0, 8);
        const thead = document.createElement("thead");
        const head = document.createElement("tr");
        keys.forEach(key => head.append(element("th", "", key)));
        thead.append(head);
        table.append(thead);
        const tbody = document.createElement("tbody");
        rows.slice(0, 20).forEach(row => {
            const tr = document.createElement("tr");
            keys.forEach(key => tr.append(element("td", "", format(row[key]))));
            tbody.append(tr);
        });
        table.append(tbody);
        return table;
    }

    function renderEvidence(title, items, cssClass) {
        const section = element("section", "dashboard-intelligence__evidence");
        section.append(element("h3", "", title));
        if (!Array.isArray(items) || items.length === 0) {
            section.append(element("p", "text-muted", "Không có mục nào."));
            return section;
        }
        items.forEach(item => {
            const card = element("article", cssClass || "dashboard-intelligence__insight");
            card.append(element("strong", "", item.title || item.code || "Phân tích"));
            card.append(element("p", "", item.statement || item.message || item.text || ""));
            if (Array.isArray(item.evidenceIds) && item.evidenceIds.length)
                card.append(element("small", "text-muted", `Evidence: ${item.evidenceIds.join(", ")}`));
            section.append(card);
        });
        return section;
    }

    function renderResult(data) {
        result.replaceChildren();
        toggleResult.disabled = false;
        setResultVisibility(true);
        preview.hidden = true;

        const meta = element("div", "dashboard-intelligence__meta");
        const period = data.dataPeriod || {};
        [
            `Intent: ${data.intent}`,
            `Kỳ: ${(period.from || "").slice(0, 10)} – ${(period.to || "").slice(0, 10)}`,
            `Cửa hàng: ${(data.storeIds || []).length}`,
            `Dữ liệu: ${data.dataStatus}`,
            `AI: ${data.aiStatus}`,
            `Độ tin cậy: ${Math.round(Number(data.confidence || 0) * 100)}%`
        ].forEach(text => meta.append(element("span", "", text)));
        result.append(meta);

        const summary = element("section", "dashboard-intelligence__summary");
        summary.append(element("h3", "", "Tóm tắt"));
        summary.append(element("p", "", data.summary || "Không đủ dữ liệu để kết luận."));
        result.append(summary);
        result.append(renderEvidence("Fact", data.facts, "dashboard-intelligence__fact"));
        result.append(renderEvidence("Inference", data.inferences, "dashboard-intelligence__inference"));
        result.append(renderEvidence("Bất thường", data.anomalies, "dashboard-intelligence__anomaly"));
        result.append(renderEvidence("Khuyến nghị", data.recommendations, "dashboard-intelligence__recommendation"));

        (data.charts || []).forEach(chart => {
            if (!chartTypes.has(chart.type)) return;
            const block = element("section", "dashboard-intelligence__chart");
            block.append(element("h3", "", chart.title || "Thống kê"));
            block.append(renderTable(chart.rows));
            result.append(block);
        });

        if (Array.isArray(data.warnings) && data.warnings.length)
            result.append(renderEvidence(
                "Lưu ý",
                data.warnings.map(text => ({ text })),
                "analytics-notice"
            ));
    }

    toggleResult.addEventListener("click", () => {
        if (toggleResult.disabled) return;
        setResultVisibility(result.hidden);
    });

    async function analyze() {
        const question = prompt.value.trim();
        if (question.length < 3) {
            showStatus("Vui lòng nhập câu hỏi từ 3 ký tự.", true);
            return;
        }

        const fromDate = document.getElementById("dashboardFromDate")?.value || null;
        const toDate = document.getElementById("dashboardToDate")?.value || null;
        const storeValue = document.getElementById("dashboardStore")?.value || "";
        analyzeButton.disabled = true;
        showStatus("Đang lập kế hoạch dữ liệu và phân tích trong phạm vi được cấp quyền...");
        try {
            const data = await post(root.dataset.aiAnalyze, {
                prompt: question,
                locale: "vi-VN",
                fromDate,
                toDate,
                storeId: storeValue ? Number(storeValue) : null
            });
            renderResult(data);
            showStatus(data.usedFallback
                ? "Đã hoàn tất; một phần phân tích dùng fallback an toàn."
                : "Đã hoàn tất phân tích AI dựa trên evidence.");
        } catch (error) {
            showStatus(error instanceof Error ? error.message : String(error), true);
        } finally {
            analyzeButton.disabled = false;
        }
    }

    analyzeButton.addEventListener("click", () => void analyze());
    prompt.addEventListener("keydown", event => {
        if (event.key === "Enter") {
            event.preventDefault();
            void analyze();
        }
    });
    applyQuestionFromGuide();
})();
