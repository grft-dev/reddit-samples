using System.Reflection;
using Billing;

namespace Example1.Tests;

public class InvoiceCalculatorTests
{
    [Fact]
    public void CalculateMonthlyBill_MultipliesAndRoundsHalfAwayFromZero()
    {
        Assert.Equal(50.00m, InvoiceCalculator.CalculateMonthlyBill(10m, 5));
        Assert.Equal(10.01m, InvoiceCalculator.CalculateMonthlyBill(10.005m, 1));
        Assert.Equal(10.00m, InvoiceCalculator.CalculateMonthlyBill(10.004m, 1));
        Assert.Equal(-8.00m, InvoiceCalculator.CalculateMonthlyBill(-2m, 4));
    }

    [Fact]
    public void CalculateMonthlyBill_RejectsNegativeUnits()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => InvoiceCalculator.CalculateMonthlyBill(10m, -1));
    }

    [Fact]
    public void ApplyTax_UsesFractionalRate()
    {
        Assert.Equal(123.00m, InvoiceCalculator.ApplyTax(100m, 0.23m));
        Assert.Equal(101.50m, InvoiceCalculator.ApplyTax(100m, 0.015m));
    }

    [Fact]
    public void ApplyTax_RejectsNegativeRate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => InvoiceCalculator.ApplyTax(100m, -0.01m));
    }

    [Fact]
    public void LateFee_IsOneAndAHalfPercentPerDay()
    {
        Assert.Equal(12.00m, InvoiceCalculator.LateFee(200m, 4));
        Assert.Equal(0m, InvoiceCalculator.LateFee(200m, 0));
        Assert.Equal(0m, InvoiceCalculator.LateFee(0m, 4));
        Assert.Equal(0m, InvoiceCalculator.LateFee(-10m, 4));
    }

    [Fact]
    public void FormatInvoice_IsAnInstanceMethod()
    {
        var method = typeof(InvoiceCalculator).GetMethod(nameof(InvoiceCalculator.FormatInvoice));
        Assert.NotNull(method);
        Assert.True(method.IsPublic);
        Assert.False(method.IsStatic);
        Assert.Equal("Invoice total 50.00", new InvoiceCalculator().FormatInvoice(50m));
    }

    [Fact]
    public void RoundForLedger_IsPrivate()
    {
        var method = typeof(InvoiceCalculator).GetMethod(
            "RoundForLedger",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        Assert.False(method.IsPublic);
        var rounded = (decimal)method.Invoke(null, [1.235m])!;
        Assert.Equal(1.24m, rounded);
    }
}
