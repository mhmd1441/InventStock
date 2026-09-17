using InventStock.Domain.Entities;
using InventStock.Domain.Enums;
using InventStock.Domain.Exceptions;

namespace InventStock.Tests.Domain;

public class StockMovementTests
{
    private static readonly Guid PartId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void StockIn_StoresQuantityAsPositive()
    {
        var movement = StockMovement.StockIn(PartId, 20, UserId, "Delivery", "PO-1");

        Assert.Equal(MovementType.In, movement.MovementType);
        Assert.Equal(20, movement.QuantityChange);
    }

    [Fact]
    public void StockOut_StoresQuantityAsNegative()
    {
        var movement = StockMovement.StockOut(PartId, 5, UserId, "Workshop issue", null);

        Assert.Equal(MovementType.Out, movement.MovementType);
        Assert.Equal(-5, movement.QuantityChange);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void StockIn_RejectsNonPositiveQuantity(int quantity)
    {
        Assert.Throws<DomainException>(() => StockMovement.StockIn(PartId, quantity, UserId, null, null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void StockOut_RejectsNonPositiveQuantity(int quantity)
    {
        Assert.Throws<DomainException>(() => StockMovement.StockOut(PartId, quantity, UserId, null, null));
    }

    [Fact]
    public void Adjustment_KeepsSignOfSuppliedChange()
    {
        var shrinkage = StockMovement.Adjustment(PartId, -2, UserId, "Damaged in storage", null);
        var recount = StockMovement.Adjustment(PartId, 3, UserId, "Recount", null);

        Assert.Equal(-2, shrinkage.QuantityChange);
        Assert.Equal(3, recount.QuantityChange);
        Assert.Equal(MovementType.Adjustment, shrinkage.MovementType);
    }

    [Fact]
    public void Adjustment_RequiresAReason()
    {
        Assert.Throws<DomainException>(() => StockMovement.Adjustment(PartId, -2, UserId, "   ", null));
    }

    [Fact]
    public void Adjustment_RejectsZeroChange()
    {
        Assert.Throws<DomainException>(() => StockMovement.Adjustment(PartId, 0, UserId, "Recount", null));
    }

    [Fact]
    public void Create_RequiresAPerformingUser()
    {
        Assert.Throws<DomainException>(() => StockMovement.StockIn(PartId, 1, Guid.Empty, null, null));
    }

    [Fact]
    public void Create_RequiresAPart()
    {
        Assert.Throws<DomainException>(() => StockMovement.StockIn(Guid.Empty, 1, UserId, null, null));
    }

    [Fact]
    public void SignedQuantities_SumToCurrentStock()
    {
        var movements = new[]
        {
            StockMovement.StockIn(PartId, 20, UserId, null, null),
            StockMovement.StockOut(PartId, 5, UserId, null, null),
            StockMovement.Adjustment(PartId, -2, UserId, "Damaged", null)
        };

        Assert.Equal(13, movements.Sum(m => m.QuantityChange));
    }
}
