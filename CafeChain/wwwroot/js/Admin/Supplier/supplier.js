(() => {
    'use strict';

    const API = '/Admin/AdminSupplier';
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
        duplicateWarningId: null,
        duplicateMatches: []
    };

    const $ = (selector, root = document) => root.querySelector(selector);
    const $$ = (selector, root = document) => [...root.querySelectorAll(selector)];
    const detailPanel = $('#supplierDetail');
    const detailContent = $('#supplierDetailContent');
    const detailPlaceholder = $('#supplierDetailPlaceholder');
    const antiForgeryToken = () =>
        $('#supplierAntiForgeryForm input[name="__RequestVerificationToken"]')?.value || '';

    const escapeHtml = (value) => String(value ?? '')
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#039;');

    const formatMoney = (value) => `${new Intl.NumberFormat('vi-VN').format(Number(value || 0))} đ`;
    const formatDate = (value) => value
        ? new Intl.DateTimeFormat('vi-VN', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
        : 'Chưa có dữ liệu';

    async function api(path, options = {}) {
        const init = { ...options, headers: { Accept: 'application/json', ...(options.headers || {}) } };
        const method = (options.method || 'GET').toUpperCase();
        if (method !== 'GET' && method !== 'HEAD') {
            init.headers.RequestVerificationToken = antiForgeryToken();
        }
        if (options.body && typeof options.body !== 'string') {
            init.headers['Content-Type'] = 'application/json';
            init.body = JSON.stringify(options.body);
        }
        const response = await fetch(`${API}${path}`, init);
        let payload;
        try { payload = await response.json(); }
        catch { throw new Error('Máy chủ trả về dữ liệu không hợp lệ.'); }
        if (!response.ok || payload.success === false) {
            const error = new Error(payload.message || `Yêu cầu thất bại (HTTP ${response.status}).`);
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
            setFieldError(input, error, 'Mã số thuế phải gồm 10 chữ số hoặc 10 chữ số, dấu gạch ngang và 3 chữ số.');
            input?.focus();
            return undefined;
        }
        if (input) input.value = normalized || '';
        setFieldError(input, error, '');
        return normalized;
    }

    function applyFilters() {
        const query = ($('#supplierSearch')?.value || '').trim().toLocaleLowerCase('vi');
        const status = $('#supplierStatusFilter')?.value || 'all';
        let count = 0;
        $$('#supplierRows tr').forEach(row => {
            const matchesText = !query || (row.dataset.search || '').includes(query);
            const matchesStatus = status === 'all' || row.dataset.status === status;
            const visible = matchesText && matchesStatus;
            row.classList.toggle('is-hidden', !visible);
            if (visible) count += 1;
        });
        $('#supplierResultCount').textContent = `${count} kết quả`;
        $('#supplierEmptyState').classList.toggle('is-hidden', count !== 0);
    }

    $('#supplierSearch')?.addEventListener('input', applyFilters);
    $('#supplierStatusFilter')?.addEventListener('change', applyFilters);

    function selectTab(tabName) {
        $$('.supplier-tabs button').forEach(button => button.classList.toggle('is-active', button.dataset.tab === tabName));
        $$('.supplier-tab').forEach(panel => panel.classList.toggle('is-active', panel.dataset.tabPanel === tabName));
    }
    $$('.supplier-tabs button').forEach(button => button.addEventListener('click', () => selectTab(button.dataset.tab)));

    function openDetailShell() {
        detailPanel.classList.add('is-open');
        detailPanel.setAttribute('aria-hidden', 'false');
        detailPlaceholder.classList.add('is-hidden');
        detailContent.classList.remove('is-hidden');
    }

    function closeDetail() {
        detailPanel.classList.remove('is-open');
        detailPanel.setAttribute('aria-hidden', 'true');
        $$('#supplierRows tr').forEach(row => row.classList.remove('is-selected'));
        if (window.innerWidth < 1180) {
            detailContent.classList.add('is-hidden');
            detailPlaceholder.classList.remove('is-hidden');
        }
    }
    $('#closeSupplierDetail')?.addEventListener('click', closeDetail);

    async function loadReferenceData() {
        if (!canMutate || (state.ingredients.length && state.units.length)) return;
        const [ingredientPayload, unitPayload] = await Promise.all([
            api('/GetIngredientOptions'),
            api('/GetContentUnitOptions')
        ]);
        state.ingredients = ingredientPayload.data || [];
        state.units = unitPayload.data || [];
        fillSelect($('#offerIngredient'), state.ingredients, 'ingredientId', item => `${item.code} · ${item.name}`);
        fillSelect($('#offerUnit'), state.units, 'unitId', item => `${item.unitCode} · ${item.name}`);
        fillSelect(
            $('#offerLooseUnit'),
            state.units.filter(item => ['kg', 'l', 'pcs'].includes(String(item.unitCode || '').toLowerCase())),
            'unitId',
            item => `${item.unitCode} · ${item.name}`);
        fillSelect($('#newPackageUnit'), state.units, 'unitId', item => `${item.unitCode} · ${item.name}`);
    }

    function fillSelect(select, items, valueKey, labelFactory, placeholder = 'Chọn dữ liệu') {
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
        openDetailShell();
        selectTab('overview');
        setBusy(detailContent, true);
        $$('#supplierRows tr').forEach(row => row.classList.toggle('is-selected', Number(row.dataset.supplierId) === state.supplierId));

        try {
            const detailPayload = await api(`/GetById?id=${state.supplierId}`);
            state.detail = detailPayload.data;
            renderDetail();
            await Promise.all([loadOffers(), loadStores(), loadReferenceData()]);
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
        $('#detailSummary').textContent = d.address || 'Chưa cập nhật địa chỉ';
        $('#overviewSupplierId').value = d.supplierId;
        $('#overviewRowVersion').value = d.rowVersion || '';
        $('#overviewName').value = d.name || '';
        $('#overviewTaxCode').value = d.taxCode || '';
        $('#overviewAddress').value = d.address || '';
        $('#overviewNote').value = d.note || '';
        $('#overviewActive').value = String(Boolean(d.active));
        $('#auditCreatedAt').textContent = formatDate(d.createdAt);
        $('#auditUpdatedAt').textContent = formatDate(d.updatedAt);
        $('#auditVersion').textContent = d.rowVersion || 'Chưa có dữ liệu';
        renderSupplierAudits();
        renderPhones();
        renderContacts();
    }

    function renderSupplierAudits() {
        const root = $('#supplierAuditEvents');
        const rows = state.detail?.audits || [];
        if (!root) return;
        if (!rows.length) {
            root.innerHTML = emptyStack('Chưa có thay đổi mã số thuế hoặc xác nhận trùng được ghi nhận.');
            return;
        }
        const labels = {
            SUPPLIER_CREATED: 'Tạo nhà cung cấp',
            SUPPLIER_TAX_CODE_UPDATED: 'Cập nhật mã số thuế',
            SUPPLIER_DUPLICATE_OVERRIDE: 'Xác nhận tạo dù có dấu hiệu trùng'
        };
        root.innerHTML = rows.map(item => `
            <div class="supplier-history-row">
                <span>${escapeHtml(formatDate(item.createdAt))}</span>
                <div><strong>${escapeHtml(labels[item.action] || item.action)}</strong><small>Nhân viên #${escapeHtml(item.actorStaffId)}</small></div>
                <span>${escapeHtml(item.newData || '')}</span>
            </div>`).join('');
    }

    function applyReadOnlyMode() {
        if (canMutate) return;
        $$('#supplierDetail input, #supplierDetail select, #supplierDetail textarea').forEach(control => control.disabled = true);
    }

    function renderPhones() {
        const root = $('#phoneList');
        const rows = state.detail?.phones || [];
        if (!rows.length) { root.innerHTML = emptyStack('Chưa có số điện thoại.'); return; }
        root.innerHTML = rows.map(phone => `
            <div class="supplier-stack-item">
                <div class="supplier-stack-item-main"><strong>${escapeHtml(phone.phoneNumber)}</strong><small>${phone.isPrimary ? 'Số điện thoại chính' : 'Số điện thoại phụ'}</small></div>
                ${canMutate && !phone.isPrimary ? `<div class="supplier-stack-actions"><button type="button" class="supplier-btn supplier-btn-danger delete-phone" data-id="${phone.supplierPhoneId}">Xóa số</button></div>` : ''}
            </div>`).join('');
        $$('.delete-phone', root).forEach(button => button.addEventListener('click', () => deletePhone(button.dataset.id)));
    }

    function renderContacts() {
        const root = $('#contactList');
        const rows = state.detail?.contacts || [];
        if (!rows.length) { root.innerHTML = emptyStack('Chưa có người liên hệ.'); return; }
        root.innerHTML = rows.map(contact => `
            <div class="supplier-stack-item">
                <div class="supplier-stack-item-main">
                    <strong>${escapeHtml(contact.name)} ${contact.isPrimary ? '<span class="supplier-status is-current">Đầu mối chính</span>' : ''}</strong>
                    <span>${escapeHtml(contact.position || 'Chưa có chức vụ')}</span>
                    <small>${escapeHtml([contact.phone, contact.email].filter(Boolean).join(' · ') || 'Chưa có điện thoại/email')}</small>
                </div>
                ${canMutate ? `<div class="supplier-stack-actions">
                    <button type="button" class="supplier-btn supplier-btn-light edit-contact" data-id="${contact.supplierContactId}">Sửa</button>
                    ${contact.isPrimary ? '' : `<button type="button" class="supplier-btn supplier-btn-light primary-contact" data-id="${contact.supplierContactId}">Đặt làm chính</button><button type="button" class="supplier-btn supplier-btn-danger delete-contact" data-id="${contact.supplierContactId}">Xóa</button>`}
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
        const taxCode = validateTaxCodeInput($('#overviewTaxCode'), $('#overviewTaxCodeError'));
        if (taxCode === undefined) return;
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
            toast('Đã lưu thông tin nhà cung cấp.');
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
        setBusy(form, true);
        try {
            await api('/AddPhone', { method: 'POST', body: { supplierId: state.supplierId, phoneNumber: $('#newPhone').value.trim() } });
            $('#newPhone').value = '';
            await refreshDetailData();
            toast('Đã thêm số điện thoại.');
        } catch (error) { toast(error.message, 'error'); }
        finally { setBusy(form, false); }
    });

    async function deletePhone(id) {
        if (!window.confirm('Xóa số điện thoại này?')) return;
        try { await api(`/DeletePhone?supplierPhoneId=${id}`, { method: 'POST' }); await refreshDetailData(); toast('Đã xóa số điện thoại.'); }
        catch (error) { toast(error.message, 'error'); }
    }

    function resetContactForm() {
        $('#contactForm')?.reset();
        $('#contactId').value = '';
        $('#cancelContactEdit')?.classList.add('is-hidden');
        if ($('#saveContactButton')) $('#saveContactButton').textContent = 'Thêm liên hệ';
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
        $('#saveContactButton').textContent = 'Lưu liên hệ';
    }
    $('#cancelContactEdit')?.addEventListener('click', resetContactForm);

    $('#contactForm')?.addEventListener('submit', async event => {
        event.preventDefault();
        const id = Number($('#contactId').value || 0);
        const body = {
            supplierContactId: id || undefined,
            supplierId: state.supplierId,
            name: $('#contactName').value.trim(),
            phone: $('#contactPhone').value.trim() || null,
            email: $('#contactEmail').value.trim() || null,
            position: $('#contactPosition').value.trim() || null,
            active: true
        };
        const form = event.currentTarget;
        setBusy(form, true);
        try {
            await api(id ? '/UpdateContact' : '/AddContact', { method: 'POST', body });
            resetContactForm();
            await refreshDetailData();
            toast(id ? 'Đã cập nhật người liên hệ.' : 'Đã thêm người liên hệ.');
        } catch (error) { toast(error.message, 'error'); }
        finally { setBusy(form, false); }
    });

    async function setPrimaryContact(id) {
        try { await api(`/SetPrimaryContact?supplierContactId=${id}`, { method: 'POST' }); await refreshDetailData(); toast('Đã cập nhật đầu mối chính.'); }
        catch (error) { toast(error.message, 'error'); }
    }
    async function deleteContact(id) {
        if (!window.confirm('Xóa người liên hệ này?')) return;
        try { await api(`/DeleteContact?supplierContactId=${id}`, { method: 'POST' }); await refreshDetailData(); toast('Đã xóa người liên hệ.'); }
        catch (error) { toast(error.message, 'error'); }
    }

    async function loadOffers() {
        const payload = await api(`/GetIngredientOffers?supplierId=${state.supplierId}`);
        state.offers = payload.data || [];
        renderOffers();
    }

    function renderOffers() {
        const root = $('#offerList');
        if (!state.offers.length) { root.innerHTML = emptyStack('Chưa có gói mua nguyên liệu.'); return; }
        root.innerHTML = state.offers.map(offer => `
            <div class="supplier-stack-item">
                <div class="supplier-stack-item-main">
                    <strong>${escapeHtml(offer.ingredientName)} ${offer.isPrimary ? '<span class="supplier-status is-current">Nguồn chính</span>' : ''}</strong>
                    <span>${escapeHtml(offer.packageDisplay)} · ${escapeHtml(offer.priceDisplay)}</span>
                    <small>MOQ ${offer.minimumOrderPackageCount || 0} gói · Lead time ${offer.leadTimeDays || 0} ngày · ${offer.allowsLoosePurchase ? `Có mua lẻ theo ${escapeHtml(offer.looseProcurementUnitName || 'đơn vị procurement')}` : 'Chỉ mua theo gói'} · ${offer.active ? 'Đang hoạt động' : 'Ngừng hoạt động'}</small>
                </div>
                <div class="supplier-stack-actions">
                    <button type="button" class="supplier-btn supplier-btn-light view-price" data-id="${offer.ingredientSupplierId}">Đổi giá & lịch sử</button>
                    ${canMutate ? `<button type="button" class="supplier-btn supplier-btn-light edit-offer" data-id="${offer.ingredientSupplierId}">Sửa metadata</button><button type="button" class="supplier-btn ${offer.active ? 'supplier-btn-danger' : 'supplier-btn-light'} toggle-offer" data-id="${offer.ingredientSupplierId}" data-active="${!offer.active}">${offer.active ? 'Ngừng dùng' : 'Kích hoạt'}</button>` : ''}
                </div>
            </div>`).join('');
        $$('.view-price', root).forEach(button => button.addEventListener('click', () => openPricing(Number(button.dataset.id))));
        $$('.edit-offer', root).forEach(button => button.addEventListener('click', () => beginOfferEdit(Number(button.dataset.id))));
        $$('.toggle-offer', root).forEach(button => button.addEventListener('click', () => toggleOffer(Number(button.dataset.id), button.dataset.active === 'true')));
    }

    function resetOfferForm() {
        $('#offerForm')?.reset();
        $('#offerId').value = '';
        $('#offerRowVersion').value = '';
        $('#offerActive').checked = true;
        $('#offerAllowsLoose').checked = false;
        syncLooseOfferFields();
        ['offerIngredient', 'offerUnit', 'offerPackageQuantity', 'offerPrice'].forEach(id => { if ($(`#${id}`)) $(`#${id}`).disabled = false; });
        $('#cancelOfferEdit')?.classList.add('is-hidden');
        if ($('#saveOfferButton')) $('#saveOfferButton').textContent = 'Thêm gói mua';
    }

    function beginOfferEdit(id) {
        const offer = state.offers.find(item => item.ingredientSupplierId === id);
        if (!offer) return;
        $('#offerId').value = id;
        $('#offerRowVersion').value = offer.rowVersion || '';
        $('#offerIngredient').value = offer.ingredientId;
        $('#offerUnit').value = offer.unitId;
        $('#offerPackageQuantity').value = offer.packageQuantity;
        $('#offerPrice').value = offer.currentPrice;
        $('#offerMoq').value = offer.minimumOrderPackageCount || '';
        $('#offerLeadTime').value = offer.leadTimeDays ?? '';
        $('#offerPrimary').checked = Boolean(offer.isPrimary);
        $('#offerActive').checked = Boolean(offer.active);
        $('#offerAllowsLoose').checked = Boolean(offer.allowsLoosePurchase);
        $('#offerLooseUnit').value = offer.looseProcurementUnitId || '';
        $('#offerLoosePrice').value = offer.currentProcurementUnitPrice ?? '';
        syncLooseOfferFields();
        $('#offerNote').value = offer.note || '';
        ['offerIngredient', 'offerUnit', 'offerPackageQuantity', 'offerPrice'].forEach(fieldId => $(`#${fieldId}`).disabled = true);
        $('#cancelOfferEdit').classList.remove('is-hidden');
        $('#saveOfferButton').textContent = 'Lưu metadata';
        $('#offerForm').scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }
    $('#cancelOfferEdit')?.addEventListener('click', resetOfferForm);

    function syncLooseOfferFields() {
        const enabled = Boolean($('#offerAllowsLoose')?.checked);
        $$('[data-loose-field]').forEach(field => field.classList.toggle('is-hidden', !enabled));
        if ($('#offerLooseUnit')) $('#offerLooseUnit').required = enabled;
        if ($('#offerLoosePrice')) $('#offerLoosePrice').required = enabled;
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
            note: $('#offerNote').value.trim() || null,
            rowVersion: current?.rowVersion || null
        };
        const form = event.currentTarget;
        setBusy(form, true);
        try {
            await api(id ? '/UpdateIngredientOffer' : '/CreateIngredientOffer', { method: 'POST', body });
            resetOfferForm();
            await loadOffers();
            toast(id ? 'Đã cập nhật gói mua.' : 'Đã thêm gói mua.');
        } catch (error) { toast(error.message, 'error'); }
        finally { setBusy(form, false); }
    });

    async function toggleOffer(id, active) {
        const offer = state.offers.find(item => item.ingredientSupplierId === id);
        if (!offer?.rowVersion) {
            toast('Dữ liệu gói mua đã thay đổi. Vui lòng tải lại trước khi cập nhật.', 'error');
            return;
        }
        try {
            await api('/ToggleIngredientOffer', {
                method: 'POST',
                body: { ingredientSupplierId: id, active, rowVersion: offer.rowVersion }
            });
            await loadOffers();
            toast(active ? 'Đã kích hoạt gói mua.' : 'Đã ngừng sử dụng gói mua.');
        } catch (error) { toast(error.message, 'error'); }
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
        root.innerHTML = emptyStack('Đang tải lịch sử giá...');
        try {
            const payload = await api(`/GetPriceHistory?ingredientSupplierId=${id}`);
            const rows = payload.data || [];
            root.innerHTML = rows.length ? rows.map(row => `
                <div class="supplier-history-row">
                    <time>${escapeHtml(formatDate(row.effectiveDateUtc))}</time>
                    <div><strong>${escapeHtml(formatMoney(row.price))}</strong><small>${escapeHtml(`${row.packageQuantity || 0} ${row.packageUnitName || ''} / gói · ${row.note || 'Không có ghi chú'}`)}</small></div>
                    <span class="supplier-status ${row.isCurrent ? 'is-current' : ''}">${row.isCurrent ? 'Hiện hành' : 'Đã đóng'}</span>
                </div>`).join('') : emptyStack('Chưa có lịch sử giá.');
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
            toast('Đã cập nhật giá và lưu lịch sử.');
        } catch (error) { toast(error.message, 'error'); }
        finally { setBusy(form, false); }
    });

    async function loadStores() {
        const [assignmentPayload, optionPayload] = await Promise.all([
            api(`/GetSupplierStores?supplierId=${state.supplierId}`),
            api('/GetStoreOptions')
        ]);
        state.stores = assignmentPayload.data || [];
        renderStores();
        if (canMutate) fillSelect($('#assignmentStore'), optionPayload.data || [], 'storeId', item => item.name, 'Chọn cửa hàng');
    }

    function renderStores() {
        const root = $('#storeList');
        if (!state.stores.length) { root.innerHTML = emptyStack('Chưa gán nhà cung cấp cho cửa hàng.'); return; }
        root.innerHTML = state.stores.map(store => `
            <div class="supplier-stack-item">
                <div class="supplier-stack-item-main"><strong>${escapeHtml(store.storeName)}</strong><span>${escapeHtml(store.deliverySchedule || 'Chưa có lịch giao hàng')}</span><small>Lead time riêng: ${store.leadTimeOverrideDays ?? 'Theo gói mua'} · ${store.active ? 'Đang hoạt động' : 'Ngừng hoạt động'}</small></div>
                ${canMutate ? `<div class="supplier-stack-actions"><button type="button" class="supplier-btn supplier-btn-light edit-store" data-id="${store.supplierStoreId}">Chỉnh sửa</button></div>` : ''}
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
        if ($('#saveStoreButton')) $('#saveStoreButton').textContent = 'Gán cửa hàng';
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
        $('#saveStoreButton').textContent = 'Lưu phạm vi';
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
            toast('Đã cập nhật phạm vi cửa hàng.');
        } catch (error) { toast(error.message, 'error'); }
        finally { setBusy(form, false); }
    });

    const modal = $('#createSupplierModal');
    const duplicatePanel = $('#supplierDuplicatePanel');

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
            matchedSignals: ['Mã số thuế']
        }] : []);

        state.duplicateWarningId = isSoft ? softData.warningId : null;
        state.duplicateMatches = matches;
        $('#duplicatePanelTitle').textContent = isSoft ? 'Có thể trùng nhà cung cấp' : 'Mã số thuế đã tồn tại';
        $('#duplicatePanelMessage').textContent = error.message;
        $('#duplicateSupplierList').innerHTML = matches.map(item => `
            <div class="supplier-duplicate-item">
                <strong>${escapeHtml(item.code)} · ${escapeHtml(item.name)}</strong>
                <span>${item.active ? 'Đang hoạt động' : 'Ngừng hoạt động'} · Khớp: ${escapeHtml((item.matchedSignals || []).join(', '))}</span>
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
        modal.classList.toggle('is-open', open);
        modal.setAttribute('aria-hidden', String(!open));
        document.body.style.overflow = open ? 'hidden' : '';
        if (open) window.setTimeout(() => $('#createName')?.focus(), 50);
        else resetDuplicateWarning();
    }
    $('#createSupplierButton')?.addEventListener('click', () => setModalOpen(true));
    $$('[data-close-modal]').forEach(button => button.addEventListener('click', () => setModalOpen(false)));
    modal?.addEventListener('click', event => { if (event.target === modal) setModalOpen(false); });

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
            toast('Đã kích hoạt lại nhà cung cấp hiện có.');
            window.location.reload();
        } catch (error) { toast(error.message, 'error'); }
    });

    function buildCreateBody(confirmDuplicate) {
        const taxCode = validateTaxCodeInput($('#createTaxCode'), $('#createTaxCodeError'));
        if (taxCode === undefined) return null;
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
            setFieldError($('#duplicateReason'), $('#duplicateReasonError'), 'Vui lòng nhập lý do vẫn tạo nhà cung cấp mới.');
            $('#duplicateReason')?.focus();
            return;
        }
        setFieldError($('#duplicateReason'), $('#duplicateReasonError'), '');
        setBusy(form, true);
        try {
            await api('/Create', { method: 'POST', body });
            toast('Đã tạo nhà cung cấp.');
            window.location.reload();
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

    $$('#createSupplierForm input, #createSupplierForm textarea').forEach(control => {
        if (control.id === 'duplicateReason') return;
        control.addEventListener('input', () => {
            if (duplicatePanel && !duplicatePanel.classList.contains('is-hidden')) resetDuplicateWarning();
        });
    });

    document.addEventListener('keydown', event => {
        if (event.key !== 'Escape') return;
        if (modal?.classList.contains('is-open')) setModalOpen(false);
        else closeDetail();
    });
})();
