using System.Net;
using System.Reflection;
using System.Text;
using Billing.GrpcContract;
using Billing.RestContract;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;

namespace Example3.Tests;

public class ContractTests
{
    [Fact]
    public void ThreeContracts_AgreeOnTheSameProduct()
    {
        Assert.Equal(50d, new Billing.Graftcode.BillingService().CalculateMonthlyBill(10d, 5));
        Assert.Equal(50m, new Billing.RestContract.BillingService().CalculateMonthlyBill(10m, 5));
        Assert.Equal(50m, new Billing.GrpcContract.BillingService().CalculateMonthlyBill(10m, 5));

        Assert.Equal(59.97d, new Billing.Graftcode.BillingService().CalculateMonthlyBill(19.99d, 3), 2);
        Assert.Equal(59.97m, new Billing.RestContract.BillingService().CalculateMonthlyBill(19.99m, 3));
        Assert.Equal(59.97m, new Billing.GrpcContract.BillingService().CalculateMonthlyBill(19.99m, 3));
    }

    [Fact]
    public void RestController_AssignsBillingService_AndMatchesOpenApiPath()
    {
        var service = new Billing.RestContract.BillingService();
        var controller = new BillingController(service);
        var field = typeof(BillingController).GetField("_billing", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        Assert.True(field.IsInitOnly);
        Assert.Same(service, field.GetValue(controller));

        var result = controller.CalculateMonthlyBill(new Billing.RestContract.MonthlyBillRequest { UnitPrice = 10m, Units = 5 });
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsType<Billing.RestContract.MonthlyBillResponse>(ok.Value);
        Assert.Equal(50m, body.Total);

        var exampleDir = Path.Combine(CopyRules.RepoRoot(), "example-3-contracts");
        var controllerSource = File.ReadAllText(Path.Combine(exampleDir, "rest", "BillingController.cs"));
        var openapi = File.ReadAllText(Path.Combine(exampleDir, "rest", "openapi.yaml"));
        Assert.Contains("[Route(\"api/v1/billing\")]", controllerSource, StringComparison.Ordinal);
        Assert.Contains("[HttpPost(\"monthly-bill\")]", controllerSource, StringComparison.Ordinal);
        Assert.Contains("/api/v1/billing/monthly-bill:", openapi, StringComparison.Ordinal);
        Assert.Contains("operationId: calculateMonthlyBill", openapi, StringComparison.Ordinal);
        Assert.Contains("MonthlyBillRequest:", openapi, StringComparison.Ordinal);
        Assert.Contains("MonthlyBillResponse:", openapi, StringComparison.Ordinal);
        Assert.Contains("Illustrative REST contract", openapi, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RestClient_PostsRouteAndDto()
    {
        var handler = new StubHandler("""{"total":50.00}""");
        using var http = new HttpClient(handler);

        var total = await BillingRestClient.CalculateMonthlyBillAsync(http, "token-1", 10m, 5);

        Assert.Equal(50m, total);
        Assert.Equal(
            "https://billing.example/api/v1/billing/monthly-bill",
            handler.Request!.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Request.Headers.Authorization!.Scheme);
        Assert.Contains("\"unitPrice\":10", handler.Body, StringComparison.Ordinal);
        Assert.Contains("\"units\":5", handler.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GrpcAdapter_UsesGeneratedBase_AndMapsStrings()
    {
        Assert.Equal("BillingApiBase", typeof(BillingGrpcService).BaseType!.Name);
        Assert.NotNull(typeof(BillingApi.BillingApiClient));

        var service = new BillingGrpcService(new Billing.GrpcContract.BillingService());
        var response = await service.CalculateMonthlyBill(
            new Billing.GrpcContract.MonthlyBillRequest { UnitPrice = "19.99", Units = 3 },
            null!);

        Assert.Equal("59.97", response.Total);

        var invalid = await Assert.ThrowsAsync<RpcException>(() => service.CalculateMonthlyBill(
            new Billing.GrpcContract.MonthlyBillRequest { UnitPrice = "nope", Units = 1 },
            null!));
        Assert.Equal(StatusCode.InvalidArgument, invalid.StatusCode);

        Assert.Throws<ArgumentOutOfRangeException>(() => new Billing.GrpcContract.BillingService()
            .CalculateMonthlyBill(1m, -1));
    }

    [Fact]
    public void Folders_KeepOnlyTheArtifactsEachPathMaintains()
    {
        var exampleDir = Path.Combine(CopyRules.RepoRoot(), "example-3-contracts");

        Assert.Equal(
            ["Billing.Graftcode.csproj", "BillingService.cs", "Dockerfile"],
            Names(Path.Combine(exampleDir, "graftcode")));
        Assert.Equal(
            ["Billing.RestContract.csproj", "BillingController.cs", "BillingRestClient.cs", "BillingService.cs", "MonthlyBillDtos.cs", "openapi.yaml"],
            Names(Path.Combine(exampleDir, "rest")));
        Assert.Equal(
            ["Billing.GrpcContract.csproj", "BillingGrpcService.cs", "BillingService.cs", "billing.proto"],
            Names(Path.Combine(exampleDir, "grpc")));

        var graft = File.ReadAllText(Path.Combine(exampleDir, "graftcode", "BillingService.cs"));
        Assert.Contains("public method signature", graft, StringComparison.Ordinal);
        Assert.DoesNotContain("[Route", graft, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpPost", graft, StringComparison.Ordinal);
        Assert.DoesNotContain("syntax = \"proto3\"", graft, StringComparison.Ordinal);
        Assert.DoesNotContain("HttpClient", graft, StringComparison.Ordinal);
        Assert.DoesNotContain("operationId", graft, StringComparison.Ordinal);

        var proto = File.ReadAllText(Path.Combine(exampleDir, "grpc", "billing.proto"));
        Assert.Contains("rpc CalculateMonthlyBill", proto, StringComparison.Ordinal);
        Assert.Contains("service BillingApi", proto, StringComparison.Ordinal);

        var grpcProject = File.ReadAllText(Path.Combine(exampleDir, "grpc", "Billing.GrpcContract.csproj"));
        Assert.Contains("Grpc.Tools", grpcProject, StringComparison.Ordinal);
        Assert.Contains("billing.proto", grpcProject, StringComparison.Ordinal);

        foreach (var file in Directory.GetFiles(Path.Combine(exampleDir, "rest"), "*.cs"))
        {
            if (Path.GetFileName(file) == "BillingService.cs")
            {
                continue;
            }

            var text = File.ReadAllText(file);
            Assert.Contains("Illustrative REST", text, StringComparison.Ordinal);
            Assert.Contains("not Graftcode", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Copy_AvoidsOverclaims()
    {
        CopyRules.AssertRepository(CopyRules.RepoRoot());
        var readme = File.ReadAllText(Path.Combine(CopyRules.RepoRoot(), "README.md"));
        Assert.Contains("example-3-contracts", readme, StringComparison.Ordinal);
        Assert.Contains("dotnet test", readme, StringComparison.Ordinal);
        Assert.Contains("does not publish comparative numbers", readme, StringComparison.Ordinal);
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
