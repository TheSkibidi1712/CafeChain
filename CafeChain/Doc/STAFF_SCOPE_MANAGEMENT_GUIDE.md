# NGHIỆP VỤ PHẠM VI NHÂN VIÊN VÀ HƯỚNG DẪN SỬ DỤNG FORM

## 1. Mục đích

Tài liệu này mô tả cách CafeChain tạo nhân viên, gán vai trò, giới hạn phạm vi dữ liệu và xác định cửa hàng công tác chính. Phạm vi áp dụng là chuỗi vừa và nhỏ gồm 2–5 cửa hàng.

Thiết kế sử dụng bốn khái niệm độc lập:

| Khái niệm | Ý nghĩa |
| --- | --- |
| `Permission` | Một thao tác cụ thể mà tài khoản được phép thực hiện, ví dụ `Staff.Create` hoặc `Shift.Update`. |
| `Role` | Nhóm chức vụ và tập permission mặc định, ví dụ Quản lý chi nhánh hoặc Nhân viên bán hàng. |
| `StaffScope` | Giới hạn dữ liệu mà nhân viên được phép xem và thao tác. |
| `Staff.StoreId` | Cửa hàng công tác chính, dùng cho lịch ca, POS và nghiệp vụ vận hành hằng ngày. |

`Staff.StoreId` không thay thế `StaffScope`. Một nhân viên có thể công tác chính tại cửa hàng A nhưng được cấp thêm quyền quản lý cửa hàng B.

## 2. Loại phạm vi

Code và ID là contract kỹ thuật, không thay đổi theo ngôn ngữ giao diện.

| ID | Code | Tên hiển thị | Dữ liệu được bao phủ |
| ---: | --- | --- | --- |
| 1 | `COUNTRY` | Toàn chuỗi | Tất cả cửa hàng thuộc CafeChain. |
| 2 | `PROVINCE` | Tỉnh/Thành phố | Các cửa hàng thuộc một tỉnh/thành phố. |
| 4 | `WARD` | Xã/Phường/Đặc khu | Các cửa hàng thuộc một xã/phường/đặc khu. |
| 5 | `STORE` | Cửa hàng | Một cửa hàng cụ thể. |

Tên tiếng Việt chỉ dùng để hiển thị. Backend luôn xử lý bằng ID hoặc Code.

## 3. Vì sao phải chọn Role và Scope trước cửa hàng chính?

Thứ tự nghiệp vụ chuẩn là:

```text
Vai trò
    ↓
Loại phạm vi
    ↓
Phạm vi cụ thể
    ↓
Cửa hàng chính nằm trong phạm vi
    ↓
Tạo Staff + Account + Role + StaffScope
```

Lý do:

1. Vai trò quyết định loại phạm vi nào hợp lệ.
2. Phạm vi quyết định tập cửa hàng tài khoản được truy cập.
3. Cửa hàng chính phải được chọn từ tập cửa hàng đó.
4. Nếu chọn cửa hàng trước, người dùng có thể chọn một cửa hàng rồi cấp một phạm vi không bao phủ cửa hàng này.
5. Custom permission được cấu hình sau khi tạo vì thao tác này cần `StaffId` và `AccountId` đã tồn tại.

Backend vẫn kiểm tra lại toàn bộ dữ liệu. Việc thay `StoreId` thủ công trên request không thể mở rộng quyền truy cập.

## 4. Ma trận vai trò và phạm vi ban đầu

| Vai trò của nhân viên mới | Phạm vi ban đầu | Cách chọn cửa hàng chính |
| --- | --- | --- |
| Chủ doanh nghiệp | Toàn chuỗi | Chọn một cửa hàng đang hoạt động làm nơi công tác chính. |
| Quản trị hệ thống | Toàn chuỗi | Chọn một cửa hàng đang hoạt động làm nơi công tác chính. |
| Quản lý vùng | Tỉnh/Thành phố, Xã/Phường/Đặc khu hoặc Cửa hàng | Chỉ chọn được cửa hàng thuộc địa bàn đã cấp. |
| Quản lý chi nhánh | Cửa hàng | Cửa hàng scope đồng thời là cửa hàng chính. |
| Ca trưởng | Cửa hàng | Cửa hàng scope đồng thời là cửa hàng chính. |
| Nhân viên bán hàng | Cửa hàng | Cửa hàng scope đồng thời là cửa hàng chính. |
| Kế toán/kho | Cửa hàng | Cửa hàng scope đồng thời là cửa hàng chính. |

Người tạo chỉ được gán vai trò và phạm vi thấp hơn, nằm trong quyền và phạm vi của chính mình.

## 5. Hướng dẫn tạo nhân viên

### Bước 1 – Thông tin chung

1. Nhập họ tên, CCCD, giới tính, ngày sinh và số điện thoại.
2. Avatar là tùy chọn; chỉ hỗ trợ JPG, JPEG, PNG hoặc WebP, tối đa 2 MB.
3. Chọn Tỉnh/Thành phố của địa chỉ thường trú.
4. Chọn Xã/Phường/Đặc khu trực thuộc tỉnh/thành phố.
5. Nhập số nhà và tên đường ở trường Địa chỉ chi tiết.

Địa chỉ thường trú không quyết định phạm vi dữ liệu của nhân viên.

### Bước 2 – Vai trò và phạm vi

1. Chọn Vai trò.
2. Hệ thống giới hạn các Loại phạm vi phù hợp với vai trò.
3. Chọn phạm vi cụ thể:
   - Toàn chuỗi: hệ thống tự chọn phạm vi toàn chuỗi.
   - Tỉnh/Thành phố: chọn một tỉnh.
   - Xã/Phường/Đặc khu: chọn tỉnh/thành phố trước, sau đó chọn đơn vị cấp xã trực thuộc.
   - Cửa hàng: chọn một cửa hàng đang hoạt động.
4. Chọn Cửa hàng chính từ danh sách đã được lọc theo phạm vi.

Nếu scope là Cửa hàng, hệ thống tự đồng bộ cửa hàng đó thành cửa hàng chính.

### Bước 3 – Công việc và tài khoản

1. Nhập email đăng nhập và mật khẩu ban đầu.
2. Chọn ngày bắt đầu và trạng thái nhân sự.
3. Nhấn **Tạo nhân viên**.
4. Hệ thống lưu Account, Staff, AccountRole, StaffScope, điện thoại và địa chỉ trong cùng transaction.
5. Sau khi thành công, hệ thống chuyển tới Bảng phân quyền và chọn sẵn nhân viên vừa tạo.

## 6. Thêm nhiều phạm vi sau khi tạo

Form tạo chỉ cấp một phạm vi ban đầu. Để một người quản lý nhiều cửa hàng:

1. Mở **Bảng phân quyền**.
2. Chọn tab **Phạm vi cửa hàng**.
3. Chọn nhân viên.
4. Thêm từng scope `Cửa hàng` cần quản lý.
5. Lưu thay đổi.

Không cần cấp phạm vi Tỉnh/Thành phố nếu người đó chỉ quản lý hai cửa hàng cụ thể trong cùng tỉnh.

## 7. Ví dụ

### Nhân viên bán hàng tại một cửa hàng

- Role: Nhân viên bán hàng.
- Scope: Cửa hàng A.
- Cửa hàng chính: Cửa hàng A.
- Kết quả: chỉ sử dụng POS và xem dữ liệu được cấp tại cửa hàng A.

### Quản lý hai cửa hàng

- Role: Quản lý vùng hoặc vai trò quản lý phù hợp.
- Scope ban đầu: Cửa hàng A.
- Cửa hàng chính: Cửa hàng A.
- Sau khi tạo: thêm scope Cửa hàng B tại Bảng phân quyền.

### Quản lý theo địa bàn

- Role: Quản lý vùng.
- Scope: Tỉnh/Thành phố hoặc Xã/Phường/Đặc khu.
- Cửa hàng chính: một cửa hàng đang hoạt động thuộc địa bàn đó.
- Cửa hàng tạo mới trong cùng địa bàn được scope resolver nhận diện theo dữ liệu địa giới.

## 8. Lỗi thường gặp

| Thông báo/tình huống | Nguyên nhân | Cách xử lý |
| --- | --- | --- |
| Cửa hàng chính nằm ngoài phạm vi | StoreId không thuộc ScopeRef đã chọn. | Chọn lại scope hoặc cửa hàng chính. |
| Phạm vi không có cửa hàng phù hợp | Không có cửa hàng đang hoạt động trong địa bàn. | Chọn phạm vi khác hoặc kiểm tra cấu hình cửa hàng. |
| Vai trò hoặc loại phạm vi không hợp lệ | ScopeType không phù hợp vai trò. | Chọn lại vai trò; form sẽ giới hạn scope hợp lệ. |
| Không thấy cửa hàng cần chọn | Cửa hàng nằm ngoài scope của người đang thao tác. | Nhờ người có phạm vi cao hơn thực hiện. |
| Đổi vai trò làm mất lựa chọn | Scope cũ không còn hợp lệ với vai trò mới. | Chọn lại scope và cửa hàng chính theo thứ tự. |

## 9. Quy tắc an toàn backend

- Controller chỉ gọi Service; Service sử dụng Repository và scope resolver.
- Danh sách scope/cửa hàng trả về phải nằm trong phạm vi actor.
- Backend kiểm tra role hierarchy, permission và StoreScope, không tin dữ liệu ẩn trên giao diện.
- `ScopeCoversStoreAsync` xác nhận cửa hàng chính nằm trong phạm vi được cấp.
- Tạo nhân viên là transaction; một bước lỗi phải rollback toàn bộ.
- Permission tùy chỉnh không được vượt permission hiệu lực của actor.
- Việc ẩn dropdown hoặc nút chỉ hỗ trợ UX, không thay thế kiểm tra authorization.

