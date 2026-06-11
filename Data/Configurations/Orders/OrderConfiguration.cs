using CafeChain.Models;
using CafeChain.Models.Orders;
using CafeChain.Models.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Reflection.Emit;

namespace CafeChain.Data.Configurations.Orders
{
    // ========================== ORDER ==========================
    public class OrderConfiguration : IEntityTypeConfiguration<Order>
    {
        public void Configure(EntityTypeBuilder<Order> entity)
        {
            entity.ToTable("Orders");

            entity.HasKey(x => x.OrderId);

            // ================= TEXT =================
            entity.Property(x => x.Source)
                .HasMaxLength(50);

            entity.Property(x => x.Note)
                .HasMaxLength(500);

            // ================= MONEY (QUAN TRỌNG) =================
            entity.Property(x => x.SubTotal)
                .HasColumnType("decimal(18,2)")
                .HasDefaultValue(0);

            entity.Property(x => x.VoucherDiscount)
                .HasColumnType("decimal(18,2)")
                .HasDefaultValue(0);

            entity.Property(x => x.PointDiscount)
                .HasColumnType("decimal(18,2)")
                .HasDefaultValue(0);

            entity.Property(x => x.PointsUsed)
                .HasDefaultValue(0);

            entity.Property(x => x.Total)
                .HasColumnType("decimal(18,2)");

            // ================= TIME =================
            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            // ================= RELATION =================
            entity.HasOne(x => x.Customer)
                .WithMany(x => x.Orders)
                .HasForeignKey(x => x.CustomerId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.Store)
                .WithMany(x => x.Orders)
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Staff)
                .WithMany(x => x.Orders)
                .HasForeignKey(x => x.StaffId)
                .OnDelete(DeleteBehavior.NoAction); // tránh multiple cascade

            entity.HasOne(x => x.OrderStatus)
                .WithMany(x => x.Orders)
                .HasForeignKey(x => x.OrderStatusId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OrderType)
                .WithMany(x => x.Orders)
                .HasForeignKey(x => x.OrderTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            // ================= INDEX =================
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => x.CustomerId);
            entity.HasIndex(x => x.StoreId);
            entity.HasIndex(x => x.StaffId);

            // ADR-0002: Idempotency Key — Unique chỉ khi ClientOrderId IS NOT NULL
            // Đơn online (ClientOrderId = null) không bị ảnh hưởng bởi constraint này
            entity.HasIndex(x => x.ClientOrderId)
                .IsUnique()
                .HasFilter("[ClientOrderId] IS NOT NULL")
                .HasDatabaseName("IX_Orders_ClientOrderId_Unique");

           
        }
    }

    // ========================== ORDER DETAIL ==========================
    public class OrderDetailConfiguration : IEntityTypeConfiguration<OrderDetail>
    {
        public void Configure(EntityTypeBuilder<OrderDetail> entity)
        {
            entity.ToTable("OrderDetails");

            entity.HasKey(x => x.OrderDetailId);

            entity.Property(x => x.DrinkName)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.SizeName)
                .HasMaxLength(50);

            entity.Property(x => x.Price)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.Quantity)
                .IsRequired();

            entity.Property(x => x.Note)
                .HasMaxLength(500);

            // RELATION
            entity.HasOne(x => x.Order)
                .WithMany(x => x.OrderDetails)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade); // giữ cái này

            entity.HasOne(x => x.Drink)
                .WithMany(x => x.OrderDetails)
                .HasForeignKey(x => x.DrinkId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Size)
                .WithMany(x => x.OrderDetails)
                .HasForeignKey(x => x.SizeId)
                .OnDelete(DeleteBehavior.SetNull);

            // INDEX
            entity.HasIndex(x => x.OrderId);
            entity.HasIndex(x => x.DrinkId);
            entity.HasIndex(x => x.SizeId);

           

        }
    }

    // ========================== ORDER TOPPING ==========================
    public class OrderToppingConfiguration : IEntityTypeConfiguration<OrderTopping>
    {
        public void Configure(EntityTypeBuilder<OrderTopping> entity)
        {
            entity.ToTable("OrderToppings");

            entity.HasKey(x => x.OrderToppingId);

            entity.Property(x => x.ToppingName)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.Price)
                .HasColumnType("decimal(18,2)");

            entity.HasOne(x => x.OrderDetail)
                .WithMany(x => x.OrderToppings)
                .HasForeignKey(x => x.OrderDetailId)
                .OnDelete(DeleteBehavior.Cascade); // chain chính

            entity.HasOne(x => x.Topping)
                .WithMany(o => o.OrderToppings)
                .HasForeignKey(x => x.ToppingId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.OrderDetailId, x.ToppingId })
                .IsUnique();

        }
    }

    // ========================== ORDER STATUS ==========================
    public class OrderStatusConfiguration : IEntityTypeConfiguration<OrderStatus>
    {
        public void Configure(EntityTypeBuilder<OrderStatus> entity)
        {
            entity.ToTable("OrderStatuses");

            entity.HasKey(x => x.OrderStatusId);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasIndex(x => x.Name)
                .IsUnique();

            entity.HasData(
                new OrderStatus { OrderStatusId = 7, Name = "Chờ thanh toán", BadgeColor = "badge bg-warning" },
                new OrderStatus { OrderStatusId = 1, Name = "Chờ xác nhận", BadgeColor = "badge bg-secondary" },
                new OrderStatus { OrderStatusId = 2, Name = "Đang pha chế", BadgeColor = "badge bg-primary" },
                new OrderStatus { OrderStatusId = 3, Name = "Chờ lấy hàng", BadgeColor = "badge bg-info text-dark" },
                new OrderStatus { OrderStatusId = 4, Name = "Đang giao hàng", BadgeColor = "badge bg-warning text-dark" },
                new OrderStatus { OrderStatusId = 5, Name = "Hoàn thành", BadgeColor = "badge bg-success" },
                new OrderStatus { OrderStatusId = 6, Name = "Đã hủy", BadgeColor = "badge bg-danger" }
            );
        }
    }

    // ========================== ORDER TYPE ==========================
    public class OrderTypeConfiguration : IEntityTypeConfiguration<OrderType>
    {
        public void Configure(EntityTypeBuilder<OrderType> entity)
        {
            entity.ToTable("OrderTypes");

            entity.HasKey(x => x.OrderTypeId);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasIndex(x => x.Name)
                .IsUnique();

            entity.HasData(
                new OrderType { OrderTypeId = 1, Name = "Dine In" },
                new OrderType { OrderTypeId = 2, Name = "Take Away" },
                new OrderType { OrderTypeId = 3, Name = "Delivery" }
            );
        }
    }
}