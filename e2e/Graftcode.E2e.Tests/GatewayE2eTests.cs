using System.Net;
using Xunit.Sdk;

namespace Graftcode.E2e.Tests;

public class GatewayE2eTests
{
    [Fact(Timeout = 900_000)]
    public async Task Example1_McpListsFilteredStaticMethodsAndCallsThem()
    {
        if (!await DockerCli.IsDaemonAvailable())
        {
            throw SkipException.ForSkip(DockerCli.UnavailableReason);
        }

        var root = RepoRoot();
        await using var gateway = await RunningGateway.StartAsync(
            dockerfile: Path.Combine(root, "example-1-mcp-class", "host", "Dockerfile"),
            contextDirectory: Path.Combine(root, "example-1-mcp-class"),
            imageTag: "graftcode-sample-invoice-calculator:e2e",
            containerName: "graftcode-sample-e2e-mcp",
            CancellationToken.None);

        await WaitForStatusAsync(gateway);
        await using var mcp = new McpClient();
        await mcp.InitializeAsync(CancellationToken.None);
        var tools = await mcp.ListToolsAsync(CancellationToken.None);
        var names = tools.Select(tool => tool.Name).ToArray();

        var bill = RequireTool(tools, "CalculateMonthlyBill");
        var tax = RequireTool(tools, "ApplyTax");
        Assert.DoesNotContain(names, name => name.Contains("LateFee", StringComparison.Ordinal));
        Assert.DoesNotContain(names, name => name.Contains("FormatInvoice", StringComparison.Ordinal));

        // No Authorization header is set. Listing tools is not an authorization check.
        // --types and --methods selected the callable surface.
        var logs = await gateway.LogsAsync();
        Assert.Contains("Graftcode Gateway", logs, StringComparison.Ordinal);

        var billBody = await mcp.CallAsync(bill, [10m, 5m], CancellationToken.None);
        McpClient.AssertResultOrDecimalBinding(billBody, 50m, "CalculateMonthlyBill");
        var taxBody = await mcp.CallAsync(tax, [100m, 0.23m], CancellationToken.None);
        McpClient.AssertResultOrDecimalBinding(taxBody, 123m, "ApplyTax");
    }

    [Fact(Timeout = 900_000)]
    public async Task Example2_HostedReceiverAnswersCalculateMonthlyBill()
    {
        if (!await DockerCli.IsDaemonAvailable())
        {
            throw SkipException.ForSkip(DockerCli.UnavailableReason);
        }

        var root = RepoRoot();
        var caller = File.ReadAllText(Path.Combine(root, "example-2-before-after", "after-caller", "MonthlyBillCaller.cs"));
        var standIn = File.ReadAllText(Path.Combine(root, "example-2-before-after", "after-caller", "LocalGraftStandIn.cs"));
        Assert.DoesNotContain("_billing", caller, StringComparison.Ordinal);
        Assert.Contains("network call", caller, StringComparison.Ordinal);
        Assert.Contains("does not open a socket", standIn, StringComparison.Ordinal);

        await using var gateway = await RunningGateway.StartAsync(
            dockerfile: Path.Combine(root, "example-2-before-after", "after-receiver", "Dockerfile"),
            contextDirectory: Path.Combine(root, "example-2-before-after", "after-receiver"),
            imageTag: "graftcode-sample-billing-receiver:e2e",
            containerName: "graftcode-sample-e2e-receiver",
            CancellationToken.None);

        await WaitForStatusAsync(gateway);
        await using var mcp = new McpClient();
        await mcp.InitializeAsync(CancellationToken.None);
        var tools = await mcp.ListToolsAsync(CancellationToken.None);
        var bill = RequireTool(tools, "CalculateMonthlyBill");

        // The illustrative REST project is not hosted here.
        // This call goes to Gateway. A ws:// or wss:// Graft is still a network call.
        // No Graft package is installed and no package coordinate is copied from Vision.
        var logs = await gateway.LogsAsync();
        Assert.Contains("Graftcode Gateway", logs, StringComparison.Ordinal);
        var body = await mcp.CallAsync(bill, [19.99m, 3m], CancellationToken.None);
        McpClient.AssertResultOrDecimalBinding(body, 59.97m, "CalculateMonthlyBill");
    }

    [Fact(Timeout = 900_000)]
    public async Task Example3_GraftcodeMethodSignatureAnswersThroughGateway()
    {
        if (!await DockerCli.IsDaemonAvailable())
        {
            throw SkipException.ForSkip(DockerCli.UnavailableReason);
        }

        var root = RepoRoot();
        await using var gateway = await RunningGateway.StartAsync(
            dockerfile: Path.Combine(root, "example-3-contracts", "graftcode", "Dockerfile"),
            contextDirectory: Path.Combine(root, "example-3-contracts", "graftcode"),
            imageTag: "graftcode-sample-billing-contract:e2e",
            containerName: "graftcode-sample-e2e-contract",
            CancellationToken.None);

        await WaitForStatusAsync(gateway);
        await using var mcp = new McpClient();
        await mcp.InitializeAsync(CancellationToken.None);
        var tools = await mcp.ListToolsAsync(CancellationToken.None);
        var bill = RequireTool(tools, "CalculateMonthlyBill");
        var logs = await gateway.LogsAsync();
        Assert.Contains("Graftcode Gateway", logs, StringComparison.Ordinal);
        // Amounts are double and units int on the hosted method, so JSON numbers
        // bind on Gateway 1.4.14. The result must be a number, not a binding error.
        // The REST and gRPC hosts are not started here and are not MCP servers.
        var fifty = await mcp.CallAsync(bill, [10m, 5m], CancellationToken.None);
        McpClient.AssertResultOrDecimalBinding(fifty, 50m, "CalculateMonthlyBill");
        var body = await mcp.CallAsync(bill, [19.99m, 3m], CancellationToken.None);
        McpClient.AssertResultOrDecimalBinding(body, 59.97m, "CalculateMonthlyBill");
    }

    private static McpTool RequireTool(IReadOnlyList<McpTool> tools, string methodName)
    {
        var tool = tools.FirstOrDefault(candidate => candidate.Name.Contains(methodName, StringComparison.Ordinal));
        Assert.True(tool is not null, $"MCP tools/list did not include {methodName}. Names: {string.Join(", ", tools.Select(candidate => candidate.Name))}");
        return tool!;
    }

    private static async Task WaitForStatusAsync(RunningGateway gateway)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        Exception? last = null;
        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var response = await http.GetAsync("http://localhost:81/status");
                var body = (await response.Content.ReadAsStringAsync()).Trim();
                if (response.StatusCode == HttpStatusCode.OK && body == "OK")
                {
                    using var vision = await http.GetAsync("http://localhost:81/GV");
                    Assert.True(
                        vision.IsSuccessStatusCode,
                        $"Vision at http://localhost:81/GV returned {(int)vision.StatusCode}.");
                    return;
                }

                last = new InvalidOperationException($"GET /status returned {(int)response.StatusCode} body '{body}'.");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                last = ex;
            }

            await Task.Delay(1000);
        }

        var logs = await gateway.LogsAsync();
        throw new TimeoutException(
            $"Gateway did not serve GET http://localhost:81/status with body OK. Last error: {last}{Environment.NewLine}{logs}");
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "GraftcodeSamples.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not find GraftcodeSamples.sln.");
    }
}
