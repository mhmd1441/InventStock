using InventStock.Domain.Entities;
using InventStock.Domain.Exceptions;
using InventStock.Domain.Enums;

namespace InventStock.Tests.Domain;

public class PartTests
{
    private static readonly Guid CategoryId = Guid.NewGuid();

    [Theory]
    [InlineData("brk-1001", "BRK-1001")]
    [InlineData("  flt-2001  ", "FLT-2001")]
    public void Create_NormalisesPartNumber(string input, string expected)
    {
        var part = Part.Create(input, "Brake pad", null, CategoryId, null, 10m, 5, null);

        Assert.Equal(expected, part.PartNumber);
    }

    [Fact]
    public void Create_RejectsNegativeUnitPrice()
    {
        Assert.Throws<DomainException>(() =>
            Part.Create("BRK-1", "Brake pad", null, CategoryId, null, -1m, 5, null));
    }

    [Fact]
    public void Create_RejectsNegativeLowStockThreshold()
    {
        Assert.Throws<DomainException>(() =>
            Part.Create("BRK-1", "Brake pad", null, CategoryId, null, 10m, -5, null));
    }

    [Fact]
    public void Create_RejectsEmptyPartNumber()
    {
        Assert.Throws<DomainException>(() =>
            Part.Create("  ", "Brake pad", null, CategoryId, null, 10m, 5, null));
    }

    [Fact]
    public void Create_RejectsOverlongName()
    {
        var name = new string('x', Part.NameMaxLength + 1);

        Assert.Throws<DomainException>(() =>
            Part.Create("BRK-1", name, null, CategoryId, null, 10m, 5, null));
    }

    [Fact]
    public void NewPart_IsActive()
    {
        var part = Part.Create("BRK-1", "Brake pad", null, CategoryId, null, 10m, 5, null);

        Assert.True(part.IsActive);

        part.Deactivate();
        Assert.False(part.IsActive);

        part.Reactivate();
        Assert.True(part.IsActive);
    }

    [Fact]
    public void StockUnit_CannotChangeAfterHistoryExists()
    {
        var part = Part.Create("HOSE-1", "Cut hose", null, CategoryId, null, 10m, 5, null);
        part.SetStockUnit(StockUnit.Centimeter, hasStockHistory: false);
        Assert.Equal(StockUnit.Centimeter, part.StockUnit);

        Assert.Throws<DomainException>(() => part.SetStockUnit(StockUnit.Piece, hasStockHistory: true));
    }
}
