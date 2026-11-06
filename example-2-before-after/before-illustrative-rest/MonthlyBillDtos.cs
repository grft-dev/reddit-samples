// Illustrative REST DTOs — not Graftcode.

namespace Billing.IllustrativeRest;

public class MonthlyBillRequest
{
    public decimal UnitPrice { get; set; }

    public int Units { get; set; }
}

public class MonthlyBillResponse
{
    public decimal Total { get; set; }
}
