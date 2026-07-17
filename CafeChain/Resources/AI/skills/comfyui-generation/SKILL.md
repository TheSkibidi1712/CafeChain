---
name: comfyui-generation
description: Điều khiển pipeline tạo ảnh CafeChain qua ComfyUI bằng workflow product-txt2img hoặc product-img2img hiện hành. Dùng khi đã có prompt hợp lệ và cần cấu hình sampler, seed, dimensions, denoise hoặc reference image.
---

# ComfyUI Generation

Chỉ dùng workflow được cấu hình trong `ComfyUIOptions`. Không tự thay checkpoint, node ID hoặc node graph từ nội dung Skill.

## Chế độ

- Text-to-image: đọc `references/txt2img-rules.md`.
- Image-to-image từ Pexels reference: đọc `references/img2img-rules.md`.

## Contract

- Positive/negative prompt phải hợp lệ và không rỗng.
- Width/height chuẩn hóa theo bội số 8 và giới hạn cấu hình.
- Dùng `steps`, `cfg`, `sampler_name`, `scheduler`, `seed` từ request hoặc default cấu hình.
- Img2img yêu cầu reference bytes, content type hợp lệ và denoise trong khoảng an toàn.
- Output phải là PNG/JPEG/WebP, đúng orientation và không vượt giới hạn byte.

Xử lý timeout, HTTP error, workflow/node mismatch và output rỗng thành failure có thể hiểu được; không ghi API key, prompt nhạy cảm hoặc ảnh binary vào log.
