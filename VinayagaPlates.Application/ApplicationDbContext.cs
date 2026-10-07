using Microsoft.EntityFrameworkCore;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Application
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<Permission> Permissions { get; set; }
        public DbSet<RolePermission> RolePermissions { get; set; }

        public DbSet<ProductCategory> ProductCategories { get; set; }
        public DbSet<ProductVariant> ProductVariants { get; set; }
        public DbSet<ProductUnit> ProductUnits { get; set; }
        public DbSet<Product> Products { get; set; }

        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<Partner> Partners { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<CustomerPricing> CustomerPricings { get; set; }

        public DbSet<BusinessAccount> BusinessAccounts { get; set; }
        public DbSet<AccountTransaction> AccountTransactions { get; set; }
        public DbSet<PartnerLedger> PartnerLedgers { get; set; }

        public DbSet<InventoryBatch> InventoryBatches { get; set; }
        public DbSet<InventoryMovement> InventoryMovements { get; set; }

        public DbSet<Purchase> Purchases { get; set; }
        public DbSet<PurchaseDetail> PurchaseDetails { get; set; }

        public DbSet<Sale> Sales { get; set; }
        public DbSet<SaleDetail> SaleDetails { get; set; }

        public DbSet<Location> Locations { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderDetail> OrderDetails { get; set; }
        public DbSet<StockReservation> StockReservations { get; set; }
        public DbSet<InventoryAllocation> InventoryAllocations { get; set; }
        public DbSet<PurchaseExpense> PurchaseExpenses { get; set; }
        public DbSet<PurchaseExpenseAllocation> PurchaseExpenseAllocations { get; set; }
        public DbSet<CustomerLedger> CustomerLedgers { get; set; }
        public DbSet<SupplierLedger> SupplierLedgers { get; set; }
        public DbSet<PartnerTransaction> PartnerTransactions { get; set; }

        public DbSet<AuditLog> AuditLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User Roles Join Table
            modelBuilder.Entity<UserRole>()
                .HasKey(ur => new { ur.UserId, ur.RoleId });

            modelBuilder.Entity<UserRole>()
                .HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId);

            modelBuilder.Entity<UserRole>()
                .HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId);

            // Role Permissions Join Table
            modelBuilder.Entity<RolePermission>()
                .HasKey(rp => new { rp.RoleId, rp.PermissionId });

            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(rp => rp.RoleId);

            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId);

            // Set Primary Keys explicitly for transactional entities
            modelBuilder.Entity<AccountTransaction>().HasKey(t => t.TransactionId);
            modelBuilder.Entity<PartnerLedger>().HasKey(pl => pl.LedgerId);
            modelBuilder.Entity<InventoryMovement>().HasKey(im => im.MovementId);
            modelBuilder.Entity<PurchaseDetail>().HasKey(pd => pd.PurchaseDetailId);
            modelBuilder.Entity<SaleDetail>().HasKey(sd => sd.SaleDetailId);
            modelBuilder.Entity<AuditLog>().HasKey(al => al.AuditId);
            modelBuilder.Entity<CustomerPricing>().HasKey(cp => cp.CustomerPricingId);
            modelBuilder.Entity<BusinessAccount>().HasKey(ba => ba.AccountId);
            modelBuilder.Entity<Partner>().HasKey(p => p.PartnerId);
            modelBuilder.Entity<Customer>().HasKey(c => c.CustomerId);
            modelBuilder.Entity<Supplier>().HasKey(s => s.SupplierId);
            modelBuilder.Entity<Product>().HasKey(p => p.ProductId);
            modelBuilder.Entity<ProductCategory>().HasKey(pc => pc.CategoryId);
            modelBuilder.Entity<ProductVariant>().HasKey(pv => pv.VariantId);
            modelBuilder.Entity<ProductUnit>().HasKey(pu => pu.UnitId);
            modelBuilder.Entity<InventoryBatch>().HasKey(ib => ib.BatchId);
            modelBuilder.Entity<Purchase>().HasKey(p => p.PurchaseId);
            modelBuilder.Entity<Sale>().HasKey(s => s.SaleId);
            modelBuilder.Entity<User>().HasKey(u => u.UserId);
            modelBuilder.Entity<Role>().HasKey(r => r.RoleId);
            modelBuilder.Entity<Role>().ToTable("vp_ms_Role");
            modelBuilder.Entity<Permission>().HasKey(p => p.PermissionId);

            // New Entity Key Mapping
            modelBuilder.Entity<Location>().HasKey(l => l.LocationId);
            modelBuilder.Entity<Order>().HasKey(o => o.OrderId);
            modelBuilder.Entity<OrderDetail>().HasKey(od => od.OrderDetailId);
            modelBuilder.Entity<StockReservation>().HasKey(sr => sr.ReservationId);
            modelBuilder.Entity<InventoryAllocation>().HasKey(ia => ia.AllocationId);
            modelBuilder.Entity<PurchaseExpense>().HasKey(pe => pe.PurchaseExpenseId);
            modelBuilder.Entity<PurchaseExpenseAllocation>().HasKey(pea => pea.AllocationId);
            modelBuilder.Entity<CustomerLedger>().HasKey(cl => cl.LedgerId);
            modelBuilder.Entity<SupplierLedger>().HasKey(sl => sl.LedgerId);
            modelBuilder.Entity<PartnerTransaction>().HasKey(pt => pt.PartnerTransactionId);

            // Precise Numeric Formatting for PostgreSQL
            modelBuilder.Entity<InventoryBatch>().Property(ib => ib.UnitCost).HasPrecision(18, 4);
            modelBuilder.Entity<InventoryBatch>().Property(ib => ib.LandedUnitCost).HasPrecision(18, 4);
            modelBuilder.Entity<InventoryBatch>().Property(ib => ib.TotalLandedCost).HasPrecision(18, 2);
            modelBuilder.Entity<InventoryMovement>().Property(im => im.UnitCost).HasPrecision(18, 4);
            modelBuilder.Entity<InventoryMovement>().Property(im => im.TotalCost).HasPrecision(18, 2);
            modelBuilder.Entity<CustomerPricing>().Property(cp => cp.CustomPrice).HasPrecision(18, 2);
            modelBuilder.Entity<PurchaseExpense>().Property(pe => pe.Amount).HasPrecision(18, 2);
            modelBuilder.Entity<PurchaseExpenseAllocation>().Property(pea => pea.AllocatedAmount).HasPrecision(18, 4);
            modelBuilder.Entity<CustomerLedger>().Property(cl => cl.Amount).HasPrecision(18, 2);
            modelBuilder.Entity<SupplierLedger>().Property(sl => sl.Amount).HasPrecision(18, 2);
            modelBuilder.Entity<PartnerTransaction>().Property(pt => pt.Amount).HasPrecision(18, 2);

            // Indexes for Search & Traceability Optimization
            modelBuilder.Entity<InventoryMovement>().HasIndex(im => im.ProductId);
            modelBuilder.Entity<InventoryMovement>().HasIndex(im => im.BatchId);
            modelBuilder.Entity<InventoryMovement>().HasIndex(im => im.LocationId);
            modelBuilder.Entity<InventoryBatch>().HasIndex(ib => ib.ProductId);
            modelBuilder.Entity<InventoryBatch>().HasIndex(ib => ib.LocationId);
            modelBuilder.Entity<StockReservation>().HasIndex(sr => sr.OrderDetailId);
            modelBuilder.Entity<CustomerPricing>().HasIndex(cp => new { cp.CustomerId, cp.ProductId });

            // Master Data Mapping
            modelBuilder.Entity<Product>()
                .HasOne(p => p.Category)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Product>()
                .HasOne(p => p.Variant)
                .WithMany(v => v.Products)
                .HasForeignKey(p => p.VariantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Product>()
                .HasOne(p => p.Unit)
                .WithMany(u => u.Products)
                .HasForeignKey(p => p.UnitId)
                .OnDelete(DeleteBehavior.Restrict);

            // Customer Pricing Mappings
            modelBuilder.Entity<CustomerPricing>()
                .HasOne(cp => cp.Customer)
                .WithMany(c => c.Pricings)
                .HasForeignKey(cp => cp.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CustomerPricing>()
                .HasOne(cp => cp.Product)
                .WithMany()
                .HasForeignKey(cp => cp.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // Account & Partner Transactions Mappings
            modelBuilder.Entity<AccountTransaction>()
                .HasOne(t => t.Account)
                .WithMany(a => a.Transactions)
                .HasForeignKey(t => t.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PartnerLedger>()
                .HasOne(pl => pl.Partner)
                .WithMany(p => p.Ledgers)
                .HasForeignKey(pl => pl.PartnerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Inventory Mappings
            modelBuilder.Entity<InventoryBatch>()
                .HasOne(b => b.Product)
                .WithMany(p => p.InventoryBatches)
                .HasForeignKey(b => b.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InventoryBatch>()
                .HasOne(b => b.Location)
                .WithMany()
                .HasForeignKey(b => b.LocationId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InventoryMovement>()
                .HasOne(m => m.Batch)
                .WithMany(b => b.Movements)
                .HasForeignKey(m => m.BatchId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InventoryMovement>()
                .HasOne(m => m.Product)
                .WithMany()
                .HasForeignKey(m => m.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InventoryMovement>()
                .HasOne(m => m.Location)
                .WithMany()
                .HasForeignKey(m => m.LocationId)
                .OnDelete(DeleteBehavior.Restrict);

            // Purchase Mappings
            modelBuilder.Entity<Purchase>()
                .HasOne(p => p.Supplier)
                .WithMany(s => s.Purchases)
                .HasForeignKey(p => p.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PurchaseDetail>()
                .HasOne(pd => pd.Purchase)
                .WithMany(p => p.Details)
                .HasForeignKey(pd => pd.PurchaseId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PurchaseDetail>()
                .HasOne(pd => pd.Product)
                .WithMany()
                .HasForeignKey(pd => pd.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            // Sales Mappings
            modelBuilder.Entity<Sale>()
                .HasOne(s => s.Customer)
                .WithMany(c => c.Sales)
                .HasForeignKey(s => s.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SaleDetail>()
                .HasOne(sd => sd.Sale)
                .WithMany(s => s.Details)
                .HasForeignKey(sd => sd.SaleId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SaleDetail>()
                .HasOne(sd => sd.Product)
                .WithMany()
                .HasForeignKey(sd => sd.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SaleDetail>()
                .HasOne(sd => sd.Batch)
                .WithMany()
                .HasForeignKey(sd => sd.BatchId)
                .OnDelete(DeleteBehavior.Restrict);

            // Customer/Supplier Ledgers
            modelBuilder.Entity<CustomerLedger>()
                .HasOne(cl => cl.Customer)
                .WithMany()
                .HasForeignKey(cl => cl.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SupplierLedger>()
                .HasOne(sl => sl.Supplier)
                .WithMany()
                .HasForeignKey(sl => sl.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            // Partner Transaction Mapping
            modelBuilder.Entity<PartnerTransaction>()
                .HasOne(pt => pt.Partner)
                .WithMany()
                .HasForeignKey(pt => pt.PartnerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PartnerTransaction>()
                .HasOne(pt => pt.Account)
                .WithMany()
                .HasForeignKey(pt => pt.AccountId)
                .OnDelete(DeleteBehavior.Restrict);

            // Purchase Expense Allocation
            modelBuilder.Entity<PurchaseExpense>()
                .HasOne(pe => pe.Purchase)
                .WithMany()
                .HasForeignKey(pe => pe.PurchaseId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PurchaseExpenseAllocation>()
                .HasOne(pea => pea.PurchaseExpense)
                .WithMany(pe => pe.Allocations)
                .HasForeignKey(pea => pea.PurchaseExpenseId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PurchaseExpenseAllocation>()
                .HasOne(pea => pea.PurchaseDetail)
                .WithMany()
                .HasForeignKey(pea => pea.PurchaseDetailId)
                .OnDelete(DeleteBehavior.Restrict);

            // Orders and Reservations
            modelBuilder.Entity<Order>()
                .HasOne(o => o.Customer)
                .WithMany()
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<OrderDetail>()
                .HasOne(od => od.Order)
                .WithMany(o => o.Details)
                .HasForeignKey(od => od.OrderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<OrderDetail>()
                .HasOne(od => od.Product)
                .WithMany()
                .HasForeignKey(od => od.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StockReservation>()
                .HasOne(sr => sr.OrderDetail)
                .WithMany(od => od.Reservations)
                .HasForeignKey(sr => sr.OrderDetailId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StockReservation>()
                .HasOne(sr => sr.Product)
                .WithMany()
                .HasForeignKey(sr => sr.ProductId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StockReservation>()
                .HasOne(sr => sr.Location)
                .WithMany()
                .HasForeignKey(sr => sr.LocationId)
                .OnDelete(DeleteBehavior.Restrict);

            // Inventory Allocations
            modelBuilder.Entity<InventoryAllocation>()
                .HasOne(ia => ia.SaleDetail)
                .WithMany()
                .HasForeignKey(ia => ia.SaleDetailId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<InventoryAllocation>()
                .HasOne(ia => ia.Batch)
                .WithMany()
                .HasForeignKey(ia => ia.BatchId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
