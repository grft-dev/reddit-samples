// Illustrative REST DTO — not Graftcode.
// One of the artifacts this path keeps beside the method.

namespace Billing.RestContract;

public class MonthlyBillRequest
{
    public decimal UnitPrice { get; set; }

    public int Units { get; set; }
}

public class MonthlyBillResponse
{
    public decimal Total { get; set; }
}
