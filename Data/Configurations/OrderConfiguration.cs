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

            entity.HasKey(x => x.OrdId);

            entity.Property(x => x.Source)
                .HasMaxLength(50);

            entity.Property(x => x.Note)
                .HasMaxLength(500);

            entity.Property(x => x.Total)
                .HasColumnType("decimal(18,2)");

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            // RELATION
            entity.HasOne(x => x.Customer)
                .WithMany(x => x.Orders)
                .HasForeignKey(x => x.CusId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.Store)
                .WithMany()
                .HasForeignKey(x => x.StoId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.DiningTable)
                .WithMany(x => x.Orders)
                .HasForeignKey(x => x.TabId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.Staff)
                .WithMany()
                .HasForeignKey(x => x.StaId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(x => x.OrderStatus)
                .WithMany(x => x.Orders)
                .HasForeignKey(x => x.OrSId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.OrderType)
                .WithMany(x => x.Orders)
                .HasForeignKey(x => x.OrTId)
                .OnDelete(DeleteBehavior.Restrict);

            // INDEX
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => x.CusId);
            entity.HasIndex(x => x.StoId);
            entity.HasIndex(x => x.StaId);
        }
    }

    // ========================== ORDER DETAIL ==========================
    public class OrderDetailConfiguration : IEntityTypeConfiguration<OrderDetail>
    {
        public void Configure(EntityTypeBuilder<OrderDetail> entity)
        {
            entity.ToTable("OrderDetails");

            entity.HasKey(x => x.OrDId);

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
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Drink)
                .WithMany()
                .HasForeignKey(x => x.DriId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Size)
                .WithMany()
                .HasForeignKey(x => x.SizId)
                .OnDelete(DeleteBehavior.SetNull);

            // INDEX
            entity.HasIndex(x => x.OrderId);
            entity.HasIndex(x => x.DriId);
            entity.HasIndex(x => x.SizId);
        }
    }

    // ========================== ORDER TOPPING ==========================
    public class OrderToppingConfiguration : IEntityTypeConfiguration<OrderTopping>
    {
        public void Configure(EntityTypeBuilder<OrderTopping> entity)
        {
            entity.ToTable("OrderToppings");

            entity.HasKey(x => x.OrTgId);

            entity.Property(x => x.ToppingName)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(x => x.Price)
                .HasColumnType("decimal(18,2)");

            entity.HasOne(x => x.OrderDetail)
                .WithMany(x => x.OrderToppings)
                .HasForeignKey(x => x.OrDId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Topping)
                .WithMany()
                .HasForeignKey(x => x.TopId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.OrDId, x.TopId })
                .IsUnique();
        }
    }

    // ========================== ORDER STATUS ==========================
    public class OrderStatusConfiguration : IEntityTypeConfiguration<OrderStatus>
    {
        public void Configure(EntityTypeBuilder<OrderStatus> entity)
        {
            entity.ToTable("OrderStatuses");

            entity.HasKey(x => x.OrSId);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasIndex(x => x.Name)
                .IsUnique();
        }
    }

    // ========================== ORDER TYPE ==========================
    public class OrderTypeConfiguration : IEntityTypeConfiguration<OrderType>
    {
        public void Configure(EntityTypeBuilder<OrderType> entity)
        {
            entity.ToTable("OrderTypes");

            entity.HasKey(x => x.OrTId);

            entity.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasIndex(x => x.Name)
                .IsUnique();
        }
    }

    // ========================== DINING TABLE ==========================
    public class DiningTableConfiguration : IEntityTypeConfiguration<DiningTable>
    {
        public void Configure(EntityTypeBuilder<DiningTable> entity)
        {
            entity.ToTable("DiningTables");

            entity.HasKey(x => x.TabId);

            entity.Property(x => x.TableNumber)
                .IsRequired();

            entity.Property(x => x.Status)
                .HasMaxLength(50);

            entity.Property(x => x.Active)
                .HasDefaultValue(true);

            entity.HasOne(x => x.Store)
                .WithMany()
                .HasForeignKey(x => x.StoId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => new { x.StoId, x.TableNumber })
                .IsUnique();
        }
    }

    // ========================== KITCHEN ORDER ==========================
    public class KitchenOrderConfiguration : IEntityTypeConfiguration<KitchenOrder>
    {
        public void Configure(EntityTypeBuilder<KitchenOrder> entity)
        {
            entity.ToTable("KitchenOrders");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Status)
                .HasMaxLength(50);

            entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("GETDATE()");

            entity.HasOne(x => x.Order)
                .WithMany(x => x.KitchenOrders)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.OrderId);
        }
    }
}