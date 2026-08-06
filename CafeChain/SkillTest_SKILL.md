---
name: skill-test
description: >
  Quy tắc chọn phạm vi kiểm thử cho CafeChain. Ưu tiên test gần nhất,
  mở rộng theo vùng ảnh hưởng thật, không chạy test ngoài task hoặc full suite
  theo thói quen.
version: 2.0.0
language: vi
project: CafeChain
---

# SkillTest — Kiểm thử đúng phạm vi cho CafeChain

## 1. Mục tiêu

Skill này giúp agent phát hiện regression của thay đổi hiện tại nhưng không lãng phí thời gian chạy test ngoài phạm vi.

Mục tiêu:

1. Chạy test gần code và workflow vừa sửa.
2. Mở rộng theo dependency thật.
3. Không sửa lỗi ngoài task chỉ để làm suite xanh.
4. Có đủ bằng chứng trước commit/push.
5. Chỉ chạy full suite khi có trigger rõ.

## 2. Nguyên tắc cứng

- **MUST** lập `TEST_SCOPE_PLAN` trước khi chạy test.
- **MUST** chạy test tái hiện lỗi hoặc test gần code vừa sửa trước.
- **MUST** chạy test module bị ảnh hưởng trực tiếp sau khi isolated test xanh.
- **MUST NOT** chạy toàn bộ Backend, Frontend hoặc monorepo chỉ để “cho chắc”.
- **MUST NOT** chạy test module không có dependency với thay đổi hiện tại.
- **MUST NOT** sửa lỗi ngoài task nếu lỗi đó không chặn luồng nghiệm thu.
- **MUST NOT** gọi test fail là regression nếu chưa có evidence.
- **MUST** đọc summary cuối và exit code.
- **MUST** báo exact command, số test, thời lượng và lý do chọn phạm vi.
- **MUST** giữ diff và staged files đúng scope.
- **SHOULD** ưu tiên runtime smoke đúng scenario Owner hơn test rộng không liên quan.

## 3. TEST_SCOPE_PLAN bắt buộc

Trước khi test, comment:

```text
TEST_SCOPE_PLAN

Task:
<bug/feature>

Changed areas:
- <module/file/service/page>

Directly impacted workflows:
- <workflow>

Shared dependencies touched:
- NONE
hoặc
- <shared dependency>

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
   Reason: <trigger cụ thể>
```

Không bắt đầu bằng full suite khi chưa có kế hoạch này.

## 4. Phân loại phạm vi

### 4.1 Phạm vi hẹp

Ví dụ:

```text
label tiếng Việt
CSS một form
validation cục bộ
DTO mapping
query một trang
controller/service không dùng chung
```

Chạy:

```text
isolated
→ module
→ build phần liên quan
→ runtime smoke
```

Không cần full suite.

### 4.2 Phạm vi liên module có giới hạn

Ví dụ:

```text
Supplier → PA → PO
Restock → PA → PO → Receipt
Menu → BOM → Costing → POS
```

Chạy:

```text
isolated
→ test các module trên luồng
→ integration/contract test
→ build
→ runtime smoke end-to-end
```

Không chạy module ngoài luồng.

### 4.3 Shared/risk cao

Chỉ cân nhắc full suite nếu sửa:

```text
authentication/authorization/middleware
DbContext hoặc schema/migration
UnitConversion engine dùng chung
inventory ledger
shared money/UOM serializer
global routing/state/design system
dependency/framework/test config
shared utility có nhiều consumer
API contract nền tảng
```

Nếu chỉ sửa một consumer của shared service mà không sửa shared service thì không mặc định chạy full suite.

## 5. Bản đồ module CafeChain

### Supplier

Ưu tiên:

```text
Supplier lifecycle
Supplier package/UOM
Supplier Store scope
Duplicate detection
PA/PO supplier selection nếu contract bị chạm
```

Không mặc định chạy POS, WorkShift, Menu hoặc Quản lý đá.

### Restock

Ưu tiên:

```text
Restock create/detail/list
Demand UOM
Source selection
Restock → PA
```

Chỉ mở rộng PO/Receipt khi contract downstream bị chạm.

### PA / PO / POB

Ưu tiên:

```text
PA state
PO classification
allocation
idempotency
approval
receipt traceability
```

Không chạy Menu/POS nếu không có shared dependency.

### Inventory / UnitConversion

Nếu sửa engine dùng chung, test:

```text
Ingredient UOM
Supplier package UOM
Receipt conversion
BOM costing
Inventory ledger
```

Nếu chỉ sửa dropdown/filter UI thì không chạy full inventory suite.

### Menu / BOM / Costing / Topping

Ưu tiên:

```text
BOM theo size
FIFO costing
suggested price
topping policy
POS option snapshot
```

Không chạy Supplier/Procurement nếu không sửa nguồn giá hoặc conversion dùng chung.

### POS / WorkShift / Ice Management

Chỉ chạy cùng nhau khi thay đổi contract liên kết ca, tiêu hao hoặc snapshot.

## 6. Quy trình kiểm thử

### Tầng 0 — Reproduce

Trước khi sửa:

```text
tái hiện bug
hoặc
thêm characterization test
```

Ghi input, expected, actual, route/API và state liên quan.

### Tầng 1 — Isolated

Chạy một test case, class hoặc file gần nhất.

Ví dụ tổng quát:

```bash
dotnet test <test-project> --filter "<test-name-or-class>"
npm test -- <test-file>
```

Sau patch nhỏ, chạy lại isolated test.

### Tầng 2 — Affected module

Chạy test module/workflow trực tiếp.

Ví dụ task Supplier + UOM:

```text
Supplier tests
UnitConversion tests liên quan
PA/PO supplier-selection tests
```

Không chạy module không nằm trong impact map.

### Tầng 3 — Static/build

Dùng lệnh thật của repo:

```text
dotnet build <affected project/solution>
npm run lint
npm run typecheck
npm run build
```

Không format toàn repo nếu task không yêu cầu.

### Tầng 4 — Runtime smoke

Bắt buộc với UI/API.

Smoke bám scenario Owner:

```text
Create → List → Detail → Edit → downstream action liên quan
```

Smoke không thay automated test.

### Tầng 5 — Full suite gate

Chỉ chạy khi trigger ở mục 7 đúng.

## 7. Full suite decision gate

### REQUIRED khi

```text
1. Sửa schema/migration/DbContext dùng chung.
2. Sửa auth/permission/middleware.
3. Sửa shared UnitConversion/Inventory/Costing engine.
4. Sửa global routing/state/design system.
5. Sửa dependency/test config.
6. Refactor lớn với consumer chưa xác định chắc.
7. Chuẩn bị merge/release và repo bắt buộc.
8. Owner yêu cầu rõ.
```

### NOT REQUIRED khi

```text
1. Sửa UI text/CSS cô lập.
2. Sửa form/query/controller/service có module test rõ.
3. Sửa validation cục bộ.
4. Sửa projection/read model một module.
5. Đã có isolated + module + integration + smoke đầy đủ.
6. Full suite không thêm tín hiệu đáng kể cho risk của task.
```

Trước khi chạy full suite, comment:

```text
FULL_SUITE_JUSTIFICATION

Trigger:
<exact trigger>

Why affected-module tests are insufficient:
<lý do>

Expected duration:
<dựa trên lịch sử repo>

Command:
<exact command>
```

Không có justification thì không chạy.

## 8. Ngân sách thời gian

Gợi ý:

```text
Isolated: dưới 2 phút
Affected module: dưới 10 phút
Full suite: chỉ khi có trigger
```

Nếu lệnh vượt thời gian bình thường:

```text
kiểm tra process/log
báo tiến độ
không mở thêm lệnh rộng song song
```

Nếu full suite dài nhưng là gate bắt buộc:

```text
đẩy sang CI hoặc phiên cuối
ghi exact command
không tuyên bố PASS khi chưa có kết quả
```

## 9. Xử lý test fail

### Test trong phạm vi

```text
isolated
→ root cause
→ sửa
→ isolated
→ module
→ smoke
```

### Test ngoài phạm vi

Không sửa ngay. Phân loại:

```text
OUT_OF_SCOPE_PRE_EXISTING
OUT_OF_SCOPE_FIXTURE
OUT_OF_SCOPE_ENVIRONMENT
OUT_OF_SCOPE_FLAKY
POSSIBLE_SHARED_REGRESSION
```

Chỉ mở rộng scope khi có evidence thay đổi hiện tại tác động tới test đó.

Không gọi flaky chỉ vì rerun xanh một lần.

Không sửa test chỉ để suite xanh. Chỉ cập nhật khi business contract thật sự đổi.

## 10. Lỗi ngoài task

Sửa ngay chỉ khi:

```text
blocker của luồng nghiệm thu
P0/P1 trực tiếp
shared defect gây sai task
fix nhỏ, an toàn, có test
```

Nếu không:

```text
ghi issue
ghi evidence/severity
tiếp tục task chính
```

## 11. Migration

Chỉ kiểm tra migration khi schema hoặc EF configuration thay đổi.

Nếu không:

```text
Migration status: NOT APPLICABLE
```

Không reset database chỉ để test UI/query.

## 12. Git hygiene

Trước commit:

```bash
git status --short
git diff --check
git diff --stat
git diff
```

Stage chính xác:

```bash
git add <exact-file>
git add -p
git diff --cached --name-only
git diff --cached --stat
git diff --cached --check
```

Không dùng `git add .` hoặc `git add -A` khi có dirty file ngoài scope.

## 13. Verification summary bắt buộc

```text
VERIFICATION_SUMMARY

Task scope:
- ...

Changed modules:
- ...

Tests executed:
1. Isolated:
   PASS/FAIL — <command> — <count> — <duration>

2. Affected module:
   PASS/FAIL — <command> — <count> — <duration>

3. Integration/contract:
   PASS/FAIL/NOT APPLICABLE — <command>

4. Static/build:
   PASS/FAIL/SKIPPED — <command/reason>

5. Runtime smoke:
   PASS/FAIL — <scenario>

6. Full suite:
   NOT REQUIRED / PASS / FAIL / DEFERRED TO CI
   Reason:
   <gate decision>

Out-of-scope failures:
- NONE
hoặc
- <test> — <classification> — <evidence>

Migration:
- CLEAN / NOT APPLICABLE / NOT CHECKED

git diff --check:
- CLEAN

Remaining risks:
- NONE
hoặc
- ...
```

## 14. Tiêu chí hoàn thành

Chỉ DONE khi:

```text
isolated xanh
affected-module xanh
integration/contract xanh nếu cần
build/static phù hợp xanh
runtime smoke đúng scenario xanh
full suite có quyết định rõ
không còn lỗi trong scope chưa giải thích
diff đúng phạm vi
commit/push đúng rule
```

Không ghi:

```text
full suite chưa chạy nhưng chắc không ảnh hưởng
```

Phải ghi:

```text
Full suite: NOT REQUIRED
Reason: <theo gate>
```

hoặc:

```text
Full suite: DEFERRED TO CI
Reason: <trigger và giới hạn>
```

## 15. Quy tắc nhanh

```text
Sửa bug?
→ Reproduce + isolated.

Isolated xanh?
→ Affected module.

Có contract liên module?
→ Integration đúng luồng.

Có UI/API?
→ Runtime smoke.

Chạm shared engine/schema/auth/global config?
→ Xét full suite.

Không có trigger?
→ Không chạy full suite.

Test ngoài scope fail?
→ Phân loại, không sửa nếu không liên quan.

Chuẩn bị commit?
→ Build/static → smoke → diff check → stage exact files.
```
