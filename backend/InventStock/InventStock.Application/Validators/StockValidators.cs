using FluentValidation;
using InventStock.Application.DTOs.Stock;
using InventStock.Domain.Entities;

namespace InventStock.Application.Validators;

public sealed class StockInRequestValidator : AbstractValidator<StockInRequest>
{
    public StockInRequestValidator()
    {
        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .LessThanOrEqualTo(1_000_000);

        RuleFor(x => x.Reason).MaximumLength(StockMovement.ReasonMaxLength);
        RuleFor(x => x.Reference).MaximumLength(StockMovement.ReferenceMaxLength);
    }
}

public sealed class StockOutRequestValidator : AbstractValidator<StockOutRequest>
{
    public StockOutRequestValidator()
    {
        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .LessThanOrEqualTo(1_000_000);

        RuleFor(x => x.Reason).MaximumLength(StockMovement.ReasonMaxLength);
        RuleFor(x => x.Reference).MaximumLength(StockMovement.ReferenceMaxLength);
    }
}

public sealed class StockAdjustmentRequestValidator : AbstractValidator<StockAdjustmentRequest>
{
    public StockAdjustmentRequestValidator()
    {
        RuleFor(x => x.QuantityChange)
            .NotEqual(0)
            .WithMessage("Adjustment quantity must not be zero.")
            .GreaterThanOrEqualTo(-1_000_000)
            .LessThanOrEqualTo(1_000_000);

        // An adjustment without a reason destroys the value of the audit trail.
        RuleFor(x => x.Reason)
            .NotEmpty()
            .MaximumLength(StockMovement.ReasonMaxLength);

        RuleFor(x => x.Reference).MaximumLength(StockMovement.ReferenceMaxLength);
    }
}
