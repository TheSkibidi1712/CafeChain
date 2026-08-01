# Kịch bản bảo vệ đồ án: AI trong CafeChain (5–7 phút)

## Slide 1 — Bài toán (30 giây)

CafeChain có nhiều dữ liệu bán hàng, tồn kho, mua hàng và nhà cung cấp. Người quản lý cần câu trả lời nhanh nhưng vẫn phải bảo vệ dữ liệu từng chi nhánh và không được để AI tự tạo chứng từ.

Thông điệp chính: AI trong dự án là lớp phân tích có kiểm chứng, không phải chatbot toàn quyền.

## Slide 2 — Giá trị nghiệp vụ (40 giây)

- Trả lời câu hỏi quản trị theo đúng kỳ và cửa hàng.
- Xếp hạng sản phẩm, phát hiện rủi ro kho/nhà cung cấp và nêu ưu tiên vận hành.
- Giải thích gợi ý nhập hàng nhưng vẫn yêu cầu người có quyền xác nhận.
- Duy trì nghiệp vụ khi Ollama không khả dụng nhờ fallback deterministic.

## Slide 3 — Kiến trúc (50 giây)

Trình bày luồng:

`Question → BusinessIntent/AnswerFocus → DataPlan → scoped query → EvidencePack → LLM validator/fallback → focused UI`

Nhấn mạnh ba ranh giới:

1. Controller/service kiểm permission và StaffScope.
2. Repository chỉ nhận StoreIds đã được server cho phép.
3. LLM chỉ nhận evidence đã chọn, không kết nối database và không sinh SQL.

## Slide 4 — Demo Dashboard (90 giây)

### Demo A: Top 10 sản phẩm

1. Chọn kỳ và cửa hàng.
2. Hỏi “Top 10 sản phẩm bán chạy nhất trong kỳ là gì?”.
3. Chỉ ra `AnswerFocus = TopSellingProducts` ở log/debug, không hiển thị mã này cho người dùng.
4. Kết quả có trả lời trực tiếp, tối đa 3 proof points, HorizontalBar và bảng cùng dataset.
5. Giải thích quy tắc `TotalSold DESC`, hòa thì `NetSales DESC`; không có khuyến nghị ngoài câu hỏi.

### Demo B: Bất thường vận hành

1. Hỏi “Có bất thường vận hành nào cần chú ý không?”.
2. Kết quả ưu tiên tối đa 3 rủi ro, hiển thị “ngày” thay cho `DAY`.
3. Action chỉ là bước kiểm tra; hệ thống không đoán nguyên nhân và không tự sửa dữ liệu.

## Slide 5 — Demo Gợi ý nhập hàng và bảo mật (60 giây)

- Người có `ReorderSuggestion.View` xem được danh sách.
- Nút xác nhận chỉ hiện khi có `Restock.Create`.
- BusinessOwner và StoreManager theo StaffScope.
- SystemAdmin chỉ xem tất cả cửa hàng active trong riêng module Reorder Suggestions; quyền global không lan sang Dashboard.
- Thử sửa StoreId ngoài scope: backend trả 403 và ghi audit.
- Khi xác nhận, server tính lại quantity, kiểm token/fingerprint, RequestKey, transaction và concurrency.

## Slide 6 — Grounding và fallback (45 giây)

LLM phải trả JSON gồm `directAnswer`, `proofPoints`, `actionToCheck`, `usedEvidenceIds`, `limitations`. Backend từ chối kết quả nếu:

- dùng EvidenceId không tồn tại;
- bịa tên hoặc số;
- chứa prompt injection/SQL;
- vượt quá 3 proof points;
- trả action cho câu hỏi không phải rủi ro/ưu tiên.

Khi bị từ chối hoặc timeout, một trong bảy fallback family tạo cùng layout. Người dùng vẫn thấy facts/chart backend, không thấy exception provider.

## Slide 7 — Các chức năng AI khác (40 giây)

- Gợi ý danh mục/đồ uống/size/topping với uniqueness policy.
- Pipeline ảnh Pexels/ComfyUI khi được cấu hình.
- Forecast, supplier intelligence, POS recommendation và operational anomaly.

Nói rõ trạng thái: forecast/supplier có API/backend; POS/anomaly có worker; mức độ nối UI không đồng đều. Không mô tả backend-only như một màn hình hoàn chỉnh.

## Slide 8 — Kiểm thử, giới hạn và hướng phát triển (45 giây)

Đã kiểm theo bốn lớp: RBAC/scope, transaction/idempotency, evidence/LLM guardrail, frontend stale-response. SeedAll phải chạy lặp không duplicate và không đổi account override.

Giới hạn:

- chưa có hội thoại đa lượt;
- không có SQL động;
- AI phụ thuộc chất lượng dữ liệu;
- AI không tự thực thi nghiệp vụ.

Hướng phát triển: tăng coverage integration trên SQL Server, đo chất lượng forecast/fallback theo thời gian, bổ sung UI có kiểm soát cho các backend AI đang ở trạng thái nền.

## Câu hỏi phản biện thường gặp

### AI có đọc toàn bộ database không?

Không. Service chọn widget và StoreIds trong scope, repository tạo dataset, sau đó LLM chỉ nhận EvidencePack giới hạn.

### Nếu AI bịa số thì sao?

Backend trích numeric claim và so với evidence theo tolerance; entity/EvidenceId cũng phải tồn tại. Sai thì bỏ response và dùng fallback.

### Vì sao cần biểu đồ nếu đã có câu trả lời?

Biểu đồ giúp kiểm tra quan hệ/xếp hạng nhanh. Chart và table dùng cùng rows nên không tạo hai nguồn sự thật.

### SystemAdmin có phải luôn global không?

Không. Mặc định mọi module theo Effective StaffScope. Ngoại lệ all-active-store chỉ dành cho Reorder Suggestions theo yêu cầu nghiệp vụ.

### AI có tự đặt hàng không?

Không. AI chỉ giải thích/gợi ý. Tạo RestockRequest cần `Restock.Create`, xác nhận người dùng, token, tính lại server-side, RequestKey và transaction.

### Vì sao dùng Ollama?

Ollama phù hợp triển khai nội bộ và structured output. Kiến trúc không phụ thuộc tuyệt đối vào provider vì mọi luồng quan trọng có validator và fallback.
