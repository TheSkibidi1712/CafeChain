# UI_LANGUAGE_AND_TEST_SCOPE_RULE.md
## Quy tắc bắt buộc khi agent chỉnh sửa bất kỳ form hoặc giao diện nào trong CafeChain

Áp dụng cho mọi task có thay đổi:

```text
form
modal
drawer
tab
bảng dữ liệu
trang danh sách
trang chi tiết
thông báo
audit/history
workflow người dùng
```

Rule này phải được đọc cùng:

```text
AGENT_TASK_RULES.md
Rule.md
RULES.md
FIX.md
.agents/skills/SkillTest/SKILL.md
```

---

# 1. Mục tiêu

Mọi nội dung hiển thị cho người dùng nghiệp vụ phải:

```text
dễ hiểu
đúng nghiệp vụ
nhất quán bằng tiếng Việt
không lộ tên code
không lộ enum/raw key
không lộ dữ liệu kỹ thuật
```

Agent không được chỉ sửa chức năng hoặc layout rồi bỏ qua:

```text
từ tiếng Anh
tên biến
enum
status thô
raw JSON
GUID
Base64
exception kỹ thuật
localization key chưa resolve
```

---

# 2. Phạm vi bắt buộc phải rà soát

Khi chỉnh sửa bất kỳ màn hình nào, agent MUST kiểm tra toàn bộ nội dung người dùng có thể nhìn thấy trong phạm vi đó:

```text
Tiêu đề trang
Tiêu đề form
Breadcrumb
Tên tab
Tên section
Nhãn trường
Placeholder
Helper text
Tooltip
Tên cột
Badge
Trạng thái
Nút thao tác
Menu ngữ cảnh
Empty state
Loading state
Validation message
Toast
Business error
Dialog xác nhận
Audit/history
Text từ enum/status
Text trả về từ backend
Export/PDF nếu thuộc cùng workflow
```

Nếu phát hiện từ chưa được mapping trong cùng form hoặc cùng workflow:

```text
phải sửa luôn
```

Nếu nằm ngoài module và không ảnh hưởng task hiện tại:

```text
ghi issue riêng kèm evidence
không mở scope vô hạn
```

---

# 3. Nội dung không được xuất hiện trên UI nghiệp vụ

Agent MUST NOT để lại:

```text
Tên class
Tên entity kỹ thuật
Tên enum
Tên biến
Tên property
Tên API field
Tên database field
Tên event
Tên command
Raw localization key
Raw JSON
GUID
Hash
Base64
RowVersion
Stack trace
Exception tiếng Anh
Unicode escape
Enum.ToString() chưa mapping
```

Ví dụ phải loại bỏ hoặc mapping:

```text
PreparedItem
DRINK_RECIPE
ACTIVE_POLICY
Legacy
PurchaseMode
RowVersion
WarningFingerprint
MatchedSignalsJson
IngredientSupplier
PhysicalUnitConversionRegistry
Contract theo DrinkSize
```

---

# 4. Quy tắc Việt hóa

## 4.1 Thuật ngữ nghiệp vụ

Phải Việt hóa đúng ngữ cảnh.

Ví dụ:

```text
Supplier
→ Nhà cung cấp

Purchase Advice
→ Đề nghị mua hàng

Prepared Item
→ Bán thành phẩm

Lead Time
→ Thời gian giao hàng dự kiến

Primary Supplier
→ Nhà cung cấp chính
```

Không dịch máy móc chỉ dựa trên tên biến.

Ví dụ `Primary` có thể là:

```text
Nguồn cung chính
Liên hệ chính
Quy cách chính
Giá mặc định
```

Phải inspect business contract trước khi chọn bản dịch.

## 4.2 Thuật ngữ chuyên môn được giữ kèm tiếng Việt

Có thể giữ thuật ngữ phổ biến nhưng lần xuất hiện chính phải giải thích:

```text
Giá vốn theo nhập trước - xuất trước (FIFO)
Biên lợi nhuận gộp (Margin)
Tỷ lệ cộng giá (Markup)
Định mức nguyên liệu (BOM)
Số lượng đặt tối thiểu (MOQ)
Điểm bán hàng (POS)
```

Không chỉ hiển thị:

```text
FIFO
Margin
Markup
BOM
MOQ
```

mà không có nghĩa tiếng Việt.

## 4.3 Giá trị được giữ nguyên

Không dịch tùy tiện:

```text
Tên thương hiệu
Tên sản phẩm
Mã chứng từ
Mã SKU
Email
URL
Mã số thuế
kg
g
ml
L
VND
```

Nhưng phải đặt trong ngữ cảnh có nhãn tiếng Việt rõ ràng.

---

# 5. Nguồn mapping chuẩn

Ưu tiên theo thứ tự:

```text
1. Localization/resource hiện có trong repository
2. Thuật ngữ đã thống nhất trong module
3. Business contract hoặc quyết định Owner đã chốt
4. Mapping tập trung mới
```

Không dùng nhiều tên cho cùng một entity.

Ví dụ không dùng lẫn:

```text
Nhà cung ứng
Nhà cung cấp
Đối tác cung ứng
```

nếu đều cùng chỉ `Supplier`.

Không hard-code cùng một bản dịch rải rác trong nhiều file nếu repository đã có resource hoặc mapping tập trung.

Các enum, status, action và business error phải có nguồn mapping thống nhất.

---

# 6. Backend message cũng phải được Việt hóa

Không chỉ che lỗi ở frontend.

Agent phải inspect và mapping:

```text
Validation error
Business conflict
Duplicate
Not found
Forbidden
Concurrency conflict
Invalid state
```

Không render trực tiếp:

```text
Duplicate key
Entity not found
InvalidOperationException
Row version conflict
```

Phải trả thông báo nghiệp vụ tiếng Việt, ví dụ:

```text
Nhà cung cấp đã tồn tại.

Dữ liệu đã được người khác cập nhật.
Vui lòng tải lại trước khi tiếp tục.

Bạn không có quyền thực hiện thao tác này.

Không tìm thấy dữ liệu trong phạm vi được phép.
```

---

# 7. Quy tắc cho Audit và lịch sử

Audit dành cho người dùng không được hiển thị:

```text
Raw JSON
GUID
Base64
RowVersion
Fingerprint
Unicode escape
Nhân viên #5
```

Phải hiển thị thành dữ liệu nghiệp vụ:

```text
Tên thao tác
Người thực hiện
Vai trò
Thời gian
Giá trị trước
Giá trị sau
Lý do
```

Ví dụ:

```text
Cập nhật giá gói mua

Người thực hiện: Nguyễn Văn A
Vai trò: Nhân viên kế toán kho
Giá cũ: 160.000 đ
Giá mới: 168.000 đ
Thời gian: 06/08/2026 18:00
```

---

# 8. Static scan bắt buộc

Trước khi báo hoàn thành, agent phải rà các file UI, mapping và response thuộc phạm vi thay đổi để tìm:

```text
enum.ToString()
raw status
raw JSON
tên entity kỹ thuật
localization key chưa resolve
helper text tiếng Anh
toast tiếng Anh
business error tiếng Anh
câu pha trộn tiếng Việt và tiếng Anh
Unicode escape
```

Có thể dùng lệnh phù hợp, ví dụ:

```bash
rg -n "PreparedItem|DRINK_RECIPE|Legacy|RowVersion|WarningFingerprint" <changed-paths>

rg -n "\b[A-Z][A-Z0-9_]{2,}\b" <changed-paths>

rg -n "\\u[0-9a-fA-F]{4}" <changed-paths>
```

Chỉ scan module hoặc phạm vi thay đổi, không scan toàn repository theo thói quen.

---

# 9. Kiểm thử phải tuân thủ SkillTest

Trước khi chạy bất kỳ test nào, agent phải đọc:

```text
.agents/skills/SkillTest/SKILL.md
```

Sau đó comment:

```text
TEST_SCOPE_PLAN

Task:
<task hiện tại>

Changed areas:
- ...

Directly impacted workflows:
- ...

Shared dependencies touched:
- NONE
hoặc
- ...

Selected verification:
1. Isolated:
   <exact command>

2. Affected module:
   <exact command>

3. Integration/contract:
   <exact command hoặc NOT APPLICABLE>

4. Static/build:
   <exact command>

5. Runtime smoke:
   <scenario>

6. Full suite:
   NOT REQUIRED / REQUIRED
   Reason:
   <trigger cụ thể>
```

Không được bắt đầu bằng full suite.

Không chạy test ngoài task chỉ để “cho chắc”.

Chỉ mở rộng full suite khi có trigger rõ trong `SkillTest`, ví dụ:

```text
schema/migration
auth/permission
shared UnitConversion engine
inventory ledger
global routing/state
dependency/test configuration
Owner yêu cầu
```

Nếu đề xuất full suite, phải có:

```text
FULL_SUITE_JUSTIFICATION
```

Nếu test ngoài phạm vi fail:

```text
phân loại
ghi evidence
không tự ý sửa ngoài task
```

---

# 10. Automated checks cho ngôn ngữ UI

Agent phải bổ sung hoặc cập nhật test phù hợp, ví dụ:

```text
<Module>Ui_DoesNotRenderRawEnum
<Module>Ui_DoesNotRenderRawJson
<Module>Ui_UsesVietnameseStatusLabels
<Module>Ui_DoesNotRenderGuidOrBase64
<Module>Ui_DoesNotRenderTechnicalEntityNames
<Module>Ui_LocalizesBackendBusinessErrors
```

Có thể dùng:

```text
component test
page test
integration test
static check
```

Snapshot không được là bằng chứng duy nhất.

---

# 11. Runtime smoke bắt buộc

Agent phải kiểm tra màn hình thật ở tối thiểu các trạng thái phù hợp:

```text
Mặc định
Có dữ liệu
Không có dữ liệu
Đang tải
Validation lỗi
Thành công
Business conflict
Không có quyền
Không tìm thấy
Dialog xác nhận
Audit/history
```

Không chỉ kiểm tra happy path.

Nếu runtime phát hiện từ chưa mapping hoặc nội dung kỹ thuật:

```text
tiếp tục sửa
chạy lại test
chạy lại runtime smoke
không báo DONE khi còn lỗi
```

---

# 12. Báo cáo bắt buộc

Trước khi kết thúc, agent phải trả:

```text
UI_LANGUAGE_AUDIT

Screens/forms inspected:
- ...

Technical terms found and replaced:
- <trước> → <sau>

Raw enum/status mappings added:
- ...

Backend business messages localized:
- ...

Audit/history changes:
- ...

Terms intentionally retained:
- <thuật ngữ> — <lý do>

Static checks:
- <command/result>

Automated tests:
- <command/count/duration/result>

Runtime states verified:
- ...

Remaining unmapped terms:
- NONE
```

Nếu còn từ chưa mapping, phải ghi:

```text
Thuật ngữ
Vị trí
Lý do chưa sửa
Rủi ro
Issue theo dõi
```

Không được báo DONE nếu còn từ chưa mapping mà không giải thích.

---

# 13. Definition of Done

Chỉ được báo hoàn thành khi:

```text
Không còn raw enum/status key trên UI
Không còn tên code/entity/property trên UI nghiệp vụ
Không còn raw JSON/GUID/Base64/Unicode escape
Không còn exception hoặc business error tiếng Anh
Thuật ngữ nghiệp vụ nhất quán
Từ viết tắt được giải thích khi cần
Label/placeholder/helper/validation/toast đều đã rà
Empty/loading/error/success state đã kiểm tra
Automated verification đúng phạm vi đã PASS
Runtime smoke đã PASS
Không còn thuật ngữ chưa mapping trong phạm vi
Commit/push đúng rule của task
Staged state rỗng
```

Kết luận bắt buộc:

```text
ALL_USER_FACING_TERMS_REVIEWED
UNMAPPED_TECHNICAL_TERMS_REMOVED
RAW_ENUMS_AND_STATUS_KEYS_MAPPED
BACKEND_BUSINESS_MESSAGES_LOCALIZED
VIETNAMESE_TERMINOLOGY_CONSISTENT
ACRONYMS_EXPLAINED_WHERE_NEEDED
RAW_JSON_GUID_BASE64_HIDDEN
UI_LANGUAGE_AUTOMATED_CHECKS_PASSED
UI_LANGUAGE_RUNTIME_SMOKE_PASSED
NO_UNEXPLAINED_UNMAPPED_TERMS_REMAIN
TEST_SCOPE_PLAN_FOLLOWED
NO_UNJUSTIFIED_FULL_SUITE_EXECUTED
```
