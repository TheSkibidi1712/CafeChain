# Trình theo dõi issue: GitHub

Các issue và PRD của repo này được lưu trữ trên GitHub Issues. Sử dụng `gh` CLI cho tất cả các thao tác.

## Quy ước

- **Tạo issue**: `gh issue create --title "..." --body "..."`. Dùng heredoc cho nội dung nhiều dòng.
- **Đọc issue**: `gh issue view <number> --comments`, lọc bình luận bằng `jq` và lấy cả nhãn.
- **Liệt kê issue**: `gh issue list --state open --json number,title,body,labels,comments --jq '[.[] | {number, title, body, labels: [.labels[].name], comments: [.comments[].body]}]'` với bộ lọc `--label` và `--state` phù hợp.
- **Bình luận issue**: `gh issue comment <number> --body "..."`
- **Thêm / xóa nhãn**: `gh issue edit <number> --add-label "..."` / `--remove-label "..."`
- **Đóng issue**: `gh issue close <number> --comment "..."`

Repo được suy ra từ `git remote -v` — `gh` tự động nhận diện khi chạy trong thư mục clone.

## Khi một skill yêu cầu "đăng lên trình theo dõi issue"

Tạo một GitHub issue.

## Khi một skill yêu cầu "lấy ticket liên quan"

Chạy `gh issue view <number> --comments`.

