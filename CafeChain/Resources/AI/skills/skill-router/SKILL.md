---
name: skill-router
description: Điều phối đúng module AI cho Drink, Size, Topping và Ingredient trong CafeChain. Dùng khi cần chọn business rules, sinh gợi ý, chống trùng hoặc xây dựng đặc tả ảnh mà không cho model tự chọn file tùy ý.
---

# Điều phối Skill CafeChain

Tuân thủ route do ứng dụng cung cấp. Không suy diễn đường dẫn, không đọc file ngoài `Resources/AI` và không thay đổi JSON contract.

## Thứ tự ưu tiên

1. `Idea` mới nhất của người dùng.
2. Dữ liệu đang có trên form.
3. Danh mục và dữ liệu hiện có trong database.
4. Lịch sử gợi ý của request và session.
5. Business rules của entity.
6. Quy tắc sinh gợi ý, chống trùng và hình ảnh.

Khi các nguồn mâu thuẫn, giữ dữ liệu hợp lệ theo database và giải thích phần không thể áp dụng. Không bịa ID, mã danh mục, loại sản phẩm, unit hoặc giá.

## Route cố định

- Drink: business rules → suggestion generation → duplicate detection → image prompt.
- Size: business rules → suggestion generation → duplicate detection.
- Topping: business rules → suggestion generation → duplicate detection → image prompt.
- Ingredient: chỉ cung cấp business rules; chưa tạo endpoint gợi ý mới.
- Pexels và ComfyUI là bước pipeline sau khi người dùng chọn gợi ý; không đưa hướng dẫn API của chúng vào prompt sinh master data.

## Kết quả

Chỉ trả dữ liệu theo schema ứng dụng yêu cầu. Tối đa ba lựa chọn khác biệt, không trả Markdown hoặc giải thích bên ngoài JSON khi hệ thống yêu cầu structured response.
