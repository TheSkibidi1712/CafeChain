document.addEventListener("DOMContentLoaded", function () {

    function safe(arr) {
        return Array.isArray(arr) ? arr : [];
    }

    function formatDate(d) {
        if (!d) return "";

        const date = new Date(d);

        if (isNaN(date.getTime())) return "";

        return date.toLocaleDateString("vi-VN");
    }

    const stores = safe(dashboardData.stores);

    const provinceSelect = document.getElementById("provinceId");
    const districtSelect = document.getElementById("districtId");
    const storeSelect = document.getElementById("storeId");

    // =====================================================
    // GLOBAL CHART STYLE
    // =====================================================

    const globalTextStyle = {
        fontFamily: "Segoe UI, Arial, sans-serif"
    };

    const commonAxisLabel = {
        interval: 0,
        rotate: 0, // FIX toàn bộ rotate
        fontSize: 12,
        fontFamily: "Segoe UI, Arial, sans-serif",
        width: 120,
        overflow: "truncate"
    };

    // =====================================================
    // DROPDOWN FILTER
    // =====================================================

    function filterDropdown() {
        const province = provinceSelect?.value;
        const district = districtSelect?.value;
        const currentStore = storeSelect?.value;

        let filtered = [...stores];

        if (province) {
            filtered = filtered.filter(x =>
                String(x.provinceId) === String(province)
            );
        }

        if (district) {
            filtered = filtered.filter(x =>
                String(x.districtId) === String(district)
            );
        }

        if (storeSelect) {
            storeSelect.innerHTML = `<option value="">Tất cả cửa hàng</option>`;

            filtered.forEach(s => {
                const selected =
                    String(currentStore) === String(s.storeId)
                        ? "selected"
                        : "";

                storeSelect.innerHTML += `
                    <option value="${s.storeId}" ${selected}>
                        ${s.storeName}
                    </option>
                `;
            });
        }
    }

    provinceSelect?.addEventListener("change", function () {
        if (districtSelect) {
            districtSelect.value = "";
        }

        filterDropdown();
    });

    districtSelect?.addEventListener("change", function () {
        filterDropdown();
    });

    // =====================================================
    // QUERY PARAMS
    // =====================================================

    function getParams() {
        const params = new URLSearchParams();

        const from = document.getElementById("fromDate")?.value;
        const to = document.getElementById("toDate")?.value;
        const store = storeSelect?.value;
        const province = provinceSelect?.value;
        const district = districtSelect?.value;

        if (from) params.append("FromDate", from);
        if (to) params.append("ToDate", to);
        if (store) params.append("StoreId", store);
        if (province) params.append("ProvinceId", province);
        if (district) params.append("DistrictId", district);

        return params.toString();
    }

    // =====================================================
    // APPLY FILTER
    // =====================================================

    document.getElementById("btnApply")?.addEventListener("click", function () {
        const from = document.getElementById("fromDate")?.value;
        const to = document.getElementById("toDate")?.value;

        if (from && to && new Date(from) > new Date(to)) {
            toast("Từ ngày không được lớn hơn đến ngày", "error");
            return;
        }

        window.location.href = "/Admin/Dashboard?" + getParams();
    });

    // =====================================================
    // CHART HELPER
    // =====================================================

    let chartInstances = {};

    function createChart(elementId, option) {
        const el = document.getElementById(elementId);

        if (!el) return null;

        if (chartInstances[elementId]) {
            chartInstances[elementId].dispose();
        }

        const chart = echarts.init(el);

        chart.setOption({
            textStyle: globalTextStyle,
            ...option
        });

        chartInstances[elementId] = chart;

        return chart;
    }

    function resizeCharts() {
        Object.values(chartInstances).forEach(chart => {
            if (chart) chart.resize();
        });
    }

    window.addEventListener("resize", resizeCharts);

    // =====================================================
    // REVENUE
    // =====================================================

    const revenue = safe(dashboardData.revenue);

    createChart("revenueChart", {
        tooltip: {
            trigger: "axis"
        },
        grid: {
            left: 50,
            right: 30,
            top: 40,
            bottom: 60
        },
        xAxis: {
            type: "category",
            data: revenue.map(x => formatDate(x.date)),
            axisLabel: commonAxisLabel
        },
        yAxis: {
            type: "value"
        },
        series: [{
            type: "line",
            smooth: true,
            data: revenue.map(x => x.revenue || 0)
        }]
    });

    // =====================================================
    // STORE
    // =====================================================

    const store = safe(dashboardData.revenueByStore);

    createChart("storeChart", {
        tooltip: {
            trigger: "axis"
        },
        grid: {
            left: 50,
            right: 30,
            top: 40,
            bottom: 80
        },
        xAxis: {
            type: "category",
            data: store.map(x => x.name || ""),
            axisLabel: commonAxisLabel
        },
        yAxis: {
            type: "value"
        },
        series: [{
            type: "bar",
            data: store.map(x => x.revenue || 0)
        }]
    });

    // =====================================================
    // TOP DRINKS (FIX dấu ?)
    // =====================================================

    const drinks = safe(dashboardData.topDrinks);

    createChart("topDrinkChart", {
        tooltip: {
            trigger: "axis"
        },
        grid: {
            left: 50,
            right: 30,
            top: 40,
            bottom: 100
        },
        xAxis: {
            type: "category",
            data: drinks.map(x =>
                x.drinkName ||
                x.name ||
                x.title ||
                "Không tên"
            ),
            axisLabel: {
                ...commonAxisLabel,
                width: 140,
                overflow: "break"
            }
        },
        yAxis: {
            type: "value"
        },
        series: [{
            type: "bar",
            data: drinks.map(x =>
                x.totalSold ||
                x.quantity ||
                x.total ||
                0
            )
        }]
    });

    // =====================================================
    // PAYMENT
    // =====================================================

    const payments = safe(dashboardData.payments);

    createChart("paymentChart", {
        tooltip: {
            trigger: "item"
        },
        series: [{
            type: "pie",
            radius: "70%",
            label: {
                fontFamily: "Segoe UI, Arial, sans-serif"
            },
            data: payments.map(x => ({
                name: x.name || "",
                value: x.revenue || 0
            }))
        }]
    });

    setTimeout(resizeCharts, 300);
    setTimeout(resizeCharts, 800);
});