namespace Billing.Caller;

// AFTER - Caller.
// Install the Graft; copy the package name and host from Vision.
// GraftConfig and BillingService in this project are a local stand-in
// (LocalGraftStandIn.cs). No Gateway is running. The stand-in does not
// open a socket.
// With a real installed Graft, a ws:// or wss:// host is still a network call.
public static class MonthlyBillCaller
{
    public static double Calculate(double unitPrice, int units)
    {
        GraftConfig.Host = "ws://billing.example/ws"; // before the first call

        var billing = new BillingService();
        return billing.CalculateMonthlyBill(unitPrice, units);
    }
}
