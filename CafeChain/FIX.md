Bạn hãy đóng vai Senior Developer và Database Engineer có 20 năm kinh nghiệm, chuyên ASP.NET MVC, Layered Architecture, EF Core, SQL Server và nghiệp vụ quản lý tồn kho.

Lưu ý:
Một số file trong đây đang được làm bằng giả định, hãy phân tích dựa vào các file có trong dự án hoặc do tôi gửi, tránh trường hợp code không có trong dự án và tự ý thay đổi cũng như bịa ra các dòng code tôi không có.

Nhiệm vụ:
Refactor nghiệp vụ Inventory trong dự án CafeChain để tách rõ:
1. Phiếu Nhập Kho
2. Phiếu Xuất Kho
3. Phiếu Chuyển Kho
4. Cơ chế âm kho có kiểm soát

Mục tiêu chính:
- Xóa các phần nghiệp vụ liên quan đến "Nhập nội bộ" và "Xuất nội bộ" đang bị dùng sai.
- Không dùng Phiếu Nhập hoặc Phiếu Xuất để xử lý chuyển kho.
- Tạo riêng box/màn hình/flow cho "Phiếu Chuyển Kho".
- Cho phép cấu hình kho âm có kiểm soát.
- Chỉnh validation số lượng nhập/xuất/chuyển kho.
- Chỉnh model, enum, configuration, service, controller và view tương ứng.
Dự án CafeChain hiện đang bị nhầm nghiệp vụ giữa:
- Phiếu nhập nội bộ
- Phiếu xuất nội bộ
- Phiếu chuyển kho giữa chi nhánh

Hiện tại một số view hoặc enum có thể đang để "Nhập nội bộ" hoặc "Xuất nội bộ" trong Phiếu Nhập/Phiếu Xuất.

Cần refactor lại để:
- Không còn hiển thị hoặc xử lý nhập/xuất nội bộ trong các màn hình tạo phiếu nhập/xuất.
- Phiếu chuyển kho phải có flow riêng.
- Khi người dùng bấm "Tạo phiếu mới", hệ thống phải có box riêng cho "Phiếu Chuyển Kho", ngang hàng với Phiếu Nhập và Phiếu Xuất.
InventoryDocument chỉ dùng cho các chứng từ một kho:
- Nhập từ nhà cung cấp
- Xuất bán hàng
- Xuất hủy
- Điều chỉnh tăng
- Điều chỉnh giảm
- Kiểm kê

InventoryTransfer dùng riêng cho nghiệp vụ chuyển kho hai đầu:
- Kho nguồn
- Kho đích
- Danh sách nguyên liệu chuyển
- Số lượng chuyển
- Xác nhận chuyển
- Trừ kho nguồn
- Cộng kho đích

Không dùng InventoryDocument để mô phỏng chuyển kho.
Không dùng cặp phiếu xuất nội bộ + nhập nội bộ để xử lý chuyển kho.
Hãy rà soát toàn bộ dự án và xóa hoặc ẩn các phần liên quan đến nghiệp vụ:
- IMPORT_INTERNAL
- INTERNAL_OUT
- INTERNAL_IMPORT
- Nhập nội bộ
- Xuất nội bộ

Áp dụng ở các nơi sau:
1. Enum
2. View
3. Dropdown chọn loại phiếu
4. Controller
5. Service
6. ViewModel / DTO
7. Validation
8. Seed data nếu có
9. Mapping hiển thị tiếng Việt
10. Các switch-case xử lý nghiệp vụ tồn kho

Lưu ý:
- Không xóa nếu enum đang được dữ liệu cũ tham chiếu và có nguy cơ lỗi migration.
- Nếu cần giữ enum để backward compatible thì đánh dấu obsolete hoặc không hiển thị trên UI.
- Không cho người dùng tạo mới phiếu nhập nội bộ hoặc xuất nội bộ nữa.
Ở màn hình tạo phiếu mới, hiện tại có thể đang có:
- Phiếu Nhập
- Phiếu Xuất

Hãy thêm box riêng:
- Phiếu Chuyển Kho

Box Phiếu Chuyển Kho không được nằm trong Phiếu Nhập hoặc Phiếu Xuất.

UI mong muốn:

[ Phiếu Nhập Kho ]
Dùng để nhập nguyên liệu từ nhà cung cấp hoặc điều chỉnh tăng.

[ Phiếu Xuất Kho ]
Dùng để xuất bán hàng, xuất hủy, xuất điều chỉnh giảm.

[ Phiếu Chuyển Kho ]
Dùng để điều chuyển nguyên liệu giữa các chi nhánh/kho trong cùng hệ thống.

Khi click Phiếu Chuyển Kho:
- Điều hướng đến màn hình tạo InventoryTransfer.
- Không dùng form InventoryDocument.
Quy tắc chung:
Không cho nhập số lượng nhỏ hơn hoặc bằng 0 đối với mọi dòng nguyên liệu.

Áp dụng cho:
1. Phiếu Nhập từ nhà cung cấp
2. Phiếu Xuất bán hàng
3. Phiếu Chuyển kho

Cụ thể:

A. Phiếu Nhập - Nhập từ nhà cung cấp
- Quantity phải > 0.
- BaseQuantity phải > 0.
- Không cho nhập số âm.
- Không cho nhập số 0.
- Nếu nhập sai, hiển thị lỗi tại dòng nguyên liệu.

B. Phiếu Xuất - Bán hàng
- Quantity phải > 0.
- BaseQuantity phải > 0.
- Không cho nhập số âm.
- Không cho nhập số 0.
- Nếu bật cấu hình cho phép kho âm, hệ thống có thể cho phép tồn kho sau xuất nhỏ hơn 0.
- Nhưng số lượng xuất vẫn luôn phải là số dương.

C. Phiếu Chuyển Kho
- Quantity phải > 0.
- BaseQuantity phải > 0.
- Không cho nhập số âm.
- Không cho nhập số 0.
- Nếu bật cấu hình cho phép kho âm, hệ thống có thể cho phép kho nguồn âm sau khi chuyển.
- Khi xác nhận:
  + Trừ kho nguồn.
  + Cộng kho đích.
  Khi tạo Phiếu Nhập và chọn mục đích là "Nhập từ nhà cung cấp":

Validation:
- Bắt buộc chọn SupplierId.
- Quantity từng dòng phải > 0.
- BaseQuantity từng dòng phải > 0.
- UnitPrice nếu có thì không được âm.
- TotalAmount nếu có thì không được âm.

Partner logic:
- PartnerType = SUPPLIER.
- PartnerId = SupplierId.
- PartnerName = tên nhà cung cấp tại thời điểm tạo phiếu.

Yêu cầu UI:
- Không cần hiển thị PartnerName trên view.
- Nhưng trong database vẫn phải lưu PartnerName để làm snapshot/fallback.
- SupplierName được lấy từ bảng Supplier.
Khi tạo Phiếu Xuất và chọn mục đích là "Bán hàng":

Validation:
- Quantity từng dòng phải > 0.
- BaseQuantity từng dòng phải > 0.
- Không cho nhập số âm.
- Không cho nhập số 0.

Stock logic:
- Nếu hệ thống không cho phép kho âm:
  + Không cho xác nhận nếu tồn kho không đủ.
- Nếu hệ thống cho phép kho âm có kiểm soát:
  + Cho phép xác nhận nếu tồn sau xuất nằm trong ngưỡng âm cho phép.
  + Nếu vượt ngưỡng âm, cần quản lý xác nhận hoặc chặn theo cấu hình.
  Phiếu Chuyển Kho là nghiệp vụ riêng, không nằm trong Phiếu Nhập hoặc Phiếu Xuất.

Model sử dụng:
- InventoryTransfer
- InventoryTransferDetail

Không dùng:
- InventoryDocument Type IMPORT
- InventoryDocument Type EXPORT
- IMPORT_INTERNAL
- INTERNAL_OUT
- INTERNAL_IMPORT

Luồng xử lý:
1. Người dùng mở màn hình Tạo Phiếu Chuyển Kho.
2. Chọn kho nguồn.
3. Chọn kho đích.
4. Thêm nguyên liệu.
5. Nhập số lượng chuyển.
6. Hệ thống validate số lượng > 0.
7. Hệ thống kiểm tra tồn kho nguồn.
8. Khi nhấn Xác nhận:
   - Nếu không cho phép kho âm: kho nguồn phải đủ.
   - Nếu cho phép kho âm: kho nguồn có thể âm trong ngưỡng cho phép.
   - Trừ tồn kho nguồn.
   - Cộng tồn kho đích.
   - Ghi transaction xuất ở kho nguồn.
   - Ghi transaction nhập ở kho đích.
   - Cập nhật phiếu thành Completed.

Không làm trong giai đoạn này:
- Shipping
- Đang vận chuyển
- Chờ nhận hàng
- Nhận một phần
- Kho đích xác nhận nhận hàng riêng
Hãy chỉnh lại configuration để hệ thống có thể bật/tắt kho âm.

Cấu hình đề xuất:
- AllowNegativeStock: bool
- RequireManagerApprovalForNegativeStock: bool
- DefaultMaxNegativeQuantity: decimal?
- MaxNegativeQuantity theo từng nguyên liệu nếu có
- MaxNegativeQuantity theo từng kho nếu có

Nguyên tắc:
- Nếu AllowNegativeStock = false:
  + Không cho tồn kho sau giao dịch nhỏ hơn 0.
- Nếu AllowNegativeStock = true:
  + Cho phép tồn kho sau giao dịch nhỏ hơn 0.
  + Nhưng phải kiểm tra ngưỡng âm cho phép.
- Nếu vượt ngưỡng âm:
  + Nếu RequireManagerApprovalForNegativeStock = true thì yêu cầu quản lý duyệt.
  + Nếu không có cơ chế duyệt thì chặn xác nhận.
  Tên nghiệp vụ:
Âm kho có kiểm soát để đảm bảo trải nghiệm khách hàng.

Mục đích:
Trong thực tế vận hành quán cà phê, có trường hợp đơn hàng đã được tạo hoặc khách đã xác nhận mua, nhưng hệ thống phát hiện nguyên liệu không đủ.

Thay vì bắt buộc hủy món ngay, hệ thống cho phép tiếp tục phục vụ trong phạm vi kiểm soát.

Áp dụng cho:
- Xuất bán hàng.
- Chuyển kho nếu kho nguồn thiếu nhưng được phép âm.
- Các giao dịch xuất kho khác nếu được cấu hình.

Không áp dụng cho:
- Nhập kho từ nhà cung cấp.
- Điều chỉnh tăng.
- Các nghiệp vụ làm tăng tồn kho.

Khi phát sinh âm kho, hệ thống cần ghi nhận:
- Kho phát sinh âm.
- Nguyên liệu bị âm.
- Tồn trước giao dịch.
- Số lượng xuất/chuyển.
- Tồn sau giao dịch.
- Nhân viên thao tác.
- Thời gian thao tác.
- Lý do cho phép âm kho.
- Có cần quản lý duyệt hay không.
- Trạng thái xử lý âm kho.
Bổ sung trạng thái xử lý tồn kho cho transaction hoặc log xuất kho.

Các trạng thái đề xuất:
- NORMAL: giao dịch bình thường, tồn sau giao dịch >= 0.
- LOW_STOCK: tồn sau giao dịch thấp hơn ngưỡng tối thiểu nhưng chưa âm.
- NEGATIVE_PENDING: tồn sau giao dịch âm và đang chờ quản lý xác nhận.
- NEGATIVE_CONFIRMED: tồn âm đã được xác nhận.
- ADJUSTED: tồn âm đã được xử lý bằng nhập bù hoặc điều chỉnh.

Có thể tạo bảng để lưu trữ các phiếu khi xử lí tồn âm

Khi phát sinh kho âm, hệ thống cần lưu lý do.

Các lý do đề xuất:
- ORDER_ALREADY_CREATED: đơn đã được tạo cho khách.
- CUSTOMER_ALREADY_PAID: khách đã thanh toán.
- PHYSICAL_STOCK_AVAILABLE: thực tế còn nguyên liệu nhưng hệ thống chưa cập nhật.
- STOCK_COUNT_DELAY: kiểm kê hoặc cập nhật tồn kho bị chậm.
- MANAGER_APPROVED: quản lý cho phép xử lý.
- CUSTOMER_EXPERIENCE_PRIORITY: ưu tiên trải nghiệm khách hàng.
- OTHER: lý do khác.

Nếu reason = OTHER thì bắt buộc nhập ghi chú.

Không phải vai trò nào cũng được xác nhận âm kho.

Quy tắc đề xuất:
- Admin: cấu hình quyền và ngưỡng âm kho.
- Chủ doanh nghiệp: xem toàn bộ báo cáo âm kho.
- Quản lý vùng: xem âm kho các chi nhánh phụ trách.
- Quản lý chi nhánh: được duyệt âm kho tại chi nhánh.
- Nhân viên bán hàng: được tạo yêu cầu âm kho, không tự duyệt nếu vượt ngưỡng.
- Kế toán/Kho: xử lý nhập bù, điều chỉnh tồn kho.

Trong phạm vi đồ án:
- Có thể đơn giản hóa bằng cách chỉ kiểm tra quyền Manager/Admin khi cần duyệt âm kho.

Luồng chính:
1. Khách order món.
2. Nhân viên tạo đơn.
3. Hệ thống tính định mức nguyên liệu.
4. Hệ thống phát hiện tồn kho không đủ.
5. Hệ thống hiển thị cảnh báo:
   - Nguyên liệu thiếu.
   - Tồn hiện tại.
   - Số lượng cần dùng.
   - Tồn sau khi xuất.
6. Nếu AllowNegativeStock = false:
   - Không cho xác nhận xuất kho.
7. Nếu AllowNegativeStock = true:
   - Kiểm tra tồn sau xuất có nằm trong ngưỡng âm cho phép không.
8. Nếu nằm trong ngưỡng:
   - Cho phép xác nhận.
   - Ghi nhận giao dịch xuất kho âm.
   - Tạo cảnh báo cho quản lý.
9. Nếu vượt ngưỡng:
   - Yêu cầu quản lý duyệt hoặc chặn theo cấu hình.
10. Sau khi xác nhận:
   - Tồn kho có thể âm.
   - Giao dịch phải được đánh dấu là âm kho có kiểm soát.

Luồng chính:
1. Người dùng tạo Phiếu Chuyển Kho.
2. Chọn kho nguồn và kho đích.
3. Nhập nguyên liệu và số lượng chuyển.
4. Hệ thống kiểm tra tồn kho nguồn.
5. Nếu kho nguồn đủ:
   - Cho phép xác nhận bình thường.
6. Nếu kho nguồn không đủ:
   - Nếu AllowNegativeStock = false: không cho xác nhận.
   - Nếu AllowNegativeStock = true: kiểm tra ngưỡng âm cho phép.
7. Nếu tồn sau chuyển nằm trong ngưỡng âm:
   - Cho phép xác nhận.
   - Trừ kho nguồn xuống âm.
   - Cộng kho đích.
   - Ghi nhận transaction âm kho tại kho nguồn.
8. Nếu vượt ngưỡng:
   - Yêu cầu quản lý duyệt hoặc chặn theo cấu hình.

Tất cả action và loại phiếu ghi dữ liệu quan trọng phải dùng RequestDeduplication để chống double click.

Áp dụng cho:
- InventoryDocument.CreateImport
- InventoryDocument.CreateExport
- InventoryDocument.ConfirmImport
- InventoryDocument.ConfirmExport
- InventoryTransfer.CreateDraft
- InventoryTransfer.UpdateDraft
- InventoryTransfer.Confirm
- InventoryTransfer.Cancel

Model hiện có:

namespace CafeChain.Models.Systems
{
    public class RequestDeduplication
    {
        public int RequestDeduplicationId { get; set; }
        public string RequestKey { get; set; } = null!;
        public string ActionName { get; set; } = null!;
        public int StaffId { get; set; }
        public int? ReferenceId { get; set; }
        public string Status { get; set; } = null!;
        public string? RequestBody { get; set; }
        public string? ResponseBody { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiredAt { get; set; }
    }
}

Database phải có unique index:
(RequestKey, ActionName, StaffId)

Status:
- PROCESSING
- SUCCESS
- FAILED
- EXPIRED

Nguyên tắc:
- Nếu request mới: insert PROCESSING rồi xử lý.
- Nếu duplicate và status SUCCESS: trả lại kết quả cũ, không tạo phiếu mới.
- Nếu duplicate và status PROCESSING: báo "Yêu cầu đang được xử lý".
- Nếu duplicate và status FAILED: yêu cầu frontend tạo RequestKey mới nếu muốn gửi lại.

Tách service theo nghiệp vụ:

InventoryDocumentService
InventoryTransferService:
- CreateDraftAsync
- UpdateDraftAsync
- ConfirmAsync
- CancelAsync
- ValidateStockAsync

NegativeInventoryService:
- CanAllowNegativeStockAsync
- ValidateNegativeThresholdAsync
- CreateNegativeStockWarningAsync
- RequireManagerApprovalAsync

Tất cả method xác nhận phiếu phải chạy trong database transaction.
Nếu lỗi ở bất kỳ bước nào thì rollback toàn bộ.

Controller cần chỉnh lại:

1. InventoryDocumentController
- Không hiển thị lựa chọn nhập nội bộ.
- Không hiển thị lựa chọn xuất nội bộ.
- Validate nhập từ nhà cung cấp.
- Validate xuất bán hàng.
- Nhận RequestKey từ frontend.
- Gọi RequestDeduplication trước khi ghi dữ liệu.

2. InventoryTransferController
- Có action riêng cho tạo phiếu chuyển kho.
- Có view riêng cho phiếu chuyển kho.
- Có action confirm riêng.
- Không gọi InventoryDocument để tạo phiếu nhập/xuất nội bộ.
- Nhận RequestKey từ frontend.
- Khi confirm thì trừ kho nguồn và cộng kho đích trong cùng transaction.

Cần rà soát và sửa view:

1. Màn hình tạo phiếu mới:
- Thêm box "Phiếu Chuyển Kho".
- Không để chuyển kho trong Phiếu Nhập hoặc Phiếu Xuất.

2. Form Phiếu Nhập:
- Xóa option nhập nội bộ.
- Khi chọn nhập từ nhà cung cấp:
  + Hiển thị dropdown nhà cung cấp.
  + Không hiển thị PartnerName.
  + Validate số lượng > 0.

3. Form Phiếu Xuất:
- Xóa option xuất nội bộ.
- Khi chọn bán hàng:
  + Validate số lượng > 0.
  + Nếu thiếu tồn, hiển thị cảnh báo theo cấu hình âm kho.

4. Form Phiếu Chuyển Kho:
- Chọn kho nguồn.
- Chọn kho đích.
- Không cho chọn cùng một kho.
- Nhập danh sách nguyên liệu.
- Validate số lượng > 0.
- Hiển thị tồn kho nguồn realtime.
- Nếu bật kho âm, hiển thị cảnh báo khi tồn sau chuyển < 0.
- Khi confirm, disable button và gửi RequestKey.

Cần chỉnh EF Core configuration:

1. Cho phép tồn kho âm:
- Không đặt check constraint CurrentQuantity >= 0.
- Nếu đang có check constraint chặn số âm thì phải xóa hoặc sửa.
- Các cột tồn kho vẫn dùng decimal(18,3) hoặc precision phù hợp.
- Không validate tồn kho âm ở database bằng check constraint.

2. Vẫn cần check số lượng giao dịch:
- Quantity > 0.
- BaseQuantity > 0.
- Không cho dòng chứng từ có số lượng âm hoặc bằng 0.

3. RequestDeduplication:
- RequestKey required.
- ActionName required.
- StaffId required.
- Status required.
- Unique index trên RequestKey + ActionName + StaffId.

4. InventoryTransfer:
- FromStoreId khác ToStoreId.
- Code unique.
- Status required.
- Quantity/BaseQuantity ở detail phải > 0.
- Mỗi IngredientId chỉ xuất hiện một lần trong cùng phiếu chuyển.

Mọi thao tác xác nhận phiếu phải atomic.

Khi confirm Phiếu Nhập:
- Tăng tồn kho.
- Ghi InventoryTransaction.
- Cập nhật trạng thái phiếu.

Khi confirm Phiếu Xuất:
- Kiểm tra cấu hình kho âm.
- Trừ tồn kho.
- Ghi InventoryTransaction.
- Nếu phát sinh âm kho thì ghi log/cảnh báo.
- Cập nhật trạng thái phiếu.

Khi confirm Phiếu Chuyển Kho:
- Kiểm tra kho nguồn và kho đích.
- Kiểm tra cấu hình kho âm cho kho nguồn.
- Trừ kho nguồn.
- Cộng kho đích.
- Ghi transaction OUT_TRANSFER tại kho nguồn.
- Ghi transaction IN_TRANSFER tại kho đích.
- Nếu kho nguồn âm thì ghi log/cảnh báo.
- Cập nhật trạng thái phiếu.

Nếu bất kỳ bước nào lỗi:
- Rollback toàn bộ.
- Không được để tồn kho bị lệch.

Sau khi hoàn thành, hệ thống phải đạt các tiêu chí sau:

1. Không còn option "Nhập nội bộ" trong form tạo Phiếu Nhập.
2. Không còn option "Xuất nội bộ" trong form tạo Phiếu Xuất.
3. Màn hình tạo phiếu mới có box riêng "Phiếu Chuyển Kho".
4. Phiếu Chuyển Kho dùng InventoryTransfer, không dùng InventoryDocument.
5. Nhập từ nhà cung cấp bắt buộc số lượng > 0.
6. Xuất bán hàng bắt buộc số lượng > 0.
7. Chuyển kho bắt buộc số lượng > 0.
8. Nếu bật kho âm, tồn kho được phép nhỏ hơn 0 theo ngưỡng cấu hình.
9. Nếu tắt kho âm, hệ thống không cho xác nhận khi không đủ tồn.
10. Khi nhập từ nhà cung cấp:
    - PartnerType = SUPPLIER.
    - PartnerId = SupplierId.
    - PartnerName = Supplier.Name.
    - PartnerName không cần hiển thị trên view.
11. RequestDeduplication chống được double click tạo 2 phiếu.
12. Confirm phiếu không bị trừ/cộng tồn kho 2 lần khi user click nhiều lần.
13. Tất cả thao tác confirm chạy trong database transaction.
14. Không có check constraint chặn tồn kho âm ở bảng tồn kho.
15. Vẫn có check constraint hoặc validation chặn Quantity <= 0 ở chi tiết phiếu.

- Những điều không được làm:
Không được:
- Dùng Phiếu Nhập để làm chuyển kho.
- Dùng Phiếu Xuất để làm chuyển kho.
- Tạo cặp phiếu xuất nội bộ + nhập nội bộ cho chuyển kho.
- Cho nhập Quantity âm hoặc bằng 0.
- Cho AI tự động quyết định duyệt âm kho.
- Chỉ dựa vào frontend disable button để chống double click.
- Cập nhật tồn kho ngoài database transaction.
- Xóa dữ liệu cũ nếu chưa có migration an toàn.

Nếu trong code hiện tại đang có logic:
- Quantity < 0 để biểu diễn xuất kho
thì cần refactor lại.

Quy tắc mới:
- Quantity luôn là số dương.
- Loại transaction quyết định cộng hay trừ kho.

Ví dụ:
- IMPORT_PURCHASE: cộng kho.
- SALE: trừ kho.
- TRANSFER_OUT: trừ kho nguồn.
- TRANSFER_IN: cộng kho đích.

Không dùng số âm trong Quantity để biểu diễn chiều giao dịch.


