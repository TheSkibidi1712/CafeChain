document.addEventListener('click', async event => {
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const catalog = window.CafeChainUiCatalog?.read('anomalies-ui-catalog') || {};
    const t = (key, values) => window.CafeChainUiCatalog?.text(catalog, key, values) || key;

    const postAction = async (button, payload) => {
        button.disabled = true;
        try {
            const response = await fetch('/Admin/AdminOperationalAnomalies/Feedback', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', RequestVerificationToken: token },
                body: JSON.stringify(payload)
            });
            const body = await response.json().catch(() => ({}));
            if (response.ok) return body;
            if (response.status === 409) {
                await Swal.fire({ icon: 'warning', title: t('Anomalies.Js.DataChanged'), text: body.message || t('Anomalies.Js.SignalUpdated') });
                return null;
            }
            await Swal.fire({ icon: 'error', title: t('Anomalies.Js.UpdateFailed'), text: body.message || t('Anomalies.Js.UpdateFailedFallback') });
            return null;
        } catch (error) {
            await Swal.fire({ icon: 'error', title: t('Anomalies.Js.ConnectionError'), text: t('Anomalies.Js.ConnectionErrorText') });
            return null;
        } finally {
            button.disabled = false;
        }
    };

    const buildExplanationContent = body => {
        const wrapper = document.createElement('div');
        wrapper.className = 'text-start';
        const explanation = document.createElement('p');
        explanation.textContent = body.data?.explanation || body.message || t('Anomalies.Js.ExplainFallback');
        wrapper.appendChild(explanation);

        const contextBlocks = [
            [t('Anomalies.Js.ImpactLabel'), body.presentation?.impactSummary],
            [t('Anomalies.Js.WhyLabel'), body.presentation?.whyDetected]
        ];
        contextBlocks.forEach(([title, value]) => {
            if (!value) return;
            const heading = document.createElement('strong');
            heading.textContent = title;
            wrapper.appendChild(heading);
            const paragraph = document.createElement('p');
            paragraph.textContent = value;
            wrapper.appendChild(paragraph);
        });

        const checks = body.presentation?.suggestedChecks || [];
        if (checks.length) {
            const heading = document.createElement('strong');
            heading.textContent = t('Anomalies.Js.ChecksLabel');
            wrapper.appendChild(heading);
            const list = document.createElement('ul');
            checks.forEach(check => {
                const item = document.createElement('li');
                item.textContent = check;
                list.appendChild(item);
            });
            wrapper.appendChild(list);
        }

        const actionGroups = [
            [t('Anomalies.Js.ImmediateLabel'), body.presentation?.immediateActions],
            [t('Anomalies.Js.PreparationLabel'), body.presentation?.preparationChecklist]
        ];
        actionGroups.forEach(([title, values]) => {
            if (!values?.length) return;
            const heading = document.createElement('strong');
            heading.textContent = title;
            wrapper.appendChild(heading);
            const list = document.createElement('ul');
            values.forEach(value => {
                const item = document.createElement('li');
                item.textContent = value;
                list.appendChild(item);
            });
            wrapper.appendChild(list);
        });

        const warning = document.createElement('p');
        warning.className = 'small text-muted mb-0';
        warning.textContent = t('Anomalies.Js.Disclaimer');
        wrapper.appendChild(warning);
        return wrapper;
    };

    const explain = event.target.closest('[data-explain]');
    if (explain) {
        const originalText = explain.textContent;
        explain.disabled = true;
        explain.textContent = t('Anomalies.Js.Analyzing');

        // Show loading Swal
        Swal.fire({
            title: t('Anomalies.Js.Analyzing'),
            text: t('Anomalies.Js.AnalyzingText'),
            allowOutsideClick: false,
            allowEscapeKey: false,
            didOpen: () => {
                Swal.showLoading();
            }
        });

        try {
            const response = await fetch('/Admin/AdminOperationalAnomalies/Explain', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', RequestVerificationToken: token },
                body: JSON.stringify(Number(explain.dataset.explain))
            });
            const body = await response.json();
            await Swal.fire({
                icon: body.success ? 'info' : 'error',
                title: body.presentation?.metricDisplayName || t('Anomalies.Js.ExplainTitle'),
                html: buildExplanationContent(body)
            });
        } catch (err) {
            await Swal.fire({
                icon: 'error',
                title: t('Anomalies.Js.ConnectionError'),
                text: t('Anomalies.Js.AiConnectionErrorText')
            });
        } finally {
            explain.disabled = false;
            explain.textContent = originalText;
        }
        return;
    }

    const ack = event.target.closest('[data-ack]');
    if (ack) {
        if (await postAction(ack, { id: Number(ack.dataset.ack), action: 'ACKNOWLEDGE', rowVersion: ack.dataset.version })) location.reload();
        return;
    }

    const resolve = event.target.closest('[data-resolve]');
    if (resolve) {
        const answer = await Swal.fire({
            title: t('Anomalies.Js.ResolveTitle'),
            input: 'textarea',
            inputLabel: t('Anomalies.Js.ResolveNote'),
            showCancelButton: true,
            confirmButtonText: t('Anomalies.Js.ResolveConfirm'),
            cancelButtonText: t('Anomalies.Js.CancelShort')
        });
        if (answer.isConfirmed)
            if (await postAction(resolve, { id: Number(resolve.dataset.resolve), action: 'RESOLVE', rowVersion: resolve.dataset.version, note: answer.value })) location.reload();
        return;
    }

    const feedback = event.target.closest('[data-feedback]');
    if (feedback) {
        const answer = await Swal.fire({
            title: t('Anomalies.Js.FeedbackTitle'),
            input: 'select',
            inputOptions: { Useful: t('Anomalies.Js.FeedbackUseful'), NotUseful: t('Anomalies.Js.FeedbackNotUseful'), FalsePositive: t('Anomalies.Js.FeedbackFalsePositive') },
            showCancelButton: true,
            confirmButtonText: t('Anomalies.Js.FeedbackConfirm'),
            cancelButtonText: t('Anomalies.Js.CancelShort')
        });
        if (answer.isConfirmed) {
            const result = await postAction(feedback, { id: Number(feedback.dataset.feedback), action: 'FEEDBACK', rowVersion: feedback.dataset.version, feedback: answer.value });
            if (result?.success) {
                const data = result.data || {};
                feedback.dataset.version = data.rowVersion || feedback.dataset.version;
                feedback.textContent = data.feedbackDisplay || result.message || t('Anomalies.Js.FeedbackRecorded');
                feedback.classList.add('is-completed');
                feedback.disabled = true;
                const status = document.querySelector(`[data-feedback-status="${feedback.dataset.feedback}"]`);
                if (status) status.textContent = data.feedbackDisplay || result.message || t('Anomalies.Js.FeedbackRecorded');
                await Swal.fire({ icon: 'success', title: t('Anomalies.Js.FeedbackRecorded'), text: result.message || t('Anomalies.Js.FeedbackSaved'), timer: 1600, showConfirmButton: false });
            }
        }
    }
});
