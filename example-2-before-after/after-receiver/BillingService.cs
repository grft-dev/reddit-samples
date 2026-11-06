namespace Billing;

// Receiver. Plain public method. No controller, route, or DTO.
// This is the method the introduction names CalculateMonthlyBill.
public class BillingService
{
    public decimal CalculateMonthlyBill(decimal unitPrice, int units)
    {
        if (units < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(units));
        }

        return unitPrice * units;
    }
}
