Hãy đọc và phân tích toàn bộ project hiện tại của tôi, bao gồm source code, database, API, authentication/authorization, business logic và các module chính.

Mục tiêu: tạo nội dung cho bài thuyết trình bảo vệ đồ án, ngắn gọn, dễ hiểu và thể hiện rõ những phần tôi đã trực tiếp xây dựng.

Yêu cầu:

1. Tóm tắt dự án:

* Dự án giải quyết vấn đề gì?
* Đối tượng sử dụng là ai?
* Các chức năng/module chính.
* Kiến trúc tổng thể: Frontend → API/Backend → Database.

2. Chọn 3–5 nghiệp vụ quan trọng nhất để trình bày.
   Với mỗi nghiệp vụ, trình bày theo format:

* Người dùng muốn làm gì?
* Quy trình xử lý từ UI → API → Backend → Database → Response.
* API chính tham gia, chỉ nêu API quan trọng, không liệt kê CRUD dư thừa.
* Validation, authentication, authorization/phân quyền.
* Business logic hoặc trường hợp đặc biệt.
* Phần kỹ thuật tôi đã thực hiện.
* Vấn đề khó và cách tôi giải quyết.

Ưu tiên các nghiệp vụ có:

* nhiều role,
* nhiều trạng thái,
* phân quyền,
* xử lý dữ liệu phức tạp,
* dashboard/report,
* hoặc logic vượt ngoài CRUD đơn giản.

3. Tạo nội dung bài thuyết trình theo từng slide.

Mỗi slide gồm:

* Tiêu đề.
* 3–5 bullet ngắn để đặt lên slide.
* Sơ đồ luồng nếu cần.
* Lời thoại khoảng 30–60 giây để tôi trình bày.
* Không viết quá nhiều chữ lên slide.

Đặc biệt phải có các slide:

* Tổng quan bài toán.
* Kiến trúc hệ thống.
* Các role và phân quyền.
* Quy trình nghiệp vụ quan trọng.
* API và cách backend xử lý.
* Database/data flow nếu cần.
* Những phần tôi trực tiếp thực hiện.
* Khó khăn và cách giải quyết.
* Demo flow.
* Kết luận và hướng phát triển.

4. Khi trình bày API, không đọc danh sách endpoint.
   Hãy giải thích theo nghiệp vụ theo format:

Người dùng → Frontend → API → Authentication → Authorization → Validation → Business Logic → Database → Response.

Chỉ ghi tên/method endpoint khi nó giúp làm rõ luồng.

5. Phân biệt rõ:

* CRUD thông thường.
* Business logic thực sự.
* Phần kỹ thuật đáng nói khi bảo vệ đồ án.

Nếu phát hiện logic chưa hợp lý hoặc lỗi trong project, hãy chỉ rõ nhưng không tự nhận đó là tính năng hoàn chỉnh.

6. Tạo danh sách câu hỏi hội đồng có khả năng hỏi.

Chia thành:

* Câu hỏi tổng quan dự án.
* Câu hỏi nghiệp vụ.
* API/REST.
* Authentication.
* Authorization/phân quyền.
* Database.
* Frontend ↔ Backend.
* Security.
* Performance.
* Error handling.
* Testing.
* Deployment.
* Các quyết định thiết kế.
* Câu hỏi kiểu “Tại sao em làm như vậy?”
* Câu hỏi phản biện “Nếu hệ thống có nhiều người dùng hơn thì sao?”
* Câu hỏi kiểm tra xem sinh viên có thực sự làm project hay không.

Với mỗi câu hỏi:

* Viết câu trả lời mẫu ngắn khoảng 20–40 giây.
* Trả lời dựa đúng vào code hiện tại.
* Không bịa tính năng project chưa có.
* Nếu project còn hạn chế, trả lời thẳng hạn chế hiện tại và hướng cải thiện.

7. Cuối cùng tạo một mục:

“10 câu hội đồng dễ hỏi nhất”

Mỗi câu bao gồm:

* Câu hỏi.
* Ý chính bắt buộc phải trả lời.
* Câu trả lời mẫu tự nhiên, giống sinh viên đang bảo vệ, không giống đọc tài liệu.

Ưu tiên cách diễn đạt:
“Ở phần này em phụ trách…”
“Em lựa chọn cách này vì…”
“Backend của em xử lý…”
“Điểm em cần giải quyết ở nghiệp vụ này là…”
“Hiện tại hệ thống đang…, nếu phát triển thêm em sẽ…”

Không được viết kiểu marketing hoặc phóng đại năng lực của hệ thống.
