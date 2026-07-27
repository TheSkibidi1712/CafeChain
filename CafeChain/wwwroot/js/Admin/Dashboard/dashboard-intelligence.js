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
    const supportedChartTypes = new Set([
        "Kpi", "Line", "Bar", "HorizontalBar", "Donut", "StackedBar", "Heatmap", "Scatter", "Table"
    ]);
    const horizontalBarVisibleRows = 12;
    const defaultLabels = {
        bucketDate: "Ngày", storeName: "Cửa hàng", totalOrders: "Tổng đơn",
        netSales: "Doanh thu thuần", drinkName: "Sản phẩm", productRevenue: "Doanh thu sản phẩm",
        ingredientName: "Nguyên liệu", availableQuantity: "Tồn khả dụng", shortageQuantity: "Mức thiếu",
        suggestedQuantity: "Số lượng đề xuất", supplierName: "Nhà cung cấp",
        rejectionRate: "Tỷ lệ từ chối", paymentMethodName: "Phương thức thanh toán",
        amount: "Giá trị", hourOfDay: "Giờ", isoWeekday: "Thứ", status: "Trạng thái",
        confirmedCogs: "Giá vốn hàng bán (COGS)", confirmedGrossProfit: "Lợi nhuận gộp",
        confirmedMarginRate: "Biên lợi nhuận", cancellationRate: "Tỷ lệ hủy",
        completedOrders: "Đơn hoàn tất", cancelledOrders: "Đơn hủy", dataStatus: "Chất lượng dữ liệu",
        alertValue: "Giá trị cảnh báo", issueCount: "Số sự cố", spend: "Chi phí mua",
        averageBaseUnitCost: "Giá mua bình quân", wasteValue: "Giá trị hao hụt",
        quantity: "Số lượng", volume: "Số lượng bán", fullName: "Nhân viên"
    };
    const statusLabels = {
        Complete: "Đầy đủ", Partial: "Một phần", Insufficient: "Chưa đủ dữ liệu",
        AVAILABLE: "Có dữ liệu", NO_DATA: "Không có dữ liệu", PARTIAL_COGS: "Thiếu dữ liệu giá vốn",
        Fallback: "Chế độ dự phòng", Available: "Sẵn sàng"
    };
    const aiStatusLabels = { Available: "AI khả dụng", Fallback: "Chế độ dự phòng" };
    const priorityLabels = { Critical: "Nghiêm trọng", High: "Cao", Medium: "Trung bình", Low: "Thấp" };
    const severityLabels = { CRITICAL: "Nghiêm trọng", URGENT: "Khẩn cấp", WARNING: "Cảnh báo", INFO: "Thông tin" };
    const trendLabels = {
        Increasing: "Tăng", Decreasing: "Giảm", Stable: "Ổn định",
        MixedIncreasing: "Biến động nhưng tăng", MixedDecreasing: "Biến động nhưng giảm",
        Mixed: "Biến động", Insufficient: "Chưa đủ dữ liệu"
    };
    let activeController = null;
    let requestSequence = 0;
    const chartInstances = [];

    function element(tag, className, text) {
        const node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined) node.textContent = text;
        return node;
    }

    function clamp(value, minimum, maximum) {
        return Math.min(maximum, Math.max(minimum, value));
    }

    function escapeHtml(value) {
        return String(value ?? "")
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll("\"", "&quot;")
            .replaceAll("'", "&#039;");
    }

    function isCompactViewport() {
        return window.matchMedia?.("(max-width: 720px)")?.matches === true;
    }

    function chartCanvasHeight(chartType, rowCount) {
        if (chartType !== "HorizontalBar") return 380;
        const compact = isCompactViewport();
        const minimum = compact ? 360 : 380;
        const maximum = compact ? 400 : 440;
        const rowHeight = compact ? 22 : 26;
        return clamp(
            120 + Math.min(Math.max(rowCount, 1), horizontalBarVisibleRows) * rowHeight,
            minimum,
            maximum
        );
    }

    function showStatus(message, isError = false) {
        status.hidden = !message;
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

    function disposeCharts() {
        chartInstances.splice(0).forEach(instance => {
            if (!instance?.isDisposed?.()) instance.dispose();
        });
    }

    function formatUnit(value, unit = "") {
        if (value === null || value === undefined || value === "") return "—";
        if (typeof value === "boolean") return value ? "Có" : "Không";
        const number = Number(value);
        if (!Number.isFinite(number)) return String(value);
        const normalized = String(unit || "").toUpperCase();
        if (normalized === "VND")
            return new Intl.NumberFormat("vi-VN", {
                style: "currency", currency: "VND", maximumFractionDigits: 0
            }).format(number);
        if (normalized === "PERCENT")
            return `${new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 2 }).format(number * 100)}%`;
        const formatted = new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 2 }).format(number);
        if (normalized === "ORDER") return `${formatted} đơn`;
        if (normalized === "COUNT") return formatted;
        if (normalized && !["DAY", "HOUR", "PRODUCT", "INGREDIENT"].includes(normalized))
            return `${formatted} ${normalized.toLowerCase()}`;
        if (normalized === "DAY") return `${formatted} ngày`;
        if (normalized === "HOUR") return `${formatted} giờ`;
        return formatted;
    }

    function fieldLabel(chart, key) {
        return chart?.fieldLabels?.[key] || defaultLabels[key] || key;
    }

    function localizedLabel(value, map, fallback = "Không xác định") {
        if (value === null || value === undefined || value === "") return fallback;
        return map[value] || map[String(value).toUpperCase()] || String(value);
    }

    function evidenceMap(data) {
        return new Map([...(data.facts || []), ...(data.statistics || [])]
            .filter(item => item?.evidenceId)
            .map(item => [item.evidenceId, item]));
    }

    function renderSourceViewer(ids, evidence) {
        const valid = (ids || []).map(id => evidence.get(id)).filter(Boolean);
        if (!valid.length) return null;
        const details = element("details", "dashboard-intelligence__source");
        details.append(element("summary", "", "Xem nguồn dữ liệu"));
        valid.forEach(item => {
            const row = element("div", "dashboard-intelligence__source-row");
            row.append(element("strong", "", item.evidenceId));
            row.append(element("span", "", item.statement || item.title || ""));
            if (item.entityName)
                row.append(element("span", "", `Đối tượng: ${item.entityName}${item.storeName ? ` — ${item.storeName}` : ""}`));
            row.append(element("span", "", `${defaultLabels[item.metricName] || item.metricName || "Giá trị"}: ${formatUnit(item.currentValue, item.unit)}`));
            details.append(row);
        });
        return details;
    }

    function renderNarratives(title, items, evidence, cssClass) {
        const section = element("section", "dashboard-intelligence__evidence");
        section.append(element("h3", "", title));
        if (!Array.isArray(items) || items.length === 0) {
            section.append(element("p", "text-muted", "Không có mục nào."));
            return section;
        }
        items.forEach(item => {
            const card = element("article", cssClass || "dashboard-intelligence__inference");
            if (item?.priority)
                card.append(element("span", `dashboard-intelligence__priority is-${String(item.priority).toLowerCase()}`,
                    localizedLabel(item.priority, priorityLabels)));
            card.append(element("p", "", item?.text || item?.statement || item?.message || ""));
            if (item?.verifyCondition)
                card.append(element("p", "dashboard-intelligence__verify", `Điều kiện kiểm tra: ${item.verifyCondition}`));
            const ids = item?.evidenceIds || (item?.evidenceId ? [item.evidenceId] : []);
            if (ids.length) {
                card.append(element("small", "text-muted", `Nguồn bằng chứng: ${ids.join(", ")}`));
                const source = renderSourceViewer(ids, evidence);
                if (source) card.append(source);
            }
            section.append(card);
        });
        return section;
    }

    function renderEvidence(title, items, evidence) {
        const section = element("section", "dashboard-intelligence__evidence");
        section.append(element("h3", "", title));
        if (!Array.isArray(items) || items.length === 0) {
            section.append(element("p", "text-muted", "Không có số liệu phù hợp."));
            return section;
        }
        items.slice(0, 30).forEach(item => {
            const card = element("article", "dashboard-intelligence__fact");
            card.append(element("strong", "", item.title || item.metricName || "Số liệu"));
            card.append(element("p", "", item.statement || formatUnit(item.currentValue, item.unit)));
            card.append(element("small", "text-muted",
                `${item.evidenceId} · ${localizedLabel(item.dataStatus, statusLabels, "Không xác định")}`));
            const source = renderSourceViewer([item.evidenceId], evidence);
            if (source) card.append(source);
            section.append(card);
        });
        return section;
    }

    function localizedKeys(chart, rows) {
        if (!rows.length) return [];
        const preferred = [chart.xField, chart.yField, chart.valueField, chart.seriesField].filter(Boolean);
        return [...new Set([...preferred, ...Object.keys(rows[0])])]
            .filter(key => key !== "dataStatus")
            .slice(0, 10);
    }

    function renderTable(chart, rows, reason = "") {
        const wrapper = element("div", "dashboard-intelligence__table-wrap");
        if (reason) wrapper.append(element("p", "analytics-notice", `Hiển thị bảng thay thế: ${reason}`));
        if (!Array.isArray(rows) || rows.length === 0) {
            wrapper.append(element("p", "analytics-notice", "Không có dữ liệu trong kỳ."));
            return wrapper;
        }
        const keys = localizedKeys(chart, rows);
        const table = element("table", "dashboard-intelligence__table");
        const thead = document.createElement("thead");
        const header = document.createElement("tr");
        keys.forEach(key => header.append(element("th", "", fieldLabel(chart, key))));
        thead.append(header);
        table.append(thead);
        const tbody = document.createElement("tbody");
        rows.slice(0, 30).forEach(row => {
            const tr = document.createElement("tr");
            keys.forEach(key => {
                const unit = key === chart.valueField || key === chart.yField ? chart.yUnit : "";
                tr.append(element("td", "", formatUnit(row[key], unit)));
            });
            tbody.append(tr);
        });
        table.append(tbody);
        wrapper.append(table);
        return wrapper;
    }

    function validChartData(chart, rows) {
        if (!window.echarts) return "ECharts chưa sẵn sàng";
        if (!supportedChartTypes.has(chart.type) || chart.type === "Table" || chart.type === "Kpi")
            return "Loại dữ liệu phù hợp hơn với bảng";
        if (!Array.isArray(rows) || rows.length < Number(chart.minimumRows || 1))
            return "Chưa đủ số dòng tối thiểu";
        const required = chart.type === "Donut"
            ? [chart.xField, chart.valueField || chart.yField]
            : chart.type === "Heatmap"
                ? [chart.xField, chart.yField, chart.valueField]
                : [chart.xField, chart.yField || chart.valueField];
        if (required.some(field => !field || !rows.some(row => row[field] !== null && row[field] !== undefined)))
            return "Thiếu trường trục hoặc giá trị";
        const valueField = chart.valueField || chart.yField;
        if (!rows.some(row => Number.isFinite(Number(row[valueField]))))
            return "Toàn bộ giá trị đều rỗng";
        return "";
    }

    function buildSeries(chart, rows) {
        const valueField = chart.valueField || chart.yField;
        const tooltip = params => {
            const items = Array.isArray(params) ? params : [params];
            const heading = items[0]?.axisValueLabel || items[0]?.name || "";
            const lines = items.map(item => {
                const value = Array.isArray(item.value) ? item.value[item.value.length - 1] : item.value;
                return `${item.marker || ""}${escapeHtml(item.seriesName || fieldLabel(chart, valueField))}: ${escapeHtml(formatUnit(value, chart.yUnit))}`;
            });
            return `${heading ? `<strong>${escapeHtml(heading)}</strong><br>` : ""}${lines.join("<br>")}`;
        };
        if (chart.type === "Donut") {
            return {
                tooltip: { trigger: "item", formatter: tooltip },
                legend: { bottom: 0 },
                series: [{
                    type: "pie", radius: ["42%", "68%"],
                    data: rows.map(row => ({ name: String(row[chart.xField] ?? "Không xác định"), value: Number(row[valueField]) }))
                }]
            };
        }
        if (chart.type === "Heatmap") {
            const xValues = [...new Set(rows.map(row => String(row[chart.xField])))];
            const yValues = [...new Set(rows.map(row => String(row[chart.yField])))];
            const data = rows.map(row => [
                xValues.indexOf(String(row[chart.xField])),
                yValues.indexOf(String(row[chart.yField])),
                Number(row[valueField])
            ]);
            const values = data.map(item => item[2]).filter(Number.isFinite);
            return {
                tooltip: { formatter: params => `${xValues[params.value[0]]}, ${yValues[params.value[1]]}: ${formatUnit(params.value[2], chart.yUnit)}` },
                grid: { left: 55, right: 20, top: 20, bottom: 45 },
                xAxis: { type: "category", data: xValues, name: fieldLabel(chart, chart.xField) },
                yAxis: { type: "category", data: yValues, name: fieldLabel(chart, chart.yField) },
                visualMap: {
                    min: Math.min(...values), max: Math.max(...values), calculable: true,
                    orient: "horizontal", left: "center", bottom: 0
                },
                series: [{ type: "heatmap", data }]
            };
        }
        if (chart.type === "Scatter") {
            return {
                tooltip: {
                    formatter: params => `${params.data.name || ""}<br>${fieldLabel(chart, chart.xField)}: ${formatUnit(params.value[0], chart.xUnit)}<br>${fieldLabel(chart, chart.yField)}: ${formatUnit(params.value[1], chart.yUnit)}`
                },
                grid: { left: 70, right: 25, top: 20, bottom: 55 },
                xAxis: { type: "value", name: fieldLabel(chart, chart.xField) },
                yAxis: { type: "value", name: fieldLabel(chart, chart.yField) },
                series: [{
                    type: "scatter",
                    data: rows.map(row => ({
                        name: chart.seriesField ? String(row[chart.seriesField] ?? "") : "",
                        value: [Number(row[chart.xField]), Number(row[chart.yField])]
                    }))
                }]
            };
        }

        const horizontal = chart.type === "HorizontalBar";
        const rawCategories = rows.map(row => String(row[chart.xField] ?? "Không xác định"));
        const categoryCounts = rawCategories.reduce((counts, category) => {
            counts.set(category, (counts.get(category) || 0) + 1);
            return counts;
        }, new Map());
        const categories = horizontal && !chart.seriesField
            ? rawCategories.map((category, index) => {
                if (categoryCounts.get(category) === 1) return category;
                const row = rows[index];
                const scope = row.storeName || (row.storeId ? `Cửa hàng #${row.storeId}` : `#${index + 1}`);
                return `${category} — ${scope}`;
            })
            : [...new Set(rawCategories)];
        const seriesNames = chart.seriesField
            ? [...new Set(rows.map(row => String(row[chart.seriesField] ?? "Không xác định")))]
            : [chart.title];
        const useVerticalZoom = horizontal && categories.length > horizontalBarVisibleRows;
        const verticalZoomEnd = useVerticalZoom
            ? horizontalBarVisibleRows / categories.length * 100
            : 100;
        const compact = isCompactViewport();
        const longestCategoryLength = categories.reduce(
            (maximum, category) => Math.max(maximum, category.length),
            0
        );
        const horizontalGridLeft = clamp(
            longestCategoryLength * (compact ? 6 : 7) + 28,
            compact ? 120 : 150,
            compact ? 190 : 280
        );
        const series = seriesNames.map(name => ({
            name,
            type: chart.type === "Line" ? "line" : "bar",
            stack: chart.type === "StackedBar" ? "total" : undefined,
            smooth: chart.type === "Line",
            barMaxWidth: horizontal ? 18 : undefined,
            data: horizontal && !chart.seriesField
                ? rows.map(row => Number(row[valueField]) || 0)
                : categories.map(category => {
                const row = rows.find(item =>
                    String(item[chart.xField] ?? "Không xác định") === category
                    && (!chart.seriesField || String(item[chart.seriesField] ?? "Không xác định") === name));
                return row ? Number(row[valueField]) : 0;
                })
        }));
        const categoryAxis = { type: "category", data: categories, axisLabel: { interval: 0, rotate: horizontal ? 0 : 25 } };
        const horizontalCategoryAxis = {
            type: "category",
            data: categories,
            inverse: true,
            axisLabel: {
                interval: 0,
                hideOverlap: false,
                width: horizontalGridLeft - (compact ? 22 : 30),
                lineHeight: 16,
                overflow: "truncate",
                ellipsis: "…"
            }
        };
        const valueAxis = { type: "value", axisLabel: { formatter: value => formatUnit(value, chart.yUnit) } };
        return {
            tooltip: { trigger: "axis", formatter: tooltip },
            legend: chart.seriesField ? { bottom: 0 } : undefined,
            dataZoom: useVerticalZoom ? [
                {
                    type: "inside",
                    yAxisIndex: 0,
                    start: 0,
                    end: verticalZoomEnd,
                    filterMode: "none",
                    zoomOnMouseWheel: false,
                    moveOnMouseWheel: true,
                    moveOnMouseMove: true
                },
                {
                    type: "slider",
                    yAxisIndex: 0,
                    orient: "vertical",
                    start: 0,
                    end: verticalZoomEnd,
                    right: 4,
                    top: 20,
                    bottom: 35,
                    width: 14,
                    brushSelect: false,
                    showDetail: false
                }
            ] : undefined,
            grid: {
                left: horizontal ? horizontalGridLeft : 70,
                right: useVerticalZoom ? 48 : 25,
                top: 20,
                bottom: chart.seriesField ? 70 : 55,
                containLabel: false
            },
            xAxis: horizontal ? valueAxis : categoryAxis,
            yAxis: horizontal ? horizontalCategoryAxis : valueAxis,
            series
        };
    }

    function renderChart(chart) {
        const block = element("section", "dashboard-intelligence__chart");
        block.append(element("h3", "", chart.title || "Biểu đồ"));
        const rows = Array.isArray(chart.rows) ? chart.rows : [];
        const fallbackReason = validChartData(chart, rows);
        if (fallbackReason) {
            block.append(renderTable(chart, rows, fallbackReason));
            return block;
        }
        const canvas = element("div", "dashboard-intelligence__chart-canvas");
        canvas.style.height = `${chartCanvasHeight(chart.type, rows.length)}px`;
        canvas.dataset.chartType = chart.type;
        canvas.dataset.rowCount = String(rows.length);
        canvas.setAttribute("role", "img");
        canvas.setAttribute("aria-label", chart.title || "Biểu đồ phân tích");
        block.append(canvas);
        try {
            const instance = window.echarts.init(canvas);
            instance.setOption(buildSeries(chart, rows), { notMerge: true });
            chartInstances.push(instance);
        } catch {
            canvas.replaceWith(renderTable(chart, rows, "Không thể khởi tạo biểu đồ"));
        }
        return block;
    }

    let resizeFrame = 0;
    window.addEventListener("resize", () => {
        window.cancelAnimationFrame(resizeFrame);
        resizeFrame = window.requestAnimationFrame(() => {
            chartInstances.forEach(instance => {
                if (!instance || instance.isDisposed?.()) return;
                const canvas = instance.getDom();
                canvas.style.height = `${chartCanvasHeight(
                    canvas.dataset.chartType,
                    Number(canvas.dataset.rowCount || 0)
                )}px`;
                instance.resize();
            });
        });
    });

    function renderChartAnalyses(items, evidence) {
        return renderNarratives(
            "Phân tích biểu đồ",
            (items || []).map(item => ({
                text: item.summary,
                evidenceIds: (item.evidence || []).map(source => source.evidenceId)
            })),
            evidence,
            "dashboard-intelligence__inference"
        );
    }

    function renderResult(data) {
        disposeCharts();
        result.replaceChildren();
        toggleResult.disabled = false;
        setResultVisibility(true);
        preview.hidden = true;
        const evidence = evidenceMap(data);
        const period = data.dataPeriod || {};
        const stores = (data.stores || data.context?.stores || []).map(store => store.storeName).filter(Boolean);
        const meta = element("div", "dashboard-intelligence__meta");
        [
            `Kỳ Dashboard: ${String(period.from || "").slice(0, 10)} → ${String(period.to || "").slice(0, 10)}`,
            `Cửa hàng: ${stores.length ? stores.join(", ") : (data.storeIds || []).join(", ")}`,
            `Trạng thái dữ liệu: ${localizedLabel(data.dataStatus, statusLabels, "Không xác định")}`,
            `AI: ${localizedLabel(data.aiStatus, aiStatusLabels, "Chế độ dự phòng")}`,
            `Độ tin cậy: ${Math.round(Number(data.confidence || 0) * 100)}%`,
            data.analysisId ? `AnalysisId: ${data.analysisId}` : ""
        ].filter(Boolean).forEach(text => meta.append(element("span", "", text)));
        result.append(meta);

        const summary = element("section", "dashboard-intelligence__summary");
        summary.append(element("h3", "", "Tóm tắt"));
        summary.append(element("p", "", data.summary || "Không đủ dữ liệu để kết luận."));
        result.append(summary);
        result.append(renderEvidence("Số liệu chính", [...(data.facts || []), ...(data.statistics || [])], evidence));
        const analysisItems = Array.isArray(data.inferences) && data.inferences.length
            ? data.inferences
            : (data.overview || []);
        result.append(renderNarratives("Phân tích", analysisItems, evidence, "dashboard-intelligence__inference"));
        result.append(renderNarratives("Bất thường", data.anomalies || [], evidence, "dashboard-intelligence__anomaly"));
        result.append(renderNarratives("Khuyến nghị", data.recommendations || [], evidence, "dashboard-intelligence__recommendation"));

        const charts = element("section", "dashboard-intelligence__charts");
        charts.append(element("h3", "", "Biểu đồ và dữ liệu minh họa"));
        (data.charts || []).forEach(chart => charts.append(renderChart(chart)));
        result.append(charts);
        result.append(renderChartAnalyses(data.chartAnalyses || [], evidence));
        result.append(renderNarratives("Điểm đáng chú ý", data.notablePoints || data.anomalies || [],
            evidence, "dashboard-intelligence__inference"));
        result.append(renderNarratives("Kết luận", data.conclusions || [],
            evidence, "dashboard-intelligence__inference"));

        const warnings = [...(data.warnings || [])];
        if (data.dataStatus !== "Complete")
            warnings.unshift(`Chất lượng dữ liệu: ${localizedLabel(data.dataStatus, statusLabels, "Không xác định")}. Hãy xem giới hạn trước khi sử dụng khuyến nghị.`);
        if (data.aiStatus === "Fallback")
            warnings.unshift(`Ollama fallback: ${data.fallbackReason || "facts và biểu đồ backend vẫn được giữ nguyên."}`);
        if (warnings.length)
            result.append(renderNarratives(
                "Cảnh báo dữ liệu",
                warnings.map(text => ({ text })),
                evidence,
                "analytics-notice"
            ));
    }

    async function post(url, body, signal) {
        const response = await fetch(url, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "RequestVerificationToken": token,
                "X-Requested-With": "XMLHttpRequest"
            },
            body: JSON.stringify(body),
            credentials: "same-origin",
            signal
        });
        const envelope = await response.json().catch(() => ({}));
        if (!response.ok || envelope.success === false)
            throw new Error(envelope.message || "Không thể xử lý yêu cầu.");
        return envelope.data;
    }

    async function analyze() {
        const question = prompt.value.trim();
        if (question.length > 0 && question.length < 3) {
            showStatus("Vui lòng nhập câu hỏi từ 3 ký tự hoặc để trống để xem tổng quan.", true);
            return;
        }
        activeController?.abort();
        const controller = new AbortController();
        activeController = controller;
        const sequence = ++requestSequence;
        const contextId = root.dataset.contextId || "";
        const expectedFingerprint = root.dataset.filterFingerprint || "";
        analyzeButton.disabled = true;
        showStatus("Đang tải dữ liệu theo phạm vi quyền và xây dựng evidence...");
        try {
            const data = await post(root.dataset.aiAnalyze, {
                prompt: question,
                contextId: contextId || null,
                locale: "vi-VN"
            }, controller.signal);
            const contextStillCurrent = contextId === (root.dataset.contextId || "");
            const fingerprintStillCurrent = !expectedFingerprint
                || !data.filterFingerprint
                || expectedFingerprint === data.filterFingerprint;
            if (controller.signal.aborted || sequence !== requestSequence
                || !contextStillCurrent || !fingerprintStillCurrent)
                return;
            if (data.filterFingerprint)
                root.dataset.filterFingerprint = data.filterFingerprint;
            renderResult(data);
            showStatus(data.usedFallback
                ? "Đã hoàn tất bằng fallback an toàn; facts và biểu đồ backend vẫn khả dụng."
                : "Đã hoàn tất phân tích dựa trên evidence.");
        } catch (error) {
            if (error?.name !== "AbortError")
                showStatus(error instanceof Error ? error.message : String(error), true);
        } finally {
            if (sequence === requestSequence) {
                analyzeButton.disabled = false;
                if (activeController === controller) activeController = null;
            }
        }
    }

    window.addEventListener("cafechain:dashboard-context-changing", () => {
        requestSequence++;
        activeController?.abort();
        activeController = null;
        analyzeButton.disabled = false;
        showStatus("Phạm vi Dashboard đang thay đổi; yêu cầu AI cũ đã được hủy.");
    });
    window.addEventListener("cafechain:dashboard-context-changed", event => {
        root.dataset.filterFingerprint = event.detail?.filterFingerprint || "";
        result.replaceChildren();
        result.hidden = true;
        toggleResult.disabled = true;
    });
    toggleResult.addEventListener("click", () => {
        if (!toggleResult.disabled) setResultVisibility(result.hidden);
    });
    analyzeButton.addEventListener("click", () => void analyze());
    prompt.addEventListener("keydown", event => {
        if (event.key === "Enter") {
            event.preventDefault();
            void analyze();
        }
    });

    const url = new URL(window.location.href);
    const suggestedQuestion = url.searchParams.get("aiQuestion")?.trim();
    if (suggestedQuestion) {
        prompt.value = suggestedQuestion.slice(0, Number(prompt.maxLength) || 500);
        url.searchParams.delete("aiQuestion");
        window.history.replaceState({}, document.title, `${url.pathname}${url.search}${url.hash}`);
    }
})();
