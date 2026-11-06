using System.Text.Json;
using System.Text.RegularExpressions;
using Billing;

namespace Example1.Tests;

public class HostSnippetTests
{
    [Fact]
    public void GatewayCommand_UsesDocumentedFilterFlags_AndMcpEndpoint()
    {
        var exampleDir = Path.Combine(CopyRules.RepoRoot(), "example-1-mcp-class");
        var scriptFile = File.ReadAllText(Path.Combine(exampleDir, "host", "gg.sh"));
        var commandAt = scriptFile.IndexOf("gg ./InvoiceCalculator", StringComparison.Ordinal);
        Assert.True(commandAt >= 0);
        var script = scriptFile[commandAt..];
        var dockerfile = File.ReadAllText(Path.Combine(exampleDir, "host", "Dockerfile"));
        var mcp = JsonDocument.Parse(File.ReadAllText(Path.Combine(exampleDir, "host", "mcp.json")));

        Assert.Contains("http://localhost:81/mcp", scriptFile, StringComparison.Ordinal);
        Assert.Contains("do not authenticate or authorize", scriptFile, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("--types", script, StringComparison.Ordinal);
        Assert.Contains("--methods", script, StringComparison.Ordinal);
        Assert.Contains("--mcpBaseClass", script, StringComparison.Ordinal);

        var methods = FlagValue(script, "--methods").Split(',');
        Assert.Equal(["CalculateMonthlyBill", "ApplyTax"], methods);
        Assert.Equal("Billing.InvoiceCalculator", FlagValue(script, "--types"));
        Assert.Equal(typeof(InvoiceCalculator).FullName, FlagValue(script, "--types"));
        Assert.Equal(typeof(InvoiceCalculator).FullName, FlagValue(script, "--mcpBaseClass"));
        Assert.Contains("InvoiceCalculator/bin/Debug/net9.0/InvoiceCalculator.dll", script, StringComparison.Ordinal);

        Assert.DoesNotContain("LateFee", methods);
        Assert.DoesNotContain("FormatInvoice", methods);
        Assert.DoesNotContain("RoundForLedger", methods);

        foreach (var name in methods)
        {
            var method = typeof(InvoiceCalculator).GetMethod(name);
            Assert.NotNull(method);
            Assert.True(method.IsPublic);
            Assert.True(method.IsStatic);
        }

        Assert.Contains("\"--types\", \"Billing.InvoiceCalculator\"", dockerfile, StringComparison.Ordinal);
        Assert.Contains("\"--methods\", \"CalculateMonthlyBill,ApplyTax\"", dockerfile, StringComparison.Ordinal);
        Assert.Contains("EXPOSE 81", dockerfile, StringComparison.Ordinal);
        Assert.Contains("not authorization", dockerfile, StringComparison.OrdinalIgnoreCase);

        var url = mcp.RootElement
            .GetProperty("mcpServers")
            .GetProperty("invoice-calculator")
            .GetProperty("url")
            .GetString();
        Assert.Equal("http://localhost:81/mcp", url);
    }

    [Fact]
    public void SampleLayout_IsTheClassPlusTheHostSnippets()
    {
        var exampleDir = Path.Combine(CopyRules.RepoRoot(), "example-1-mcp-class");
        Assert.Equal(
            ["InvoiceCalculator.cs", "InvoiceCalculator.csproj"],
            FileNames(Path.Combine(exampleDir, "InvoiceCalculator")));
        Assert.Equal(
            ["Dockerfile", "gg.sh", "mcp.json"],
            FileNames(Path.Combine(exampleDir, "host")));
    }

    [Fact]
    public void Copy_AvoidsOverclaims()
    {
        CopyRules.AssertRepository(CopyRules.RepoRoot());
        var readme = File.ReadAllText(Path.Combine(CopyRules.RepoRoot(), "README.md"));
        Assert.Contains("dotnet test", readme, StringComparison.Ordinal);
        Assert.Contains("example-1-mcp-class", readme, StringComparison.Ordinal);
        Assert.Contains("static methods are also MCP tools", readme, StringComparison.Ordinal);
    }

    private static string FlagValue(string script, string flag)
    {
        var match = Regex.Match(script, Regex.Escape(flag) + @"\s+(\S+)");
        Assert.True(match.Success, $"Missing {flag} in gg.sh.");
        return match.Groups[1].Value;
    }

    private static string[] FileNames(string directory)
        => Directory.GetFiles(directory)
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .Cast<string>()
            .ToArray();
}
