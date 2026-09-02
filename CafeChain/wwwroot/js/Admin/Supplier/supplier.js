(() => {
    'use strict';

    const API = '/Admin/AdminSupplier';
    const i18n = window.supplierI18n || {};
    const t = (key, ...args) => {
        const template = i18n[key] ?? key;
        if (!args.length) return template;
        return String(template).replace(/\{(\d+)\}/g, (_, index) => args[Number(index)] ?? '');
    };
    const page = document.querySelector('.supplier-page');
    if (!page) return;

    const canMutate = page.dataset.canMutate === 'true';
    const state = {
        supplierId: null,
        detail: null,
        offers: [],
        stores: [],
        ingredients: [],
        units: [],
        pricingOffer: null,
        offerToggleRequests: new Set(),
        duplicateWarningId: null,
        duplicateMatches: [],
        loaded: { reference: false, offers: false, stores: false, audit: false }
    };

    const $ = (selector, root = document) => root.querySelector(selector);
    const $$ = (selector, root = document) => [...root.querySelectorAll(selector)];
    const detailPanel = $('#supplierDetail');
    const detailContent = $('#supplierDetailContent');
    const detailPlaceholder = $('#supplierDetailPlaceholder');
    const offerList = $('#offerList');

    const escapeHtml = (value) => String(value ?? '')
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#039;');

    const formatMoney = (value) => `${new Intl.NumberFormat('vi-VN').format(Number(value || 0))} đ`;
    const formatDate = (value) => value
        ? new Intl.DateTimeFormat('vi-VN', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
        : t('noData');

    async function api(path, options = {}) {
        const init = { ...options, headers: { Accept: 'application/json', ...(options.headers || {}) } };
        if (options.body && typeof options.body !== 'string') {
            init.headers['Content-Type'] = 'application/json';
            init.body = JSON.stringify(options.body);
        }
        const response = await fetch(`${API}${path}`, init);
        let payload;
        try { payload = await response.json(); }
        catch { throw new Error(t('invalidResponse')); }
        if (!response.ok || payload.success === false) {
            const error = new Error(payload.message || t('requestFailed', response.status));
            error.payload = payload;
            throw error;
        }
        return payload;
    }

    function toast(message, type = 'success') {
        const item = document.createElement('div');
        item.className = `supplier-toast${type === 'error' ? ' is-error' : ''}`;
        item.textContent = message;
        $('#supplierToasts').append(item);
        window.setTimeout(() => item.remove(), 4200);
    }

    function setBusy(element, busy) {
        if (!element) return;
        element.classList.toggle('supplier-loading', busy);
        $$('button, input, select, textarea', element).forEach(control => {
            if (busy) {
                control.dataset.wasDisabled = control.disabled ? 'true' : 'false';
                control.disabled = true;
            } else if (control.dataset.wasDisabled !== 'true') {
                control.disabled = false;
            }
        });
    }

    function emptyStack(message) {
        return `<div class="supplier-empty-inline">${escapeHtml(message)}</div>`;
    }

    function normalizeTaxCode(value) {
        if (!value || !value.trim()) return null;
        let compact = value.trim().replace(/\s+/g, '').replace(/[‐‑‒–−]/g, '-');
        if (/^\d{13}$/.test(compact)) compact = `${compact.slice(0, 10)}-${compact.slice(10)}`;
        return /^\d{10}(-\d{3})?$/.test(compact) ? compact : undefined;
    }

    function isValidEmail(email) {
        if (!email) return true;
        return /^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/.test(email.trim());
    }

    function isValidPhone(phone) {
        if (!phone) return true;
        const clean = phone.replace(/[\s.\-\+()]/g, '');
        return /^\d{8,11}$/.test(clean);
    }

    function validateEmailInput(input, labelName = t('labelEmailDefault')) {
        if (!input) return true;
        const val = input.value.trim();
        if (val && !isValidEmail(val)) {
            input.setCustomValidity(t('errorEmailFormat', labelName.toLowerCase()));
            return false;
        }
        input.setCustomValidity('');
        return true;
    }

    function validatePhoneInput(input, isRequired = false, labelName = t('labelPhoneDefault')) {
        if (!input) return true;
        const val = input.value.trim();
        if (isRequired && !val) {
            input.setCustomValidity(t('errorRequired', labelName.toLowerCase()));
            return false;
        }
        if (val && !isValidPhone(val)) {
            input.setCustomValidity(t('errorPhoneFormat', labelName));
            return false;
        }
        input.setCustomValidity('');
        return true;
    }

    function setFieldError(input, error, message) {
        if (!input || !error) return;
        input.classList.toggle('is-invalid', Boolean(message));
        input.setAttribute('aria-invalid', String(Boolean(message)));
        error.textContent = message || '';
        error.classList.toggle('is-hidden', !message);
    }

    function validateTaxCodeInput(input, error) {
        const normalized = normalizeTaxCode(input?.value || '');
        if (normalized === undefined) {
            setFieldError(input, error, t('errorTaxCodeFormat'));
            input?.focus();
            return undefined;
        }
        if (input) input.value = normalized || '';
        setFieldError(input, error, '');
        return normalized;
    }

    function selectTab(tabName) {
        $$('.supplier-tabs button').forEach(button => button.classList.toggle('is-active', button.dataset.tab === tabName));
        $$('.supplier-tab').forEach(panel => panel.classList.toggle('is-active', panel.dataset.tabPanel === tabName));
    }

    async function ensureTabData(tabName) {
        try {
            if (tabName === 'offers' && !state.loaded.offers) {
                $('#offerList').innerHTML = emptyStack(t('loadingOffers'));
                await Promise.all([loadOffers(), loadReferenceData()]);
            } else if (tabName === 'stores' && !state.loaded.stores) {
                $('#storeList').innerHTML = emptyStack(t('loadingStores'));
                await loadStores();
            } else if (tabName === 'audit' && !state.loaded.audit) {
                $('#supplierAuditEvents').innerHTML = emptyStack(t('loadingAudit'));
                await loadAuditHistory();
            }
        } catch (error) {
            toast(error.message, 'error');
        }
    }

    $$('.supplier-tabs button').forEach(button => button.addEventListener('click', async () => {
        selectTab(button.dataset.tab);
        await ensureTabData(button.dataset.tab);
    }));

    function openDetailShell() {
        detailPlaceholder.classList.add('is-hidden');
        detailContent.classList.remove('is-hidden');
        bootstrap.Offcanvas.getOrCreateInstance(detailPanel).show();
    }

    function closeDetail() {
        bootstrap.Offcanvas.getOrCreateInstance(detailPanel).hide();
    }
    detailPanel?.addEventListener('hidden.bs.offcanvas', () => {
        $$('#supplierRows tr').forEach(row => row.classList.remove('is-selected'));
        detailContent.classList.add('is-hidden');
        detailPlaceholder.classList.remove('is-hidden');
    });

    async function loadReferenceData() {
        if (!canMutate || state.loaded.reference) return;
        const ingredientPayload = await api('/GetIngredientOptions');
        state.ingredients = ingredientPayload.data || [];
        fillSelect($('#offerIngredient'), state.ingredients, 'ingredientId', item => `${item.code} · ${item.name}`);
        fillSelect($('#offerUnit'), [], 'unitId', item => item.name, t('selectIngredientFirst'));
        fillSelect($('#offerLooseUnit'), [], 'unitId', item => item.name, t('selectIngredientFirst'));
        fillSelect($('#newPackageUnit'), [], 'unitId', item => item.name, t('selectOfferFirst'));
        state.loaded.reference = true;
    }

    async function loadCompatibleUnits(ingredientId, selectedContentUnitId = '', selectedLooseUnitId = '') {
        if (!ingredientId) {
            state.units = [];
            fillSelect($('#offerUnit'), [], 'unitId', item => item.name, t('selectIngredientFirst'));
            fillSelect($('#offerLooseUnit'), [], 'unitId', item => item.name, t('selectIngredientFirst'));
            updateLoosePriceLabel();
            return;
        }

        const payload = await api(`/GetCompatibleUnitOptions?ingredientId=${ingredientId}`);
        state.units = payload.data || [];
        fillSelect($('#offerUnit'), state.units, 'unitId', item => `${item.unitCode} · ${item.name}`, t('selectContentUnit'));
        fillSelect($('#offerLooseUnit'), state.units, 'unitId', item => `${item.unitCode} · ${item.name}`, t('selectLooseUnit'));
        if (selectedContentUnitId) $('#offerUnit').value = String(selectedContentUnitId);
        if (selectedLooseUnitId) $('#offerLooseUnit').value = String(selectedLooseUnitId);
        updateLoosePriceLabel();
    }

    function updateLoosePriceLabel() {
        const selected = $('#offerLooseUnit')?.selectedOptions?.[0];
        const code = selected?.textContent?.split('·')?.[0]?.trim();
        $('#offerLoosePriceLabel').textContent = t('loosePriceLabel', code || t('loosePriceDefaultUnit'));
        updateDerivedLoosePrice();
    }

    function updateDerivedLoosePrice() {
        const priceInput = $('#offerLoosePrice');
        const derived = $('#offerLoosePriceMode')?.value === 'DERIVED';
        if (!priceInput) return;
        priceInput.readOnly = derived;
        if (!derived) return;

        const packageQuantity = Number($('#offerPackageQuantity')?.value || 0);
        const packagePrice = Number($('#offerPrice')?.value || 0);
        const packageUnit = state.units.find(item => String(item.unitId) === String($('#offerUnit')?.value));
        const looseUnit = state.units.find(item => String(item.unitId) === String($('#offerLooseUnit')?.value));
        const packageInLoose = packageUnit && looseUnit && Number(looseUnit.conversionFactorToBase) > 0
            ? packageQuantity * Number(packageUnit.conversionFactorToBase) / Number(looseUnit.conversionFactorToBase)
            : 0;
        priceInput.value = packageInLoose > 0 && packagePrice > 0
            ? (packagePrice / packageInLoose).toFixed(2)
            : '';
    }

    function fillSelect(select, items, valueKey, labelFactory, placeholder = t('selectData')) {
        if (!select) return;
        const current = select.value;
        select.innerHTML = `<option value="">${escapeHtml(placeholder)}</option>`;
        for (const item of items) {
            const option = document.createElement('option');
            option.value = item[valueKey];
            option.textContent = labelFactory(item);
            select.append(option);
        }
        if (current) select.value = current;
    }

    async function openSupplier(id) {
        state.supplierId = Number(id);
        state.offers = [];
        state.stores = [];
        state.pricingOffer = null;
        state.loaded = { reference: state.loaded.reference, offers: false, stores: false, audit: false };
        openDetailShell();
        selectTab('overview');
        setBusy(detailContent, true);
        $$('#supplierRows tr').forEach(row => row.classList.toggle('is-selected', Number(row.dataset.supplierId) === state.supplierId));

        try {
            const detailPayload = await api(`/GetById?id=${state.supplierId}`);
            state.detail = detailPayload.data;
            renderDetail();
        } catch (error) {
            toast(error.message, 'error');
            closeDetail();
        } finally {
            setBusy(detailContent, false);
            applyReadOnlyMode();
        }
    }

    $$('.open-supplier').forEach(button => button.addEventListener('click', event => {
        event.stopPropagation();
        openSupplier(button.dataset.id);
    }));
    $$('#supplierRows tr').forEach(row => row.addEventListener('click', event => {
        if (event.target.closest('button')) return;
        openSupplier(row.dataset.supplierId);
    }));

    function renderDetail() {
        const d = state.detail;
        $('#detailCode').textContent = d.code;
        $('#detailName').textContent = d.name;
        $('#detailSummary').textContent = d.address || t('noAddress');
        const detailStatus = $('#detailStatus');
        detailStatus.textContent = d.active ? t('statusActive') : t('statusInactive');
        detailStatus.className = `supplier-status ${d.active ? 'is-active' : 'is-inactive'}`;
        $('#overviewSupplierId').value = d.supplierId;
        $('#overviewRowVersion').value = d.rowVersion || '';
        $('#overviewName').value = d.name || '';
        $('#overviewTaxCode').value = d.taxCode || '';
        $('#overviewAddress').value = d.address || '';
        $('#overviewNote').value = d.note || '';
        $('#overviewActive').value = String(Boolean(d.active));
        $('#auditCreatedAt').textContent = formatDate(d.createdAt);
        $('#auditUpdatedAt').textContent = formatDate(d.updatedAt);
        renderSupplierAudits();
        renderPhones();
        renderContacts();
    }

    function renderSupplierAudits() {
        const root = $('#supplierAuditEvents');
        const rows = state.detail?.audits || [];
        if (!root) return;
        if (!state.loaded.audit) {
            root.innerHTML = emptyStack(t('openAuditTab'));
            return;
        }
        if (!rows.length) {
            root.innerHTML = emptyStack(t('auditEmpty'));
            return;
        }
        root.innerHTML = rows.map(item => `
            <article class="supplier-history-row supplier-audit-event">
                <time datetime="${escapeHtml(item.createdAt)}">${escapeHtml(formatDate(item.createdAt))}</time>
                <div class="supplier-audit-event__summary">
                    <strong>${escapeHtml(item.title || t('auditDefaultTitle'))}</strong>
                    <small>${escapeHtml(item.actorName || t('auditSystemActor'))}${item.actorRole ? ` · ${escapeHtml(item.actorRole)}` : ''}</small>
                </div>
                <dl class="supplier-audit-event__changes">
                    ${(item.changes || []).map(change => `
                        <div>
                            <dt>${escapeHtml(change.label)}</dt>
                            ${change.before != null ? `<dd><span class="supplier-audit-value is-before">${escapeHtml(change.before)}</span><i class="fas fa-arrow-right" aria-hidden="true"></i><span class="supplier-audit-value">${escapeHtml(change.after ?? t('auditEmptyValue'))}</span></dd>` : `<dd><span class="supplier-audit-value">${escapeHtml(change.after ?? t('auditEmptyValue'))}</span></dd>`}
                        </div>`).join('') || `<div><dt>${escapeHtml(t('auditChangeFallback'))}</dt><dd><span class="supplier-audit-value">${escapeHtml(t('auditSavedChanges'))}</span></dd></div>`}
                </dl>
            </article>`).join('');
    }

    async function loadAuditHistory() {
        const supplierId = state.supplierId;
        const payload = await api(`/GetAuditHistory?supplierId=${supplierId}`);
        if (state.supplierId !== supplierId) return;
        state.detail.audits = payload.data || [];
        state.loaded.audit = true;
        renderSupplierAudits();
    }

    function applyReadOnlyMode() {
        if (canMutate) return;
        $$('#supplierDetail input, #supplierDetail select, #supplierDetail textarea').forEach(control => control.disabled = true);
    }

    function renderPhones() {
        const root = $('#phoneList');
        const rows = state.detail?.phones || [];
        if (!rows.length) { root.innerHTML = emptyStack(t('phonesEmpty')); return; }
        root.innerHTML = rows.map(phone => `
            <div class="supplier-stack-item">
                <div class="supplier-stack-item-main"><strong>${escapeHtml(phone.phoneNumber)}</strong><small>${phone.isPrimary ? t('phonesPrimary') : t('phonesSecondary')}</small></div>
                ${canMutate && !phone.isPrimary ? `<div class="supplier-stack-actions"><button type="button" class="supplier-btn supplier-btn-danger delete-phone" data-id="${phone.supplierPhoneId}">${t('phonesDelete')}</button></div>` : ''}
            </div>`).join('');
        $$('.delete-phone', root).forEach(button => button.addEventListener('click', () => deletePhone(button.dataset.id)));
    }

    function renderContacts() {
        const root = $('#contactList');
        const rows = state.detail?.contacts || [];
        if (!rows.length) { root.innerHTML = emptyStack(t('contactsEmpty')); return; }
        root.innerHTML = rows.map(contact => `
            <div class="supplier-stack-item">
                <div class="supplier-stack-item-main">
                    <strong>${escapeHtml(contact.name)} ${contact.isPrimary ? `<span class="supplier-status is-current">${t('contactsPrimaryBadge')}</span>` : ''}</strong>
                    <span>${escapeHtml(contact.position || t('contactsNoPosition'))}</span>
                    <small>${escapeHtml([contact.phone, contact.email].filter(Boolean).join(' · ') || t('contactsNoChannels'))}</small>
                </div>
                ${canMutate ? `<div class="supplier-stack-actions">
                    <button type="button" class="supplier-btn supplier-btn-light edit-contact" data-id="${contact.supplierContactId}">${t('edit')}</button>
                    ${contact.isPrimary ? '' : `<button type="button" class="supplier-btn supplier-btn-light primary-contact" data-id="${contact.supplierContactId}">${t('setPrimary')}</button><button type="button" class="supplier-btn supplier-btn-danger delete-contact" data-id="${contact.supplierContactId}">${t('delete')}</button>`}
                </div>` : ''}
            </div>`).join('');
        $$('.edit-contact', root).forEach(button => button.addEventListener('click', () => beginContactEdit(Number(button.dataset.id))));
        $$('.primary-contact', root).forEach(button => button.addEventListener('click', () => setPrimaryContact(button.dataset.id)));
        $$('.delete-contact', root).forEach(button => button.addEventListener('click', () => deleteContact(button.dataset.id)));
    }

    async function refreshDetailData() {
        const payload = await api(`/GetById?id=${state.supplierId}`);
        state.detail = payload.data;
        renderDetail();
    }

    $('#supplierOverviewForm')?.addEventListener('submit', async event => {
        event.preventDefault();
        if (!canMutate) return;
        const form = event.currentTarget;
        const nameInput = $('#overviewName');
        const taxCodeInput = $('#overviewTaxCode');

        if (nameInput) {
            if (!nameInput.value.trim()) {
                nameInput.setCustomValidity(t('errorNameRequired'));
            } else {
                nameInput.setCustomValidity('');
            }
        }

        const taxCode = validateTaxCodeInput(taxCodeInput, $('#overviewTaxCodeError'));
        if (taxCode === undefined) {
            if (taxCodeInput) {
                taxCodeInput.setCustomValidity(t('errorTaxCodeInvalid'));
                form.reportValidity();
            }
            return;
        } else if (taxCodeInput) {
            taxCodeInput.setCustomValidity('');
        }

        if (!form.checkValidity()) {
            form.reportValidity();
            return;
        }

        setBusy(form, true);
        try {
            await api('/Update', { method: 'POST', body: {
                supplierId: state.supplierId,
                name: $('#overviewName').value.trim(),
                taxCode,
                address: $('#overviewAddress').value.trim() || null,
                note: $('#overviewNote').value.trim() || null,
                active: $('#overviewActive').value === 'true',
                rowVersion: $('#overviewRowVersion').value
            }});
            toast(t('toastSavedSupplier'));
            window.location.reload();
        } catch (error) {
            if (error.payload?.code === 'SUPPLIER_TAX_CODE_INVALID' || error.payload?.code === 'SUPPLIER_TAX_CODE_DUPLICATE') {
                setFieldError($('#overviewTaxCode'), $('#overviewTaxCodeError'), error.message);
                $('#overviewTaxCode')?.focus();
            }
            toast(error.message, 'error');
        }
        finally { setBusy(form, false); }
    });

    $('#addPhoneForm')?.addEventListener('submit', async event => {
        event.preventDefault();
        const form = event.currentTarget;
        const phoneInput = $('#newPhone');
        if (!validatePhoneInput(phoneInput, true, t('labelPhoneNew'))) {
            form.reportValidity();
            return;
        }
        setBusy(form, true);
        try {
            await api('/AddPhone', { method: 'POST', body: { supplierId: state.supplierId, phoneNumber: phoneInput.value.trim() } });
            $('#newPhone').value = '';
            await refreshDetailData();
            toast(t('toastPhoneAdded'));
        } catch (error) { toast(error.message, 'error'); }
        finally { setBusy(form, false); }
    });

    async function deletePhone(id) {
        if (!await requestConfirmation(
            t('deletePhoneTitle'),
            t('deletePhoneText'))) return;
        try { await api(`/DeletePhone?supplierPhoneId=${id}`, { method: 'POST' }); await refreshDetailData(); toast(t('toastPhoneDeleted')); }
        catch (error) { toast(error.message, 'error'); }
    }

    function resetContactForm() {
        $('#contactForm')?.reset();
        $('#contactId').value = '';
        $('#cancelContactEdit')?.classList.add('is-hidden');
        if ($('#saveContactButton')) $('#saveContactButton').textContent = t('contactAdd');
    }

    function beginContactEdit(id) {
        const contact = (state.detail.contacts || []).find(item => item.supplierContactId === id);
        if (!contact) return;
        $('#contactId').value = id;
        $('#contactName').value = contact.name || '';
        $('#contactPosition').value = contact.position || '';
        $('#contactPhone').value = contact.phone || '';
        $('#contactEmail').value = contact.email || '';
        $('#cancelContactEdit').classList.remove('is-hidden');
        $('#saveContactButton').textContent = t('contactSave');
    }
    $('#cancelContactEdit')?.addEventListener('click', resetContactForm);

    $('#contactForm')?.addEventListener('submit', async event => {
        event.preventDefault();
        const id = Number($('#contactId').value || 0);

        const nameInput = $('#contactName');
        if (nameInput) {
            if (!nameInput.value.trim()) {
                nameInput.setCustomValidity(t('errorContactNameRequired'));
            } else {
                nameInput.setCustomValidity('');
            }
        }

        const emailInput = $('#contactEmail');
        validateEmailInput(emailInput, t('labelEmailContact'));

        const phoneInput = $('#contactPhone');
        validatePhoneInput(phoneInput, false, t('labelPhoneContact'));

        const form = event.currentTarget;
        if (!form.checkValidity()) {
            form.reportValidity();
            return;
        }

        const body = {
            supplierContactId: id || undefined,
            supplierId: state.supplierId,
            name: $('#contactName').value.trim(),
            phone: phoneInput.value.trim() || null,
            email: emailInput.value.trim() || null,
            position: $('#contactPosition').value.trim() || null,
            active: true
        };
        setBusy(form, true);
        try {
            await api(id ? '/UpdateContact' : '/AddContact', { method: 'POST', body });
            resetContactForm();
            await refreshDetailData();
            toast(id ? t('toastContactUpdated') : t('toastContactAdded'));
        } catch (error) { toast(error.message, 'error'); }
        finally { setBusy(form, false); }
    });

    async function setPrimaryContact(id) {
        try { await api(`/SetPrimaryContact?supplierContactId=${id}`, { method: 'POST' }); await refreshDetailData(); toast(t('toastPrimaryContactUpdated')); }
        catch (error) { toast(error.message, 'error'); }
    }
    async function deleteContact(id) {
        if (!await requestConfirmation(
            t('deleteContactTitle'),
            t('deleteContactText'))) return;
        try { await api(`/DeleteContact?supplierContactId=${id}`, { method: 'POST' }); await refreshDetailData(); toast(t('toastContactDeleted')); }
        catch (error) { toast(error.message, 'error'); }
    }

    async function loadOffers() {
        const supplierId = state.supplierId;
        const payload = await api(`/GetIngredientOffers?supplierId=${supplierId}`);
        if (state.supplierId !== supplierId) return;
        state.offers = payload.data || [];
        state.loaded.offers = true;
        renderOffers();
    }

    function renderOffers() {
        const root = $('#offerList');
        if (!state.offers.length) { root.innerHTML = emptyStack(t('offersEmpty')); return; }
        root.innerHTML = state.offers.map(offer => `
            <div class="supplier-stack-item">
                <div class="supplier-stack-item-main">
                    <strong>${escapeHtml(offer.ingredientName)} ${offer.isPrimary ? `<span class="supplier-status is-current">${t('offersPrimaryBadge')}</span>` : ''}</strong>
                    <span>${escapeHtml(offer.packageDisplay)} · ${escapeHtml(offer.priceDisplay)}</span>
                    <small>${t('offersMetaPrefix', offer.minimumOrderPackageCount || 1, offer.leadTimeDays || 0)} · ${offer.allowsLoosePurchase ? t('offersLooseMeta', escapeHtml(offer.looseProcurementUnitName || t('offersLooseUnitDefault')), offer.loosePriceMode === 'DERIVED' ? t('offersLoosePriceDerived') : t('offersLoosePriceManual'), offer.looseMinimumOrderQuantity ?? 0, offer.looseQuantityStep ?? t('offersLooseStepUnlimited')) : t('offersPackageOnly')}</small>
                    <div class="supplier-offer-state" aria-label="${t('offersStatusAria')}">
                        <span class="supplier-status ${offer.active ? 'is-active' : 'is-inactive'}">${offer.active ? t('statusActive') : t('statusInactive')}</span>
                        <span class="supplier-status ${offer.isProcurementReady ? 'is-ready' : 'is-not-ready'}">${escapeHtml(offer.procurementReadinessLabel)}</span>
                    </div>
                    ${offer.isProcurementReady ? '' : `<p class="supplier-readiness-help">${escapeHtml(offer.procurementReadinessMessage)}${canMutate ? ` ${t('offersReadinessHelp')}` : ''}</p>`}
                </div>
                <div class="supplier-stack-actions">
                    <button type="button" class="supplier-btn supplier-btn-light view-price" data-id="${offer.ingredientSupplierId}">${t('offersViewPrice')}</button>
                    ${canMutate ? `<button type="button" class="supplier-btn supplier-btn-light edit-offer" data-id="${offer.ingredientSupplierId}">${offer.isProcurementReady ? t('offersEdit') : t('offersEditNotReady')}</button><button type="button" class="supplier-btn ${offer.active ? 'supplier-btn-danger' : 'supplier-btn-light'} toggle-offer" data-id="${offer.ingredientSupplierId}" data-active="${!offer.active}">${offer.active ? t('offersDeactivate') : t('offersActivate')}</button>` : ''}
                </div>
            </div>`).join('');
    }

    offerList?.addEventListener('click', async event => {
        const target = event.target instanceof Element ? event.target : null;
        const button = target?.closest('button[data-id]');
        if (!button || !offerList.contains(button)) return;
        const id = Number(button.dataset.id);
        if (button.classList.contains('view-price')) await openPricing(id);
        else if (button.classList.contains('edit-offer')) await beginOfferEdit(id);
        else if (button.classList.contains('toggle-offer')) {
            await toggleOffer(id, button.dataset.active === 'true', button);
        }
    });

    function resetOfferForm() {
        $('#offerForm')?.reset();
        $('#offerId').value = '';
        $('#offerRowVersion').value = '';
        $('#offerActive').checked = true;
        $('#offerAllowsLoose').checked = false;
        $('#offerLoosePriceMode').value = 'INDEPENDENT';
        $('#offerLooseMoq').value = '';
        $('#offerLooseStep').value = '';
        syncLooseOfferFields();
        ['offerIngredient', 'offerUnit', 'offerPackageQuantity', 'offerPrice'].forEach(id => { if ($(`#${id}`)) $(`#${id}`).disabled = false; });
        $('#cancelOfferEdit')?.classList.add('is-hidden');
        if ($('#saveOfferButton')) $('#saveOfferButton').textContent = t('offersAdd');
    }

    async function beginOfferEdit(id) {
        const offer = state.offers.find(item => item.ingredientSupplierId === id);
        if (!offer) return;
        $('#offerId').value = id;
        $('#offerRowVersion').value = offer.rowVersion || '';
        $('#offerIngredient').value = offer.ingredientId;
        await loadCompatibleUnits(offer.ingredientId, offer.unitId, offer.looseProcurementUnitId);
        $('#offerUnit').value = offer.unitId;
        $('#offerPackageQuantity').value = offer.packageQuantity;
        $('#offerPrice').value = offer.currentPrice;
        $('#offerMoq').value = offer.minimumOrderPackageCount || '';
        $('#offerLeadTime').value = offer.leadTimeDays ?? '';
        $('#offerPrimary').checked = Boolean(offer.isPrimary);
        $('#offerActive').checked = Boolean(offer.active);
        $('#offerAllowsLoose').checked = Boolean(offer.allowsLoosePurchase);
        $('#offerLooseUnit').value = offer.looseProcurementUnitId || '';
        $('#offerLoosePriceMode').value = offer.loosePriceMode || 'INDEPENDENT';
        $('#offerLoosePrice').value = offer.currentProcurementUnitPrice ?? '';
        $('#offerLooseMoq').value = offer.looseMinimumOrderQuantity ?? '';
        $('#offerLooseStep').value = offer.looseQuantityStep ?? '';
        syncLooseOfferFields();
        $('#offerNote').value = offer.note || '';
        ['offerIngredient', 'offerUnit', 'offerPackageQuantity', 'offerPrice'].forEach(fieldId => $(`#${fieldId}`).disabled = true);
        $('#cancelOfferEdit').classList.remove('is-hidden');
        $('#saveOfferButton').textContent = t('offersSaveMetadata');
        $('#offerForm').scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }
    $('#cancelOfferEdit')?.addEventListener('click', resetOfferForm);
    $('#offerIngredient')?.addEventListener('change', event => loadCompatibleUnits(Number(event.target.value || 0)));
    $('#offerLooseUnit')?.addEventListener('change', updateLoosePriceLabel);
    $('#offerUnit')?.addEventListener('change', updateDerivedLoosePrice);
    $('#offerPackageQuantity')?.addEventListener('input', updateDerivedLoosePrice);
    $('#offerPrice')?.addEventListener('input', updateDerivedLoosePrice);
    $('#offerLoosePriceMode')?.addEventListener('change', updateDerivedLoosePrice);

    function syncLooseOfferFields() {
        const enabled = Boolean($('#offerAllowsLoose')?.checked);
        $$('[data-loose-field]').forEach(field => field.classList.toggle('is-hidden', !enabled));
        if ($('#offerLooseUnit')) $('#offerLooseUnit').required = enabled;
        if ($('#offerLoosePrice')) $('#offerLoosePrice').required = enabled;
        updateLoosePriceLabel();
    }
    $('#offerAllowsLoose')?.addEventListener('change', syncLooseOfferFields);

    $('#offerForm')?.addEventListener('submit', async event => {
        event.preventDefault();
        const id = Number($('#offerId').value || 0);
        const current = id ? state.offers.find(item => item.ingredientSupplierId === id) : null;
        const body = {
            ingredientSupplierId: id || null,
            supplierId: state.supplierId,
            ingredientId: current?.ingredientId ?? Number($('#offerIngredient').value),
            unitId: current?.unitId ?? Number($('#offerUnit').value),
            packageQuantity: current?.packageQuantity ?? Number($('#offerPackageQuantity').value),
            currentPrice: current?.currentPrice ?? Number($('#offerPrice').value),
            minimumOrderPackageCount: $('#offerMoq').value ? Number($('#offerMoq').value) : null,
            leadTimeDays: $('#offerLeadTime').value ? Number($('#offerLeadTime').value) : null,
            isPrimary: $('#offerPrimary').checked,
            active: $('#offerActive').checked,
            allowsLoosePurchase: $('#offerAllowsLoose').checked,
            looseProcurementUnitId: $('#offerAllowsLoose').checked
                ? Number($('#offerLooseUnit').value)
                : null,
            currentProcurementUnitPrice: $('#offerAllowsLoose').checked
                ? Number($('#offerLoosePrice').value)
                : null,
            loosePriceMode: $('#offerAllowsLoose').checked
                ? $('#offerLoosePriceMode').value
                : 'INDEPENDENT',
            looseMinimumOrderQuantity: $('#offerAllowsLoose').checked && $('#offerLooseMoq').value
                ? Number($('#offerLooseMoq').value)
                : null,
            looseQuantityStep: $('#offerAllowsLoose').checked && $('#offerLooseStep').value
                ? Number($('#offerLooseStep').value)
                : null,
            note: $('#offerNote').value.trim() || null,
            rowVersion: current?.rowVersion || null
        };
        const form = event.currentTarget;
        setBusy(form, true);
        try {
            await api(id ? '/UpdateIngredientOffer' : '/CreateIngredientOffer', { method: 'POST', body });
            resetOfferForm();
            await loadOffers();
            toast(id ? t('toastOfferUpdated') : t('toastOfferAdded'));
        } catch (error) { toast(error.message, 'error'); }
        finally { setBusy(form, false); }
    });

    async function toggleOffer(id, active, button) {
        if (state.offerToggleRequests.has(id)) return;
        const offer = state.offers.find(item => item.ingredientSupplierId === id);
        if (!offer?.rowVersion) {
            toast(t('toastOfferStale'), 'error');
            return;
        }
        state.offerToggleRequests.add(id);
        if (button) button.disabled = true;
        try {
            await api('/ToggleIngredientOffer', {
                method: 'POST',
                body: { ingredientSupplierId: id, active, rowVersion: offer.rowVersion }
            });
            await loadOffers();
            toast(active ? t('toastOfferActivated') : t('toastOfferDeactivated'));
        } catch (error) { toast(error.message, 'error'); }
        finally {
            state.offerToggleRequests.delete(id);
            if (button?.isConnected) button.disabled = false;
        }
    }

    async function openPricing(id) {
        const offer = state.offers.find(item => item.ingredientSupplierId === id);
        if (!offer) return;
        state.pricingOffer = offer;
        selectTab('pricing');
        $('#pricingEmpty').classList.add('is-hidden');
        $('#pricingWorkspace').classList.remove('is-hidden');
        $('#pricingOfferName').textContent = offer.ingredientName;
        $('#pricingCurrentValue').textContent = `${offer.priceDisplay} · ${offer.packageDisplay}`;
        if (canMutate) {
            const unitPayload = await api(`/GetCompatibleUnitOptions?ingredientId=${offer.ingredientId}`);
            fillSelect($('#newPackageUnit'), unitPayload.data || [], 'unitId', item => `${item.unitCode} · ${item.name}`, t('selectContentUnit'));
            $('#priceOfferId').value = id;
            $('#priceRowVersion').value = offer.rowVersion || '';
            $('#newPackagePrice').value = offer.currentPrice;
            $('#newPackageQuantity').value = offer.packageQuantity;
            $('#newPackageUnit').value = offer.unitId;
            $('#priceReason').value = '';
        }
        await loadPriceHistory(id);
    }

    async function loadPriceHistory(id) {
        const root = $('#priceHistoryList');
        root.innerHTML = emptyStack(t('pricingLoading'));
        try {
            const payload = await api(`/GetPriceHistory?ingredientSupplierId=${id}`);
            const rows = payload.data || [];
            root.innerHTML = rows.length ? rows.map(row => `
                <div class="supplier-history-row">
                    <time>${escapeHtml(formatDate(row.effectiveDateUtc))}</time>
                    <div><strong>${escapeHtml(formatMoney(row.price))}</strong><small>${escapeHtml(t('pricingMeta', row.packageQuantity || 0, row.packageUnitName || '', row.note || t('pricingNoNote')))}</small></div>
                    <span class="supplier-status ${row.isCurrent ? 'is-current' : ''}">${row.isCurrent ? t('pricingCurrent') : t('pricingClosed')}</span>
                </div>`).join('') : emptyStack(t('pricingEmpty'));
        } catch (error) { root.innerHTML = emptyStack(error.message); }
    }

    $('#priceChangeForm')?.addEventListener('submit', async event => {
        event.preventDefault();
        const form = event.currentTarget;
        setBusy(form, true);
        try {
            await api('/ChangeIngredientOfferPrice', { method: 'POST', body: {
                ingredientSupplierId: Number($('#priceOfferId').value),
                packagePrice: Number($('#newPackagePrice').value),
                packageQuantity: Number($('#newPackageQuantity').value),
                packageUnitId: Number($('#newPackageUnit').value),
                reason: $('#priceReason').value.trim(),
                rowVersion: $('#priceRowVersion').value
            }});
            await loadOffers();
            const currentId = Number($('#priceOfferId').value);
            const refreshed = state.offers.find(item => item.ingredientSupplierId === currentId);
            if (refreshed) await openPricing(currentId);
            toast(t('toastPriceSaved'));
        } catch (error) { toast(error.message, 'error'); }
        finally { setBusy(form, false); }
    });

    async function loadStores() {
        const supplierId = state.supplierId;
        const assignmentRequest = api(`/GetSupplierStores?supplierId=${supplierId}`);
        const optionRequest = canMutate ? api('/GetStoreOptions') : Promise.resolve({ data: [] });
        const [assignmentPayload, optionPayload] = await Promise.all([assignmentRequest, optionRequest]);
        if (state.supplierId !== supplierId) return;
        state.stores = assignmentPayload.data || [];
        state.loaded.stores = true;
        renderStores();
        if (canMutate) fillSelect($('#assignmentStore'), optionPayload.data || [], 'storeId', item => item.name, t('selectStore'));
    }

    function renderStores() {
        const root = $('#storeList');
        if (!state.stores.length) { root.innerHTML = emptyStack(t('storesEmpty')); return; }
        root.innerHTML = state.stores.map(store => `
            <div class="supplier-stack-item">
                <div class="supplier-stack-item-main"><strong>${escapeHtml(store.storeName)}</strong><span>${escapeHtml(store.deliverySchedule || t('storesNoSchedule'))}</span><small>${t('storesMeta', store.leadTimeOverrideDays ?? t('storesFollowOffer'), store.active ? t('statusActive') : t('statusInactive'))}</small></div>
                ${canMutate ? `<div class="supplier-stack-actions"><button type="button" class="supplier-btn supplier-btn-light edit-store" data-id="${store.supplierStoreId}">${t('storesEdit')}</button></div>` : ''}
            </div>`).join('');
        $$('.edit-store', root).forEach(button => button.addEventListener('click', () => beginStoreEdit(Number(button.dataset.id))));
    }

    function resetStoreForm() {
        $('#storeAssignmentForm')?.reset();
        $('#supplierStoreId').value = '';
        $('#supplierStoreRowVersion').value = '';
        $('#assignmentStore').disabled = false;
        $('#assignmentActive').checked = true;
        $('#cancelStoreEdit')?.classList.add('is-hidden');
        if ($('#saveStoreButton')) $('#saveStoreButton').textContent = t('storesAssign');
    }

    function beginStoreEdit(id) {
        const store = state.stores.find(item => item.supplierStoreId === id);
        if (!store) return;
        $('#supplierStoreId').value = id;
        $('#supplierStoreRowVersion').value = store.rowVersion || '';
        $('#assignmentStore').value = store.storeId;
        $('#assignmentStore').disabled = true;
        $('#assignmentLeadTime').value = store.leadTimeOverrideDays ?? '';
        $('#assignmentSchedule').value = store.deliverySchedule || '';
        $('#assignmentNote').value = store.note || '';
        $('#assignmentActive').checked = Boolean(store.active);
        $('#cancelStoreEdit').classList.remove('is-hidden');
        $('#saveStoreButton').textContent = t('storesSave');
    }
    $('#cancelStoreEdit')?.addEventListener('click', resetStoreForm);

    $('#storeAssignmentForm')?.addEventListener('submit', async event => {
        event.preventDefault();
        const form = event.currentTarget;
        const existing = state.stores.find(item => item.supplierStoreId === Number($('#supplierStoreId').value));
        setBusy(form, true);
        try {
            await api('/SaveSupplierStore', { method: 'POST', body: {
                supplierStoreId: existing?.supplierStoreId || null,
                supplierId: state.supplierId,
                storeId: existing?.storeId || Number($('#assignmentStore').value),
                leadTimeOverrideDays: $('#assignmentLeadTime').value ? Number($('#assignmentLeadTime').value) : null,
                deliverySchedule: $('#assignmentSchedule').value.trim() || null,
                note: $('#assignmentNote').value.trim() || null,
                active: $('#assignmentActive').checked,
                rowVersion: existing?.rowVersion || null
            }});
            resetStoreForm();
            await loadStores();
            toast(t('toastStoresSaved'));
        } catch (error) { toast(error.message, 'error'); }
        finally { setBusy(form, false); }
    });

    const modal = $('#createSupplierModal');
    const duplicatePanel = $('#supplierDuplicatePanel');
    async function requestConfirmation(title, message) {
        if (!window.Swal) return window.confirm(`${title}\n${message}`);
        const result = await window.Swal.fire({
            title,
            text: message,
            icon: 'warning',
            showCancelButton: true,
            confirmButtonText: t('confirm'),
            cancelButtonText: t('keep'),
            focusCancel: true
        });
        return result.isConfirmed === true;
    }

    function resetDuplicateWarning() {
        state.duplicateWarningId = null;
        state.duplicateMatches = [];
        duplicatePanel?.classList.add('is-hidden');
        $('#duplicateReasonGroup')?.classList.add('is-hidden');
        $('#confirmDuplicateCreate')?.classList.add('is-hidden');
        $('#openDuplicateSupplier')?.classList.add('is-hidden');
        $('#reactivateDuplicateSupplier')?.classList.add('is-hidden');
        if ($('#duplicateReason')) $('#duplicateReason').value = '';
        setFieldError($('#duplicateReason'), $('#duplicateReasonError'), '');
    }

    function showDuplicatePanel(error) {
        const payload = error.payload || {};
        const isSoft = payload.code === 'SUPPLIER_POSSIBLE_DUPLICATE';
        const softData = payload.data || {};
        const hardMatch = payload.data?.existingSupplier;
        const matches = isSoft ? (softData.matches || []) : (hardMatch ? [{
            supplierId: hardMatch.supplierId,
            code: hardMatch.code,
            name: hardMatch.name,
            active: hardMatch.active,
            matchedSignals: [t('duplicateSignalTaxCode')]
        }] : []);

        state.duplicateWarningId = isSoft ? softData.warningId : null;
        state.duplicateMatches = matches;
        $('#duplicatePanelTitle').textContent = isSoft ? t('duplicatePossibleTitle') : t('duplicateExistsTitle');
        $('#duplicatePanelMessage').textContent = error.message;
        $('#duplicateSupplierList').innerHTML = matches.map(item => `
            <div class="supplier-duplicate-item">
                <strong>${escapeHtml(item.code)} · ${escapeHtml(item.name)}</strong>
                <span>${item.active ? t('statusActive') : t('statusInactive')} · ${t('duplicateMatchLabel')}: ${escapeHtml((item.matchedSignals || []).join(', '))}</span>
            </div>`).join('');

        duplicatePanel?.classList.remove('is-hidden');
        $('#openDuplicateSupplier')?.classList.toggle('is-hidden', matches.length === 0);
        $('#reactivateDuplicateSupplier')?.classList.toggle('is-hidden', !hardMatch || hardMatch.active);
        $('#duplicateReasonGroup')?.classList.toggle('is-hidden', !isSoft);
        $('#confirmDuplicateCreate')?.classList.toggle('is-hidden', !isSoft);
        duplicatePanel?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }

    function setModalOpen(open) {
        if (!modal) return;
        const instance = bootstrap.Modal.getOrCreateInstance(modal);
        if (open) instance.show();
        else instance.hide();
    }
    $('#createSupplierButton')?.addEventListener('click', () => setModalOpen(true));
    modal?.addEventListener('shown.bs.modal', () => $('#createName')?.focus());
    modal?.addEventListener('hidden.bs.modal', resetDuplicateWarning);

    $('#cancelDuplicateWarning')?.addEventListener('click', resetDuplicateWarning);
    $('#openDuplicateSupplier')?.addEventListener('click', () => {
        const match = state.duplicateMatches[0];
        if (!match) return;
        setModalOpen(false);
        openSupplier(match.supplierId);
    });
    $('#reactivateDuplicateSupplier')?.addEventListener('click', async () => {
        const match = state.duplicateMatches[0];
        if (!match || match.active) return;
        try {
            await api(`/ToggleStatus?id=${match.supplierId}`, { method: 'POST' });
            toast(t('toastSupplierReactivated'));
            window.location.reload();
        } catch (error) { toast(error.message, 'error'); }
    });

    function buildCreateBody(confirmDuplicate) {
        const form = $('#createSupplierForm');
        if (!form) return null;

        const nameInput = $('#createName');
        if (nameInput) {
            if (!nameInput.value.trim()) {
                nameInput.setCustomValidity(t('errorNameRequired'));
            } else {
                nameInput.setCustomValidity('');
            }
        }

        const taxCodeInput = $('#createTaxCode');
        const taxCode = validateTaxCodeInput(taxCodeInput, $('#createTaxCodeError'));
        if (taxCode === undefined) {
            if (taxCodeInput) {
                taxCodeInput.setCustomValidity(t('errorTaxCodeInvalid'));
            }
        } else if (taxCodeInput) {
            taxCodeInput.setCustomValidity('');
        }

        const phoneInput = $('#createPhone');
        validatePhoneInput(phoneInput, true, t('labelPhoneMain'));

        const contactNameInput = $('#createContactName');
        if (contactNameInput) {
            if (!contactNameInput.value.trim()) {
                contactNameInput.setCustomValidity(t('errorContactNameRequiredCreate'));
            } else {
                contactNameInput.setCustomValidity('');
            }
        }

        const contactPhoneInput = $('#createContactPhone');
        validatePhoneInput(contactPhoneInput, false, t('labelPhoneContactCreate'));

        const contactEmailInput = $('#createContactEmail');
        validateEmailInput(contactEmailInput, t('labelEmailContactCreate'));

        if (!form.checkValidity()) {
            form.reportValidity();
            return null;
        }

        return {
            name: $('#createName').value.trim(),
            taxCode,
            address: $('#createAddress').value.trim() || null,
            note: $('#createNote').value.trim() || null,
            primaryPhone: $('#createPhone').value.trim(),
            primaryContactName: $('#createContactName').value.trim(),
            primaryContactPhone: $('#createContactPhone').value.trim() || null,
            primaryContactEmail: $('#createContactEmail').value.trim() || null,
            primaryContactPosition: $('#createContactPosition').value.trim() || null,
            additionalPhones: [],
            additionalContacts: [],
            duplicateWarningId: confirmDuplicate ? state.duplicateWarningId : null,
            duplicateOverrideReason: confirmDuplicate ? $('#duplicateReason').value.trim() : null
        };
    }

    async function submitCreate(confirmDuplicate) {
        const form = $('#createSupplierForm');
        const body = buildCreateBody(confirmDuplicate);
        if (!body) return;
        if (confirmDuplicate && !body.duplicateOverrideReason) {
            setFieldError($('#duplicateReason'), $('#duplicateReasonError'), t('errorDuplicateReasonRequired'));
            $('#duplicateReason')?.focus();
            return;
        }
        setFieldError($('#duplicateReason'), $('#duplicateReasonError'), '');
        setBusy(form, true);
        try {
            const result = await api('/Create', { method: 'POST', body });
            const createdSupplierId = Number(result.data);
            const target = new URL(window.location.href);
            target.searchParams.set('search', body.name);
            target.searchParams.delete('status');
            target.searchParams.set('page', '1');
            target.searchParams.set('openSupplierId', String(createdSupplierId));
            target.searchParams.set('created', '1');
            window.location.assign(target.toString());
        } catch (error) {
            if (error.payload?.code === 'SUPPLIER_TAX_CODE_INVALID') {
                setFieldError($('#createTaxCode'), $('#createTaxCodeError'), error.message);
                $('#createTaxCode')?.focus();
            } else if (error.payload?.code === 'SUPPLIER_TAX_CODE_DUPLICATE'
                || error.payload?.code === 'SUPPLIER_POSSIBLE_DUPLICATE') {
                showDuplicatePanel(error);
            } else if (error.payload?.code === 'SUPPLIER_DUPLICATE_OVERRIDE_REASON_REQUIRED') {
                setFieldError($('#duplicateReason'), $('#duplicateReasonError'), error.message);
                $('#duplicateReason')?.focus();
            } else if (error.payload?.code === 'SUPPLIER_DUPLICATE_WARNING_INVALID'
                || error.payload?.code === 'SUPPLIER_DUPLICATE_WARNING_STALE') {
                resetDuplicateWarning();
                toast(error.message, 'error');
            } else {
                toast(error.message, 'error');
            }
        } finally { setBusy(form, false); }
    }

    $('#createSupplierForm')?.addEventListener('submit', async event => {
        event.preventDefault();
        await submitCreate(false);
    });
    $('#confirmDuplicateCreate')?.addEventListener('click', () => submitCreate(true));

    const initialSupplierId = Number(new URLSearchParams(window.location.search).get('openSupplierId'));
    if (Number.isInteger(initialSupplierId) && initialSupplierId > 0) {
        openSupplier(initialSupplierId).then(() => {
            const target = new URL(window.location.href);
            if (target.searchParams.get('created') !== '1'
                || state.detail?.supplierId !== initialSupplierId) return;

            toast(t('toastSupplierCreated', state.detail.name));
            target.searchParams.delete('created');
            window.history.replaceState({}, '', target.toString());
        });
    }

    $$('input, textarea, select').forEach(control => {
        control.addEventListener('input', () => {
            control.setCustomValidity('');
            if (control.closest('#createSupplierForm') && control.id !== 'duplicateReason') {
                if (duplicatePanel && !duplicatePanel.classList.contains('is-hidden')) resetDuplicateWarning();
            }
        });
    });

})();
