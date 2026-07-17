(() => {
    "use strict";

    const root = document.getElementById("dashboardRoot");
    if (!root) return;

    const stores = JSON.parse(document.getElementById("dashboardStores")?.textContent || "[]");
    const panel = document.getElementById("dashboardPanel");
    const notice = document.getElementById("dashboardNotice");
    const applyButton = document.getElementById("dashboardApply");
    const fields = {
        from: document.getElementById("dashboardFromDate"), to: document.getElementById("dashboardToDate"),
        province: document.getElementById("dashboardProvince"), district: document.getElementById("dashboardDistrict"),
        store: document.getElementById("dashboardStore"), granularity: document.getElementById("dashboardGranularity"),
        top: document.getElementById("dashboardTop")
    };
    const cache = new Map();
    const charts = new Map();
    let activeSection = "Executive";
    let activeRequest = null;

    const sections = {
        Executive: [
            chart("netSalesTrend", "Doanh số thuần", "line", "bucketDate", "netSales", true),
            chart("storeRanking", "Xếp hạng cửa hàng", "bar", "storeName", "netSales"),
            chart("paymentMethodMix", "Cơ cấu thanh toán", "donut", "paymentMethodName", "amount"),
            chart("orderHeatmap", "Mật độ đơn theo giờ", "heatmap", "hourOfDay", "totalOrders", true),
            table("operationalAlerts", "Cảnh báo vận hành", ["severity", "alertType", "storeId", "alertValue", "message"], true)
        ],
        Operations: [
            kpi("kpis", "KPI ca làm việc", ["totalWorkShifts", "openWorkShifts", "exceptionClosedCount", "reconciliationCount", "absoluteCashDiscrepancy"], true),
            chart("cashDiscrepancy", "Chênh lệch két", "bar", "workShiftId", "cashDiscrepancy"),
            chart("shiftSales", "Doanh số theo ca", "bar", "workShiftId", "netSales"),
            chart("paymentMix", "Thanh toán theo ca", "bar", "paymentMethodName", "amount"),
            chart("hourlyOrders", "Đơn hàng theo giờ", "line", "hourOfDay", "totalOrders"),
            table("offlineReconciliation", "Đối soát offline", ["workShiftId", "storeId", "offlineOrderCountAtClose", "offlineEstimatedTotalAtClose", "requiresReconciliation", "hasLateOfflineSync"]),
            table("topDiscrepancies", "Ca chênh lệch lớn", ["workShiftId", "storeId", "staffId", "cashDiscrepancy", "discrepancyReason", "endTime"])
        ],
        Inventory: [
            chart("shortageRisk", "Nguy cơ thiếu hàng", "bar", "ingredientName", "availableQty"),
            chart("movement", "Biến động kho", "line", "movementDate", "quantity"),
            table("thresholdRisk", "Rủi ro ngưỡng tồn", ["storeId", "ingredientName", "availableQty", "minStockLevel", "maxNegativeQty"]),
            table("reorderSuggestions", "Đề xuất đặt lại", ["storeId", "ingredientName", "requestedQuantity", "suggestedQuantity", "priority", "status"]),
            chart("waste", "Hao hụt", "bar", "ingredientName", "wasteValue"),
            table("fifoAge", "Tuổi lớp giá FIFO", ["storeId", "ingredientId", "preparedItemId", "remainingQuantity", "ageDays", "remainingValue"])
        ],
        Procurement: [
            chart("purchaseOrderPipeline", "Pipeline đơn mua", "donut", "status", "orderedValue"),
            table("overduePurchaseOrders", "Đơn mua quá hạn", ["code", "storeId", "supplierName", "status", "expectedDeliveryAtUtc", "overdueDays"]),
            chart("supplierQuality", "Chất lượng nhà cung cấp", "bar", "supplierName", "rejectionRate"),
            chart("purchasePriceTrend", "Xu hướng giá mua", "line", "receiptDate", "averageBaseUnitCost"),
            chart("spendBreakdown", "Chi tiêu nhà cung cấp", "bar", "supplierName", "spend"),
            chart("supplierIssueMix", "Cơ cấu sự cố", "donut", "issueType", "issueCount")
        ],
        Product: [
            chart("topProducts", "Top sản phẩm", "bar", "drinkName", "productRevenue"),
            chart("volumeMargin", "Sản lượng và biên lợi nhuận", "scatter", "volume", "confirmedMarginRate"),
            chart("sizeMargin", "Lợi nhuận theo size", "bar", "sizeName", "confirmedGrossProfit"),
            chart("topToppings", "Top topping", "bar", "toppingName", "revenue"),
            table("bomHealth", "Sức khỏe BOM", ["drinkCode", "drinkName", "recipeCount", "recipeLineCount", "invalidLineCount"]),
            table("lowEfficiency", "Tiêu hao cao / hiệu quả thấp", ["drinkName", "totalSold", "confirmedCogs", "confirmedGrossProfit"])
        ],
        Workforce: [
            chart("shiftStatus", "Trạng thái phân ca", "bar", "statusCode", "staffShiftId"),
            chart("hourlyDemand", "Nhu cầu nhân sự theo giờ", "line", "hourOfDay", "ordersPerStaff"),
            table("staffPerformance", "Hiệu suất nhân viên", ["fullName", "storeId", "totalOrders", "netSales", "payrollHours", "salesPerPayrollHour"], true)
        ]
    };

    function chart(key, title, kind, label, value, wide = false) { return { key, title, kind, label, value, wide }; }
    function table(key, title, columns, wide = false) { return { key, title, kind: "table", columns, wide }; }
    function kpi(key, title, columns, wide = false) { return { key, title, kind: "kpi", columns, wide }; }

    function escapeHtml(value) {
        return String(value ?? "").replace(/[&<>'"]/g, character => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", "'": "&#39;", '"': "&quot;" })[character]);
    }

    function friendlyName(value) {
        return value.replace(/([a-z])([A-Z])/g, "$1 $2").replace(/^./, x => x.toUpperCase());
    }

    function format(value) {
        if (value === null || value === undefined || value === "") return "—";
        if (typeof value === "boolean") return value ? "Có" : "Không";
        if (typeof value === "number") return new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 2 }).format(value);
        if (/^\d{4}-\d{2}-\d{2}T/.test(value)) return new Date(value).toLocaleString("vi-VN");
        return String(value);
    }

    function distinct(items, id, name, selected) {
        const seen = new Set();
        return items.filter(item => item[id] != null && !seen.has(item[id]) && seen.add(item[id]))
            .map(item => `<option value="${item[id]}" ${String(item[id]) === String(selected || "") ? "selected" : ""}>${escapeHtml(item[name])}</option>`).join("");
    }

    function populateFilters(initial = false) {
        const selectedProvince = initial ? root.dataset.province : fields.province.value;
        const selectedDistrict = initial ? root.dataset.district : fields.district.value;
        const selectedStore = initial ? root.dataset.store : fields.store.value;
        fields.province.innerHTML = `<option value="">Tất cả</option>${distinct(stores, "provinceId", "provinceName", selectedProvince)}`;
        const districtStores = selectedProvince ? stores.filter(x => String(x.provinceId) === String(selectedProvince)) : stores;
        fields.district.innerHTML = `<option value="">Tất cả</option>${distinct(districtStores, "districtId", "districtName", selectedDistrict)}`;
        const storeOptions = districtStores.filter(x => !selectedDistrict || String(x.districtId) === String(selectedDistrict));
        fields.store.innerHTML = `<option value="">Tất cả</option>${distinct(storeOptions, "storeId", "storeName", selectedStore)}`;
    }

    function query(section) {
        const parameters = new URLSearchParams({ section, FromDate: fields.from.value, ToDate: fields.to.value, Granularity: fields.granularity.value, Top: fields.top.value || "10" });
        if (fields.province.value) parameters.set("ProvinceId", fields.province.value);
        if (fields.district.value) parameters.set("DistrictId", fields.district.value);
        if (fields.store.value) parameters.set("StoreId", fields.store.value);
        return parameters;
    }

    function renderSkeleton(section) {
        disposeCharts();
        panel.innerHTML = sections[section].map(widget => `<article class="analytics-widget ${widget.wide ? "is-wide" : ""}" data-widget="${widget.key}"><div class="analytics-widget__header"><h2>${widget.title}</h2></div><div class="analytics-skeleton"></div></article>`).join("");
    }

    async function loadSection(section, force = false) {
        activeSection = section;
        if (!force && cache.has(section)) { renderSection(section, cache.get(section)); return; }
        activeRequest?.abort();
        activeRequest = new AbortController();
        renderSkeleton(section);
        showNotice("");
        applyButton.disabled = true;
        try {
            const response = await fetch(`${root.dataset.endpoint}?${query(section)}`, { signal: activeRequest.signal, headers: { Accept: "application/json" } });
            const payload = await response.json();
            if (!response.ok || !payload.success) throw new Error(payload.message || "Không thể tải dữ liệu dashboard.");
            cache.set(section, payload.data);
            renderSection(section, payload.data);
        } catch (error) {
            if (error.name !== "AbortError") renderSectionError(section, error.message);
        } finally {
            applyButton.disabled = false;
        }
    }

    function renderSection(section, response) {
        disposeCharts();
        const data = response?.data || {};
        const warnings = [];
        panel.innerHTML = sections[section].map(widget => {
            const result = data[widget.key] || { status: "ERROR", message: "Payload không có widget này." };
            (result.warnings || []).forEach(item => warnings.push(`${widget.title}: ${item}`));
            return widgetShell(widget, result);
        }).join("");
        sections[section].forEach(widget => renderWidget(widget, data[widget.key]));
        showNotice(warnings.length ? `Dữ liệu một phần: ${warnings.join(" · ")}` : "");
    }

    function widgetShell(widget, result) {
        const badge = result?.warnings?.length ? `<span class="analytics-badge">Dữ liệu một phần</span>` : "";
        return `<article class="analytics-widget ${widget.wide ? "is-wide" : ""} ${widget.kind === "kpi" ? "is-compact" : ""}" data-widget="${widget.key}"><div class="analytics-widget__header"><h2>${widget.title}</h2>${badge}</div><div class="analytics-widget__body" id="widget-${widget.key}"></div></article>`;
    }

    function renderWidget(widget, result) {
        const target = document.getElementById(`widget-${widget.key}`);
        if (!target) return;
        if (!result || result.status === "ERROR") {
            target.innerHTML = state("Không tải được widget", result?.message || "Lỗi không xác định", true);
            target.querySelector("button")?.addEventListener("click", () => loadSection(activeSection, true));
            return;
        }
        const rows = Array.isArray(result.data) ? result.data : [];
        if (result.status === "NO_DATA" || rows.length === 0) { target.innerHTML = state("Chưa có dữ liệu", "Hãy thử đổi thời gian hoặc phạm vi cửa hàng."); return; }
        if (widget.kind === "table") renderTable(target, rows, widget.columns);
        else if (widget.kind === "kpi") renderKpis(target, rows[0], widget.columns);
        else renderChart(target, rows, widget);
    }

    function state(title, message, retry = false) {
        return `<div class="analytics-state"><div><strong>${escapeHtml(title)}</strong><span>${escapeHtml(message)}</span>${retry ? '<br><button type="button" class="analytics-retry">Thử lại</button>' : ""}</div></div>`;
    }

    function renderTable(target, rows, columns) {
        target.innerHTML = `<div class="analytics-table-wrap"><table class="analytics-table"><thead><tr>${columns.map(x => `<th>${friendlyName(x)}</th>`).join("")}</tr></thead><tbody>${rows.map(row => `<tr>${columns.map(x => `<td>${escapeHtml(format(row[x]))}</td>`).join("")}</tr>`).join("")}</tbody></table></div>`;
    }

    function renderKpis(target, row, columns) {
        target.innerHTML = `<div class="analytics-kpis">${columns.map(key => `<div class="analytics-kpi"><span>${friendlyName(key)}</span><strong>${escapeHtml(format(row[key]))}</strong></div>`).join("")}</div>`;
    }

    function renderChart(target, rows, widget) {
        if (!window.echarts) { target.innerHTML = state("Thiếu ECharts", "Không thể khởi tạo biểu đồ."); return; }
        const element = document.createElement("div");
        element.className = "analytics-chart";
        target.appendChild(element);
        const instance = window.echarts.init(element);
        charts.set(widget.key, instance);
        instance.setOption(chartOption(rows, widget));
    }

    function chartOption(rows, widget) {
        const base = { animationDuration: 350, textStyle: { fontFamily: "Segoe UI, sans-serif" }, tooltip: { trigger: "axis" }, grid: { left: 58, right: 22, top: 28, bottom: 62, containLabel: true } };
        if (widget.kind === "donut") return { ...base, tooltip: { trigger: "item" }, legend: { bottom: 0, type: "scroll" }, series: [{ type: "pie", radius: ["45%", "70%"], data: rows.map(row => ({ name: format(row[widget.label]), value: Number(row[widget.value] || 0) })), itemStyle: { borderColor: "#fff", borderWidth: 2 } }] };
        if (widget.kind === "scatter") return { ...base, xAxis: { type: "value", name: friendlyName(widget.label) }, yAxis: { type: "value", name: friendlyName(widget.value) }, series: [{ type: "scatter", symbolSize: 14, data: rows.map(row => [Number(row[widget.label] || 0), Number(row[widget.value] || 0), row.drinkName]), tooltip: { formatter: params => `${escapeHtml(params.data[2])}<br>${format(params.data[0])} · ${format(params.data[1])}` } }] };
        if (widget.kind === "heatmap") {
            const values = rows.map(row => [Number(row.hourOfDay), Number(row.isoWeekday) - 1, Number(row[widget.value] || 0)]);
            const max = Math.max(1, ...values.map(x => x[2]));
            return { ...base, tooltip: { position: "top", formatter: x => `Thứ ${x.data[1] + 1}, ${x.data[0]}h: ${format(x.data[2])}` }, xAxis: { type: "category", data: Array.from({ length: 24 }, (_, x) => `${x}h`), splitArea: { show: true } }, yAxis: { type: "category", data: ["T2", "T3", "T4", "T5", "T6", "T7", "CN"], splitArea: { show: true } }, visualMap: { min: 0, max, calculable: true, orient: "horizontal", left: "center", bottom: 0 }, series: [{ type: "heatmap", data: values }] };
        }
        const labels = rows.map(row => format(row[widget.label]));
        return { ...base, xAxis: { type: "category", data: labels, axisLabel: { interval: 0, rotate: labels.length > 8 ? 25 : 0, width: 100, overflow: "truncate" } }, yAxis: { type: "value" }, series: [{ type: widget.kind, smooth: widget.kind === "line", showSymbol: rows.length < 20, areaStyle: widget.kind === "line" ? { opacity: .08 } : undefined, data: rows.map(row => Number(row[widget.value] || 0)), itemStyle: { color: "#166534" }, lineStyle: { color: "#166534", width: 3 } }] };
    }

    function renderSectionError(section, message) {
        disposeCharts();
        panel.innerHTML = `<article class="analytics-widget is-wide"><div class="analytics-state"><div><strong>Không tải được tab ${escapeHtml(section)}</strong><span>${escapeHtml(message)}</span><br><button type="button" class="analytics-retry">Thử lại</button></div></div></article>`;
        panel.querySelector("button")?.addEventListener("click", () => loadSection(section, true));
    }

    function showNotice(message) { notice.hidden = !message; notice.textContent = message; }
    function disposeCharts() { charts.forEach(chartInstance => chartInstance.dispose()); charts.clear(); }

    document.querySelectorAll(".analytics-tab").forEach(button => button.addEventListener("click", () => {
        document.querySelectorAll(".analytics-tab").forEach(item => { item.classList.toggle("is-active", item === button); item.setAttribute("aria-selected", item === button ? "true" : "false"); });
        loadSection(button.dataset.section);
    }));
    fields.province.addEventListener("change", () => { fields.district.value = ""; fields.store.value = ""; populateFilters(); });
    fields.district.addEventListener("change", () => { fields.store.value = ""; populateFilters(); });
    applyButton.addEventListener("click", () => {
        if (fields.from.value && fields.to.value && fields.from.value > fields.to.value) { showNotice("Từ ngày không được lớn hơn đến ngày."); return; }
        cache.clear(); activeRequest?.abort(); loadSection(activeSection, true);
    });
    window.addEventListener("resize", () => charts.forEach(chartInstance => chartInstance.resize()));

    populateFilters(true);
    loadSection(activeSection);
})();
