namespace Billing;

// Plain C# class. No MCP attributes and no Graftcode types.
// The Gateway command and the MCP URL are in host/gg.sh and host/mcp.json.
// --types and --methods select the callable surface. They do not
// authenticate or authorize a caller.
// The .NET MCP quick start says static methods are also MCP tools.
// Confirm the hosted surface in Vision (http://localhost:81/GV).
// This sample does not describe instance members as MCP tools.
// Amounts are double so MCP JSON numbers bind on Gateway 1.4.14.
// That is a binding choice for this sample, not a ledger-type claim.
// Units stay int.
public class InvoiceCalculator
{
    public static double CalculateMonthlyBill(double unitPrice, int units)
    {
        if (units < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(units));
        }

        return RoundForLedger(unitPrice * units);
    }

    // taxRate is a fraction: 0.23 means 23%.
    public static double ApplyTax(double amount, double taxRate)
    {
        if (taxRate < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(taxRate));
        }

        return RoundForLedger(amount * (1d + taxRate));
    }

    // 1.5% of the balance for each day past due.
    // No fee when nothing is owed or the invoice is not yet due.
    public static double LateFee(double balance, int daysPastDue)
    {
        if (daysPastDue <= 0 || balance <= 0d)
        {
            return 0d;
        }

        return RoundForLedger(balance * 0.015d * daysPastDue);
    }

    // Public instance method. host/gg.sh does not name it.
    // This sample does not describe it as an MCP tool.
    public string FormatInvoice(double total)
        => "Invoice total " + total.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    // Private helper. Not part of the public surface.
    private static double RoundForLedger(double value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
