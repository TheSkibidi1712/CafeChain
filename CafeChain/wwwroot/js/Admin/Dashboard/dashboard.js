(() => {
    "use strict";

    const root = document.getElementById("dashboardRoot");
    if (!root) return;

    const stores = JSON.parse(document.getElementById("dashboardStores")?.textContent || "[]");
    const dashboardContext = JSON.parse(document.getElementById("dashboardContext")?.textContent || "null");
    const panel = document.getElementById("dashboardPanel");
    const aiPanel = document.getElementById("dashboardAiPanel");
    const notice = document.getElementById("dashboardNotice");
    const tablist = document.querySelector(".analytics-tabs");
    const applyButton = document.getElementById("dashboardApply");
    const fields = {
        from: document.getElementById("dashboardFromDate"), to: document.getElementById("dashboardToDate"),
        province: document.getElementById("dashboardProvince"), district: document.getElementById("dashboardDistrict"),
        store: document.getElementById("dashboardStore"), granularity: document.getElementById("dashboardGranularity"),
        top: document.getElementById("dashboardTop"), preset: document.getElementById("dashboardPreset")
    };
    const cache = new Map();
    const charts = new Map();
    let activeSection = "Executive";
    let activeRequest = null;
    let isAiActive = false;

    const fieldMeta = {
        bucketDate: ["Thời gian", "date"], movementDate: ["Thời gian", "date"], receiptDate: ["Thời gian", "date"],
        storeId: ["Mã cửa hàng", "id"], storeName: ["Cửa hàng", "text"], staffId: ["Mã nhân viên", "id"],
        fullName: ["Nhân viên", "text"], workShiftId: ["Mã ca", "id"], staffShiftId: ["Mã phân ca", "id"],
        totalOrders: ["Số đơn", "count"], totalTransactions: ["Số giao dịch", "count"], transactionCount: ["Số giao dịch", "count"],
        netSales: ["Doanh số thuần", "currency"], amount: ["Số tiền", "currency"], averageOrderValue: ["Giá trị đơn trung bình", "currency"],
        cashDiscrepancy: ["Chênh lệch két", "currency"], absoluteDiscrepancy: ["Chênh lệch tuyệt đối", "currency"],
        absoluteCashDiscrepancy: ["Tổng chênh lệch két", "currency"], startingCash: ["Tiền đầu ca", "currency"],
        expectedEndingCash: ["Tiền cuối ca dự kiến", "currency"], actualEndingCash: ["Tiền cuối ca thực tế", "currency"],
        offlineEstimatedTotalAtClose: ["Doanh số offline dự kiến", "currency"], offlineCashTotalAtClose: ["Tiền mặt offline", "currency"],
        paymentMethodName: ["Phương thức thanh toán", "text"], share: ["Tỷ trọng", "percent"],
        hourOfDay: ["Giờ", "hour"], isoWeekday: ["Thứ", "count"],
        severity: ["Mức độ", "status"], alertType: ["Loại cảnh báo", "status"], alertValue: ["Giá trị cảnh báo", "number"], message: ["Nội dung", "text"],
        totalWorkShifts: ["Tổng số ca", "count"], openWorkShifts: ["Ca đang mở", "count"],
        exceptionClosedCount: ["Ngoại lệ đã đóng", "count"], reconciliationCount: ["Ca cần đối soát", "count"],
        offlineOrderCountAtClose: ["Số đơn offline", "count"], requiresReconciliation: ["Cần đối soát", "boolean"],
        hasLateOfflineSync: ["Có đồng bộ trễ", "boolean"], isExceptionClosed: ["Ngoại lệ đã đóng", "boolean"],
        discrepancyReason: ["Lý do chênh lệch", "text"], endTime: ["Kết thúc lúc", "dateTime"],
        ingredientId: ["Mã nguyên liệu", "id"], ingredientName: ["Nguyên liệu", "text"],
        availableQty: ["Tồn khả dụng", "quantity"], reservedQty: ["Đang giữ chỗ", "quantity"],
        minStockLevel: ["Ngưỡng tối thiểu", "quantity"], maxNegativeQty: ["Âm kho tối đa", "quantity"],
        requestedQuantity: ["Số lượng yêu cầu", "quantity"], suggestedQuantity: ["Số lượng đề xuất", "quantity"],
        priority: ["Ưu tiên", "status"], status: ["Trạng thái", "status"], statusCode: ["Trạng thái", "status"],
        quantity: ["Số lượng", "quantity"], wasteValue: ["Giá trị hao hụt", "currency"], wasteQuantity: ["Lượng hao hụt", "quantity"],
        remainingQuantity: ["Số lượng còn lại", "quantity"], remainingValue: ["Giá trị còn lại", "currency"], ageDays: ["Tuổi lớp FIFO (ngày)", "count"],
        preparedItemId: ["Mã bán thành phẩm", "id"],
        code: ["Mã", "text"], supplierName: ["Nhà cung cấp", "text"], expectedDeliveryAtUtc: ["Dự kiến giao", "dateTime"], overdueDays: ["Quá hạn (ngày)", "count"],
        orderedValue: ["Giá trị đặt mua", "currency"], rejectionRate: ["Tỷ lệ từ chối", "percent"],
        averageBaseUnitCost: ["Giá đơn vị cơ sở", "currency"], spend: ["Chi tiêu", "currency"],
        issueType: ["Loại sự cố", "status"], issueCount: ["Số sự cố", "count"],
        drinkCode: ["Mã sản phẩm", "text"], drinkName: ["Sản phẩm", "text"], productRevenue: ["Doanh thu sản phẩm", "currency"],
        revenue: ["Doanh thu", "currency"], confirmedCogs: ["COGS đã xác nhận", "currency"],
        confirmedGrossProfit: ["Lợi nhuận gộp", "currency"], confirmedMarginRate: ["Biên lợi nhuận", "percent"],
        volume: ["Sản lượng", "count"], totalSold: ["Số lượng bán", "count"], sizeName: ["Kích cỡ", "text"],
        toppingName: ["Topping", "text"], recipeCount: ["Số công thức", "count"], recipeLineCount: ["Số dòng BOM", "count"], invalidLineCount: ["Dòng BOM lỗi", "count"],
        shiftId: ["Mã ca dự kiến", "id"], shiftName: ["Ca dự kiến", "text"], workDate: ["Ngày làm", "date"],
        plannedStartAt: ["Bắt đầu dự kiến", "dateTime"], plannedEndAt: ["Kết thúc dự kiến", "dateTime"], isOvernight: ["Ca qua đêm", "boolean"],
        scheduledStaffCount: ["Số lịch nhân sự", "count"], workShiftCount: ["Số ca POS", "count"], ordersPerWorkShift: ["Đơn/ca POS", "number"]
    };

    const codeLabels = {
        DRAFT: "Nháp", APPROVED: "Đã duyệt", MARKED_AS_SENT: "Đã gửi nhà cung cấp", PARTIALLY_RECEIVED: "Nhận một phần",
        COMPLETED: "Hoàn tất", CANCELLED: "Đã hủy", SCHEDULED: "Đã lên lịch",
        OPEN: "Đang mở", CLOSED: "Đã đóng", UNDER_REVIEW: "Đang xem xét", RESOLVED: "Đã xử lý", DISMISSED: "Đã bỏ qua",
        CRITICAL: "Nghiêm trọng", WARNING: "Cảnh báo", HIGH: "Cao", NORMAL: "Bình thường", URGENT: "Khẩn cấp", LOW: "Thấp",
        SUBMITTED: "Đã gửi", PROCESSING: "Đang xử lý", REJECTED: "Đã từ chối",
        LOW_STOCK: "Tồn kho thấp", CASH_DISCREPANCY: "Chênh lệch két", OVERDUE_PO: "Đơn mua quá hạn",
        LATE_DELIVERY: "Giao trễ", SHORT_DELIVERY: "Giao thiếu", WRONG_ITEM: "Sai hàng", DAMAGED: "Hư hỏng", EXPIRED: "Hết hạn",
        QUALITY_FAILURE: "Không đạt chất lượng", PACKAGING_FAILURE: "Lỗi bao bì", DOCUMENT_MISMATCH: "Sai chứng từ", OTHER: "Khác"
    };

    const inventoryTransactionLabels = {
        1: "Nhập kho", 2: "Xuất kho", 3: "Hao hụt", 4: "Kiểm kê", 5: "Nhập từ sản xuất", 6: "Xuất cho sản xuất",
        7: "Khấu trừ bán hàng", 8: "Điều chỉnh tăng", 9: "Điều chỉnh giảm", 10: "Chuyển kho đi", 11: "Chuyển kho đến",
        12: "Hợp nhất giảm", 13: "Hợp nhất tăng", 14: "Nhập từ phiếu nhận", 15: "Hoàn trả bán hàng"
    };

    const sections = {
        Executive: [
            chart("netSalesTrend", "Doanh số thuần", "line", "bucketDate", "netSales", { wide: true, axis: "time", valueFormat: "currency" }),
            chart("storeRanking", "Xếp hạng cửa hàng", "bar", "storeName", "netSales", { entity: "store", valueFormat: "currency" }),
            chart("paymentMethodMix", "Cơ cấu thanh toán", "donut", "paymentMethodName", "amount", { valueFormat: "currency" }),
            chart("orderHeatmap", "Mật độ đơn theo giờ", "heatmap", "hourOfDay", "totalOrders", { wide: true, valueFormat: "count" }),
            table("operationalAlerts", "Cảnh báo vận hành", ["severity", "alertType", "storeId", "alertValue", "message"], true)
        ],
        Operations: [
            kpi("kpis", "KPI ca làm việc", ["totalWorkShifts", "openWorkShifts", "exceptionClosedCount", "reconciliationCount", "absoluteCashDiscrepancy"], true),
            chart("cashDiscrepancy", "Chênh lệch két", "bar", row => shiftLabel(row, true), "cashDiscrepancy", { valueFormat: "currency" }),
            chart("shiftSales", "Doanh số theo ca", "bar", row => shiftLabel(row), "netSales", { valueFormat: "currency" }),
            chart("paymentMix", "Thanh toán theo ca", "bar", row => shiftLabel(row), "amount", { seriesBy: "paymentMethodName", stack: true, valueFormat: "currency", missingValue: 0 }),
            chart("hourlyOrders", "Đơn hàng theo giờ", "line", row => hourLabel(row.hourOfDay), "totalOrders", { valueFormat: "count" }),
            table("offlineReconciliation", "Đối soát offline", ["workShiftId", "storeId", "offlineOrderCountAtClose", "offlineEstimatedTotalAtClose", "requiresReconciliation", "hasLateOfflineSync"]),
            table("topDiscrepancies", "Ca chênh lệch lớn", ["workShiftId", "storeId", "staffId", "cashDiscrepancy", "discrepancyReason", "endTime"])
        ],
        Inventory: [
            chart("shortageRisk", "Nguy cơ thiếu hàng", "bar", row => ingredientStoreLabel(row), "availableQty", { valueFormat: "quantity" }),
            chart("movement", "Biến động kho", "line", "movementDate", "quantity", { axis: "time", seriesBy: "transactionType", seriesLabel: inventoryTransactionLabel, valueFormat: "quantity", missingValue: 0 }),
            table("thresholdRisk", "Rủi ro ngưỡng tồn", ["storeId", "ingredientName", "availableQty", "minStockLevel", "maxNegativeQty"]),
            table("reorderSuggestions", "Đề xuất đặt lại", ["storeId", "ingredientName", "requestedQuantity", "suggestedQuantity", "priority", "status"]),
            chart("waste", "Hao hụt", "bar", row => ingredientStoreLabel(row), "wasteValue", { valueFormat: "currency" }),
            table("fifoAge", "Tuổi lớp giá FIFO", ["storeId", "ingredientId", "preparedItemId", "remainingQuantity", "ageDays", "remainingValue"])
        ],
        Procurement: [
            chart("purchaseOrderPipeline", "Pipeline đơn mua", "donut", row => translateCode(row.status), "orderedValue", { valueFormat: "currency" }),
            table("overduePurchaseOrders", "Đơn mua quá hạn", ["code", "storeId", "supplierName", "status", "expectedDeliveryAtUtc", "overdueDays"]),
            chart("supplierQuality", "Chất lượng nhà cung cấp", "bar", "supplierName", "rejectionRate", { entity: "supplier", valueFormat: "percent" }),
            chart("purchasePriceTrend", "Xu hướng giá mua", "line", "receiptDate", "averageBaseUnitCost", { axis: "time", seriesBy: "ingredientName", seriesEntity: "ingredient", valueFormat: "currency", missingValue: null }),
            chart("spendBreakdown", "Chi tiêu nhà cung cấp", "bar", row => supplierStoreLabel(row), "spend", { valueFormat: "currency" }),
            chart("supplierIssueMix", "Cơ cấu sự cố", "donut", row => `${translateCode(row.issueType)} – ${translateCode(row.status)}`, "issueCount", { valueFormat: "count" })
        ],
        Product: [
            chart("topProducts", "Top sản phẩm", "bar", "drinkName", "productRevenue", { entity: "drink", valueFormat: "currency" }),
            chart("volumeMargin", "Sản lượng và biên lợi nhuận", "scatter", "volume", "confirmedMarginRate", { valueFormat: "percent" }),
            chart("sizeMargin", "Lợi nhuận theo size", "bar", "sizeName", "confirmedGrossProfit", { entity: "size", valueFormat: "currency" }),
            chart("topToppings", "Top topping", "bar", "toppingName", "revenue", { entity: "topping", valueFormat: "currency" }),
            table("bomHealth", "Sức khỏe BOM", ["drinkCode", "drinkName", "recipeCount", "recipeLineCount", "invalidLineCount"]),
            table("lowEfficiency", "Tiêu hao cao / hiệu quả thấp", ["drinkName", "totalSold", "confirmedCogs", "confirmedGrossProfit"])
        ],
        Workforce: [
            chart("shiftStatus", "Trạng thái phân ca", "bar", "statusCode", "staffShiftId", { aggregate: "count", valueFormat: "count", valueLabel: "Số ca" }),
            chart("hourlyDemand", "Đơn hàng theo giờ", "line", row => hourLabel(row.hourOfDay), "totalOrders", { valueFormat: "count" }),
            chart("scheduledStaff", "Lịch nhân sự theo giờ", "line", row => hourLabel(row.hourOfDay), "scheduledStaffCount", { dataKey: "hourlyDemand", valueFormat: "count" }),
            table("staffPerformance", "Hoạt động POS theo nhân viên", ["fullName", "storeId", "workShiftCount", "totalOrders", "netSales", "averageOrderValue", "ordersPerWorkShift"], true)
        ]
    };

    function chart(key, title, kind, label, value, options = {}) { return { key, title, kind, label, value, ...options, wide: Boolean(options.wide) }; }
    function table(key, title, columns, wide = false) { return { key, title, kind: "table", columns, wide }; }
    function kpi(key, title, columns, wide = false) { return { key, title, kind: "kpi", columns, wide }; }

    function escapeHtml(value) {
        return String(value ?? "").replace(/[&<>'"]/g, character => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", "'": "&#39;", '"': "&quot;" })[character]);
    }

    function friendlyName(value) { return String(value).replace(/([a-z])([A-Z])/g, "$1 $2").replace(/^./, x => x.toUpperCase()); }
    function fieldLabel(key) { return fieldMeta[key]?.[0] || friendlyName(key); }
    function fieldFormat(key) { return fieldMeta[key]?.[1]; }

    function format(value, type) {
        if (value === null || value === undefined || value === "") return "—";
        if (type === "status") return translateCode(value);
        if (type === "boolean" || typeof value === "boolean") return value ? "Có" : "Không";
        if (type === "currency") return `${new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 0 }).format(Number(value) || 0)} ₫`;
        if (type === "percent") return `${new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 2 }).format((Number(value) || 0) * 100)}%`;
        if (type === "count") return new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 0 }).format(Number(value) || 0);
        if (type === "quantity") return new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 3 }).format(Number(value) || 0);
        if (type === "number" || typeof value === "number") return new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 2 }).format(Number(value) || 0);
        if (type === "hour") return hourLabel(value);
        if (type === "id") return `#${value}`;
        if (type === "dateTime" || /^\d{4}-\d{2}-\d{2}T/.test(String(value))) return new Date(value).toLocaleString("vi-VN");
        if (type === "date" || /^\d{4}-\d{2}-\d{2}$/.test(String(value))) return new Date(value).toLocaleDateString("vi-VN");
        return String(value);
    }

    function translateCode(value) {
        const normalized = String(value ?? "").trim();
        if (!normalized) return "Không xác định";
        return codeLabels[normalized.toUpperCase()] || normalized.replaceAll("_", " ");
    }

    function inventoryTransactionLabel(value) { return inventoryTransactionLabels[Number(value)] || `Loại giao dịch #${value}`; }
    function hourLabel(value) { return `${String(Number(value) || 0).padStart(2, "0")}h`; }

    function entityLabel(row, entity, preferred) {
        const definitions = {
            store: ["storeName", "storeId", "Cửa hàng"], supplier: ["supplierName", "supplierId", "Nhà cung cấp"],
            ingredient: ["ingredientName", "ingredientId", "Nguyên liệu"], drink: ["drinkName", "drinkId", "Sản phẩm"],
            topping: ["toppingName", "toppingId", "Topping"], size: ["sizeName", "sizeId", "Kích cỡ"]
        };
        const definition = definitions[entity];
        const name = String(preferred ?? (definition ? row[definition[0]] : "") ?? "").trim();
        if (name) return name;
        const id = definition ? row[definition[1]] : null;
        return id === null || id === undefined || id === "" ? (definition?.[2] || "Không xác định") : `${definition[2]} #${id}`;
    }

    function shiftLabel(row, detailed = false) {
        const shift = `Ca #${row.workShiftId ?? "?"}`;
        if (!detailed) return shift;
        const owner = String(row.fullName || row.storeName || "").trim() || (row.storeId ? `Cửa hàng #${row.storeId}` : "Chưa xác định");
        return `${shift} – ${owner}`;
    }

    function ingredientStoreLabel(row) {
        const ingredient = entityLabel(row, "ingredient");
        const store = entityLabel(row, "store");
        return row.storeId || row.storeName ? `${ingredient} – ${store}` : ingredient;
    }

    function supplierStoreLabel(row) {
        const supplier = entityLabel(row, "supplier");
        const store = entityLabel(row, "store");
        return row.storeId || row.storeName ? `${supplier} – ${store}` : supplier;
    }

    function resolveLabel(row, widget) {
        if (typeof widget.label === "function") return nonEmptyLabel(widget.label(row));
        const raw = row[widget.label];
        if (widget.entity) return nonEmptyLabel(entityLabel(row, widget.entity, raw));
        return nonEmptyLabel(raw, fieldLabel(widget.label));
    }

    function resolveSeriesLabel(row, widget) {
        const raw = row[widget.seriesBy];
        if (widget.seriesLabel) return nonEmptyLabel(widget.seriesLabel(raw));
        if (widget.seriesEntity) return nonEmptyLabel(entityLabel(row, widget.seriesEntity, raw));
        return nonEmptyLabel(raw, fieldLabel(widget.seriesBy));
    }

    function nonEmptyLabel(value, fallback = "Không xác định") {
        const normalized = String(value ?? "").trim();
        return normalized || fallback;
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
        if (root.dataset.contextId) parameters.set("contextId", root.dataset.contextId);
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
            if (payload.data?.contextId) root.dataset.contextId = payload.data.contextId;
            if (payload.data?.generatedAt) root.dataset.generatedAt = payload.data.generatedAt;
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
            const result = data[widget.dataKey || widget.key] || { status: "ERROR", message: "Payload không có widget này." };
            (result.warnings || []).forEach(item => warnings.push(`${widget.title}: ${item}`));
            return widgetShell(widget, result);
        }).join("");
        sections[section].forEach(widget => renderWidget(widget, data[widget.dataKey || widget.key], response?.granularity));
        const scheduleNotice = section === "Workforce" ? "Lịch nhân sự là kế hoạch dự kiến, không phải dữ liệu chấm công hoặc tính lương." : "";
        const warningNotice = warnings.length ? `Dữ liệu một phần: ${warnings.join(" · ")}` : "";
        showNotice([scheduleNotice, warningNotice].filter(Boolean).join(" · "));
    }

    function widgetShell(widget, result) {
        const badge = result?.warnings?.length ? `<span class="analytics-badge">Dữ liệu một phần</span>` : "";
        return `<article class="analytics-widget ${widget.wide ? "is-wide" : ""} ${widget.kind === "kpi" ? "is-compact" : ""}" data-widget="${widget.key}"><div class="analytics-widget__header"><h2>${widget.title}</h2>${badge}</div><div class="analytics-widget__body" id="widget-${widget.key}"></div></article>`;
    }

    function renderWidget(widget, result, granularity) {
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
        else renderChart(target, rows, widget, granularity);
    }

    function state(title, message, retry = false) {
        return `<div class="analytics-state"><div><strong>${escapeHtml(title)}</strong><span>${escapeHtml(message)}</span>${retry ? '<br><button type="button" class="analytics-retry">Thử lại</button>' : ""}</div></div>`;
    }

    function renderTable(target, rows, columns) {
        target.innerHTML = `<div class="analytics-table-wrap"><table class="analytics-table"><thead><tr>${columns.map(key => `<th title="${escapeHtml(fieldLabel(key))}">${escapeHtml(fieldLabel(key))}</th>`).join("")}</tr></thead><tbody>${rows.map(row => `<tr>${columns.map(key => tableCell(row[key], key)).join("")}</tr>`).join("")}</tbody></table></div>`;
    }

    function tableCell(value, key) {
        const type = fieldFormat(key);
        const display = format(value, type);
        const compact = ["currency", "percent", "count", "quantity", "number", "boolean", "date", "dateTime", "id", "hour"].includes(type);
        return `<td class="${compact ? "is-compact-value" : ""}" title="${escapeHtml(display)}">${escapeHtml(display)}</td>`;
    }

    function renderKpis(target, row, columns) {
        target.innerHTML = `<div class="analytics-kpis">${columns.map(key => `<div class="analytics-kpi"><span>${escapeHtml(fieldLabel(key))}</span><strong title="${escapeHtml(format(row[key], fieldFormat(key)))}">${escapeHtml(format(row[key], fieldFormat(key)))}</strong></div>`).join("")}</div>`;
    }

    const chartResizeObserver = typeof ResizeObserver === "function"
        ? new ResizeObserver(entries => entries.forEach(entry => {
            const context = [...charts.values()]
                .find(item => item.observedElements.includes(entry.target));
            if (context) scheduleChartLayout(context);
        }))
        : null;

    function renderChart(target, rows, widget, granularity) {
        if (!window.echarts) { target.innerHTML = state("Thiếu ECharts", "Không thể khởi tạo biểu đồ."); return; }
        const element = document.createElement("div");
        element.className = "analytics-chart";
        target.appendChild(element);
        const context = {
            instance: null,
            element,
            rows,
            widget,
            granularity,
            capacity: null,
            resizeFrame: 0,
            observedElements: [element, target]
        };
        charts.set(widget.key, context);
        context.observedElements.forEach(observed => chartResizeObserver?.observe(observed));
        scheduleChartLayout(context);
    }

    function scheduleChartLayout(context) {
        if (!context || !context.element.isConnected) return;
        window.cancelAnimationFrame(context.resizeFrame);
        context.resizeFrame = window.requestAnimationFrame(() => {
            context.resizeFrame = window.requestAnimationFrame(() => refreshChartLayout(context));
        });
    }

    function refreshChartLayout(context) {
        if (!context?.element?.isConnected) return;
        const bounds = context.element.getBoundingClientRect();
        if (bounds.width <= 1 || bounds.height <= 1) return;
        const capacity = categoryCapacity(bounds.width);
        if (!context.instance || context.instance.isDisposed?.()) {
            context.instance = window.echarts.init(context.element);
            context.capacity = capacity;
            context.instance.setOption(
                chartOption(context.rows, context.widget, context.granularity, capacity),
                { notMerge: true }
            );
            context.instance.resize();
            return;
        }
        context.instance.resize();
        if (capacity === context.capacity) return;
        context.capacity = capacity;
        context.instance.setOption(chartOption(context.rows, context.widget, context.granularity, capacity), { notMerge: true, lazyUpdate: true });
    }

    function categoryCapacity(width) {
        if (width <= 430) return 6;
        if (width <= 700) return 8;
        if (width <= 980) return 10;
        return 12;
    }

    function chartOption(rows, widget, granularity, capacity) {
        const base = { animationDuration: 350, textStyle: { fontFamily: "Segoe UI, sans-serif" }, tooltip: { trigger: "axis", confine: true }, aria: { enabled: true }, grid: { left: 66, right: 34, top: 42, bottom: 82, containLabel: true } };
        if (widget.kind === "donut") return donutOption(rows, widget, base);
        if (widget.kind === "scatter") return scatterOption(rows, widget, base);
        if (widget.kind === "heatmap") return heatmapOption(rows, widget, base);
        if (widget.aggregate === "count") return aggregateOption(rows, widget, base, granularity, capacity);
        if (widget.seriesBy) return multiSeriesOption(rows, widget, base, granularity, capacity);
        return singleSeriesOption(rows, widget, base, granularity, capacity);
    }

    function donutOption(rows, widget, base) {
        return {
            ...base,
            tooltip: { trigger: "item", confine: true, formatter: params => `${escapeHtml(params.name)}<br>${escapeHtml(format(params.value, widget.valueFormat))} (${format(params.percent / 100, "percent")})` },
            legend: { bottom: 0, type: "scroll", formatter: name => truncateLabel(name, 28) },
            series: [{ type: "pie", radius: ["43%", "68%"], avoidLabelOverlap: true, data: rows.map(row => ({ name: resolveLabel(row, widget), value: Number(row[widget.value] || 0) })), itemStyle: { borderColor: "#fff", borderWidth: 2 } }]
        };
    }

    function scatterOption(rows, widget, base) {
        return {
            ...base,
            grid: { ...base.grid, left: 76, right: 48, bottom: 68 },
            tooltip: {
                trigger: "item", confine: true,
                formatter: params => {
                    const row = params.data.raw;
                    return `<strong>${escapeHtml(entityLabel(row, "drink"))}</strong><br>Sản lượng: ${escapeHtml(format(row.volume, "count"))}<br>Doanh thu: ${escapeHtml(format(row.revenue, "currency"))}<br>COGS: ${escapeHtml(format(row.confirmedCogs, "currency"))}<br>Biên lợi nhuận: ${escapeHtml(format(row.confirmedMarginRate, "percent"))}`;
                }
            },
            xAxis: { type: "value", name: "Sản lượng", nameLocation: "middle", nameGap: 34, minInterval: 1 },
            yAxis: { type: "value", name: "Biên lợi nhuận", nameLocation: "middle", nameGap: 48, axisLabel: { formatter: value => format(value, "percent") } },
            series: [{
                type: "scatter", symbolSize: 16,
                data: rows.map(row => ({ name: entityLabel(row, "drink"), value: [Number(row.volume || 0), Number(row.confirmedMarginRate || 0)], raw: row })),
                label: { show: rows.length <= 12, position: "top", formatter: params => wrapAxisLabel(params.data.name, 14) },
                labelLayout: { hideOverlap: rows.length > 12 }, itemStyle: { color: "#2563eb", opacity: .75 }
            }]
        };
    }

    function heatmapOption(rows, widget, base) {
        const values = rows.map(row => [Number(row.hourOfDay), Number(row.isoWeekday) - 1, Number(row[widget.value] || 0)]);
        const max = Math.max(1, ...values.map(item => item[2]));
        return {
            ...base, grid: { ...base.grid, top: 20, bottom: 76 },
            tooltip: { position: "top", confine: true, formatter: item => `${["T2", "T3", "T4", "T5", "T6", "T7", "CN"][item.data[1]]}, ${hourLabel(item.data[0])}: ${format(item.data[2], widget.valueFormat)}` },
            xAxis: { type: "category", data: Array.from({ length: 24 }, (_, hour) => hourLabel(hour)), splitArea: { show: true }, axisLabel: { interval: 1 } },
            yAxis: { type: "category", data: ["T2", "T3", "T4", "T5", "T6", "T7", "CN"], splitArea: { show: true } },
            visualMap: { min: 0, max, calculable: true, orient: "horizontal", left: "center", bottom: 0 },
            series: [{ type: "heatmap", data: values }]
        };
    }

    function aggregateOption(rows, widget, base, granularity, capacity) {
        const grouped = new Map();
        rows.forEach(row => {
            const label = translateCode(resolveLabel(row, widget));
            grouped.set(label, (grouped.get(label) || 0) + 1);
        });
        const aggregated = [...grouped].map(([label, value]) => ({ label, value }));
        return cartesianOption(base, aggregated.map(row => row.label), [{ name: "Số ca", type: "bar", data: aggregated.map(row => row.value) }], widget, granularity, capacity, false);
    }

    function multiSeriesOption(rows, widget, base, granularity, capacity) {
        const labels = unique(rows.map(row => resolveLabel(row, widget)));
        const seriesNames = unique(rows.map(row => resolveSeriesLabel(row, widget)));
        const values = new Map();
        rows.forEach(row => {
            const key = `${resolveLabel(row, widget)}\u0000${resolveSeriesLabel(row, widget)}`;
            values.set(key, (values.get(key) ?? 0) + Number(row[widget.value] || 0));
        });
        const series = seriesNames.map(name => ({
            name, type: widget.kind, stack: widget.stack ? "total" : undefined, smooth: widget.kind === "line", connectNulls: false,
            showSymbol: labels.length <= 20, data: labels.map(label => values.has(`${label}\u0000${name}`) ? values.get(`${label}\u0000${name}`) : widget.missingValue)
        }));
        return cartesianOption(base, labels, series, widget, granularity, capacity, widget.axis === "time");
    }

    function singleSeriesOption(rows, widget, base, granularity, capacity) {
        const labels = rows.map(row => resolveLabel(row, widget));
        const series = [{
            name: widget.title, type: widget.kind, smooth: widget.kind === "line", showSymbol: rows.length <= 20,
            areaStyle: widget.kind === "line" ? { opacity: .08 } : undefined,
            data: rows.map(row => Number(row[widget.value] || 0)), itemStyle: { color: "#6F4E37" }, lineStyle: { color: "#6F4E37", width: 3 }
        }];
        return cartesianOption(base, labels, series, widget, granularity, capacity, widget.axis === "time");
    }

    function cartesianOption(base, labels, series, widget, granularity, capacity, dateAxis) {
        const visibleCapacity = dateAxis ? 30 : capacity;
        const useZoom = labels.length > visibleCapacity;
        const zoomEnd = useZoom ? Math.max(2, Math.min(100, visibleCapacity / labels.length * 100)) : 100;
        const categoryAxis = !dateAxis;
        return {
            ...base,
            color: ["#6F4E37", "#C67A45", "#2F6F5E", "#99623B", "#991B1B", "#475569", "#C67A45"],
            grid: { ...base.grid, bottom: useZoom ? 130 : (categoryAxis ? 110 : 82) },
            legend: series.length > 1 ? { type: "scroll", top: 0, left: 10, right: 10 } : undefined,
            tooltip: { ...base.tooltip, formatter: parameters => chartTooltip(parameters, widget, dateAxis, granularity) },
            dataZoom: useZoom ? [
                { type: "inside", start: 0, end: zoomEnd, filterMode: "none", zoomOnMouseWheel: true, moveOnMouseMove: true },
                { type: "slider", start: 0, end: zoomEnd, height: 18, bottom: 10, brushSelect: false }
            ] : undefined,
            xAxis: {
                type: "category", data: labels, boundaryGap: series.some(item => item.type === "bar"),
                axisLabel: {
                    interval: "auto",           /* FIX: bỏ qua label tự động, không ép show all */
                    hideOverlap: true,          /* FIX: ẩn label bị đè kể cả category axis */
                    rotate: categoryAxis ? 30 : 0, /* FIX: xoay 30° để label dài khỏi chồng nhau */
                    width: categoryAxis ? 90 : 82, lineHeight: 16, overflow: "truncate", ellipsis: "…",
                    formatter: value => dateAxis
                        ? formatAxisLabel(value, true, granularity)
                        : (categoryAxis ? truncateLabel(value, 22) : wrapAxisLabel(value, 14))
                },
                axisPointer: { label: { formatter: params => fullAxisLabel(params.value, dateAxis, granularity) } }
            },
            yAxis: {
                type: "value", name: widget.valueLabel || fieldLabel(widget.value), nameTextStyle: { padding: [0, 0, 6, 0] },
                axisLabel: { formatter: value => compactNumber(value, widget.valueFormat) }
            },
            series
        };
    }

    function chartTooltip(parameters, widget, dateAxis, granularity) {
        const points = Array.isArray(parameters) ? parameters : [parameters];
        const heading = fullAxisLabel(points[0]?.axisValue, dateAxis, granularity);
        const lines = points.filter(point => point.value !== null && point.value !== undefined).map(point => {
            const value = Array.isArray(point.value) ? point.value.at(-1) : point.value;
            return `${point.marker || ""} ${escapeHtml(point.seriesName || widget.title)}: <strong>${escapeHtml(format(Number(value || 0), widget.valueFormat))}</strong>`;
        });
        return `<strong>${escapeHtml(heading)}</strong><br>${lines.join("<br>")}`;
    }

    function wrapAxisLabel(value, maxLength) {
        const text = nonEmptyLabel(value);
        if (text.length <= maxLength) return text;
        const words = text.split(/\s+/);
        const lines = [""];
        for (const word of words) {
            const current = lines.at(-1);
            if (!current || `${current} ${word}`.length <= maxLength) lines[lines.length - 1] = current ? `${current} ${word}` : word;
            else if (lines.length < 2) lines.push(word);
            else { lines[1] = truncateLabel(`${lines[1]} ${word}`, maxLength); break; }
        }
        if (lines.length === 1 && lines[0].length > maxLength) return `${lines[0].slice(0, maxLength - 1)}…`;
        return lines.map(line => truncateLabel(line, maxLength)).join("\n");
    }

    function truncateLabel(value, maxLength) {
        const text = nonEmptyLabel(value);
        return text.length <= maxLength ? text : `${text.slice(0, Math.max(1, maxLength - 1)).trim()}…`;
    }

    function compactNumber(value, type) {
        if (type === "percent") return format(value, "percent");
        const number = Number(value) || 0;
        if (Math.abs(number) >= 1_000_000_000) return `${new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 1 }).format(number / 1_000_000_000)} tỷ`;
        if (Math.abs(number) >= 1_000_000) return `${new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 1 }).format(number / 1_000_000)} tr`;
        if (Math.abs(number) >= 1_000) return `${new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 1 }).format(number / 1_000)}k`;
        return format(number, type === "currency" ? "count" : type);
    }

    function formatAxisLabel(value, dateAxis, granularity) {
        if (!dateAxis) return nonEmptyLabel(value);
        const date = toValidDate(value);
        if (!date) return nonEmptyLabel(value);
        const mode = String(granularity || "Day").toUpperCase();
        if (mode === "HOUR") return `${twoDigits(date.getHours())}:00\n${twoDigits(date.getDate())}/${twoDigits(date.getMonth() + 1)}`;
        if (mode === "WEEK") return `T${twoDigits(isoWeek(date))}\n${date.getFullYear()}`;
        if (mode === "MONTH") return `${twoDigits(date.getMonth() + 1)}/${date.getFullYear()}`;
        return `${twoDigits(date.getDate())}/${twoDigits(date.getMonth() + 1)}`;
    }

    function fullAxisLabel(value, dateAxis, granularity) {
        if (!dateAxis) return nonEmptyLabel(value);
        const date = toValidDate(value);
        if (!date) return nonEmptyLabel(value);
        const mode = String(granularity || "Day").toUpperCase();
        if (mode === "HOUR") return date.toLocaleString("vi-VN");
        if (mode === "WEEK") return `Tuần ${isoWeek(date)}, từ ${date.toLocaleDateString("vi-VN")}`;
        if (mode === "MONTH") return `Tháng ${date.getMonth() + 1}/${date.getFullYear()}`;
        return date.toLocaleDateString("vi-VN");
    }

    function unique(values) { return [...new Set(values)]; }
    function toValidDate(value) { const date = new Date(value); return Number.isNaN(date.getTime()) ? null : date; }
    function twoDigits(value) { return String(value).padStart(2, "0"); }

    function isoWeek(value) {
        const date = new Date(Date.UTC(value.getFullYear(), value.getMonth(), value.getDate()));
        const weekday = date.getUTCDay() || 7;
        date.setUTCDate(date.getUTCDate() + 4 - weekday);
        const yearStart = new Date(Date.UTC(date.getUTCFullYear(), 0, 1));
        return Math.ceil((((date - yearStart) / 86400000) + 1) / 7);
    }

    function renderSectionError(section, message) {
        disposeCharts();
        panel.innerHTML = `<article class="analytics-widget is-wide"><div class="analytics-state"><div><strong>Không tải được tab ${escapeHtml(section)}</strong><span>${escapeHtml(message)}</span><br><button type="button" class="analytics-retry">Thử lại</button></div></div></article>`;
        panel.querySelector("button")?.addEventListener("click", () => loadSection(section, true));
    }

    function showNotice(message) {
        notice.hidden = isAiActive || !message;
        notice.textContent = message;
    }

    function disposeCharts() {
        charts.forEach(context => {
            window.cancelAnimationFrame(context.resizeFrame);
            context.observedElements.forEach(observed => chartResizeObserver?.unobserve(observed));
            if (context.instance && !context.instance.isDisposed?.()) context.instance.dispose();
        });
        charts.clear();
    }

    function selectTab(button) {
        document.querySelectorAll(".analytics-tab").forEach(item => {
            const selected = item === button;
            item.classList.toggle("is-active", selected);
            item.setAttribute("aria-selected", selected ? "true" : "false");
            item.tabIndex = selected ? 0 : -1;
        });
    }

    function activateAiTab(button) {
        isAiActive = true;
        selectTab(button);
        panel.hidden = true;
        notice.hidden = true;
        aiPanel.hidden = false;
        window.dispatchEvent(new CustomEvent("cafechain:dashboard-ai-visible"));
    }

    function activateSectionTab(button) {
        isAiActive = false;
        selectTab(button);
        aiPanel.hidden = true;
        panel.hidden = false;
        panel.setAttribute("aria-labelledby", button.id);
        notice.hidden = !notice.textContent;
        void loadSection(button.dataset.section).then(() => {
            window.dispatchEvent(new CustomEvent("cafechain:dashboard-charts-visible"));
        });
    }

    document.querySelectorAll(".analytics-tab").forEach(button => button.addEventListener("click", () => {
        if (button.hasAttribute("data-ai-tab")) activateAiTab(button);
        else activateSectionTab(button);
    }));
    tablist?.addEventListener("keydown", event => {
        if (!["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key)) return;
        const tabs = [...tablist.querySelectorAll('[role="tab"]')];
        const currentIndex = Math.max(0, tabs.indexOf(document.activeElement));
        const nextIndex = event.key === "Home"
            ? 0
            : event.key === "End"
                ? tabs.length - 1
                : (currentIndex + (event.key === "ArrowRight" ? 1 : -1) + tabs.length) % tabs.length;
        event.preventDefault();
        tabs[nextIndex].focus();
        tabs[nextIndex].click();
    });
    fields.province.addEventListener("change", () => { fields.district.value = ""; fields.store.value = ""; populateFilters(); });
    fields.district.addEventListener("change", () => { fields.store.value = ""; populateFilters(); });
    [fields.from, fields.to].forEach(field => field.addEventListener("change", () => { if (fields.preset) fields.preset.value = ""; }));
    async function applyDashboardContext() {
        window.dispatchEvent(new CustomEvent("cafechain:dashboard-context-changing"));
        if (fields.from.value && fields.to.value && fields.from.value > fields.to.value) { showNotice("Từ ngày không được lớn hơn đến ngày."); return; }
        applyButton.disabled = true;
        try {
            const token = document.querySelector("#dashboardAntiForgery input[name='__RequestVerificationToken']")?.value || "";
            const response = await fetch(root.dataset.contextEndpoint, {
                method: "POST",
                credentials: "same-origin",
                headers: { "Content-Type": "application/json", "RequestVerificationToken": token, "X-Requested-With": "XMLHttpRequest" },
                body: JSON.stringify({
                    fromDate: fields.from.value,
                    toDate: fields.to.value,
                    provinceId: fields.province.value ? Number(fields.province.value) : null,
                    districtId: fields.district.value ? Number(fields.district.value) : null,
                    storeId: fields.store.value ? Number(fields.store.value) : null,
                    granularity: fields.granularity.value,
                    top: Number(fields.top.value || 10),
                    preset: fields.preset?.value || null
                })
            });
            const payload = await response.json();
            if (!response.ok || !payload.success) throw new Error(payload.message || "Không thể tạo context Dashboard.");
            root.dataset.contextId = payload.data.contextId;
            root.dataset.generatedAt = payload.data.generatedAt;
            root.dataset.filterFingerprint = payload.data.filterFingerprint || "";
            if (payload.data.fromDate) fields.from.value = String(payload.data.fromDate).slice(0, 10);
            if (payload.data.toDate) fields.to.value = String(payload.data.toDate).slice(0, 10);
            cache.clear();
            activeRequest?.abort();
            if (isAiActive) {
                disposeCharts();
                panel.replaceChildren();
            } else {
                await loadSection(activeSection, true);
            }
            window.dispatchEvent(new CustomEvent("cafechain:dashboard-context-changed", {
                detail: {
                    contextId: root.dataset.contextId,
                    filterFingerprint: root.dataset.filterFingerprint
                }
            }));
        } catch (error) {
            showNotice(error instanceof Error ? error.message : String(error));
        } finally {
            applyButton.disabled = false;
        }
    }
    applyButton.addEventListener("click", () => void applyDashboardContext());
    fields.preset?.addEventListener("change", () => {
        if (!fields.preset.value) return;
        void applyDashboardContext();
    });
    window.addEventListener("resize", () => charts.forEach(scheduleChartLayout));
    window.addEventListener("cafechain:dashboard-charts-visible", () => charts.forEach(scheduleChartLayout));

    populateFilters(true);
    loadSection(activeSection);
})();
