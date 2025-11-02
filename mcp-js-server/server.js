// server.js
import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import { CallToolRequestSchema, ListToolsRequestSchema } from '@modelcontextprotocol/sdk/types.js';
import express from 'express';
import { createServer } from 'http';

// ============================================================================
// MOCK DATA
// ============================================================================
const PATIENTS = {
  "P001": { name: "Alice Johnson", age: 68, comorbidities: 3 },
  "P002": { name: "Robert Smith", age: 45, comorbidities: 1 },
  "P003": { name: "Maria Lopez", age: 72, comorbidities: 5 }
};

const HEALTH_CONDITIONS = {
  "P001": [
    { name: "Type 2 Diabetes", severity: "moderate", dateDiagnosed: "2019-05-12" },
    { name: "Hypertension", severity: "mild", dateDiagnosed: "2015-09-03" }
  ],
  "P002": [
    { name: "Asthma", severity: "mild", dateDiagnosed: "2002-11-22" }
  ],
  "P003": [
    { name: "Chronic Kidney Disease", severity: "severe", dateDiagnosed: "2018-04-01" },
    { name: "Coronary Artery Disease", severity: "severe", dateDiagnosed: "2020-10-10" }
  ]
};

// ============================================================================
// TOOL HANDLERS
// ============================================================================

function handleCalculateRiskScore(args) {
  const { age, comorbidityCount } = args;
  const score = age * 0.2 + comorbidityCount * 5;
  let category = "low";
  if (score > 30) category = "medium";
  if (score > 60) category = "high";

  return JSON.stringify({ score, category }, null, 2);
}

function handleGetPatientHealthConditions(args) {
  const { patientId } = args;
  const conditions = HEALTH_CONDITIONS[patientId] || [];
  if (conditions.length === 0) {
    return JSON.stringify({ patientId, message: "No conditions found." }, null, 2);
  }
  return JSON.stringify({ patientId, conditions }, null, 2);
}

function handleGetPatientSummary(args) {
  const { patientId } = args;
  const patient = PATIENTS[patientId];
  if (!patient) {
    return `No patient found with ID ${patientId}`;
  }
  const conditions = HEALTH_CONDITIONS[patientId] || [];
  return JSON.stringify({ ...patient, conditions }, null, 2);
}

// ============================================================================
// TOOL CONFIGURATION
// ============================================================================
const TOOLS_CONFIG = [
  {
    name: "calculate_risk_score",
    description: "Compute a clinical risk score from age and comorbidity count.",
    inputSchema: {
      type: "object",
      properties: {
        age: { type: "number", description: "Age of the patient" },
        comorbidityCount: { type: "number", description: "Number of chronic conditions" }
      },
      required: ["age", "comorbidityCount"]
    },
    handler: handleCalculateRiskScore
  },
  {
    name: "get_patient_health_conditions",
    description: "Retrieve known health conditions for a patient.",
    inputSchema: {
      type: "object",
      properties: {
        patientId: { type: "string", description: "Patient ID" }
      },
      required: ["patientId"]
    },
    handler: handleGetPatientHealthConditions
  },
  {
    name: "get_patient_summary",
    description: "Return demographic and condition summary for a patient.",
    inputSchema: {
      type: "object",
      properties: {
        patientId: { type: "string", description: "Patient ID" }
      },
      required: ["patientId"]
    },
    handler: handleGetPatientSummary
  }
];

// ============================================================================
// TOOL EXECUTION
// ============================================================================
function executeTool(name, args) {
  const tool = TOOLS_CONFIG.find(t => t.name === name);
  if (!tool) throw new Error(`Unknown tool: ${name}`);
  const result = tool.handler(args || {});
  return { type: "text", text: result };
}

// ============================================================================
// MCP SERVER SETUP
// ============================================================================
const server = new Server(
  {
    name: "risk-scorer-mcp",
    version: "1.0.0"
  },
  {
    capabilities: { tools: {} }
  }
);

// List tools
server.setRequestHandler(ListToolsRequestSchema, async () => {
  return {
    tools: TOOLS_CONFIG.map(({ name, description, inputSchema }) => ({
      name,
      description,
      inputSchema
    }))
  };
});

// Call tool
server.setRequestHandler(CallToolRequestSchema, async (request) => {
  const { name, arguments: args } = request.params;
  console.log(`\n🔧 Tool called: ${name}`);
  console.log(`📝 Parameters:`, JSON.stringify(args, null, 2));
  try {
    const result = executeTool(name, args);
    console.log(`✅ Tool executed successfully`);
    return { content: [result] };
  } catch (err) {
    console.log(`❌ Tool execution failed: ${err.message}`);
    return {
      content: [{ type: "text", text: `Error: ${err.message}` }],
      isError: true
    };
  }
});

// ============================================================================
// HTTP SERVER FOR RETELL / TESTING
// ============================================================================
async function runHttpServer() {
  const app = express();
  const PORT = process.env.PORT || 8080;
  app.use(express.json());

  app.use((req, res, next) => {
    res.setHeader("Access-Control-Allow-Origin", "*");
    res.setHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
    res.setHeader("Access-Control-Allow-Headers", "Content-Type");
    if (req.method === "OPTIONS") return res.sendStatus(200);
    next();
  });

  const toolsList = {
    tools: TOOLS_CONFIG.map(({ name, description, inputSchema }) => ({
      name,
      description,
      inputSchema
    }))
  };

  app.get("/health", (_, res) => {
    res.json({ status: "ok", name: "risk-scorer-mcp", version: "1.0.0" });
  });

  app.post("/mcp", async (req, res) => {
    const { id, method, params } = req.body;

    if (method === "initialize") {
      return res.json({
        jsonrpc: "2.0",
        id,
        result: {
          protocolVersion: "2024-11-05",
          serverInfo: { name: "risk-scorer-mcp", version: "1.0.0" },
          capabilities: { tools: {} }
        }
      });
    }

    if (method === "tools/list") {
      return res.json({ jsonrpc: "2.0", id, result: toolsList });
    }

    if (method === "tools/call") {
      try {
        const { name, arguments: args } = params;
        console.log(`\n🔧 Tool called: ${name}`);
        console.log(`📝 Parameters:`, JSON.stringify(args, null, 2));
        const result = executeTool(name, args);
        console.log(`✅ Tool executed successfully`);
        return res.json({ jsonrpc: "2.0", id, result: { content: [result] } });
      } catch (err) {
        console.log(`❌ Tool execution failed: ${err.message}`);
        return res.json({
          jsonrpc: "2.0",
          id,
          error: { code: -32603, message: err.message }
        });
      }
    }

    return res.status(400).json({ jsonrpc: "2.0", id, error: { code: -32601, message: "Method not found" } });
  });

  const httpServer = createServer(app);
  httpServer.listen(PORT, "0.0.0.0", () => {
    console.log("=".repeat(60));
    console.log("🚀 Risk Scorer MCP Server");
    console.log("=".repeat(60));
    console.log(`📡 HTTP: http://localhost:${PORT}/mcp`);
    console.log(`💚 Health: http://localhost:${PORT}/health`);
    console.log(`🧩 Tools: ${TOOLS_CONFIG.length} available`);
    console.log("=".repeat(60));
  });
}

// ============================================================================
// START SERVER
// ============================================================================
const args = process.argv.slice(2);
if (args.includes("--http") || process.env.HTTP_MODE === "true") {
  runHttpServer();
} else {
  const transport = new StdioServerTransport();
  server.connect(transport);
  console.error("🚀 Risk Scorer MCP Server running in STDIO mode");
  console.error("📝 Use --http flag for Retell or web client integration");
}