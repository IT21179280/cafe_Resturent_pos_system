using CafePOS.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CafePOS.Api.Data;

public class AppDbContext : IdentityDbContext<AppUser, AppRole, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductModifier> ProductModifiers => Set<ProductModifier>();
    public DbSet<Discount> Discounts => Set<Discount>();
    public DbSet<DiningTable> DiningTables => Set<DiningTable>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderItemModifier> OrderItemModifiers => Set<OrderItemModifier>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Refund> Refunds => Set<Refund>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<RecipeItem> RecipeItems => Set<RecipeItem>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<ShiftExpense> ShiftExpenses => Set<ShiftExpense>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AppUser>(e =>
        {
            e.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.HasMany(x => x.RefreshTokens).WithOne(x => x.User).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<AppRole>(e => e.Property(x => x.Name).HasMaxLength(100));

        builder.Entity<RefreshToken>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => new { x.UserId, x.ExpiresAt });
            e.Ignore(x => x.IsActive);
        });

        builder.Entity<Category>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.ImageUrl).HasMaxLength(500);
            e.HasIndex(x => x.Name).IsUnique().HasFilter("\"IsDeleted\" = false");
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<Product>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Sku).HasMaxLength(60).IsRequired();
            e.Property(x => x.Barcode).HasMaxLength(60);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.ImageUrl).HasMaxLength(500);
            e.HasIndex(x => x.Sku).IsUnique().HasFilter("\"IsDeleted\" = false");
            e.HasIndex(x => x.Barcode).IsUnique().HasFilter("\"Barcode\" IS NOT NULL AND \"IsDeleted\" = false");
            e.HasIndex(x => x.CategoryId);
            e.HasOne(x => x.Category).WithMany(x => x.Products).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<ProductModifier>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.HasOne(x => x.Product).WithMany(x => x.Modifiers).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<Discount>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<DiningTable>(e =>
        {
            e.Property(x => x.Number).HasMaxLength(30).IsRequired();
            e.Property(x => x.Area).HasMaxLength(100);
            e.HasIndex(x => x.Number).IsUnique().HasFilter("\"IsDeleted\" = false");
            e.HasQueryFilter(x => !x.IsDeleted);
            // Table occupancy is last-write-wins and protected with row locks in OrderService.
            // Keeping xmin as a concurrency token caused false "changed by another user"
            // failures when two POS screens touched the same table.
            e.Property<uint>("xmin").HasColumnName("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate();
        });

        builder.Entity<Order>(e =>
        {
            e.Property(x => x.OrderNumber).HasMaxLength(40).IsRequired();
            e.Property(x => x.ClientOrderId).HasMaxLength(80);
            e.Property(x => x.CustomerName).HasMaxLength(200);
            e.Property(x => x.CustomerPhone).HasMaxLength(40);
            e.Property(x => x.DeliveryAddress).HasMaxLength(500);
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.Property(x => x.CancelReason).HasMaxLength(500);
            e.HasIndex(x => x.OrderNumber).IsUnique();
            e.HasIndex(x => x.ClientOrderId).IsUnique().HasFilter("\"ClientOrderId\" IS NOT NULL");
            e.HasIndex(x => new { x.Status, x.PlacedAt });
            e.HasIndex(x => new { x.CashierId, x.PlacedAt });
            e.HasIndex(x => x.PlacedAt);
            e.HasOne(x => x.Table).WithMany().HasForeignKey(x => x.TableId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Cashier).WithMany().HasForeignKey(x => x.CashierId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Shift).WithMany().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Discount).WithMany().HasForeignKey(x => x.DiscountId).OnDelete(DeleteBehavior.SetNull);
            e.HasQueryFilter(x => !x.IsDeleted);
            e.Property<uint>("xmin").IsRowVersion();
        });

        builder.Entity<OrderItem>(e =>
        {
            e.Property(x => x.ProductName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasOne(x => x.Order).WithMany(x => x.Items).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.SetNull);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<OrderItemModifier>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.HasOne(x => x.OrderItem).WithMany(x => x.Modifiers).HasForeignKey(x => x.OrderItemId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<Payment>(e =>
        {
            e.Property(x => x.Reference).HasMaxLength(150);
            e.HasOne(x => x.Order).WithMany(x => x.Payments).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => !x.IsDeleted);
            e.Property<uint>("xmin").IsRowVersion();
        });

        builder.Entity<Refund>(e =>
        {
            e.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            e.HasOne(x => x.Order).WithMany(x => x.Refunds).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Payment).WithMany(x => x.Refunds).HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<Ingredient>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Sku).HasMaxLength(60);
            e.Property(x => x.Unit).HasMaxLength(20).IsRequired();
            e.Property(x => x.CurrentStock).HasPrecision(18, 3);
            e.Property(x => x.LowStockThreshold).HasPrecision(18, 3);
            e.Property(x => x.CostPerUnit).HasPrecision(18, 4);
            e.HasIndex(x => x.Name).IsUnique().HasFilter("\"IsDeleted\" = false");
            e.HasOne(x => x.DefaultSupplier).WithMany().HasForeignKey(x => x.DefaultSupplierId).OnDelete(DeleteBehavior.SetNull);
            e.HasQueryFilter(x => !x.IsDeleted);
            e.Property<uint>("xmin").IsRowVersion();
        });

        builder.Entity<RecipeItem>(e =>
        {
            e.Property(x => x.Quantity).HasPrecision(18, 3);
            e.HasIndex(x => new { x.ProductId, x.IngredientId }).IsUnique().HasFilter("\"IsDeleted\" = false");
            e.HasOne(x => x.Product).WithMany(x => x.RecipeItems).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Ingredient).WithMany(x => x.RecipeItems).HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<InventoryMovement>(e =>
        {
            e.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            e.Property(x => x.Reference).HasMaxLength(100);
            e.Property(x => x.Quantity).HasPrecision(18, 3);
            e.Property(x => x.BalanceAfter).HasPrecision(18, 3);
            e.Property(x => x.UnitCost).HasPrecision(18, 4);
            e.HasIndex(x => new { x.IngredientId, x.OccurredAt });
            e.HasIndex(x => x.OrderId);
            e.HasOne(x => x.Ingredient).WithMany(x => x.Movements).HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<Supplier>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(40);
            e.Property(x => x.Email).HasMaxLength(200);
            e.HasIndex(x => x.Name).IsUnique().HasFilter("\"IsDeleted\" = false");
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<PurchaseOrder>(e =>
        {
            e.Property(x => x.Number).HasMaxLength(40).IsRequired();
            e.HasIndex(x => x.Number).IsUnique();
            e.HasOne(x => x.Supplier).WithMany(x => x.PurchaseOrders).HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(x => !x.IsDeleted);
            e.Property<uint>("xmin").IsRowVersion();
        });

        builder.Entity<PurchaseOrderItem>(e =>
        {
            e.Property(x => x.Quantity).HasPrecision(18, 3);
            e.Property(x => x.ReceivedQuantity).HasPrecision(18, 3);
            e.Property(x => x.UnitCost).HasPrecision(18, 4);
            e.HasOne(x => x.PurchaseOrder).WithMany(x => x.Items).HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Ingredient).WithMany().HasForeignKey(x => x.IngredientId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<Shift>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.Status });
            e.HasOne(x => x.User).WithMany(x => x.Shifts).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(x => !x.IsDeleted);
            e.Property<uint>("xmin").IsRowVersion();
        });

        builder.Entity<ShiftExpense>(e =>
        {
            e.Property(x => x.Description).HasMaxLength(300).IsRequired();
            e.Property(x => x.Category).HasMaxLength(100);
            e.HasOne(x => x.Shift).WithMany(x => x.Expenses).HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(x => !x.IsDeleted);
        });

        builder.Entity<AuditLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.UserName).HasMaxLength(200);
            e.Property(x => x.Action).HasMaxLength(100).IsRequired();
            e.Property(x => x.EntityName).HasMaxLength(100).IsRequired();
            e.Property(x => x.EntityId).HasMaxLength(100);
            e.Property(x => x.IpAddress).HasMaxLength(60);
            e.HasIndex(x => x.CreatedAt);
        });

        builder.Entity<AppSetting>(e =>
        {
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(100);
            e.Property(x => x.Value).HasMaxLength(2000);
            e.Property(x => x.Description).HasMaxLength(500);
        });

        builder.Entity<NumberSequence>(e =>
        {
            e.HasKey(x => x.Name);
            e.Property(x => x.Name).HasMaxLength(50);
        });

        // Default precision for every money column unless overridden above.
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(decimal) && property.GetPrecision() is null)
                    property.SetPrecision(18);
                    property.SetScale(2);
            }
        }
    }

    public override int SaveChanges() => SaveChangesAsync().GetAwaiter().GetResult();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = entry.Entity.CreatedAt == default ? now : entry.Entity.CreatedAt;
                    entry.Entity.UpdatedAt = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    break;
                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.DeletedAt = now;
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }
        return await base.SaveChangesAsync(cancellationToken);
    }

    public async Task<long> NextNumberAsync(string name, CancellationToken ct = default)
    {
        await Database.ExecuteSqlRawAsync(
            "INSERT INTO \"NumberSequences\" (\"Name\", \"Value\") VALUES ({0}, 1) ON CONFLICT (\"Name\") DO UPDATE SET \"Value\" = \"NumberSequences\".\"Value\" + 1",
            new object[] { name }, ct);
        return await NumberSequences.Where(x => x.Name == name).Select(x => x.Value).FirstAsync(ct);
    }
}
