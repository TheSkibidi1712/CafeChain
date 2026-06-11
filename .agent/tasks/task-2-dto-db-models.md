# 🤖 TASK 2: NÂNG CẤP DTO & CẤU TRÚC CSDL HỖ TRỢ POS TERMINAL & AUDIT
> **Mục tiêu:** Định nghĩa các lớp DTO mới cho đăng ký nhanh khách hàng và ủy quyền, tạo thực thể `PosTerminal` mới, cập nhật `InvoiceAuditLog` và `WorkShift`, cấu hình quan hệ khóa ngoại EF Core, và thực hiện migration CSDL.

---

## 1. TÀI LIỆU THAM KHẢO NGỮ CẢNH
* Cấu trúc CSDL và DTO tham khảo tại: [POS_AI_SYSTEM_INSTRUCTIONS.md](file:///d:/FPL_KY2/DATN/BE/CafeChain/POS_AI_SYSTEM_INSTRUCTIONS.md)
* Quy tắc kiến trúc & an ninh mạng: [dotnet-architecture.md](file:///d:/FPL_KY2/DATN/BE/CafeChain/.agent/rules/dotnet-architecture.md)

---

## 2. QUY TẮC KIẾN TRÚC BẮT BUỘC TUÂN THỦ (ARCHITECTURAL COMPLIANCE)
Model AI khi thực thi task này bắt buộc phải tuân thủ nghiêm ngặt các quy tắc kiến trúc sau:
1. **Repository Pattern**: Tất cả truy cập cơ sở dữ liệu phải được thực hiện thông qua Interface Repository thay vì tiêm hoặc truy vấn trực tiếp `AppDbContext` trong controller hoặc service (trừ các cấu hình DbContext).
2. **Thin Controller**: Controller chỉ chịu trách nhiệm điều phối request, check `ModelState`, gọi Service và trả về View/JSON. Không viết logic nghiệp vụ trong controller.
3. **No Direct Entity Exposure**: Tuyệt đối không expose thực thể DB (`Staff`, `Store`) trực tiếp cho Client. Tất cả giao tiếp qua API đều phải sử dụng các lớp DTO/ViewModel an toàn đã được định nghĩa.
4. **Zero-Trust Security & Anti-IDOR**: Trích xuất danh tính (ID, StoreId) trực tiếp từ Cookie Claims ở Server-side, tuyệt đối không tin tưởng các trường input ẩn hoặc query parameters được truyền từ client.
5. **Asynchronous (Async/Await)**: Mọi thao tác truy cập I/O database bắt buộc sử dụng phương thức bất đồng bộ.

---

## 2. DANH SÁCH FILE CẦN THAO TÁC (TARGET FILES)
* 📄 [POSOrderCommitDto.cs](file:///d:/FPL_KY2/DATN/BE/CafeChain/Application/DTOs/POS/POSOrderCommitDto.cs) [MODIFY]
* 📄 [PosTerminal.cs](file:///d:/FPL_KY2/DATN/BE/CafeChain/Models/Stores/PosTerminal.cs) [NEW]
* 📄 [WorkShift.cs](file:///d:/FPL_KY2/DATN/BE/CafeChain/Models/Stores/WorkShift.cs) [MODIFY]
* 📄 [InvoiceAuditLog.cs](file:///d:/FPL_KY2/DATN/BE/CafeChain/Models/Orders/InvoiceAuditLog.cs) [MODIFY]
* 📄 [AppDbContext.cs](file:///d:/FPL_KY2/DATN/BE/CafeChain/Data/AppDbContext.cs) [MODIFY]
* 📄 [StoreConfiguration.cs](file:///d:/FPL_KY2/DATN/BE/CafeChain/Data/Configurations/Stores/StoreConfiguration.cs) [MODIFY]
* 📄 [InvoiceAuditLogConfiguration.cs](file:///d:/FPL_KY2/DATN/BE/CafeChain/Data/Configurations/Orders/InvoiceAuditLogConfiguration.cs) [MODIFY]

---

## 3. CÁC BƯỚC THỰC THI (STEP-BY-STEP INSTRUCTIONS)

### 🔹 Bước 1: Bổ sung DTO mới vào `POSOrderCommitDto.cs`
* Thêm hai class DTO sau vào cuối file trong namespace `CafeChain.Application.DTOs.POS`:
  ```csharp
  public class QuickCustomerRegisterDto
  {
      public string Phone { get; set; } = null!;
      public string FullName { get; set; } = null!;
      public DateTime? DateOfBirth { get; set; }
  }

  public class BypassAuthorizationRequest
  {
      public string Pin { get; set; } = null!;
      public string ActionName { get; set; } = null!; // Ví dụ: "SOFT_VOUCHER_BYPASS", "OPEN_SHIFT_LATE", "PRICE_OVERRIDE"
      public int? TargetId { get; set; }
      public string Reason { get; set; } = null!;
      public decimal? DiscountValue { get; set; }
  }
  ```

### 🔹 Bước 2: Tạo thực thể `PosTerminal.cs` mới
* Tạo file `PosTerminal.cs` tại thư mục `Models/Stores/` với nội dung:
  ```csharp
  using System;
  using System.Collections.Generic;
  using System.ComponentModel.DataAnnotations;

  namespace CafeChain.Models.Stores
  {
      /// <summary>
      /// Thực thể đại diện cho thiết bị POS Terminal tại quầy
      /// </summary>
      public class PosTerminal
      {
          [Key]
          public string TerminalId { get; set; } = string.Empty; // GUID từ localStorage
          public int StoreId { get; set; }
          [MaxLength(100)]
          public string Name { get; set; } = string.Empty; // Tên thân thiện
          public bool Active { get; set; } = true;
          public DateTime CreatedAt { get; set; } = DateTime.Now;

          public virtual Store Store { get; set; } = null!;
          public virtual ICollection<WorkShift> WorkShifts { get; set; } = new List<WorkShift>();
      }
  }
  ```

### 🔹 Bước 3: Cập nhật điều hướng trong `WorkShift.cs`
* Bổ sung thuộc tính điều hướng (navigation property) liên kết tới `PosTerminal`:
  ```csharp
  public virtual PosTerminal? PosTerminal { get; set; }
  ```
  đảm bảo thuộc tính đặt gần `public virtual Store Store { get; set; }`.

### 🔹 Bước 4: Cập nhật thực thể `InvoiceAuditLog.cs`
* Điều chỉnh thuộc tính `OrderId` thành kiểu Nullable `int?`:
  ```csharp
  public int? OrderId { get; set; }
  ```
* Bổ sung cột ghi nhận giá trị voucher/chiết khấu được duyệt bypass:
  ```csharp
  public decimal? DiscountValue { get; set; }
  ```

### 🔹 Bước 5: Cấu hình Fluent API trong `StoreConfiguration.cs` và `InvoiceAuditLogConfiguration.cs`
* **Trong `StoreConfiguration.cs`:**
  1. Thêm cấu hình Entity cho `PosTerminal`:
     ```csharp
     public class PosTerminalConfiguration : IEntityTypeConfiguration<PosTerminal>
     {
         public void Configure(EntityTypeBuilder<PosTerminal> entity)
         {
             entity.ToTable("PosTerminals");
             entity.HasKey(x => x.TerminalId);
             entity.Property(x => x.Name).IsRequired().HasMaxLength(100);
             entity.Property(x => x.Active).HasDefaultValue(true);
             entity.Property(x => x.CreatedAt).HasDefaultValueSql("GETDATE()");

             entity.HasOne(x => x.Store)
                 .WithMany()
                 .HasForeignKey(x => x.StoreId)
                 .OnDelete(DeleteBehavior.Cascade);
         }
     }
     ```
  2. Trong class `WorkShiftConfiguration`, bổ sung cấu hình liên kết ngoại tới `PosTerminal`:
     ```csharp
     entity.HasOne(x => x.PosTerminal)
         .WithMany(p => p.WorkShifts)
         .HasForeignKey(x => x.PosTerminalId)
         .OnDelete(DeleteBehavior.Restrict);
     ```
* **Trong `InvoiceAuditLogConfiguration.cs`:**
  1. Cấu hình `OrderId` là nullable:
     ```csharp
     entity.Property(x => x.OrderId).IsRequired(false);
     ```
  2. Cấu hình `DiscountValue` kiểu decimal:
     ```csharp
     entity.Property(x => x.DiscountValue)
         .HasColumnType("decimal(18,2)")
         .IsRequired(false);
     ```

### 🔹 Bước 6: Đăng ký `DbSet` trong `AppDbContext.cs`
* Bổ sung vào nhóm `// ========================= STORE =========================`:
  ```csharp
  public DbSet<PosTerminal> PosTerminals { get; set; }
  ```
* Đảm bảo trong hàm `OnModelCreating` của `AppDbContext.cs` đã load configurations (nếu dự án dùng `builder.ApplyConfigurationsFromAssembly` thì không cần làm gì thêm, nếu load thủ công thì phải đăng ký `builder.ApplyConfiguration(new PosTerminalConfiguration())`).

---

## 4. KẾ HOẠCH XÁC MINH (VERIFICATION PLAN)
* Chạy build dự án: `dotnet build`.
* Thực hiện tạo bản migration mới trong Terminal/CLI:
  ```powershell
  dotnet ef migrations add AddPosTerminalAndUpgradeAuditLogs
  ```
* Cập nhật CSDL:
  ```powershell
  dotnet ef database update
  ```
* Xác minh trong SQL Server Management Studio (SSMS) hoặc Database Explorer:
  - Bảng `PosTerminals` được tạo với các trường `TerminalId` (PK, nvarchar), `StoreId`, `Name`, `Active`, `CreatedAt`.
  - Bảng `WorkShifts` có khóa ngoại `PosTerminalId` trỏ tới `PosTerminals.TerminalId`.
  - Bảng `InvoiceAuditLogs` có cột `OrderId` kiểu NULL, có thêm cột `DiscountValue` kiểu `decimal(18,2)` cho phép NULL.
