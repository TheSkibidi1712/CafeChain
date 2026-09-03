Hãy điều tra toàn bộ dự án CafeChain để tìm nguyên nhân gốc của lỗi authentication, authorization, session và SignalR trên môi trường production.

## 1. Mục tiêu

Tôi cần một cuộc điều tra dựa trên bằng chứng, không chỉ suy đoán từ một vài file.

Hãy thăm dò toàn bộ repository, truy vết đầy đủ luồng:

1. Đăng nhập web bằng cookie.
2. Khôi phục người dùng từ cookie trên từng request.
3. Nạp claims và quyền của người dùng.
4. SignalR StaffHub kết nối và gọi `/negotiate`.
5. Mở POS và tạo POS access session.
6. Phát hành JWT cho POS.
7. Client POS lưu và gửi JWT.
8. Server lựa chọn Cookie hay Bearer scheme.
9. Server kiểm tra JWT và `PosSessionId`.
10. Các worker hoặc cơ chế timeout thu hồi session.

Không sửa theo phỏng đoán. Trước hết phải tìm bằng chứng và xác định nguyên nhân gốc. Nếu có nhiều lỗi độc lập, hãy tách riêng từng lỗi.

## 2. Môi trường và hiện tượng

Domain production:

```text
https://cafechain.site
```

Hiện tượng:

* Chạy localhost thì đăng nhập và sử dụng bình thường.
* Khi deploy lên host, người dùng đăng nhập được ban đầu.
* Khoảng 5–10 phút sau, tài khoản tự động bị đăng xuất hoặc ứng dụng không còn nhận diện tài khoản.
* Sau thời điểm đó, các quyền cũng biến mất.
* SignalR bị mất kết nối.
* Client liên tục gọi lại endpoint `/negotiate?negotiateVersion=1`.
* Endpoint `/negotiate` trả về HTTP 401.
* Response có nội dung:

```json
{
  "success": false,
  "message": "Bạn cần đăng nhập để truy cập chức năng này."
}
```

* Console xuất hiện các lỗi tương tự:

```text
Failed to load resource: the server responded with a status of 401
Failed to complete negotiation with the server
Failed to start the connection
WebSocket closed with status code 1006
```

* Khi mở POS và chuyển sang sử dụng JWT thì vẫn không hoạt động.
* Cần phân biệt rõ “cookie bị mất”, “cookie còn nhưng server không giải mã được”, “JWT không được gửi”, “JWT không được chọn đúng scheme” và “JWT hợp lệ nhưng POS server-side session đã bị thu hồi”.

## 3. Cấu hình hiện biết

Authentication hiện đặt Cookie làm scheme mặc định:

```csharp
services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(...)
    .AddJwtBearer(...);
```

Cookie được cấu hình:

```csharp
options.ExpireTimeSpan = TimeSpan.FromDays(7);
options.SlidingExpiration = true;

options.Cookie.HttpOnly = true;
options.Cookie.SecurePolicy = environment.IsDevelopment()
    ? CookieSecurePolicy.SameAsRequest
    : CookieSecurePolicy.Always;
options.Cookie.SameSite = SameSiteMode.Lax;
```

Cookie handler trả 401 cho JSON request bằng thông báo:

```text
Bạn cần đăng nhập để truy cập chức năng này.
```

JWT Bearer đọc `access_token` từ query string nhưng chỉ cho các đường dẫn:

```csharp
/hubs/inventory-notifications
/hubs/workshifts
```

Sau khi chữ ký và thời hạn JWT hợp lệ, code tiếp tục lấy:

```csharp
PosSessionId
jti
```

sau đó gọi:

```csharp
IPosAccessSessionService.ValidateAsync(sessionId, jwtId, cancellationToken)
```

Nếu validation không thành công, JWT bị `context.Fail(...)` và trả về 401.

Authorization có các policy:

* `AdminPanelAccess`
* `AdminDashboardApp`
* `StaffHubApp`
* `PosApp`

Tất cả đều gọi `RequireAuthenticatedUser()` trước khi kiểm tra requirement quyền.

`Program.cs` hiện chỉ thể hiện:

```csharp
app.UseCafeChainPipeline();
app.MapCafeChainEndpoints();
```

Do đó phải mở định nghĩa thật sự của hai extension này để kiểm tra middleware order và cách map hub/endpoint.

## 4. Giả thuyết cần chứng minh hoặc loại trừ

### A. Data Protection key không được persist

Tìm toàn dự án:

```text
AddDataProtection
PersistKeysToFileSystem
PersistKeysToStackExchangeRedis
PersistKeysToAzureBlobStorage
SetApplicationName
DataProtection
```

Xác định:

* Production có lưu Data Protection key ra persistent volume không.
* Host có chạy Docker/container với filesystem tạm thời không.
* App có bị sleep, restart hoặc recycle sau 5–10 phút không.
* Có nhiều replica/instance không.
* Các instance có dùng chung key ring không.
* Application name có ổn định giữa các lần deploy không.
* IIS Application Pool có load user profile không nếu dùng IIS.
* Log có `Unprotect ticket failed`, `key not found`, `DataProtection` hoặc lỗi giải mã cookie không.

Nếu cookie vẫn còn trong trình duyệt nhưng server trả 401 sau khi process/container đổi thì ưu tiên kết luận Data Protection key bị mất.

### B. Thời hạn cookie bị ghi đè lúc đăng nhập

Tìm tất cả:

```text
SignInAsync
AuthenticationProperties
IsPersistent
ExpiresUtc
IssuedUtc
AllowRefresh
ExpireTimeSpan
SlidingExpiration
AddMinutes
AddHours
AddDays
```

Kiểm tra:

* Chỗ đăng nhập có đặt `ExpiresUtc = AddMinutes(5)` hoặc `AddMinutes(10)` không.
* Có đặt `IsPersistent = true` không.
* Có code phát hành lại cookie với thời hạn ngắn không.
* Có `ValidatePrincipal`, security stamp hoặc custom cookie event thu hồi principal không.
* Có endpoint hoặc middleware tự gọi `SignOutAsync` không.
* Có response nào gửi `Set-Cookie` để xóa authentication cookie không.
* `ExpireTimeSpan = 7 ngày` có đang bị `AuthenticationProperties.ExpiresUtc` ghi đè không.

### C. ASP.NET Session hoặc permission cache bị mất

Tìm:

```text
AddSession
UseSession
IdleTimeout
ISession
HttpContext.Session
.AspNetCore.Session
IDistributedCache
AddDistributedMemoryCache
AddMemoryCache
IMemoryCache
ConcurrentDictionary
static Dictionary
```

Kiểm tra:

* Có lưu user ID, role, permission hoặc POS access session trong ASP.NET Session không.
* Có dùng in-memory session/cache trong production không.
* Có cấu hình `IdleTimeout` 5–10 phút không.
* Nếu có nhiều instance, session/cache có bị tách riêng theo từng instance không.
* Permission handler có phụ thuộc dữ liệu nằm trong session hoặc memory cache không.
* Việc “mất quyền” là dữ liệu quyền thật sự bị thay đổi hay chỉ vì `HttpContext.User` không còn authenticated.

### D. Sai middleware order

Mở toàn bộ `UseCafeChainPipeline()` và kiểm tra thứ tự thực tế:

```csharp
UseForwardedHeaders
UseHttpsRedirection
UseRouting
UseCors
UseCookiePolicy
UseSession
UseAuthentication
UseAuthorization
MapControllers
MapRazorPages
MapHub
```

Phải xác minh:

* `UseAuthentication()` chạy trước `UseAuthorization()`.
* Hai middleware này chạy trước endpoint/hub.
* `UseForwardedHeaders()` chạy đủ sớm khi có reverse proxy.
* CORS policy đúng thứ tự và cho phép credentials nếu có cross-origin.
* Cookie policy có đang ghi đè `SameSite` không.

Không được kết luận middleware đúng chỉ dựa vào `Program.cs`, vì logic đang nằm trong extension.

### E. Cookie bị xung đột

Kiểm tra:

* Cookie authentication có đang dùng tên mặc định `.AspNetCore.Cookies` không.
* Domain `cafechain.site` có nhiều app cùng tạo cookie tên này không.
* Cookie `Domain`, `Path`, `Secure`, `HttpOnly`, `SameSite`, `Expires` và `Max-Age`.
* Có cookie trùng tên nhưng khác `Path` hoặc `Domain` không.
* Subdomain nào khác đang ghi đè/xóa cookie không.

Nếu không cần chia sẻ cookie, đề xuất tên riêng như:

```csharp
options.Cookie.Name = ".CafeChain.Auth";
options.Cookie.Path = "/";
```

Nếu cố ý chia sẻ cookie giữa nhiều app thì phải kiểm tra cùng cookie name, authentication scheme, Data Protection key ring và application name.

### F. SignalR StaffHub dùng sai authentication scheme

Tìm:

```text
MapHub
HubConnectionBuilder
withUrl
accessTokenFactory
withCredentials
withAutomaticReconnect
AuthorizeAttribute
AuthenticationSchemes
JwtBearerDefaults.AuthenticationScheme
CookieAuthenticationDefaults.AuthenticationScheme
StaffHub
inventory-notifications
workshifts
negotiate
```

Với từng hub, lập bảng:

* Route thực tế.
* Policy được yêu cầu.
* Authentication scheme được chọn.
* Client gửi cookie hay JWT.
* Server mong đợi cookie hay JWT.
* Có `accessTokenFactory` không.
* Token được gửi trong `Authorization` header hay `access_token` query.
* Route có khớp hai path trong `OnMessageReceived` không.

Lưu ý: scheme mặc định hiện là Cookie. Nếu hub chỉ có `[Authorize]`, Bearer handler có thể không chạy dù client gửi JWT. Nếu hub POS phải dùng JWT, kiểm tra có khai báo rõ:

```csharp
[Authorize(AuthenticationSchemes =
    JwtBearerDefaults.AuthenticationScheme)]
```

hoặc policy có chỉ định `Bearer` hay không.

Không tự đổi toàn bộ hub sang Bearer nếu StaffHub được thiết kế dùng cookie. Phải xác định scheme đúng cho từng nhóm endpoint.

### G. JWT POS được tạo sai hoặc hết hạn sớm

Tìm toàn bộ nơi tạo JWT:

```text
JwtSecurityToken
SecurityTokenDescriptor
SigningCredentials
Jwt:Key
Jwt:Issuer
Jwt:Audience
expires
notBefore
jti
PosSessionId
WriteToken
```

Kiểm tra:

* `exp`, `nbf`, issuer, audience và signing key.
* Có dùng `DateTime.Now` thay vì UTC không.
* Thời gian server production có sai lệch không.
* `Jwt:Key`, issuer và audience giữa nơi phát hành và nơi xác thực có giống nhau không.
* Có cấu hình riêng cho Production ghi đè giá trị không.
* JWT có thật sự chứa đúng `PosSessionId` và `jti` không.
* Client có lưu đúng token mới nhất không.
* Client có gửi nhầm authentication cookie thay vì JWT không.
* Client có giữ token cũ sau khi mở một POS session mới không.

### H. `IPosAccessSessionService.ValidateAsync()` thu hồi JWT

Đây là phần bắt buộc phải truy vết sâu.

Tìm:

* Interface và tất cả implementation của `IPosAccessSessionService`.
* Code tạo POS session.
* Entity/database table tương ứng.
* `ValidateAsync`.
* `Revoke`, `Close`, `Expire`, `Cleanup`.
* BackgroundService, hosted worker, cron job hoặc cleanup worker.
* TTL, idle timeout, work shift status và access mode.
* Logic giới hạn một POS session cho mỗi user/device/browser.
* Logic khi mở tab mới hoặc đăng nhập tài khoản khác.
* Transaction cập nhật `jti`, session ID và trạng thái active.

Phải trả lời:

1. POS session được lưu trong database, distributed cache hay memory?
2. Thời hạn chính xác của POS session là bao nhiêu?
3. Worker nào có thể đóng session sau 5–10 phút?
4. `ValidateAsync()` có yêu cầu work shift đang mở không?
5. Một request khác có thay `jti` khiến JWT vừa phát hành bị vô hiệu không?
6. Có race condition khi nhiều tab cùng gọi mở POS không?
7. Khi validation thất bại, `ErrorCode` và `Message` cụ thể là gì?
8. JWT thất bại trước hay sau khi gọi `ValidateAsync()`?

### I. Hosting và nhiều instance

Kiểm tra các file:

```text
Dockerfile
docker-compose*
nginx*
web.config
appsettings*.json
launchSettings.json
hosting.json
render.yaml
railway*
fly.toml
Procfile
CI/CD workflows
deployment scripts
```

Xác định:

* Nền tảng host thực tế.
* App có sleep khi không hoạt động không.
* Restart/redeploy frequency.
* Số lượng instance.
* Persistent volume.
* Sticky session/session affinity.
* Reverse proxy headers.
* WebSocket support.
* Data Protection key store.
* Distributed cache.

Lưu ý: thiếu sticky session thường gây lỗi SignalR connection sau negotiate, nhưng lỗi hiện tại là 401 ngay tại `/negotiate`, nên phải ưu tiên authentication cookie/JWT trước.

## 5. Bổ sung logging chẩn đoán an toàn nếu chưa đủ bằng chứng

Chỉ thêm logging tạm thời nếu cần. Không log nội dung cookie, JWT, mật khẩu hoặc signing key.

Có thể log:

* Timestamp UTC.
* Hostname/instance ID/process ID.
* Request path.
* Authentication scheme được dùng.
* Cookie có tồn tại hay không, chỉ boolean.
* `AuthenticateResult.Succeeded`.
* Loại exception từ `AuthenticateResult.Failure`.
* `IssuedUtc` và `ExpiresUtc`.
* JWT issuer/audience và `exp`, nhưng không log raw token.
* `PosSessionId`, có thể mask nếu cần.
* Kết quả `ValidateAsync`, error code và reason.
* User ID đã mask.
* Thời điểm worker thu hồi POS session.
* App startup/restart.
* Data Protection warning/error.

Bổ sung `OnAuthenticationFailed` cho JWT để biết JWT thất bại do:

* Expired.
* Invalid signature.
* Invalid issuer.
* Invalid audience.
* Missing token.
* POS session validation failure.

## 6. Kiểm tra thực tế trên browser production

Hướng dẫn kiểm tra tại thời điểm vừa đăng nhập và sau khi lỗi xuất hiện:

1. Application → Cookies → `https://cafechain.site`.
2. Ghi nhận tên cookie, Domain, Path, SameSite, Secure và Expires.
3. Network → request `/negotiate`.
4. Xem Request Headers có gửi authentication cookie hoặc Authorization header không.
5. Xem response có `Set-Cookie` xóa cookie không.
6. Giải mã payload JWT để xem `exp`, `nbf`, `iss`, `aud`, `jti`, `PosSessionId`; không cần và không được cung cấp secret.
7. So sánh thời điểm lỗi với log restart của host và log cleanup POS session.

Phân nhánh kết luận:

* Cookie không có trong request: lỗi thuộc tính cookie, domain/path, SameSite, Secure hoặc frontend.
* Cookie có trong request nhưng cookie authentication thất bại: kiểm tra Data Protection key, ticket expiry, cookie collision và scheme.
* JWT không có trong request: lỗi frontend/token storage/SignalR configuration.
* JWT có nhưng Bearer handler không chạy: sai authentication scheme/policy.
* Bearer handler chạy nhưng chữ ký hoặc `exp` sai: lỗi phát hành/config JWT.
* JWT cryptographically hợp lệ nhưng `ValidateAsync()` fail: lỗi POS access session, worker, database, TTL hoặc `jti`.

## 7. Yêu cầu kết quả cuối cùng

Sau khi điều tra, hãy cung cấp:

1. Sơ đồ ngắn luồng Cookie → StaffHub → mở POS → JWT → POS session.
2. Danh sách file và method thực sự tham gia.
3. Nguyên nhân gốc đã chứng minh, kèm bằng chứng code/log.
4. Tách riêng:

   * Lỗi cookie web.
   * Lỗi SignalR StaffHub.
   * Lỗi JWT POS.
   * Lỗi POS server-side session.
5. Chỉ rõ lỗi nào là nguyên nhân và lỗi nào chỉ là hậu quả.
6. Trích chính xác file và số dòng.
7. Đề xuất bản sửa tối thiểu.
8. Các rủi ro của bản sửa.
9. Test hồi quy cần bổ sung.
10. Checklist xác minh sau deploy.

Không trả lời chung chung kiểu “có thể do cookie”. Nếu chưa đủ bằng chứng để kết luận, hãy nói chính xác dữ liệu nào còn thiếu và thêm instrumentation để lấy dữ liệu đó.

Không tự ý giảm bảo mật như:

* Tắt kiểm tra lifetime.
* Tắt issuer/audience validation.
* Đưa JWT secret ra client.
* Dùng `AllowAnyOrigin()` cùng credentials.
* Bỏ `Secure`.
* Chuyển toàn bộ session sang thời hạn vô hạn.
* Bỏ kiểm tra POS server-side session.

Trước khi sửa code, hãy báo cáo chẩn đoán theo mức độ tin cậy. Chỉ thực hiện bản sửa khi đã xác định được luồng gây lỗi.
