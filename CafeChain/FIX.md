PROMPT REFACTOR AISERVICE TÍCH HỢP OLLAMA CHO CAFÉCHAIN

Bạn hãy đóng vai là một Senior Developer chuyên ASP.NET Core MVC theo mô hình Layered Architecture.

Hãy phân tích và refactor dự án CafeChain để bổ sung một module AIService tích hợp Ollama chạy local nhằm hỗ trợ:

   Gợi ý dữ liệu cho các form Create.
   So sánh nhà cung cấp khi nhập kho.
   Giải thích kết quả phân tích doanh thu.
   Dự đoán doanh thu ngắn hạn bằng thống kê cơ bản.
   Cảnh báo các dữ liệu bất thường.
   Tạo nội dung mô tả hoặc giải thích dễ hiểu cho người dùng.

Ollama được chạy local tại:
http://localhost:11434

Model mặc định:
qwen3:4b

API sử dụng:
POST http://localhost:11434/api/chat

Lưu ý:
Ollama local không sử dụng API key.

1. Nguyên tắc thiết kế bắt buộc
Không được để Ollama tự tính toán hoặc tự quyết định các dữ liệu nghiệp vụ quan trọng.

Phải phân chia rõ:

   Repository
   → lấy dữ liệu chính xác từ database

   Service / Rule Engine
   → tính toán, so sánh, thống kê và chọn kết quả chính xác

   Ollama
   → diễn giải, tóm tắt, tạo mô tả và giải thích kết quả

   JavaScript
   → hiển thị hoặc điền dữ liệu gợi ý vào form

   Người dùng
   → kiểm tra và quyết định submit

Các phép tính sau phải được thực hiện bằng C#, không giao hoàn toàn cho Ollama:
   Giá nhập thực tế.
   Quy đổi đơn vị.
   Chiết khấu.
   Phí vận chuyển.
   MOQ.
   Tồn kho.
   Trung bình doanh thu.
   Phần trăm tăng giảm.
   Dự đoán doanh thu.
   Điểm xếp hạng nhà cung cấp.
   Kiểm tra điều kiện nghiệp vụ.
   Kiểm tra quyền.
   Validation DTO.
   Ghi dữ liệu vào database.

Ollama chỉ được dùng để:
   Giải thích kết quả.
   Tạo mô tả ngắn.
   Viết cảnh báo dễ hiểu.
   Tóm tắt dữ liệu đã được hệ thống tính toán.
   Gợi ý nội dung text.
   Phân tích ưu và nhược điểm dựa trên danh sách dữ liệu được cung cấp.
   Trả structured JSON đơn giản nếu cần.

Không cho Ollama:
   Tự truy cập database.
   Tự gọi Repository.
   Tự tạo hoặc xác nhận phiếu.
   Tự thay đổi tồn kho.
   Tự ghi dữ liệu.
   Tự submit form.
   Tự chọn dữ liệu không tồn tại trong danh sách.
   Tự bịa giá, số lượng hoặc nhà cung cấp.
   Trả về Entity để ghi trực tiếp vào database.

2. Quy tắc Layered Architecture hiện tại

Phải tuân thủ các quy tắc sau.

2.1. Controller

Controller chỉ được:
   Nhận request.
   Validate request cơ bản.
   Gọi Service.
   Trả View hoặc JSON.

Controller không được:
   Dùng AppDbContext.
   Dùng Repository trực tiếp.
   Gọi HttpClient trực tiếp tới Ollama.
   Viết thuật toán thống kê.
   Tính giá nhà cung cấp.
   Chứa logic nghiệp vụ dài.

Có thể dùng private method trong Controller để hỗ trợ chuẩn hóa response nếu thực sự cần thiết.

2.2. Service

Service:
   Không dùng AppDbContext.
   Chỉ dùng Repository, DTO, ViewModel và các service phụ trợ.
   Chịu trách nhiệm điều phối nghiệp vụ.
   Tính toán dữ liệu chính xác.
   Chuẩn bị context an toàn cho Ollama.
   Validate lại kết quả do Ollama trả về.
   Có fallback khi Ollama không hoạt động.

Service cần dùng private method để tách:
   Tính trung bình.
   Tính phần trăm thay đổi.
   Tính giá thực tế.
   Tính điểm nhà cung cấp.
   Xác định mức độ cảnh báo.
   Tạo prompt.
   Validate response từ Ollama.
   Tạo fallback response.

2.3. Repository

Repository chỉ chịu trách nhiệm:
   Truy vấn database.
   Lấy dữ liệu lịch sử.
   Lấy tồn kho.
   Lấy doanh thu.
   Lấy đơn hàng.
   Lấy phiếu bị hủy.
   Lấy thông tin nguyên liệu.
   Lấy thông tin nhà cung cấp.
   Lấy báo giá và lịch sử nhập.

Repository không được:
   Gọi Ollama.
   Tạo prompt.
   Phân tích bằng AI.
   Tính điểm lựa chọn nhà cung cấp.
   Tạo cảnh báo nghiệp vụ.
   SaveChangesAsync nhiều lần không cần thiết.

Nếu có thao tác ghi dữ liệu thì Repository phải expose riêng:
   BeginTransactionAsync
   SaveChangesAsync
   CommitAsync
   RollbackAsync
Service quyết định khi nào lưu và commit.


3. Kiến trúc AI đề xuất

Không thiết kế quá trừu tượng.
Chỉ cần kiến trúc đủ rõ ràng để có thể đổi model hoặc tắt Ollama khi cần.
Luồng đề xuất:
   Controller
      ↓
   IAIService
      ├── IAIRepository
      ├── các Repository nghiệp vụ hiện có
      ├── IOllamaClient
      └── Rule-based / Statistical Logic
Có thể sử dụng:
   IAIService
   IAIRepository
   IOllamaClient
Không cần tạo quá nhiều abstraction như:
   IAIEngine
   IAIOrchestrator
   IAIWorkflow
   IAIStrategy
   IAIPipeline
   IAIExecutor
   IAICommandBus
trừ khi dự án hiện tại đã có kiến trúc tương ứng.

4. Cấu hình Ollama
Bổ sung cấu hình sau vào appsettings.json:
   "AI": {
   "Enabled": true,
   "Provider": "Ollama",
   "UseFallbackWhenUnavailable": true
   },

   "Ollama": {
   "BaseUrl": "http://localhost:11434",
   "Model": "qwen3:4b",
   "TimeoutSeconds": 120,
   "KeepAlive": "5m",
   "Temperature": 0.2,
   "Think": false
   }
Không thêm ApiKey vì Ollama chạy local không cần API key.
Thiết kế OllamaOptions để map cấu hình.
Không hard-code:
   "http://localhost:11434"
   "qwen3:4b"
trong Service.

Phải đăng ký HttpClient bằng Dependency Injection.
Ví dụ hướng đăng ký:
builder.Services.Configure<OllamaOptions>(
    builder.Configuration.GetSection("Ollama"));
builder.Services.AddHttpClient<IOllamaClient, OllamaClient>();
Phải thiết lập timeout từ cấu hình.

5. Thiết kế OllamaClient

Thiết kế IOllamaClient và OllamaClient để gọi:

POST /api/chat

Request tối thiểu:

{
  "model": "qwen3:4b",
  "messages": [
    {
      "role": "system",
      "content": "..."
    },
    {
      "role": "user",
      "content": "..."
    }
  ],
  "stream": false,
  "think": false,
  "keep_alive": "5m",
  "options": {
    "temperature": 0.2
  }
}

Yêu cầu:

Dùng HttpClientFactory.
Không tạo mới HttpClient mỗi request.
Có CancellationToken.
Có timeout.
Xử lý khi Ollama không chạy.
Xử lý HTTP status khác thành công.
Xử lý response rỗng.
Xử lý JSON sai định dạng.
Ghi log nhưng không log dữ liệu nhạy cảm.
Không để lỗi Ollama làm hỏng toàn bộ form Create.
Khi Ollama lỗi, trả về fallback từ rule-based logic.

Thiết kế result rõ ràng, ví dụ:

public sealed class OllamaResultDTO
{
    public bool Success { get; set; }

    public string? Content { get; set; }

    public string? ErrorMessage { get; set; }

    public bool UsedFallback { get; set; }
}
6. Phạm vi chức năng của AIService
6.1. Gợi ý dữ liệu cho form tạo đồ uống

Dựa trên:

Name
Category
ProductType
Các size hiện có
Các topping hiện có
Quy tắc nghiệp vụ hiện tại

Hệ thống có thể gợi ý:

DrinkCode
Description
Size phù hợp
Topping phù hợp
Từ khóa mô tả

Phân chia trách nhiệm:

DrinkCode
→ C# tạo bằng rule xác định, không để Ollama tự tạo tùy ý.

Size và Topping
→ C# lọc từ dữ liệu có thật trong database.

Description
→ Ollama có thể tạo dựa trên dữ liệu đã lọc.

Không được để Ollama trả về SizeId hoặc ToppingId không tồn tại.

Sau khi nhận kết quả, Service phải validate ID với danh sách được phép.

6.2. Gợi ý dữ liệu cho form tạo nguyên liệu

Dựa trên:

Name
Category
Unit
Dữ liệu nguyên liệu cùng nhóm
Mức tiêu thụ lịch sử nếu có

Gợi ý:

IngredientCode
Mô tả.
Ngưỡng tồn kho tối thiểu.
Ngưỡng cảnh báo.
Ghi chú bảo quản.

Phân chia trách nhiệm:

IngredientCode
→ C# tạo.

Ngưỡng tồn kho tối thiểu
→ C# tính từ mức tiêu thụ trung bình và thời gian nhập hàng.

Mô tả, ghi chú bảo quản
→ Ollama diễn giải.

Unit và Category
→ Chỉ chọn trong dữ liệu có thật.
6.3. Gợi ý khi tạo phiếu nhập kho

Đây là chức năng trọng tâm.

Khi người dùng chọn:

Cửa hàng.
Nguyên liệu.
Số lượng dự kiến.
Ngày nhập.
Nhà cung cấp hiện tại nếu có.

Hệ thống phải:

Lấy danh sách nhà cung cấp đang cung cấp nguyên liệu.
Lấy báo giá hiện hành.
Lấy quy cách và đơn vị.
Quy đổi về cùng đơn vị cơ sở.
Lấy MOQ.
Lấy chiết khấu nếu có.
Lấy phí vận chuyển nếu có.
Lấy thời gian giao trung bình.
Lấy lịch sử nhập gần nhất.
Lấy tỷ lệ giao đúng hạn nếu có.
Lấy tỷ lệ giao thiếu hoặc hủy nếu có.
Tính giá thực tế bằng C#.
Xếp hạng nhà cung cấp bằng C#.
Đưa kết quả đã tính vào Ollama để tạo giải thích.
Trả kết quả cho JavaScript.
Người dùng bấm “Áp dụng gợi ý” thì mới điền input.

Công thức giá thực tế có thể dùng:

Giá thực tế mỗi đơn vị =
Giá niêm yết
- Chiết khấu mỗi đơn vị
+ Phí vận chuyển phân bổ mỗi đơn vị
+ Chi phí khác mỗi đơn vị

Nếu các nhà cung cấp dùng đơn vị khác nhau, phải quy đổi về cùng đơn vị chuẩn trước khi so sánh.

Kết quả cần có:

Nhà cung cấp đề xuất.
Nhà cung cấp hiện tại.
Đơn vị phù hợp.
Số lượng đề xuất.
Đơn giá tham khảo.
Giá thực tế.
MOQ.
Số tiền tiết kiệm.
Phần trăm tiết kiệm.
Thời gian giao hàng.
Mức độ rủi ro.
Danh sách so sánh.
Lý do đề xuất do Ollama diễn giải.
Cờ RequiresUserConfirmation = true.

Không được tự động:

Đổi nhà cung cấp.
Thêm nguyên liệu.
Thay số lượng.
Thay đơn giá.
Submit form.

JavaScript chỉ điền khi người dùng bấm:

Áp dụng gợi ý
7. Quy tắc một phiếu nhập và nhà cung cấp

Phải kiểm tra model hiện tại của dự án.

Nếu InventoryDocument chỉ có một SupplierId, phải giữ nguyên nguyên tắc:

Một phiếu nhập chỉ thuộc một nhà cung cấp.

Nếu nhiều nguyên liệu có nhà cung cấp tối ưu khác nhau, không tự ý đưa nhiều nhà cung cấp vào cùng phiếu.

Có thể áp dụng một trong hai cách:

Cách ưu tiên cho đồ án

Tính nhà cung cấp có thể cung cấp toàn bộ danh sách nguyên liệu với tổng chi phí hợp lý nhất.

Cách đơn giản theo form hiện tại

Người dùng chọn nhà cung cấp trước, hệ thống chỉ:

Phân tích giá của nhà cung cấp hiện tại.
So sánh với nhà cung cấp khác.
Cảnh báo nếu có lựa chọn tốt hơn.
Cho người dùng bấm “Chuyển sang nhà cung cấp đề xuất”.

Không tự động tách thành nhiều phiếu nếu chưa có yêu cầu nghiệp vụ rõ ràng.

8. Phân tích và cảnh báo doanh thu

Các rule thống kê phải viết bằng C#.

Ollama chỉ dùng để diễn giải kết quả.

8.1. Cảnh báo giảm doanh thu

Nếu:

Doanh thu hôm nay
<
Trung bình doanh thu 7 ngày gần nhất × 0.8

Trả về cảnh báo:

Doanh thu hôm nay đang giảm hơn 20% so với trung bình 7 ngày gần nhất.

Kết quả phải có thêm:

Doanh thu hôm nay.
Trung bình 7 ngày.
Số tiền chênh lệch.
Phần trăm thay đổi.
Mức cảnh báo.
8.2. Cảnh báo hủy đơn bất thường

Nếu:

Số đơn hủy hôm nay
>
Trung bình số đơn hủy 7 ngày gần nhất × 1.3

Trả về cảnh báo:

Số đơn hủy hôm nay tăng bất thường so với trung bình 7 ngày gần nhất.

Phải xử lý trường hợp trung bình bằng 0 để tránh chia cho 0.

8.3. Cảnh báo sản phẩm bán chạy giảm hiệu suất

Nếu:

Số lượng bán hôm nay
<
Trung bình số lượng bán trong tuần trước × 0.6

Trả về:

Sản phẩm bán chạy đang có dấu hiệu giảm hiệu suất bán hàng.

Chỉ phân tích sản phẩm có đủ dữ liệu lịch sử.

8.4. Cảnh báo tồn kho thấp

Nếu:

Tồn kho hiện tại < Ngưỡng tồn kho tối thiểu

Trả về:

Nguyên liệu đang dưới ngưỡng tồn kho tối thiểu, cần xem xét nhập hàng.

Có thể bổ sung:

Số ngày tồn kho còn lại =
Tồn kho hiện tại / Mức tiêu thụ trung bình ngày

Nếu mức tiêu thụ trung bình bằng 0, không được chia.

8.5. Cảnh báo hủy phiếu bất thường

Nếu:

Số phiếu kho bị hủy hôm nay
>
Trung bình số phiếu kho bị hủy 7 ngày gần nhất × 1.3

Trả về:

Số lượng phiếu kho bị hủy hôm nay đang tăng bất thường.
9. Dự đoán doanh thu ngắn hạn

Không dùng Ollama để tạo con số dự đoán.

Dùng C# và thống kê cơ bản.

Công thức:

Doanh thu dự đoán =
Doanh thu hôm qua × 0.5
+ Trung bình 3 ngày gần nhất × 0.3
+ Trung bình 7 ngày gần nhất × 0.2

Nếu chưa đủ dữ liệu:

Có 1–2 ngày: dùng trung bình số ngày hiện có.
Có 3–6 ngày: dùng trung bình 3 ngày và trung bình toàn bộ dữ liệu hiện có.
Có từ 7 ngày: dùng đầy đủ công thức.

Mức độ tin cậy:

Dưới 3 ngày dữ liệu:
Low

Từ 3 đến dưới 7 ngày:
Medium

Từ 7 ngày trở lên:
High

Kết quả cần gồm:

Ngày dự đoán.
Doanh thu dự đoán.
Số ngày dữ liệu đã dùng.
Công thức đã dùng.
Mức độ tin cậy.
Ghi chú.
IsFallback.
DataSufficient.

Ollama chỉ nhận kết quả sau khi tính để tạo phần giải thích như:

Doanh thu dự đoán trong 3 ngày tới tương đối ổn định.
Mức tin cậy ở mức High vì hệ thống có đủ dữ liệu 7 ngày gần nhất.
10. Prompt gửi cho Ollama

System prompt phải rõ ràng, không quá dài.

Ví dụ:

Bạn là trợ lý phân tích vận hành cho hệ thống CafeChain.

Bạn chỉ được sử dụng dữ liệu có trong JSON do hệ thống cung cấp.

Không được tự bịa:
- nhà cung cấp
- giá nhập
- số lượng
- doanh thu
- tỷ lệ
- mã định danh
- nguyên liệu
- sản phẩm

Các phép tính đã được hệ thống ASP.NET Core thực hiện.
Không tự tính lại hoặc thay đổi kết quả.

Nhiệm vụ của bạn:
- giải thích ngắn gọn kết quả
- nêu ưu và nhược điểm
- tạo cảnh báo dễ hiểu
- viết bằng tiếng Việt
- không đề xuất hành động ngoài danh sách cho phép
- không yêu cầu tự động lưu hoặc xác nhận dữ liệu

Nếu dữ liệu không đủ, hãy trả về thông báo không đủ dữ liệu.
Trả đúng JSON theo cấu trúc được yêu cầu.

Payload user nên là JSON đã rút gọn.

Không gửi:

Entity đầy đủ.
Navigation Property.
Password.
Token.
API key.
Email password.
Dữ liệu khách hàng không cần thiết.
Toàn bộ lịch sử database.
11. Structured response từ Ollama

Ưu tiên response đơn giản.

Ví dụ:

{
  "summary": "Nhà cung cấp A có giá thực tế thấp nhất.",
  "reason": "Giá thấp hơn 5% và thời gian giao hàng ổn định.",
  "riskLevel": "Low",
  "warnings": [],
  "recommendedAction": "ReviewAndApply"
}

Sau khi nhận response, Service phải validate:

recommendedAction chỉ thuộc danh sách cho phép.
riskLevel chỉ gồm Low, Medium, High.
Không dùng SupplierId ngoài danh sách.
Không dùng IngredientId ngoài danh sách.
Không dùng dữ liệu số khác với dữ liệu Service đã tính.
Nếu JSON sai thì dùng fallback.

Không dùng response Ollama để map trực tiếp vào Entity.

12. Fallback khi Ollama không hoạt động

Tính năng chính không được phụ thuộc hoàn toàn vào Ollama.

Nếu:

Ollama chưa mở.
Model chưa tải.
Request timeout.
API trả lỗi.
Response rỗng.
JSON không hợp lệ.
Model trả kết quả sai cấu trúc.

Thì Service vẫn phải trả:

Nhà cung cấp được C# chọn.
Giá đã tính.
Số lượng đề xuất.
Cảnh báo rule-based.
Dự đoán doanh thu.
Lý do fallback được tạo bằng template C#.

Ví dụ:

Nhà cung cấp A được đề xuất vì có giá thực tế thấp nhất trong danh sách.
Phần giải thích bằng Ollama hiện không khả dụng.

Kết quả phải có:

UsedOllama = false;
UsedFallback = true;

Không trả lỗi 500 chỉ vì Ollama không chạy, trừ khi endpoint được thiết kế chỉ để kiểm tra kết nối Ollama.

13. DTO/ViewModel cần thiết

Chỉ tạo DTO thực sự cần thiết.

Có thể cân nhắc:

AiSuggestionRequestDTO
AiSuggestionResultDTO

SupplierSuggestionRequestDTO
SupplierSuggestionResultDTO
SupplierComparisonDTO

RevenueAnalysisResultDTO
RevenueForecastDTO
AnomalyWarningDTO
InventoryWarningDTO

OllamaChatRequestDTO
OllamaChatResponseDTO
OllamaResultDTO

Không tạo nhiều DTO trùng ý nghĩa.

DTO phải:

Có validation.
Không chứa Entity navigation.
Không chứa secret.
Không chứa dữ liệu dư thừa.
Sử dụng decimal cho tiền và số lượng.
Có CancellationToken ở method async.
Có nullable hợp lý.
Có message rõ ràng khi dữ liệu thiếu.
14. Interface cần thiết

Thiết kế interface không quá lớn.

Ví dụ:

public interface IAIService
{
    Task<AiSuggestionResultDTO> SuggestInputAsync(
        AiSuggestionRequestDTO request,
        CancellationToken cancellationToken = default);

    Task<SupplierSuggestionResultDTO> SuggestSupplierAsync(
        SupplierSuggestionRequestDTO request,
        CancellationToken cancellationToken = default);

    Task<RevenueAnalysisResultDTO> AnalyzeRevenueAsync(
        int? storeId,
        DateTime analysisDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RevenueForecastDTO>> ForecastRevenueAsync(
        int? storeId,
        int days,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryWarningDTO>> GetInventoryWarningsAsync(
        int? storeId,
        CancellationToken cancellationToken = default);
}

Repository:

public interface IAIRepository
{
    Task<IReadOnlyList<DailyRevenueDTO>> GetDailyRevenueAsync(...);

    Task<IReadOnlyList<DailyCancelledOrderDTO>>
        GetCancelledOrderStatisticsAsync(...);

    Task<IReadOnlyList<ProductSalesStatisticDTO>>
        GetProductSalesStatisticsAsync(...);

    Task<IReadOnlyList<InventoryStatisticDTO>>
        GetInventoryStatisticsAsync(...);

    Task<IReadOnlyList<SupplierOfferDTO>>
        GetSupplierOffersAsync(...);

    Task<IReadOnlyList<InventoryDocumentStatisticDTO>>
        GetCancelledInventoryDocumentStatisticsAsync(...);
}

Nếu các Repository nghiệp vụ hiện tại đã có các method này, phải tái sử dụng thay vì tạo IAIRepository trùng lặp.

Trước khi quyết định tạo Repository mới, hãy phân tích các interface Repository hiện có.

15. API/Action đề xuất

Có thể thiết kế:

POST /Admin/AI/SuggestInput
POST /Admin/AI/SuggestSupplier
GET  /Admin/AI/RevenueAnalysis
GET  /Admin/AI/RevenueForecast
GET  /Admin/AI/InventoryWarnings
GET  /Admin/AI/Health

Health dùng để kiểm tra:

Ollama server có chạy không.
Model có sẵn không.
Không cần gọi model sinh nội dung dài.

Các action trả JSON nhất quán:

{
  "success": true,
  "message": "Phân tích thành công.",
  "data": {},
  "usedOllama": true,
  "usedFallback": false
}

Khi lỗi:

{
  "success": false,
  "message": "Không đủ dữ liệu để tạo gợi ý.",
  "data": null
}

Không trả stack trace ra client.

16. JavaScript trên form Create

Thiết kế JavaScript theo nguyên tắc:

Người dùng nhập một số field.
Người dùng bấm nút Gợi ý AI hoặc Phân tích nhà cung cấp.
JavaScript lấy dữ liệu hiện tại.
Gọi endpoint bằng fetch.
Hiển thị loading.
Nhận kết quả.
Hiển thị modal hoặc panel so sánh.
Người dùng bấm Áp dụng gợi ý.
JavaScript mới điền dữ liệu.
Không submit form.

Không gọi Ollama ở mỗi lần keyup.

Có thể dùng:

Nút bấm chủ động.
Hoặc debounce tối thiểu khoảng 800–1500 ms nếu thực sự cần.

Không ghi đè field đã có dữ liệu trừ khi:

Field đang trống.
Hoặc người dùng xác nhận ghi đè.

Phải xử lý:

Request trùng.
Người dùng bấm nhiều lần.
Abort request cũ khi request mới bắt đầu.
Loading state.
Disable nút trong lúc xử lý.
Toast thành công hoặc thất bại.
Anti-forgery token.
Timeout phía client nếu cần.
17. Trường hợp riêng của form phiếu nhập

Form phiếu nhập cần thêm nút:

Phân tích nhà cung cấp

Kết quả hiển thị:

Nhà cung cấp đề xuất
Giá niêm yết
Giá thực tế
MOQ
Thời gian giao
Số tiền tiết kiệm
Phần trăm tiết kiệm
Lý do đề xuất
Rủi ro

Các nút:

Áp dụng gợi ý
Xem so sánh
Bỏ qua

Chỉ khi bấm Áp dụng gợi ý mới điền:

SupplierId
UnitId
Quantity
UnitPrice

Không tự điền trước khi người dùng xác nhận.

Nếu thay đổi SupplierId làm JavaScript hiện tại reload danh sách nguyên liệu, phải giữ lại các dòng hiện tại hoặc yêu cầu xác nhận trước khi đổi.

Không để việc dispatch sự kiện change làm mất dữ liệu người dùng.

18. Logging và bảo mật

Không log:

Prompt có secret.
JWT.
Password.
API key.
Thông tin thanh toán.
Dữ liệu nhạy cảm không cần thiết.

Có thể log:

Thời gian request.
Tên model.
Loại chức năng.
Thành công hoặc thất bại.
Thời gian phản hồi.
Có dùng fallback hay không.
Kích thước prompt.
Mã lỗi đã chuẩn hóa.

Không lưu toàn bộ prompt và response nếu chứa dữ liệu kinh doanh nhạy cảm.

Ollama chạy local nên không cần API key.

Không tự ý thêm Ollama:ApiKey.

19. Các case cần test

Phải liệt kê và viết hướng test cho các case:

Ollama
Ollama đang chạy.
Ollama không chạy.
Model chưa tải.
Model trả response rỗng.
Model trả JSON sai.
Model timeout.
Model trả SupplierId không hợp lệ.
Model bịa dữ liệu.
Fallback hoạt động.
Nhà cung cấp
Một nguyên liệu có một nhà cung cấp.
Một nguyên liệu có nhiều nhà cung cấp.
Nhà cung cấp có đơn vị khác nhau.
MOQ không đạt.
Có chiết khấu.
Không có chiết khấu.
Có phí vận chuyển.
Không có phí vận chuyển.
Không có báo giá hiện hành.
Hai nhà cung cấp có cùng giá.
Nhà cung cấp rẻ nhưng giao chậm.
Nhà cung cấp đắt hơn nhưng ổn định hơn.
Một phiếu có nhiều nguyên liệu.
Không nhà cung cấp nào cung cấp đủ toàn bộ danh sách.
Doanh thu
Không có dữ liệu.
Có dưới 3 ngày.
Có từ 3 đến 6 ngày.
Có đủ 7 ngày.
Trung bình bằng 0.
Doanh thu giảm đúng 20%.
Doanh thu giảm trên 20%.
Số đơn hủy tăng đúng 30%.
Số đơn hủy tăng trên 30%.
Form
Field đang trống.
Field đã có dữ liệu.
Người dùng đồng ý ghi đè.
Người dùng từ chối áp dụng.
Người dùng bấm nhiều lần.
Request cũ bị hủy.
Không tự submit.
Không tự xác nhận phiếu.
20. Yêu cầu trước khi bắt đầu code

Không tự ý bịa tên Entity, field, Repository, Controller hoặc View.

Trước tiên phải yêu cầu tôi gửi các file liên quan.

Tối thiểu cần kiểm tra:

Program.cs
appsettings.json

InventoryDocument
InventoryDocumentDetail

Supplier
Ingredient
IngredientSupllier
Unit
UnitConvenrsion

AdminInventoryDocument Service
AdminInventoryDocument Repository
AdminInventoryDocument Controller

DTO tạo phiếu nhập
View Create phiếu nhập
JavaScript Create phiếu nhập

Order
OrderDetail
OrderStatus

Các Repository doanh thu hiện có
Các Service báo cáo hiện có

Nếu cần thêm table hoặc field, phải giải thích:

Thiếu dữ liệu nào.
Vì sao cần.
Field hoặc table dùng cho nghiệp vụ gì.
Có thể tái sử dụng bảng hiện tại hay không.
Có bắt buộc migration hay không.

Chỉ bắt đầu refactor khi đã đủ file cần thiết.

21. Thứ tự trả lời bắt buộc

Hãy trả lời theo thứ tự:

Phân tích phạm vi nào nên dùng rule-based.
Phân tích phạm vi nào nên dùng Ollama.
Chỉ ra phần nào tuyệt đối không giao cho Ollama.
Đánh giá các file hiện tại tôi đã cung cấp.
Liệt kê chính xác file cần sửa.
Liệt kê file thực sự cần tạo.
Thiết kế cấu hình Ollama.
Thiết kế DTO.
Thiết kế IOllamaClient.
Thiết kế IAIService.
Thiết kế Repository hoặc tái sử dụng Repository hiện có.
Viết code Ollama client.
Viết code AIService.
Viết code rule-based.
Viết code Controller.
Viết code JavaScript.
Viết fallback.
Viết validation.
Giải thích luồng hoạt động.
Liệt kê test case.
Liệt kê toàn bộ method và file đã thay đổi.
22. Các yêu cầu cuối cùng
Không dùng machine learning framework.
Không fine-tune model trong phạm vi hiện tại.
Không dùng thư viện AI nặng.
Không để Ollama tự tính tiền hoặc số lượng.
Không để Ollama truy cập database.
Không để Ollama tự ghi dữ liệu.
Không để Ollama tự submit hoặc xác nhận phiếu.
Không hard-code BaseUrl và model.
Không tạo quá nhiều abstraction.
Không tạo file không cần thiết.
Không sửa nghiệp vụ ngoài phạm vi yêu cầu.
Không thay đổi Entity khi chưa có lý do rõ ràng.
Không trả code giả nếu đã có đủ file.
Code phải chạy được với ASP.NET Core MVC.
Dùng async/await đầy đủ.
Truyền CancellationToken.
Có xử lý timeout.
Có fallback khi Ollama không hoạt động.
Ưu tiên code rõ ràng, phù hợp đồ án tốt nghiệp.
Mọi gợi ý chỉ được áp dụng sau khi người dùng xác nhận.