namespace VinayagaPlates.Contracts.DTOs
{
    public record PartnerTransactionRequest(
        int PartnerId,
        string TransactionType,
        decimal Amount,
        string Description,
        string AccountName);

    public record PartnerLedgerCreateRequest(
        int PartnerId,
        string TransactionType,
        decimal Amount,
        string Description);

    public record PartnerLedgerUpdateRequest(
        int PartnerId,
        string TransactionType,
        decimal Amount,
        string Description);

    public record PartnerLedgerResponse(
        int LedgerId,
        int PartnerId,
        string PartnerName,
        string TransactionType,
        decimal Amount,
        string Description,
        DateTime CreatedAt);
}
