// NOT a generated Graft and NOT a Gateway client.
// These types exist so the Caller compiles in the documented shape
// without a running Gateway. They call the Receiver in-process.
// This stand-in does not open a socket. Package coordinates are not invented here;
// copy them from Vision when you install a real Graft.

namespace Billing.Caller;

public static class GraftConfig
{
    public static string? Host { get; set; }
}

public class BillingService
{
    public BillingService()
    {
        if (string.IsNullOrWhiteSpace(GraftConfig.Host))
        {
            throw new InvalidOperationException(
                "Set GraftConfig.Host before the first call. Copy the host from Vision.");
        }
    }

    public decimal CalculateMonthlyBill(decimal unitPrice, int units)
        => new global::Billing.BillingService().CalculateMonthlyBill(unitPrice, units);
}
