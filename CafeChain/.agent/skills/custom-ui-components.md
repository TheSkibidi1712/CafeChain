# Custom UI Component Development Skill

This skill documents standard frontend construction patterns for premium, high-interaction UI components used across the StaffHub and POS modules.

---

## 1. Dynamic Biometric Scanner Component (face-api.min.js)
To avoid standard browser overlays, the timekeeping scanner relies on a custom-designed canvas viewport matching the container client boundaries instead of native video streams.

### A. Razor HTML Structure
```html
<div class="faceid-container">
    <div class="scanner-frame" id="scannerViewport">
        <!-- Structural corners for visual alignment -->
        <span class="corner tl"></span><span class="corner tr"></span>
        <span class="corner bl"></span><span class="corner br"></span>
        
        <!-- Video stream and Canvas overlap -->
        <video id="webcamStream" autoplay muted playsinline></video>
        <canvas id="biometricCanvas"></canvas>
    </div>
    <div class="scanner-actions">
        <button class="btn-action btn-primary" id="btnTriggerScan">Bắt đầu quét</button>
    </div>
</div>
```

### B. Viewport Matching Logic
To prevent rendering offsets on mobile screens, force the canvas to stretch exactly over the CSS client bounding box of the parent frame:
```javascript
function initializeCanvas(video, canvas, viewport) {
    const rect = viewport.getBoundingClientRect();
    const displaySize = { width: rect.width, height: rect.height };
    
    canvas.width = rect.width;
    canvas.height = rect.height;
    
    faceapi.matchDimensions(canvas, displaySize);
    return displaySize;
}
```

---

## 2. Dynamic Array Fields Component (Phones / Addresses)
To manage dynamic child forms in creation/editing views (e.g. adding multiple phone numbers dynamically in `Edit.cshtml`), always generate proper C# indexes.

### A. JavaScript Template Generator
```javascript
let phoneIndex = 0; // Tracks input indexes

function addPhoneField() {
    const container = document.getElementById('phoneListContainer');
    const html = `
        <div class="phone-item d-flex gap-2 mb-2 align-items-center">
            <input type="text" 
                   name="Phones[${phoneIndex}]" 
                   class="form-control premium-input" 
                   placeholder="Nhập số điện thoại..." 
                   required />
            <button type="button" class="btn-remove" onclick="this.parentElement.remove(); reindexPhones();">
                <i class="fas fa-trash-alt"></i>
            </button>
        </div>
    `;
    $(container).append(html);
    phoneIndex++;
}

function reindexPhones() {
    // Keeps indexes contiguous to prevent ModelBinding failure on missing items
    $('#phoneListContainer .phone-item').each(function(idx, el) {
        $(el).find('input').attr('name', `Phones[${idx}]`);
    });
    phoneIndex = $('#phoneListContainer .phone-item').length;
}
```

---

## 3. Glassmorphism Status Badges
Premium statuses (Checked-in, overnight shifts, active network flags) must utilize visual gloss styles instead of flat grey colors.

```css
.badge-glass-success {
    background: rgba(34, 197, 94, 0.15);
    color: #22c55e;
    border: 1px solid rgba(34, 197, 94, 0.25);
    padding: 6px 12px;
    border-radius: 20px;
    font-size: 12px;
    font-weight: 600;
    display: inline-flex;
    align-items: center;
    gap: 6px;
}

.badge-glass-success::before {
    content: '';
    width: 6px;
    height: 6px;
    background: #22c55e;
    border-radius: 50%;
    box-shadow: 0 0 8px #22c55e;
}
```

---

## 4. SweetAlert2 Loading & Notification Triggers
Avoid interrupting user flow. When processing background operations like verifying check-in networks, display a premium animated loader, followed by success alerts.

```javascript
function runAjaxWithLoader(apiUrl, payload, successCallback) {
    Swal.fire({
        title: 'Đang xử lý dữ liệu...',
        text: 'Vui lòng không đóng cửa sổ.',
        allowOutsideClick: false,
        didOpen: () => {
            Swal.showLoading();
        }
    });

    $.ajax({
        url: apiUrl,
        type: 'POST',
        data: payload,
        success: function(res) {
            Swal.close();
            if (res.success) {
                Swal.fire({
                    icon: 'success',
                    title: 'Hoàn tất!',
                    text: res.message,
                    timer: 2000,
                    showConfirmButton: false
                }).then(() => {
                    if (successCallback) successCallback(res.data);
                });
            } else {
                Swal.fire('Lỗi nghiệp vụ!', res.message, 'warning');
            }
        },
        error: function(xhr) {
            Swal.close();
            const errorMsg = xhr.responseJSON?.message || 'Lỗi mạng không mong muốn.';
            Swal.fire('Thất bại!', errorMsg, 'error');
        }
    });
}
```
