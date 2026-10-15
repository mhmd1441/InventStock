using InventStock.Application.Services;

namespace InventStock.Tests.Infrastructure;

public sealed class ReturnAllocationTests
{
    [Fact]
    public void Three_partial_returns_recover_exact_original_cent_total()
    {
        // 3 × $0.335 was posted as $1.01. Three independent $0.34 refunds would create a cent.
        var first = ReturnAllocation.AdditionalRefund(1.01m, 3, 0, 0, 1);
        var second = ReturnAllocation.AdditionalRefund(1.01m, 3, 1, first, 1);
        var third = ReturnAllocation.AdditionalRefund(1.01m, 3, 2, first + second, 1);
        Assert.Equal(1.01m, first + second + third);
        Assert.Equal(0.34m, first);
        Assert.Equal(0.33m, second);
        Assert.Equal(0.34m, third);
    }

    [Fact]
    public void A_full_return_recovers_only_the_unreturned_remainder()
    {
        Assert.Equal(0.67m, ReturnAllocation.AdditionalRefund(1.01m, 3, 1, 0.34m, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => ReturnAllocation.AdditionalRefund(1.01m, 3, 2, 0.67m, 2));
    }

    [Fact]
    public void Corrupt_prior_refunds_are_rejected_rather_than_overpaid()
    {
        Assert.Throws<InvalidOperationException>(() => ReturnAllocation.AdditionalRefund(1.01m, 3, 1, 0.80m, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ReturnAllocation.AdditionalRefund(1.01m, 3, 1, 1.02m, 1));
    }
}
