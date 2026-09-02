/**
 * CafeChain Admin BOM Builder (#126)
 * Preserve POST field names: RecipeType, PreparedItemId, ExpectedYield, OutputUnitId, Details[].*
 */
(function (window, $) {
    'use strict';

    var recipeCatalog = window.CafeChainUiCatalog ? window.CafeChainUiCatalog.read('recipeUiCatalog') : {};
    var t = function (key, values) {
        return window.CafeChainUiCatalog
            ? window.CafeChainUiCatalog.text(recipeCatalog, key, values)
            : (recipeCatalog[key] || key);
    };
    var locale = document.documentElement.dataset.culture || 'vi-VN';

    function antiforgeryHeaders() {
        if (window.CafeChainAdminAjax && window.CafeChainAdminAjax.antiforgeryHeaders) {
            return window.CafeChainAdminAjax.antiforgeryHeaders();
        }
        var token = $('input[name="__RequestVerificationToken"]').val() || '';
        return { 'RequestVerificationToken': token, 'Content-Type': 'application/json' };
    }

    function formatMoney(n) {
        return new Intl.NumberFormat(locale).format(Math.round(n || 0));
    }

    function formatUnitLabel(value) {
        var code = String(value || '').trim().toLowerCase();
        if (code === 'g' || code === 'gram') return 'g';
        if (code === 'kg' || code === 'kilogram') return 'kg';
        if (code === 'ml' || code === 'milliliter') return 'ml';
        if (code === 'l' || code === 'liter') return 'L';
        if (code === 'pcs' || code === 'piece') return t('Recipe.Js.Piece');
        return value || t('Recipe.Js.Unit');
    }

    function buildItemDataMap() {
        var map = {};
        $('#itemTemplateSelect option').each(function () {
            var val = $(this).val();
            if (!val) return;
            map[val] = {
                basecost: parseFloat($(this).attr('data-basecost')) || 0,
                costcomplete: $(this).attr('data-costcomplete') === '1',
                packageprice: parseFloat($(this).attr('data-packageprice')) || 0,
                packageqty: parseFloat($(this).attr('data-packageqty')) || 0,
                packageunit: $(this).attr('data-packageunit') || '',
                baseunitcode: formatUnitLabel($(this).attr('data-baseunitcode') || ''),
                costmessage: $(this).attr('data-costmessage') || '',
                unitid: parseInt($(this).attr('data-unitid'), 10) || 0,
                unitname: formatUnitLabel($(this).attr('data-unitname') || ''),
                kind: $(this).attr('data-kind') || '',
                label: $(this).attr('data-label') || $(this).text(),
                recipeid: $(this).attr('data-recipeid') || '',
                picode: $(this).attr('data-picode') || '',
                piname: $(this).attr('data-piname') || ''
            };
        });
        return map;
    }

    function formatPreparedOption(data) {
        if (!data.id) return data.text;
        var $opt = $(data.element);
        var code = $opt.data('code') || '';
        var name = $opt.data('name') || data.text;
        var unit = $opt.data('baseunitcode') || '';
        var hasActive = String($opt.data('hasactive')) === '1' || String($opt.data('hasactive')) === 'true';
        var activeLabel = hasActive ? t('Recipe.Js.Yes') : t('Recipe.Js.None');
        return $(
            '<div class="rb-pi-option">' +
            '<div class="fw-semibold">[' + code + '] ' + name + '</div>' +
            '<div class="small text-muted">' + t('Recipe.Js.InventoryUnit', { unit: unit }) + ' · ' + t('Recipe.Js.ActiveRecipe', { status: activeLabel }) + '</div>' +
            '</div>'
        );
    }

    function formatPreparedSelection(data) {
        if (!data.id) return data.text;
        var $opt = $(data.element);
        var code = $opt.data('code') || '';
        var name = $opt.data('name') || data.text;
        return '[' + code + '] ' + name;
    }

    function formatBomOption(data) {
        if (!data.id) return data.text;
        var $opt = $(data.element);
        var kind = $opt.data('kind') || '';
        if (kind === 'child_recipe') {
            var rid = $opt.data('recipeid') || data.id;
            var pi = $opt.data('picode') ? (t('Recipe.Js.PreparedPrefix') + ': [' + $opt.data('picode') + '] ' + ($opt.data('piname') || '')) : t('Recipe.Js.PreparedPrefix') + ': (' + t('Recipe.Js.Unmapped') + ')';
            return $(
                '<div><div class="fw-semibold">[REC#' + rid + '] ' + ($opt.data('label') || data.text) + '</div>' +
                '<div class="small text-muted">' + t('Recipe.Js.PinnedVersion') + ' · ' + pi + '</div></div>'
            );
        }
        if (kind === 'ingredient') {
            var bu = $opt.data('baseunitcode') || $opt.data('unitname') || '';
            return $(
                '<div><div class="fw-semibold">' + (data.text || '') + '</div>' +
                '<div class="small text-muted">' + t('Recipe.Js.BaseUnit', { unit: bu }) + '</div></div>'
            );
        }
        return data.text;
    }

    function initCreate(cfg) {
        var rowCount = 0;
        var itemDataMap = buildItemDataMap();
        var initialSizeId = cfg.initialSizeId ? String(cfg.initialSizeId) : '';
        var previewTimer = null;
        var createBlockedByActiveRecipe = false;
        var saveInFlight = false;
        var tableBody = $('#bomTableBody');
        var sourceLines = Array.isArray(cfg.sourceLines) ? cfg.sourceLines : [];

        function selectedBusinessText(selector) {
            var text = $(selector).find(':selected').text().trim();
            if (!text || text.indexOf('--') === 0) return '';
            return text.replace(/\s*\([^)]*đ\)\s*$/, '').trim();
        }

        function currentLines() {
            var lines = [];
            tableBody.find('tr').each(function () {
                var code = $(this).find('.item-select').val();
                if (!code) return;
                lines.push({
                    itemCode: String(code),
                    qty: parseFloat($(this).find('.item-qty').val()) || 0,
                    unitId: parseInt($(this).find('.item-unitid').val(), 10) || 0,
                    unitName: $(this).find('.item-unitname').val() || ''
                });
            });
            return lines;
        }

        function updatePublicationReview() {
            if (!$('#publicationTarget').length) return;

            var type = $('input[name="RecipeType"]:checked').val();
            var target = t('Recipe.NotSelected');
            var output = t('Recipe.Unknown');
            if (type === 'POS') {
                var drink = selectedBusinessText('#drinkSelect');
                var size = selectedBusinessText('#sizeSelect');
                target = drink && size ? t('Recipe.Js.PosTarget', { drink: drink, size: size }) : t('Recipe.Js.PosTargetMissing');
                output = drink && size ? t('Recipe.Js.PosOutput', { drink: drink, size: size }) : t('Recipe.Unknown');
            } else if (type === 'TOPPING') {
                var topping = selectedBusinessText('#toppingSelect');
                target = topping ? t('Recipe.Js.ToppingTarget', { name: topping }) : t('Recipe.Js.ToppingMissing');
                output = topping ? t('Recipe.Js.ToppingOutput', { name: topping }) : t('Recipe.Unknown');
            } else if (type === 'SUBRECIPE') {
                var prepared = $('#preparedItemSelect option:selected').data('name') || selectedBusinessText('#preparedItemSelect');
                var quantity = parseFloat($('#expectedYieldInput').val()) || 0;
                var unit = selectedBusinessText('#outputUnitSelect');
                target = prepared ? t('Recipe.Js.PreparedTarget', { name: prepared }) : t('Recipe.Js.PreparedMissing');
                output = prepared && quantity > 0 && unit
                    ? t('Recipe.Js.YieldOutput', { quantity: quantity, unit: unit })
                    : t('Recipe.Js.YieldMissing');
            }

            var lines = currentLines();
            $('#publicationTarget').text(target);
            $('#publicationOutput').text(output);
            $('#publicationBomSummary').text(lines.length
                ? t('Recipe.Js.LineSummary', { count: lines.length })
                : t('Recipe.NoComponents'));

            if (!cfg.isNewVersion) {
                $('#publicationChangeSummary').text(t('Recipe.AppliedImmediately'));
                return;
            }

            var before = {};
            sourceLines.forEach(function (line) { before[String(line.itemCode || '')] = line; });
            var after = {};
            lines.forEach(function (line) { after[line.itemCode] = line; });
            var added = Object.keys(after).filter(function (key) { return !before[key]; }).length;
            var removed = Object.keys(before).filter(function (key) { return !after[key]; }).length;
            var changed = Object.keys(after).filter(function (key) {
                if (!before[key]) return false;
                var oldQuantity = parseFloat(before[key].qty) || 0;
                var oldUnitId = parseInt(before[key].unitId, 10) || 0;
                return oldQuantity !== after[key].qty || oldUnitId !== after[key].unitId;
            }).length;
            $('#publicationChangeSummary').text(t('Recipe.Js.ChangeSummary', { added: added, removed: removed, changed: changed }));
        }

        function showFormError(message, errors) {
            var items = Array.isArray(errors) ? errors.filter(Boolean) : [];
            var html = '<div class="fw-semibold">' + $('<div>').text(message || t('Recipe.Js.Invalid')).html() + '</div>';
            if (items.length) {
                html += '<ul class="mb-0 mt-2">';
                items.forEach(function (item) {
                    html += '<li>' + $('<div>').text(item).html() + '</li>';
                });
                html += '</ul>';
            }
            $('#formErrorSummary').html(html).show().focus();
            document.getElementById('formErrorSummary')?.scrollIntoView({ behavior: 'smooth', block: 'center' });
        }

        function setSaving(isSaving) {
            saveInFlight = isSaving;
            var btn = $('#btnSaveRecipe');
            btn.prop('disabled', isSaving || createBlockedByActiveRecipe);
            btn.text(isSaving ? t('Recipe.Js.Applying') : t('Recipe.SaveApply'));
            btn.attr('aria-busy', isSaving ? 'true' : 'false');
        }

        function clearBtpFields() {
            $('#preparedItemSelect').val(null).trigger('change');
            $('#expectedYieldInput').val('');
            $('#outputUnitSelect').val('');
            setPreviewIdle(t('Recipe.NormalizedHint'));
            $('#piSummary').hide().empty();
            $('#piActiveConflict').hide().empty();
            createBlockedByActiveRecipe = false;
            $('#btnSaveRecipe').prop('disabled', false);
        }

        function setPreviewIdle(msg) {
            $('#normalizedOutputPreview').html(
                '<div class="rb-preview-main text-muted">—</div>' +
                '<div class="rb-preview-sub">' + (msg || '') + '</div>'
            );
        }

        function setPreviewLoading() {
            $('#normalizedOutputPreview').html(
                '<div class="rb-preview-main text-muted">' + t('Recipe.Js.Calculating') + '</div>' +
                '<div class="rb-preview-sub">' + t('Recipe.Js.Normalizing') + '</div>'
            );
        }

        function refreshNormalizedPreview() {
            var pi = parseInt($('#preparedItemSelect').val(), 10) || 0;
            var qty = parseFloat($('#expectedYieldInput').val()) || 0;
            var unit = parseInt($('#outputUnitSelect').val(), 10) || 0;
            if (!pi) { setPreviewIdle(t('Recipe.Js.SelectPreparedOutput')); return; }
            if (!qty || qty <= 0) { setPreviewIdle(t('Recipe.Js.EnterYield')); return; }
            if (!unit) { setPreviewIdle(t('Recipe.Js.SelectOutputUnit')); return; }
            setPreviewLoading();
            $.ajax({
                url: cfg.previewUrl,
                type: 'POST',
                headers: antiforgeryHeaders(),
                data: JSON.stringify({ preparedItemId: pi, outputQuantity: qty, outputUnitId: unit }),
                success: function (res) {
                    if (res.success) {
                        var main = (res.outputQuantity != null ? res.outputQuantity : qty) + ' ' +
                            (res.outputUnitCode || '') + ' ' + t('Recipe.Js.PerBatch');
                        var sub = (res.normalizedQuantityInBase != null
                            ? t('Recipe.Js.InventoryQuantity', { quantity: Number(res.normalizedQuantityInBase).toLocaleString(locale), unit: res.baseUnitCode || '' })
                            : (res.preview || ''));
                        $('#normalizedOutputPreview').html(
                            '<div class="rb-preview-main">' + main + '</div>' +
                            '<div class="rb-preview-sub">' + sub + '</div>'
                        );
                    } else {
                        $('#normalizedOutputPreview').html(
                            '<div class="rb-preview-main text-danger">' + t('Recipe.Js.CannotConvert') + '</div>' +
                            '<div class="rb-preview-sub">' + (res.message || '') + '</div>'
                        );
                    }
                },
                error: function () {
                    $('#normalizedOutputPreview').html(
                        '<div class="rb-preview-main text-danger">' + t('Recipe.Js.PreviewError') + '</div>' +
                        '<div class="rb-preview-sub">' + t('Recipe.Js.SessionError') + '</div>'
                    );
                }
            });
        }

        function schedulePreview() {
            clearTimeout(previewTimer);
            previewTimer = setTimeout(refreshNormalizedPreview, 350);
        }

        function updatePreparedSummary() {
            var $opt = $('#preparedItemSelect option:selected');
            var id = $opt.val();
            if (!id) {
                $('#piSummary').hide().empty();
                $('#piActiveConflict').hide().empty();
                createBlockedByActiveRecipe = false;
                $('#btnSaveRecipe').prop('disabled', false);
                return;
            }
            var code = $opt.data('code') || '';
            var name = $opt.data('name') || '';
            var unitCode = $opt.data('baseunitcode') || '';
            var unitName = $opt.data('baseunitname') || '';
            var activeId = $opt.data('activerecipeid');
            var activeCode = $opt.data('activerecipecode') || '';
            var versionCount = parseInt($opt.data('versioncount'), 10) || 0;
            var hasActive = String($opt.data('hasactive')) === '1' || String($opt.data('hasactive')) === 'true' || !!activeId;

            var html =
                '<div class="small"><strong>' + t('Recipe.Js.Code') + ':</strong> ' + code + '</div>' +
                '<div class="small"><strong>' + t('Recipe.Js.Name') + ':</strong> ' + name + '</div>' +
                '<div class="small"><strong>' + t('Recipe.Js.InventoryUnit', { unit: '' }) + '</strong> ' + unitCode + (unitName ? ' (' + unitName + ')' : '') + '</div>' +
                '<div class="small"><strong>' + t('Recipe.Js.Status') + ':</strong> ' + t('Recipe.Js.Active') + '</div>' +
                '<div class="small"><strong>' + t('Recipe.Js.CurrentActiveRecipe') + ':</strong> ' +
                (hasActive ? (activeCode || t('Recipe.Js.ActiveExists')) : t('Recipe.Js.None')) +
                '</div>' +
                '<div class="small"><strong>' + t('Recipe.Js.VersionCount') + ':</strong> ' + versionCount + '</div>';
            $('#piSummary').html(html).show();

            if (hasActive && activeId) {
                createBlockedByActiveRecipe = true;
                var editHref = (cfg.editUrlTemplate || '').replace('{id}', activeId);
                $('#piActiveConflict').html(
                    '<strong>' + t('Recipe.Js.ActiveConflictTitle') + '</strong>' + (activeCode ? ' (' + activeCode + ')' : '') + '. ' +
                    t('Recipe.Js.ActiveConflictHint') + '<br class="mb-1"/>' +
                    '<a class="btn btn-sm btn-outline-secondary mt-1 me-1" href="' + editHref + '">' + t('Recipe.Js.ViewRecipe') + '</a>' +
                    '<a class="btn btn-sm btn-outline-primary mt-1" href="' + editHref + '">' + t('Recipe.Js.CreateVersion') + '</a>'
                ).show();
                $('#btnSaveRecipe').prop('disabled', true);
            } else {
                createBlockedByActiveRecipe = false;
                $('#piActiveConflict').hide().empty();
                $('#btnSaveRecipe').prop('disabled', false);
            }
            schedulePreview();
        }

        function initPreparedSelect() {
            if ($('#preparedItemSelect').hasClass('select2-hidden-accessible')) {
                $('#preparedItemSelect').select2('destroy');
            }
            $('#preparedItemSelect').select2({
                theme: 'bootstrap-5',
                width: '100%',
                placeholder: t('Recipe.SelectPrepared'),
                allowClear: true,
                matcher: function (params, data) {
                    if ($.trim(params.term) === '') return data;
                    if (!data.id) return null;
                    var term = params.term.toLowerCase();
                    var $el = $(data.element);
                    var code = String($el.data('code') || '').toLowerCase();
                    var name = String($el.data('name') || data.text || '').toLowerCase();
                    if (code.indexOf(term) > -1 || name.indexOf(term) > -1 || String(data.text).toLowerCase().indexOf(term) > -1) {
                        return data;
                    }
                    return null;
                },
                templateResult: formatPreparedOption,
                templateSelection: formatPreparedSelection,
                language: {
                    noResults: function () { return t('Recipe.Js.PreparedNotFound'); },
                    searching: function () { return t('Recipe.Js.Searching'); }
                }
            });
        }

        function refreshPreparedOptions() {
            var $btn = $('#btnRefreshPreparedItems').prop('disabled', true).text(t('Recipe.Js.Loading'));
            $.getJSON(cfg.bomOptionsUrl)
                .done(function (res) {
                    if (!res.success) return;
                    var current = $('#preparedItemSelect').val();
                    var html = '<option value="">' + t('Recipe.SelectPrepared') + '</option>';
                    (res.data || []).forEach(function (p) {
                        html += '<option value="' + p.preparedItemId + '"' +
                            ' data-code="' + (p.code || '') + '"' +
                            ' data-name="' + (p.name || '') + '"' +
                            ' data-baseunitcode="' + (p.baseUnitCode || '') + '"' +
                            ' data-baseunitname="' + (p.baseUnitName || '') + '"' +
                            ' data-activerecipeid="' + (p.activeRecipeId || '') + '"' +
                            ' data-activerecipecode="' + (p.activeRecipeCode || '') + '"' +
                            ' data-activerecipename="' + (p.activeRecipeName || '') + '"' +
                            ' data-versioncount="' + (p.versionCount || 0) + '"' +
                            ' data-hasactive="' + (p.hasActiveRecipe ? '1' : '0') + '">' +
                            '[' + p.code + '] ' + p.name + '</option>';
                    });
                    $('#preparedItemSelect').html(html);
                    initPreparedSelect();
                    if (current) {
                        $('#preparedItemSelect').val(current).trigger('change');
                    }
                })
                .always(function () {
                    $btn.prop('disabled', false).text(t('Recipe.RefreshPrepared'));
                });
        }

        function typeBadgeHtml(itemCode) {
            if (itemCode && itemCode.indexOf('ING_') === 0) {
                return '<span class="rb-badge-raw">' + t('Recipe.RawIngredients') + '</span>';
            }
            if (itemCode && itemCode.indexOf('REC_') === 0) {
                return '<span class="rb-badge-sub">' + t('Recipe.ChildPrepared') + '</span>';
            }
            return '<span class="text-muted small">—</span>';
        }

        function dataStatusHtml(itemInfo, itemCode) {
            if (!itemCode) return '';
            if (itemInfo && itemInfo.costcomplete && itemInfo.basecost > 0) {
                return '<span class="rb-status-badge rb-status-complete">' + t('Recipe.Js.DataComplete') + '</span>';
            }
            return '<span class="rb-status-badge rb-status-incomplete">' + t('Recipe.Js.DataIncomplete') + '</span>';
        }

        function findDuplicateRows(itemCode, exceptTr) {
            var rows = [];
            if (!itemCode) return rows;
            tableBody.find('tr').each(function () {
                if (exceptTr && this === exceptTr[0]) return;
                var v = $(this).find('.item-select').val();
                if (v === itemCode) rows.push($(this));
            });
            return rows;
        }

        function showDupWarn(tr, itemCode, others) {
            tr.find('.rb-dup-warn').remove();
            if (!others.length) return;
            var nums = others.map(function ($r) {
                return $r.find('td:first').text();
            }).join(', ');
            var first = others[0];
            var warn = $(
                '<div class="rb-dup-warn" role="alert">' +
                t('Recipe.Js.Duplicate', { rows: nums }) + ' ' +
                '<button type="button" class="btn btn-sm btn-outline-primary btn-merge-dup ms-1">' + t('Recipe.Js.MergeRow', { row: first.find('td:first').text() }) + '</button> ' +
                '<button type="button" class="btn btn-sm btn-outline-secondary btn-clear-dup ms-1">' + t('Recipe.Js.ChooseOther') + '</button>' +
                '</div>'
            );
            tr.find('td').eq(2).append(warn);
            warn.find('.btn-clear-dup').on('click', function () {
                tr.find('.item-select').val(null).trigger('change');
                tr.find('.rb-dup-warn').remove();
            });
            warn.find('.btn-merge-dup').on('click', function () {
                var unitA = parseInt(tr.find('.item-unitid').val(), 10) || 0;
                var unitB = parseInt(first.find('.item-unitid').val(), 10) || 0;
                if (unitA !== unitB) {
                    if (window.Swal) {
                        Swal.fire({
                            icon: 'warning',
                            title: t('Recipe.Js.CannotMerge'),
                            text: t('Recipe.Js.UnitMismatch', { unitA: unitA, unitB: unitB }),
                            confirmButtonColor: '#6f4e37',
                            customClass: { popup: 'rb-confirm-popup' }
                        });
                    }
                    return;
                }
                var q1 = parseFloat(tr.find('.item-qty').val()) || 0;
                var q2 = parseFloat(first.find('.item-qty').val()) || 0;
                first.find('.item-qty').val(q1 + q2);
                tr.remove();
                renumberRows();
                calculateTotal();
            });
        }

        function renumberRows() {
            tableBody.find('tr').each(function (i) {
                $(this).find('td:first').text(i + 1);
                $(this).find('.item-select').attr('name', 'Details[' + i + '].ItemCode');
                $(this).find('.item-qty').attr('name', 'Details[' + i + '].Quantity');
                $(this).find('.item-unitname').attr('name', 'Details[' + i + '].UnitName');
                $(this).find('.item-unitid').attr('name', 'Details[' + i + '].UnitId');
                $(this).find('.item-yield').attr('name', 'Details[' + i + '].YieldPercentage');
            });
            rowCount = tableBody.find('tr').length;
        }

        function calculateTotal() {
            var completeTotal = 0;
            var issues = [];
            var anyIncomplete = false;
            var anyRow = false;

            tableBody.find('tr').each(function () {
                var itemCode = $(this).find('.item-select').val();
                var quantity = parseFloat($(this).find('.item-qty').val()) || 0;
                var itemInfo = itemDataMap[itemCode];
                anyRow = anyRow || !!itemCode;

                $(this).find('.row-type-badge').html(typeBadgeHtml(itemCode));
                $(this).find('.row-data-status').html(dataStatusHtml(itemInfo, itemCode));

                if (!itemCode) {
                    $(this).find('.row-total-display').html('<span class="text-muted small">' + t('Recipe.NotSelected') + '</span>');
                    return;
                }

                var costComplete = itemInfo && itemInfo.costcomplete && itemInfo.basecost > 0;
                if (costComplete) {
                    var actualCost = quantity * itemInfo.basecost;
                    completeTotal += actualCost;
                    var unitCode = itemInfo.baseunitcode || itemInfo.unitname || t('Recipe.Js.Unit');
                    $(this).find('.row-total-display').html(
                        '<span class="rb-cost-sub">' + formatMoney(itemInfo.basecost) + ' ₫/' + unitCode + '</span>' +
                        '<span class="rb-cost-main d-block">' + formatMoney(actualCost) + ' <small>₫</small></span>'
                    );
                } else {
                    anyIncomplete = true;
                    var msg = (itemInfo && itemInfo.costmessage) ? itemInfo.costmessage : t('Recipe.Js.CostMissing');
                    issues.push({ code: itemCode, message: msg });
                    $(this).find('.row-total-display').html(
                        '<span class="text-warning small">' + t('Recipe.Js.IncompleteData') + '</span>'
                    );
                }
            });

            var $panel = $('#costPanel');
            var $issues = $('#displayCostIssues').empty();
            var $ctas = $('#costCtas').empty();

            if (!anyRow) {
                $panel.removeClass('complete').addClass('incomplete');
                $('#displayTotalCost').text('—');
                $('#displayCostStatus').text(t('Recipe.NoComponentSelected'));
                $issues.hide();
                $('#footerTotalCost').text('—');
                $('#hiddenTotalCost').val(0);
                $('#footerFoodCostPct').text('—');
                updatePublicationReview();
                return;
            }

            if (anyIncomplete || issues.length) {
                $panel.removeClass('complete').addClass('incomplete');
                $('#displayTotalCost').text('—');
                $('#displayCostStatus').text(t('Recipe.Js.CostCannotCalculate'));
                issues.forEach(function (iss) {
                    $issues.append('<li><code>' + iss.code + '</code> — ' + iss.message + '</li>');
                });
                $issues.show();
                // Only real Admin routes
                $ctas.append('<a class="btn btn-sm btn-outline-secondary" href="/Admin/AdminIngredient">' + t('Recipe.Js.ViewIngredients') + '</a>');
                $ctas.append('<a class="btn btn-sm btn-outline-secondary" href="/Admin/AdminSupplier">' + t('Recipe.Js.ViewSuppliers') + '</a>');
                $ctas.append('<a class="btn btn-sm btn-outline-secondary" href="/Admin/AdminUnitConversion">' + t('Recipe.Js.ViewConversions') + '</a>');
                $('#footerTotalCost').text(t('Recipe.Js.IncompleteUpper'));
                $('#hiddenTotalCost').val(0);
                $('#footerFoodCostPct').text('—');
            } else {
                $panel.removeClass('incomplete').addClass('complete');
                $('#displayTotalCost').html(formatMoney(completeTotal) + ' <small>₫ ' + t('Recipe.Js.PerBatch') + '</small>');
                $('#displayCostStatus').text(t('Recipe.Js.CostComplete'));
                $issues.hide();
                $('#footerTotalCost').text(formatMoney(completeTotal) + ' ₫');
                $('#hiddenTotalCost').val(Math.round(completeTotal));
                var sellingPrice = parseFloat($('#sizeSelect').find(':selected').data('price')) || 0;
                if (sellingPrice > 0) {
                    var pct = ((completeTotal / sellingPrice) * 100).toFixed(1);
                    $('#footerFoodCostPct').text(pct + '%');
                } else {
                    $('#footerFoodCostPct').text('—');
                }
            }
            updatePublicationReview();
        }

        function addRow(prefill) {
            var index = rowCount++;
            var selectOptionsHtml = $('#itemTemplateSelect').html();
            var tr = $('<tr></tr>');
            tr.html(
                '<td class="text-center text-muted fw-bold">' + (index + 1) + '</td>' +
                '<td class="row-type-badge"><span class="text-muted small">—</span></td>' +
                '<td>' +
                '<select name="Details[' + index + '].ItemCode" class="form-select form-select-sm item-select" required aria-label="' + t('Recipe.Js.ComponentRowAria', { row: index + 1 }) + '">' +
                selectOptionsHtml +
                '</select></td>' +
                '<td><input type="number" step="0.01" min="0.01" name="Details[' + index + '].Quantity" ' +
                'class="form-control form-control-sm text-end item-qty" value="' + (prefill && prefill.qty ? prefill.qty : '1') + '" required aria-label="' + t('Recipe.Column.Quantity') + '" /></td>' +
                '<td>' +
                '<input type="text" class="form-control form-control-sm production-readonly-field item-unitname" name="Details[' + index + '].UnitName" readonly tabindex="-1" aria-label="' + t('Recipe.Js.AutoUnitAria') + '" placeholder="' + t('Recipe.Js.Auto') + '" />' +
                '<input type="hidden" name="Details[' + index + '].UnitId" class="item-unitid" value="0" />' +
                '<input type="hidden" name="Details[' + index + '].YieldPercentage" class="item-yield" value="100" />' +
                '</td>' +
                '<td class="text-center row-total-display"><span class="text-muted small">' + t('Recipe.NotSelected') + '</span></td>' +
                '<td class="text-center row-data-status"></td>' +
                '<td class="text-center">' +
                '<button type="button" class="btn btn-sm btn-outline-danger btn-remove-row" aria-label="' + t('Recipe.Js.RemoveRow') + '">' + t('Recipe.Js.RemoveRow') + '</button>' +
                '</td>'
            );
            tableBody.append(tr);

            var newSelect = tr.find('.item-select');
            newSelect.select2({
                theme: 'bootstrap-5',
                placeholder: t('Recipe.Js.SearchComponent'),
                width: '100%',
                templateResult: formatBomOption,
                language: { noResults: function () { return t('Recipe.Js.NoComponent'); } }
            });

            newSelect.on('select2:select', function (e) {
                var selectedVal = e.params.data.id;
                var itemInfo = itemDataMap[selectedVal];
                if (itemInfo) {
                    tr.find('.item-unitname').val(itemInfo.unitname);
                    tr.find('.item-unitid').val(itemInfo.unitid);
                    tr.find('.item-qty').focus().select();
                }
                var dups = findDuplicateRows(selectedVal, tr);
                showDupWarn(tr, selectedVal, dups);
                calculateTotal();
            });
            newSelect.on('select2:clear', function () {
                tr.find('.item-unitname').val('');
                tr.find('.item-unitid').val(0);
                tr.find('.rb-dup-warn').remove();
                calculateTotal();
            });
            tr.find('.item-qty').on('input', calculateTotal);

            if (prefill && prefill.itemCode) {
                newSelect.val(prefill.itemCode).trigger('change');
                var info = itemDataMap[prefill.itemCode];
                if (info) {
                    tr.find('.item-unitname').val(info.unitname);
                    tr.find('.item-unitid').val(prefill.unitId || info.unitid);
                }
                if (prefill.qty) tr.find('.item-qty').val(prefill.qty);
                calculateTotal();
            }
        }

        // Type toggle
        $('input[name="RecipeType"]').on('change', function () {
            var type = $(this).val();
            if (type === 'POS') {
                $('#sectionPOS_Drink').show();
                if ($('#drinkSelect').val()) $('#sectionPOS_Size').show();
                $('#sectionTopping, #sectionSub_Recipe, #sectionOutput').hide();
                $('#toppingSelect').val(null).trigger('change');
                clearBtpFields();
            } else if (type === 'TOPPING') {
                $('#sectionTopping').show();
                $('#sectionPOS_Drink, #sectionPOS_Size, #sectionSub_Recipe, #sectionOutput').hide();
                $('#drinkSelect').val(null).trigger('change');
                clearBtpFields();
            } else {
                $('#sectionPOS_Drink, #sectionPOS_Size, #sectionTopping').hide();
                $('#sectionSub_Recipe, #sectionOutput').show();
                $('#drinkSelect, #toppingSelect').val(null).trigger('change');
            }
            updatePublicationReview();
        });

        $('#preparedItemSelect').on('change', function () {
            updatePreparedSummary();
            updatePublicationReview();
        });
        $('#expectedYieldInput, #outputUnitSelect').on('change input', function () {
            schedulePreview();
            updatePublicationReview();
        });
        $('#btnRefreshPreparedItems').on('click', refreshPreparedOptions);
        $('#btnAddRow').on('click', function () { addRow(); });

        $(document).on('click', '.btn-remove-row', function () {
            $(this).closest('tr').remove();
            renumberRows();
            calculateTotal();
        });

        $('#drinkSelect').select2({ theme: 'bootstrap-5', placeholder: t('Recipe.Js.SearchProduct'), width: '100%' });
        $('#toppingSelect').select2({ theme: 'bootstrap-5', placeholder: t('Recipe.Js.SearchTopping'), width: '100%' });
        initPreparedSelect();

        $('#drinkSelect').on('change', function () {
            var drinkId = $(this).val();
            var sizeSection = $('#sectionPOS_Size');
            var sizeSelect = $('#sizeSelect');
            if (!drinkId) {
                sizeSection.hide();
                sizeSelect.html('<option value="">' + t('Recipe.Js.SelectSize') + '</option>');
                return;
            }
            $.getJSON(cfg.sizesUrl, { drinkId: drinkId }, function (data) {
                var html = '<option value="">' + t('Recipe.Js.SelectSize') + '</option>';
                if (data && data.length) {
                    data.forEach(function (s) {
                        var price = formatMoney(s.price);
                        html += '<option value="' + s.sizeId + '" data-price="' + s.price + '">' + s.sizeName + ' (' + price + ' ₫)</option>';
                    });
                }
                sizeSelect.html(html);
                if (initialSizeId) {
                    sizeSelect.val(initialSizeId);
                    initialSizeId = '';
                }
                sizeSection.show();
                sizeSelect.trigger('change');
            });
        });

        $('#sizeSelect, #toppingSelect').on('change', function () {
            calculateTotal();
            updatePublicationReview();
        });

        if (cfg.addInitialRow !== false) {
            addRow();
        }

        $('#btnSaveRecipe').on('click', function () {
            if (saveInFlight) return;

            if (createBlockedByActiveRecipe) {
                showFormError(t('Recipe.Js.ActivePreparedConflict'));
                return;
            }
            var recipeType = $('input[name="RecipeType"]:checked').val();
            var payload = {
                RecipeType: recipeType,
                DrinkId: (recipeType === 'POS' && $('#drinkSelect').val()) ? parseInt($('#drinkSelect').val(), 10) : null,
                SizeId: (recipeType === 'POS' && $('#sizeSelect').val()) ? parseInt($('#sizeSelect').val(), 10) : null,
                ToppingId: (recipeType === 'TOPPING' && $('#toppingSelect').val()) ? parseInt($('#toppingSelect').val(), 10) : null,
                PreparedItemId: (recipeType === 'SUBRECIPE' && $('#preparedItemSelect').val()) ? parseInt($('#preparedItemSelect').val(), 10) : null,
                ExpectedYield: (recipeType === 'SUBRECIPE' && $('input[name="ExpectedYield"]').val()) ? parseFloat($('input[name="ExpectedYield"]').val()) : null,
                OutputUnitId: (recipeType === 'SUBRECIPE' && $('#outputUnitSelect').val()) ? parseInt($('#outputUnitSelect').val(), 10) : null,
                Description: $('textarea[name="Description"]').val(),
                Active: true,
                Details: []
            };

            var codes = {};
            var hasDup = false;
            tableBody.find('tr').each(function () {
                var itemCode = $(this).find('.item-select').val();
                if (!itemCode) return;
                if (codes[itemCode]) hasDup = true;
                codes[itemCode] = true;
                payload.Details.push({
                    ItemCode: itemCode,
                    Quantity: parseFloat($(this).find('.item-qty').val()) || 0,
                    UnitName: $(this).find('.item-unitname').val(),
                    UnitId: parseInt($(this).find('.item-unitid').val(), 10) || 0,
                    YieldPercentage: 100
                });
            });

            if (!payload.Details.length) {
                showFormError(t('Recipe.Js.AtLeastOne'));
                return;
            }
            if (hasDup) {
                showFormError(t('Recipe.Js.DuplicateBeforeSave'));
                return;
            }

            $('#formErrorSummary').hide();
            setSaving(true);
            if (window.Swal) {
                Swal.fire({ title: t('Recipe.Js.Saving'), allowOutsideClick: false, didOpen: function () { Swal.showLoading(); } });
            }

            $.ajax({
                url: cfg.createUrl,
                type: 'POST',
                headers: antiforgeryHeaders(),
                contentType: 'application/json; charset=utf-8',
                data: JSON.stringify(payload),
                success: function (res) {
                    if (res.success) {
                        if (window.Swal) {
                            Swal.fire({ icon: 'success', title: t('Recipe.Js.Applied'), text: res.message, confirmButtonColor: '#6f4e37', customClass: { popup: 'rb-confirm-popup' } })
                                .then(function () { window.location.href = res.redirectUrl || cfg.indexUrl; });
                        } else {
                            window.location.href = res.redirectUrl || cfg.indexUrl;
                        }
                    } else {
                        var msg = res.message || t('Recipe.Js.SaveError');
                        showFormError(msg, res.errors);
                        if (window.Swal) Swal.close();
                    }
                },
                error: function (xhr) {
                    var res = xhr.responseJSON || {};
                    var fallback = xhr.status === 409
                        ? t('Recipe.Js.Conflict')
                        : (xhr.status >= 500
                            ? t('Recipe.Js.ServerError')
                            : t('Recipe.Js.Invalid'));
                    showFormError(res.message || fallback, res.errors);
                    if (window.Swal) Swal.close();
                },
                complete: function () {
                    setSaving(false);
                }
            });
        });
    }

    function initEdit(cfg) {
        // Edit uses classic form post — enhance presentation only + hide yield column + prepared select2
        initCreate($.extend({}, cfg, { createMode: false }));
        // Re-enable save always on edit (versioning path)
        createBlockedByActiveRecipe = false;
        $('#btnSaveRecipe').prop('disabled', false).off('click');
        // Edit saves via form submit button name if present
    }

    window.CafeChainBomBuilder = {
        initCreate: initCreate,
        initEdit: function (cfg) {
            // Shared UX helpers for Edit page light theme
            if ($('#preparedItemSelect').length) {
                // select2 prepared if not locked
                if (!$('#preparedItemSelect').prop('disabled')) {
                    // reuse create init partially via light path
                }
            }
            // Debounced preview
            function antiforgery() {
                var token = $('input[name="__RequestVerificationToken"]').val() || '';
                return { 'RequestVerificationToken': token, 'Content-Type': 'application/json' };
            }
            var editPreviewTimer;
            function preview() {
                var pi = parseInt($('#preparedItemSelect').val() || $('input[name="PreparedItemId"]').val(), 10) || 0;
                var qty = parseFloat($('#expectedYieldInput').val()) || 0;
                var unit = parseInt($('#outputUnitSelect').val(), 10) || 0;
                if (!pi || !qty || !unit) {
                    $('#normalizedOutputPreview').html('<div class="rb-preview-sub text-muted">' + t('Recipe.Js.EnterPreparedYieldUnit') + '</div>');
                    return;
                }
                $('#normalizedOutputPreview').html('<div class="rb-preview-sub">' + t('Recipe.Js.Calculating') + '</div>');
                $.ajax({
                    url: cfg.previewUrl,
                    type: 'POST',
                    headers: antiforgery(),
                    data: JSON.stringify({ preparedItemId: pi, outputQuantity: qty, outputUnitId: unit }),
                    success: function (res) {
                        if (res.success) {
                            $('#normalizedOutputPreview').html(
                                '<div class="rb-preview-main">' + (res.outputQuantity || qty) + ' ' + (res.outputUnitCode || '') + ' ' + t('Recipe.Js.PerBatch') + '</div>' +
                                '<div class="rb-preview-sub">' + (res.normalizedQuantityInBase != null ? t('Recipe.Js.InventoryQuantity', { quantity: Number(res.normalizedQuantityInBase).toLocaleString(locale), unit: res.baseUnitCode || '' }) : (res.preview || '')) + '</div>'
                            );
                        } else {
                            $('#normalizedOutputPreview').html('<div class="text-danger small">' + (res.message || t('Recipe.Js.Error')) + '</div>');
                        }
                    }
                });
            }
            $('#expectedYieldInput, #outputUnitSelect, #preparedItemSelect').on('change input', function () {
                clearTimeout(editPreviewTimer); editPreviewTimer = setTimeout(preview, 350);
            });
            preview();

            if (!$('#preparedItemSelect').prop('disabled')) {
                $('#preparedItemSelect').select2({
                    theme: 'bootstrap-5', width: '100%',
                    templateResult: formatPreparedOption,
                    templateSelection: formatPreparedSelection,
                    matcher: function (params, data) {
                        if ($.trim(params.term) === '') return data;
                        if (!data.id) return null;
                        var term = params.term.toLowerCase();
                        var $el = $(data.element);
                        var code = String($el.data('code') || '').toLowerCase();
                        var name = String($el.data('name') || data.text || '').toLowerCase();
                        if (code.indexOf(term) > -1 || name.indexOf(term) > -1) return data;
                        return null;
                    }
                });
            }
        }
    };
})(window, jQuery);
