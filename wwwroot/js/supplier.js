/* ============================================================
   SUPPLIER MANAGEMENT — supplier.js
   Handles: Create, Edit, Toggle, Detail Tabs (Phones/Banks/Contacts)
   ============================================================ */

const SUP_BASE = '/Admin/AdminSupplier';

// ===================== VALIDATION HELPERS =====================

/** Số điện thoại VN: 10-11 số, bắt đầu bằng 0 */
function isValidPhone(val) { return /^0\d{9,10}$/.test(val); }

/** Mã số thuế VN: 10 hoặc 13 chữ số */
function isValidTaxCode(val) { return /^\d{10}(\d{3})?$/.test(val); }

/** Email chuẩn doanh nghiệp: bắt buộc có @ và domain hợp lệ */
function isValidEmail(val) {
    return /^[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}$/.test(val);
}

/** Số tài khoản ngân hàng: chỉ số, tối thiểu 6 ký tự */
function isValidBankNumber(val) { return /^\d{6,}$/.test(val); }

/** Họ tên: chỉ chữ cái (có dấu VN) và khoảng trắng, không ký tự đặc biệt */
function isValidName(val) {
    return val.trim().length > 0 &&
           /^[\p{L}\s]+$/u.test(val.trim());
}

// ─── BLOCK KEYS: Chặn ngay từ bàn phím ─────────────────────────────────────

/**
 * Chặn nhập ký tự không phải số (keydown + paste).
 * Cho phép: 0-9, Backspace, Delete, Tab, mũi tên, Ctrl+A/C/V/X
 */
function blockNonNumeric(selector) {
    const ALLOWED_KEYS = ['Backspace','Delete','Tab','ArrowLeft','ArrowRight','Home','End'];

    $(document).on('keydown', selector, function (e) {
        if (ALLOWED_KEYS.includes(e.key)) return;
        if ((e.ctrlKey || e.metaKey) && ['a','c','v','x'].includes(e.key.toLowerCase())) return;
        if (!/^\d$/.test(e.key)) {
            e.preventDefault();
            supFlashError(this);
        }
    });

    // Chặn paste chứa ký tự không phải số
    $(document).on('paste', selector, function (e) {
        e.preventDefault();
        const pasted = (e.originalEvent.clipboardData || window.clipboardData)
                       .getData('text').replace(/[^\d]/g, '');
        const el = this;
        const start = el.selectionStart, end = el.selectionEnd;
        const cur = $(this).val();
        const maxLen = parseInt($(this).attr('maxlength')) || 999;
        const newVal = (cur.slice(0, start) + pasted + cur.slice(end)).slice(0, maxLen);
        $(this).val(newVal);
    });

    // Backup cho mobile (input event)
    $(document).on('input', selector, function () {
        const cur = $(this).val();
        const clean = cur.replace(/[^\d]/g, '');
        if (cur !== clean) $(this).val(clean);
    });
}

/**
 * Chặn nhập ký tự đặc biệt cho trường Họ tên.
 * Cho phép: chữ cái (kể cả có dấu VN), khoảng trắng.
 */
function blockSpecialCharsInName(selector) {
    const ALLOWED_KEYS = ['Backspace','Delete','Tab','ArrowLeft','ArrowRight','Home','End',' '];

    $(document).on('keydown', selector, function (e) {
        if (ALLOWED_KEYS.includes(e.key)) return;
        if ((e.ctrlKey || e.metaKey) && ['a','c','v','x'].includes(e.key.toLowerCase())) return;
        // Cho phép ký tự là chữ cái Unicode (kể cả tiếng Việt có dấu)
        if (/^\p{L}$/u.test(e.key)) return;
        e.preventDefault();
        supFlashError(this);
    });

    $(document).on('paste', selector, function (e) {
        e.preventDefault();
        const pasted = (e.originalEvent.clipboardData || window.clipboardData)
                       .getData('text').replace(/[^\p{L}\s]/gu, '');
        const el = this;
        const start = el.selectionStart, end = el.selectionEnd;
        const cur = $(this).val();
        const newVal = cur.slice(0, start) + pasted + cur.slice(end);
        $(this).val(newVal);
    });

    $(document).on('input', selector, function () {
        const cur = $(this).val();
        const clean = cur.replace(/[^\p{L}\s]/gu, '');
        if (cur !== clean) $(this).val(clean);
    });
}

/**
 * Hiệu ứng flash đỏ khi nhập ký tự không hợp lệ
 */
function supFlashError(el) {
    $(el).addClass('input-error-flash');
    setTimeout(() => $(el).removeClass('input-error-flash'), 400);
}

// ─── ÁP DỤNG RÀNG BUỘC SAU KHI DOM READY ───────────────────────────────────
$(function () {
    // Chỉ nhập số: Mã số thuế
    blockNonNumeric('#c-taxcode');
    blockNonNumeric('#e-taxcode');

    // Chỉ nhập số: SĐT NCC chính (modal tạo mới)
    blockNonNumeric('#c-phone');

    // Chỉ nhập số: SĐT phụ (tab Chi tiết > Điện thoại)
    blockNonNumeric('#ph-number');

    // Chỉ nhập số: SĐT người liên hệ (modal tạo + tab Chi tiết > Liên hệ)
    blockNonNumeric('#c-cphone');
    blockNonNumeric('#ct-phone');

    // Chỉ nhập số: Số tài khoản ngân hàng (modal tạo + tab Chi tiết > Ngân hàng)
    blockNonNumeric('#c-accnumber');
    blockNonNumeric('#bk-number');

    // Chỉ chữ cái + khoảng trắng: Họ tên người liên hệ
    blockSpecialCharsInName('#c-cname');
    blockSpecialCharsInName('#ct-name');
});

// ===================== TOAST =====================
function supToast(msg, type = 'success') {
    const el = $('<div class="sup-toast-item sup-toast-' + type + '">' + msg + '</div>');
    $('#supToast').append(el);
    setTimeout(() => el.remove(), 3100);
}

// ===================== MODAL HELPERS =====================
function supOpen(id)  { $('#' + id).addClass('open'); }
function supClose(id) { $('#' + id).removeClass('open'); }

$(document).on('click', '[data-close]', function () {
    supClose($(this).data('close'));
});

// Close on backdrop click
$(document).on('click', '.sup-modal', function (e) {
    if ($(e.target).hasClass('sup-modal')) supClose($(this).attr('id'));
});

// ===================== FILTER =====================
$('#btnFilter').on('click', function () {
    const search = $('#searchBox').val().trim();
    const status = $('#statusFilter').val();
    let url = SUP_BASE + '/Index?';
    if (search) url += 'search=' + encodeURIComponent(search) + '&';
    if (status !== '') url += 'status=' + status;
    window.location.href = url;
});

$('#searchBox').on('keydown', function (e) {
    if (e.key === 'Enter') $('#btnFilter').click();
});

// ===================== BANK PICKER =====================
const BANKS_VN = [
    'Agribank – Ngân hàng Nông nghiệp và PTNT Việt Nam',
    'BIDV – Ngân hàng Đầu tư và Phát triển Việt Nam',
    'Vietcombank – Ngân hàng Ngoại thương Việt Nam',
    'VietinBank – Ngân hàng Công thương Việt Nam',
    'MB Bank – Ngân hàng Quân đội',
    'Techcombank – Ngân hàng Kỹ thương Việt Nam',
    'ACB – Ngân hàng Á Châu',
    'VPBank – Ngân hàng Việt Nam Thịnh Vượng',
    'TPBank – Ngân hàng Tiên Phong',
    'Sacombank – Ngân hàng Sài Gòn Thương Tín',
    'HDBank – Ngân hàng Phát triển TP.HCM',
    'VIB – Ngân hàng Quốc tế Việt Nam',
    'OCB – Ngân hàng Phương Đông',
    'MSB – Ngân hàng Hàng Hải Việt Nam',
    'SeABank – Ngân hàng Đông Nam Á',
    'LienVietPostBank – Ngân hàng Bưu điện Liên Việt',
    'SHB – Ngân hàng Sài Gòn – Hà Nội',
    'Eximbank – Ngân hàng Xuất nhập khẩu Việt Nam',
    'NAB – Nam A Bank',
    'PVcomBank – Ngân hàng Đại Chúng Việt Nam',
    'OceanBank – Ngân hàng Đại Dương',
    'GPBank – Ngân hàng Dầu khí Toàn cầu',
    'CBBank – Ngân hàng Xây dựng',
    'Bac A Bank – Ngân hàng Bắc Á',
    'KienLong Bank – Ngân hàng Kiên Long',
    'ABBank – Ngân hàng An Bình',
    'BaoViet Bank – Ngân hàng Bảo Việt',
    'VietBank – Ngân hàng Việt Nam Thương Tín',
    'SCB – Ngân hàng Sài Gòn',
    'NCB – Ngân hàng Quốc dân',
    'PGBank – Ngân hàng Thịnh vượng và Phát triển',
    'Saigonbank – Ngân hàng Sài Gòn Công Thương',
    'VietCapitalBank – Ngân hàng Bản Việt',
    'IVB – Ngân hàng Indovina',
    'HSBC Việt Nam',
    'Standard Chartered Việt Nam',
    'Citibank Việt Nam',
    'Shinhan Bank Việt Nam',
    'Woori Bank Việt Nam',
    'UOB Việt Nam',
    'CIMB Bank Việt Nam',
    'Hong Leong Bank Việt Nam',
    'MHB – Ngân hàng Phát triển Nhà ĐBSCL',
    'DongA Bank – Ngân hàng Đông Á',
    'Viet A Bank – Ngân hàng Việt Á',
];

/**
 * Khởi tạo một bank-picker searchable dropdown
 */
function initBankPicker(displayId, valSpanId, dropdownId, listId, hiddenId) {
    const $display  = $('#' + displayId);
    const $valSpan  = $('#' + valSpanId);
    const $dropdown = $('#' + dropdownId);
    const $list     = $('#' + listId);
    const $hidden   = $('#' + hiddenId);
    const $search   = $dropdown.find('.bank-picker-search');

    function renderList(filter) {
        const filtered = filter
            ? BANKS_VN.filter(b => b.toLowerCase().includes(filter.toLowerCase()))
            : BANKS_VN;
        if (filtered.length === 0) {
            $list.html('<li class="bank-picker-empty">Không tìm thấy ngân hàng</li>');
        } else {
            $list.html(filtered.map(b =>
                `<li class="bank-picker-item" data-val="${b}">${b}</li>`
            ).join(''));
        }
    }

    renderList('');

    $display.on('click', function (e) {
        e.stopPropagation();
        const isOpen = $dropdown.hasClass('open');
        // đóng tất cả picker khác
        $('.bank-picker-dropdown').removeClass('open');
        if (!isOpen) {
            $dropdown.addClass('open');
            $search.val('').focus();
            renderList('');
        }
    });

    $search.on('input', function () {
        renderList($(this).val());
    });

    // Ngăn click trong search làm đóng dropdown
    $search.on('click', function (e) { e.stopPropagation(); });

    $list.on('click', '.bank-picker-item', function () {
        const val = $(this).data('val');
        $hidden.val(val);
        $valSpan.text(val).addClass('selected');
        $dropdown.removeClass('open');
    });
}

// Đóng tất cả picker khi click ngoài
$(document).on('click', function (e) {
    if (!$(e.target).closest('.bank-picker').length) {
        $('.bank-picker-dropdown').removeClass('open');
    }
});

function resetBankPicker(valSpanId, hiddenId) {
    $('#' + valSpanId).text('Chọn ngân hàng').removeClass('selected');
    $('#' + hiddenId).val('');
}

// Khởi tạo 2 pickers sau khi DOM ready
$(function () {
    initBankPicker('c-bankpicker-display', 'c-bankpicker-val', 'c-bankpicker-dropdown', 'c-bankpicker-list', 'c-bankname');
    initBankPicker('bk-bankpicker-display', 'bk-bankpicker-val', 'bk-bankpicker-dropdown', 'bk-bankpicker-list', 'bk-name');
});

// ===================== LOCATION CASCADE =====================
function initLocationCascade(provId, distId, wardId) {
    const $prov = $('#' + provId);
    const $dist = $('#' + distId);
    const $ward = $('#' + wardId);

    // Load provinces once on page load
    if ($prov.find('option').length <= 1) {
        fetch(SUP_BASE + '/GetProvinces')
            .then(r => r.ok ? r.json() : Promise.reject(r.statusText))
            .then(data => {
                data.forEach(p => {
                    const code = p.code ?? p.Code ?? p.ProvinceId;
                    const name = p.name ?? p.Name;
                    $prov.append(`<option value="${code}">${name}</option>`);
                });
            })
            .catch(e => console.error("Lỗi tải Tỉnh/Thành:", e));
    }

    $prov.off('change.loc').on('change.loc', function () {
        $dist.html('<option value="">— Chọn Quận/Huyện —</option>').prop('disabled', true);
        $ward.html('<option value="">— Chọn Phường/Xã —</option>').prop('disabled', true);
        const pid = $(this).val();
        if (!pid) return;
        
        $dist.addClass('bg-light');
        fetch(SUP_BASE + '/GetDistricts?provinceId=' + pid)
            .then(r => r.ok ? r.json() : Promise.reject(r.statusText))
            .then(data => {
                data.forEach(d => {
                    const code = d.code ?? d.Code ?? d.DistrictId;
                    const name = d.name ?? d.Name;
                    $dist.append(`<option value="${code}">${name}</option>`);
                });
                $dist.prop('disabled', false).removeClass('bg-light');
            })
            .catch(e => {
                console.error("Lỗi tải Quận/Huyện:", e);
                supToast("Lỗi tải dữ liệu. Vui lòng thử lại.", "error");
                $dist.prop('disabled', false).removeClass('bg-light'); // Unlock anyway
            });
    });

    $dist.off('change.loc').on('change.loc', function () {
        $ward.html('<option value="">— Chọn Phường/Xã —</option>').prop('disabled', true);
        const did = $(this).val();
        if (!did) return;
        
        $ward.addClass('bg-light');
        fetch(SUP_BASE + '/GetWards?districtId=' + did)
            .then(r => r.ok ? r.json() : Promise.reject(r.statusText))
            .then(data => {
                data.forEach(w => {
                    const code = w.code ?? w.Code ?? w.WardId;
                    const name = w.name ?? w.Name;
                    $ward.append(`<option value="${code}">${name}</option>`);
                });
                $ward.prop('disabled', false).removeClass('bg-light');
            })
            .catch(e => {
                console.error("Lỗi tải Phường/Xã:", e);
                supToast("Lỗi tải dữ liệu. Vui lòng thử lại.", "error");
                $ward.prop('disabled', false).removeClass('bg-light'); // Unlock anyway
            });
    });
}

// Khởi tạo cascade cho cả 2 modal
$(function () {
    initLocationCascade('c-province', 'c-district', 'c-ward');
    initLocationCascade('e-province', 'e-district', 'e-ward');
});

// ===================== CREATE =====================
$('#btnCreate').on('click', function () {
    // Reset form
    $('#c-name,#c-taxcode,#c-website,#c-street').val('');
    $('#c-phone').val('');
    resetBankPicker('c-bankpicker-val', 'c-bankname');
    $('#c-accnumber,#c-accholder').val('');
    $('#c-cname,#c-cposition,#c-cphone,#c-cemail').val('');

    // Reset địa chỉ
    $('#c-province').val('');
    $('#c-district').html('<option value="">— Chọn Quận/Huyện —</option>').prop('disabled', true);
    $('#c-ward').html('<option value="">— Chọn Phường/Xã —</option>').prop('disabled', true);

    // Tải mã NCC tự động
    $('#c-code-display').html('<i class="fa fa-spinner fa-spin"></i> Đang tạo mã...');
    $('#c-code').val('');
    fetch(SUP_BASE + '/GetNextCode')
        .then(r => r.ok ? r.json() : Promise.reject(r.statusText))
        .then(res => {
            if (res.success) {
                $('#c-code').val(res.code);
                $('#c-code-display').html('<i class="fa fa-tag"></i> ' + res.code);
            } else {
                throw new Error(res.message);
            }
        })
        .catch(e => {
            console.error("Lỗi tải mã:", e);
            $('#c-code-display').html('<i class="fa fa-exclamation-triangle" style="color:red"></i> Lỗi tạo mã');
            supToast("Không thể tạo mã NCC. Kiểm tra kết nối máy chủ.", "error");
        });

    supOpen('createModal');
});

$('#btnSaveCreate').on('click', function () {
    const dto = {
        name:      $('#c-name').val().trim(),
        taxCode:   $('#c-taxcode').val().trim() || null,
        website:   $('#c-website').val().trim() || null,

        provinceId:    parseInt($('#c-province').val()) || null,
        districtId:    parseInt($('#c-district').val()) || null,
        wardId:        parseInt($('#c-ward').val())     || null,
        streetAddress: $('#c-street').val().trim() || null,

        primaryPhone: $('#c-phone').val().trim(),

        primaryBankName:      $('#c-bankname').val().trim(),
        primaryAccountNumber: $('#c-accnumber').val().trim(),
        primaryAccountHolder: $('#c-accholder').val().trim(),

        primaryContactName:     $('#c-cname').val().trim(),
        primaryContactPosition: $('#c-cposition').val().trim() || null,
        primaryContactPhone:    $('#c-cphone').val().trim() || null,
        primaryContactEmail:    $('#c-cemail').val().trim() || null,
    };

    // --- Validate bắt buộc ---
    if (!dto.name) {
        supToast('Tên NCC không được để trống', 'error'); return;
    }
    if (!dto.primaryPhone) {
        supToast('SĐT chính không được để trống', 'error'); return;
    }
    if (!isValidPhone(dto.primaryPhone)) {
        supToast('SĐT chính không hợp lệ (10-11 số, bắt đầu bằng 0)', 'error'); return;
    }
    if (dto.taxCode && !isValidTaxCode(dto.taxCode)) {
        supToast('Mã số thuế không hợp lệ (10 hoặc 13 chữ số)', 'error'); return;
    }
    if (!dto.primaryBankName) {
        supToast('Vui lòng chọn ngân hàng', 'error'); return;
    }
    if (!dto.primaryAccountNumber) {
        supToast('Số tài khoản không được để trống', 'error'); return;
    }
    if (!isValidBankNumber(dto.primaryAccountNumber)) {
        supToast('Số tài khoản chỉ được chứa chữ số (tối thiểu 6 số)', 'error'); return;
    }
    if (!dto.primaryAccountHolder) {
        supToast('Chủ tài khoản không được để trống', 'error'); return;
    }
    if (!dto.primaryContactName) {
        supToast('Họ tên người liên hệ không được để trống', 'error'); return;
    }
    if (!isValidName(dto.primaryContactName)) {
        supToast('Họ tên không được chứa số hay ký tự đặc biệt', 'error'); return;
    }
    if (dto.primaryContactPhone && !isValidPhone(dto.primaryContactPhone)) {
        supToast('SĐT người liên hệ không hợp lệ (10-11 số, bắt đầu bằng 0)', 'error'); return;
    }
    if (dto.primaryContactEmail && !isValidEmail(dto.primaryContactEmail)) {
        supToast('Email người liên hệ không đúng định dạng (vd: ten@congty.com)', 'error'); return;
    }

    $.ajax({
        url: SUP_BASE + '/Create',
        method: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(dto),
        success: function (res) {
            if (res.success) {
                supToast(res.message);
                supClose('createModal');
                setTimeout(() => location.reload(), 800);
            } else {
                supToast(res.message, 'error');
            }
        },
        error: function () { supToast('Lỗi kết nối máy chủ', 'error'); }
    });
});

// ===================== EDIT =====================
$(document).on('click', '.edit-btn', function () {
    const id = $(this).data('id');
    $.get(SUP_BASE + '/GetById', { id }, function (res) {
        if (!res.success) { supToast(res.message, 'error'); return; }
        const d = res.data;
        $('#e-id').val(d.supplierId);
        $('#e-code-text').text(d.code);
        $('#e-name').val(d.name);
        $('#e-taxcode').val(d.taxCode || '');
        $('#e-website').val(d.website || '');
        $('#e-active').val(d.active.toString());

        // Hiển thị địa chỉ cũ (text)
        $('#e-address-current').val(d.address || '');

        // Reset 3 cấp về mặc định (chưa chọn)
        $('#e-province').val('');
        $('#e-district').html('<option value="">— Chọn Quận/Huyện —</option>').prop('disabled', true);
        $('#e-ward').html('<option value="">— Chọn Phường/Xã —</option>').prop('disabled', true);
        $('#e-street').val('');

        supOpen('editModal');
    });
});

$('#btnSaveEdit').on('click', function () {
    const dto = {
        supplierId: parseInt($('#e-id').val()),
        name:       $('#e-name').val().trim(),
        taxCode:    $('#e-taxcode').val().trim() || null,
        website:    $('#e-website').val().trim() || null,
        active:     $('#e-active').val() === 'true',

        provinceId:    parseInt($('#e-province').val()) || null,
        districtId:    parseInt($('#e-district').val()) || null,
        wardId:        parseInt($('#e-ward').val())     || null,
        streetAddress: $('#e-street').val().trim() || null,
    };

    if (!dto.name) {
        supToast('Tên NCC không được để trống', 'error'); return;
    }
    if (dto.taxCode && !isValidTaxCode(dto.taxCode)) {
        supToast('Mã số thuế không hợp lệ (10 hoặc 13 chữ số)', 'error'); return;
    }

    $.ajax({
        url: SUP_BASE + '/Update',
        method: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(dto),
        success: function (res) {
            if (res.success) {
                supToast(res.message);
                supClose('editModal');
                setTimeout(() => location.reload(), 800);
            } else {
                supToast(res.message, 'error');
            }
        },
        error: function () { supToast('Lỗi kết nối máy chủ', 'error'); }
    });
});

// ===================== TOGGLE STATUS =====================
$(document).on('click', '.toggle-btn', function () {
    const id = $(this).data('id');
    if (!confirm('Xác nhận thay đổi trạng thái?')) return;
    $.post(SUP_BASE + '/ToggleStatus', { id }, function (res) {
        if (res.success) { supToast(res.message); setTimeout(() => location.reload(), 800); }
        else supToast(res.message, 'error');
    });
});

// ===================== DETAIL MODAL =====================
let currentSupplierId = null;

function loadDetail(id) {
    $.get(SUP_BASE + '/GetById', { id }, function (res) {
        if (!res.success) { supToast(res.message, 'error'); return; }
        const d = res.data;
        currentSupplierId = d.supplierId;
        $('#d-supplierId').val(d.supplierId);
        $('#detailTitle').html('<i class="fa fa-list-ul"></i> Chi tiết: ' + d.name + ' <span class="sup-code">' + d.code + '</span>');
        renderPhones(d.phones);
        renderBanks(d.bankAccounts);
        renderContacts(d.contacts);
        supOpen('detailModal');
    });
}

$(document).on('click', '.detail-btn', function () {
    loadDetail($(this).data('id'));
    // reset bank picker khi mở modal chi tiết
    resetBankPicker('bk-bankpicker-val', 'bk-name');
    $('#bk-number,#bk-holder').val('');
});

// ----- TABS -----
$(document).on('click', '.sup-tab', function () {
    const target = $(this).data('tab');
    $('.sup-tab').removeClass('active');
    $('.sup-tab-panel').removeClass('active');
    $(this).addClass('active');
    $('#' + target).addClass('active');
});

// ===================== PHONES =====================
function renderPhones(phones) {
    let html = '';
    phones.forEach(p => {
        const tag = p.isPrimary
            ? '<span class="tag-primary">Chính</span>'
            : '<span class="tag-sub">Phụ</span>';
        const del = p.isPrimary
            ? ''
            : `<i class="fa fa-trash-can sup-icon-btn del-phone" data-id="${p.supplierPhoneId}" title="Xoá"></i>`;
        html += `<tr><td>${p.phoneNumber}</td><td>${tag}</td><td class="text-center">${del}</td></tr>`;
    });
    $('#phoneList').html(html || '<tr><td colspan="3" class="text-center text-muted">Chưa có SĐT nào</td></tr>');
}

$('#btnAddPhone').on('click', function () {
    const phone = $('#ph-number').val().trim();
    if (!phone) { supToast('Nhập số điện thoại', 'error'); return; }
    if (!isValidPhone(phone)) { supToast('SĐT không hợp lệ (10-11 số, bắt đầu bằng 0)', 'error'); return; }
    $.ajax({
        url: SUP_BASE + '/AddPhone',
        method: 'POST',
        contentType: 'application/json',
        data: JSON.stringify({ supplierId: currentSupplierId, phoneNumber: phone }),
        success: function (res) {
            if (res.success) {
                supToast(res.message);
                $('#ph-number').val('');
                loadDetail(currentSupplierId);
            } else supToast(res.message, 'error');
        }
    });
});

$(document).on('click', '.del-phone', function () {
    const id = $(this).data('id');
    if (!confirm('Xoá số điện thoại này?')) return;
    $.post(SUP_BASE + '/DeletePhone', { supplierPhoneId: id }, function (res) {
        if (res.success) { supToast(res.message); loadDetail(currentSupplierId); }
        else supToast(res.message, 'error');
    });
});

// ===================== BANKS =====================
function renderBanks(banks) {
    let html = '';
    banks.forEach(b => {
        const tag = b.isPrimary
            ? '<span class="tag-primary">Chính</span>'
            : '<span class="tag-sub">Phụ</span>';
        const del = b.isPrimary
            ? ''
            : `<i class="fa fa-trash-can sup-icon-btn del-bank" data-id="${b.supplierBankAccountId}" title="Xoá"></i>`;
        html += `<tr>
            <td>${b.bankName}</td>
            <td>${b.accountNumber}</td>
            <td>${b.accountHolder}</td>
            <td>${tag}</td>
            <td class="text-center">${del}</td>
        </tr>`;
    });
    $('#bankList').html(html || '<tr><td colspan="5" class="text-center text-muted">Chưa có tài khoản nào</td></tr>');
}

$('#btnAddBank').on('click', function () {
    const bankName      = $('#bk-name').val().trim();
    const accountNumber = $('#bk-number').val().trim();
    const accountHolder = $('#bk-holder').val().trim();

    if (!bankName) { supToast('Vui lòng chọn ngân hàng', 'error'); return; }
    if (!accountNumber) { supToast('Số tài khoản không được để trống', 'error'); return; }
    if (!isValidBankNumber(accountNumber)) { supToast('Số tài khoản chỉ được chứa chữ số (tối thiểu 6 số)', 'error'); return; }
    if (!accountHolder) { supToast('Chủ tài khoản không được để trống', 'error'); return; }

    $.ajax({
        url: SUP_BASE + '/AddBankAccount',
        method: 'POST',
        contentType: 'application/json',
        data: JSON.stringify({ supplierId: currentSupplierId, bankName, accountNumber, accountHolder }),
        success: function (res) {
            if (res.success) {
                supToast(res.message);
                resetBankPicker('bk-bankpicker-val', 'bk-name');
                $('#bk-number,#bk-holder').val('');
                loadDetail(currentSupplierId);
            } else supToast(res.message, 'error');
        }
    });
});

$(document).on('click', '.del-bank', function () {
    const id = $(this).data('id');
    if (!confirm('Xoá tài khoản ngân hàng này?')) return;
    $.post(SUP_BASE + '/DeleteBankAccount', { supplierBankAccountId: id }, function (res) {
        if (res.success) { supToast(res.message); loadDetail(currentSupplierId); }
        else supToast(res.message, 'error');
    });
});

// ===================== CONTACTS =====================
function renderContacts(contacts) {
    let html = '';
    contacts.forEach(c => {
        const tag = c.isPrimary
            ? '<span class="tag-primary">Chính</span>'
            : '<span class="tag-sub">Phụ</span>';
        const del = c.isPrimary
            ? ''
            : `<i class="fa fa-trash-can sup-icon-btn del-contact" data-id="${c.supplierContactId}" title="Xoá"></i>`;
        html += `<tr>
            <td>${c.name}</td>
            <td>${c.position || '—'}</td>
            <td>${c.phone || '—'}</td>
            <td>${c.email || '—'}</td>
            <td>${tag}</td>
            <td class="text-center">${del}</td>
        </tr>`;
    });
    $('#contactList').html(html || '<tr><td colspan="6" class="text-center text-muted">Chưa có liên hệ nào</td></tr>');
}

$('#btnAddContact').on('click', function () {
    const name     = $('#ct-name').val().trim();
    const position = $('#ct-position').val().trim() || null;
    const phone    = $('#ct-phone').val().trim() || null;
    const email    = $('#ct-email').val().trim() || null;

    if (!name) { supToast('Nhập tên người liên hệ', 'error'); return; }
    if (!isValidName(name)) { supToast('Họ tên không được chứa số hay ký tự đặc biệt', 'error'); return; }
    if (phone && !isValidPhone(phone)) { supToast('SĐT người liên hệ không hợp lệ (10-11 số, bắt đầu bằng 0)', 'error'); return; }
    if (email && !isValidEmail(email)) { supToast('Email không đúng định dạng (vd: ten@congty.com)', 'error'); return; }

    $.ajax({
        url: SUP_BASE + '/AddContact',
        method: 'POST',
        contentType: 'application/json',
        data: JSON.stringify({ supplierId: currentSupplierId, name, position, phone, email }),
        success: function (res) {
            if (res.success) {
                supToast(res.message);
                $('#ct-name,#ct-position,#ct-phone,#ct-email').val('');
                loadDetail(currentSupplierId);
            } else supToast(res.message, 'error');
        }
    });
});

$(document).on('click', '.del-contact', function () {
    const id = $(this).data('id');
    if (!confirm('Xoá liên hệ này?')) return;
    $.post(SUP_BASE + '/DeleteContact', { supplierContactId: id }, function (res) {
        if (res.success) { supToast(res.message); loadDetail(currentSupplierId); }
        else supToast(res.message, 'error');
    });
});
