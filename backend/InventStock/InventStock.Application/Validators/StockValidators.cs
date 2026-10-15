using FluentValidation;
using InventStock.Application.DTOs.Stock;
using InventStock.Domain.Entities;

namespace InventStock.Application.Validators;

public sealed class StockInRequestValidator : AbstractValidator<StockInRequest>
{
    public StockInRequestValidator()
    {
        RuleFor(x => x.OperationId).NotEmpty();
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
        RuleFor(x => x.OperationId).NotEmpty();
        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .LessThanOrEqualTo(1_000_000);

        RuleFor(x => x.Reason).NotEmpty().MaximumLength(StockMovement.ReasonMaxLength);
        RuleFor(x => x.Reference).MaximumLength(StockMovement.ReferenceMaxLength);
    }
}

public sealed class StockAdjustmentRequestValidator : AbstractValidator<StockAdjustmentRequest>
{
    public StockAdjustmentRequestValidator()
    {
        RuleFor(x => x.OperationId).NotEmpty();
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

public sealed class StockCountRequestValidator : AbstractValidator<StockCountRequest>
{
    public StockCountRequestValidator()
    {
        RuleFor(x => x.OperationId).NotEmpty();
        RuleFor(x => x.CountedQuantity).InclusiveBetween(0, 1_000_000);
        // A physically confirmed shortage can leave the book balance negative until this count.
        RuleFor(x => x.ExpectedBookQuantity).InclusiveBetween(-1_000_000, 1_000_000);
        RuleFor(x => x.Reason).MaximumLength(StockMovement.ReasonMaxLength);
    }
}

public sealed class StockHoldRequestValidator : AbstractValidator<StockHoldRequest>
{
    public StockHoldRequestValidator()
    {
        RuleFor(x => x.OperationId).NotEmpty(); RuleFor(x => x.Quantity).InclusiveBetween(1, 1_000_000);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(StockHoldMovement.ReasonMaxLength);
        RuleFor(x => x.PhysicalSeparationConfirmed).Equal(true).WithMessage("Confirm that held goods were physically separated or clearly tagged.");
    }
}

public sealed class StockReleaseRequestValidator : AbstractValidator<StockReleaseRequest>
{
    public StockReleaseRequestValidator()
    {
        RuleFor(x => x.OperationId).NotEmpty(); RuleFor(x => x.Quantity).InclusiveBetween(1, 1_000_000);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(StockHoldMovement.ReasonMaxLength);
    }
}
