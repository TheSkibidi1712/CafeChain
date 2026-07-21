# YÊU CẦU REFACTOR APPLAUNCHER VÀ PHÂN TÍCH NGHIỆP VỤ QUẢN LÝ KHO

Hãy đóng vai một **Senior Software Architect và Business Analyst có kinh nghiệm thực tế với hệ thống POS, quản lý kho, mua hàng và chuỗi cửa hàng**.

Bạn hãy đọc và phân tích toàn bộ codebase hiện tại trước khi đề xuất hoặc chỉnh sửa. Mục tiêu là đảm bảo thiết kế mới phù hợp với những gì dự án đã triển khai, tránh xây dựng một luồng nghiệp vụ mới không tương thích với hệ thống hiện tại.

---

## I. NGUYÊN TẮC THỰC HIỆN

1. Phải đọc và phân tích codebase trước khi chỉnh sửa.
2. Ưu tiên nghiệp vụ và cấu trúc đang tồn tại trong dự án.
3. Không tự ý xóa, đổi tên hoặc tạo mới model, enum, bảng, service, repository, controller hay DTO nếu chưa chứng minh được sự cần thiết.
4. Không được tự suy diễn nghiệp vụ chỉ dựa trên mô tả bên dưới.
5. Khi mô tả của tôi khác với code hiện tại:

   * Xác định rõ điểm khác nhau.
   * Giải thích code hiện tại đang vận hành theo luồng nào.
   * Đề xuất phương án thống nhất.
   * Ưu tiên phương án ít ảnh hưởng đến dữ liệu và chức năng đã hoàn thành.
6. Không được sửa code hàng loạt trước khi hoàn thành tài liệu phân tích.
7. Những phần chưa đủ code hoặc thiếu dữ liệu phải được ghi rõ là chưa thể kết luận, không được tự bịa.
8. Tiếp tục tuân thủ kiến trúc của dự án:

   * Controller chỉ gọi Service.
   * Service sử dụng Repository, DTO và ViewModel.
   * Không sử dụng trực tiếp `AppDbContext` trong Controller hoặc Service.
   * Repository chịu trách nhiệm truy vấn dữ liệu.
   * Service quyết định thời điểm transaction và `SaveChangesAsync`.
9. Không làm lại toàn bộ hệ thống kho nếu chức năng hiện tại đã có thể tái sử dụng hoặc mở rộng.

---

# II. REFACTOR APPLAUNCHER

## 1. Vấn đề hiện tại

Khi người dùng mở ứng dụng POS thông qua `AppLauncher`, hệ thống đang tự động khởi động `PrintBridge`.

Việc này được thiết kế do nhầm lẫn. `PrintBridge` hiện chỉ dùng để giả lập hoặc hỗ trợ thử nghiệm việc in tem, không phải thành phần bắt buộc để POS có thể khởi động và hoạt động.

## 2. Yêu cầu chỉnh sửa

Hãy tìm toàn bộ luồng khởi động POS trong `AppLauncher`, bao gồm:

* Nơi gọi hoặc chạy `PrintBridge`.
* Process được khởi tạo.
* Script hoặc file thực thi liên quan.
* Cơ chế kiểm tra trạng thái `PrintBridge`.
* Logic chờ `PrintBridge` sẵn sàng.
* Logic tự động khởi động lại.
* Logic đóng `PrintBridge` khi tắt POS.
* Các cấu hình hoặc biến môi trường liên quan.

Sau đó thực hiện:

1. Loại bỏ việc tự động khởi động `PrintBridge` khi mở POS.
2. POS phải có thể khởi động độc lập mà không phụ thuộc vào `PrintBridge`.
3. Không được làm ảnh hưởng đến các chức năng khác của `AppLauncher`.
4. Không xóa toàn bộ source code của `PrintBridge` nếu nó vẫn được sử dụng để kiểm thử in tem.
5. Nếu cần giữ lại chức năng chạy `PrintBridge`, hãy chuyển nó thành một chức năng thủ công hoặc cấu hình tùy chọn, ví dụ:

   * Nút “Khởi động PrintBridge”.
   * Cấu hình `EnablePrintBridge`.
   * Chỉ chạy trong môi trường development hoặc testing.
6. Kiểm tra các trường hợp:

   * Mở POS khi `PrintBridge` chưa chạy.
   * Mở POS khi `PrintBridge` đang chạy.
   * Tắt POS.
   * Khởi động lại POS.
   * `PrintBridge` bị lỗi hoặc không tồn tại.
7. Không để lỗi `PrintBridge` làm POS không thể khởi động.

## 3. Kết quả cần báo cáo

Liệt kê rõ:

* File nào đã phân tích.
* File nào cần chỉnh sửa.
* Method hoặc đoạn logic nào đang tự khởi động `PrintBridge`.
* Cách xử lý mới.
* Ảnh hưởng của thay đổi.
* Các trường hợp kiểm thử cần thực hiện.

---

# III. PHÂN TÍCH NGHIỆP VỤ MUA HÀNG VÀ NHẬP KHO

## 1. Bối cảnh nghiệp vụ mong muốn

Hệ thống đang được định hướng theo luồng:

```text
Chi nhánh tạo PA
        ↓
Kho tổng tiếp nhận PA
        ↓
Tách hoặc chuyển thành các PO con
        ↓
Phát hiện các PO có thể gộp
        ↓
Gộp thành PO tổng hoặc PO mua hàng phù hợp
        ↓
Gửi đơn cho nhà cung cấp
        ↓
Nhận hàng
        ↓
Tạo phiếu nhập kho
        ↓
Cập nhật tồn kho, công nợ và giá vốn
```

Trong đó:

* `PA` là yêu cầu mua hàng hoặc yêu cầu cung ứng từ chi nhánh.
* `PO` là đơn đặt hàng gửi cho nhà cung cấp.
* Kho tổng tiếp nhận các PA từ nhiều chi nhánh.
* Kho tổng có thể tách một PA thành nhiều PO nếu phải mua từ nhiều nhà cung cấp.
* Nhiều PO con có thể được gộp nếu đáp ứng điều kiện phù hợp.
* Phiếu nhập kho phải được tạo dựa trên hàng thực tế nhận từ PO, không chỉ dựa trên số lượng đặt mua.

Tuy nhiên, phần nghiệp vụ nhập hàng có thể đã được hai developer triển khai theo hai hướng khác nhau.

Vì vậy, bạn phải ưu tiên phân tích nghiệp vụ thực tế đang tồn tại trong codebase trước khi áp dụng luồng trên.

---

## 2. Phạm vi code cần phân tích

Hãy tìm và phân tích toàn bộ thành phần liên quan đến:

* Purchase Request.
* Purchase Approval.
* Purchase Order.
* Purchase Order Detail.
* Supplier.
* Ingredient Supplier.
* Supplier Price.
* Inventory Document.
* Inventory Document Detail.
* Inventory Transaction.
* Inventory Transfer.
* Inventory Cost Layer.
* Inventory Cost Allocation.
* Stock.
* Warehouse.
* Store.
* Goods Receipt.
* Goods Issue.
* Stock Adjustment.
* Stocktake hoặc Inventory Count.
* Cancellation hoặc Void.
* Debt hoặc Supplier Debt.
* Snapshot.
* Request Deduplication.
* Enum trạng thái và loại chứng từ.
* Controller.
* Service.
* Repository.
* DTO.
* ViewModel.
* View.
* JavaScript.
* Seed data.
* Configuration.
* Migration.
* Stored procedure nếu có.

Không chỉ tìm theo đúng tên tiếng Anh nêu trên. Hãy kiểm tra cả những class hoặc chức năng có tên khác nhưng cùng mục đích nghiệp vụ.

---

# IV. PHÂN TÍCH LUỒNG PA → PO → NHẬP KHO

## 1. PA tại chi nhánh

Hãy xác định:

* Ai được tạo PA.
* PA được tạo tại chi nhánh hay kho tổng.
* PA có những trường dữ liệu nào.
* PA yêu cầu nguyên liệu theo đơn vị nào.
* PA có liên kết với cửa hàng, kho, nhân viên và thời gian cần hàng hay không.
* PA có trạng thái nháp, gửi duyệt, đã duyệt, từ chối, xử lý một phần và hoàn tất hay không.
* Có cơ chế duyệt PA hay không.
* Một PA có thể được xử lý nhiều lần hay không.
* Có lưu số lượng đã đặt mua và số lượng còn thiếu hay không.
* Có chống tạo PO trùng từ cùng một PA hay không.
* PA sau khi bị hủy hoặc từ chối có còn được chuyển thành PO hay không.

Hãy chỉ ra luồng thực tế hiện có trong code.

## 2. Chuyển PA thành PO

Hãy xác định:

* PO được tạo trực tiếp hay được tạo từ PA.
* Một PA có thể tạo nhiều PO không.
* Một PO có thể chứa dữ liệu từ nhiều PA không.
* Hệ thống chọn nhà cung cấp bằng tay hay tự động.
* Có so sánh giá nhà cung cấp theo từng nguyên liệu không.
* Có kiểm tra:

  * Giá hiện tại.
  * Đơn vị đóng gói.
  * Số lượng trong một gói.
  * Đơn vị cơ sở.
  * Số lượng tối thiểu.
  * Nhà cung cấp ưu tiên.
  * Thời gian giao hàng.
  * Nhà cung cấp đang hoạt động.
* Có lưu nguồn gốc từng dòng PO đến từ PA nào không.
* Có xử lý trường hợp một nguyên liệu được chia cho nhiều nhà cung cấp không.

## 3. Tách PO con

Hãy phân tích cách hệ thống nên tách PO dựa trên:

* Nhà cung cấp.
* Địa điểm giao hàng.
* Kho nhận hàng.
* Ngày giao dự kiến.
* Loại tiền tệ nếu có.
* Điều khoản thanh toán.
* Chính sách giá.
* Đơn vị đóng gói.
* Chi nhánh yêu cầu.
* Nhóm nguyên liệu.

Không được mặc định rằng cứ mỗi PA là một PO.

Hãy xác định tiêu chí tách PO phù hợp với code hiện tại.

## 4. Phát hiện PO trùng hoặc có thể gộp

Hãy định nghĩa rõ hai khái niệm:

### PO bị trùng

PO được xem là bị trùng khi có nguy cơ tạo lặp từ cùng một nguồn nghiệp vụ, ví dụ:

* Cùng PA.
* Cùng dòng PA.
* Cùng nhà cung cấp.
* Cùng nguyên liệu.
* Cùng số lượng.
* Cùng khoảng thời gian tạo.
* Cùng request key hoặc khóa chống trùng.

### PO có thể gộp

PO có thể gộp khi:

* Cùng nhà cung cấp.
* Cùng địa điểm giao hàng hoặc kho nhận.
* Cùng ngày giao hoặc khoảng giao hàng.
* Cùng điều khoản thanh toán.
* Cùng trạng thái chưa gửi hoặc chưa xác nhận.
* Cùng loại tiền tệ.
* Không bị khóa hoặc đã phát sinh nhận hàng.
* Không làm mất khả năng truy vết về PA gốc.

Hãy kiểm tra trong codebase hiện tại đã có:

* Cơ chế phát hiện trùng.
* Request key.
* Unique constraint.
* Idempotency.
* Mapping PA–PO.
* Mapping dòng PA–dòng PO.
* Chức năng gộp PO.
* Chức năng tách PO.

Nếu chưa có, hãy đề xuất thiết kế cụ thể, nhưng chưa tự ý tạo bảng mới trước khi phân tích ảnh hưởng.

## 5. Gộp PO

Khi gộp PO, phải bảo đảm:

1. Không làm mất liên kết với PA gốc.
2. Không cộng trùng số lượng.
3. Không gộp PO đã gửi cho nhà cung cấp hoặc đã nhận hàng, trừ khi nghiệp vụ hiện tại cho phép.
4. Giữ được lịch sử:

   * PO nào đã được gộp.
   * Ai thực hiện.
   * Thời gian thực hiện.
   * Lý do gộp.
5. Các PO nguồn phải chuyển sang trạng thái phù hợp, không được tiếp tục sử dụng để nhập kho lần nữa.
6. PO sau khi gộp phải lưu được tổng số lượng và nguồn yêu cầu từ từng chi nhánh.
7. Việc gộp không được làm sai công nợ hoặc giá nhập.

Hãy mô tả thuật toán hoặc quy trình gộp cụ thể phù hợp với hệ thống hiện tại.

## 6. Tạo phiếu nhập từ PO

Hãy kiểm tra:

* Phiếu nhập được tạo tự động hay thủ công.
* Một PO có thể tạo nhiều phiếu nhập không.
* Có hỗ trợ giao hàng nhiều lần không.
* Có hỗ trợ nhận thiếu, nhận thừa hoặc từ chối hàng không.
* Có đối chiếu số lượng đặt, số lượng đã nhận và số lượng còn lại không.
* Có kiểm tra giá thực nhận với giá PO không.
* Có lưu lô hàng, hạn sử dụng hoặc mã lô không.
* Có cập nhật:

  * Tồn kho.
  * Giao dịch kho.
  * Lớp giá vốn.
  * Phân bổ FIFO.
  * Công nợ nhà cung cấp.
  * Trạng thái PO.
* Có chống xác nhận phiếu nhập hai lần không.
* Có transaction và rollback nếu cập nhật một phần bị lỗi không.

Phiếu nhập phải dựa trên hàng thực nhận. Không được tự động lấy toàn bộ số lượng của PO rồi tăng kho nếu người dùng chưa xác nhận số lượng nhận thực tế.

---

# V. THIẾT KẾ CÁC PHIẾU KHO LIÊN QUAN

Sau khi phân tích codebase, hãy thiết kế hoặc chuẩn hóa nghiệp vụ cho các loại phiếu sau.

## 1. Phiếu nhập kho

Phân tích các trường hợp:

* Nhập từ nhà cung cấp.
* Nhập điều chỉnh tăng.
* Nhập trả hàng từ POS nếu hệ thống hỗ trợ.
* Nhập hàng từ phiếu chuyển kho.
* Nhập do kiểm kê thừa.

Đối với mỗi trường hợp, hãy chỉ rõ:

* Chứng từ nguồn.
* Kho nhận.
* Đối tác.
* Cách cập nhật tồn kho.
* Cách cập nhật giá vốn.
* Cách cập nhật công nợ.
* Trạng thái chứng từ.
* Quyền tạo, sửa, xác nhận và hủy.

## 2. Phiếu xuất kho

Phân tích các trường hợp:

* Xuất bán hàng qua POS.
* Xuất hủy nguyên liệu.
* Xuất điều chỉnh giảm.
* Xuất cho phiếu chuyển kho.
* Xuất do kiểm kê thiếu.
* Xuất trả nhà cung cấp nếu có.

Phải làm rõ:

* Phiếu xuất nào được tạo tự động.
* Phiếu xuất nào do người dùng tạo.
* Thời điểm trừ tồn kho.
* Cách tính giá vốn.
* Cách xử lý kho âm.
* Cách xử lý đơn POS bị hủy hoặc hoàn tiền.
* Cơ chế chống trừ kho hai lần.

## 3. Phiếu hủy

Hãy phân tích xem “Phiếu hủy” trong dự án là:

* Hủy nguyên liệu hỏng.
* Hủy hàng hết hạn.
* Hủy thành phẩm.
* Hủy món đã pha.
* Hủy chứng từ kho.
* Hay chỉ là trạng thái hủy của một phiếu khác.

Không được đánh đồng:

* Hủy chứng từ.
* Xuất hủy hàng hóa.
* Xóa dữ liệu.

Nếu hệ thống cần phiếu hủy riêng, phải xác định:

* Lý do hủy.
* Người đề nghị.
* Người duyệt.
* Kho bị trừ.
* Số lượng.
* Giá trị tổn thất.
* Hình ảnh hoặc bằng chứng nếu có.
* Tác động đến tồn kho và báo cáo.
* Có cho phép khôi phục hay tạo phiếu đảo không.

## 4. Phiếu kiểm kê

Hãy thiết kế luồng:

```text
Tạo đợt kiểm kê
    ↓
Khóa hoặc chốt phạm vi kiểm kê
    ↓
Ghi nhận số lượng hệ thống
    ↓
Nhập số lượng thực tế
    ↓
Tính chênh lệch
    ↓
Duyệt kết quả
    ↓
Tạo phiếu điều chỉnh tăng hoặc giảm
    ↓
Cập nhật tồn kho
```

Phải làm rõ:

* Kiểm kê theo cửa hàng, kho, nhóm nguyên liệu hay toàn bộ kho.
* Có khóa giao dịch trong lúc kiểm kê không.
* Nếu POS vẫn bán hàng thì lấy thời điểm chốt tồn như thế nào.
* Có lưu số lượng hệ thống tại thời điểm bắt đầu không.
* Có bắt buộc người khác duyệt kết quả không.
* Chênh lệch tăng tạo phiếu nào.
* Chênh lệch giảm tạo phiếu nào.
* Có lưu lịch sử điều chỉnh không.
* Có cho phép sửa sau khi đã duyệt không.

## 5. Phiếu chuyển kho

Dự án phải tiếp tục giữ “Phiếu Chuyển Kho” như một nghiệp vụ độc lập, không gộp chung vào nhập nội bộ hoặc xuất nội bộ.

Hãy kiểm tra luồng hiện tại và chuẩn hóa theo hướng:

```text
Tạo yêu cầu chuyển kho
        ↓
Kho nguồn duyệt và xuất hàng
        ↓
Hàng đang vận chuyển
        ↓
Kho đích nhận hàng
        ↓
Xác nhận số lượng thực nhận
        ↓
Hoàn tất chuyển kho
```

Phải phân tích:

* Thời điểm trừ kho nguồn.
* Thời điểm cộng kho đích.
* Trường hợp kho đích nhận thiếu hoặc thừa.
* Trường hợp hàng bị hỏng khi vận chuyển.
* Trường hợp hủy khi chưa xuất.
* Trường hợp hủy sau khi đã xuất.
* Giá vốn chuyển sang kho đích.
* Mapping giữa phiếu chuyển, phiếu xuất và phiếu nhập.
* Có cần tạo hai chứng từ kho con hay chỉ dùng transaction hay không.

---

# VI. PHÂN TÍCH “FORM PHIẾU KHO” CÓ CẦN THIẾT KHÔNG

Hãy tìm form hoặc module đang được gọi là “Phiếu Kho” trong dự án và phân tích mục đích thực tế của nó.

Cần trả lời cụ thể:

1. “Phiếu Kho” là một chứng từ nghiệp vụ độc lập hay chỉ là form dùng chung cho nhiều loại chứng từ?
2. Nó có trùng với:

   * Phiếu nhập.
   * Phiếu xuất.
   * Phiếu điều chỉnh.
   * Phiếu kiểm kê.
   * Phiếu chuyển kho.
3. Nó đang lưu vào một bảng chung như `InventoryDocument` hay một bảng riêng?
4. Các loại phiếu được phân biệt bằng enum hay bằng bảng riêng?
5. Form này có đang chứa quá nhiều nhánh điều kiện làm khó bảo trì không?
6. Có nên:

   * Giữ nguyên form chung.
   * Tách thành các màn hình riêng.
   * Giữ backend chung nhưng tách giao diện.
   * Chỉ dùng làm màn hình tra cứu tổng hợp.
7. Trường hợp nào người dùng thực sự cần mở form “Phiếu Kho”?
8. POS có cần truy cập trực tiếp form này hay không?
9. Nếu bỏ form, chức năng nào sẽ bị mất?
10. Nếu giữ form, cần giới hạn vai trò và quyền truy cập ra sao?

Sau khi phân tích, hãy đưa ra một trong các kết luận rõ ràng:

* Giữ nguyên.
* Giữ backend chung nhưng tách giao diện.
* Chuyển thành màn hình danh sách tổng hợp.
* Loại bỏ vì trùng nghiệp vụ.

Không được kết luận chung chung. Phải dẫn chứng bằng model, controller, service, repository, view và luồng dữ liệu hiện tại.

---

# VII. TÍCH HỢP NGHIỆP VỤ KHO VỚI POS

Hãy phân tích POS đang tác động đến tồn kho như thế nào.

## 1. Khi bán hàng thành công

Kiểm tra:

* POS trừ trực tiếp nguyên liệu hay trừ thành phẩm.
* Có sử dụng công thức đồ uống không.
* Có quy đổi đơn vị không.
* Có trừ topping, size, đá, đường hoặc nguyên liệu tùy chọn không.
* Thời điểm trừ kho:

  * Khi tạo đơn.
  * Khi thanh toán.
  * Khi hoàn tất pha chế.
* Có tạo `InventoryTransaction` hay phiếu xuất kho không.
* Có lưu liên kết với Order hoặc OrderDetail không.
* Có chống trừ kho hai lần không.

## 2. Khi hủy đơn

Phân tích riêng:

* Hủy trước khi pha chế.
* Hủy sau khi đã pha.
* Hủy sau khi thanh toán.
* Hoàn tiền.
* Hủy một món trong đơn.
* Hủy toàn bộ đơn.

Không được mặc định tất cả trường hợp đều cộng lại kho.

Ví dụ:

* Chưa pha chế: có thể hoàn lại phần giữ kho.
* Đã pha chế: nguyên liệu đã tiêu hao, có thể cần ghi nhận xuất hủy thay vì cộng lại kho.
* Thanh toán rồi nhưng hoàn tiền: xử lý doanh thu và xử lý kho là hai nghiệp vụ khác nhau.

## 3. Khi hoàn hoặc đổi món

Hãy xác định:

* Có nhập lại kho hay không.
* Có tạo phiếu hủy không.
* Có tạo giao dịch đảo không.
* Có làm thay đổi giá vốn không.
* Có lưu lý do và nhân viên xử lý không.

## 4. Khi kiểm kê và POS cùng hoạt động

Hãy đề xuất cơ chế đảm bảo:

* Không mất giao dịch POS.
* Không tính sai số lượng hệ thống tại thời điểm kiểm kê.
* Có timestamp hoặc mốc chốt tồn.
* Giao dịch phát sinh sau thời điểm chốt được xử lý riêng.
* Không ghi đè tồn kho trực tiếp nếu chưa tạo chứng từ điều chỉnh.

## 5. Khi kho âm

Phân tích cơ chế hiện tại:

* POS có được bán khi thiếu tồn không.
* Ai có quyền duyệt âm kho.
* Mức âm được phép.
* Có cảnh báo không.
* Có ghi nhận lý do không.
* Sau khi nhập hàng, lượng âm được bù như thế nào.
* Giá vốn tạm tính và giá vốn chính thức được xử lý ra sao.
* Có ảnh hưởng đến FIFO hoặc Cost Layer không.

---

# VIII. TRẠNG THÁI VÀ QUY TẮC CHỨNG TỪ

Hãy lập bảng trạng thái thực tế cho từng loại chứng từ.

Ví dụ tham khảo:

```text
Draft
Submitted
Approved
PartiallyProcessed
Processing
PartiallyReceived
Received
Completed
Rejected
Cancelled
Voided
```

Tuy nhiên, chỉ sử dụng trạng thái phù hợp với codebase hiện tại.

Với mỗi trạng thái, phải mô tả:

* Ai được chuyển trạng thái.
* Điều kiện chuyển.
* Có được sửa dữ liệu hay không.
* Có được xóa hoặc hủy hay không.
* Đã cập nhật tồn kho chưa.
* Đã cập nhật công nợ chưa.
* Có thể tạo chứng từ tiếp theo hay không.

Phân biệt rõ:

* `Cancel`: hủy trước khi nghiệp vụ phát sinh.
* `Void`: vô hiệu hóa sau khi đã phát sinh và cần chứng từ đảo.
* `Delete`: xóa dữ liệu, hiện tại không ưu tiên sử dụng.
* `Reverse`: tạo giao dịch ngược để hoàn tác.

---

# IX. PHÂN QUYỀN

Hãy xác định quyền phù hợp cho từng chức năng dựa trên hệ thống quyền hiện tại.

Tối thiểu cần xem xét:

* Xem PA.
* Tạo PA.
* Gửi PA.
* Duyệt hoặc từ chối PA.
* Tạo PO từ PA.
* Tách PO.
* Gộp PO.
* Gửi PO cho nhà cung cấp.
* Xác nhận nhận hàng.
* Tạo phiếu nhập.
* Xác nhận phiếu nhập.
* Tạo phiếu xuất.
* Xác nhận phiếu xuất.
* Tạo phiếu hủy.
* Duyệt phiếu hủy.
* Tạo kiểm kê.
* Nhập kết quả kiểm kê.
* Duyệt chênh lệch.
* Tạo chuyển kho.
* Duyệt xuất kho nguồn.
* Xác nhận nhận tại kho đích.
* Duyệt âm kho.
* Xem giá nhập và công nợ.

Dự án hiện chưa ưu tiên chức năng delete, vì vậy không cần triển khai hoặc hiển thị quyền delete nếu chưa có nghiệp vụ rõ ràng.

---

# X. YÊU CẦU VỀ TÍNH TOÀN VẸN DỮ LIỆU

Hãy kiểm tra và đề xuất cơ chế cho các vấn đề sau:

1. Chống tạo trùng PA, PO và phiếu kho.
2. Chống xác nhận một chứng từ hai lần.
3. Không cập nhật tồn kho nếu transaction thất bại.
4. Rollback toàn bộ khi một bước trong quá trình xác nhận lỗi.
5. Không cho số lượng âm hoặc bằng 0 tại những nghiệp vụ không cho phép.
6. Quy đổi đơn vị phải nhất quán.
7. Giá nhập phải gắn với thời điểm hiệu lực.
8. Công nợ không được ghi nhận hai lần.
9. Giao dịch kho phải truy vết được chứng từ nguồn.
10. Mọi chứng từ phải có:

    * Người tạo.
    * Người xác nhận.
    * Thời gian tạo.
    * Thời gian xác nhận.
    * Lý do hủy hoặc điều chỉnh.
11. Không cho sửa trực tiếp chứng từ đã xác nhận.
12. Nếu cần sửa, phải hủy đúng quy trình hoặc tạo chứng từ đảo.
13. Không dùng phép gán trực tiếp tồn kho để thay thế lịch sử transaction.

---

# XI. TÀI LIỆU ĐẦU RA

Hãy ghi kết quả phân tích vào:

```text
Doc/FIX.md
```

Nếu `FIX.md` đã có nhiều nội dung hoặc việc ghi thêm khiến tài liệu khó quản lý, hãy tạo một file mới trong thư mục `Doc`, ví dụ:

```text
Doc/INVENTORY_PURCHASING_WORKFLOW.md
```

Không được ghi đè hoặc xóa nội dung cũ trong `FIX.md`.

## Cấu trúc tài liệu bắt buộc

### 1. Hiện trạng codebase

* Các module liên quan.
* Các model chính.
* Các enum.
* Các bảng dữ liệu.
* Luồng hiện tại.
* Những chức năng đã hoàn thành.
* Những chức năng đang làm dở.
* Những phần bị trùng hoặc xung đột giữa hai developer.

### 2. Luồng nghiệp vụ hiện tại

Mô tả bằng từng bước cụ thể, từ lúc chi nhánh tạo yêu cầu đến lúc cập nhật tồn kho.

### 3. Luồng nghiệp vụ đề xuất

Mô tả đầy đủ:

```text
PA → PO con → Kiểm tra trùng → Gộp PO → Nhận hàng → Phiếu nhập → Tồn kho
```

### 4. So sánh hiện trạng và đề xuất

Lập bảng:

| Hạng mục | Code hiện tại | Nghiệp vụ mong muốn | Khoảng thiếu | Phương án xử lý |
| -------- | ------------- | ------------------- | ------------ | --------------- |

### 5. Phân tích từng loại phiếu

* Phiếu nhập.
* Phiếu xuất.
* Phiếu hủy.
* Phiếu kiểm kê.
* Phiếu chuyển kho.
* Phiếu điều chỉnh nếu có.

### 6. Phân tích form Phiếu Kho

Đưa ra kết luận rõ ràng về việc giữ, tách, đổi mục đích hoặc loại bỏ.

### 7. Tích hợp với POS

* Bán hàng.
* Hủy đơn.
* Hoàn tiền.
* Hủy món.
* Xuất hủy.
* Kho âm.
* Kiểm kê khi POS đang hoạt động.

### 8. Trạng thái chứng từ

Lập bảng chuyển trạng thái cho từng loại chứng từ.

### 9. Phân quyền

Lập ma trận vai trò và quyền thao tác.

### 10. Rủi ro dữ liệu

Liệt kê:

* Trừ kho hai lần.
* Nhập kho hai lần.
* PO trùng.
* Gộp sai PO.
* Mất liên kết PA–PO.
* Sai công nợ.
* Sai giá vốn.
* Sai quy đổi đơn vị.
* Race condition.
* Không rollback khi lỗi.

### 11. Danh sách file cần chỉnh sửa

Phân loại theo:

* Model.
* Enum.
* DTO.
* ViewModel.
* Repository.
* Service.
* Controller.
* View.
* JavaScript.
* Configuration.
* Migration.
* Seed data.
* Test.

### 12. Kế hoạch triển khai

Chia thành các giai đoạn nhỏ:

* Giai đoạn 1: Phân tích và thống nhất nghiệp vụ.
* Giai đoạn 2: Chuẩn hóa model, enum và trạng thái.
* Giai đoạn 3: Chuẩn hóa PA và PO.
* Giai đoạn 4: Tạo phiếu nhập từ PO.
* Giai đoạn 5: Chuẩn hóa xuất, hủy và kiểm kê.
* Giai đoạn 6: Tích hợp POS.
* Giai đoạn 7: Kiểm thử và migration dữ liệu.

Mỗi giai đoạn phải nêu:

* File cần sửa.
* Mục tiêu.
* Rủi ro.
* Điều kiện hoàn thành.
* Các test case cần chạy.

---

# XII. CÁCH THỰC HIỆN

Thực hiện theo đúng thứ tự sau:

## Bước 1: Phân tích AppLauncher

Tìm và mô tả chính xác luồng tự động chạy `PrintBridge`.

## Bước 2: Refactor AppLauncher

Bỏ phụ thuộc bắt buộc giữa POS và `PrintBridge`, nhưng giữ khả năng chạy thủ công nếu còn cần kiểm thử.

## Bước 3: Khảo sát code nghiệp vụ kho

Lập danh sách toàn bộ file liên quan đến PA, PO, phiếu kho, tồn kho, công nợ, giá vốn và POS.

## Bước 4: Dựng lại luồng hiện tại từ code

Không dựa vào suy đoán. Phải dẫn chứng bằng class, method và trường dữ liệu.

## Bước 5: So sánh với luồng PA → PO → Kho

Chỉ ra phần nào đã có, phần nào đang thiếu, phần nào bị làm theo hai hướng khác nhau.

## Bước 6: Viết tài liệu thiết kế

Ghi đầy đủ vào `Doc/FIX.md` hoặc file tài liệu mới trong thư mục `Doc`.

## Bước 7: Đề xuất kế hoạch refactor

Chưa sửa hàng loạt nghiệp vụ kho nếu tài liệu chưa chỉ rõ phạm vi và ảnh hưởng.

## Bước 8: Chỉ bắt đầu sửa code nghiệp vụ kho theo từng nhóm nhỏ

Mỗi lần chỉ nên xử lý một nhóm chức năng có liên quan chặt chẽ, ví dụ:

1. PA và trạng thái PA.
2. Mapping PA–PO.
3. Tách và gộp PO.
4. Nhận hàng và tạo phiếu nhập.
5. Phiếu xuất.
6. Phiếu hủy.
7. Kiểm kê.
8. Tích hợp POS.

Không được refactor toàn bộ module kho trong một lần nếu chưa kiểm chứng được dữ liệu và luồng hiện tại.

---

# XIII. ĐỊNH DẠNG PHẢN HỒI

Sau khi hoàn thành, hãy trả về:

1. Tóm tắt hiện trạng.
2. Các lỗi hoặc xung đột nghiệp vụ đã phát hiện.
3. Kết luận về `PrintBridge`.
4. Kết luận về form “Phiếu Kho”.
5. Luồng PA → PO → Nhập kho được đề xuất.
6. Danh sách file đã chỉnh sửa.
7. Danh sách file chỉ phân tích nhưng chưa chỉnh sửa.
8. Đường dẫn tài liệu đã tạo hoặc cập nhật.
9. Những phần chưa thể thực hiện do thiếu code.
10. Danh sách test case cần chạy.

Không chỉ trả lời rằng “đã hoàn thành”. Phải trình bày rõ thay đổi, lý do và ảnh hưởng của từng thay đổi.
