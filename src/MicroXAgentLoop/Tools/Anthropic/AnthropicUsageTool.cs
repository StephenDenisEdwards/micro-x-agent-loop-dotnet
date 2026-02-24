using System.Text.Json;
using System.Text.Json.Nodes;
using MicroXAgentLoop.Tools;

namespace MicroXAgentLoop.Tools.Anthropic;

public class AnthropicUsageTool : ITool
{
    private const string BaseUrl = "https://api.anthropic.com/v1/organizations";

    private static readonly Dictionary<string, string> Endpoints = new()
    {
        ["usage"] = "/usage_report/messages",
        ["cost"] = "/cost_report",
        ["claude_code"] = "/usage_report/claude_code",
    };

    private static readonly Dictionary<string, string> ReportLabels = new()
    {
        ["usage"] = "Token Usage Report",
        ["cost"] = "Cost Report",
        ["claude_code"] = "Claude Code Usage Report",
    };

    private readonly string _adminKey;
    private static HttpClient Http => HttpClientFactory.Api;

    public AnthropicUsageTool(string adminApiKey)
    {
        _adminKey = adminApiKey;
    }

    public string Name => "anthropic_usage";

    public string Description =>
        "Query Anthropic Admin API for organization usage and cost reports. " +
        "Supports three actions: 'usage' (token-level usage), 'cost' (spend in USD, converted from cents), " +
        "'claude_code' (Claude Code productivity metrics).";

    private static readonly JsonNode Schema = JsonNode.Parse("""
        {
            "type": "object",
            "properties": {
                "action": {
                    "type": "string",
                    "enum": ["usage", "cost", "claude_code"],
                    "description": "Which report: 'usage' (token usage), 'cost' (spend in USD), 'claude_code' (productivity metrics)"
                },
                "starting_at": {
                    "type": "string",
                    "description": "Start time — RFC 3339 for usage/cost (e.g. '2025-02-01T00:00:00Z'), YYYY-MM-DD for claude_code"
                },
                "ending_at": {
                    "type": "string",
                    "description": "Optional end time (same format as starting_at)"
                },
                "bucket_width": {
                    "type": "string",
                    "description": "Time granularity: '1m', '1h', or '1d' (usage supports all three; cost only supports '1d')"
                },
                "group_by": {
                    "type": "array",
                    "items": { "type": "string" },
                    "description": "Group results by fields (e.g. ['model', 'workspace_id'] for usage; ['workspace_id', 'description'] for cost)"
                },
                "limit": {
                    "type": "number",
                    "description": "Max number of time buckets / records to return"
                }
            },
            "required": ["action", "starting_at"]
        }
        """)!;

    public JsonNode InputSchema => Schema;

    public async Task<string> ExecuteAsync(JsonNode input, CancellationToken ct = default)
    {
        try
        {
            var action = input["action"]!.GetValue<string>();
            if (!Endpoints.TryGetValue(action, out var endpoint))
                return $"Unknown action '{action}'. Must be one of: usage, cost, claude_code.";

            var url = BaseUrl + endpoint;

            var queryParams = new List<string>
            {
                $"starting_at={Uri.EscapeDataString(input["starting_at"]!.GetValue<string>())}",
            };

            if (input["ending_at"] is not null)
                queryParams.Add($"ending_at={Uri.EscapeDataString(input["ending_at"]!.GetValue<string>())}");

            if (input["bucket_width"] is not null)
                queryParams.Add($"bucket_width={Uri.EscapeDataString(input["bucket_width"]!.GetValue<string>())}");

            if (input["group_by"] is JsonArray groupBy)
                foreach (var field in groupBy)
                    queryParams.Add($"group_by[]={Uri.EscapeDataString(field!.GetValue<string>())}");

            if (input["limit"] is not null)
                queryParams.Add($"limit={input["limit"]!.GetValue<int>()}");

            var fullUrl = $"{url}?{string.Join("&", queryParams)}";

            using var request = new HttpRequestMessage(HttpMethod.Get, fullUrl);
            request.Headers.Add("x-api-key", _adminKey);
            request.Headers.Add("anthropic-version", "2023-06-01");

            using var response = await Http.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                return $"Anthropic Admin API error: HTTP {(int)response.StatusCode} — {errorBody}";
            }

            var body = await response.Content.ReadAsStringAsync();
            var label = ReportLabels[action];

            if (action == "cost")
            {
                var node = JsonNode.Parse(body);
                if (node is not null)
                {
                    ConvertCostAmounts(node);
                    body = node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                }
            }
            else
            {
                // Pretty-print the JSON
                var element = JsonSerializer.Deserialize<JsonElement>(body);
                body = JsonSerializer.Serialize(element, new JsonSerializerOptions { WriteIndented = true });
            }

            return $"{label}:\n{body}";
        }
        catch (Exception ex)
        {
            return $"Error querying Anthropic Admin API: {ex.Message}";
        }
    }

    /// <summary>
    /// Convert 'amount' fields from cents to dollars in-place, recursively.
    /// The Anthropic cost API returns amounts in lowest currency units (cents).
    /// </summary>
    private static void ConvertCostAmounts(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            if (obj.ContainsKey("amount") && obj.ContainsKey("currency"))
            {
                if (double.TryParse(obj["amount"]!.ToString(), out var cents))
                {
                    obj.Remove("amount");
                    obj["amount_usd"] = Math.Round(cents / 100, 2);
                }
            }

            foreach (var kvp in obj.ToList())
            {
                if (kvp.Value is JsonObject or JsonArray)
                    ConvertCostAmounts(kvp.Value);
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item is JsonObject or JsonArray)
                    ConvertCostAmounts(item);
            }
        }
    }
}
