(() => {
    'use strict';

    const app = document.getElementById('aiImportApp');
    if (!app) return;

    const byId = id => document.getElementById(id);
    const catalog = window.CafeChainUiCatalog.read('aiImportUiCatalog');
    const t = (key, values) => window.CafeChainUiCatalog.text(catalog, key, values);
    const token = document.querySelector('#antiForgeryForm input[name="__RequestVerificationToken"]')?.value || '';
    const state = {
        session: null,
        activeGroupId: null,
        page: 1,
        pageSize: 50,
        editingItem: null,
        confirmKey: null,
        editorOptions: null,
        confirmErrors: new Map(),
        sessionGeneration: 0,
        mutationBusy: false,
        requestBusyCount: 0
    };

    const interactionFactory = window.AIImportInteractions;
    if (!interactionFactory) throw new Error('AI Import interaction coordinator is not loaded.');

    const alertCoordinator = window.Swal
        ? interactionFactory.createAlertCoordinator({ swal: window.Swal, target: document.body })
        : null;
    const mutationGuard = interactionFactory.createOperationGuard(value => {
        state.mutationBusy = value;
        syncBusyUi();
    });

    const entityLabels = {
        Category: t('Entity.Category'), Drink: t('Entity.Drink'), Size: t('Entity.Size'), Ingredient: t('Entity.Ingredient'), Supplier: t('Entity.Supplier'), Unknown: t('Common.Unknown')
    };
    const statusLabels = {
        VALID: 'Hợp lệ', WARNING: 'Cảnh báo', ERROR: 'Lỗi', REVIEW_REQUIRED: 'Cần xem lại', SKIPPED: 'Bỏ qua', IMPORTED: 'Đã nhập',
        READY_TO_PREVIEW: 'Sẵn sàng xem trước', ANALYZING: 'Đang phân tích', VALIDATING: 'Đang kiểm tra', IMPORTING: 'Đang nhập',
        COMPLETED: 'Hoàn tất', FAILED: 'Thất bại', CANCELLED: 'Đã hủy', EXPIRED: 'Đã hết hạn', UPLOADED: 'Đã tải lên'
    };
    const actionLabels = { CREATE: t('Action.Create'), SKIP: t('Action.Skip') };
    const formatLabels = { XLSX: 'Excel', DOCX: 'DOCX', PDF: 'PDF', MULTI: 'Nhiều tệp' };
    const sourceDocumentStatusLabels = {
        PROCESSING: 'Đang xử lý', READY: 'Sẵn sàng', FAILED: 'Không thành công', REMOVED: 'Đã loại khỏi phiên'
    };
    const columnClassificationLabels = {
        MAPPED: 'Đã ánh xạ',
        IGNORED: 'Hệ thống nhận diện nhưng bỏ qua',
        UNKNOWN: 'Chưa xác định',
        FORBIDDEN: 'Không được phép nhập'
    };
    const columnClassificationDescriptions = {
        MAPPED: 'Cột nguồn đang được nối với một trường dữ liệu nghiệp vụ.',
        IGNORED: 'Cột thông tin phụ đã được nhận diện và mặc định không nhập.',
        UNKNOWN: 'Hệ thống chưa xác định ý nghĩa nghiệp vụ của cột này.',
        FORBIDDEN: 'Cột chứa định danh, phạm vi, quyền hoặc lệnh không được phép nhập.'
    };
    const sourceKindLabels = {
        SOURCE: 'Nguồn', OCR: 'Nhận dạng ký tự', AI: 'AI', CELL: 'Ô dữ liệu', TEXT: 'Văn bản',
        TEXT_LAYER: 'Lớp văn bản', AI_AFTER_TEXT: 'AI sau khi đọc văn bản',
        AI_AFTER_OCR: 'AI sau khi nhận dạng ký tự'
    };
    const issueSeverityLabels = { ERROR: 'Lỗi', REVIEW: 'Cần xem lại', WARNING: 'Cảnh báo' };
    const issueResolutionLabels = {
        EDIT_FIELD: 'Sửa trường dữ liệu', REMAP_GROUP: 'Ánh xạ lại cột', ACKNOWLEDGE: 'Xác nhận cảnh báo',
        MANUAL_REVIEW: 'Đối chiếu thủ công', SKIP_CONFLICT: 'Bỏ qua bản ghi xung đột', REUPLOAD_OR_SKIP: 'Tải lại hoặc bỏ qua'
    };
    const extractionModeLabels = {
        XLSX_DETERMINISTIC: 'Excel theo quy tắc', XLSX_AI_MAPPING: 'Excel ánh xạ bằng AI',
        DOCX_TABLE_DETERMINISTIC: 'Bảng DOCX theo quy tắc', DOCX_TEXT_DETERMINISTIC: 'Văn bản DOCX theo quy tắc', DOCX_AI_EXTRACTION: 'DOCX trích xuất bằng AI',
        PDF_TEXT_DETERMINISTIC: 'Văn bản PDF theo quy tắc', PDF_TEXT_AI_EXTRACTION: 'Văn bản PDF trích xuất bằng AI',
        PDF_OCR_DETERMINISTIC: 'PDF nhận dạng ký tự theo quy tắc', PDF_OCR_AI_EXTRACTION: 'PDF nhận dạng ký tự và trích xuất bằng AI',
        PDF_MIXED_TEXT_OCR: 'PDF hỗn hợp văn bản và nhận dạng ký tự'
    };
    const extractionBadge = mode => mode?.includes('MIXED') ? 'HỖN HỢP' : mode?.includes('OCR') ? 'OCR' : 'VĂN BẢN';
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
            sections: [{ title: 'Thông tin kích cỡ', fields: [
                { name: 'SizeCode', label: 'Mã kích cỡ', required: true, max: 20, code: true, placeholder: 'Ví dụ: M' },
                { name: 'Name', label: 'Tên kích cỡ', required: true, max: 50, placeholder: 'Ví dụ: Vừa' },
                { name: 'SizeType', label: 'Loại kích cỡ', required: true, type: 'select', options: [{ value: 'Cup', label: 'Theo ly' }, { value: 'Volume', label: 'Theo dung tích' }] },
                { name: 'Description', label: 'Mô tả kích cỡ', max: 300, wide: true, placeholder: 'Ví dụ: Kích cỡ vừa' }
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
    const fieldLabel = (entity, name) => fieldDefinition(entity, name)?.label || 'Trường dữ liệu';

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

    function syncBusyUi() {
        const requestBusy = state.requestBusyCount > 0;
        const interactionBusy = requestBusy || state.mutationBusy;
        const session = state.session;
        const ready = session?.status === 'READY_TO_PREVIEW';
        const editing = !!state.editingItem;

        byId('loadingPanel').hidden = !requestBusy;
        app.setAttribute('aria-busy', interactionBusy ? 'true' : 'false');
        byId('analyzeButton').disabled = interactionBusy || !(byId('excelFile').files?.length);
        byId('confirmButton').disabled = interactionBusy || !ready || session?.canConfirm !== true;
        byId('cancelButton').disabled = interactionBusy || !['READY_TO_PREVIEW', 'FAILED'].includes(session?.status);
        byId('reanalyzeButton').disabled = interactionBusy || !session;
        byId('saveMappingButton').disabled = interactionBusy || !ready;
        byId('skipItemButton').disabled = interactionBusy || !editing || !ready;

        if (interactionBusy) byId('saveItemButton').disabled = true;
        else if (editing) validateEditor(false);
        else byId('saveItemButton').disabled = true;

        app.querySelectorAll('.remove-source, .source-mapping-choice, .history-row').forEach(element => {
            element.disabled = interactionBusy;
        });
    }

    function busy(value) {
        state.requestBusyCount = Math.max(0, state.requestBusyCount + (value ? 1 : -1));
        syncBusyUi();
    }

    function runMutation(key, operation) {
        return mutationGuard.run(key, operation);
    }

    async function fireAlert(options) {
        if (!alertCoordinator) {
            console.error('SweetAlert2 chưa được tải.', options.text || options.title || '');
            return { isConfirmed: false };
        }
        const alertKey = `${options.icon || 'info'}:${options.title || ''}:${options.text || ''}`;
        return alertCoordinator.show(alertKey, {
            confirmButtonColor: '#70482f',
            cancelButtonColor: '#667085',
            confirmButtonText: 'Đóng',
            heightAuto: false,
            returnFocus: false,
            allowOutsideClick: false,
            ...options
        });
    }

    function showAlert(text, icon = 'info', title = '') {
        const titles = { success: 'Thành công', error: 'Không thể thực hiện', warning: 'Cần chú ý', info: 'Thông báo' };
        return fireAlert({ title: title || titles[icon] || titles.info, text, icon });
    }

    async function confirmAction(title, text, confirmButtonText, icon = 'warning') {
        const result = await fireAlert({
            title,
            text,
            icon,
            showCancelButton: true,
            confirmButtonText,
            cancelButtonText: 'Không'
        });
        return result.isConfirmed === true;
    }

    function errorMessage(error) {
        return error.message || 'Không thể thực hiện yêu cầu.';
    }
    function uniqueIssues(issues) {
        const values = new Map();
        for (const issue of issues || []) {
            const key = issue.issueKey || [issue.code, issue.field || '', issue.metadata?.referenceTarget || '',
                issue.metadata?.matchedSupplierId || '', locatorLabel(issue.sourceLocator || issue.position, '')].join('|');
            values.set(key, issue);
        }
        return [...values.values()];
    }
    function escapeHtml(value) { const div = document.createElement('div'); div.textContent = value ?? ''; return div.innerHTML; }
    function displayValue(value) { return value === null || value === undefined || value === '' ? '—' : String(value); }
    function locatorLabel(locator, fallback = '') {
        if (!locator) return fallback;
        if (locator.sheet) return `${locator.sheet}${locator.row ? ` · dòng ${locator.row}` : ''}`;
        if (locator.table) return `Bảng ${locator.table}${locator.tableRow ? ` · dòng ${locator.tableRow}` : ''}`;
        if (locator.paragraph) return `Đoạn ${locator.paragraph}`;
        if (locator.page) return `Trang ${locator.page}${locator.block ? ` · khối ${locator.block}` : ''}`;
        if (Number.isInteger(locator.textStart)) return `Văn bản ${locator.textStart}–${locator.textEnd ?? locator.textStart}`;
        return fallback;
    }
    function closeEditDialog() {
        const dialog = byId('editDialog');
        if (dialog?.open) dialog.close();
        state.editingItem = null;
    }
    function closeAllDialogs() {
        closeEditDialog();
        alertCoordinator?.close();
        app.querySelectorAll('dialog[open]').forEach(dialog => dialog.close());
    }
    function resetSessionFilters() {
        ['statusFilter', 'sourceFilter', 'entityFilter'].forEach(id => {
            const input = byId(id);
            if (input) input.value = '';
        });
    }
    function resetSelectedFiles() {
        const fileInput = byId('excelFile');
        if (fileInput) fileInput.value = '';
        byId('selectedFileName').textContent = '';
        byId('analyzeButton').disabled = true;
        byId('uploadErrors').hidden = true;
        byId('uploadErrors').innerHTML = '';
    }
    function closeSessionView() {
        state.sessionGeneration++;
        closeAllDialogs();
        byId('workspace').hidden = true;
        byId('historyPanel').hidden = true;
        state.session = null;
        state.activeGroupId = null;
        state.page = 1;
        resetSessionFilters();
        resetSelectedFiles();
        clearMutationState();
    }
    function sessionGuard() {
        return { id: state.session?.sessionId, generation: state.sessionGeneration };
    }
    function guardIsCurrent(guard) {
        return guard.id === state.session?.sessionId && guard.generation === state.sessionGeneration;
    }

    function closeImportWorkspace(result) {
        state.sessionGeneration++;
        closeEditDialog();
        byId('workspace').hidden = true;
        byId('historyPanel').hidden = true;
        state.session = null;
        state.activeGroupId = null;
        state.page = 1;

        resetSessionFilters();
        resetSelectedFiles();

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
        byId('sessionFile').textContent = `#${session.sessionId} · [${formatLabels[session.sourceFormat] || 'Tệp dữ liệu'}] ${session.fileName}`;
        const modes = (session.extractionModes || []).map(mode => extractionModeLabels[mode] || 'Cách trích xuất chưa xác định').join(', ');
        byId('sessionMeta').textContent = `Bản xem trước v${session.previewVersion} · ${modes || 'Đang xác định nguồn'} · hết hạn ${new Date(session.expiresAtUtc).toLocaleString('vi-VN')}`;
        byId('sessionStatus').textContent = statusLabels[session.status] || 'Chưa xác định';
        const ready = session.status === 'READY_TO_PREVIEW';
        byId('confirmButton').disabled = !ready || session.canConfirm !== true;
        byId('cancelButton').disabled = !['READY_TO_PREVIEW', 'FAILED'].includes(session.status);
        const summary = session.summary;
        const metrics = [['Tổng dòng', summary.totalRows], ['Hợp lệ', summary.valid], ['Cảnh báo', summary.warnings], ['Lỗi', summary.errors], ['Cần xem lại', summary.reviewRequired], ['Bỏ qua', summary.skipped]];
        byId('summaryGrid').innerHTML = metrics.map(([label, count]) => `<div class="summary-tile"><strong>${count}</strong><span>${label}</span></div>`).join('');
        byId('sourceDocuments').innerHTML = (session.sourceDocuments || []).map(source => `<div class="source-document status-${escapeHtml(source.status.toLowerCase())}"><strong>[${escapeHtml(formatLabels[source.sourceFormat] || 'Tệp dữ liệu')}] ${escapeHtml(source.fileName)}</strong><span class="row-status">${escapeHtml(sourceDocumentStatusLabels[source.status] || 'Chưa xác định')}</span><small>${source.errorCode ? `${escapeHtml(source.errorMessage || 'Tài liệu nguồn không thể xử lý.')}` : `${(source.fileSize / 1024 / 1024).toFixed(2)} MiB`}</small>${ready && source.status !== 'REMOVED' ? `<button type="button" class="btn-ai small danger remove-source" data-source-id="${source.sourceDocumentId}">Loại nguồn</button>` : ''}</div>`).join('');
        const sourceFilter = byId('sourceFilter');
        const selectedSource = sourceFilter.value;
        const availableSources = (session.sourceDocuments || []).filter(source => source.status !== 'REMOVED');
        sourceFilter.innerHTML = `<option value="">Tất cả</option>${availableSources.map(source => `<option value="${source.sourceDocumentId}">${escapeHtml(source.fileName)}</option>`).join('')}`;
        sourceFilter.value = availableSources.some(source => String(source.sourceDocumentId) === selectedSource) ? selectedSource : '';
        const analysisWarnings = session.analysisWarnings || [];
        const warningBox = byId('analysisWarnings');
        warningBox.hidden = analysisWarnings.length === 0;
        warningBox.innerHTML = analysisWarnings.map(x => `<div>${escapeHtml(x.message)}</div>`).join('');
        const blockers = uniqueIssues(session.confirmBlockers || []);
        const blockerBox = byId('confirmBlockers');
        blockerBox.hidden = blockers.length === 0;
        blockerBox.innerHTML = blockers.length
            ? `<strong>Chưa thể xác nhận nhập</strong>${blockers.map(issue => `<div>${escapeHtml(issue.message)}</div>`).join('')}`
            : '';
        const entityFilter = byId('entityFilter').value;
        const visibleGroups = session.groups.filter(group =>
            (!sourceFilter.value || String(group.sourceDocumentId) === sourceFilter.value)
            && (!entityFilter || group.entityType === entityFilter));
        byId('groupCount').textContent = visibleGroups.length;
        if (!state.activeGroupId || !visibleGroups.some(x => x.groupId === state.activeGroupId)) state.activeGroupId = visibleGroups[0]?.groupId;
        byId('groupTabs').innerHTML = visibleGroups.map(group => `<button type="button" class="group-tab ${group.groupId === state.activeGroupId ? 'active' : ''}" data-group-id="${group.groupId}"><strong>${escapeHtml(group.sourceFileName || session.fileName)} · ${escapeHtml(group.sourceLabel || group.sheetName)} · ${escapeHtml(entityLabels[group.entityType] || 'Chưa xác định')}</strong><small>${escapeHtml(locatorLabel(group.sourceLocator, group.regionAddress))} · ${(group.confidence * 100).toFixed(0)}%</small></button>`).join('');
        const group = session.groups.find(x => x.groupId === state.activeGroupId);
        if (!group) {
            byId('activeGroupTitle').textContent = 'Không có vùng dữ liệu';
            byId('activeGroupMeta').textContent = '';
            byId('mappingFields').innerHTML = '';
            byId('previewRows').innerHTML = '<tr><td colspan="5" class="empty-preview">Tệp nguồn này chưa có vùng dữ liệu hợp lệ.</td></tr>';
            syncBusyUi();
            return;
        }
        byId('activeGroupTitle').textContent = `${group.sourceLabel || group.sheetName} / ${locatorLabel(group.sourceLocator, group.regionAddress)}`;
        byId('activeGroupMeta').textContent = `[${extractionBadge(group.extractionMode)}] ${extractionModeLabels[group.extractionMode] || 'Cách trích xuất chưa xác định'} · thứ tự phụ thuộc ${group.dependencyOrder}`;
        byId('groupEntity').value = group.entityType;
        renderMapping(group);
        renderRows(group);
        byId('pageInfo').textContent = `${session.page.page}/${session.page.totalPages} · ${session.page.totalItems} dòng`;
        byId('previousPage').disabled = session.page.page <= 1;
        byId('nextPage').disabled = session.page.page >= session.page.totalPages;
        syncBusyUi();
    }

    function renderMapping(group) {
        const fields = entityFields[group.entityType] || Object.keys(group.mapping || {});
        const columns = group.sourceColumns?.length ? group.sourceColumns : (group.sourceHeaders || []).map(header => ({ key: header, label: header, classification: 'UNKNOWN' }));
        const conflicts = new Map((group.issues || []).filter(issue => issue.code === 'XUNG_ĐỘT_ÁNH_XẠ')
            .map(issue => [issue.metadata?.targetField || issue.field, issue]));
        const conflictBySource = new Map();
        for (const [targetField, conflict] of conflicts) {
            for (const sourceKey of conflict.metadata?.candidateSourceKeys || []) conflictBySource.set(sourceKey, targetField);
        }
        const targetBySource = new Map(Object.entries(group.mapping || {})
            .filter(([, sourceKey]) => sourceKey)
            .map(([targetField, sourceKey]) => [sourceKey, targetField]));
        const assignedTargets = new Map([...targetBySource].map(([sourceKey, targetField]) => [targetField, sourceKey]));
        const samples = new Map(columns.map(column => [column.key, (group.items || [])
            .map(item => item.rawData?.[column.key])
            .find(value => value !== null && value !== undefined && value !== '')]));
        const grouped = new Map(['MAPPED', 'IGNORED', 'UNKNOWN', 'FORBIDDEN'].map(classification => [classification, []]));

        for (const column of columns) {
            const currentTarget = targetBySource.get(column.key) || '';
            const classification = currentTarget ? 'MAPPED' : (column.classification || 'UNKNOWN');
            const forbidden = classification === 'FORBIDDEN';
            const targetOptions = fields
                .filter(field => !assignedTargets.has(field) || assignedTargets.get(field) === column.key)
                .map(field => `<option value="${escapeHtml(field)}" ${currentTarget === field ? 'selected' : ''}>${escapeHtml(fieldLabel(group.entityType, field))}</option>`)
                .join('');
            const selector = forbidden
                ? '<select disabled aria-label="Cột không được phép nhập"><option>Không thể ánh xạ cột này</option></select>'
                : `<select data-source-column="${escapeHtml(column.key)}" data-target-field="${escapeHtml(currentTarget)}" aria-label="Chọn trường đích cho cột ${escapeHtml(column.label || column.key)}"><option value="">⊘ Bỏ qua cột này</option><optgroup label="Trường có thể ánh xạ">${targetOptions}</optgroup></select>`;
            const conflictTarget = conflictBySource.get(column.key);
            const conflictNote = conflictTarget
                ? `<em class="mapping-required">Cần chọn nguồn cho ${escapeHtml(fieldLabel(group.entityType, conflictTarget))}</em>`
                : '';
            const reason = columnClassificationDescriptions[classification] || columnClassificationDescriptions.UNKNOWN;
            grouped.get(classification)?.push(`<div class="mapping-row ${conflictTarget ? 'mapping-conflict' : ''}" data-column-classification="${escapeHtml(classification)}"><div class="mapping-source"><strong>${escapeHtml(column.label || column.key)}</strong>${column.key !== column.label ? `<small>Mã cột: ${escapeHtml(column.key)}</small>` : ''}${conflictNote}</div><div class="mapping-sample" title="${escapeHtml(displayValue(samples.get(column.key)))}">${escapeHtml(displayValue(samples.get(column.key)))}</div><div class="mapping-state"><span class="mapping-state-badge status-${escapeHtml(classification.toLowerCase())}">${escapeHtml(columnClassificationLabels[classification] || columnClassificationLabels.UNKNOWN)}</span><small>${escapeHtml(reason)}</small></div><div class="mapping-target">${selector}</div></div>`);
        }

        byId('mappingFields').innerHTML = [...grouped.entries()]
            .filter(([, rows]) => rows.length)
            .map(([classification, rows]) => `<section class="mapping-section"><h4>${escapeHtml(columnClassificationLabels[classification])}<span>${rows.length}</span></h4>${rows.join('')}</section>`)
            .join('') || '<p class="mapping-empty">Không có cột nguồn để ánh xạ.</p>';
        syncMappingTargetAvailability();
    }

    function syncMappingTargetAvailability() {
        const selects = [...byId('mappingFields').querySelectorAll('[data-source-column]')];
        const selectedTargets = new Map(selects.filter(select => select.value)
            .map(select => [select.value, select.dataset.sourceColumn]));
        for (const select of selects) {
            for (const option of select.querySelectorAll('optgroup option')) {
                option.disabled = selectedTargets.has(option.value)
                    && selectedTargets.get(option.value) !== select.dataset.sourceColumn;
            }
            select.dataset.targetField = select.value;
        }
    }

    function renderBusinessValues(group, item) {
        const fields = entityFields[group.entityType] || Object.keys(item.normalizedData || {});
        const cards = fields.map(field => {
            const sourceHeader = group.mapping?.[field];
            const raw = sourceHeader ? item.rawData?.[sourceHeader] : null;
            const normalized = item.normalizedData?.[field];
            const fieldEvidence = item.fieldEvidence?.[field];
            const evidenceMeta = fieldEvidence
                ? `<small class="source-evidence">${escapeHtml(sourceKindLabels[fieldEvidence.sourceKind] || 'Nguồn')} · ${escapeHtml(locatorLabel(fieldEvidence.sourceLocator, ''))}${fieldEvidence.ocrConfidence == null ? '' : ` · OCR ${(fieldEvidence.ocrConfidence * 100).toFixed(0)}%`}${fieldEvidence.aiConfidence == null ? '' : ` · AI ${(fieldEvidence.aiConfidence * 100).toFixed(0)}%`} · Văn bản gốc: ${escapeHtml(fieldEvidence.rawText || '')}</small>`
                : '';
            return `<div class="business-value"><strong>${escapeHtml(fieldLabel(group.entityType, field))}</strong><div><span class="value-caption">Nguồn</span><span class="value-clamp" title="${escapeHtml(displayValue(raw))}">${escapeHtml(displayValue(raw))}</span></div><i class="fa-solid fa-arrow-right" aria-hidden="true"></i><div><span class="value-caption">Chuẩn hóa</span><span class="value-clamp" title="${escapeHtml(displayValue(normalized))}">${escapeHtml(displayValue(normalized))}</span></div>${evidenceMeta}</div>`;
        }).join('');
        const mappedHeaders = new Set(Object.values(group.mapping || {}).filter(Boolean));
        const supplemental = Object.entries(item.rawData || {}).filter(([key]) => !mappedHeaders.has(key));
        const columnMap = new Map((group.sourceColumns || []).map(column => [column.key, column]));
        const extra = supplemental.length
            ? `<details class="supplemental-data"><summary>Dữ liệu nguồn bổ sung (${supplemental.length})</summary>${supplemental.map(([key, value]) => { const column = columnMap.get(key); return `<div><b>${escapeHtml(column?.label || key)}${column?.classification ? ` · ${escapeHtml(column.classification)}` : ''}</b><span title="${escapeHtml(displayValue(value))}">${escapeHtml(displayValue(value))}</span></div>`; }).join('')}</details>`
            : '';
        return `<div class="business-values">${cards}</div>${extra}`;
    }

    function renderRows(group) {
        const rows = group.items || [];
        byId('previewRows').innerHTML = rows.length ? rows.map(item => {
            const confirmIssues = uniqueIssues(state.confirmErrors.get(item.itemId) || []);
            const canonicalIssues = item.issues?.length ? item.issues : [...(item.errors || []), ...(item.warnings || [])];
            const mergedIssues = uniqueIssues([...canonicalIssues, ...confirmIssues]);
            const errors = mergedIssues.filter(issue => issue.severity !== 'WARNING');
            const warnings = mergedIssues.filter(issue => issue.severity === 'WARNING');
            const issueMarkup = (issue, warning) => {
                const severity = issue.severity || (warning ? 'WARNING' : 'ERROR');
                const label = fieldDefinition(group.entityType, issue.field)
                    ? fieldLabel(group.entityType, issue.field)
                    : (warning ? 'Cảnh báo' : 'Lỗi');
                const fieldControl = issue.field
                    ? `<button type="button" class="issue-field-link edit-row" data-item-id="${item.itemId}" data-focus-field="${escapeHtml(issue.field)}">${escapeHtml(label)}</button>`
                    : `<strong>${escapeHtml(label)}</strong>`;
                const locator = locatorLabel(issue.sourceLocator || issue.position, '');
                const resolution = issue.metadata?.resolution;
                const metadata = [issueSeverityLabels[severity] || null, locator, issueResolutionLabels[resolution] || null]
                    .filter(Boolean).map(escapeHtml).join(' · ');
                return `<div class="issue ${warning ? 'warning' : ''}">${fieldControl}<span>${escapeHtml(issue.message)}</span>${metadata ? `<small>${metadata}</small>` : ''}</div>`;
            };
            const issues = [
                ...errors.map(x => issueMarkup(x, false)),
                ...warnings.map(x => issueMarkup(x, true))
            ].join('') || '<span class="no-issue">Không có lỗi</span>';
            const trace = locatorLabel(item.sourceLocator, Object.values(item.sourceTrace || {}).filter(Boolean).join(', ') || group.sourceLabel || group.sheetName);
            const confidenceParts = [
                item.sourceConfidence == null ? null : `Nguồn ${(item.sourceConfidence * 100).toFixed(0)}%`,
                item.layoutConfidence == null ? null : `Bố cục ${(item.layoutConfidence * 100).toFixed(0)}%`,
                item.ocrConfidence == null ? null : `OCR ${(item.ocrConfidence * 100).toFixed(0)}%`,
                item.aiConfidence == null ? null : `AI ${(item.aiConfidence * 100).toFixed(0)}%`
            ].filter(Boolean);
            const confidence = confidenceParts.length ? `<small class="source-confidence">${escapeHtml(confidenceParts.join(' · '))}</small>` : '';
            const evidence = item.evidenceSnippet ? `<small class="source-evidence" title="${escapeHtml(item.evidenceSnippet)}">${escapeHtml(item.evidenceSnippet)}</small>` : '';
            const effectiveStatus = confirmIssues.length ? 'ERROR' : item.status;
            return `<tr class="preview-row status-${effectiveStatus.toLowerCase()}" data-item-row="${item.itemId}"><td><strong class="source-row-number">${escapeHtml(trace)}</strong>${confidence}${evidence}</td><td><span class="row-status status-${effectiveStatus.toLowerCase()}">${escapeHtml(statusLabels[effectiveStatus] || 'Chưa xác định')}</span><small class="action-label">${escapeHtml(actionLabels[item.action] || 'Chưa xác định')}</small></td><td>${renderBusinessValues(group, item)}</td><td class="issues-cell">${issues}</td><td><button type="button" class="btn-ai small edit-row" data-item-id="${item.itemId}">Sửa dòng</button></td></tr>`;
        }).join('') : '<tr><td colspan="5" class="empty-preview">Không có dòng phù hợp với bộ lọc.</td></tr>';
    }

    async function loadSession(id, keepGroup = true, generation = state.sessionGeneration) {
        const groupId = keepGroup ? state.activeGroupId : null;
        const params = new URLSearchParams({ page: state.page, pageSize: state.pageSize });
        if (groupId) params.set('groupId', groupId);
        if (byId('statusFilter').value) params.set('status', byId('statusFilter').value);
        const session = await api(`/api/ai-import/${id}?${params}`);
        if (generation !== state.sessionGeneration) return false;
        state.session = session;
        render();
        return true;
    }

    async function switchSession(id) {
        const previous = {
            session: state.session,
            activeGroupId: state.activeGroupId,
            page: state.page,
            status: byId('statusFilter').value,
            source: byId('sourceFilter').value,
            entity: byId('entityFilter').value
        };
        const generation = ++state.sessionGeneration;
        closeAllDialogs();
        state.activeGroupId = null;
        state.page = 1;
        resetSessionFilters();
        clearMutationState();
        try {
            return await loadSession(id, false, generation);
        } catch (error) {
            if (generation !== state.sessionGeneration) return false;
            state.session = previous.session;
            state.activeGroupId = previous.activeGroupId;
            state.page = previous.page;
            byId('statusFilter').value = previous.status;
            byId('sourceFilter').value = previous.source;
            byId('entityFilter').value = previous.entity;
            if (state.session) render();
            throw error;
        }
    }

    async function analyze() {
        const files = Array.from(byId('excelFile').files || []);
        if (!files.length) return;
        return runMutation('analyze', async () => {
            const form = new FormData();
            files.forEach(file => form.append('Files', file));
            if (byId('entityHint').value) form.append('EntityHint', byId('entityHint').value);
            form.append('UseOcr', byId('useOcr').checked ? 'true' : 'false');
            byId('uploadErrors').hidden = true;
            byId('uploadErrors').innerHTML = '';
            busy(true);
            try {
                state.session = await api('/api/ai-import/analyze', { method: 'POST', body: form });
                state.sessionGeneration++;
                state.activeGroupId = state.session.groups[0]?.groupId;
                state.page = 1;
                clearMutationState();
                render();
                await showAlert('Đã phân tích xong. Hãy kiểm tra bản xem trước trước khi xác nhận nhập.', 'success', 'Phân tích thành công');
            } catch (error) {
                const details = uniqueIssues(error.details || []);
                const uploadErrors = byId('uploadErrors');
                uploadErrors.hidden = false;
                uploadErrors.innerHTML = details.length
                    ? details.map(detail => `<div><strong>${escapeHtml(detail.metadata?.fileName || 'Tệp đã chọn')}</strong><code>${escapeHtml(detail.code || error.code || '')}</code><span>${escapeHtml(detail.message || errorMessage(error))}</span></div>`).join('')
                    : `<div><strong>Tệp đã chọn</strong><code>${escapeHtml(error.code || '')}</code><span>${escapeHtml(errorMessage(error))}</span></div>`;
                uploadErrors.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
            } finally { busy(false); }
        });
    }

    async function saveMapping() {
        const group = state.session.groups.find(x => x.groupId === state.activeGroupId);
        if (!group) return;
        return runMutation('save-mapping', async () => {
            const fields = entityFields[byId('groupEntity').value] || [];
            const mapping = Object.fromEntries(fields.map(field => [field, null]));
            const ignoredSourceColumns = [];
            document.querySelectorAll('[data-source-column]').forEach(select => {
                if (select.value) mapping[select.value] = select.dataset.sourceColumn;
                else ignoredSourceColumns.push(select.dataset.sourceColumn);
            });
            const guard = sessionGuard();
            busy(true);
            try {
                const session = await api(`/api/ai-import/${guard.id}/groups/${group.groupId}`, { method: 'PATCH', body: JSON.stringify({ expectedPreviewVersion: state.session.previewVersion, entityType: byId('groupEntity').value, mapping, ignoredSourceColumns }) });
                if (!guardIsCurrent(guard)) return;
                state.session = session;
                clearMutationState();
                render();
                await showAlert('Đã lưu ánh xạ cột và kiểm tra lại toàn bộ vùng.', 'success');
            } catch (error) { await handleMutationError(error); }
            finally { busy(false); }
        });
    }

    async function remapFromEditor(targetField, sourceKey) {
        const group = state.session?.groups.find(x => x.groupId === state.activeGroupId);
        const itemId = state.editingItem?.itemId;
        if (!group || !targetField || !sourceKey) return;
        return runMutation(`remap:${group.groupId}`, async () => {
            const guard = sessionGuard();
            const mapping = Object.fromEntries(Object.entries(group.mapping || {})
                .map(([field, mappedSource]) => [field, mappedSource === sourceKey && field !== targetField ? null : mappedSource]));
            mapping[targetField] = sourceKey;
            const ignoredSourceColumns = (group.sourceColumns || [])
                .filter(column => column.classification === 'IGNORED' && column.key !== sourceKey)
                .map(column => column.key);
            busy(true);
            try {
                const session = await api(`/api/ai-import/${guard.id}/groups/${group.groupId}`, {
                    method: 'PATCH',
                    body: JSON.stringify({
                        expectedPreviewVersion: state.session.previewVersion,
                        entityType: group.entityType,
                        mapping,
                        ignoredSourceColumns
                    })
                });
                if (!guardIsCurrent(guard)) return;
                state.session = session;
                clearMutationState();
                render();
                const updatedGroup = state.session.groups.find(x => x.groupId === group.groupId);
                const updatedItem = updatedGroup?.items.find(x => x.itemId === itemId);
                if (updatedItem) renderEditor(updatedGroup, updatedItem, await ensureEditorOptions());
                await showAlert(`Đã chọn ${sourceKey} làm nguồn cho ${fieldLabel(group.entityType, targetField)} trên toàn bộ vùng dữ liệu.`, 'success');
            } catch (error) { await handleMutationError(error); }
            finally { busy(false); }
        });
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
        const hint = field.type === 'icon' ? 'Chỉ nhập 1 biểu tượng Unicode.' : (field.max ? `Tối đa ${field.max} ký tự.` : '');
        return `<label class="editor-field ${field.wide ? 'is-wide' : ''}"><span>${escapeHtml(field.label)} ${required}</span>${control}<small class="field-hint">${hint}</small><small class="field-error" data-field-error="${field.name}">${escapeHtml(initialError)}</small></label>`;
    }

    function applyServerFieldErrors(entity, serverErrors) {
        for (const issue of serverErrors.filter(issue => fieldDefinition(entity, issue.field))) {
            const input = byId('editFields').querySelector(`[data-edit-field="${issue.field}"]`);
            if (!input) continue;
            input.dataset.serverError = issue.message || 'Giá trị chưa hợp lệ.';
            input.classList.add('is-invalid');
        }
    }

    function renderEditFormAlert(group, item, serverErrors) {
        const alert = byId('editFormAlert');
        const issues = [...serverErrors];
        if (item.status === 'REVIEW_REQUIRED' && issues.length === 0 && !(item.warnings || []).length) {
            issues.push({
                code: 'REVIEW_REQUIRED',
                message: 'Dữ liệu trích xuất có độ tin cậy thấp. Hãy kiểm tra các trường rồi bấm “Lưu và kiểm tra lại” để xác nhận.'
            });
        }
        alert.hidden = issues.length === 0;
        if (issues.length === 0) {
            alert.replaceChildren();
            return;
        }
        alert.innerHTML = `<strong>Dòng này cần được xử lý trước khi nhập.</strong><ul>${issues.map(issue => {
            const definition = fieldDefinition(group.entityType, issue.field);
            const label = definition?.label || issue.code || 'Lỗi';
            const link = definition ? ` data-editor-error-field="${escapeHtml(issue.field)}" href="#"` : '';
            return `<li><a${link}>${escapeHtml(label)}</a>: ${escapeHtml(issue.message)}</li>`;
        }).join('')}</ul>`;
        alert.querySelectorAll('[data-editor-error-field]').forEach(link => link.addEventListener('click', event => {
            event.preventDefault();
            const input = byId('editFields').querySelector(`[data-edit-field="${link.dataset.editorErrorField}"]`);
            input?.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
            input?.focus({ preventScroll: true });
        }));
    }

    function resetEditorViewport() {
        const editDialogBody = byId('editDialogBody');
        editDialogBody.scrollTop = 0;
        const firstServerError = byId('editFields').querySelector('[data-server-error]');
        firstServerError?.scrollIntoView({ block: 'nearest' });
    }

    function categoryIconError(value) {
        const icon = String(value || '').trim();
        if (!icon) return '';
        if (/[<>&]/u.test(icon)) return 'Biểu tượng không được chứa HTML.';
        const segments = typeof Intl.Segmenter === 'function' ? Array.from(new Intl.Segmenter('vi', { granularity: 'grapheme' }).segment(icon), x => x.segment) : Array.from(icon);
        if (segments.length !== 1) return 'Chỉ được chọn một biểu tượng Unicode.';
        let hasSymbol = false;
        for (const character of icon) {
            if (/\p{S}/u.test(character)) hasSymbol = true;
            else if (!/[\p{M}\u200D]/u.test(character)) return 'Biểu tượng phải là ký hiệu Unicode, không phải chữ hoặc số.';
        }
        if (!hasSymbol) return 'Biểu tượng phải là một ký hiệu Unicode hợp lệ.';
        return icon.length > 10 ? 'Biểu tượng tối đa 10 ký tự.' : '';
    }

    function initializeIconEditor() {
        const root = byId('editFields').querySelector('[data-icon-editor]');
        if (!root) return;
        const input = root.querySelector('[data-icon-input]');
        const preview = root.querySelector('[data-icon-preview]');
        const options = root.querySelector('[data-icon-options]');
        const errorBox = byId('editFields').querySelector('[data-field-error="Icon"]');
        let lastValidIcon = categoryIconError(input.value) ? '' : input.value.trim();
        root.querySelector('[data-icon-toggle]').addEventListener('click', () => { options.hidden = !options.hidden; });
        root.querySelector('[data-icon-clear]').addEventListener('click', () => { input.value = ''; input.dispatchEvent(new Event('input', { bubbles: true })); });
        root.addEventListener('click', event => {
            const button = event.target.closest('[data-icon]');
            if (!button) return;
            input.value = button.dataset.icon || '';
            input.dispatchEvent(new Event('input', { bubbles: true }));
            options.hidden = true;
        });
        input.addEventListener('input', () => {
            const nextIcon = input.value.trim();
            const error = categoryIconError(nextIcon);
            if (error) {
                input.value = lastValidIcon;
                input.dataset.iconInputError = error;
                input.setCustomValidity(error);
                input.classList.add('is-invalid');
                if (errorBox) errorBox.textContent = error;
            } else {
                lastValidIcon = nextIcon;
                delete input.dataset.iconInputError;
            }
            preview.textContent = input.value.trim() || '—';
        });
    }

    function renderEditor(group, item, options) {
        const schema = editorSchemas[group.entityType];
        if (!schema) return;
        state.editingItem = item;
        byId('editEntityName').textContent = entityLabels[group.entityType] || 'Chưa xác định';
        byId('editRowNumber').textContent = item.sourceRow;
        const serverErrors = uniqueIssues([...((item.issues || item.errors || []).filter(issue => issue.severity !== 'WARNING')), ...(state.confirmErrors.get(item.itemId) || [])]);
        byId('editFields').innerHTML = schema.sections.map(section => `<fieldset class="editor-section"><legend>${escapeHtml(section.title)}</legend>${section.note ? `<p>${escapeHtml(section.note)}</p>` : ''}<div class="editor-grid">${section.fields.map(field => editorFieldHtml(group.entityType, field, item.normalizedData?.[field.name], options, serverErrors)).join('')}</div></fieldset>`).join('');
        applyServerFieldErrors(group.entityType, serverErrors);
        const mappedHeaders = new Set(Object.values(group.mapping || {}).filter(Boolean));
        const supplemental = Object.entries(item.rawData || {}).filter(([key]) => !mappedHeaders.has(key));
        const evidenceRows = Object.entries(item.fieldEvidence || {}).map(([field, evidence]) => `<div><b>${escapeHtml(fieldLabel(group.entityType, field))} · ${escapeHtml(sourceKindLabels[evidence.sourceKind] || 'Nguồn')}</b><span>${escapeHtml(locatorLabel(evidence.sourceLocator, ''))}${evidence.ocrConfidence == null ? '' : ` · OCR ${(evidence.ocrConfidence * 100).toFixed(0)}%`}${evidence.aiConfidence == null ? '' : ` · AI ${(evidence.aiConfidence * 100).toFixed(0)}%`}<br>${escapeHtml(evidence.rawText || '')}</span></div>`);
        const mappingConflicts = (group.issues || []).filter(issue => issue.code === 'XUNG_ĐỘT_ÁNH_XẠ');
        const choices = new Map();
        for (const issue of mappingConflicts) {
            const targetField = issue.metadata?.targetField || issue.field;
            for (const sourceKey of issue.metadata?.candidateSourceKeys || [])
                choices.set(sourceKey, targetField);
        }
        const columnMap = new Map((group.sourceColumns || []).map(column => [column.key, column]));
        const supplementalRows = supplemental.map(([key, value]) => {
            const targetField = choices.get(key);
            const column = columnMap.get(key);
            const action = targetField
                ? `<button type="button" class="btn-ai small source-mapping-choice" data-source-key="${escapeHtml(key)}" data-target-field="${escapeHtml(targetField)}">Chọn làm nguồn cho “${escapeHtml(fieldLabel(group.entityType, targetField))}”</button><small>Lựa chọn này áp dụng cho toàn bộ vùng dữ liệu hiện tại.</small>`
                : '';
            return `<div><b>${escapeHtml(column?.label || key)}${key !== column?.label ? ` · ${escapeHtml(key)}` : ''}</b><span>${escapeHtml(displayValue(value))}${action}</span></div>`;
        });
        byId('editSourceData').innerHTML = [...evidenceRows, ...supplementalRows].join('') || '<p>Không có dữ liệu ngoài ánh xạ.</p>';
        const hasWarnings = (item.warnings || []).length > 0;
        const supplierReview = group.entityType === 'Supplier' ? item.supplierDuplicateReview : null;
        const needsOverride = supplierReview?.requiresReason === true;
        byId('warningSection').hidden = !hasWarnings;
        const regularWarnings = uniqueIssues(item.warnings || []).filter(issue => issue.code !== 'NHÀ_CUNG_CẤP_TƯƠNG_TỰ');
        const supplierMatches = (supplierReview?.matches || []).map(match => `<div class="edit-warning-message supplier-match"><strong>${escapeHtml(`${match.code || 'Nhà cung cấp'} · ${match.name || ''}`)}</strong><span>Các tín hiệu trùng: ${escapeHtml((match.matchedSignals || []).join(', '))}</span></div>`).join('');
        byId('editWarningMessages').innerHTML = `${regularWarnings.map(issue => `<div class="edit-warning-message"><strong>${escapeHtml(issue.field ? fieldLabel(group.entityType, issue.field) : 'Cảnh báo')}</strong><span>${escapeHtml(issue.message)}</span></div>`).join('')}${supplierMatches}`;
        byId('acknowledgeWarnings').checked = item.warningsAcknowledged;
        const reviewableIssues = (item.issues || []).filter(issue => issue.metadata?.resolution === 'MANUAL_REVIEW');
        byId('manualReviewRow').hidden = reviewableIssues.length === 0;
        byId('manualReviewConfirmed').checked = item.manualReviewConfirmed === true;
        byId('overrideReason').value = item.duplicateOverrideReason || '';
        byId('overrideReasonGroup').hidden = !needsOverride;
        renderEditFormAlert(group, item, serverErrors);
        initializeIconEditor();
        byId('editFields').querySelectorAll('[data-uppercase-code]').forEach(input => input.addEventListener('input', () => {
            const cursor = input.selectionStart;
            input.value = input.value.toUpperCase();
            input.setSelectionRange(cursor, cursor);
        }));
        byId('editItemForm').querySelectorAll('input,select,textarea').forEach(input => input.addEventListener('input', () => {
            delete input.dataset.serverError;
            validateEditor(false);
        }));
        validateEditor(false);
        syncBusyUi();
        requestAnimationFrame(resetEditorViewport);
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
            else if (field.type === 'icon') error = input.dataset.iconInputError || categoryIconError(value);
            if (!error && input.dataset.serverError) error = input.dataset.serverError;
            input.setCustomValidity(error);
            const errorBox = byId('editFields').querySelector(`[data-field-error="${field.name}"]`);
            if (errorBox) errorBox.textContent = error;
            input.classList.toggle('is-invalid', !!error);
            valid = valid && !error;
        }
        const needsAcknowledgement = !byId('warningSection').hidden;
        if (needsAcknowledgement && !byId('acknowledgeWarnings').checked) valid = false;
        const needsManualReview = !byId('manualReviewRow').hidden;
        if (needsManualReview && !byId('manualReviewConfirmed').checked) valid = false;
        const override = byId('overrideReason');
        const overrideError = !byId('overrideReasonGroup').hidden && !override.value.trim() ? 'Vui lòng nhập lý do vẫn tạo nhà cung cấp.' : '';
        override.setCustomValidity(overrideError);
        byId('editItemForm').querySelector('[data-field-error="DuplicateOverrideReason"]').textContent = overrideError;
        valid = valid && !overrideError;
        byId('saveItemButton').disabled = !valid;
        if (showMessages && !valid) {
            const alert = byId('editFormAlert');
            if (alert.hidden) {
                alert.hidden = false;
                alert.textContent = 'Vui lòng hoàn tất các trường bắt buộc và xử lý cảnh báo bên dưới.';
            }
            const target = byId('editItemForm').querySelector(':invalid')
                || (needsManualReview && !byId('manualReviewConfirmed').checked ? byId('manualReviewRow') : null)
                || (!byId('warningSection').hidden ? byId('warningSection') : null);
            target?.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
            if (target?.matches('input,select,textarea')) target.focus({ preventScroll: true });
        }
        return valid;
    }

    async function openEditor(itemId, focusField) {
        const group = state.session.groups.find(x => x.groupId === state.activeGroupId);
        const item = group?.items.find(x => x.itemId === itemId);
        if (!item) return;
        const guard = sessionGuard();
        busy(true);
        try {
            const options = await ensureEditorOptions();
            if (!guardIsCurrent(guard) || state.session.status !== 'READY_TO_PREVIEW') return;
            renderEditor(group, item, options);
            byId('editDialog').showModal();
            requestAnimationFrame(() => {
                resetEditorViewport();
                const input = focusField ? byId('editFields').querySelector(`[data-edit-field="${CSS.escape(focusField)}"]`) : null;
                input?.scrollIntoView({ block: 'nearest' });
                input?.focus({ preventScroll: true });
            });
        } catch (error) { await showAlert(error.message, 'error'); }
        finally { busy(false); }
    }

    async function saveItem(action) {
        const item = state.editingItem;
        if (!item) return;
        if (action === 'CREATE' && !validateEditor(true)) return;
        return runMutation(`item:${item.itemId}`, async () => {
            const values = {};
            document.querySelectorAll('[data-edit-field]').forEach(input => values[input.dataset.editField] = input.value);
            const guard = sessionGuard();
            busy(true);
            try {
                const session = await api(`/api/ai-import/${guard.id}/items/${item.itemId}`, { method: 'PATCH', body: JSON.stringify({ expectedPreviewVersion: state.session.previewVersion, action, values, warningsAcknowledged: byId('acknowledgeWarnings').checked, manualReviewConfirmed: action === 'CREATE' && byId('manualReviewConfirmed').checked, duplicateOverrideReason: byId('overrideReason').value }) });
                if (!guardIsCurrent(guard)) return;
                state.session = session;
                clearMutationState();
                const group = state.session.groups.find(x => x.groupId === state.activeGroupId);
                const updatedItem = group?.items.find(x => x.itemId === item.itemId);
                render();
                if (action === 'CREATE' && updatedItem && ['ERROR', 'REVIEW_REQUIRED'].includes(updatedItem.status)) {
                    const options = await ensureEditorOptions();
                    renderEditor(group, updatedItem, options);
                } else {
                    closeEditDialog();
                    await showAlert(action === 'SKIP' ? 'Đã bỏ qua dòng.' : 'Đã lưu và kiểm tra lại dòng.', 'success');
                }
            } catch (error) { await handleMutationError(error); }
            finally { busy(false); }
        });
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
        const position = first.position || {};
        const targetGroup = state.session.groups.find(group => group.sheetName === position.sheet
            || (position.page && group.sourceLocator?.page === position.page)
            || (position.table && group.sourceLocator?.table === position.table)
            || (position.paragraph && group.sourceLocator?.paragraph === position.paragraph));
        if (targetGroup) state.activeGroupId = targetGroup.groupId;
        state.page = 1;
        byId('statusFilter').value = '';
        byId('sourceFilter').value = '';
        byId('entityFilter').value = '';
        await loadSession(state.session.sessionId);
        requestAnimationFrame(() => document.querySelector(`[data-item-row="${first.itemId}"]`)?.scrollIntoView({ behavior: 'smooth', block: 'center' }));
    }

    async function confirmSession() {
        return runMutation('confirm-session', async () => {
            if (!await confirmAction('Xác nhận nhập', 'Hệ thống sẽ tạo toàn bộ dữ liệu trong một giao dịch. Bạn muốn tiếp tục?', 'Xác nhận nhập')) return;
            state.confirmKey ||= (crypto.randomUUID ? crypto.randomUUID() : `${Date.now()}-${Math.random()}`);
            busy(true);
            try {
                const result = await api(`/api/ai-import/${state.session.sessionId}/confirm`, { method: 'POST', headers: { 'Idempotency-Key': state.confirmKey }, body: JSON.stringify({ expectedPreviewVersion: state.session.previewVersion }) });
                await showAlert(`Hoàn tất: đã nhập ${result.imported}, bỏ qua ${result.skipped}.`, 'success', 'Nhập dữ liệu thành công');
                clearMutationState();
                closeImportWorkspace(result);
            } catch (error) {
                captureConfirmErrors(error.details);
                await showAlert(errorMessage(error), 'error');
                if (['PREVIEW_ĐÃ_THAY_ĐỔI', 'PREVIEW_CHƯA_SẴN_SÀNG'].includes(error.code) || error.details?.length) await focusConfirmError(error.details);
            } finally { busy(false); }
        });
    }

    async function handleMutationError(error) {
        await showAlert(errorMessage(error), 'error');
        if (error.code === 'PREVIEW_ĐÃ_THAY_ĐỔI') {
            closeEditDialog();
            await loadSession(state.session.sessionId);
        }
    }

    async function history() {
        try {
            const data = await api('/api/ai-import/history?page=1&pageSize=30');
            byId('historyRows').innerHTML = data.items.map(x => `<button type="button" class="history-row group-tab" data-history-id="${x.sessionId}"><strong>#${x.sessionId} · [${escapeHtml(formatLabels[x.sourceFormat] || 'Tệp dữ liệu')}] ${escapeHtml(x.fileName)}</strong><span class="row-status">${escapeHtml(statusLabels[x.status] || 'Chưa xác định')}</span><small>${new Date(x.createdAtUtc).toLocaleString('vi-VN')} · ${x.importedRows}/${x.totalRows} đã nhập</small></button>`).join('') || '<p>Chưa có phiên.</p>';
            syncBusyUi();
            byId('historyPanel').hidden = false;
        } catch (error) { await showAlert(error.message, 'error'); }
    }

    const fileInput = byId('excelFile');
    byId('chooseFileButton').addEventListener('click', () => fileInput.click());
    fileInput.addEventListener('change', () => {
        const files = Array.from(fileInput.files || []);
        byId('uploadErrors').hidden = true;
        byId('uploadErrors').innerHTML = '';
        byId('selectedFileName').innerHTML = files.map(file => `<span>${escapeHtml(file.name)} · ${(file.size / 1024 / 1024).toFixed(2)} MiB</span>`).join('');
        syncBusyUi();
    });
    const drop = byId('dropZone');
    ['dragenter', 'dragover'].forEach(name => drop.addEventListener(name, event => { event.preventDefault(); drop.classList.add('dragging'); }));
    ['dragleave', 'drop'].forEach(name => drop.addEventListener(name, event => { event.preventDefault(); drop.classList.remove('dragging'); }));
    drop.addEventListener('drop', event => { if (event.dataTransfer.files.length) { const transfer = new DataTransfer(); Array.from(event.dataTransfer.files).forEach(file => transfer.items.add(file)); fileInput.files = transfer.files; fileInput.dispatchEvent(new Event('change')); } });
    byId('analyzeButton').addEventListener('click', analyze);
    byId('saveMappingButton').addEventListener('click', saveMapping);
    byId('confirmButton').addEventListener('click', confirmSession);
    byId('closeSessionButton').addEventListener('click', closeSessionView);
    byId('reanalyzeButton').addEventListener('click', () => runMutation('reanalyze-session', async () => {
        busy(true);
        try {
            state.session = await api(`/api/ai-import/${state.session.sessionId}/reanalyze`, { method: 'POST', body: JSON.stringify({ expectedPreviewVersion: state.session.previewVersion }) });
            clearMutationState();
            render();
            await showAlert('Đã phân tích lại và cập nhật bản xem trước.', 'success', 'Phân tích thành công');
        } catch (error) { await handleMutationError(error); }
        finally { busy(false); }
    }));
    byId('cancelButton').addEventListener('click', () => runMutation('cancel-session', async () => {
        if (!await confirmAction('Hủy phiên', 'Dữ liệu bản xem trước của phiên này sẽ không thể tiếp tục nhập.', 'Hủy phiên')) return;
        busy(true);
        try {
            state.session = await api(`/api/ai-import/${state.session.sessionId}/cancel`, { method: 'POST', body: JSON.stringify({ expectedPreviewVersion: state.session.previewVersion }) });
            state.sessionGeneration++;
            closeAllDialogs();
            state.editingItem = null;
            clearMutationState();
            render();
            await showAlert('Đã hủy phiên nhập dữ liệu.', 'success');
        } catch (error) { await handleMutationError(error); }
        finally { busy(false); }
    }));
    byId('groupEntity').addEventListener('change', () => { const group = state.session.groups.find(x => x.groupId === state.activeGroupId); group.entityType = byId('groupEntity').value; renderMapping(group); });
    byId('mappingFields').addEventListener('change', event => {
        if (!event.target.matches('[data-source-column]')) return;
        syncMappingTargetAvailability();
    });
    byId('groupTabs').addEventListener('click', async event => { const button = event.target.closest('[data-group-id]'); if (!button) return; state.activeGroupId = Number(button.dataset.groupId); state.page = 1; await loadSession(state.session.sessionId); });
    byId('previewRows').addEventListener('click', event => { const button = event.target.closest('.edit-row'); if (button) openEditor(Number(button.dataset.itemId), button.dataset.focusField); });
    byId('sourceDocuments').addEventListener('click', async event => {
        const button = event.target.closest('.remove-source');
        if (!button || !state.session) return;
        return runMutation(`remove-source:${button.dataset.sourceId}`, async () => {
            if (!await confirmAction('Loại tài liệu nguồn', 'Toàn bộ vùng và bản ghi dự kiến thuộc tài liệu này sẽ bị loại khỏi phiên.', 'Loại nguồn')) return;
            const guard = sessionGuard();
            busy(true);
            try {
                const session = await api(`/api/ai-import/${guard.id}/sources/${button.dataset.sourceId}?expectedPreviewVersion=${state.session.previewVersion}`, { method: 'DELETE' });
                if (!guardIsCurrent(guard)) return;
                state.session = session;
                state.activeGroupId = session.groups[0]?.groupId;
                clearMutationState();
                render();
            } catch (error) { await handleMutationError(error); }
            finally { busy(false); }
        });
    });
    byId('saveItemButton').addEventListener('click', () => saveItem('CREATE'));
    byId('skipItemButton').addEventListener('click', () => saveItem('SKIP'));
    byId('acknowledgeWarnings').addEventListener('change', () => validateEditor(false));
    byId('manualReviewConfirmed').addEventListener('change', () => validateEditor(false));
    byId('overrideReason').addEventListener('input', () => validateEditor(false));
    byId('editSourceData').addEventListener('click', event => {
        const button = event.target.closest('[data-source-key][data-target-field]');
        if (button) remapFromEditor(button.dataset.targetField, button.dataset.sourceKey);
    });
    byId('editDialog').addEventListener('close', () => { state.editingItem = null; });
    byId('statusFilter').addEventListener('change', async () => { if (!state.session) return; state.page = 1; await loadSession(state.session.sessionId); });
    byId('sourceFilter').addEventListener('change', async () => {
        if (!state.session) return;
        const sourceId = byId('sourceFilter').value;
        const entityType = byId('entityFilter').value;
        state.activeGroupId = state.session.groups.find(group =>
            (!sourceId || String(group.sourceDocumentId) === sourceId)
            && (!entityType || group.entityType === entityType))?.groupId;
        state.page = 1;
        await loadSession(state.session.sessionId);
    });
    byId('entityFilter').addEventListener('change', async () => {
        if (!state.session) return;
        const sourceId = byId('sourceFilter').value;
        const entityType = byId('entityFilter').value;
        state.activeGroupId = state.session.groups.find(group =>
            (!sourceId || String(group.sourceDocumentId) === sourceId)
            && (!entityType || group.entityType === entityType))?.groupId;
        state.page = 1;
        await loadSession(state.session.sessionId);
    });
    byId('previousPage').addEventListener('click', async () => { state.page--; await loadSession(state.session.sessionId); });
    byId('nextPage').addEventListener('click', async () => { state.page++; await loadSession(state.session.sessionId); });
    byId('refreshHistoryButton').addEventListener('click', history);
    byId('closeHistoryButton').addEventListener('click', () => byId('historyPanel').hidden = true);
    byId('historyRows').addEventListener('click', async event => {
        const row = event.target.closest('[data-history-id]');
        if (!row) return;
        busy(true);
        try {
            if (await switchSession(Number(row.dataset.historyId))) byId('historyPanel').hidden = true;
        } catch (error) {
            await showAlert(errorMessage(error), 'error');
        } finally {
            busy(false);
        }
    });

    let ocrCapabilityRequestVersion = 0;
    async function refreshOcrCapability() {
        const requestVersion = ++ocrCapabilityRequestVersion;
        const toggle = byId('useOcr');
        try {
            const capability = await api('/api/ai-import/ocr-capability');
            if (requestVersion !== ocrCapabilityRequestVersion) return;
            const ready = capability.effectiveEnabled === true;
            toggle.disabled = !ready;
            if (!ready) toggle.checked = false;
            const status = ready
                ? t('Ocr.Ready', { provider: capability.provider, version: capability.providerVersion || 'Tesseract', languages: capability.languages })
                : capability.healthMessage || t('Ocr.Unavailable');
            toggle.title = status;
            toggle.closest('label')?.setAttribute('title', status);
        } catch (error) {
            if (requestVersion !== ocrCapabilityRequestVersion) return;
            toggle.disabled = true;
            toggle.checked = false;
            const status = errorMessage(error);
            toggle.title = status;
            toggle.closest('label')?.setAttribute('title', status);
        }
    }

    void refreshOcrCapability();
    window.addEventListener('focus', refreshOcrCapability);
})();
