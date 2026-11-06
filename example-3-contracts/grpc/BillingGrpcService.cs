using System.Globalization;
using Grpc.Core;

namespace Billing.GrpcContract;

// Adapter from the generated service base to the method.
// You maintain billing.proto and this mapping. Grpc.Tools generates BillingApiBase.
public sealed class BillingGrpcService : BillingApi.BillingApiBase
{
    private readonly BillingService _billing;

    public BillingGrpcService(BillingService billing)
    {
        _billing = billing ?? throw new ArgumentNullException(nameof(billing));
    }

    public override Task<MonthlyBillResponse> CalculateMonthlyBill(
        MonthlyBillRequest request,
        ServerCallContext context)
    {
        if (!decimal.TryParse(
                request.UnitPrice,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var unitPrice))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "unit_price must be a decimal number."));
        }

        var total = _billing.CalculateMonthlyBill(unitPrice, request.Units);
        return Task.FromResult(new MonthlyBillResponse
        {
            Total = total.ToString(CultureInfo.InvariantCulture),
        });
    }
}
