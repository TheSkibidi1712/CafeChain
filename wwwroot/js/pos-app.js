// ==========================================
// CAFECHAIN POS APPLICATION — PREMIUM JS
// ==========================================

// === STATE ===
let menuData = { categories: [], storeToppings: [] };
let cart = [];
let selectedCustomer = null;
let appliedVoucher = { code: '', discount: 0 };
let pointsToUse = 0;
let orderType = 1;
let activeShiftId = null;
let tempSelectedDrink = null;
let tempSelectedSize = null;
let tempSelectedToppings = [];
let ckCashAmount = 0;
let ckTotal = 0;
let pinValue = '';
let pinResolve = null;
let pinActionName = '';
let pinTargetId = 0;
let csExpectedCash = 0;
let successCountdownTimer = null;

// POS Terminal Lock
const POS_TERMINAL_KEY = 'CafeChain_POS_TerminalId';
function getPosTerminalId() {
    let id = localStorage.getItem(POS_TERMINAL_KEY);
    if (!id) { id = crypto.randomUUID(); localStorage.setItem(POS_TERMINAL_KEY, id); }
    return id;
}

const pastelClasses = ['pastel-orange','pastel-green','pastel-yellow','pastel-blue','pastel-pink','pastel-purple'];
const drinkIcons = ['fa-mug-hot','fa-coffee','fa-glass-water','fa-leaf','fa-lemon','fa-wine-glass','fa-blender'];
const fmt = (n) => new Intl.NumberFormat('vi-VN').format(n) + 'đ';

// ==========================================
// 1. INITIALIZATION
// ==========================================
$(document).ready(async function () {
    function updateNetworkUI() {
        const ind = document.getElementById('networkIndicator');
        const txt = document.getElementById('networkStatusText');
        const banner = document.getElementById('offlineBanner');
        if (ind) { navigator.onLine ? ind.classList.remove('offline') : ind.classList.add('offline'); }
        if (txt) { txt.textContent = navigator.onLine ? 'Online' : 'Offline'; }
        if (banner) { navigator.onLine ? banner.classList.remove('show') : banner.classList.add('show'); }
        if (navigator.onLine) syncOfflineOrders();
    }
    window.addEventListener('online', updateNetworkUI);
    window.addEventListener('offline', updateNetworkUI);
    updateNetworkUI();
    await registerTerminal();
    await checkActiveShift();
});

// === Terminal GUID Auto-Register ===
async function registerTerminal() {
    const terminalId = getPosTerminalId();
    try {
        const res = await $.ajax({
            url: '/Admin/AdminPOS/RegisterTerminal', type: 'POST', contentType: 'application/json',
            data: JSON.stringify({ terminalId: terminalId, name: 'Thiết bị POS ' + terminalId.substring(0, 5) })
        });
        const name = res.terminalName || ('POS ' + terminalId.substring(0, 5));
        $('#headerTerminalName').text(name);
    } catch {
        $('#headerTerminalName').text('POS ' + terminalId.substring(0, 5));
    }
}

// ==========================================
// 2. SHIFT MANAGEMENT
// ==========================================
async function checkActiveShift() {
    try {
        const res = await $.get('/Admin/AdminPOS/GetActiveShift');
        if (res.success && res.hasActiveShift) {
            activeShiftId = res.shift.shiftId;
            $('#shiftOverlay').hide();
            updateShiftUI(true, res.shift.startTime);
            await loadMenuData();
        } else { $('#shiftOverlay').show(); }
    } catch { $('#shiftOverlay').show(); }
}

function updateShiftUI(isOpen, startTime) {
    if (isOpen) {
        const t = startTime ? new Date(startTime).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }) : '';
        $('#headerShiftStatus').text(`Ca đang mở — ${t}`);
        $('#shiftBadge').css('background', 'rgba(34,197,94,0.3)');
        $('#btnCloseShift').show();
    } else {
        $('#headerShiftStatus').text('Chưa mở ca');
        $('#shiftBadge').css('background', 'rgba(255,255,255,0.1)');
        $('#btnCloseShift').hide();
    }
}

function setQuickCash(val, el) {
    $('#startingCashInput').val(val);
    $('.quick-cash button').removeClass('active');
    if (el) $(el).addClass('active');
}

$('#btnOpenShift').click(async function () {
    const cash = parseFloat($('#startingCashInput').val());
    if (!cash || cash < 0) {
        Swal.fire({ icon: 'warning', title: 'Thiếu thông tin', text: 'Vui lòng nhập số tiền lẻ đầu ca!', confirmButtonColor: '#F97316' });
        return;
    }
    $(this).prop('disabled', true).html('<i class="fas fa-spinner fa-spin"></i> Đang xử lý...');
    try {
        const posTerminalId = getPosTerminalId();
        const res = await $.ajax({ url: '/Admin/AdminPOS/OpenShift', type: 'POST', contentType: 'application/json', data: JSON.stringify({ startingCash: cash, posTerminalId: posTerminalId }) });
        if (res.success) {
            $('#shiftOverlay').fadeOut(300);
            updateShiftUI(true, new Date().toISOString());
            Swal.fire({ toast: true, position: 'top-end', icon: 'success', title: 'Mở ca thành công!', showConfirmButton: false, timer: 2000 });
            await loadMenuData();
        } else if (res.message && res.message.startsWith('LATE_OPENING_REQUIRES_BYPASS')) {
            // Late opening > 30 min — require supervisor PIN bypass
            const msgParts = res.message.split('|');
            const reason = msgParts.length > 1 ? msgParts[1] : 'Mở ca trễ hơn 30 phút';
            const bypass = await openPinModal('OPEN_SHIFT_LATE', 0, 'Mở ca trễ > 30 phút');
            if (bypass) {
                // Retry opening shift after bypass approved
                const retryRes = await $.ajax({ url: '/Admin/AdminPOS/OpenShift', type: 'POST', contentType: 'application/json', data: JSON.stringify({ startingCash: cash, posTerminalId: posTerminalId }) });
                if (retryRes.success) {
                    $('#shiftOverlay').fadeOut(300);
                    updateShiftUI(true, new Date().toISOString());
                    Swal.fire({ toast: true, position: 'top-end', icon: 'success', title: 'Mở ca thành công (bypass trễ)!', showConfirmButton: false, timer: 2000 });
                    await loadMenuData();
                } else {
                    Swal.fire({ icon: 'error', title: 'Không thể mở ca', html: `<div style="color:#ef4444">${retryRes.message}</div>`, confirmButtonColor: '#F97316' });
                }
            } else {
                Swal.fire({ icon: 'info', title: 'Đã hủy', text: reason, confirmButtonColor: '#F97316' });
            }
        } else {
            Swal.fire({ icon: 'error', title: 'Không thể mở ca', html: `<div style="color:#ef4444">${res.message}</div>`, confirmButtonColor: '#F97316' });
        }
    } catch (err) {
        Swal.fire('Lỗi', 'Mất kết nối máy chủ', 'error');
    }
    $(this).prop('disabled', false).html('<i class="fas fa-unlock"></i> Xác Nhận Mở Ca');
});

// ==========================================
// 3. MENU
// ==========================================
async function loadMenuData() {
    try {
        const res = await $.get('/Admin/AdminPOS/GetMenuData');
        if (res.success) { menuData = res; renderCategories(); renderProducts('all'); }
    } catch { console.error('Failed to load menu'); }
}

function renderCategories() {
    let html = '<div class="cat-tab active" data-cat="all" onclick="filterCategory(\'all\', this)">Tất cả</div>';
    menuData.categories.forEach(c => {
        html += `<div class="cat-tab" data-cat="${c.categoryId}" onclick="filterCategory(${c.categoryId}, this)">${c.name}</div>`;
    });
    $('#categoryTabs').html(html);
}

function renderProducts(catId) {
    const search = $('#searchInput').val().toLowerCase().trim();
    let drinks = [];
    menuData.categories.forEach(c => {
        if (catId === 'all' || c.categoryId === catId) {
            c.drinks.forEach(d => { if (!search || d.name.toLowerCase().includes(search)) drinks.push(d); });
        }
    });
    if (drinks.length === 0) {
        $('#productGrid').html('<div class="cart-empty" style="grid-column:1/-1"><i class="fas fa-mug-hot"></i><p>Không tìm thấy sản phẩm</p></div>');
        return;
    }
    let html = '';
    drinks.forEach((d, idx) => {
        const price = d.sizes.length > 0 ? d.sizes[0].price : 0;
        const priceLabel = d.sizes.length > 1 ? `Từ ${fmt(price)}` : fmt(price);
        const pastel = pastelClasses[idx % pastelClasses.length];
        const icon = drinkIcons[idx % drinkIcons.length];
        const imgHtml = d.imageUrl
            ? `<img class="card-img" src="${d.imageUrl}" alt="${d.name}" loading="lazy">`
            : `<div class="card-img-placeholder ${pastel}"><i class="fas ${icon}"></i></div>`;
        html += `<div class="product-card" onclick='selectDrink(${JSON.stringify(d).replace(/'/g, "\\'")})'>
            ${imgHtml}
            <div class="card-body"><div class="card-name">${d.name}</div><div class="card-price">${priceLabel}</div></div>
        </div>`;
    });
    $('#productGrid').html(html);
}

function filterCategory(catId, el) { $('.cat-tab').removeClass('active'); $(el).addClass('active'); renderProducts(catId); }
$('#searchInput').on('input', function () { const activeCat = $('.cat-tab.active').data('cat'); renderProducts(activeCat); });

// ==========================================
// 4. SIZE & TOPPING SELECTOR
// ==========================================
function selectDrink(drink) {
    tempSelectedDrink = drink; tempSelectedSize = null; tempSelectedToppings = [];
    if (drink.sizes.length <= 1 && (!drink.toppings || drink.toppings.length === 0)) {
        tempSelectedSize = drink.sizes[0] || null; addToCart(); return;
    }
    $('#sizeModalTitle').text(drink.name); $('#sizeModalSub').text('Chọn size và topping');
    let sizeHtml = '';
    drink.sizes.forEach((s, i) => {
        sizeHtml += `<div class="size-option ${i === 0 ? 'selected' : ''}" onclick="pickSize(${s.sizeId}, '${s.sizeName}', ${s.price}, this)" style="${i === 0 ? 'border-color:#F97316;background:#fff7ed;' : ''}">
            <span class="size-name">${s.sizeName}</span><span class="size-price">${fmt(s.price)}</span></div>`;
    });
    if (drink.sizes.length > 0) tempSelectedSize = drink.sizes[0];
    $('#sizeOptions').html(sizeHtml);
    const allToppings = drink.toppings && drink.toppings.length > 0 ? drink.toppings : menuData.storeToppings;
    if (allToppings && allToppings.length > 0) {
        let tpHtml = '';
        allToppings.forEach(t => {
            tpHtml += `<div class="topping-checkbox"><label><input type="checkbox" value="${t.toppingId}" data-name="${t.toppingName}" data-price="${t.price}" onchange="toggleTopping(this)"> ${t.toppingName}</label><span class="tp-price">+${fmt(t.price)}</span></div>`;
        });
        $('#toppingOptions').html(tpHtml); $('#toppingSection').show();
    } else { $('#toppingSection').hide(); }
    $('#sizeSelectorOverlay').fadeIn(150);
}

function pickSize(sizeId, sizeName, price, el) {
    tempSelectedSize = { sizeId, sizeName, price };
    $('.size-option').css({ 'border-color': '#e2e8f0', background: 'white' });
    $(el).css({ 'border-color': '#F97316', background: '#fff7ed' });
}
function toggleTopping(cb) {
    const id = parseInt(cb.value), name = cb.dataset.name, price = parseFloat(cb.dataset.price);
    if (cb.checked) tempSelectedToppings.push({ toppingId: id, toppingName: name, price });
    else tempSelectedToppings = tempSelectedToppings.filter(t => t.toppingId !== id);
}
function confirmAddToCart() { addToCart(); closeSizeSelector(); }
function closeSizeSelector() { $('#sizeSelectorOverlay').fadeOut(150); }

// ==========================================
// 5. CART
// ==========================================
function addToCart() {
    const drink = tempSelectedDrink, size = tempSelectedSize, toppings = [...tempSelectedToppings];
    const tpKey = toppings.map(t => t.toppingId).sort().join(',');
    const existing = cart.find(c => c.drinkId === drink.drinkId && (c.sizeId || 0) === (size?.sizeId || 0) && c.tpKey === tpKey);
    if (existing) { existing.quantity++; }
    else {
        const toppingTotal = toppings.reduce((s, t) => s + t.price, 0);
        cart.push({ drinkId: drink.drinkId, name: drink.name, sizeId: size?.sizeId || null, sizeName: size?.sizeName || '', basePrice: size?.price || 0, toppings, toppingTotal, unitPrice: (size?.price || 0) + toppingTotal, quantity: 1, note: '', tpKey });
    }
    renderCart();
    Swal.fire({ toast: true, position: 'top-end', icon: 'success', title: `Đã thêm ${drink.name}`, showConfirmButton: false, timer: 1000 });
}

function renderCart() {
    if (cart.length === 0) {
        $('#cartItems').html('<div class="cart-empty"><i class="fas fa-shopping-basket"></i><p>Chọn sản phẩm để bắt đầu</p></div>');
        $('#cartCount').text('0'); $('#btnCheckout').prop('disabled', true); updateSummary(); return;
    }
    let html = '';
    cart.forEach((item, idx) => {
        const tpText = item.toppings.map(t => t.toppingName).join(', ');
        const customization = `${item.sizeName}${tpText ? ' • ' + tpText : ''}`;
        const lineTotal = item.unitPrice * item.quantity;
        html += `<div class="cart-item">
            <button class="btn-remove" onclick="removeItem(${idx})"><i class="fas fa-trash"></i></button>
            <div class="item-top"><div class="item-name">${item.name}</div></div>
            <div class="item-customization">${customization}</div>
            <div class="item-bottom">
                <div class="qty-control"><button class="qty-btn" onclick="changeQty(${idx},-1)">−</button><span class="qty-val">${item.quantity}</span><button class="qty-btn" onclick="changeQty(${idx},1)">+</button></div>
                <div class="item-price">${fmt(lineTotal)}</div>
            </div></div>`;
    });
    $('#cartItems').html(html);
    $('#cartCount').text(cart.reduce((s, i) => s + i.quantity, 0));
    $('#btnCheckout').prop('disabled', false); updateSummary();
}

function changeQty(idx, delta) { cart[idx].quantity += delta; if (cart[idx].quantity <= 0) cart.splice(idx, 1); renderCart(); }
function removeItem(idx) { cart.splice(idx, 1); renderCart(); }

function updateSummary() {
    const subTotal = cart.reduce((s, i) => s + i.unitPrice * i.quantity, 0);
    const vDisc = appliedVoucher.discount || 0;
    const pDisc = pointsToUse * 1000;
    let total = subTotal - vDisc - pDisc; if (total < 0) total = 0;
    $('#sumSubTotal').text(fmt(subTotal));
    if (vDisc > 0) { $('#rowVoucher').show(); $('#sumVoucher').text('-' + fmt(vDisc)); } else { $('#rowVoucher').hide(); }
    if (pDisc > 0) { $('#rowPoints').show(); $('#sumPoints').text('-' + fmt(pDisc)); } else { $('#rowPoints').hide(); }
    $('#sumTotal').text(fmt(total));
}

// ==========================================
// 6. CUSTOMER & VOUCHER
// ==========================================
async function searchCustomer() {
    const phone = $('#customerPhone').val().trim();
    if (!phone) { Swal.fire({ icon: 'info', title: 'Nhập SĐT', text: 'Vui lòng nhập số điện thoại khách hàng', confirmButtonColor: '#F97316' }); return; }
    try {
        const res = await $.get('/Admin/AdminPOS/SearchCustomer', { phone });
        if (res.success) {
            selectedCustomer = res.customer;
            $('#custName').text(res.customer.fullName);
            $('#custPoints').text(res.customer.currentPoints.toLocaleString());
            const initials = res.customer.fullName.split(' ').map(w => w[0]).join('').substring(0, 2).toUpperCase();
            $('#custAvatar').text(initials);
            $('#customerBadge').css('display', 'flex');
            if (res.customer.currentPoints > 0) {
                const { value } = await Swal.fire({
                    title: 'Dùng điểm tích lũy?',
                    html: `Khách <b>${res.customer.fullName}</b> có <b>${res.customer.currentPoints.toLocaleString()}</b> điểm.<br>1 điểm = 1.000đ. Nhập số điểm muốn dùng:`,
                    input: 'number', inputAttributes: { min: 0, max: res.customer.currentPoints },
                    showCancelButton: true, cancelButtonText: 'Không dùng', confirmButtonText: 'Áp dụng', confirmButtonColor: '#F97316'
                });
                if (value && parseInt(value) > 0) { pointsToUse = Math.min(parseInt(value), res.customer.currentPoints); updateSummary(); }
            }
        } else { Swal.fire({ icon: 'info', title: 'Không tìm thấy', text: res.message, confirmButtonColor: '#F97316' }); }
    } catch { Swal.fire('Lỗi', 'Không thể tìm khách hàng', 'error'); }
}
function clearCustomer() { selectedCustomer = null; pointsToUse = 0; $('#customerBadge').css('display', 'none'); $('#customerPhone').val(''); updateSummary(); }

async function applyVoucher() {
    const code = $('#voucherCode').val().trim();
    if (!code) { Swal.fire({ icon: 'info', title: 'Nhập mã', text: 'Vui lòng nhập mã voucher', confirmButtonColor: '#F97316' }); return; }
    const subTotal = cart.reduce((s, i) => s + i.unitPrice * i.quantity, 0);
    try {
        const res = await $.ajax({ url: '/api/Pos/validate-voucher', type: 'POST', contentType: 'application/json', data: JSON.stringify({ code, customerId: selectedCustomer?.customerId || 0, subTotal }) });
        if (res.success) { appliedVoucher = { code, discount: res.discountAmount }; updateSummary(); Swal.fire({ toast: true, position: 'top-end', icon: 'success', title: `Giảm ${fmt(res.discountAmount)}`, showConfirmButton: false, timer: 2000 }); }
        else { Swal.fire({ icon: 'error', title: 'Voucher không hợp lệ', text: res.message, confirmButtonColor: '#F97316' }); }
    } catch { Swal.fire('Lỗi', 'Không thể kiểm tra voucher', 'error'); }
}
function setOrderType(type, el) { orderType = type; $('.order-type-btn').removeClass('active'); $(el).addClass('active'); }

// ==========================================
// 7. CHECKOUT MODAL
// ==========================================
function openCheckoutModal() {
    if (cart.length === 0) return;
    const subTotal = cart.reduce((s, i) => s + i.unitPrice * i.quantity, 0);
    ckTotal = Math.max(0, subTotal - (appliedVoucher.discount || 0) - (pointsToUse * 1000));
    ckCashAmount = 0;
    $('#ckTotalBadge').text(fmt(ckTotal));
    $('#ckCashDisplay').text('0đ');
    $('#ckSumTotal').text(fmt(ckTotal)); $('#ckSumReceived').text('0đ'); $('#ckSumChange').text('0đ');
    $('#btnConfirmPayment').prop('disabled', true);
    $('#checkoutOverlay').addClass('active');
}
function closeCheckoutModal() { $('#checkoutOverlay').removeClass('active'); }
let currentCkTab = 'cash';
function switchCkTab(tab, el) {
    $('.ck-tab').removeClass('active'); $(el).addClass('active');
    currentCkTab = tab;
    // Show/hide panels based on tab
    if (tab === 'split') {
        $('#ckCashPanel').hide(); $('#ckSplitPanel').show();
        $('#splitTotalLabel').text(fmt(ckTotal));
        $('#splitCashAmount').val(''); $('#splitQrAmount').val('');
        updateSplitTotal();
    } else {
        $('#ckCashPanel').show(); $('#ckSplitPanel').hide();
    }
}

// Split Payment Validation
function updateSplitTotal() {
    const cashAmt = parseFloat($('#splitCashAmount').val()) || 0;
    const qrAmt = parseFloat($('#splitQrAmount').val()) || 0;
    const splitSum = cashAmt + qrAmt;
    const diff = splitSum - ckTotal;
    const valEl = $('#splitValidation');
    if (splitSum === 0) {
        valEl.html('<span style="color:#94a3b8;">Nhập số tiền cho từng phương thức</span>').css('background', '#f8fafc');
        $('#btnConfirmPayment').prop('disabled', true);
    } else if (Math.abs(diff) < 1) {
        valEl.html('<span style="color:#16a34a;">✓ Tổng khớp chính xác!</span>').css('background', '#f0fdf4');
        $('#btnConfirmPayment').prop('disabled', false);
    } else if (diff > 0) {
        valEl.html(`<span style="color:#f59e0b;">⚠ Thừa ${fmt(diff)}</span>`).css('background', '#fffbeb');
        $('#btnConfirmPayment').prop('disabled', false);
    } else {
        valEl.html(`<span style="color:#ef4444;">✗ Còn thiếu ${fmt(Math.abs(diff))}</span>`).css('background', '#fef2f2');
        $('#btnConfirmPayment').prop('disabled', true);
    }
}

function setCkAmount(val) {
    if (val === 'exact') ckCashAmount = ckTotal;
    else if (val === 0) ckCashAmount = 0;
    else ckCashAmount = val;
    updateCkDisplay();
}
function numpadInput(key) {
    if (key === 'back') ckCashAmount = Math.floor(ckCashAmount / 10);
    else ckCashAmount = ckCashAmount * 10 + parseInt(key);
    updateCkDisplay();
}
function updateCkDisplay() {
    $('#ckCashDisplay').text(fmt(ckCashAmount));
    $('#ckSumReceived').text(fmt(ckCashAmount));
    const change = ckCashAmount - ckTotal;
    $('#ckSumChange').text(change >= 0 ? fmt(change) : '-' + fmt(Math.abs(change)));
    $('#ckSumChange').css('color', change >= 0 ? '#16a34a' : '#ef4444');
    $('#btnConfirmPayment').prop('disabled', ckCashAmount < ckTotal);
}

async function confirmPayment() {
    const subTotal = cart.reduce((s, i) => s + i.unitPrice * i.quantity, 0);

    // Build payment lines based on current tab
    let payments = [];
    let receivedAmount = 0;
    if (currentCkTab === 'split') {
        const cashAmt = parseFloat($('#splitCashAmount').val()) || 0;
        const qrAmt = parseFloat($('#splitQrAmount').val()) || 0;
        if (cashAmt + qrAmt < ckTotal) return;
        if (cashAmt > 0) payments.push({ paymentMethodId: 1, amount: cashAmt });
        if (qrAmt > 0) payments.push({ paymentMethodId: 2, amount: qrAmt });
        receivedAmount = cashAmt + qrAmt;
    } else if (currentCkTab === 'qr') {
        payments = [{ paymentMethodId: 2, amount: ckTotal }];
        receivedAmount = ckTotal;
    } else {
        if (ckCashAmount < ckTotal) return;
        payments = [{ paymentMethodId: 1, amount: ckTotal }];
        receivedAmount = ckCashAmount;
    }

    const dto = {
        items: cart.map(c => ({ drinkId: c.drinkId, sizeId: c.sizeId, quantity: c.quantity, note: c.note, toppings: c.toppings.map(t => ({ toppingId: t.toppingId })) })),
        customerId: selectedCustomer?.customerId || null,
        voucherCode: appliedVoucher.code || null,
        pointsUsed: pointsToUse,
        payments: payments,
        orderTypeId: orderType,
        receivedAmount: receivedAmount,
        note: ''
    };

    // Offline mode
    if (!navigator.onLine) {
        saveOfflineOrder(dto); closeCheckoutModal();
        showSuccessModal({ orderId: 'OFFLINE', subTotal, voucherDiscount: appliedVoucher.discount || 0, pointDiscount: pointsToUse * 1000, total: ckTotal, receivedAmount: receivedAmount, changeAmount: receivedAmount - ckTotal, earnedPoints: 0 });
        return;
    }

    try {
        Swal.fire({ title: 'Đang xử lý...', allowOutsideClick: false, didOpen: () => Swal.showLoading() });
        const res = await $.ajax({ url: '/Admin/AdminPOS/CommitOrder', type: 'POST', contentType: 'application/json', data: JSON.stringify(dto) });
        Swal.close();
        if (res.success) { closeCheckoutModal(); showSuccessModal(res); }
        else { Swal.fire({ icon: 'error', title: 'Lỗi', text: res.message, confirmButtonColor: '#F97316' }); }
    } catch { Swal.close(); Swal.fire('Lỗi', 'Mất kết nối máy chủ', 'error'); }
}

// ==========================================
// 8. SUCCESS MODAL
// ==========================================
function showSuccessModal(res) {
    const change = res.changeAmount || 0;
    const orderId = res.orderId;
    const now = new Date();
    $('#successOrderBadge').text(`ĐƠN #${orderId}`);
    $('#successOrderTime').text(now.toLocaleDateString('vi-VN') + ' • ' + now.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' }));
    // Items
    let itemsHtml = '';
    cart.forEach(item => {
        itemsHtml += `<div class="order-item-row"><span><span class="item-qty">${item.quantity}x</span> ${item.name} ${item.sizeName ? '(' + item.sizeName + ')' : ''}</span><span>${fmt(item.unitPrice * item.quantity)}</span></div>`;
    });
    $('#successOrderItems').html(itemsHtml);
    // Financial
    let finHtml = `<div class="fin-row"><span>Tạm tính</span><span>${fmt(res.subTotal)}</span></div>`;
    if (res.voucherDiscount > 0) finHtml += `<div class="fin-row"><span>Voucher giảm</span><span style="color:#16a34a">-${fmt(res.voucherDiscount)}</span></div>`;
    if (res.pointDiscount > 0) finHtml += `<div class="fin-row"><span>Điểm tích lũy</span><span style="color:#16a34a">-${fmt(res.pointDiscount)}</span></div>`;
    finHtml += `<div class="fin-row fin-total"><span>Tổng thanh toán</span><span>${fmt(res.total)}</span></div>`;
    $('#successFinSummary').html(finHtml);
    // Change
    $('#successCashPaid').text(fmt(res.receivedAmount || ckCashAmount));
    $('#successChangeAmount').text(fmt(change));
    // Loyalty
    const earned = res.earnedPoints || Math.floor((res.total || 0) / 10000);
    if (earned > 0 && selectedCustomer) { $('#successLoyalty').show(); $('#successLoyaltyText').text(`Khách hàng tích được +${earned} điểm từ đơn hàng này`); }
    else { $('#successLoyalty').hide(); }
    // Countdown
    let sec = 10;
    $('#countdownSec').text(sec);
    if (successCountdownTimer) clearInterval(successCountdownTimer);
    successCountdownTimer = setInterval(() => { sec--; $('#countdownSec').text(sec); if (sec <= 0) { clearInterval(successCountdownTimer); nextOrder(); } }, 1000);
    $('#successOverlay').addClass('active');
}

function nextOrder() {
    if (successCountdownTimer) clearInterval(successCountdownTimer);
    $('#successOverlay').removeClass('active');
    cart = []; selectedCustomer = null; appliedVoucher = { code: '', discount: 0 }; pointsToUse = 0;
    $('#voucherCode').val(''); $('#customerPhone').val(''); $('#customerBadge').css('display', 'none');
    renderCart();
}
function printReceipt() {
    // Build bar ticket HTML for thermal printer
    const orderId = $('#successOrderBadge').text();
    const time = $('#successOrderTime').text();
    let ticketHtml = `<div style="font-family:monospace;width:300px;padding:10px;font-size:12px;">`;
    ticketHtml += `<div style="text-align:center;font-weight:bold;font-size:16px;border-bottom:2px dashed #333;padding-bottom:8px;margin-bottom:8px;">☕ CAFECHAIN - PHIẾU PHA CHẾ</div>`;
    ticketHtml += `<div style="text-align:center;margin-bottom:8px;">${orderId} • ${time}</div>`;
    ticketHtml += `<div style="border-bottom:1px dashed #999;margin-bottom:8px;"></div>`;
    cart.forEach((item, idx) => {
        const tpText = item.toppings.map(t => t.toppingName).join(', ');
        ticketHtml += `<div style="margin-bottom:6px;">`;
        ticketHtml += `<div style="font-weight:bold;font-size:14px;">${idx + 1}. ${item.name} x${item.quantity}</div>`;
        ticketHtml += `<div style="padding-left:14px;color:#555;">Size: ${item.sizeName || 'Mặc định'}</div>`;
        if (tpText) ticketHtml += `<div style="padding-left:14px;color:#555;">Topping: ${tpText}</div>`;
        if (item.note) ticketHtml += `<div style="padding-left:14px;color:#d97706;">📝 ${item.note}</div>`;
        ticketHtml += `</div>`;
    });
    ticketHtml += `<div style="border-top:2px dashed #333;margin-top:8px;padding-top:8px;text-align:center;font-size:11px;color:#666;">Barista vui lòng pha chế theo đơn</div>`;
    ticketHtml += `</div>`;

    // Open print window
    const printWin = window.open('', '_blank', 'width=350,height=500');
    printWin.document.write(`<html><head><title>Phiếu pha chế</title><style>@media print{body{margin:0;}@page{margin:5mm;}}</style></head><body>${ticketHtml}</body></html>`);
    printWin.document.close();
    printWin.focus();
    setTimeout(() => { printWin.print(); printWin.close(); }, 300);
}

// ==========================================
// 9. CLOSE SHIFT MODAL
// ==========================================
$('#btnCloseShift').click(async function () {
    try {
        const res = await $.get('/Admin/AdminPOS/GetCloseShiftData');
        if (!res.success) { Swal.fire({ icon: 'error', title: 'Lỗi', text: res.message }); return; }
        const s = res.shift;
        $('#csStartTime').text(s.startTime);
        $('#csCurrentTime').text(s.currentTime);
        $('#csDuration').text(`${s.durationHours} giờ ${s.durationMinutes} phút`);
        $('#csTotalOrders').text(`${s.totalOrders} đơn`);
        $('#csStartingCash').text(fmt(s.startingCash));
        $('#csCashSales').text(fmt(s.totalCashSales));
        $('#csQrSales').text(fmt(s.totalQrSales));
        $('#csCashChange').text('-' + fmt(s.cashChangeGiven));
        $('#csNetRevenue').text(fmt(s.netRevenue));
        csExpectedCash = s.expectedEndingCash;
        $('#csActualCash').val('');
        $('#discrepancyBox').removeClass('show positive');
        $('#discReason').val('');
        $('#closeShiftOverlay').addClass('active');
    } catch { Swal.fire('Lỗi', 'Không thể tải dữ liệu ca', 'error'); }
});

function closeCloseShiftModal() { $('#closeShiftOverlay').removeClass('active'); }
function setCloseCash(val) { $('#csActualCash').val(val); checkDiscrepancy(); }

function checkDiscrepancy() {
    const actual = parseFloat($('#csActualCash').val()) || 0;
    const diff = actual - csExpectedCash;
    if (actual > 0 && diff !== 0) {
        const box = $('#discrepancyBox');
        box.addClass('show');
        if (diff > 0) { box.addClass('positive').find('.disc-title').html('<i class="fas fa-check-circle"></i> Thừa tiền két!'); }
        else { box.removeClass('positive').find('.disc-title').html('<i class="fas fa-exclamation-triangle"></i> Phát hiện chênh lệch két tiền!'); }
        $('#discExpected').text(fmt(csExpectedCash));
        $('#discActual').text(fmt(actual));
        $('#discAmount').text((diff > 0 ? '+' : '') + fmt(diff));
    } else { $('#discrepancyBox').removeClass('show positive'); }
    checkCloseShiftReady();
}

function checkCloseShiftReady() {
    const actual = parseFloat($('#csActualCash').val()) || 0;
    const diff = actual - csExpectedCash;
    const needsReason = actual > 0 && diff !== 0;
    const hasReason = $('#discReason').val().trim().length > 0;
    $('#btnConfirmCloseShift').prop('disabled', actual <= 0 || (needsReason && !hasReason));
}

async function confirmCloseShift() {
    const actual = parseFloat($('#csActualCash').val()) || 0;
    const reason = $('#discReason').val().trim();
    try {
        const res = await $.ajax({ url: '/Admin/AdminPOS/CloseShift', type: 'POST', contentType: 'application/json', data: JSON.stringify({ actualEndingCash: actual, discrepancyReason: reason }) });
        if (res.success) {
            closeCloseShiftModal();
            await Swal.fire({ icon: 'success', title: 'Đóng ca thành công!', text: res.message, confirmButtonColor: '#F97316' });
            window.location.href = '/Admin/Dashboard';
        } else {
            if (res.message.includes('chênh lệch') && !reason) {
                checkDiscrepancy();
                Swal.fire({ icon: 'warning', title: 'Cần nhập lý do', text: res.message, confirmButtonColor: '#F97316' });
            } else { Swal.fire({ icon: 'error', title: 'Lỗi', text: res.message, confirmButtonColor: '#F97316' }); }
        }
    } catch { Swal.fire('Lỗi', 'Mất kết nối', 'error'); }
}

// ==========================================
// 10. PIN AUTH
// ==========================================
function openPinModal(actionName, targetId, actionLabel) {
    pinValue = ''; pinActionName = actionName; pinTargetId = targetId;
    updatePinDots();
    $('#pinActionBadge').html(`⊘ ${actionLabel}`);
    $('#pinWarning').text('⚠ Còn 5 lần thử. Sai 5 lần sẽ khóa 15 phút.');
    $('#btnPinConfirm').removeClass('ready');
    $('#pinOverlay').addClass('active');
    return new Promise(resolve => { pinResolve = resolve; });
}
function closePinModal() { $('#pinOverlay').removeClass('active'); if (pinResolve) { pinResolve(null); pinResolve = null; } }
function pinInput(key) {
    if (key === 'back') { pinValue = pinValue.slice(0, -1); }
    else if (pinValue.length < 4) { pinValue += key; }
    updatePinDots();
    if (pinValue.length === 4) $('#btnPinConfirm').addClass('ready');
    else $('#btnPinConfirm').removeClass('ready');
}
function updatePinDots() {
    for (let i = 0; i < 4; i++) {
        if (i < pinValue.length) $(`#pinDot${i}`).addClass('filled');
        else $(`#pinDot${i}`).removeClass('filled');
    }
}
async function submitPin() {
    if (pinValue.length !== 4) return;
    try {
        const res = await $.ajax({ url: '/Admin/AdminPOS/AuthorizeSupervisor', type: 'POST', contentType: 'application/json',
            data: JSON.stringify({ pin: pinValue, actionName: pinActionName, targetId: pinTargetId, reason: '' }) });
        if (res.success) {
            $('#pinOverlay').removeClass('active');
            if (pinResolve) { pinResolve(true); pinResolve = null; }
            Swal.fire({ toast: true, position: 'top-end', icon: 'success', title: res.message, showConfirmButton: false, timer: 2000 });
        } else {
            pinValue = ''; updatePinDots(); $('#btnPinConfirm').removeClass('ready');
            const remain = res.remainingAttempts != null ? res.remainingAttempts : '?';
            $('#pinWarning').text(`⚠ ${res.message}`);
        }
    } catch { Swal.fire('Lỗi', 'Mất kết nối', 'error'); }
}

// ==========================================
// 11. OFFLINE MODE
// ==========================================
const OFFLINE_KEY = 'CafeChain_Offline_Orders';
function saveOfflineOrder(dto) {
    const orders = JSON.parse(localStorage.getItem(OFFLINE_KEY) || '[]');
    orders.push({ ...dto, offlineTimestamp: new Date().toISOString() });
    localStorage.setItem(OFFLINE_KEY, JSON.stringify(orders));
    Swal.fire({ toast: true, position: 'top-end', icon: 'warning', title: 'Đơn hàng đã lưu offline', showConfirmButton: false, timer: 2000 });
}
async function syncOfflineOrders() {
    const orders = JSON.parse(localStorage.getItem(OFFLINE_KEY) || '[]');
    if (orders.length === 0) return;
    try {
        // Use batch sync API instead of looping CommitOrder individually
        const syncPayload = orders.map(o => ({
            orderTypeId: o.orderTypeId || 1,
            receivedAmount: o.receivedAmount || 0,
            note: o.note || '',
            details: (o.items || []).map(i => ({ itemId: i.drinkId, quantity: i.quantity }))
        }));
        const res = await $.ajax({
            url: '/Admin/AdminPOS/SyncOfflineOrders', type: 'POST',
            contentType: 'application/json', data: JSON.stringify(syncPayload)
        });
        localStorage.removeItem(OFFLINE_KEY);
        Swal.fire({ toast: true, position: 'top-end', icon: 'success', title: res.message || `Đã đồng bộ ${orders.length} đơn offline`, showConfirmButton: false, timer: 3000 });
    } catch { console.error('Offline sync failed'); }
}

// ==========================================
// 12. QUICK REGISTER CUSTOMER
// ==========================================
function openQuickRegModal() {
    const phone = $('#customerPhone').val().trim();
    if (!phone) {
        Swal.fire({ icon: 'info', title: 'Nhập SĐT trước', text: 'Vui lòng nhập số điện thoại khách hàng cần đăng ký trước khi nhấn nút +', confirmButtonColor: '#F97316' });
        return;
    }
    $('#quickRegPhone').val(phone);
    $('#quickRegName').val('');
    $('#quickRegDob').val('');
    $('#quickRegOverlay').addClass('active');
    setTimeout(() => $('#quickRegName').focus(), 200);
}
function closeQuickRegModal() { $('#quickRegOverlay').removeClass('active'); }

async function submitQuickRegister() {
    const phone = $('#quickRegPhone').val().trim();
    const fullName = $('#quickRegName').val().trim();
    const dob = $('#quickRegDob').val() || null;
    if (!fullName) {
        Swal.fire({ icon: 'warning', title: 'Thiếu thông tin', text: 'Vui lòng nhập họ và tên khách hàng!', confirmButtonColor: '#F97316' });
        return;
    }
    try {
        const res = await $.ajax({
            url: '/Admin/AdminPOS/RegisterCustomer', type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify({ phone, fullName, dateOfBirth: dob })
        });
        if (res.success) {
            closeQuickRegModal();
            // Auto-assign the new customer to the cart
            selectedCustomer = res.customer || res.data;
            if (selectedCustomer) {
                $('#custName').text(selectedCustomer.fullName);
                $('#custPoints').text((selectedCustomer.currentPoints || 0).toLocaleString());
                const initials = selectedCustomer.fullName.split(' ').map(w => w[0]).join('').substring(0, 2).toUpperCase();
                $('#custAvatar').text(initials);
                $('#customerBadge').css('display', 'flex');
            }
            Swal.fire({ toast: true, position: 'top-end', icon: 'success', title: res.message || 'Đăng ký thành công!', showConfirmButton: false, timer: 2000 });
        } else {
            Swal.fire({ icon: 'error', title: 'Đăng ký thất bại', text: res.message, confirmButtonColor: '#F97316' });
        }
    } catch {
        Swal.fire('Lỗi', 'Mất kết nối máy chủ', 'error');
    }
}
