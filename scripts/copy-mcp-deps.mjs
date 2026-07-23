import { cpSync, mkdirSync } from "node:fs";

mkdirSync("release/mcp", {
    recursive: true
});

cpSync(
    "packages/mcp-server/node_modules",
    "release/mcp/node_modules",
    {
        recursive: true
    }
);

console.log("MCP dependencies copied.");
