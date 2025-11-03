# MCP Server & Client Example (JavaScript + .NET)

This repository demonstrates Model Context Protocol (MCP) implementations in both **JavaScript** and **.NET**, showcasing cross-platform interoperability. Both implementations provide identical clinical risk assessment tools and can communicate with each other seamlessly.

## Project Structure

```
mcp_poc/
├── mcp-js-server/         # JavaScript MCP Server (Express)
├── mcp-js-client/         # JavaScript MCP Client (LangChain + OpenAI)
├── mcp-dotnet-server/     # .NET MCP Server (ASP.NET Core) ✅
├── mcp-dotnet-client/     # .NET MCP Client (Microsoft.Extensions.AI) ✅
└── DOTNET_IMPLEMENTATION.md  # Detailed .NET implementation guide
```

## Features

### Implementations

Both JavaScript and .NET implementations provide:

**MCP Servers** (`mcp-js-server/` & `mcp-dotnet-server/`)
- ✅ **Clinical risk assessment tools** via MCP protocol
- ✅ **HTTP/JSON-RPC endpoint** for easy integration
- ✅ **Three identical tools:**
  - `calculate_risk_score` - Computes clinical risk scores from age and comorbidity count
  - `get_patient_health_conditions` - Retrieves health conditions for a patient
  - `get_patient_summary` - Returns complete patient demographic and condition summary

**MCP Clients** (`mcp-js-client/` & `mcp-dotnet-client/`)
- ✅ **LLM integration** with OpenAI/Azure OpenAI
- ✅ **Automatic tool discovery** from MCP server
- ✅ **LLM-powered tool calling** - AI decides when and how to use tools
- ✅ **Multiple test scenarios** demonstrating real-world usage

### Cross-Platform Interoperability

- 🔄 **JavaScript Client** ↔ **JavaScript Server** ✅
- 🔄 **JavaScript Client** ↔ **.NET Server** ✅
- 🔄 **.NET Client** ↔ **.NET Server** ✅
- 🔄 **.NET Client** ↔ **JavaScript Server** ✅

## Prerequisites

### For JavaScript Implementation
- Node.js v20+
- npm or yarn

### For .NET Implementation
- .NET 8.0 SDK or higher
- Visual Studio 2022, VS Code, or Rider (optional)

### For Both
- OpenAI API key or Azure OpenAI credentials

## Setup

### 1. Clone the repository

```bash
git clone git@github.com:kumarchitta/mcp_js_example.git
cd mcp_js_example
```

### 2. JavaScript Setup

#### Install Server Dependencies
```bash
cd mcp-js-server
npm install
```

#### Install Client Dependencies
```bash
cd ../mcp-js-client
npm install
```

#### Configure Environment Variables
Create a `.env` file in `mcp-js-client/`:

```env
# For OpenAI
OPENAI_API_KEY=your_openai_api_key
MODEL_PROVIDER=openai

# OR for Azure OpenAI
# MODEL_PROVIDER=azure
# AZURE_API_KEY=your_azure_api_key
# AZURE_ENDPOINT=https://your-resource.openai.azure.com
# AZURE_DEPLOYMENT_NAME=gpt-4o
```

### 3. .NET Setup

#### Restore Server Dependencies
```bash
cd mcp-dotnet-server/McpDotnetServer
dotnet restore
```

#### Restore Client Dependencies
```bash
cd ../../mcp-dotnet-client/McpDotnetClient
dotnet restore
```

#### Configure Settings
Edit `appsettings.json` in `mcp-dotnet-client/McpDotnetClient/`:

```json
{
  "MODEL_PROVIDER": "openai",
  "OPENAI_API_KEY": "your-openai-api-key",
  "MCP_SERVER_URL": "http://localhost:8080/mcp"
}
```

## Usage

### Option 1: JavaScript Stack

#### Start JavaScript MCP Server
```bash
cd mcp-js-server
npm run dev
```

#### Run JavaScript MCP Client
In a new terminal:
```bash
cd mcp-js-client
npm run dev
```

### Option 2: .NET Stack

#### Start .NET MCP Server
```bash
cd mcp-dotnet-server/McpDotnetServer
dotnet run
```

#### Run .NET MCP Client
In a new terminal:
```bash
cd mcp-dotnet-client/McpDotnetClient
dotnet run
```

### Option 3: Mixed Stack (Demonstrates Interoperability)

#### JavaScript Client → .NET Server
```bash
# Terminal 1: Start .NET Server
cd mcp-dotnet-server/McpDotnetServer
dotnet run

# Terminal 2: Run JavaScript Client
cd mcp-js-client
MCP_SERVER_URL=http://localhost:8080/mcp npm run dev
```

#### .NET Client → JavaScript Server
```bash
# Terminal 1: Start JavaScript Server
cd mcp-js-server
PORT=3000 npm start

# Terminal 2: Run .NET Client (update appsettings.json MCP_SERVER_URL to http://localhost:3000/mcp)
cd mcp-dotnet-client/McpDotnetClient
dotnet run
```

### What the Clients Do

Both clients will:
1. Connect to the MCP server
2. Load available tools
3. Run 6 test queries demonstrating LLM + MCP integration
4. Display results and tool calls

## Example Output

```
✅ Loaded 3 tools from MCP server:
   • calculate_risk_score — Compute a clinical risk score from age and comorbidity count.
   • get_patient_health_conditions — Retrieve known health conditions for a patient.
   • get_patient_summary — Return demographic and condition summary for a patient.

📝 Test 1/6:
Query: "Calculate the risk score for a 72-year-old patient with 5 comorbidities."

✨ Final LLM Response: The risk score for a 72-year-old patient with 5 comorbidities is 39.4, which falls into the "medium" risk category.
```

## Mock Data

The server includes mock patient data:
- **P001**: Alice Johnson (68 years, 3 comorbidities)
- **P002**: Robert Smith (45 years, 1 comorbidity)
- **P003**: Maria Lopez (72 years, 5 comorbidities)

## Direct API Testing

You can also test the server directly with curl:

```bash
# Query patient health conditions
curl -X POST http://localhost:8080/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": 1,
    "method": "tools/call",
    "params": {
      "name": "get_patient_health_conditions",
      "arguments": { "patientId": "P001" }
    }
  }'
```

## MCP Inspector
```bash
  npx @modelcontextprotocol/inspector
```

## Architecture

### JavaScript Stack
```
┌─────────────┐         ┌─────────────┐         ┌─────────────┐
│             │         │             │         │             │
│  OpenAI/    │◄────────│  MCP Client │◄────────│  MCP Server │
│  Azure      │  Tools  │  (LangChain)│  HTTP   │  (Express)  │
│             │         │             │         │             │
└─────────────┘         └─────────────┘         └─────────────┘
                             │                        │
                        Decides when              Executes
                        to call tools             clinical tools
```

### .NET Stack
```
┌─────────────┐         ┌─────────────┐         ┌─────────────┐
│             │         │             │         │             │
│  OpenAI/    │◄────────│  MCP Client │◄────────│  MCP Server │
│  Azure      │  Tools  │(MS.Ext.AI)  │HTTP/SSE │(ASP.NET Core│
│             │         │             │         │             │
└─────────────┘         └─────────────┘         └─────────────┘
                             │                        │
                        Decides when              Executes
                        to call tools             clinical tools
```

### Cross-Platform Interoperability ⭐
```
    JavaScript Client ─────┐
                           ├──► JavaScript Server
         .NET Client ─────┘         OR
                                .NET Server
```
Both implementations use the same MCP protocol, ensuring full interoperability!

## Technologies Used

### JavaScript Stack
- **@modelcontextprotocol/sdk** - MCP protocol implementation
- **@langchain/openai** - LangChain OpenAI integration
- **@langchain/core** - LangChain core tools
- **Express** - HTTP server
- **Zod** - Schema validation

### .NET Stack
- **ModelContextProtocol SDK** - Official C# MCP SDK (Microsoft/Anthropic collaboration)
- **ModelContextProtocol.AspNetCore** - ASP.NET Core integration
- **Microsoft.Extensions.AI** - AI abstraction layer
- **Azure.AI.OpenAI** - Azure OpenAI client
- **OpenAI SDK** - OpenAI client

## Key Differences

| Aspect | JavaScript | .NET |
|--------|-----------|------|
| **Server Framework** | Express.js | ASP.NET Core Minimal API |
| **Tool Definition** | Plain functions | Attribute-based (`[McpServerTool]`) |
| **Type Safety** | Runtime (JSDoc) | Compile-time (C# types) |
| **Response Format** | JSON | Server-Sent Events (SSE) |
| **Client Integration** | LangChain | Microsoft.Extensions.AI |
| **Deployment** | Node.js runtime | Native/Docker/Cloud |

## Benefits of Each Implementation

### JavaScript
- ✅ Rapid development and iteration
- ✅ Extensive npm ecosystem
- ✅ Easy to get started
- ✅ Large community and examples

### .NET
- ✅ Compile-time type safety
- ✅ Better performance (native compilation)
- ✅ Enterprise features (DI, logging, health checks)
- ✅ Strong tooling (Visual Studio, Rider)
- ✅ Easy containerization and cloud deployment

## Quick Testing

### Test JavaScript Server
```bash
cd mcp-js-server
npm start

# In another terminal
curl http://localhost:8080/health
```

### Test .NET Server
```bash
cd mcp-dotnet-server/McpDotnetServer
dotnet run

# In another terminal
curl http://localhost:8080/health
```

### Test MCP Protocol Directly
```bash
# Test tools/list endpoint
curl -X POST http://localhost:8080/mcp \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}'

# Test calculate_risk_score tool
curl -X POST http://localhost:8080/mcp \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc":"2.0",
    "id":2,
    "method":"tools/call",
    "params":{
      "name":"calculate_risk_score",
      "arguments":{"age":72,"comorbidityCount":5}
    }
  }'
```

## Documentation

- **Main Documentation**: This README
- **Detailed .NET Guide**: [DOTNET_IMPLEMENTATION.md](DOTNET_IMPLEMENTATION.md)
- **.NET Server**: [mcp-dotnet-server/README.md](mcp-dotnet-server/README.md)
- **.NET Client**: [mcp-dotnet-client/README.md](mcp-dotnet-client/README.md)

## Implementation Status

| Component | Language | Status | Notes |
|-----------|----------|--------|-------|
| MCP Server | JavaScript | ✅ Complete | Express.js, JSON responses |
| MCP Server | .NET | ✅ Complete | ASP.NET Core, SSE responses |
| MCP Client | JavaScript | ✅ Complete | LangChain integration |
| MCP Client | .NET | ✅ Complete | Microsoft.Extensions.AI integration |
| Interoperability | Both | ✅ Verified | All combinations tested |

## What Makes This Special

This repository demonstrates:

1. **Language Agnostic Protocol**: The same MCP protocol works across JavaScript and .NET
2. **Full Interoperability**: Any client can talk to any server, regardless of language
3. **Production Ready**: Both implementations use official SDKs and best practices
4. **Real-World Example**: Healthcare risk assessment tools with LLM integration
5. **Modern Tech**: Latest .NET 8, Node.js 20+, OpenAI GPT-4o

## Next Steps

- ✅ Explore the code in both implementations
- ✅ Run the examples and see MCP in action
- ✅ Try mixing JavaScript and .NET components
- 📚 Read [DOTNET_IMPLEMENTATION.md](DOTNET_IMPLEMENTATION.md) for detailed .NET guide
- 🔧 Adapt the tools for your own use case
- 🚀 Deploy to production (Docker, Azure, AWS, etc.)

## License

ISC

## Author

Aswani Kumar Chitta

![MCP Demo](image.png)