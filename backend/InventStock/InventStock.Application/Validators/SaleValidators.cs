using FluentValidation;
using InventStock.Application.DTOs.Sales;
using InventStock.Domain.Entities;

namespace InventStock.Application.Validators;

public sealed class PostSaleRequestValidator : AbstractValidator<PostSaleRequest>
{
    public PostSaleRequestValidator()
    {
        RuleFor(x => x.OperationId).NotEmpty();
        RuleFor(x => x.CustomerId).NotEqual(Guid.Empty).When(x => x.CustomerId.HasValue);
        RuleFor(x => x.ConfirmedTotalUsd).InclusiveBetween(0.01m, 999999999m).PrecisionScale(18, 2, true);
        RuleFor(x => x.CustomerCreditUsedUsd).InclusiveBetween(0, 999999999m).PrecisionScale(18, 2, true);
        RuleFor(x => x.UnpaidDebtUsd).InclusiveBetween(0, 999999999m).PrecisionScale(18, 2, true);
        RuleFor(x => x).Must(x => x.CustomerId.HasValue || (x.CustomerCreditUsedUsd == 0 && x.UnpaidDebtUsd == 0))
            .WithMessage("A named customer is required for credit or unpaid debt.");
        RuleFor(x => x.Lines).NotNull().Must(x => x is { Count: > 0 and <= 100 });
        RuleForEach(x => x.Lines).NotNull().ChildRules(line =>
        {
            line.RuleFor(x => x.PartId).NotEmpty();
            line.RuleFor(x => x.Quantity).InclusiveBetween(1, 1000000);
            line.RuleFor(x => x.UnitSellingPriceUsd).InclusiveBetween(0.000001m, 999999m).PrecisionScale(18, 6, true);
            line.RuleFor(x => x.StockExceptionReason).MaximumLength(500);
            line.RuleFor(x => x).Must(x => !x.PhysicalGoodsConfirmed || !string.IsNullOrWhiteSpace(x.StockExceptionReason))
                .WithMessage("Physical confirmation requires a stock exception reason.");
        });
        RuleFor(x => x.Payments).NotNull().Must(x => x is { Count: <= 20 });
        RuleFor(x => x.Change).NotNull().Must(x => x is { Count: <= 20 });
        RuleForEach(x => x.Payments).NotNull().SetValidator(new SaleTenderValidator());
        RuleForEach(x => x.Change).NotNull().SetValidator(new SaleTenderValidator());
    }
}

public sealed class PostPaperRecoverySaleRequestValidator : AbstractValidator<PostPaperRecoverySaleRequest>
{
    public PostPaperRecoverySaleRequestValidator()
    {
        RuleFor(x => x.Sale).NotNull().SetValidator(new PostSaleRequestValidator());
        RuleFor(x => x.OriginalOccurredAtUtc).Must(x => x.Kind == DateTimeKind.Utc && x >= new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc) && x <= DateTime.UtcNow);
        RuleFor(x => x.PaperReference).NotEmpty().MaximumLength(PaperRecoveryEntry.PaperReferenceMaxLength);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(PaperRecoveryEntry.ReasonMaxLength);
        RuleFor(x => x.OriginalSellerNote).MaximumLength(PaperRecoveryEntry.OriginalSellerNoteMaxLength);
    }
}

public sealed class SaleTenderValidator : AbstractValidator<SaleTenderRequest>
{
    public SaleTenderValidator()
    {
        RuleFor(x => x.CashAccountId).NotEmpty();
        RuleFor(x => x.Currency).IsInEnum();
        RuleFor(x => x.NativeAmount).InclusiveBetween(0.000001m, 999999999999m).PrecisionScale(20, 6, true);
        RuleFor(x => x.ConfirmedUsdAmount).InclusiveBetween(0.01m, 999999999m).PrecisionScale(18, 2, true);
        RuleFor(x => x).Must(x => x.Currency != Domain.Enums.MoneyCurrency.USD || x.NativeAmount == x.ConfirmedUsdAmount)
            .WithMessage("USD tender must equal its confirmed USD settlement.");
    }
}

public sealed class CorrectSaleTenderAccountRequestValidator : AbstractValidator<CorrectSaleTenderAccountRequest>
{
    public CorrectSaleTenderAccountRequestValidator()
    {
        RuleFor(x => x.OperationId).NotEmpty();
        RuleFor(x => x.CorrectCashAccountId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public sealed class ReverseSaleTenderAccountCorrectionRequestValidator : AbstractValidator<ReverseSaleTenderAccountCorrectionRequest>
{
    public ReverseSaleTenderAccountCorrectionRequestValidator()
    {
        RuleFor(x => x.OperationId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
