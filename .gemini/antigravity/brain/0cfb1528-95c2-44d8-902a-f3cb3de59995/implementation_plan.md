# Kế hoạch Khắc Phục Lỗi Thiết Kế Hệ Thống Đánh Giá Từ Chuyên Gia

Rất chân thành cảm ơn những phản biện cực kỳ sắc bén của bạn! Bạn đã chỉ ra chính xác 4 "tử huyệt" của phương pháp vừa rồi. Đúng là dưới góc nhìn hệ thống lớn thực tế (Production), cách làm cũ sẽ gây nổ server và rác dữ liệu.

Mình nhận sai sót và xin phép đề xuất kế hoạch tái cấu trúc triệt để (The Hardcore Clean Way) để giải quyết 4 vấn đề này như sau:

## 1. Xử Lý Lỗi Dư Thừa Dữ Liệu (Denormalization Anomaly)
**Vấn đề:** Không được lưu chuỗi ghép sẵn xuống DB trong khi đã có Foreign Key `WardId`.
**Giải pháp:**
- Bảng `CustomerAddress`, cột `Address` hiện tại **chỉ dùng để lưu Số nhà / Tên đường / Hẻm**. Không lưu Phường, Tỉnh vào đây nữa.
- Việc ghép tên Phường, Tỉnh sẽ được thực hiện **ĐỘNG (Dynamically)** tại View hoặc khi Select data từ DB bằng cách dùng `.Include(a => a.Ward).ThenInclude(w => w.Province)`. Khách hàng sửa tên phường trong danh mục -> Mọi địa chỉ liên quan tự động cập nhật tên mới ngay lập tức.

## 2. Diệt Trừ Vòng Lặp N+1 Query
**Vấn đề:** Loop qua mảng `NewAddresses` và gọi DB truy vấn từng WardId.
**Giải pháp:**
- Tuyệt vời là... chúng ta **không cần Query bảng Ward** nữa! Vì chúng ta không ghép chuỗi để lưu vào DB! 
- Khi người dùng gửi DTO chứa `Street` và `WardId`, ở Service, ta chỉ việc `new CustomerAddress { Address = dto.Street, WardId = dto.WardId }`.
- Entity Framework sẽ tự ánh xạ Foreign Key. Tốc độ thực thi Data Insert sẽ tính bằng mili-giây và 0 lần Query thừa.

## 3. Khắc Phục Dữ Liệu Khách Cũ (Nullable WardId Trap)
**Vấn đề:** Khách cũ có `WardId = null` gây khó cho Report và thống kê.
**Giải pháp:**
- **Bước 1:** Bổ sung C# Property `public string DisplayAddress => Ward != null ? $"{Address}, {Ward.Name}, {Ward.Province?.Name}" : Address;` trong model `CustomerAddress`. Nó sẽ giúp hiển thị UI tương thích với cả địa chỉ mới (ghép động) và cũ (chuỗi tĩnh).
- **Bước 2 (Force Update tại UI):** Ở trang Profile, nếu địa chỉ Mặc định hiện tại của khách có `WardId == null`, ta sẽ hiển thị một Alert cảnh báo: *"Vui lòng cập nhật lại địa chỉ giao hàng theo định dạng phường/xã mới để đảm bảo thuật toán tính ship chính xác."*
- Bằng cách này, chúng ta gieo hiệu ứng "Crowdsourcing" để user tự làm sạch rác data cho hệ thống.

## 4. Xử Lý Lệch Pha Dữ Liệu (400 Bad Request & Match String Brittle)
**Vấn đề:** `Profile.cshtml` gửi PrimaryAddress là dạng string, nhưng việc dùng UI có lúc sinh thêm Object.
**Giải pháp tối thượng: Không match bằng Chuỗi nữa!**
- Tại trang `Profile.cshtml`, trong thẻ `<select id="addressSelect">`, ta sẽ gán `value` của `<option>` bằng **`CustomerAddressId`**, thay vì lưu nguyên câu String.
- DTO `UpdateProfileRequest` sẽ đổi `string PrimaryAddress` thành `int? PrimaryAddressId`.
- Trong Service `UpdateProfileAsync()`, việc lật cờ isDefault trở nên siêu nhanh: `a.IsDefault = (a.CustomerAddressId == request.PrimaryAddressId);`.

---

## User Review Required

> [!WARNING]
> Mình sẽ thay đổi core logic của hàm lưu Profile cũng như các thuộc tính trong UI `PrimaryAddress` -> `PrimaryAddressId`. Đây là các thay đổi "phẫu thuật tận gốc" để giải quyết rủi ro hệ thống. 
> 
> Bạn có muốn tôi thực hiện toàn bộ combo 4 giải pháp này ngay lập tức vào code không? (Các file bị ảnh hưởng: `CustomerAddress.cs`, `UpdateProfileRequest.cs`, `CustomerService.cs` và `Profile.cshtml`).
