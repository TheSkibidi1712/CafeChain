(() => {
    'use strict';

    const app = document.getElementById('storeMenuApp');
    if (!app) return;

    const permissions = {
        publish: app.dataset.canPublish === 'true',
        operate: app.dataset.canOperate === 'true',
        override: app.dataset.canOverridePrice === 'true'
    };
    const token = app.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const els = {
        store: document.getElementById('storeMenuStore'),
        search: document.getElementById('storeMenuSearch'),
        configured: document.getElementById('configuredStatusFilter'),
        operational: document.getElementById('operationalStatusFilter'),
        refresh: document.getElementById('refreshStoreMenu'),
        retry: document.getElementById('retryStoreMenu'),
        summary: document.getElementById('storeMenuSummary'),
        loading: document.getElementById('storeMenuLoading'),
        error: document.getElementById('storeMenuError'),
        errorMessage: document.getElementById('storeMenuErrorMessage'),
        empty: document.getElementById('storeMenuEmpty'),
        emptyTitle: document.getElementById('storeMenuEmptyTitle'),
        emptyMessage: document.getElementById('storeMenuEmptyMessage'),
        tableWrap: document.getElementById('storeMenuTableWrap'),
        rows: document.getElementById('storeMenuRows'),
        total: document.getElementById('totalSkuCount'),
        active: document.getElementById('activeSkuCount'),
        unavailable: document.getElementById('unavailableSkuCount'),
        overrides: document.getElementById('overrideSkuCount'),
        backdrop: document.getElementById('storeMenuDrawerBackdrop'),
        drawer: document.getElementById('storeMenuDrawer'),
        drawerEyebrow: document.getElementById('storeMenuDrawerEyebrow'),
        drawerTitle: document.getElementById('storeMenuDrawerTitle'),
        drawerSummary: document.getElementById('storeMenuDrawerSummary'),
        form: document.getElementById('storeMenuActionForm'),
        actionType: document.getElementById('storeMenuActionType'),
        priceFields: document.getElementById('storeMenuPriceFields'),
        overridePrice: document.getElementById('storeMenuOverridePrice'),
        orderFields: document.getElementById('storeMenuOrderFields'),
        displayOrder: document.getElementById('storeMenuDisplayOrder'),
        reason: document.getElementById('storeMenuReason'),
        actionError: document.getElementById('storeMenuActionError'),
        submit: document.getElementById('storeMenuSubmitAction'),
        toast: document.getElementById('storeMenuToast')
    };

    const configuredLabels = {
        DRAFT: 'Bản nháp', SCHEDULED: 'Đã lên lịch', ACTIVE: 'Đang bán',
        PAUSED: 'Tạm dừng', ENDED: 'Đã kết thúc'
    };
    const operationalLabels = {
        AVAILABLE: 'Sẵn sàng', LOW_STOCK: 'Sắp hết', OUT_OF_STOCK: 'Hết nguyên liệu',
        RECIPE_INVALID: 'BOM chưa hợp lệ', TOPPING_UNAVAILABLE: 'Thiếu topping bắt buộc',
        STORE_NOT_READY: 'Cửa hàng chưa sẵn sàng', UNKNOWN: 'Chưa xác định'
    };
    const actionMeta = {
        PUBLISH: ['Publish SKU', 'SKU sẽ xuất hiện trên catalog cửa hàng khi đủ điều kiện bán.', 'Publish'],
        PAUSE: ['Tạm dừng SKU', 'Thu ngân sẽ không thể bán SKU này tại cửa hàng.', 'Xác nhận tạm dừng'],
        RESUME: ['Mở bán lại SKU', 'SKU được đưa lại vào catalog nếu BOM và tồn kho sẵn sàng.', 'Mở bán lại'],
        CHANGE_DISPLAY_ORDER: ['Đổi thứ tự hiển thị', 'Số nhỏ hơn được ưu tiên hiển thị trước trên POS.', 'Lưu thứ tự'],
        SET_PRICE_OVERRIDE: ['Đặt giá riêng cửa hàng', 'Giá này thay thế giá toàn hệ thống cho đúng SKU tại cửa hàng.', 'Áp dụng giá riêng'],
        USE_GLOBAL_PRICE: ['Dùng giá toàn hệ thống', 'Giá riêng sẽ bị xóa và SKU quay về giá global hiện tại.', 'Dùng giá toàn hệ thống']
    };

    let allRows = [];
    let selected = null;
    let loading = false;
    let toastTimer = null;

    const escapeHtml = value => String(value ?? '')
        .replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;').replaceAll("'", '&#039;');
    const money = value => value == null ? 'Chưa đủ dữ liệu' : `${Number(value).toLocaleString('vi-VN')} đ`;
    const dateTime = value => value ? new Intl.DateTimeFormat('vi-VN', {
        dateStyle: 'short', timeStyle: 'short'
    }).format(new Date(value)) : null;
    const statusTone = status => {
        if (status === 'AVAILABLE' || status === 'ACTIVE') return 'success';
        if (status === 'LOW_STOCK' || status === 'SCHEDULED' || status === 'DRAFT') return 'warning';
        if (status === 'PAUSED' || status === 'ENDED') return 'neutral';
        return 'danger';
    };

    async function request(url, options = {}) {
        const headers = { Accept: 'application/json', ...(options.headers || {}) };
        if (options.body) headers['Content-Type'] = 'application/json';
        if (options.method && options.method !== 'GET') headers.RequestVerificationToken = token;
        const response = await fetch(url, { ...options, headers });
        const payload = await response.json().catch(() => ({ success: false, message: `HTTP ${response.status}` }));
        if (!response.ok || payload.success === false) {
            const error = new Error(payload.message || 'Không thể xử lý yêu cầu.');
            error.code = payload.errorCode;
            error.status = response.status;
            throw error;
        }
        return payload;
    }

    function setMainState(name, message = '') {
        els.loading.hidden = name !== 'loading';
        els.error.hidden = name !== 'error';
        els.empty.hidden = name !== 'empty';
        els.tableWrap.hidden = name !== 'table';
        if (name === 'error') els.errorMessage.textContent = message;
    }

    async function loadRows() {
        if (loading || !els.store?.value) {
            if (!els.store?.value) {
                setMainState('empty');
                els.emptyTitle.textContent = 'Không có cửa hàng trong phạm vi được cấp quyền.';
                els.emptyMessage.textContent = 'Liên hệ quản trị viên để kiểm tra role và phạm vi cửa hàng.';
            }
            return;
        }
        loading = true;
        setMainState('loading');
        els.summary.hidden = true;
        try {
            const payload = await request(`/Admin/AdminStoreMenu/Rows?storeId=${encodeURIComponent(els.store.value)}`);
            allRows = Array.isArray(payload.data) ? payload.data : [];
            updateSummary();
            applyFilters();
        } catch (error) {
            allRows = [];
            setMainState('error', error.message);
        } finally {
            loading = false;
        }
    }

    function updateSummary() {
        els.total.textContent = allRows.length;
        els.active.textContent = allRows.filter(x => x.configuredStatus === 'ACTIVE').length;
        els.unavailable.textContent = allRows.filter(x => !x.isSellable).length;
        els.overrides.textContent = allRows.filter(x => x.storeOverride != null).length;
        els.summary.hidden = false;
    }

    function applyFilters() {
        const query = els.search.value.trim().toLocaleLowerCase('vi');
        const configured = els.configured.value;
        const operational = els.operational.value;
        const rows = allRows.filter(row => {
            const haystack = `${row.drinkCode} ${row.drinkName} ${row.sizeName} ${row.categoryName}`.toLocaleLowerCase('vi');
            return (!query || haystack.includes(query))
                && (!configured || row.configuredStatus === configured)
                && (!operational || row.operationalStatus === operational);
        });
        renderRows(rows);
    }

    function renderRows(rows) {
        if (!rows.length) {
            setMainState('empty');
            els.emptyTitle.textContent = allRows.length ? 'Không có SKU phù hợp bộ lọc.' : 'Chưa có SKU trong menu cửa hàng.';
            els.emptyMessage.textContent = allRows.length
                ? 'Thử xóa từ khóa hoặc chọn trạng thái khác.'
                : 'Dữ liệu StoreMenuItem cần được backfill/publish trước khi vận hành.';
            return;
        }
        els.rows.innerHTML = rows.map(rowTemplate).join('');
        setMainState('table');
    }

    function rowTemplate(row) {
        const configured = configuredLabels[row.configuredStatus] || row.configuredStatus;
        const operational = operationalLabels[row.operationalStatus] || row.operationalStatus;
        const margin = row.estimatedGrossMarginPercent == null ? 'Chưa tính được' : `${Number(row.estimatedGrossMarginPercent).toLocaleString('vi-VN', { maximumFractionDigits: 2 })}%`;
        const from = dateTime(row.effectiveFromUtc);
        const to = dateTime(row.effectiveToUtc);
        const window = !from && !to ? 'Không giới hạn' : `${from ? `Từ ${from}` : 'Có hiệu lực ngay'}<br>${to ? `Đến ${to}` : 'Không có ngày kết thúc'}`;
        return `<tr data-menu-id="${row.storeMenuItemId}">
            <td class="sm-number"><strong>${row.displayOrder}</strong></td>
            <td><div class="sm-sku"><span class="sm-sku-icon"><i class="fas fa-mug-hot"></i></span><div><strong title="${escapeHtml(row.drinkName)}">${escapeHtml(row.drinkName)}</strong><span>${escapeHtml(row.drinkCode)} · ${escapeHtml(row.sizeName)}</span><span>${escapeHtml(row.categoryName)}</span></div></div></td>
            <td><div class="sm-status-stack"><span class="sm-badge sm-badge-${statusTone(row.configuredStatus)}">${escapeHtml(configured)}</span><span class="sm-badge sm-badge-${statusTone(row.operationalStatus)}" title="${escapeHtml(row.availabilityReason)}">${escapeHtml(operational)}</span><span class="sm-muted-line" title="${escapeHtml(row.availabilityReason)}">${escapeHtml(row.availabilityReason)}</span></div></td>
            <td class="sm-number"><div class="sm-price-stack"><strong>${money(row.effectivePrice)}</strong><span class="sm-price-source">${row.priceSource === 'STORE_OVERRIDE' ? 'Giá riêng cửa hàng' : 'Giá toàn hệ thống'}</span><span class="sm-muted-line">Global: ${money(row.globalPrice)}${row.storeOverride != null ? `<br>Override: ${money(row.storeOverride)}` : ''}</span></div></td>
            <td class="sm-number"><div class="sm-cost-stack"><strong>${money(row.fifoCost)}</strong><span class="${row.estimatedGrossMarginPercent != null && row.estimatedGrossMarginPercent < 0 ? 'sm-badge sm-badge-danger' : 'sm-muted-line'}">Margin ${escapeHtml(margin)}</span><span class="sm-muted-line">${escapeHtml(row.costStatus)}</span></div></td>
            <td><div class="sm-window">${window}</div></td>
            <td><div class="sm-row-actions">${actionButtons(row)}</div></td>
        </tr>`;
    }

    function actionButtons(row) {
        const buttons = [];
        if (permissions.publish && row.configuredStatus === 'DRAFT') buttons.push(button('PUBLISH', 'fa-cloud-arrow-up', 'Publish SKU'));
        if (permissions.operate && ['ACTIVE', 'SCHEDULED'].includes(row.configuredStatus)) buttons.push(button('PAUSE', 'fa-pause', 'Tạm dừng'));
        if (permissions.operate && row.configuredStatus === 'PAUSED') buttons.push(button('RESUME', 'fa-play', 'Mở bán lại'));
        if (permissions.operate) buttons.push(button('CHANGE_DISPLAY_ORDER', 'fa-arrow-down-1-9', 'Đổi thứ tự'));
        if (permissions.override) buttons.push(button('SET_PRICE_OVERRIDE', 'fa-tag', 'Đặt giá riêng'));
        if (permissions.override && row.storeOverride != null) buttons.push(button('USE_GLOBAL_PRICE', 'fa-rotate-left', 'Dùng giá global'));
        if (row.recipeId) buttons.push(link(`/Admin/AdminRecipe/Edit/${row.recipeId}`, 'fa-flask', 'Xem BOM'));
        buttons.push(link(`/Admin/AdminDrinkProfitability?storeId=${row.storeId}&drinkId=${row.drinkId}`, 'fa-chart-line', 'Xem lợi nhuận'));
        return buttons.join('');
    }

    const button = (action, icon, title) => `<button type="button" class="sm-row-button" data-action="${action}" title="${title}"><i class="fas ${icon}"></i></button>`;
    const link = (href, icon, title) => `<a class="sm-row-button" href="${href}" title="${title}"><i class="fas ${icon}"></i></a>`;

    function openDrawer(row, action) {
        const meta = actionMeta[action];
        if (!meta) return;
        selected = row;
        els.actionType.value = action;
        els.drawerEyebrow.textContent = `${row.drinkCode} · ${row.sizeName}`;
        els.drawerTitle.textContent = meta[0];
        els.drawerSummary.innerHTML = `<strong>${escapeHtml(row.drinkName)} · ${escapeHtml(row.sizeName)}</strong><span>Giá hiệu lực ${money(row.effectivePrice)} · ${escapeHtml(configuredLabels[row.configuredStatus] || row.configuredStatus)}</span><span>${escapeHtml(meta[1])}</span>`;
        els.priceFields.hidden = !['SET_PRICE_OVERRIDE'].includes(action);
        els.orderFields.hidden = action !== 'CHANGE_DISPLAY_ORDER';
        els.overridePrice.value = action === 'SET_PRICE_OVERRIDE' ? (row.storeOverride ?? row.effectivePrice) : '';
        els.displayOrder.value = row.displayOrder;
        els.reason.value = '';
        els.actionError.hidden = true;
        els.submit.textContent = meta[2];
        els.submit.disabled = false;
        els.backdrop.hidden = false;
        els.drawer.classList.add('is-open');
        els.drawer.setAttribute('aria-hidden', 'false');
        document.body.style.overflow = 'hidden';
        window.setTimeout(() => (action === 'CHANGE_DISPLAY_ORDER' ? els.displayOrder : action === 'SET_PRICE_OVERRIDE' ? els.overridePrice : els.reason).focus(), 50);
    }

    function closeDrawer() {
        els.drawer.classList.remove('is-open');
        els.drawer.setAttribute('aria-hidden', 'true');
        els.backdrop.hidden = true;
        document.body.style.overflow = '';
        selected = null;
    }

    async function submitAction(event) {
        event.preventDefault();
        if (!selected) return;
        const action = els.actionType.value;
        const reason = els.reason.value.trim();
        if (!reason) return showActionError('Bắt buộc nhập lý do thay đổi.');
        if (action === 'SET_PRICE_OVERRIDE' && Number(els.overridePrice.value) <= 0)
            return showActionError('Giá riêng phải lớn hơn 0.');
        if (action === 'CHANGE_DISPLAY_ORDER' && Number(els.displayOrder.value) < 0)
            return showActionError('Thứ tự hiển thị phải từ 0 trở lên.');

        els.submit.disabled = true;
        els.submit.textContent = 'Đang lưu...';
        els.actionError.hidden = true;
        try {
            const isPrice = ['SET_PRICE_OVERRIDE', 'USE_GLOBAL_PRICE'].includes(action);
            const body = isPrice ? {
                storeMenuItemId: selected.storeMenuItemId,
                priceOverride: action === 'SET_PRICE_OVERRIDE' ? Number(els.overridePrice.value) : null,
                expectedRowVersion: selected.rowVersion,
                reason
            } : {
                storeMenuItemId: selected.storeMenuItemId,
                action,
                displayOrder: action === 'CHANGE_DISPLAY_ORDER' ? Number(els.displayOrder.value) : null,
                expectedRowVersion: selected.rowVersion,
                reason
            };
            const payload = await request(isPrice
                ? '/Admin/AdminStoreMenu/UpdatePriceOverride'
                : '/Admin/AdminStoreMenu/UpdateLifecycle', {
                method: 'POST', body: JSON.stringify(body)
            });
            closeDrawer();
            showToast(payload.message || 'Đã cập nhật menu cửa hàng.');
            await loadRows();
        } catch (error) {
            showActionError(error.message);
            if (error.status === 409) els.submit.textContent = 'Tải lại để tiếp tục';
        } finally {
            els.submit.disabled = false;
            if (selected) els.submit.textContent = actionMeta[action]?.[2] || 'Xác nhận';
        }
    }

    function showActionError(message) {
        els.actionError.textContent = message;
        els.actionError.hidden = false;
    }

    function showToast(message) {
        window.clearTimeout(toastTimer);
        els.toast.textContent = message;
        els.toast.hidden = false;
        toastTimer = window.setTimeout(() => { els.toast.hidden = true; }, 3500);
    }

    els.store?.addEventListener('change', loadRows);
    els.refresh?.addEventListener('click', loadRows);
    els.retry?.addEventListener('click', loadRows);
    els.search?.addEventListener('input', applyFilters);
    els.configured?.addEventListener('change', applyFilters);
    els.operational?.addEventListener('change', applyFilters);
    els.rows?.addEventListener('click', event => {
        const trigger = event.target.closest('[data-action]');
        if (!trigger) return;
        const row = allRows.find(x => x.storeMenuItemId === Number(trigger.closest('tr')?.dataset.menuId));
        if (row) openDrawer(row, trigger.dataset.action);
    });
    els.form?.addEventListener('submit', submitAction);
    els.backdrop?.addEventListener('click', closeDrawer);
    document.querySelectorAll('[data-close-drawer]').forEach(x => x.addEventListener('click', closeDrawer));
    document.addEventListener('keydown', event => { if (event.key === 'Escape' && selected) closeDrawer(); });

    loadRows();
})();
