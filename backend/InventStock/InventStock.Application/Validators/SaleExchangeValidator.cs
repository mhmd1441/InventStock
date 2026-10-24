using FluentValidation;
using InventStock.Application.DTOs.Sales;
using InventStock.Domain.Entities;

namespace InventStock.Application.Validators;

public sealed class PostSaleExchangeRequestValidator : AbstractValidator<PostSaleExchangeRequest>
{
    public PostSaleExchangeRequestValidator()
    {
        RuleFor(x => x.OperationId).NotEmpty();
        RuleFor(x => x.OriginalSaleId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ConfirmedReturnTotalUsd).InclusiveBetween(0.01m, 999999999m).PrecisionScale(18, 2, true);
        RuleFor(x => x.ReturnAppliedUsd).InclusiveBetween(0, 999999999m).PrecisionScale(18, 2, true);
        RuleFor(x => x.AccountReductionUsd).InclusiveBetween(0, 999999999m).PrecisionScale(18, 2, true);
        RuleFor(x => x.ConfirmedReplacementTotalUsd).InclusiveBetween(0.01m, 999999999m).PrecisionScale(18, 2, true);
        RuleFor(x => x.CustomerCreditUsedUsd).InclusiveBetween(0, 999999999m).PrecisionScale(18, 2, true);
        RuleFor(x => x.UnpaidDebtUsd).InclusiveBetween(0, 999999999m).PrecisionScale(18, 2, true);

        RuleFor(x => x.ReturnLines).NotNull().Must(x => x is { Count: > 0 and <= 100 });
        RuleFor(x => x.ReturnLines).Must(x => x is null || x.Select(y => y.SaleLineId).Distinct().Count() == x.Count)
            .WithMessage("A sale line may appear only once in an exchange return.");
        RuleForEach(x => x.ReturnLines).NotNull().ChildRules(line =>
        {
            line.RuleFor(x => x.SaleLineId).NotEmpty();
            line.RuleFor(x => x.Quantity).InclusiveBetween(1, 1000000);
            line.RuleFor(x => x.Condition).IsInEnum();
            line.RuleFor(x => x.IssueReason).MaximumLength(500);
            line.RuleFor(x => x).Must(x => x.Condition != ReturnCondition.Quarantined ||
                (x.PhysicalSeparationConfirmed && !string.IsNullOrWhiteSpace(x.IssueReason)))
                .WithMessage("Faulty returned goods must be physically separated or tagged, with a reason.");
        });

        RuleFor(x => x.ReplacementLines).NotNull().Must(x => x is { Count: > 0 and <= 100 });
        RuleForEach(x => x.ReplacementLines).NotNull().ChildRules(line =>
        {
            line.RuleFor(x => x.PartId).NotEmpty();
            line.RuleFor(x => x.Quantity).InclusiveBetween(1, 1000000);
            line.RuleFor(x => x.UnitSellingPriceUsd).InclusiveBetween(0.000001m, 999999m).PrecisionScale(18, 6, true);
            line.RuleFor(x => x.StockExceptionReason).MaximumLength(500);
            line.RuleFor(x => x).Must(x => !x.PhysicalGoodsConfirmed || !string.IsNullOrWhiteSpace(x.StockExceptionReason))
                .WithMessage("Physical confirmation requires a stock exception reason.");
        });

        RuleFor(x => x.Refunds).NotNull().Must(x => x is { Count: <= 20 });
        RuleFor(x => x.Payments).NotNull().Must(x => x is { Count: <= 20 });
        RuleFor(x => x.Change).NotNull().Must(x => x is { Count: <= 20 });
        RuleForEach(x => x.Refunds).NotNull().SetValidator(new SaleTenderValidator());
        RuleForEach(x => x.Payments).NotNull().SetValidator(new SaleTenderValidator());
        RuleForEach(x => x.Change).NotNull().SetValidator(new SaleTenderValidator());
    }
}
