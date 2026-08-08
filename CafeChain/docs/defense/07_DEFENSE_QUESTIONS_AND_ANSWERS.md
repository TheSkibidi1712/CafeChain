# Câu hỏi và trả lời bảo vệ

## A. Bài toán và kiến trúc

### 1. CafeChain giải quyết bài toán gì?

**Ngắn:** CafeChain kết nối bán hàng, ca, menu/BOM, tồn kho và mua hàng của một chuỗi đồ uống trên cùng dữ liệu truy vết được.

**Chi tiết:** Một Order từ POS liên kết Store/WorkShift, snapshot menu và Payment; tiêu hao đi vào inventory/costing; thiếu hàng tạo Restock rồi PA/PO/Receipt. Authority: `AppDbContext.cs`, các model Order/Inventory/Procurement.

### 2. Vì sao không chỉ làm một ứng dụng POS?

**Ngắn:** POS chỉ ghi doanh thu; chuỗi còn cần quản lý người, tồn, nguyên liệu, cung ứng và phê duyệt.

**Chi tiết:** Nếu tách rời, giá/BOM/tồn và PO không truy ngược được về giao dịch. CafeChain giữ identity và snapshot xuyên module để đối soát.

### 3. Kiến trúc của dự án là gì?

**Ngắn:** Modular monolith ASP.NET Core với MVC/API, application services, EF Core/SQL Server; POS là React riêng.

**Chi tiết:** `Program.cs` đăng ký web, database, auth, service, repository và worker trong một process. Đây không phải microservices và cũng không phải Clean Architecture thuần vì service đôi khi dùng DbContext trực tiếp.

### 4. Vì sao dùng hai frontend Razor và React?

**Ngắn:** Admin thiên về form/workflow server-rendered; POS cần tương tác nhanh và offline.

**Chi tiết:** Razor nằm trong `Areas/Admin/Views`; React/Vite dùng Dexie/IndexedDB và SignalR trong `CafeChain.Frontend`. Đổi lại dự án phải quản lý consistency giữa hai stack.

### 5. Database và ORM là gì?

**Ngắn:** SQL Server với Entity Framework Core 8.

**Chi tiết:** `DatabaseServiceExtensions` gọi `UseSqlServer` và lazy-loading proxies; `AppDbContext` quản lý entity/configuration.

### 6. Vì sao gọi đây là hệ thống có traceability?

**Ngắn:** Vì chứng từ sau giữ reference/snapshot về chứng từ và dữ liệu trước.

**Chi tiết:** PA line trỏ Restock/allocation; PO line trỏ PA line/Restock và giữ package/UOM/price; Receipt line trỏ PO line và InventoryTransaction. Xem các entity procurement/stock.

## B. Authentication, authorization và role

### 7. Authentication khác authorization thế nào?

**Ngắn:** Authentication xác định “ai”; authorization quyết định “được làm gì và ở đâu”.

**Chi tiết:** Cookie/JWT được cấu hình trong `AuthenticationServiceExtensions`; permission handler, scope và state guard nằm ở authorization/service.

### 8. Vì sao cần cả role và permission?

**Ngắn:** Role giúp phân nhóm, permission cho phép kiểm soát đến từng action.

**Chi tiết:** `AccountantWarehouse` có nhiều permission procurement nhưng không có PO Approve. `PermissionRequirement` kiểm tra effective permission thay vì hard-code role cho mọi endpoint.

### 9. Chỉ ẩn nút trên UI có đủ bảo mật không?

**Ngắn:** Không. Backend luôn phải chặn direct request.

**Chi tiết:** Menu dùng effective permissions, controller có `[RequirePermission]`, service revalidate scope/state/ownership. Ví dụ PO approve còn kiểm tra creator và RowVersion.

### 10. StoreScope dùng để làm gì?

**Ngắn:** Ngăn actor đọc hoặc sửa dữ liệu cửa hàng ngoài phạm vi được giao.

**Chi tiết:** `AdminStoreScopeResolver` và `IScopeAuthorizationService` resolve allowed stores; service Receipt/PO/Ice tiếp tục kiểm tra StoreId khi mutate.

### 11. SystemAdmin có phải superuser nghiệp vụ không?

**Ngắn:** Không mặc định.

**Chi tiết:** Permission seed cấp SystemAdmin chủ yếu staff/permission/settings/diagnostics; nhiều quyền PA/PO/receipt không được cấp. Đây là separation of duties có chủ đích.

### 12. Quản lý vùng trong code tên gì?

**Ngắn:** `AreaManager`.

**Chi tiết:** UI gọi “Quản lý vùng”; không có enum `RegionManager` trong role constants hiện tại. Authority: `RoleConstants.cs`.

### 13. Vì sao StoreManager không tự tạo PO?

**Ngắn:** Store khai báo nhu cầu, còn Kế toán/kho kiểm soát nguồn, supplier và điều khoản mua.

**Chi tiết:** Store Manager có Restock Create/Submit; PA Create/Review/Supplier/CreatePO được seed cho Accountant. Tách này giảm xung đột lợi ích và sai quy cách.

### 14. Maker-checker được thực hiện ra sao?

**Ngắn:** Người tạo PO/POB không được tự duyệt.

**Chi tiết:** Service so `CreatedByStaffId` với actor duyệt và từ chối nếu trùng; Owner cần permission và state/RowVersion hợp lệ. Xem `PurchaseOrderService.TransitionAsync` và `PurchaseOrderBatchService.ApproveAsync`.

## C. POS và WorkShift

### 15. Ai được bán hàng?

**Ngắn:** Actor có `App.POS` và permission action, thuộc store/WorkShift/operator hợp lệ.

**Chi tiết:** Seed cấp App.POS cho Store Manager, Shift Supervisor và Sales Staff; API còn kiểm tra JWT claims, terminal và ca.

### 16. Ai mở và đóng ca?

**Ngắn:** Người có permission WorkShift tương ứng; không suy ra chỉ từ tên role.

**Chi tiết:** `POSShiftController` bảo vệ từng route Open/Close/Exception/Reconcile; `WorkShiftService` revalidate staff/store/schedule/OTP.

### 17. WorkShift lưu những gì?

**Ngắn:** Store, chủ ca/operator, terminal, thời gian, business date, trạng thái và số liệu két.

**Chi tiết:** `WorkShift.cs` có StartingCash, ExpectedEndingCash, ActualEndingCash, discrepancy, row version, offline manifest và reconciliation fields.

### 18. Nếu hai lần nhấn thanh toán thì sao?

**Ngắn:** Cùng `ClientOrderId` và payload sẽ trả lại Order cũ, không tạo đơn thứ hai.

**Chi tiết:** `POSOrderService.CommitOrderAsync` check trước transaction và unique race; payload khác với cùng key trả conflict `IDEMPOTENCY_KEY_REUSED`.

### 19. Nếu mạng lỗi giữa lúc thanh toán thì sao?

**Ngắn:** POS giữ queue/snapshot local và sync lại bằng idempotency key.

**Chi tiết:** React dùng Dexie/IndexedDB; offline batch mang `ClientOrderId`; server trả order cũ khi retry. External payment vẫn cần reconciliation riêng.

### 20. Server có tin giá do client gửi không?

**Ngắn:** Không; server xác nhận catalog, recipe và giá.

**Chi tiết:** `POSStoreMenuSaleValidator` resolve store menu/recipe/topping; `POSOrderService` tạo line từ accepted values.

### 21. Vì sao OrderLine cần snapshot?

**Ngắn:** Để lịch sử không đổi khi menu, giá, BOM hoặc topping policy thay đổi.

**Chi tiết:** `OrderDetail` giữ accepted price, catalog version, recipe snapshot, size/name và ice; topping giữ policy snapshot.

### 22. Vì sao cho phép tồn âm?

**Ngắn:** Để POS offline không dừng bán chỉ vì không xác minh được tồn realtime.

**Chi tiết:** ADR-0001 chọn blind selling; negative inventory phải được cảnh báo và đối soát sau sync. Đây là trade-off vận hành, không phải bỏ ledger.

### 23. Vì sao trừ kho lỗi không rollback đơn đã trả tiền?

**Ngắn:** Vì giao dịch tài chính đã hoàn tất; side effect tồn phải retry an toàn.

**Chi tiết:** Controller/webhook gọi trừ tồn sau commit và service chống trừ lặp theo Order. ADR-0009 mô tả hướng durable recovery, nhưng worker/outbox chuyên dụng chưa được code hiện tại chứng minh; đây là hạn chế cần nói rõ.

### 24. Khi nào ca cần đối soát?

**Ngắn:** Khi đóng ngoại lệ, có late offline sync hoặc dữ liệu két cần kiểm tra lại.

**Chi tiết:** trạng thái `RECONCILIATION_REQUIRED`; `ReconcileAsync` chặn nếu còn offline order/payment, dùng request key và row version rồi cập nhật expected cash.

## D. Inventory, UOM và costing

### 25. Vì sao phải có base UOM?

**Ngắn:** Để mọi nghiệp vụ so sánh và ghi tồn bằng cùng một đơn vị chuẩn.

**Chi tiết:** Ingredient dùng g/ml/cái; Restock/PO/Receipt có thể nhập kg/L/gói nhưng conversion materialize về base quantity trước posting.

### 26. Có cho đổi L sang kg không?

**Ngắn:** Không, trừ khi có conversion hợp lệ theo nguyên liệu/density.

**Chi tiết:** `UnitConversion` thuộc Ingredient; physical conversion kiểm tra dimension. Không dùng danh sách kg/L/pcs chung cho mọi ingredient.

### 27. Mua theo gói và mua lẻ khác nhau thế nào?

**Ngắn:** Gói tính số package × package price; mua lẻ tính quantity theo loose UOM × loose unit price.

**Chi tiết:** `PurchaseMode`, `IngredientSupplier` và PO line giữ field tách biệt. Package áp package MOQ; loose áp loose MOQ/step.

### 28. Giá gói được quy về giá base unit thế nào?

**Ngắn:** Giá gói chia cho lượng base UOM trong gói.

**Chi tiết:** Ví dụ 168.000 đ cho 200 ml là 840 đ/ml. Conversion và package quantity được snapshot; không hiểu 168 là 168.000 nếu người dùng không nhập rõ.

### 29. FIFO là gì trong CafeChain?

**Ngắn:** Khi tính xuất/COGS, lấy lượng từ lớp giá nhập sớm trước.

**Chi tiết:** `InventoryCostLayer` có remaining quantity/unit cost; profitability service sắp theo CreatedAt/ID rồi consume đến đủ required quantity.

### 30. Khi nào giá vốn được coi là đầy đủ?

**Ngắn:** Chỉ khi BOM, BTP, topping mặc định và conversion đều có đủ lớp giá/số lượng.

**Chi tiết:** `DrinkSizeProfitabilityQueryService` trả status theo section; estimated cost là null khi incomplete, không lấy known partial cost làm total.

### 31. Margin khác Markup thế nào?

**Ngắn:** Margin chia lợi nhuận cho giá bán; Markup chia lợi nhuận cho giá vốn.

**Chi tiết:** Với giá 30.000, cost 10.000, profit 20.000: Margin 66,67%; Markup 200%. Authority: `PriceSuggestionService`.

### 32. Giá đề xuất có tự thay đổi giá bán không?

**Ngắn:** Không, `Suggest` chỉ tính preview.

**Chi tiết:** Persist là endpoint `UpdatePrice` riêng, cần Owner permission, reason và RowVersion/audit.

### 33. PreparedItem khác Recipe thế nào?

**Ngắn:** PreparedItem là identity tồn kho BTP ổn định; Recipe là phiên bản công thức.

**Chi tiết:** Một BTP có thể có nhiều recipe versions; thay formula không được chia stock cũ sang identity mới. ADR-0006 và model tương ứng xác nhận.

### 34. Topping mặc định có đồng nghĩa miễn phí không?

**Ngắn:** Không.

**Chi tiết:** `IsDefaultSelected`, `PriceTreatment` và `CostTreatment` là ba khái niệm độc lập. Default chỉ nói POS tự chọn; giá/cost do treatment quyết định.

## E. Procurement và Supplier

### 35. Restock khác PA và PO thế nào?

**Ngắn:** Restock là nhu cầu; PA là đề nghị xem xét mua; PO là đơn đặt với supplier.

**Chi tiết:** Ba aggregate có state, actor và trace riêng. Không aggregate nào tự tăng tồn.

### 36. Một PA có tạo được PO thường không?

**Ngắn:** Có, nếu một nguồn hợp lệ và còn quantity sẵn sàng đặt.

**Chi tiết:** PO gộp chỉ dành cho nhiều nguồn tương thích; service/controller có flow normal riêng. Không có rule chung “tối thiểu hai PA” cho PO thường.

### 37. Khi nào tạo PO gộp?

**Ngắn:** Khi có ít nhất hai nguồn độc lập tương thích về supplier và điều kiện mua.

**Chi tiết:** Batch tạo allocation và child PO theo store; một nguồn phải đi PO thường. `PurchaseOrderBatchService` bảo vệ classification/state.

### 38. Làm sao chống cùng PA bị đặt hai lần?

**Ngắn:** Re-read và lock allocation, kiểm remaining quantity, row version/idempotency rồi ghi ordered quantity trong transaction.

**Chi tiết:** PO line/allocation trace số lượng; SQL locks/serializable và conflict handling ngăn hai actor cùng consume một phần.

### 39. Supplier package lưu gì?

**Ngắn:** Ingredient, content UOM/quantity, package price/MOQ, lead time, primary flag và loose purchase fields.

**Chi tiết:** Authority là `IngredientSupplier`; price history tách riêng, PO/receipt giữ snapshot.

### 40. Lead time theo cửa hàng xử lý thế nào?

**Ngắn:** `SupplierStore.LeadTimeOverrideDays` override lead time mặc định khi có giá trị.

**Chi tiết:** Assignment inactive không dùng cho đơn mới nhưng lịch sử vẫn giữ. DeliverySchedule hiện là text metadata nếu không có calculator cụ thể.

### 41. Khi nào tồn kho thực sự tăng?

**Ngắn:** Chỉ khi BranchReceipt được `CONFIRMED` và chỉ theo accepted quantity.

**Chi tiết:** Confirm khóa receipt/request/inventory, materialize conversion, ghi fulfillment/PO posting, InventoryTransaction và cost layer trong transaction.

### 42. Nếu confirm receipt hai lần thì sao?

**Ngắn:** Lần sau trả replay success, không post tồn lần hai.

**Chi tiết:** `BranchReceiptService.ConfirmAsync` nhận thấy status confirmed và trả các transaction ID cũ; line có InventoryTransactionId cũng là guard.

### 43. Rejected quantity có tăng tồn không?

**Ngắn:** Không.

**Chi tiết:** Receipt line tách received/accepted và rejected, có reason/issue type; inventory posting chỉ lấy accepted/received-base theo contract.

### 44. Nếu supplier đổi giá sau khi đã đặt thì PO cũ ra sao?

**Ngắn:** PO cũ không đổi vì line đã snapshot giá, package và conversion.

**Chi tiết:** Live offer phục vụ lựa chọn mới; historical PO/receipt dùng fields snapshot để audit và costing.

## F. Operational Ice, audit và reporting

### 45. WorkShift và OperationalShift khác nhau thế nào?

**Ngắn:** WorkShift quản lý POS/két; OperationalShift quản lý đá theo ca.

**Chi tiết:** OperationalShift có thể link nhiều WorkShift cùng store/time overlap để tính theoretical usage, nhưng không thay state của WorkShift.

### 46. Làm sao tránh một WorkShift bị tính đá hai lần?

**Ngắn:** Backend kiểm conflict và relation/transaction đảm bảo một link hợp lệ.

**Chi tiết:** Candidate và link revalidate store, overlap, status, scope; idempotent replay không tạo duplicate.

### 47. Chênh lệch đá được ghi kho thế nào?

**Ngắn:** Chênh lệch dương cần xuất kho có posting idempotent; chênh lệch âm không tự tăng tồn.

**Chi tiết:** `OperationalIceService` tạo `ICE_VARIANCE_OUT` với before/after/cost và `IceInventoryPosting`; reconcile âm chỉ ghi lý do/đóng theo contract.

### 48. Vì sao audit không nên hiển thị raw JSON?

**Ngắn:** Raw JSON khó hiểu, dễ lộ ID kỹ thuật và không giúp người dùng quyết định.

**Chi tiết:** UI nên projection thành event title, actor, thời gian, lý do và before/after nghiệp vụ; payload kỹ thuật giữ cho admin/log khi cần.

### 49. Dashboard lấy số liệu lợi nhuận từ đâu?

**Ngắn:** Từ doanh thu Order và phần COGS đã xác nhận/đầy đủ.

**Chi tiết:** Widget metadata phân biệt confirmed COGS/gross profit/data status; phần cost thiếu không nên coi là 0. Xem `DashboardWidgetCatalog` và analytics services.

### 50. Làm sao biết một KPI có đúng denominator và filter?

**Ngắn:** Tra metadata/widget query cụ thể, không suy từ tên biểu đồ.

**Chi tiết:** Mỗi widget có dimension/value/unit/sample/filter riêng trong `DashboardWidgetCatalog`; evidence index phải trỏ đúng service/query.

## G. Testing, security, performance và hướng phát triển

### 51. Chiến lược test của task tài liệu là gì?

**Ngắn:** Test đúng module/evidence cần xác minh, không chạy full suite vô cớ.

**Chi tiết:** `SkillTest_SKILL.md` yêu cầu TEST_SCOPE_PLAN; task docs dùng permission, procurement, supplier/UOM, costing, ice tests và static doc checks.

### 52. Hệ thống xử lý SQL injection thế nào?

**Ngắn:** Phần lớn query dùng LINQ/EF parameterization; raw SQL khóa dùng interpolation parameterized.

**Chi tiết:** Không nối input người dùng vào SQL. Vẫn cần review mọi raw SQL mới và giữ validation/least privilege.

### 53. Secret được quản lý ra sao?

**Ngắn:** Development dùng user secrets; deployment dùng environment/secret provider.

**Chi tiết:** `Program.cs` re-add user secrets/environment; JWT key bắt buộc. Không commit password/token/appsettings secret vào docs.

### 54. Vì sao dùng RowVersion chưa đủ để chống duplicate?

**Ngắn:** RowVersion chống lost update, còn retry/concurrent insert cần idempotency/unique identity.

**Chi tiết:** POS dùng `ClientOrderId`; receipt dùng request/posting key; PO allocation cần lock/remaining quantity. Các kỹ thuật bổ sung nhau.

### 55. Rủi ro performance chính là gì?

**Ngắn:** N+1/lazy loading, query history lớn và analytics nặng.

**Chi tiết:** List nên projection + `AsNoTracking` + pagination; detail không load toàn history; dashboard có query chuyên biệt. EF lazy proxies phải dùng thận trọng.

### 56. Hạn chế quan trọng nhất hiện tại là gì?

**Ngắn:** Runtime role rehearsal chưa tự động và demo DB còn một số status legacy.

**Chi tiết:** Code permission/state đã rõ, nhưng data drift có thể làm demo khó hiểu. Ưu tiên repair idempotent và automated navigation/handoff smoke.

### 57. Tính năng topping thay thế đã hoàn chỉnh chưa?

**Ngắn:** Chưa đủ authority để khẳng định hoàn chỉnh.

**Chi tiết:** Constants hiện có included/additional/display-only, chưa có replacement treatment tổng quát. Đây là future work, không demo như đã xong.

### 58. Nếu được phát triển tiếp, ưu tiên gì?

**Ngắn:** Automated role E2E, normalized status repair, unified audit và observability.

**Chi tiết:** Các hạng mục này giảm rủi ro vận hành mà không phá contract nghiệp vụ đã có; xem `08_KNOWN_LIMITATIONS_AND_FUTURE_WORK.md`.
