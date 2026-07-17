# Topping rules

- `ToppingCode`: bắt buộc, uppercase, tối đa 50 ký tự và duy nhất.
- `Name`: bắt buộc, tối đa 100 ký tự và duy nhất.
- `Price`: lớn hơn 0; AI chỉ dùng giá form hoặc default do ứng dụng cung cấp, không tự suy ra giá thị trường.
- Topping mới mặc định active.
- Không tự tạo `DrinkTopping`, `DrinkDefaultTopping`, recipe hoặc COGS.
- Topping phải có image concept phù hợp một món thêm vào đồ uống, không mô tả như một ly đồ uống hoàn chỉnh.
- Khi tạo mới, ảnh chỉ là gợi ý; yêu cầu ảnh bắt buộc vẫn do form/service kiểm tra.
- Loại tên/code trùng hoặc concept gần trùng database/history.
