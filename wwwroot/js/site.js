// site.js

function scrollSlider(button, direction) {
    const container = button.closest('.product-slider-container').querySelector('.product-slider');
    const firstItem = container.querySelector('.slider-item');

    // 1. Tính toán khoảng cách cuộn của 1 card
    let scrollAmount = firstItem ? (firstItem.offsetWidth + 24) : 300;

    // 2. Lấy các thông số vị trí hiện tại
    const currentScroll = container.scrollLeft; // Vị trí hiện tại
    const maxScroll = container.scrollWidth - container.clientWidth; // Vị trí tối đa có thể cuộn

    if (direction === 1) {
        // --- LOGIC BẤM TIẾP (NEXT) ---
        // Nếu vị trí hiện tại đã gần sát nút cuối (sai số 5px)
        if (currentScroll >= maxScroll - 5) {
            container.scrollTo({ left: 0, behavior: 'smooth' }); // Quay về đầu
        } else {
            container.scrollBy({ left: scrollAmount, behavior: 'smooth' }); // Cuộn tiếp
        }
    } else {
        // --- LOGIC BẤM LÙI (PREV) ---
        // Nếu đang ở sát đầu trang
        if (currentScroll <= 5) {
            container.scrollTo({ left: maxScroll, behavior: 'smooth' }); // Nhảy xuống cuối
        } else {
            container.scrollBy({ left: -scrollAmount, behavior: 'smooth' }); // Cuộn lùi
        }
    }
}

function addToCart(drinkId) {
    $.post('/Cart/AddToCart', { id: drinkId }, function (data) {
        if (data.success) {
            // Cập nhật số lượng trên icon giỏ hàng ở Header
            const badge = $('#cart-badge');
            badge.text(data.totalCount);
            badge.removeClass('d-none');

            alert("Đã thêm món vào giỏ hàng thành công!");
        }
    });
}