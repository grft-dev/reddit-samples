// Local Kestrel host for the illustrative REST contract in ../rest - not Graftcode.
// It serves BillingController over plain HTTP so the e2e suite can call
// POST /api/v1/billing/monthly-bill over a socket. It is not an MCP server,
// and it does not bind the Gateway ports 80 or 81 unless you pass them.
//
//   dotnet run --project example-3-contracts/rest-host -- http://127.0.0.1:5080

using Billing.RestContract;

namespace Billing.RestHost;

public static class RestHost
{
    public const string DefaultUrl = "http://127.0.0.1:5080";

    // Pass http://127.0.0.1:0 for an ephemeral port; read the bound address
    // from WebApplication.Urls after StartAsync.
    public static WebApplication Build(string url)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(url);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services.AddSingleton<BillingService>();
        builder.Services
            .AddControllers()
            .AddApplicationPart(typeof(BillingController).Assembly);

        var app = builder.Build();
        app.MapControllers();
        return app;
    }

    public static async Task Main(string[] args)
    {
        var url = args.FirstOrDefault(arg => arg.StartsWith("http", StringComparison.OrdinalIgnoreCase)) ?? DefaultUrl;
        await using var app = Build(url);
        await app.StartAsync();
        Console.WriteLine($"Illustrative REST host listening on {string.Join(", ", app.Urls)}");
        await app.WaitForShutdownAsync();
    }
}
