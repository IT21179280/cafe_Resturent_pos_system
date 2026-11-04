using CafePOS.Core.Entities;
using CafePOS.Core.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CafePOS.Api.Data;

public static class DbSeeder
{
    public const string AdminEmail = "admin@cafepos.local";
    public const string AdminPassword = "Admin@12345";

    public static async Task SeedAsync(IServiceProvider services, ILogger logger)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();

        foreach (var role in Enum.GetNames<UserRole>())
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new AppRole(role));

        if (!await db.Users.AnyAsync())
        {
            await CreateUserAsync(userManager, "Administrator", "System Administrator", AdminEmail, AdminPassword,
                UserRole.Administrator, mustChangePassword: true);
            await CreateUserAsync(userManager, "manager", "Dilani Perera", "manager@cafepos.local", "Manager@12345",
                UserRole.Manager, mustChangePassword: false);
            await CreateUserAsync(userManager, "cashier", "Nuwan Silva", "cashier@cafepos.local", "Cashier@12345",
                UserRole.Cashier, mustChangePassword: false);
            await CreateUserAsync(userManager, "kitchen", "Kamal Fernando", "kitchen@cafepos.local", "Kitchen@12345",
                UserRole.Kitchen, mustChangePassword: false);
            logger.LogInformation("Seeded default users (admin@cafepos.local / Admin@12345)");
        }

        if (!await db.Categories.AnyAsync())
        {
            var categories = new[]
            {
                new Category { Name = "Starters", Description = "Soups, rolls and light bites", DisplayOrder = 1 },
                new Category { Name = "Rice & Curry", Description = "Traditional Sri Lankan rice plates", DisplayOrder = 2 },
                new Category { Name = "Kottu & Noodles", Description = "Kottu, noodles and fried rice", DisplayOrder = 3 },
                new Category { Name = "Grills", Description = "Char-grilled meats and seafood", DisplayOrder = 4 },
                new Category { Name = "Burgers & Sandwiches", Description = "Burgers, subs and sandwiches", DisplayOrder = 5 },
                new Category { Name = "Desserts", Description = "Sweet treats", DisplayOrder = 6 },
                new Category { Name = "Beverages", Description = "Hot and cold drinks", DisplayOrder = 7 }
            };
            db.Categories.AddRange(categories);
            await db.SaveChangesAsync();

            Product P(string name, string sku, string barcode, string cat, decimal price, decimal cost,
                PreparationStation station, decimal tax, bool track = false) => new()
            {
                Name = name, Sku = sku, Barcode = barcode,
                CategoryId = categories.First(c => c.Name == cat).Id,
                Price = price, CostPrice = cost, PreparationStation = station, TaxRate = tax, TrackInventory = track,
                IsAvailable = true
            };

            var products = new List<Product>
            {
                P("Vegetable Soup", "ST-001", "2000000000011", "Starters", 450, 140, PreparationStation.Kitchen, 0),
                P("Chicken Spring Rolls", "ST-002", "2000000000028", "Starters", 650, 210, PreparationStation.Kitchen, 0),
                P("Devilled Prawns", "ST-003", "2000000000035", "Starters", 1450, 620, PreparationStation.Kitchen, 0, true),
                P("Chicken Rice & Curry", "RC-001", "2000000000042", "Rice & Curry", 890, 310, PreparationStation.Kitchen, 0, true),
                P("Fish Rice & Curry", "RC-002", "2000000000059", "Rice & Curry", 850, 300, PreparationStation.Kitchen, 0, true),
                P("Vegetable Rice & Curry", "RC-003", "2000000000066", "Rice & Curry", 650, 210, PreparationStation.Kitchen, 0, true),
                P("Chicken Kottu", "KT-001", "2000000000073", "Kottu & Noodles", 1050, 380, PreparationStation.Grill, 0, true),
                P("Cheese Chicken Kottu", "KT-002", "2000000000080", "Kottu & Noodles", 1350, 520, PreparationStation.Grill, 0, true),
                P("Beef Kottu", "KT-003", "2000000000097", "Kottu & Noodles", 1200, 470, PreparationStation.Grill, 0, true),
                P("Chicken Fried Rice", "KT-004", "2000000000103", "Kottu & Noodles", 950, 330, PreparationStation.Kitchen, 0, true),
                P("Chicken Grilled", "GR-001", "2000000000110", "Grills", 1500, 640, PreparationStation.Grill, 0, true),
                P("Beef Steak", "GR-002", "2000000000127", "Grills", 2200, 1050, PreparationStation.Grill, 0, true),
                P("Grilled Salmon", "GR-003", "2000000000134", "Grills", 2650, 1400, PreparationStation.Grill, 0, true),
                P("Chicken Burger", "BG-001", "2000000000141", "Burgers & Sandwiches", 950, 340, PreparationStation.Kitchen, 0, true),
                P("Beef Burger", "BG-002", "2000000000158", "Burgers & Sandwiches", 1150, 450, PreparationStation.Kitchen, 0, true),
                P("Veg Sandwich", "BG-003", "2000000000165", "Burgers & Sandwiches", 550, 170, PreparationStation.Kitchen, 0),
                P("Chocolate Cake Slice", "DS-001", "2000000000172", "Desserts", 450, 160, PreparationStation.Bakery, 0),
                P("Ice Cream (2 Scoops)", "DS-002", "2000000000189", "Desserts", 380, 140, PreparationStation.Dessert, 0),
                P("Watalappan", "DS-003", "2000000000196", "Desserts", 350, 120, PreparationStation.Bakery, 0),
                P("Plain Tea", "BV-001", "2000000000202", "Beverages", 150, 30, PreparationStation.Bar, 0, true),
                P("Milk Tea", "BV-002", "2000000000219", "Beverages", 220, 55, PreparationStation.Bar, 0, true),
                P("Coffee", "BV-003", "2000000000226", "Beverages", 350, 110, PreparationStation.Bar, 0, true),
                P("Iced Coffee", "BV-004", "2000000000233", "Beverages", 450, 140, PreparationStation.Bar, 0, true),
                P("Fresh Lime Juice", "BV-005", "2000000000240", "Beverages", 380, 90, PreparationStation.Bar, 0, true),
                P("Coca-Cola (Can)", "BV-006", "2000000000257", "Beverages", 250, 140, PreparationStation.Bar, 0, true),
                P("Bottled Water", "BV-007", "2000000000264", "Beverages", 120, 60, PreparationStation.Bar, 0, true)
            };
            db.Products.AddRange(products);
            await db.SaveChangesAsync();

            AddModifiers(db, products, "KT-001", new[] { ("Extra Cheese", 150m), ("Extra Egg", 80m), ("Spicy", 0m) });
            AddModifiers(db, products, "BG-001", new[] { ("Extra Cheese", 120m), ("Add Bacon", 300m) });
            AddModifiers(db, products, "BV-003", new[] { ("Extra Shot", 80m), ("Oat Milk", 100m) });

            var suppliers = new[]
            {
                new Supplier { Name = "Lanka Fresh Produce", ContactPerson = "Samantha", Phone = "0112345678", Email = "orders@lankafresh.lk", Address = "12 Market Road, Colombo 11" },
                new Supplier { Name = "Ceylon Meats Ltd", ContactPerson = "Ravi", Phone = "0113456789", Email = "sales@ceylonmeats.lk", Address = "45 Industrial Zone, Kelaniya" },
                new Supplier { Name = "Global Beverages", ContactPerson = "Fathima", Phone = "0114567890", Email = "supply@globalbev.lk", Address = "88 Galle Road, Colombo 03" }
            };
            db.Suppliers.AddRange(suppliers);
            await db.SaveChangesAsync();

            var ingredients = new[]
            {
                new Ingredient { Name = "Basmati Rice", Sku = "ING-001", Unit = "g", CurrentStock = 25000, LowStockThreshold = 5000, CostPerUnit = 0.28m, DefaultSupplierId = suppliers[0].Id },
                new Ingredient { Name = "Chicken", Sku = "ING-002", Unit = "g", CurrentStock = 18000, LowStockThreshold = 4000, CostPerUnit = 0.95m, DefaultSupplierId = suppliers[1].Id },
                new Ingredient { Name = "Beef", Sku = "ING-003", Unit = "g", CurrentStock = 9000, LowStockThreshold = 3000, CostPerUnit = 1.65m, DefaultSupplierId = suppliers[1].Id },
                new Ingredient { Name = "Prawns", Sku = "ING-004", Unit = "g", CurrentStock = 3500, LowStockThreshold = 1000, CostPerUnit = 2.40m, DefaultSupplierId = suppliers[1].Id },
                new Ingredient { Name = "Salmon Fillet", Sku = "ING-005", Unit = "g", CurrentStock = 2500, LowStockThreshold = 800, CostPerUnit = 3.80m, DefaultSupplierId = suppliers[1].Id },
                new Ingredient { Name = "Eggs", Sku = "ING-006", Unit = "pcs", CurrentStock = 240, LowStockThreshold = 60, CostPerUnit = 35m, DefaultSupplierId = suppliers[0].Id },
                new Ingredient { Name = "Wheat Flour", Sku = "ING-007", Unit = "g", CurrentStock = 15000, LowStockThreshold = 4000, CostPerUnit = 0.14m, DefaultSupplierId = suppliers[0].Id },
                new Ingredient { Name = "Coconut Milk", Sku = "ING-008", Unit = "ml", CurrentStock = 12000, LowStockThreshold = 3000, CostPerUnit = 0.32m, DefaultSupplierId = suppliers[0].Id },
                new Ingredient { Name = "Onions", Sku = "ING-009", Unit = "g", CurrentStock = 10000, LowStockThreshold = 2500, CostPerUnit = 0.22m, DefaultSupplierId = suppliers[0].Id },
                new Ingredient { Name = "Tomatoes", Sku = "ING-010", Unit = "g", CurrentStock = 8000, LowStockThreshold = 2000, CostPerUnit = 0.26m, DefaultSupplierId = suppliers[0].Id },
                new Ingredient { Name = "Potatoes", Sku = "ING-011", Unit = "g", CurrentStock = 12000, LowStockThreshold = 3000, CostPerUnit = 0.18m, DefaultSupplierId = suppliers[0].Id },
                new Ingredient { Name = "Cheese", Sku = "ING-012", Unit = "g", CurrentStock = 4000, LowStockThreshold = 1000, CostPerUnit = 1.20m, DefaultSupplierId = suppliers[0].Id },
                new Ingredient { Name = "Curry Powder", Sku = "ING-013", Unit = "g", CurrentStock = 5000, LowStockThreshold = 1000, CostPerUnit = 0.40m, DefaultSupplierId = suppliers[0].Id },
                new Ingredient { Name = "Coffee Beans", Sku = "ING-014", Unit = "g", CurrentStock = 6000, LowStockThreshold = 1500, CostPerUnit = 2.10m, DefaultSupplierId = suppliers[2].Id },
                new Ingredient { Name = "Tea Leaves", Sku = "ING-015", Unit = "g", CurrentStock = 4500, LowStockThreshold = 1000, CostPerUnit = 1.10m, DefaultSupplierId = suppliers[2].Id },
                new Ingredient { Name = "Milk", Sku = "ING-016", Unit = "ml", CurrentStock = 15000, LowStockThreshold = 4000, CostPerUnit = 0.26m, DefaultSupplierId = suppliers[2].Id },
                new Ingredient { Name = "Sugar", Sku = "ING-017", Unit = "g", CurrentStock = 20000, LowStockThreshold = 4000, CostPerUnit = 0.18m, DefaultSupplierId = suppliers[0].Id },
                new Ingredient { Name = "Chocolate", Sku = "ING-018", Unit = "g", CurrentStock = 3000, LowStockThreshold = 800, CostPerUnit = 1.80m, DefaultSupplierId = suppliers[0].Id },
                new Ingredient { Name = "Butter", Sku = "ING-019", Unit = "g", CurrentStock = 3500, LowStockThreshold = 900, CostPerUnit = 1.35m, DefaultSupplierId = suppliers[0].Id },
                new Ingredient { Name = "Cooking Oil", Sku = "ING-020", Unit = "ml", CurrentStock = 18000, LowStockThreshold = 4000, CostPerUnit = 0.30m, DefaultSupplierId = suppliers[0].Id },
                new Ingredient { Name = "Bread Buns", Sku = "ING-021", Unit = "pcs", CurrentStock = 120, LowStockThreshold = 30, CostPerUnit = 45m, DefaultSupplierId = suppliers[0].Id },
                new Ingredient { Name = "Soft Drinks (Can)", Sku = "ING-022", Unit = "pcs", CurrentStock = 96, LowStockThreshold = 24, CostPerUnit = 110m, DefaultSupplierId = suppliers[2].Id },
                new Ingredient { Name = "Water Bottles", Sku = "ING-023", Unit = "pcs", CurrentStock = 72, LowStockThreshold = 24, CostPerUnit = 40m, DefaultSupplierId = suppliers[2].Id },
                new Ingredient { Name = "Lime", Sku = "ING-024", Unit = "pcs", CurrentStock = 80, LowStockThreshold = 20, CostPerUnit = 25m, DefaultSupplierId = suppliers[0].Id }
            };
            db.Ingredients.AddRange(ingredients);
            await db.SaveChangesAsync();

            RecipeItem? R(string sku, string ingredient, decimal qty) => products.FirstOrDefault(p => p.Sku == sku) is { } prod
                ? new RecipeItem { ProductId = prod.Id, IngredientId = ingredients.First(i => i.Name == ingredient).Id, Quantity = qty }
                : null;

            var recipes = new List<RecipeItem?>
            {
                R("RC-001", "Basmati Rice", 200), R("RC-001", "Chicken", 180), R("RC-001", "Coconut Milk", 80),
                R("RC-001", "Curry Powder", 12), R("RC-001", "Onions", 60),
                R("RC-002", "Basmati Rice", 200), R("RC-002", "Coconut Milk", 80), R("RC-002", "Curry Powder", 12),
                R("RC-003", "Basmati Rice", 200), R("RC-003", "Coconut Milk", 80), R("RC-003", "Curry Powder", 12),
                R("KT-001", "Wheat Flour", 120), R("KT-001", "Chicken", 150), R("KT-001", "Eggs", 1), R("KT-001", "Onions", 50), R("KT-001", "Cooking Oil", 40),
                R("KT-002", "Wheat Flour", 120), R("KT-002", "Chicken", 150), R("KT-002", "Cheese", 60), R("KT-002", "Eggs", 1), R("KT-002", "Cooking Oil", 40),
                R("KT-003", "Wheat Flour", 120), R("KT-003", "Beef", 160), R("KT-003", "Eggs", 1), R("KT-003", "Cooking Oil", 40),
                R("KT-004", "Basmati Rice", 250), R("KT-004", "Chicken", 120), R("KT-004", "Eggs", 1), R("KT-004", "Cooking Oil", 35),
                R("GR-001", "Chicken", 250), R("GR-001", "Butter", 20),
                R("GR-002", "Beef", 300), R("GR-002", "Butter", 25),
                R("GR-003", "Salmon Fillet", 220), R("GR-003", "Butter", 20),
                R("BG-001", "Bread Buns", 1), R("BG-001", "Chicken", 120), R("BG-001", "Cheese", 30),
                R("BG-002", "Bread Buns", 1), R("BG-002", "Beef", 140), R("BG-002", "Cheese", 30),
                R("DS-001", "Wheat Flour", 80), R("DS-001", "Chocolate", 60), R("DS-001", "Butter", 30), R("DS-001", "Sugar", 50),
                R("DS-003", "Coconut Milk", 100), R("DS-003", "Sugar", 40), R("DS-003", "Eggs", 1),
                R("BV-001", "Tea Leaves", 5), R("BV-001", "Sugar", 10),
                R("BV-002", "Tea Leaves", 5), R("BV-002", "Milk", 80), R("BV-002", "Sugar", 10),
                R("BV-003", "Coffee Beans", 18), R("BV-003", "Milk", 60),
                R("BV-004", "Coffee Beans", 18), R("BV-004", "Milk", 100), R("BV-004", "Sugar", 12),
                R("BV-005", "Lime", 2), R("BV-005", "Sugar", 15),
                R("BV-006", "Soft Drinks (Can)", 1),
                R("BV-007", "Water Bottles", 1)
            };
            db.RecipeItems.AddRange(recipes.OfType<RecipeItem>());
            await db.SaveChangesAsync();

            db.DiningTables.AddRange(
                new DiningTable { Number = "T01", Capacity = 2, Area = "Main Hall", DisplayOrder = 1 },
                new DiningTable { Number = "T02", Capacity = 2, Area = "Main Hall", DisplayOrder = 2 },
                new DiningTable { Number = "T03", Capacity = 4, Area = "Main Hall", DisplayOrder = 3 },
                new DiningTable { Number = "T04", Capacity = 4, Area = "Main Hall", DisplayOrder = 4 },
                new DiningTable { Number = "T05", Capacity = 4, Area = "Main Hall", DisplayOrder = 5 },
                new DiningTable { Number = "T06", Capacity = 6, Area = "Main Hall", DisplayOrder = 6 },
                new DiningTable { Number = "T07", Capacity = 4, Area = "Garden", DisplayOrder = 7 },
                new DiningTable { Number = "T08", Capacity = 4, Area = "Garden", DisplayOrder = 8 },
                new DiningTable { Number = "T09", Capacity = 6, Area = "Garden", DisplayOrder = 9 },
                new DiningTable { Number = "T10", Capacity = 8, Area = "Private Room", DisplayOrder = 10 },
                new DiningTable { Number = "T11", Capacity = 2, Area = "Bar", DisplayOrder = 11 },
                new DiningTable { Number = "T12", Capacity = 2, Area = "Bar", DisplayOrder = 12 });
            await db.SaveChangesAsync();

            db.Discounts.AddRange(
                new Discount { Name = "Staff 10%", Type = DiscountType.Percentage, Value = 10, RequiresManagerApproval = true },
                new Discount { Name = "Loyalty 5%", Type = DiscountType.Percentage, Value = 5 },
                new Discount { Name = "LKR 100 Off", Type = DiscountType.FixedAmount, Value = 100 });
            await db.SaveChangesAsync();

            db.AppSettings.AddRange(
                new AppSetting { Key = SettingKeys.RestaurantName, Value = "CafePOS Restaurant", Description = "Restaurant name printed on receipts" },
                new AppSetting { Key = SettingKeys.RestaurantAddress, Value = "No. 25, Galle Road, Colombo 03", Description = "Restaurant address" },
                new AppSetting { Key = SettingKeys.RestaurantPhone, Value = "011 234 5678", Description = "Restaurant phone" },
                new AppSetting { Key = SettingKeys.RestaurantEmail, Value = "hello@cafepos.local", Description = "Restaurant email" },
                new AppSetting { Key = SettingKeys.Currency, Value = "LKR", Description = "Currency code" },
                new AppSetting { Key = SettingKeys.ServiceChargeRate, Value = "0", Description = "Service charge percentage" },
                new AppSetting { Key = SettingKeys.DefaultTaxRate, Value = "0", Description = "Default tax rate percentage" },
                new AppSetting { Key = SettingKeys.ReceiptFooter, Value = "Thank you! Please come again.", Description = "Receipt footer text" },
                new AppSetting { Key = SettingKeys.InventoryDeductionEvent, Value = "Completed", Description = "Completed, Served or Paid" },
                new AppSetting { Key = SettingKeys.LowStockAlerts, Value = "true", Description = "Show low stock alerts" },
                new AppSetting { Key = SettingKeys.SystemName, Value = "CafePOS", Description = "POS and app display name" },
                new AppSetting { Key = SettingKeys.LogoUrl, Value = string.Empty, Description = "POS logo image URL" },
                new AppSetting { Key = SettingKeys.FirebaseDatabaseUrl, Value = string.Empty, Description = "Firebase Realtime Database URL" },
                new AppSetting { Key = SettingKeys.FirebaseSyncEnabled, Value = "false", Description = "Enable Firebase mirroring" });
            db.NumberSequences.AddRange(
                new NumberSequence { Name = "order", Value = 0 },
                new NumberSequence { Name = "purchase", Value = 0 });
            await db.SaveChangesAsync();
            logger.LogInformation("Seeded demo menu, tables, inventory, recipes, suppliers and settings");
        }

        // These settings were added after the first release, so make sure existing
        // databases receive them even when the demo catalog is already present.
        if (!await db.AppSettings.AnyAsync(s => s.Key == SettingKeys.SystemName))
        {
            db.AppSettings.Add(new AppSetting
            {
                Key = SettingKeys.SystemName,
                Value = "CafePOS",
                Description = "POS and app display name"
            });
        }
        if (!await db.AppSettings.AnyAsync(s => s.Key == SettingKeys.LogoUrl))
        {
            db.AppSettings.Add(new AppSetting
            {
                Key = SettingKeys.LogoUrl,
                Value = string.Empty,
                Description = "POS logo image URL"
            });
        }
        if (!await db.AppSettings.AnyAsync(s => s.Key == SettingKeys.FirebaseDatabaseUrl))
        {
            db.AppSettings.Add(new AppSetting
            {
                Key = SettingKeys.FirebaseDatabaseUrl,
                Value = string.Empty,
                Description = "Firebase Realtime Database URL"
            });
        }
        if (!await db.AppSettings.AnyAsync(s => s.Key == SettingKeys.FirebaseSyncEnabled))
        {
            db.AppSettings.Add(new AppSetting
            {
                Key = SettingKeys.FirebaseSyncEnabled,
                Value = "false",
                Description = "Enable Firebase mirroring"
            });
        }
        await db.SaveChangesAsync();
    }

    private static void AddModifiers(AppDbContext db, List<Product> products, string sku, (string Name, decimal Price)[] modifiers)
    {
        var product = products.FirstOrDefault(p => p.Sku == sku);
        if (product is null) return;
        foreach (var m in modifiers)
            db.ProductModifiers.Add(new ProductModifier { ProductId = product.Id, Name = m.Name, PriceDelta = m.Price });
    }

    private static async Task CreateUserAsync(UserManager<AppUser> userManager, string userName, string fullName,
        string email, string password, UserRole role, bool mustChangePassword)
    {
        var user = new AppUser
        {
            UserName = userName,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName,
            Role = role,
            IsActive = true,
            MustChangePassword = mustChangePassword
        };
        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException($"Failed to seed user {email}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        await userManager.AddToRoleAsync(user, role.ToString());
    }
}
