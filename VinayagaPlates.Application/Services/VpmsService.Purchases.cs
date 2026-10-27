using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VinayagaPlates.Contracts.DTOs;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Application.Services
{
    public partial class VpmsService
    {
        /// <summary>
        /// Deletes a purchase, validates stock usage, reverts inventory batches/movements,
        /// and cleanly rolls back linked purchase payments and ledger debits. Fully ACID transactional.
        /// </summary>
        public async Task<object> DeletePurchaseAsync(int id)
        {
            using (var transaction = await _db.Database.BeginTransactionAsync())
            {
                try
                {
                    var purchase = await _db.Purchases
                        .Include(p => p.Details)
                        .Include(p => p.Supplier)
                        .FirstOrDefaultAsync(p => p.PurchaseId == id);

                    if (purchase == null)
                        throw new ArgumentException("Purchase not found.");

                    // 1. Inventory reversal check
                    if (purchase.Details != null)
                    {
                        foreach (var d in purchase.Details)
                        {
                            if (d.BatchId > 0)
                            {
                                var batch = await _db.InventoryBatches.FindAsync(d.BatchId);
                                if (batch != null)
                                {
                                    if (batch.CurrentQuantity < batch.InitialQuantity)
                                    {
                                        throw new InvalidOperationException($"Cannot delete purchase because {batch.InitialQuantity - batch.CurrentQuantity} units from batch {batch.BatchNumber} have already been sold in Sales.");
                                    }

                                    var movements = await _db.InventoryMovements
                                        .Where(m => m.BatchId == batch.BatchId || (m.ReferenceType == "PURCHASE" && m.ReferenceId == id.ToString()))
                                        .ToListAsync();
                                    if (movements.Any())
                                    {
                                        _db.InventoryMovements.RemoveRange(movements);
                                    }

                                    _db.InventoryBatches.Remove(batch);
                                }
                            }
                        }
                    }

                    // 2. Remove purchase payments and linked account transactions
                    var payments = await _db.PurchasePayments.Where(p => p.PurchaseId == id).ToListAsync();
                    var paymentIdStrs = payments.Select(p => p.PaymentId.ToString()).ToList();

                    var linkedTxs = await _db.AccountTransactions
                        .Where(t => (t.ReferenceType == "PURCHASE" || t.ReferenceType == "PURCHASE_PAYMENT") &&
                            (t.ReferenceId == id.ToString() ||
                             t.ReferenceId == purchase.PurchaseNumber ||
                             paymentIdStrs.Contains(t.ReferenceId)))
                        .ToListAsync();

                    if (linkedTxs.Any())
                    {
                        _db.AccountTransactions.RemoveRange(linkedTxs);
                    }

                    if (payments.Any())
                    {
                        _db.PurchasePayments.RemoveRange(payments);
                    }

                    // 3. Remove expense allocations and purchase expenses if any
                    var expenseAllocations = await _db.PurchaseExpenseAllocations
                        .Where(pea => pea.PurchaseDetail.PurchaseId == id)
                        .ToListAsync();
                    if (expenseAllocations.Any())
                    {
                        _db.PurchaseExpenseAllocations.RemoveRange(expenseAllocations);
                    }

                    var expenses = await _db.PurchaseExpenses
                        .Where(pe => pe.PurchaseId == id)
                        .ToListAsync();
                    if (expenses.Any())
                    {
                        _db.PurchaseExpenses.RemoveRange(expenses);
                    }

                    // 4. Remove purchase details and purchase
                    if (purchase.Details != null)
                    {
                        _db.PurchaseDetails.RemoveRange(purchase.Details);
                    }
                    _db.Purchases.Remove(purchase);

                    await _db.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return new
                    {
                        purchase.PurchaseId,
                        purchase.PurchaseNumber,
                        SupplierName = purchase.Supplier?.SupplierName ?? $"Supplier ID {purchase.SupplierId}",
                        purchase.TotalAmount,
                        purchase.PaidAmount,
                        purchase.Status,
                        purchase.PurchaseDate,
                        TotalItems = purchase.Details?.Sum(d => d.Quantity) ?? 0
                    };
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }
    }
}
