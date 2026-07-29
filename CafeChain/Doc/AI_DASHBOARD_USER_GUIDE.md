# Hướng dẫn sử dụng AI Dashboard CafeChain

Tài liệu này dành cho quản trị viên, quản lý cửa hàng và nhóm vận hành hệ thống AI Dashboard.

## 1. Mục đích

AI Dashboard giúp xem KPI và giải thích hoạt động kinh doanh dựa trên dữ liệu backend đã tổng hợp:

```text
Database → Repository → Dashboard Service → Fact/Statistic/Evidence
         → AI explanation hoặc deterministic fallback
```

AI không truy vấn database, không sinh SQL và không được tự tạo số liệu. Khi thiếu dữ liệu, hệ thống phải hiển thị giới hạn thay vì suy đoán nguyên nhân.

## 2. Quyền truy cập và phạm vi cửa hàng

Người dùng phải có policy `AdminDashboardApp`.

Phạm vi dữ liệu được xác định từ `StaffScope` ở backend:

- Có scope một cửa hàng: chỉ xem được cửa hàng đó.
- Có nhiều scope: dữ liệu được giới hạn trong tập cửa hàng được cấp quyền.
- Gửi `StoreId` không thuộc scope sẽ bị từ chối trước khi query dữ liệu.
- Không có scope hợp lệ sẽ không được coi là có quyền truy cập.

Không dùng bộ lọc trên giao diện làm nguồn tin cậy duy nhất.

## 3. Quy trình sử dụng

1. Mở Dashboard quản trị và chọn cửa hàng hoặc tập cửa hàng được phép.
2. Chọn khoảng thời gian. Khoảng ngày được xử lý theo quy ước backend; không tự cộng thêm ngày ở giao diện.
3. Nhập câu hỏi phân tích, ví dụ:
   - `Tại sao doanh thu giảm?`
   - `Cửa hàng nào có doanh thu cao nhất?`
   - `Nguyên liệu nào đang có nguy cơ thiếu?`
   - `Đơn mua nào đang quá hạn?`
4. Chọn phân tích hoặc xem kết quả KPI.
5. Đọc các phần:
   - Fact: số liệu backend.
   - Statistic: tổng hợp, tỷ lệ, xếp hạng hoặc baseline.
   - Anomaly: cảnh báo có ngưỡng hoặc quy tắc backend hỗ trợ.
   - Entity Evidence: cửa hàng, sản phẩm, nguyên liệu, nhà cung cấp hoặc đơn mua liên quan.
   - Recommendation: hành động kiểm tra an toàn, không phải lệnh tự động.
6. Nếu đổi bộ lọc liên tục, chỉ kết quả của request mới nhất được giữ lại.

Các API MVC tương ứng:

```text
POST /Admin/AdminDashboardIntelligence/Parse
POST /Admin/AdminDashboardIntelligence/Execute
POST /Admin/AdminDashboardIntelligence/Analyze
POST /Admin/AdminDashboardIntelligence/Explain
```

Các request cần antiforgery token và phải truyền `CancellationToken` từ request HTTP.

## 4. Ý nghĩa Metric

Mỗi widget có metric contract riêng gồm tên metric, đơn vị, phép tổng hợp và field giá trị. Một số ví dụ:

| Widget | Metric | Đơn vị | Cách tính |
|---|---|---|---|
| WorkShiftSales | Revenue | VND | SUM(netSales) |
| OrderHeatmap | OrderCount | ORDER | SUM(totalOrders) |
| InventoryThresholdRisk | RiskIngredientCount | INGREDIENT | SUM(riskIngredientCount) |
| PurchaseOrderPipeline | PurchaseOrderCount | COUNT | SUM(purchaseOrderCount) |
| SizeMargin | GrossProfit | VND | SUM(confirmedGrossProfit) |
| WorkforceStaffPerformance | OrdersPerWorkShift | ORDER | SUM(totalOrders) / SUM(workShiftCount) |

Không diễn giải số dòng dữ liệu thành metric nghiệp vụ nếu catalog không khai báo điều đó.

## 5. DataStatus và Confidence

| DataStatus | Ý nghĩa | Cách đọc kết quả |
|---|---|---|
| `OK` | Dữ liệu đầy đủ | Có thể sử dụng phân tích bình thường |
| `NO_DATA` | Không có dòng dữ liệu | Không có kết luận meaningful; Ollama không được gọi |
| `PARTIAL` | Có một phần dữ liệu hoặc widget lỗi | Giảm mức tin cậy, đọc phần giới hạn |
| `PARTIAL_COGS` | Thiếu COGS | Không kết luận chắc chắn về gross profit/margin |
| `MISSING_CONFIG` | Thiếu threshold/BOM/config | Không kết luận chắc chắn về rủi ro cấu hình |
| `ERROR` | Lỗi hoàn toàn | Kết quả AI chuyển deterministic fallback |

Confidence được backend tính từ sample size, baseline, entity evidence, widget lỗi và DataStatus. Model không được tự chọn Confidence.

## 6. Đọc Evidence an toàn

Store evidence có thể gồm Revenue, Orders, AOV, Rank và ContributionPercent.

Payment evidence phân biệt:

- `TransactionShare`: tỷ trọng số giao dịch.
- `RevenueShare`: tỷ trọng doanh thu.

Product/category evidence có thể gồm Quantity, Revenue, COGS, GrossProfit, Margin và ContributionPercent. Nếu COGS thiếu, trạng thái phải là `PARTIAL_COGS`.

Purchase Order evidence có thể gồm mã PO, cửa hàng, nhà cung cấp, ngày dự kiến, số ngày trễ, giá trị đặt và status. Không dùng tên giả như `Unknown Supplier` để làm evidence đầy đủ.

## 7. AI explanation và fallback

Khi `ExplanationEnabled=true` và dữ liệu đủ:

1. Backend chọn Fact/Statistic/Anomaly/Evidence liên quan đến intent.
2. Context bị giới hạn theo top-K và ngân sách.
3. Ollama trả structured JSON.
4. Backend kiểm tra schema, EvidenceId, chart coverage và numeric claims.

Nếu Ollama timeout, connection refused, HTTP error, JSON lỗi, schema lỗi hoặc số liệu không khớp evidence:

```text
Ollama → validation fail → deterministic fallback
```

Fact, chart và statistic backend vẫn được giữ nguyên.

Nếu `ExplanationEnabled=false`, hệ thống không gọi Ollama cho explanation.

## 8. Chart và table fallback

Dashboard hỗ trợ line, bar, horizontal bar, donut, stacked bar, heatmap, scatter và KPI.

Khi dữ liệu không đủ hoặc cấu hình chart không hợp lệ, giao diện dùng table fallback. Không coi chart rỗng là dữ liệu bằng không.

Khi đổi filter:

- Request cũ bị hủy bằng `AbortController` khi có thể.
- Request sequence và filter fingerprint ngăn kết quả cũ ghi đè kết quả mới.

## 9. Xử lý tình huống thường gặp

### Không thấy dữ liệu

Kiểm tra khoảng thời gian, cửa hàng được cấp scope và DataStatus. Nếu là `NO_DATA`, đây là kết quả hợp lệ chứ không phải metric bằng 0.

### Kết quả có cảnh báo `PARTIAL_COGS`

Chỉ sử dụng Revenue/Quantity; không kết luận chắc chắn về margin hoặc gross profit.

### AI không trả lời

Kiểm tra `ExplanationEnabled`, trạng thái Ollama và log fallback. Dashboard vẫn phải hiển thị Fact/chart deterministic.

### Bị 403

Tài khoản thiếu policy hoặc request chứa cửa hàng ngoài StaffScope. Không thử vượt qua bằng cách đổi StoreId trên frontend.

## 10. Cấu hình feature flag

Mặc định production:

```json
{
  "DashboardIntelligence": {
    "IntentParserEnabled": true,
    "ExplanationEnabled": false
  }
}
```

Development có thể bật explanation để kiểm thử Ollama. Không bật explanation production nếu chưa hoàn tất kiểm tra runtime, timeout và fallback.

## 11. Kiểm thử dành cho developer

Từ thư mục solution:

```powershell
dotnet build CafeChain/CafeChain.csproj --no-restore

dotnet test CafeChain.Tests/CafeChain.Tests.csproj --no-build `
  --filter "FullyQualifiedName~DashboardMetricContractTests|FullyQualifiedName~DashboardDataQualityContractTests|FullyQualifiedName~DashboardAiFallbackContractTests|FullyQualifiedName~DashboardIntelligenceP0P1ContractTests"
```

SQL integration cần SQL Server test instance và biến:

```powershell
$env:CAFECHAIN_TEST_SQLSERVER_CONNECTION_STRING =
  "Server=localhost\SQLEXPRESS02;Database={Database};Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=true"
```

Kiểm tra Ollama:

```powershell
Invoke-RestMethod http://localhost:11434/api/tags
```

## 12. Giới hạn nghiệm thu hiện tại

Các contract/unit/SQL/Ollama validation của AI Dashboard đã được triển khai. Full solution test vẫn có thể chứa lỗi module ngoài phạm vi. Browser E2E chỉ được ghi nhận PASS khi browser backend khả dụng và đã kiểm tra DOM/network thực tế.
