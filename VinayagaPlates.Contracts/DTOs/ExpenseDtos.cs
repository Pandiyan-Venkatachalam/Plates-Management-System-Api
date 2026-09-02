using System.Collections.Generic;

namespace VinayagaPlates.Contracts.DTOs
{
    // --- Expense (Outflow) ---
    public record ExpenseContributionRequest(int AccountId, decimal Amount);

    public record ExpenseCreateRequest(
        string Description,
        decimal Amount,
        int AccountId,
        List<ExpenseContributionRequest>? Contributions = null);

    // --- Business Account CRUD ---
    public record AccountCreateRequest(
        int Id,
        string AccountName,
        string AccountType);           // e.g. CASH, BANK

    public record AccountUpdateRequest(
        string AccountName,
        string AccountType);
}
