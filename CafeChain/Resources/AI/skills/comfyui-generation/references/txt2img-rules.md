# Text-to-image rules

- Dùng workflow cấu hình `Resources/AI/ComfyUI/product-txt2img.json`.
- Gán checkpoint, positive prompt và negative prompt vào node ID cấu hình.
- Gán width/height/batch size vào empty latent node; kích thước là bội số 8, trong 256–2048.
- Gán sampler: `steps` 1–100, `cfg` 1–30, sampler/scheduler đã sanitize và seed dương.
- Dùng `denoise = 1.0`; không cần reference image.
- Output count trong 2–4 và filename prefix chỉ chứa chữ, số, `-`, `_`.
- Workflow JSON phải parse được và có đầy đủ node trước khi gửi `/prompt`.
