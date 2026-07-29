# Quy tắc Git Workflow - CafeChain

Tài liệu này hướng dẫn workflow Git đơn giản cho mọi contributor của CafeChain. Mục tiêu là làm việc đúng nhánh, chỉ commit file cần thiết và đưa code lên GitHub thông qua Pull Request.

> [!IMPORTANT]
> `Rule.md` là quy định làm việc của nhóm, không tự chặn thao tác Git. Muốn chặn direct push bằng kỹ thuật, repository owner phải cấu hình [GitHub protected branches hoặc rulesets](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches).

Trong các lệnh bên dưới, nội dung giữa dấu `<...>` là phần phải thay bằng giá trị thật. Ví dụ, hãy dùng `feature/quan-ly-kho` thay cho `feature/<ten-chuc-nang>`.

## 1. Quy định về nhánh

- `main`: nhánh ổn định/phát hành, chỉ nhận Pull Request từ `develop`.
- `develop`: nhánh tích hợp chung, mọi tính năng phải bắt đầu từ đây.
- `feature/<ten-chuc-nang>`: nhánh cá nhân dùng để phát triển một tính năng.

Contributor bắt buộc:

- Không code, commit hoặc push trực tiếp trên `main` và `develop`.
- Không tạo feature branch từ `main`.
- Không tạo Pull Request của feature trực tiếp vào `main`.
- Không tự merge Pull Request của mình.
- Không bật auto-merge.
- Không force-push.
- Chỉ push nhánh `feature/...` do mình đang làm.

## 2. Clone và tạo feature branch

### Clone repository lần đầu

```powershell
git clone https://github.com/TheSkibidi1712/CafeChain.git
cd CafeChain
git checkout develop
git pull origin develop
git checkout -b feature/<ten-chuc-nang>
git branch
```

### Tạo tính năng mới trong repository đã clone

```powershell
git checkout develop
git pull origin develop
git checkout -b feature/<ten-chuc-nang>
git branch
```

Trong kết quả `git branch`, dấu `*` phải nằm trước nhánh `feature/...` trước khi bắt đầu sửa code.

Tên nhánh phải:

- Bắt đầu bằng `feature/`.
- Viết thường, không dấu, không khoảng trắng.
- Dùng dấu gạch ngang giữa các từ.

Ví dụ đúng:

```text
feature/quan-ly-kho
feature/bao-cao-doanh-thu
feature/phan-bo-da-theo-ca
```

Ví dụ sai:

```text
feature/QuanLyKho
feature/Quan Ly Kho
fearture/quan-ly-kho
```

Nếu đã tạo nhánh từ sai base, không push nhánh đó. Hãy quay lại `develop`, cập nhật code và tạo feature branch mới.

## 3. Stage và commit

Không dùng:

```powershell
git add .
git add -A
```

Hai lệnh trên có thể đưa cả appsettings, secret, file ghi chú hoặc migration ngoài phạm vi vào commit.

Chỉ stage đúng file cần commit:

```powershell
git status
git add <file-can-commit>
git commit -m "Noi dung commit"
```

Nếu có nhiều file, chạy `git add <file-can-commit>` riêng cho từng file. Sau đó dùng `git status` để kiểm tra lại danh sách file đã stage.

Commit message được viết tự do nhưng phải ngắn gọn và nói rõ thay đổi. Tránh các nội dung mơ hồ như `update`, `done` hoặc `code moi`.

### Bỏ stage một file bị chọn nhầm

Lệnh sau bỏ file khỏi commit sắp tạo nhưng vẫn giữ thay đổi ở máy local:

```powershell
git reset HEAD <file-stage-nham>
git status
```

Ví dụ:

```powershell
git reset HEAD CafeChain/FIX.md
git reset HEAD CafeChain/appsettings.json
git reset HEAD CafeChain/Migrations/
git status
```

## 4. File không được commit theo mặc định

Không đưa các file sau vào commit nếu chưa được maintainer cho phép rõ ràng:

- `CafeChain/FIX.md`.
- `CafeChain/appsettings.json`.
- `CafeChain/appsettings.Development.json`.
- `CafeChain.PrintBridge/appsettings.json`.
- `CafeChain.PrintBridge/appsettings.Development.json`.
- Các appsettings khác.
- Migration sinh nhầm hoặc không liên quan đến tính năng.
- File build, log, cache, output tạm và cấu hình riêng của IDE.

Các appsettings hiện đã được Git theo dõi nên `.gitignore` không tự ngăn chúng đi vào commit. Contributor phải dùng `git status` để kiểm tra trước khi commit. Xem thêm [Ignoring files](https://docs.github.com/en/get-started/getting-started-with-git/ignoring-files).

Tuyệt đối không commit:

- Mật khẩu.
- API key, token hoặc private key.
- JWT secret.
- Connection string chứa tài khoản hoặc mật khẩu thật.
- Secret của PayOS, Cloudinary, Pexels, email, PrintBridge hoặc dịch vụ bên ngoài.

Secret dùng trên máy cá nhân phải được lưu bằng .NET User Secrets hoặc biến môi trường. Tham khảo [Safe storage of app secrets in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets?view=aspnetcore-10.0).

## 5. Quy định về migrations

Migration có chủ đích không phải file rác. Theo [Microsoft EF Core - Managing Migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing), migration thay đổi schema cần được lưu cùng mã nguồn.

Chỉ commit migration khi tính năng thực sự thay đổi database schema. Một thay đổi schema phải có đủ:

- File migration `.cs`.
- File `.Designer.cs`.
- Thay đổi tương ứng trong `AppDbContextModelSnapshot.cs`.

Không commit migration sinh nhầm, migration ngoài phạm vi hoặc chỉ một phần của bộ migration. Pull Request phải mô tả bảng/cột bị ảnh hưởng và cách đã kiểm thử.

Nếu stage nhầm migration:

```powershell
git reset HEAD CafeChain/Migrations/
git status
```

## 6. Kiểm tra và push feature branch

Trước khi push, kiểm tra dấu `*` đang nằm ở feature branch, cập nhật thay đổi mới nhất từ `develop`, rồi kiểm tra lại file:

```powershell
git branch
git pull origin develop
git status
```

Chỉ push feature branch:

```powershell
git push -u origin feature/<ten-chuc-nang>
```

Các lần push tiếp theo trên cùng nhánh:

```powershell
git push
```

Tuyệt đối không chạy:

```powershell
git push origin main
git push origin develop
git push --force
```

Contributor nên chạy test liên quan trong IDE trước khi push. Khi Pull Request được mở, GitHub CI sẽ tự động restore, build và test dự án; không cần ghi nhớ chuỗi lệnh .NET dài. Pull Request chỉ được merge khi job `CI - Build & Test` xanh.

## 7. Tạo Pull Request

Pull Request của tính năng phải chọn:

- **Base:** `develop`.
- **Compare:** `feature/<ten-chuc-nang>`.

Pull Request phải có tiêu đề rõ ràng, mô tả nội dung thay đổi, cách kiểm thử và ảnh chụp nếu có thay đổi giao diện. Nếu có migration, phải mô tả ảnh hưởng database.

Contributor chỉ:

1. Push feature branch.
2. Mở hoặc cập nhật Pull Request.
3. Sửa code theo phản hồi nếu có.
4. Chờ maintainer merge.

Contributor không tự merge và không bật auto-merge. Workflow hiện tại không bắt buộc approval, nhưng job `CI - Build & Test` phải xanh và maintainer phải merge thủ công.

Nếu tạo nhầm Pull Request vào `main`, phải đổi base về `develop` hoặc đóng Pull Request sai và tạo lại.

## 8. Xử lý khi commit hoặc push nhầm

### Đã stage nhầm file

```powershell
git reset HEAD <file-stage-nham>
git status
```

### Đã commit nhầm nhưng chưa push

Không push commit đó. Hãy báo maintainer để được hướng dẫn sửa commit an toàn, đặc biệt nếu commit chứa appsettings hoặc secret.

### Đã push secret

1. Dừng push thêm commit.
2. Báo ngay cho maintainer hoặc repository owner.
3. Thu hồi hoặc rotate credential bị lộ.
4. Không cho rằng xóa secret ở commit mới là đủ vì secret vẫn còn trong Git history.
5. Maintainer xử lý lịch sử theo [Removing sensitive data from a repository](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/removing-sensitive-data-from-a-repository).

Không đăng secret vào issue, Pull Request, ảnh chụp màn hình hoặc tin nhắn công khai.

## 9. Checklist trước khi mở Pull Request

- [ ] `git branch` hiển thị dấu `*` trước `feature/<ten-chuc-nang>`.
- [ ] Nhánh được tạo từ `develop`.
- [ ] Không dùng `git add .` hoặc `git add -A`.
- [ ] `git status` chỉ hiển thị đúng file cần commit.
- [ ] Không có `CafeChain/FIX.md` trong commit.
- [ ] Không có appsettings chưa được maintainer cho phép.
- [ ] Không có password, token, API key hoặc credential thật.
- [ ] Migration, nếu có, đầy đủ migration, designer và snapshot.
- [ ] Đã push đúng feature branch.
- [ ] Pull Request có base `develop`, không phải `main`.
- [ ] Không bật auto-merge và không tự merge.
- [ ] Job `CI - Build & Test` xanh trước khi merge.

## 10. Khuyến nghị dành cho repository owner

Để quy tắc được chặn bằng kỹ thuật, repository owner nên bảo vệ cả `main` và `develop`:

- Bắt buộc thay đổi thông qua Pull Request.
- Bắt buộc status check `CI - Build & Test`.
- Chặn direct push, force-push và xóa nhánh.
- Không bật merge queue.
- Tắt **Allow auto-merge** theo [Managing auto-merge for pull requests](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/configuring-pull-request-merges/managing-auto-merge-for-pull-requests-in-your-repository).

Việc cấu hình GitHub là bước quản trị riêng; file này không tự thay đổi các thiết lập đó.
