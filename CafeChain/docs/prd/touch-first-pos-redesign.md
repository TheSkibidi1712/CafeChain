# Touch-First POS Redesign PRD

Status: Approved

Owner: CafeChain

Epic: GitHub #200

Baseline: `75559a3a7eaeeb7f05a7ce35df7dab0afaf84566`

Target branch: `feature/POS`

## 1. Muc tieu

Tai cau truc giao dien POS theo huong touch-first va one-screen selling, giam thao tac nhung khong thay doi authority hien tai:

`Payment commit -> Order/Payment -> CashDrawer -> Inventory Deduction/FIFO -> Silent Print`.

Flow dich:

`Mo POS -> chon Tai quan/Mang di -> chon mon -> Thanh toan -> Tien mat/VietQR/Ket hop -> thanh cong -> in nen -> gio moi`.

## 2. Nguyen tac bat bien

- Giu nguyen API, database va business rules neu khong co blocker domain thuc su.
- Backend/catalog van la nguon gia va availability co tham quyen.
- `ClientOrderId` va cac guard hien tai tiep tuc chong double submit/duplicate commit.
- POS khong truy cap truc tiep inventory; Inventory Deduction chi chay sau committed paid order.
- Print failure khong duoc bien payment thanh failed.
- Offline chi ho tro cash va giu nguyen IndexedDB Sync contract.
- Khong dua Web/Delivery/ShopeeFood vao counter flow.

## 3. Scope

### In scope

- Header POS, order type, search, network/printer/cashier status.
- Categories, product grid, sticky cart va sticky payment action.
- Product option sheet: size, topping, da, duong, quantity, note, price preview.
- Cash, VietQR va split payment trong mot workspace.
- Payment success/error, print/offline feedback.
- Customer-facing display qua `BroadcastChannel` va separate-window fallback.
- Responsive, touch, keyboard va accessibility cho cac viewport dich.

### Out of scope

- Quan ly ban, tach don, thanh toan sau.
- PaymentSession domain extraction, PayOS webhook redesign.
- Inventory/FIFO, CashDrawer, Offline Sync authority redesign.
- Web/Delivery lifecycle, Order history #199, Dashboard, OTP, Procurement.
- Refund redesign hoac migration khong can thiet.

## 4. Inspect hien trang

### 4.1 Component va ownership

| Component/module | Ownership hien tai | Danh gia |
|---|---|---|
| `App.tsx` / `RootLayout` | Router, top navigation, printer simulator | UI shell; route change se unmount `POSLayout` |
| `TopNavbar.tsx` | Navigation, session summary, notification polling, network/printer status | UI + polling nhe; nhan tab bi an o mot so width |
| `POSLayout.tsx` | Catalog filter, cart, modifier routing, WorkShift, cash, split, VietQR, SignalR, offline queue, temporary print | Business orchestration va UI dang bi ket hop trong 2.116 dong |
| `ProductModifierModal.tsx` | Modifier draft, validation, focus trap, price preview | UI state cuc bo; dang la centered modal va dung checkbox topping |
| `usePOSData.ts` | IndexedDB live query, catalog Sync, connectivity, pending count | Data hook, co the giu nguyen |
| `OfflineSyncService.ts` | Catalog cache va Offline Order Sync | Business/data service, khong refactor trong epic |
| `usePrinterStatus.ts` | PrintBridge SignalR, heartbeat, reconnect | Business integration hook, giu authority |
| `index.css` | Design tokens, three-zone grid, responsive sheets | Palette dung; breakpoint va dimension can harden |

### 4.2 Tra loi cac cau hoi inspect

- **Component chua business logic:** `POSLayout.tsx` chua commit payload, offline fallback, payment state machine, cancel/refund audit, SignalR completion, WorkShift guard va temporary print. `usePOSData`, `OfflineSyncService`, `usePrinterStatus` chua integration logic rieng.
- **Component chi chua UI:** `ProductImage`; phan lon `NetworkStatusIndicator`, `PrinterStatusBadge`; view trong `TopNavbar`. `ProductModifierModal` la UI draft co tinh gia preview theo catalog.
- **Cart state:** `useState<CartItem[]>` trong `POSLayout`; snapshot duoc copy khi bat dau payment/offline.
- **Payment state co the tach:** co. Tach orchestration sang `usePOSCheckout`/payment workspace adapter la kha thi, nhung service/API function phai giu payload va side-effect order. Khong tach domain/backend.
- **Duplicated handlers:** cash commit, offline fallback va success reset lap o nhieu nhanh; guard `checkoutInFlightRef` va message/reset cung lap. Can gom presentation-safe helper, khong gom sai cac contract khac nhau.
- **Modal long modal:** DOM la sibling, nhung QR dialog va cash-return alert co the render dong thoi, tao hai lop modal ve UX/focus. Product modifier/cash/QR cung deu la full modal thay vi mot workspace/sheet co mot owner.
- **State de mat:** resize/rerender khong lam mat cart; route navigation/page reload lam unmount va mat cart/pending payment in-memory. Active payment close guard chi luu subset trong sessionStorage, khong the restore payment UI day du. Epic khong tu mo rong persistence contract.
- **Hard-coded breakpoint:** `1199px`, `819px`, `420px`; cart width `340-440px`; topbar `64px`. Can doi chieu viewport matrix va dat responsive constraints on dinh.
- **Horizontal scroll:** page shell dang `overflow:hidden`; category va navbar co intentional horizontal scroll o medium width. Khong co page-level scroll, nhung navbar icon-only va category width can runtime check.
- **QR rendering:** hien tai nhung PayOS checkout bang `iframe` va co link mo PayOS; payload co `qrCode` nhung UI chua render QR rieng.
- **Print error:** printer health o `PrinterStatusBadge`; temporary print error qua toast; official print message chi noi lenh da gui va khong co persisted physical result.
- **Offline state:** `NetworkStatusIndicator`, catalog metadata, pending order count, Offline Order card va temporary print actions.
- **Sau success:** cash, offline, split cash va SignalR QR deu clear cart; message success 3,5 giay. Chua co success overlay chuyen tiep ro rang.
- **Double submit guard:** `checkoutInFlightRef.current`, `isCheckingOut`, `hasPendingPayment`, disabled buttons va backend `ClientOrderId`.

## 5. Interaction contract

### 5.1 Main selling screen

- Header 64px toi da: order type segmented control, search, online/offline, printer, cashier, More actions.
- Desktop landscape dung ba zone: category 12-15%, catalog 52-58%, cart 28-34%.
- Cart va CTA `Thanh toan` luon nhin thay; chi item list cuon.
- Khi width hep, category chuyen thanh horizontal rail va cart thanh right sheet; cart state khong remount.
- Product card toi thieu 120x100px, toan card la quick-add target; option action co nhan ro.

### 5.2 Product option sheet

- Item khong can lua chon: mot cham them voi default size/topping policy.
- Item co lua chon: mot sheet duy nhat gom size, topping, da, duong, quantity, note, price preview.
- Khong modal long modal; mot luc chi co mot interaction layer co focus owner.
- Topping dung touch row/toggle co nhan, khong phu thuoc checkbox nho.
- Add va Update phan biet ro; Cancel tach khoi primary action.

### 5.3 Cart interaction

- Moi line hien ten, size/topping, note, quantity, unit/line total.
- `-`, `+`, `Sua`, `Xoa` co touch target >=44px; action it dung vao More co nhan.
- Cart bi lock trong payment active; ly do hien inline.
- Catalog version/price stale guard va refresh contract duoc giu nguyen.

### 5.4 Cash payment

- Mot payment workspace hien tong can tra, tien khach dua, tien thua.
- Quick values: dung so tien, 50k, 100k, 200k, 500k; keypad 64-72px.
- Confirm disabled khi thieu tien hoac khong phai boi so 1.000d.
- Cancel quay ve cart, khong commit, khong Inventory Deduction, khong print.
- Success hien 1-2 giay, print tiep tuc nen, sau do reset gio.

### 5.5 VietQR

- Hien amount, order code, countdown, waiting state va QR/PayOS surface lon.
- Co Cancel, chuyen sang cash khi contract cho phep, va print/open fallback.
- Khong co manual confirm; webhook/SignalR la authority.
- Success tu dong close workspace, clear cart va thong bao print command.

### 5.6 Split payment

- Chi duoc khoi tao tu tab `Thanh toan ket hop`.
- Temporary cash giu `collecting`, khong post CashDrawer truoc final commit.
- Hien da nhan cash, con lai, phuong thuc hoan tat va refund warning khi cancel.
- Cash + QR va cash + cash giu nguyen idempotency hien tai.

### 5.7 Cancel, success, print failure va offline

- Cancel co cash tam phai qua mot alert confirmation duy nhat, khong chong len payment dialog.
- Success overlay khong chan background print qua 2 giay va tu ve empty cart.
- Print disconnected/error hien ben canh payment result, khong rollback paid order.
- Offline vo hieu VietQR/split, cho phep cash, luu queue va hien temporary print actions.

### 5.8 Customer-facing display

- Route: `/pos/customer-display`, khong dung `RootLayout` noi bo.
- States: Idle, Cart, Awaiting QR, Success, Cancelled/Expired, Offline.
- POS mo mot window cho ca; `BroadcastChannel` gui view model toi gian, khong gui JWT/cookie/payment secret/PII.
- Window Management API chi la progressive enhancement; manual move/fullscreen la fallback bat buoc.

## 6. Responsive va touch contract

| Viewport | Layout contract |
|---|---|
| 1024x600 | Compact header; horizontal category rail; 2-3 catalog columns; cart 340-360px; footer/CTA visible |
| 1280x800 | Three-zone; category ~144px; catalog flexible; cart ~384px |
| 1366x768 | Three-zone; 3-4 catalog columns; sticky cart; no page scroll |
| iPad Pro landscape | Same three-zone or medium rail depending CSS pixels; safe areas; touch-first, no hover dependency |
| 1920x1080 | Three-zone with constrained card density; cart <=34%; no oversized whitespace |

Standards: primary 56-64px, keypad 64-72px, spacing 8-12px, body 16-18px, total 26-32px, visible focus, tabular monetary figures, reduced-motion support.

## 7. Component dependency map dich

```text
App
|- RootLayout
|  |- TopNavbar
|  |  |- OrderTypeControl (selling route)
|  |  |- NetworkStatusIndicator
|  |  `- PrinterStatusBadge
|  `- POSLayout (orchestration owner)
|     |- CategoryRail
|     |- ProductGrid
|     |  `- ProductCard
|     |- CartPanel
|     |  `- CartLine
|     |- ProductOptionSheet
|     `- PaymentWorkspace
|        |- CashPanel
|        |- VietQrPanel
|        |- SplitPaymentPanel
|        `- CashReturnAlert
`- CustomerDisplayPage (separate route, no RootLayout)
   `- useCustomerDisplayReceiver

POSLayout
|- usePOSData -> IndexedDB / OfflineSyncService
|- usePrinterStatus (via header)
|- usePOSCheckout (presentation orchestration extracted in #204)
|  |- apiClient POS commit/cancel
|  |- PayOS SignalR
|  |- posShiftCloseGuard
|  `- OfflineSyncService fallback
`- customerDisplayPublisher (#205, sanitized view model only)
```

Rule: `POSLayout` van la mot mounted state owner tren selling route. Component con nhan state/callback; khong tu goi commit API hoac tao business state thu hai.

## 8. Exact file plan

### #202 Main layout

- Modify: `CafeChain.Frontend/src/POSLayout.tsx`, `components/TopNavbar.tsx`, `src/index.css`.
- Add neu tach presentation lam giam complexity: `components/pos/CategoryRail.tsx`, `ProductGrid.tsx`, `CartPanel.tsx`.
- Tests: update `CafeChain.Tests/POSResponsiveRedesignTests.cs`.

### #203 Options/cart

- Modify/rename presentation: `components/ProductModifierModal.tsx` -> giu import compatibility hoac chuyen thanh sheet.
- Add: `components/pos/ProductOptionSheet.tsx`, `CartLine.tsx` neu thuc su giam duplication.
- Modify minimal wiring: `POSLayout.tsx`, `index.css`, responsive source tests.

### #204 Payment workspace

- Add: `components/pos/payment/PaymentWorkspace.tsx` va focused panels; `hooks/usePOSCheckout.ts` neu extraction giu nguyen payload/side effects.
- Modify: `POSLayout.tsx`, `index.css`.
- Extend existing POS cash/split/VietQR source/integration tests; khong doi backend neu contract da du.

### #205 Customer display

- Add: `pages/CustomerDisplay.tsx`, `services/customerDisplayChannel.ts`.
- Modify: `App.tsx`, `TopNavbar.tsx`/More actions, `POSLayout.tsx`, `index.css`.
- Add focused source tests; khong them dependency.

### #206 Hardening

- Modify chi file co measured defect trong cac file tren.
- Update `POSResponsiveRedesignTests.cs` va handoff/device evidence docs neu can.

## 9. Risk matrix

| Risk | Muc do | Guard/mitigation |
|---|---:|---|
| Tach UI lam doi payment payload/side-effect order | Cao | Snapshot tests, payment regressions, khong doi service/API authority |
| Component remount lam mat cart/pending state | Cao | Mot POSLayout owner, conditional presentation khong key/remount owner |
| Hai modal cung active va focus xung dot | Cao | Mot interaction-layer state machine, alert thay the workspace thay vi chong lop |
| Double submit sau khi doi CTA | Cao | Giu `checkoutInFlightRef`, disabled state, `ClientOrderId`, regression double confirm |
| iPad viewport/safe-area che CTA | Trung binh | `100dvh`, safe-area, screenshot/pixel check theo matrix |
| QR iframe khong phu hop customer display | Trung binh | Display chi nhan safe QR representation neu backend co; neu khong, dung safe checkout surface/fallback va report blocker |
| Route navigation lam mat in-memory cart | Trung binh | More actions khong tu dong navigate trong active cart; persistence ngoai scope duoc ghi risk |
| Browser khong ho tro multi-screen | Trung binh | `BroadcastChannel` + separate window/manual placement fallback |
| PrintBridge khong xac nhan giay vat ly | Thap cho payment | Chi thong bao lenh da gui; khong tuyen bo da in |
| Dependency/lockfile churn | Thap | Khong them package; dung React/CSS/Web APIs hien co |

## 10. Gates va verification

- Moi issue co comment start/progress/final evidence, commit va push rieng.
- Khong bat dau issue tiep theo truoc khi issue truoc closed.
- Frontend lint/build va targeted tests sau moi source issue.
- Runtime smoke theo viewport/flow trong issue gate.
- Truoc commit: cached name/stat/check; stage exact paths.
- Khong commit protected dirty files, migration/snapshot; khong PR, merge hoac doi branch.

Wireframes: `docs/wireframes/touch-first-pos-redesign.md`.
