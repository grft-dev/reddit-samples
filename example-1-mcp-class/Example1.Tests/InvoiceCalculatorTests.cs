using System.Reflection;
using Billing;

namespace Example1.Tests;

public class InvoiceCalculatorTests
{
    [Fact]
    public void CalculateMonthlyBill_MultipliesAndRoundsHalfAwayFromZero()
    {
        Assert.Equal(50.00, InvoiceCalculator.CalculateMonthlyBill(10d, 5));
        // 10.125 is an exact binary value; 10.005 is not.
        Assert.Equal(10.13, InvoiceCalculator.CalculateMonthlyBill(10.125d, 1));
        Assert.Equal(10.12, InvoiceCalculator.CalculateMonthlyBill(10.124d, 1));
        Assert.Equal(-8.00, InvoiceCalculator.CalculateMonthlyBill(-2d, 4));
    }

    [Fact]
    public void CalculateMonthlyBill_RejectsNegativeUnits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => InvoiceCalculator.CalculateMonthlyBill(10d, -1));
    }

    [Fact]
    public void ApplyTax_UsesFractionalRate()
    {
        Assert.Equal(123.00, InvoiceCalculator.ApplyTax(100d, 0.23d));
        Assert.Equal(101.50, InvoiceCalculator.ApplyTax(100d, 0.015d));
    }

    [Fact]
    public void ApplyTax_RejectsNegativeRate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => InvoiceCalculator.ApplyTax(100d, -0.01d));
    }

    [Fact]
    public void LateFee_IsOneAndAHalfPercentPerDay()
    {
        Assert.Equal(12.00, InvoiceCalculator.LateFee(200d, 4));
        Assert.Equal(0d, InvoiceCalculator.LateFee(200d, 0));
        Assert.Equal(0d, InvoiceCalculator.LateFee(0d, 4));
        Assert.Equal(0d, InvoiceCalculator.LateFee(-10d, 4));
    }

    [Fact]
    public void FormatInvoice_IsAnInstanceMethod()
    {
        var method = typeof(InvoiceCalculator).GetMethod(nameof(InvoiceCalculator.FormatInvoice));
        Assert.NotNull(method);
        Assert.True(method.IsPublic);
        Assert.False(method.IsStatic);
        Assert.Equal("Invoice total 50.00", new InvoiceCalculator().FormatInvoice(50d));
    }

    [Fact]
    public void RoundForLedger_IsPrivate()
    {
        var method = typeof(InvoiceCalculator).GetMethod(
            "RoundForLedger",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        Assert.False(method.IsPublic);
        var rounded = (double)method.Invoke(null, [2.125d])!;
        Assert.Equal(2.13, rounded);
    }
}
