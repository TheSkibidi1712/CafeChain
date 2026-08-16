# AI Smart Import Runtime Smoke Report

- Run: `aiimport-20260816094707`
- Status: **FAILED**
- AI Import scoped status: **PASSED**
- Rendered UI runtime: **BLOCKED** (the Codex browser runtime reported no available backend; the isolated app/database started successfully and was removed after the attempt)
- Fixture: 126 (Excel 43, DOC/DOCX 33, PDF text 30, PDF scan 20)
- Tesseract: tesseract v5.4.0.20240606 | `vie+eng` | `--oem 1 --psm 3`
- Ollama smoke model: `qwen3:4b` (test process only)
- Migration: `20260815152712_InitialCreate` -> `20260816170000_AddPreparedItemTargetStockLevel`

## Stage results

| Stage | Status | Duration (ms) |
|---|---:|---:|
| Build | PASSED | 1276 |
| 126 fixtures - deterministic/offline | PASSED | 2260 |
| AI Import non-SQL regression | PASSED | 6584 |
| 20 PDF scan - native Tesseract | PASSED | 14661 |
| Narrative fallback - Ollama qwen3:4b | PASSED | 5789 |
| SQL Server migration/session/confirm | PASSED | 15885 |
| Full regression suite | FAILED | 115561 |

Post-run verification after broadening the filter: **231/231 AI Import non-SQL tests passed**. Three stale migration-baseline contracts were refactored and passed; this reduced full-suite failures from 47 to 44.

## Confirmed blocker/limitation

- `S19_scan_unknown_extra_columns.pdf`: `tessdata_fast vie+eng` with PSM 3 at DPI 200 yields one word. The pipeline returns typed layout failure and never infers an unknown header without evidence.
- The SQL stage requires `CAFECHAIN_TEST_SQLSERVER_CONNECTION_STRING`; it uses a unique GUID database and deletes it during teardown.
- Rendered UI click/screenshot journeys were not claimed as passed because no Browser backend was available. View and JavaScript contracts are included in the non-SQL regression stage.
- Full-suite failures outside the scoped AI Import matrix remain visible as regression debt: `  Error Message: |   Failed CafeChain.Tests.StoreMenuHardeningIssue166SqlServerTests.SqlServer_ConcurrentMenuUpdate_AllowsOneWinner [10 ms] |   Error Message: |   Failed CafeChain.Tests.StoreMenuHardeningIssue166SqlServerTests.SqlServer_OfflineSnapshotPersistsWithoutRepricing [32 ms] |   Error Message: |   Failed CafeChain.Tests.StoreMenuHardeningIssue166SqlServerTests.SqlServer_OneStoreMenuItemPerStoreDrinkSize [17 ms] |   Error Message: |   Failed CafeChain.Tests.DrinkSizeProfitabilitySqlServerTests.SqlServer_OneEffectiveRecipePerDrinkSize [1 ms] |   Error Message: |   Failed CafeChain.Tests.StoreMenuHardeningIssue166SqlServerTests.SqlServer_ConcurrentVersionIncrement_UpdatesOnce [9 ms] |   Error Message: | Failed!  - Failed:    44, Passed:  2543, Skipped:     0, Total:  2587, Duration: 1 m 54 s - CafeChain.Tests.dll (net8.0)`.

This report contains no document content, OCR text, secret, connection string, or temporary path.
