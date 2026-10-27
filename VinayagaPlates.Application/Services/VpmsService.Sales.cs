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
        /// Deletes a sale, rolls back stock to batches, and cleans up linked payments and ledger transactions.
        /// Fully ACID transactional (MNC Standard).
        /// </summary>
        public async Task<object> DeleteSaleAsync(int id)
        {
            using (var transaction = await _db.Database.BeginTransactionAsync())
            {
                try
                {
                    var sale = await _db.Sales
                        .Include(s => s.Details)
                        .Include(s => s.Customer)
                        .FirstOrDefaultAsync(s => s.SaleId == id);

                    if (sale == null)
                        throw new ArgumentException("Sale not found.");

                    // 1. Restore the batch quantities (revert stock deduction)
                    if (sale.Details != null)
                    {
                        foreach (var d in sale.Details)
                        {
                            if (d.BatchId > 0)
                            {
                                var batch = await _db.InventoryBatches.FindAsync(d.BatchId);
                                if (batch != null)
                                {
                                    batch.CurrentQuantity += d.Quantity;
                                }
                            }
                        }

                        // Remove child sale details
                        _db.SaleDetails.RemoveRange(sale.Details);
                        await _db.SaveChangesAsync();
                    }

                    int totalItems = sale.Details?.Sum(d => d.Quantity) ?? 0;

                    // 2. Remove linked account transactions (Legacy SALE)
                    var legacyTxs = await _db.AccountTransactions
                        .Where(t => t.ReferenceType == "SALE" && (t.ReferenceId == id.ToString() || t.ReferenceId == sale.SaleNumber))
                        .ToListAsync();
                    if (legacyTxs.Any())
                    {
                        _db.AccountTransactions.RemoveRange(legacyTxs);
                    }

                    // 3. Remove SalePayments and their linked AccountTransactions
                    var payments = await _db.SalePayments.Where(p => p.SaleId == id).ToListAsync();
                    if (payments.Any())
                    {
                        var paymentIds = payments.Select(p => p.PaymentId.ToString()).ToList();
                        var paymentTxs = await _db.AccountTransactions
                            .Where(t => (t.ReferenceType == "SALE_PAYMENT" || t.ReferenceType == "SALE_REFUND") && paymentIds.Contains(t.ReferenceId))
                            .ToListAsync();

                        if (paymentTxs.Any())
                        {
                            _db.AccountTransactions.RemoveRange(paymentTxs);
                        }

                        _db.SalePayments.RemoveRange(payments);
                    }

                    // 4. Remove parent sale record
                    _db.Sales.Remove(sale);
                    await _db.SaveChangesAsync();

                    await transaction.CommitAsync();

                    return new
                    {
                        sale.SaleId,
                        sale.SaleNumber,
                        CustomerName = sale.Customer?.CustomerName ?? $"Customer ID {sale.CustomerId}",
                        sale.TotalAmount,
                        sale.PaidAmount,
                        sale.Status,
                        sale.SaleDate,
                        TotalItems = totalItems
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
