// client.js
import "dotenv/config.js";
import { ChatOpenAI } from "@langchain/openai";
import { DynamicStructuredTool } from "@langchain/core/tools";
import { z } from "zod";

// ---------- 1. Setup LLM ----------
const provider = process.env.MODEL_PROVIDER || "azure";

let llm;
if (provider === "azure") {
  llm = new ChatOpenAI({
    temperature: 0.2,
    model: process.env.AZURE_DEPLOYMENT_NAME || "gpt-4o",
    configuration: {
      apiKey: process.env.AZURE_API_KEY,
      baseURL: process.env.AZURE_ENDPOINT,
      deploymentName: process.env.AZURE_DEPLOYMENT_NAME,
    },
  });
  console.log("🔷 Using Azure OpenAI model:", process.env.AZURE_DEPLOYMENT_NAME);
} else {
  llm = new ChatOpenAI({
    model: "gpt-4o-mini",
    apiKey: process.env.OPENAI_API_KEY,
    temperature: 0.2,
  });
  console.log("🔶 Using OpenAI model: gpt-4o-mini");
}

// ---------- 2. Simple HTTP JSON-RPC MCP Client ----------
class SimpleHTTPMCPClient {
  constructor(url) {
    this.url = url;
    this.messageId = 0;
  }

  async callJsonRpc(method, params = {}) {
    const response = await fetch(this.url, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        jsonrpc: "2.0",
        id: ++this.messageId,
        method,
        params,
      }),
    });

    if (!response.ok) {
      throw new Error(`HTTP ${response.status}: ${await response.text()}`);
    }

    const data = await response.json();
    if (data.error) {
      throw new Error(`JSON-RPC Error: ${data.error.message}`);
    }

    return data.result;
  }

  async initialize() {
    const result = await this.callJsonRpc("initialize", {});
    return result;
  }

  async listTools() {
    const result = await this.callJsonRpc("tools/list", {});
    return result.tools;
  }

  async callTool(name, args) {
    const result = await this.callJsonRpc("tools/call", { name, arguments: args });
    return result.content;
  }
}

// ---------- 3. Main Function ----------
async function main() {
  const mcpUrl = process.env.MCP_SERVER_URL || "http://localhost:8080/mcp";
  const client = new SimpleHTTPMCPClient(mcpUrl);

  // Initialize connection
  await client.initialize();

  // List available tools
  const mcpTools = await client.listTools();
  console.log(`\n✅ Loaded ${mcpTools.length} tools from MCP server:`);
  mcpTools.forEach(t => console.log(`   • ${t.name} — ${t.description}`));

  // Convert MCP tools to LangChain tools
  const langchainTools = mcpTools.map(mcpTool => {
    // Convert MCP input schema to Zod schema
    const zodSchema = z.object(
      Object.fromEntries(
        Object.entries(mcpTool.inputSchema.properties || {}).map(([key, prop]) => [
          key,
          prop.type === "number" ? z.number() : z.string()
        ])
      )
    );

    return new DynamicStructuredTool({
      name: mcpTool.name,
      description: mcpTool.description,
      schema: zodSchema,
      func: async (input) => {
        const result = await client.callTool(mcpTool.name, input);
        return result[0]?.text || JSON.stringify(result);
      },
    });
  });

  // Bind tools to LLM
  const llmWithTools = llm.bindTools(langchainTools);

  // System prompt for the LLM
  const systemPrompt = `You are a helpful clinical assistant with access to patient data and risk assessment tools.

Available Tools:
- calculate_risk_score: Computes clinical risk scores based on age and comorbidity count
- get_patient_health_conditions: Retrieves health conditions for a specific patient ID
- get_patient_summary: Gets complete demographic and condition summary for a patient

Patient Database:
- P001: Alice Johnson (68 years old, 3 comorbidities)
- P002: Robert Smith (45 years old, 1 comorbidity)
- P003: Maria Lopez (72 years old, 5 comorbidities)

Instructions:
- don't use markdown format in your response. Console friendly response only.
- Always use the appropriate tools to fetch real-time data
- Provide clear, concise responses
- When comparing patients or calculating risks, use the tools to get accurate data
- Present medical information in an understandable way`;

  // ---------- 4. Test with multiple queries ----------
  const testQueries = [
    "Calculate the risk score for a 72-year-old patient with 5 comorbidities.",
    "What health conditions does patient P001 have?",
    "Give me a complete summary of patient P003.",
    "What is the risk score for patient P002? They are 45 years old with 1 comorbidity.",
    "Compare the health conditions of patients P001 and P002.",
    "Which patient has the highest risk: Alice Johnson (68 years, 3 comorbidities) or Maria Lopez (72 years, 5 comorbidities)?",
  ];

  console.log("\n\n🤖 Testing LLM with MCP tools:\n");
  console.log("=" .repeat(70));

  for (let i = 0; i < testQueries.length; i++) {
    const query = testQueries[i];
    console.log(`\n📝 Test ${i + 1}/${testQueries.length}:`);
    console.log(`Query: "${query}"\n`);

    const response = await llmWithTools.invoke([
      { role: "system", content: systemPrompt },
      { role: "user", content: query }
    ]);

    // console.log("📩 LLM Response:", response.content);

    // Check if LLM wants to call tools
    if (response.tool_calls && response.tool_calls.length > 0) {
      // console.log("\n🔧 LLM requested tool calls:");

      const toolResults = [];

      for (const toolCall of response.tool_calls) {
        // console.log(`\n   → Tool: ${toolCall.name}`);
        // console.log(`   → Args:`, JSON.stringify(toolCall.args, null, 2));

        // Execute the tool
        const tool = langchainTools.find(t => t.name === toolCall.name);
        if (tool) {
          const toolResult = await tool.invoke(toolCall.args);
          // console.log(`   → Result:`, toolResult);

          toolResults.push({
            role: "tool",
            content: toolResult,
            tool_call_id: toolCall.id
          });
        }
      }

      // Send all tool results back to LLM
      if (toolResults.length > 0) {
        const finalResponse = await llm.invoke([
          { role: "system", content: systemPrompt },
          { role: "user", content: query },
          { role: "assistant", content: response.content, tool_calls: response.tool_calls },
          ...toolResults
        ]);

        console.log("\n✨ Final LLM Response:", finalResponse.content);
      }
    }

    console.log("\n" + "─".repeat(70));

    // Small delay between requests
    if (i < testQueries.length - 1) {
      await new Promise(resolve => setTimeout(resolve, 500));
    }
  }

  console.log("\n✅ All tests completed!");
}

main().catch(err => {
  console.error("❌ Error running client:", err);
  process.exit(1);
});