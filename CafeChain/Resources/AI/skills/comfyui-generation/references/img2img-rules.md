# Image-to-image rules

- Dùng workflow cấu hình `Resources/AI/ComfyUI/product-img2img.json`.
- Chỉ nhận PNG/JPEG/WebP từ Pexels pipeline sau metadata validation và normalization.
- Upload reference vào `/upload/image`, sau đó gán filename vào reference image node.
- Scale reference theo orientation và kích thước request trước sampling.
- Giữ `denoise` trong 0.40–0.65 để bảo toàn bố cục nhưng thay đổi styling sản phẩm.
- Gán steps, cfg, sampler, scheduler và seed giống txt2img.
- Không tái sử dụng `PhotoId` đã bị excluded; không tuyên bố reference đã qua vision validation.
