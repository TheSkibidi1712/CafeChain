(() => {
    'use strict';

    const app = document.getElementById('profitabilityApp');
    if (!app) return;

    const canUpdatePrice = app.dataset.canUpdatePrice === 'true';
    const canManagePolicy = app.dataset.canManagePolicy === 'true';
    const token = app.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
    const els = {
        store: document.getElementById('storeSelect'), drink: document.getElementById('drinkSelect'),
        refresh: document.getElementById('refreshPreview'), retry: document.getElementById('retryPreview'),
        loading: document.getElementById('loadingState'), error: document.getElementById('errorState'),
        errorMessage: document.getElementById('errorMessage'), empty: document.getElementById('emptyState'),
        tableWrap: document.getElementById('tableWrap'), rows: document.getElementById('profitabilityRows'),
        meta: document.getElementById('previewMeta'), timestamp: document.getElementById('costTimestamp'),
        sizeCount: document.getElementById('sizeCount'), readyCount: document.getElementById('readyCount'),
        priceDrawer: document.getElementById('priceDrawer'),
        policyDrawer: document.getElementById('policyDrawer')
    };
    let preview = null;
    let selectedRow = null;
    let policyPayload = { policies: [], options: [] };

    const currency = value => value == null ? 'Chưa đủ dữ liệu' : `${Number(value).toLocaleString('vi-VN')}đ`;
    const percent = value => value == null ? '—' : `${Number(value).toLocaleString('vi-VN', { maximumFractionDigits: 2 })}%`;
    const number = value => Number(value ?? 0).toLocaleString('vi-VN', { maximumFractionDigits: 5 });
    const escapeHtml = value => String(value ?? '').replace(/[&<>'"]/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' })[char]);
    const isComplete = row => row.costStatus === 'COMPLETE';

    const statusLabels = {
        EXACT_READY: 'BOM đúng size', GENERIC_FALLBACK_ONLY: 'Chỉ có BOM fallback', MISSING_RECIPE: 'Thiếu BOM theo size',
        MULTIPLE_ACTIVE_RECIPE: 'Trùng BOM hiệu lực', INVALID_RECIPE: 'BOM không hợp lệ', FUTURE_RECIPE_ONLY: 'BOM chưa đến ngày hiệu lực',
        COMPLETE: 'Đầy đủ', INCOMPLETE: 'Chưa đầy đủ', MISSING_DEFAULT_TOPPING_POLICY: 'Thiếu policy topping',
        MISSING_COST_LAYER: 'Thiếu lớp giá FIFO', INSUFFICIENT_COST_QUANTITY: 'Không đủ lượng FIFO',
        MISSING_CONVERSION: 'Thiếu quy đổi', INVALID_BOM: 'BOM không hợp lệ'
    };
    const treatmentLabels = {
        INCLUDED_IN_BASE_PRICE: 'Đã gồm trong giá gốc', ADD_TOPPING_PRICE: 'Cộng giá topping',
        INCLUDED_IN_DRINK_RECIPE: 'Đã gồm trong BOM đồ uống', ADD_TOPPING_RECIPE_COST: 'Cộng BOM topping', DISPLAY_ONLY: 'Chỉ hiển thị'
    };

    async function request(url, options = {}) {
        const headers = { Accept: 'application/json', ...(options.headers || {}) };
        if (options.method && options.method !== 'GET') headers.RequestVerificationToken = token;
        if (options.body) headers['Content-Type'] = 'application/json';
        const response = await fetch(url, { credentials: 'same-origin', ...options, headers });
        const payload = await response.json().catch(() => ({ success: false, message: `HTTP ${response.status}` }));
        if (!response.ok || payload.success === false) {
            const error = new Error(payload.message || 'Không thể xử lý yêu cầu.');
            error.code = payload.errorCode;
            throw error;
        }
        return payload;
    }

    function setState(name, message = '') {
        ['loading', 'error', 'empty', 'tableWrap'].forEach(key => { els[key].hidden = key !== name; });
        if (name === 'error') els.errorMessage.textContent = message;
        els.meta.hidden = name !== 'tableWrap';
    }

    async function loadPreview() {
        if (!els.store.value || !els.drink.value) {
            setState('empty');
            return;
        }
        setState('loading');
        els.refresh.disabled = true;
        try {
            const payload = await request(`/Admin/AdminDrinkProfitability/Preview?storeId=${encodeURIComponent(els.store.value)}&drinkId=${encodeURIComponent(els.drink.value)}`);
            preview = payload.data;
            renderPreview(preview);
        } catch (error) {
            setState('error', error.message);
        } finally {
            els.refresh.disabled = false;
        }
    }

    function renderPreview(data) {
        const rows = data?.sizes ?? [];
        if (rows.length === 0) { setState('empty'); return; }
        els.timestamp.textContent = new Date(data.costTimestampUtc).toLocaleString('vi-VN');
        els.sizeCount.textContent = rows.length;
        els.readyCount.textContent = rows.filter(isComplete).length;
        els.rows.innerHTML = rows.map(renderRow).join('');
        setState('tableWrap');
    }

    function renderRow(row) {
        const ready = isComplete(row);
        const statusClass = ready ? 'pf-status-ready' : row.knownCost > 0 ? 'pf-status-warning' : 'pf-status-error';
        const priceButton = canUpdatePrice
            ? `<button type="button" class="pf-row-button" data-price-id="${row.drinkSizeId}"><i class="fas fa-pen"></i> Cập nhật giá</button>` : '';
        const policyButton = canManagePolicy
            ? `<button type="button" class="pf-row-button" data-policy-id="${row.drinkSizeId}"><i class="fas fa-sliders"></i> Topping</button>` : '';
        return `<tr>
            <td><span class="pf-size-name">${escapeHtml(row.sizeName)}</span><span class="pf-subtext">${escapeHtml(row.recipeCode || 'Chưa có BOM')} · ${escapeHtml(statusLabels[row.recipeStatus] || row.recipeStatus)}</span></td>
            <td><span class="pf-status ${statusClass}">${escapeHtml(statusLabels[row.costStatus] || row.costStatus)}</span><span class="pf-subtext">${escapeHtml(row.costMessage)}</span></td>
            <td class="pf-number">${currency(row.estimatedCost)}${!ready && row.knownCost > 0 ? `<span class="pf-subtext">Đã biết: ${currency(row.knownCost)}</span>` : ''}</td>
            <td class="pf-number">${currency(row.currentGlobalPrice)}${row.defaultToppingPriceImpact ? `<span class="pf-subtext">Topping: +${currency(row.defaultToppingPriceImpact)}</span>` : ''}</td>
            <td class="pf-number">${currency(row.grossProfit)}</td>
            <td class="pf-number">${percent(row.grossMarginPercent)}</td>
            <td class="pf-number">${percent(row.markupPercent)}</td>
            <td><div class="pf-row-actions"><button type="button" class="pf-row-button" data-detail-id="${row.drinkSizeId}">Chi tiết</button>${priceButton}${policyButton}</div></td>
        </tr>
        <tr class="pf-detail-row" id="detail-${row.drinkSizeId}" hidden><td colspan="8">${renderDetails(row)}</td></tr>`;
    }

    function renderDetails(row) {
        const components = row.components?.length ? row.components.map(x => `<tr><td>${escapeHtml(x.itemName)}<span class="pf-subtext">${escapeHtml(x.source)}</span></td><td>${number(x.requiredQuantity)} ${escapeHtml(x.unitName)}</td><td>${number(x.coveredQuantity)}</td><td>${currency(x.knownCost)}</td><td>${escapeHtml(statusLabels[x.status] || x.status)}<span class="pf-subtext">${escapeHtml(x.message)}</span></td></tr>`).join('') : '<tr><td colspan="5">Chưa có thành phần chi phí.</td></tr>';
        const toppings = row.defaultToppings?.length ? row.defaultToppings.map(x => `<tr><td>${escapeHtml(x.toppingName)}</td><td>${number(x.quantityPerDrink)}</td><td>${escapeHtml(treatmentLabels[x.priceTreatment] || x.priceTreatment)}</td><td>${escapeHtml(treatmentLabels[x.costTreatment] || x.costTreatment)}</td><td>${currency(x.priceImpact)}</td><td>${currency(x.costImpact)}</td></tr>`).join('') : '<tr><td colspan="6">Chưa có topping mặc định được chọn.</td></tr>';
        return `<div class="pf-detail-grid"><section><h4>Phân bổ FIFO mô phỏng</h4><div class="pf-table-wrap"><table class="pf-mini-table"><thead><tr><th>Thành phần</th><th>Cần</th><th>Có giá</th><th>Chi phí biết được</th><th>Trạng thái</th></tr></thead><tbody>${components}</tbody></table></div></section><section><h4>Topping mặc định</h4><div class="pf-table-wrap"><table class="pf-mini-table"><thead><tr><th>Topping</th><th>SL</th><th>Giá</th><th>Vốn</th><th>Ảnh hưởng giá</th><th>Ảnh hưởng vốn</th></tr></thead><tbody>${toppings}</tbody></table></div></section></div>`;
    }

    function findRow(id) { return preview?.sizes?.find(x => x.drinkSizeId === Number(id)); }
    function openDrawer(drawer) {
        [els.priceDrawer, els.policyDrawer]
            .filter(item => item !== drawer)
            .forEach(item => bootstrap.Offcanvas.getInstance(item)?.hide());
        bootstrap.Offcanvas.getOrCreateInstance(drawer).show();
    }
    function closeDrawers() {
        [els.priceDrawer, els.policyDrawer].forEach(item => bootstrap.Offcanvas.getInstance(item)?.hide());
    }

    function openPrice(id) {
        selectedRow = findRow(id);
        if (!selectedRow) return;
        document.getElementById('priceDrawerTitle').textContent = `${preview.drinkName} · ${selectedRow.sizeName}`;
        document.getElementById('drawerCost').textContent = currency(selectedRow.estimatedCost);
        document.getElementById('drawerCurrentPrice').textContent = currency(selectedRow.currentGlobalPrice);
        document.getElementById('newSellingPrice').value = selectedRow.currentGlobalPrice;
        document.getElementById('priceReason').value = '';
        document.getElementById('targetValue').value = '';
        document.getElementById('suggestionResult').hidden = true;
        document.getElementById('priceFormError').hidden = true;
        document.getElementById('incompleteConfirmation').hidden = isComplete(selectedRow);
        document.getElementById('confirmIncomplete').checked = false;
        document.getElementById('calculateSuggestion').disabled = !isComplete(selectedRow);
        openDrawer(els.priceDrawer);
    }

    async function calculateSuggestion() {
        if (!selectedRow || !isComplete(selectedRow)) return;
        const button = document.getElementById('calculateSuggestion');
        button.disabled = true;
        try {
            const payload = await request('/Admin/AdminDrinkProfitability/Suggest', { method: 'POST', body: JSON.stringify({
                estimatedCost: selectedRow.estimatedCost,
                currentSellingPrice: selectedRow.currentGlobalPrice,
                targetMode: document.getElementById('targetMode').value,
                targetValue: Number(document.getElementById('targetValue').value),
                roundingMode: document.getElementById('roundingMode').value
            }) });
            const data = payload.data;
            const result = document.getElementById('suggestionResult');
            result.innerHTML = `<strong>${currency(data.roundedSuggestedPrice)}</strong><br>Giá thô: ${currency(data.rawSuggestedPrice)} · Lời: ${currency(data.effectiveGrossProfit)} · Margin: ${percent(data.effectiveMarginPercent)} · Markup: ${percent(data.effectiveMarkupPercent)} <button type="button" class="pf-row-button" id="applySuggestion">Dùng giá này</button>`;
            result.hidden = false;
            document.getElementById('applySuggestion').addEventListener('click', () => { document.getElementById('newSellingPrice').value = data.roundedSuggestedPrice; });
        } catch (error) {
            showInline('priceFormError', error.message);
        } finally { button.disabled = false; }
    }

    async function savePrice() {
        if (!selectedRow) return;
        const reason = document.getElementById('priceReason').value.trim();
        if (!isComplete(selectedRow) && (!document.getElementById('confirmIncomplete').checked || !reason)) {
            showInline('priceFormError', 'Giá vốn chưa đầy đủ. Hãy xác nhận cảnh báo và nhập lý do.');
            return;
        }
        const button = document.getElementById('savePrice');
        button.disabled = true;
        try {
            await request(`/Admin/AdminDrinkProfitability/UpdatePrice?storeId=${encodeURIComponent(els.store.value)}`, { method: 'POST', body: JSON.stringify({
                drinkSizeId: selectedRow.drinkSizeId,
                newSellingPrice: Number(document.getElementById('newSellingPrice').value),
                expectedRowVersion: selectedRow.rowVersion,
                reason: reason || null
            }) });
            window.toast?.('Đã cập nhật giá bán toàn hệ thống.', 'success');
            closeDrawers();
            await loadPreview();
        } catch (error) { showInline('priceFormError', error.message); }
        finally { button.disabled = false; }
    }

    async function openPolicies(id) {
        selectedRow = findRow(id);
        if (!selectedRow) return;
        document.getElementById('policyDrawerTitle').textContent = `${preview.drinkName} · ${selectedRow.sizeName}`;
        document.getElementById('policyList').innerHTML = '<div class="pf-state"><span class="pf-spinner"></span></div>';
        resetPolicyForm();
        openDrawer(els.policyDrawer);
        try {
            const payload = await request(`/Admin/AdminDrinkProfitability/ToppingPolicies?drinkSizeId=${selectedRow.drinkSizeId}`);
            policyPayload = payload.data;
            renderPolicies();
        } catch (error) { document.getElementById('policyList').innerHTML = `<div class="pf-inline-error">${escapeHtml(error.message)}</div>`; }
    }

    function renderPolicies() {
        const list = document.getElementById('policyList');
        list.innerHTML = policyPayload.policies.length ? policyPayload.policies.map(x => `<article class="pf-policy-item"><div><strong>${escapeHtml(x.toppingName)}</strong><p>${number(x.quantityPerDrink)} · ${escapeHtml(treatmentLabels[x.priceTreatment])} · ${escapeHtml(treatmentLabels[x.costTreatment])}${x.isDefaultSelected ? ' · Chọn mặc định' : ''}</p></div><button type="button" class="pf-row-button" data-edit-policy="${x.policyId}">Sửa</button></article>`).join('') : '<div class="pf-inline-error">Chưa có policy active. Giá vốn sẽ báo thiếu nếu topping đang là mặc định legacy.</div>';
        document.getElementById('policyTopping').innerHTML = policyPayload.options.map(x => `<option value="${x.toppingId}">${escapeHtml(x.name)} · ${currency(x.price)}</option>`).join('');
        list.querySelectorAll('[data-edit-policy]').forEach(button => button.addEventListener('click', () => editPolicy(button.dataset.editPolicy)));
    }

    function editPolicy(id) {
        const policy = policyPayload.policies.find(x => x.policyId === Number(id));
        if (!policy) return;
        document.getElementById('policyFormTitle').textContent = `Sửa ${policy.toppingName}`;
        document.getElementById('policyId').value = policy.policyId;
        document.getElementById('policyRowVersion').value = policy.rowVersion;
        document.getElementById('policyTopping').value = policy.toppingId;
        document.getElementById('policyTopping').disabled = true;
        document.getElementById('priceTreatment').value = policy.priceTreatment;
        document.getElementById('costTreatment').value = policy.costTreatment;
        document.getElementById('policyQuantity').value = policy.quantityPerDrink;
        document.getElementById('policyDefaultSelected').checked = policy.isDefaultSelected;
        document.getElementById('policyReason').value = '';
    }

    function resetPolicyForm() {
        document.getElementById('policyFormTitle').textContent = 'Thêm chính sách';
        document.getElementById('policyId').value = '';
        document.getElementById('policyRowVersion').value = '';
        document.getElementById('policyTopping').disabled = false;
        document.getElementById('priceTreatment').value = 'INCLUDED_IN_BASE_PRICE';
        document.getElementById('costTreatment').value = 'INCLUDED_IN_DRINK_RECIPE';
        document.getElementById('policyQuantity').value = '1';
        document.getElementById('policyDefaultSelected').checked = true;
        document.getElementById('policyReason').value = '';
        document.getElementById('policyFormError').hidden = true;
    }

    async function savePolicy() {
        if (!selectedRow) return;
        const button = document.getElementById('savePolicy');
        button.disabled = true;
        try {
            await request('/Admin/AdminDrinkProfitability/UpsertToppingPolicy', { method: 'POST', body: JSON.stringify({
                policyId: Number(document.getElementById('policyId').value) || null,
                drinkSizeId: selectedRow.drinkSizeId,
                toppingId: Number(document.getElementById('policyTopping').value),
                isDefaultSelected: document.getElementById('policyDefaultSelected').checked,
                priceTreatment: document.getElementById('priceTreatment').value,
                costTreatment: document.getElementById('costTreatment').value,
                quantityPerDrink: Number(document.getElementById('policyQuantity').value),
                isActive: true,
                expectedRowVersion: document.getElementById('policyRowVersion').value || null,
                reason: document.getElementById('policyReason').value.trim() || null
            }) });
            window.toast?.('Đã lưu chính sách topping mặc định.', 'success');
            await openPolicies(selectedRow.drinkSizeId);
        } catch (error) { showInline('policyFormError', error.message); }
        finally { button.disabled = false; }
    }

    function showInline(id, message) { const el = document.getElementById(id); el.textContent = message; el.hidden = false; }

    els.refresh.addEventListener('click', loadPreview);
    els.retry.addEventListener('click', loadPreview);
    els.rows.addEventListener('click', event => {
        const detail = event.target.closest('[data-detail-id]');
        const price = event.target.closest('[data-price-id]');
        const policy = event.target.closest('[data-policy-id]');
        if (detail) document.getElementById(`detail-${detail.dataset.detailId}`).hidden = !document.getElementById(`detail-${detail.dataset.detailId}`).hidden;
        if (price) openPrice(price.dataset.priceId);
        if (policy) openPolicies(policy.dataset.policyId);
    });
    document.getElementById('calculateSuggestion').addEventListener('click', calculateSuggestion);
    document.getElementById('savePrice').addEventListener('click', savePrice);
    document.getElementById('savePolicy').addEventListener('click', savePolicy);
    document.getElementById('costTreatment').addEventListener('change', event => { if (event.target.value === 'DISPLAY_ONLY') document.getElementById('priceTreatment').value = 'INCLUDED_IN_BASE_PRICE'; });
    loadPreview();
})();
