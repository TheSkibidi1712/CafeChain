# PROMPT REFACTOR HỆ THỐNG AI, DASHBOARD, ÂM KHO VÀ APP LAUNCHER

Hãy đóng vai là một **Senior Software Engineer, Solution Architect và AI Engineer có 20 năm kinh nghiệm**, chuyên sâu về:

* ASP.NET Core MVC.
* Layered Architecture.
* Entity Framework Core.
* SQL Server và Stored Procedure.
* Ollama và Large Language Model.
* Prompt Engineering.
* Retrieval-Augmented Generation.
* ComfyUI.
* Pexels API.
* Dashboard Analytics.
* Quản lý kho theo nhiều đơn vị tính.
* Thiết kế hệ thống phân quyền.

Hãy đọc toàn bộ source code, model, DTO, ViewModel, Controller, Service, Repository, JavaScript, View, configuration và file `FIX.md` hiện có trong dự án trước khi chỉnh sửa.

Không được suy đoán cấu trúc dữ liệu khi chưa kiểm tra source code thực tế.

---

# I. NGUYÊN TẮC BẮT BUỘC

## 1. Kiến trúc hệ thống

Phải tuân thủ luồng:

```text
Controller
    ↓
Service
    ↓
Repository
    ↓
Database
```

Yêu cầu cụ thể:

* Controller không được sử dụng trực tiếp `AppDbContext`.
* Controller không được gọi Repository trực tiếp.
* Controller chỉ gọi Service và sử dụng private method để hỗ trợ xử lý View.
* Service không được sử dụng trực tiếp `AppDbContext`.
* Service chỉ sử dụng Repository, DTO, ViewModel, Mapper và các abstraction cần thiết.
* Repository chịu trách nhiệm truy vấn dữ liệu.
* Transaction và `SaveChangesAsync` phải được Service kiểm soát hợp lý.
* Không gọi `SaveChangesAsync` nhiều lần không cần thiết trong một nghiệp vụ.
* Không đưa business logic vào Controller, View hoặc JavaScript.
* JavaScript chỉ xử lý UI, validation phía client và gọi API.
* Mọi dữ liệu tài chính, tồn kho và phân quyền phải được kiểm tra lại ở phía server.

## 2. Nguyên tắc sửa source code

* Không tự ý tạo file mới nếu chưa thực sự cần thiết.
* Ưu tiên refactor các file hiện có.
* Nếu bắt buộc phải tạo file mới, phải giải thích rõ:

  * Tên file.
  * Vị trí.
  * Trách nhiệm của file.
  * Lý do không thể đặt logic trong file hiện có.
* Không xóa nghiệp vụ đang hoạt động nếu không thuộc phạm vi yêu cầu.
* Không hard-code ID, role, đơn vị hoặc trạng thái nghiệp vụ.
* Tái sử dụng enum, constant, configuration và helper hiện có.
* Không làm thay đổi dữ liệu lịch sử ngoài phạm vi migration cần thiết.
* Phải kiểm tra backward compatibility đối với dữ liệu cũ.

## 3. Phạm vi thực hiện

Chia yêu cầu thành bốn phase:

```text
Phase 1: Refactor AI Service, Ollama, Skill.md, Pexels và ComfyUI.
Phase 2: Thiết kế Stored Procedure và dữ liệu Dashboard.
Phase 3: Refactor System Settings, âm kho, đơn vị tính, đơn giá và tồn kho.
Phase 4: Thiết kế App Launcher sau đăng nhập.
```

Trong lần thực hiện này:

* Phải triển khai hoàn chỉnh Phase 1.
* Phải triển khai hoàn chỉnh Phase 2.
* Phải triển khai hoàn chỉnh Phase 3.
* Phase 4 chỉ được phân tích, thiết kế kiến trúc và ghi kế hoạch vào `FIX.md`.
* Không được sửa Login, RedirectByRole hoặc các View đăng nhập trong Phase 4 ở lần này.

Mỗi phase phải độc lập, có thể kiểm thử và commit riêng.

---

# PHASE 1 — REFACTOR AI SERVICE, OLLAMA, SKILL.MD, PEXELS VÀ COMFYUI

## 1. Mục tiêu

Refactor lại toàn bộ luồng AI để giải quyết các vấn đề:

* AI thường xuyên đưa ra các gợi ý đã tồn tại.
* Các gợi ý thiếu sáng tạo và lặp lại.
* AI không bám sát ý tưởng người dùng nhập vào.
* Khi người dùng nhập ý tưởng sau lần gợi ý đầu tiên, AI không sử dụng ý tưởng mới để tạo gợi ý tiếp theo.
* AI thường tạo lại nội dung gần giống lần trước.
* Prompt gửi sang ComfyUI quá ngắn và thiếu chi tiết.
* Ảnh tạo ra có background, góc chụp, ánh sáng và bố cục lặp lại.
* Khi dùng ảnh từ Pexels làm ảnh tham chiếu, ảnh ComfyUI tạo lại gần như không thay đổi.
* Pexels có thể trả về ảnh không đúng đối tượng.
* Thiếu cơ chế đánh giá độ phù hợp giữa ảnh và gợi ý.

## 2. Làm rõ cơ chế “Ollama học theo Skill.md”

Không được hiểu “học” là Ollama tự động fine-tune hoặc tự cập nhật trọng số mô hình sau mỗi request.

Phải thiết kế theo hướng:

```text
Skill.md
   ↓
Skill Loader
   ↓
Skill Parser / Skill Cache
   ↓
System Prompt hoặc Context Injection
   ↓
Ollama Request
```

Có thể sử dụng một trong các cơ chế:

* System prompt cố định.
* Context injection.
* Prompt template.
* Retrieval-Augmented Generation.
* Semantic retrieval nếu `Skill.md` quá dài.
* Cache nội dung Skill để tránh đọc file lại ở mọi request.

Chỉ sử dụng fine-tuning hoặc LoRA nếu dự án thực sự có pipeline huấn luyện riêng. Không được gọi việc gửi `Skill.md` vào prompt là “train model”.

## 3. Yêu cầu đối với Skill.md

AiService phải:

* Đọc nội dung `Skill.md`.
* Kiểm tra file có tồn tại hay không.
* Kiểm tra file rỗng.
* Cache nội dung file.
* Có cơ chế reload cache khi file thay đổi hoặc hết thời gian cache.
* Chèn các quy tắc cần thiết vào system prompt.
* Không gửi toàn bộ nội dung dư thừa vào Ollama nếu file quá dài.
* Ưu tiên lấy đúng section skill liên quan đến loại form đang xử lý.
* Ghi log khi không đọc được Skill.
* Có fallback prompt an toàn nếu Skill không khả dụng.

Phân loại skill theo context, ví dụ:

```text
Drink
Drink Category
Size
Topping
Ingredient
Supplier
Inventory
Store
Promotion
Staff
Dashboard
Image Generation
```

Mỗi request phải xác định đúng loại entity/form trước khi lấy skill tương ứng.

## 4. Luồng tạo gợi ý AI

Thiết kế luồng:

```text
Dữ liệu người dùng nhập
    ↓
Dữ liệu hiện có trong form
    ↓
Danh sách dữ liệu đã tồn tại trong database
    ↓
Lịch sử gợi ý gần nhất trong phiên hiện tại
    ↓
Skill phù hợp từ Skill.md
    ↓
Prompt Builder
    ↓
Ollama
    ↓
Validate JSON
    ↓
Loại bỏ trùng lặp
    ↓
Chấm điểm sáng tạo và độ liên quan
    ↓
Trả về 1–3 gợi ý
```

## 5. Ưu tiên ý tưởng của người dùng

Prompt phải phân biệt rõ:

* `UserIdea`: ý tưởng người dùng chủ động nhập.
* `ExistingFormData`: dữ liệu đang có trên form.
* `ExistingEntities`: dữ liệu đã tồn tại trong database.
* `PreviousSuggestions`: các gợi ý đã tạo trước đó.
* `GenerationMode`: tạo mới, phát triển ý tưởng hoặc tạo biến thể.

Thứ tự ưu tiên:

```text
1. Ý tưởng mới nhất của người dùng.
2. Các ràng buộc nghiệp vụ của form.
3. Skill.md.
4. Dữ liệu hiện có trên form.
5. Tránh trùng dữ liệu database.
6. Tránh lặp lại các gợi ý trước.
```

Khi `UserIdea` có giá trị:

* Phải lấy ý tưởng đó làm chủ đề trung tâm.
* Không được bỏ qua ý tưởng.
* Không được trả lại gợi ý chung chung không liên quan.
* Phải tạo các biến thể sáng tạo dựa trên ý tưởng đó.
* Có thể mở rộng nhưng không được làm sai bản chất ý tưởng.

Khi `UserIdea` trống:

* AI được phép tự sáng tạo.
* Phải lấy dữ liệu hiện có trong hệ thống để tránh trùng.
* Phải tạo gợi ý mới thay vì sao chép dữ liệu đã có.

## 6. Chống gợi ý trùng lặp

Phải kiểm tra:

* Trùng tên chính xác.
* Trùng tên sau khi chuẩn hóa.
* Trùng mã.
* Tên gần giống.
* Cùng nguyên liệu chính.
* Cùng mô tả.
* Cùng profile màu sắc.
* Cùng concept ảnh.
* Cùng prompt ảnh.
* Trùng với các gợi ý vừa được tạo trong phiên.

Nên chuẩn hóa trước khi so sánh:

```text
Trim
Lowercase
Bỏ dấu tiếng Việt khi cần so sánh
Chuẩn hóa khoảng trắng
Loại bỏ ký tự đặc biệt không cần thiết
```

Có thể dùng similarity score, nhưng ngưỡng phải được cấu hình thay vì hard-code rải rác.

Khi một gợi ý bị trùng:

* Không trả thẳng về client.
* Thử tạo lại gợi ý thay thế với số lần retry giới hạn.
* Không được retry vô hạn.
* Nếu vẫn không đủ số lượng, trả về số lượng hợp lệ hiện có cùng cảnh báo rõ ràng.

## 7. Tăng mức độ sáng tạo

Prompt phải yêu cầu Ollama đa dạng hóa:

* Hương vị.
* Nguyên liệu.
* Màu sắc.
* Phong cách.
* Nhóm khách hàng.
* Mùa.
* Dịp sử dụng.
* Cách trang trí.
* Cảm giác thương hiệu.
* Mức giá hoặc phân khúc.
* Concept chụp ảnh.

Không được để cả ba option chỉ khác nhau ở tên.

Ba option nên có định hướng khác nhau, ví dụ:

```text
Option 1: An toàn, dễ bán, phù hợp số đông.
Option 2: Sáng tạo, có điểm nhấn thị giác.
Option 3: Premium hoặc theo xu hướng.
```

Thiết lập hợp lý các tham số của Ollama như:

* Temperature.
* TopP.
* TopK.
* RepeatPenalty.
* Seed nếu API hỗ trợ.
* Context length.
* Output token limit.

Không hard-code tất cả request cùng một seed vì có thể làm kết quả lặp lại.

## 8. Structured Output

Ollama phải trả về JSON theo schema cụ thể.

Ví dụ:

```json
{
  "suggestions": [
    {
      "name": "",
      "code": "",
      "description": "",
      "reasoningSummary": "",
      "theme": "",
      "primaryColor": "",
      "secondaryColor": "",
      "ingredients": [],
      "tags": [],
      "imageConcept": {
        "subject": "",
        "background": "",
        "composition": "",
        "cameraAngle": "",
        "lighting": "",
        "colorPalette": "",
        "style": "",
        "mood": "",
        "props": [],
        "negativePrompt": ""
      }
    }
  ]
}
```

Yêu cầu:

* Không parse JSON bằng cách cắt chuỗi tùy tiện.
* Phải loại bỏ markdown code fence nếu model vẫn trả về.
* Validate schema.
* Validate số lượng option.
* Validate field bắt buộc.
* Có fallback khi JSON không hợp lệ.
* Ghi log response lỗi nhưng không ghi dữ liệu nhạy cảm.

## 9. Refactor ComfyUI Prompt Builder

Không để Ollama chỉ trả về một câu prompt ảnh ngắn.

Prompt ảnh phải được xây dựng theo cấu trúc:

```text
Subject
Product details
Container or packaging
Main ingredients
Garnish
Background
Surface
Composition
Camera angle
Lens style
Depth of field
Lighting
Color palette
Mood
Visual style
Brand feeling
Image quality
Commercial photography requirements
Uniqueness constraints
```

Ví dụ các nhóm thông tin:

* Chủ thể chính.
* Loại đồ uống hoặc sản phẩm.
* Màu sắc chính và phụ.
* Ly, cốc, chai hoặc bao bì.
* Topping và garnish.
* Background.
* Mặt bàn.
* Bố cục.
* Góc máy.
* Ánh sáng.
* Độ sâu trường ảnh.
* Chất liệu.
* Phong cách.
* Mood.
* Mức độ chuyên nghiệp.
* Commercial product photography.
* High-detail.
* Photorealistic nếu phù hợp.
* Không chứa text hoặc watermark nếu không được yêu cầu.

Phải có `NegativePrompt`, ví dụ:

```text
blurry, low resolution, deformed glass, duplicate garnish,
multiple drinks when one is requested, incorrect ingredients,
text, watermark, logo, distorted straw, unnatural liquid,
oversaturated, underexposed, cluttered background
```

Negative prompt phải được tùy biến theo từng entity, không sử dụng một chuỗi cố định cho tất cả trường hợp.

## 10. Đa dạng hóa ảnh ComfyUI

Phải tránh việc mọi ảnh đều có cùng:

* Background.
* Góc chụp.
* Ly.
* Ánh sáng.
* Bố cục.
* Tông màu.
* Props.
* Seed.

Thiết kế các style profile có kiểm soát, ví dụ:

```text
Minimal Studio
Luxury Dark
Tropical Fresh
Japanese Cafe
Vietnamese Modern
Pastel Lifestyle
Natural Ingredient
Commercial Menu
Premium Advertising
Outdoor Summer
```

Style profile phải được chọn dựa trên:

* Ý tưởng người dùng.
* Màu sắc sản phẩm.
* Nhóm sản phẩm.
* Concept thương hiệu.
* Các ảnh đã tạo gần đây.

Không chọn ngẫu nhiên hoàn toàn nếu làm sai concept.

## 11. Luồng Pexels

Luồng đề xuất:

```text
AI Suggestion
    ↓
Search Query Builder
    ↓
Tạo nhiều truy vấn tiếng Anh
    ↓
Pexels Search
    ↓
Lọc metadata
    ↓
Chấm điểm ảnh
    ↓
Chọn ảnh phù hợp nhất
    ↓
Nếu không đủ điểm thì dùng ComfyUI text-to-image
    ↓
Nếu đủ điểm thì cho phép dùng làm reference cho img2img
```

Không dùng trực tiếp tên tiếng Việt làm query duy nhất.

Phải tạo các query tiếng Anh từ:

* Tên sản phẩm.
* Loại sản phẩm.
* Màu sắc.
* Nguyên liệu chính.
* Phong cách.
* Kiểu chụp.
* Background.
* Container.

Ví dụ:

```text
mango yogurt drink commercial product photography
purple taro milk tea studio background
strawberry smoothie glass cafe menu photography
```

Phải loại bỏ các từ khóa không nên dùng cho tìm kiếm ảnh như:

* Tên riêng quá đặc thù.
* Mã nội bộ.
* Mô tả dài.
* Câu quảng cáo.
* Các từ không thể hiện hình ảnh.

## 12. Chấm điểm ảnh Pexels

Không chọn ảnh đầu tiên API trả về.

Thiết kế scoring dựa trên các tín hiệu có thể kiểm tra được:

* Mức độ phù hợp của query.
* Orientation.
* Kích thước ảnh.
* Tỷ lệ ảnh.
* Màu chủ đạo nếu metadata hỗ trợ.
* Alt text hoặc photographer description nếu có.
* Loại chủ thể.
* Số lượng đối tượng trong ảnh.
* Yêu cầu portrait, landscape hoặc square.
* Tránh ảnh có người nếu form yêu cầu ảnh sản phẩm.
* Tránh ảnh quá rộng khi cần ảnh menu.

Nếu Pexels không cung cấp đủ semantic metadata thì phải thừa nhận giới hạn này. Không được tuyên bố hệ thống “nhìn hiểu ảnh” nếu chưa có vision model.

Có thể mở rộng bằng vision model sau này, nhưng không tự ý thêm dependency ngoài phạm vi dự án.

## 13. Pexels kết hợp ComfyUI

Khi dùng ảnh Pexels làm reference:

* Không được chỉ tải ảnh rồi trả lại gần như nguyên bản.
* Phải xác định mục đích dùng reference:

  * Giữ bố cục.
  * Giữ màu.
  * Giữ hình dáng.
  * Chỉ tham khảo phong cách.
* Phải cấu hình mức `denoise strength` phù hợp.
* Nếu denoise quá thấp, ảnh gần như không thay đổi.
* Nếu quá cao, ảnh mất hoàn toàn reference.
* Giá trị phải lấy từ configuration hoặc style profile.
* Không hard-code cùng một giá trị cho mọi workflow.
* Có thể thay đổi:

  * Background.
  * Props.
  * Ánh sáng.
  * Góc máy.
  * Màu sắc.
  * Garnish.
  * Phong cách thương mại.
* Không thay đổi sai loại sản phẩm hoặc nguyên liệu chính.

Phải kiểm tra workflow ComfyUI hiện có để xác định:

* Checkpoint.
* Sampler.
* Scheduler.
* Steps.
* CFG.
* Seed.
* Denoise.
* ControlNet/IPAdapter nếu dự án đang sử dụng.
* Image dimensions.
* Input/output node.

Không được sửa node ID bằng cách đoán.

## 14. Configuration của Phase 1

Các giá trị nên đưa vào configuration:

```text
OllamaModel
OllamaTemperature
OllamaTopP
OllamaTopK
OllamaRepeatPenalty
OllamaTimeoutSeconds
OllamaMaxRetries
SkillFilePath
SkillCacheMinutes
SuggestionCount
SuggestionSimilarityThreshold
PexelsMinImageScore
PexelsSearchLimit
ComfyUiDefaultWidth
ComfyUiDefaultHeight
ComfyUiDenoiseStrength
ComfyUiSteps
ComfyUiCfg
```

Phải validate configuration khi startup hoặc khi service khởi tạo.

## 15. Kết quả bắt buộc của Phase 1

Sau khi hoàn thành phải cung cấp:

1. Phân tích nguyên nhân khiến gợi ý và ảnh bị lặp.
2. Sơ đồ luồng AI mới.
3. Danh sách file đã sửa.
4. Nội dung code đầy đủ của các method hoặc file đã sửa.
5. Prompt template mới.
6. JSON schema mới.
7. ComfyUI prompt builder.
8. Negative prompt builder.
9. Pexels query builder.
10. Logic chống trùng.
11. Cơ chế Skill loader và cache.
12. Cấu hình cần thêm.
13. Test case.
14. Acceptance criteria.
15. Nội dung cập nhật tương ứng trong `FIX.md`.

---

# PHASE 2 — STORED PROCEDURE VÀ DASHBOARD ANALYTICS

## 1. Mục tiêu

Dựa hoàn toàn vào model và schema thực tế của dự án để thiết kế các Stored Procedure phục vụ dashboard.

Không được viết Stored Procedure dựa trên tên bảng giả định.

Trước khi thiết kế phải:

1. Đọc toàn bộ model liên quan.
2. Đọc `DbSet`.
3. Đọc Entity Configuration.
4. Đọc migration hoặc schema SQL nếu có.
5. Xác định khóa chính, khóa ngoại.
6. Xác định enum và status hợp lệ.
7. Xác định cách tính doanh số, giá vốn, hoàn tiền, hủy đơn và thanh toán.
8. Xác định dữ liệu thuộc cửa hàng nào.
9. Xác định timezone đang sử dụng.
10. Xác định dữ liệu soft-delete hoặc inactive.

Nếu một biểu đồ chưa đủ dữ liệu nguồn:

* Không được tự tạo cột giả.
* Ghi rõ `NOT_SUPPORTED_BY_CURRENT_SCHEMA`.
* Chỉ ra model hoặc field còn thiếu.
* Đề xuất bổ sung ở phần riêng.
* Không đưa đề xuất đó vào Stored Procedure hiện tại khi chưa được xác nhận.

## 2. Quy tắc chung cho Stored Procedure

Mỗi Stored Procedure phải:

* Có tên thống nhất.
* Có comment mô tả.
* Có parameter rõ ràng.
* Hỗ trợ filter theo phạm vi quyền người dùng khi phù hợp.
* Hỗ trợ `StoreId` hoặc store scope.
* Hỗ trợ khoảng thời gian.
* Hỗ trợ timezone nếu dữ liệu lưu UTC.
* Không sử dụng `SELECT *`.
* Không dùng dynamic SQL nếu không cần thiết.
* Tránh cursor.
* Tránh scalar function trong truy vấn lớn nếu gây giảm hiệu năng.
* Dùng CTE, temporary table hoặc window function hợp lý.
* Có xử lý chia cho 0.
* Có xử lý dữ liệu null.
* Có status dữ liệu thiếu.
* Không làm tròn quá sớm.
* Không dùng giá hiện tại để tính ngược dữ liệu lịch sử.
* Có đề xuất index phù hợp.
* Có ví dụ `EXEC`.
* Có result schema.
* Có test case biên.
* Có lưu ý về hiệu năng.

Nên sử dụng prefix thống nhất, ví dụ:

```text
dbo.usp_Dashboard_...
dbo.usp_Operations_...
dbo.usp_Inventory_...
dbo.usp_Procurement_...
dbo.usp_ProductAnalytics_...
dbo.usp_Workforce_...
```

Tên cuối cùng phải phù hợp convention đang có trong dự án.

## 3. Phân quyền và phạm vi dữ liệu

Stored Procedure không được mặc định trả dữ liệu toàn chuỗi cho mọi người dùng.

Phải kiểm tra cơ chế scope hiện tại:

* Toàn hệ thống.
* Khu vực.
* Cửa hàng.
* Nhân viên.
* Ca làm việc.

Nếu dự án xử lý scope tại Service:

* Stored Procedure nhận danh sách StoreId hoặc filter đã được Service xác thực.
* Không tin trực tiếp StoreId do client gửi lên.
* Service phải kiểm tra quyền trước khi gọi Repository.

## 4. Granularity theo khoảng thời gian

Đối với dữ liệu theo thời gian:

```text
Dưới 31 ngày: theo ngày.
Từ 31 đến 180 ngày: theo tuần.
Trên 180 ngày: theo tháng.
```

Phải đảm bảo:

* Kỳ trước có cùng độ dài kỳ hiện tại.
* Khoảng thời gian không bị overlap.
* Xử lý đúng ngày đầu và ngày cuối.
* Có thể trả về các mốc không có dữ liệu với giá trị 0 nếu dashboard cần đường biểu đồ liên tục.
* Định nghĩa rõ tuần bắt đầu từ ngày nào theo business rule hiện tại.

## 5. Nhóm Stored Procedure Dashboard điều hành

### 5.1 Đường doanh số theo thời gian

Loại biểu đồ: line chart.

Series:

* Doanh số thuần kỳ hiện tại.
* Doanh số thuần kỳ trước cùng độ dài.

Phải xác định chính xác:

```text
NetSales =
    CompletedSales
    - ValidRefunds
    - Discounts
    - CancelledOrVoidedAmountsExcluded
```

Công thức cuối cùng phải bám theo model thực tế.

Result cần có tối thiểu:

```text
PeriodKey
PeriodLabel
CurrentNetSales
PreviousNetSales
CurrentOrderCount
PreviousOrderCount
Granularity
```

Hỗ trợ drill-down:

* Ngày hoặc period.
* Danh sách đơn.
* Cửa hàng.

### 5.2 Xếp hạng cửa hàng

Cho phép chọn metric:

```text
NET_SALES
ORDER_COUNT
AVERAGE_ORDER_VALUE
CONFIRMED_GROSS_PROFIT
COST_COVERAGE_RATE
```

Không chỉ xếp hạng theo doanh thu.

Result nên có:

```text
StoreId
StoreCode
StoreName
NetSales
OrderCount
AverageOrderValue
ConfirmedGrossProfit
CostCoverageRate
SelectedMetric
SelectedMetricValue
Rank
DataStatus
```

Chỉ tính gross profit từ dữ liệu giá vốn đã xác nhận.

### 5.3 Cơ cấu phương thức thanh toán

Nguồn:

```text
Payments
PaymentMethods
PaymentStatuses
```

Hiển thị:

* Số tiền.
* Tỷ trọng.
* Số giao dịch.

Result:

```text
PaymentMethodId
PaymentMethodCode
PaymentMethodName
TransactionCount
TotalAmount
Percentage
```

Phải xác định rõ các status được tính là thanh toán thành công.

### 5.4 Heatmap đơn hàng theo thứ và giờ

Trục X:

```text
HourOfDay: 0–23
```

Trục Y:

```text
DayOfWeek
```

Cho phép metric:

```text
ORDER_COUNT
NET_SALES
```

Result:

```text
DayOfWeekNumber
DayOfWeekLabel
HourOfDay
OrderCount
NetSales
SelectedMetricValue
```

Phải xử lý timezone của cửa hàng.

### 5.5 Bảng cảnh báo điều hành

Thứ tự ưu tiên:

1. Ca két có chênh lệch lớn.
2. Ca `RequiresReconciliation = true`.
3. Late offline sync.
4. Tồn âm.
5. Stock alert `URGENT` đang mở.
6. PO quá hạn.
7. Supplier issue đang `OPEN` hoặc `UNDER_REVIEW`.

Result nên được chuẩn hóa:

```text
AlertType
Severity
Priority
ReferenceId
ReferenceCode
StoreId
StoreName
Title
Description
DetectedAt
Status
DrillDownUrlOrKey
```

Không đưa vào dashboard:

* Danh sách hàng trăm nguyên liệu.
* Toàn bộ lịch sử chấm công.
* Form xử lý phiếu kho.
* BOM chi tiết.

Các nội dung này chỉ drill-down sang module chuyên biệt.

## 6. Nhóm Stored Procedure WorkShift và POS

### 6.1 Chênh lệch két theo WorkShift

Loại: diverging bar.

Result:

```text
WorkShiftId
StoreId
StoreName
CashierId
CashierName
OpenedAt
ClosedAt
ExpectedCash
ActualCash
CashDiscrepancy
AbsoluteDiscrepancy
Threshold
RequiresOtp
RequiresReconciliation
```

Dương là dư, âm là thiếu.

Ngưỡng phải lấy từ configuration hiện hành, không hard-code.

### 6.2 Doanh số và số đơn theo ca

Loại: combo bar và line.

Result:

```text
WorkShiftId
ShiftLabel
StoreId
CashierId
NetSales
OrderCount
AverageOrderValue
```

Có drill-down theo WorkShift.

### 6.3 Cơ cấu thanh toán theo ca

Loại: stacked bar.

Result:

```text
WorkShiftId
PaymentMethodCode
PaymentMethodName
TransactionCount
TotalAmount
PercentageWithinShift
```

### 6.4 Ngoại lệ offline và đối soát

Result:

```text
ShiftId
StoreId
StoreName
CashierId
CashierName
ClosedAt
OfflineOrderCountAtClose
OfflineEstimatedTotalAtClose
LateOfflineSyncCount
CashDiscrepancy
ReconciliationStatus
RequiresReconciliation
```

### 6.5 Đơn theo giờ trong ngày

Có filter:

* Cửa hàng.
* Ngày.
* Khoảng thời gian.

Result:

```text
BusinessDate
HourOfDay
OrderCount
NetSales
```

Phải có đủ 24 mốc giờ nếu UI cần phát hiện khoảng POS không phát sinh giao dịch.

### 6.6 Thống kê WorkShift không nhất thiết là biểu đồ

Thiết kế dataset cho:

* Top ca có chênh lệch lớn nhất.
* Tỷ lệ đóng ca đúng quy trình.
* Tỷ lệ ca cần supervisor hoặc OTP.
* Số terminal đang hoạt động.
* Số terminal có ca mở.

Chỉ group `DiscrepancyReason` khi có reason code chuẩn hóa. Không group trực tiếp text tự do.

## 7. Nhóm Stored Procedure tồn kho

### 7.1 Top rủi ro thiếu hàng

Công thức:

```text
DaysOfCover = UsableQty / AverageDailyUsage
```

Chỉ tính khi:

```text
AverageDailyUsage > 0
```

Nếu không đủ dữ liệu:

```text
DataStatus = INSUFFICIENT_HISTORY
DaysOfCover = NULL
```

Không được gán `DaysOfCover = 0`.

Result:

```text
StoreId
IngredientId
IngredientCode
IngredientName
BaseUnit
UsableQty
AverageDailyUsage
DaysOfCover
MinStockLevel
RiskLevel
DataStatus
```

### 7.2 Biến động kho theo loại giao dịch

Series riêng:

* Import hoặc branch receipt.
* Sales deduction.
* Production in.
* Production out.
* Waste.
* Adjustment.
* Transfer in.
* Transfer out.
* Sales return.

Không gộp `WASTE` với `SALES_DEDUCTION`.

Result:

```text
PeriodKey
TransactionType
BaseQuantity
InventoryValue
```

### 7.3 Tồn hiện tại so với ngưỡng

Chỉ trả top 10–20 mặt hàng có rủi ro cao nhất.

Result:

```text
IngredientId
IngredientName
UsableQty
MinStockLevel
ReorderPoint
ShortageQuantity
RiskScore
DataStatus
```

### 7.4 Đề xuất nhập hàng

Công thức:

```text
ReorderPoint =
    AverageDailyUsage × LeadTimeDays
    + MinStockLevel

SuggestedQty =
    MAX(
        0,
        ReorderPoint
        - AvailableQty
        - IncomingApprovedPoQuantity
    )
```

Result:

```text
IngredientId
IngredientName
AvailableQty
AverageDailyUsage
LeadTimeDays
IncomingApprovedPoQuantity
MinStockLevel
ReorderPoint
SuggestedBaseQuantity
SuggestedPackageQuantity
MOQ
SupplierId
SupplierName
EstimatedCost
DataStatus
```

Phải xử lý conversion giữa base unit và package unit.

### 7.5 Hao hụt theo nguyên liệu và cửa hàng

Cho phép metric:

```text
BASE_QUANTITY
WASTE_VALUE
```

Khi so toàn chuỗi, ưu tiên giá trị tiền.

Khi xem một nguyên liệu cụ thể, có thể dùng base quantity.

Result:

```text
StoreId
IngredientId
IngredientName
BaseUnit
WasteBaseQuantity
WasteValue
WasteTransactionCount
```

### 7.6 Tuổi lớp giá FIFO

Phân nhóm:

```text
0–7 ngày
8–30 ngày
31–60 ngày
Trên 60 ngày
```

Nguồn:

```text
InventoryCostLayers.CreatedAt
InventoryCostLayers.RemainingQuantity
```

Result:

```text
AgeBucket
LayerCount
RemainingQuantity
RemainingValue
```

Đây chỉ là chỉ báo tồn lâu.

Không được gọi là hết hạn nếu hệ thống không lưu expiry date hoặc lot expiry.

## 8. Nhóm Stored Procedure mua hàng và nhà cung cấp

### 8.1 Pipeline đơn mua hàng

Trạng thái:

* Draft.
* Approved.
* Sent.
* Partially received.
* Completed.
* Cancelled.

Hiển thị:

```text
Status
PurchaseOrderCount
TotalOrderValue
RemainingUnreceivedValue
```

### 8.2 PO quá hạn theo nhà cung cấp

Result:

```text
SupplierId
SupplierName
OverduePoCount
OutstandingValue
MaximumDelayDays
AverageDelayDays
```

### 8.3 So sánh chất lượng nhà cung cấp

Metric:

* On-time rate.
* Fill rate.
* Rejection rate.
* Issue rate.
* Average delay days.

Status:

```text
GOOD
WATCH
RISK
INSUFFICIENT_DATA
```

Không dùng radar chart.

### 8.4 Biến động giá mua

Nguồn:

```text
IngredientSupplierPriceHistories
```

Chuẩn hóa:

```text
NormalizedUnitCost =
    PackagePrice / PackageBaseQuantity
```

Không so sánh trực tiếp package price khi package quantity hoặc unit khác nhau.

Result:

```text
EffectiveDate
IngredientId
SupplierId
PackagePrice
PackageBaseQuantity
NormalizedUnitCost
BaseUnit
```

### 8.5 Cơ cấu chi tiêu mua hàng

Group theo:

* Nhà cung cấp.
* Nhóm nguyên liệu.

Phải dùng giá snapshot tại thời điểm đặt hoặc nhận.

Không dùng `CurrentPrice` để tính ngược lịch sử.

### 8.6 Loại sự cố nhà cung cấp

Group theo `IssueType`:

* Late.
* Short.
* Wrong item.
* Damaged.
* Expired.
* Quality failure.
* Packaging failure.
* Document mismatch.
* Other.

Chỉ sử dụng issue không bị dismissed khi tính chất lượng.

## 9. Nhóm Stored Procedure sản phẩm và lợi nhuận

### 9.1 Top sản phẩm

Hai chế độ:

```text
TOP_BY_QUANTITY
TOP_BY_CONFIRMED_GROSS_PROFIT
```

Result nên ở cấp drink-size:

```text
DrinkId
DrinkName
SizeId
SizeName
QuantitySold
NetSales
ConfirmedCogs
ConfirmedGrossProfit
GrossMarginPercent
CostCoverageRate
DataStatus
```

### 9.2 Ma trận sản lượng và biên lợi nhuận

Scatter plot:

```text
X = QuantitySold
Y = GrossMarginPercent
Point = DrinkSize
OptionalSize = NetSales
```

Chỉ đưa SKU có cost complete vào scatter.

SKU thiếu cost phải nằm trong cảnh báo riêng.

### 9.3 Doanh số và margin theo size

Metric:

* Giá bán bình quân.
* Giá vốn đơn vị đã xác nhận.
* Lợi nhuận gộp đơn vị.

Dựa trên module `DrinkSizeProfitability` hiện có nếu module này tồn tại trong source code.

### 9.4 Top topping

Hiển thị:

* Số lượt gắn topping.
* Topping attachment rate.
* Doanh số topping.
* Giá vốn topping khi complete.

Công thức attachment rate phải có mẫu số rõ ràng, ví dụ số đơn hoặc số sản phẩm đủ điều kiện gắn topping theo model thực tế.

Không chỉ trả `TotalUsed`.

### 9.5 Sức khỏe BOM và dữ liệu giá vốn

Status:

* COMPLETE.
* MISSING_RECIPE.
* MISSING_CONVERSION.
* MISSING_COST_LAYER.
* INSUFFICIENT_COST_QUANTITY.
* INVALID_BOM.
* MISSING_DEFAULT_TOPPING_POLICY.

Result:

```text
Status
SkuCount
Percentage
```

### 9.6 Sản phẩm tiêu hao cao nhưng hiệu quả thấp

Dùng bảng ưu tiên.

Các cột:

```text
DrinkSizeId
ProductName
QuantitySold
NetSales
ConfirmedCogs
GrossProfit
GrossMarginPercent
IngredientConsumptionValue
CostCoverageRate
DataStatus
```

Không sử dụng AI để tính các chỉ số.

AI chỉ được dùng giải thích sau khi dữ liệu đã đầy đủ.

## 10. Nhóm Stored Procedure nhân sự

### 10.1 Trạng thái ca nhân sự

Stacked bar theo ngày/cửa hàng:

* Planned.
* Checked in.
* Completed.
* Absent.

### 10.2 Nhu cầu bán hàng và nhân sự theo giờ

Combo chart:

* Số nhân viên đang trong ca.
* Số đơn theo giờ.
* Doanh số theo giờ nếu cần.

Result:

```text
BusinessDate
HourOfDay
ActiveStaffCount
OrderCount
NetSales
OrdersPerStaff
```

### 10.3 Hiệu suất theo nhân viên

Không xếp hạng chỉ bằng doanh thu tuyệt đối.

Hiển thị:

```text
StaffId
StaffName
RoleId
RoleName
StoreId
WorkShiftCount
OrderCount
NetSales
WorkedHours
OrdersPerHour
SalesPerHour
CompletedShiftRate
DataCompletenessStatus
```

Chỉ so sánh trong cùng vai trò và cùng loại cửa hàng.

Không kết luận hiệu suất cá nhân khi dữ liệu ca hoặc giờ công thiếu.

### 10.4 Chất lượng chấm công

Metric:

* Face verified rate.
* Invalid check-in rate.
* Check-in ngoài store/network nhưng hợp lệ nếu log có phân loại.
* Top cửa hàng có nhiều lỗi chấm công.

Không suy đoán loại lỗi nếu model không lưu.

## 11. Mapping Stored Procedure sang ứng dụng

Ngoài script SQL, phải thiết kế:

```text
DashboardController
DashboardService
DashboardRepository
Dashboard DTO/ViewModel
Filter DTO
Chart Response DTO
```

Tuy nhiên phải ưu tiên tái sử dụng các file/module dashboard hiện có.

Controller chỉ:

* Nhận filter.
* Validate ModelState.
* Gọi Service.
* Trả View hoặc JSON.

Service:

* Kiểm tra quyền.
* Resolve store scope.
* Chuẩn hóa ngày.
* Chọn granularity.
* Gọi Repository.
* Map kết quả.

Repository:

* Gọi Stored Procedure.
* Truyền parameter an toàn.
* Không ghép SQL từ input.
* Không chứa logic phân quyền.

## 12. Kết quả bắt buộc của Phase 2

Sau khi hoàn thành phải cung cấp:

1. Mapping từng biểu đồ với model và bảng thực tế.
2. Danh sách biểu đồ đủ dữ liệu.
3. Danh sách biểu đồ thiếu dữ liệu.
4. Danh sách Stored Procedure.
5. Script SQL hoàn chỉnh.
6. Result schema cho từng procedure.
7. Parameter cho từng procedure.
8. DTO hoặc ViewModel tương ứng.
9. Repository method.
10. Service method.
11. Controller endpoint.
12. Index đề xuất.
13. Test case.
14. Ví dụ `EXEC`.
15. Nội dung cập nhật trong `FIX.md`.

---

# PHASE 3 — SYSTEM SETTINGS, ÂM KHO, ĐƠN VỊ, ĐƠN GIÁ VÀ HIỂN THỊ TỒN

## 1. Mục tiêu

Refactor lại phần cài đặt hệ thống và nghiệp vụ kho để:

* Chỉ giữ lại tab cấu hình âm kho.
* Xóa hoặc ẩn các tab cài đặt khác khỏi giao diện theo yêu cầu.
* Sửa lỗi ngưỡng âm kho theo từng loại đơn vị.
* Sửa lỗi `1000 ml` nhưng hệ thống chỉ cho âm `1 ml`.
* Sửa lỗi chuyển từ `g` sang `kg` bị mất đơn giá.
* Sửa lỗi các đơn vị khác cũng bị mất đơn giá khi đổi đơn vị.
* Hiển thị tồn kho theo đơn vị người dùng đang chọn.
* Không hiển thị `99999 g` khi người dùng đang chọn `kg`.
* Đảm bảo mọi phép tính tồn kho được chuẩn hóa về base unit.

## 2. Refactor System Settings

Giao diện cài đặt chỉ giữ:

```text
Cấu hình âm kho
```

Các tab khác:

* Không hiển thị trên giao diện.
* Không được xóa database hoặc code nền nếu có module khác đang sử dụng.
* Chỉ xóa hoàn toàn khi xác minh chắc chắn không có dependency.
* Route/API cũ phải được kiểm tra để tránh truy cập trái phép bằng URL trực tiếp.

Phải kiểm tra quyền truy cập phần cấu hình âm kho.

Không được chỉ ẩn tab bằng CSS mà vẫn cho phép người không có quyền truy cập endpoint.

## 3. Quy tắc chuẩn hóa đơn vị

Toàn bộ nghiệp vụ kho phải sử dụng:

```text
BaseQuantity
BaseUnit
ConversionFactor
```

Nguyên tắc:

```text
SelectedQuantity × ConversionFactor = BaseQuantity
```

Ví dụ:

```text
1 kg = 1000 g
1 l = 1000 ml
```

Nếu base unit là gram:

```text
1 kg → 1000 g
```

Nếu base unit là millilitre:

```text
1 l → 1000 ml
```

Cần xác định chính xác ý nghĩa của `ConversionFactor` trong model hiện tại:

* Số base unit trên một selected unit.
* Hay số selected unit trên một base unit.

Không được tự đảo công thức nếu chưa đọc model và dữ liệu seed.

## 4. Sửa lỗi ngưỡng âm kho g và ml

Hiện trạng:

* Cấu hình âm kho `1000 g` hoạt động gần đúng.
* Cấu hình âm kho `1000 ml` nhưng khi xuất chỉ cho âm khoảng `1 ml`.

Đây là dấu hiệu conversion factor đang:

* Bị chia hai lần.
* Bị nhân sai chiều.
* Nhầm giữa litre và millilitre.
* Đang coi `1000 ml` là `1 base unit`.
* Hoặc lưu threshold theo selected unit nhưng so sánh với base unit.

Phải truy vết toàn bộ luồng:

```text
System Setting Input
    ↓
DTO
    ↓
Controller
    ↓
Service
    ↓
Repository
    ↓
Database Value
    ↓
Inventory Validation
    ↓
Stock Confirmation
```

Phải xác định ngưỡng được lưu theo đơn vị nào.

Thiết kế thống nhất:

```text
Tất cả ngưỡng âm kho lưu dưới dạng BaseQuantity.
```

UI có thể nhập theo đơn vị người dùng chọn, nhưng Service phải chuyển về base unit trước khi lưu.

Khi đọc lên:

```text
StoredBaseThreshold / ConversionFactor
    = DisplayThresholdInSelectedUnit
```

Không được so sánh:

```text
SelectedQuantity
```

trực tiếp với:

```text
BaseThreshold
```

Phải đưa cả hai về cùng đơn vị trước khi so sánh.

## 5. Kiểm tra âm kho

Định nghĩa:

```text
ProjectedBaseStock =
    CurrentBaseStock
    + BaseIncomingQuantity
    - BaseOutgoingQuantity
```

Nếu:

```text
ProjectedBaseStock >= 0
```

thì hợp lệ.

Nếu:

```text
ProjectedBaseStock < 0
```

thì:

```text
NegativeBaseQuantity = ABS(ProjectedBaseStock)
```

Kiểm tra:

```text
NegativeBaseQuantity <= AllowedNegativeBaseThreshold
```

Ngoài ra vẫn phải kiểm tra:

* Người dùng có quyền tạo âm kho.
* Có cần OTP hoặc supervisor approval không.
* Loại chứng từ có cho phép âm kho không.
* Cửa hàng có bật cấu hình âm kho không.
* Nguyên liệu có cấu hình riêng hay dùng cấu hình mặc định.
* Config riêng của nguyên liệu phải ưu tiên hơn config mặc định.
* Trạng thái chứng từ có hợp lệ không.

Không được chỉ validation ở JavaScript.

## 6. Cấu hình ngưỡng mặc định và ngưỡng riêng

Thứ tự resolve:

```text
1. Ngưỡng riêng của nguyên liệu tại cửa hàng.
2. Ngưỡng riêng của nguyên liệu toàn hệ thống nếu model hỗ trợ.
3. Ngưỡng mặc định theo nhóm đơn vị hoặc loại nguyên liệu.
4. Ngưỡng mặc định toàn hệ thống.
5. Không cho âm kho nếu không tìm thấy cấu hình hợp lệ.
```

Thứ tự cuối cùng phải dựa trên model thực tế.

Mỗi ngưỡng phải có:

```text
Value
StoredUnit
BaseValue
Source
EffectiveConfigurationId
```

Nếu database hiện chỉ lưu một con số, phải chuẩn hóa ý nghĩa cột đó và migration dữ liệu cũ một cách an toàn.

## 7. Refactor đơn giá khi đổi đơn vị

Hiện trạng:

* Chọn đơn vị `g` thì đơn giá là `22đ`.
* Chuyển sang `kg` thì đơn giá mất.
* Các đơn vị khác cũng gặp lỗi tương tự.

Phải xác định loại giá hiện tại:

```text
PricePerBaseUnit
PricePerSelectedUnit
PackagePrice
SnapshotUnitPrice
```

Không được dùng chung một field mà không xác định rõ ý nghĩa.

Nếu đơn giá lưu theo base unit:

```text
SelectedUnitPrice =
    BaseUnitPrice × ConversionFactor
```

Ví dụ:

```text
22 đ/g
1 kg = 1000 g
PricePerKg = 22 × 1000 = 22.000 đ/kg
```

Nếu đơn giá lưu theo package hoặc selected unit thì phải chuyển ngược đúng công thức.

Phải refactor:

* API trả dữ liệu đơn vị.
* DTO.
* ViewModel.
* JavaScript khi đổi select đơn vị.
* Server-side calculation.
* Snapshot khi lưu chứng từ.
* Export hoặc detail view nếu dùng cùng field.

Không chỉ sửa hiển thị bằng JavaScript vì giá lưu vào chứng từ có thể sai.

## 8. Hiển thị tồn theo đơn vị đang chọn

Tồn kho vẫn lưu bằng base unit.

Khi người dùng chọn đơn vị:

```text
DisplayStock =
    BaseStock / ConversionFactor
```

Ví dụ:

```text
99999 g / 1000 = 99.999 kg
```

UI phải hiển thị:

```text
99.999 kg
```

thay vì:

```text
99999 g
```

Khi đổi từ `kg` sang `g`:

```text
99999 g
```

Các dữ liệu cần đồng bộ theo selected unit:

* Tồn hiện tại.
* Tồn khả dụng.
* Tồn dự kiến sau giao dịch.
* Ngưỡng âm kho.
* Số lượng âm dự kiến.
* Đơn giá.
* Thành tiền.
* Min stock.
* Reorder point nếu đang hiển thị cùng form.

## 9. Quy tắc precision và rounding

Không được dùng `double` cho tiền hoặc số lượng kho nếu model đang hỗ trợ `decimal`.

Phải xác định precision phù hợp:

```text
Quantity: decimal
Money: decimal
ConversionFactor: decimal
```

Không làm tròn ở giữa quá trình tính.

Chỉ format khi hiển thị.

Cần xác định số chữ số thập phân theo unit, ví dụ:

```text
g: có thể 0–3 chữ số tùy nghiệp vụ.
kg: có thể 0–3 chữ số.
ml: có thể 0–3 chữ số.
l: có thể 0–3 chữ số.
piece: thường là số nguyên, trừ khi business rule cho phép lẻ.
```

Không hard-code quy tắc này nếu model Unit đã có precision.

## 10. API dữ liệu đơn vị

Khi người dùng chọn nguyên liệu hoặc đơn vị, API nên trả một object rõ ràng:

```json
{
  "ingredientId": 0,
  "selectedUnitId": 0,
  "selectedUnitCode": "KG",
  "baseUnitId": 0,
  "baseUnitCode": "G",
  "conversionFactor": 1000,
  "baseStockQuantity": 99999,
  "displayStockQuantity": 99.999,
  "baseUnitPrice": 22,
  "selectedUnitPrice": 22000,
  "allowedNegativeBaseQuantity": 1000,
  "allowedNegativeDisplayQuantity": 1
}
```

Tên field cuối cùng phải khớp convention DTO trong dự án.

## 11. Transaction và concurrency

Khi xác nhận phiếu xuất:

* Phải đọc tồn kho mới nhất trong transaction.
* Không tin giá trị tồn kho client gửi lên.
* Tính lại conversion ở server.
* Kiểm tra lại âm kho.
* Kiểm tra lại quyền.
* Lưu transaction/snapshot theo base quantity.
* Lưu unit và conversion snapshot nếu model hỗ trợ.
* Tránh race condition khi hai phiếu cùng xuất một nguyên liệu.
* Không để double-click xác nhận tạo giao dịch trùng.
* Tái sử dụng cơ chế RequestKey hoặc RequestDeduplication nếu dự án đã có.

## 12. Các trường hợp kiểm thử bắt buộc

### Trường hợp 1

```text
Base unit: g
Tồn hiện tại: 500 g
Ngưỡng âm: 1000 g
Xuất: 1 kg
```

Kết quả:

```text
Projected stock = -500 g
Cho phép nếu người dùng đủ quyền.
```

### Trường hợp 2

```text
Base unit: ml
Tồn hiện tại: 500 ml
Ngưỡng âm: 1000 ml
Xuất: 1 l
```

Kết quả:

```text
Projected stock = -500 ml
Cho phép nếu người dùng đủ quyền.
```

### Trường hợp 3

```text
Base unit: ml
Tồn hiện tại: 0 ml
Ngưỡng âm: 1000 ml
Xuất: 1.001 l
```

Kết quả:

```text
Projected stock = -1001 ml
Không cho phép.
```

### Trường hợp 4

```text
Đơn giá: 22 đ/g
Đơn vị chọn: kg
```

Kết quả:

```text
Đơn giá hiển thị = 22.000 đ/kg
```

### Trường hợp 5

```text
Tồn: 99999 g
Đơn vị chọn: kg
```

Kết quả:

```text
Tồn hiển thị = 99.999 kg
```

### Trường hợp 6

```text
Đổi đơn vị nhiều lần:
g → kg → g
```

Kết quả:

* Không mất đơn giá.
* Không sai tồn.
* Không tích lũy sai số.
* Không nhân hoặc chia conversion nhiều lần.

### Trường hợp 7

```text
Client sửa conversionFactor bằng DevTools
```

Kết quả:

* Server bỏ qua conversion từ client.
* Server lấy conversion từ database.
* Chứng từ không bị lưu sai.

## 13. Kết quả bắt buộc của Phase 3

Sau khi hoàn thành phải cung cấp:

1. Root cause của lỗi gram và millilitre.
2. Root cause của lỗi mất đơn giá.
3. Root cause của lỗi hiển thị tồn sai đơn vị.
4. Sơ đồ conversion mới.
5. Danh sách file đã sửa.
6. DTO và ViewModel đã sửa.
7. Service và Repository đã sửa.
8. JavaScript đã sửa.
9. View đã sửa.
10. Migration hoặc script chuyển dữ liệu nếu cần.
11. Test case.
12. Acceptance criteria.
13. Nội dung cập nhật trong `FIX.md`.

---

# PHASE 4 — APP LAUNCHER SAU ĐĂNG NHẬP

## 1. Mục tiêu tương lai

Sau khi đăng nhập thành công, không redirect trực tiếp theo role.

Thay vào đó mở một View dạng App Launcher giống màn hình ứng dụng trên điện thoại.

Các card dự kiến:

```text
Admin Dashboard
StaffHub
POS
```

Mỗi card có:

* Tên ứng dụng.
* Icon tương ứng.
* Mô tả ngắn.
* Trạng thái có quyền hoặc không có quyền.
* Route đích.
* Thiết kế responsive.
* Hover/focus/keyboard accessibility.
* Giao diện phù hợp desktop và mobile.

## 2. Phân quyền

Ứng dụng hiển thị theo permission thực tế, không chỉ dựa vào role name.

Ví dụ:

```text
Admin Dashboard:
    chỉ hiển thị khi có quyền truy cập Admin Panel/Dashboard.

StaffHub:
    chỉ hiển thị khi có quyền truy cập StaffHub.

POS:
    chỉ hiển thị khi có quyền sử dụng POS.
```

Người dùng có thể thấy một, hai hoặc cả ba ứng dụng.

Nếu không có ứng dụng nào:

* Hiển thị thông báo không có quyền truy cập.
* Không redirect vòng lặp.
* Không hiển thị card không có quyền dưới dạng có thể click.

Cần kiểm tra người dùng truy cập URL trực tiếp. Không được chỉ dựa vào việc ẩn card.

## 3. Phạm vi lần này

Trong lần refactor hiện tại:

* Chỉ phân tích luồng đăng nhập đang có.
* Xác định các role, permission và policy liên quan.
* Thiết kế ViewModel cho App Launcher.
* Thiết kế route và luồng điều hướng.
* Liệt kê file dự kiến sửa.
* Viết đặc tả vào `FIX.md`.
* Không sửa code Phase 4.
* Không thay đổi LoginAsync.
* Không thay đổi RedirectByRole.
* Không tạo View App Launcher ở lần này.

---

# II. THỨ TỰ THỰC HIỆN

Phải thực hiện theo thứ tự:

```text
Bước 1: Phân tích source code và model hiện tại.
Bước 2: Xác định file liên quan đến từng phase.
Bước 3: Ghi nhận lỗi và root cause.
Bước 4: Refactor Phase 1.
Bước 5: Kiểm thử Phase 1.
Bước 6: Refactor Phase 2.
Bước 7: Kiểm thử Stored Procedure và mapping.
Bước 8: Refactor Phase 3.
Bước 9: Kiểm thử conversion và âm kho.
Bước 10: Phân tích và thiết kế Phase 4, không sửa code.
Bước 11: Cập nhật FIX.md.
Bước 12: Tổng hợp toàn bộ thay đổi.
```

Không được làm đồng thời tất cả file mà không xác định dependency.

---

# III. CẤU TRÚC NỘI DUNG TRONG FIX.MD

Hãy cập nhật `FIX.md` theo cấu trúc:

```markdown
# REFACTOR PLAN

## Tổng quan

## Kiến trúc hiện tại

## Các vấn đề phát hiện

# PHASE 1 — AI SERVICE

## Mục tiêu
## Luồng hiện tại
## Lỗi hiện tại
## Root cause
## Kiến trúc đề xuất
## Danh sách file cần sửa
## Chi tiết nghiệp vụ
## Prompt template
## Structured output
## Pexels flow
## ComfyUI flow
## Configuration
## Test cases
## Acceptance criteria
## Trạng thái thực hiện

# PHASE 2 — DASHBOARD STORED PROCEDURES

## Model mapping
## Công thức nghiệp vụ
## Stored Procedure list
## Procedure contracts
## Service/Repository mapping
## Index đề xuất
## Test cases
## Acceptance criteria
## Trạng thái thực hiện

# PHASE 3 — NEGATIVE STOCK AND UNIT CONVERSION

## Luồng hiện tại
## Root cause
## Quy tắc base unit
## Cấu hình âm kho
## Chuyển đổi tồn
## Chuyển đổi đơn giá
## Validation
## Transaction
## Test cases
## Acceptance criteria
## Trạng thái thực hiện

# PHASE 4 — APP LAUNCHER

## Mục tiêu
## Permission mapping
## Luồng đề xuất
## ViewModel đề xuất
## Danh sách file dự kiến sửa
## Rủi ro
## Acceptance criteria
## Trạng thái: CHƯA TRIỂN KHAI

# DANH SÁCH FILE ĐÃ THAY ĐỔI

# DATABASE CHANGES

# CONFIGURATION CHANGES

# TEST SUMMARY

# RỦI RO VÀ BACKWARD COMPATIBILITY
```

---

# IV. YÊU CẦU VỀ KẾT QUẢ TRẢ VỀ

Sau khi hoàn thành, trả kết quả theo cấu trúc:

## 1. Tổng quan

Tóm tắt ngắn gọn:

* Lỗi đã tìm thấy.
* Phase đã hoàn thành.
* Phase chưa triển khai.
* Thay đổi quan trọng.

## 2. Root cause

Phân tích nguyên nhân gốc của từng lỗi, không chỉ mô tả hiện tượng.

## 3. Danh sách file

Lập bảng:

```text
File
Phase
Loại thay đổi
Lý do
Mức ảnh hưởng
```

## 4. Code đã chỉnh sửa

* Cung cấp đầy đủ nội dung file hoặc method được chỉnh sửa.
* Không chỉ đưa pseudo-code.
* Không viết `// phần còn lại giữ nguyên` tại vị trí chứa logic quan trọng.
* Không bỏ qua namespace, interface hoặc dependency cần thiết.

## 5. Database

* Stored Procedure đầy đủ.
* Migration hoặc SQL script nếu cần.
* Index đề xuất.
* Ví dụ thực thi.
* Cấu trúc result set.

## 6. Configuration

Liệt kê đầy đủ key cần thêm hoặc sửa.

Không đưa secret hoặc API key thực vào source code.

## 7. Test

Bao gồm:

* Unit test.
* Integration test.
* SQL test.
* Test conversion.
* Test authorization.
* Test lỗi Ollama.
* Test Pexels không có kết quả.
* Test ComfyUI timeout.
* Test concurrent stock confirmation.

## 8. Acceptance criteria

Đánh dấu từng tiêu chí:

```text
PASS
FAIL
BLOCKED
NOT_SUPPORTED_BY_CURRENT_SCHEMA
```

## 9. Phase 4

Chỉ cung cấp bản thiết kế và kế hoạch.

Không triển khai code.

---

# V. TIÊU CHÍ NGHIỆM THU TỔNG THỂ

## Phase 1 hoàn thành khi

* Ollama luôn nhận được skill phù hợp.
* Ý tưởng mới nhất của người dùng được ưu tiên.
* Gợi ý không lặp lại dữ liệu hiện có quá ngưỡng cho phép.
* Mỗi lần trả về tối đa 1–3 option hợp lệ.
* Các option khác biệt thực sự.
* JSON được validate.
* Prompt ComfyUI có đầy đủ subject, background, lighting, style, composition và negative prompt.
* Ảnh Pexels không được chọn chỉ vì đứng đầu kết quả.
* Img2img có thay đổi đủ rõ nhưng không làm sai sản phẩm.
* Có fallback khi Ollama, Pexels hoặc ComfyUI lỗi.

## Phase 2 hoàn thành khi

* Stored Procedure bám đúng model thật.
* Không sử dụng bảng hoặc cột không tồn tại.
* Công thức tài chính và kho được định nghĩa rõ.
* Có scope theo quyền.
* Có result contract.
* Có index đề xuất.
* Có test.
* Các dashboard thiếu dữ liệu được đánh dấu rõ.

## Phase 3 hoàn thành khi

* `1000 g` và `1000 ml` được xử lý nhất quán.
* Không còn lỗi `1000 ml` bị hiểu thành `1 ml`.
* Đổi từ `g` sang `kg` không mất đơn giá.
* Đổi từ `ml` sang `l` không mất đơn giá.
* Tồn hiển thị đúng đơn vị đang chọn.
* Validation server sử dụng base unit.
* Client không thể giả mạo conversion factor.
* Không phát sinh sai số khi đổi đơn vị nhiều lần.
* Kiểm tra âm kho được thực hiện lại trong transaction.

## Phase 4 đạt yêu cầu thiết kế khi

* Có sơ đồ luồng sau đăng nhập.
* Có permission mapping cho từng app.
* Có ViewModel đề xuất.
* Có route đề xuất.
* Có danh sách file dự kiến sửa.
* Không có code Phase 4 bị thay đổi trong lần này.

---

# VI. LƯU Ý CUỐI CÙNG

* Không được chỉ viết kế hoạch chung chung cho ba phase đầu.
* Phải thực sự refactor Phase 1, Phase 2 và Phase 3.
* Phase 4 chỉ thiết kế.
* Không được tuyên bố hoàn thành nếu chưa cung cấp code, SQL và test tương ứng.
* Không được gọi context injection là fine-tuning.
* Không sử dụng AI để tự tính số liệu dashboard thay cho SQL.
* Không dùng dữ liệu giá hiện tại để tính ngược dữ liệu lịch sử.
* Không để JavaScript quyết định số tồn, giá vốn, âm kho hoặc quyền.
* Mọi dữ liệu gửi từ client phải được server kiểm tra lại.
* Khi phát hiện model thiếu dữ liệu, phải ghi rõ giới hạn thay vì tự bịa field.
* Tất cả thay đổi phải được ghi lại trong `FIX.md`.


---

# REFACTOR PLAN — KẾT QUẢ TRIỂN KHAI PHASE 1 VÀ PHASE 2

## Tổng quan

Phase 1 đã được refactor theo mô hình skill catalog nhiều module. Cây `Resources/AI` gồm 22 file placeholder được tạo đúng cấu trúc; nội dung vẫn để trống theo yêu cầu. Runtime phát hiện file rỗng, ghi warning và sử dụng prompt/DTO validator tích hợp nên luồng AI hiện tại không bị gián đoạn.

Phase 2 đã có script SQL Server idempotent, 33 Stored Procedure chuẩn, 13 contract tương thích cũ, filter/response DTO, Repository gọi procedure bằng allow-list, Service xác thực store scope và endpoint JSON mới. Dashboard UI cũ không bị thay đổi.

## Kiến trúc hiện tại và root cause

### Phase 1

Các lỗi chính trước refactor:

- Prompt được viết trực tiếp trong `AIService.MasterData.cs`, không có skill routing hoặc context theo entity.
- Ý tưởng người dùng chỉ là một field trong payload, không có GenerationMode.
- Chống trùng chỉ so tên/mã sau normalize, không phát hiện tên gần giống hoặc lịch sử cùng session.
- Fallback tĩnh có thể lặp giữa nhiều lần gọi.
- Visual Specification dùng một phong cách gần như cố định.
- ComfyUI chỉ ghi đè seed/denoise; steps, CFG, sampler và scheduler nằm cứng trong workflow.
- JSON từ Ollama chỉ deserialize DTO và không retry có giới hạn khi sai cấu trúc.

### Phase 2

Các procedure cũ có các vấn đề:

- Có procedure tính doanh thu từ Payment, procedure khác tính từ Order.Total nên không thống nhất.
- Nhiều predicate dùng CAST trên cột ngày, làm giảm khả năng dùng index.
- Top topping đếm mỗi OrderTopping một lần và bỏ qua OrderDetail.Quantity.
- Product revenue dùng OrderDetail.Price nhưng không tách topping, dẫn đến cộng trùng khi hiển thị đồng thời sản phẩm và topping.
- Chưa có contract cho WorkShift, FIFO, procurement, BOM health và reconciliation.
- Controller cũ nhận StaffId gián tiếp nhưng backend analytics chưa có allow-list procedure và response contract chung.

## PHASE 1 — AI SERVICE

### Skill catalog và routing

Các interface mới:

- `IAISkillCatalog.GetContextAsync(entityType, includeImageSkills)`.
- `IAISuggestionHistoryStore.Get/Add`.
- `AISkillContext`: entity, file đã load, context, schema và warnings.

Route cố định:

| Entity | Skill chain |
|---|---|
| Drink | router → business/drink → suggestion → diversity → duplicate → image prompt → Pexels → ComfyUI |
| Size | router → business/size → suggestion → diversity → duplicate |
| Topping | router → business/topping → suggestion → diversity → duplicate → image prompt → Pexels → ComfyUI |
| Ingredient | business/ingredient đã được chuẩn bị, chưa có endpoint |

Catalog chỉ đọc path nằm trong ContentRoot, cache theo `LastWriteTimeUtc`, tự reload khi file đổi và giới hạn context 12.000 ký tự. Schema rỗng/sai JSON được bỏ qua để dùng DTO validator.

### GenerationMode và lịch sử

Request Drink/Size/Topping có:

- `GenerationMode = New | Develop | Variant`.
- `PreviousSuggestions` tối đa 30 item.
- Selector GenerationMode trên ba form Admin.

Lịch sử máy chủ được giữ tối đa 30 tên trong 30 phút, theo Staff/Account + HTTP session + entity. Không phát sinh migration.

### Duplicate detection và scoring

- Normalize Unicode và bỏ dấu tiếng Việt.
- Exact name/code.
- Levenshtein name similarity, mặc định 0,86.
- Token Jaccard cho mô tả/concept.
- Composite threshold mặc định 0,82.
- Mỗi option có persona, CreativityScore, RelevanceScore, Reason và DuplicateSignals.
- Ba persona theo thứ tự: `safe-commercial`, `visual-creative`, `premium-trend`.

### Pexels và ComfyUI

Visual Specification đã bổ sung style profile, mood, container, surface, garnish, props, lens, depth of field và reference purpose. Pexels vẫn chỉ chấm metadata và luôn cảnh báo chưa có vision validation.

ComfyUI có thể ghi đè:

- `Steps`
- `Cfg`
- `SamplerName`
- `Scheduler`
- `Seed`
- `Denoise`

Hai workflow mới trong `Resources/AI/workflows` vẫn rỗng và chưa được cấu hình runtime. Hai workflow `Resources/AI/ComfyUI/product-*.json` tiếp tục là authority đang chạy.

### Configuration Phase 1

```text
AI:SkillRootPath
AI:SchemaRootPath
AI:MaximumSkillContextCharacters
AI:SuggestionHistoryLimit
AI:SuggestionHistoryMinutes
AI:NearNameSimilarityThreshold
AI:CompositeSimilarityThreshold
AI:MinimumRelevanceScore
AI:StructuredResponseRetries
Ollama:TopP
Ollama:TopK
Ollama:RepeatPenalty
ComfyUI:Steps
ComfyUI:Cfg
ComfyUI:SamplerName
ComfyUI:Scheduler
```

### Trạng thái Phase 1

| Tiêu chí | Trạng thái |
|---|---|
| Cây skill đúng cấu trúc | PASS |
| File skill giữ rỗng để người dùng tự viết | PASS |
| Router/cache/reload/fallback | PASS |
| GenerationMode và history session | PASS |
| Near-duplicate detection | PASS |
| Pexels metadata validation | PASS |
| Comfy sampler configuration | PASS |
| Ollama nhận đầy đủ nội dung nghiệp vụ từ Skill | BLOCKED — file Skill chưa có nội dung |

## PHASE 2 — DASHBOARD STORED PROCEDURES

### Công thức nghiệp vụ

- Chỉ đơn `OrderStatusId = 5` được tính là completed sale.
- Merchandise revenue = `Order.Total - Order.ShippingFee`.
- Full refund status Completed tạo phần đảo doanh thu.
- Product revenue loại phần topping khỏi `OrderDetail.Price`.
- Topping revenue = `OrderTopping.Price × OrderDetail.Quantity`.
- Gross profit chỉ xác nhận cho dòng có `CostStatus = Complete`; dữ liệu thiếu trả `PARTIAL_COGS`.
- Filter ngày dùng `>= FromDate AND < ToDate + 1 day`.
- Store IDs được Service kiểm tra theo StaffScope trước khi Repository gọi procedure.
- Repository ánh xạ enum widget sang tên procedure cố định; client không thể gửi tên SQL tùy ý.

### Hợp đồng backend

Endpoint:

```http
GET /Admin/Dashboard/GetAnalytics?widget={DashboardAnalyticsWidget}&fromDate=...&toDate=...&storeId=...&granularity=Day&top=10
```

Response chứa widget, FromDate, ToExclusive, Granularity, StoreIds đã được cấp quyền, DataStatus, Warnings và các rows theo result contract của procedure.

### Danh sách procedure

| Nhóm | Procedure |
|---|---|
| Điều hành | NetSalesTrend, StoreRanking, PaymentMethodMix, OrderHeatmap, OperationalAlerts |
| WorkShift/POS | CashDiscrepancy, Sales, PaymentMix, OfflineReconciliationExceptions, HourlyOrders, TopDiscrepancies, Kpis |
| Kho | ShortageRisk, MovementByType, ThresholdRisk, ReorderSuggestions, WasteByStoreIngredient, FifoLayerAge |
| Mua hàng | PurchaseOrderPipeline, OverduePurchaseOrders, SupplierQuality, PurchasePriceTrend, SpendBreakdown, SupplierIssueMix |
| Sản phẩm | TopProducts, VolumeMarginMatrix, SizeMargin, TopToppings, BomHealth, HighConsumptionLowEfficiency |
| Nhân sự | ShiftStatus, HourlyDemand, StaffPerformance |

Có 13 `dbo.sp_*` legacy compatibility contract cho DashboardRepository hiện tại.

### Index đề xuất

Không tự động tạo index trong script. Đề xuất đánh giá execution plan trước khi tạo:

```sql
-- Orders(StoreId, OrderStatusId, CreatedAt) INCLUDE (Total, ShippingFee, StaffId, WorkShiftId, CustomerId)
-- Payments(OrderId, PaymentStatusId, PaidAt) INCLUDE (Amount, PaymentMethodId, CashSessionId)
-- OrderRefunds(OrderId, Status, CompletedAtUtc) INCLUDE (StoreId, RefundAmount)
-- InventoryTransactions(StoreInventoryId, CreatedAt, Type) INCLUDE (Quantity, TotalCost, AfterQty)
-- WorkShifts(StoreId, StartTime, EndTime) INCLUDE (CashDiscrepancy, RequiresReconciliation)
-- PurchaseOrders(StoreId, Status, ExpectedDeliveryAtUtc)
-- BranchReceipts(StoreId, Status, ReceivedAt)
-- StaffShifts(StaffId, WorkDate) INCLUDE (ActualCheckIn, ActualCheckOut, PayrollHours, StatusId)
```

## PHÂN TÍCH GIẢI PHÁP CHẤM CÔNG

### Vấn đề phát hiện

- Check-in hiện tại không bảo đảm luôn xác minh IP cửa hàng trong cùng transaction nghiệp vụ.
- Chưa lưu GPS tại thời điểm check-in/out.
- Luồng VerifyRecentCheckIn chưa ràng buộc đầy đủ Store.
- POS token không chứng minh người dùng có StaffShift hợp lệ.
- Forwarded headers có thể bị tin cậy sai nếu proxy trust chưa được giới hạn.
- AttendanceLog chủ yếu ghi thành công, thiếu log cho lần thử thất bại.
- StartBreak đang sử dụng StatusId 4 trong khi seed hiện tại là ABSENT.
- FaceDescriptor có thể bị chính chủ ghi đè mà thiếu luồng phê duyệt/quản trị.

### Thiết kế đề xuất, chưa triển khai

- StaffShift cần StoreId snapshot để lịch sử không thay đổi khi Staff chuyển cửa hàng.
- AttendanceLog cần success/failure, store, source, IP hash, GPS, accuracy, reason code và verification factors.
- Thêm AttendanceExceptionApproval cho ngoại lệ mạng/GPS/thiết bị.
- Chỉ trusted proxy mới được cung cấp forwarded headers.
- Face enrollment/re-enrollment phải có audit và quyền phê duyệt.
- AttendanceQuality procedure: `NOT_SUPPORTED_BY_CURRENT_SCHEMA`.

## TEST SUMMARY

- Build ứng dụng: PASS, không có compile error.
- AI/Skill/SQL/dashboard analytics tests trọng tâm: 18/18 PASS.
- Toàn bộ test không phụ thuộc SQL Server: 1010/1010 PASS.
- 9 test AI image cũ tiếp tục PASS.
- SQL Server integration: BLOCKED vì local SQL Server trả `Failed to generate SSPI context`; test và disposable database contract đã được thêm nhưng chưa thể xác minh runtime.
- Script không chứa wildcard projection, dirty-read hint, dynamic procedure execution hoặc cursor.

## DANH SÁCH FILE ĐÃ THAY ĐỔI

- Resources/AI: cây 22 placeholder file.
- Application/Services/AI và Application/DTOs/AI: catalog, history, GenerationMode, uniqueness, scoring và visual specification.
- ComfyUI/Ollama configuration và client.
- Dashboard DTO/Repository/Service/Controller.
- SQL analytics script và test.
- Ba form Admin cùng JavaScript payload.
- FIX.md.

## CẬP NHẬT REFACTOR AI SKILLS VÀ DASHBOARD TYPED — 2026-07-17

Phần cập nhật này thay thế các trạng thái cũ “Skill còn rỗng” và “Dashboard UI cũ không thay đổi” ở phần tổng kết phía trên. Không thay đổi SQL nhúng, Stored Procedure, migration hoặc schema database.

### AI Skills

- Bảy `SKILL.md` đã được viết lại với một frontmatter `name/description`, đúng tên folder và chỉ chứa workflow thuộc domain CafeChain.
- Mười một reference đã có business rule/contract dựa trên model, DTO, service và validation thực tế. Nội dung hướng dẫn là tiếng Việt; field, JSON và prompt ảnh là tiếng Anh.
- `ai-suggestion.schema.json`, `image-concept.schema.json` và `output-schema.json` là JSON hợp lệ và có phạm vi riêng.
- Catalog loại YAML frontmatter khỏi prompt, ưu tiên entity reference, không đưa Pexels/ComfyUI vào prompt gợi ý sản phẩm, kiểm tra schema và giữ giới hạn 12.000 ký tự mà không cắt giữa entity rule.
- `txt2img-rules.md` và `img2img-rules.md` mô tả node mapping của workflow `product-*.json` đang chạy; workflow thử nghiệm và node graph hiện hành không bị sửa.

### Dashboard typed/lazy-load

- Public contract gồm `DashboardSection`, `DashboardFilterDto`, `DashboardWidgetResult<T>`, sáu section response và 33 row DTO typed; View không còn dùng `Dictionary<string, object>`.
- `IDashboardRepository` có sáu method theo nhóm. Mỗi nhóm mở một connection, gọi tuần tự procedure cố định, truyền đủ năm parameter và cô lập lỗi ở từng widget. Repository không gọi `dbo.sp_*`.
- `DashboardService` lấy store scope từ `IScopeAuthorizationService`, áp Province/District/Store filter trước khi gọi Repository; cancellation được truyền xuyên suốt.
- `DashboardController` dùng policy `RequireAdminPanelAccess`, bổ sung `GetSection`; `GetData` và `GetAnalytics` là compatibility adapter.
- `Index.cshtml` chỉ render shell, filter và store metadata. Sáu tab tải lần đầu theo nhu cầu, cache theo bộ lọc, hủy request cũ bằng `AbortController`, hỗ trợ skeleton, `NO_DATA`, partial warning, error/retry và resize ECharts.

### Trạng thái xác minh cập nhật

- Build ứng dụng và test project: PASS, 0 compile error.
- Unit/static test trọng tâm cho Skill routing/context/schema, scope/filter/cancellation/controller, 33 procedure contract và Dashboard View: 13/13 PASS.
- Toàn bộ test không phụ thuộc SQL Server: 1014/1014 PASS; 9/9 test AI image hiện có tiếp tục PASS.
- JavaScript Dashboard qua `node --check`: PASS.
- Bảy Skill và toàn bộ JSON dưới `Resources/AI` đã qua kiểm tra frontmatter/name/JSON tương đương: PASS. Không chạy được `quick_validate.py` vì máy chỉ có Windows Store Python alias, không có Python runtime.
- SQL integration chỉ nhắm database test riêng nhưng hiện BLOCKED: SQL Server instance không khả dụng (`SNI error 26 - Error Locating Server/Instance Specified`). Không có database vận hành nào bị thay đổi.

## DATABASE CHANGES

Không có migration và script chưa được tự động áp dụng vào database vận hành.

## SQL SCRIPT ĐỒNG BỘ

Nguồn độc lập: `Scripts/20260717_DashboardAnalyticsStoredProcedures.idempotent.sql`.

<!-- BEGIN GENERATED: PHASE2_SQL -->
```sql
/* CafeChain dashboard analytics — SQL Server, idempotent, schema-aligned 2026-07-17. */
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER FUNCTION dbo.ufn_AnalyticsStoreScope(@StoreIds nvarchar(max))
RETURNS TABLE
AS
RETURN
(
    SELECT s.StoreId
    FROM dbo.Stores AS s
    WHERE @StoreIds IS NULL
       OR LTRIM(RTRIM(@StoreIds)) = N''
       OR EXISTS
       (
           SELECT 1
           FROM STRING_SPLIT(@StoreIds, N',') AS value
           WHERE TRY_CONVERT(int, LTRIM(RTRIM(value.value))) = s.StoreId
       )
);
GO

CREATE OR ALTER PROCEDURE dbo.usp_Dashboard_NetSalesTrend
    @FromDate date, @ToDate date, @StoreIds nvarchar(max),
    @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    IF @FromDate IS NULL OR @ToDate IS NULL OR @FromDate > @ToDate THROW 50001, 'Invalid date range.', 1;
    DECLARE @ToExclusive datetime2 = DATEADD(day, 1, CONVERT(datetime2, @ToDate));
    ;WITH Dates AS
    (
        SELECT @FromDate AS BucketDate
        UNION ALL SELECT DATEADD(day, 1, BucketDate) FROM Dates WHERE BucketDate < @ToDate
    ), Events AS
    (
        SELECT CONVERT(date, o.CreatedAt) AS EventDate, 1 AS OrderCount,
               CONVERT(decimal(19,2), o.Total - o.ShippingFee) AS NetSales
        FROM dbo.Orders AS o
        INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = o.StoreId
        WHERE o.OrderStatusId = 5 AND o.CreatedAt >= @FromDate AND o.CreatedAt < @ToExclusive
        UNION ALL
        SELECT CONVERT(date, r.CompletedAtUtc), 0,
               -CONVERT(decimal(19,2), o.Total - o.ShippingFee)
        FROM dbo.OrderRefunds AS r
        INNER JOIN dbo.Orders AS o ON o.OrderId = r.OrderId AND o.OrderStatusId = 5
        INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = r.StoreId
        WHERE r.Status = 3 AND r.CompletedAtUtc >= @FromDate AND r.CompletedAtUtc < @ToExclusive
    )
    SELECT d.BucketDate, COALESCE(SUM(e.OrderCount), 0) AS TotalOrders,
           COALESCE(SUM(e.NetSales), 0) AS NetSales,
           CASE WHEN SUM(CASE WHEN e.EventDate IS NOT NULL THEN 1 ELSE 0 END) = 0 THEN 'NO_DATA' ELSE 'AVAILABLE' END AS DataStatus
    FROM Dates AS d
    LEFT JOIN Events AS e ON e.EventDate = d.BucketDate
    GROUP BY d.BucketDate
    ORDER BY d.BucketDate
    OPTION (MAXRECURSION 32767);
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Inventory_ShortageRisk
    @FromDate date, @ToDate date, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) si.StoreInventoryId, si.StoreId, si.IngredientId, i.Name AS IngredientName,
           si.AvailableQty, si.ReservedQty, si.MinStockLevel,
           CASE WHEN si.AvailableQty < 0 THEN 'CRITICAL' WHEN si.MinStockLevel IS NULL THEN 'UNCONFIGURED'
                WHEN si.AvailableQty <= si.MinStockLevel THEN 'HIGH' ELSE 'NORMAL' END AS RiskLevel,
           CASE WHEN si.MinStockLevel IS NULL THEN 'THRESHOLD_NOT_CONFIGURED' ELSE 'AVAILABLE' END AS DataStatus
    FROM dbo.StoreInventories AS si
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=si.StoreId
    LEFT JOIN dbo.Ingredients AS i ON i.IngredientId=si.IngredientId
    WHERE si.IngredientId IS NOT NULL
    ORDER BY CASE WHEN si.AvailableQty < 0 THEN 0 WHEN si.MinStockLevel IS NULL THEN 2 ELSE 1 END,
             si.AvailableQty-COALESCE(si.MinStockLevel,0), si.StoreInventoryId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Inventory_MovementByType
    @FromDate date, @ToDate date, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CONVERT(date,it.CreatedAt) AS MovementDate, it.Type AS TransactionType,
           COUNT_BIG(it.InventoryTransactionId) AS TransactionCount,
           SUM(it.Quantity) AS Quantity, COALESCE(SUM(it.TotalCost),0) AS TotalCost, 'AVAILABLE' AS DataStatus
    FROM dbo.InventoryTransactions AS it
    INNER JOIN dbo.StoreInventories AS si ON si.StoreInventoryId=it.StoreInventoryId
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=si.StoreId
    WHERE it.CreatedAt>=@FromDate AND it.CreatedAt<DATEADD(day,1,CONVERT(datetime2,@ToDate))
    GROUP BY CONVERT(date,it.CreatedAt),it.Type ORDER BY MovementDate,it.Type;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Inventory_ThresholdRisk
    @FromDate date, @ToDate date, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT si.StoreInventoryId,si.StoreId,si.IngredientId,i.Name AS IngredientName,
           si.AvailableQty,si.ReservedQty,si.MinStockLevel,si.MaxNegativeQty,
           si.AvailableQty-COALESCE(si.MinStockLevel,0) AS QuantityAboveMinimum,
           CASE WHEN si.MinStockLevel IS NULL THEN 'THRESHOLD_NOT_CONFIGURED'
                WHEN si.AvailableQty<0 THEN 'NEGATIVE' WHEN si.AvailableQty<=si.MinStockLevel THEN 'BELOW_MINIMUM'
                ELSE 'HEALTHY' END AS DataStatus
    FROM dbo.StoreInventories AS si INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=si.StoreId
    LEFT JOIN dbo.Ingredients AS i ON i.IngredientId=si.IngredientId
    WHERE si.IngredientId IS NOT NULL ORDER BY si.StoreId,QuantityAboveMinimum;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Inventory_ReorderSuggestions
    @FromDate date, @ToDate date, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) rr.RestockRequestId,rr.StoreId,rr.IngredientId,i.Name AS IngredientName,
           rr.RequestedQuantity,rr.SuggestedQuantity,rr.SuggestionAverageDailyUsageSnapshot,
           rr.SuggestionLeadTimeDaysSnapshot,rr.SuggestionIncomingQuantitySnapshot,rr.SuggestionReason,
           rr.Status,rr.Priority,rr.CreatedAt,'AVAILABLE' AS DataStatus
    FROM dbo.RestockRequests AS rr INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=rr.StoreId
    LEFT JOIN dbo.Ingredients AS i ON i.IngredientId=rr.IngredientId
    WHERE rr.CreatedAt>=@FromDate AND rr.CreatedAt<DATEADD(day,1,CONVERT(datetime2,@ToDate))
    ORDER BY CASE rr.Priority WHEN 'URGENT' THEN 0 WHEN 'HIGH' THEN 1 ELSE 2 END,rr.CreatedAt DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Inventory_WasteByStoreIngredient
    @FromDate date, @ToDate date, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) si.StoreId,s.Name AS StoreName,si.IngredientId,i.Name AS IngredientName,
           SUM(ABS(it.Quantity)) AS WasteQuantity,COALESCE(SUM(ABS(it.TotalCost)),0) AS WasteValue,
           COUNT_BIG(it.InventoryTransactionId) AS TransactionCount,'AVAILABLE' AS DataStatus
    FROM dbo.InventoryTransactions AS it INNER JOIN dbo.StoreInventories AS si ON si.StoreInventoryId=it.StoreInventoryId
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=si.StoreId
    INNER JOIN dbo.Stores AS s ON s.StoreId=si.StoreId LEFT JOIN dbo.Ingredients AS i ON i.IngredientId=si.IngredientId
    WHERE it.Type=3 AND it.CreatedAt>=@FromDate AND it.CreatedAt<DATEADD(day,1,CONVERT(datetime2,@ToDate))
    GROUP BY si.StoreId,s.Name,si.IngredientId,i.Name ORDER BY WasteValue DESC,WasteQuantity DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Inventory_FifoLayerAge
    @FromDate date, @ToDate date, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) l.InventoryCostLayerId,l.StoreId,l.IngredientId,l.PreparedItemId,
           l.RemainingQuantity,l.UnitCost,l.CreatedAt,DATEDIFF(day,l.CreatedAt,SYSUTCDATETIME()) AS AgeDays,
           l.RemainingQuantity*l.UnitCost AS RemainingValue,
           CASE WHEN l.RemainingQuantity<=0 THEN 'DEPLETED' WHEN DATEDIFF(day,l.CreatedAt,SYSUTCDATETIME())>=90 THEN 'AGED_90_PLUS'
                WHEN DATEDIFF(day,l.CreatedAt,SYSUTCDATETIME())>=30 THEN 'AGED_30_PLUS' ELSE 'CURRENT' END AS DataStatus
    FROM dbo.InventoryCostLayers AS l INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=l.StoreId
    WHERE l.RemainingQuantity>0 ORDER BY AgeDays DESC,l.InventoryCostLayerId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Procurement_PurchaseOrderPipeline
    @FromDate date,@ToDate date,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT po.Status,COUNT_BIG(po.PurchaseOrderId) AS PurchaseOrderCount,
           COALESCE(SUM(line.OrderValue),0) AS OrderedValue,'AVAILABLE' AS DataStatus
    FROM dbo.PurchaseOrders AS po INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=po.StoreId
    OUTER APPLY(SELECT SUM(pol.PackagePriceSnapshot*pol.PackageCount) AS OrderValue FROM dbo.PurchaseOrderLines AS pol WHERE pol.PurchaseOrderId=po.PurchaseOrderId) line
    WHERE po.OrderDate>=@FromDate AND po.OrderDate<DATEADD(day,1,CONVERT(datetime2,@ToDate))
    GROUP BY po.Status ORDER BY PurchaseOrderCount DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Procurement_OverduePurchaseOrders
    @FromDate date,@ToDate date,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) po.PurchaseOrderId,po.Code,po.StoreId,po.SupplierId,s.Name AS SupplierName,
           po.Status,po.OrderDate,po.ExpectedDeliveryAtUtc,DATEDIFF(day,po.ExpectedDeliveryAtUtc,SYSUTCDATETIME()) AS OverdueDays,
           'OVERDUE' AS DataStatus
    FROM dbo.PurchaseOrders AS po INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=po.StoreId
    INNER JOIN dbo.Suppliers AS s ON s.SupplierId=po.SupplierId
    WHERE po.ExpectedDeliveryAtUtc<SYSUTCDATETIME() AND po.Status NOT IN('COMPLETED','CANCELLED')
    ORDER BY OverdueDays DESC,po.PurchaseOrderId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Procurement_SupplierQuality
    @FromDate date,@ToDate date,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) br.SupplierId,s.Name AS SupplierName,
           SUM(brl.ReceivedBaseQuantity) AS AcceptedBaseQuantity,SUM(brl.RejectedBaseQuantity) AS RejectedBaseQuantity,
           CONVERT(decimal(9,4),COALESCE(SUM(brl.RejectedBaseQuantity)/NULLIF(SUM(brl.ReceivedBaseQuantity+brl.RejectedBaseQuantity),0),0)) AS RejectionRate,
           COUNT_BIG(DISTINCT br.BranchReceiptId) AS ReceiptCount,'AVAILABLE' AS DataStatus
    FROM dbo.BranchReceipts AS br INNER JOIN dbo.BranchReceiptLines AS brl ON brl.BranchReceiptId=br.BranchReceiptId
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=br.StoreId
    LEFT JOIN dbo.Suppliers AS s ON s.SupplierId=br.SupplierId
    WHERE br.Status='CONFIRMED' AND br.ReceivedAt>=@FromDate AND br.ReceivedAt<DATEADD(day,1,CONVERT(datetime2,@ToDate))
    GROUP BY br.SupplierId,s.Name ORDER BY RejectionRate DESC,RejectedBaseQuantity DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Procurement_PurchasePriceTrend
    @FromDate date,@ToDate date,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CONVERT(date,br.ReceivedAt) AS ReceiptDate,brl.IngredientId,i.Name AS IngredientName,
           AVG(brl.BaseUnitCostSnapshot) AS AverageBaseUnitCost,MIN(brl.BaseUnitCostSnapshot) AS MinimumBaseUnitCost,
           MAX(brl.BaseUnitCostSnapshot) AS MaximumBaseUnitCost,SUM(brl.ReceivedBaseQuantity) AS ReceivedBaseQuantity,'AVAILABLE' AS DataStatus
    FROM dbo.BranchReceipts AS br INNER JOIN dbo.BranchReceiptLines AS brl ON brl.BranchReceiptId=br.BranchReceiptId
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=br.StoreId LEFT JOIN dbo.Ingredients AS i ON i.IngredientId=brl.IngredientId
    WHERE br.Status='CONFIRMED' AND brl.IngredientId IS NOT NULL AND br.ReceivedAt>=@FromDate AND br.ReceivedAt<DATEADD(day,1,CONVERT(datetime2,@ToDate))
    GROUP BY CONVERT(date,br.ReceivedAt),brl.IngredientId,i.Name ORDER BY ReceiptDate,brl.IngredientId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Procurement_SpendBreakdown
    @FromDate date,@ToDate date,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) br.SupplierId,s.Name AS SupplierName,br.StoreId,
           SUM(brl.LineTotalCost) AS Spend,COUNT_BIG(DISTINCT br.BranchReceiptId) AS ReceiptCount,'AVAILABLE' AS DataStatus
    FROM dbo.BranchReceipts AS br INNER JOIN dbo.BranchReceiptLines AS brl ON brl.BranchReceiptId=br.BranchReceiptId
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=br.StoreId LEFT JOIN dbo.Suppliers AS s ON s.SupplierId=br.SupplierId
    WHERE br.Status='CONFIRMED' AND br.ReceivedAt>=@FromDate AND br.ReceivedAt<DATEADD(day,1,CONVERT(datetime2,@ToDate))
    GROUP BY br.SupplierId,s.Name,br.StoreId ORDER BY Spend DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Procurement_SupplierIssueMix
    @FromDate date,@ToDate date,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT issue.IssueType,issue.Status,COUNT_BIG(issue.SupplierReceiptIssueId) AS IssueCount,
           SUM(issue.AffectedBaseQuantity) AS AffectedBaseQuantity,'AVAILABLE' AS DataStatus
    FROM dbo.SupplierReceiptIssues AS issue INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=issue.StoreId
    WHERE issue.ReportedAtUtc>=@FromDate AND issue.ReportedAtUtc<DATEADD(day,1,CONVERT(datetime2,@ToDate))
    GROUP BY issue.IssueType,issue.Status ORDER BY IssueCount DESC,issue.IssueType;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Dashboard_StoreRanking
    @FromDate date, @ToDate date, @StoreIds nvarchar(max),
    @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ToExclusive datetime2 = DATEADD(day, 1, CONVERT(datetime2, @ToDate));
    SELECT TOP (ISNULL(NULLIF(@Top, 0), 10)) s.StoreId, s.Name AS StoreName,
           COUNT_BIG(o.OrderId) AS TotalOrders,
           COALESCE(SUM(o.Total - o.ShippingFee - CASE WHEN r.OrderRefundId IS NULL THEN 0 ELSE o.Total - o.ShippingFee END), 0) AS NetSales,
           COALESCE(AVG(CONVERT(decimal(19,2), o.Total - o.ShippingFee)), 0) AS AverageOrderValue,
           CASE WHEN COUNT_BIG(o.OrderId) = 0 THEN 'NO_DATA' ELSE 'AVAILABLE' END AS DataStatus
    FROM dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope
    INNER JOIN dbo.Stores AS s ON s.StoreId = scope.StoreId
    LEFT JOIN dbo.Orders AS o ON o.StoreId = s.StoreId AND o.OrderStatusId = 5
        AND o.CreatedAt >= @FromDate AND o.CreatedAt < @ToExclusive
    LEFT JOIN dbo.OrderRefunds AS r ON r.OrderId = o.OrderId AND r.Status = 3
    GROUP BY s.StoreId, s.Name
    ORDER BY NetSales DESC, s.StoreId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Dashboard_PaymentMethodMix
    @FromDate date, @ToDate date, @StoreIds nvarchar(max),
    @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ToExclusive datetime2 = DATEADD(day, 1, CONVERT(datetime2, @ToDate));
    SELECT pm.PaymentMethodId, pm.Code AS PaymentMethodCode, pm.Name AS PaymentMethodName,
           COUNT_BIG(p.PaymentId) AS TotalTransactions,
           COALESCE(SUM(p.Amount), 0) AS Amount,
           CONVERT(decimal(9,4), COALESCE(SUM(p.Amount) / NULLIF(SUM(SUM(p.Amount)) OVER (), 0), 0)) AS Share,
           'AVAILABLE' AS DataStatus
    FROM dbo.Payments AS p
    INNER JOIN dbo.PaymentMethods AS pm ON pm.PaymentMethodId = p.PaymentMethodId
    INNER JOIN dbo.Orders AS o ON o.OrderId = p.OrderId AND o.OrderStatusId = 5
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = o.StoreId
    WHERE p.PaymentStatusId = 2 AND COALESCE(p.PaidAt, o.CreatedAt) >= @FromDate
      AND COALESCE(p.PaidAt, o.CreatedAt) < @ToExclusive
    GROUP BY pm.PaymentMethodId, pm.Code, pm.Name
    ORDER BY Amount DESC, pm.PaymentMethodId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Dashboard_OrderHeatmap
    @FromDate date, @ToDate date, @StoreIds nvarchar(max),
    @Granularity varchar(10) = 'Hour', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @ToExclusive datetime2 = DATEADD(day, 1, CONVERT(datetime2, @ToDate));
    ;WITH WeekDays AS (SELECT value AS IsoWeekday FROM (VALUES(1),(2),(3),(4),(5),(6),(7)) d(value)),
    Hours AS (SELECT 0 AS HourOfDay UNION ALL SELECT HourOfDay + 1 FROM Hours WHERE HourOfDay < 23),
    Actual AS
    (
        SELECT 1 + (DATEDIFF(day, '19000101', CONVERT(date, o.CreatedAt)) % 7) AS IsoWeekday,
               DATEPART(hour, o.CreatedAt) AS HourOfDay, COUNT_BIG(o.OrderId) AS TotalOrders,
               SUM(o.Total - o.ShippingFee) AS NetSales
        FROM dbo.Orders AS o
        INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = o.StoreId
        WHERE o.OrderStatusId = 5 AND o.CreatedAt >= @FromDate AND o.CreatedAt < @ToExclusive
        GROUP BY 1 + (DATEDIFF(day, '19000101', CONVERT(date, o.CreatedAt)) % 7), DATEPART(hour, o.CreatedAt)
    )
    SELECT w.IsoWeekday, h.HourOfDay, COALESCE(a.TotalOrders, 0) AS TotalOrders,
           COALESCE(a.NetSales, 0) AS NetSales,
           CASE WHEN a.TotalOrders IS NULL THEN 'NO_DATA' ELSE 'AVAILABLE' END AS DataStatus
    FROM WeekDays AS w CROSS JOIN Hours AS h
    LEFT JOIN Actual AS a ON a.IsoWeekday = w.IsoWeekday AND a.HourOfDay = h.HourOfDay
    ORDER BY w.IsoWeekday, h.HourOfDay OPTION (MAXRECURSION 24);
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Dashboard_OperationalAlerts
    @FromDate date, @ToDate date, @StoreIds nvarchar(max),
    @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top, 0), 10)) alert.AlertType, alert.StoreId, alert.EntityId,
           alert.Severity, alert.AlertValue, alert.Message, alert.DataStatus
    FROM
    (
        SELECT 'LOW_STOCK' AS AlertType, si.StoreId, si.StoreInventoryId AS EntityId,
               CASE WHEN si.AvailableQty < 0 THEN 'CRITICAL' ELSE 'WARNING' END AS Severity,
               si.AvailableQty AS AlertValue, CONCAT('Tồn dưới ngưỡng: ', i.Name) AS Message, 'AVAILABLE' AS DataStatus
        FROM dbo.StoreInventories AS si
        INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = si.StoreId
        LEFT JOIN dbo.Ingredients AS i ON i.IngredientId = si.IngredientId
        WHERE si.MinStockLevel IS NOT NULL AND si.AvailableQty <= si.MinStockLevel
        UNION ALL
        SELECT 'CASH_DISCREPANCY', w.StoreId, w.ShiftId,
               CASE WHEN ABS(COALESCE(w.CashDiscrepancy, 0)) >= 50000 THEN 'CRITICAL' ELSE 'WARNING' END,
               COALESCE(w.CashDiscrepancy, 0), CONCAT('Chênh lệch WorkShift #', w.ShiftId), 'AVAILABLE'
        FROM dbo.WorkShifts AS w
        INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = w.StoreId
        WHERE w.EndTime >= @FromDate AND w.EndTime < DATEADD(day, 1, CONVERT(datetime2, @ToDate))
          AND ABS(COALESCE(w.CashDiscrepancy, 0)) > 0
        UNION ALL
        SELECT 'OVERDUE_PO', po.StoreId, po.PurchaseOrderId, 'WARNING',
               DATEDIFF(day, po.ExpectedDeliveryAtUtc, SYSUTCDATETIME()), CONCAT('PO quá hạn: ', po.Code), 'AVAILABLE'
        FROM dbo.PurchaseOrders AS po
        INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = po.StoreId
        WHERE po.ExpectedDeliveryAtUtc < SYSUTCDATETIME() AND po.Status NOT IN ('COMPLETED', 'CANCELLED')
    ) AS alert
    ORDER BY CASE alert.Severity WHEN 'CRITICAL' THEN 0 ELSE 1 END, ABS(alert.AlertValue) DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Operations_WorkShiftCashDiscrepancy
    @FromDate date, @ToDate date, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT w.ShiftId AS WorkShiftId, w.StoreId, s.Name AS StoreName, w.UserId AS StaffId, st.FullName,
           w.StartTime, w.EndTime, w.StartingCash, w.ExpectedEndingCash, w.ActualEndingCash,
           w.CashDiscrepancy, w.DiscrepancyReason, w.IsExceptionClosed, w.RequiresReconciliation,
           CASE WHEN w.EndTime IS NULL THEN 'OPEN' ELSE 'CLOSED' END AS DataStatus
    FROM dbo.WorkShifts AS w
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = w.StoreId
    INNER JOIN dbo.Stores AS s ON s.StoreId = w.StoreId
    INNER JOIN dbo.Staffs AS st ON st.StaffId = w.UserId
    WHERE w.StartTime < DATEADD(day, 1, CONVERT(datetime2, @ToDate))
      AND COALESCE(w.EndTime, DATEADD(day, 1, CONVERT(datetime2, @ToDate))) >= @FromDate
    ORDER BY w.StartTime DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Operations_WorkShiftSales
    @FromDate date, @ToDate date, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT w.ShiftId AS WorkShiftId, w.StoreId, COUNT_BIG(o.OrderId) AS TotalOrders,
           COALESCE(SUM(o.Total - o.ShippingFee), 0) AS NetSales,
           COALESCE(AVG(CONVERT(decimal(19,2), o.Total - o.ShippingFee)), 0) AS AverageOrderValue,
           CASE WHEN COUNT_BIG(o.OrderId) = 0 THEN 'NO_DATA' ELSE 'AVAILABLE' END AS DataStatus
    FROM dbo.WorkShifts AS w
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = w.StoreId
    LEFT JOIN dbo.Orders AS o ON o.WorkShiftId = w.ShiftId AND o.OrderStatusId = 5
    WHERE w.StartTime >= @FromDate AND w.StartTime < DATEADD(day, 1, CONVERT(datetime2, @ToDate))
    GROUP BY w.ShiftId, w.StoreId ORDER BY w.ShiftId DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Operations_WorkShiftPaymentMix
    @FromDate date, @ToDate date, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT o.WorkShiftId, o.StoreId, pm.PaymentMethodId, pm.Code AS PaymentMethodCode, pm.Name AS PaymentMethodName,
           COUNT_BIG(p.PaymentId) AS TotalTransactions, SUM(p.Amount) AS Amount, 'AVAILABLE' AS DataStatus
    FROM dbo.Payments AS p INNER JOIN dbo.Orders AS o ON o.OrderId = p.OrderId AND o.OrderStatusId = 5
    INNER JOIN dbo.PaymentMethods AS pm ON pm.PaymentMethodId = p.PaymentMethodId
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = o.StoreId
    WHERE o.WorkShiftId IS NOT NULL AND p.PaymentStatusId = 2 AND o.CreatedAt >= @FromDate
      AND o.CreatedAt < DATEADD(day, 1, CONVERT(datetime2, @ToDate))
    GROUP BY o.WorkShiftId, o.StoreId, pm.PaymentMethodId, pm.Code, pm.Name
    ORDER BY o.WorkShiftId DESC, Amount DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Operations_OfflineReconciliationExceptions
    @FromDate date, @ToDate date, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT w.ShiftId AS WorkShiftId, w.StoreId, w.IsExceptionClosed, w.OfflineOrderCountAtClose,
           w.OfflineEstimatedTotalAtClose, w.OfflineCashTotalAtClose, w.RequiresReconciliation,
           w.HasLateOfflineSync, w.LateOfflineSyncCount, w.LastLateOfflineSyncedAt,
           CASE WHEN w.RequiresReconciliation = 1 THEN 'REQUIRES_RECONCILIATION' ELSE 'LATE_SYNC' END AS DataStatus
    FROM dbo.WorkShifts AS w INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = w.StoreId
    WHERE w.StartTime >= @FromDate AND w.StartTime < DATEADD(day, 1, CONVERT(datetime2, @ToDate))
      AND (w.IsExceptionClosed = 1 OR w.RequiresReconciliation = 1 OR w.HasLateOfflineSync = 1)
    ORDER BY w.StartTime DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Operations_HourlyOrders
    @FromDate date, @ToDate date, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Hour', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    ;WITH Hours AS (SELECT 0 AS HourOfDay UNION ALL SELECT HourOfDay + 1 FROM Hours WHERE HourOfDay < 23),
    Actual AS
    (
        SELECT DATEPART(hour, o.CreatedAt) AS HourOfDay, COUNT_BIG(o.OrderId) AS TotalOrders,
               SUM(o.Total - o.ShippingFee) AS NetSales
        FROM dbo.Orders AS o INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = o.StoreId
        WHERE o.OrderStatusId = 5 AND o.CreatedAt >= @FromDate
          AND o.CreatedAt < DATEADD(day, 1, CONVERT(datetime2, @ToDate))
        GROUP BY DATEPART(hour, o.CreatedAt)
    )
    SELECT h.HourOfDay, COALESCE(a.TotalOrders, 0) AS TotalOrders, COALESCE(a.NetSales, 0) AS NetSales,
           CASE WHEN a.TotalOrders IS NULL THEN 'NO_DATA' ELSE 'AVAILABLE' END AS DataStatus
    FROM Hours AS h LEFT JOIN Actual AS a ON a.HourOfDay = h.HourOfDay
    ORDER BY h.HourOfDay OPTION (MAXRECURSION 24);
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Operations_WorkShiftTopDiscrepancies
    @FromDate date, @ToDate date, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) w.ShiftId AS WorkShiftId, w.StoreId, w.UserId AS StaffId,
           w.CashDiscrepancy, ABS(COALESCE(w.CashDiscrepancy,0)) AS AbsoluteDiscrepancy,
           w.DiscrepancyReason, w.EndTime, 'AVAILABLE' AS DataStatus
    FROM dbo.WorkShifts AS w INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId = w.StoreId
    WHERE w.EndTime >= @FromDate AND w.EndTime < DATEADD(day,1,CONVERT(datetime2,@ToDate))
      AND w.CashDiscrepancy IS NOT NULL ORDER BY ABS(w.CashDiscrepancy) DESC, w.ShiftId DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Operations_WorkShiftKpis
    @FromDate date, @ToDate date, @StoreIds nvarchar(max), @Granularity varchar(10) = 'Day', @Top int = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT_BIG(w.ShiftId) AS TotalWorkShifts,
           SUM(CASE WHEN w.EndTime IS NULL THEN 1 ELSE 0 END) AS OpenWorkShifts,
           SUM(CASE WHEN w.IsExceptionClosed = 1 THEN 1 ELSE 0 END) AS ExceptionClosedCount,
           SUM(CASE WHEN w.RequiresReconciliation = 1 THEN 1 ELSE 0 END) AS ReconciliationCount,
           COALESCE(SUM(ABS(COALESCE(w.CashDiscrepancy,0))),0) AS AbsoluteCashDiscrepancy,
           CASE WHEN COUNT_BIG(w.ShiftId)=0 THEN 'NO_DATA' ELSE 'AVAILABLE' END AS DataStatus
    FROM dbo.WorkShifts AS w INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=w.StoreId
    WHERE w.StartTime >= @FromDate AND w.StartTime < DATEADD(day,1,CONVERT(datetime2,@ToDate));
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Product_TopProducts
    @FromDate date,@ToDate date,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) od.DrinkId,od.DrinkName,
           SUM(od.Quantity) AS TotalSold,
           SUM((od.Price-COALESCE(t.ToppingUnitPrice,0))*od.Quantity) AS ProductRevenue,
           SUM(CASE WHEN od.CostStatus=1 THEN od.TotalCogs ELSE 0 END) AS ConfirmedCogs,
           SUM(CASE WHEN od.CostStatus=1 THEN (od.Price-COALESCE(t.ToppingUnitPrice,0))*od.Quantity-COALESCE(od.TotalCogs,0) ELSE 0 END) AS ConfirmedGrossProfit,
           CASE WHEN SUM(CASE WHEN od.CostStatus<>1 THEN 1 ELSE 0 END)>0 THEN 'PARTIAL_COGS' ELSE 'AVAILABLE' END AS DataStatus
    FROM dbo.OrderDetails AS od INNER JOIN dbo.Orders AS o ON o.OrderId=od.OrderId AND o.OrderStatusId=5
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=o.StoreId
    OUTER APPLY(SELECT SUM(ot.Price) AS ToppingUnitPrice FROM dbo.OrderToppings AS ot WHERE ot.OrderDetailId=od.OrderDetailId) t
    WHERE o.CreatedAt>=@FromDate AND o.CreatedAt<DATEADD(day,1,CONVERT(datetime2,@ToDate))
    GROUP BY od.DrinkId,od.DrinkName ORDER BY ProductRevenue DESC,TotalSold DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Product_VolumeMarginMatrix
    @FromDate date,@ToDate date,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) od.DrinkId,od.DrinkName,SUM(od.Quantity) AS Volume,
           SUM((od.Price-COALESCE(t.ToppingUnitPrice,0))*od.Quantity) AS Revenue,
           SUM(CASE WHEN od.CostStatus=1 THEN od.TotalCogs ELSE 0 END) AS ConfirmedCogs,
           CONVERT(decimal(9,4),COALESCE(SUM(CASE WHEN od.CostStatus=1 THEN (od.Price-COALESCE(t.ToppingUnitPrice,0))*od.Quantity-COALESCE(od.TotalCogs,0) ELSE 0 END)
             /NULLIF(SUM(CASE WHEN od.CostStatus=1 THEN (od.Price-COALESCE(t.ToppingUnitPrice,0))*od.Quantity ELSE 0 END),0),0)) AS ConfirmedMarginRate,
           CASE WHEN SUM(CASE WHEN od.CostStatus<>1 THEN 1 ELSE 0 END)>0 THEN 'PARTIAL_COGS' ELSE 'AVAILABLE' END AS DataStatus
    FROM dbo.OrderDetails AS od INNER JOIN dbo.Orders AS o ON o.OrderId=od.OrderId AND o.OrderStatusId=5
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=o.StoreId
    OUTER APPLY(SELECT SUM(ot.Price) AS ToppingUnitPrice FROM dbo.OrderToppings AS ot WHERE ot.OrderDetailId=od.OrderDetailId) t
    WHERE o.CreatedAt>=@FromDate AND o.CreatedAt<DATEADD(day,1,CONVERT(datetime2,@ToDate))
    GROUP BY od.DrinkId,od.DrinkName ORDER BY Volume DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Product_SizeMargin
    @FromDate date,@ToDate date,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT od.SizeId,COALESCE(od.SizeName,'Không size') AS SizeName,SUM(od.Quantity) AS TotalSold,
           SUM((od.Price-COALESCE(t.ToppingUnitPrice,0))*od.Quantity) AS Revenue,
           SUM(CASE WHEN od.CostStatus=1 THEN od.TotalCogs ELSE 0 END) AS ConfirmedCogs,
           SUM(CASE WHEN od.CostStatus=1 THEN (od.Price-COALESCE(t.ToppingUnitPrice,0))*od.Quantity-COALESCE(od.TotalCogs,0) ELSE 0 END) AS ConfirmedGrossProfit,
           CASE WHEN SUM(CASE WHEN od.CostStatus<>1 THEN 1 ELSE 0 END)>0 THEN 'PARTIAL_COGS' ELSE 'AVAILABLE' END AS DataStatus
    FROM dbo.OrderDetails AS od INNER JOIN dbo.Orders AS o ON o.OrderId=od.OrderId AND o.OrderStatusId=5
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=o.StoreId
    OUTER APPLY(SELECT SUM(ot.Price) AS ToppingUnitPrice FROM dbo.OrderToppings AS ot WHERE ot.OrderDetailId=od.OrderDetailId) t
    WHERE o.CreatedAt>=@FromDate AND o.CreatedAt<DATEADD(day,1,CONVERT(datetime2,@ToDate))
    GROUP BY od.SizeId,od.SizeName ORDER BY Revenue DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Product_TopToppings
    @FromDate date,@ToDate date,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) ot.ToppingId,ot.ToppingName,
           SUM(od.Quantity) AS TotalUsed,SUM(ot.Price*od.Quantity) AS Revenue,
           SUM(CASE WHEN ot.CostStatus=1 THEN ot.TotalCogs ELSE 0 END) AS ConfirmedCogs,
           CASE WHEN SUM(CASE WHEN ot.CostStatus<>1 THEN 1 ELSE 0 END)>0 THEN 'PARTIAL_COGS' ELSE 'AVAILABLE' END AS DataStatus
    FROM dbo.OrderToppings AS ot INNER JOIN dbo.OrderDetails AS od ON od.OrderDetailId=ot.OrderDetailId
    INNER JOIN dbo.Orders AS o ON o.OrderId=od.OrderId AND o.OrderStatusId=5
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=o.StoreId
    WHERE o.CreatedAt>=@FromDate AND o.CreatedAt<DATEADD(day,1,CONVERT(datetime2,@ToDate))
    GROUP BY ot.ToppingId,ot.ToppingName ORDER BY Revenue DESC,TotalUsed DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Product_BomHealth
    @FromDate date,@ToDate date,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) d.DrinkId,d.DrinkCode,d.Name AS DrinkName,
           COUNT(DISTINCT r.RecipeId) AS RecipeCount,COUNT(rd.RecipeDetailId) AS RecipeLineCount,
           SUM(CASE WHEN rd.IngredientId IS NULL AND rd.ChildRecipeId IS NULL THEN 1 ELSE 0 END) AS InvalidLineCount,
           CASE WHEN COUNT(DISTINCT r.RecipeId)=0 THEN 'MISSING_BOM'
                WHEN SUM(CASE WHEN rd.IngredientId IS NULL AND rd.ChildRecipeId IS NULL THEN 1 ELSE 0 END)>0 THEN 'INVALID_BOM'
                ELSE 'AVAILABLE' END AS DataStatus
    FROM dbo.Drinks AS d LEFT JOIN dbo.Recipes AS r ON r.DrinkId=d.DrinkId AND r.Active=1
    LEFT JOIN dbo.RecipeDetails AS rd ON rd.RecipeId=r.RecipeId
    GROUP BY d.DrinkId,d.DrinkCode,d.Name ORDER BY InvalidLineCount DESC,RecipeCount,d.DrinkId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Product_HighConsumptionLowEfficiency
    @FromDate date,@ToDate date,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) od.DrinkId,od.DrinkName,SUM(od.Quantity) AS TotalSold,
           SUM(CASE WHEN od.CostStatus=1 THEN od.TotalCogs ELSE 0 END) AS ConfirmedCogs,
           SUM(CASE WHEN od.CostStatus=1 THEN od.Price*od.Quantity-COALESCE(od.TotalCogs,0) ELSE 0 END) AS ConfirmedGrossProfit,
           CASE WHEN SUM(CASE WHEN od.CostStatus<>1 THEN 1 ELSE 0 END)>0 THEN 'PARTIAL_COGS' ELSE 'AVAILABLE' END AS DataStatus
    FROM dbo.OrderDetails AS od INNER JOIN dbo.Orders AS o ON o.OrderId=od.OrderId AND o.OrderStatusId=5
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=o.StoreId
    WHERE o.CreatedAt>=@FromDate AND o.CreatedAt<DATEADD(day,1,CONVERT(datetime2,@ToDate))
    GROUP BY od.DrinkId,od.DrinkName
    ORDER BY ConfirmedCogs DESC,ConfirmedGrossProfit ASC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Workforce_ShiftStatus
    @FromDate date,@ToDate date,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ss.StaffShiftId,ss.StaffId,st.FullName,st.StoreId,ss.WorkDate,ss.ActualCheckIn,ss.ActualCheckOut,
           ss.PayrollHours,ss.StatusId,status.Code AS StatusCode,ss.IsAdHoc,
           'CURRENT_STAFF_STORE_SCOPE' AS DataStatus
    FROM dbo.StaffShifts AS ss INNER JOIN dbo.Staffs AS st ON st.StaffId=ss.StaffId
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=st.StoreId
    INNER JOIN dbo.StaffShiftStatuses AS status ON status.StaffShiftStatusId=ss.StatusId
    WHERE ss.WorkDate>=@FromDate AND ss.WorkDate<DATEADD(day,1,CONVERT(datetime2,@ToDate))
    ORDER BY ss.WorkDate DESC,ss.StaffShiftId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Workforce_HourlyDemand
    @FromDate date,@ToDate date,@StoreIds nvarchar(max),@Granularity varchar(10)='Hour',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    ;WITH Hours AS(SELECT 0 AS HourOfDay UNION ALL SELECT HourOfDay+1 FROM Hours WHERE HourOfDay<23),
    Demand AS
    (
        SELECT DATEPART(hour,o.CreatedAt) AS HourOfDay,COUNT_BIG(o.OrderId) AS TotalOrders,SUM(o.Total-o.ShippingFee) AS NetSales
        FROM dbo.Orders AS o INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=o.StoreId
        WHERE o.OrderStatusId=5 AND o.CreatedAt>=@FromDate AND o.CreatedAt<DATEADD(day,1,CONVERT(datetime2,@ToDate))
        GROUP BY DATEPART(hour,o.CreatedAt)
    ),Staffing AS
    (
        SELECT h.HourOfDay,COUNT_BIG(ss.StaffShiftId) AS StaffShiftCount
        FROM Hours AS h INNER JOIN dbo.StaffShifts AS ss ON ss.ActualCheckIn IS NOT NULL
          AND h.HourOfDay BETWEEN DATEPART(hour,ss.ActualCheckIn) AND DATEPART(hour,COALESCE(ss.ActualCheckOut,ss.ActualCheckIn))
        INNER JOIN dbo.Staffs AS st ON st.StaffId=ss.StaffId INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=st.StoreId
        WHERE ss.WorkDate>=@FromDate AND ss.WorkDate<DATEADD(day,1,CONVERT(datetime2,@ToDate)) GROUP BY h.HourOfDay
    )
    SELECT h.HourOfDay,COALESCE(d.TotalOrders,0) AS TotalOrders,COALESCE(d.NetSales,0) AS NetSales,
           COALESCE(s.StaffShiftCount,0) AS StaffShiftCount,
           CONVERT(decimal(19,2),COALESCE(d.TotalOrders/NULLIF(CONVERT(decimal(19,2),s.StaffShiftCount),0),0)) AS OrdersPerStaff,
           CASE WHEN d.TotalOrders IS NULL AND s.StaffShiftCount IS NULL THEN 'NO_DATA' ELSE 'CURRENT_STAFF_STORE_SCOPE' END AS DataStatus
    FROM Hours AS h LEFT JOIN Demand AS d ON d.HourOfDay=h.HourOfDay LEFT JOIN Staffing AS s ON s.HourOfDay=h.HourOfDay
    ORDER BY h.HourOfDay OPTION(MAXRECURSION 24);
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Workforce_StaffPerformance
    @FromDate date,@ToDate date,@StoreIds nvarchar(max),@Granularity varchar(10)='Day',@Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) st.StaffId,st.FullName,st.StoreId,
           COUNT_BIG(DISTINCT o.OrderId) AS TotalOrders,COALESCE(SUM(o.Total-o.ShippingFee),0) AS NetSales,
           COALESCE(hours.PayrollHours,0) AS PayrollHours,
           CONVERT(decimal(19,2),COALESCE(SUM(o.Total-o.ShippingFee)/NULLIF(hours.PayrollHours,0),0)) AS SalesPerPayrollHour,
           'CURRENT_STAFF_STORE_SCOPE' AS DataStatus
    FROM dbo.Staffs AS st INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=st.StoreId
    LEFT JOIN dbo.Orders AS o ON o.StaffId=st.StaffId AND o.OrderStatusId=5 AND o.CreatedAt>=@FromDate AND o.CreatedAt<DATEADD(day,1,CONVERT(datetime2,@ToDate))
    OUTER APPLY(SELECT SUM(ss.PayrollHours) AS PayrollHours FROM dbo.StaffShifts AS ss WHERE ss.StaffId=st.StaffId
      AND ss.WorkDate>=@FromDate AND ss.WorkDate<DATEADD(day,1,CONVERT(datetime2,@ToDate))) hours
    GROUP BY st.StaffId,st.FullName,st.StoreId,hours.PayrollHours
    ORDER BY NetSales DESC,st.StaffId;
END;
GO

/* Legacy compatibility contracts used by the current DashboardRepository. */
CREATE OR ALTER PROCEDURE dbo.sp_Revenue_By_Store
    @FromDate datetime,@ToDate datetime,@StoreIds nvarchar(max)=NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT s.StoreId,s.Name,COUNT_BIG(o.OrderId) AS TotalOrders,
           COALESCE(SUM(o.Total-o.ShippingFee-CASE WHEN r.OrderRefundId IS NULL THEN 0 ELSE o.Total-o.ShippingFee END),0) AS Revenue
    FROM dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope INNER JOIN dbo.Stores AS s ON s.StoreId=scope.StoreId
    LEFT JOIN dbo.Orders AS o ON o.StoreId=s.StoreId AND o.OrderStatusId=5 AND o.CreatedAt>=@FromDate AND o.CreatedAt<DATEADD(day,1,CONVERT(date,@ToDate))
    LEFT JOIN dbo.OrderRefunds AS r ON r.OrderId=o.OrderId AND r.Status=3
    GROUP BY s.StoreId,s.Name ORDER BY Revenue DESC,s.StoreId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Revenue_Filtered
    @FromDate datetime,@ToDate datetime,@StoreIds nvarchar(max)=NULL,@ProvinceId int=NULL,@DistrictId int=NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CONVERT(date,o.CreatedAt) AS [Date],COUNT_BIG(o.OrderId) AS TotalOrders,
           COALESCE(SUM(o.Total-o.ShippingFee-CASE WHEN r.OrderRefundId IS NULL THEN 0 ELSE o.Total-o.ShippingFee END),0) AS Revenue
    FROM dbo.Orders AS o INNER JOIN dbo.Stores AS s ON s.StoreId=o.StoreId
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=o.StoreId
    LEFT JOIN dbo.OrderRefunds AS r ON r.OrderId=o.OrderId AND r.Status=3
    WHERE o.OrderStatusId=5 AND o.CreatedAt>=@FromDate AND o.CreatedAt<DATEADD(day,1,CONVERT(date,@ToDate))
      AND (@ProvinceId IS NULL OR s.ProvinceId=@ProvinceId) AND (@DistrictId IS NULL OR s.DistrictId=@DistrictId)
    GROUP BY CONVERT(date,o.CreatedAt) ORDER BY [Date];
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Inventory_Summary @StoreId int
AS
BEGIN
    SET NOCOUNT ON;
    SELECT i.IngredientId,i.Name,
           COALESCE(SUM(CASE WHEN it.Type IN(1,5,8,11,13,14,15) THEN ABS(it.Quantity) ELSE 0 END),0) AS TotalImport,
           COALESCE(SUM(CASE WHEN it.Type IN(2,6,7,9,10,12) THEN ABS(it.Quantity) ELSE 0 END),0) AS TotalExport,
           COALESCE(SUM(CASE WHEN it.Type=3 THEN ABS(it.Quantity) ELSE 0 END),0) AS TotalWaste,
           si.AvailableQty AS CurrentStock
    FROM dbo.StoreInventories AS si INNER JOIN dbo.Ingredients AS i ON i.IngredientId=si.IngredientId
    LEFT JOIN dbo.InventoryTransactions AS it ON it.StoreInventoryId=si.StoreInventoryId
    WHERE si.StoreId=@StoreId GROUP BY i.IngredientId,i.Name,si.AvailableQty ORDER BY i.Name;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Waste_Report
    @FromDate datetime,@ToDate datetime,@StoreIds nvarchar(max)=NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT si.StoreId,s.Name AS StoreName,i.IngredientId,i.Name AS IngredientName,
           SUM(ABS(it.Quantity)) AS TotalWasteQty,COALESCE(SUM(ABS(it.TotalCost)),0) AS TotalWasteValue
    FROM dbo.InventoryTransactions AS it INNER JOIN dbo.StoreInventories AS si ON si.StoreInventoryId=it.StoreInventoryId
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=si.StoreId
    INNER JOIN dbo.Stores AS s ON s.StoreId=si.StoreId INNER JOIN dbo.Ingredients AS i ON i.IngredientId=si.IngredientId
    WHERE it.Type=3 AND it.CreatedAt>=@FromDate AND it.CreatedAt<DATEADD(day,1,CONVERT(date,@ToDate))
    GROUP BY si.StoreId,s.Name,i.IngredientId,i.Name ORDER BY TotalWasteQty DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Cash_Flow_Today @StoreIds nvarchar(max)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT cs.CashSessionId,cs.StaffId,cs.OpenTime,cs.CloseTime,cs.StartCash,
           COALESCE(SUM(CASE WHEN pm.Code='CASH' THEN p.Amount ELSE 0 END),0) AS CashIn,
           COALESCE(SUM(CASE WHEN pm.Code<>'CASH' THEN p.Amount ELSE 0 END),0) AS NonCashIn,
           COALESCE(SUM(p.Amount),0) AS TotalRevenue
    FROM dbo.CashSessions AS cs INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=cs.StoreId
    LEFT JOIN dbo.Payments AS p ON p.CashSessionId=cs.CashSessionId AND p.PaymentStatusId=2
    LEFT JOIN dbo.PaymentMethods AS pm ON pm.PaymentMethodId=p.PaymentMethodId
    WHERE cs.OpenTime>=CONVERT(date,GETDATE()) AND cs.OpenTime<DATEADD(day,1,CONVERT(date,GETDATE()))
    GROUP BY cs.CashSessionId,cs.StaffId,cs.OpenTime,cs.CloseTime,cs.StartCash ORDER BY cs.OpenTime DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Top_Selling_Drinks_Filtered
    @Top int=10,@FromDate datetime,@ToDate datetime,@StoreIds nvarchar(max)=NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) od.DrinkId,od.DrinkName,SUM(od.Quantity) AS TotalSold,
           SUM((od.Price-COALESCE(t.ToppingUnitPrice,0))*od.Quantity) AS Revenue
    FROM dbo.OrderDetails AS od INNER JOIN dbo.Orders AS o ON o.OrderId=od.OrderId AND o.OrderStatusId=5
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=o.StoreId
    OUTER APPLY(SELECT SUM(ot.Price) AS ToppingUnitPrice FROM dbo.OrderToppings AS ot WHERE ot.OrderDetailId=od.OrderDetailId) t
    WHERE o.CreatedAt>=@FromDate AND o.CreatedAt<DATEADD(day,1,CONVERT(date,@ToDate))
    GROUP BY od.DrinkId,od.DrinkName ORDER BY TotalSold DESC,Revenue DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Top_Toppings_Filtered
    @FromDate datetime,@ToDate datetime,@StoreIds nvarchar(max)=NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT ot.ToppingId,ot.ToppingName,SUM(od.Quantity) AS TotalUsed,SUM(ot.Price*od.Quantity) AS Revenue
    FROM dbo.OrderToppings AS ot INNER JOIN dbo.OrderDetails AS od ON od.OrderDetailId=ot.OrderDetailId
    INNER JOIN dbo.Orders AS o ON o.OrderId=od.OrderId AND o.OrderStatusId=5
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=o.StoreId
    WHERE o.CreatedAt>=@FromDate AND o.CreatedAt<DATEADD(day,1,CONVERT(date,@ToDate))
    GROUP BY ot.ToppingId,ot.ToppingName ORDER BY TotalUsed DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Top_Customers @Top int=10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (ISNULL(NULLIF(@Top,0),10)) c.CustomerId,c.FullName,COUNT_BIG(o.OrderId) AS TotalOrders,
           SUM(o.Total-o.ShippingFee) AS TotalSpent
    FROM dbo.Orders AS o INNER JOIN dbo.Customers AS c ON c.CustomerId=o.CustomerId
    WHERE o.OrderStatusId=5 GROUP BY c.CustomerId,c.FullName ORDER BY TotalSpent DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Revenue_By_PaymentMethod_Filtered
    @FromDate datetime,@ToDate datetime,@StoreIds nvarchar(max)=NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT pm.Name,COUNT_BIG(p.PaymentId) AS TotalTransactions,COALESCE(SUM(p.Amount),0) AS Revenue
    FROM dbo.Payments AS p INNER JOIN dbo.PaymentMethods AS pm ON pm.PaymentMethodId=p.PaymentMethodId
    INNER JOIN dbo.Orders AS o ON o.OrderId=p.OrderId AND o.OrderStatusId=5
    INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=o.StoreId
    WHERE p.PaymentStatusId=2 AND o.CreatedAt>=@FromDate AND o.CreatedAt<DATEADD(day,1,CONVERT(date,@ToDate))
    GROUP BY pm.Name ORDER BY Revenue DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Order_Status_Stats
AS
BEGIN
    SET NOCOUNT ON;
    SELECT os.Name,COUNT_BIG(o.OrderId) AS TotalOrders FROM dbo.OrderStatuses AS os
    LEFT JOIN dbo.Orders AS o ON o.OrderStatusId=os.OrderStatusId GROUP BY os.Name ORDER BY os.Name;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Revenue_By_Hour
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DATEPART(hour,o.CreatedAt) AS HourOfDay,COUNT_BIG(o.OrderId) AS TotalOrders,
           SUM(o.Total-o.ShippingFee) AS Revenue FROM dbo.Orders AS o WHERE o.OrderStatusId=5
    GROUP BY DATEPART(hour,o.CreatedAt) ORDER BY HourOfDay;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Staff_Performance_Filtered
    @FromDate datetime,@ToDate datetime,@StoreIds nvarchar(max)=NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT st.StaffId,st.FullName,COUNT_BIG(o.OrderId) AS TotalOrders,
           COALESCE(SUM(o.Total-o.ShippingFee),0) AS Revenue
    FROM dbo.Staffs AS st INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=st.StoreId
    LEFT JOIN dbo.Orders AS o ON o.StaffId=st.StaffId AND o.OrderStatusId=5
      AND o.CreatedAt>=@FromDate AND o.CreatedAt<DATEADD(day,1,CONVERT(date,@ToDate))
    GROUP BY st.StaffId,st.FullName ORDER BY Revenue DESC,TotalOrders DESC;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_Dashboard_Summary_Filtered
    @FromDate datetime,@ToDate datetime,@StoreIds nvarchar(max)=NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT_BIG(o.OrderId) AS TotalOrders,
           COALESCE(SUM(o.Total-o.ShippingFee-CASE WHEN r.OrderRefundId IS NULL THEN 0 ELSE o.Total-o.ShippingFee END),0) AS Revenue,
           COUNT(DISTINCT o.CustomerId) AS TotalCustomers,
           SUM(CASE WHEN o.CreatedAt>=CONVERT(date,GETDATE()) AND o.CreatedAt<DATEADD(day,1,CONVERT(date,GETDATE())) THEN 1 ELSE 0 END) AS TodayOrders
    FROM dbo.Orders AS o INNER JOIN dbo.ufn_AnalyticsStoreScope(@StoreIds) AS scope ON scope.StoreId=o.StoreId
    LEFT JOIN dbo.OrderRefunds AS r ON r.OrderId=o.OrderId AND r.Status=3
    WHERE o.OrderStatusId=5 AND o.CreatedAt>=@FromDate AND o.CreatedAt<DATEADD(day,1,CONVERT(date,@ToDate));
END;
GO
```
<!-- END GENERATED: PHASE2_SQL -->

---

# REFACTOR RESULT — PHASE 3 VÀ PHASE 4 (2026-07-17)

> Yêu cầu ban đầu của Phase 4 ghi “chỉ thiết kế, không sửa code”. Giới hạn này đã được thay thế bởi yêu cầu mới nhất: triển khai đầy đủ Phase 3 và Phase 4. Nội dung yêu cầu gốc phía trên được giữ lại để đối chiếu lịch sử.

## PHASE 3 — NEGATIVE STOCK AND UNIT CONVERSION

### Trạng thái

`IMPLEMENTED`

### Root cause

1. `AdminInventoryDocumentCreateService` tự tìm `Ingredient.UnitConversions` theo một chiều nên bỏ qua cặp vật lý `g/kg`, `ml/l`, reverse conversion và conflict validation của `IUnitConversionService`.
2. JavaScript kiểm tra `CanAutoFillUnitPrice` và `PackageUnitId` trước khi thử giá vốn base. Dữ liệu FIFO có package unit bằng base unit nên đổi từ `g` sang `kg` làm mất giá dù `SuggestedBaseUnitCost` vẫn hợp lệ.
3. Available quantity, preflight và negative limit luôn render từ base quantity/base unit, khiến số lớn như `99999 g` không đổi theo đơn vị người dùng đang nhập.
4. Màn hình Settings tổng quát vẫn POST được nhiều nhóm setting, trong khi nghiệp vụ Phase 3 chỉ cho phép Owner/System Admin quản lý âm kho.

### Kiến trúc sau refactor

```text
Selected quantity/unit
        |
        v
IUnitConversionService
  same unit -> physical registry -> ingredient conversion -> fail closed
        |
        v
BaseQuantity / MaxNegativeQty (database authority)
        |
        +--> inventory policy, transaction, row lock, audit
        |
        +--> display quantity = base quantity / trusted factor
```

- `InventoryUnitOptionDTO` là contract dùng chung: `UnitId`, `UnitCode`, `UnitName`, `ConversionFactorToBase`, `IsBaseUnit`.
- `GetActiveUnitOptionsAsync` chỉ trả đơn vị active và quy đổi được về base; conflict hoặc unit không hợp lệ làm request fail closed.
- Client chỉ gửi `DisplayUnitId`; server tự tải factor và quy đổi. Không có conversion factor từ client trong update contract.
- Đơn vị hiển thị không được persist. Lần tải mới luôn bắt đầu từ base unit.
- BTP chưa có conversion graph riêng nên chỉ nhận base unit.
- `InventoryPriceSemantics` phân biệt `BASE_UNIT_COST`, `PURCHASE_PACKAGE`, `NONE`.
- Preflight giữ base fields cũ và bổ sung selected-unit fields để tương thích payload hiện tại.

### Quy tắc lưu

- `1 kg` của nguyên liệu base `g` được lưu thành `1000 g`.
- `1 l` của nguyên liệu base `ml` được lưu thành `1000 ml`.
- Giá FIFO/base cost: `selected unit price = base unit cost × conversion factor`.
- Giá nhập mua hàng tiếp tục được server tính từ package quantity và supplier offer.
- `BaseQuantity` client gửi lên bị ghi đè bởi kết quả quy đổi server.
- Transaction, deduplication, row lock, maker-checker, `RowVersion` và audit log được giữ nguyên.

### System Settings

- `/Admin/AdminSetting` chỉ render cấu hình âm kho.
- Controller khóa toàn bộ route cho `BusinessOwner` và `SystemAdmin`; truy cập trực tiếp bởi role khác nhận 403.
- Action cập nhật settings tổng quát đã được gỡ khỏi controller và interface. Dữ liệu `SystemSettings` hiện hữu không bị xóa.

### Contract preflight bổ sung

```text
UnitId
UnitCode
ConversionFactorToBase
BeforeDisplayQty
IssueDisplayQty
ProjectedAfterDisplayQty
EffectiveMaxNegativeDisplayQty
```

### Database

Không có migration schema cho Phase 3. `StoreInventory.MaxNegativeQty` và mọi quantity authority tiếp tục dùng base unit.

## PHASE 4 — APP LAUNCHER

### Trạng thái

`IMPLEMENTED`

### Permission mapping

| Permission | Mapping mặc định |
|---|---|
| `App.AdminDashboard` | Business Owner, Area Manager, Store Manager, Accountant/Warehouse, System Admin |
| `App.StaffHub` | Tất cả role nhân viên, loại Customer |
| `App.POS` | Store Manager, Sales Staff, Shift Supervisor |

- Permission group mới: `APPLICATION`.
- Migration `20260717193000_AddApplicationPermissions` chỉ thêm dữ liệu, không thay đổi schema.
- Migration lookup theo permission code và role name, dùng `NOT EXISTS` nên chạy an toàn khi dữ liệu tương ứng đã tồn tại.
- `RolePermissions` và `AccountPermissionOverrides` vẫn là nguồn quyết định cuối cùng.

### Authorization flow

```text
Cookie authenticated
      |
      v
PermissionRequirement(permission code)
      |
      v
IAdminPermissionService.HasPermissionAsync(accountId, code)
      |
      +--> role grant
      +--> account allow/deny override
      |
      v
Launcher card + destination controller use the same policy
```

Các policy mới:

- `RequireAdminDashboardApp`
- `RequireStaffHubApp`
- `RequirePosApp`

Ẩn card không thay thế authorization. `DashboardController`, `StaffHubController`, `IssuePosToken` và `AdminPOSController` đều kiểm tra policy tương ứng.

### Redirect sau đăng nhập

1. `returnUrl` nội bộ hợp lệ có ưu tiên cao nhất.
2. Tài khoản có claim/DTO `StaffId` đi tới `/AppLauncher`.
3. Customer tiếp tục về `/Home`.
4. Tài khoản đã đăng nhập mở lại Login cũng dùng cùng quy tắc.
5. `LoginResponseDto.Role` được giữ để tương thích; primary role dùng thứ tự `RoleConstants` chính xác và không còn tìm chuỗi tiếng Anh `Admin/Manager/Cashier`.

### App Launcher contract

```text
IAppLauncherService.GetAppsAsync(accountId, displayName, cancellationToken)
AppLauncherVM
AppLauncherCardDTO
AppCode: AdminDashboard | StaffHub | Pos
```

Route card:

- Admin Dashboard: `/Admin/Dashboard`
- StaffHub: `/StaffHub`
- POS: `/Admin/AdminPOS`

Launcher chỉ render card được cấp quyền. Nếu danh sách rỗng, trang hiển thị hướng dẫn liên hệ quản trị và nút đăng xuất, không redirect vòng lặp.

## DASHBOARD USER GUIDE

- Tài liệu vận hành: `docs/user-guides/dashboard-analytics.md`.
- Trang trong ứng dụng: `GET /Admin/Dashboard/Guide`.
- Trang Guide dùng cùng `RequireAdminDashboardApp` với Dashboard.
- Nội dung bao gồm StaffScope, bộ lọc, lazy-load/cache/cancellation, sáu tab, công thức doanh thu/COGS, trạng thái widget và troubleshooting.

## DANH SÁCH THAY ĐỔI CHÍNH

| Nhóm | Thay đổi | Mức ảnh hưởng |
|---|---|---|
| Unit conversion | DTO dùng chung, catalog đơn vị, server conversion | Cao |
| Inventory document | Base quantity, price semantics, display/preflight | Cao |
| Negative settings | UI chuyên biệt, selected unit, server authorization | Cao |
| Application permission | Requirement, handler, policies, data migration | Cao |
| Login/Launcher | Redirect theo StaffId và permission cards | Cao |
| Dashboard | Guide Markdown và Razor | Thấp |

## TEST SUMMARY VÀ NGHIỆM THU

- Application build: `PASS` (0 error; warning hiện hữu của solution vẫn còn).
- Unit/static tests không phụ thuộc SQL Server: `1024/1024 PASS` (bao gồm acceptance test `1 l -> 1000 ml`).
- EF migration script generation: `PASS`; script chỉ chứa DML permission và migration history, không có schema kho.
- SQL analytics integration: `BLOCKED_BY_ENVIRONMENT` nếu SQL Server test instance tiếp tục trả provider error 26; không chạy trên database vận hành.
- Acceptance bắt buộc trước deploy: apply migration permission, kiểm tra account override deny, thử direct URL 403, thử `g/kg`, `ml/l`, forged `BaseQuantity`, giá FIFO và preflight display.

## BACKWARD COMPATIBILITY

- Không sửa Stored Procedures và schema kho.
- Payload preflight cũ vẫn còn các base fields.
- `LoginResponseDto.Role`, `RequireAdminPanelAccess` và permission administration hiện tại vẫn tồn tại.
- Phase 4 chỉ thay destination mặc định sau login của nhân viên; local `returnUrl` và customer Home được giữ nguyên.
