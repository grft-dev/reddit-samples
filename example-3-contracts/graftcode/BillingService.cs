namespace Billing.Graftcode;

// What you maintain on this path: the public method signature.
// Gateway publishes the selected callable surface, and Callers install
// the generated package. Package name and host come from Vision.
// This file is not a route, a DTO, an OpenAPI document, or a .proto.
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
