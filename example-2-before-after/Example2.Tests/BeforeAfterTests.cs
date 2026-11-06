using System.Net;
using System.Reflection;
using System.Text;
using Billing.Caller;
using Billing.IllustrativeRest;
using Microsoft.AspNetCore.Mvc;
using ReceiverBilling = Billing.BillingService;

namespace Example2.Tests;

public class BeforeAfterTests
{
    [Fact]
    public void Receiver_MatchesTheIntroductionProduct()
    {
        var total = new ReceiverBilling().CalculateMonthlyBill(10d, 5);
        Assert.Equal(50d, total);
        Assert.Equal(59.97d, new ReceiverBilling().CalculateMonthlyBill(19.99d, 3), 2);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ReceiverBilling().CalculateMonthlyBill(10d, -1));
    }

    [Fact]
    public void IllustrativeController_AssignsAndUsesBillingService()
    {
        var service = new ReceiverBilling();
        var controller = new BillingController(service);

        var field = typeof(BillingController).GetField(
            "_billing",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        Assert.True(field.IsInitOnly);
        Assert.Same(service, field.GetValue(controller));

        var result = controller.CalculateMonthlyBill(new MonthlyBillRequest
        {
            UnitPrice = 10m,
            Units = 5,
        });
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<MonthlyBillResponse>(ok.Value);
        Assert.Equal(50m, body.Total);
    }

    [Fact]
    public async Task IllustrativeClient_PostsTheDocumentedRoute()
    {
        var handler = new StubHandler("""{"total":50.00}""");
        using var http = new HttpClient(handler);

        var total = await BillingHttpClient.CalculateMonthlyBillAsync(http, "token-1", 10m, 5);

        Assert.Equal(50m, total);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal(
            "https://billing.example/api/v1/billing/monthly-bill",
            handler.Request.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Request.Headers.Authorization!.Scheme);
        Assert.Equal("token-1", handler.Request.Headers.Authorization.Parameter);
        Assert.Contains("\"unitPrice\":10", handler.Body, StringComparison.Ordinal);
        Assert.Contains("\"units\":5", handler.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Caller_SetsHostThenConstructsTheGraftType()
    {
        GraftConfig.Host = null;

        var total = MonthlyBillCaller.Calculate(10d, 5);

        Assert.Equal(50d, total);
        Assert.Equal("ws://billing.example/ws", GraftConfig.Host);
        Assert.Empty(typeof(MonthlyBillCaller).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
        Assert.Empty(typeof(Billing.Caller.BillingService).GetFields(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
    }

    [Fact]
    public void Caller_RequiresHostBeforeConstruction()
    {
        GraftConfig.Host = null;
        var ex = Assert.Throws<InvalidOperationException>(() => new Billing.Caller.BillingService());
        Assert.Contains("GraftConfig.Host", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Caller_PropagatesReceiverValidation()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MonthlyBillCaller.Calculate(10d, -1));
    }

    [Fact]
    public void BeforeFiles_AreMarkedIllustrativeRest_AndCallerHasNoBillingField()
    {
        var exampleDir = Path.Combine(CopyRules.RepoRoot(), "example-2-before-after");
        var beforeDir = Path.Combine(exampleDir, "before-illustrative-rest");
        foreach (var file in Directory.GetFiles(beforeDir, "*.cs"))
        {
            var text = File.ReadAllText(file);
            Assert.Contains("Illustrative REST", text, StringComparison.Ordinal);
            Assert.Contains("not Graftcode", text, StringComparison.Ordinal);
        }

        var callerDir = Path.Combine(exampleDir, "after-caller");
        foreach (var file in Directory.GetFiles(callerDir, "*.cs"))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("_billing", text, StringComparison.Ordinal);
            Assert.DoesNotContain("HttpClient", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Socket", text, StringComparison.Ordinal);
        }

        var caller = File.ReadAllText(Path.Combine(callerDir, "MonthlyBillCaller.cs"));
        Assert.Contains("GraftConfig.Host = \"ws://billing.example/ws\"", caller, StringComparison.Ordinal);
        Assert.Contains("new BillingService()", caller, StringComparison.Ordinal);
        Assert.Contains("network call", caller, StringComparison.Ordinal);
        var hostIndex = caller.IndexOf("GraftConfig.Host", StringComparison.Ordinal);
        var newIndex = caller.IndexOf("new BillingService()", StringComparison.Ordinal);
        Assert.True(hostIndex >= 0 && newIndex > hostIndex);

        var standIn = File.ReadAllText(Path.Combine(callerDir, "LocalGraftStandIn.cs"));
        Assert.Contains("NOT a generated Graft", standIn, StringComparison.Ordinal);
        Assert.Contains("does not open a socket", standIn, StringComparison.Ordinal);
        Assert.Contains("global::Billing.BillingService", standIn, StringComparison.Ordinal);
    }

    [Fact]
    public void Layout_SeparatesIllustrativeRestFromReceiverAndCaller()
    {
        var exampleDir = Path.Combine(CopyRules.RepoRoot(), "example-2-before-after");
        Assert.Equal(
            ["Billing.IllustrativeRest.csproj", "BillingController.cs", "BillingHttpClient.cs", "MonthlyBillDtos.cs"],
            Names(Path.Combine(exampleDir, "before-illustrative-rest")));
        Assert.Equal(
            ["Billing.Receiver.csproj", "BillingService.cs", "Dockerfile"],
            Names(Path.Combine(exampleDir, "after-receiver")));
        Assert.Equal(
            ["Billing.Caller.csproj", "LocalGraftStandIn.cs", "MonthlyBillCaller.cs"],
            Names(Path.Combine(exampleDir, "after-caller")));
    }

    [Fact]
    public void Copy_AvoidsOverclaims()
    {
        CopyRules.AssertRepository(CopyRules.RepoRoot());
        var readme = File.ReadAllText(Path.Combine(CopyRules.RepoRoot(), "README.md"));
        Assert.Contains("example-2-before-after", readme, StringComparison.Ordinal);
        Assert.Contains("dotnet test", readme, StringComparison.Ordinal);
        Assert.Contains("network call", readme, StringComparison.OrdinalIgnoreCase);
    }

    private static string[] Names(string directory)
        => Directory.GetFiles(directory)
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .Cast<string>()
            .ToArray();

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _responseJson;

        public StubHandler(string responseJson)
        {
            _responseJson = responseJson;
        }

        public HttpRequestMessage? Request { get; private set; }

        public string Body { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseJson, Encoding.UTF8, "application/json"),
            };
        }
    }
}


