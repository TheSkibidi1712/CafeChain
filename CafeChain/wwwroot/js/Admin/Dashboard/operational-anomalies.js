document.addEventListener('click', async event => {
    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const explain = event.target.closest('[data-explain]');
    if (explain) {
        explain.disabled = true;
        try { const response = await fetch('/Admin/AdminOperationalAnomalies/Explain', { method: 'POST', headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token }, body: JSON.stringify(Number(explain.dataset.explain)) }); const body = await response.json(); await Swal.fire({ icon: body.success ? 'info' : 'error', title: 'Giải thích tín hiệu', text: body.data?.explanation || body.message || 'Không thể giải thích.' }); } finally { explain.disabled = false; }
    }
    const ack = event.target.closest('[data-ack]');
    if (ack) {
        ack.disabled = true;
        const response = await fetch('/Admin/AdminOperationalAnomalies/Feedback', { method: 'POST', headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token }, body: JSON.stringify({ id: Number(ack.dataset.ack), action: 'ACKNOWLEDGE', rowVersion: ack.dataset.version }) });
        if (response.ok) location.reload(); else { const body = await response.json(); await Swal.fire({ icon: 'error', title: 'Không thể cập nhật', text: body.message || 'Dữ liệu đã thay đổi.' }); ack.disabled = false; }
    }
});
