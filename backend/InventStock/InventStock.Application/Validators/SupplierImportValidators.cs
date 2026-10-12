using FluentValidation;
using InventStock.Application.DTOs.Imports;
using InventStock.Domain.Entities;

namespace InventStock.Application.Validators;

public sealed class StageSupplierImportRequestValidator : AbstractValidator<StageSupplierImportRequest>
{
    public StageSupplierImportRequestValidator()
    {
        RuleFor(x => x.SupplierId).NotEmpty();
        RuleFor(x => x.SourceFileName).NotEmpty().MaximumLength(SupplierImportBatch.FileNameMaxLength);
        RuleFor(x => x.SourceSha256).Matches("^[A-Fa-f0-9]{64}$");
        RuleFor(x => x.Rows).NotEmpty().Must(x => x.Count <= 10_000).WithMessage("An import may contain at most 10,000 rows.");
        RuleForEach(x => x.Rows).SetValidator(new StageSupplierImportRowRequestValidator());
    }
}

public sealed class StageSupplierImportRowRequestValidator : AbstractValidator<StageSupplierImportRowRequest>
{
    public StageSupplierImportRowRequestValidator()
    {
        RuleFor(x => x.SourceRowNumber).GreaterThan(0);
        RuleFor(x => x.PartNumber).NotEmpty().MaximumLength(Part.PartNumberMaxLength);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Part.NameMaxLength);
        RuleFor(x => x.Brand).MaximumLength(SupplierImportRow.BrandMaxLength);
        RuleFor(x => x.Variant).MaximumLength(SupplierImportRow.VariantMaxLength);
        RuleFor(x => x.SupplierCode).MaximumLength(SupplierImportRow.SupplierCodeMaxLength);
        RuleFor(x => x.CatalogueCost).InclusiveBetween(0m, 999_999_999m).When(x => x.CatalogueCost.HasValue);
        RuleFor(x => x).Must(x => x.CatalogueCost.HasValue == x.Currency.HasValue)
            .WithMessage("Currency is required exactly when catalogue cost is present.");
        RuleFor(x => x.Currency).IsInEnum().When(x => x.Currency.HasValue);
        RuleFor(x => x.TaxTreatment).IsInEnum();
    }
}

public sealed class ReviewSupplierImportRowRequestValidator : AbstractValidator<ReviewSupplierImportRowRequest>
{
    public ReviewSupplierImportRowRequestValidator()
    {
        RuleFor(x => x.Decision).IsInEnum().NotEqual(Domain.Enums.SupplierImportDecision.Pending);
        RuleFor(x => x.StockUnit).IsInEnum();
    }
}
