# Graftcode ad samples

Screenshot-ready C# samples for three Graftcode creatives. The code follows the public docs at <https://docs.graftcode.com>. It does not publish a package, install a generated Graft, or state a throughput or latency comparison.

Projects target `net9.0`. `global.json` uses `rollForward: latestMajor`, so the .NET 10 SDK can build them.

## Run the unit tests

These tests do not need Docker and do not start Graftcode Gateway.

```bash
dotnet test GraftcodeSamples.sln
```

One example at a time:

```bash
dotnet test example-1-mcp-class/Example1.Tests
dotnet test example-2-before-after/Example2.Tests
dotnet test example-3-contracts/Example3.Tests
```

What they do:

- Compile every sample project in the solution.
- Check method results, the illustrative HTTP client against a stub handler, and the gRPC adapter against the service base `Grpc.Tools` generates during the build.
- Check that the sample files contain the documented flags, the MCP URL, the illustrative-REST marker, and the Caller wiring.
- Reject copy this repo will not show: a callable-surface filter described as authentication, and any throughput or latency comparison.

What they do not do:

- They do not execute `example-1-mcp-class/host/gg.sh`.
- They do not build the Dockerfiles.
- They do not open `http://localhost:81/mcp` or install a generated Graft.
- `GraftConfig` and `BillingService` under `example-2-before-after/after-caller` are a local stand-in so the call site compiles. The stand-in calls the Receiver in-process. It does not open a socket. A `ws://` or `wss://` host on a real installed Graft is still a network call.

## Run the e2e tests

Requirements:

- Docker, with a running daemon.
- Network access during `docker build`. Graftcode does not publish a Gateway image to `docker pull` ([Deploy Gateway with Docker](https://docs.graftcode.com/how-to-guides/deploy-gateway-with-docker), [Run Gateway locally](https://docs.graftcode.com/how-to-guides/run-gateway-locally)). Each Dockerfile starts from `mcr.microsoft.com/dotnet/sdk:9.0` and installs `gg` with the [.NET MCP quick start](https://docs.graftcode.com/quick-start/expose-mcp/dotnet) command: `curl -fsSL grft.dev/get/gg | sh && dpkg -i gg.deb`.
- Host ports `80` and `81` free. The quick start maps `-p 80:80 -p 81:81`. Port `80` is the WebSocket runtime port. Port `81` is the Vision HTTP port. MCP is `http://localhost:81/mcp`. Liveness is `GET http://localhost:81/status` (body `OK`). Vision is `http://localhost:81/GV`.

```bash
dotnet test e2e/Graftcode.E2e.Tests/Graftcode.E2e.Tests.csproj
```

The e2e project is not in `GraftcodeSamples.sln`, so the unit command above does not build images. If `docker info` fails, each e2e test is skipped instead of failing.

What the e2e starts:

- Example 1 builds `example-1-mcp-class/host/Dockerfile` and calls `http://localhost:81/mcp`. `tools/list` must include `CalculateMonthlyBill` and `ApplyTax`, and must not include `LateFee` or `FormatInvoice`. `tools/call` uses the argument names from that tool's `inputSchema` (the Java MCP quick start says not to assume source parameter names). No `Authorization` header is sent. The filter is not authorization. On the `gg` build installed by the quick start command here (Gateway 1.4.14), `tools/call` reached the `decimal` parameters and then failed inside the CLR because the JSON number was boxed as `Int32` or `Double`. The test accepts that observed binding error, and it also accepts a numeric result if a later build converts the value.
- Example 2 builds only `example-2-before-after/after-receiver/Dockerfile` and calls `CalculateMonthlyBill` through that Gateway. The illustrative REST project is not started. `LocalGraftStandIn` is still not a generated Graft and still does not open a socket. The hosted call is a real Gateway call. A `ws://` or `wss://` Graft remains a network call. The Caller source still has no `_billing` field.
- Example 3 builds only `example-3-contracts/graftcode/Dockerfile` and calls that method through Gateway. The REST and gRPC fixtures are not started as servers.

What the e2e does not do:

- It does not pass `--projectKey` or `GC_PROJECT_KEY`.
- It does not install a Graft and does not write down a package name, version, or registry URL. Copy those from Vision for the process you are looking at. The CLI default `--endpoint` is `https://grft.dev`; this suite does not override it.
- It does not run ads.
- Local image tags such as `graftcode-sample-invoice-calculator:e2e` are tags this suite passes to `docker build`. They are not images Graftcode publishes.

The three e2e tests share ports `80` and `81`, so the project disables parallel test execution.

## Example 1 — MCP from a class

`example-1-mcp-class/`

`InvoiceCalculator` is an ordinary class: three public static methods, one public instance method, and a private rounding helper. The class has no MCP attributes and no Graftcode types.

`host/gg.sh` is the terminal snippet. The flags match [Filter the callable surface](https://docs.graftcode.com/how-to-guides/filter-the-callable-surface):

```bash
gg ./InvoiceCalculator/bin/Debug/net9.0/InvoiceCalculator.dll \
  --types Billing.InvoiceCalculator \
  --methods CalculateMonthlyBill,ApplyTax \
  --mcpBaseClass Billing.InvoiceCalculator
```

The docs' own example is `gg ./Pricing/bin/Debug/net9.0/Pricing.dll --types Pricing.PriceService --methods Calculate`. This sample uses the same flags on its own assembly.

`--mcpBaseClass` is from [Expose Receiver methods for MCP](https://docs.graftcode.com/how-to-guides/expose-receiver-methods-for-mcp). Use it when `tools/call` sends a bare method name and `params.class` is empty.

`host/mcp.json` points at `http://localhost:81/mcp`, the URL in the [.NET MCP quick start](https://docs.graftcode.com/quick-start/expose-mcp/dotnet). Port 81 is the default Vision HTTP port (`--httpPort` in the [Gateway CLI reference](https://docs.graftcode.com/reference/gateway-cli-reference)). That quick start also serves Graftcode Vision at `http://localhost:81/GV`.

The command names `CalculateMonthlyBill` and `ApplyTax`. `LateFee` is public and static and is left off the command. `FormatInvoice` is a public instance method and is also left off. Filter matching differs by runtime. Confirm the hosted surface in Vision before installing a Graft. This repo does not guess which members a given Gateway build will list.

### Honesty notes

- `--types` and `--methods` select the callable surface. [Callable surface](https://docs.graftcode.com/core-concepts/callable-surface) says that surface is an exposure boundary, not an authorization boundary. The flags do not authenticate or authorize a caller. Invocation auth is application-defined: [Authentication and authorization operations](https://docs.graftcode.com/operations/authentication-and-authorization-operations).
- The .NET MCP quick start says public methods are exposed as Grafts and that static methods are also MCP tools. Other sentences on that page, and the introduction, describe the MCP surface more broadly. Those sentences do not match. This sample follows the static-method sentence and does not describe instance members as MCP tools. [Known limitations](https://docs.graftcode.com/reference/known-limitations) say MCP scope depends on the runtime and configuration.
- A JavaScript caller cannot represent every `decimal` exactly ([known limitations](https://docs.graftcode.com/reference/known-limitations)). This class uses `decimal` because it is a billing type. That is not a promise that the same signature is portable to every Caller.

## Example 2 — Frontend to backend, before and after

`example-2-before-after/`

This is the introduction's worked example on [What is Graftcode?](https://docs.graftcode.com/introduction/what-is-graftcode): illustrative REST on one side, a public Receiver method and a Caller that uses the installed Graft like local code on the other. That snippet is C# on both sides, which is why this example is C# even though the scenario list calls the same idea frontend to backend.

The React quick start is a different page. It installs a generated npm package, sets `GraftConfig.host`, and calls `BillingLogic.calculateMonthlyBill` with three arguments and a camelCase name. This repo does not recreate that app, and the two signatures are not the same method.

### Before — illustrative REST, not Graftcode

`before-illustrative-rest/`

- `BillingController` — route `POST /api/v1/billing/monthly-bill`
- `MonthlyBillDtos` — request and response types
- `BillingHttpClient` — handwritten client

The controller's `_billing` field is assigned in the constructor and used by the action. It is the Receiver, not a Graft.

### After — Receiver and Caller

`after-receiver/BillingService.cs` is the introduction's method: `unitPrice * units`. The sample rejects negative `units`. It does not round. Example 1's invoice calculator rounds to cents; that is a different class.

`after-caller/MonthlyBillCaller.cs` is the introduction's call shape:

```csharp
GraftConfig.Host = "ws://billing.example/ws"; // before the first call
var billing = new BillingService();
return billing.CalculateMonthlyBill(unitPrice, units);
```

The host string is the introduction's example. Copy the real host from Vision. For a network hop, the operations guide says to use `wss://`.

`LocalGraftStandIn.cs` is not a generated package. [Package generation](https://docs.graftcode.com/core-concepts/package-generation) says package names come from the running Gateway or Vision and does not invent a naming formula. The stand-in stores `Host`, requires it before construction, and then calls the Receiver in-process. A remote Graft is still a network call. Setting `Host` to `ws://billing.example/ws` in this repo does not dial that address.

There is no `_billing` field on the Caller.

## Example 3 — Contract: REST vs gRPC vs Graftcode

`example-3-contracts/`

The same operation, `CalculateMonthlyBill(decimal, int)`, and the artifacts each path asks you to keep. This follows the comparison on the introduction and on [Use Graftcode alongside an existing REST API](https://docs.graftcode.com/how-to-guides/use-graftcode-alongside-an-existing-rest-api): REST and gRPC keep a protocol contract separate from the method; with Graftcode the supported public method surface is the contract and the installed Graft is the client.

| Path | What the sample keeps |
| --- | --- |
| `rest/` | Route, DTO, `openapi.yaml`, handwritten client |
| `grpc/` | `billing.proto` and the `Grpc.Tools` codegen item. `BillingGrpcService` maps proto strings onto the method. The client and service base are generated at build time. |
| `graftcode/` | The public method only. No route, DTO, OpenAPI document, or `.proto`. |

The three copies of the method are independent so each folder can be screenshotted on its own. The tests check that `10 * 5` and `19.99 * 3` agree.

Money on the REST document is a JSON number, which is not an exact decimal type. Money on the proto is a string so the fraction is not a binary float. The Graftcode method uses `decimal` directly. Those are the contracts as written, not a ranking.

The docs say a call still depends on the runtime pair, execution mode, payload, transport, topology, and workload, and that the documentation does not publish comparative numbers. This repo does not add any.

## Docs this repo is aligned with

- <https://docs.graftcode.com/introduction/what-is-graftcode>
- <https://docs.graftcode.com/how-to-guides/filter-the-callable-surface>
- <https://docs.graftcode.com/core-concepts/callable-surface>
- <https://docs.graftcode.com/how-to-guides/expose-receiver-methods-for-mcp>
- <https://docs.graftcode.com/quick-start/expose-mcp/dotnet>
- <https://docs.graftcode.com/reference/gateway-cli-reference>
- <https://docs.graftcode.com/reference/known-limitations>
- <https://docs.graftcode.com/core-concepts/package-generation>
- <https://docs.graftcode.com/how-to-guides/use-graftcode-alongside-an-existing-rest-api>
- <https://docs.graftcode.com/operations/authentication-and-authorization-operations>
- <https://docs.graftcode.com/reference/project-key-registry-host-and-credentials>
- <https://docs.graftcode.com/quick-start>
- <https://docs.graftcode.com/how-to-guides/deploy-gateway-with-docker>
- <https://docs.graftcode.com/how-to-guides/run-gateway-locally>
- <https://docs.graftcode.com/reference/ports-and-protocols-reference>
- <https://docs.graftcode.com/operations/health-checks>
- <https://docs.graftcode.com/quick-start/expose-mcp/java>
