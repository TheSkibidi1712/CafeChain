using CafeChain.Models;
using CafeChain.Models.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations
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
                .WithMany()
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Staff)
                .WithMany()
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

            // ================= SEED DATA (FIX LẠI) =================
            entity.HasData(
                new Order
                {
                    OrderId = 1,
                    CustomerId = 1,
                    StoreId = 1,
                    OrderStatusId = 3,
                    OrderTypeId = 1,
                    TableId = 1,
                    StaffId = 1,
                    Source = "POS",
                    Note = "",

                    SubTotal = 45000,
                    VoucherDiscount = 0,
                    PointDiscount = 0,
                    PointsUsed = 0,
                    Total = 45000,

                    CreatedAt = new DateTime(2025, 1, 1, 8, 0, 0)
                },
                new Order
                {
                    OrderId = 2,
                    CustomerId = 2,
                    StoreId = 1,
                    OrderStatusId = 2,
                    OrderTypeId = 2,
                    TableId = null,
                    StaffId = 2,
                    Source = "APP",
                    Note = "Ít đá",

                    SubTotal = 60000,
                    VoucherDiscount = 0,
                    PointDiscount = 0,
                    PointsUsed = 0,
                    Total = 60000,

                    CreatedAt = new DateTime(2025, 1, 1, 9, 0, 0)
                },
                new Order
                {
                    OrderId = 3,
                    CustomerId = 3,
                    StoreId = 2,
                    OrderStatusId = 1,
                    OrderTypeId = 3,
                    TableId = 3,
                    StaffId = 3,
                    Source = "POS",
                    Note = "",

                    SubTotal = 70000,
                    VoucherDiscount = 0,
                    PointDiscount = 0,
                    PointsUsed = 0,
                    Total = 70000,

                    CreatedAt = new DateTime(2025, 1, 1, 10, 0, 0)
                }
            );
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
                .WithMany()
                .HasForeignKey(x => x.DrinkId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Size)
                .WithMany()
                .HasForeignKey(x => x.SizeId)
                .OnDelete(DeleteBehavior.SetNull);

            // INDEX
            entity.HasIndex(x => x.OrderId);
            entity.HasIndex(x => x.DrinkId);
            entity.HasIndex(x => x.SizeId);

            entity.HasData(
                new OrderDetail
                {
                    OrderDetailId = 1,
                    OrderId = 1,
                    DrinkId = 1,
                    SizeId = 2,
                    DrinkName = "Cà phê sữa",
                    SizeName = "M",
                    Price = 25000,
                    Quantity = 1,
                    Note = ""
                },
                new OrderDetail
                {
                    OrderDetailId = 2,
                    OrderId = 1,
                    DrinkId = 2,
                    SizeId = 2,
                    DrinkName = "Cà phê đen",
                    SizeName = "M",
                    Price = 20000,
                    Quantity = 1,
                    Note = ""
                },
                new OrderDetail
                {
                    OrderDetailId = 3,
                    OrderId = 2,
                    DrinkId = 3,
                    SizeId = 3,
                    DrinkName = "Trà sữa trân châu",
                    SizeName = "L",
                    Price = 60000,
                    Quantity = 1,
                    Note = "Ít đá"
                }
            );

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

            entity.HasData(
                new OrderTopping
                {
                    OrderToppingId = 1,
                    OrderDetailId = 3,
                    ToppingId = 1,
                    ToppingName = "Trân châu đen",
                    Price = 5000
                },
                new OrderTopping
                {
                    OrderToppingId = 2,
                    OrderDetailId = 3,
                    ToppingId = 2,
                    ToppingName = "Trân châu trắng",
                    Price = 5000
                }
            );
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
                new OrderStatus { OrderStatusId = 1, Name = "Pending" },        // mới tạo
                new OrderStatus { OrderStatusId = 2, Name = "Confirmed" },      // đã xác nhận
                new OrderStatus { OrderStatusId = 3, Name = "Preparing" },      // đang pha chế
                new OrderStatus { OrderStatusId = 4, Name = "Ready" },          // đã xong, chờ lấy
                new OrderStatus { OrderStatusId = 5, Name = "Completed" },      // hoàn tất
                new OrderStatus { OrderStatusId = 6, Name = "Cancelled" }       // hủy
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