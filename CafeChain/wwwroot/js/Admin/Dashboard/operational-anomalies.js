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
            if (response.ok) { location.reload(); return; }
            const body = await response.json();
            await Swal.fire({ icon: 'error', title: 'Không thể cập nhật', text: body.message || 'Dữ liệu đã thay đổi.' });
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

        const warning = document.createElement('p');
        warning.className = 'small text-muted mb-0';
        warning.textContent = 'Đây chỉ là tín hiệu hỗ trợ kiểm tra, chưa đủ cơ sở kết luận nguyên nhân, sai phạm hoặc trách nhiệm cá nhân.';
        wrapper.appendChild(warning);
        return wrapper;
    };

    const explain = event.target.closest('[data-explain]');
    if (explain) {
        const originalText = explain.innerHTML;
        explain.disabled = true;
        explain.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i> Đang phân tích...';

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
            explain.innerHTML = originalText;
        }
        return;
    }

    const ack = event.target.closest('[data-ack]');
    if (ack) {
        await postAction(ack, { id: Number(ack.dataset.ack), action: 'ACKNOWLEDGE', rowVersion: ack.dataset.version });
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
            await postAction(resolve, { id: Number(resolve.dataset.resolve), action: 'RESOLVE', rowVersion: resolve.dataset.version, note: answer.value });
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
        if (answer.isConfirmed)
            await postAction(feedback, { id: Number(feedback.dataset.feedback), action: 'FEEDBACK', rowVersion: feedback.dataset.version, feedback: answer.value });
    }
});
