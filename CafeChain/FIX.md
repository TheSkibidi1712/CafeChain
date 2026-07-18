# YÊU CẦU REFACTOR SEED DATA, SUPPLIER CONFIGURATION VÀ APP LAUNCHER

Hãy đóng vai trò là **Senior .NET Developer, Database Engineer và Software Architect có 20 năm kinh nghiệm**, chuyên về:

* ASP.NET Core MVC.
* Entity Framework Core.
* SQL Server.
* Stored Procedure.
* Layered Architecture.
* Seed data và dữ liệu kiểm thử.
* Hệ thống quản lý kho.
* Tích hợp nhiều ứng dụng Frontend và Backend.
* Thiết kế App Launcher cho hệ thống đa ứng dụng.

Hãy đọc kỹ toàn bộ file được cung cấp trước khi chỉnh sửa.

Không được tự suy đoán cấu trúc bảng, tên cột, khóa ngoại, enum, Stored Procedure, Model, Configuration hoặc đường dẫn dự án nếu chưa tìm thấy trong mã nguồn.

---

# I. NGUYÊN TẮC THỰC HIỆN

## 1. Phân tích trước khi sửa

Trước khi viết seed hoặc sửa code, phải phân tích:

1. Các bảng trong database.
2. Các Stored Procedure liên quan.
3. Quan hệ khóa ngoại giữa các bảng.
4. Các file seed hiện tại.
5. Dữ liệu nào đã tồn tại.
6. Dữ liệu nào còn thiếu để test nghiệp vụ.
7. Các Configuration của Entity Framework Core.
8. Workflow tạo và xác nhận Phiếu Kho.
9. Cơ chế App Launcher hiện tại.
10. Cách `CafeChain.Bridge` và `CafeChain.Frontend` đang được khởi chạy.

Không được viết seed ngay khi chưa hoàn tất bước phân tích.

## 2. Không bịa dữ liệu kỹ thuật

Không được tự ý bịa:

* Tên bảng.
* Tên cột.
* Tên Stored Procedure.
* Tên Model.
* Tên DbSet.
* Tên Configuration.
* Tên Repository hoặc Service.
* Enum value.
* Permission code.
* Role.
* Supplier type.
* Unit.
* Store.
* Ingredient.
* Giá trị khóa ngoại.
* Đường dẫn project.
* Lệnh chạy project.

Nếu thiếu file cần thiết, phải liệt kê rõ file còn thiếu và chỉ bắt đầu thiết kế seed sau khi đã có đủ dữ liệu.

## 3. Giữ nguyên kiến trúc dự án

Không được:

* Đưa `AppDbContext` trực tiếp vào Controller hoặc Service.
* Thay đổi kiến trúc Layered Architecture hiện tại.
* Tạo thêm Service hoặc Repository không cần thiết.
* Tự ý tạo migration.
* Xóa dữ liệu seed cũ khi chưa xác định rõ ảnh hưởng.
* Thay đổi Stored Procedure nếu yêu cầu chỉ là tạo dữ liệu test.
* Hard-code đường dẫn máy cá nhân không có trong cấu hình.

---

# II. THIẾT KẾ SEED DATA DỰA TRÊN STORED PROCEDURE

## 1. Mục tiêu

Dựa vào các Stored Procedure trong file SQL được cung cấp, hãy xác định đầy đủ những bảng và dữ liệu cần có để có thể test được toàn bộ nghiệp vụ mà các Stored Procedure đang xử lý.

Seed data phải được thiết kế dựa trên:

* Cấu trúc bảng thật.
* Tham số đầu vào của Stored Procedure.
* Các câu `JOIN`.
* Các điều kiện `WHERE`.
* Các câu `INSERT`, `UPDATE`, `DELETE`.
* Các bảng tạm.
* Các điều kiện kiểm tra trạng thái.
* Các ràng buộc khóa ngoại.
* Các enum hoặc code được Stored Procedure sử dụng.
* Dữ liệu seed hiện có trong dự án.

## 2. File seed làm cơ sở

Tôi sẽ cung cấp các file seed hiện tại sau.

Hãy sử dụng các file đó để:

* Tiếp tục đúng ID hiện tại.
* Không tạo dữ liệu trùng.
* Không phá vỡ quan hệ khóa ngoại.
* Không thay đổi các dữ liệu demo đang hoạt động.
* Đồng bộ tên, mã, trạng thái và ngày tạo.
* Giữ cùng phong cách trình bày SQL của các file seed hiện tại.

Không bắt đầu tạo seed hoàn chỉnh cho đến khi đã đọc đủ:

* File SQL chứa Stored Procedure.
* File seed hiện tại.
* Model hoặc script tạo bảng liên quan.
* Các enum hoặc constants có liên quan.

## 3. Phân tích từng Stored Procedure

Với mỗi Stored Procedure, phải lập bảng phân tích theo cấu trúc:

| Stored Procedure | Mục đích | Bảng đọc | Bảng ghi | Điều kiện dữ liệu | Seed cần thiết |
| ---------------- | -------- | -------- | -------- | ----------------- | -------------- |

Phải chỉ rõ:

* Stored Procedure cần Store nào.
* Cần Staff nào.
* Cần Supplier nào.
* Cần Ingredient nào.
* Cần Unit nào.
* Cần tồn kho ban đầu hay không.
* Cần giá nhà cung cấp hay không.
* Cần phiếu nháp hay phiếu đã xác nhận.
* Cần công nợ hay không.
* Cần lịch sử giá hay không.
* Cần Cost Layer hay không.
* Cần Transaction hay không.
* Cần Snapshot hay không.
* Cần Permission hoặc Role hay không.

## 4. Phạm vi seed dữ liệu test

Hãy xác định và tạo seed đầy đủ cho các nhóm bảng thực tế cần thiết, có thể bao gồm nhưng không giới hạn:

### Nhóm cửa hàng và nhân sự

* Stores.
* Staffs.
* Accounts.
* Roles.
* AccountRoles.
* StaffScopes.
* Permissions.
* RolePermissions.

Chỉ tạo khi Stored Procedure hoặc nghiệp vụ kiểm thử thực sự cần.

### Nhóm nguyên liệu và đơn vị

* Ingredients.
* Units.
* UnitConversions.
* IngredientCategories.
* IngredientUnits.
* StoreIngredients hoặc bảng tồn kho tương đương.

### Nhóm nhà cung cấp

* Suppliers.
* SupplierStores.
* SupplierIngredients.
* IngredientSuppliers.
* IngredientSupplierPriceHistories.
* SupplierConfigurations.
* SupplierPaymentTerms.
* Các bảng liên kết thực tế có trong hệ thống.

### Nhóm kho

* InventoryDocuments.
* InventoryDocumentDetails.
* InventoryDocumentSnapshots.
* InventoryDocumentSnapshotDetails.
* InventoryTransactions.
* InventoryCostLayers.
* InventoryCostAllocations.
* InventoryBalances.
* InventoryTransfers.
* InventoryTransferDetails.
* Debts hoặc SupplierDebts.
* RequestDeduplications.

Chỉ sử dụng đúng tên bảng thật trong dự án.

## 5. Kịch bản dữ liệu kiểm thử bắt buộc

Seed phải hỗ trợ tối thiểu các kịch bản sau, nếu hệ thống có nghiệp vụ tương ứng:

### Kịch bản 1: Nhập kho từ nhà cung cấp

* Có nhà cung cấp đang hoạt động.
* Có nguyên liệu được nhà cung cấp cung cấp.
* Có giá mua hiện tại.
* Có đơn vị đóng gói.
* Có quy đổi sang đơn vị cơ sở.
* Có cửa hàng nhận hàng.
* Có nhân viên lập phiếu.
* Có đủ Configuration để tạo phiếu.
* Có thể tạo nháp.
* Có thể xác nhận phiếu.
* Sau xác nhận, tồn kho được tăng đúng.

### Kịch bản 2: Xuất kho bán hàng hoặc sử dụng nội bộ hợp lệ

Chỉ tạo nếu nghiệp vụ này còn tồn tại trong hệ thống.

* Có tồn kho ban đầu.
* Có nguyên liệu đủ số lượng.
* Có nhân viên được phép xác nhận.
* Sau xác nhận, tồn kho giảm đúng.

### Kịch bản 3: Xuất kho dẫn đến âm kho

* Có nguyên liệu không đủ tồn.
* Configuration cho phép âm kho nếu hệ thống hỗ trợ.
* Có người dùng có quyền phê duyệt.
* Có cảnh báo tồn kho âm.
* Có thể kiểm tra workflow từ chối hoặc phê duyệt.

### Kịch bản 4: Chuyển kho

* Có Store nguồn.
* Có Store đích.
* Có tồn kho tại Store nguồn.
* Có nguyên liệu chuyển.
* Có phiếu chuyển kho.
* Sau xác nhận, kho nguồn giảm và kho đích tăng.

### Kịch bản 5: Giá nhà cung cấp

* Có ít nhất một lịch sử giá cũ.
* Có một giá hiện tại.
* Chỉ có một bản ghi `IsCurrent = 1` cho mỗi quan hệ nhà cung cấp – nguyên liệu.
* Ngày hiệu lực phải hợp lý.
* Giá và đơn vị đóng gói phải phù hợp.

### Kịch bản 6: Công nợ nhà cung cấp

Nếu Stored Procedure có xử lý công nợ:

* Có hình thức thanh toán ngay.
* Có hình thức thanh toán sau.
* Có hạn thanh toán.
* Có phiếu nhập phát sinh công nợ.
* Có dữ liệu để kiểm tra thanh toán một phần hoặc toàn bộ.

## 6. Quy tắc ID

* Tiếp tục từ ID lớn nhất trong seed hiện tại.
* Không reset ID về 1 nếu bảng đã có seed.
* Không trùng khóa chính.
* Không trùng business code.
* Không hard-code ID sai quan hệ.
* Phải tạo bảng mapping ID nếu dữ liệu phức tạp.
* Kiểm tra toàn bộ khóa ngoại trước khi xuất kết quả.

## 7. Quy tắc ngày tháng

Dữ liệu seed phải dùng ngày cố định để có thể test lặp lại.

Không sử dụng:

```sql
GETDATE()
```

trừ khi Stored Procedure bắt buộc phải kiểm tra thời gian hiện tại và có giải thích rõ.

Ưu tiên các ngày cố định, ví dụ:

```sql
'2026-01-01'
'2026-01-15'
'2026-02-01'
```

Ngày phải bảo đảm:

* Giá cũ có ngày hiệu lực trước giá hiện tại.
* Phiếu đã xác nhận có `ConfirmedAt`.
* Phiếu nháp không có `ConfirmedAt`.
* Ngày hết hạn phải sau ngày tạo.
* Công nợ phải có hạn thanh toán hợp lý.

## 8. Quy tắc Unicode

Mọi chuỗi tiếng Việt trong SQL Server phải sử dụng tiền tố `N`.

Ví dụ:

```sql
N'Nhà cung cấp nguyên liệu'
N'Giá hiện tại'
N'Phiếu nhập kho kiểm thử'
```

Không được tạo chuỗi bị lỗi encoding như:

```text
Giá hi?n t?i
Nhà cung c?p
```

## 9. Quy tắc `IDENTITY_INSERT`

Với bảng có cột Identity và cần chỉ định ID:

```sql
SET IDENTITY_INSERT TableName ON;
GO

INSERT INTO TableName (...)
VALUES (...);
GO

SET IDENTITY_INSERT TableName OFF;
GO
```

Phải bảo đảm:

* Mỗi thời điểm chỉ có một bảng bật `IDENTITY_INSERT`.
* Luôn tắt sau khi insert.
* Không thiếu cột bắt buộc.
* Số lượng cột phải khớp số lượng value.

## 10. Thứ tự chạy seed

Seed phải được sắp xếp theo thứ tự phụ thuộc khóa ngoại.

Ví dụ:

```text
Configuration
→ Stores
→ Roles
→ Accounts
→ Staffs
→ Suppliers
→ Units
→ Ingredients
→ SupplierIngredients
→ SupplierPriceHistories
→ InventoryDocuments
→ InventoryDocumentDetails
→ InventoryTransactions
→ InventoryCostLayers
```

Thứ tự thực tế phải dựa trên database hiện tại.

## 11. Khả năng chạy lại seed

Ưu tiên seed có thể chạy lại mà không lỗi.

Có thể sử dụng:

```sql
IF NOT EXISTS (...)
BEGIN
    INSERT ...
END
```

Nếu file seed hiện tại đang dùng seed một lần với `IDENTITY_INSERT`, phải giữ đồng nhất phong cách nhưng cần cảnh báo rõ rằng script không idempotent.

Không được vừa xóa toàn bộ bảng vừa insert lại, trừ khi đó là file reset database riêng.

---

# III. SỬA CAFECHAIN_STORE1_COMPLETE_DEMO_MENU_SEED

## 1. File cơ sở

Hãy sử dụng file:

```text
CafeChain_Store1_Complete_Demo_Menu_Seed
```

làm nguồn dữ liệu chính cho Store 1.

Phải giữ nguyên các dữ liệu menu đang hoạt động, trừ khi dữ liệu đó gây lỗi khóa ngoại hoặc sai nghiệp vụ.

## 2. Mục tiêu

Sửa và bổ sung Configuration của nhà cung cấp để có thể test đầy đủ nghiệp vụ Phiếu Kho, đặc biệt là:

* Tạo phiếu nhập từ nhà cung cấp.
* Chọn nhà cung cấp.
* Lấy danh sách nguyên liệu theo nhà cung cấp.
* Lấy giá hiện tại.
* Lấy đơn vị đóng gói.
* Quy đổi về đơn vị cơ sở.
* Tính thành tiền.
* Tính VAT nếu hệ thống hỗ trợ.
* Tạo phiếu nháp.
* Xác nhận phiếu.
* Tăng tồn kho.
* Phát sinh công nợ nếu thanh toán sau.
* Kiểm tra lịch sử giá.

## 3. Phân tích Configuration của nhà cung cấp

Hãy kiểm tra các EntityTypeConfiguration, Model hoặc seed liên quan đến:

* Supplier.
* SupplierStore.
* SupplierIngredient.
* IngredientSupplier.
* IngredientSupplierPriceHistory.
* SupplierConfiguration.
* PaymentTerm.
* PackageUnit.
* BaseUnit.
* MinimumOrderQuantity.
* LeadTime.
* TaxCode.
* VAT.
* Active.
* IsPreferred.
* IsCurrent.

Chỉ sử dụng các field thực sự tồn tại.

## 4. Dữ liệu nhà cung cấp cần có

Tạo đủ dữ liệu để Store 1 có thể test.

Tối thiểu nên có:

### Nhà cung cấp 1: Nhà cung cấp tổng hợp

* Cung cấp nhiều nguyên liệu.
* Có giá mua hợp lệ.
* Có đơn vị đóng gói.
* Có thanh toán sau.
* Có hạn thanh toán.
* Đang hoạt động.

### Nhà cung cấp 2: Nhà cung cấp nguyên liệu tươi

* Cung cấp trái cây, sữa hoặc nguyên liệu tươi.
* Giá và đơn vị đóng gói khác nhà cung cấp 1.
* Có thể thanh toán ngay.
* Đang hoạt động.

### Nhà cung cấp 3: Nhà cung cấp không hoạt động

* Có dữ liệu liên kết để test.
* `Active = 0`.
* Không được hiển thị trong combobox tạo phiếu mới nếu nghiệp vụ quy định như vậy.

Không bắt buộc đúng ba nhà cung cấp nếu seed hiện tại đã có cấu trúc khác. Hãy điều chỉnh theo dữ liệu thực tế.

## 5. Cấu hình quan hệ nhà cung cấp – nguyên liệu

Mỗi nguyên liệu dùng để test nhập kho phải có:

* Nhà cung cấp tương ứng.
* Giá mua hiện tại.
* PackageQuantity.
* PackageUnitId.
* EffectiveDate.
* IsCurrent.
* Active nếu có.
* MinimumOrderQuantity nếu có.
* SupplierCode hoặc IngredientCode đúng dữ liệu hiện tại.

Ví dụ nghiệp vụ:

```text
Nhà cung cấp bán 1 thùng sữa gồm 12 hộp.
Mỗi hộp 1 lít.
Đơn vị cơ sở của tồn kho là ml.
Khi nhập 2 thùng, hệ thống phải quy đổi đúng tổng số ml.
```

Không hard-code ví dụ này nếu Model không hỗ trợ nhiều cấp đóng gói.

## 6. Lịch sử giá

Với một số nguyên liệu, tạo:

* Một giá cũ có `IsCurrent = 0`.
* Một giá hiện tại có `IsCurrent = 1`.
* Ngày giá hiện tại sau ngày giá cũ.
* Không có hai bản ghi hiện tại cho cùng một nhà cung cấp – nguyên liệu.

## 7. Cấu hình cho Phiếu Kho

Hãy kiểm tra các Configuration hoặc Settings liên quan đến:

* Cho phép kho âm.
* Phê duyệt kho âm.
* Tạo công nợ.
* VAT mặc định.
* Số ngày thanh toán.
* Quy tắc xác nhận phiếu.
* Quy tắc FIFO.
* Quy tắc giá vốn.
* Request deduplication.
* Store mặc định.
* Nhà cung cấp mặc định.
* Trạng thái phiếu.
* Quyền người xác nhận.

Bổ sung seed đúng nơi nếu các cấu hình này được lưu trong database.

Nếu Configuration là `appsettings.json`, `Options` hoặc code configuration thì không đưa vào SQL seed. Khi đó phải chỉnh đúng file cấu hình tương ứng và giải thích rõ.

## 8. Đảm bảo dữ liệu menu và dữ liệu kho đồng bộ

Các Ingredient đang được dùng trong Recipe của menu Store 1 cần có dữ liệu kho và dữ liệu nhà cung cấp phù hợp để test.

Phải kiểm tra:

```text
Drink
→ Recipe
→ RecipeIngredient
→ Ingredient
→ Unit
→ SupplierIngredient
→ SupplierPrice
→ Inventory
```

Không tạo menu dùng Ingredient không thể nhập kho.

Không tạo SupplierIngredient tham chiếu Ingredient không tồn tại.

---

# IV. KIỂM TRA WORKFLOW PHIẾU KHO

Sau khi bổ sung seed, phải kiểm tra theo workflow:

```text
Mở trang tạo Phiếu Kho
→ Chọn loại phiếu
→ Chọn Store
→ Chọn nhà cung cấp
→ Load nguyên liệu của nhà cung cấp
→ Chọn nguyên liệu
→ Load giá và đơn vị đóng gói
→ Nhập số lượng
→ Tính tổng tiền
→ Lưu nháp
→ Xem chi tiết
→ Xác nhận
→ Ghi InventoryTransaction
→ Cập nhật tồn kho
→ Tạo Cost Layer
→ Tạo công nợ nếu cần
```

Phải đối chiếu từng bước với:

* Controller.
* Service.
* Repository.
* DTO.
* ViewModel.
* JavaScript.
* Stored Procedure.
* Database Configuration.

Nếu một bước không dùng Stored Procedure mà dùng EF Core, phải ghi rõ.

## Các test case tối thiểu

1. Tạo phiếu nhập với nhà cung cấp hợp lệ.
2. Nhà cung cấp không hoạt động không xuất hiện.
3. Chọn nhà cung cấp có danh sách nguyên liệu.
4. Chọn nhà cung cấp không có nguyên liệu.
5. Lấy đúng giá hiện tại.
6. Không lấy giá cũ làm giá hiện tại.
7. Chọn đúng PackageUnit.
8. Quy đổi đúng BaseUnit.
9. Số lượng bằng 0 bị từ chối.
10. Số lượng âm bị từ chối khi nhập từ nhà cung cấp.
11. Lưu nháp thành công.
12. Xác nhận phiếu thành công.
13. Tồn kho tăng đúng.
14. Cost Layer được tạo đúng.
15. Transaction được tạo đúng.
16. Công nợ được tạo đúng với nhà cung cấp thanh toán sau.
17. Không tạo công nợ với thanh toán ngay.
18. Không tạo hai phiếu khi double-click.
19. Xác nhận lại phiếu đã xác nhận phải bị từ chối.
20. Không cho xác nhận khi thiếu giá hoặc đơn vị quy đổi.

---

# V. SỬA APP LAUNCHER CHO POS MỚI

## 1. Vấn đề hiện tại

Trong App Launcher, khi người dùng bấm vào ứng dụng `POS`, hệ thống vẫn đang sử dụng đường dẫn hoặc cơ chế khởi chạy POS cũ.

Điều này không còn phù hợp với kiến trúc hiện tại.

POS mới đang sử dụng:

* `CafeChain.Bridge`.
* `CafeChain.Frontend`.

URL của POS hiện tại:

```text
http://127.0.0.1:5173/order
```

## 2. Mục tiêu

Khi người dùng bấm `POS` trong App Launcher:

1. Kiểm tra hoặc khởi chạy `CafeChain.Bridge`.
2. Kiểm tra hoặc khởi chạy `CafeChain.Frontend`.
3. Chờ Frontend sẵn sàng theo cơ chế hiện tại của hệ thống.
4. Mở trình duyệt tại:

```text
http://127.0.0.1:5173/order
```

Không được tiếp tục sử dụng URL POS cũ.

## 3. Phân tích App Launcher hiện tại

Hãy tìm và phân tích:

* View chứa nút POS.
* JavaScript xử lý click POS.
* Controller hoặc endpoint launcher.
* Service khởi chạy ứng dụng.
* Configuration chứa URL.
* Process start logic.
* Health-check logic.
* Port configuration.
* Các app path cũ.
* Cách mở trình duyệt.
* Cách kiểm tra process đã chạy.
* Cơ chế log lỗi.
* Cơ chế chống mở nhiều process.

Phải chỉ rõ đường dẫn cũ đang được lấy từ đâu:

* Hard-code trong View.
* Hard-code trong JavaScript.
* Controller.
* Service.
* `appsettings.json`.
* Environment variable.
* Database setting.
* File JSON riêng của Launcher.

## 4. Không hard-code URL rải rác

Không được ghi:

```csharp
"http://127.0.0.1:5173/order"
```

ở nhiều file.

Phải sử dụng một nguồn cấu hình tập trung theo cấu trúc dự án hiện tại.

Ví dụ nếu hệ thống đang dùng `appsettings.json`:

```json
{
  "AppLauncher": {
    "Pos": {
      "BridgeProject": "CafeChain.Bridge",
      "FrontendProject": "CafeChain.Frontend",
      "Url": "http://127.0.0.1:5173/order",
      "HealthCheckUrl": "http://127.0.0.1:5173"
    }
  }
}
```

Đây chỉ là ví dụ. Không tự thêm đúng cấu trúc này nếu dự án đã có Configuration khác.

## 5. Quy tắc khởi chạy Bridge

Trước khi khởi chạy, phải kiểm tra:

* Bridge đã chạy chưa.
* Port của Bridge có đang lắng nghe không.
* Có process cùng tên đang chạy không.
* Đường dẫn project hoặc executable có tồn tại không.
* Có cần `dotnet run`, chạy file `.exe` hoặc command riêng không.

Không mở nhiều instance của Bridge.

Nếu Bridge đã chạy, bỏ qua bước khởi động lại.

Nếu Bridge khởi động lỗi:

* Không mở POS như thể thành công.
* Hiển thị lỗi rõ ràng.
* Ghi log nguyên nhân.
* Không để loading vô hạn.

## 6. Quy tắc khởi chạy Frontend

Trước khi khởi chạy, phải kiểm tra:

* Frontend đã chạy tại port `5173` chưa.
* Có cần chạy `npm run dev`, `pnpm dev`, `yarn dev` hoặc executable khác không.
* Phải dựa vào `package.json` và cấu trúc thực tế.
* Không tự đoán package manager.
* Không chạy thêm Frontend nếu port đã sẵn sàng.
* Không mở nhiều terminal hoặc process trùng.

Nếu Frontend đã chạy, mở trực tiếp URL POS.

## 7. Health check

Không dùng `Thread.Sleep` cố định làm giải pháp chính.

Nên dùng cơ chế kiểm tra:

* HTTP request đến Frontend.
* TCP port check.
* Timeout có giới hạn.
* Retry với khoảng nghỉ ngắn.

Ví dụ workflow:

```text
Kiểm tra Bridge
→ Nếu chưa chạy thì khởi chạy Bridge
→ Kiểm tra Frontend
→ Nếu chưa chạy thì khởi chạy Frontend
→ Health check Frontend
→ Khi Frontend sẵn sàng thì mở /order
```

Phải có timeout.

Không chờ vô hạn.

## 8. Mở trình duyệt

Khi Frontend đã sẵn sàng, mở:

```text
http://127.0.0.1:5173/order
```

Nếu hệ thống dùng C#:

* Dùng cơ chế mở URL tương thích Windows hiện tại.
* Không khóa request trong thời gian dài.
* Không để process của Launcher bị treo.

Nếu App Launcher chạy trong trình duyệt và chỉ cần điều hướng:

* Có thể dùng `window.open`.
* Nhưng vẫn phải bảo đảm Bridge và Frontend đã sẵn sàng trước.

## 9. Chống bấm nhiều lần

Khi người dùng bấm POS:

* Disable nút tạm thời.
* Hiển thị trạng thái đang khởi chạy.
* Không gửi nhiều request launcher cùng lúc.
* Không tạo nhiều process.
* Enable lại khi thành công hoặc thất bại.
* Nếu POS đã chạy, mở trực tiếp.

## 10. Thông báo trạng thái

Giao diện nên hiển thị các trạng thái rõ ràng:

```text
Đang kiểm tra CafeChain.Bridge...
Đang khởi chạy CafeChain.Bridge...
Đang kiểm tra CafeChain.Frontend...
Đang khởi chạy CafeChain.Frontend...
POS đã sẵn sàng.
Không thể khởi chạy POS.
```

Không hiển thị lỗi chung chung như:

```text
Có lỗi xảy ra.
```

## 11. Bảo mật

Không cho phép client truyền tùy ý:

* Đường dẫn executable.
* Tên process.
* Command.
* Working directory.
* URL cần mở.

Các giá trị phải lấy từ Configuration đáng tin cậy.

Không ghép trực tiếp dữ liệu request vào command line.

---

# VI. CÁC TRƯỜNG HỢP APP LAUNCHER CẦN KIỂM THỬ

1. Bridge và Frontend đều chưa chạy.
2. Bridge đã chạy, Frontend chưa chạy.
3. Frontend đã chạy, Bridge chưa chạy.
4. Cả hai đều đã chạy.
5. Frontend chạy đúng port 5173.
6. Port 5173 bị ứng dụng khác chiếm.
7. Không tìm thấy project Bridge.
8. Không tìm thấy project Frontend.
9. Thiếu Node.js hoặc package manager.
10. Bridge khởi động thất bại.
11. Frontend khởi động thất bại.
12. Frontend khởi động chậm.
13. Health check timeout.
14. Người dùng bấm POS nhiều lần.
15. URL mở đúng `/order`.
16. Không còn sử dụng URL POS cũ.
17. Launcher không mở nhiều process trùng.
18. Lỗi được ghi log đầy đủ.
19. Nút POS được mở khóa lại khi lỗi.
20. Sau khi POS đã chạy, lần bấm tiếp theo mở nhanh mà không khởi động lại.

---

# VII. FILE TÔI SẼ CUNG CẤP

Tôi sẽ gửi các file cần thiết, có thể bao gồm:

## Database và seed

* File SQL chứa Stored Procedure.
* Script tạo bảng hoặc database schema.
* `CafeChain_Store1_Complete_Demo_Menu_Seed`.
* Các file seed Supplier.
* Các file seed Ingredient.
* Các file seed Unit.
* Các file seed Inventory.
* Các file Configuration liên quan.
* Enum và Constants.

## Phiếu Kho

* Inventory Model.
* DTO.
* ViewModel.
* Controller.
* Service.
* Repository.
* JavaScript.
* View.
* Stored Procedure liên quan.

## App Launcher

* View App Launcher.
* JavaScript App Launcher.
* Controller.
* Service.
* Configuration.
* `appsettings.json`.
* Project structure của `CafeChain.Bridge`.
* Project structure của `CafeChain.Frontend`.
* `package.json`.
* Các script chạy ứng dụng hiện tại.

Hãy chỉ sử dụng các file tôi thực sự cung cấp.

---

# VIII. TRÌNH TỰ THỰC HIỆN BẮT BUỘC

## Giai đoạn 1: Phân tích

Trình bày:

1. Danh sách Stored Procedure.
2. Mục đích từng Stored Procedure.
3. Danh sách bảng liên quan.
4. Quan hệ khóa ngoại.
5. Dữ liệu seed hiện có.
6. Dữ liệu còn thiếu.
7. Các lỗi hoặc dữ liệu không nhất quán.
8. Configuration Supplier hiện tại.
9. Workflow Phiếu Kho hiện tại.
10. App Launcher đang dùng đường dẫn POS cũ ở đâu.
11. Cách Bridge và Frontend đang được chạy.

## Giai đoạn 2: Đề xuất

Trình bày:

1. Danh sách bảng cần bổ sung seed.
2. Số lượng dữ liệu dự kiến cho từng bảng.
3. Mapping ID.
4. Thứ tự chạy seed.
5. Kịch bản kiểm thử.
6. Configuration cần sửa.
7. File App Launcher cần sửa.
8. Cơ chế health check.
9. Cơ chế chống process trùng.

## Giai đoạn 3: Thực hiện

Sau khi đã có đủ file:

1. Tạo seed SQL hoàn chỉnh.
2. Sửa `CafeChain_Store1_Complete_Demo_Menu_Seed`.
3. Bổ sung Configuration nhà cung cấp.
4. Sửa App Launcher.
5. Thay URL POS cũ bằng cấu hình URL mới.
6. Sử dụng `CafeChain.Bridge` và `CafeChain.Frontend`.
7. Bổ sung xử lý lỗi và log.
8. Bổ sung test case.

---

# IX. YÊU CẦU KẾT QUẢ ĐẦU RA

Khi hoàn thành, hãy cung cấp theo đúng thứ tự:

## 1. Báo cáo phân tích

* Stored Procedure nào cần dữ liệu gì.
* Bảng nào cần seed.
* Quan hệ dữ liệu.
* Configuration Supplier còn thiếu.
* Nguyên nhân Phiếu Kho chưa test được.
* Nguyên nhân App Launcher vẫn mở POS cũ.

## 2. Danh sách file đã sửa

Ghi rõ:

```text
Đường dẫn file
Mục đích sửa
Các method hoặc section đã thay đổi
```

Không được bịa đường dẫn file.

## 3. Seed SQL hoàn chỉnh

* Trình bày thành một script có thể copy và chạy.
* Đúng thứ tự khóa ngoại.
* Có `GO` hợp lý.
* Có `IDENTITY_INSERT` đúng.
* Có Unicode `N''`.
* Không thiếu cột.
* Không trùng ID.
* Không lỗi số lượng cột và value.
* Có chú thích từng section.

## 4. Code App Launcher hoàn chỉnh

Không chỉ đưa đoạn code rời rạc.

Phải cung cấp đầy đủ:

* Method đã sửa.
* Configuration đã sửa.
* JavaScript đã sửa.
* View liên quan nếu có.
* Error handling.
* Health check.
* Process detection.
* URL POS mới.

## 5. Test checklist

Ghi rõ:

```text
Test case
Dữ liệu đầu vào
Kết quả mong đợi
Kết quả thực tế
```

## 6. Phần chưa thể thực hiện

Nếu thiếu file, phải ghi rõ:

* File nào còn thiếu.
* Vì sao file đó cần thiết.
* Phần nào chưa thể hoàn thiện.
* Không tự suy đoán để lấp chỗ trống.

---

# X. TIÊU CHÍ HOÀN THÀNH

Chỉ xem là hoàn thành khi đáp ứng đầy đủ:

* Seed bám đúng Stored Procedure.
* Seed không lỗi khóa ngoại.
* Có đủ dữ liệu test Phiếu Kho.
* Store 1 có nhà cung cấp và nguyên liệu hợp lệ.
* Có giá hiện tại và lịch sử giá.
* Có PackageUnit và quy đổi đúng.
* Có thể tạo và xác nhận phiếu nhập.
* Tồn kho được cập nhật đúng.
* Cost Layer và Transaction được tạo nếu nghiệp vụ yêu cầu.
* Công nợ được tạo đúng theo Configuration.
* Nhà cung cấp không hoạt động không xuất hiện khi tạo phiếu.
* App Launcher không còn sử dụng POS cũ.
* App Launcher sử dụng `CafeChain.Bridge`.
* App Launcher sử dụng `CafeChain.Frontend`.
* POS mở đúng URL:

```text
http://127.0.0.1:5173/order
```

* Không mở nhiều Bridge hoặc Frontend process.
* Có health check và timeout.
* Có xử lý lỗi rõ ràng.
* Không phá vỡ các chức năng hiện tại.

---

# XI. BÁO CÁO THỰC HIỆN REFACTOR NGÀY 2026-07-18

## 1. Kết quả phân tích Stored Procedure Dashboard

File `Scripts/20260717_DashboardAnalyticsStoredProcedures.idempotent.sql` hiện có đúng 46 Stored Procedure. Các procedure chỉ đọc dữ liệu báo cáo; seed mới không sửa nội dung procedure.

| Nhóm | Stored Procedure | Bảng dữ liệu chính cần có |
| --- | --- | --- |
| Dashboard tổng hợp | `usp_Dashboard_NetSalesTrend`, `usp_Dashboard_StoreRanking`, `usp_Dashboard_PaymentMethodMix`, `usp_Dashboard_OrderHeatmap`, `usp_Dashboard_OperationalAlerts`, `sp_Revenue_By_Store`, `sp_Revenue_Filtered`, `sp_Revenue_By_PaymentMethod_Filtered`, `sp_Revenue_By_Hour`, `sp_Dashboard_Summary_Filtered` | `Orders`, `OrderDetails`, `Payments`, `OrderRefunds`, `Stores` |
| Kho | `usp_Inventory_ShortageRisk`, `usp_Inventory_MovementByType`, `usp_Inventory_ThresholdRisk`, `usp_Inventory_ReorderSuggestions`, `usp_Inventory_WasteByStoreIngredient`, `usp_Inventory_FifoLayerAge`, `sp_Inventory_Summary`, `sp_Waste_Report` | `StoreIngredients`, `Ingredients`, `InventoryTransactions`, `InventoryCostLayers`, `RestockRequests` |
| Mua hàng/Supplier | `usp_Procurement_PurchaseOrderPipeline`, `usp_Procurement_OverduePurchaseOrders`, `usp_Procurement_SupplierQuality`, `usp_Procurement_PurchasePriceTrend`, `usp_Procurement_SpendBreakdown`, `usp_Procurement_SupplierIssueMix` | `Suppliers`, `SupplierStores`, `IngredientSuppliers`, `IngredientSupplierPriceHistories`, `PurchaseOrders`, `PurchaseOrderLines`, `BranchReceipts`, `BranchReceiptLines`, `SupplierReceiptIssues` |
| Ca làm việc/vận hành | `usp_Operations_WorkShiftCashDiscrepancy`, `usp_Operations_WorkShiftSales`, `usp_Operations_WorkShiftPaymentMix`, `usp_Operations_OfflineReconciliationExceptions`, `usp_Operations_HourlyOrders`, `usp_Operations_WorkShiftTopDiscrepancies`, `usp_Operations_WorkShiftKpis`, `sp_Cash_Flow_Today`, `sp_Staff_Performance_Filtered` | `WorkShifts`, `StaffShifts`, `CashSessions`, `Orders`, `Payments`, `Staffs` |
| Sản phẩm/BOM | `usp_Product_TopProducts`, `usp_Product_VolumeMarginMatrix`, `usp_Product_SizeMargin`, `usp_Product_TopToppings`, `usp_Product_BomHealth`, `usp_Product_HighConsumptionLowEfficiency`, `sp_Top_Selling_Drinks_Filtered`, `sp_Top_Toppings_Filtered` | `Drinks`, `Sizes`, `Toppings`, `OrderDetails`, `OrderToppings`, `Recipes`, `RecipeDetails`, `Ingredients` |
| Nhân sự | `usp_Workforce_ShiftStatus`, `usp_Workforce_HourlyDemand`, `usp_Workforce_StaffPerformance` | `Staffs`, `StaffShifts`, `WorkShifts`, `Orders` |
| Khách hàng/trạng thái | `sp_Top_Customers`, `sp_Order_Status_Stats` | `Customers`, `Orders`, `OrderStatuses` |

Các bước tạo và xác nhận Phiếu Kho hiện tại đi theo `Controller -> Service -> Repository -> EF Core`; không gọi 46 Stored Procedure Dashboard. Stored Procedure chỉ phục vụ truy vấn thống kê sau khi nghiệp vụ đã ghi dữ liệu.

## 2. Nguồn ID và thứ tự chạy seed

EF Core Configuration/`HasData` là nguồn ID nền. Ba file sau được giữ nguyên nội dung và không được thay thế:

1. `Part1_SeedDataDrink.sql`: Category 4–8, Drink 7–30, DrinkImage 25–120; phải chạy trước seed Store 1.
2. `Part7_SeedDataPermission.sql`: PermissionGroup 6–8, Permission 5–26.
3. `SeedDataDiaChi.sql`: giữ nguyên toàn bộ Province/District/Ward và các ID rời rạc.

Thứ tự chạy chính thức:

1. Migration hiện có để tạo schema và dữ liệu `HasData`.
2. Chạy nguyên trạng `Part1_SeedDataDrink.sql`.
3. Chạy nguyên trạng `Part7_SeedDataPermission.sql`.
4. Chạy nguyên trạng `SeedDataDiaChi.sql`.
5. Chạy `Scripts/20260718_CafeChain_Store1_Complete_Demo_Seed.idempotent.sql`.
6. Chạy `Scripts/20260717_DashboardAnalyticsStoredProcedures.idempotent.sql`.
7. Chạy `Scripts/20260718_Dashboard_Demo_Data_Seed.idempotent.sql`.

Không chạy Part2–Part6 cùng bộ seed mới. Hai script mới không reset identity và không dùng ID tùy ý cho quan hệ nghiệp vụ; khóa ngoại được tra qua `DrinkCode`, `Supplier.Code`, `Ingredient.Code`, `UnitCode`, `OrderCode` và marker `DEMO_*`/`DASH_DEMO_*`.

## 3. Seed Store 1 đã bổ sung

`20260718_CafeChain_Store1_Complete_Demo_Seed.idempotent.sql` thực hiện:

- Không hard-code `USE [CafeChain]`; script chạy đúng database mà connection hiện tại đã chọn và fail-fast theo schema.
- Kích hoạt menu Store 1 cho các Drink từ Part1 mà không tạo lại Drink, Category, Size hoặc Topping.
- Bổ sung Recipe/RecipeDetails/BOM còn thiếu từ Ingredient và Unit đã được EF Configuration seed.
- Tạo hai Supplier hoạt động và một Supplier không hoạt động bằng business code.
- Tạo SupplierStore theo Store, IngredientSupplier, PackageQuantity, PackageUnit, MOQ, giá cũ và giá hiện tại.
- Bảo đảm mỗi offer chỉ có đúng một lịch sử giá `IsCurrent = 1`.
- Tạo tồn đầu kỳ, InventoryDocument, InventoryDocumentDetails, InventoryTransactions và FIFO InventoryCostLayers cân bằng.
- Tạo dữ liệu nháp chuyển kho để kiểm thử workflow Store 1 sang Store 2.
- Dùng ngày cố định, Unicode `N''`, `XACT_ABORT`, transaction, fail-fast schema check và kiểm tra invariant cuối script.

## 4. Seed Dashboard Store 1 đã bổ sung

`20260718_Dashboard_Demo_Data_Seed.idempotent.sql` tạo dữ liệu có marker riêng cho:

- Không hard-code database; có đầy đủ SET options cần thiết cho filtered index/computed index của schema hiện tại.
- `Orders`, `OrderDetails`, `OrderToppings`, `Payments`, `OrderRefunds` với nhiều trạng thái, giờ bán, sản phẩm, topping và phương thức thanh toán.
- `WorkShifts`, `StaffShifts`, `CashSessions` để kiểm thử doanh số và chênh lệch tiền theo ca.
- `PurchaseOrders`, `PurchaseOrderLines`, `BranchReceipts`, `BranchReceiptLines`, `SupplierReceiptIssues`, `RestockRequests`.
- Phiếu hao hụt và InventoryTransaction để báo cáo waste/movement có dữ liệu.

Ngoại lệ duy nhất về ngày động là dữ liệu CashSession phục vụ `sp_Cash_Flow_Today`, vì procedure này hard-code ngày hiện tại. Các dữ liệu lịch sử còn lại dùng ngày cố định tháng 01/2026. Script tra khóa ngoại theo business code và chạy lại không nhân đôi dữ liệu sở hữu bởi marker.

## 5. Refactor Supplier và Phiếu Kho

- Supplier dropdown được lọc theo `storeId`, `Supplier.IsActive` và `SupplierStore.IsActive`.
- Endpoint nguyên liệu nhận cả `supplierId` và `storeId`; kiểm tra phạm vi Store của tài khoản.
- Repository chỉ trả offer hoạt động có đúng một current price history.
- Service kiểm tra Supplier–Store, offer, giá hiện tại, `PackageQuantity > 0`, conversion factor, MOQ và số lượng dương.
- Package price được quy đổi thành đơn giá nhập và base-unit cost; không còn loại bỏ package có `PackageQuantity > 1`.
- Khi đổi Store hoặc loại phiếu, UI tải lại Supplier và xóa dữ liệu Supplier/chi tiết không còn hợp lệ.

## 6. App Launcher POS mới

- POS không còn điều hướng tới `/Admin/AdminPOS`.
- Project thực tế được giữ là `CafeChain.PrintBridge`; không tạo tên project giả `CafeChain.Bridge`.
- Cấu hình tập trung tại `appsettings.json`, gồm project/directory tương đối, StoreId, URL `/order`, Health URL, port, retry và timeout.
- `POST /AppLauncher/LaunchPos` và `GET /AppLauncher/PosStatus` chỉ lấy Store từ claim, không nhận path/command/URL từ client.
- `PosLaunchCoordinator` singleton khóa request đồng thời, kiểm tra heartbeat Bridge, port/HTTP Frontend, khởi chạy `dotnet run` và `npm run dev -- --host 127.0.0.1 --port 5173 --strictPort` khi cần.
- `PrintBridgeHub` cập nhật registry heartbeat; PrintBridge dùng named mutex theo Store để ngăn hai instance.
- Client mở tab trống ngay trong sự kiện click, khóa card, hiển thị trạng thái, gọi `IssuePosToken`, chuyển tới `/order#pos_token=...`, đóng tab khi lỗi và luôn mở khóa trong `finally`.

## 7. Danh sách file chính đã sửa/tạo

| File | Mục đích |
| --- | --- |
| `Scripts/20260718_CafeChain_Store1_Complete_Demo_Seed.idempotent.sql` | Seed menu/kho/Supplier Store 1 theo EF Configuration và Part1 |
| `Scripts/20260718_Dashboard_Demo_Data_Seed.idempotent.sql` | Seed dữ liệu cho 46 Stored Procedure Dashboard |
| `Infrastructure/Repositories/Admin/InventoryDocuments/AdminInventoryDocumentRepository.cs` | Lọc Supplier/offer/current price theo Store |
| `Application/Services/Admin/InventoryDocuments/AdminInventoryDocumentCreateService.cs` | Validation Supplier/package/conversion/MOQ và chuẩn hóa giá |
| `Areas/Admin/Controllers/AdminInventoryDocumentController.cs` | Endpoint Supplier/SupplierIngredients có Store |
| `wwwroot/js/Admin/InventoryDocument/inventorydocumentcreate.js` | Đồng bộ Store–Supplier và package pricing trên UI |
| `Application/Options/PosLauncherOptions.cs` | Contract cấu hình POS tập trung |
| `Application/Services/AppLauncher/PosLaunchCoordinator.cs` | Điều phối Bridge/Frontend, health check, timeout, chống trùng |
| `Application/Services/AppLauncher/PrintBridgePresenceTracker.cs` | Theo dõi heartbeat Bridge theo Store |
| `Controllers/AppLauncherController.cs` | API LaunchPos/PosStatus bảo vệ bằng permission và anti-forgery |
| `Hubs/PrintBridgeHub.cs` | Cập nhật online/heartbeat/disconnect vào tracker |
| `Views/AppLauncher/Index.cshtml`, `wwwroot/js/AppLauncher/app-launcher.js` | UX khởi chạy POS, token handoff và chống double-click |
| `CafeChain.PrintBridge/Program.cs` | Named mutex ngăn Bridge trùng Store |
| `CafeChain.Tests/SeedAndPosLauncherRefactorTests.cs` | Test marker seed, legacy route và heartbeat expiry |

## 8. Test checklist và kết quả thực tế

| Test case | Đầu vào | Mong đợi | Thực tế |
| --- | --- | --- | --- |
| Parse seed Store 1 | SQL Server `SET PARSEONLY ON` | Không lỗi cú pháp, không ghi dữ liệu | Đạt |
| Parse seed Dashboard | SQL Server `SET PARSEONLY ON` | Không lỗi cú pháp, không ghi dữ liệu | Đạt |
| Build solution | `dotnet build CafeChain.slnx --no-restore` | 0 lỗi | Đạt, 0 lỗi; warning cũ còn tồn tại |
| Test Supplier package | PackageQuantity khác 1 | Quy đổi đúng package/base-unit cost | Đạt |
| Test launcher/seed marker | 29 test mục tiêu | Tất cả pass | Đạt 29/29 |
| Regression Phiếu Kho/Supplier | 82 test liên quan | Tất cả pass | Đạt 82/82 |
| Build Frontend | `npm.cmd run build` | TypeScript và Vite build thành công | Đạt; chỉ có warning bundle/SignalR dependency |
| Part1 và Part7 trên database sạch tạm | Migration + file gốc trong Downloads | Chạy đúng sau `HasData` | Đạt |
| Seed Store 1 lần 1/lần 2 | Database sạch tạm, chạy sau Part1/Part7 | Không trùng và không lỗi FK | Đạt; cả hai lần giữ 54 SKU và 7 offer |
| Seed Dashboard lần 1/lần 2 | Store 1 seed + 46 procedure | Không nhân đôi marker | Đạt; cả hai lần giữ 9 order, 1 PO và 1 supplier issue |
| Chạy đủ 46 Stored Procedure | Store 1, `2026-01-01` đến `2026-01-31` | Không procedure nào lỗi | Đạt 46/46 |
| Đối soát doanh thu | Payment hoàn tất trừ Refund hoàn tất | Khớp NetSales Dashboard | Đạt: 313.000 - 38.000 = 275.000, khớp 275.000 |
| Seed địa chỉ nguyên trạng | `SeedDataDiaChi.sql` qua `sqlcmd` | Chạy sau migration | Chưa đạt: session mặc định thiếu `QUOTED_IDENTIFIER`; thử qua stdin làm sai encoding Unicode. File được giữ nguyên theo yêu cầu và không dùng kết quả lỗi này |
| End-to-end App Launcher | Bridge/Frontend thực tế trên máy triển khai | Mở `/order` sau heartbeat và health check | Chưa chạy end-to-end vì cần process và phiên đăng nhập thực tế |

## 9. Giới hạn schema đã xác nhận

Schema hiện tại không có bảng/configuration riêng cho SupplierDebt, PaymentTerm hoặc cấu hình VAT nhà cung cấp. Vì vậy refactor không tạo bảng giả, không tạo migration và không seed công nợ/thanh toán kỳ hạn. `InventoryDocument` có trường VAT nhưng workflow hiện tại chưa có cấu hình Supplier VAT/PaymentTerm để tự phát sinh công nợ; phần này chỉ có thể triển khai sau khi nghiệp vụ và schema tương ứng được bổ sung chính thức.

Database kiểm thử tạm `CafeChain_RefactorVerify_20260718` đã được xóa sau kiểm tra. Một lần chạy ban đầu phát hiện `USE [CafeChain]` còn sót trong seed mới; SQL Server dừng ở filtered index và toàn bộ transaction được rollback bởi `XACT_ABORT`. Hai script đã được sửa để không còn `USE` cứng; database làm việc không giữ dữ liệu từ lần chạy lỗi. Với `SeedDataDiaChi.sql`, cần chạy bằng SSMS/sqlcmd file mode giữ UTF-8 và bật trước các SET options: `ANSI_NULLS`, `ANSI_PADDING`, `ANSI_WARNINGS`, `ARITHABORT`, `CONCAT_NULL_YIELDS_NULL`, `QUOTED_IDENTIFIER` = ON và `NUMERIC_ROUNDABORT` = OFF. Không chuyển file qua pipeline text vì sẽ làm hỏng tiếng Việt.
