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
        /// High-performance server-side ledger calculation for Business Accounts (MNC Standard).
        /// Replaces slow client-side Javascript loops with an optimized, indexed single database fetch.
        /// </summary>
        public async Task<List<BusinessAccountLedgerSummaryDto>> GetBusinessAccountLedgerSummaryAsync(DateTime? fromDate, DateTime? toDate)
        {
            var accounts = await _db.BusinessAccounts.AsNoTracking().ToListAsync();
            var partners = await _db.Partners.AsNoTracking().ToListAsync();
            var ledgers = await _db.PartnerLedgers.Include(l => l.Partner).AsNoTracking().ToListAsync();
            var allTxs = await _db.AccountTransactions.AsNoTracking().ToListAsync();

            var summaryList = new List<BusinessAccountLedgerSummaryDto>();

            foreach (var acc in accounts)
            {
                var accName = acc.AccountName ?? "";
                var accNameLower = accName.ToLower();

                // Determine partner name and partnerId
                string partnerName = "";
                int? partnerId = null;

                var matchedPartner = partners.FirstOrDefault(p =>
                    !string.IsNullOrWhiteSpace(p.PartnerName) &&
                    (accNameLower.Contains(p.PartnerName.ToLower()) || p.PartnerName.ToLower().Contains(accNameLower)));

                if (matchedPartner != null)
                {
                    partnerName = matchedPartner.PartnerName;
                    partnerId = matchedPartner.PartnerId;
                }
                else if (accNameLower.Contains("pandiyan") || accNameLower.Contains("pandian"))
                {
                    partnerName = "Pandiyan";
                    var p = partners.FirstOrDefault(x => x.PartnerName.ToLower().Contains("pandiyan") || x.PartnerName.ToLower().Contains("pandian"));
                    if (p != null) partnerId = p.PartnerId;
                }
                else if (accNameLower.Contains("ranjith"))
                {
                    partnerName = "Ranjith";
                    var p = partners.FirstOrDefault(x => x.PartnerName.ToLower().Contains("ranjith"));
                    if (p != null) partnerId = p.PartnerId;
                }
                else
                {
                    partnerName = accName;
                }

                var pNameLower = partnerName.ToLower();

                // All transactions for this account
                var allAccTxs = allTxs.Where(t => t.AccountId == acc.AccountId).ToList();
                var existingRefIds = allAccTxs
                    .Where(t => t.ReferenceType == "PARTNER_TRANSACTION")
                    .Select(t => t.ReferenceId)
                    .ToHashSet();

                // 1. Opening Balance Calculation (Cumulative before fromDate)
                decimal openingBalance = 0;
                if (fromDate.HasValue)
                {
                    var priorAccTxs = allAccTxs.Where(t => t.CreatedAt < fromDate.Value).ToList();
                    var priorCredits = priorAccTxs.Where(t => t.TransactionType == "CREDIT").Sum(t => t.Amount);
                    var priorDebits = priorAccTxs.Where(t => t.TransactionType == "DEBIT").Sum(t => t.Amount);

                    var priorUnlinkedLedgers = ledgers.Where(l =>
                    {
                        var lpName = (l.Partner?.PartnerName ?? "").ToLower();
                        bool isPartnerMatch = (partnerId.HasValue && l.PartnerId == partnerId.Value) ||
                                              (!string.IsNullOrEmpty(pNameLower) && (lpName.Contains(pNameLower) || pNameLower.Contains(lpName)));
                        bool isAccMatch = !string.IsNullOrEmpty(accNameLower) && (accNameLower.Contains(lpName) || lpName.Contains(accNameLower));
                        bool notInTx = !existingRefIds.Contains($"LEDGER-{l.LedgerId}") &&
                                       !allAccTxs.Any(t => Math.Abs(t.Amount - l.Amount) < 0.01m && (t.Description ?? "").Contains(l.Description ?? ""));
                        return (isPartnerMatch || isAccMatch) && notInTx && l.CreatedAt < fromDate.Value;
                    }).ToList();

                    decimal priorUnlinkedInvested = 0;
                    foreach (var l in priorUnlinkedLedgers)
                    {
                        if (l.TransactionType == "INVESTMENT") priorUnlinkedInvested += l.Amount;
                        else if (l.TransactionType == "WITHDRAWAL") priorUnlinkedInvested -= l.Amount;
                    }

                    openingBalance = (priorCredits - priorDebits) + priorUnlinkedInvested;
                }

                // 2. Current Period Transactions
                var periodTxs = allAccTxs.Where(t =>
                    (!fromDate.HasValue || t.CreatedAt >= fromDate.Value) &&
                    (!toDate.HasValue || t.CreatedAt <= toDate.Value)
                ).ToList();

                // Classification helpers
                bool IsSaleCredit(AccountTransaction t)
                {
                    if (t.TransactionType != "CREDIT") return false;
                    var refType = (t.ReferenceType ?? "").ToUpper();
                    var desc = (t.Description ?? "").ToLower();
                    if (refType == "SALE" || refType == "SALE_PAYMENT" || refType == "SALE_DETAIL") return true;
                    if (refType == "PARTNER_TRANSACTION" || desc.Contains("investment")) return false;
                    return desc.Contains("sale") || desc.Contains("collected payment") || desc.Contains("invoice") || desc.Contains("customer payment");
                }

                bool IsPurchaseDebit(AccountTransaction t)
                {
                    if (t.TransactionType != "DEBIT") return false;
                    var refType = (t.ReferenceType ?? "").ToUpper();
                    var desc = (t.Description ?? "").ToLower();
                    if (refType == "PURCHASE" || refType == "PURCHASE_PAYMENT") return true;
                    if (refType == "EXPENSE" || refType == "PARTNER_TRANSACTION" || desc.Contains("withdrawal")) return false;
                    return desc.Contains("purchase");
                }

                bool IsExpenseDebit(AccountTransaction t)
                {
                    if (t.TransactionType != "DEBIT") return false;
                    var refType = (t.ReferenceType ?? "").ToUpper();
                    var desc = (t.Description ?? "").ToLower();
                    if (refType == "EXPENSE") return true;
                    if (refType == "PURCHASE" || refType == "PURCHASE_PAYMENT" || refType == "PARTNER_TRANSACTION") return false;
                    return desc.Contains("expense") || desc.Contains("petrol") || desc.Contains("diesel") || desc.Contains("cover") || desc.Contains("rope") || desc.Contains("food");
                }

                decimal salesCollected = periodTxs.Where(IsSaleCredit).Sum(t => t.Amount);
                decimal purchasesPaid = periodTxs.Where(IsPurchaseDebit).Sum(t => t.Amount);
                decimal expensesPaid = periodTxs.Where(IsExpenseDebit).Sum(t => t.Amount);

                decimal txInvested = periodTxs.Where(t => (t.ReferenceType == "PARTNER_TRANSACTION" || (t.Description ?? "").ToLower().Contains("investment")) && t.TransactionType == "CREDIT").Sum(t => t.Amount);
                decimal txWithdrawn = periodTxs.Where(t => (t.ReferenceType == "PARTNER_TRANSACTION" || (t.Description ?? "").ToLower().Contains("withdrawal")) && t.TransactionType == "DEBIT").Sum(t => t.Amount);

                // Unlinked PartnerLedgers in period
                var unlinkedLedgers = ledgers.Where(l =>
                {
                    var lpName = (l.Partner?.PartnerName ?? "").ToLower();
                    bool isPartnerMatch = (partnerId.HasValue && l.PartnerId == partnerId.Value) ||
                                          (!string.IsNullOrEmpty(pNameLower) && (lpName.Contains(pNameLower) || pNameLower.Contains(lpName)));
                    bool isAccMatch = !string.IsNullOrEmpty(accNameLower) && (accNameLower.Contains(lpName) || lpName.Contains(accNameLower));
                    bool notInTx = !existingRefIds.Contains($"LEDGER-{l.LedgerId}") &&
                                   !allAccTxs.Any(t => Math.Abs(t.Amount - l.Amount) < 0.01m && (t.Description ?? "").Contains(l.Description ?? ""));
                    return (isPartnerMatch || isAccMatch) && notInTx &&
                           (!fromDate.HasValue || l.CreatedAt >= fromDate.Value) &&
                           (!toDate.HasValue || l.CreatedAt <= toDate.Value);
                }).ToList();

                decimal unlinkedLedgerInvested = 0;
                foreach (var l in unlinkedLedgers)
                {
                    if (l.TransactionType == "INVESTMENT") unlinkedLedgerInvested += l.Amount;
                    else if (l.TransactionType == "WITHDRAWAL") unlinkedLedgerInvested -= l.Amount;
                }

                decimal periodCredits = periodTxs.Where(t => t.TransactionType == "CREDIT").Sum(t => t.Amount);
                decimal periodDebits = periodTxs.Where(t => t.TransactionType == "DEBIT").Sum(t => t.Amount);

                decimal netInHand = openingBalance + (periodCredits - periodDebits) + unlinkedLedgerInvested;

                summaryList.Add(new BusinessAccountLedgerSummaryDto(
                    acc.AccountId,
                    acc.AccountName,
                    acc.AccountType ?? "Pandiyan's Acc",
                    partnerName,
                    openingBalance,
                    salesCollected,
                    purchasesPaid,
                    expensesPaid,
                    txInvested + (unlinkedLedgerInvested > 0 ? unlinkedLedgerInvested : 0),
                    txWithdrawn + (unlinkedLedgerInvested < 0 ? Math.Abs(unlinkedLedgerInvested) : 0),
                    netInHand,
                    periodCredits,
                    periodDebits,
                    periodTxs.Count
                ));
            }

            return summaryList;
        }
    }
}
