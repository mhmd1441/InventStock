using FluentValidation;
using InventStock.Application.DTOs.Customers;

namespace InventStock.Application.Validators;

public sealed class SaveCustomerRequestValidator : AbstractValidator<SaveCustomerRequest>
{
    public SaveCustomerRequestValidator()
    {
        RuleFor(x => x.OperationId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DistinguishingLabel).MaximumLength(200);
        RuleFor(x => x.Phone).MaximumLength(50);
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.ExpectedRevision).GreaterThanOrEqualTo(0);
    }
}
public sealed class PostCustomerOpeningRequestValidator : AbstractValidator<PostCustomerOpeningRequest>
{
    public PostCustomerOpeningRequestValidator()
    {
        RuleFor(x => x.OperationId).NotEmpty();
        RuleFor(x => x.BalanceUsd).InclusiveBetween(-999999999m, 999999999m).PrecisionScale(18, 2, true);
        RuleFor(x => x.AsOfUtc).Must(x => x.Kind == DateTimeKind.Utc && x >= new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc) && x <= DateTime.UtcNow);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
public sealed class PostCustomerPaymentRequestValidator : AbstractValidator<PostCustomerPaymentRequest>
{
    public PostCustomerPaymentRequestValidator()
    {
        RuleFor(x => x.OperationId).NotEmpty();
        RuleFor(x => x.ConfirmedUsdCredit).InclusiveBetween(0.01m, 999999999m).PrecisionScale(18, 2, true);
        RuleFor(x => x.ConfirmedExcessCreditUsd).InclusiveBetween(0, 999999999m).PrecisionScale(18, 2, true);
        RuleFor(x => x.Payments).NotNull().Must(x => x is { Count: > 0 and <= 20 });
        RuleFor(x => x.Change).NotNull().Must(x => x is { Count: <= 20 });
        RuleForEach(x => x.Payments).NotNull().SetValidator(new SaleTenderValidator());
        RuleForEach(x => x.Change).NotNull().SetValidator(new SaleTenderValidator());
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public sealed class CorrectCustomerPaymentRequestValidator : AbstractValidator<CorrectCustomerPaymentRequest>
{
    public CorrectCustomerPaymentRequestValidator()
    {
        RuleFor(x => x.OperationId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.ConfirmedBalanceAfterUsd).InclusiveBetween(-999999999m, 999999999m).PrecisionScale(18, 2, true);
    }
}

public sealed class CorrectCustomerOpeningRequestValidator : AbstractValidator<CorrectCustomerOpeningRequest>
{
    public CorrectCustomerOpeningRequestValidator()
    {
        RuleFor(x => x.OperationId).NotEmpty();
        RuleFor(x => x.ExpectedCurrentOpeningBalanceUsd).InclusiveBetween(-999999999m, 999999999m).PrecisionScale(18, 2, true);
        RuleFor(x => x.CorrectedOpeningBalanceUsd).InclusiveBetween(-999999999m, 999999999m).PrecisionScale(18, 2, true);
        RuleFor(x => x.ConfirmedBalanceAfterUsd).InclusiveBetween(-999999999m, 999999999m).PrecisionScale(18, 2, true);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
