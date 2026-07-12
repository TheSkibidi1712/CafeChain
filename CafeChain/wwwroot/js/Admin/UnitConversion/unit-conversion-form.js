/**
 * #127 Admin unit conversion form — server Evaluate revalidation preview.
 * POST field names preserved; PackageConflictAcknowledged is command-only.
 */
(function (window, $) {
    'use strict';

    function token() {
        return $('input[name="__RequestVerificationToken"]').val() || '';
    }

    function fmt(n) {
        if (n == null || n === '') return '—';
        return Number(n).toLocaleString('vi-VN', { maximumFractionDigits: 8 });
    }

    function renderEval(panel, res) {
        if (!res) {
            panel.removeClass('ok error conflict').html('<div class="small text-muted">Chưa đủ dữ liệu để đánh giá.</div>');
            return;
        }
        var html = '';
        if (res.factor != null) {
            html += '<div class="mb-2"><strong>Hệ số:</strong> 1 ' + (res.fromUnitCode || '') +
                ' = ' + fmt(res.factor) + ' ' + (res.toUnitCode || '') + '</div>';
            html += '<div class="small text-muted mb-2">Chiều ngược (suy ra, không lưu row): 1 ' +
                (res.toUnitCode || '') + ' ≈ ' + fmt(res.reverseFactor) + ' ' + (res.fromUnitCode || '') + '</div>';
        }
        if (res.fromDimension) {
            html += '<div class="small mb-2">Chiều: ' + res.fromDimension + ' → ' + res.toDimension + '</div>';
        }
        if (res.hasPhysicalConflict) {
            html += '<div class="text-danger small mb-1"><strong>PHYSICAL_CONVERSION_CONFLICT</strong> — ' +
                (res.message || '') + '</div>';
        }
        if (res.isPhysicalStandard || res.errorCode === 'PHYSICAL_STANDARD_ALREADY_SUPPORTED') {
            html += '<div class="text-danger small mb-1">Quy đổi vật lý chuẩn — không lưu row.</div>';
        }
        if (res.isMassVolumeCross || res.errorCode === 'CROSS_DIMENSION_CONVERSION_NOT_SUPPORTED') {
            html += '<div class="text-danger small mb-1"><strong>CROSS_DIMENSION</strong> — không hỗ trợ mass↔volume.</div>';
        }
        if (res.hasPackageConflict) {
            html += '<div class="mb-2 p-2 border rounded bg-warning bg-opacity-10">';
            html += '<strong>Mâu thuẫn với package NCC</strong><ul class="mb-1 small">';
            html += '<li>Quy cách NCC: ' + fmt(res.primaryPackageQuantity) + ' ' +
                (res.primaryPackageUnitCode || '') + '/gói' +
                (res.primarySupplierName ? ' (' + res.primarySupplierName + ')' : '') + '</li>';
            html += '<li>Quy đổi đo lường ngụ ý: ' + fmt(res.proposedPackageLikeQuantity) + ' ' +
                (res.primaryPackageUnitCode || '') + '</li>';
            html += '<li>Giá vốn dùng quy cách nhà cung cấp — không tự chọn winner.</li>';
            html += '</ul></div>';
            $('#pkgAckWrap').show();
        } else {
            $('#pkgAckWrap').hide();
            if (!$('#PackageConflictAcknowledged').data('user')) {
                $('#PackageConflictAcknowledged').prop('checked', false);
            }
        }
        if (res.warnings && res.warnings.length) {
            html += '<ul class="small text-warning">';
            res.warnings.forEach(function (w) { html += '<li>' + w + '</li>'; });
            html += '</ul>';
        }
        if (!res.success && res.message) {
            html += '<div class="small text-danger mt-1">' + res.message +
                (res.errorCode ? ' <code>' + res.errorCode + '</code>' : '') + '</div>';
            panel.removeClass('ok').addClass(res.hasPackageConflict && !res.success ? 'conflict' : 'error');
        } else if (res.success) {
            html += '<div class="small text-success mt-1">Hợp lệ' +
                (res.hasPackageConflict ? ' (đã cần xác nhận package)' : '') + '.</div>';
            panel.removeClass('error conflict').addClass(res.hasPackageConflict ? 'conflict' : 'ok');
        }

        panel.html(html || '<div class="small text-muted">—</div>');
    }

    function init(cfg) {
        var timer = null;
        var panel = $('#evalPanel');

        function payload() {
            return {
                unitConversionId: cfg.unitConversionId || ($('#UnitConversionId').val() ? parseInt($('#UnitConversionId').val(), 10) : null),
                ingredientId: parseInt($('#ingredientSelect').val(), 10) || 0,
                fromUnitId: parseInt($('#FromUnitId').val(), 10) || 0,
                fromQuantity: parseFloat($('#FromQuantity').val()) || 0,
                toUnitId: parseInt($('#ToUnitId').val(), 10) || 0,
                toQuantity: parseFloat($('#ToQuantity').val()) || 0,
                packageConflictAcknowledged: $('#PackageConflictAcknowledged').is(':checked')
            };
        }

        function evaluate() {
            var p = payload();
            if (!p.ingredientId || !p.fromUnitId || !p.toUnitId || p.fromQuantity <= 0 || p.toQuantity <= 0) {
                renderEval(panel, null);
                return;
            }
            panel.html('<div class="small text-muted">Đang đánh giá trên server…</div>');
            $.ajax({
                url: cfg.evaluateUrl,
                type: 'POST',
                headers: {
                    'RequestVerificationToken': token(),
                    'Content-Type': 'application/json'
                },
                data: JSON.stringify(p),
                success: function (res) { renderEval(panel, res); },
                error: function () {
                    panel.removeClass('ok').addClass('error')
                        .html('<div class="small text-danger">Lỗi evaluate (antiforgery/kết nối).</div>');
                }
            });
        }

        function schedule() {
            clearTimeout(timer);
            timer = setTimeout(evaluate, 350);
        }

        $('#ingredientSelect, #FromUnitId, #ToUnitId, #FromQuantity, #ToQuantity, #PackageConflictAcknowledged')
            .on('change input', schedule);

        $('#PackageConflictAcknowledged').on('change', function () {
            $(this).data('user', true);
        });

        // Initial
        schedule();
    }

    window.CafeChainUnitConversionForm = { init: init };
})(window, jQuery);
