(function (window, $) {
    'use strict';

    var config = { locale: 'vi-VN', text: {} };

    function token() {
        if (window.CafeChainAdminAjax && window.CafeChainAdminAjax.getAntiforgeryToken) {
            return window.CafeChainAdminAjax.getAntiforgeryToken();
        }
        return $('input[name="__RequestVerificationToken"]').val() || '';
    }

    function text(key) {
        var args = Array.prototype.slice.call(arguments, 1);
        var value = config.text[key] || key;
        return value.replace(/\{(\d+)\}/g, function (_, index) {
            return args[Number(index)] == null ? '' : String(args[Number(index)]);
        });
    }

    function formatNumber(value) {
        if (value == null || value === '') return '—';
        return Number(value).toLocaleString(config.locale || 'vi-VN', { maximumFractionDigits: 8 });
    }

    function renderEvaluation(panel, result) {
        if (!result) {
            panel.removeClass('ok error conflict').html('<div class="small text-muted">' + text('insufficientData') + '</div>');
            return;
        }

        var html = '';
        if (result.factor != null) {
            html += '<div class="mb-2"><strong>' + text('factorLabel') + '</strong> ' +
                text('factorFormula', result.fromUnitCode || '', formatNumber(result.factor) + ' ' + (result.toUnitCode || '')) + '</div>';
            html += '<div class="small text-muted mb-2">' +
                text('reverseNote', result.toUnitCode || '', formatNumber(result.reverseFactor), result.fromUnitCode || '') + '</div>';
        }
        if (result.fromDimension) {
            html += '<div class="small mb-2">' + text('direction', result.fromDimension, result.toDimension) + '</div>';
        }
        if (result.hasPhysicalConflict) html += '<div class="text-danger small mb-1">' + text('physicalConflict') + '</div>';
        if (result.isPhysicalStandard || result.errorCode === 'PHYSICAL_STANDARD_ALREADY_SUPPORTED') {
            html += '<div class="text-danger small mb-1">' + text('physicalNote') + '</div>';
        }
        if (result.isMassVolumeCross || result.errorCode === 'CROSS_DIMENSION_CONVERSION_NOT_SUPPORTED') {
            html += '<div class="text-danger small mb-1">' + text('crossDimension') + '</div>';
        }
        if (result.hasPackageConflict) {
            html += '<div class="mb-2 p-2 border rounded bg-warning bg-opacity-10"><strong>' + text('packageConflict') + '</strong><ul class="mb-1 small">';
            html += '<li>' + text('supplierSpec', formatNumber(result.primaryPackageQuantity), result.primaryPackageUnitCode || '') +
                (result.primarySupplierName ? ' (' + result.primarySupplierName + ')' : '') + '</li>';
            html += '<li>' + text('impliedMeasuring') + ' ' + formatNumber(result.proposedPackageLikeQuantity) + ' ' + (result.primaryPackageUnitCode || '') + '</li>';
            html += '<li>' + text('baseCostNote') + '</li></ul></div>';
            $('#pkgAckWrap').show();
        } else {
            $('#pkgAckWrap').hide();
            if (!$('#PackageConflictAcknowledged').data('user')) $('#PackageConflictAcknowledged').prop('checked', false);
        }
        if (result.warnings && result.warnings.length) {
            html += '<ul class="small text-warning">';
            result.warnings.forEach(function (warning) { html += '<li>' + warning + '</li>'; });
            html += '</ul>';
        }
        if (!result.success && result.message) {
            html += '<div class="small text-danger mt-1">' + result.message + (result.errorCode ? ' <code>' + result.errorCode + '</code>' : '') + '</div>';
            panel.removeClass('ok').addClass(result.hasPackageConflict ? 'conflict' : 'error');
        } else if (result.success) {
            html += '<div class="small text-success mt-1">' + text('valid') + (result.hasPackageConflict ? ' ' + text('needPackageAck') : '') + '</div>';
            panel.removeClass('error conflict').addClass(result.hasPackageConflict ? 'conflict' : 'ok');
        }
        panel.html(html || '<div class="small text-muted">—</div>');
    }

    function init(options) {
        config = options || config;
        var timer = null;
        var panel = $('#evalPanel');

        function payload() {
            return {
                unitConversionId: config.unitConversionId || ($('#UnitConversionId').val() ? parseInt($('#UnitConversionId').val(), 10) : null),
                ingredientId: parseInt($('#ingredientSelect').val(), 10) || 0,
                fromUnitId: parseInt($('#FromUnitId').val(), 10) || 0,
                fromQuantity: parseFloat($('#FromQuantity').val()) || 0,
                toUnitId: parseInt($('#ToUnitId').val(), 10) || 0,
                toQuantity: parseFloat($('#ToQuantity').val()) || 0,
                packageConflictAcknowledged: $('#PackageConflictAcknowledged').is(':checked')
            };
        }

        function evaluate() {
            var data = payload();
            if (!data.ingredientId || !data.fromUnitId || !data.toUnitId || data.fromQuantity <= 0 || data.toQuantity <= 0) {
                renderEvaluation(panel, null);
                return;
            }
            panel.html('<div class="small text-muted">' + text('loading') + '</div>');
            $.ajax({
                url: config.evaluateUrl,
                type: 'POST',
                headers: { 'RequestVerificationToken': token(), 'Content-Type': 'application/json' },
                data: JSON.stringify(data),
                success: function (result) { renderEvaluation(panel, result); },
                error: function () { panel.removeClass('ok').addClass('error').html('<div class="small text-danger">' + text('error') + '</div>'); }
            });
        }

        function schedule() { clearTimeout(timer); timer = setTimeout(evaluate, 350); }
        function updateSelectTitles() {
            $('#FromUnitId, #ToUnitId').each(function () {
                var selectedText = $(this).find('option:selected').text();
                if (selectedText) $(this).attr('title', $.trim(selectedText));
            });
        }

        $('#ingredientSelect, #FromUnitId, #ToUnitId, #FromQuantity, #ToQuantity, #PackageConflictAcknowledged').on('change input', function () {
            updateSelectTitles();
            schedule();
        });
        $('#PackageConflictAcknowledged').on('change', function () { $(this).data('user', true); });
        updateSelectTitles();
        schedule();
    }

    window.CafeChainUnitConversionForm = { init: init };
})(window, jQuery);
