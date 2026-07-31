# Kịch bản thuyết trình AI CafeChain

Thời lượng mục tiêu: **6 phút 20 giây**. Nội dung dưới đây phân biệt rõ AI,
rule-based, fallback và prototype theo source ngày 30/07/2026.

## Slide 1 — Giới thiệu

**Nội dung hiển thị**

- CafeChain: quản trị chuỗi cà phê dựa trên dữ liệu.
- Bài toán: dữ liệu nhiều nhưng người quản lý cần kết luận nhanh.
- Mục tiêu AI: hỗ trợ đọc, giải thích và ra quyết định; không tự quyết định.

**Lời thuyết trình đề xuất**

“CafeChain có dữ liệu doanh thu, đơn hàng, kho và mua hàng nhưng người quản lý
không nên phải đọc từng bảng để tìm vấn đề. Đề tài dùng AI để hiểu câu hỏi và
giải thích evidence đã được backend kiểm tra. AI chỉ hỗ trợ; quyền phê duyệt và
quyết định cuối vẫn thuộc con người.”

**Thời lượng dự kiến:** 25 giây.

**Điểm cần nhấn mạnh:** AI không tự ghi dữ liệu và không thay thế nghiệp vụ.

## Slide 2 — Các chức năng AI chính

**Nội dung hiển thị**

- AI Dashboard: hỏi đáp theo dữ liệu và biểu đồ.
- Gợi ý nhập hàng: rule tính số lượng, AI giải thích.
- Fallback: vẫn có kết quả khi Ollama lỗi.
- Prototype: gợi ý master-data và ảnh Pexels/ComfyUI.

**Lời thuyết trình đề xuất**

“Hai luồng có thể dùng để demo là AI Dashboard và giải thích gợi ý nhập hàng.
Dashboard biến câu hỏi thành kế hoạch dữ liệu có whitelist. Với nhập hàng,
backend tính số lượng bằng công thức, Ollama chỉ viết lời giải thích. Dự án còn
có prototype gợi ý danh mục, đồ uống, size, topping và ảnh, nhưng entry point
đang ẩn nên em không trình bày chúng như tính năng đã sẵn sàng.”

**Thời lượng dự kiến:** 35 giây.

**Điểm cần nhấn mạnh:** không phóng đại prototype; số nhập hàng không do LLM tính.

## Slide 3 — Kiến trúc tổng thể

**Nội dung hiển thị**

```text
Dữ liệu nghiệp vụ
→ Permission + StaffScope
→ Application Service / rule
→ EvidencePack
→ AI Provider
→ Output validation
→ Kết quả hoặc fallback
→ Người dùng xác nhận
```

**Lời thuyết trình đề xuất**

“Yêu cầu không đi thẳng từ giao diện đến AI. Hệ thống kiểm account, permission
và StaffScope trước, rồi service lấy dữ liệu qua repository hoặc stored
procedure đã định nghĩa. AI chỉ nhận EvidencePack có cấu trúc. Output phải khớp
schema và số liệu nguồn; nếu sai thì bị loại và chuyển sang fallback.”

**Thời lượng dự kiến:** 35 giây.

**Điểm cần nhấn mạnh:** LLM không nhận quyền chạy SQL hoặc truy cập DbContext.

## Slide 4 — AI Dashboard

**Nội dung hiển thị**

- Hiểu `BusinessIntent` và `AnswerFocus`.
- Lập `DataPlan` trong whitelist.
- Lấy `EvidencePack` đúng StaffScope.
- Trả `AnalysisContext`, bảng và biểu đồ.
- Chặn số liệu không grounded; có deterministic fallback.

**Lời thuyết trình đề xuất**

“Người dùng có thể hỏi: ‘Top 10 sản phẩm bán chạy là gì?’ hoặc ‘Nguyên liệu nào
nên đặt lại trước?’. Backend nhận dạng BusinessIntent và AnswerFocus, sau đó tự
lập DataPlan: widget nào, kỳ nào, Store nào và cần metric gì. EvidencePack được
tạo từ dữ liệu thật. Kết quả gồm context, kết luận, bảng và biểu đồ. Nếu Ollama
trả số không có trong evidence hoặc JSON sai, hệ thống từ chối. Cấu hình hiện
tại tắt phần LLM explanation, vì vậy demo vẫn chạy bằng deterministic fallback
thay vì giả vờ AI đang khả dụng.”

**Thời lượng dự kiến:** 55 giây.

**Điểm cần nhấn mạnh:** evidence-first và trạng thái AI/fallback được công khai.

## Slide 5 — Gợi ý nhập hàng

**Nội dung hiển thị**

```text
ReorderPoint = AvgDailyConsumption × LeadTime + MinimumStock
RawDemand = max(0, ReorderPoint - Available - Incoming)
FinalQuantity = làm tròn theo package và minimum order
```

- Trừ phần đã được Restock/PA/PO bao phủ.
- AI chỉ giải thích lý do và rủi ro.
- Người dùng có `Restock.Create` mới được xác nhận.

**Lời thuyết trình đề xuất**

“Đây là điểm cần phân biệt rõ AI và rule. Backend lấy mức tiêu thụ trung bình,
lead time, ngưỡng tồn, tồn khả dụng và hàng đang về để tính điểm đặt hàng. Nhu
cầu còn lại được trừ phần procurement đang xử lý, rồi làm tròn theo quy cách
đóng gói và minimum order. Ollama không được đổi các con số này; nó chỉ giải
thích. Khi người dùng xác nhận, server tính lại, kiểm token, scope, trạng thái,
unit và RequestKey trước khi tạo hoặc bổ sung yêu cầu nhập.”

**Thời lượng dự kiến:** 55 giây.

**Điểm cần nhấn mạnh:** deterministic source of truth; transaction và idempotency.

## Slide 6 — AI nội dung và hình ảnh

**Nội dung hiển thị**

- Prototype có backend/JS/UI code nhưng entry point đang ẩn.
- Ollama gợi ý Drink/Topping → Visual Specification.
- Pexels: tìm và chấm metadata.
- ComfyUI: img2img/txt2img.
- Chưa vision validation; attribution chưa persist.

**Lời thuyết trình đề xuất**

“Dự án có pipeline prototype cho nội dung và ảnh. Ollama tạo gợi ý cùng Visual
Specification; Pexels trả ảnh tham chiếu được xếp hạng theo metadata; ComfyUI có
thể tạo biến thể. Tuy nhiên hệ thống chưa dùng vision model để kiểm tra ngữ
nghĩa ảnh, attribution Pexels chưa được lưu lâu dài và các entry point hiện bị
ẩn. Vì vậy đây là hướng thử nghiệm, không phải kết quả production.”

**Thời lượng dự kiến:** 30 giây.

**Điểm cần nhấn mạnh:** người dùng phải kiểm ảnh và bản quyền.

## Slide 7 — Bảo mật và độ tin cậy

**Nội dung hiển thị**

- Permission-first, account override `Deny`.
- StaffScope trước khi query; không tin `storeId` client.
- SystemAdmin global chỉ ở ReorderSuggestion, chỉ Store Active.
- Schema + evidence + file validation.
- Antiforgery, rate limit, RequestKey và fallback.

**Lời thuyết trình đề xuất**

“Quyền trả lời ai được gọi action; StaffScope trả lời được xem cửa hàng nào;
business rule trả lời trạng thái có cho phép xử lý không. StoreId từ URL, form
hay JSON đều phải kiểm lại. Quản trị hệ thống chỉ có global Active Store trong
module Gợi ý nhập hàng, không mặc định nhìn toàn bộ doanh thu, PO hay module
khác. Dashboard giới hạn prompt, schema và evidence; luồng tạo Restock có
antiforgery, transaction và deduplication để chống double-click.”

**Thời lượng dự kiến:** 45 giây.

**Điểm cần nhấn mạnh:** ẩn nút không thay thế bảo mật backend.

## Slide 8 — Kết quả đạt được

**Nội dung hiển thị**

- Rút ngắn thời gian đọc nhiều bảng dữ liệu.
- Kết luận gắn với evidence và biểu đồ.
- Ưu tiên nhập hàng nhất quán, có giải thích.
- Hoạt động được khi provider lỗi nhờ fallback.
- Con người giữ quyền xác nhận.

**Lời thuyết trình đề xuất**

“Giá trị chính không phải là để AI tự điều hành quán. Giá trị là biến dữ liệu
phân tán thành kết luận có thể kiểm chứng, giúp người quản lý phát hiện vấn đề
và xử lý nhanh hơn. Fallback giữ hệ thống dùng được khi AI offline, còn mọi
chứng từ vẫn cần permission và xác nhận của người dùng.”

**Thời lượng dự kiến:** 35 giây.

**Điểm cần nhấn mạnh:** khả năng kiểm chứng quan trọng hơn câu trả lời hoa mỹ.

## Slide 9 — Demo đề xuất

**Nội dung hiển thị**

1. Đăng nhập role có quyền.
2. Mở Dashboard, chọn Store và kỳ.
3. Hỏi một câu trong catalog.
4. Xem context, evidence, chart và trạng thái fallback.
5. Mở Gợi ý nhập hàng.
6. Xem công thức/lời giải thích.
7. Tạo yêu cầu nhập với `Restock.Create`.
8. Minh họa 403 khi sửa Store hoặc thiếu permission.

**Lời thuyết trình đề xuất**

“Em demo một luồng xuyên suốt: hỏi Dashboard về rủi ro kho, đối chiếu evidence
và biểu đồ, sau đó mở danh sách gợi ý nhập. Em chọn một nguyên liệu, xem số
lượng rule và lời giải thích, rồi xác nhận tạo yêu cầu nhập. Cuối cùng em sửa
Store hoặc dùng tài khoản thiếu quyền để chứng minh backend trả 403, không chỉ
ẩn menu.”

**Thời lượng dự kiến:** 45 giây.

**Điểm cần nhấn mạnh:** chuẩn bị trước dữ liệu Store, threshold, offer/package và
hai tài khoản có/không có quyền.

## Slide 10 — Hạn chế và hướng phát triển

**Nội dung hiển thị**

- Bật từng feature flag sau test/pilot.
- Ký Visual Specification, persist attribution.
- Thêm vision/moderation và monitoring provider.
- Đánh giá forecast bằng backtest.
- Nếu tối ưu lịch: xây solver riêng, LLM chỉ giải thích.

**Lời thuyết trình đề xuất**

“Hướng tiếp theo là harden bảo mật, quan sát provider và pilot theo từng cờ tính
năng. Các prototype ảnh cần ký specification, lưu attribution và thêm kiểm tra
ngữ nghĩa. Forecast cần backtest. Tối ưu lịch, nếu phát triển, phải là solver
ràng buộc riêng chứ không để LLM tự xếp ca.”

**Thời lượng dự kiến:** 20 giây.

**Điểm cần nhấn mạnh:** đây là roadmap, không phải claim đã hoàn thành.

## Tổng thời lượng

| Slide | Thời lượng |
| --- | ---: |
| 1 | 0:25 |
| 2 | 0:35 |
| 3 | 0:35 |
| 4 | 0:55 |
| 5 | 0:55 |
| 6 | 0:30 |
| 7 | 0:45 |
| 8 | 0:35 |
| 9 | 0:45 |
| 10 | 0:20 |
| **Tổng** | **6:20** |

## Câu hỏi phản biện và cách trả lời

### 1. Vì sao dùng AI mà không chỉ dùng stored procedure?

Stored procedure/repository vẫn là nguồn dữ liệu và phép tính chuẩn. AI chỉ tạo
giá trị ở phần hiểu câu hỏi tự nhiên và diễn giải evidence. Những phần có công
thức rõ ràng vẫn dùng SQL/C# rule vì dễ kiểm thử và audit hơn.

### 2. Phần nào là AI, phần nào là rule-based?

AI gồm parse/diễn giải bằng Ollama và prototype sinh nội dung/ảnh. Reorder
quantity, DataPlan whitelist, forecast SeasonalNaive/MovingAverage, supplier
weighted score, anomaly median/MAD, POS support/confidence/lift và validation là
rule/thống kê deterministic.

### 3. Làm sao chống AI bịa dữ liệu?

LLM chỉ nhận EvidencePack; output bị kiểm schema, field, echo, evidence ID,
widget coverage và số grounded. Sai contract hoặc có số không tồn tại thì bị
reject và hệ thống dùng deterministic fallback.

### 4. Nếu Ollama bị lỗi thì hệ thống hoạt động thế nào?

Dashboard và Reorder vẫn lấy facts và tính kết quả bằng backend. Chỉ phần câu
chữ chuyển sang fallback. Master-data trả candidate fallback hoặc báo provider
unavailable tùy contract. Không có chứng từ nào được tự tạo vì Ollama lỗi.

### 5. AI có truy cập toàn bộ database không?

Không. LLM không có DbContext hay SQL tool. Application service resolve
permission/StaffScope, chạy repository hoặc stored procedure đã whitelist rồi
mới serialize evidence tối thiểu cần thiết.

### 6. Làm sao bảo vệ dữ liệu giữa các chi nhánh?

Requested Store được giao với EffectiveStoreIds ở backend trước query. Owner và
Manager theo StaffScope. SystemAdmin chỉ có global Active Store trong
ReorderSuggestion; các module khác vẫn dùng default StaffScope. Store ngoài
scope trả 403 và được audit.

### 7. Vì sao cần biểu đồ cùng đoạn phân tích?

Đoạn phân tích giúp đọc nhanh; biểu đồ và bảng giúp kiểm chứng xu hướng/ranking.
ChartPlan tham chiếu cùng EvidencePack, nên người dùng có thể đối chiếu câu chữ
với con số nguồn thay vì tin AI mù quáng.

### 8. Gợi ý nhập hàng được tính như thế nào?

Hệ thống tính mức tiêu thụ trung bình, nhân lead time, cộng minimum stock, trừ
tồn khả dụng và hàng đang về, tiếp tục trừ nhu cầu đã được pipeline mua hàng bao
phủ, rồi làm tròn theo package và minimum order. Thiếu dữ liệu bắt buộc thì trả
DataIncomplete, không đoán.

### 9. SystemAdmin có được xem toàn bộ dữ liệu không?

Không mặc định. Yêu cầu đặc biệt chỉ cho SystemAdmin xem mọi Store Active trong
module Gợi ý nhập hàng và action liên quan đã chốt. Dashboard, doanh thu, PO,
phiếu kho, chuyển kho và module khác không tự nhận global scope.

### 10. Vì sao vẫn cần người dùng xác nhận kết quả AI?

AI có thể không hiểu đủ bối cảnh, provider có thể lỗi và dữ liệu có thể thiếu.
Ngoài ra nghiệp vụ còn có ngân sách, nhà cung cấp, separation of duties và trách
nhiệm phê duyệt. Vì vậy AI chỉ đề xuất/giải thích; người có quyền chịu trách
nhiệm xác nhận.

