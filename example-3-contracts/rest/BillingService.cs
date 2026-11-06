namespace Billing.RestContract;

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
