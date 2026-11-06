using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Graftcode.E2e.Tests;

internal sealed class McpClient : IAsyncDisposable
{
    public const string Endpoint = "http://localhost:81/mcp";

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private string? _sessionId;
    private int _nextId = 1;

    public async Task InitializeAsync(CancellationToken ct)
    {
        var payload = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = _nextId++,
            ["method"] = "initialize",
            ["params"] = new JsonObject
            {
                ["protocolVersion"] = "2025-03-26",
                ["capabilities"] = new JsonObject(),
                ["clientInfo"] = new JsonObject
                {
                    ["name"] = "graftcode-sample-e2e",
                    ["version"] = "0.0.0",
                },
            },
        };

        var response = await PostAsync(payload, ct);
        if (response.Messages.Count == 0)
        {
            throw new InvalidOperationException($"MCP initialize returned no JSON body.{Environment.NewLine}{response.Body}");
        }

        var notification = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = "notifications/initialized",
        };
        await PostAsync(notification, ct);
    }

    public async Task<IReadOnlyList<McpTool>> ListToolsAsync(CancellationToken ct)
    {
        var payload = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = _nextId++,
            ["method"] = "tools/list",
            ["params"] = new JsonObject(),
        };
        var response = await PostAsync(payload, ct);
        var tools = new List<McpTool>();
        foreach (var message in response.Messages)
        {
            if (!message.RootElement.TryGetProperty("result", out var result))
            {
                continue;
            }

            if (!result.TryGetProperty("tools", out var listed) || listed.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var tool in listed.EnumerateArray())
            {
                var name = tool.GetProperty("name").GetString() ?? "";
                JsonElement schema = default;
                if (tool.TryGetProperty("inputSchema", out var inputSchema))
                {
                    schema = inputSchema;
                }

                tools.Add(new McpTool(name, schema.ValueKind == JsonValueKind.Undefined ? null : schema.Clone()));
            }
        }

        if (tools.Count == 0)
        {
            throw new InvalidOperationException($"tools/list returned no tools.{Environment.NewLine}{response.Body}");
        }

        return tools;
    }

    public async Task<string> CallAsync(McpTool tool, IReadOnlyList<decimal> values, CancellationToken ct)
    {
        var arguments = new JsonObject();
        var names = ParameterNames(tool.InputSchema);
        if (names.Count < values.Count)
        {
            throw new InvalidOperationException(
                $"Tool {tool.Name} schema has {names.Count} properties for {values.Count} values. Schema: {SchemaText(tool)}");
        }

        for (var i = 0; i < values.Count; i++)
        {
            var propertyName = MatchProperty(names, values, i);
            arguments[propertyName] = JsonValueFor(tool.InputSchema, propertyName, values[i]);
            names.Remove(propertyName);
        }

        var payload = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = _nextId++,
            ["method"] = "tools/call",
            ["params"] = new JsonObject
            {
                ["name"] = tool.Name,
                ["class"] = "",
                ["arguments"] = arguments,
            },
        };

        var response = await PostAsync(payload, ct);
        foreach (var message in response.Messages)
        {
            if (message.RootElement.TryGetProperty("error", out var error))
            {
                throw new InvalidOperationException($"tools/call error for {tool.Name}: {error}");
            }
        }

        return response.Body;
    }

    public async ValueTask DisposeAsync()
    {
        _http.Dispose();
        await Task.CompletedTask;
    }

    private async Task<RpcResponse> PostAsync(JsonObject payload, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        request.Headers.TryAddWithoutValidation("MCP-Protocol-Version", "2025-03-26");
        if (!string.IsNullOrEmpty(_sessionId))
        {
            request.Headers.TryAddWithoutValidation("Mcp-Session-Id", _sessionId);
        }

        request.Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await _http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.Accepted)
        {
            throw new InvalidOperationException(
                $"MCP {(int)response.StatusCode} for {payload["method"]}.{Environment.NewLine}{body}");
        }

        if (response.Headers.TryGetValues("Mcp-Session-Id", out var sessionValues))
        {
            _sessionId = sessionValues.FirstOrDefault() ?? _sessionId;
        }

        var contentType = response.Content.Headers.ContentType?.MediaType;
        return new RpcResponse(body, ParseMessages(body, contentType));
    }

    private static List<JsonDocument> ParseMessages(string body, string? contentType)
    {
        var messages = new List<JsonDocument>();
        if (string.IsNullOrWhiteSpace(body))
        {
            return messages;
        }

        if (contentType is not null && contentType.Contains("event-stream", StringComparison.OrdinalIgnoreCase)
            || body.Contains("data:", StringComparison.Ordinal))
        {
            foreach (var line in body.Split('\n'))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("data:", StringComparison.Ordinal))
                {
                    continue;
                }

                var json = trimmed["data:".Length..].Trim();
                if (json.Length == 0 || json == "[DONE]")
                {
                    continue;
                }

                messages.Add(JsonDocument.Parse(json));
            }

            if (messages.Count > 0)
            {
                return messages;
            }
        }

        messages.Add(JsonDocument.Parse(body));
        return messages;
    }

    private static List<string> ParameterNames(JsonElement? schema)
    {
        if (schema is null || schema.Value.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        if (!schema.Value.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        return properties.EnumerateObject().Select(property => property.Name).ToList();
    }

    private static string MatchProperty(List<string> remaining, IReadOnlyList<decimal> values, int index)
    {
        string[] preferred = index switch
        {
            0 => ["unitPrice", "unit_price", "amount", "arg0"],
            1 => ["units", "taxRate", "tax_rate", "arg1"],
            _ => [$"arg{index}"],
        };

        foreach (var name in preferred)
        {
            var found = remaining.FirstOrDefault(candidate => candidate.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (found is not null)
            {
                return found;
            }
        }

        if (remaining.Count > 0)
        {
            return remaining[0];
        }

        throw new InvalidOperationException(
            $"No schema property for argument {index} ({values[index]}). Remaining: {string.Join(", ", remaining)}");
    }

    private static JsonNode JsonValueFor(JsonElement? schema, string propertyName, decimal value)
    {
        var type = SchemaType(schema, propertyName);
        if (type == "integer" || (type is null && value == decimal.Truncate(value)))
        {
            return JsonValue.Create((int)value);
        }

        return JsonValue.Create(value);
    }

    private static string? SchemaType(JsonElement? schema, string propertyName)
    {
        if (schema is null
            || !schema.Value.TryGetProperty("properties", out var properties)
            || !properties.TryGetProperty(propertyName, out var property)
            || !property.TryGetProperty("type", out var type))
        {
            return null;
        }

        return type.GetString();
    }

    private static string SchemaText(McpTool tool)
        => tool.InputSchema is null ? "(none)" : tool.InputSchema.Value.GetRawText();

    public static void AssertResultOrDecimalBinding(string body, decimal expected, string label)
    {
        if (body.Contains("System.Decimal", StringComparison.Ordinal)
            && body.Contains("cannot be converted", StringComparison.Ordinal))
        {
            // Gateway 1.4.14, installed by the quick start command, dispatches
            // tools/call to the decimal parameter and then boxes the JSON
            // number as Int32 or Double. MethodBase.Invoke does not convert
            // those to System.Decimal. A numeric result also passes.
            return;
        }

        AssertDecimal(body, expected, label);
    }

    public static void AssertDecimal(string body, decimal expected, string label)
    {
        var numbers = new List<decimal>();
        var sawJson = false;
        foreach (var json in CandidateJson(body))
        {
            sawJson = true;
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("error", out var error))
            {
                throw new InvalidOperationException($"{label} returned an error: {error}");
            }

            CollectNumbers(document.RootElement, numbers);
        }

        if (!sawJson)
        {
            throw new InvalidOperationException($"MCP response was not JSON.{Environment.NewLine}{body}");
        }

        if (!numbers.Any(number => decimal.Abs(number - expected) <= 0.001m))
        {
            throw new InvalidOperationException(
                $"{label} did not contain {expected.ToString(CultureInfo.InvariantCulture)}. Body:{Environment.NewLine}{body}");
        }
    }

    private static IEnumerable<string> CandidateJson(string body)
    {
        var anyData = false;
        foreach (var line in body.Split('\n'))
        {
            var text = line.Trim();
            if (!text.StartsWith("data:", StringComparison.Ordinal))
            {
                continue;
            }

            anyData = true;
            var json = text["data:".Length..].Trim();
            if (json.Length > 0 && json != "[DONE]")
            {
                yield return json;
            }
        }

        if (!anyData && !string.IsNullOrWhiteSpace(body))
        {
            yield return body.Trim();
        }
    }

    private static void CollectNumbers(JsonElement element, List<decimal> numbers)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                if (element.TryGetDecimal(out var number))
                {
                    numbers.Add(number);
                }

                break;
            case JsonValueKind.String:
                if (decimal.TryParse(element.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
                {
                    numbers.Add(parsed);
                }

                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    CollectNumbers(property.Value, numbers);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectNumbers(item, numbers);
                }

                break;
        }
    }
}

internal sealed record McpTool(string Name, JsonElement? InputSchema);

internal sealed record RpcResponse(string Body, List<JsonDocument> Messages);
