# Thuật ngữ CafeChain

| Tên tiếng Việt | Tên code/Anh | Giải thích | Ví dụ CafeChain |
|---|---|---|---|
| Vai trò | Role | Nhóm trách nhiệm của người dùng | `AccountantWarehouse` = Kế toán/kho |
| Quyền | Permission | Khả năng thực hiện một action cụ thể | `PurchaseOrder.Approve` |
| Phạm vi | Scope | Tập store/region actor được truy cập | Store Manager chỉ thao tác store được cấp |
| Cửa hàng | Store | Đơn vị vận hành bán hàng và tồn kho | CafeChain Thủ Dầu Một |
| Vùng | Region/Area | Nhóm cửa hàng do Quản lý vùng theo dõi | `AreaManager` có nhiều StoreScope |
| Ca POS | WorkShift | Phiên trách nhiệm của người, terminal và két | Mở ca, bán hàng, đóng/đối soát |
| Ca vận hành đá | OperationalShift | Ca riêng để quản lý phân bổ/tiêu hao đá | Có thể link nhiều WorkShift POS |
| Đơn hàng | Order | Giao dịch bán với total/status/store | Đơn tiền mặt tại POS |
| Dòng đơn hàng | OrderDetail | Món/size/giá/BOM snapshot đã bán | 2 Matcha latte size M |
| Thanh toán | Payment | Dòng tiền theo method/status | Tiền mặt hoặc VietQR |
| Idempotency | Idempotency | Retry cùng thao tác không tạo kết quả nghiệp vụ thứ hai | `ClientOrderId` trả lại Order cũ |
| Đồng thời | Concurrency | Nhiều actor xử lý cùng resource | Hai người cùng duyệt PO |
| Phiên bản hàng | RowVersion | Token optimistic concurrency | PO stale bị yêu cầu tải lại |
| Maker-checker | Maker-checker | Người tạo và người duyệt phải tách biệt | Kế toán tạo PO, Owner duyệt |
| Ảnh chụp dữ liệu | Snapshot | Giá trị lịch sử đóng băng tại thời điểm giao dịch | PO line giữ giá/UOM; Order line giữ recipe |
| Yêu cầu nhập hàng | Restock Request | Ý định bổ sung hàng của cửa hàng | Store cần thêm 10 kg cacao |
| Phân bổ nguồn | Purchase/Sourcing Allocation | Phần nhu cầu được gán mua/chuyển/sản xuất/từ chối | 8 kg PURCHASE, 2 kg TRANSFER |
| Đề nghị mua | Purchase Advice (PA) | Chứng từ xem xét nhu cầu mua trước khi đặt hàng | PA bao phủ 8 kg cacao |
| Đơn đặt hàng | Purchase Order (PO) | Cam kết mua với supplier cho một store/nguồn | PO thường từ một PA |
| Đơn đặt hàng gộp | Purchase Order Batch (POB) | Aggregate nhiều nguồn tương thích, có PO con | Hai store cùng mua một NCC |
| Phiếu nhận hàng | Branch Receipt | Chứng từ kiểm đếm tại store | DRAFT rồi CONFIRMED |
| Nhà cung cấp | Supplier | Đối tác cung ứng nguyên liệu | NCC sữa/bao bì |
| Gói mua | Supplier Package/IngredientSupplier | Quy cách vật lý và giá mua | 1 gói = 200 ml, 168.000 đ |
| Mua lẻ | Loose Purchase | Mua theo UOM vật lý thay vì số gói | 1,5 L với giá đ/L |
| Đơn vị tồn cơ sở | Base UOM | Đơn vị chuẩn lưu tồn | g, ml, cái |
| Đơn vị nhu cầu | Demand UOM | Đơn vị người dùng nhập ở Restock | kg/L/cái compatible |
| Đơn vị nội dung gói | Content UOM | Đơn vị mô tả lượng trong một package | 200 ml/gói |
| Quy đổi đơn vị | Unit Conversion | Chuyển quantity giữa UOM compatible | 1 L = 1.000 ml |
| Số lượng đặt tối thiểu | MOQ | Ngưỡng package/loose của offer | Tối thiểu 2 gói |
| Định mức nguyên liệu | BOM/Recipe | Thành phần và số lượng để làm món/BTP | 20 g cà phê cho size M |
| Bán thành phẩm | Prepared Item (BTP) | Identity tồn kho ổn định được sản xuất từ recipe | Cốt cà phê, syrup đường |
| Nhập trước xuất trước | FIFO | Dùng lớp giá cũ trước để tính xuất/COGS | Layer nhập ngày trước được consume trước |
| Giá vốn hàng bán | COGS | Chi phí thực/ước tính của món đã bán | Order `TotalCogs` khi complete |
| Lợi nhuận gộp | Gross Profit | Giá bán trừ giá vốn | 30.000 - 9.000 = 21.000 đ |
| Biên lợi nhuận | Margin | Lợi nhuận / giá bán | 21.000 / 30.000 = 70% |
| Tỷ lệ cộng giá | Markup | Lợi nhuận / giá vốn | 21.000 / 9.000 = 233,33% |
| Chính sách topping | Topping Policy | Quy định default, giá bán, cost và quantity theo size | Included/Add price; Included/Add cost |
| Tồn khả dụng | Available Quantity | Lượng có thể sử dụng theo inventory model | `AvailableQty` |
| Đang giữ chỗ | Reserved Quantity | Lượng đã reserve cho workflow | `ReservedQty` |
| Bút toán tồn | Inventory Transaction | Ledger before/after và nguồn phát sinh | SALES_DEDUCTION, receipt-in |
| Lớp giá | Inventory Cost Layer | Lượng tồn kèm unit cost cho FIFO | Lô 100 g giá 50 đ/g |
| Nhật ký | Audit | Ai thay gì, khi nào, lý do | Price audit, PA transition |
| Blind selling | Blind Selling | POS tiếp tục bán offline dù không xác minh tồn online | Có thể tạo negative inventory |
| Giao dịch nguyên tử | Transaction | Các ghi thay đổi cùng thành công hoặc rollback | Receipt + ledger + fulfillment |
| Chuyển giao | Handoff | Role/ca giao responsibility/dữ liệu cho role/ca khác | Store gửi Restock cho Accountant |
| Tiêu hao lý thuyết | Theoretical Usage | Lượng dự kiến từ POS/BOM | Operational Ice lấy từ linked WorkShift |
| Chênh lệch | Variance | Thực tế trừ lý thuyết | Đá thiếu cần phê duyệt/posting |

## Các cặp không được dùng lẫn

- Restock ≠ PA ≠ PO ≠ Receipt.
- WorkShift ≠ OperationalShift.
- Giá bán ≠ giá vốn ≠ giá gói ≠ giá mua lẻ.
- Base UOM ≠ demand UOM ≠ package content UOM.
- Margin ≠ Markup.
- Default topping ≠ topping miễn phí ≠ topping đã nằm trong BOM.
- Permission ≠ role; role chỉ là một nguồn cấp permission.
