document.addEventListener('click', async event => {
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

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
                await Swal.fire({ icon: 'warning', title: 'Dữ liệu đã thay đổi', text: body.message || 'Tín hiệu đã được cập nhật. Hãy tải lại dữ liệu trước khi thao tác tiếp.' });
                return null;
            }
            await Swal.fire({ icon: 'error', title: 'Không thể cập nhật', text: body.message || 'Dữ liệu đã thay đổi.' });
            return null;
        } catch (error) {
            await Swal.fire({ icon: 'error', title: 'Lỗi kết nối', text: 'Không thể lưu thao tác lúc này. Vui lòng thử lại.' });
            return null;
        } finally {
            button.disabled = false;
        }
    };

    const buildExplanationContent = body => {
        const wrapper = document.createElement('div');
        wrapper.className = 'text-start';
        const explanation = document.createElement('p');
        explanation.textContent = body.data?.explanation || body.message || 'Không thể giải thích tín hiệu.';
        wrapper.appendChild(explanation);

        const contextBlocks = [
            ['Ảnh hưởng có thể xảy ra:', body.presentation?.impactSummary],
            ['Vì sao có thông báo:', body.presentation?.whyDetected]
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
            heading.textContent = 'Dữ liệu nên kiểm tra:';
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
            ['Việc cần làm ngay:', body.presentation?.immediateActions],
            ['Hồ sơ cần chuẩn bị:', body.presentation?.preparationChecklist]
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
        warning.textContent = 'Đây chỉ là tín hiệu hỗ trợ kiểm tra, chưa đủ cơ sở kết luận nguyên nhân, sai phạm hoặc trách nhiệm cá nhân.';
        wrapper.appendChild(warning);
        return wrapper;
    };

    const explain = event.target.closest('[data-explain]');
    if (explain) {
        const originalText = explain.textContent;
        explain.disabled = true;
        explain.textContent = 'Đang phân tích...';

        // Show loading Swal
        Swal.fire({
            title: 'Đang phân tích...',
            text: 'Hệ thống AI đang phân tích dữ liệu tín hiệu vận hành, vui lòng đợi trong giây lát.',
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
                title: body.presentation?.metricDisplayName || 'Giải thích tín hiệu',
                html: buildExplanationContent(body)
            });
        } catch (err) {
            await Swal.fire({
                icon: 'error',
                title: 'Lỗi kết nối',
                text: 'Không thể kết nối đến hệ thống AI để giải thích tín hiệu lúc này.'
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
            title: 'Xác nhận đã xử lý',
            input: 'textarea',
            inputLabel: 'Ghi chú xử lý',
            showCancelButton: true,
            confirmButtonText: 'Đánh dấu đã xử lý',
            cancelButtonText: 'Hủy'
        });
        if (answer.isConfirmed)
            if (await postAction(resolve, { id: Number(resolve.dataset.resolve), action: 'RESOLVE', rowVersion: resolve.dataset.version, note: answer.value })) location.reload();
        return;
    }

    const feedback = event.target.closest('[data-feedback]');
    if (feedback) {
        const answer = await Swal.fire({
            title: 'Phản hồi tín hiệu',
            input: 'select',
            inputOptions: { Useful: 'Hữu ích', NotUseful: 'Không hữu ích', FalsePositive: 'Cảnh báo không phù hợp' },
            showCancelButton: true,
            confirmButtonText: 'Gửi phản hồi',
            cancelButtonText: 'Hủy'
        });
        if (answer.isConfirmed) {
            const result = await postAction(feedback, { id: Number(feedback.dataset.feedback), action: 'FEEDBACK', rowVersion: feedback.dataset.version, feedback: answer.value });
            if (result?.success) {
                const data = result.data || {};
                feedback.dataset.version = data.rowVersion || feedback.dataset.version;
                feedback.textContent = data.feedbackDisplay || result.message || 'Đã ghi nhận phản hồi';
                feedback.classList.add('is-completed');
                feedback.disabled = true;
                const status = document.querySelector(`[data-feedback-status="${feedback.dataset.feedback}"]`);
                if (status) status.textContent = data.feedbackDisplay || result.message || 'Đã ghi nhận phản hồi';
                await Swal.fire({ icon: 'success', title: 'Đã ghi nhận phản hồi', text: result.message || 'Phản hồi đã được lưu.', timer: 1600, showConfirmButton: false });
            }
        }
    }
});
