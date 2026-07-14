(function exposeCafeChainAiImagePipeline(global) {
    'use strict';

    const stateLabels = {
        Idle: 'Sẵn sàng.',
        GeneratingSuggestions: 'AI đang tạo gợi ý...',
        SuggestionsReady: 'Hãy chọn một gợi ý.',
        SearchingPexels: 'Đang tìm ảnh tham chiếu phù hợp...',
        ValidatingPexelsImages: 'Đang xếp hạng metadata ảnh Pexels...',
        PexelsReferenceReady: 'Hãy chọn một ảnh Pexels làm tham chiếu.',
        GeneratingWithComfyUI: 'Đang tạo nhiều ảnh bằng ComfyUI...',
        ValidatingGeneratedImages: 'Đang kiểm tra kỹ thuật các ảnh được tạo...',
        Completed: 'Ảnh đã sẵn sàng. Hãy chọn ảnh cuối.',
        Failed: 'Pipeline ảnh chưa hoàn tất.'
    };

    function create(config) {
        const element = id => document.getElementById(id);
        const ui = {
            button: element(config.ids.button),
            form: element(config.ids.form),
            idea: element(config.ids.idea),
            panel: element(config.ids.panel),
            optionList: element(config.ids.optionList),
            referenceList: element(config.ids.referenceList),
            generatedList: element(config.ids.generatedList),
            status: element(config.ids.status),
            warnings: element(config.ids.warnings),
            source: element(config.ids.source),
            usePexels: element(config.ids.usePexels),
            generate: element(config.ids.generate),
            fallback: element(config.ids.fallback),
            retrySearch: element(config.ids.retrySearch),
            apply: element(config.ids.apply),
            dismiss: element(config.ids.dismiss)
        };
        if (!ui.button || !ui.form || !ui.panel) return null;

        let requestId = null;
        let selectedSuggestion = null;
        let selectedReference = null;
        let selectedOutput = null;
        let excludedPhotoIds = [];
        let controller = null;
        let operationVersion = 0;
        const objectUrls = [];
        const token = () => ui.form.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
        const notify = (message, type) => config.notify(message, type || 'success');

        const setState = state => {
            ui.status.textContent = stateLabels[state] || state;
            ui.status.dataset.state = state;
        };

        const abortCurrent = () => {
            operationVersion++;
            controller?.abort();
            controller = null;
        };

        const revokeUrls = () => {
            while (objectUrls.length) URL.revokeObjectURL(objectUrls.pop());
        };

        const resetImages = () => {
            selectedReference = null;
            selectedOutput = null;
            ui.referenceList.replaceChildren();
            ui.generatedList.replaceChildren();
            ui.usePexels.disabled = true;
            ui.generate.disabled = true;
            ui.fallback.classList.add('d-none');
            ui.fallback.disabled = false;
            ui.apply.disabled = true;
            revokeUrls();
        };

        const clear = () => {
            abortCurrent();
            requestId = null;
            selectedSuggestion = null;
            excludedPhotoIds = [];
            resetImages();
            ui.optionList.replaceChildren();
            ui.warnings.textContent = '';
            ui.panel.classList.add('d-none');
            setState('Idle');
        };

        const post = async (url, body, timeoutMs, version) => {
            controller = new AbortController();
            const active = controller;
            const timeout = setTimeout(() => active.abort(), timeoutMs);
            try {
                const response = await fetch(url, {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token() },
                    body: JSON.stringify(body),
                    signal: active.signal
                });
                const result = await response.json().catch(() => ({}));
                if (version !== operationVersion) throw new DOMException('Stale response', 'AbortError');
                if (!response.ok || !result.success) {
                    const error = new Error(result.message || 'Không thể xử lý yêu cầu AI.');
                    error.payload = result;
                    throw error;
                }
                return result;
            } finally {
                clearTimeout(timeout);
                if (controller === active) controller = null;
            }
        };

        const renderSuggestions = result => {
            requestId = result.requestId;
            ui.optionList.replaceChildren();
            (result.options || []).slice(0, 3).forEach(option => {
                const card = document.createElement('button');
                card.type = 'button';
                card.className = 'ai-option-card text-start';
                config.renderSuggestion(card, option);
                card.addEventListener('click', () => selectSuggestion(option, card));
                ui.optionList.appendChild(card);
            });
            ui.source.textContent = result.usedOllama ? 'Ollama + C#' : 'C# fallback';
            ui.warnings.textContent = (result.warnings || []).join(' ');
            ui.panel.classList.remove('d-none');
            setState('SuggestionsReady');
        };

        const selectSuggestion = async (option, card) => {
            abortCurrent();
            resetImages();
            selectedSuggestion = option;
            ui.optionList.querySelectorAll('.ai-option-card').forEach(x => x.classList.remove('is-selected'));
            card.classList.add('is-selected');
            excludedPhotoIds = [];
            await searchReferences(false);
        };

        const searchReferences = async (excludeCurrent) => {
            if (!selectedSuggestion?.visualSpecification) return;
            if (excludeCurrent) {
                const currentIds = Array.from(ui.referenceList.querySelectorAll('[data-photo-id]'))
                    .map(x => Number(x.dataset.photoId)).filter(Number.isFinite);
                excludedPhotoIds = [...new Set([...excludedPhotoIds, ...currentIds])];
            }
            abortCurrent();
            resetImages();
            const version = ++operationVersion;
            setState('SearchingPexels');
            ui.retrySearch.disabled = true;
            try {
                const result = await post(config.urls.references, {
                    requestId,
                    suggestionId: selectedSuggestion.suggestionId,
                    entityType: selectedSuggestion.entityType,
                    visualSpecification: selectedSuggestion.visualSpecification,
                    excludedPhotoIds
                }, 90000, version);
                if (selectedSuggestion.suggestionId !== result.data.suggestionId) return;
                setState('ValidatingPexelsImages');
                renderReferences(result.data.candidates || []);
                ui.warnings.textContent = (result.data.warnings || []).join(' ');
                setState('PexelsReferenceReady');
            } catch (error) {
                if (error.name !== 'AbortError') {
                    setState('Failed');
                    const failure = error.payload?.data;
                    const canFallback = failure?.textFallbackAvailable === true;
                    ui.fallback.classList.toggle('d-none', !canFallback);
                    ui.warnings.textContent = canFallback
                        ? `${error.message} Bạn có thể xác nhận tạo ảnh bằng ComfyUI không dùng Pexels.`
                        : `${error.message} Bạn có thể sửa ý tưởng hoặc tìm lại.`;
                    notify(error.message, 'error');
                }
            } finally {
                if (version === operationVersion) ui.retrySearch.disabled = false;
            }
        };

        const renderReferences = candidates => {
            ui.referenceList.replaceChildren();
            candidates.forEach(candidate => {
                const card = document.createElement('button');
                card.type = 'button';
                card.className = 'ai-image-card text-start';
                card.dataset.photoId = String(candidate.photoId);
                const img = document.createElement('img');
                img.src = candidate.previewUrl;
                img.alt = candidate.alt || 'Ứng viên ảnh Pexels';
                img.loading = 'lazy';
                const meta = document.createElement('span');
                meta.textContent = `Photo by ${candidate.photographer || 'Pexels contributor'} on Pexels · ${(Number(candidate.score) * 100).toFixed(0)}% · ${candidate.width}×${candidate.height}`;
                card.title = [candidate.matchedQuery, ...(candidate.warnings || [])].filter(Boolean).join('\n');
                card.append(img, meta);
                card.addEventListener('click', () => {
                    selectedReference = candidate;
                    selectedOutput = null;
                    ui.referenceList.querySelectorAll('.ai-image-card').forEach(x => x.classList.remove('is-selected'));
                    card.classList.add('is-selected');
                    ui.usePexels.disabled = false;
                    ui.generate.disabled = false;
                    ui.apply.disabled = true;
                    ui.generatedList.replaceChildren();
                });
                ui.referenceList.appendChild(card);
            });
        };

        const generate = async () => {
            if (!selectedSuggestion || !selectedReference) return;
            const version = ++operationVersion;
            selectedOutput = null;
            ui.generatedList.replaceChildren();
            ui.generate.disabled = true;
            ui.apply.disabled = true;
            setState('GeneratingWithComfyUI');
            try {
                const result = await post(config.urls.generate, {
                    requestId,
                    suggestionId: selectedSuggestion.suggestionId,
                    entityType: selectedSuggestion.entityType,
                    photoId: selectedReference.photoId,
                    matchedQuery: selectedReference.matchedQuery,
                    visualSpecification: selectedSuggestion.visualSpecification,
                    fileNamePrefix: config.fileNamePrefix(selectedSuggestion)
                }, 240000, version);
                if (selectedSuggestion.suggestionId !== result.data.suggestionId) return;
                setState('ValidatingGeneratedImages');
                renderOutputs(result.data.generatedImages || []);
                ui.warnings.textContent = (result.data.warnings || []).join(' ');
                setState('Completed');
            } catch (error) {
                if (error.name !== 'AbortError') {
                    setState('Failed');
                    ui.warnings.textContent = `${error.message} Ảnh tham chiếu vẫn được giữ để thử lại.`;
                    notify(error.message, 'error');
                }
            } finally {
                if (version === operationVersion) ui.generate.disabled = !selectedReference;
            }
        };

        const usePexels = async () => {
            if (!selectedSuggestion || !selectedReference) return;
            abortCurrent();
            const version = ++operationVersion;
            selectedOutput = null;
            ui.generatedList.replaceChildren();
            ui.usePexels.disabled = true;
            ui.generate.disabled = true;
            ui.apply.disabled = true;
            setState('ValidatingPexelsImages');
            try {
                const result = await post(config.urls.usePexels, {
                    requestId,
                    suggestionId: selectedSuggestion.suggestionId,
                    entityType: selectedSuggestion.entityType,
                    photoId: selectedReference.photoId,
                    matchedQuery: selectedReference.matchedQuery,
                    visualSpecification: selectedSuggestion.visualSpecification,
                    fileNamePrefix: config.fileNamePrefix(selectedSuggestion)
                }, 90000, version);
                if (selectedSuggestion.suggestionId !== result.data.suggestionId) return;
                renderOutputs(result.data.generatedImages || [], true);
                ui.warnings.textContent = [result.data.generatedImages?.[0]?.attributionText,
                    ...(result.data.warnings || [])].filter(Boolean).join(' · ');
                setState('Completed');
            } catch (error) {
                if (error.name !== 'AbortError') {
                    setState('Failed');
                    ui.warnings.textContent = `${error.message} Ảnh Pexels đã chọn vẫn được giữ để thử lại.`;
                    notify(error.message, 'error');
                }
            } finally {
                if (version === operationVersion) {
                    ui.usePexels.disabled = !selectedReference;
                    ui.generate.disabled = !selectedReference;
                }
            }
        };

        const generateWithoutReference = async () => {
            if (!selectedSuggestion) return;
            if (!window.confirm('Pexels không có ảnh phù hợp. Tạo 3 ảnh mới chỉ từ Visual Specification bằng ComfyUI?')) return;
            abortCurrent();
            const version = ++operationVersion;
            selectedOutput = null;
            ui.generatedList.replaceChildren();
            ui.fallback.disabled = true;
            ui.apply.disabled = true;
            setState('GeneratingWithComfyUI');
            try {
                const result = await post(config.urls.generateWithoutReference, {
                    requestId,
                    suggestionId: selectedSuggestion.suggestionId,
                    entityType: selectedSuggestion.entityType,
                    visualSpecification: selectedSuggestion.visualSpecification,
                    fileNamePrefix: config.fileNamePrefix(selectedSuggestion)
                }, 240000, version);
                if (selectedSuggestion.suggestionId !== result.data.suggestionId) return;
                setState('ValidatingGeneratedImages');
                renderOutputs(result.data.generatedImages || []);
                ui.warnings.textContent = (result.data.warnings || []).join(' ');
                setState('Completed');
            } catch (error) {
                if (error.name !== 'AbortError') {
                    setState('Failed');
                    ui.warnings.textContent = `${error.message} Gợi ý hiện tại vẫn được giữ để thử lại.`;
                    notify(error.message, 'error');
                }
            } finally {
                if (version === operationVersion) ui.fallback.disabled = false;
            }
        };

        const renderOutputs = (outputs, autoSelect) => {
            revokeUrls();
            ui.generatedList.replaceChildren();
            outputs.forEach(output => {
                const file = base64File(output, config.defaultFileName);
                const url = URL.createObjectURL(file);
                objectUrls.push(url);
                const card = document.createElement('button');
                card.type = 'button';
                card.className = 'ai-image-card';
                const img = document.createElement('img');
                img.src = url;
                img.alt = `${output.source || 'ComfyUI'} - cần người dùng xác nhận`;
                const meta = document.createElement('span');
                meta.textContent = `${output.source || 'ComfyUI'} · ${output.width}×${output.height} · cần xác nhận nội dung`;
                card.append(img, meta);
                card.addEventListener('click', () => {
                    selectedOutput = { output, file };
                    ui.generatedList.querySelectorAll('.ai-image-card').forEach(x => x.classList.remove('is-selected'));
                    card.classList.add('is-selected');
                    ui.apply.disabled = !selectedSuggestion?.canApply;
                });
                ui.generatedList.appendChild(card);
                if (autoSelect && !selectedOutput) {
                    selectedOutput = { output, file };
                    card.classList.add('is-selected');
                    ui.apply.disabled = !selectedSuggestion?.canApply;
                }
            });
        };

        const base64File = (data, fallbackName) => {
            const bytes = atob(data.base64Data || '');
            const array = new Uint8Array(bytes.length);
            for (let index = 0; index < bytes.length; index++) array[index] = bytes.charCodeAt(index);
            return new File([array], data.fileName || fallbackName, { type: data.contentType || 'image/png' });
        };

        ui.button.addEventListener('click', async () => {
            clear();
            ui.panel.classList.remove('d-none');
            const version = ++operationVersion;
            const original = ui.button.innerHTML;
            ui.button.disabled = true;
            ui.button.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i>Đang gợi ý...';
            setState('GeneratingSuggestions');
            try {
                const result = await post(config.urls.suggestions, config.suggestionPayload(), 130000, version);
                renderSuggestions(result);
            } catch (error) {
                if (error.name !== 'AbortError') {
                    setState('Failed');
                    notify(error.message, 'error');
                }
            } finally {
                if (version === operationVersion) {
                    ui.button.disabled = false;
                    ui.button.innerHTML = original;
                }
            }
        });
        ui.usePexels.addEventListener('click', usePexels);
        ui.generate.addEventListener('click', generate);
        ui.fallback.addEventListener('click', generateWithoutReference);
        ui.retrySearch.addEventListener('click', () => searchReferences(true));
        ui.dismiss.addEventListener('click', clear);
        ui.apply.addEventListener('click', async () => {
            if (!selectedSuggestion?.canApply || !selectedOutput)
                return notify('Vui lòng chọn gợi ý và xác nhận ảnh cuối.', 'error');
            if (config.willOverwrite() && !window.confirm('Dữ liệu hoặc ảnh hiện tại sẽ được thay thế. Tiếp tục áp dụng?')) return;
            const applied = await config.apply(selectedSuggestion, selectedOutput.file);
            if (applied !== false) {
                clear();
                notify('Đã áp dụng vào form. Vui lòng kiểm tra trước khi lưu.');
            }
        });
        config.invalidateElements().forEach(input => input?.addEventListener(
            input.tagName === 'SELECT' ? 'change' : 'input', clear));
        setState('Idle');
        return { clear };
    }

    global.CafeChainAIImagePipeline = { create };
})(window);
