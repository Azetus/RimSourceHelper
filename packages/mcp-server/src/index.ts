import { Server } from "@modelcontextprotocol/sdk/server/index.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { CallToolRequestSchema, ListToolsRequestSchema } from "@modelcontextprotocol/sdk/types.js";
import { loadConfig } from "./config.js";
import { toolDefinitions, handleToolCall } from "./tools/index.js";

const config = loadConfig();

const server = new Server(
  { name: "rimsource-helper", version: "1.0.0" },
  { capabilities: { tools: {} } }
);

// 返回所有工具定义
server.setRequestHandler(ListToolsRequestSchema, async () => ({
  tools: toolDefinitions
}));

// 路由工具调用
server.setRequestHandler(CallToolRequestSchema, async (request) => {
  const { name, arguments: args } = request.params;
  return handleToolCall(name, args, config);
});

// 启动 stdio 传输
const transport = new StdioServerTransport();
await server.connect(transport);
