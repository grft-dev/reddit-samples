// Local Kestrel host for the gRPC contract in ../grpc - not Graftcode.
// It maps BillingGrpcService (generated from billing.proto) over plaintext
// HTTP/2 on TCP so the e2e suite can call BillingApi.CalculateMonthlyBill
// through a real channel. It is not an MCP server, and it does not bind the
// Gateway ports 80 or 81 unless you pass them.
//
//   dotnet run --project example-3-contracts/grpc-host -- http://127.0.0.1:5081

using Billing.GrpcContract;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Billing.GrpcHost;

public static class GrpcHost
{
    public const string DefaultUrl = "http://127.0.0.1:5081";

    // Pass http://127.0.0.1:0 for an ephemeral port; read the bound address
    // from WebApplication.Urls after StartAsync.
    public static WebApplication Build(string url)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(url);
        builder.WebHost.ConfigureKestrel(kestrel =>
            kestrel.ConfigureEndpointDefaults(listen => listen.Protocols = HttpProtocols.Http2));
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services.AddGrpc();
        builder.Services.AddSingleton<BillingService>();

        var app = builder.Build();
        app.MapGrpcService<BillingGrpcService>();
        return app;
    }

    public static async Task Main(string[] args)
    {
        var url = args.FirstOrDefault(arg => arg.StartsWith("http", StringComparison.OrdinalIgnoreCase)) ?? DefaultUrl;
        await using var app = Build(url);
        await app.StartAsync();
        Console.WriteLine($"gRPC host (h2c) listening on {string.Join(", ", app.Urls)}");
        await app.WaitForShutdownAsync();
    }
}
