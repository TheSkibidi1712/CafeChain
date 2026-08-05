using CafeChain.Models.Drinks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CafeChain.Data.Configurations.Drinks.Toppings
{
    public class DrinkSizeToppingPolicyConfiguration : IEntityTypeConfiguration<DrinkSizeToppingPolicy>
    {
        public void Configure(EntityTypeBuilder<DrinkSizeToppingPolicy> entity)
        {
            entity.ToTable("DrinkSizeToppingPolicies", table =>
            {
                table.HasCheckConstraint("CK_DrinkSizeToppingPolicies_Quantity", "[QuantityPerDrink] > 0");
                table.HasCheckConstraint("CK_DrinkSizeToppingPolicies_PriceTreatment", "[PriceTreatment] IN ('INCLUDED_IN_BASE_PRICE','ADD_TOPPING_PRICE')");
                table.HasCheckConstraint("CK_DrinkSizeToppingPolicies_CostTreatment", "[CostTreatment] IN ('INCLUDED_IN_DRINK_RECIPE','ADD_TOPPING_RECIPE_COST','DISPLAY_ONLY')");
                table.HasCheckConstraint("CK_DrinkSizeToppingPolicies_RequiredDefault", "[IsRequired] = 0 OR [IsDefaultSelected] = 1");
                table.HasCheckConstraint("CK_DrinkSizeToppingPolicies_QuantityUnit", "[QuantityUnit] = 'RECIPE_PORTION'");
            });

            entity.HasKey(x => x.DrinkSizeToppingPolicyId);
            entity.Property(x => x.PriceTreatment).IsRequired().HasMaxLength(40);
            entity.Property(x => x.CostTreatment).IsRequired().HasMaxLength(40);
            entity.Property(x => x.QuantityPerDrink).HasColumnType("decimal(18,5)");
            entity.Property(x => x.QuantityUnit).IsRequired().HasMaxLength(32).HasDefaultValue("RECIPE_PORTION");
            entity.Property(x => x.IsRequired).HasDefaultValue(false);
            entity.Property(x => x.CreatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.UpdatedAtUtc).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.RowVersion).IsRowVersion().IsConcurrencyToken();

            entity.HasOne(x => x.DrinkSize).WithMany(x => x.ToppingPolicies)
                .HasForeignKey(x => x.DrinkSizeId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Topping).WithMany()
                .HasForeignKey(x => x.ToppingId).OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.DrinkSizeId, x.ToppingId });
            entity.HasIndex(x => new { x.DrinkSizeId, x.ToppingId })
                .IsUnique()
                .HasFilter("[IsActive] = 1")
                .HasDatabaseName("UX_DrinkSizeToppingPolicies_Active");
        }
    }
}
