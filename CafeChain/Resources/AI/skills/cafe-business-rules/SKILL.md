---
name: cafe-business-rules
description: Áp dụng nghiệp vụ master data CafeChain cho Drink, Size, Topping và Ingredient. Dùng khi tạo hoặc kiểm tra gợi ý để bảo đảm mã, tên, loại, giá, unit, conversion và quan hệ công thức phù hợp model/service hiện tại.
---

# Business Rules CafeChain

Đọc đúng reference theo entity trước khi tạo gợi ý:

- Drink: `references/drink-rules.md`
- Size: `references/size-rules.md`
- Topping: `references/topping-rules.md`
- Ingredient: `references/ingredient-rules.md`

## Quy tắc chung

- Chỉ dùng Category, ProductType, SizeType, Unit và ID có trong payload/database.
- Chuẩn hóa code bằng trim và uppercase; không tái sử dụng code hoặc tên đã tồn tại.
- Không tự tạo quan hệ DrinkSize, DrinkTopping, Recipe, UnitConversion hoặc giá vốn nếu payload không yêu cầu.
- Không suy ra COGS khi BOM, conversion hoặc cost layer chưa đầy đủ.
- Dữ liệu AI luôn là gợi ý; người dùng phải xác nhận trước khi lưu.
- Nếu không đủ dữ liệu để tạo giá trị hợp lệ, trả warning hoặc bỏ option thay vì bịa dữ liệu.

## Kiểm tra cuối

Xác nhận độ dài field, enum, trạng thái active, uniqueness và quan hệ entity. Mọi lựa chọn phải có thể đi qua validator C# tương ứng.
