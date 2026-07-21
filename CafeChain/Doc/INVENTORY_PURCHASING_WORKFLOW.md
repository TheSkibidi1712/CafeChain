# PHÂN TÍCH APPLAUNCHER, MUA HÀNG VÀ QUẢN LÝ KHO

## Phạm vi và nguyên tắc kết luận

- Tài liệu được dựng từ code hiện tại, migration, test, JavaScript, view và các ADR trong `docs/adr`.
- Phần **hiện trạng** chỉ mô tả hành vi có bằng chứng trong code. Phần **đề xuất** không được xem là chức năng đã có.
- Đợt thay đổi này chỉ refactor AppLauncher. Không thay đổi model, enum, database hoặc luồng nghiệp vụ kho.
- Trong code hiện tại, PA là `PurchaseAdvice`, không phải một model `PurchaseApproval` riêng.
- Không tìm thấy model `Warehouse`; `Store` và `StoreInventory` đang đóng vai trò địa điểm/kho tồn.
- Không tìm thấy phân hệ Supplier Debt/Accounts Payable, lot number hoặc expiry date đủ để kết luận đã có quản lý công nợ/lô hàng.

## Báo cáo refactor AppLauncher và PrintBridge

### Luồng cũ

Luồng bắt đầu tại `AppLauncherController.LaunchPos`, gọi `IPosLaunchCoordinator.EnsureReadyAsync(storeId)`. Trong `PosLaunchCoordinator` cũ:

1. So sánh cửa hàng đăng nhập với `PosLauncherOptions.PrintBridgeStoreId`.
2. Kiểm tra heartbeat bằng `IPrintBridgePresenceTracker.IsOnline`.
3. Nếu offline, resolve `PrintBridgeProject` rồi gọi `StartBridge`.
4. `StartBridge` tạo process `dotnet run --project <CafeChain.PrintBridge.csproj>`.
5. Launcher chờ SignalR heartbeat đến hết `StartupTimeoutSeconds`.
6. Chỉ sau khi bridge online mới kiểm tra và khởi động CafeChain.Frontend bằng `npm run dev`.
7. `GetStatusAsync` chỉ trả `Ready` khi cả bridge và frontend sẵn sàng.

Do đó các lỗi project bridge không tồn tại, thiếu `dotnet`, heartbeat timeout hoặc store không khớp đều chặn POS. Không có background supervisor tự khởi động lại bridge. `Dispose` cũ chỉ dispose process handle, không gọi `Kill`, nên cũng không có nghiệp vụ đóng bridge thật sự khi POS tắt.

### Luồng mới

- `EnsureReadyAsync` chỉ kiểm tra health endpoint, port, project frontend và khởi động frontend khi cần.
- `GetStatusAsync` chỉ dùng health check của frontend để xác định `Ready`.
- Không còn process, heartbeat, timeout, store binding hoặc error code PrintBridge trong launcher.
- Các route và chữ ký `EnsureReadyAsync(int storeId)`/`GetStatusAsync(int storeId)` được giữ để tương thích controller/client.
- Giá trị enum phía client được giữ ổn định: `CheckingFrontend=3`, `StartingFrontend=4`, `Ready=5`, `Failed=6`.
- Project `CafeChain.PrintBridge`, `PrintBridgeHub`, `PrintBridgePresenceTracker` và `PrintBridge:ApiKey` vẫn tồn tại cho in thử và theo dõi máy in.
- Chạy thủ công khi cần kiểm thử:

```powershell
dotnet run --project CafeChain.PrintBridge/CafeChain.PrintBridge.csproj
```

### Ảnh hưởng

- POS không còn thất bại chỉ vì PrintBridge tắt, crash, cấu hình sai hoặc không tồn tại.
- AppLauncher vẫn tự khởi động frontend như trước.
- Việc in qua bridge không bị xóa nhưng người kiểm thử phải chạy bridge chủ động.
- Không thay đổi quyền mở POS, token đăng nhập POS hoặc Hub in.

# 1. Hiện trạng codebase

## 1.1 Module và bảng dữ liệu chính

| Nhóm | Model/bảng chính | Vai trò hiện tại |
| --- | --- | --- |
| Cảnh báo và nhu cầu | `StockAlert`, `RestockRequest`, `RestockRequestTransition` | Phát hiện thiếu hàng và tạo nhu cầu bổ sung theo cửa hàng. |
| PA | `PurchaseAdvice`, `PurchaseAdviceLine`, `PurchaseAdviceTransition` | Đề nghị mua của chi nhánh, lấy nguồn từ RestockRequest. |
| PO trực tiếp | `PurchaseOrder`, `PurchaseOrderLine`, `PurchaseOrderReceiptPosting` | Đơn mua theo nhà cung cấp/cửa hàng, có thể gắn trực tiếp RestockRequest. |
| Gom nhu cầu | `PurchaseOrderBatch`, `PurchaseOrderBatchLine`, `PurchaseOrderLineAllocation`, `PurchaseOrderBatchDocumentRevision` | Gom dòng PA trước khi tạo PO con; lưu mapping dòng PA sang PO. |
| Nhà cung cấp | `Supplier`, `SupplierStore`, `IngredientSupplier`, `IngredientSupplierPriceHistory` | Phạm vi cửa hàng, offer/package, giá và lịch sử giá. |
| Nhận hàng | `BranchReceipt`, `BranchReceiptLine`, `SupplierReceiptIssue` | Nhận thực tế từ PO/chuyển kho, ghi nhận phần chấp nhận và từ chối. |
| Fulfillment | `RestockRequestFulfillment`, `RestockFulfillmentPosting` | Một model thể hiện kế hoạch/link; model posting thể hiện phần đã xác nhận. |
| Chứng từ chung | `InventoryDocument`, `InventoryDocumentDetail`, `InventoryDocumentSnapshot`, `InventoryDocumentSnapshotDetail` | Backend chung cho nhập/xuất/hủy/kiểm kê/điều chỉnh và một số luồng tự động. |
| Sổ tồn | `StoreInventory`, `InventoryTransaction` | Số dư tồn hiện tại và lịch sử biến động. |
| Giá vốn | `InventoryCostLayer`, `InventoryCostAllocation`, `InventoryNegativeCostGap`, `InventoryCostGapSettlement` | FIFO, phân bổ chi phí và bù giá vốn khi tồn âm. |
| Chuyển kho | `InventoryTransfer`, `InventoryTransferDetail`, `InventoryTransferCostAllocation` | Nghiệp vụ độc lập, xuất nguồn rồi nhận tại đích. |
| Kiểm kê | `StockTakeSession`, `StockTakeDetail` | Có schema nhưng chưa có workflow service/controller sử dụng. |
| POS | `Order`, `OrderDetail`, `OrderTopping`, `OrderRefund` | Đơn bán, món/topping và hoàn tiền. |

## 1.2 Enum và status thực tế

- PA: `DRAFT`, `SUBMITTED`, `UNDER_REVIEW`, `REJECTED`, `CANCELLED`, `ALLOCATED`.
- RestockRequest: `DRAFT`, `SUBMITTED`, `PROCESSING`, `PARTIALLY_RECEIVED`, `COMPLETED`, `REJECTED`, `CANCELLED`.
- PO: `DRAFT`, `APPROVED`, `MARKED_AS_SENT`, `PARTIALLY_RECEIVED`, `COMPLETED`, `CANCELLED`.
- PO Batch: `DRAFT`, `PENDING_APPROVAL`, `APPROVED`, `PDF_GENERATED`, `SENT_TO_SUPPLIER`, `PARTIALLY_RECEIVED`, `COMPLETED`, `CANCELLED`.
- BranchReceipt: `DRAFT`, `CONFIRMED`.
- InventoryDocument: `DRAFT`, `PENDING`, `CONFIRMED`, `CANCELLED`.
- InventoryTransfer: `DRAFT`, `DISPATCHED`, `COMPLETED`, `CANCELLED`.
- Loại InventoryDocument: `IMPORT`, `EXPORT`, `WASTE`, `STOCK_TAKE`, `PRODUCTION_IN`, `PRODUCTION_OUT`, `SALES_DEDUCTION`, `ADJUSTMENT_IN`; `INTERNAL_IMPORT` đã obsolete.
- Purpose gồm mua hàng, điều chỉnh, bán, quà/tặng/mẫu, kiểm kê và các lý do hủy như damaged/expired/broken/contaminated/lost. Các purpose internal transfer đã obsolete.

## 1.3 Chức năng đã hoàn thành ở mức code

- PA có RequestKey, mã chứng từ, row version, transition history và filtered unique constraint cho reservation đang hoạt động trên RestockRequest.
- Preview consolidation kiểm tra offer nhà cung cấp, package, quy đổi base unit, MOQ, phạm vi cửa hàng và lead time.
- PO Batch có RequestKey và tạo atomically batch, batch lines, child PO, child PO lines và allocations.
- PO/BranchReceipt hỗ trợ nhận nhiều lần, số lượng thực nhận, từ chối hàng và cập nhật trạng thái partial/completed.
- BranchReceipt confirm có transaction, posting chống lặp, InventoryTransaction và CostLayer.
- Transfer đã tách khỏi InventoryDocument: dispatch trừ kho nguồn; receive cộng kho đích và giữ cost allocation.
- POS mới có InventoryDeduction, FIFO/cost gap, topping và tồn âm theo ADR-0001/0004.
- Refund đã xác minh transaction SALES_DEDUCTION trước khi tạo SALES_RETURN.

## 1.4 Chức năng làm dở hoặc chưa có

- PA không có trạng thái `APPROVED`; `UNDER_REVIEW` đang vừa mang nghĩa bắt đầu duyệt vừa là đầu vào consolidation.
- Không có merge các PO đã tồn tại, merge history hoặc trạng thái source PO bị superseded.
- PO trực tiếp không có mapping dòng PA và không có business RequestKey tương đương batch.
- Không có Supplier Debt/AP posting.
- Không có lot/batch/expiry trên nhận hàng.
- `StockTakeSession`/`StockTakeDetail` chưa có service, controller, UI hoặc posting authority.
- Chưa có quy trình hàng hỏng/thiếu trong vận chuyển sau khi dispatch và chưa có reverse transfer sau xuất.
- Chưa có item-level POS cancellation/waste workflow đầy đủ.
- Không có snapshot BOM tại thời điểm bán.

## 1.5 Xung đột/đường triển khai song song

1. **PO trực tiếp và PO từ PA Batch**: route trực tiếp gắn RestockRequest nhưng mất nguồn PA; route Batch giữ traceability tốt hơn.
2. **BranchReceipt và InventoryDocument IMPORT**: cả hai đều có khả năng tăng tồn. Nếu cùng biểu diễn một lần nhận nhà cung cấp sẽ có nguy cơ nhập hai lần.
3. **RestockRequestFulfillment và RestockFulfillmentPosting**: fulfillment biểu diễn kế hoạch/link, posting mới là bằng chứng xác nhận; tên và FK hiện dễ khiến developer dùng nhầm authority.
4. **StockTakeSession và InventoryDocument STOCK_TAKE**: schema session tồn tại nhưng màn hình thực tế xác nhận điều chỉnh qua InventoryDocument.
5. **POS mới và InventoryService cũ**: POS mới deduct sau thanh toán; luồng cũ reserve/release/confirm. Dùng release cũ cho đơn POS chưa reserve có thể làm tăng tồn giả.
6. **Kiến trúc truy cập dữ liệu**: InventoryDocument/Transfer có repository rõ hơn, nhưng nhiều service procurement/POS còn inject `AppDbContext` trực tiếp, chưa đạt quy tắc Service → Repository.

# 2. Luồng nghiệp vụ hiện tại

## 2.1 Từ cảnh báo tồn đến PA

1. Stock alert hoặc người dùng tạo `RestockRequest` cho một cửa hàng và một inventory identity.
2. RestockRequest chuyển đến `PROCESSING`/`PARTIALLY_RECEIVED` mới đủ điều kiện tạo PA.
3. StoreManager trong phạm vi cửa hàng tạo `PurchaseAdvice` bằng `PurchaseAdviceService.CreateAsync`.
4. PA lưu StoreId, RequesterStaffId, NeededByDate, Priority, RequestKey, timestamps và row version.
5. Dòng PA lưu RestockRequestId, IngredientId, UnitId, RequestedBaseQty, AllocatedBaseQty, AcceptedBaseQty và trạng thái đóng dòng.
6. Chỉ ingredient được hỗ trợ chắc chắn trong luồng mua; mua prepared item/BTP bị chặn hoặc chưa hoàn thiện.
7. PA `DRAFT` được sửa, sau đó `SubmitAsync` chuyển sang `SUBMITTED`.
8. AccountantWarehouse/BusinessOwner gọi `StartReviewAsync`, chuyển sang `UNDER_REVIEW` và ghi ReviewedAt/ReviewedBy.
9. `RejectAsync` chỉ áp dụng từ `UNDER_REVIEW`; `CancelAsync` áp dụng cho `DRAFT`/`SUBMITTED`.

### Lỗi cần ưu tiên

`PurchaseAdviceConsolidationService` đang truy vấn cả `SUBMITTED` và `UNDER_REVIEW`. Vì vậy một PA vừa submit, chưa qua `StartReviewAsync`, vẫn có thể được đưa vào preview/tạo Batch. Đây là khoảng thiếu phê duyệt thực sự.

## 2.2 Hai đường tạo PO

### Đường A — tạo PO trực tiếp

- `PurchaseOrderService.CreateDraftAsync` do AccountantWarehouse/BusinessOwner thực hiện.
- Chọn supplier/offer/package; kiểm tra supplier hoạt động và phục vụ cửa hàng, giá hiện hành, package conversion, MOQ và số lượng còn lại của RestockRequest.
- Một RestockRequest có thể được chia sang nhiều PO nếu tổng allocation không vượt remaining.
- Dòng PO có RestockRequestId nhưng không có PurchaseAdviceLineId.
- PO không có business RequestKey; mã PO ngẫu nhiên/unique không thay thế idempotency nguồn nghiệp vụ.

### Đường B — consolidation PA thành Batch và PO con

- Người dùng chọn dòng PA, preview đề xuất offer/package và nhóm dữ liệu.
- `PurchaseOrderBatchService.CreateAsync` tạo một Batch theo supplier/currency/delivery window.
- Hệ thống tạo child PO theo Store, vì Store hiện là điểm nhận hàng.
- `PurchaseOrderLineAllocation` giữ quan hệ PA line → batch line → child PO line và số lượng phân bổ.
- Đây là route duy nhất hiện giữ được nguồn nhiều PA trong một đợt mua.
- Batch có RequestKey và transaction/concurrency protection tốt hơn route trực tiếp.

## 2.3 Gửi PO và nhận hàng

1. Batch: `PENDING_APPROVAL → APPROVED → PDF_GENERATED → SENT_TO_SUPPLIER`.
2. PO con: `DRAFT → APPROVED → MARKED_AS_SENT`.
3. `BranchReceiptService.CreateOrOpenPurchaseOrderDraftAsync` tạo/mở draft receipt đang hoạt động cho PO.
4. Người nhận nhập ActualReceivedQty, RejectedQty và lý do từ chối.
5. AcceptedQty = ActualReceivedQty - RejectedQty; chỉ accepted mới được posting vào tồn.
6. Confirm đối chiếu remaining PO, giá/package snapshot và row version.
7. Trong transaction, service tạo fulfillment posting, PO receipt posting, InventoryTransaction, cost layer, cập nhật StoreInventory, receipt, PO, batch, RestockRequest và stock alert.
8. Một PO có thể có nhiều receipt tuần tự; receipt mới chỉ mở sau khi receipt trước đã confirmed.
9. Unique posting và trạng thái immutable ngăn cùng một receipt line được xác nhận lại.

## 2.4 Chuyển kho

1. Tạo `InventoryTransfer` ở `DRAFT`.
2. Dispatch kiểm tra tồn, trừ kho nguồn, consume FIFO và ghi cost allocations; trạng thái thành `DISPATCHED`.
3. Hàng ở trạng thái in-transit, chưa cộng kho đích.
4. Receive nhập số lượng nhận, tạo BranchReceipt/transaction `IN_TRANSFER` và cộng kho đích theo giá vốn nguồn.
5. Nhận nhiều lần được hỗ trợ; nhận vượt bị chặn; đủ số lượng thì `COMPLETED`.
6. Chỉ transfer `DRAFT` được cancel. Chưa có workflow xử lý thiếu/hỏng hoặc reverse sau dispatch.

# 3. Luồng nghiệp vụ đề xuất

```text
RestockRequest
    → PA Draft
    → Submitted
    → Approved
    → Consolidation theo dòng
    → kiểm tra RequestKey/allocation
    → Batch + PO con theo điểm nhận
    → gửi nhà cung cấp
    → BranchReceipt theo hàng thực nhận
    → InventoryTransaction + CostLayer
    → cập nhật PO/Batch/RestockRequest
```

## 3.1 Quy tắc đề xuất

- Bổ sung trạng thái `APPROVED` cho PA ở giai đoạn sau; chỉ `APPROVED` mới được consolidation.
- Không coi `SUBMITTED` là đã duyệt và không dùng `UNDER_REVIEW` như trạng thái chấp thuận ngầm.
- Dùng `PurchaseOrderLineAllocation` làm authority truy vết nhu cầu; route PO trực tiếp phải được giới hạn hoặc đưa về cùng mapping.
- Khóa chống trùng phải dựa trên RequestKey và source line allocation trong transaction, không dựa vào “cùng số lượng trong vài phút”.
- Tách PO theo supplier, Store/điểm nhận, currency, delivery window và package offer. Payment terms chưa có model nên chưa thể là tiêu chí thực thi.
- Dùng Batch để gom nhu cầu **trước** khi tạo PO. Chưa merge PO hậu tạo cho đến khi có merge history, source status và migration được duyệt.
- Không merge PO đã gửi, có receipt hoặc đã khóa chứng từ.
- `BranchReceipt` là authority nhận PO. Không tạo thêm InventoryDocument IMPORT cho cùng receipt.
- Chỉ hàng accepted được nhập; rejected tạo SupplierReceiptIssue và không tăng tồn.
- Supplier debt chỉ được posting khi có phân hệ AP và idempotency riêng; hiện tại ghi “chưa hỗ trợ”.

## 3.2 Thuật toán kiểm tra trùng và gom

1. Bắt đầu transaction ở isolation phù hợp và khóa các PA lines/allocations được chọn.
2. Xác minh PA ở `APPROVED`, line còn quantity và không bị đóng/hủy.
3. Từ mỗi line chọn offer hợp lệ theo supplier active, SupplierStore, hiệu lực giá, base conversion, package và MOQ.
4. Tạo grouping key: SupplierId + StoreId + Currency + DeliveryWindow + IngredientSupplier/Package.
5. Kiểm tra RequestKey của lệnh tạo Batch; replay cùng payload trả lại Batch cũ, tái dùng key khác payload phải bị từ chối.
6. Tạo allocation theo source line và số lượng remaining, không cộng từ tổng PO đã có.
7. Unique constraint/concurrency phải bảo đảm tổng allocation không vượt remaining.
8. Chỉ sau khi allocations hợp lệ mới tạo batch/PO và commit toàn bộ.

### Phân biệt duplicate và mergeable

- **Duplicate**: cùng request key hoặc cố tạo lại allocation cho cùng source line vượt remaining. Đây là lỗi và phải chặn.
- **Mergeable**: các nhu cầu khác nguồn nhưng cùng grouping key, chưa phát sinh PO đã gửi/receipt. Đây là tối ưu gom mua, không phải lỗi dữ liệu.

# 4. So sánh hiện trạng và đề xuất

| Hạng mục | Code hiện tại | Nghiệp vụ mong muốn | Khoảng thiếu | Phương án xử lý |
| --- | --- | --- | --- | --- |
| Duyệt PA | Submitted → UnderReview | Có trạng thái duyệt rõ | Submitted vẫn consolidation được | Thêm Approved sau impact analysis; chỉ Approved được gom |
| PA–PO | Chỉ batch route có line allocation | Mọi PO truy được PA | PO trực tiếp mất nguồn PA | Hội tụ về allocation hoặc giới hạn PO trực tiếp |
| Tách PO | Child PO theo Store; batch theo supplier/currency/window | Tách theo điểm nhận và điều kiện thương mại | Chưa có Warehouse/payment term | Dùng Store và trường hiện có; chưa bịa field mới |
| Chống trùng | PA/Batch RequestKey; PO trực tiếp chưa có | Idempotency toàn chuỗi | Route trực tiếp yếu | Thêm RequestKey/mapping sau khi duyệt migration |
| Gộp PO | Gom trước tạo bằng Batch | Có thể gom nhu cầu | Không có merge PO/history | Tiếp tục pre-creation consolidation |
| Nhận PO | BranchReceipt thực nhận/partial | Nhập theo hàng thực tế | Có màn InventoryDocument IMPORT song song | Chọn BranchReceipt làm authority |
| Công nợ | Không có SupplierDebt/AP | Ghi nhận nợ một lần | Thiếu model và posting | Tách dự án AP riêng, chưa triển khai |
| Lô/hạn dùng | Không có dữ liệu đủ | Truy lô/hạn dùng | Thiếu schema/UI | Chưa thể kết luận; cần thiết kế riêng |
| Kiểm kê | InventoryDocument STOCK_TAKE; session model không dùng | Có cutoff/snapshot/approval | Hai hướng triển khai | Kích hoạt StockTakeSession hoặc bỏ schema sau đánh giá dữ liệu |
| Chuyển kho | Dispatch/receive độc lập | Đang vận chuyển và nhận thực tế | Thiếu discrepancy/reverse | Mở rộng workflow transfer, không nhập chung phiếu kho |
| POS cancel | Reserve/release cũ và deduct mới cùng tồn tại | Reverse theo bằng chứng posting | Có nguy cơ cộng tồn sai | Chỉ reverse transaction thực sự tồn tại |
| Kiến trúc | Nhiều service dùng DbContext | Service → Repository | Technical debt diện rộng | Refactor theo bounded workflow, không làm hàng loạt |

# 5. Phân tích từng loại phiếu

## 5.1 Phiếu nhập

| Trường hợp | Chứng từ nguồn/authority | Tồn kho | Giá vốn | Công nợ |
| --- | --- | --- | --- | --- |
| Nhập nhà cung cấp từ PO | BranchReceipt + PO line posting | Cộng accepted qty khi confirm | Tạo layer theo actual cost/package snapshot | Chưa hỗ trợ AP |
| Nhập điều chỉnh tăng | InventoryDocument ADJUSTMENT_IN/IMPORT_ADJUSTMENT | Cộng khi confirm | Phải nhập/giải trình giá hợp lệ | Không phát sinh |
| Hoàn bán POS | SALES_RETURN do refund service | Chỉ reverse phần có SALES_DEDUCTION hợp lệ | Reverse/reconcile theo allocation gốc | Không phát sinh |
| Nhập chuyển kho | InventoryTransfer receive + BranchReceipt | Cộng ở đích khi nhận | Mang cost allocation từ nguồn | Không phát sinh |
| Kiểm kê thừa | STOCK_TAKE/adjustment posting | Cộng delta sau duyệt | Theo policy giá điều chỉnh cần thống nhất | Không phát sinh |

Confirmed receipt/document phải immutable. Sửa sai bằng reverse document, không sửa trực tiếp transaction hoặc gán lại số dư.

## 5.2 Phiếu xuất

| Trường hợp | Cách tạo | Thời điểm trừ | Giá vốn/âm kho |
| --- | --- | --- | --- |
| Bán POS | Tự động qua InventoryDeductionService | Sau payment/commit theo POS mới | FIFO; thiếu layer tạo negative cost gap |
| Xuất hủy | Người dùng tạo WASTE | Khi confirm | FIFO và ghi purpose lý do |
| Điều chỉnh giảm | InventoryDocument EXPORT/ADJUSTMENT_OUT | Khi confirm | FIFO; cần quyền và lý do |
| Chuyển kho | InventoryTransfer dispatch | Khi dispatch | FIFO, giữ allocation in-transit |
| Kiểm kê thiếu | STOCK_TAKE/adjustment | Sau duyệt chênh lệch | FIFO/negative policy |
| Trả NCC | Chưa có workflow được chứng minh | Chưa kết luận | Cần source receipt và reversal AP trước khi làm |

## 5.3 Phiếu hủy

- `WASTE` hiện là **xuất hủy hàng hóa** với purpose damaged/expired/broken/contaminated/lost.
- `CANCELLED` là trạng thái hủy chứng từ trước khi posting, không đồng nghĩa xuất hủy.
- Delete không phải nghiệp vụ ưu tiên và không được dùng để hoàn tác chứng từ confirmed.
- Chưa có đầy đủ đề nghị/duyệt, ảnh bằng chứng và người duyệt cho waste. Nếu bổ sung, giữ WASTE trên backend InventoryDocument nhưng tách UI và permission riêng.
- Chứng từ đã confirmed phải tạo reverse; không “khôi phục” bằng sửa số dư.

## 5.4 Phiếu kiểm kê

Hiện tại model `StockTakeSession`/`StockTakeDetail` chưa được nối vào workflow. Đề xuất chuẩn hóa:

1. Tạo session theo Store và phạm vi inventory identity.
2. Ghi `CutoffAtUtc` và snapshot system quantity khi mở.
3. Không bắt buộc khóa POS; mọi transaction sau cutoff vẫn được ghi bình thường.
4. Số hệ thống để đối chiếu = snapshot tại cutoff cộng/trừ movement sau cutoff theo chính sách chốt.
5. Nhập counted quantity và lý do chênh lệch.
6. Người khác duyệt nếu vượt ngưỡng.
7. Tạo adjustment transactions tăng/giảm, liên kết session.
8. Session approved/posted là immutable.

Không ghi đè `StoreInventory` bằng counted quantity mà không có transaction điều chỉnh.

## 5.5 Phiếu chuyển kho

- Giữ `InventoryTransfer` là nghiệp vụ độc lập.
- Trừ kho nguồn tại dispatch, cộng kho đích tại receive.
- Nhận thiếu phải lưu outstanding hoặc discrepancy; không tự complete.
- Nhận thừa tiếp tục bị chặn, trừ khi có quy trình điều tra và adjustment riêng.
- Hỏng trong vận chuyển cần discrepancy/waste tại điểm sở hữu được quy định; hiện chưa có.
- Cancel trước dispatch được phép. Sau dispatch phải reverse/return workflow, không cancel trạng thái.
- Không cần tạo hai InventoryDocument con nếu InventoryTransaction và transfer cost allocation đã truy vết đầy đủ; BranchReceipt ở đích tiếp tục là bằng chứng nhận.

# 6. Phân tích form “Phiếu Kho”

## Kết luận: giữ backend chung nhưng tách giao diện

### Bằng chứng

- Model chung là `InventoryDocument`/`InventoryDocumentDetail`, phân biệt bằng `InventoryDocumentType`, `InventoryDocumentPurpose` và `InventoryDocumentStatus`.
- `AdminInventoryDocumentController` gọi service/repository và dùng chung các view `Areas/Admin/Views/AdminInventoryDocument`.
- `inventorydocumentcreate.js` xử lý nhiều nhánh import/export/waste/stock-take/adjustment, khiến validation và UI khó bảo trì.
- Internal transfer trong enum/purpose đã obsolete và controller cũ trả 410; chuyển kho đã có controller/service/model riêng.
- PO receipt đã có BranchReceipt riêng nên không cần tạo lại qua form chung.

### Vai trò sau chuẩn hóa

- Trang “Phiếu Kho” chính trở thành danh sách tra cứu/tổng hợp ledger, filter theo loại và nguồn.
- Giữ repository/backend chung cho nghiệp vụ adjustment/manual document phù hợp.
- Tách màn hình tạo: nhập điều chỉnh, xuất điều chỉnh, xuất hủy, kiểm kê.
- PO receipt và transfer receive đi qua màn hình nghiệp vụ riêng.
- POS chỉ gọi API/order services, không mở form quản trị Phiếu Kho.
- Chỉ BusinessOwner/AccountantWarehouse và vai trò cửa hàng đúng phạm vi được xem/tạo theo loại; quyền confirm phải tách khỏi quyền create.

Nếu xóa form ngay sẽ mất màn hình điều chỉnh, waste, stock take hiện tại và tra cứu chứng từ. Vì vậy không chọn “loại bỏ”.

# 7. Tích hợp với POS

## 7.1 Bán hàng

- `POSOrderService` commit order/payment trước; controller/webhook/offline sync gọi `InventoryDeductionService` sau đó.
- Deduction dùng BOM một cấp theo ADR-0004: trừ ingredient hoặc prepared item trực tiếp; COGS recursive là luồng khác.
- Có xử lý topping và quy đổi đơn vị.
- Negative Inventory được phép theo ADR-0001; cost gap được tạo khi thiếu layer.
- InventoryTransaction liên kết ReferenceOrderId và loại SALES_DEDUCTION.
- Idempotency hiện kiểm tra tồn tại transaction theo order/type; kiểm tra này quá rộng cho trường hợp posting thiếu một phần dòng.
- Công thức được đọc ở thời điểm deduction; không có sale-time BOM snapshot.

## 7.2 Hủy đơn và hoàn tiền

- Hủy online trước xử lý có reserve nên có thể release reservation.
- AdminOrderService hiện có thể gọi release cho nhiều trạng thái. Với đơn POS mới không reserve, hành vi này có nguy cơ cộng AvailableQty sai.
- Hủy sau pha chế không được mặc định nhập lại: phải ghi nhận waste/consumption.
- Hoàn tiền đã thanh toán và hoàn tồn là hai quyết định riêng.
- `OrderRefundService` hiện hỗ trợ reverse dựa trên bằng chứng SALES_DEDUCTION, nhưng phạm vi refund còn giới hạn; chưa có workflow item-level hoàn/đổi đầy đủ.

### Quy tắc đề xuất

1. Trước pha, nếu chỉ reserve thì release đúng reservation posting.
2. Sau pha, không hoàn nguyên liệu; tạo waste nếu cần.
3. Sau payment, refund doanh thu độc lập với inventory reversal.
4. Chỉ reverse các InventoryTransaction thực sự đã posting, theo line/allocation key.
5. Mỗi reverse lưu reason, actor, timestamp và source transaction.

## 7.3 POS hoạt động khi kiểm kê

- Không tắt POS toàn cửa hàng theo mặc định.
- Session lưu cutoff timestamp và system snapshot.
- Transaction POS sau cutoff không bị mất và không được ghi đè bởi counted quantity.
- Khi duyệt, tính chênh lệch theo snapshot + movements sau cutoff theo quy tắc đã khóa.
- Posting adjustment trong một transaction có idempotency key của session.

## 7.4 Kho âm

- Code hiện cho phép bán âm và tạo negative cost gap; chưa có quyền duyệt âm theo từng giao dịch POS.
- Có cảnh báo tồn nhưng không phải approval gate.
- Khi nhập hàng, cost gap settlement bù lại chi phí thiếu layer.
- Đề xuất giữ blind selling nhưng thêm cảnh báo, limit/policy theo Store và báo cáo cost gap; không chặn POS nếu chưa có quyết định nghiệp vụ mới.

# 8. Trạng thái và quy tắc chứng từ

## 8.1 PA

| Trạng thái | Actor hiện tại | Sửa | Tạo bước tiếp | Tác động tồn/nợ |
| --- | --- | --- | --- | --- |
| DRAFT | StoreManager đúng Store | Có | Submit | Không |
| SUBMITTED | StoreManager cancel; AW/BO review | Không | Hiện đang consolidation được — cần sửa | Không |
| UNDER_REVIEW | AW/BO | Không | Reject hoặc consolidation | Không |
| ALLOCATED | Hệ thống sau phân bổ đủ | Không | Theo dõi PO | Không |
| REJECTED/CANCELLED | Actor theo transition | Không | Không | Không |

`APPROVED` là trạng thái đề xuất, chưa có trong code/migration.

## 8.2 PO và Batch

| Chứng từ | Trạng thái chính | Quy tắc |
| --- | --- | --- |
| PO | DRAFT → APPROVED → MARKED_AS_SENT → PARTIALLY_RECEIVED → COMPLETED | Chỉ receipt thực tế cập nhật received; cancel/close remaining theo quyền hiện có |
| Batch | DRAFT/PENDING_APPROVAL → APPROVED → PDF_GENERATED → SENT_TO_SUPPLIER → PARTIALLY_RECEIVED → COMPLETED | BO approve/cancel; AW tạo/gửi; không sửa sau khi gửi |

## 8.3 Receipt, InventoryDocument và Transfer

| Chứng từ | Trạng thái | Posting |
| --- | --- | --- |
| BranchReceipt | DRAFT → CONFIRMED | Chỉ CONFIRMED tăng tồn; sau đó immutable |
| InventoryDocument | DRAFT/PENDING → CONFIRMED hoặc CANCELLED | Chỉ CONFIRMED tạo movement; confirmed không cancel trực tiếp |
| InventoryTransfer | DRAFT → DISPATCHED → COMPLETED | Dispatch trừ nguồn; receive cộng đích; cancel chỉ trước dispatch |

## 8.4 Ý nghĩa thao tác

- **Cancel**: dừng chứng từ trước khi phát sinh movement không thể sửa.
- **Void**: vô hiệu hóa chứng từ đã phát sinh; code chưa chuẩn hóa đầy đủ và phải đi kèm reverse.
- **Reverse**: tạo movement đối ứng có link đến nguồn.
- **Delete**: xóa dữ liệu; không dùng cho chứng từ nghiệp vụ đã lưu/confirmed.

# 9. Phân quyền

Hệ thống hiện chủ yếu dùng role checks, chưa có bộ permission code chi tiết cho từng action kho.

| Chức năng | Role hiện tại/đề xuất gần nhất | Ghi chú |
| --- | --- | --- |
| Xem/tạo/sửa/submit PA | StoreManager đúng Store | Chỉ draft được sửa |
| Review/reject PA | AccountantWarehouse, BusinessOwner | Cần thêm approve rõ ràng |
| Consolidation/tạo PO | AccountantWarehouse, BusinessOwner | BO duyệt Batch/PO |
| Gửi PO | AccountantWarehouse | Chỉ sau approve/document hợp lệ |
| Nhận PO | StoreManager/ShiftSupervisor đúng Store; BO giám sát | Controller/service hiện có điểm chưa đồng nhất, cần test scope |
| Xem giá nhập | AccountantWarehouse, BusinessOwner | Không mở rộng cho SalesStaff |
| Tạo/confirm adjustment | AW/BO; StoreManager theo phạm vi nếu được giao | Nên tách create và confirm |
| Tạo waste | StoreManager/ShiftSupervisor đúng Store | Duyệt vượt ngưỡng bởi AW/BO |
| Tạo/đếm kiểm kê | StoreManager/ShiftSupervisor | Người duyệt nên khác người đếm khi vượt ngưỡng |
| Tạo/dispatch transfer | Code hiện BO/AW | Giữ hiện trạng trong đợt này |
| Receive transfer | Nghiệp vụ mong muốn StoreManager/ShiftSupervisor tại đích | Code hiện còn thiên về BO/AW, là gap |
| Duyệt âm kho | Chưa có action riêng | Blind selling hiện tự cho phép |
| Xem công nợ NCC | Chưa có phân hệ | Chưa cấp quyền giả định |

Không thêm quyền Delete nếu chưa có nghiệp vụ và audit policy rõ ràng.

# 10. Rủi ro dữ liệu và biện pháp

| Rủi ro | Bằng chứng/nguyên nhân | Biện pháp đề xuất |
| --- | --- | --- |
| Trừ kho hai lần | POS controller, webhook, offline đều có thể gọi deduction | Idempotency theo order line/component/version, unique posting |
| Cộng kho sai khi cancel | Release luồng reserve cũ cho POS không reserve | Reverse theo transaction evidence |
| Nhập kho hai lần | BranchReceipt và InventoryDocument IMPORT song song | Một authority cho PO receipt |
| PO trùng | PO trực tiếp thiếu RequestKey/mapping PA | RequestKey + source allocation unique |
| Gộp sai PO | Chưa có merge history | Chỉ pre-creation consolidation; không merge PO đã gửi/received |
| Mất PA–PO | PO trực tiếp chỉ có RestockRequestId | Mapping line-level bắt buộc |
| Sai công nợ | Chưa có AP nhưng có thể developer gắn nhầm purpose DEBT | Không posting nợ đến khi có AP design |
| Sai giá vốn | Tồn âm, thiếu layer, actual cost khác PO | Cost gap/settlement và snapshot giá receipt |
| Sai quy đổi | Package qty/base unit không đồng nhất | Dùng package validator và snapshot conversion |
| Race condition | Submit/allocate/confirm đồng thời | RowVersion, serializable transaction, unique constraints |
| Không rollback | Nhiều bảng cập nhật trong confirm | Service giữ transaction và SaveChanges boundary |
| Sửa chứng từ confirmed | UI/service validation không đồng nhất | Immutable server-side; reverse document |
| Mất movement khi kiểm kê | Gán số đếm trực tiếp | Cutoff snapshot + adjustment transaction |

# 11. Danh sách file cần chỉnh sửa

## 11.1 Đã chỉnh sửa trong đợt AppLauncher

- Service: `Application/Services/AppLauncher/PosLaunchCoordinator.cs`, `AppLauncherService.cs`.
- DTO/options: `Application/DTOs/AppLauncher/PosLaunchDTOs.cs`, `Application/Options/PosLauncherOptions.cs`.
- JavaScript/config: `wwwroot/js/AppLauncher/app-launcher.js`, `appsettings.json`.
- Test: `CafeChain.Tests/PosLaunchCoordinatorTests.cs`, `SeedAndPosLauncherRefactorTests.cs`, `AppLauncherServiceTests.cs`.
- Tài liệu: `Doc/INVENTORY_PURCHASING_WORKFLOW.md`.

## 11.2 Dự kiến cho các giai đoạn kho — chưa chỉnh sửa

| Loại | Nhóm file dự kiến |
| --- | --- |
| Model | `Models/Inventories/Procurement/*`, `Stock/*`, `StockTake/*`, `Documents/*`, POS refund/source posting models |
| Enum/constants | `PurchaseAdviceConstants.cs`, `PurchaseOrderConstants.cs`, inventory document/transfer enums |
| DTO/ViewModel | DTO procurement/receipt; `ViewModels/Admin/InventoryDocuments/*`, transfer và POS refund DTO |
| Repository | Bổ sung repository cho PA/PO/Batch/BranchReceipt/POS posting; mở rộng AdminInventoryDocument/Transfer repositories |
| Service | PurchaseAdvice, Consolidation, PurchaseOrder, Batch, BranchReceipt, Restock posting, InventoryDeduction, refund/cancel |
| Controller | AdminPurchaseAdvices, Consolidation, PO/Batch, BranchReceipts, InventoryDocument, Transfer, POS order/refund |
| View | AdminPurchaseAdvices, PurchaseOrderBatches, BranchReceipts, InventoryDocument, InventoryTransfer |
| JavaScript | `inventorydocumentcreate.js`, `inventorydocument.js`, `inventorytransfercreate.js` và UI procurement |
| Configuration | Negative inventory policy, role/permission mapping nếu được duyệt |
| Migration | Chỉ sau khi duyệt Approved state, idempotency/mapping, stocktake cutoff hoặc AP schema |
| Seed | Seed role/permission/status và dữ liệu migration tương thích |
| Test | PA #184, consolidation #185, batch #186–189, PO #178, BranchReceipt #128, transfer, inventory document, POS deduction/refund/offline |

# 12. Kế hoạch triển khai

## Giai đoạn 1 — phân tích và thống nhất authority

- **File**: tài liệu này, ADR liên quan; chưa sửa nghiệp vụ.
- **Mục tiêu**: chốt PA approval, BranchReceipt authority, vai trò InventoryDocument và transfer.
- **Rủi ro**: hai màn hình tiếp tục posting cùng nghiệp vụ nếu chưa thống nhất.
- **Hoàn thành khi**: Product/BA/dev ký xác nhận bảng trạng thái và source-of-truth.
- **Test**: walkthrough PA→PO→receipt, inventory movement reconciliation.

## Giai đoạn 2 — chuẩn hóa model, enum và trạng thái

- **File**: PurchaseAdvice constants/model/configuration, migration, transition DTO/service/repository.
- **Mục tiêu**: thêm Approved có audit; xác định Cancel/Void/Reverse.
- **Rủi ro**: dữ liệu PA cũ ở UnderReview/Allocated cần mapping.
- **Hoàn thành khi**: migration idempotent, rollback script và transition tests pass.
- **Test**: double approve, stale row version, reject/cancel, dữ liệu legacy.

## Giai đoạn 3 — chuẩn hóa PA và PO

- **File**: PurchaseAdvice/Consolidation/PO/Batch services, repositories, mappings, controllers/views.
- **Mục tiêu**: chỉ Approved được gom; mọi PO line truy được source allocation; chống duplicate.
- **Rủi ro**: route PO trực tiếp và batch cạnh tranh allocation.
- **Hoàn thành khi**: cùng source không thể đặt vượt remaining và replay trả cùng kết quả.
- **Test**: một PA nhiều supplier, nhiều PA một batch, concurrent create, store/supplier/package mismatch.

## Giai đoạn 4 — tạo phiếu nhập từ PO

- **File**: BranchReceipt service/repository/controller/views, PO posting/status updater, inventory/cost services.
- **Mục tiêu**: BranchReceipt là authority duy nhất cho PO; actual/rejected/partial đầy đủ.
- **Rủi ro**: receipt cũ từng được nhập thêm qua InventoryDocument.
- **Hoàn thành khi**: reconcile receipt → transaction → layer → PO/restock bằng 0 sai lệch.
- **Test**: partial, reject, over-receipt, double confirm, concurrent confirm, rollback giữa chừng.

## Giai đoạn 5 — xuất, hủy, kiểm kê và chuyển kho

- **File**: InventoryDocument UI/service/repository, StockTake models/workflow, transfer workflow.
- **Mục tiêu**: tách UI; cutoff kiểm kê; discrepancy/reverse transfer; waste approval.
- **Rủi ro**: thay đổi màn hình đang dùng và dữ liệu stocktake chưa nối.
- **Hoàn thành khi**: mỗi movement có source, confirmed immutable, không còn internal transfer qua generic form.
- **Test**: concurrent POS during count, waste approval, partial transfer, damaged transit, reverse after dispatch.

## Giai đoạn 6 — tích hợp POS

- **File**: POSOrder/InventoryDeduction/AdminOrder/InventoryService/OrderRefund và posting repositories.
- **Mục tiêu**: một cơ chế reserve/deduct/reverse; idempotency line-level; không cộng tồn giả.
- **Rủi ro**: online, cash, PayOS, webhook và offline replay có timing khác nhau.
- **Hoàn thành khi**: mỗi component bán có tối đa một deduction và mỗi reverse tham chiếu posting gốc.
- **Test**: cash/PayOS/offline replay, topping/BTP, cancel trước/sau pha, refund, negative inventory/cost settlement.

## Giai đoạn 7 — migration dữ liệu và hồi quy

- **File**: migration, repair/reconciliation SQL, seed, toàn bộ integration tests.
- **Mục tiêu**: nâng cấp dữ liệu không mất traceability và phát hiện chênh lệch trước cutover.
- **Rủi ro**: allocation/receipt cũ thiếu source; test SQL cần instance thật.
- **Hoàn thành khi**: dry-run/rollback thành công, reconciliation bằng 0 hoặc có danh sách ngoại lệ được duyệt.
- **Test**: migration repeatability, concurrency SQL Server, full regression POS/procurement/inventory.

## Phụ lục A — file đã phân tích nhưng không chỉnh sửa

- Controller: `AdminPurchaseAdvicesController`, `AdminPurchaseAdviceConsolidationController`, `AdminPurchaseOrdersController`, `AdminPurchaseOrderBatchesController`, `AdminBranchReceiptsController`, `AdminInventoryDocumentController`, `AdminInventoryTransferController`, POS order/refund controllers.
- Service: PurchaseAdvice, Consolidation, PO, Batch, BranchReceipt, RestockRequest/Posting, InventoryDocument, InventoryTransfer, InventoryDeduction, POSOrder, AdminOrder, InventoryService, OrderRefund.
- Model: toàn bộ nhóm Procurement, Stock/Receipt, Documents, Transactions, Costing, Transfers, StockTake, Supplier, StoreInventory và Order/Refund.
- Repository: AdminInventoryDocument, AdminInventoryTransfer, AdminStoreInventory, AdminSupplier và POS/order repositories.
- UI: các view Admin PurchaseAdvice/PO/Batch/BranchReceipt/InventoryDocument/InventoryTransfer và JavaScript tương ứng.
- Data: `Migrations/20260721040634_InitialCreate*`, model snapshot, seed SQL, dashboard stored procedures và test SQL Server liên quan.
- Kiến trúc: `CONTEXT.md`, ADR-0001 đến ADR-0009 và `docs/agents/domain.md`.

## Phụ lục B — test case AppLauncher

1. Frontend đã chạy, PrintBridge chưa chạy: POS trả Ready.
2. Frontend đã chạy, PrintBridge đang chạy: kết quả không đổi.
3. Project PrintBridge bị đổi tên/xóa hoặc process crash: POS không bị ảnh hưởng.
4. Store khác cấu hình bridge cũ: POS vẫn mở nếu có quyền.
5. Frontend chưa chạy: launcher khởi động npm và chờ health check.
6. Thiếu frontend/package.json: trả `POS_FRONTEND_PROJECT_MISSING`.
7. Port frontend bị process khác chiếm: trả `POS_FRONTEND_PORT_IN_USE`.
8. Thiếu npm hoặc startup timeout: trả đúng lỗi frontend.
9. Gọi launch đồng thời: semaphore ngăn khởi động trùng.
10. Tắt/mở lại POS: không kill PrintBridge độc lập.
11. Chạy PrintBridge thủ công: Hub/presence và in thử vẫn hoạt động.
12. UI/polling không còn thông báo kiểm tra PrintBridge.
