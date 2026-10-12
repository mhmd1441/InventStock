using FluentValidation;
using InventStock.Application.DTOs.Cash;
using InventStock.Domain.Entities;
using InventStock.Domain.Enums;

namespace InventStock.Application.Validators;

public sealed class PostCashOpeningBalanceRequestValidator : AbstractValidator<PostCashOpeningBalanceRequest>
{
    public PostCashOpeningBalanceRequestValidator()
    {
        RuleFor(x => x.OperationId).NotEmpty();
        RuleFor(x => x.CountedAmount).InclusiveBetween(0m, 999_999_999_999m);
        RuleFor(x => x.CountedAtUtc).NotEmpty().LessThanOrEqualTo(DateTime.UtcNow.AddMinutes(5));
    }
}

public sealed class SaveExpenseCategoryRequestValidator : AbstractValidator<SaveExpenseCategoryRequest>
{
    public SaveExpenseCategoryRequestValidator() => RuleFor(x => x.Name).NotEmpty().MaximumLength(ExpenseCategory.NameMaxLength);
}

public sealed class PostOutgoingPaymentRequestValidator : AbstractValidator<PostOutgoingPaymentRequest>
{
    public PostOutgoingPaymentRequestValidator()
    {
        RuleFor(x => x.OperationId).NotEmpty();
        RuleFor(x => x.ExpenseCategoryId).NotEmpty();
        RuleFor(x => x.FundingSource).IsInEnum();
        RuleFor(x => x.OriginalAmount).GreaterThan(0).LessThanOrEqualTo(999_999_999_999m);
        RuleFor(x => x.Currency).IsInEnum();
        RuleFor(x => x.AgreedUsdValue).GreaterThan(0).LessThanOrEqualTo(999_999_999_999m);
        RuleFor(x => x.PaidOn).NotEmpty().LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));
        RuleFor(x => x.Payee).MaximumLength(OutgoingPayment.TextMaxLength);
        RuleFor(x => x.Description).MaximumLength(OutgoingPayment.TextMaxLength);
        RuleFor(x => x.ForPeriod).MaximumLength(OutgoingPayment.TextMaxLength);
        RuleFor(x => x.ReceiptReference).MaximumLength(OutgoingPayment.ReceiptReferenceMaxLength);
        RuleFor(x => x).Must(x => x.FundingSource == PaymentFundingSource.ShopAccount ? x.CashAccountId.HasValue : !x.CashAccountId.HasValue)
            .WithMessage("Shop-funded payments require an account; personally funded payments must not select one.");
        RuleFor(x => x).Must(x => x.Currency != MoneyCurrency.USD || x.OriginalAmount == x.AgreedUsdValue)
            .WithMessage("For USD payments, the agreed USD value must equal the original amount.");
    }
}

public sealed class ReverseOutgoingPaymentRequestValidator : AbstractValidator<ReverseOutgoingPaymentRequest>
{
    public ReverseOutgoingPaymentRequestValidator()
    {
        RuleFor(x => x.OperationId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(OutgoingPaymentReversal.ReasonMaxLength);
        RuleFor(x => x.ReversedAtUtc).NotEmpty().LessThanOrEqualTo(DateTime.UtcNow.AddMinutes(5));
    }
}
