using FluentValidation;
using InventStock.Application.DTOs.Sales;
using InventStock.Domain.Entities;

namespace InventStock.Application.Validators;

public sealed class PostLegacySaleReturnRequestValidator : AbstractValidator<PostLegacySaleReturnRequest>
{
    public PostLegacySaleReturnRequestValidator()
    {
        RuleFor(x => x.OperationId).NotEmpty();
        RuleFor(x => x.OriginalReference).MaximumLength(200);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ConfirmedTotalUsd).InclusiveBetween(0.01m, 999999999m).PrecisionScale(18, 2, true);
        RuleFor(x => x.AccountReductionUsd).InclusiveBetween(0, 999999999m).PrecisionScale(18, 2, true);
        RuleFor(x => x.Lines).NotNull().Must(x => x is { Count: > 0 and <= 100 });
        RuleFor(x => x.Lines).Must(x => x is null || x.Select(y => y.PartId).Distinct().Count() == x.Count)
            .WithMessage("A SKU may appear only once in a sold-before-InventStock return.");
        RuleForEach(x => x.Lines).NotNull().ChildRules(line =>
        {
            line.RuleFor(x => x.PartId).NotEmpty();
            line.RuleFor(x => x.Quantity).InclusiveBetween(1, 1000000);
            line.RuleFor(x => x.RefundUsd).InclusiveBetween(0.01m, 999999999m).PrecisionScale(18, 2, true);
            line.RuleFor(x => x.Condition).IsInEnum();
            line.RuleFor(x => x.IssueReason).MaximumLength(500);
            line.RuleFor(x => x).Must(x => x.Condition != ReturnCondition.Quarantined ||
                (x.PhysicalSeparationConfirmed && !string.IsNullOrWhiteSpace(x.IssueReason)))
                .WithMessage("Faulty returned goods must be physically separated or tagged, with a reason.");
        });
        RuleFor(x => x.Refunds).NotNull().Must(x => x is { Count: <= 20 });
        RuleForEach(x => x.Refunds).NotNull().SetValidator(new SaleTenderValidator());
    }
}
