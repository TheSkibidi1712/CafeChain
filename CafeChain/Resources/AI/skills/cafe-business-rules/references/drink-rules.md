# Drink rules

- `DrinkCode`: bắt buộc, trim, uppercase, tối đa 50 ký tự và duy nhất.
- `Name`: bắt buộc, trim, tối đa 200 ký tự và duy nhất sau chuẩn hóa.
- `Description`: tối đa 1.000 ký tự.
- `CategoryId` và `ProductTypeId`: chỉ dùng bản ghi active được cung cấp; không bịa ID.
- `ProductType` phải phù hợp cách bán: đồ pha chế dùng handcrafted; hàng đóng chai/lon dùng retail khi catalog cho phép.
- Drink mới mặc định `Active = true`; `CreatedAt` do server thiết lập.
- Không tự gán Size, Topping, Recipe, calculated COGS hoặc ảnh đã lưu.
- Image prompt phải phản ánh đúng tên, category, product type và mô tả.
- Drink có tên/code trùng hoặc gần trùng database/history phải bị loại.
