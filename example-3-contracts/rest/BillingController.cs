// Illustrative REST controller — not Graftcode.
// What you maintain on this path, besides the method:
// this route, the DTO, openapi.yaml, and BillingRestClient.

using Microsoft.AspNetCore.Mvc;

namespace Billing.RestContract;

[ApiController]
[Route("api/v1/billing")]
public class BillingController : ControllerBase
{
    private readonly BillingService _billing;

    public BillingController(BillingService billing)
    {
        _billing = billing ?? throw new ArgumentNullException(nameof(billing));
    }

    [HttpPost("monthly-bill")]
    public ActionResult<MonthlyBillResponse> CalculateMonthlyBill([FromBody] MonthlyBillRequest request)
    {
        var total = _billing.CalculateMonthlyBill(request.UnitPrice, request.Units);
        return Ok(new MonthlyBillResponse { Total = total });
    }
}
