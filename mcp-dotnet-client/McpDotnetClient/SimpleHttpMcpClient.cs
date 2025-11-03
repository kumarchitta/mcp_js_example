using System.Text;
using System.Text.Json;

// ============================================================================
// SIMPLE HTTP MCP CLIENT
// ============================================================================
public class SimpleHttpMcpClient
{
    private readonly HttpClient _httpClient;
    private readonly string _url;
    private int _messageId = 0;

    public SimpleHttpMcpClient(string url)
    {
        _url = url;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json, text/event-stream");
    }

    private async Task<JsonElement> CallJsonRpcAsync(string method, object? parameters = null)
    {
        var request = new
        {
            jsonrpc = "2.0",
            id = ++_messageId,
            method = method,
            @params = parameters ?? new { }
        };

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(_url, content);

        if (!response.IsSuccessStatusCode)
        {
            var errorText = await response.Content.ReadAsStringAsync();
            throw new Exception($"HTTP {response.StatusCode}: {errorText}");
        }

        var responseText = await response.Content.ReadAsStringAsync();

        // Handle Server-Sent Events format
        if (responseText.StartsWith("event: message"))
        {
            var lines = responseText.Split('\n');
            foreach (var line in lines)
            {
                if (line.StartsWith("data: "))
                {
                    responseText = line.Substring(6);
                    break;
                }
            }
        }

        var result = JsonDocument.Parse(responseText);

        if (result.RootElement.TryGetProperty("error", out var error))
        {
            throw new Exception($"JSON-RPC Error: {error.GetProperty("message").GetString()}");
        }

        return result.RootElement.GetProperty("result");
    }

    public async Task<JsonElement> InitializeAsync()
    {
        var initParams = new
        {
            protocolVersion = "2024-11-05",
            clientInfo = new
            {
                name = "mcp-dotnet-client",
                version = "1.0.0"
            },
            capabilities = new { }
        };
        return await CallJsonRpcAsync("initialize", initParams);
    }

    public async Task<List<McpTool>> ListToolsAsync()
    {
        var result = await CallJsonRpcAsync("tools/list", new { });
        var toolsArray = result.GetProperty("tools");

        var tools = new List<McpTool>();
        foreach (var tool in toolsArray.EnumerateArray())
        {
            tools.Add(new McpTool
            {
                Name = tool.GetProperty("name").GetString() ?? string.Empty,
                Description = tool.GetProperty("description").GetString() ?? string.Empty,
                InputSchema = JsonSerializer.Deserialize<InputSchema>(tool.GetProperty("inputSchema").GetRawText()) ?? new InputSchema()
            });
        }

        return tools;
    }

    public async Task<List<ToolContent>> CallToolAsync(string name, Dictionary<string, object> args)
    {
        var result = await CallJsonRpcAsync("tools/call", new { name = name, arguments = args });
        var contentArray = result.GetProperty("content");

        var contents = new List<ToolContent>();
        foreach (var item in contentArray.EnumerateArray())
        {
            contents.Add(new ToolContent
            {
                Type = item.GetProperty("type").GetString() ?? string.Empty,
                Text = item.GetProperty("text").GetString() ?? string.Empty
            });
        }

        return contents;
    }
}
