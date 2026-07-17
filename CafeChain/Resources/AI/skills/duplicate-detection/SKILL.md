---
name: duplicate-detection
description: Phát hiện gợi ý Drink, Size và Topping bị trùng hoặc gần trùng với database, request history và session history. Dùng trước khi chấp nhận option AI hoặc fallback.
---

# Chống trùng gợi ý

## Chuẩn hóa

- Code: trim, uppercase và bỏ ký tự phân cách không có ý nghĩa.
- Text: lowercase, bỏ dấu, chuẩn hóa khoảng trắng và punctuation.
- Token: loại token rỗng; giữ token mô tả nguyên liệu, hương vị, size và concept ảnh.

## Tín hiệu

1. Exact code hoặc exact normalized name: loại ngay.
2. Levenshtein name similarity đạt ngưỡng cấu hình: đánh dấu `NEAR_NAME`.
3. Token/ingredient Jaccard cao: đánh dấu `TOKEN_OVERLAP`.
4. Description và image concept cùng gần giống: đánh dấu `COMPOSITE_SIMILARITY`.
5. Trùng request/session history: đánh dấu `RECENT_SUGGESTION`.

Không chỉ đổi tiền tố, hậu tố, dấu câu hoặc một tính từ để né trùng. Với Size, các tên chuẩn như M/L/XL hoặc dung tích chỉ bị trùng khi cùng normalized value và cùng `SizeType`.

## Kết quả

- Option vi phạm exact hoặc composite threshold phải bị loại.
- Option được giữ có thể trả `duplicateSignals: []`.
- Không hạ threshold chỉ để đủ ba lựa chọn.
- Fallback C# phải đi qua cùng phép kiểm tra như output Ollama.
