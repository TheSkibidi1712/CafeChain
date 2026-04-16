/* ============================================================
   SUPPLIER MANAGEMENT — supplier.js
   Handles: Create, Edit, Toggle, Detail Tabs (Phones/Banks/Contacts)
   ============================================================ */

const SUP_BASE = '/Admin/AdminSupplier';

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

// ===================== CREATE =====================
$('#btnCreate').on('click', function () {
    // reset form
    $('#c-code,#c-name,#c-taxcode,#c-website,#c-address').val('');
    $('#c-phone').val('');
    resetBankPicker('c-bankpicker-val', 'c-bankname');
    $('#c-accnumber,#c-accholder').val('');
    $('#c-cname,#c-cposition,#c-cphone,#c-cemail').val('');
    supOpen('createModal');
});

$('#btnSaveCreate').on('click', function () {
    const dto = {
        code: $('#c-code').val().trim(),
        name: $('#c-name').val().trim(),
        taxCode: $('#c-taxcode').val().trim() || null,
        website: $('#c-website').val().trim() || null,
        address: $('#c-address').val().trim() || null,

        primaryPhone: $('#c-phone').val().trim(),

        primaryBankName:      $('#c-bankname').val().trim(),
        primaryAccountNumber: $('#c-accnumber').val().trim(),
        primaryAccountHolder: $('#c-accholder').val().trim(),

        primaryContactName:     $('#c-cname').val().trim(),
        primaryContactPosition: $('#c-cposition').val().trim() || null,
        primaryContactPhone:    $('#c-cphone').val().trim() || null,
        primaryContactEmail:    $('#c-cemail').val().trim() || null,
    };

    if (!dto.code || !dto.name || !dto.primaryPhone ||
        !dto.primaryBankName || !dto.primaryAccountNumber || !dto.primaryAccountHolder ||
        !dto.primaryContactName) {
        supToast('Vui lòng điền đầy đủ các trường bắt buộc (*)', 'error');
        return;
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
        $('#e-code').val(d.code);
        $('#e-name').val(d.name);
        $('#e-taxcode').val(d.taxCode || '');
        $('#e-website').val(d.website || '');
        $('#e-address').val(d.address || '');
        $('#e-active').val(d.active.toString());
        supOpen('editModal');
    });
});

$('#btnSaveEdit').on('click', function () {
    const dto = {
        supplierId: parseInt($('#e-id').val()),
        code:       $('#e-code').val().trim(),
        name:       $('#e-name').val().trim(),
        taxCode:    $('#e-taxcode').val().trim() || null,
        website:    $('#e-website').val().trim() || null,
        address:    $('#e-address').val().trim() || null,
        active:     $('#e-active').val() === 'true'
    };

    if (!dto.code || !dto.name) {
        supToast('Mã và Tên NCC không được để trống', 'error');
        return;
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

    if (!bankName || !accountNumber || !accountHolder) {
        supToast('Vui lòng điền đầy đủ thông tin ngân hàng', 'error'); return;
    }

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
