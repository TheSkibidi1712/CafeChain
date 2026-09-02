(() => {
    'use strict';

    const app = document.getElementById('storeMenuApp');
    if (!app) return;
    const catalog = window.CafeChainUiCatalog?.read('storeMenuUiCatalog') ?? {};
    const t = (key, values) => window.CafeChainUiCatalog?.text(catalog, key, values) ?? catalog[key] ?? key;
    const locale = document.documentElement.dataset.culture || 'vi-VN';

    const permissions = {
        publish: app.dataset.canPublish === 'true',
        operate: app.dataset.canOperate === 'true',
        provision: app.dataset.canProvision === 'true',
        override: app.dataset.canOverridePrice === 'true'
    };
    const token = app.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const els = {
        store: document.getElementById('storeMenuStore'),
        search: document.getElementById('storeMenuSearch'),
        configured: document.getElementById('configuredStatusFilter'),
        operational: document.getElementById('operationalStatusFilter'),
        refresh: document.getElementById('refreshStoreMenu'),
        provision: document.getElementById('provisionStoreMenu'),
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

    const configuredLabels = Object.fromEntries(['DRAFT', 'SCHEDULED', 'ACTIVE', 'PAUSED', 'ENDED']
        .map(code => [code, t(`StoreMenu.Status.${({ DRAFT: 'Draft', SCHEDULED: 'Scheduled', ACTIVE: 'Active', PAUSED: 'Paused', ENDED: 'Ended' })[code]}`)]));
    const operationalKeys = { AVAILABLE: 'Available', LOW_STOCK: 'LowStock', OUT_OF_STOCK: 'OutOfStock', RECIPE_INVALID: 'InvalidRecipe', TOPPING_UNAVAILABLE: 'Topping', STORE_NOT_READY: 'Store' };
    const operationalLabels = Object.fromEntries(Object.entries(operationalKeys).map(([code, key]) => [code, t(`StoreMenu.Availability.${key}`)]));
    operationalLabels.UNKNOWN = t('StoreMenu.Js.Unknown');
    const actionMeta = {
        PUBLISH: ['Publish'], PAUSE: ['Pause'], RESUME: ['Resume'], CHANGE_DISPLAY_ORDER: ['Order'], SET_PRICE_OVERRIDE: ['Override'], USE_GLOBAL_PRICE: ['Global']
    };
    Object.values(actionMeta).forEach(meta => meta.splice(0, 1,
        t(`StoreMenu.Action.${meta[0]}.Title`), t(`StoreMenu.Action.${meta[0]}.Hint`), t(`StoreMenu.Action.${meta[0]}.Submit`)));

    let allRows = [];
    let selected = null;
    let loading = false;
    let toastTimer = null;

    const escapeHtml = value => String(value ?? '')
        .replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;').replaceAll("'", '&#039;');
    const moneyFormatter = new Intl.NumberFormat(locale, { style: 'currency', currency: 'VND', maximumFractionDigits: 0 });
    const money = value => value == null ? t('StoreMenu.Js.IncompleteData') : moneyFormatter.format(Number(value));
    const dateTime = value => value ? new Intl.DateTimeFormat(locale, {
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
            const error = new Error(payload.message || t('StoreMenu.Js.RequestFailed'));
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
                els.emptyTitle.textContent = t('StoreMenu.Js.NoStores');
                els.emptyMessage.textContent = t('StoreMenu.Js.NoStoresHint');
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

    async function provisionMissing() {
        if (!permissions.provision || !els.store?.value || !els.provision) return;
        els.provision.disabled = true;
        try {
            const payload = await request(`/Admin/AdminStoreMenu/ProvisionMissing?storeId=${encodeURIComponent(els.store.value)}`, {
                method: 'POST'
            });
            showToast(payload.message || t('StoreMenu.Js.SyncSuccess'));
            await loadRows();
        } catch (error) {
            showToast(error.message);
        } finally {
            els.provision.disabled = false;
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
        const query = els.search.value.trim().toLocaleLowerCase(locale);
        const configured = els.configured.value;
        const operational = els.operational.value;
        const rows = allRows.filter(row => {
            const haystack = `${row.drinkCode} ${row.drinkName} ${row.sizeName} ${row.categoryName}`.toLocaleLowerCase(locale);
            return (!query || haystack.includes(query))
                && (!configured || row.configuredStatus === configured)
                && (!operational || row.operationalStatus === operational);
        });
        renderRows(rows);
    }

    function renderRows(rows) {
        if (!rows.length) {
            setMainState('empty');
            els.emptyTitle.textContent = allRows.length ? t('StoreMenu.Js.NoFilteredItems') : t('StoreMenu.Js.NoItems');
            els.emptyMessage.textContent = allRows.length
                ? t('StoreMenu.Js.ClearFiltersHint')
                : t('StoreMenu.Js.SetupHint');
            return;
        }
        els.rows.innerHTML = rows.map(rowTemplate).join('');
        setMainState('table');
    }

    function rowTemplate(row) {
        const configured = configuredLabels[row.configuredStatus] || row.configuredStatus;
        const operational = operationalLabels[row.operationalStatus] || row.operationalStatus;
        const margin = row.estimatedGrossMarginPercent == null ? t('StoreMenu.Js.NotCalculated') : `${new Intl.NumberFormat(locale, { maximumFractionDigits: 2 }).format(Number(row.estimatedGrossMarginPercent))}%`;
        const from = dateTime(row.effectiveFromUtc);
        const to = dateTime(row.effectiveToUtc);
        const window = !from && !to ? escapeHtml(t('StoreMenu.Js.NoLimit')) : `${from ? escapeHtml(t('StoreMenu.Js.From', { value: from })) : escapeHtml(t('StoreMenu.Js.Immediate'))}<br>${to ? escapeHtml(t('StoreMenu.Js.To', { value: to })) : escapeHtml(t('StoreMenu.Js.NoEnd'))}`;
        return `<tr data-menu-id="${row.storeMenuItemId}">
            <td class="sm-number"><strong>${row.displayOrder}</strong></td>
            <td><div class="sm-sku"><span class="sm-sku-icon"><i class="fas fa-mug-hot"></i></span><div><strong title="${escapeHtml(row.drinkName)}">${escapeHtml(row.drinkName)}</strong><span>${escapeHtml(row.drinkCode)} · ${escapeHtml(row.sizeName)}</span><span>${escapeHtml(row.categoryName)}</span></div></div></td>
            <td><div class="sm-status-stack"><span class="sm-badge sm-badge-${statusTone(row.configuredStatus)}">${escapeHtml(configured)}</span><span class="sm-badge sm-badge-${statusTone(row.operationalStatus)}" title="${escapeHtml(row.availabilityReason)}">${escapeHtml(operational)}</span><span class="sm-muted-line" title="${escapeHtml(row.availabilityReason)}">${escapeHtml(row.availabilityReason)}</span></div></td>
            <td class="sm-number"><div class="sm-price-stack"><strong>${money(row.effectivePrice)}</strong><span class="sm-price-source">${escapeHtml(row.priceSource === 'STORE_OVERRIDE' ? t('StoreMenu.Js.StorePrice') : t('StoreMenu.Js.GlobalPrice'))}</span><span class="sm-muted-line">${escapeHtml(t('StoreMenu.Js.GlobalPrice'))}: ${money(row.globalPrice)}${row.storeOverride != null ? `<br>${escapeHtml(t('StoreMenu.Js.StorePrice'))}: ${money(row.storeOverride)}` : ''}</span></div></td>
            <td class="sm-number"><div class="sm-cost-stack"><strong>${money(row.fifoCost)}</strong><span class="${row.estimatedGrossMarginPercent != null && row.estimatedGrossMarginPercent < 0 ? 'sm-badge sm-badge-danger' : 'sm-muted-line'}">${escapeHtml(t('StoreMenu.Js.Margin', { value: margin }))}</span><span class="sm-muted-line">${escapeHtml(t(`Profit.Status.${row.costStatus}`))}</span></div></td>
            <td><div class="sm-window">${window}</div></td>
            <td><div class="sm-row-actions">${actionButtons(row)}</div></td>
        </tr>`;
    }

    function actionButtons(row) {
        const buttons = [];
        if (permissions.publish && row.configuredStatus === 'DRAFT') buttons.push(button('PUBLISH', 'fa-cloud-arrow-up', actionMeta.PUBLISH[0]));
        if (permissions.operate && ['ACTIVE', 'SCHEDULED'].includes(row.configuredStatus)) buttons.push(button('PAUSE', 'fa-pause', actionMeta.PAUSE[0]));
        if (permissions.operate && row.configuredStatus === 'PAUSED') buttons.push(button('RESUME', 'fa-play', actionMeta.RESUME[0]));
        if (permissions.operate) buttons.push(button('CHANGE_DISPLAY_ORDER', 'fa-arrow-down-1-9', actionMeta.CHANGE_DISPLAY_ORDER[0]));
        if (permissions.override) buttons.push(button('SET_PRICE_OVERRIDE', 'fa-tag', actionMeta.SET_PRICE_OVERRIDE[0]));
        if (permissions.override && row.storeOverride != null) buttons.push(button('USE_GLOBAL_PRICE', 'fa-rotate-left', actionMeta.USE_GLOBAL_PRICE[0]));
        if (row.recipeId) buttons.push(link(`/Admin/AdminRecipe/Edit/${row.recipeId}`, 'fa-flask', t('StoreMenu.Action.ViewBom')));
        buttons.push(link(`/Admin/AdminDrinkProfitability?storeId=${row.storeId}&drinkId=${row.drinkId}`, 'fa-chart-line', t('StoreMenu.Action.ViewProfit')));
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
        els.drawerSummary.innerHTML = `<strong>${escapeHtml(row.drinkName)} · ${escapeHtml(row.sizeName)}</strong><span>${escapeHtml(t('StoreMenu.Js.EffectivePrice', { value: money(row.effectivePrice) }))} · ${escapeHtml(configuredLabels[row.configuredStatus] || row.configuredStatus)}</span><span>${escapeHtml(meta[1])}</span>`;
        els.priceFields.hidden = !['SET_PRICE_OVERRIDE'].includes(action);
        els.orderFields.hidden = action !== 'CHANGE_DISPLAY_ORDER';
        els.overridePrice.value = action === 'SET_PRICE_OVERRIDE' ? (row.storeOverride ?? row.effectivePrice) : '';
        els.displayOrder.value = row.displayOrder;
        els.reason.value = '';
        els.actionError.hidden = true;
        els.submit.textContent = meta[2];
        els.submit.disabled = false;
        bootstrap.Offcanvas.getOrCreateInstance(els.drawer).show();
        els.drawer.addEventListener('shown.bs.offcanvas', () =>
            (action === 'CHANGE_DISPLAY_ORDER' ? els.displayOrder : action === 'SET_PRICE_OVERRIDE' ? els.overridePrice : els.reason).focus(),
            { once: true });
    }

    function closeDrawer() {
        bootstrap.Offcanvas.getOrCreateInstance(els.drawer).hide();
    }

    async function submitAction(event) {
        event.preventDefault();
        if (!selected) return;
        const action = els.actionType.value;
        const reason = els.reason.value.trim();
        if (!reason) return showActionError(t('StoreMenu.Js.ReasonRequired'));
        if (action === 'SET_PRICE_OVERRIDE' && Number(els.overridePrice.value) <= 0)
            return showActionError(t('StoreMenu.Js.PriceInvalid'));
        if (action === 'CHANGE_DISPLAY_ORDER' && Number(els.displayOrder.value) < 0)
            return showActionError(t('StoreMenu.Js.OrderInvalid'));

        els.submit.disabled = true;
        els.submit.textContent = t('StoreMenu.Js.Saving');
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
            showToast(payload.message || t('StoreMenu.Js.UpdateSuccess'));
            await loadRows();
        } catch (error) {
            showActionError(error.message);
            if (error.status === 409) els.submit.textContent = t('StoreMenu.Js.ReloadToContinue');
        } finally {
            els.submit.disabled = false;
            if (selected) els.submit.textContent = actionMeta[action]?.[2] || t('StoreMenu.Js.Confirm');
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
    els.provision?.addEventListener('click', provisionMissing);
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
    els.drawer?.addEventListener('hidden.bs.offcanvas', () => { selected = null; });

    loadRows();
})();
