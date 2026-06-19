# Web Design & UI Backbone Rules

All frontend components, layout structures, and styling systems implemented for the CafeChain StaffHub and POS systems must align with modern web design standards to deliver a premium, high-interaction experience.

---

## 1. Visual & Aesthetic Architecture
Aesthetics are a core engineering priority. Simple, default, or raw browser inputs are UNACCEPTABLE. All views must utilize our curated premium theme.

### A. Color & Token System
We avoid generic browser primary colors. We rely on a cohesive HSL-based palette that represents premium coffee styling combined with technical excellence:
- **Background (Light mode)**: Elegant warm creams and whites (`#ffffff`, `#faf8f6`).
- **Background (Dark mode)**: High-tech obsidian blue (`#1a1a2e` gradient to `#0f3460`).
- **Accent/Brand**: Roasted terracotta orange (`#e8643c`, HSL `12, 80%, 57%`).
- **Accent Light**: Smooth peach glow (`#fff3ef`).
- **Success**: Emerald green (`#22c55e`).
- **Warning**: Warm amber (`#f59e0b`).
- **Danger**: Crimson red (`#ef4444`).
- **Text Primary**: Charcoal slate (`#1e293b`).
- **Text Muted**: Soft ash (`#94a3b8`).

### B. Typography & Iconography
- **Font Face**: Google Font family **Inter** or **Outfit** as the default system standard. Set hierarchy:
```css
font-family: 'Inter', -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
```
- **Icons**: Utilize **FontAwesome 6** (solid, regular, brand sets) for clear, intuitive visual markers on action states.

### C. Glassmorphism Design
StaffHub and POS components must feel lightweight, layered, and premium. Use subtle transparency, borders, and backdrop filters for floating panels:
```css
.premium-card {
    background: rgba(255, 255, 255, 0.05);
    backdrop-filter: blur(12px);
    -webkit-backdrop-filter: blur(12px);
    border: 1px solid rgba(255, 255, 255, 0.1);
    box-shadow: 0 8px 32px 0 rgba(0, 0, 0, 0.2);
    border-radius: 16px;
}
```

---

## 2. Dynamic Interaction & Micro-Animations
Interfaces must feel active and responsive to user interaction.

- **Button Scale Shifts**: All clickable inputs and buttons must scale down on activation:
```css
.btn-action {
    transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
}
.btn-action:hover {
    transform: translateY(-2px);
    box-shadow: 0 6px 20px rgba(232, 100, 60, 0.25);
}
.btn-action:active {
    transform: scale(0.96);
}
```
- **Pulse Indicators**: Active state elements (e.g., active checked-in ca-trưởng or active terminal status) must utilize CSS pulse keyframe animations to indicate real-time status.

---

## 3. SweetAlert2 & Unified Notification Standard
Traditional browser `alert()` or `confirm()` boxes are STRICTLY PROHIBITED. All confirmation popups, success states, and backend-returned validation warnings must leverage **SweetAlert2**.

### A. SweetAlert2 Theme Overrides
Configure SweetAlert2 modals with custom buttons matching the HSL color palette:
```javascript
const SwalCafe = Swal.mixin({
    customClass: {
        confirmButton: 'btn btn-action btn-confirm-custom',
        cancelButton: 'btn btn-action btn-cancel-custom'
    },
    buttonsStyling: false
});
```

### B. SweetAlert2 with AJAX POST & Anti-Forgery Protection
When sending an AJAX request from a SweetAlert2 popup (e.g., toggle status or checking in), you must capture the CSRF token from the Razor page:
```javascript
function confirmAndToggleStatus(staffId) {
    // Capture CSRF token
    const token = $('input[name="__RequestVerificationToken"]').val();

    Swal.fire({
        title: 'Xác nhận thay đổi?',
        text: "Bạn có chắc muốn thay đổi trạng thái hoạt động của nhân viên này?",
        icon: 'warning',
        showCancelButton: true,
        confirmButtonText: 'Đồng ý',
        cancelButtonText: 'Hủy',
        confirmButtonColor: '#e8643c'
    }).then((result) => {
        if (result.isConfirmed) {
            Swal.fire({
                title: 'Đang xử lý...',
                didOpen: () => { Swal.showLoading(); }
            });

            $.ajax({
                url: `/Admin/AdminStaff/ToggleStatus/${staffId}`,
                type: 'POST',
                headers: {
                    'RequestVerificationToken': token // Injecting anti-forgery header
                },
                success: function (res) {
                    if (res.success) {
                        Swal.fire('Thành công!', res.message, 'success')
                            .then(() => location.reload());
                    } else {
                        Swal.fire('Lỗi!', res.message, 'error');
                    }
                },
                error: function (xhr) {
                    const msg = xhr.responseJSON?.message || 'Có lỗi mạng xảy ra.';
                    Swal.fire('Thất bại!', msg, 'error');
                }
            });
        }
    });
}
```

---

## 4. Layout Standard: Split-Screen & Mobile Responsiveness
Staff terminals like the POS or StaffHub are used on various hardware (desktop monitors, fixed tablets, and personal smartphones).
- **Split Layout**: Design a grid system dividing the screen:
  - **Left side**: Key actions, biometric feedback/canvas, real-time store stats, and local time clocks.
  - **Right side**: Active context timeline (e.g., shifts assigned today, active invoice totals, order queue).
- **Flex-grid Breakpoints**: Under 768px wide, all grids must stack into a single-column layout, ensuring cashiers can read schedules and verify transactions on BYOD mobile setups.
