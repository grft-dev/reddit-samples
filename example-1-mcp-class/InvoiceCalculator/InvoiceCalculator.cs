namespace Billing;

// Plain C# class. No MCP attributes and no Graftcode types.
// The Gateway command and the MCP URL are in host/gg.sh and host/mcp.json.
// --types and --methods select the callable surface. They do not
// authenticate or authorize a caller.
// The .NET MCP quick start says static methods are also MCP tools.
// Confirm the hosted surface in Vision (http://localhost:81/GV).
// This sample does not describe instance members as MCP tools.
public class InvoiceCalculator
{
    public static decimal CalculateMonthlyBill(decimal unitPrice, int units)
    {
        if (units < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(units));
        }

        return RoundForLedger(unitPrice * units);
    }

    // taxRate is a fraction: 0.23 means 23%.
    public static decimal ApplyTax(decimal amount, decimal taxRate)
    {
        if (taxRate < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(taxRate));
        }

        return RoundForLedger(amount * (1m + taxRate));
    }

    // 1.5% of the balance for each day past due.
    // No fee when nothing is owed or the invoice is not yet due.
    public static decimal LateFee(decimal balance, int daysPastDue)
    {
        if (daysPastDue <= 0 || balance <= 0m)
        {
            return 0m;
        }

        return RoundForLedger(balance * 0.015m * daysPastDue);
    }

    // Public instance method. host/gg.sh does not name it.
    // This sample does not describe it as an MCP tool.
    public string FormatInvoice(decimal total)
        => "Invoice total " + total.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    // Private helper. Not part of the public surface.
    private static decimal RoundForLedger(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
