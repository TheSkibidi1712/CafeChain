# Tài Liệu Domain

Hướng dẫn các skill kỹ thuật cách đọc tài liệu domain của repo khi khám phá codebase.

## Trước khi khám phá, hãy đọc những file sau

- **`CONTEXT.md`** tại thư mục gốc của repo, hoặc
- **`CONTEXT-MAP.md`** tại thư mục gốc nếu tồn tại — file này trỏ đến từng `CONTEXT.md` cho mỗi ngữ cảnh. Đọc từng file liên quan đến chủ đề.
- **`docs/adr/`** — đọc các ADR liên quan đến khu vực bạn sắp làm việc.

Nếu bất kỳ file nào không tồn tại, **tiến hành bình thường**. Không cần cảnh báo về sự vắng mặt của chúng; không đề xuất tạo trước. Skill tạo tài liệu (`/grill-with-docs`) sẽ tạo chúng khi các thuật ngữ hoặc quyết định thực sự được giải quyết.

## Cấu trúc file

Repo đơn ngữ cảnh:

```
/
├── CONTEXT.md
├── docs/adr/
│   ├── 0001-*.md
│   └── 0002-*.md
└── src/
```

## Sử dụng thuật ngữ của bảng chú giải

Khi đầu ra của bạn đề cập đến một khái niệm domain (trong tiêu đề issue, đề xuất tái cấu trúc, giả thuyết, tên test), hãy dùng thuật ngữ như đã định nghĩa trong `CONTEXT.md`. Không dùng từ đồng nghĩa mà bảng chú giải đã loại trừ.

Nếu khái niệm bạn cần chưa có trong bảng chú giải, đó là dấu hiệu — hoặc bạn đang tạo ra ngôn ngữ mà dự án không dùng (hãy cân nhắc lại) hoặc có một khoảng trống thực sự (ghi chú cho `/grill-with-docs`).

## Cảnh báo xung đột ADR

Nếu đầu ra của bạn mâu thuẫn với một ADR hiện có, hãy nêu rõ thay vì âm thầm ghi đè:

> _Mâu thuẫn với ADR-0007 — nhưng đáng để mở lại thảo luận vì…_
