# Touch-First POS Wireframes

Status: Approved

PRD: `docs/prd/touch-first-pos-redesign.md`

Epic: GitHub #200

Ky hieu: `[ action ]`, `( status )`, `{ scroll region }`. Cac so do la interaction contract, khong phai pixel-perfect visual.

## 1. 1024 x 600

```text
+--------------------------------------------------------------------------------------+
| [Tai quan|Mang di] [ Tim mon................................ ] (Online) (May in) [BH] |
+--------------------------------------------------------------------------------------+
| {Tat ca | Coffee | Tra | ... horizontal category rail}                              |
+-----------------------------------------------+--------------------------------------+
| PRODUCT GRID 2-3 COT                          | GIO HANG                             |
| +-------------+ +-------------+               | 1. Ca phe sua                        |
| | anh         | | anh         |               | Size M, it da                  [-][+] |
| | Ca phe sua  | | Bac xiu     |               | 35.000d                      [Sua...] |
| | 35.000d     | | 39.000d     |               |--------------------------------------|
| +-------------+ +-------------+               | {cart lines scroll only}             |
| +-------------+ +-------------+               |--------------------------------------|
| | ...         | | ...         |               | Tam tinh                    35.000d   |
| +-------------+ +-------------+               | TONG CONG                   35.000d   |
| {catalog scroll only}                          | [         THANH TOAN 35.000d       ]  |
+-----------------------------------------------+--------------------------------------+
```

Contract: topbar va category rail toi da 112px tong chieu cao; cart 340-360px; CTA 56px khong bi day khoi viewport.

## 2. 1280 x 800

```text
+------------------------------------------------------------------------------------------------+
| CafeChain | [Tai quan|Mang di] | [ Tim mon...................... ] | Online | May in | BH | ... |
+--------------+-----------------------------------------------------+-----------------------------+
| DANH MUC     | SAN PHAM                                            | GIO HANG                    |
| [Tat ca 29]  | +--------------+ +--------------+ +--------------+ | 1. Ca phe sua        [-][+] |
| [Coffee 8]   | | image        | | image        | | image        | | Size M, tran chau     [Sua] |
| [Tra 7]      | | name         | | name         | | name         | | 42.000d               [Xoa] |
| [Da xay 5]   | | price        | | price        | | price        | |-----------------------------|
| [Topping 4]  | +--------------+ +--------------+ +--------------+ | {cart lines scroll}         |
|              | {catalog scroll; 3-4 columns}                       |-----------------------------|
|              |                                                     | Tong cong          42.000d  |
|              |                                                     | [    THANH TOAN 42.000d    ] |
+--------------+-----------------------------------------------------+-----------------------------+
```

Contract: 12-15% / 52-58% / 28-34%; header mot dong; khong horizontal page scroll.

## 3. 1366 x 768

```text
+------------------------------------------------------------------------------------------------------+
| CC POS | [TAI QUAN|MANG DI] | [ Tim mon............................. ] | ONLINE | MAY IN | BH | [Them] |
+---------------+---------------------------------------------------------+------------------------------+
| [Tat ca]      | Product grid 4 cot, card >= 160 x 210                   | GIO HANG                     |
| [Coffee]      | +-----------+ +-----------+ +-----------+ +-----------+ | Order type + item count      |
| [Tra]         | | image     | | image     | | image     | | image     | | {lines scroll}               |
| [Sinh to]     | | name      | | name      | | name      | | name      | |                              |
| [Banh]        | | price     | | price     | | price     | | price     | | Tam tinh                     |
| ...           | +-----------+ +-----------+ +-----------+ +-----------+ | Tong 26-32px                 |
|               | {vertical catalog scroll}                               | [      THANH TOAN 56px      ] |
+---------------+---------------------------------------------------------+------------------------------+
```

## 4. iPad Pro landscape

```text
safe-left                                                                  safe-right
   |  [Order type] [Search........................] (Net) (Print) [Cashier]  |
   |-----------------------------------------------------------------------|
   | {category rail neu CSS width < desktop gate}                          |
   |--------------------------------------------+--------------------------|
   | PRODUCT GRID                               | STICKY CART              |
   | touch cards, no hover dependency           | 48px line controls       |
   | scroll momentum / overscroll contained     | total + 56px pay CTA     |
   |--------------------------------------------+--------------------------|
safe-bottom: CTA va sheet footer nam tren env(safe-area-inset-bottom)
```

Contract: khong co action chi xuat hien khi hover; focus/touch state ro; sheet dung `100dvh` va safe-area.

## 5. 1920 x 1080

```text
+------------------------------------------------------------------------------------------------------------------+
| CC POS | order type | search                                        | network | printer | cashier | more         |
+------------------+----------------------------------------------------------------+------------------------------+
| CATEGORY 12%     | PRODUCT GRID 56%                                               | CART 32%                     |
|                  | +---------------+ +---------------+ +---------------+ +------- | Header                        |
| vertical rail    | | actual image  | | actual image  | | actual image  | | ...   | {cart lines}                  |
|                  | | name + price  | | name + price  | | name + price  | |       |                               |
|                  | +---------------+ +---------------+ +---------------+ +------- |                               |
|                  | Keep card max density; do not stretch text or images            | totals                        |
|                  | {catalog scroll}                                                 | [       THANH TOAN          ] |
+------------------+----------------------------------------------------------------+------------------------------+
```

Contract: card width co responsive min/max; khong phong dai grid/card vo han; monetary alignment dung tabular figures.

## 6. Product option sheet

```text
+---------------------------------------------------------------+
| Ca phe sua da                                      [Dong sheet] |
|---------------------------------------------------------------|
| Size       [S 29k] [M 35k selected] [L 40k]                   |
| Da         [0%] [50%] [100%]                                  |
| Duong      [0%] [50%] [100%]                                  |
| Topping    [ Tran chau          +8k ] [toggle]                 |
|            [ Kem sua        Da gom ] [required]               |
| So luong   [-] 1 [+]                                          |
| Ghi chu    [.................................................] |
|---------------------------------------------------------------|
| Tam tinh 43.000d        [Huy]      [Them vao gio / Cap nhat]   |
+---------------------------------------------------------------+
```

Chi mot sheet; khong mo topping/modal con. Huy va primary khong dat sat nhau neu co nguy co nham cham.

## 7. Unified payment workspace

```text
+--------------------------------------------------------------------------------+
| THANH TOAN  | [Tien mat] [VietQR] [Thanh toan ket hop]              [Quay lai] |
|--------------------------------------------------------------------------------|
| Tien mat                         | VietQR                    | Split             |
| Tong can tra       33.000d       | QR / PayOS surface lon   | Cash da nhan      |
| Khach dua         100.000d       | So tien / ma don         | Con lai           |
| Tien thua          67.000d       | Countdown / waiting      | Cash/QR completion|
| [Dung tien] [50k] [100k] [...]  | [Huy] [Chuyen cash]      | [Huy + return]    |
| [ keypad 64-72px ]               | [In/Mo fallback]         |                  |
|--------------------------------------------------------------------------------|
| Error/status inline                        [       XAC NHAN / DANG CHO        ] |
+--------------------------------------------------------------------------------+
```

Mot workspace owner. Cash-return alert thay the content/interaction layer hien tai, khong nam tren mot QR modal van active.

## 8. Customer display

```text
IDLE                 CART                 AWAITING QR          SUCCESS
+----------------+  +------------------+ +------------------+ +------------------+
| CafeChain      |  | Don cua quy khach| | Quet ma VietQR   | | Thanh toan       |
| Xin moi dat mon|  | 2 x Ca phe sua   | | [ LARGE QR ]     | | thanh cong       |
|                |  | 1 x Tra vai      | | 75.000d          | | Cam on quy khach |
|                |  | Tong 105.000d    | | 04:32            | | reset 2-5s       |
+----------------+  +------------------+ +------------------+ +------------------+
```

Display message chi gom item display name/quantity/line total, total, safe QR representation, expiry va state. Khong gom token, cookie, internal checkout secret hoac PII.
