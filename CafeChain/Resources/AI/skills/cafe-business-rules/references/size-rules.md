# Size rules

- `SizeCode`: bắt buộc, uppercase, tối đa 20 ký tự và duy nhất.
- `Name`: bắt buộc, tối đa 50 ký tự và duy nhất.
- `Description`: tối đa 300 ký tự.
- `SizeType` chỉ nhận enum hiện có: `Cup` cho cỡ ly, `Volume` cho dung tích đóng gói.
- Không dùng tên dung tích như `350ml` với `Cup` nếu payload không chỉ rõ nghiệp vụ khác.
- Không tạo giá bán trong Size; giá thuộc quan hệ `DrinkSize`.
- Size mới mặc định active. Không tự liên kết Size vào Drink.
- Với `Develop` hoặc `Variant`, giữ `SizeType` của current form trừ khi Idea yêu cầu thay đổi hợp lệ.
