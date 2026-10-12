using FluentValidation;
using InventStock.Application.DTOs.Receiving;
using InventStock.Domain.Entities;

namespace InventStock.Application.Validators;

public sealed class PostPurchaseReceiptRequestValidator : AbstractValidator<PostPurchaseReceiptRequest>
{
    public PostPurchaseReceiptRequestValidator()
    {
        RuleFor(x => x.OperationId).NotEmpty();
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.SupplierReference).MaximumLength(PurchaseReceipt.ReferenceMaxLength);
        RuleFor(x => x.Notes).MaximumLength(PurchaseReceipt.NotesMaxLength);
        RuleFor(x => x.ReceivedAtUtc).LessThanOrEqualTo(DateTime.UtcNow.AddMinutes(5)).When(x => x.ReceivedAtUtc != default);
        RuleFor(x => x.Lines).NotEmpty().Must(x => x.Count <= 200).WithMessage("A receipt may contain at most 200 lines.");
        RuleForEach(x => x.Lines).SetValidator(new PostPurchaseReceiptLineRequestValidator());
    }
}

public sealed class PostPurchaseReceiptLineRequestValidator : AbstractValidator<PostPurchaseReceiptLineRequest>
{
    public PostPurchaseReceiptLineRequestValidator()
    {
        RuleFor(x => x.PartId).NotEmpty();
        RuleFor(x => x.PackageCount).InclusiveBetween(1, 1_000_000);
        RuleFor(x => x.BaseUnitsPerPackage).InclusiveBetween(1, 1_000_000);
        RuleFor(x => x).Must(x => (long)x.PackageCount * x.BaseUnitsPerPackage <= int.MaxValue)
            .WithMessage("Received base quantity is too large.");
        RuleFor(x => x.CostPerPackage).InclusiveBetween(0m, 999_999_999m).When(x => x.CostPerPackage.HasValue);
        RuleFor(x => x).Must(x => x.CostPerPackage.HasValue == x.Currency.HasValue)
            .WithMessage("Currency is required exactly when a purchase cost is entered.");
        RuleFor(x => x.Currency).IsInEnum().When(x => x.Currency.HasValue);
        RuleFor(x => x.TaxTreatment).IsInEnum();
        RuleFor(x => x.SupplierCode).MaximumLength(PurchaseReceiptLine.SupplierCodeMaxLength);
        RuleFor(x => x.Notes).MaximumLength(PurchaseReceiptLine.NotesMaxLength);
    }
}
