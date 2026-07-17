# Ingredient rules

- `Code`: bắt buộc, trim, uppercase và duy nhất.
- `Name`: bắt buộc, trim và duy nhất.
- `BaseUnitId`: chỉ dùng Unit active có trong payload; không bịa unit.
- Ingredient mới mặc định active.
- Unit conversion thuộc từng Ingredient; `FromUnitId` phải khác `ToUnitId`.
- `FromQuantity` và `ToQuantity` phải lớn hơn 0.
- Không tạo đồng thời hai conversion đảo chiều hoặc trùng cặp unit.
- Không tự suy ra density để đổi mass sang volume.
- Không tự tạo supplier price, inventory balance, recipe line hoặc cost layer.
- Endpoint AI Ingredient chưa mở; reference này chỉ dùng để kiểm tra business context.
