# Bàn giao lỗi hiển thị BTP cho dev 2

## Mục tiêu và phạm vi

Tài liệu này tổng hợp các lỗi còn nằm trong service, Razor/HTML và frontend sau khi dữ liệu seed BTP được chuẩn hóa. Phần sửa hiện tại chỉ thay đổi `Scripts/SeedAll.sql`; dev 2 xử lý các mục bên dưới trong một nhánh riêng.

Không tạo thêm `InventoryDocument IMPORT` khi xác nhận nhập hàng. Trong kiến trúc hiện tại, `BranchReceipt` là chứng từ nhập chính thức và `BranchReceiptService.ConfirmAsync` chịu trách nhiệm tạo `BRANCH_RECEIPT_IN` cùng FIFO cost layer.

## 1. POS branch inventory hạ cấp BTP canonical thành legacy

**Nguồn:** `Application/Services/POS/PosBranchInventoryService.cs`, vùng projection khoảng dòng 129-159 và mapping khoảng dòng 161-211.

**Triệu chứng:** BTP canonical vẫn hiển thị đơn vị `—` và frontend báo “Chưa xác nhận đơn vị tồn” chỉ vì hàng tồn còn giữ `RecipeId` tương thích.

**Nguyên nhân:**

- Projection không lấy `BtpIdentityState` và `QuantitySemanticsStatus` đã lưu trên `StoreInventory`.
- Mapping tại khoảng dòng 187-211 suy luận `RecipeId != null` đồng nghĩa với legacy/unknown.
- Một dòng có cả `PreparedItemId` và compatibility `RecipeId` vẫn có thể là canonical nếu `BtpIdentityState=Canonical` và `QuantitySemanticsStatus=BaseUnitConfirmed`.

**Yêu cầu sửa:**

- Đưa hai trường trạng thái persisted vào projection.
- Khi có `PreparedItemId`, ưu tiên identity của PreparedItem.
- Chỉ đặt `IsLegacyUnmapped=true` khi không có `PreparedItemId` hoặc trạng thái identity thực sự là legacy/unmapped.
- Với BTP canonical đã xác nhận base unit, trả `PreparedItem.BaseUnit.UnitCode/Name` dù compatibility `RecipeId` còn tồn tại.
- Không tự ghi đè trạng thái persisted bằng suy luận từ việc có hay không có `RecipeId`.

**Kiểm thử chấp nhận:**

1. PreparedItem canonical có `RecipeId`: trả loại BTP, đúng mã/tên/đơn vị và `BASE_UNIT_QUANTITY_CONFIRMED`.
2. Recipe legacy không có `PreparedItemId`: tiếp tục trả `IsLegacyUnmapped=true`, đơn vị không được khẳng định.
3. Ingredient: hành vi không đổi.
4. Bộ lọc `RECIPE`/`PREPARED_ITEM` và tìm kiếm vẫn trả đủ các dòng tương thích.

## 2. InventoryItemIdentityResolver bỏ qua trạng thái persisted

**Nguồn:** `Application/Services/Inventories/InventoryItemIdentityResolver.cs`, đặc biệt khoảng dòng 63-93.

**Triệu chứng:** Resolver trả `QuantitySemanticsStatuses.Unknown` cho BTP canonical có cả `PreparedItemId` và `RecipeId`.

**Nguyên nhân:** tại khoảng dòng 90, trạng thái được quyết định chỉ bằng `hasRecipe` thay vì dùng `StoreInventory.QuantitySemanticsStatus` và `BtpIdentityState`.

**Yêu cầu sửa:**

- Ưu tiên trạng thái identity/quantity semantics persisted.
- Compatibility `RecipeId` chỉ là metadata lịch sử, không tự biến một PreparedItem canonical thành legacy.
- Chỉ fallback sang suy luận cũ khi dữ liệu trạng thái persisted thực sự chưa có.
- Giữ nguyên cảnh báo cho các row chỉ có Recipe hoặc row có tổ hợp identity không hợp lệ.

**Kiểm thử chấp nhận:**

1. Canonical PreparedItem + compatibility Recipe trả base unit confirmed.
2. PreparedItem chưa review và không có trạng thái persisted dùng fallback an toàn.
3. Recipe-only trả legacy/unmapped.
4. Ingredient kèm Recipe/PreparedItem sai contract tiếp tục sinh validation issue.

## 3. Razor render literal ContractVersion

**Nguồn:** `Areas/Admin/Views/AdminProductionOrder/Index.cshtml`, dòng khoảng 104.

**Hiện tại:**

```cshtml
Contract v@item.ContractVersion
```

Razor có thể render literal `Contract v@item.ContractVersion` trong markup liền chữ.

**Đề xuất:**

```cshtml
Contract v@(item.ContractVersion)
```

**Kiểm thử chấp nhận:** danh sách lệnh sản xuất hiển thị `Contract v1` hoặc phiên bản thực tế, không còn chuỗi chứa ký tự `@`.

## 4. Cảnh báo ở BranchInventory.tsx là triệu chứng downstream

**Nguồn:** `CafeChain.Frontend/src/pages/BranchInventory.tsx`, khoảng dòng 437-449.

Frontend chỉ hiển thị badge dựa trên `isLegacyUnmapped` và `quantitySemanticsStatus` do backend trả về. Không thêm phép suy luận identity mới ở frontend. Sau khi sửa hai service trên, chỉ cần xác minh:

- BTP canonical không hiện “BTP legacy” hoặc “Chưa xác nhận đơn vị tồn”.
- Recipe-only legacy vẫn hiện cảnh báo.
- Đơn vị kho BTP được hiển thị từ response backend.

## 5. Phân loại các ảnh đã báo

| Hiện tượng | Chủ sở hữu | Kết luận |
|---|---|---|
| BTP canonical hiện `—` hoặc “Chưa xác nhận đơn vị tồn” | Dev 2/service | Sửa trạng thái identity và unit mapping ở hai service trên. |
| Danh sách lệnh hiện `Contract v@item.ContractVersion` | Dev 2/Razor | Sửa ranh giới biểu thức Razor. |
| Sản lượng dự kiến của lệnh là `—` | SeedAll | Batch repair v2 điền output snapshot cho production run SeedAll. |
| Recipe #148 `DEMO_PREP_LEGACY_CREAM` inactive | Không sửa | Đây là fixture archived dùng kiểm tra dữ liệu legacy. |
| Không có `InventoryDocument IMPORT` sau nhận hàng | Không phải lỗi | `BranchReceipt` là authority nhập kho và tạo transaction/FIFO. |

## 6. Regression checklist cho dev 2

- Chạy unit test của `InventoryItemIdentityResolver` với canonical, legacy và invalid identity.
- Chạy integration/API test danh sách branch inventory với BTP có compatibility Recipe.
- Mở màn hình branch inventory và xác nhận mã, tên, đơn vị, badge của Aloe vera base cùng các BTP khác.
- Mở danh sách lệnh sản xuất và xác nhận ContractVersion, số mẻ, sản lượng dự kiến và đơn vị đầu ra.
- Không thay đổi schema, migration hoặc SeedAll trong nhánh sửa service/UI.
