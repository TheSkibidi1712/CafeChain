---
name: suggestion-generation
description: Sinh tối đa ba gợi ý master data CafeChain cho Drink, Size hoặc Topping. Dùng khi xử lý chế độ New, Develop hoặc Variant với Idea, dữ liệu form, database, lịch sử session và business rules đã được cung cấp.
---

# Sinh gợi ý CafeChain

## Quy trình

1. Xác định entity và `generationMode`.
2. Lấy `idea` làm chủ đề trung tâm; không thay bằng trend chung.
3. Dùng current form làm nền cho `Develop` và `Variant`.
4. Loại tên/mã đã có trong database hoặc history.
5. Tạo tối đa ba option theo `references/diversity-profiles.md`.
6. Kiểm tra business rules, duplicate signals và schema trước khi trả.

## Generation mode

- `New`: tạo khái niệm mới, không sao chép current form.
- `Develop`: hoàn thiện current form và giữ ý định chính.
- `Variant`: giữ lõi sản phẩm nhưng thay đổi rõ hương vị, hình thức hoặc phân khúc.

## Ràng buộc output

- Chỉ trả JSON; không dùng code fence.
- Không trả nhiều hơn ba option.
- Mỗi option phải có persona, `creativityScore`, `relevanceScore`, reason và `duplicateSignals`.
- Điểm nằm trong 0–100; option không liên quan Idea phải bị loại.
- Không bịa ID. Chỉ trả code/enum có trong payload cho phép.
- Nếu toàn bộ option không hợp lệ, cho phép retry có giới hạn rồi dùng fallback qua cùng validator.

JSON shape chi tiết nằm trong `references/output-schema.json` và schema chuẩn tại `Resources/AI/schemas/ai-suggestion.schema.json`.
