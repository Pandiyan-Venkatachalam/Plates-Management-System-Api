using System;

namespace VinayagaPlates.Contracts.DTOs
{
    public record BusinessAccountLedgerSummaryDto(
        int AccountId,
        string AccountName,
        string AccountType,
        string PartnerName,
        decimal OpeningBalance,
        decimal SalesCollected,
        decimal PurchasesPaid,
        decimal ExpensesPaid,
        decimal Invested,
        decimal Withdrawn,
        decimal NetInHand,
        decimal PeriodCredits,
        decimal PeriodDebits,
        int TransactionCount
    );
}
