# Đối chiếu nghiệm thu nghiệp vụ tồn âm

Ngày đối chiếu: 15/07/2026  
Baseline schema: `20260715104817_InitialCreate`  
Manual negative feature: mặc định `false`, chưa được phép bật khi SQL Server gate chưa đạt.

## Kết quả xác minh hiện tại

- Build: đạt, 0 lỗi; 688 warning hiện hữu.
- Test tập trung routing/policy/authorization/moneyless-cost: 26/26 đạt.
- Toàn bộ test `Category!=SqlServerIntegration`: 860/860 đạt.
- `CAFECHAIN_TEST_SQLSERVER_CONNECTION_STRING`: chưa đặt.
- Local `MSSQL$SQLEXPRESS`: đang dừng.
- Kết luận rollout: SQL Server gate chưa đạt; tiếp tục giữ feature tắt.

## Quy tắc migration

- Baseline mới chỉ dùng để tạo database mới.
- `20260715_RefactorNegativeInventoryWorkflow.idempotent.sql` là tombstone và luôn phát lỗi; không còn chứa schema mutation.
- Database đã có migration history cũ phải được backup, chạy audit và có reconcile migration riêng.
- Không sửa migration history, không chạy lại `InitialCreate`, không clamp số âm về 0.

## Bảng 18 tiêu chí

| # | Tiêu chí | Bằng chứng chính | Trạng thái |
|---:|---|---|---|
| 1 | POS Blind Selling không đọc manual flag | `InventoryIssuePolicyTests`, POS regression | Đạt ở non-SQL gate; phải giữ xanh khi rollout |
| 2 | Chỉ SALE/GIFT/DEBT/SAMPLE xin âm | Policy matrix tests | Đạt |
| 3 | WASTE/ADJUSTMENT_OUT/PRODUCTION_OUT/transfer fail-closed | Policy và document/transfer tests | Đạt |
| 4 | PENDING chưa mutate kho/cost | InventoryDocument integration assertions | Đạt ở non-SQL gate |
| 5 | Không self-approval; enforce role/scope | Application service + approval UI/source tests | Đạt ở source/non-SQL gate |
| 6 | Approval lưu requester/approver/reason/version/line snapshot | Model/configuration/migration và detail mapping | Đạt về schema/code |
| 7 | Approve re-evaluate dưới lock | `ApproveNegativeAsync` + SQL Server integration | Code có; SQL gate chưa xác minh |
| 8 | Partial FIFO tạo durable gap, không fallback | Moneyless cost/gap tests | Đạt ở non-SQL gate |
| 9 | Transfer full FIFO, bỏ client UnitPrice | Transfer prepared-item/source tests | Đạt ở non-SQL gate |
| 10 | Inbound settlement deficit trước FIFO remaining | BranchReceipt/transfer settlement tests | Đạt ở non-SQL gate |
| 11 | Nhập bù một phần không vướng snapshot cũ | StoreInventory là source of truth; settlement tests | Đạt ở code; SQL gate chưa xác minh |
| 12 | Không trừ ReservedQty lần hai | Policy/service tests | Đạt |
| 13 | Snapshot sau processing, unique theo document | Snapshot service/configuration + SQL unique test | Code có; SQL gate chưa xác minh |
| 14 | Chỉ CONFIRMED export, đúng tiêu đề | Export/snapshot tests | Đạt ở non-SQL gate |
| 15 | Replay/concurrency không tạo duplicate | Dedup tests + SQL unique/concurrency tests | Non-SQL đạt; SQL gate chưa xác minh |
| 16 | Sinh mã không dùng COUNT + 1 | `DocumentNumberCounterAllocator` và configuration | Code có; SQL concurrency chưa xác minh |
| 17 | SQL Server locking/RowVersion/index/counter tests | Trait `SqlServerIntegration` | Chưa đạt nếu test database không kết nối được |
| 18 | Feature vẫn mặc định tắt | `SystemSettingConfiguration` source test | Đạt |

## Conventional routing và UI maker-checker

- Hai inventory controller không có `[Route]` hoặc verb attribute có template.
- Action dùng conventional `{area}/{controller}/{action}/{id?}` và giữ `[HttpGet]`/`[HttpPost]` không template.
- Razor sinh endpoint bằng `Url.Action`/tag helper; JavaScript không hardcode inventory controller path.
- Detail chỉ render nút review khi approval đang `REQUESTED`, actor đúng role/scope và không phải requester.
- Approve cho review note tùy chọn; Reject bắt buộc review note.
- Server vẫn là authority cuối và map scope/stale/validation thành 403/409/400 hoặc 422.

## Lệnh gate

~~~powershell
dotnet build CafeChain/CafeChain.slnx --no-restore --verbosity:minimal
dotnet test CafeChain.Tests/CafeChain.Tests.csproj --no-build --filter "Category!=SqlServerIntegration"
dotnet test CafeChain.Tests/CafeChain.Tests.csproj --no-build --filter "Category=SqlServerIntegration"
~~~

SQL Server gate dùng `CAFECHAIN_TEST_SQLSERVER_CONNECTION_STRING`; nếu không đặt, test helper thử `localhost\SQLEXPRESS`. Không được thay gate này bằng EF InMemory và không được bật feature khi gate chưa chạy thành công.
