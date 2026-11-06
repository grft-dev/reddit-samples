namespace Billing;

// Receiver. Plain public method. No controller, route, or DTO.
// This is the method the introduction names CalculateMonthlyBill.
// Amounts are double so MCP JSON numbers bind on Gateway 1.4.14.
// Units stay int.
public class BillingService
{
    public double CalculateMonthlyBill(double unitPrice, int units)
    {
        if (units < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(units));
        }

        return unitPrice * units;
    }
}
