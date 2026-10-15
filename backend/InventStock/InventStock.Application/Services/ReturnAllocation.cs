namespace InventStock.Application.Services;

/// <summary>
/// Allocates the immutable, already-rounded sale-line total across cumulative returned base units.
/// Repeated partial returns must add up to exactly the posted amount, never a freshly rounded unit price.
/// The caller must lock the original sale line and read prior returns in the same database transaction.
/// </summary>
public static class ReturnAllocation
{
    public static decimal AdditionalRefund(decimal postedLineTotalUsd, int soldQuantity,
        int previouslyReturnedQuantity, decimal previouslyRefundedUsd, int requestedQuantity)
    {
        if (soldQuantity <= 0 || requestedQuantity <= 0 || previouslyReturnedQuantity < 0 ||
            previouslyReturnedQuantity > soldQuantity || requestedQuantity > soldQuantity - previouslyReturnedQuantity)
            throw new ArgumentOutOfRangeException(nameof(requestedQuantity), "Return quantity must fit within the original sale line.");
        if (postedLineTotalUsd < 0 || decimal.Round(postedLineTotalUsd, 2) != postedLineTotalUsd ||
            previouslyRefundedUsd < 0 || previouslyRefundedUsd > postedLineTotalUsd ||
            decimal.Round(previouslyRefundedUsd, 2) != previouslyRefundedUsd)
            throw new ArgumentOutOfRangeException(nameof(previouslyRefundedUsd), "Refund amounts must be valid posted USD cents.");

        var cumulativeQuantity = checked(previouslyReturnedQuantity + requestedQuantity);
        var cumulativeRefund = decimal.Round(postedLineTotalUsd * cumulativeQuantity / soldQuantity,
            2, MidpointRounding.AwayFromZero);
        var additional = cumulativeRefund - previouslyRefundedUsd;
        if (additional < 0 || additional > postedLineTotalUsd - previouslyRefundedUsd)
            throw new InvalidOperationException("Prior refunds do not reconcile with the original posted line.");
        return additional;
    }
}
