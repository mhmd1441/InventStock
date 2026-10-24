using InventStock.Domain.Common;
using InventStock.Domain.Exceptions;

namespace InventStock.Domain.Entities;

/// <summary>
/// Evidence that a sale or customer payment was first written on paper and entered later.
/// The unique paper reference prevents the same handwritten event from being posted twice.
/// </summary>
public sealed class PaperRecoveryEntry : AuditableEntity
{
    public const int PaperReferenceMaxLength = 100;
    public const int ReasonMaxLength = 500;
    public const int OriginalSellerNoteMaxLength = 500;

    private PaperRecoveryEntry() { }

    public string PaperReference { get; private set; } = null!;
    public DateTime OriginalOccurredAtUtc { get; private set; }
    public DateTime EnteredAtUtc { get; private set; }
    public Guid EnteredByUserId { get; private set; }
    public string Reason { get; private set; } = null!;
    public string? OriginalSellerNote { get; private set; }
    public Guid? SaleId { get; private set; }
    public Guid? CustomerPaymentId { get; private set; }

    public static PaperRecoveryEntry ForSale(string paperReference, DateTime originalOccurredAtUtc,
        string reason, string? originalSellerNote, Guid saleId, Guid actorId, DateTime enteredAtUtc) =>
        Create(paperReference, originalOccurredAtUtc, reason, originalSellerNote, saleId, null, actorId, enteredAtUtc);

    public static PaperRecoveryEntry ForCustomerPayment(string paperReference, DateTime originalOccurredAtUtc,
        string reason, string? originalSellerNote, Guid customerPaymentId, Guid actorId, DateTime enteredAtUtc) =>
        Create(paperReference, originalOccurredAtUtc, reason, originalSellerNote, null, customerPaymentId, actorId, enteredAtUtc);

    private static PaperRecoveryEntry Create(string paperReference, DateTime originalOccurredAtUtc, string reason,
        string? originalSellerNote, Guid? saleId, Guid? customerPaymentId, Guid actorId, DateTime enteredAtUtc)
    {
        if (originalOccurredAtUtc.Kind != DateTimeKind.Utc || originalOccurredAtUtc > enteredAtUtc)
            throw new DomainException("The original paper time must be UTC and cannot be later than entry time.");
        return new PaperRecoveryEntry
        {
            PaperReference = Guard.AgainstNullOrWhiteSpace(paperReference, nameof(paperReference), PaperReferenceMaxLength),
            OriginalOccurredAtUtc = originalOccurredAtUtc,
            EnteredAtUtc = enteredAtUtc,
            EnteredByUserId = actorId,
            Reason = Guard.AgainstNullOrWhiteSpace(reason, nameof(reason), ReasonMaxLength),
            OriginalSellerNote = string.IsNullOrWhiteSpace(originalSellerNote) ? null : originalSellerNote.Trim(),
            SaleId = saleId,
            CustomerPaymentId = customerPaymentId
        };
    }
}
