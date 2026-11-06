// Illustrative REST client — not Graftcode.

using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Billing.IllustrativeRest;

public static class BillingHttpClient
{
    public static async Task<decimal> CalculateMonthlyBillAsync(
        HttpClient http,
        string token,
        decimal unitPrice,
        int units,
        CancellationToken cancellationToken = default)
    {
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await http.PostAsJsonAsync(
            "https://billing.example/api/v1/billing/monthly-bill",
            new MonthlyBillRequest { UnitPrice = unitPrice, Units = units },
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<MonthlyBillResponse>(cancellationToken);
        if (body is null)
        {
            throw new InvalidOperationException("Illustrative REST response had no body.");
        }

        return body.Total;
    }
}
