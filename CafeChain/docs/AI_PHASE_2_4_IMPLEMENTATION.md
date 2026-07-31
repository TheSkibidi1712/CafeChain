# AI Phase 2–4 Implementation

> **Historical implementation note.** Trạng thái feature flag, UI visibility,
> StaffScope và giới hạn runtime hiện hành được tổng hợp tại
> [`../Doc/AI_FEATURES_BUSINESS_AND_TECHNICAL_GUIDE.md`](../Doc/AI_FEATURES_BUSINESS_AND_TECHNICAL_GUIDE.md).
> Không dùng bảng phase trong tài liệu này để khẳng định một feature đang bật.

Ngày cập nhật: 22/07/2026

Tài liệu này là phần triển khai tiếp theo của `AI_BUSINESS_ANALYSIS.md`. Mục tiêu là ghi lại contract đã đưa vào code, điều kiện bật tính năng và các giới hạn dữ liệu cần tôn trọng khi pilot.

## 1. Trạng thái triển khai

| Phase | Thành phần | Trạng thái | Cơ chế an toàn chính |
| --- | --- | --- | --- |
| 2 | Dashboard natural-language analytics | Hoàn thành code, mặc định OFF | 8 intent whitelist, StoreScope trước query, không có SQL trong contract |
| 2 | Dashboard insight/explanation | Hoàn thành code, mặc định OFF | Rule/statistics quyết định insight; LLM chỉ giải thích `AnalysisId` do server lưu |
| 3 | Revenue/product forecast | Hoàn thành engine và worker, mặc định OFF | Data-quality gate, rolling-origin backtest, không future leakage |
| 3 | Supplier scoring | Hoàn thành code, mặc định OFF | Score deterministic, conversion/MOQ là constraint, AI không đổi ranking |
| 4 | Cấu hình lịch và cảnh báo thiếu người | Hoàn thành cấu hình và worker, mặc định OFF | Rule backend phát hiện thiếu người; quản lý phân lịch thủ công |
| 4 | POS cross-sell | Hoàn thành materialization, POS panel và A/B telemetry, mặc định OFF | Tối đa 3 mục, không tự thêm, POS có fallback |
| 4 | Operational anomaly | Hoàn thành baseline/MAD, feedback và notification, mặc định OFF | Materiality + robust score, StoreScope, không kết luận gian lận |

Migration `AddAiIntelligencePhase2To4V3` bổ sung persistence cho forecast, dữ liệu tối ưu ca, catalog/exposure POS, operational anomaly và `Orders.RecommendationSessionId`. Migration không lặp lại các cột Phase 1 và không thay đổi enum cũ.

## 2. Dashboard Intelligence

Catalog chỉ gồm:

1. `NetSalesTrend`.
2. `StoreRanking`.
3. `TopProducts`.
4. `HourlyOrders`.
5. `InventoryWasteByStoreIngredient`.
6. `OverduePurchaseOrders`.
7. `SupplierQuality`.
8. `WorkforceShiftStatus`.

Parser deterministic được ưu tiên. Ollama chỉ được dùng khi bật flag và parser deterministic không nhận diện được. Prompt ngoài catalog trả `UNSUPPORTED_INTENT`; không có fallback SQL.

`Execute` luôn lấy danh sách Store khả dụng qua Dashboard service hiện có; filter luôn mang `StaffId` của actor. Dataset giải thích được giữ trong cache 10 phút theo `StaffId + AnalysisId`, vì vậy client không thể gửi dataset tùy ý vào endpoint explanation.

Giới hạn mặc định:

- 20 request/phút/Staff.
- Prompt tối đa 500 ký tự.
- Period tối đa 366 ngày.
- Top từ 1 đến 100.
- Chart do policy cố định chọn; không render HTML/JavaScript từ LLM.

## 3. Forecast và Supplier Intelligence

Engine production đầu tiên gồm:

- Seasonal naive.
- Moving average 7/14/28 ngày.
- Single exponential smoothing với alpha grid.
- Additive Holt-Winters, season length 7.

Việc chọn model dùng tối thiểu bốn rolling folds, WAPE là metric chính và MAE là metric phụ. Seasonal naive luôn là candidate nên model kém baseline không được promote. Forecast point âm bị clamp về 0; khoảng 80% được tính từ empirical residual quantile.

Forecast sản phẩm được lưu theo `Store + Drink`. Việc chuyển product forecast sang ingredient demand chỉ được phép khi chứng minh đủ:

- BOM theo size đang hiệu lực.
- Lịch sử tỷ trọng size đủ ổn định.
- Unit conversion hợp lệ.
- Không có stock-out bias đáng kể.

Nếu thiếu một điều kiện, hệ thống trả quality/warning như `MISSING_BOM`, `INVALID_CONVERSION`, `STOCK_OUT_BIAS`, không tự chia forecast Drink cho recipe và không tạo quantity giả. Forecast chỉ là demand signal; quantity mua cuối vẫn thuộc Reorder Rule Phase 1.

Supplier score v1 dùng giá base-unit 30%, on-time 20%, fill rate 20%, quality 20% và lead time 10%. Package/conversion sai bị loại; MOQ/overbuy là constraint hoặc penalty. Dưới 5 receipt trả `INSUFFICIENT_DATA`; LLM không đổi score, ranking hoặc primary supplier.

## 4. Cấu hình lịch và cảnh báo thiếu người

- Màn hình giữ bốn cấu hình: availability, giới hạn giờ, định mức nhân sự và time-off.
- Worker rule-based kiểm tra định mức và lịch hiện có trong hai ngày tới.
- Danh sách ứng viên chỉ gồm nhân viên active, đúng Store/role, khả dụng, không nghỉ, không trùng ca và không vượt giới hạn giờ/nghỉ.
- Thông báo được persist, dedupe, nhắc lại theo cooldown, resolve khi ca đủ người và phát realtime qua SignalR.
- Chức năng tạo, giải thích và áp dụng phương án phân công đã được gỡ. Quản lý tiếp tục phân lịch thủ công trên màn hình lịch hiện có.

## 5. POS Recommendation

Materialization chỉ dùng order Completed, loại full-refund, áp ngưỡng basket/support/confidence/lift và confirmed non-negative margin. Control/treatment được chia ổn định bằng hash server-side. `RecommendationSessionId` ghi display/click/add/purchase idempotent mà không lưu PII.

POS chỉ hiển thị tối đa ba mục, không popup, không tự thêm món và bỏ qua lỗi recommendation. Khi phục vụ, hệ thống lọc lại menu đang active và tái sử dụng `IStoreMenuAvailabilityEvaluator`; recommendation chỉ được trả khi có ít nhất một size đang thực sự sellable. Menu active đơn thuần không được coi là bằng chứng đủ tồn kho.

## 6. Advanced Anomaly Detection

V1 dùng threshold/materiality, seasonal median và MAD/robust score cho:

- Revenue và order count.
- Waste/adjustment.
- Cash discrepancy.
- Supplier issue.
- Product-volume drop khi không có bằng chứng stock alert.

Kết quả chỉ là tín hiệu điều tra, không phải kết luận gian lận. Notification HIGH/CRITICAL dùng dedupe, resolve và StoreScope. Feedback acknowledge/dismiss/confirm được giữ để theo dõi false-positive rate.

## 7. AI skills và fallback

Các skill typed được whitelist:

- `dashboard-intent-parser`.
- `dashboard-insight-explanation`.
- `forecast-result-explanation`.
- `supplier-score-explanation`.
- `anomaly-explanation`.

Mỗi schema dùng `additionalProperties=false`. Kết quả explanation phải echo đúng ID, metric/score/model và các số liệu server đã tính. JSON lỗi, field lạ hoặc echo mismatch đều bị reject và trả deterministic fallback. Worker không gọi Ollama.

## 8. Feature flags

Tất cả mặc định OFF:

```text
DashboardIntelligence:IntentParserEnabled
DashboardIntelligence:ExplanationEnabled
Forecasting:RevenueEnabled
Forecasting:ProductEnabled
SupplierIntelligence:ScoringEnabled
StaffScheduleNotifications:Enabled
PosRecommendation:Enabled
AnomalyDetection:Enabled
```

Không có cờ AI tổng. Mỗi capability được pilot độc lập theo Store và có thể rollback độc lập.

## 9. Exit gate trước khi bật

1. Áp migration trên bản sao database và xác nhận backup/rollback.
2. Chạy shadow mode tối thiểu một chu kỳ dữ liệu, chưa gửi notification thật.
3. Kiểm tra chéo StoreScope bằng Owner, Area Manager và Store Manager.
4. Forecast đạt data-quality gate và không kém seasonal naive.
5. Cảnh báo thiếu lịch đúng StoreScope, không lặp thông báo và tự resolve khi ca đủ người.
6. POS pilot xác nhận cả menu và inventory availability, đồng thời có control group.
7. Anomaly đo false-positive/dismiss rate trước khi gửi rộng.
8. Ollama OFF/timeout/JSON sai không làm lỗi Dashboard, Forecast, Shift, POS hoặc Notification core.

## 10. Kết quả kiểm tra kỹ thuật

- Application build: thành công, 0 error.
- EF pending-model check: không còn thay đổi model chưa có migration.
- Migration Phase 2–4: đã áp dụng thành công lên database kiểm thử và xác nhận các bảng chính tồn tại.
- JavaScript syntax: Dashboard Intelligence, Operational Anomaly, Shift Optimization và POS đều qua `node --check`.
- Contract/AI/Staff/Phase 1 target tests: đạt.
- Non-SQL regression: 1.212/1.212 test đạt khi loại các fixture SQL Server và source test phụ thuộc script demo đã bị người dùng xóa.
- Full suite không thể kết luận đạt trong môi trường mặc định vì các SQL fixture không kết nối được SQL Server và một test vẫn tham chiếu script demo đã xóa; các file đó không được tự ý khôi phục.
