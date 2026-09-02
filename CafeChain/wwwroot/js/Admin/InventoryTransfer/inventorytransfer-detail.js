(() => {
    const catalog = window.CafeChainUiCatalog.read("inventoryTransferUiCatalog");
    const t = (key, values) => window.CafeChainUiCatalog.text(catalog, key, values);
    const panel = document.getElementById('transfer-resolution-panel');
    if (!panel) return;

    const actionSelect = document.getElementById('transfer-resolution-action');
    const submit = document.getElementById('transfer-resolution-submit');
    const reason = document.getElementById('transfer-resolution-reason');
    const message = document.getElementById('transfer-resolution-message');
    const lines = Array.from(panel.querySelectorAll('.transfer-resolution-line'));
    const token = panel.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

    const requestKey = () => self.crypto?.randomUUID?.() || `${Date.now()}-${Math.random().toString(16).slice(2)}`;
    const number = (input) => Math.max(0, Number.parseFloat(input?.value || '0') || 0);
    const maxFor = (line, action) => {
        if (action === 'request-return') return Number(line.dataset.returnable || 0);
        if (action === 'confirm-return') return Number(line.dataset.pendingReturn || 0);
        return Number(line.dataset.open || 0);
    };

    function renderAction() {
        const action = actionSelect.value;
        panel.dataset.action = action;
        const isReceive = action === 'receive';
        panel.querySelectorAll('.transfer-receive-only').forEach((el) => el.hidden = !isReceive);
        panel.querySelectorAll('.transfer-action-quantity').forEach((el) => el.hidden = isReceive || !action);
        lines.forEach((line) => {
            const quantity = line.querySelector('.js-resolution-quantity');
            if (quantity) quantity.max = String(maxFor(line, action));
        });
        message.textContent = '';
        submit.disabled = !action;
    }

    function buildPayload() {
        const action = actionSelect.value;
        const key = requestKey();
        if (action === 'receive') {
            const payloadLines = lines.map((line) => ({
                inventoryTransferDetailId: Number(line.dataset.detailId),
                receivedBaseQuantity: number(line.querySelector('.js-accepted-quantity')),
                rejectedBaseQuantity: number(line.querySelector('.js-rejected-quantity')),
                rejectionIssueType: line.querySelector('.js-rejection-type')?.value || null,
                rejectionReason: line.querySelector('.js-rejection-reason')?.value.trim() || null
            })).filter((line) => line.receivedBaseQuantity + line.rejectedBaseQuantity > 0);
            payloadLines.forEach((entry) => {
                const source = lines.find((line) => Number(line.dataset.detailId) === entry.inventoryTransferDetailId);
                if (entry.receivedBaseQuantity + entry.rejectedBaseQuantity > maxFor(source, action) + 0.0001)
                    throw new Error(t("Transfer.Js.ResolutionExceedsPending"));
                if (entry.rejectedBaseQuantity > 0 && (!entry.rejectionIssueType || !entry.rejectionReason))
                    throw new Error(t("Transfer.Js.RejectionDetailsRequired"));
            });
            if (!payloadLines.length) throw new Error(t("Transfer.Js.ResolutionQuantityRequired"));
            return {
                url: panel.dataset.receiveUrl,
                body: {
                    rowVersion: panel.dataset.rowVersion,
                    requestKey: key,
                    receivedAt: new Date().toISOString(),
                    note: reason.value.trim() || null,
                    lines: payloadLines
                }
            };
        }

        const payloadLines = lines.map((line) => ({
            inventoryTransferDetailId: Number(line.dataset.detailId),
            baseQuantity: number(line.querySelector('.js-resolution-quantity'))
        })).filter((line) => line.baseQuantity > 0);
        if (!payloadLines.length) throw new Error(t("Transfer.Js.FollowUpQuantityRequired"));
        payloadLines.forEach((entry) => {
            const source = lines.find((line) => Number(line.dataset.detailId) === entry.inventoryTransferDetailId);
            if (entry.baseQuantity > maxFor(source, action) + 0.0001)
                throw new Error(t("Transfer.Js.ResolutionLineExceeded"));
        });

        const note = reason.value.trim();
        if (action !== 'follow-up' && !note)
            throw new Error(t("Transfer.Js.ResolutionReasonRequired"));
        const urls = {
            'request-return': panel.dataset.returnUrl,
            'confirm-return': panel.dataset.confirmReturnUrl,
            'write-off': panel.dataset.resolveUrl,
            'close-shortage': panel.dataset.resolveUrl,
            'follow-up': panel.dataset.followUpUrl
        };
        return {
            url: urls[action],
            body: action === 'follow-up'
                ? { rowVersion: panel.dataset.rowVersion, requestKey: key, note: note || null, lines: payloadLines }
                : {
                    rowVersion: panel.dataset.rowVersion,
                    requestKey: key,
                    reason: note,
                    resolutionType: action === 'write-off' ? 4 : action === 'close-shortage' ? 5 : null,
                    lines: payloadLines
                },
            followUp: action === 'follow-up'
        };
    }

    async function execute() {
        try {
            const request = buildPayload();
            submit.disabled = true;
            message.className = 'transfer-resolution-message';
            message.textContent = t("Transfer.Js.Recording");
            const response = await fetch(request.url, {
                method: 'POST',
                credentials: 'same-origin',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token
                },
                body: JSON.stringify(request.body)
            });
            const data = await response.json().catch(() => ({}));
            if (!response.ok || data.success === false)
                throw new Error(data.message || t("Transfer.Js.ResolutionFailed"));
            message.classList.add('is-success');
            message.textContent = request.followUp ? t("Transfer.Js.FollowUpCreated") : t("Transfer.Js.RecordedSuccessfully");
            if (request.followUp && data.transfer?.inventoryTransferId) {
                window.location.href = `${window.location.pathname.replace(/\/Detail\/\d+$/i, '')}/Detail/${data.transfer.inventoryTransferId}`;
                return;
            }
            window.location.reload();
        } catch (error) {
            message.className = 'transfer-resolution-message is-error';
            message.textContent = error instanceof Error ? error.message : t("Transfer.Js.ResolutionFailed");
            submit.disabled = false;
        }
    }

    actionSelect.addEventListener('change', renderAction);
    submit.addEventListener('click', execute);
    renderAction();
})();
