/* ============================================================
   SUPPLIER MANAGEMENT — supplier.js
   Handles: Create (+ secondary items), Edit, Toggle, Detail Tabs
   ============================================================ */

const SUP_BASE = '/Admin/AdminSupplier';

// ===================== VALIDATION HELPERS =====================

/** Số điện thoại VN: 10-11 số, bắt đầu bằng 0 */
function isValidPhone(val) { return /^0\d{9,10}$/.test(val); }

/** Mã số thuế VN: 10 hoặc 13 chữ số */
function isValidTaxCode(val) { return /^\d{10}(\d{3})?$/.test(val); }

/** Email chuẩn doanh nghiệp */
function isValidEmail(val) {
    return /^[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}$/.test(val);
}

/** Số tài khoản ngân hàng: chỉ số, tối thiểu 6 ký tự */
function isValidBankNumber(val) { return /^\d{6,}$/.test(val); }

/** Họ tên: chỉ chữ cái (có dấu VN) và khoảng trắng */
function isValidName(val) {
    return val.trim().length > 0 && /^[\p{L}\s]+$/u.test(val.trim());
}

// ─── BLOCK KEYS ─────────────────────────────────────────────────────────────

function blockNonNumeric(selector) {
    const ALLOWED_KEYS = ['Backspace', 'Delete', 'Tab', 'ArrowLeft', 'ArrowRight', 'Home', 'End'];
    $(document).on('keydown', selector, function (e) {
        if (ALLOWED_KEYS.includes(e.key)) return;
        if ((e.ctrlKey || e.metaKey) && ['a', 'c', 'v', 'x'].includes(e.key.toLowerCase())) return;
        if (!/^\d$/.test(e.key)) { e.preventDefault(); supFlashError(this); }
    });
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
    $(document).on('input', selector, function () {
        const cur = $(this).val();
        const clean = cur.replace(/[^\d]/g, '');
        if (cur !== clean) $(this).val(clean);
    });
}

function blockSpecialCharsInName(selector) {
    const ALLOWED_KEYS = ['Backspace', 'Delete', 'Tab', 'ArrowLeft', 'ArrowRight', 'Home', 'End', ' '];
    $(document).on('keydown', selector, function (e) {
        if (ALLOWED_KEYS.includes(e.key)) return;
        if ((e.ctrlKey || e.metaKey) && ['a', 'c', 'v', 'x'].includes(e.key.toLowerCase())) return;
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
        $(this).val(cur.slice(0, start) + pasted + cur.slice(end));
    });
    $(document).on('input', selector, function () {
        const cur = $(this).val();
        const clean = cur.replace(/[^\p{L}\s]/gu, '');
        if (cur !== clean) $(this).val(clean);
    });
}

function supFlashError(el) {
    $(el).addClass('input-error-flash');
    setTimeout(() => $(el).removeClass('input-error-flash'), 400);
}

// Áp dụng ràng buộc sau khi DOM ready
$(function () {
    // Chỉ số: MST
    blockNonNumeric('#c-taxcode');
    blockNonNumeric('#e-taxcode');
    // Chỉ số: SĐT
    blockNonNumeric('#c-phone');
    blockNonNumeric('#c-phone-extra');
    blockNonNumeric('#ph-number');
    blockNonNumeric('#c-cphone');
    blockNonNumeric('#c2-cphone');
    blockNonNumeric('#ct-phone');
    // Chỉ số: Số TK
    blockNonNumeric('#c-accnumber');
    blockNonNumeric('#c2-accnumber');
    blockNonNumeric('#bk-number');
    // Chỉ chữ cái: Họ tên
    blockSpecialCharsInName('#c-cname');
    blockSpecialCharsInName('#c2-cname');
    blockSpecialCharsInName('#ct-name');
});

// ===================== TOAST =====================
function supToast(msg, type = 'success') {
    const el = $('<div class="sup-toast-item sup-toast-' + type + '">' + msg + '</div>');
    $('#supToast').append(el);
    setTimeout(() => el.remove(), 3100);
}

// ===================== MODAL HELPERS =====================
function supOpen(id) { $('#' + id).addClass('open'); }
function supClose(id) { $('#' + id).removeClass('open'); }

$(document).on('click', '[data-close]', function () {
    supClose($(this).data('close'));
});

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

function initBankPicker(displayId, valSpanId, dropdownId, listId, hiddenId) {
    const $display = $('#' + displayId);
    const $valSpan = $('#' + valSpanId);
    const $dropdown = $('#' + dropdownId);
    const $list = $('#' + listId);
    const $hidden = $('#' + hiddenId);
    const $search = $dropdown.find('.bank-picker-search');

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
        $('.bank-picker-dropdown').removeClass('open');
        if (!isOpen) {
            $dropdown.addClass('open');
            $search.val('').focus();
            renderList('');
        }
    });

    $search.on('input', function () { renderList($(this).val()); });
    $search.on('click', function (e) { e.stopPropagation(); });

    $list.on('click', '.bank-picker-item', function () {
        const val = $(this).data('val');
        $hidden.val(val);
        $valSpan.text(val).addClass('selected');
        $dropdown.removeClass('open');
    });
}

$(document).on('click', function (e) {
    if (!$(e.target).closest('.bank-picker').length) {
        $('.bank-picker-dropdown').removeClass('open');
    }
});

function resetBankPicker(valSpanId, hiddenId) {
    $('#' + valSpanId).text('Chọn ngân hàng').removeClass('selected');
    $('#' + hiddenId).val('');
}

$(function () {
    initBankPicker('c-bankpicker-display', 'c-bankpicker-val', 'c-bankpicker-dropdown', 'c-bankpicker-list', 'c-bankname');
    initBankPicker('c2-bankpicker-display', 'c2-bankpicker-val', 'c2-bankpicker-dropdown', 'c2-bankpicker-list', 'c2-bankname');
    initBankPicker('bk-bankpicker-display', 'bk-bankpicker-val', 'bk-bankpicker-dropdown', 'bk-bankpicker-list', 'bk-name');
});

// ===================== LOCATION CASCADE (chỉ cho Create modal) =====================
let provincesLoaded = false;

$(function () {
    // Đăng ký sự kiện cascade cho c-province / c-district
    $(document).on('change', '#c-province', function () {
        $('#c-district').html('<option value="">— Chọn Quận/Huyện —</option>').prop('disabled', true);
        $('#c-ward').html('<option value="">— Chọn Phường/Xã —</option>').prop('disabled', true);
        const pid = $(this).val();
        if (!pid) return;
        $.ajax({
            url: SUP_BASE + '/GetDistricts',
            data: { provinceId: pid },
            success: function (data) {
                data.forEach(d => $('#c-district').append(`<option value="${d.code}">${d.name}</option>`));
                $('#c-district').prop('disabled', false);
            },
            error: function (xhr) {
                console.error('Lỗi tải Quận/Huyện:', xhr.status, xhr.responseText);
                supToast('Lỗi tải dữ liệu Quận/Huyện', 'error');
            }
        });
    });

    $(document).on('change', '#c-district', function () {
        $('#c-ward').html('<option value="">— Chọn Phường/Xã —</option>').prop('disabled', true);
        const did = $(this).val();
        if (!did) return;
        $.ajax({
            url: SUP_BASE + '/GetWards',
            data: { districtId: did },
            success: function (data) {
                data.forEach(w => $('#c-ward').append(`<option value="${w.code}">${w.name}</option>`));
                $('#c-ward').prop('disabled', false);
            },
            error: function (xhr) {
                console.error('Lỗi tải Phường/Xã:', xhr.status, xhr.responseText);
                supToast('Lỗi tải dữ liệu Phường/Xã', 'error');
            }
        });
    });
});

function loadProvinces() {
    if (provincesLoaded) return;
    $('#c-province').html('<option value="">— Đang tải... —</option>');
    $.ajax({
        url: SUP_BASE + '/GetProvinces',
        success: function (data) {
            $('#c-province').html('<option value="">— Chọn Tỉnh/Thành phố —</option>');
            if (data && data.length > 0) {
                data.forEach(p => $('#c-province').append(`<option value="${p.code}">${p.name}</option>`));
                provincesLoaded = true;
            } else {
                console.warn('GetProvinces trả về rỗng');
                supToast('Không có dữ liệu tỉnh/thành trong hệ thống', 'error');
            }
        },
        error: function (xhr) {
            console.error('Lỗi tải Tỉnh/Thành:', xhr.status, xhr.responseText);
            $('#c-province').html('<option value="">— Lỗi tải dữ liệu —</option>');
            supToast('Lỗi tải danh sách tỉnh thành', 'error');
        }
    });
}

// ===================== SECONDARY ITEMS — Create modal =====================
let cExtraPhones = [];
let cExtraBanks = [];
let cExtraContacts = [];

// ── Render phones ──
function renderCExtraPhones() {
    if (cExtraPhones.length === 0) { $('#c-phone-extra-list').html(''); return; }
    let html = '<div class="sup-extra-chips">';
    cExtraPhones.forEach((p, i) => {
        html += `<span class="sup-extra-chip">
            <span class="tag-sub">Phụ</span>
            <span class="sup-chip-val">${p}</span>
            <i class="fa fa-xmark sup-chip-del extra-del-phone" data-idx="${i}"></i>
        </span>`;
    });
    html += '</div>';
    $('#c-phone-extra-list').html(html);
}

// ── Render banks ──
function renderCExtraBanks() {
    if (cExtraBanks.length === 0) { $('#c-bank-extra-list').html(''); return; }
    let html = '<table class="sup-extra-table"><tbody>';
    cExtraBanks.forEach((b, i) => {
        html += `<tr>
            <td><span class="tag-sub">Phụ</span></td>
            <td>${b.bankName}</td>
            <td>${b.accountNumber}</td>
            <td>${b.accountHolder}</td>
            <td><i class="fa fa-trash-can sup-chip-del extra-del-bank" data-idx="${i}" title="Xoá"></i></td>
        </tr>`;
    });
    html += '</tbody></table>';
    $('#c-bank-extra-list').html(html);
}

// ── Render contacts ──
function renderCExtraContacts() {
    if (cExtraContacts.length === 0) { $('#c-contact-extra-list').html(''); return; }
    let html = '<table class="sup-extra-table"><tbody>';
    cExtraContacts.forEach((c, i) => {
        html += `<tr>
            <td><span class="tag-sub">Phụ</span></td>
            <td>${c.name}</td>
            <td>${c.position || '—'}</td>
            <td>${c.phone || '—'}</td>
            <td>${c.email || '—'}</td>
            <td><i class="fa fa-trash-can sup-chip-del extra-del-contact" data-idx="${i}" title="Xoá"></i></td>
        </tr>`;
    });
    html += '</tbody></table>';
    $('#c-contact-extra-list').html(html);
}

// ── Delete handlers ──
$(document).on('click', '.extra-del-phone', function () { cExtraPhones.splice($(this).data('idx'), 1); renderCExtraPhones(); });
$(document).on('click', '.extra-del-bank', function () { cExtraBanks.splice($(this).data('idx'), 1); renderCExtraBanks(); });
$(document).on('click', '.extra-del-contact', function () { cExtraContacts.splice($(this).data('idx'), 1); renderCExtraContacts(); });

// ── Add phone phụ ──
$('#btnCAddPhone').on('click', function () {
    const ph = $('#c-phone-extra').val().trim();
    if (!ph) { supToast('Nhập số điện thoại phụ', 'error'); return; }
    if (!isValidPhone(ph)) { supToast('SĐT phụ không hợp lệ (10-11 số, bắt đầu bằng 0)', 'error'); return; }
    cExtraPhones.push(ph);
    $('#c-phone-extra').val('');
    renderCExtraPhones();
});

// ── Bank phụ: show/hide form ──
$('#btnCShowBankForm').on('click', function () {
    $('#c-bank-extra-form').slideDown(180);
    $(this).hide();
});
$('#btnCCancelBank').on('click', function () {
    $('#c-bank-extra-form').slideUp(180);
    $('#btnCShowBankForm').show();
    resetBankPicker('c2-bankpicker-val', 'c2-bankname');
    $('#c2-accnumber,#c2-accholder').val('');
});

// ── Add bank phụ ──
$('#btnCAddBank').on('click', function () {
    const bankName = $('#c2-bankname').val().trim();
    const accountNumber = $('#c2-accnumber').val().trim();
    const accountHolder = $('#c2-accholder').val().trim();
    if (!bankName) { supToast('Vui lòng chọn ngân hàng phụ', 'error'); return; }
    if (!accountNumber) { supToast('Nhập số tài khoản', 'error'); return; }
    if (!isValidBankNumber(accountNumber)) { supToast('Số TK chỉ chứa chữ số (tối thiểu 6 số)', 'error'); return; }
    if (!accountHolder) { supToast('Nhập chủ tài khoản', 'error'); return; }
    cExtraBanks.push({ bankName, accountNumber, accountHolder });
    renderCExtraBanks();
    resetBankPicker('c2-bankpicker-val', 'c2-bankname');
    $('#c2-accnumber,#c2-accholder').val('');
    $('#c-bank-extra-form').slideUp(180);
    $('#btnCShowBankForm').show();
});

// ── Contact phụ: show/hide form ──
$('#btnCShowContactForm').on('click', function () {
    $('#c-contact-extra-form').slideDown(180);
    $(this).hide();
});
$('#btnCCancelContact').on('click', function () {
    $('#c-contact-extra-form').slideUp(180);
    $('#btnCShowContactForm').show();
    $('#c2-cname,#c2-cposition,#c2-cphone,#c2-cemail').val('');
});

// ── Add contact phụ ──
$('#btnCAddContact').on('click', function () {
    const name = $('#c2-cname').val().trim();
    const position = $('#c2-cposition').val().trim() || null;
    const phone = $('#c2-cphone').val().trim() || null;
    const email = $('#c2-cemail').val().trim() || null;
    if (!name) { supToast('Nhập tên người liên hệ phụ', 'error'); return; }
    if (!isValidName(name)) { supToast('Họ tên không được chứa số hay ký tự đặc biệt', 'error'); return; }
    if (phone && !isValidPhone(phone)) { supToast('SĐT phụ không hợp lệ (10-11 số, bắt đầu bằng 0)', 'error'); return; }
    if (email && !isValidEmail(email)) { supToast('Email không đúng định dạng', 'error'); return; }
    cExtraContacts.push({ name, position, phone, email });
    renderCExtraContacts();
    $('#c2-cname,#c2-cposition,#c2-cphone,#c2-cemail').val('');
    $('#c-contact-extra-form').slideUp(180);
    $('#btnCShowContactForm').show();
});

// ===================== CREATE WIZARD =====================
let currentWizardStep = 1;

function setWizardStep(step) {
    currentWizardStep = step;
    // Panels
    $('.sup-wizard-panel').removeClass('active');
    $('#createStep' + step).addClass('active');
    // Step indicators
    $('.sup-wizard-step').removeClass('active done');
    if (step === 1) {
        $('.sup-wizard-step[data-step="1"]').addClass('active');
        $('.sup-wizard-line').removeClass('done');
    } else {
        $('.sup-wizard-step[data-step="1"]').addClass('done');
        $('.sup-wizard-step[data-step="2"]').addClass('active');
        $('.sup-wizard-line').addClass('done');
    }
    // Buttons
    $('#btnWizardPrev').toggle(step > 1);
    $('#btnWizardNext').toggle(step < 2);
    $('#btnSaveCreate').toggle(step === 2);
    // Scroll to top of modal body
    $('#createModal .sup-modal-body').scrollTop(0);
}

// Validate step 1 before proceeding
function validateStep1() {
    const name = $('#c-name').val().trim();
    const phone = $('#c-phone').val().trim();
    if (!name) { supToast('Tên NCC không được để trống', 'error'); return false; }
    if (!phone) { supToast('SĐT chính không được để trống', 'error'); return false; }
    if (!isValidPhone(phone)) { supToast('SĐT chính không hợp lệ (10-11 số, bắt đầu bằng 0)', 'error'); return false; }
    const taxCode = ($('#c-taxcode').val() || '').trim();
    if (taxCode && !isValidTaxCode(taxCode)) { supToast('Mã số thuế không hợp lệ (10 hoặc 13 chữ số)', 'error'); return false; }
    return true;
}

$('#btnWizardNext').on('click', function () {
    if (currentWizardStep === 1 && validateStep1()) {
        setWizardStep(2);
    }
});

$('#btnWizardPrev').on('click', function () {
    if (currentWizardStep === 2) {
        setWizardStep(1);
    }
});

$('#btnCreate').on('click', function () {
    // Reset form cơ bản
    $('#c-name,#c-taxcode,#c-website,#c-street').val('');
    $('#c-phone,#c-phone-extra').val('');
    resetBankPicker('c-bankpicker-val', 'c-bankname');
    $('#c-accnumber,#c-accholder').val('');
    $('#c-cname,#c-cposition,#c-cphone,#c-cemail').val('');

    // Reset địa chỉ
    $('#c-district').html('<option value="">— Chọn Quận/Huyện —</option>').prop('disabled', true);
    $('#c-ward').html('<option value="">— Chọn Phường/Xã —</option>').prop('disabled', true);

    // Reset extra items
    cExtraPhones = []; cExtraBanks = []; cExtraContacts = [];
    renderCExtraPhones(); renderCExtraBanks(); renderCExtraContacts();

    // Ẩn các form phụ và hiện lại nút thêm
    $('#c-bank-extra-form,#c-contact-extra-form').hide();
    $('#btnCShowBankForm,#btnCShowContactForm').show();

    // Reset wizard về bước 1
    setWizardStep(1);

    // Tải danh sách tỉnh/thành (force reload mỗi lần mở)
    provincesLoaded = false;
    loadProvinces();

    // Tải mã NCC tự động
    $('#c-code-display').html('<i class="fa fa-spinner fa-spin"></i> Đang tạo mã...');
    $('#c-code').val('');
    fetch(SUP_BASE + '/GetNextCode')
        .then(r => r.ok ? r.json() : Promise.reject(r.statusText))
        .then(res => {
            if (res.success) {
                $('#c-code').val(res.code);
                $('#c-code-display').html('<i class="fa fa-tag"></i> ' + res.code);
            } else throw new Error(res.message);
        })
        .catch(e => {
            console.error('Lỗi tải mã:', e);
            $('#c-code-display').html('<i class="fa fa-exclamation-triangle" style="color:red"></i> Lỗi tạo mã');
            supToast('Không thể tạo mã NCC. Kiểm tra kết nối máy chủ.', 'error');
        });

    supOpen('createModal');
});

$('#btnSaveCreate').on('click', function () {
    const dto = {
        name: $('#c-name').val().trim(),
        taxCode: ($('#c-taxcode').val() || '').trim() || null,
        website: ($('#c-website').val() || '').trim() || null,

        provinceId: parseInt($('#c-province').val()) || null,
        districtId: parseInt($('#c-district').val()) || null,
        wardId: parseInt($('#c-ward').val()) || null,
        streetAddress: ($('#c-street').val() || '').trim() || null,

        primaryPhone: ($('#c-phone').val() || '').trim(),

        primaryBankName: ($('#c-bankname').val() || '').trim(),
        primaryAccountNumber: ($('#c-accnumber').val() || '').trim(),
        primaryAccountHolder: ($('#c-accholder').val() || '').trim(),

        primaryContactName: ($('#c-cname').val() || '').trim(),
        primaryContactPosition: ($('#c-cposition').val() || '').trim() || null,
        primaryContactPhone: ($('#c-cphone').val() || '').trim() || null,
        primaryContactEmail: ($('#c-cemail').val() || '').trim() || null,

        // Danh sách phụ (thu thập từ memory)
        additionalPhones: cExtraPhones,
        additionalBankAccounts: cExtraBanks,
        additionalContacts: cExtraContacts,
    };

    // --- Validate bắt buộc ---
    if (!dto.name) { supToast('Tên NCC không được để trống', 'error'); return; }
    if (!dto.primaryPhone) { supToast('SĐT chính không được để trống', 'error'); return; }
    if (!isValidPhone(dto.primaryPhone)) { supToast('SĐT chính không hợp lệ (10-11 số, bắt đầu bằng 0)', 'error'); return; }
    if (dto.taxCode && !isValidTaxCode(dto.taxCode)) { supToast('Mã số thuế không hợp lệ (10 hoặc 13 chữ số)', 'error'); return; }
    if (!dto.primaryBankName) { supToast('Vui lòng chọn ngân hàng chính', 'error'); return; }
    if (!dto.primaryAccountNumber) { supToast('Số tài khoản chính không được để trống', 'error'); return; }
    if (!isValidBankNumber(dto.primaryAccountNumber)) { supToast('Số tài khoản chỉ được chứa chữ số (tối thiểu 6 số)', 'error'); return; }
    if (!dto.primaryAccountHolder) { supToast('Chủ tài khoản chính không được để trống', 'error'); return; }
    if (!dto.primaryContactName) { supToast('Họ tên người liên hệ chính không được để trống', 'error'); return; }
    if (!isValidName(dto.primaryContactName)) { supToast('Họ tên không được chứa số hay ký tự đặc biệt', 'error'); return; }
    if (dto.primaryContactPhone && !isValidPhone(dto.primaryContactPhone)) { supToast('SĐT người liên hệ không hợp lệ', 'error'); return; }
    if (dto.primaryContactEmail && !isValidEmail(dto.primaryContactEmail)) { supToast('Email người liên hệ không đúng định dạng', 'error'); return; }

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
        supOpen('editModal');
    });
});

$('#btnSaveEdit').on('click', function () {
    const dto = {
        supplierId: parseInt($('#e-id').val()),
        name: ($('#e-name').val() || '').trim(),
        taxCode: ($('#e-taxcode').val() || '').trim() || null,
        website: ($('#e-website').val() || '').trim() || null,
        active: $('#e-active').val() === 'true',
        // Không gửi địa chỉ → service giữ nguyên địa chỉ cũ
    };

    if (!dto.name) { supToast('Tên NCC không được để trống', 'error'); return; }
    if (dto.taxCode && !isValidTaxCode(dto.taxCode)) { supToast('Mã số thuế không hợp lệ (10 hoặc 13 chữ số)', 'error'); return; }

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

// ===================== PHONES (tab chi tiết) =====================
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
            if (res.success) { supToast(res.message); $('#ph-number').val(''); loadDetail(currentSupplierId); }
            else supToast(res.message, 'error');
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

// ===================== BANKS (tab chi tiết) =====================
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
    const bankName = $('#bk-name').val().trim();
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

// ===================== CONTACTS (tab chi tiết) =====================
function renderContacts(contacts) {
    if (!contacts || contacts.length === 0) {
        $('#contactList').html('<div class="ct-empty"><i class="fa fa-user-slash"></i><span>Chưa có người liên hệ nào</span></div>');
        return;
    }

    let html = '';

    // Hiển thị contact chính trước
    const primary = contacts.filter(c => c.isPrimary);
    const secondary = contacts.filter(c => !c.isPrimary);

    primary.forEach(c => {
        html += `
        <div class="ct-card ct-card-primary">
            <div class="ct-card-badge">
                <span class="tag-primary"><i class="fa fa-star"></i> Chính</span>
            </div>
            <div class="ct-card-body">
                <div class="ct-card-name">
                    <i class="fa fa-user-tie ct-icon-primary"></i>
                    <strong>${c.name}</strong>
                    ${c.position ? `<span class="ct-position">${c.position}</span>` : ''}
                </div>
                <div class="ct-card-info">
                    ${c.phone ? `<span><i class="fa fa-phone"></i> ${c.phone}</span>` : ''}
                    ${c.email ? `<span><i class="fa fa-envelope"></i> ${c.email}</span>` : ''}
                </div>
            </div>
            <div class="ct-card-actions">
                <span class="ct-no-action-hint"><i class="fa fa-shield-halved"></i> Đầu mối chính</span>
            </div>
        </div>`;
    });

    secondary.forEach(c => {
        html += `
        <div class="ct-card ct-card-secondary">
            <div class="ct-card-badge">
                <span class="tag-sub">Phụ</span>
            </div>
            <div class="ct-card-body">
                <div class="ct-card-name">
                    <i class="fa fa-user ct-icon-secondary"></i>
                    <strong>${c.name}</strong>
                    ${c.position ? `<span class="ct-position">${c.position}</span>` : ''}
                </div>
                <div class="ct-card-info">
                    ${c.phone ? `<span><i class="fa fa-phone"></i> ${c.phone}</span>` : ''}
                    ${c.email ? `<span><i class="fa fa-envelope"></i> ${c.email}</span>` : ''}
                </div>
            </div>
            <div class="ct-card-actions">
                <button class="sup-btn sup-btn-xs btn-set-primary-contact" data-id="${c.supplierContactId}" title="Đặt làm liên hệ chính">
                    <i class="fa fa-star"></i> Đặt làm chính
                </button>
                <i class="fa fa-trash-can sup-icon-btn del-contact" data-id="${c.supplierContactId}" title="Xoá liên hệ này"></i>
            </div>
        </div>`;
    });

    $('#contactList').html(html);
}

// ── Toggle form thêm liên hệ phụ ──
$('#btnShowAddContactForm').on('click', function () {
    $('#ct-add-form').slideDown(180);
    $(this).hide();
    $('#ct-name').focus();
});
$('#btnCancelAddContact').on('click', function () {
    $('#ct-add-form').slideUp(180);
    $('#btnShowAddContactForm').show();
    $('#ct-name,#ct-position,#ct-phone,#ct-email').val('');
});

$('#btnAddContact').on('click', function () {
    const name = $('#ct-name').val().trim();
    const position = $('#ct-position').val().trim() || null;
    const phone = $('#ct-phone').val().trim() || null;
    const email = $('#ct-email').val().trim() || null;
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
                $('#ct-add-form').slideUp(180);
                $('#btnShowAddContactForm').show();
                loadDetail(currentSupplierId);
            } else supToast(res.message, 'error');
        }
    });
});

// ── Đặt làm liên hệ chính ──
$(document).on('click', '.btn-set-primary-contact', function () {
    const id = $(this).data('id');
    if (!confirm('Đặt người này làm đầu mối liên hệ chính?')) return;
    $.post(SUP_BASE + '/SetPrimaryContact', { supplierContactId: id }, function (res) {
        if (res.success) { supToast(res.message); loadDetail(currentSupplierId); }
        else supToast(res.message, 'error');
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
