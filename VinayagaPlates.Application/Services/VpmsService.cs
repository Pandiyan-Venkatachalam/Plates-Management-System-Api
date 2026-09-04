using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VinayagaPlates.Application.Interfaces;
using VinayagaPlates.Application.Repositories;
using VinayagaPlates.Contracts.DTOs;
using VinayagaPlates.Domain.Entities;
using VinayagaPlates.Application.Constants;

namespace VinayagaPlates.Application.Services
{
    public class VpmsService
    {
        private readonly IUserRepository _userRepo;
        private readonly IProductRepository _productRepo;
        private readonly IPurchaseRepository _purchaseRepo;
        private readonly ISalesRepository _salesRepo;
        private readonly IPartnerRepository _partnerRepo;
        private readonly IAccountRepository _accountRepo;
        private readonly IBatchRepository _batchRepo;
        private readonly IOrderRepository _orderRepo;
        private readonly IPasswordHasher _hasher;
        private readonly ApplicationDbContext _db;

        public VpmsService(
            IUserRepository userRepo,
            IProductRepository productRepo,
            IPurchaseRepository purchaseRepo,
            ISalesRepository salesRepo,
            IPartnerRepository partnerRepo,
            IAccountRepository accountRepo,
            IBatchRepository batchRepo,
            IOrderRepository orderRepo,
            IPasswordHasher hasher,
            ApplicationDbContext db)
        {
            _userRepo = userRepo;
            _productRepo = productRepo;
            _purchaseRepo = purchaseRepo;
            _salesRepo = salesRepo;
            _partnerRepo = partnerRepo;
            _accountRepo = accountRepo;
            _batchRepo = batchRepo;
            _orderRepo = orderRepo;
            _hasher = hasher;
            _db = db;
        }

        // --- AUTH & SETUP SEED ---

        public async Task SeedAsync()
        {
            // Seed roles 
            if (!await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(_db.Roles))
            {
                var roles = new[]
                {
                    new Role { RoleName = RoleConstants.Admin, Description = "Administrator Role" },
                    new Role { RoleName = RoleConstants.Partner, Description = "Partner Role" },
                    new Role { RoleName = RoleConstants.User, Description = "Standard User Role" }
                };
                await _db.Roles.AddRangeAsync(roles);
                await _db.SaveChangesAsync();
            }

            // Seed Pandiyan admin user
            if (!await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(_db.Users, u => u.Username.ToLower() == "pandiyan"))
            {
                var adminUser = new User
                {
                    UserCode = "U" + DateTime.UtcNow.Ticks.ToString().Substring(10),
                    FullName = "Pandiyan",
                    Username = "Pandiyan",
                    Email = "Pandiyan@gmail.com",
                    Phone = "6369660378",
                    PasswordHash = _hasher.HashPassword("123"),
                    IsActive = true,
                    CreatedBy = "SYSTEM",
                    CreatedAt = DateTime.UtcNow
                };

                await _db.Users.AddAsync(adminUser);
                await _db.SaveChangesAsync();

                var adminRole = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(_db.Roles, r => r.RoleName == RoleConstants.Admin);
                if (adminRole != null)
                {
                    await _db.UserRoles.AddAsync(new UserRole { UserId = adminUser.UserId, RoleId = adminRole.RoleId });
                    await _db.SaveChangesAsync();
                }
            }

            // Seed default business accounts only if table is completely empty
            if (!await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(_db.BusinessAccounts))
            {
                await _db.BusinessAccounts.AddAsync(new BusinessAccount { AccountName = "Ranjith", AccountType = "Ranjith's Acc", CreatedBy = "SYSTEM", CreatedAt = DateTime.UtcNow });
                await _db.BusinessAccounts.AddAsync(new BusinessAccount { AccountName = "Pandiyan", AccountType = "Pandiyan's Acc", CreatedBy = "SYSTEM", CreatedAt = DateTime.UtcNow });
                await _db.SaveChangesAsync();
            }

            // Cleanup any auto-created Cash/Bank duplicate accounts
            var existingAccounts = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(_db.BusinessAccounts);
            var unwantedAccounts = existingAccounts.Where(a => (a.AccountName == "Cash" || a.AccountName == "Bank") && a.AccountId > 2).ToList();
            if (unwantedAccounts.Any())
            {
                _db.BusinessAccounts.RemoveRange(unwantedAccounts);
                await _db.SaveChangesAsync();
            }

            var allAccounts = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(_db.BusinessAccounts);
            Console.WriteLine($"--- SEEDED ACCOUNTS COUNT: {allAccounts.Count} ---");
            foreach (var acc in allAccounts)
            {
                Console.WriteLine($"Account: ID={acc.AccountId}, Name='{acc.AccountName}', Type='{acc.AccountType}'");
            }

            // Seed default Location if none exist
            if (!await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(_db.Locations))
            {
                var location = new Location
                {
                    LocationName = "Godown",
                    IsActive = true,
                    CreatedBy = "SYSTEM",
                    CreatedAt = DateTime.UtcNow
                };
                await _db.Locations.AddAsync(location);
                await _db.SaveChangesAsync();
            }

            // Seed default Partner if none exist
            if (!await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(_db.Partners))
            {
                var partner = new Partner
                {
                    PartnerName = "Pandiyan",
                    ContactPhone = "6369660378",
                    CreatedBy = "SYSTEM",
                    CreatedAt = DateTime.UtcNow
                };
                await _db.Partners.AddAsync(partner);
                await _db.SaveChangesAsync();
            }

            // Migrate legacy batch numbers starting with PUR- to the new format
            var legacyBatches = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                _db.InventoryBatches.Where(b => b.BatchNumber.StartsWith("PUR-"))
            );
            foreach (var batch in legacyBatches)
            {
                var newNumber = $"PUR-{batch.ReceivedDate.ToString("ddMMyyyy")}-{batch.ProductId}";
                if (batch.BatchNumber != newNumber)
                {
                    batch.BatchNumber = newNumber;
                }
            }
            await _db.SaveChangesAsync();
        }

        public async Task<bool> RegisterUserAsync(RegisterRequest req, string createdBy)
        {
            var existing = await _userRepo.GetByUsernameAsync(req.Username);
            if (existing != null) return false;

            var roleExists = await _userRepo.RoleExistsAsync(req.Role);
            if (!roleExists) return false;

            var role = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(_db.Roles, r => r.RoleName == req.Role);
            if (role == null) return false;

            var newUser = new User
            {
                UserCode = "U" + DateTime.UtcNow.Ticks.ToString().Substring(10),
                FullName = req.FullName,
                Username = req.Username,
                Email = req.Email,
                Phone = req.Phone,
                PasswordHash = _hasher.HashPassword(req.Password),
                IsActive = true,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow
            };

            await _userRepo.AddAsync(newUser);
            await _userRepo.SaveChangesAsync();

            await _userRepo.AddUserRoleAsync(new UserRole { UserId = newUser.UserId, RoleId = role.RoleId });
            await _userRepo.SaveChangesAsync();

            return true;
        }

        public async Task<IEnumerable<object>> GetAllUsersAsync()
        {
            return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                _db.Users.Select(u => new {
                    u.UserId,
                    u.UserCode,
                    u.FullName,
                    u.Username,
                    u.Email,
                    u.Phone,
                    u.IsActive,
                    Roles = u.UserRoles.Select(ur => ur.Role.RoleName).ToList()
                })
            );
        }

        public async Task<object> GetDashboardStatsAsync()
        {
            var sales = await _salesRepo.GetAllAsync();
            var purchases = await _purchaseRepo.GetAllAsync();
            var accountTransactions = await _accountRepo.GetTransactionsAsync();
            var partnerLedgers = await _partnerRepo.GetLedgersAsync();
            var products = await _productRepo.GetProductsWithDetailsAsync();

            var totalSales = sales.Where(s => s.Status == "COMPLETED").Sum(s => s.TotalAmount);
            var totalPurchases = purchases.Where(p => p.Status == "COMPLETED").Sum(p => p.TotalAmount);

            decimal cashInHand = accountTransactions
                .Where(t => t.TransactionType == "CREDIT")
                .Sum(t => t.Amount) - 
                accountTransactions
                .Where(t => t.TransactionType == "DEBIT")
                .Sum(t => t.Amount);

            decimal totalInvestments = partnerLedgers.Where(l => l.TransactionType == "INVESTMENT").Sum(l => l.Amount);
            decimal totalWithdrawals = partnerLedgers.Where(l => l.TransactionType == "WITHDRAWAL").Sum(l => l.Amount);

            var productsCount = products.Count();
            var stockAlertsCount = products.Count(p => p.InventoryBatches.Sum(b => b.CurrentQuantity) <= p.MinStockAlert);

            var monthlySales = sales
                .Where(s => s.Status == "COMPLETED")
                .GroupBy(s => new { s.SaleDate.Year, s.SaleDate.Month })
                .Select(g => new {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    MonthName = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                    Amount = g.Sum(s => s.TotalAmount)
                })
                .OrderBy(g => g.Year)
                .ThenBy(g => g.Month)
                .Select(g => new {
                    Month = g.MonthName,
                    Amount = g.Amount
                })
                .ToList();

            return new
            {
                TotalSales = totalSales,
                TotalPurchases = totalPurchases,
                CashInHand = cashInHand,
                NetPartnerEquity = totalInvestments - totalWithdrawals,
                ActiveProductsCount = productsCount,
                LowStockAlertsCount = stockAlertsCount,
                MonthlySales = monthlySales
            };
        }

        public async Task<User> AuthenticateAsync(string username, string password)
        {
            var user = await _userRepo.GetByUsernameAsync(username);
            if (user == null || !_hasher.VerifyPassword(password, user.PasswordHash))
                return null;

            user.LastLoginAt = DateTime.UtcNow;
            _userRepo.Update(user);
            await _userRepo.SaveChangesAsync();
            return user;
        }

        // --- AUDIT LOGGER ---

        public async Task LogAuditAsync(string username, string action, string table, string recordId, string oldVal = null, string newVal = null)
        {
            await _accountRepo.AddAuditLogAsync(new AuditLog
            {
                Username = username ?? "",
                ActionName = action ?? "",
                TableName = table ?? "",
                RecordId = recordId ?? "",
                OldValues = oldVal ?? "",
                NewValues = newVal ?? "",
                Timestamp = DateTime.UtcNow
            });
            await _accountRepo.SaveChangesAsync();
        }

        // --- PRODUCTS MANAGEMENT ---

        public async Task<Product> CreateProductAsync(Product prod, string username)
        {
            prod.CreatedBy = username;
            prod.CreatedAt = DateTime.UtcNow;
            await _productRepo.AddAsync(prod);
            await _productRepo.SaveChangesAsync();
            await LogAuditAsync(username, "CREATE_PRODUCT", "TB_PRODUCT", prod.ProductId.ToString(), null, prod.ProductName);
            return prod;
        }

        // --- PURCHASES (Transaction Driven) ---

        public async Task<Purchase> CreatePurchaseAsync(PurchaseCreateRequest req, string username)
        {
            using (var transaction = await _db.Database.BeginTransactionAsync())
            {
                try
                {
                    var totalDetailAmount = req.Details.Sum(d => d.Quantity * d.UnitCost);
                    var totalExpenseAmount = req.Expenses?.Sum(e => e.Amount) ?? 0;
                    var totalPurchaseAmount = totalDetailAmount + totalExpenseAmount + req.Adjustment;

                    if (req.PaidAmount > totalPurchaseAmount)
                    {
                        throw new InvalidOperationException("Paid amount cannot exceed the total purchase amount.");
                    }

                    var purchase = new Purchase
                    {
                        PurchaseNumber = "PUR-" + DateTime.UtcNow.Ticks.ToString().Substring(10),
                        SupplierId = req.SupplierId,
                        PurchaseDate = req.PurchaseDate,
                        TotalAmount = totalPurchaseAmount,
                        PaidAmount = req.PaidAmount,
                        PaymentStatus = req.PaidAmount >= (totalDetailAmount + totalExpenseAmount) ? "PAID" : req.PaidAmount > 0 ? "PARTIAL" : "UNPAID",
                        Status = "COMPLETED",
                        CreatedBy = username,
                        CreatedAt = DateTime.UtcNow
                    };

                    foreach (var d in req.Details)
                    {
                        purchase.Details.Add(new PurchaseDetail
                        {
                            ProductId = d.ProductId,
                            Quantity = d.Quantity,
                            UnitCost = d.UnitCost
                        });
                    }

                    await _purchaseRepo.AddAsync(purchase);
                    await _purchaseRepo.SaveChangesAsync();

                    // Log Purchase Expenses and prepare allocations
                    var dbExpenses = new List<PurchaseExpense>();
                    if (req.Expenses != null)
                    {
                        foreach (var ex in req.Expenses)
                        {
                            var dbExpense = new PurchaseExpense
                            {
                                PurchaseId = purchase.PurchaseId,
                                ExpenseTypeId = ex.ExpenseTypeId,
                                Amount = ex.Amount,
                                AllocationMethod = ex.AllocationMethod,
                                Description = ex.Description,
                                CreatedBy = username,
                                CreatedAt = DateTime.UtcNow
                            };
                            await _db.PurchaseExpenses.AddAsync(dbExpense);
                            dbExpenses.Add(dbExpense);
                        }
                        await _db.SaveChangesAsync();
                    }

                    // Process details and create batch projections
                    var totalQty = req.Details.Sum(d => d.Quantity);
                    var totalValue = req.Details.Sum(d => d.Quantity * d.UnitCost);

                    foreach (var detail in purchase.Details)
                    {
                        decimal allocatedExpense = 0;
                        if (req.Expenses != null && req.Expenses.Any())
                        {
                            foreach (var dbEx in dbExpenses)
                            {
                                decimal stepAllocation = 0;
                                if (dbEx.AllocationMethod == "QUANTITY_BASED" && totalQty > 0)
                                {
                                    stepAllocation = dbEx.Amount * ((decimal)detail.Quantity / totalQty);
                                }
                                else if (dbEx.AllocationMethod == "VALUE_BASED" && totalValue > 0)
                                {
                                    stepAllocation = dbEx.Amount * ((detail.Quantity * detail.UnitCost) / totalValue);
                                }

                                allocatedExpense += stepAllocation;

                                var pea = new PurchaseExpenseAllocation
                                {
                                    PurchaseExpenseId = dbEx.PurchaseExpenseId,
                                    PurchaseDetailId = detail.PurchaseDetailId,
                                    AllocatedAmount = stepAllocation,
                                    CreatedBy = username,
                                    CreatedAt = DateTime.UtcNow
                                };
                                await _db.PurchaseExpenseAllocations.AddAsync(pea);
                            }
                        }

                        decimal landedUnitCost = detail.UnitCost + (detail.Quantity > 0 ? (allocatedExpense / detail.Quantity) : 0);

                        var batch = new InventoryBatch
                        {
                            ProductId = detail.ProductId,
                            InitialQuantity = detail.Quantity,
                            CurrentQuantity = detail.Quantity,
                            UnitCost = detail.UnitCost,
                            LandedUnitCost = landedUnitCost,
                            TotalLandedCost = detail.Quantity * landedUnitCost,
                            LocationId = 1, // Default Godown location
                            ReceivedDate = req.PurchaseDate,
                            Status = "FINALIZED", // PENDING -> CALCULATED -> FINALIZED
                            BatchNumber = $"PUR-{purchase.PurchaseDate.ToString("ddMMyyyy")}-{detail.ProductId}",
                            CreatedBy = username,
                            CreatedAt = DateTime.UtcNow
                        };
                        await _batchRepo.AddBatchAsync(batch);
                        await _batchRepo.SaveChangesAsync();

                        detail.BatchId = batch.BatchId;

                        // AUTHORITATIVE SOURCE OF TRUTH MOVEMENT ENTRY (PURCHASE_IN)
                        var movement = new InventoryMovement
                        {
                            ProductId = detail.ProductId,
                            BatchId = batch.BatchId,
                            LocationId = 1,
                            MovementType = "PURCHASE_IN",
                            Direction = "IN",
                            Quantity = detail.Quantity,
                            UnitCost = landedUnitCost,
                            TotalCost = detail.Quantity * landedUnitCost,
                            ReferenceType = "PURCHASE",
                            ReferenceId = purchase.PurchaseId.ToString(),
                            Description = $"Purchase {purchase.PurchaseNumber}",
                            CreatedBy = username,
                            CreatedAt = DateTime.UtcNow
                        };
                        await _batchRepo.AddMovementAsync(movement);
                    }

                    // Map Accounts financial transactions & perform strict Cash In-Hand balance validation
                    if (req.PaymentContributions != null && req.PaymentContributions.Any(c => c.Amount > 0))
                    {
                        var totalContrib = req.PaymentContributions.Where(c => c.Amount > 0).Sum(c => c.Amount);
                        if (Math.Abs(totalContrib - req.PaidAmount) > 0.01m)
                        {
                            throw new InvalidOperationException($"The sum of payment contributions (₹{totalContrib:N2}) must match the paid amount (₹{req.PaidAmount:N2}).");
                        }

                        foreach (var contrib in req.PaymentContributions.Where(c => c.Amount > 0))
                        {
                            var acc = await _accountRepo.GetByNameAsync(contrib.AccountName) 
                                      ?? await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(_db.BusinessAccounts, a => a.AccountId.ToString() == contrib.AccountName);
                            if (acc == null)
                                throw new InvalidOperationException($"Account '{contrib.AccountName}' not found.");

                            // Authoritative In-Hand Balance Check
                            var txs = await _db.AccountTransactions.Where(t => t.AccountId == acc.AccountId).ToListAsync();
                            var credits = txs.Where(t => t.TransactionType == "CREDIT").Sum(t => t.Amount);
                            var debits = txs.Where(t => t.TransactionType == "DEBIT").Sum(t => t.Amount);
                            var available = credits - debits;

                            if (contrib.Amount > available)
                            {
                                throw new InvalidOperationException($"Insufficient funds in {acc.AccountName}. Available Cash In-Hand is ₹{available:N2}, but attempted to spend ₹{contrib.Amount:N2}. Please record a partner investment first.");
                            }

                            await _accountRepo.AddTransactionAsync(new AccountTransaction
                            {
                                AccountId = acc.AccountId,
                                TransactionType = "DEBIT",
                                Amount = contrib.Amount,
                                ReferenceType = "PURCHASE",
                                ReferenceId = purchase.PurchaseId.ToString(),
                                Description = $"Paid ₹{contrib.Amount:N2} for Purchase {purchase.PurchaseNumber}",
                                CreatedBy = username,
                                CreatedAt = purchase.PurchaseDate != default ? purchase.PurchaseDate : DateTime.UtcNow
                            });
                        }
                    }
                    else if (req.PaidAmount > 0)
                    {
                        var cashAcc = await _accountRepo.GetByNameAsync(req.PaymentMethodAccountName)
                                      ?? await _accountRepo.GetByIdAsync(1)
                                      ?? await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(_db.BusinessAccounts);
                        if (cashAcc != null)
                        {
                            // Authoritative In-Hand Balance Check
                            var txs = await _db.AccountTransactions.Where(t => t.AccountId == cashAcc.AccountId).ToListAsync();
                            var credits = txs.Where(t => t.TransactionType == "CREDIT").Sum(t => t.Amount);
                            var debits = txs.Where(t => t.TransactionType == "DEBIT").Sum(t => t.Amount);
                            var available = credits - debits;

                            if (req.PaidAmount > available)
                            {
                                throw new InvalidOperationException($"Insufficient funds in {cashAcc.AccountName}. Available Cash In-Hand is ₹{available:N2}, but attempted to spend ₹{req.PaidAmount:N2}. Please record a partner investment first.");
                            }

                            await _accountRepo.AddTransactionAsync(new AccountTransaction
                            {
                                AccountId = cashAcc.AccountId,
                                TransactionType = "DEBIT",
                                Amount = req.PaidAmount,
                                ReferenceType = "PURCHASE",
                                ReferenceId = purchase.PurchaseId.ToString(),
                                Description = $"Paid for Purchase {purchase.PurchaseNumber}",
                                CreatedBy = username,
                                CreatedAt = purchase.PurchaseDate != default ? purchase.PurchaseDate : DateTime.UtcNow
                            });
                        }
                    }

                    await _purchaseRepo.SaveChangesAsync();
                    await _accountRepo.SaveChangesAsync();
                    await LogAuditAsync(username, "CREATE_PURCHASE", "TB_PURCHASE", purchase.PurchaseId.ToString(), null, purchase.PurchaseNumber);

                    await transaction.CommitAsync();
                    return purchase;
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }

        // --- SALES (Transaction Driven / Batch Deductions) ---

        public async Task<Sale> CreateSaleAsync(SaleCreateRequest req, string username)
        {
            var total = req.Details.Sum(d => d.Quantity * d.UnitPrice) + req.Adjustment;

            if (req.PaidAmount > total)
            {
                throw new InvalidOperationException("Collected amount cannot exceed the total invoice amount.");
            }

            var sale = new Sale
            {
                SaleNumber = "SAL-" + DateTime.UtcNow.Ticks.ToString().Substring(10),
                CustomerId = req.CustomerId,
                SaleDate = req.SaleDate,
                TotalAmount = total,
                PaidAmount = req.PaidAmount,
                PaymentStatus = req.PaidAmount >= total ? "PAID" : req.PaidAmount > 0 ? "PARTIAL" : "UNPAID",
                Status = "COMPLETED",
                CreatedBy = username,
                CreatedAt = DateTime.UtcNow
            };

            foreach (var d in req.Details)
            {
                var b = await _batchRepo.GetBatchByIdAsync(d.BatchId);
                if (b == null)
                {
                    throw new InvalidOperationException($"Stock batch ID {d.BatchId} not found.");
                }
                if (b.CurrentQuantity < d.Quantity)
                {
                    throw new InvalidOperationException($"Insufficient inventory stock in batch {b.BatchNumber}. Available: {b.CurrentQuantity}, Requested: {d.Quantity}");
                }

                b.CurrentQuantity -= d.Quantity;

                var detail = new SaleDetail
                {
                    ProductId = d.ProductId,
                    Quantity = d.Quantity,
                    UnitPrice = d.UnitPrice,
                    BatchId = b.BatchId
                };
                sale.Details.Add(detail);

                _batchRepo.UpdateBatch(b);

                await _batchRepo.AddMovementAsync(new InventoryMovement
                {
                    ProductId = d.ProductId,
                    BatchId = b.BatchId,
                    LocationId = b.LocationId,
                    MovementType = "SALE_OUT",
                    Direction = "OUT",
                    Quantity = -d.Quantity,
                    UnitCost = b.UnitCost,
                    TotalCost = -d.Quantity * b.UnitCost,
                    ReferenceType = "SALE",
                    ReferenceId = sale.SaleNumber,
                    Description = $"Sale {sale.SaleNumber}",
                    CreatedBy = username,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _salesRepo.AddAsync(sale);
            await _salesRepo.SaveChangesAsync();

            var cashAcc = await _accountRepo.GetByNameAsync(req.PaymentMethodAccountName);
            if (cashAcc != null && req.PaidAmount > 0)
            {
                await _accountRepo.AddTransactionAsync(new AccountTransaction
                {
                    AccountId = cashAcc.AccountId,
                    TransactionType = "CREDIT",
                    Amount = req.PaidAmount,
                    ReferenceType = "SALE",
                    ReferenceId = sale.SaleId.ToString(),
                    Description = $"Collected payment for Sale {sale.SaleNumber}",
                    CreatedBy = username,
                    CreatedAt = DateTime.UtcNow
                });
                await _accountRepo.SaveChangesAsync();
            }

            await LogAuditAsync(username, "CREATE_SALE", "TB_SALE", sale.SaleId.ToString(), null, sale.SaleNumber);
            return sale;
        }


        // --- PARTNERS ---

        public async Task RecordPartnerTransactionAsync(int partnerId, string type, decimal amount, string desc, string accountName, string username)
        {
            var acc = !string.IsNullOrWhiteSpace(accountName) ? await _accountRepo.GetByNameAsync(accountName) : null;
            if (acc == null)
            {
                var allAccounts = await _accountRepo.GetAllAsync();
                if (!string.IsNullOrWhiteSpace(accountName))
                {
                    acc = allAccounts.FirstOrDefault(a => a.AccountName.ToLower().Contains(accountName.ToLower()) || accountName.ToLower().Contains(a.AccountName.ToLower()))
                          ?? allAccounts.FirstOrDefault();
                }
                else
                {
                    acc = allAccounts.FirstOrDefault();
                }
            }
            if (acc == null) throw new ArgumentException("Business account not found.");

            if (type == "WITHDRAWAL")
            {
                var ledgerEntries = _db.PartnerLedgers.Where(pl => pl.PartnerId == partnerId).ToList();
                decimal totalInvestment = ledgerEntries.Where(pl => pl.TransactionType == "INVESTMENT").Sum(pl => pl.Amount);
                decimal totalWithdrawals = ledgerEntries.Where(pl => pl.TransactionType == "WITHDRAWAL").Sum(pl => pl.Amount);
                decimal currentBalance = totalInvestment - totalWithdrawals;

                if (amount > currentBalance)
                {
                    throw new ArgumentException($"Cannot withdraw ₹{amount}. The partner's current net investment balance is only ₹{currentBalance}.");
                }
            }

            await _partnerRepo.AddLedgerEntryAsync(new PartnerLedger
            {
                PartnerId = partnerId,
                TransactionType = type,
                Amount = amount,
                Description = desc,
                CreatedBy = username,
                CreatedAt = DateTime.UtcNow
            });

            var financialTxType = type == "INVESTMENT" ? "CREDIT" : "DEBIT";

            await _accountRepo.AddTransactionAsync(new AccountTransaction
            {
                AccountId = acc.AccountId,
                TransactionType = financialTxType,
                Amount = amount,
                ReferenceType = "PARTNER_TRANSACTION",
                ReferenceId = "PART-" + DateTime.UtcNow.Ticks,
                Description = $"{type} by Partner. Details: {desc}",
                CreatedBy = username,
                CreatedAt = DateTime.UtcNow
            });

            await _partnerRepo.SaveChangesAsync();
            await _accountRepo.SaveChangesAsync();
            await LogAuditAsync(username, "PARTNER_TRANSACTION", "TB_PARTNER_LEDGER", partnerId.ToString(), null, $"{type}: {amount}");
        }

        // --- ADJUSTMENTS ---

        public async Task AdjustStockAsync(int batchId, int newQuantity, string desc, string username)
        {
            var batch = await _batchRepo.GetBatchByIdAsync(batchId);
            if (batch == null) throw new ArgumentException("Batch not found.");

            if (newQuantity < 0)
            {
                throw new ArgumentException("Adjusted stock quantity cannot be negative.");
            }

            int difference = newQuantity - batch.CurrentQuantity;
            batch.CurrentQuantity = newQuantity;
            _batchRepo.UpdateBatch(batch);

            await _batchRepo.AddMovementAsync(new InventoryMovement
            {
                ProductId = batch.ProductId,
                BatchId = batch.BatchId,
                LocationId = batch.LocationId ?? 1,
                MovementType = "ADJUSTMENT",
                Direction = difference >= 0 ? "IN" : "OUT",
                Quantity = difference,
                UnitCost = batch.UnitCost,
                TotalCost = difference * batch.UnitCost,
                ReferenceType = "STOCK_ADJUSTMENT",
                ReferenceId = batch.BatchId.ToString(),
                Description = desc,
                CreatedBy = username,
                CreatedAt = DateTime.UtcNow
            });

            await _batchRepo.SaveChangesAsync();
            await LogAuditAsync(username, "STOCK_ADJUSTMENT", "TB_INVENTORY_BATCH", batchId.ToString(), null, $"New qty: {newQuantity}");
        }

        public async Task UpdateSaleAsync(int id, SaleUpdateRequest req, string username)
        {
            using (var transaction = await _db.Database.BeginTransactionAsync())
            {
                try
                {
                    var sale = await _db.Sales.Include(s => s.Details).FirstOrDefaultAsync(s => s.SaleId == id);
                    if (sale == null) throw new ArgumentException("Sale not found.");

                    // Check if details have actually changed
                    bool detailsChanged = false;
                    if (sale.Details.Count != req.Details.Count)
                    {
                        detailsChanged = true;
                    }
                    else
                    {
                        var oldSorted = sale.Details.OrderBy(d => d.ProductId).ThenBy(d => d.BatchId).ThenBy(d => d.Quantity).ThenBy(d => d.UnitPrice).ToList();
                        var newSorted = req.Details.OrderBy(d => d.ProductId).ThenBy(d => d.BatchId).ThenBy(d => d.Quantity).ThenBy(d => d.UnitPrice).ToList();
                        for (int i = 0; i < oldSorted.Count; i++)
                        {
                            if (oldSorted[i].ProductId != newSorted[i].ProductId ||
                                oldSorted[i].BatchId != newSorted[i].BatchId ||
                                oldSorted[i].Quantity != newSorted[i].Quantity ||
                                oldSorted[i].UnitPrice != newSorted[i].UnitPrice)
                            {
                                detailsChanged = true;
                                break;
                            }
                        }
                    }

                    if (detailsChanged)
                    {
                        // 1. Revert previous stock deductions
                        foreach (var oldDetail in sale.Details)
                        {
                            if (oldDetail.BatchId > 0)
                            {
                                var batch = await _db.InventoryBatches.FindAsync(oldDetail.BatchId);
                                if (batch != null)
                                {
                                    batch.CurrentQuantity += oldDetail.Quantity;
                                }
                            }
                        }

                        // 2. Remove old sale details from database
                        _db.SaleDetails.RemoveRange(sale.Details);
                        await _db.SaveChangesAsync();

                        // 3. Add new sale details and deduct stock
                        sale.Details.Clear();
                        foreach (var d in req.Details)
                        {
                            var batch = await _db.InventoryBatches.FindAsync(d.BatchId);
                            if (batch == null) throw new ArgumentException($"Batch not found for product {d.ProductId}.");
                            if (batch.CurrentQuantity < d.Quantity)
                            {
                                throw new InvalidOperationException($"Insufficient stock in batch {batch.BatchNumber}. Available: {batch.CurrentQuantity}, Requested: {d.Quantity}");
                            }

                            batch.CurrentQuantity -= d.Quantity;

                            sale.Details.Add(new SaleDetail
                            {
                                ProductId = d.ProductId,
                                BatchId = d.BatchId,
                                Quantity = d.Quantity,
                                UnitPrice = d.UnitPrice
                            });
                        }
                    }

                    // 4. Update sale record fields
                    sale.CustomerId = req.CustomerId;
                    sale.SaleDate = req.SaleDate;
                    sale.TotalAmount = req.TotalAmount;
                    sale.PaidAmount = req.PaidAmount;
                    sale.PaymentStatus = req.PaymentStatus;
                    sale.Status = req.Status;
                    sale.UpdatedBy = username;
                    sale.UpdatedAt = DateTime.UtcNow;

                    // 5. Synchronize linked AccountTransaction
                    var linkedTx = await _db.AccountTransactions
                        .FirstOrDefaultAsync(t => t.ReferenceType == "SALE" && (t.ReferenceId == id.ToString() || t.ReferenceId == sale.SaleNumber));

                    var targetAcc = !string.IsNullOrWhiteSpace(req.PaymentMethodAccountName)
                        ? await _accountRepo.GetByNameAsync(req.PaymentMethodAccountName)
                        : null;

                    if (req.PaidAmount > 0)
                    {
                        if (linkedTx != null)
                        {
                            linkedTx.Amount = req.PaidAmount;
                            if (targetAcc != null)
                            {
                                linkedTx.AccountId = targetAcc.AccountId;
                            }
                            linkedTx.CreatedAt = req.SaleDate != default ? req.SaleDate : DateTime.UtcNow;
                            linkedTx.Description = $"Collected payment for Sale {sale.SaleNumber}";
                            _db.AccountTransactions.Update(linkedTx);
                        }
                        else if (targetAcc != null)
                        {
                            await _accountRepo.AddTransactionAsync(new AccountTransaction
                            {
                                AccountId = targetAcc.AccountId,
                                TransactionType = "CREDIT",
                                Amount = req.PaidAmount,
                                ReferenceType = "SALE",
                                ReferenceId = sale.SaleId.ToString(),
                                Description = $"Collected payment for Sale {sale.SaleNumber}",
                                CreatedBy = username,
                                CreatedAt = req.SaleDate != default ? req.SaleDate : DateTime.UtcNow
                            });
                        }
                    }
                    else if (linkedTx != null)
                    {
                        _db.AccountTransactions.Remove(linkedTx);
                    }

                    await _db.SaveChangesAsync();
                    await transaction.CommitAsync();

                    await LogAuditAsync(username, "SALE_UPDATE", "TB_SALES", id.ToString(), null, $"Sale {sale.SaleNumber} updated.");
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }

        public async Task UpdatePurchaseAsync(int id, PurchaseUpdateRequest req, string username)
        {
            using (var transaction = await _db.Database.BeginTransactionAsync())
            {
                try
                {
                    var purchase = await _db.Purchases.Include(p => p.Details).FirstOrDefaultAsync(p => p.PurchaseId == id);
                    if (purchase == null) throw new ArgumentException("Purchase not found.");

                    // Check if details have actually changed
                    bool detailsChanged = false;
                    if (purchase.Details.Count != req.Details.Count)
                    {
                        detailsChanged = true;
                    }
                    else
                    {
                        var oldSorted = purchase.Details.OrderBy(d => d.ProductId).ThenBy(d => d.Quantity).ThenBy(d => d.UnitCost).ToList();
                        var newSorted = req.Details.OrderBy(d => d.ProductId).ThenBy(d => d.Quantity).ThenBy(d => d.UnitCost).ToList();
                        for (int i = 0; i < oldSorted.Count; i++)
                        {
                            if (oldSorted[i].ProductId != newSorted[i].ProductId ||
                                oldSorted[i].Quantity != newSorted[i].Quantity ||
                                oldSorted[i].UnitCost != newSorted[i].UnitCost)
                            {
                                detailsChanged = true;
                                break;
                            }
                        }
                    }

                    if (detailsChanged)
                    {
                        // 1. For each old purchase detail, locate the generated batch and delete/adjust it
                        foreach (var oldDetail in purchase.Details)
                        {
                            var allocations = await _db.PurchaseExpenseAllocations.Where(pea => pea.PurchaseDetailId == oldDetail.PurchaseDetailId).ToListAsync();
                            _db.PurchaseExpenseAllocations.RemoveRange(allocations);

                            if (oldDetail.BatchId.HasValue)
                            {
                                var batch = await _db.InventoryBatches.FindAsync(oldDetail.BatchId.Value);
                                if (batch != null)
                                {
                                    // Verify if any sales have been made from this batch
                                    var soldQty = await _db.SaleDetails.Where(sd => sd.BatchId == batch.BatchId).SumAsync(sd => sd.Quantity);
                                    if (soldQty > 0)
                                    {
                                        throw new InvalidOperationException($"Cannot update purchase detail because items have already been sold from the generated batch {batch.BatchNumber}.");
                                    }
                                    var movements = await _db.InventoryMovements.Where(im => im.BatchId == batch.BatchId).ToListAsync();
                                    _db.InventoryMovements.RemoveRange(movements);

                                    _db.InventoryBatches.Remove(batch);
                                }
                            }
                        }

                        // 2. Remove old purchase details from database
                        _db.PurchaseDetails.RemoveRange(purchase.Details);
                        await _db.SaveChangesAsync();

                        // 3. Add new details and generate new batches
                        purchase.Details.Clear();
                        foreach (var d in req.Details)
                        {
                            // Generate a batch
                            var batch = new InventoryBatch
                            {
                                ProductId = d.ProductId,
                                InitialQuantity = d.Quantity,
                                CurrentQuantity = d.Quantity,
                                UnitCost = d.UnitCost,
                                LandedUnitCost = d.UnitCost,
                                TotalLandedCost = d.Quantity * d.UnitCost,
                                LocationId = 1, // Default Godown
                                ReceivedDate = req.PurchaseDate,
                                Status = "FINALIZED",
                                BatchNumber = $"PUR-{purchase.PurchaseDate.ToString("ddMMyyyy")}-{d.ProductId}",
                                CreatedBy = username,
                                CreatedAt = DateTime.UtcNow
                            };
                            await _db.InventoryBatches.AddAsync(batch);
                            await _db.SaveChangesAsync();

                            // ADD NEW MOVEMENT
                            await _db.InventoryMovements.AddAsync(new InventoryMovement
                            {
                                ProductId = d.ProductId,
                                BatchId = batch.BatchId,
                                LocationId = 1,
                                MovementType = "PURCHASE_IN",
                                Direction = "IN",
                                Quantity = d.Quantity,
                                UnitCost = d.UnitCost,
                                TotalCost = d.Quantity * d.UnitCost,
                                ReferenceType = "PURCHASE_UPDATE",
                                ReferenceId = purchase.PurchaseId.ToString(),
                                Description = $"Stock added from updated purchase PUR-{purchase.PurchaseId}",
                                CreatedBy = username,
                                CreatedAt = DateTime.UtcNow
                            });

                            purchase.Details.Add(new PurchaseDetail
                            {
                                ProductId = d.ProductId,
                                Quantity = d.Quantity,
                                UnitCost = d.UnitCost,
                                BatchId = batch.BatchId
                            });
                        }
                    }

                    // 4. Update purchase record fields
                    purchase.SupplierId = req.SupplierId;
                    purchase.PurchaseDate = req.PurchaseDate;
                    purchase.TotalAmount = req.TotalAmount;
                    purchase.PaidAmount = req.PaidAmount;
                    purchase.PaymentStatus = req.PaymentStatus;
                    purchase.Status = req.Status;
                    purchase.UpdatedBy = username;
                    purchase.UpdatedAt = DateTime.UtcNow;

                    await _db.SaveChangesAsync();
                    await transaction.CommitAsync();

                    await LogAuditAsync(username, "PURCHASE_UPDATE", "TB_PURCHASES", id.ToString(), null, $"Purchase {purchase.PurchaseNumber} updated.");
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }

        // --- SALES ORDERS (Draft / Advance Bookings & Conversion to Sales) ---

        public async Task<Order> CreateOrderAsync(OrderCreateRequest req, string username)
        {
            if (req.Details == null || !req.Details.Any())
                throw new ArgumentException("Order must contain at least one item.");

            var customer = await _db.Customers.FindAsync(req.CustomerId);
            if (customer == null) throw new ArgumentException("Customer not found.");

            var order = new Order
            {
                OrderNo = "ORD-" + DateTime.UtcNow.Ticks.ToString().Substring(10),
                CustomerId = req.CustomerId,
                OrderDate = req.OrderDate != default ? req.OrderDate : DateTime.UtcNow,
                ExpectedDate = req.ExpectedDate != default ? req.ExpectedDate : DateTime.UtcNow.AddDays(1),
                Priority = !string.IsNullOrWhiteSpace(req.Priority) ? req.Priority.ToUpper() : "NORMAL",
                Status = "DRAFT",
                Notes = req.Notes ?? "",
                CreatedBy = username,
                CreatedAt = DateTime.UtcNow
            };

            foreach (var d in req.Details)
            {
                var prod = await _productRepo.GetByIdAsync(d.ProductId);
                if (prod == null) throw new ArgumentException($"Product ID {d.ProductId} not found.");

                order.Details.Add(new OrderDetail
                {
                    ProductId = d.ProductId,
                    OrderedQuantity = d.OrderedQuantity,
                    SellingPrice = d.SellingPrice,
                    ReservedQuantity = 0,
                    DeliveredQuantity = 0
                });
            }

            await _orderRepo.AddAsync(order);
            await _orderRepo.SaveChangesAsync();
            await LogAuditAsync(username, "CREATE_ORDER", "TB_ORDERS", order.OrderId.ToString(), null, order.OrderNo);
            return order;
        }

        public async Task<Order> UpdateOrderAsync(int id, OrderUpdateRequest req, string username)
        {
            var order = await _orderRepo.GetOrderWithDetailsByIdAsync(id);
            if (order == null) throw new ArgumentException("Order not found.");

            if (order.Status == "CONVERTED")
                throw new InvalidOperationException("Cannot modify an order that has already been converted into a finalized sale.");

            order.CustomerId = req.CustomerId;
            order.OrderDate = req.OrderDate;
            order.ExpectedDate = req.ExpectedDate;
            order.Priority = !string.IsNullOrWhiteSpace(req.Priority) ? req.Priority.ToUpper() : order.Priority;
            if (!string.IsNullOrWhiteSpace(req.Status))
            {
                order.Status = req.Status.ToUpper();
            }
            order.Notes = req.Notes ?? "";
            order.UpdatedBy = username;
            order.UpdatedAt = DateTime.UtcNow;

            // Remove existing details and replace
            _db.OrderDetails.RemoveRange(order.Details);
            order.Details.Clear();

            foreach (var d in req.Details)
            {
                order.Details.Add(new OrderDetail
                {
                    OrderId = order.OrderId,
                    ProductId = d.ProductId,
                    OrderedQuantity = d.OrderedQuantity,
                    SellingPrice = d.SellingPrice,
                    ReservedQuantity = 0,
                    DeliveredQuantity = 0
                });
            }

            await _orderRepo.SaveChangesAsync();
            await LogAuditAsync(username, "UPDATE_ORDER", "TB_ORDERS", order.OrderId.ToString(), null, order.OrderNo);
            return order;
        }

        public async Task DeleteOrderAsync(int id, string username)
        {
            var order = await _orderRepo.GetOrderWithDetailsByIdAsync(id);
            if (order == null) throw new ArgumentException("Order not found.");

            if (order.Status == "CONVERTED")
                throw new InvalidOperationException("Cannot delete an order that has already been converted into a sale.");

            _db.OrderDetails.RemoveRange(order.Details);
            _orderRepo.Delete(order);
            await _orderRepo.SaveChangesAsync();
            await LogAuditAsync(username, "DELETE_ORDER", "TB_ORDERS", id.ToString(), order.OrderNo, null);
        }

        public async Task<Sale> ConvertOrderToSaleAsync(OrderConvertToSaleRequest req, string username)
        {
            var order = await _orderRepo.GetOrderWithDetailsByIdAsync(req.OrderId);
            if (order == null) throw new ArgumentException("Order not found.");

            if (order.Status == "CONVERTED")
                throw new InvalidOperationException("This order has already been converted to a sale.");

            var saleDetails = new List<SaleDetailRequest>();

            foreach (var d in order.Details)
            {
                // Auto-allocate available batch with stock
                var availableBatches = await _db.InventoryBatches
                    .Where(b => b.ProductId == d.ProductId && b.CurrentQuantity > 0)
                    .OrderBy(b => b.CreatedAt)
                    .ToListAsync();

                int remainingToFulfill = d.OrderedQuantity;
                foreach (var b in availableBatches)
                {
                    if (remainingToFulfill <= 0) break;
                    int take = Math.Min(remainingToFulfill, b.CurrentQuantity);
                    saleDetails.Add(new SaleDetailRequest(d.ProductId, take, d.SellingPrice, b.BatchId));
                    remainingToFulfill -= take;
                }

                if (remainingToFulfill > 0)
                {
                    var prod = await _productRepo.GetByIdAsync(d.ProductId);
                    throw new InvalidOperationException($"Insufficient inventory stock for '{prod?.ProductName ?? $"Product #{d.ProductId}"}'. Required: {d.OrderedQuantity}, Available: {d.OrderedQuantity - remainingToFulfill}. Please add or manufacture stock batches first.");
                }
            }

            var saleReq = new SaleCreateRequest(
                order.CustomerId,
                DateTime.UtcNow,
                saleDetails,
                req.PaidAmount,
                req.PaymentMethodAccountName,
                0
            );

            var sale = await CreateSaleAsync(saleReq, username);

            order.Status = "CONVERTED";
            order.UpdatedBy = username;
            order.UpdatedAt = DateTime.UtcNow;
            await _orderRepo.SaveChangesAsync();

            await LogAuditAsync(username, "CONVERT_ORDER_TO_SALE", "TB_ORDERS", order.OrderId.ToString(), order.OrderNo, $"Converted to Sale {sale.SaleNumber}");
            return sale;
        }
    }
}
