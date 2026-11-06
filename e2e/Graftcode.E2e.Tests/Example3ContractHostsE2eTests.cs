using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Billing.GrpcHost;
using Billing.RestHost;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using GrpcBillingApi = Billing.GrpcContract.BillingApi;
using GrpcMonthlyBillRequest = Billing.GrpcContract.MonthlyBillRequest;

namespace Graftcode.E2e.Tests;

// Example 3, REST and gRPC paths. Each test starts a real Kestrel server on
// 127.0.0.1 with an ephemeral port and calls it over TCP. No Docker, no stub
// handler, no in-process controller or adapter call. These hosts are not MCP
// servers; only the Graftcode path in GatewayE2eTests goes through /mcp.
public class Example3ContractHostsE2eTests
{
    private const string EphemeralLoopback = "http://127.0.0.1:0";

    [Theory]
    [InlineData("10", 5, "50")]
    [InlineData("19.99", 3, "59.97")]
    public async Task Example3_RestHostAnswersMonthlyBillOverHttp(string unitPrice, int units, string expected)
    {
        await using var app = RestHost.Build(EphemeralLoopback);
        await app.StartAsync();
        var baseAddress = BoundLoopbackAddress(app);

        using var http = new HttpClient { BaseAddress = baseAddress, Timeout = TimeSpan.FromSeconds(30) };
        var json = $$"""{"unitPrice":{{unitPrice}},"units":{{units}}}""";
        using var response = await http.PostAsync(
            "/api/v1/billing/monthly-bill",
            new StringContent(json, Encoding.UTF8, "application/json"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, $"POST returned {(int)response.StatusCode}: {body}");
        using var document = JsonDocument.Parse(body);
        var total = document.RootElement.GetProperty("total");
        Assert.Equal(JsonValueKind.Number, total.ValueKind);
        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), total.GetDecimal());

        // The REST host only serves its route. It is not an MCP endpoint.
        using var mcp = await http.PostAsync("/mcp", new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.NotFound, mcp.StatusCode);

        await app.StopAsync();
    }

    [Theory]
    [InlineData("10", 5, "50")]
    [InlineData("19.99", 3, "59.97")]
    public async Task Example3_GrpcHostAnswersMonthlyBillOverChannel(string unitPrice, int units, string expected)
    {
        await using var app = GrpcHost.Build(EphemeralLoopback);
        await app.StartAsync();
        var address = BoundLoopbackAddress(app);

        using var channel = GrpcChannel.ForAddress(address);
        var client = new GrpcBillingApi.BillingApiClient(channel);
        var response = await client.CalculateMonthlyBillAsync(
            new GrpcMonthlyBillRequest { UnitPrice = unitPrice, Units = units },
            deadline: DateTime.UtcNow.AddSeconds(30));

        // total is a proto string; compare as decimal so "50" and "50.00" both match 50.
        Assert.True(
            decimal.TryParse(response.Total, NumberStyles.Number, CultureInfo.InvariantCulture, out var total),
            $"total '{response.Total}' is not a decimal string.");
        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), total);

        await app.StopAsync();
    }

    [Fact]
    public async Task Example3_GrpcHostRejectsInvalidUnitPriceWithInvalidArgument()
    {
        await using var app = GrpcHost.Build(EphemeralLoopback);
        await app.StartAsync();
        var address = BoundLoopbackAddress(app);

        using var channel = GrpcChannel.ForAddress(address);
        var client = new GrpcBillingApi.BillingApiClient(channel);
        var error = await Assert.ThrowsAsync<RpcException>(async () => await client.CalculateMonthlyBillAsync(
            new GrpcMonthlyBillRequest { UnitPrice = "nope", Units = 1 },
            deadline: DateTime.UtcNow.AddSeconds(30)));

        Assert.Equal(StatusCode.InvalidArgument, error.StatusCode);

        await app.StopAsync();
    }

    private static Uri BoundLoopbackAddress(WebApplication app)
    {
        var url = Assert.Single(app.Urls);
        var uri = new Uri(url);
        Assert.Equal("127.0.0.1", uri.Host);
        Assert.NotEqual(0, uri.Port);
        Assert.NotEqual(80, uri.Port);
        Assert.NotEqual(81, uri.Port);
        return uri;
    }
}
