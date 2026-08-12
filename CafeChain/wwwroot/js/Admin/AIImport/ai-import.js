(() => {
    'use strict';

    const app = document.getElementById('aiImportApp');
    if (!app) return;

    const byId = id => document.getElementById(id);
    const token = document.querySelector('#antiForgeryForm input[name="__RequestVerificationToken"]')?.value || '';
    const state = {
        session: null,
        activeGroupId: null,
        page: 1,
        pageSize: 50,
        editingItem: null,
        confirmKey: null,
        editorOptions: null,
        confirmErrors: new Map()
    };

    const entityLabels = {
        Category: 'Danh mục', Drink: 'Đồ uống', Size: 'Size', Ingredient: 'Nguyên liệu', Supplier: 'Nhà cung cấp', Unknown: 'Chưa xác định'
    };
    const statusLabels = {
        VALID: 'Hợp lệ', WARNING: 'Cảnh báo', ERROR: 'Lỗi', REVIEW_REQUIRED: 'Cần xem lại', SKIPPED: 'Bỏ qua', IMPORTED: 'Đã nhập',
        READY_TO_PREVIEW: 'Sẵn sàng xem trước', ANALYZING: 'Đang phân tích', VALIDATING: 'Đang kiểm tra', IMPORTING: 'Đang nhập',
        COMPLETED: 'Hoàn tất', FAILED: 'Thất bại', CANCELLED: 'Đã hủy', EXPIRED: 'Đã hết hạn', UPLOADED: 'Đã tải lên'
    };
    const actionLabels = { CREATE: 'Tạo mới', SKIP: 'Bỏ qua' };
    const iconCatalog = [
        ['☕', 'Cà phê'], ['🧋', 'Trà sữa'], ['🍵', 'Trà'], ['🥤', 'Nước uống'], ['🥛', 'Sữa'], ['🧃', 'Nước ép'],
        ['🍹', 'Nước trái cây'], ['🍸', 'Mocktail'], ['🍓', 'Dâu'], ['🍊', 'Cam'], ['🍋', 'Chanh'], ['🍎', 'Táo'],
        ['🍉', 'Dưa hấu'], ['🥭', 'Xoài'], ['🍑', 'Đào'], ['🍍', 'Dứa'], ['🥑', 'Bơ'], ['🥥', 'Dừa'],
        ['🥐', 'Bánh'], ['🍫', 'Sô-cô-la'], ['🍨', 'Kem'], ['🍰', 'Bánh ngọt'], ['⭐', 'Nổi bật'], ['🔥', 'Bán chạy'],
        ['🌿', 'Thảo mộc'], ['❤️', 'Yêu thích'], ['✨', 'Theo mùa'], ['💚', 'Tốt cho sức khỏe'], ['🧊', 'Đá xay'], ['♨️', 'Đồ nóng']
    ];

    const editorSchemas = {
        Category: {
            sections: [{ title: 'Thông tin danh mục', fields: [
                { name: 'CategoryCode', label: 'Mã danh mục', required: true, max: 30, code: true, placeholder: 'Ví dụ: CAFE' },
                { name: 'Name', label: 'Tên danh mục', required: true, min: 2, max: 100, placeholder: 'Nhập tên danh mục' },
                { name: 'Icon', label: 'Biểu tượng Unicode', max: 10, type: 'icon', wide: true },
                { name: 'Active', label: 'Trạng thái', required: true, type: 'select', options: [{ value: 'true', label: 'Hoạt động' }, { value: 'false', label: 'Ngừng hoạt động' }] }
            ] }]
        },
        Drink: {
            sections: [{ title: 'Thông tin đồ uống', fields: [
                { name: 'DrinkCode', label: 'Mã đồ uống', required: true, max: 50, code: true, placeholder: 'Ví dụ: CF_SUA' },
                { name: 'Name', label: 'Tên đồ uống', required: true, max: 200, placeholder: 'Nhập tên đồ uống' },
                { name: 'Category', label: 'Danh mục', required: true, type: 'select', optionSource: 'categories' },
                { name: 'ProductType', label: 'Loại sản phẩm', required: true, type: 'select', optionSource: 'productTypes' },
                { name: 'Description', label: 'Mô tả chi tiết', max: 1000, type: 'textarea', wide: true, placeholder: 'Mô tả thành phần, hương vị...' }
            ] }]
        },
        Size: {
            sections: [{ title: 'Thông tin Size', fields: [
                { name: 'SizeCode', label: 'Mã Size', required: true, max: 20, code: true, placeholder: 'Ví dụ: M' },
                { name: 'Name', label: 'Tên Size', required: true, max: 50, placeholder: 'Ví dụ: Vừa' },
                { name: 'SizeType', label: 'Loại Size', required: true, type: 'select', options: [{ value: 'Cup', label: 'Ly' }, { value: 'Volume', label: 'Dung tích' }] },
                { name: 'Description', label: 'Mô tả Size', max: 300, wide: true, placeholder: 'Ví dụ: Size vừa' }
            ] }]
        },
        Ingredient: {
            sections: [{ title: 'Thông tin nguyên liệu', fields: [
                { name: 'Code', label: 'Mã nguyên liệu', required: true, max: 50, code: true, placeholder: 'Ví dụ: NL001' },
                { name: 'Name', label: 'Tên nguyên liệu', required: true, max: 200, placeholder: 'Nhập tên nguyên liệu' },
                { name: 'BaseUnit', label: 'Đơn vị cơ sở', required: true, type: 'select', optionSource: 'units', wide: true }
            ] }]
        },
        Supplier: {
            sections: [
                { title: 'Thông tin doanh nghiệp', note: 'Thông tin dùng để nhận diện và đối chiếu chứng từ mua hàng.', fields: [
                    { name: 'Name', label: 'Tên nhà cung cấp', required: true, max: 200 },
                    { name: 'TaxCode', label: 'Mã số thuế', max: 14, inputMode: 'numeric', pattern: '^\\d{10}(-\\d{3})?$', placeholder: '0312345679 hoặc 0312345679-001' },
                    { name: 'PrimaryPhone', label: 'Điện thoại chính', required: true, max: 20, type: 'tel' },
                    { name: 'Address', label: 'Địa chỉ', max: 500, wide: true }
                ] },
                { title: 'Đầu mối chính', note: 'Người nhận báo giá và phối hợp xử lý giao hàng.', fields: [
                    { name: 'PrimaryContactName', label: 'Người liên hệ chính', required: true, max: 150 },
                    { name: 'PrimaryContactPosition', label: 'Chức vụ', max: 100 },
                    { name: 'PrimaryContactPhone', label: 'Điện thoại đầu mối', max: 20, type: 'tel' },
                    { name: 'PrimaryContactEmail', label: 'Email đầu mối', max: 150, type: 'email' }
                ] },
                { title: 'Ghi chú vận hành', fields: [
                    { name: 'Note', label: 'Thông tin bổ sung', max: 1000, type: 'textarea', wide: true, placeholder: 'Điều kiện giao hàng hoặc lưu ý đối soát...' }
                ] }
            ]
        }
    };

    const entityFields = Object.fromEntries(Object.entries(editorSchemas).map(([entity, schema]) => [entity, schema.sections.flatMap(section => section.fields.map(field => field.name))]));
    const fieldDefinition = (entity, name) => editorSchemas[entity]?.sections.flatMap(x => x.fields).find(x => x.name === name);
    const fieldLabel = (entity, name) => fieldDefinition(entity, name)?.label || name;

    async function api(url, options = {}) {
        const headers = new Headers(options.headers || {});
        if (options.method && options.method !== 'GET') headers.set('RequestVerificationToken', token);
        if (options.body && !(options.body instanceof FormData)) headers.set('Content-Type', 'application/json');
        const response = await fetch(url, { credentials: 'same-origin', ...options, headers });
        let payload;
        try { payload = await response.json(); }
        catch { payload = { success: false, code: 'PHẢN_HỒI_KHÔNG_HỢP_LỆ', message: 'Máy chủ trả về phản hồi không hợp lệ.' }; }
        if (!response.ok || !payload.success) {
            const error = new Error(payload.message || 'Yêu cầu thất bại.');
            error.code = payload.code;
            error.status = response.status;
            error.details = payload.details || [];
            throw error;
        }
        return payload.data;
    }

    function busy(value) {
        byId('loadingPanel').hidden = !value;
        ['analyzeButton', 'confirmButton', 'cancelButton', 'reanalyzeButton'].forEach(id => { const element = byId(id); if (element) element.disabled = value; });
    }
    function message(text, kind = '') {
        const box = byId('messageBox');
        box.hidden = !text;
        box.className = `ai-import-alert ${kind}`;
        box.textContent = text || '';
    }
    function escapeHtml(value) { const div = document.createElement('div'); div.textContent = value ?? ''; return div.innerHTML; }
    function displayValue(value) { return value === null || value === undefined || value === '' ? '—' : String(value); }
    function closeEditDialog() {
        const dialog = byId('editDialog');
        if (dialog?.open) dialog.close();
        state.editingItem = null;
    }

    function closeImportWorkspace(result) {
        closeEditDialog();
        byId('workspace').hidden = true;
        byId('historyPanel').hidden = true;
        state.session = null;
        state.activeGroupId = null;
        state.page = 1;

        const fileInput = byId('excelFile');
        fileInput.value = '';
        byId('selectedFileName').textContent = '';
        byId('analyzeButton').disabled = true;

        const app = byId('aiImportApp');
        const modal = app?.closest('.modal');
        if (modal) {
            const bootstrapModal = globalThis.bootstrap?.Modal;
            if (bootstrapModal) bootstrapModal.getOrCreateInstance(modal).hide();
            else modal.querySelector('[data-bs-dismiss="modal"]')?.click();
        }
        app?.dispatchEvent(new CustomEvent('ai-import:completed', { bubbles: true, detail: result }));
    }
    function clearMutationState() {
        state.confirmKey = null;
        state.confirmErrors.clear();
        state.editorOptions = null;
    }

    function render() {
        const session = state.session;
        if (!session) return;
        byId('workspace').hidden = false;
        byId('sessionFile').textContent = `#${session.sessionId} · ${session.fileName}`;
        byId('sessionMeta').textContent = `Bản xem trước v${session.previewVersion} · hết hạn ${new Date(session.expiresAtUtc).toLocaleString('vi-VN')}`;
        byId('sessionStatus').textContent = statusLabels[session.status] || session.status;
        const ready = session.status === 'READY_TO_PREVIEW';
        byId('confirmButton').disabled = !ready;
        byId('cancelButton').disabled = !['READY_TO_PREVIEW', 'FAILED'].includes(session.status);
        const summary = session.summary;
        const metrics = [['Tổng dòng', summary.totalRows], ['Hợp lệ', summary.valid], ['Cảnh báo', summary.warnings], ['Lỗi', summary.errors], ['Cần xem lại', summary.reviewRequired], ['Bỏ qua', summary.skipped]];
        byId('summaryGrid').innerHTML = metrics.map(([label, count]) => `<div class="summary-tile"><strong>${count}</strong><span>${label}</span></div>`).join('');
        const analysisWarnings = session.analysisWarnings || [];
        const warningBox = byId('analysisWarnings');
        warningBox.hidden = analysisWarnings.length === 0;
        warningBox.innerHTML = analysisWarnings.map(x => `<div><strong>${escapeHtml(x.code)}</strong>: ${escapeHtml(x.message)}</div>`).join('');
        byId('groupCount').textContent = session.groups.length;
        if (!state.activeGroupId || !session.groups.some(x => x.groupId === state.activeGroupId)) state.activeGroupId = session.groups[0]?.groupId;
        byId('groupTabs').innerHTML = session.groups.map(group => `<button type="button" class="group-tab ${group.groupId === state.activeGroupId ? 'active' : ''}" data-group-id="${group.groupId}"><strong>${escapeHtml(group.sheetName)} · ${escapeHtml(entityLabels[group.entityType] || group.entityType)}</strong><small>${escapeHtml(group.regionAddress)} · ${(group.confidence * 100).toFixed(0)}%</small></button>`).join('');
        const group = session.groups.find(x => x.groupId === state.activeGroupId);
        if (!group) return;
        byId('activeGroupTitle').textContent = `${group.sheetName} / ${group.regionAddress}`;
        byId('activeGroupMeta').textContent = `Dòng tiêu đề ${group.headerRow} · thứ tự phụ thuộc ${group.dependencyOrder}`;
        byId('groupEntity').value = group.entityType;
        renderMapping(group);
        renderRows(group);
        byId('pageInfo').textContent = `${session.page.page}/${session.page.totalPages} · ${session.page.totalItems} dòng`;
        byId('previousPage').disabled = session.page.page <= 1;
        byId('nextPage').disabled = session.page.page >= session.page.totalPages;
    }

    function renderMapping(group) {
        const fields = entityFields[group.entityType] || Object.keys(group.mapping || {});
        byId('mappingFields').innerHTML = fields.map(field => `<label><span>${escapeHtml(fieldLabel(group.entityType, field))}</span><select data-mapping-field="${escapeHtml(field)}"><option value="">— Không ánh xạ —</option>${group.sourceHeaders.map(header => `<option value="${escapeHtml(header)}" ${group.mapping?.[field] === header ? 'selected' : ''}>${escapeHtml(header)}</option>`).join('')}</select></label>`).join('');
    }

    function renderBusinessValues(group, item) {
        const fields = entityFields[group.entityType] || Object.keys(item.normalizedData || {});
        const cards = fields.map(field => {
            const sourceHeader = group.mapping?.[field];
            const raw = sourceHeader ? item.rawData?.[sourceHeader] : null;
            const normalized = item.normalizedData?.[field];
            return `<div class="business-value"><strong>${escapeHtml(fieldLabel(group.entityType, field))}</strong><div><span class="value-caption">Nguồn</span><span class="value-clamp" title="${escapeHtml(displayValue(raw))}">${escapeHtml(displayValue(raw))}</span></div><i class="fa-solid fa-arrow-right" aria-hidden="true"></i><div><span class="value-caption">Chuẩn hóa</span><span class="value-clamp" title="${escapeHtml(displayValue(normalized))}">${escapeHtml(displayValue(normalized))}</span></div></div>`;
        }).join('');
        const mappedHeaders = new Set(Object.values(group.mapping || {}).filter(Boolean));
        const supplemental = Object.entries(item.rawData || {}).filter(([key]) => !mappedHeaders.has(key));
        const extra = supplemental.length
            ? `<details class="supplemental-data"><summary>Dữ liệu nguồn bổ sung (${supplemental.length})</summary>${supplemental.map(([key, value]) => `<div><b>${escapeHtml(key)}</b><span title="${escapeHtml(displayValue(value))}">${escapeHtml(displayValue(value))}</span></div>`).join('')}</details>`
            : '';
        return `<div class="business-values">${cards}</div>${extra}`;
    }

    function renderRows(group) {
        const rows = group.items || [];
        byId('previewRows').innerHTML = rows.length ? rows.map(item => {
            const confirmIssues = state.confirmErrors.get(item.itemId) || [];
            const errors = [...(item.errors || []), ...confirmIssues];
            const warnings = item.warnings || [];
            const issues = [
                ...errors.map(x => `<div class="issue"><strong>${escapeHtml(fieldLabel(group.entityType, x.field) || 'Lỗi')}</strong><span>${escapeHtml(x.message)}</span></div>`),
                ...warnings.map(x => `<div class="issue warning"><strong>${escapeHtml(fieldLabel(group.entityType, x.field) || 'Cảnh báo')}</strong><span>${escapeHtml(x.message)}</span></div>`)
            ].join('') || '<span class="no-issue">Không có lỗi</span>';
            const trace = Object.values(item.sourceTrace || {}).filter(Boolean).join(', ');
            const effectiveStatus = confirmIssues.length ? 'ERROR' : item.status;
            return `<tr class="preview-row status-${effectiveStatus.toLowerCase()}" data-item-row="${item.itemId}"><td><strong class="source-row-number">${item.sourceRow}</strong><small class="source-trace" title="${escapeHtml(trace)}">${escapeHtml(trace || group.sheetName)}</small></td><td><span class="row-status status-${effectiveStatus.toLowerCase()}">${escapeHtml(statusLabels[effectiveStatus] || effectiveStatus)}</span><small class="action-label">${escapeHtml(actionLabels[item.action] || item.action)}</small></td><td>${renderBusinessValues(group, item)}</td><td class="issues-cell">${issues}</td><td><button type="button" class="btn-ai small edit-row" data-item-id="${item.itemId}">Sửa dòng</button></td></tr>`;
        }).join('') : '<tr><td colspan="5" class="empty-preview">Không có dòng phù hợp với bộ lọc.</td></tr>';
    }

    async function loadSession(id, keepGroup = true) {
        const groupId = keepGroup ? state.activeGroupId : null;
        const params = new URLSearchParams({ page: state.page, pageSize: state.pageSize });
        if (groupId) params.set('groupId', groupId);
        if (byId('statusFilter').value) params.set('status', byId('statusFilter').value);
        state.session = await api(`/api/ai-import/${id}?${params}`);
        render();
    }

    async function analyze() {
        const file = byId('excelFile').files[0];
        if (!file) return;
        const form = new FormData();
        form.append('File', file);
        if (byId('entityHint').value) form.append('EntityHint', byId('entityHint').value);
        busy(true); message('');
        try {
            state.session = await api('/api/ai-import/analyze', { method: 'POST', body: form });
            state.activeGroupId = state.session.groups[0]?.groupId;
            state.page = 1;
            clearMutationState();
            render();
            message('Đã phân tích xong. Hãy kiểm tra bản xem trước trước khi xác nhận nhập.', 'success');
        } catch (error) {
            message(`${error.code ? `${error.code}: ` : ''}${error.message}`, 'error');
        } finally { busy(false); }
    }

    async function saveMapping() {
        const group = state.session.groups.find(x => x.groupId === state.activeGroupId);
        if (!group) return;
        const mapping = {};
        document.querySelectorAll('[data-mapping-field]').forEach(select => mapping[select.dataset.mappingField] = select.value || null);
        busy(true);
        try {
            state.session = await api(`/api/ai-import/${state.session.sessionId}/groups/${group.groupId}`, { method: 'PATCH', body: JSON.stringify({ expectedPreviewVersion: state.session.previewVersion, entityType: byId('groupEntity').value, mapping }) });
            clearMutationState();
            render();
            message('Đã lưu ánh xạ cột và kiểm tra lại toàn bộ vùng.', 'success');
        } catch (error) { await handleMutationError(error); }
        finally { busy(false); }
    }

    async function ensureEditorOptions() {
        if (state.editorOptions?.previewVersion === state.session.previewVersion) return state.editorOptions;
        state.editorOptions = await api(`/api/ai-import/${state.session.sessionId}/editor-options`);
        return state.editorOptions;
    }

    function selectOptions(field, currentValue, options) {
        const source = field.options || options?.[field.optionSource] || [];
        const normalized = source.map(x => 'value' in x ? x : ({ value: x.value, label: x.label }));
        if (currentValue && !normalized.some(x => String(x.value).toLocaleLowerCase() === String(currentValue).toLocaleLowerCase()))
            normalized.unshift({ value: currentValue, label: `${currentValue} · giá trị hiện tại không hợp lệ` });
        return `<option value="">— Chọn ${escapeHtml(field.label.toLocaleLowerCase('vi-VN'))} —</option>${normalized.map(option => `<option value="${escapeHtml(option.value)}" ${String(option.value).toLocaleLowerCase() === String(currentValue || '').toLocaleLowerCase() ? 'selected' : ''}>${escapeHtml(option.label)}</option>`).join('')}`;
    }

    function iconPicker(value) {
        return `<div class="icon-editor" data-icon-editor><div class="icon-editor-toolbar"><span class="icon-preview" data-icon-preview>${escapeHtml(value || '—')}</span><input data-edit-field="Icon" data-icon-input maxlength="10" autocomplete="off" value="${escapeHtml(value || '')}" placeholder="Ví dụ: ☕" /><button type="button" class="btn-ai small" data-icon-toggle>Chọn biểu tượng</button><button type="button" class="btn-ai small danger" data-icon-clear>Xóa</button></div><div class="icon-options" data-icon-options hidden>${iconCatalog.map(([icon, label]) => `<button type="button" data-icon="${icon}" title="${escapeHtml(label)}" aria-label="${escapeHtml(label)}: ${icon}">${icon}</button>`).join('')}</div></div>`;
    }

    function editorFieldHtml(entity, field, value, options, serverErrors) {
        const required = field.required ? '<em>*</em>' : '';
        const attributes = `${field.required ? 'required' : ''} ${field.max ? `maxlength="${field.max}"` : ''} ${field.min ? `minlength="${field.min}"` : ''} ${field.pattern ? `pattern="${field.pattern}"` : ''} ${field.inputMode ? `inputmode="${field.inputMode}"` : ''}`;
        let control;
        if (field.type === 'select') control = `<select data-edit-field="${field.name}" ${attributes}>${selectOptions(field, value, options)}</select>`;
        else if (field.type === 'textarea') control = `<textarea data-edit-field="${field.name}" rows="4" ${attributes} placeholder="${escapeHtml(field.placeholder || '')}">${escapeHtml(value || '')}</textarea>`;
        else if (field.type === 'icon') control = iconPicker(value);
        else control = `<input data-edit-field="${field.name}" type="${field.type || 'text'}" value="${escapeHtml(value || '')}" ${attributes} placeholder="${escapeHtml(field.placeholder || '')}" ${field.code ? 'data-uppercase-code' : ''} />`;
        const initialError = serverErrors.find(x => x.field === field.name)?.message || '';
        return `<label class="editor-field ${field.wide ? 'is-wide' : ''}"><span>${escapeHtml(field.label)} ${required}</span>${control}<small class="field-hint">${field.max ? `Tối đa ${field.max} ký tự.` : ''}</small><small class="field-error" data-field-error="${field.name}">${escapeHtml(initialError)}</small></label>`;
    }

    function categoryIconError(value) {
        const icon = String(value || '').trim();
        if (!icon) return '';
        if (icon.length > 10) return 'Icon tối đa 10 ký tự.';
        if (/[<>&]/u.test(icon)) return 'Icon không được chứa HTML.';
        const segments = typeof Intl.Segmenter === 'function' ? Array.from(new Intl.Segmenter('vi', { granularity: 'grapheme' }).segment(icon), x => x.segment) : Array.from(icon);
        if (segments.length !== 1) return 'Chỉ được chọn một biểu tượng Unicode.';
        let hasSymbol = false;
        for (const character of icon) {
            if (/\p{S}/u.test(character)) hasSymbol = true;
            else if (!/[\p{M}\u200D]/u.test(character)) return 'Icon phải là biểu tượng Unicode, không phải chữ hoặc số.';
        }
        return hasSymbol ? '' : 'Icon phải là một biểu tượng Unicode hợp lệ.';
    }

    function initializeIconEditor() {
        const root = byId('editFields').querySelector('[data-icon-editor]');
        if (!root) return;
        const input = root.querySelector('[data-icon-input]');
        const preview = root.querySelector('[data-icon-preview]');
        const options = root.querySelector('[data-icon-options]');
        root.querySelector('[data-icon-toggle]').addEventListener('click', () => { options.hidden = !options.hidden; });
        root.querySelector('[data-icon-clear]').addEventListener('click', () => { input.value = ''; input.dispatchEvent(new Event('input', { bubbles: true })); });
        root.addEventListener('click', event => {
            const button = event.target.closest('[data-icon]');
            if (!button) return;
            input.value = button.dataset.icon || '';
            input.dispatchEvent(new Event('input', { bubbles: true }));
            options.hidden = true;
        });
        input.addEventListener('beforeinput', event => { if (event.data && /[\p{L}\p{N}]/u.test(event.data)) event.preventDefault(); });
        input.addEventListener('input', () => { preview.textContent = input.value.trim() || '—'; });
    }

    function renderEditor(group, item, options) {
        const schema = editorSchemas[group.entityType];
        if (!schema) return;
        state.editingItem = item;
        byId('editEntityName').textContent = entityLabels[group.entityType] || group.entityType;
        byId('editRowNumber').textContent = item.sourceRow;
        const serverErrors = [...(item.errors || []), ...(state.confirmErrors.get(item.itemId) || [])];
        byId('editFields').innerHTML = schema.sections.map(section => `<fieldset class="editor-section"><legend>${escapeHtml(section.title)}</legend>${section.note ? `<p>${escapeHtml(section.note)}</p>` : ''}<div class="editor-grid">${section.fields.map(field => editorFieldHtml(group.entityType, field, item.normalizedData?.[field.name], options, serverErrors)).join('')}</div></fieldset>`).join('');
        const mappedHeaders = new Set(Object.values(group.mapping || {}).filter(Boolean));
        const supplemental = Object.entries(item.rawData || {}).filter(([key]) => !mappedHeaders.has(key));
        byId('editSourceData').innerHTML = supplemental.length ? supplemental.map(([key, value]) => `<div><b>${escapeHtml(key)}</b><span>${escapeHtml(displayValue(value))}</span></div>`).join('') : '<p>Không có dữ liệu ngoài ánh xạ.</p>';
        const hasWarnings = (item.warnings || []).length > 0;
        const needsOverride = group.entityType === 'Supplier' && (item.warnings || []).some(x => x.code === 'NHÀ_CUNG_CẤP_GẦN_TRÙNG');
        byId('warningSection').hidden = !hasWarnings;
        byId('acknowledgeWarnings').checked = item.warningsAcknowledged;
        byId('overrideReason').value = item.duplicateOverrideReason || '';
        byId('overrideReasonGroup').hidden = !needsOverride;
        byId('editFormAlert').hidden = serverErrors.length === 0;
        byId('editFormAlert').textContent = serverErrors.length ? 'Dòng này còn lỗi. Vui lòng sửa các trường được đánh dấu.' : '';
        initializeIconEditor();
        byId('editFields').querySelectorAll('[data-uppercase-code]').forEach(input => input.addEventListener('input', () => {
            const cursor = input.selectionStart;
            input.value = input.value.toUpperCase();
            input.setSelectionRange(cursor, cursor);
        }));
        byId('editItemForm').querySelectorAll('input,select,textarea').forEach(input => input.addEventListener('input', () => validateEditor(false)));
        validateEditor(false);
    }

    function validateEditor(showMessages) {
        const item = state.editingItem;
        const group = state.session?.groups.find(x => x.groupId === state.activeGroupId);
        if (!item || !group) return false;
        let valid = true;
        for (const field of editorSchemas[group.entityType].sections.flatMap(x => x.fields)) {
            const input = byId('editFields').querySelector(`[data-edit-field="${field.name}"]`);
            if (!input) continue;
            const value = input.value.trim();
            let error = '';
            if (field.required && !value) error = `Vui lòng nhập ${field.label.toLocaleLowerCase('vi-VN')}.`;
            else if (field.min && value.length < field.min) error = `${field.label} phải có ít nhất ${field.min} ký tự.`;
            else if (field.max && value.length > field.max) error = `${field.label} tối đa ${field.max} ký tự.`;
            else if (field.type === 'email' && value && !/^\S+@\S+\.\S+$/u.test(value)) error = 'Email đầu mối không đúng định dạng.';
            else if (field.pattern && value && !(new RegExp(field.pattern)).test(value)) error = 'Mã số thuế phải có 10 chữ số hoặc dạng 10-3 chữ số.';
            else if (field.type === 'icon') error = categoryIconError(value);
            input.setCustomValidity(error);
            const errorBox = byId('editFields').querySelector(`[data-field-error="${field.name}"]`);
            if (errorBox) errorBox.textContent = error;
            input.classList.toggle('is-invalid', !!error);
            valid = valid && !error;
        }
        const needsAcknowledgement = !byId('warningSection').hidden;
        if (needsAcknowledgement && !byId('acknowledgeWarnings').checked) valid = false;
        const override = byId('overrideReason');
        const overrideError = !byId('overrideReasonGroup').hidden && !override.value.trim() ? 'Vui lòng nhập lý do vẫn tạo nhà cung cấp.' : '';
        override.setCustomValidity(overrideError);
        byId('editItemForm').querySelector('[data-field-error="DuplicateOverrideReason"]').textContent = overrideError;
        valid = valid && !overrideError;
        byId('saveItemButton').disabled = !valid;
        if (showMessages && !valid) {
            byId('editFormAlert').hidden = false;
            byId('editFormAlert').textContent = 'Vui lòng hoàn tất các trường bắt buộc và xử lý cảnh báo.';
            byId('editItemForm').querySelector(':invalid')?.focus();
        }
        return valid;
    }

    async function openEditor(itemId) {
        const group = state.session.groups.find(x => x.groupId === state.activeGroupId);
        const item = group?.items.find(x => x.itemId === itemId);
        if (!item) return;
        busy(true);
        try {
            const options = await ensureEditorOptions();
            renderEditor(group, item, options);
            byId('editDialog').showModal();
        } catch (error) { message(error.message, 'error'); }
        finally { busy(false); }
    }

    async function saveItem(action) {
        const item = state.editingItem;
        if (!item) return;
        if (action === 'CREATE' && !validateEditor(true)) return;
        const values = {};
        document.querySelectorAll('[data-edit-field]').forEach(input => values[input.dataset.editField] = input.value);
        busy(true);
        try {
            state.session = await api(`/api/ai-import/${state.session.sessionId}/items/${item.itemId}`, { method: 'PATCH', body: JSON.stringify({ expectedPreviewVersion: state.session.previewVersion, action, values, warningsAcknowledged: byId('acknowledgeWarnings').checked, duplicateOverrideReason: byId('overrideReason').value }) });
            clearMutationState();
            const group = state.session.groups.find(x => x.groupId === state.activeGroupId);
            const updatedItem = group?.items.find(x => x.itemId === item.itemId);
            render();
            if (action === 'CREATE' && updatedItem && ['ERROR', 'REVIEW_REQUIRED'].includes(updatedItem.status)) {
                const options = await ensureEditorOptions();
                renderEditor(group, updatedItem, options);
                message('Dòng vẫn còn lỗi; vui lòng kiểm tra các trường được đánh dấu.', 'error');
            } else {
                closeEditDialog();
                message(action === 'SKIP' ? 'Đã bỏ qua dòng.' : 'Đã lưu và kiểm tra lại dòng.', 'success');
            }
        } catch (error) { await handleMutationError(error); }
        finally { busy(false); }
    }

    function captureConfirmErrors(details) {
        state.confirmErrors.clear();
        for (const detail of details || []) {
            if (!detail.itemId) continue;
            const values = state.confirmErrors.get(detail.itemId) || [];
            values.push(detail);
            state.confirmErrors.set(detail.itemId, values);
        }
    }

    async function focusConfirmError(details) {
        const first = (details || []).find(x => x.itemId);
        if (!first) return;
        const targetGroup = state.session.groups.find(x => x.sheetName === first.position?.sheet);
        if (targetGroup) state.activeGroupId = targetGroup.groupId;
        state.page = 1;
        byId('statusFilter').value = '';
        await loadSession(state.session.sessionId);
        requestAnimationFrame(() => document.querySelector(`[data-item-row="${first.itemId}"]`)?.scrollIntoView({ behavior: 'smooth', block: 'center' }));
    }

    async function confirmSession() {
        if (!confirm('Hệ thống sẽ tạo toàn bộ dữ liệu trong một giao dịch. Bạn muốn xác nhận nhập?')) return;
        state.confirmKey ||= (crypto.randomUUID ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`);
        busy(true);
        try {
            const result = await api(`/api/ai-import/${state.session.sessionId}/confirm`, { method: 'POST', headers: { 'Idempotency-Key': state.confirmKey }, body: JSON.stringify({ expectedPreviewVersion: state.session.previewVersion }) });
            message(`Hoàn tất: đã nhập ${result.imported}, bỏ qua ${result.skipped}.`, 'success');
            clearMutationState();
            closeImportWorkspace(result);
        } catch (error) {
            captureConfirmErrors(error.details);
            message(`${error.code ? `${error.code}: ` : ''}${error.message}`, 'error');
            if (['PREVIEW_ĐÃ_THAY_ĐỔI', 'PREVIEW_CHƯA_SẴN_SÀNG'].includes(error.code) || error.details?.length) await focusConfirmError(error.details);
        } finally { busy(false); }
    }

    async function handleMutationError(error) {
        message(`${error.code ? `${error.code}: ` : ''}${error.message}`, 'error');
        if (error.code === 'PREVIEW_ĐÃ_THAY_ĐỔI') await loadSession(state.session.sessionId);
    }

    async function history() {
        try {
            const data = await api('/api/ai-import/history?page=1&pageSize=30');
            byId('historyRows').innerHTML = data.items.map(x => `<button type="button" class="history-row group-tab" data-history-id="${x.sessionId}"><strong>#${x.sessionId} · ${escapeHtml(x.fileName)}</strong><span class="row-status">${escapeHtml(statusLabels[x.status] || x.status)}</span><small>${new Date(x.createdAtUtc).toLocaleString('vi-VN')} · ${x.importedRows}/${x.totalRows} đã nhập</small></button>`).join('') || '<p>Chưa có phiên.</p>';
            byId('historyPanel').hidden = false;
        } catch (error) { message(error.message, 'error'); }
    }

    const fileInput = byId('excelFile');
    byId('chooseFileButton').addEventListener('click', () => fileInput.click());
    fileInput.addEventListener('change', () => { const file = fileInput.files[0]; byId('selectedFileName').textContent = file?.name || ''; byId('analyzeButton').disabled = !file; });
    const drop = byId('dropZone');
    ['dragenter', 'dragover'].forEach(name => drop.addEventListener(name, event => { event.preventDefault(); drop.classList.add('dragging'); }));
    ['dragleave', 'drop'].forEach(name => drop.addEventListener(name, event => { event.preventDefault(); drop.classList.remove('dragging'); }));
    drop.addEventListener('drop', event => { if (event.dataTransfer.files.length) { const transfer = new DataTransfer(); transfer.items.add(event.dataTransfer.files[0]); fileInput.files = transfer.files; fileInput.dispatchEvent(new Event('change')); } });
    byId('analyzeButton').addEventListener('click', analyze);
    byId('saveMappingButton').addEventListener('click', saveMapping);
    byId('confirmButton').addEventListener('click', confirmSession);
    byId('reanalyzeButton').addEventListener('click', async () => { busy(true); try { state.session = await api(`/api/ai-import/${state.session.sessionId}/reanalyze`, { method: 'POST' }); clearMutationState(); render(); } catch (error) { message(error.message, 'error'); } finally { busy(false); } });
    byId('cancelButton').addEventListener('click', async () => {
        if (!confirm('Hủy phiên này?')) return;
        busy(true);
        try {
            state.session = await api(`/api/ai-import/${state.session.sessionId}/cancel`, { method: 'POST', body: JSON.stringify({ expectedPreviewVersion: state.session.previewVersion }) });
            closeEditDialog();
            render();
        } catch (error) { await handleMutationError(error); }
        finally { busy(false); }
    });
    byId('groupEntity').addEventListener('change', () => { const group = state.session.groups.find(x => x.groupId === state.activeGroupId); group.entityType = byId('groupEntity').value; renderMapping(group); });
    byId('groupTabs').addEventListener('click', async event => { const button = event.target.closest('[data-group-id]'); if (!button) return; state.activeGroupId = Number(button.dataset.groupId); state.page = 1; await loadSession(state.session.sessionId); });
    byId('previewRows').addEventListener('click', event => { const button = event.target.closest('.edit-row'); if (button) openEditor(Number(button.dataset.itemId)); });
    byId('saveItemButton').addEventListener('click', () => saveItem('CREATE'));
    byId('skipItemButton').addEventListener('click', () => saveItem('SKIP'));
    byId('acknowledgeWarnings').addEventListener('change', () => validateEditor(false));
    byId('overrideReason').addEventListener('input', () => validateEditor(false));
    byId('editDialog').addEventListener('close', () => { state.editingItem = null; });
    byId('statusFilter').addEventListener('change', async () => { if (!state.session) return; state.page = 1; await loadSession(state.session.sessionId); });
    byId('previousPage').addEventListener('click', async () => { state.page--; await loadSession(state.session.sessionId); });
    byId('nextPage').addEventListener('click', async () => { state.page++; await loadSession(state.session.sessionId); });
    byId('refreshHistoryButton').addEventListener('click', history);
    byId('closeHistoryButton').addEventListener('click', () => byId('historyPanel').hidden = true);
    byId('historyRows').addEventListener('click', async event => { const row = event.target.closest('[data-history-id]'); if (!row) return; state.activeGroupId = null; state.page = 1; clearMutationState(); await loadSession(Number(row.dataset.historyId), false); byId('historyPanel').hidden = true; });
})();
