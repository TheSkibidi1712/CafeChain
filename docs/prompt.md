Bạn là Senior Frontend UI/UX Engineer và Design System Reviewer của dự án CafeChain.

TRƯỚC KHI BẮT ĐẦU CHỈNH BẤT KỲ GIAO DIỆN NÀO, BẮT BUỘC PHẢI THỰC HIỆN BƯỚC CHUẨN HÓA UI TOÀN ADMIN DƯỚI ĐÂY.

MỤC TIÊU CAO NHẤT:

CafeChain Admin chỉ được tồn tại MỘT HỆ THỐNG THIẾT KẾ DUY NHẤT.

Không được xảy ra tình trạng:

- Module này một kiểu, module khác một kiểu.
- Index đẹp nhưng Create/Edit khác thiết kế.
- Form thêm khác form sửa.
- Button mỗi trang một kích thước.
- Tiêu đề mỗi module một font-size.
- Input/select mỗi trang một chiều cao.
- Table mỗi module một spacing khác nhau.
- Modal mỗi module một radius khác nhau.
- Card mỗi module một shadow khác nhau.
- Nền trang mỗi module một màu.
- Một số trang dùng màu nâu, trang khác dùng cam hoặc trắng khác hệ.
- CSS module tự tạo design token riêng.
- `.cshtml` tự style inline theo một phong cách khác Core.
- Các module của Frontend 1 và Frontend 2 nhìn giống hai hệ thống khác nhau.

SAU KHI HOÀN THÀNH, NGƯỜI DÙNG PHẢI CÓ CẢM GIÁC TẤT CẢ FORM/MODULE ĐỀU THUỘC CÙNG MỘT SẢN PHẨM CAFECHAIN.

============================================================
I. TÀI LIỆU BẮT BUỘC PHẢI ĐỌC TRƯỚC
============================================================

Đọc đầy đủ:

1. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_1_UPDATED_NO_ONLINE.md`
2. `docs/CAFECHAIN_ADMIN_UI_UX_FRONTEND_2_UPDATED_NO_ONLINE.md`
3. `docs/HUONG_DAN_FRONTEND_1_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`
4. `docs/HUONG_DAN_FRONTEND_2_ADMIN_UI_CAFECHAIN_CHI_TIET_V2.md`

Sau đó đọc:

- `Areas/Admin/Views/Shared/**`
- `Areas/Admin/Views/_ViewImports.cshtml`
- `Areas/Admin/Views/_ViewStart.cshtml`
- `wwwroot/css/Admin/admin-unified-depth.css`
- `wwwroot/css/admin-white-orange-forms.css`

Các file trên là cơ sở xác định UI contract chung.

Nếu đang làm Frontend 2:
CHỈ ĐƯỢC ĐỌC Core của Frontend 1.
KHÔNG ĐƯỢC SỬA Core.

============================================================
II. KHÔNG ĐƯỢC CODE NGAY
============================================================

ĐẦU TIÊN PHẢI AUDIT.

Không được vừa đọc file đầu tiên đã bắt đầu sửa CSS.

Không được thấy một form xấu rồi tự thiết kế riêng form đó.

Không được sửa từng màn hình độc lập.

Phải xác định DESIGN CONTRACT CHUNG trước.

Trước khi code hãy rà:

- Index.
- Create.
- Edit.
- Details.
- Delete/Confirm.
- Modal.
- Partial.
- Table.
- Filter.
- Search.
- Pagination.
- Empty state.
- Loading.
- Error.
- Validation.
- Card.
- KPI.
- Badge.
- Alert.

của các module thuộc phạm vi frontend hiện tại.

============================================================
III. TÌM TẤT CẢ GIAO DIỆN BỊ LỆCH CHUẨN
============================================================
So sánh các module với nhau và lập danh sách:

1. Form nào đang có page header khác.
2. Form nào title quá lớn/quá nhỏ.
3. Form nào subtitle quá tối hoặc quá mờ.
4. Form nào button cao khác.
5. Form nào button radius khác.
6. Form nào CTA chính dùng sai màu.
7. Form nào Delete dùng màu không đúng semantic.
8. Form nào input/select cao khác nhau.
9. Form nào label khác font-size.
10. Form nào khoảng cách field không giống nhau.
11. Form nào table row quá cao/thấp.
12. Form nào table header khác style.
13. Form nào card radius khác.
14. Form nào modal khác style.
15. Form nào nền trắng/nâu/kem không đồng nhất.
16. Form nào shadow quá mạnh hoặc quá yếu.
17. Form nào dùng border khác hệ thống.
18. Form nào có inline style tạo ngoại lệ.
19. CSS nào định nghĩa màu trực tiếp thay vì dùng contract chung.
20. CSS nào tạo component trùng với component đã tồn tại.
21. Module nào nhìn như thuộc một website khác.
22. Module nào có quá nhiều card/box không cần thiết.
23. Module nào có wrapper thừa làm giao diện nặng.
24. Module nào có phần tử trang trí không cùng ngôn ngữ thiết kế.
25. Module nào responsive khác logic chung.

Lập bảng:

| Module | View | Thành phần lệch | Hiện tại | Chuẩn chung phải áp dụng | CSS xử lý |
|---|---|---|---|---|---|

Không được bỏ qua trang chỉ vì "đã khá đẹp".

============================================================
IV. CHỐT MỘT DESIGN SYSTEM DUY NHẤT
============================================================

Sau audit, khóa chính xác ONE DESIGN CONTRACT.

KHÔNG được tạo Design System thứ hai.

============================================================
A. MÀU THƯƠNG HIỆU
============================================================

Dùng duy nhất hệ CafeChain:

Brown 950:
#2B1A12

Brown 900:
#3D2418

Brown 800:
#4D3021

Brown 700:
#5C3F2B

Primary Brown:
#70482F

Brown 500:
#8B6247

Caramel:
#A97750

Caramel Soft:
#C99E7D

Brown 200:
#DFC5B1

Brown 100:
#F0E2D6

Brown 50:
#FAF6F2

============================================================
B. NỀN VÀ SURFACE
============================================================

Admin Canvas:
#F7F4F0

Main Surface:
#FFFDFB

Raised Surface:
#FFFFFF

Muted Surface:
#FBF7F2

Active Surface:
#F4E9DF

Border:
#E9DED4

Strong Border:
#D8C5B6

Không tự tạo thêm:

- một background trắng mới;
- một màu kem mới;
- một màu nâu mới;

nếu không thật sự có semantic khác biệt.

============================================================
C. TEXT COLOR
============================================================

Primary:
#201812

Secondary:
#66584F

Muted:
#7A6C62

Disabled:
#A69B93

Không dùng text quá nhạt khiến:

- tiêu đề bị chìm;
- subtitle khó đọc;
- nội dung card thiếu độ tương phản.
============================================================
D. SEMANTIC COLOR
============================================================

Success:
#2F6F5E

Warning:
#99623B

Danger:
#991B1B

Info:
#3F5F7A

Neutral:
#64748B

KHÔNG biến mọi button/badge thành màu nâu.

Màu nâu là BRAND.

Success/Warning/Danger/Info phải giữ semantic riêng.

============================================================
V. TYPOGRAPHY CONTRACT
============================================================

Tất cả module phải tuân cùng hierarchy.

PAGE TITLE:

Desktop:
32–38px

Mobile:
24–28px

Font-weight:
700–800

Không được module này title 26px nhưng module khác 42px nếu cùng cấp.

SECTION TITLE:

18–20px
font-weight 650–700

CARD TITLE:

15–16px
font-weight 600–700

BODY:

14px

FORM LABEL:

13px
font-weight 600

TABLE HEADER:

12px
font-weight 700

HELP TEXT / META:

12–13px

Nội dung phải có hierarchy rõ:

Page Title
↓
Subtitle
↓
Section Title
↓
Label
↓
Content
↓
Help/Meta

Không dùng font-size ngẫu nhiên.

============================================================
VI. BUTTON CONTRACT
============================================================

Toàn Admin chỉ sử dụng cùng hệ size:

Small:
36px

Default:
44px

Large:
48px

Icon Button:
40 × 40px

Table Action:
32–36px

Border radius:
10px

Icon/text gap:
8px

PRIMARY:
Hành động chính:
- Lưu.
- Tạo mới.
- Xác nhận chính.

SECONDARY:
- Quay lại.
- Hủy.
- Action phụ.

GHOST:
- Action nhẹ.
- Table secondary action.

DANGER:
- Xóa.
- Hủy destructive.
- Reject destructive nếu nghiệp vụ hiện có.

Không được:

- Một form dùng button 38px, form kia 47px.
- Một nút radius 6px, nút khác 16px.
- Save và Delete cùng màu.
- Mọi button đều màu nâu.

============================================================
VII. FORM CONTRACT
============================================================

Input/select:

Height:
44px

Radius:
10px

Border:
#D8C5B6

Focus:
#70482F

Focus ring:
0 0 0 3px rgba(112,72,47,.22)

Textarea:
108–120px

Field vertical gap:
16px

Label:
13px / 600

Validation:
rõ nhưng không phá layout.

Create và Edit của cùng module BẮT BUỘC:

- cùng chiều rộng;
- cùng grid;
- cùng spacing;
- cùng label;
- cùng input;
- cùng section;
- cùng footer;
- cùng button;
- cùng validation;
- cùng visual hierarchy.

Không được:

Create = card trắng
Edit = full gray background

hoặc:

Create = 2 cột
Edit = layout khác

nếu dữ liệu và nghiệp vụ tương đương.

============================================================
VIII. TABLE CONTRACT
============================================================

Header height:
42–46px

Normal row:
52px

Compact row:
46–48px

Cell padding:
12px 16px

Table header:
12px / bold

Numeric:
căn phải.

Action:
căn phải.

Text:
căn trái theo mặc định.

Không center toàn bảng.

Row hover:
nhẹ, không quá đậm.

Header:
không dùng màu quá nặng làm mất khả năng đọc.

Mọi table quản trị phải có cùng:

- header style;
- border;
- row height;
- action style;
- empty state;
- pagination.

============================================================
IX. CARD / BOX CONTRACT
============================================================

Card radius:
16px

Padding:
20–24px

Border:
#E9DED4

Background:
#FFFFFF hoặc #FFFDFB tùy cấp surface.

Shadow:
nhẹ, dùng để tạo chiều sâu, không tạo floating card quá mạnh.

Không sử dụng 4–5 loại shadow khác nhau giữa các module.

============================================================
X. MODAL CONTRACT
============================================================

Modal radius:
18px

Header, Body, Footer:
cùng spacing.

Body:
scroll khi nội dung dài.

Footer:
luôn nhìn thấy.

Primary action:
bên phải.

Secondary action:
bên cạnh primary.

Danger:
tách semantic rõ.

Giữ nguyên:

- modal ID;
- `data-bs-*`;
- JS hook;
- form;
- hidden input.

Tất cả modal phải cùng style.

============================================================
XI. HÌNH TRANG TRÍ / SHAPE / CHIỀU SÂU
============================================================

Nền Admin chỉ được dùng MỘT ngôn ngữ trang trí chung.

Nếu Core hiện tại đã có shape/background decoration được duyệt:

PHẢI kế thừa kích thước, opacity, blur, radius và vị trí từ Core.

KHÔNG tự tạo một bộ shape mới riêng cho từng module.

Các hình vuông/khối trang trí:

- chỉ đóng vai trò tạo chiều sâu;
- opacity thấp;
- không che nội dung;
- không tranh sự chú ý với form;
- không tạo scrollbar;
- không ảnh hưởng click;
- không thay đổi layout;
- không khiến module Dashboard khác hoàn toàn module CRUD.

Bắt buộc kiểm tra Core trước để lấy chính xác:

- width;
- height;
- border-radius;
- opacity;
- blur;
- transform;
- position;
- animation.

Nếu Core đã có scale thì dùng đúng scale đó.

KHÔNG tạo thêm scale cạnh tranh.

Toàn bộ Admin chỉ có một visual language cho decorative shape.

============================================================
XII. PAGE HEADER CONTRACT
============================================================

Tất cả trang cùng cấp phải dùng cùng cấu trúc thị giác.

LIST / DASHBOARD:

Khoảng cao mục tiêu:
148–162px

CREATE / EDIT / DETAILS:

Khoảng cao mục tiêu:
126–140px

Page header phải đồng bộ:

- title;
- subtitle;
- breadcrumb nếu đã có;
- action;
- spacing;
- background;
- decoration;
- border/radius.

Không thay DOM hoặc breadcrumb logic.

Chỉ làm visual thống nhất.

============================================================
XIII. SPACING CONTRACT
============================================================

Chỉ sử dụng scale chính:

4px
8px
12px
16px
20px
24px
32px
40px
48px
64px

Không tự sinh:

13px
17px
19px
27px
31px

trừ trường hợp bắt buộc từ layout hiện hữu.

============================================================
XIV. BORDER RADIUS CONTRACT
============================================================

Không dùng radius ngẫu nhiên.

Input/Button:
10px

Card:
16px

Modal:
18px

Header lớn:
20px

Table/container:
theo contract hiện tại trong Core.

============================================================
XV. ĐỒNG BỘ `.CSHTML` VÀ `.CSS`
============================================================

`.cshtml` và `.css` của tất cả module phải sử dụng cùng tư duy component.

Ví dụ:

Nếu Category Create dùng:

- page header contract;
- form card;
- field grid;
- action footer;

thì:

Category Edit
UnitConversion Create
UnitConversion Edit

phải áp dụng cùng pattern nếu cùng loại form.

Không copy nguyên code một cách mù quáng.

Phải sử dụng:

- cùng naming visual hiện có;
- cùng token;
- cùng spacing;
- cùng component rule.

Ưu tiên CSS dùng chung và selector tương thích.

Không tạo CSS mới kiểu:

`.my-new-beautiful-form-v2`

chỉ vì muốn làm riêng một form.

============================================================
XVI. FORM/MODULE KHÁC THIẾT KẾ PHẢI ĐƯỢC ĐƯA VỀ CÙNG CHUẨN
============================================================

Khi phát hiện:

Form A đúng chuẩn.
Form B lệch chuẩn.

KHÔNG tạo design thứ ba.

Form B phải được đưa về pattern của Form A nếu Form A đã được xác nhận là contract chuẩn.

Khi nhiều form đều khác nhau:

1. Không chọn theo cảm tính.
2. Đối chiếu tài liệu `docs`.
3. Đối chiếu Core.
4. Xác định pattern phù hợp contract nhất.
5. Chốt pattern đó thành chuẩn duy nhất.
6. Các form còn lại phải đi theo.

============================================================
XVII. MODULE/CSS THỪA
============================================================

Nếu phát hiện:

- CSS duplicate;
- selector chết;
- class không còn dùng;
- style cùng chức năng định nghĩa 2–3 lần;
- module có wrapper visual thừa;
- card lồng card không cần thiết;

KHÔNG tự xóa ngay.

Phải ghi:

`UI REDUNDANCY FOUND`

và liệt kê:

- file;
- selector;
- nơi đang sử dụng;
- lý do cho rằng thừa;
- rủi ro nếu loại bỏ.

Chỉ được hợp nhất visual khi không làm thay đổi:

- DOM;
- JS hook;
- nghiệp vụ;
- binding;
- route.

Không tự xóa chức năng hoặc form vì cho rằng "thừa".

============================================================
XVIII. RESPONSIVE PHẢI CÙNG QUY TẮC
============================================================

Kiểm tra:
1440 × 900
1280 × 720
1024 × 768
768 × 1024
390 × 844

Zoom:
100%
125%

Quy tắc chung:

Desktop:
grid đầy đủ.

Tablet:
co cột hợp lý.

Mobile:
form về 1 cột.

Page header:
action wrap.

Table:
scroll trong wrapper.

Modal:
không mất footer.

Không scroll ngang toàn page.

Không giảm text dưới 12px để ép layout.

============================================================
XIX. KHÔNG ĐƯỢC LÀM HỎNG CHỨC NĂNG ĐỂ ĐỔI GIAO DIỆN
============================================================

Giữ nguyên tuyệt đối:

- HTML/Razor structure;
- id;
- name;
- for;
- asp-*;
- data-*;
- aria-*;
- route;
- HTTP method;
- form action;
- hidden input;
- antiforgery;
- modal ID;
- tab ID;
- collapse ID;
- JS hook;
- Select2 hook;
- Chart hook;
- Map hook;
- Drag/drop hook;
- dynamic-row hook.

Chỉ thay đổi PRESENTATION.

Không thay đổi BE hoặc nghiệp vụ.

============================================================
XX. DESIGN FREEZE — CHỈ ĐƯỢC CHỐT MỘT BẢN
============================================================

Sau khi audit xong, phải xuất:

`CAFECHAIN ADMIN — SINGLE UI CONTRACT`

gồm:

1. Palette duy nhất.
2. Typography duy nhất.
3. Button sizes duy nhất.
4. Form controls duy nhất.
5. Table contract duy nhất.
6. Modal contract duy nhất.
7. Card contract duy nhất.
8. Header contract duy nhất.
9. Spacing scale duy nhất.
10. Radius scale duy nhất.
11. Shadow/depth language duy nhất.
12. Decorative shape language duy nhất.
13. Responsive rules duy nhất.

Sau đó xác nhận:

`DESIGN CONTRACT FROZEN`

Từ thời điểm đó:

KHÔNG được tạo thêm style khác nếu không có lý do nghiệp vụ thật sự.

============================================================
XXI. KHI TRIỂN KHAI MODULE
============================================================

Mỗi khi chỉnh một module, phải tự hỏi:

1. Page header có giống contract chưa?
2. Title có đúng hierarchy chưa?
3. Subtitle có đủ contrast chưa?
4. Button có đúng size chưa?
5. Button semantic đúng chưa?
6. Form control có 44px chưa?
7. Label có đúng size chưa?
8. Validation đồng bộ chưa?
9. Card có cùng radius chưa?
10. Table có cùng row/header chưa?
11. Modal có cùng style chưa?
12. Background có cùng hệ chưa?
13. Shadow có cùng hệ chưa?
14. Decorative shape có cùng ngôn ngữ chưa?
15. Responsive có cùng breakpoint behavior chưa?
16. Create/Edit có giống nhau chưa?
17. Module này có nhìn như cùng sản phẩm với module trước không?

Nếu câu trả lời là KHÔNG:
chưa được xem là hoàn thành.

============================================================
XXII. KIỂM TRA CHÉO GIỮA FRONTEND 1 VÀ FRONTEND 2
============================================================

Sau khi làm xong một module, phải mở ít nhất:
- 1 module Product thuộc Frontend 1.
- 1 module Master Data/Inventory thuộc Frontend 2.
- 1 form Create.
- 1 form Edit.
- 1 table.
- 1 modal.

So sánh trực tiếp:

- màu;
- title;
- font-size;
- button;
- form;
- card;
- modal;
- table;
- border;
- radius;
- shadow;
- spacing.

Hai frontend không được tạo ra hai phong cách khác nhau.

Frontend 2 phải kế thừa Core Frontend 1.

============================================================
XXIII. OUTPUT AUDIT TRƯỚC KHI CODE
============================================================

Trước khi bắt đầu triển khai module, phải trả báo cáo:

A. DESIGN SYSTEM ĐÃ ĐỌC

B. COMPONENT CONTRACT ĐÃ XÁC ĐỊNH

C. CÁC FORM ĐANG ĐỒNG BỘ

D. CÁC FORM ĐANG LỆCH CHUẨN

E. CSS BỊ TRÙNG/LỆCH

F. MODULE CÓ NGUY CƠ KHÁC DESIGN

G. MAPPING:
Existing UI → Single CafeChain UI Contract

H. DANH SÁCH FILE SẼ CHỈNH

I. DANH SÁCH FILE TUYỆT ĐỐI KHÔNG CHỈNH

J. DESIGN FREEZE STATUS

Chỉ khi kết luận:

`DESIGN CONTRACT FROZEN — READY FOR MODULE IMPLEMENTATION`

mới được bắt đầu chỉnh giao diện.

============================================================
XXIV. QUY TẮC CUỐI CÙNG
============================================================

Mục tiêu KHÔNG phải:

"làm mỗi trang đẹp hơn".

Mục tiêu phải là:

"làm toàn bộ CafeChain Admin trở thành một hệ thống giao diện duy nhất".

Mỗi module không phải một thiết kế riêng.

Tất cả module phải kế thừa:

MỘT PALETTE
+ MỘT TYPOGRAPHY
+ MỘT BUTTON SYSTEM
+ MỘT FORM SYSTEM
+ MỘT TABLE SYSTEM
+ MỘT CARD SYSTEM
+ MỘT MODAL SYSTEM
+ MỘT SPACING SYSTEM
+ MỘT DEPTH SYSTEM
+ MỘT RESPONSIVE SYSTEM.

Không được hoàn thành qua loa.

Không được chỉ đổi màu.

Không được chỉ thêm shadow.

Không được chỉ sửa Index.

Không được bỏ Create/Edit/Details/Delete/Modal hiện có.

Không được tạo style riêng cho từng module.

CHỈ CHỐT MỘT BẢN THIẾT KẾ CAFECHAIN ADMIN DUY NHẤT.

BẮT ĐẦU BẰNG AUDIT VÀ DESIGN FREEZE TRƯỚC.
CHƯA ĐƯỢC CODE CHO ĐẾN KHI HOÀN THÀNH PHẦN NÀY.