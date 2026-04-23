document.addEventListener("DOMContentLoaded", function () {

    function safe(arr) {
        return Array.isArray(arr) ? arr : [];
    }

    function formatDate(d) {
        return new Date(d).toLocaleDateString('vi-VN');
    }

    const stores = safe(dashboardData.stores);

    const provinceSelect = document.getElementById("provinceId");
    const districtSelect = document.getElementById("districtId");
    const storeSelect = document.getElementById("storeId");

    function filterDropdown() {
        const province = provinceSelect?.value;
        const district = districtSelect?.value;

        let filtered = stores;

        if (province) {
            filtered = filtered.filter(x => x.provinceId == province);
        }

        if (district) {
            filtered = filtered.filter(x => x.districtId == district);
        }

        // STORE
        if (storeSelect) {
            storeSelect.innerHTML = `<option value="">All Store</option>`;
            filtered.forEach(s => {
                storeSelect.innerHTML += `<option value="${s.storeId}">${s.storeName}</option>`;
            });
        }
    }

    provinceSelect?.addEventListener("change", function () {
        districtSelect.value = "";
        filterDropdown();
    });

    districtSelect?.addEventListener("change", function () {
        filterDropdown();
    });

    function getParams() {
        const params = new URLSearchParams();

        const from = document.getElementById("fromDate").value;
        const to = document.getElementById("toDate").value;
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

    document.getElementById("btnApply")?.addEventListener("click", function () {
        window.location.href = "/Admin/Dashboard?" + getParams();
    });

    // ================= CHARTS =================

    const revenue = safe(dashboardData.revenue);

    echarts.init(document.getElementById('revenueChart')).setOption({
        xAxis: { type: 'category', data: revenue.map(x => formatDate(x.date)) },
        yAxis: { type: 'value' },
        series: [{ type: 'line', data: revenue.map(x => x.revenue), smooth: true }]
    });

    const store = safe(dashboardData.revenueByStore);

    echarts.init(document.getElementById('storeChart')).setOption({
        xAxis: { type: 'category', data: store.map(x => x.name) },
        yAxis: { type: 'value' },
        series: [{ type: 'bar', data: store.map(x => x.revenue) }]
    });

    const drinks = safe(dashboardData.topDrinks);

    echarts.init(document.getElementById('topDrinkChart')).setOption({
        xAxis: { type: 'category', data: drinks.map(x => x.drinkName) },
        yAxis: { type: 'value' },
        series: [{ type: 'bar', data: drinks.map(x => x.totalSold) }]
    });

    const payments = safe(dashboardData.payments);

    echarts.init(document.getElementById('paymentChart')).setOption({
        series: [{
            type: 'pie',
            data: payments.map(x => ({ name: x.name, value: x.revenue }))
        }]
    });

}); 