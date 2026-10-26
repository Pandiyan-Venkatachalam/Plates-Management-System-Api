using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using VinayagaPlates.Domain.Entities;

namespace VinayagaPlates.Application.Services
{
    public static class PurchaseReconciliationService
    {
        public static async Task ReconcilePurchasesAndAccountsAsync(ApplicationDbContext db)
        {
            var allAccounts = await db.BusinessAccounts.ToListAsync();
            if (!allAccounts.Any()) return;

            var pandiyanAcc = allAccounts.FirstOrDefault(a => 
                a.AccountName.ToLower().Contains("pandiyan") || 
                a.AccountName.ToLower().Contains("pandian") || 
                (a.AccountType != null && (a.AccountType.ToLower().Contains("pandiyan") || a.AccountType.ToLower().Contains("pandian"))));
            
            var ranjithAcc = allAccounts.FirstOrDefault(a => 
                a.AccountName.ToLower().Contains("ranjith") || 
                (a.AccountType != null && a.AccountType.ToLower().Contains("ranjith")));

            var defaultAcc = pandiyanAcc ?? allAccounts.FirstOrDefault();

            var allPurchases = await db.Purchases.ToListAsync();
            if (!allPurchases.Any()) return;

            var allPayments = await db.PurchasePayments.ToListAsync();
            var allTxs = await db.AccountTransactions
                .Where(t => t.ReferenceType == "PURCHASE" || t.ReferenceType == "PURCHASE_PAYMENT")
                .ToListAsync();

            bool changed = false;

            foreach (var p in allPurchases.Where(p => p.PaidAmount > 0))
            {
                var pidStr = p.PurchaseId.ToString();
                var pNum = p.PurchaseNumber ?? "";

                var pTxs = allTxs.Where(t => t.ReferenceId == pidStr || (!string.IsNullOrEmpty(pNum) && t.ReferenceId == pNum)).ToList();
                var pPays = allPayments.Where(pp => pp.PurchaseId == p.PurchaseId).ToList();

                bool isPur16 = p.PurchaseNumber == "PUR-16" || p.PurchaseId == 16 || Math.Abs(p.TotalAmount - 79990m) < 0.01m;
                bool isPur14 = p.PurchaseNumber == "PUR-14" || p.PurchaseId == 14 || Math.Abs(p.TotalAmount - 410m) < 0.01m;
                bool isPur10 = p.PurchaseNumber == "PUR-10" || p.PurchaseId == 10 || Math.Abs(p.TotalAmount - 1150m) < 0.01m;
                bool isPur11 = p.PurchaseNumber == "PUR-11" || p.PurchaseId == 11 || Math.Abs(p.TotalAmount - 3200m) < 0.01m;
                bool isPur8 = p.PurchaseNumber == "PUR-30138631" || p.PurchaseId == 8 || Math.Abs(p.TotalAmount - 42640m) < 0.01m;

                // ----------------------------------------------------
                // 1. PUR-14: Explicitly ensure ₹410 debit is on Pandiyan's hand
                // ----------------------------------------------------
                if (isPur14 && pandiyanAcc != null)
                {
                    // If any transaction is on Ranjith or another account, switch to Pandiyan
                    foreach (var tx in pTxs)
                    {
                        if (tx.AccountId != pandiyanAcc.AccountId)
                        {
                            tx.AccountId = pandiyanAcc.AccountId;
                            tx.Description = $"Paid ₹{tx.Amount:N2} for Purchase {p.PurchaseNumber} (Pandiyan)";
                            changed = true;
                        }
                    }

                    // If no transaction exists at all for PUR-14, create it on Pandiyan
                    if (!pTxs.Any())
                    {
                        var newTx = new AccountTransaction
                        {
                            AccountId = pandiyanAcc.AccountId,
                            TransactionType = "DEBIT",
                            Amount = p.PaidAmount > 0 ? p.PaidAmount : 410m,
                            ReferenceType = "PURCHASE",
                            ReferenceId = pidStr,
                            Description = $"Paid ₹{p.PaidAmount:N2} for Purchase {p.PurchaseNumber}",
                            CreatedBy = "Pandiyan",
                            CreatedAt = p.PurchaseDate != default ? p.PurchaseDate : p.CreatedAt
                        };
                        db.AccountTransactions.Add(newTx);
                        allTxs.Add(newTx);
                        pTxs.Add(newTx);
                        changed = true;
                    }

                    // Ensure PurchasePayments is also on Pandiyan
                    foreach (var pay in pPays)
                    {
                        if (pay.AccountId != pandiyanAcc.AccountId)
                        {
                            pay.AccountId = pandiyanAcc.AccountId;
                            changed = true;
                        }
                    }
                    if (!pPays.Any())
                    {
                        var newPay = new PurchasePayment
                        {
                            PurchaseId = p.PurchaseId,
                            Amount = p.PaidAmount > 0 ? p.PaidAmount : 410m,
                            AccountId = pandiyanAcc.AccountId,
                            PaymentMethod = "CASH",
                            Notes = "Initial payment upon purchase creation",
                            CreatedBy = "Pandiyan",
                            CreatedAt = p.PurchaseDate != default ? p.PurchaseDate : p.CreatedAt
                        };
                        db.PurchasePayments.Add(newPay);
                        allPayments.Add(newPay);
                        pPays.Add(newPay);
                        changed = true;
                    }
                }
                // ----------------------------------------------------
                // 2. PUR-8, PUR-10 & PUR-11: Explicitly paid by Ranjith
                // ----------------------------------------------------
                else if ((isPur10 || isPur11 || isPur8) && ranjithAcc != null)
                {
                    foreach (var tx in pTxs)
                    {
                        if (tx.AccountId != ranjithAcc.AccountId)
                        {
                            tx.AccountId = ranjithAcc.AccountId;
                            tx.Description = $"Paid ₹{tx.Amount:N2} for Purchase {p.PurchaseNumber} (Ranjith)";
                            changed = true;
                        }
                    }

                    if (!pTxs.Any())
                    {
                        var newTx = new AccountTransaction
                        {
                            AccountId = ranjithAcc.AccountId,
                            TransactionType = "DEBIT",
                            Amount = p.PaidAmount > 0 ? p.PaidAmount : p.TotalAmount,
                            ReferenceType = "PURCHASE",
                            ReferenceId = pidStr,
                            Description = $"Paid ₹{p.PaidAmount:N2} for Purchase {p.PurchaseNumber} (Ranjith)",
                            CreatedBy = "Ranjith",
                            CreatedAt = p.PurchaseDate != default ? p.PurchaseDate : p.CreatedAt
                        };
                        db.AccountTransactions.Add(newTx);
                        allTxs.Add(newTx);
                        pTxs.Add(newTx);
                        changed = true;
                    }

                    foreach (var pay in pPays)
                    {
                        if (pay.AccountId != ranjithAcc.AccountId)
                        {
                            pay.AccountId = ranjithAcc.AccountId;
                            changed = true;
                        }
                    }
                    if (!pPays.Any())
                    {
                        var newPay = new PurchasePayment
                        {
                            PurchaseId = p.PurchaseId,
                            Amount = p.PaidAmount > 0 ? p.PaidAmount : p.TotalAmount,
                            AccountId = ranjithAcc.AccountId,
                            PaymentMethod = "CASH",
                            Notes = "Initial payment upon purchase creation (Ranjith)",
                            CreatedBy = "Ranjith",
                            CreatedAt = p.PurchaseDate != default ? p.PurchaseDate : p.CreatedAt
                        };
                        db.PurchasePayments.Add(newPay);
                        allPayments.Add(newPay);
                        pPays.Add(newPay);
                        changed = true;
                    }
                }
                // ----------------------------------------------------
                // 2. PUR-16: Split 2 persons (Pandiyan & Ranjith)
                // ----------------------------------------------------
                else if (isPur16 && pandiyanAcc != null && ranjithAcc != null)
                {
                    decimal pandiyanSplit = 44000m;
                    decimal ranjithSplit = 35990m;

                    // Ensure AccountTransactions has 2 debits: Pandiyan ₹44,000 and Ranjith ₹35,990
                    var pandiyanTx = pTxs.FirstOrDefault(t => t.AccountId == pandiyanAcc.AccountId);
                    var ranjithTx = pTxs.FirstOrDefault(t => t.AccountId == ranjithAcc.AccountId);

                    if (pandiyanTx == null)
                    {
                        pandiyanTx = new AccountTransaction
                        {
                            AccountId = pandiyanAcc.AccountId,
                            TransactionType = "DEBIT",
                            Amount = pandiyanSplit,
                            ReferenceType = "PURCHASE",
                            ReferenceId = pidStr,
                            Description = $"Paid ₹{pandiyanSplit:N2} for Purchase {p.PurchaseNumber} (Split: Pandiyan)",
                            CreatedBy = "Pandiyan",
                            CreatedAt = p.PurchaseDate != default ? p.PurchaseDate : p.CreatedAt
                        };
                        db.AccountTransactions.Add(pandiyanTx);
                        allTxs.Add(pandiyanTx);
                        pTxs.Add(pandiyanTx);
                        changed = true;
                    }
                    else if (Math.Abs(pandiyanTx.Amount - pandiyanSplit) > 0.01m)
                    {
                        pandiyanTx.Amount = pandiyanSplit;
                        pandiyanTx.Description = $"Paid ₹{pandiyanSplit:N2} for Purchase {p.PurchaseNumber} (Split: Pandiyan)";
                        changed = true;
                    }

                    if (ranjithTx == null)
                    {
                        ranjithTx = new AccountTransaction
                        {
                            AccountId = ranjithAcc.AccountId,
                            TransactionType = "DEBIT",
                            Amount = ranjithSplit,
                            ReferenceType = "PURCHASE",
                            ReferenceId = pidStr,
                            Description = $"Paid ₹{ranjithSplit:N2} for Purchase {p.PurchaseNumber} (Split: Ranjith)",
                            CreatedBy = "Pandiyan",
                            CreatedAt = p.PurchaseDate != default ? p.PurchaseDate : p.CreatedAt
                        };
                        db.AccountTransactions.Add(ranjithTx);
                        allTxs.Add(ranjithTx);
                        pTxs.Add(ranjithTx);
                        changed = true;
                    }
                    else if (Math.Abs(ranjithTx.Amount - ranjithSplit) > 0.01m)
                    {
                        ranjithTx.Amount = ranjithSplit;
                        ranjithTx.Description = $"Paid ₹{ranjithSplit:N2} for Purchase {p.PurchaseNumber} (Split: Ranjith)";
                        changed = true;
                    }

                    // Remove any old transactions for PUR-16 with the full amount or non-matching
                    var otherTxs = pTxs.Where(t => t != pandiyanTx && t != ranjithTx).ToList();
                    if (otherTxs.Any())
                    {
                        db.AccountTransactions.RemoveRange(otherTxs);
                        foreach (var ot in otherTxs) allTxs.Remove(ot);
                        changed = true;
                    }

                    // Reconcile PurchasePayments for PUR-16: MUST have 2 payments (Pandiyan ₹44,000 & Ranjith ₹35,990)
                    var pandiyanPay = pPays.FirstOrDefault(pp => pp.AccountId == pandiyanAcc.AccountId);
                    var ranjithPay = pPays.FirstOrDefault(pp => pp.AccountId == ranjithAcc.AccountId);

                    if (pandiyanPay == null)
                    {
                        pandiyanPay = new PurchasePayment
                        {
                            PurchaseId = p.PurchaseId,
                            Amount = pandiyanSplit,
                            AccountId = pandiyanAcc.AccountId,
                            PaymentMethod = "CASH",
                            Notes = "Split initial payment upon purchase creation (Pandiyan)",
                            CreatedBy = "Pandiyan",
                            CreatedAt = p.PurchaseDate != default ? p.PurchaseDate : p.CreatedAt
                        };
                        db.PurchasePayments.Add(pandiyanPay);
                        allPayments.Add(pandiyanPay);
                        pPays.Add(pandiyanPay);
                        changed = true;
                    }
                    else if (Math.Abs(pandiyanPay.Amount - pandiyanSplit) > 0.01m)
                    {
                        pandiyanPay.Amount = pandiyanSplit;
                        changed = true;
                    }

                    if (ranjithPay == null)
                    {
                        ranjithPay = new PurchasePayment
                        {
                            PurchaseId = p.PurchaseId,
                            Amount = ranjithSplit,
                            AccountId = ranjithAcc.AccountId,
                            PaymentMethod = "CASH",
                            Notes = "Split initial payment upon purchase creation (Ranjith)",
                            CreatedBy = "Pandiyan",
                            CreatedAt = p.PurchaseDate != default ? p.PurchaseDate : p.CreatedAt
                        };
                        db.PurchasePayments.Add(ranjithPay);
                        allPayments.Add(ranjithPay);
                        pPays.Add(ranjithPay);
                        changed = true;
                    }
                    else if (Math.Abs(ranjithPay.Amount - ranjithSplit) > 0.01m)
                    {
                        ranjithPay.Amount = ranjithSplit;
                        changed = true;
                    }

                    // Remove old single payment with full amount
                    var otherPays = pPays.Where(pp => pp != pandiyanPay && pp != ranjithPay).ToList();
                    if (otherPays.Any())
                    {
                        db.PurchasePayments.RemoveRange(otherPays);
                        foreach (var op in otherPays) allPayments.Remove(op);
                        changed = true;
                    }
                }
                // ----------------------------------------------------
                // 3. Other Purchases (Split or Single)
                // ----------------------------------------------------
                else
                {
                    var distinctTxAccs = pTxs.Select(t => t.AccountId).Distinct().ToList();
                    if (distinctTxAccs.Count > 1)
                    {
                        // Multiple split transactions exist in AccountTransactions -> mirror each to PurchasePayment
                        var existingPayAccs = pPays.Select(pp => pp.AccountId).Distinct().ToHashSet();
                        foreach (var tx in pTxs)
                        {
                            if (!existingPayAccs.Contains(tx.AccountId))
                            {
                                var newPay = new PurchasePayment
                                {
                                    PurchaseId = p.PurchaseId,
                                    Amount = tx.Amount,
                                    AccountId = tx.AccountId,
                                    PaymentMethod = "CASH",
                                    Notes = "Split initial payment upon purchase creation",
                                    CreatedBy = tx.CreatedBy ?? p.CreatedBy ?? "SYSTEM",
                                    CreatedAt = tx.CreatedAt != default ? tx.CreatedAt : p.CreatedAt
                                };
                                db.PurchasePayments.Add(newPay);
                                allPayments.Add(newPay);
                                changed = true;
                            }
                        }
                    }
                    else if (distinctTxAccs.Count == 1 && !pPays.Any())
                    {
                        var tx = pTxs.First();
                        var newPay = new PurchasePayment
                        {
                            PurchaseId = p.PurchaseId,
                            Amount = p.PaidAmount,
                            AccountId = tx.AccountId,
                            PaymentMethod = "CASH",
                            Notes = "Initial payment upon purchase creation",
                            CreatedBy = tx.CreatedBy ?? p.CreatedBy ?? "SYSTEM",
                            CreatedAt = tx.CreatedAt != default ? tx.CreatedAt : p.CreatedAt
                        };
                        db.PurchasePayments.Add(newPay);
                        allPayments.Add(newPay);
                        changed = true;
                    }
                    else if (!pTxs.Any() && pPays.Any())
                    {
                        foreach (var pay in pPays)
                        {
                            var newTx = new AccountTransaction
                            {
                                AccountId = pay.AccountId,
                                TransactionType = "DEBIT",
                                Amount = pay.Amount,
                                ReferenceType = "PURCHASE",
                                ReferenceId = pidStr,
                                Description = $"Paid ₹{pay.Amount:N2} for Purchase {p.PurchaseNumber}",
                                CreatedBy = pay.CreatedBy ?? p.CreatedBy ?? "SYSTEM",
                                CreatedAt = pay.CreatedAt != default ? pay.CreatedAt : p.CreatedAt
                            };
                            db.AccountTransactions.Add(newTx);
                            allTxs.Add(newTx);
                            changed = true;
                        }
                    }
                    else if (!pTxs.Any() && !pPays.Any())
                    {
                        var targetAcc = (!string.IsNullOrWhiteSpace(p.CreatedBy) 
                            ? allAccounts.FirstOrDefault(a => 
                                a.AccountName.ToLower().Contains(p.CreatedBy.ToLower()) || 
                                (a.AccountType != null && a.AccountType.ToLower().Contains(p.CreatedBy.ToLower())))
                            : null) ?? defaultAcc;

                        if (targetAcc != null)
                        {
                            var newPay = new PurchasePayment
                            {
                                PurchaseId = p.PurchaseId,
                                Amount = p.PaidAmount,
                                AccountId = targetAcc.AccountId,
                                PaymentMethod = "CASH",
                                Notes = "Initial payment upon purchase creation",
                                CreatedBy = p.CreatedBy ?? "SYSTEM",
                                CreatedAt = p.PurchaseDate != default ? p.PurchaseDate : p.CreatedAt
                            };
                            db.PurchasePayments.Add(newPay);
                            allPayments.Add(newPay);

                            var newTx = new AccountTransaction
                            {
                                AccountId = targetAcc.AccountId,
                                TransactionType = "DEBIT",
                                Amount = p.PaidAmount,
                                ReferenceType = "PURCHASE",
                                ReferenceId = pidStr,
                                Description = $"Paid ₹{p.PaidAmount:N2} for Purchase {p.PurchaseNumber}",
                                CreatedBy = p.CreatedBy ?? "SYSTEM",
                                CreatedAt = p.PurchaseDate != default ? p.PurchaseDate : p.CreatedAt
                            };
                            db.AccountTransactions.Add(newTx);
                            allTxs.Add(newTx);
                            changed = true;
                        }
                    }
                }
            }

            if (changed)
            {
                await db.SaveChangesAsync();
            }
        }
    }
}
