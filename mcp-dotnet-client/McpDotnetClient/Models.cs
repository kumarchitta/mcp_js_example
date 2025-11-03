using System.Text.Json.Serialization;

// ============================================================================
// DATA MODELS
// ============================================================================
public class McpTool
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public InputSchema InputSchema { get; set; } = new();
}

public class InputSchema
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "object";

    [JsonPropertyName("properties")]
    public Dictionary<string, PropertyDefinition> Properties { get; set; } = new();

    [JsonPropertyName("required")]
    public List<string> Required { get; set; } = new();
}

public class PropertyDefinition
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

public class ToolContent
{
    public string Type { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}
