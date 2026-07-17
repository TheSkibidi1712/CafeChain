---
name: image-prompt-builder
description: Xây dựng Visual Specification và prompt ảnh sản phẩm CafeChain cho Drink hoặc Topping. Dùng sau khi option hợp lệ đã được tạo và cần mô tả ảnh nhất quán cho Pexels hoặc ComfyUI.
---

# Xây dựng prompt ảnh sản phẩm

Tạo đặc tả có cấu trúc, không chỉ một câu prompt ngắn. Giữ subject khớp tên và mô tả sản phẩm; không thêm nguyên liệu trái business rules.

## Thành phần bắt buộc

- `primarySubject`, `styleProfile`, `mood`
- `container`, `surface`, `background`
- `garnishes`, `props`
- `lighting`, `cameraAngle`, `lens`, `depthOfField`
- `orientation`, `colorPalette`, `referencePurpose`
- ít nhất ba `pexelsQueries` bằng tiếng Anh
- `negativePrompt` và `forbiddenKeywords`

Chọn style từ `references/style-profiles.md`, bố cục từ `references/composition-profiles.md` và negative rules từ `references/negative-prompts.md`.

## Nguyên tắc

- Ảnh phải tập trung vào một sản phẩm, phù hợp menu quán cà phê.
- Không yêu cầu logo, chữ, watermark, người hoặc bàn tay.
- Prompt tiếng Anh dùng mô tả cụ thể, không nhồi tag mâu thuẫn.
- Tạo khác biệt về container, garnish, lighting và composition giữa các option.
- Không tuyên bố ảnh đã được vision model kiểm tra.
