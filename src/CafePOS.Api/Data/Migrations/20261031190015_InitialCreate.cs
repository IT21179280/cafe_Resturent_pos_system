using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CafePOS.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, scale: 2, nullable: false),
                    Value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, scale: 2, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, scale: 2, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, scale: 2, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, scale: 2, nullable: false),
                    Role = table.Column<int>(type: "integer", scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", scale: 2, nullable: false),
                    MustChangePassword = table.Column<bool>(type: "boolean", scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    LastLoginAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: true),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, scale: 2, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, scale: 2, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, scale: 2, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, scale: 2, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", scale: 2, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", scale: 2, nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", scale: 2, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", scale: 2, nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", scale: 2, nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", scale: 2, nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", scale: 2, nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", scale: 2, nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", scale: 2, nullable: true),
                    UserName = table.Column<string>(type: "character varying(200)", maxLength: 200, scale: 2, nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, scale: 2, nullable: false),
                    EntityName = table.Column<string>(type: "character varying(100)", maxLength: 100, scale: 2, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(100)", maxLength: 100, scale: 2, nullable: true),
                    Details = table.Column<string>(type: "text", scale: 2, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(60)", maxLength: 60, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, scale: 2, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DiningTables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    Number = table.Column<string>(type: "character varying(30)", maxLength: 30, scale: 2, nullable: false),
                    Capacity = table.Column<int>(type: "integer", scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", scale: 2, nullable: false),
                    Area = table.Column<string>(type: "character varying(100)", maxLength: 100, scale: 2, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", scale: 2, nullable: false),
                    CurrentOrderId = table.Column<Guid>(type: "uuid", scale: 2, nullable: true),
                    xmin = table.Column<uint>(type: "xid", scale: 2, rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiningTables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Discounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, scale: 2, nullable: false),
                    Type = table.Column<int>(type: "integer", scale: 2, nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RequiresManagerApproval = table.Column<bool>(type: "boolean", scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Discounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NumberSequences",
                columns: table => new
                {
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, scale: 2, nullable: false),
                    Value = table.Column<long>(type: "bigint", scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NumberSequences", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "Suppliers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, scale: 2, nullable: false),
                    ContactPerson = table.Column<string>(type: "text", scale: 2, nullable: true),
                    Phone = table.Column<string>(type: "character varying(40)", maxLength: 40, scale: 2, nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, scale: 2, nullable: true),
                    Address = table.Column<string>(type: "text", scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "text", scale: 2, nullable: true),
                    Balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Suppliers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", scale: 2, nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    ClaimType = table.Column<string>(type: "text", scale: 2, nullable: true),
                    ClaimValue = table.Column<string>(type: "text", scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", scale: 2, nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    ClaimType = table.Column<string>(type: "text", scale: 2, nullable: true),
                    ClaimValue = table.Column<string>(type: "text", scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", scale: 2, nullable: false),
                    ProviderKey = table.Column<string>(type: "text", scale: 2, nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", scale: 2, nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    LoginProvider = table.Column<string>(type: "text", scale: 2, nullable: false),
                    Name = table.Column<string>(type: "text", scale: 2, nullable: false),
                    Value = table.Column<string>(type: "text", scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: true),
                    ReplacedByTokenHash = table.Column<string>(type: "text", scale: 2, nullable: true),
                    CreatedByIp = table.Column<string>(type: "text", scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Shifts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    OpenedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    ClosedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: true),
                    OpeningCash = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ExpectedCash = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CountedCash = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Difference = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "text", scale: 2, nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uuid", scale: 2, nullable: true),
                    xmin = table.Column<uint>(type: "xid", scale: 2, rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shifts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Shifts_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, scale: 2, nullable: false),
                    Sku = table.Column<string>(type: "character varying(60)", maxLength: 60, scale: 2, nullable: false),
                    Barcode = table.Column<string>(type: "character varying(60)", maxLength: 60, scale: 2, nullable: true),
                    CategoryId = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, scale: 2, nullable: true),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CostPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, scale: 2, nullable: true),
                    IsAvailable = table.Column<bool>(type: "boolean", scale: 2, nullable: false),
                    PreparationStation = table.Column<int>(type: "integer", scale: 2, nullable: false),
                    TaxRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TrackInventory = table.Column<bool>(type: "boolean", scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Products_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Ingredients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, scale: 2, nullable: false),
                    Sku = table.Column<string>(type: "character varying(60)", maxLength: 60, scale: 2, nullable: true),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, scale: 2, nullable: false),
                    CurrentStock = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LowStockThreshold = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CostPerUnit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DefaultSupplierId = table.Column<Guid>(type: "uuid", scale: 2, nullable: true),
                    xmin = table.Column<uint>(type: "xid", scale: 2, rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ingredients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ingredients_Suppliers_DefaultSupplierId",
                        column: x => x.DefaultSupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    Number = table.Column<string>(type: "character varying(40)", maxLength: 40, scale: 2, nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", scale: 2, nullable: false),
                    OrderedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "text", scale: 2, nullable: true),
                    Total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    xmin = table.Column<uint>(type: "xid", scale: 2, rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    OrderNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, scale: 2, nullable: false),
                    ClientOrderId = table.Column<string>(type: "character varying(80)", maxLength: 80, scale: 2, nullable: true),
                    Type = table.Column<int>(type: "integer", scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", scale: 2, nullable: false),
                    TableId = table.Column<Guid>(type: "uuid", scale: 2, nullable: true),
                    CashierId = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    ShiftId = table.Column<Guid>(type: "uuid", scale: 2, nullable: true),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, scale: 2, nullable: true),
                    CustomerPhone = table.Column<string>(type: "character varying(40)", maxLength: 40, scale: 2, nullable: true),
                    DeliveryAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, scale: 2, nullable: true),
                    Subtotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountType = table.Column<int>(type: "integer", scale: 2, nullable: false),
                    DiscountValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountId = table.Column<Guid>(type: "uuid", scale: 2, nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ServiceCharge = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaidAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ChangeDue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PlacedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    PreparingAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: true),
                    ReadyAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: true),
                    ServedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: true),
                    CancelReason = table.Column<string>(type: "character varying(500)", maxLength: 500, scale: 2, nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "uuid", scale: 2, nullable: true),
                    xmin = table.Column<uint>(type: "xid", scale: 2, rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Orders_AspNetUsers_CashierId",
                        column: x => x.CashierId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Orders_DiningTables_TableId",
                        column: x => x.TableId,
                        principalTable: "DiningTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Orders_Discounts_DiscountId",
                        column: x => x.DiscountId,
                        principalTable: "Discounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Orders_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ShiftExpenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    ShiftId = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, scale: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, scale: 2, nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftExpenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShiftExpenses_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductModifiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, scale: 2, nullable: false),
                    PriceDelta = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductModifiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductModifiers_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InventoryMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    IngredientId = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    Type = table.Column<int>(type: "integer", scale: 2, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, scale: 2, nullable: false),
                    Reference = table.Column<string>(type: "character varying(100)", maxLength: 100, scale: 2, nullable: true),
                    OrderId = table.Column<Guid>(type: "uuid", scale: 2, nullable: true),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", scale: 2, nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", scale: 2, nullable: true),
                    BalanceAfter = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryMovements_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RecipeItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    IngredientId = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecipeItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecipeItems_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecipeItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    IngredientId = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReceivedQuantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderItems_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderItems_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", scale: 2, nullable: true),
                    ProductName = table.Column<string>(type: "character varying(200)", maxLength: 200, scale: 2, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxRate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, scale: 2, nullable: true),
                    PreparationStation = table.Column<int>(type: "integer", scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OrderItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    Method = table.Column<int>(type: "integer", scale: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ChangeGiven = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Reference = table.Column<string>(type: "character varying(150)", maxLength: 150, scale: 2, nullable: true),
                    Status = table.Column<int>(type: "integer", scale: 2, nullable: false),
                    RefundedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CashierId = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    ShiftId = table.Column<Guid>(type: "uuid", scale: 2, nullable: true),
                    PaidAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    xmin = table.Column<uint>(type: "xid", scale: 2, rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrderItemModifiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    OrderItemId = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, scale: 2, nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItemModifiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderItemModifiers_OrderItems_OrderItemId",
                        column: x => x.OrderItemId,
                        principalTable: "OrderItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Refunds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, scale: 2, nullable: false),
                    ApprovedByUserId = table.Column<Guid>(type: "uuid", scale: 2, nullable: false),
                    ShiftId = table.Column<Guid>(type: "uuid", scale: 2, nullable: true),
                    RefundedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", scale: 2, nullable: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Refunds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Refunds_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Refunds_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_Email",
                table: "AspNetUsers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreatedAt",
                table: "AuditLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Name",
                table: "Categories",
                column: "Name",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_DiningTables_Number",
                table: "DiningTables",
                column: "Number",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_DefaultSupplierId",
                table: "Ingredients",
                column: "DefaultSupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_Name",
                table: "Ingredients",
                column: "Name",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_IngredientId_OccurredAt",
                table: "InventoryMovements",
                columns: new[] { "IngredientId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_OrderId",
                table: "InventoryMovements",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItemModifiers_OrderItemId",
                table: "OrderItemModifiers",
                column: "OrderItemId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_OrderId",
                table: "OrderItems",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_ProductId",
                table: "OrderItems",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CashierId_PlacedAt",
                table: "Orders",
                columns: new[] { "CashierId", "PlacedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ClientOrderId",
                table: "Orders",
                column: "ClientOrderId",
                unique: true,
                filter: "\"ClientOrderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_DiscountId",
                table: "Orders",
                column: "DiscountId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderNumber",
                table: "Orders",
                column: "OrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PlacedAt",
                table: "Orders",
                column: "PlacedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_ShiftId",
                table: "Orders",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status_PlacedAt",
                table: "Orders",
                columns: new[] { "Status", "PlacedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TableId",
                table: "Orders",
                column: "TableId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_OrderId",
                table: "Payments",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductModifiers_ProductId",
                table: "ProductModifiers",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Barcode",
                table: "Products",
                column: "Barcode",
                unique: true,
                filter: "\"Barcode\" IS NOT NULL AND \"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_Products_CategoryId",
                table: "Products",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Sku",
                table: "Products",
                column: "Sku",
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderItems_IngredientId",
                table: "PurchaseOrderItems",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderItems_PurchaseOrderId",
                table: "PurchaseOrderItems",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_Number",
                table: "PurchaseOrders",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_SupplierId",
                table: "PurchaseOrders",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeItems_IngredientId",
                table: "RecipeItems",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "IX_RecipeItems_ProductId_IngredientId",
                table: "RecipeItems",
                columns: new[] { "ProductId", "IngredientId" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId_ExpiresAt",
                table: "RefreshTokens",
                columns: new[] { "UserId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_OrderId",
                table: "Refunds",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_PaymentId",
                table: "Refunds",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_ShiftExpenses_ShiftId",
                table: "ShiftExpenses",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_UserId_Status",
                table: "Shifts",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_Name",
                table: "Suppliers",
                column: "Name",
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "InventoryMovements");

            migrationBuilder.DropTable(
                name: "NumberSequences");

            migrationBuilder.DropTable(
                name: "OrderItemModifiers");

            migrationBuilder.DropTable(
                name: "ProductModifiers");

            migrationBuilder.DropTable(
                name: "PurchaseOrderItems");

            migrationBuilder.DropTable(
                name: "RecipeItems");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "Refunds");

            migrationBuilder.DropTable(
                name: "ShiftExpenses");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "PurchaseOrders");

            migrationBuilder.DropTable(
                name: "Ingredients");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "Products");

            migrationBuilder.DropTable(
                name: "Suppliers");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "Categories");

            migrationBuilder.DropTable(
                name: "DiningTables");

            migrationBuilder.DropTable(
                name: "Discounts");

            migrationBuilder.DropTable(
                name: "Shifts");

            migrationBuilder.DropTable(
                name: "AspNetUsers");
        }
    }
}
