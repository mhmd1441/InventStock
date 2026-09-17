using FluentValidation;
using InventStock.Application.DTOs.Catalog;
using InventStock.Domain.Entities;

namespace InventStock.Application.Validators;

public sealed class SaveCategoryRequestValidator : AbstractValidator<SaveCategoryRequest>
{
    public SaveCategoryRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(Category.NameMaxLength);

        RuleFor(x => x.Description)
            .MaximumLength(Category.DescriptionMaxLength);
    }
}

public sealed class SaveSupplierRequestValidator : AbstractValidator<SaveSupplierRequest>
{
    public SaveSupplierRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(Supplier.NameMaxLength);

        RuleFor(x => x.ContactName)
            .MaximumLength(Supplier.ContactNameMaxLength);

        RuleFor(x => x.Email)
            .EmailAddress()
            .MaximumLength(Supplier.EmailMaxLength)
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.Phone)
            .MaximumLength(Supplier.PhoneMaxLength);

        RuleFor(x => x.Address)
            .MaximumLength(Supplier.AddressMaxLength);
    }
}

public sealed class SavePartRequestValidator : AbstractValidator<SavePartRequest>
{
    public SavePartRequestValidator()
    {
        RuleFor(x => x.PartNumber)
            .NotEmpty()
            .MaximumLength(Part.PartNumberMaxLength)
            .Matches("^[A-Za-z0-9._/-]+$")
            .WithMessage("Part number may contain only letters, digits and the characters . _ / -");

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(Part.NameMaxLength);

        RuleFor(x => x.Description)
            .MaximumLength(Part.DescriptionMaxLength);

        RuleFor(x => x.CategoryId)
            .NotEmpty();

        RuleFor(x => x.UnitPrice)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(9_999_999m);

        RuleFor(x => x.LowStockThreshold)
            .GreaterThanOrEqualTo(0)
            .LessThanOrEqualTo(1_000_000);

        RuleFor(x => x.Barcode)
            .MaximumLength(Part.BarcodeMaxLength);
    }
}
