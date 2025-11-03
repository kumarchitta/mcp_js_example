using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.AI;
using Azure;
using Azure.AI.OpenAI;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

// ============================================================================
// CONFIGURATION
// ============================================================================
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var modelProvider = configuration["MODEL_PROVIDER"] ?? "openai";
var mcpServerUrl = configuration["MCP_SERVER_URL"] ?? "http://localhost:8080/mcp";

Console.WriteLine($"🔷 Using {modelProvider} model");

// Setup LLM
IChatClient llm;
if (modelProvider == "azure")
{
    var azureApiKey = configuration["AZURE_API_KEY"] ?? throw new Exception("AZURE_API_KEY not configured");
    var azureEndpoint = configuration["AZURE_ENDPOINT"] ?? throw new Exception("AZURE_ENDPOINT not configured");
    var deploymentName = configuration["AZURE_DEPLOYMENT_NAME"] ?? "gpt-4o";

    var client = new AzureOpenAIClient(new Uri(azureEndpoint), new AzureKeyCredential(azureApiKey));
    var chatClient = client.GetChatClient(deploymentName);
    llm = chatClient.AsIChatClient();
    Console.WriteLine($"Using Azure OpenAI model: {deploymentName}");
}
else
{
    var openAiApiKey = configuration["OPENAI_API_KEY"] ?? throw new Exception("OPENAI_API_KEY not configured");
    var client = new OpenAI.OpenAIClient(openAiApiKey);
    var chatClient = client.GetChatClient("gpt-4o-mini");
    llm = chatClient.AsIChatClient();
    Console.WriteLine("Using OpenAI model: gpt-4o-mini");
}

// Initialize MCP client
var mcpClient = new SimpleHttpMcpClient(mcpServerUrl);
await mcpClient.InitializeAsync();

// List available tools
var mcpTools = await mcpClient.ListToolsAsync();
Console.WriteLine($"\n✅ Loaded {mcpTools.Count} tools from MCP server:");
foreach (var tool in mcpTools)
{
    Console.WriteLine($"   • {tool.Name} — {tool.Description}");
}

// Convert MCP tools to AIFunction tools
// For now, using a simpler approach - just wrap the MCP tool call
var aiFunctions = mcpTools.Select(mcpTool =>
{
    // Note: schema is here for reference, not currently used by AIFunctionFactory.Create

    return AIFunctionFactory.Create(
        async (int? age, int? comorbidities, string? patientId) =>
        {
            // Build parameters dictionary from actual arguments
            var parameters = new Dictionary<string, object>();
            if (age.HasValue) parameters["age"] = age.Value;
            if (comorbidities.HasValue) parameters["comorbidities"] = comorbidities.Value;
            if (!string.IsNullOrEmpty(patientId)) parameters["patientId"] = patientId;

            var result = await mcpClient.CallToolAsync(mcpTool.Name, parameters);
            return result.FirstOrDefault()?.Text ?? JsonSerializer.Serialize(result);
        },
        mcpTool.Name,
        mcpTool.Description
    );
}).ToList();

// System prompt
var systemPrompt = @"You are a helpful clinical assistant with access to patient data and risk assessment tools.

Available Tools:
- calculate_risk_score: Computes clinical risk scores based on age and comorbidity count
- get_patient_health_conditions: Retrieves health conditions for a specific patient ID
- get_patient_summary: Gets complete demographic and condition summary for a patient

Patient Database:
- P001: Alice Johnson (68 years old, 3 comorbidities)
- P002: Robert Smith (45 years old, 1 comorbidity)
- P003: Maria Lopez (72 years old, 5 comorbidities)

Instructions:
- Don't use markdown format in your response. Console friendly response only.
- Always use the appropriate tools to fetch real-time data
- Provide clear, concise responses
- When comparing patients or calculating risks, use the tools to get accurate data
- Present medical information in an understandable way";

// Test queries
var testQueries = new[]
{
    "Calculate the risk score for a 72-year-old patient with 5 comorbidities.",
    "What health conditions does patient P001 have?",
    "Give me a complete summary of patient P003.",
    "What is the risk score for patient P002? They are 45 years old with 1 comorbidity.",
    "Compare the health conditions of patients P001 and P002.",
    "Which patient has the highest risk: Alice Johnson (68 years, 3 comorbidities) or Maria Lopez (72 years, 5 comorbidities)?"
};

Console.WriteLine("\n\n🤖 Testing LLM with MCP tools:\n");
Console.WriteLine(new string('=', 70));

for (int i = 0; i < testQueries.Length; i++)
{
    var query = testQueries[i];
    Console.WriteLine($"\n📝 Test {i + 1}/{testQueries.Length}:");
    Console.WriteLine($"Query: \"{query}\"\n");

    var messages = new List<ChatMessage>
    {
        new ChatMessage(ChatRole.System, systemPrompt),
        new ChatMessage(ChatRole.User, query)
    };

    var options = new ChatOptions
    {
        Tools = aiFunctions.Cast<AITool>().ToList()
    };

    var response = await llm.GetResponseAsync(messages, options);

    // Check if there were function calls
    var lastMessage = response.Messages.LastOrDefault();
    if (lastMessage != null && lastMessage.Contents.Any(c => c is FunctionCallContent))
    {
        // Execute function calls
        var functionCalls = lastMessage.Contents.OfType<FunctionCallContent>().ToList();

        // Add the assistant's message with tool calls
        messages.Add(lastMessage);

        // Process all function calls and add tool responses
        foreach (var functionCall in functionCalls)
        {
            // Find and invoke the function
            var function = aiFunctions.FirstOrDefault(f => f.Name == functionCall.Name);
            if (function != null)
            {
                // Debug: Check what Arguments contains
                Console.WriteLine($"  🔍 Arguments type: {functionCall.Arguments?.GetType().FullName ?? "null"}");

                // Parse arguments - could be Dictionary, BinaryData, or other types
                Dictionary<string, object>? argumentsDict = null;

                if (functionCall.Arguments != null)
                {
                    // Try to cast to a dictionary first (most common case from OpenAI)
                    if (functionCall.Arguments is System.Collections.IDictionary dictObject)
                    {
                        argumentsDict = new Dictionary<string, object>();
                        foreach (System.Collections.DictionaryEntry entry in dictObject)
                        {
                            if (entry.Key != null && entry.Value != null)
                            {
                                argumentsDict[entry.Key.ToString() ?? ""] = entry.Value;
                            }
                        }
                        Console.WriteLine($"  🔍 Dictionary with {argumentsDict.Count} entries");
                    }
                    else if (functionCall.Arguments is BinaryData binaryData)
                    {
                        string argumentsJson = System.Text.Encoding.UTF8.GetString(binaryData.ToArray());
                        Console.WriteLine($"  🔍 BinaryData content: {argumentsJson}");
                        try
                        {
                            argumentsDict = JsonSerializer.Deserialize<Dictionary<string, object>>(argumentsJson);
                        }
                        catch (JsonException ex)
                        {
                            Console.WriteLine($"    ⚠️  JSON parse error: {ex.Message}");
                            argumentsDict = new Dictionary<string, object>();
                        }
                    }
                    else
                    {
                        string argumentsJson = functionCall.Arguments.ToString() ?? "{}";
                        Console.WriteLine($"  🔍 ToString content: {argumentsJson}");
                        try
                        {
                            argumentsDict = JsonSerializer.Deserialize<Dictionary<string, object>>(argumentsJson);
                        }
                        catch (JsonException ex)
                        {
                            Console.WriteLine($"    ⚠️  JSON parse error: {ex.Message}");
                            argumentsDict = new Dictionary<string, object>();
                        }
                    }
                }

                if (argumentsDict == null)
                {
                    argumentsDict = new Dictionary<string, object>();
                }

                // Debug: print what arguments we received
                Console.WriteLine($"  🔧 Calling {functionCall.Name} with args: {JsonSerializer.Serialize(argumentsDict)}");

                // The AIFunction we created expects individual parameters (age, comorbidities, patientId)
                // So we need to pass them as individual entries in AIFunctionArguments
                var arguments = new AIFunctionArguments();
                foreach (var kvp in argumentsDict)
                {
                    arguments[kvp.Key] = kvp.Value;
                }

                var result = await function.InvokeAsync(arguments);
                Console.WriteLine($"  📊 Result: {result}");

                // Create a FunctionResultContent with the call ID
                var toolResponse = new FunctionResultContent(functionCall.CallId, result?.ToString() ?? "");
                messages.Add(new ChatMessage(ChatRole.Tool, [toolResponse]));
            }
        }

        // Get final response from LLM after function execution
        var finalResponse = await llm.GetResponseAsync(messages, options);
        Console.WriteLine($"\n✨ Final LLM Response: {finalResponse.Text}");
    }
    else
    {
        Console.WriteLine($"\n✨ LLM Response: {response.Text}");
    }

    Console.WriteLine("\n" + new string('─', 70));

    // Small delay between requests
    if (i < testQueries.Length - 1)
    {
        await Task.Delay(500);
    }
}

Console.WriteLine("\n✅ All tests completed!");
