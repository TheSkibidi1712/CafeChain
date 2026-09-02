(() => {
    'use strict';

    const app = document.getElementById('profitabilityApp');
    if (!app) return;

    const catalog = window.CafeChainUiCatalog?.read('profitabilityUiCatalog') ?? {};
    const t = (key, values) => window.CafeChainUiCatalog?.text(catalog, key, values) ?? catalog[key] ?? key;
    const locale = document.documentElement.dataset.culture || 'vi-VN';
    const numberFormatter = new Intl.NumberFormat(locale, { maximumFractionDigits: 5 });
    const percentFormatter = new Intl.NumberFormat(locale, { maximumFractionDigits: 2 });
    const currencyFormatter = new Intl.NumberFormat(locale, {
        style: 'currency', currency: 'VND', maximumFractionDigits: 0
    });
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
    let policyPayload = { policies: [], options: [], legacyReviews: [] };

    const currency = value => value == null ? t('Profit.Js.IncompleteData') : currencyFormatter.format(Number(value));
    const percent = value => value == null ? '—' : `${percentFormatter.format(Number(value))}%`;
    const number = value => numberFormatter.format(Number(value ?? 0));
    const escapeHtml = value => String(value ?? '').replace(/[&<>'"]/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' })[char]);
    const isComplete = row => row.costStatus === 'COMPLETE';

    const statusLabel = code => t(`Profit.Status.${code}`);
    const treatmentLabel = code => t(`Profit.Treatment.${code}`);

    async function request(url, options = {}) {
        const headers = { Accept: 'application/json', ...(options.headers || {}) };
        if (options.method && options.method !== 'GET') headers.RequestVerificationToken = token;
        if (options.body) headers['Content-Type'] = 'application/json';
        const response = await fetch(url, { credentials: 'same-origin', ...options, headers });
        const payload = await response.json().catch(() => ({ success: false, message: `HTTP ${response.status}` }));
        if (!response.ok || payload.success === false) {
            const error = new Error(payload.message || t('Profit.Js.RequestFailed'));
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
        els.timestamp.textContent = new Intl.DateTimeFormat(locale, { dateStyle: 'short', timeStyle: 'medium' }).format(new Date(data.costTimestampUtc));
        els.sizeCount.textContent = rows.length;
        els.readyCount.textContent = rows.filter(isComplete).length;
        els.rows.innerHTML = rows.map(renderRow).join('');
        setState('tableWrap');
    }

    function renderRow(row) {
        const ready = isComplete(row);
        const statusClass = ready ? 'pf-status-ready' : row.knownCost > 0 ? 'pf-status-warning' : 'pf-status-error';
        const priceButton = canUpdatePrice
            ? `<button type="button" class="pf-row-button" data-price-id="${row.drinkSizeId}"><i class="fas fa-pen me-1"></i>${escapeHtml(t('Profit.Js.UpdatePrice'))}</button>` : '';
        const policyButton = canManagePolicy
            ? `<button type="button" class="pf-row-button" data-policy-id="${row.drinkSizeId}"><i class="fas fa-cookie-bite me-1"></i>Topping</button>` : '';
        return `<tr>
            <td><span class="pf-size-name">${escapeHtml(row.sizeName)}</span><span class="pf-subtext">${escapeHtml(row.recipeCode || t('Profit.Js.NoBom'))} · ${escapeHtml(statusLabel(row.recipeStatus))}</span></td>
            <td><span class="pf-status ${statusClass}">${escapeHtml(statusLabel(row.costStatus))}</span><span class="pf-subtext">${escapeHtml(row.costMessage)}</span></td>
            <td class="pf-number">${currency(row.estimatedCost)}${!ready && row.knownCost > 0 ? `<span class="pf-subtext">${escapeHtml(t('Profit.Js.KnownCost'))}: ${currency(row.knownCost)}</span>` : ''}</td>
            <td class="pf-number">${currency(row.currentGlobalPrice)}${row.defaultToppingPriceImpact ? `<span class="pf-subtext">Topping: +${currency(row.defaultToppingPriceImpact)}</span>` : ''}</td>
            <td class="pf-number">${currency(row.grossProfit)}</td>
            <td class="pf-number">${percent(row.grossMarginPercent)}</td>
            <td class="pf-number">${percent(row.markupPercent)}</td>
            <td><div class="pf-row-actions"><button type="button" class="pf-row-button" data-detail-id="${row.drinkSizeId}"><i class="fas fa-info-circle me-1"></i>${escapeHtml(t('Profit.Js.Details'))}</button>${priceButton}${policyButton}</div></td>
        </tr>
        <tr class="pf-detail-row" id="detail-${row.drinkSizeId}" hidden><td colspan="8">${renderDetails(row)}</td></tr>`;
    }

    function renderDetails(row) {
        const sections = row.costSections?.map(x => `<article class="pf-completeness-item"><span>${escapeHtml(x.label)}</span><strong class="pf-status ${x.status === 'COMPLETE' ? 'pf-status-ready' : 'pf-status-warning'}">${escapeHtml(statusLabel(x.status))}</strong><small>${escapeHtml(x.message)}</small></article>`).join('') ?? '';
        const components = row.components?.length ? row.components.map(x => `<tr><td>${escapeHtml(x.itemName)}<span class="pf-subtext">${escapeHtml(x.itemTypeLabel)} · ${escapeHtml(x.sourceLabel)}</span></td><td>${number(x.requiredQuantity)} ${escapeHtml(x.unitName)}</td><td>${number(x.coveredQuantity)} ${escapeHtml(x.unitName)}</td><td>${currency(x.knownCost)}</td><td>${escapeHtml(statusLabel(x.status))}<span class="pf-subtext">${escapeHtml(x.message)}</span></td></tr>`).join('') : `<tr><td colspan="5">${escapeHtml(t('Profit.Js.NoComponents'))}</td></tr>`;
        const toppings = row.defaultToppings?.length ? row.defaultToppings.map(x => `<tr><td>${escapeHtml(x.toppingName)}</td><td>${number(x.quantityPerDrink)} ${escapeHtml(t('Profit.Js.RecipePortion'))}</td><td>${escapeHtml(treatmentLabel(x.priceTreatment))}</td><td>${escapeHtml(treatmentLabel(x.costTreatment))}</td><td>${currency(x.priceImpact)}</td><td>${currency(x.costImpact)}</td></tr>`).join('') : `<tr><td colspan="6">${escapeHtml(t('Profit.Js.NoDefaultToppings'))}</td></tr>`;
        return `<div class="pf-completeness-grid">${sections}</div><div class="pf-detail-grid"><section><h4>${escapeHtml(t('Profit.Js.CostAllocation'))}</h4><div class="pf-table-wrap"><table class="pf-mini-table"><thead><tr><th>${escapeHtml(t('Profit.Js.Component'))}</th><th>${escapeHtml(t('Profit.Js.RequiredQuantity'))}</th><th>${escapeHtml(t('Profit.Js.CoveredQuantity'))}</th><th>${escapeHtml(t('Profit.Js.CalculatedCost'))}</th><th>${escapeHtml(t('Profit.Js.Status'))}</th></tr></thead><tbody>${components}</tbody></table></div></section><section><h4>${escapeHtml(t('Profit.Js.DefaultToppings'))}</h4><div class="pf-table-wrap"><table class="pf-mini-table"><thead><tr><th>Topping</th><th>${escapeHtml(t('Profit.Js.Quantity'))}</th><th>${escapeHtml(t('Profit.Js.PriceImpact'))}</th><th>${escapeHtml(t('Profit.Js.CostImpact'))}</th><th>${escapeHtml(t('Profit.Js.PriceIncrease'))}</th><th>${escapeHtml(t('Profit.Js.CostIncrease'))}</th></tr></thead><tbody>${toppings}</tbody></table></div></section></div>`;
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
            const difference = data.roundedSuggestedPrice - selectedRow.currentGlobalPrice;
            result.innerHTML = `<strong>${escapeHtml(t('Profit.Js.SuggestedPrice', { price: currency(data.roundedSuggestedPrice) }))}</strong><br>${escapeHtml(t('Profit.Js.CurrentPrice', { price: currency(selectedRow.currentGlobalPrice) }))} · ${escapeHtml(t('Profit.Js.Difference', { value: `${difference >= 0 ? '+' : ''}${currency(difference)}` }))}<br>${escapeHtml(t('Profit.Js.NewProfit', { value: currency(data.effectiveGrossProfit) }))} · ${escapeHtml(t('Profit.Js.NewMargin', { value: percent(data.effectiveMarginPercent) }))} · ${escapeHtml(t('Profit.Js.NewMarkup', { value: percent(data.effectiveMarkupPercent) }))} <button type="button" class="pf-row-button" id="applySuggestion">${escapeHtml(t('Profit.Js.UsePrice'))}</button>`;
            result.hidden = false;
            document.getElementById('applySuggestion').addEventListener('click', () => { document.getElementById('newSellingPrice').value = data.roundedSuggestedPrice; });
        } catch (error) {
            showInline('priceFormError', error.message);
        } finally { button.disabled = false; }
    }

    async function savePrice() {
        if (!selectedRow) return;
        const reason = document.getElementById('priceReason').value.trim();
        if (!reason) {
            showInline('priceFormError', t('Profit.Js.PriceReasonRequired'));
            return;
        }
        if (!isComplete(selectedRow) && !document.getElementById('confirmIncomplete').checked) {
            showInline('priceFormError', t('Profit.Js.IncompleteConfirmation'));
            return;
        }
        const button = document.getElementById('savePrice');
        button.disabled = true;
        try {
            await request(`/Admin/AdminDrinkProfitability/UpdatePrice?storeId=${encodeURIComponent(els.store.value)}`, { method: 'POST', body: JSON.stringify({
                drinkSizeId: selectedRow.drinkSizeId,
                newSellingPrice: Number(document.getElementById('newSellingPrice').value),
                expectedRowVersion: selectedRow.rowVersion,
                reason,
                confirmIncompleteCost: document.getElementById('confirmIncomplete').checked
            }) });
            window.toast?.(t('Profit.Js.PriceUpdated'), 'success');
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
        const activeMarkup = policyPayload.policies.length ? policyPayload.policies.map(x => `<article class="pf-policy-item"><div><strong>${escapeHtml(x.toppingName)}</strong><p>${number(x.quantityPerDrink)} ${escapeHtml(t('Profit.Js.RecipePortion'))} · ${escapeHtml(treatmentLabel(x.priceTreatment))} · ${escapeHtml(treatmentLabel(x.costTreatment))}${x.isDefaultSelected ? ` · ${escapeHtml(t('Profit.Js.DefaultOnPos'))}` : ''}${x.isRequired ? ` · ${escapeHtml(t('Profit.Js.Required'))}` : ''}</p></div><button type="button" class="pf-row-button" data-edit-policy="${x.policyId}">${escapeHtml(t('Profit.Js.Edit'))}</button></article>`).join('') : `<div class="pf-empty-policy">${escapeHtml(t('Profit.Js.NoPolicies'))}</div>`;
        const reviewMarkup = (policyPayload.legacyReviews ?? []).map(x => `<article class="pf-policy-item pf-policy-review"><div><strong>${escapeHtml(x.toppingName)}</strong><p>${escapeHtml(x.message)}</p></div><span class="pf-status pf-status-warning">${escapeHtml(t('Profit.Js.NeedsReview'))}</span></article>`).join('');
        list.innerHTML = activeMarkup + reviewMarkup;
        document.getElementById('policyTopping').innerHTML = policyPayload.options.map(x => `<option value="${x.toppingId}">${escapeHtml(x.name)} · ${currency(x.price)}</option>`).join('');
        document.getElementById('policyFormSection').hidden = policyPayload.options.length === 0;
        if (policyPayload.options.length === 0) list.insertAdjacentHTML('beforeend', `<div class="pf-empty-policy">${escapeHtml(t('Profit.Js.NoEligibleToppings'))}</div>`);
        list.querySelectorAll('[data-edit-policy]').forEach(button => button.addEventListener('click', () => editPolicy(button.dataset.editPolicy)));
    }

    function editPolicy(id) {
        const policy = policyPayload.policies.find(x => x.policyId === Number(id));
        if (!policy) return;
        document.getElementById('policyFormTitle').textContent = t('Profit.Js.EditPolicy', { name: policy.toppingName });
        document.getElementById('policyId').value = policy.policyId;
        document.getElementById('policyRowVersion').value = policy.rowVersion;
        document.getElementById('policyTopping').value = policy.toppingId;
        document.getElementById('policyTopping').disabled = true;
        document.getElementById('priceTreatment').value = policy.priceTreatment;
        document.getElementById('costTreatment').value = policy.costTreatment;
        document.getElementById('policyQuantity').value = policy.quantityPerDrink;
        document.getElementById('policyQuantityUnit').value = policy.quantityUnit || 'RECIPE_PORTION';
        document.getElementById('policyDefaultSelected').checked = policy.isDefaultSelected;
        document.getElementById('policyRequired').checked = policy.isRequired;
        document.getElementById('policyReason').value = '';
    }

    function resetPolicyForm() {
        document.getElementById('policyFormTitle').textContent = t('Profit.Js.AddPolicy');
        document.getElementById('policyId').value = '';
        document.getElementById('policyRowVersion').value = '';
        document.getElementById('policyTopping').disabled = false;
        document.getElementById('priceTreatment').value = 'INCLUDED_IN_BASE_PRICE';
        document.getElementById('costTreatment').value = 'INCLUDED_IN_DRINK_RECIPE';
        document.getElementById('policyQuantity').value = '1';
        document.getElementById('policyQuantityUnit').value = 'RECIPE_PORTION';
        document.getElementById('policyDefaultSelected').checked = true;
        document.getElementById('policyRequired').checked = false;
        document.getElementById('policyReason').value = '';
        document.getElementById('policyFormError').hidden = true;
    }

    async function savePolicy() {
        if (!selectedRow) return;
        const reason = document.getElementById('policyReason').value.trim();
        if (!reason) {
            showInline('policyFormError', t('Profit.Js.PolicyReasonRequired'));
            return;
        }
        const button = document.getElementById('savePolicy');
        button.disabled = true;
        try {
            await request('/Admin/AdminDrinkProfitability/UpsertToppingPolicy', { method: 'POST', body: JSON.stringify({
                policyId: Number(document.getElementById('policyId').value) || null,
                drinkSizeId: selectedRow.drinkSizeId,
                toppingId: Number(document.getElementById('policyTopping').value),
                isDefaultSelected: document.getElementById('policyDefaultSelected').checked,
                isRequired: document.getElementById('policyRequired').checked,
                priceTreatment: document.getElementById('priceTreatment').value,
                costTreatment: document.getElementById('costTreatment').value,
                quantityPerDrink: Number(document.getElementById('policyQuantity').value),
                quantityUnit: document.getElementById('policyQuantityUnit').value,
                isActive: true,
                expectedRowVersion: document.getElementById('policyRowVersion').value || null,
                reason
            }) });
            window.toast?.(t('Profit.Js.PolicySaved'), 'success');
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
    loadPreview();
})();
